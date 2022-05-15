using Microsoft.Toolkit.Uwp.UI;
using Microsoft.Toolkit.Uwp.UI.Animations;
using RX_Explorer.Class;
using RX_Explorer.Dialog;
using ShareClassLibrary;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.System.UserProfile;
using Windows.UI.Core;
using Windows.UI.Input;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;

namespace RX_Explorer.View
{
    public sealed partial class PhotoViewer : Page
    {
        private readonly ObservableCollection<PhotoDisplayItem> PhotoCollection = new ObservableCollection<PhotoDisplayItem>();

        private int LastSelectIndex;
        private double OriginHorizonOffset;
        private double OriginVerticalOffset;
        private Point OriginMousePosition;
        private EndUsageNotification MTPEndOfShare;
        private CancellationTokenSource Cancellation;

        public PhotoViewer()
        {
            InitializeComponent();
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
        }

        private async Task InitializeAsync(FileSystemStorageFile File, CancellationToken CancelToken)
        {
            TabViewContainer.Current.CurrentTabRenderer?.SetLoadingTipsStatus(true);

            try
            {
                if (File.Type.Equals(".sle", StringComparison.OrdinalIgnoreCase))
                {
                    using (Stream Stream = await File.GetStreamFromFileAsync(AccessMode.Read, OptimizeOption.RandomAccess))
                    {
                        if (!CancelToken.IsCancellationRequested)
                        {
                            try
                            {
                                SLEHeader Header = SLEHeader.GetHeader(Stream);

                                if (Header.Version >= SLEVersion.Version_1_5_0)
                                {
                                    Adjust.IsEnabled = false;
                                    SetAsWallpaper.IsEnabled = false;

                                    using (IRandomAccessStream RandomStream = new SLEInputStream(Stream, SecureArea.AESKey).AsRandomAccessStream())
                                    {
                                        BitmapImage Image = new BitmapImage();
                                        await Image.SetSourceAsync(RandomStream);

                                        PhotoCollection.Add(new PhotoDisplayItem(Image));
                                    }

                                    await Task.Delay(1000);
                                }
                                else
                                {
                                    throw new NotSupportedException();
                                }
                            }
                            finally
                            {
                                TabViewContainer.Current.CurrentTabRenderer?.SetLoadingTipsStatus(false);
                            }
                        }
                    }
                }
                else if (Regex.IsMatch(File.Type, @"\.(png|bmp|jpg|jpeg)$", RegexOptions.IgnoreCase))
                {
                    Adjust.IsEnabled = true;
                    SetAsWallpaper.IsEnabled = true;

                    if (await FileSystemStorageItemBase.OpenAsync(Path.GetDirectoryName(File.Path)) is FileSystemStorageFolder Item)
                    {
                        try
                        {
                            IReadOnlyList<FileSystemStorageFile> SearchResult = await Item.GetChildItemsAsync(SettingPage.IsShowHiddenFilesEnabled, SettingPage.IsDisplayProtectedSystemItems, Filter: BasicFilters.File, CancelToken: CancelToken, AdvanceFilter: (Name) => Regex.IsMatch(Path.GetExtension(Name), @"\.(png|bmp|jpg|jpeg)$", RegexOptions.IgnoreCase)).Cast<FileSystemStorageFile>().ToListAsync();

                            if (SearchResult.Count > 0)
                            {
                                if (Item is MTPStorageFolder MTPFolder)
                                {
                                    MTPEndOfShare = await FileSystemStorageItemBase.SetBulkAccessSharedControllerAsync(SearchResult);
                                }

                                PathConfiguration Config = SQLite.Current.GetPathConfiguration(Path.GetDirectoryName(File.Path));

                                PhotoCollection.AddRange((await SortCollectionGenerator.GetSortedCollectionAsync(SearchResult, Config.SortTarget.GetValueOrDefault(), Config.SortDirection.GetValueOrDefault())).Select((Item) => new PhotoDisplayItem(Item)));
                                PhotoFlip.SelectedItem = PhotoCollection.FirstOrDefault((Item) => Item.PhotoFile == File);
                                Pips.NumberOfPages = PhotoCollection.Count;

                                if (PhotoFlip.SelectedIndex == 0)
                                {
                                    await PhotoCollection[0].GenerateActualSourceAsync();
                                }

                                await Task.Delay(1000);
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
                        catch (OperationCanceledException)
                        {
                            //No need to handle this exception
                        }
                        finally
                        {
                            TabViewContainer.Current.CurrentTabRenderer?.SetLoadingTipsStatus(false);
                        }
                    }
                    else
                    {
                        throw new FileNotFoundException();
                    }
                }

                await Task.Delay(1000);
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
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            if (e.NavigationMode == NavigationMode.Back)
            {
                Cancellation?.Cancel();
                Cancellation?.Dispose();
                PhotoCollection.Clear();
                MTPEndOfShare?.Dispose();
                MTPEndOfShare = null;
                Cancellation = null;

                TabViewContainer.Current.CurrentTabRenderer?.SetLoadingTipsStatus(false);
            }
        }

        private async void PhotoFlip_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            foreach (PhotoDisplayItem Item in e.AddedItems)
            {
                int CurrentIndex = PhotoCollection.IndexOf(Item);

                if (CurrentIndex >= 0 && CurrentIndex < PhotoCollection.Count)
                {
                    try
                    {
                        int LastIndex = Interlocked.Exchange(ref LastSelectIndex, CurrentIndex);

                        if (LastIndex >= 0 && LastIndex < PhotoCollection.Count)
                        {
                            PhotoFlip.ContainerFromIndex(LastIndex)?.FindChildOfType<ScrollViewer>()?.ChangeView(null, null, 1);
                        }

                        await PhotoCollection[CurrentIndex].GenerateActualSourceAsync();
                    }
                    catch (Exception ex)
                    {
                        LogTracer.Log(ex, "Could not load the image on selection changed");
                    }
                }
            }
        }

        private void ScrollViewerMain_DoubleTapped(object sender, Windows.UI.Xaml.Input.DoubleTappedRoutedEventArgs e)
        {
            if (e.PointerDeviceType != Windows.Devices.Input.PointerDeviceType.Touch)
            {
                if (sender is ScrollViewer Viewer)
                {
                    Point TapPoint = e.GetPosition(Viewer);

                    if (Math.Abs(Viewer.ZoomFactor - 1.0) < 1E-6)
                    {
                        Viewer.ChangeView(TapPoint.X, TapPoint.Y, 2);
                    }
                    else
                    {
                        Viewer.ChangeView(null, null, 1);
                    }
                }
            }
        }

        private void ScrollViewerMain_PointerMoved(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (sender is ScrollViewer Viewer)
            {
                if (Viewer.ZoomFactor != 1 && e.Pointer.PointerDeviceType != Windows.Devices.Input.PointerDeviceType.Touch)
                {
                    PointerPoint Point = e.GetCurrentPoint(Viewer);

                    if (Point.Properties.IsLeftButtonPressed)
                    {
                        Viewer.ChangeView(OriginHorizonOffset + (OriginMousePosition.X - Point.Position.X), OriginVerticalOffset + (OriginMousePosition.Y - Point.Position.Y), null);
                    }
                }
            }
        }

        private void ScrollViewerMain_PointerPressed(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (sender is ScrollViewer Viewer)
            {
                if (Viewer.ZoomFactor != 1 && e.Pointer.PointerDeviceType != Windows.Devices.Input.PointerDeviceType.Touch)
                {
                    Window.Current.CoreWindow.PointerCursor = new CoreCursor(CoreCursorType.Hand, 0);

                    PointerPoint Point = e.GetCurrentPoint(Viewer);

                    if (Point.Properties.IsLeftButtonPressed)
                    {
                        OriginMousePosition = Point.Position;
                        OriginHorizonOffset = Viewer.HorizontalOffset;
                        OriginVerticalOffset = Viewer.VerticalOffset;
                    }
                }
            }
        }

        private void ScrollViewerMain_PointerReleased(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            Window.Current.CoreWindow.PointerCursor = new CoreCursor(CoreCursorType.Arrow, 0);
        }

        private async void ImageRotate_Click(object sender, RoutedEventArgs e)
        {
            PhotoDisplayItem Item = PhotoCollection[PhotoFlip.SelectedIndex];

            if (PhotoFlip.ContainerFromItem(Item) is FlipViewItem Container
                && Container.FindChildOfType<ScrollViewer>() is ScrollViewer Viewer)
            {
                VisualExtensions.SetNormalizedCenterPoint(Viewer, "0.5");

                switch (Item.RotateAngle % 360)
                {
                    case 0:
                    case 180:
                        {
                            DoubleAnimation WidthAnimation = new DoubleAnimation
                            {
                                From = Container.ActualWidth,
                                To = Container.ActualHeight,
                                Duration = TimeSpan.FromMilliseconds(300),
                                EnableDependentAnimation = true,
                                EasingFunction = new PowerEase { Power = 1, EasingMode = EasingMode.EaseOut }
                            };

                            Storyboard.SetTarget(WidthAnimation, Viewer);
                            Storyboard.SetTargetProperty(WidthAnimation, "Width");

                            await AnimationBuilder.Create()
                                                  .ExternalAnimation(WidthAnimation)
                                                  .RotationInDegrees(Item.RotateAngle += 90, duration: TimeSpan.FromMilliseconds(300), easingType: EasingType.Linear, easingMode: EasingMode.EaseOut)
                                                  .StartAsync(Viewer);

                            break;
                        }
                    case 90:
                    case 270:
                        {
                            DoubleAnimation WidthAnimation = new DoubleAnimation
                            {
                                From = Container.ActualHeight,
                                To = Container.ActualWidth,
                                Duration = TimeSpan.FromMilliseconds(300),
                                EnableDependentAnimation = true,
                                EasingFunction = new PowerEase { Power = 1, EasingMode = EasingMode.EaseOut }
                            };

                            Storyboard.SetTarget(WidthAnimation, Viewer);
                            Storyboard.SetTargetProperty(WidthAnimation, "Width");

                            await AnimationBuilder.Create()
                                                  .ExternalAnimation(WidthAnimation)
                                                  .RotationInDegrees(Item.RotateAngle += 90, duration: TimeSpan.FromMilliseconds(300), easingType: EasingType.Linear, easingMode: EasingMode.EaseOut)
                                                  .StartAsync(Viewer);

                            break;
                        }
                }
            }
        }

        private async void TranscodeImage_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                FileSystemStorageFile Item = PhotoCollection[PhotoFlip.SelectedIndex].PhotoFile;

                BitmapDecoder Decoder = null;

                using (Stream OriginStream = await Item.GetStreamFromFileAsync(AccessMode.Read, OptimizeOption.RandomAccess))
                {
                    Decoder = await BitmapDecoder.CreateAsync(OriginStream.AsRandomAccessStream());
                }

                TranscodeImageDialog Dialog = new TranscodeImageDialog(Decoder.PixelWidth, Decoder.PixelHeight);

                if (await Dialog.ShowAsync() == ContentDialogResult.Primary)
                {
                    TranscodeLoadingControl.IsLoading = true;

                    await Task.WhenAll(Task.Delay(500), GeneralTransformer.TranscodeFromImageAsync(Item, Dialog.TargetFile, Dialog.IsEnableScale, Dialog.ScaleWidth, Dialog.ScaleHeight, Dialog.InterpolationMode));

                    TranscodeLoadingControl.IsLoading = false;
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
            if (PhotoFlip.SelectedItem is PhotoDisplayItem Item)
            {
                try
                {
                    PhotoCollection.Remove(Item);
                    await Item.PhotoFile.DeleteAsync(true);
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

        private void Adjust_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (PhotoCollection.Count > 0)
                {
                    PhotoDisplayItem Item = PhotoCollection[Math.Min(Math.Max(0, PhotoFlip.SelectedIndex), PhotoCollection.Count - 1)];

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
                LogTracer.Log(ex, "An exception was threw when navigating to CropperPage");
            }
        }

        private async void SetAsWallpaper_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!UserProfilePersonalizationSettings.IsSupported())
                {
                    QueueContentDialog Dialog = new QueueContentDialog
                    {
                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                        Content = Globalization.GetString("QueueDialog_SetWallpaperNotSupport_Content"),
                        CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                    };

                    await Dialog.ShowAsync().ConfigureAwait(false);
                }
                else
                {
                    if (PhotoFlip.SelectedItem is PhotoDisplayItem Photo)
                    {
                        if (await Photo.PhotoFile.GetStorageItemAsync() is StorageFile File)
                        {
                            StorageFile TempFile = await File.CopyAsync(ApplicationData.Current.LocalFolder, Photo.PhotoFile.Name, NameCollisionOption.GenerateUniqueName);

                            try
                            {
                                if (await UserProfilePersonalizationSettings.Current.TrySetWallpaperImageAsync(TempFile))
                                {
                                    QueueContentDialog Dialog = new QueueContentDialog
                                    {
                                        Title = Globalization.GetString("Common_Dialog_TipTitle"),
                                        Content = Globalization.GetString("QueueDialog_SetWallpaperSuccess_Content"),
                                        CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                                    };

                                    await Dialog.ShowAsync().ConfigureAwait(false);
                                }
                                else
                                {
                                    QueueContentDialog Dialog = new QueueContentDialog
                                    {
                                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                        Content = Globalization.GetString("QueueDialog_SetWallpaperFailure_Content"),
                                        CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                                    };

                                    await Dialog.ShowAsync().ConfigureAwait(false);
                                }
                            }
                            finally
                            {
                                await TempFile.DeleteAsync(StorageDeleteOption.PermanentDelete);
                            }
                        }
                        else
                        {
                            QueueContentDialog Dialog = new QueueContentDialog
                            {
                                Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                Content = Globalization.GetString("QueueDialog_SetWallpaperFailure_Content"),
                                CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                            };

                            await Dialog.ShowAsync().ConfigureAwait(false);
                        }
                    }
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

                await Dialog.ShowAsync().ConfigureAwait(false);
            }
        }

        private async void PhotoGirdView_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (!args.InRecycleQueue)
            {
                if (args.Item is PhotoDisplayItem Item && !Cancellation.IsCancellationRequested)
                {
                    await Item.GenerateThumbnailAsync().ConfigureAwait(false);
                }
            }
        }

        private async void PhotoGirdView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (PhotoGirdView.SelectedIndex >= 0)
            {
                PhotoFlip.SelectedIndex = PhotoGirdView.SelectedIndex;

                if (PhotoGirdView.IsLoaded)
                {
                    await PhotoGirdView.SmoothScrollIntoViewWithIndexAsync(PhotoGirdView.SelectedIndex, ScrollItemPlacement.Center);
                }
                else
                {
                    PhotoGirdView.ScrollIntoView(PhotoCollection[PhotoGirdView.SelectedIndex], ScrollIntoViewAlignment.Leading);
                }
            }
        }

        private void Pips_SelectedIndexChanged(Microsoft.UI.Xaml.Controls.PipsPager sender, Microsoft.UI.Xaml.Controls.PipsPagerSelectedIndexChangedEventArgs args)
        {
            if (sender.SelectedPageIndex >= 0 && sender.SelectedPageIndex < PhotoCollection.Count)
            {
                PhotoFlip.SelectedIndex = sender.SelectedPageIndex;
            }
        }
    }
}
