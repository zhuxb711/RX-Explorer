using Microsoft.Toolkit.Uwp.UI;
using RX_Explorer.Class;
using RX_Explorer.Dialog;
using SharedLibrary;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.System.UserProfile;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Navigation;

namespace RX_Explorer.View
{
    public sealed partial class PhotoViewer : Page
    {
        private int LastSelectIndex;
        private Point LastZoomCenter;
        private IDisposable MTPEndOfShare;
        private CancellationTokenSource Cancellation;
        private CancellationTokenSource SingleClickCancellation;
        private readonly ObservableCollection<PhotoDisplayItem> PhotoCollection;
        private readonly InterlockedNoReentryExecution RotationExecution = new InterlockedNoReentryExecution();

        public PhotoViewer()
        {
            InitializeComponent();
            PhotoCollection = new ObservableCollection<PhotoDisplayItem>();
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            if (e.NavigationMode == NavigationMode.New)
            {
                Cancellation = new CancellationTokenSource();

                if (e.Parameter is FileSystemStorageFile File)
                {
                    await InitializeAsync(File, Cancellation.Token);
                }
            }
            else if (PhotoGirdView.SelectedItem is PhotoDisplayItem Item)
            {
                await Task.WhenAll(Item.GenerateActualSourceAsync(true), Item.GenerateThumbnailAsync(true));
            }
        }

        private async Task InitializeAsync(FileSystemStorageFile File, CancellationToken CancelToken)
        {
            TabViewContainer.Current.CurrentTabRenderer?.SetLoadingTipsStatus(true);

            try
            {
                if (File.Type.Equals(".sle", StringComparison.OrdinalIgnoreCase))
                {
                    using (Stream Stream = await File.GetStreamFromFileAsync(AccessMode.Read))
                    using (SLEInputStream SLEStream = new SLEInputStream(Stream, new UTF8Encoding(false), SecureArea.EncryptionKey))
                    {
                        CancelToken.ThrowIfCancellationRequested();

                        if (SLEStream.Header.Core.Version >= SLEVersion.SLE150)
                        {
                            Adjust.IsEnabled = false;
                            SetAsWallpaper.IsEnabled = false;

                            PhotoCollection.Add(new PhotoDisplayItem(await Helper.CreateBitmapImageAsync(SLEStream.AsRandomAccessStream())));

                            await Task.Delay(500);

                            PhotoGirdView.SelectionChanged += PhotoGirdView_SelectionChanged;
                            PhotoGirdView.SelectedIndex = -1;
                            PhotoGirdView.SelectedIndex = 0;
                        }
                        else
                        {
                            throw new NotSupportedException();
                        }
                    }
                }
                else if (Regex.IsMatch(File.Type, @"\.(png|bmp|jpg|jpeg)$", RegexOptions.IgnoreCase))
                {
                    Adjust.IsEnabled = true;
                    SetAsWallpaper.IsEnabled = true;

                    if (await FileSystemStorageItemBase.OpenAsync(Path.GetDirectoryName(File.Path)) is FileSystemStorageFolder BaseFolder)
                    {
                        IReadOnlyList<FileSystemStorageFile> SearchResult = await BaseFolder.GetChildItemsAsync(SettingPage.IsDisplayHiddenItemsEnabled, SettingPage.IsDisplayProtectedSystemItemsEnabled, Filter: BasicFilters.File, CancelToken: CancelToken, AdvanceFilter: (Name) => Regex.IsMatch(Path.GetExtension(Name), @"\.(png|bmp|jpg|jpeg)$", RegexOptions.IgnoreCase)).Cast<FileSystemStorageFile>().ToListAsync();

                        if (SearchResult.Count > 0)
                        {
                            if (BaseFolder is MTPStorageFolder MTPFolder)
                            {
                                MTPEndOfShare = await FileSystemStorageItemBase.SelfCreateBulkAccessSharedControllerAsync(SearchResult);
                            }

                            int SelectedIndex = 0;

                            PathConfiguration Config = SQLite.Current.GetPathConfiguration(BaseFolder.Path);

                            IEnumerable<FileSystemStorageFile> SortedResult = await SortCollectionGenerator.GetSortedCollectionAsync(SearchResult, Config.SortTarget.GetValueOrDefault(), Config.SortDirection.GetValueOrDefault());

                            PhotoCollection.AddRange(SortedResult.Select((Item, Index) =>
                            {
                                if (Item == File)
                                {
                                    SelectedIndex = Index;
                                }

                                return new PhotoDisplayItem(Item);
                            }));

                            await Task.Delay(500);

                            PhotoGirdView.SelectionChanged += PhotoGirdView_SelectionChanged;
                            PhotoGirdView.SelectedIndex = -1;
                            PhotoGirdView.SelectedIndex = SelectedIndex;

                            if (PhotoGridViewBorder.Opacity == 0)
                            {
                                GridViewEnterAnimation.Begin();
                            }
                        }
                        else
                        {
                            await new QueueContentDialog
                            {
                                Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                Content = Globalization.GetString("Queue_Dialog_ImageReadError_Content"),
                                CloseButtonText = Globalization.GetString("Common_Dialog_GoBack")
                            }.ShowAsync();

                            if (Frame.CanGoBack)
                            {
                                Frame.GoBack();
                            }
                        }
                    }
                    else
                    {
                        throw new FileNotFoundException();
                    }
                }
            }
            catch (OperationCanceledException)
            {
                //No need to handle this exception
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, "An exception was threw when initialize PhotoViewer");

                await new QueueContentDialog
                {
                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                    Content = Globalization.GetString("QueueDialog_CouldNotOpenImage_Content"),
                    CloseButtonText = Globalization.GetString("Common_Dialog_GoBack")
                }.ShowAsync();

                if (Frame.CanGoBack)
                {
                    Frame.GoBack();
                }
            }
            finally
            {
                TabViewContainer.Current.CurrentTabRenderer?.SetLoadingTipsStatus(false);
            }
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            if (e.NavigationMode == NavigationMode.Back)
            {
                Cancellation?.Cancel();
                Cancellation?.Dispose();
                SingleClickCancellation?.Cancel();
                SingleClickCancellation?.Dispose();

                PhotoCollection.Clear();
                MTPEndOfShare?.Dispose();
                MTPEndOfShare = null;
                Cancellation = null;
                SingleClickCancellation = null;

                PhotoGirdView.SelectionChanged -= PhotoGirdView_SelectionChanged;

                TabViewContainer.Current.CurrentTabRenderer?.SetLoadingTipsStatus(false);
            }
        }

        private async void ImageRotate_Click(object sender, RoutedEventArgs e)
        {
            if (PhotoGirdView.SelectedItem is PhotoDisplayItem Item)
            {
                try
                {
                    await RotationExecution.ExecuteAsync(async () =>
                    {
                        if (PhotoFlip.ContainerFromItem(Item)?.FindChildOfType<Image>() is Image ImageControl)
                        {
                            LastZoomCenter = ZoomTransform(ImageControl, new Point(ImageControl.ActualWidth / 2, ImageControl.ActualHeight / 2), 1);
                        }

                        using (Stream FileStream = await Item.PhotoFile.GetStreamFromFileAsync(AccessMode.Exclusive))
                        using (InMemoryRandomAccessStream MemoryStream = new InMemoryRandomAccessStream())
                        {
                            BitmapDecoder Decoder = await BitmapDecoder.CreateAsync(FileStream.AsRandomAccessStream());
                            BitmapEncoder Encoder = await BitmapEncoder.CreateForTranscodingAsync(MemoryStream, Decoder);
                            Encoder.BitmapTransform.Rotation = BitmapRotation.Clockwise90Degrees;

                            await Encoder.FlushAsync();

                            MemoryStream.Seek(0);
                            FileStream.SetLength(0);

                            await MemoryStream.AsStreamForRead().CopyToAsync(FileStream);
                        }

                        await Task.WhenAll(Item.GenerateActualSourceAsync(true), Item.GenerateThumbnailAsync(true));
                    });
                }
                catch (Exception ex)
                {
                    LogTracer.Log(ex, "Could not rotate the image");

                    QueueContentDialog Dialog = new QueueContentDialog
                    {
                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                        Content = Globalization.GetString("QueueDialog_RotationFailed_Content"),
                        CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                    };

                    await Dialog.ShowAsync();
                }
            }
        }

        private async void TranscodeImage_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (PhotoGirdView.SelectedItem is PhotoDisplayItem Item)
                {
                    if (PhotoFlip.ContainerFromItem(Item)?.FindChildOfType<Image>() is Image ImageControl)
                    {
                        LastZoomCenter = ZoomTransform(ImageControl, new Point(ImageControl.ActualWidth / 2, ImageControl.ActualHeight / 2), 1);
                    }

                    BitmapDecoder Decoder = null;

                    using (Stream OriginStream = await Item.PhotoFile.GetStreamFromFileAsync(AccessMode.Read))
                    {
                        Decoder = await BitmapDecoder.CreateAsync(OriginStream.AsRandomAccessStream());
                    }

                    TranscodeImageDialog Dialog = new TranscodeImageDialog(Decoder.PixelWidth, Decoder.PixelHeight);

                    if (await Dialog.ShowAsync() == ContentDialogResult.Primary)
                    {
                        TranscodeLoadingControl.IsLoading = true;

                        try
                        {
                            using (Stream SourceStream = await Item.PhotoFile.GetStreamFromFileAsync(AccessMode.Read))
                            using (IRandomAccessStream ResultStream = await GeneralTransformer.TranscodeFromImageAsync(SourceStream.AsRandomAccessStream(), Dialog.TargetFile.Type, Dialog.IsEnableScale, Dialog.ScaleWidth, Dialog.ScaleHeight, Dialog.InterpolationMode))
                            using (Stream TargetStream = await Dialog.TargetFile.GetStreamFromFileAsync(AccessMode.Write))
                            {
                                await ResultStream.AsStreamForRead().CopyToAsync(TargetStream);
                            }
                        }
                        finally
                        {
                            TranscodeLoadingControl.IsLoading = false;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, "Could not transcode the image");

                QueueContentDialog Dialog = new QueueContentDialog
                {
                    Title = Globalization.GetString("Common_Dialog_TipTitle"),
                    Content = Globalization.GetString("QueueDialog_TransocdeFailed_Content"),
                    CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                };

                await Dialog.ShowAsync();
            }
        }

        private async void Delete_Click(object sender, RoutedEventArgs e)
        {
            if (PhotoGirdView.SelectedItem is PhotoDisplayItem Item)
            {
                if (PhotoFlip.ContainerFromItem(Item)?.FindChildOfType<Image>() is Image ImageControl)
                {
                    LastZoomCenter = ZoomTransform(ImageControl, new Point(ImageControl.ActualWidth / 2, ImageControl.ActualHeight / 2), 1);
                }

                try
                {
                    QueueContentDialog Dialog = new QueueContentDialog
                    {
                        Title = Globalization.GetString("Common_Dialog_WarningTitle"),
                        PrimaryButtonText = Globalization.GetString("Common_Dialog_ContinueButton"),
                        CloseButtonText = Globalization.GetString("Common_Dialog_CancelButton"),
                        Content = Globalization.GetString("QueueDialog_DeleteFiles_Content")
                    };

                    if (await Dialog.ShowAsync() == ContentDialogResult.Primary)
                    {
                        await Item.PhotoFile.DeleteAsync(false);

                        int Index = PhotoCollection.IndexOf(Item);

                        if (Index >= 0 && Index < PhotoCollection.Count)
                        {
                            PhotoCollection.RemoveAt(Index);

                            if (Index < PhotoCollection.Count)
                            {
                                PhotoGirdView.SelectedIndex = Index;
                            }
                            else
                            {
                                PhotoGirdView.SelectedIndex = Index - 1;
                            }
                        }
                    }
                }
                catch (Exception)
                {
                    QueueContentDialog Dialog = new QueueContentDialog
                    {
                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                        Content = Globalization.GetString("QueueDialog_DeleteItemError_Content"),
                        CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                    };

                    await Dialog.ShowAsync();
                }
            }
        }

        private async void Adjust_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (PhotoGirdView.SelectedItem is PhotoDisplayItem Item)
                {
                    if (PhotoFlip.ContainerFromItem(Item)?.FindChildOfType<Image>() is Image ImageControl)
                    {
                        LastZoomCenter = ZoomTransform(ImageControl, new Point(ImageControl.ActualWidth / 2, ImageControl.ActualHeight / 2), 1);
                    }

                    using (Stream FStream = await Item.PhotoFile.GetStreamFromFileAsync(AccessMode.Read))
                    {
                        BitmapDecoder Decoder = await BitmapDecoder.CreateAsync(FStream.AsRandomAccessStream());

                        if (Decoder.PixelHeight <= 50 || Decoder.PixelWidth <= 50)
                        {
                            await new QueueContentDialog
                            {
                                Title = Globalization.GetString("Common_Dialog_WarningTitle"),
                                Content = Globalization.GetString("QueueDialog_CanNotAdjustSmallImage_Content"),
                                CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                            }.ShowAsync();

                            return;
                        }
                    }

                    if (AnimationController.Current.IsEnableAnimation)
                    {
                        Frame.Navigate(typeof(CropperPage), Item, new DrillInNavigationTransitionInfo());
                    }
                    else
                    {
                        Frame.Navigate(typeof(CropperPage), Item, new SuppressNavigationTransitionInfo());
                    }
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"An exception was threw when navigating to {nameof(CropperPage)}");
            }
        }

        private async void SetAsWallpaper_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (UserProfilePersonalizationSettings.IsSupported())
                {
                    if (PhotoGirdView.SelectedItem is PhotoDisplayItem Item)
                    {
                        if (PhotoFlip.ContainerFromItem(Item)?.FindChildOfType<Image>() is Image ImageControl)
                        {
                            LastZoomCenter = ZoomTransform(ImageControl, new Point(ImageControl.ActualWidth / 2, ImageControl.ActualHeight / 2), 1);
                        }

                        using (Stream PhotoStream = await Item.PhotoFile.GetStreamFromFileAsync(AccessMode.Read, OptimizeOption.Sequential))
                        {
                            string TempFilePath = Path.Combine(ApplicationData.Current.TemporaryFolder.Path, $"{Guid.NewGuid():N}{Item.PhotoFile.Type.ToLower()}");

                            using (Stream TempStream = await FileSystemStorageItemBase.CreateTemporaryFileStreamAsync(TempFilePath, IOPreference.PreferUseMoreMemory))
                            {
                                await PhotoStream.CopyToAsync(TempStream);

                                if (await UserProfilePersonalizationSettings.Current.TrySetWallpaperImageAsync(await StorageFile.GetFileFromPathAsync(TempFilePath)))
                                {
                                    QueueContentDialog Dialog = new QueueContentDialog
                                    {
                                        Title = Globalization.GetString("Common_Dialog_TipTitle"),
                                        Content = Globalization.GetString("QueueDialog_SetWallpaperSuccess_Content"),
                                        CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                                    };

                                    await Dialog.ShowAsync();
                                }
                                else
                                {
                                    PhotoStream.Seek(0, SeekOrigin.Begin);

                                    if (await FileSystemStorageItemBase.CreateNewAsync(Path.Combine(ApplicationData.Current.TemporaryFolder.Path, $"{Guid.NewGuid():N}{Item.PhotoFile.Type.ToLower()}"), CreateType.File, CreateOption.GenerateUniqueName) is FileSystemStorageFile TempFile)
                                    {
                                        try
                                        {
                                            using (Stream TempFileStream = await TempFile.GetStreamFromFileAsync(AccessMode.Write))
                                            {
                                                await PhotoStream.CopyToAsync(TempFileStream);
                                            }

                                            using (AuxiliaryTrustProcessController.Exclusive Exclusive = await AuxiliaryTrustProcessController.GetControllerExclusiveAsync())
                                            {
                                                if (!await Exclusive.Controller.SetWallpaperImageAsync(TempFile.Path))
                                                {
                                                    QueueContentDialog Dialog = new QueueContentDialog
                                                    {
                                                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                                        Content = Globalization.GetString("QueueDialog_SetWallpaperFailure_Content"),
                                                        CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                                                    };

                                                    await Dialog.ShowAsync();
                                                }
                                            }
                                        }
                                        finally
                                        {
                                            await TempFile.DeleteAsync(true);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                else
                {
                    QueueContentDialog Dialog = new QueueContentDialog
                    {
                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                        Content = Globalization.GetString("QueueDialog_SetWallpaperNotSupport_Content"),
                        CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                    };

                    await Dialog.ShowAsync();
                }
            }
            catch
            {
                QueueContentDialog Dialog = new QueueContentDialog
                {
                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                    Content = Globalization.GetString("QueueDialog_SetWallpaperFailure_Content"),
                    CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                };

                await Dialog.ShowAsync();
            }
        }

        private async void PhotoGirdView_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (!args.InRecycleQueue)
            {
                if (args.Item is PhotoDisplayItem Item && !(Cancellation?.IsCancellationRequested).GetValueOrDefault(true))
                {
                    await Item.GenerateThumbnailAsync();
                }
            }
        }

        private async void PhotoGirdView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            foreach (PhotoDisplayItem Item in e.AddedItems)
            {
                try
                {
                    int LastIndex = Interlocked.Exchange(ref LastSelectIndex, PhotoCollection.IndexOf(Item));

                    if (LastIndex >= 0 && LastIndex < PhotoCollection.Count)
                    {
                        if (PhotoFlip.ContainerFromIndex(LastIndex)?.FindChildOfType<Image>() is Image ImageControl)
                        {
                            LastZoomCenter = ZoomTransform(ImageControl, new Point(ImageControl.ActualWidth / 2, ImageControl.ActualHeight / 2), 1);
                        }
                    }

                    await Item.GenerateActualSourceAsync();

                    if (PhotoGirdView.IsLoaded)
                    {
                        await PhotoGirdView.SmoothScrollIntoViewWithItemAsync(Item, ScrollItemPlacement.Center);
                    }
                    else
                    {
                        PhotoGirdView.ScrollIntoView(Item, ScrollIntoViewAlignment.Leading);
                    }
                }
                catch (Exception ex)
                {
                    LogTracer.Log(ex, "Could not load the image on selection changed");
                }
            }
        }

        private void Image_ManipulationDelta(object sender, ManipulationDeltaRoutedEventArgs e)
        {
            if (sender is Image ImageControl)
            {
                Point LeftTopPoint = ImageControl.TransformToVisual(PhotoFlip).TransformPoint(new Point(0, 0));

                if (ImageControl.RenderTransform is CompositeTransform ScaleTransform)
                {
                    if (e.Delta.Translation.X > 0)
                    {
                        if (LeftTopPoint.X < 0)
                        {
                            ScaleTransform.TranslateX += Math.Min(Math.Abs(LeftTopPoint.X), e.Delta.Translation.X);
                        }
                    }
                    else
                    {
                        if (LeftTopPoint.X > PhotoFlip.ActualWidth - ImageControl.ActualWidth * ScaleTransform.ScaleX)
                        {
                            ScaleTransform.TranslateX += Math.Max(PhotoFlip.ActualWidth - LeftTopPoint.X - ImageControl.ActualWidth * ScaleTransform.ScaleX, e.Delta.Translation.X);
                        }
                    }

                    if (e.Delta.Translation.Y > 0)
                    {
                        if (LeftTopPoint.Y < 0)
                        {
                            ScaleTransform.TranslateY += Math.Min(Math.Abs(LeftTopPoint.Y), e.Delta.Translation.Y);
                        }
                    }
                    else
                    {
                        if (LeftTopPoint.Y + ImageControl.ActualHeight * ScaleTransform.ScaleY > PhotoFlip.ActualHeight)
                        {
                            ScaleTransform.TranslateY += Math.Max(PhotoFlip.ActualHeight - LeftTopPoint.Y - ImageControl.ActualHeight * ScaleTransform.ScaleY, e.Delta.Translation.Y);
                        }
                    }
                }
            }
        }

        private void Image_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
        {
            SingleClickCancellation?.Cancel();

            if (sender is Image ImageControl)
            {
                ZoomSlider.ValueChanged -= ZoomSlider_ValueChanged;

                if (ZoomSlider.Value == 1)
                {
                    if (PhotoGridViewBorder.Opacity == 1)
                    {
                        GridViewExitAnimation.Begin();
                    }

                    LastZoomCenter = ZoomTransform(ImageControl, e.GetPosition(ImageControl), 2);
                }
                else
                {
                    if (PhotoGridViewBorder.Opacity == 0)
                    {
                        GridViewEnterAnimation.Begin();
                    }

                    LastZoomCenter = ZoomTransform(ImageControl, e.GetPosition(ImageControl), 1);
                }

                ZoomSlider.ValueChanged += ZoomSlider_ValueChanged;
            }
        }

        private void Image_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            SingleClickCancellation?.Cancel();
            SingleClickCancellation?.Dispose();
            SingleClickCancellation = new CancellationTokenSource();

            Task.Delay(300).ContinueWith((_, Input) =>
            {
                if (Input is (CancellationToken CancelToken, Point PressPoint))
                {
                    if (!CancelToken.IsCancellationRequested)
                    {
                        if (PhotoGridViewBorder.Opacity == 0)
                        {
                            Point GlobalPoint = Window.Current.CoreWindow.PointerPosition;
                            Point CurrentPoint = new Point(GlobalPoint.X - Window.Current.Bounds.X, GlobalPoint.Y - Window.Current.Bounds.Y);

                            if (Math.Abs(CurrentPoint.X - PressPoint.X) < 15 && Math.Abs(CurrentPoint.Y - PressPoint.Y) < 15)
                            {
                                GridViewEnterAnimation.Begin();
                            }
                        }
                        else
                        {
                            GridViewExitAnimation.Begin();
                        }
                    }
                }
            }, (SingleClickCancellation.Token, e.GetCurrentPoint(null).Position), TaskScheduler.FromCurrentSynchronizationContext());

            if (ZoomSlider.Value > 1)
            {
                Window.Current.CoreWindow.PointerCursor = new CoreCursor(CoreCursorType.SizeAll, 0);
            }
        }

        private void Image_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            Window.Current.CoreWindow.PointerCursor = new CoreCursor(CoreCursorType.Arrow, 0);
        }

        private void ZoomSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            if (PhotoGirdView?.SelectedItem is PhotoDisplayItem Item)
            {
                if (PhotoFlip.ContainerFromItem(Item).FindChildOfType<Image>() is Image ImageControl)
                {
                    if (e.NewValue == 1)
                    {
                        LastZoomCenter = ZoomTransform(ImageControl, new Point(ImageControl.ActualWidth / 2, ImageControl.ActualHeight / 2), 1);
                    }
                    else
                    {
                        if (LastZoomCenter == default)
                        {
                            LastZoomCenter = new Point(ImageControl.ActualWidth / 2, ImageControl.ActualHeight / 2);
                        }

                        LastZoomCenter = ZoomTransform(ImageControl, LastZoomCenter, e.NewValue);
                    }
                }
            }
        }

        private Point ZoomTransform(FrameworkElement Element, Point CenterPoint, double ZoomFactor)
        {
            ZoomSlider.Value = ZoomFactor;

            if (Element.RenderTransform is CompositeTransform ScaleTransform)
            {
                Storyboard Board = new Storyboard();

                DoubleAnimation ScaleXAnimation;
                DoubleAnimation ScaleYAnimation;

                if (ZoomFactor == 1)
                {
                    ScaleXAnimation = new DoubleAnimation
                    {
                        From = ScaleTransform.ScaleX,
                        To = 1,
                        Duration = TimeSpan.FromMilliseconds(300),
                        EnableDependentAnimation = true
                    };

                    ScaleYAnimation = new DoubleAnimation
                    {
                        From = ScaleTransform.ScaleY,
                        To = 1,
                        Duration = TimeSpan.FromMilliseconds(300),
                        EnableDependentAnimation = true
                    };
                }
                else
                {
                    Point RelatedPoint = Element.TransformToVisual(PhotoFlip).TransformPoint(CenterPoint);

                    double EmptyXWidth = RelatedPoint.X - CenterPoint.X;
                    double EmptyYWidth = RelatedPoint.Y - CenterPoint.Y;

                    if ((2 / (ZoomFactor - 1) + 2) * EmptyXWidth > PhotoFlip.ActualWidth)
                    {
                        ScaleTransform.CenterX = Element.ActualWidth / 2;
                    }
                    else
                    {
                        ScaleTransform.CenterX = Math.Min(Math.Max(EmptyXWidth / (ZoomFactor - 1), CenterPoint.X), PhotoFlip.ActualWidth - (1 / (ZoomFactor - 1) + 2) * EmptyXWidth);
                    }

                    if ((2 / (ZoomFactor - 1) + 2) * EmptyYWidth > PhotoFlip.ActualHeight)
                    {
                        ScaleTransform.CenterY = Element.ActualHeight / 2;
                    }
                    else
                    {
                        ScaleTransform.CenterY = Math.Min(Math.Max(EmptyYWidth / (ZoomFactor - 1), CenterPoint.Y), PhotoFlip.ActualHeight - (1 / (ZoomFactor - 1) + 2) * EmptyYWidth);
                    }

                    ScaleXAnimation = new DoubleAnimation
                    {
                        From = ScaleTransform.ScaleX,
                        To = ZoomFactor,
                        Duration = TimeSpan.FromMilliseconds(300),
                        EnableDependentAnimation = true
                    };

                    ScaleYAnimation = new DoubleAnimation
                    {
                        From = ScaleTransform.ScaleY,
                        To = ZoomFactor,
                        Duration = TimeSpan.FromMilliseconds(300),
                        EnableDependentAnimation = true
                    };
                }

                DoubleAnimation TransformXAnimation = new DoubleAnimation
                {
                    From = ScaleTransform.TranslateX,
                    To = 0,
                    Duration = TimeSpan.FromMilliseconds(300),
                    EnableDependentAnimation = true
                };

                DoubleAnimation TransformYAnimation = new DoubleAnimation
                {
                    From = ScaleTransform.TranslateY,
                    To = 0,
                    Duration = TimeSpan.FromMilliseconds(300),
                    EnableDependentAnimation = true
                };

                Storyboard.SetTarget(TransformXAnimation, Element);
                Storyboard.SetTargetProperty(TransformXAnimation, "(UIElement.RenderTransform).(CompositeTransform.TranslateX)");

                Storyboard.SetTarget(TransformYAnimation, Element);
                Storyboard.SetTargetProperty(TransformYAnimation, "(UIElement.RenderTransform).(CompositeTransform.TranslateY)");

                Storyboard.SetTarget(ScaleXAnimation, Element);
                Storyboard.SetTargetProperty(ScaleXAnimation, "(UIElement.RenderTransform).(CompositeTransform.ScaleX)");

                Storyboard.SetTarget(ScaleYAnimation, Element);
                Storyboard.SetTargetProperty(ScaleYAnimation, "(UIElement.RenderTransform).(CompositeTransform.ScaleY)");

                Board.Children.Add(ScaleXAnimation);
                Board.Children.Add(ScaleYAnimation);
                Board.Children.Add(TransformXAnimation);
                Board.Children.Add(TransformYAnimation);

                Board.Begin();

                return new Point(ScaleTransform.CenterX, ScaleTransform.CenterY);
            }

            return new Point(Element.ActualWidth / 2, Element.ActualHeight / 2);
        }
    }
}
