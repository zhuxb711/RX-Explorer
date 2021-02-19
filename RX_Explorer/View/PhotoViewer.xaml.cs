using AnimationEffectProvider;
using Microsoft.Toolkit.Uwp.UI.Animations;
using RX_Explorer.Class;
using RX_Explorer.Dialog;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
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
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Navigation;

namespace RX_Explorer
{
    public sealed partial class PhotoViewer : Page
    {
        ObservableCollection<PhotoDisplaySupport> PhotoCollection;
        AnimationFlipViewBehavior Behavior = new AnimationFlipViewBehavior();
        string SelectedPhotoPath;
        int LastSelectIndex;
        double OriginHorizonOffset;
        double OriginVerticalOffset;
        Point OriginMousePosition;
        CancellationTokenSource Cancellation;
        Queue<int> LoadQueue;
        private int LockResource;
        ManualResetEvent ExitLocker;

        public PhotoViewer()
        {
            InitializeComponent();
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            if (e.NavigationMode == NavigationMode.New && e.Parameter is string Parameters)
            {
                SelectedPhotoPath = Parameters;

                await Initialize().ConfigureAwait(false);
            }
        }

        private async Task Initialize()
        {
            try
            {
                ExitLocker = new ManualResetEvent(false);
                Cancellation = new CancellationTokenSource();
                LoadQueue = new Queue<int>();

                Behavior.Attach(Flip);

                if (await FileSystemStorageItemBase.OpenAsync(Path.GetDirectoryName(SelectedPhotoPath), ItemFilters.Folder).ConfigureAwait(true) is FileSystemStorageItemBase Item)
                {
                    FileSystemStorageItemBase[] PictureFileList = (await Item.GetChildrenItemsAsync(SettingControl.IsDisplayHiddenItem, ItemFilters.File)).Where((Item) => Item.Type.Equals(".png", StringComparison.OrdinalIgnoreCase) || Item.Type.Equals(".jpg", StringComparison.OrdinalIgnoreCase) || Item.Type.Equals(".bmp", StringComparison.OrdinalIgnoreCase)).ToArray();

                    if (PictureFileList.Length == 0)
                    {
                        QueueContentDialog Dialog = new QueueContentDialog
                        {
                            Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                            Content = Globalization.GetString("Queue_Dialog_ImageReadError_Content"),
                            CloseButtonText = Globalization.GetString("Common_Dialog_GoBack")
                        };
                        _ = await Dialog.ShowAsync().ConfigureAwait(true);

                        Frame.GoBack();
                    }
                    else
                    {
                        int LastSelectIndex = Array.FindIndex(PictureFileList, (Photo) => Photo.Path.Equals(SelectedPhotoPath, StringComparison.OrdinalIgnoreCase));
                        
                        if (LastSelectIndex < 0 || LastSelectIndex >= PictureFileList.Length)
                        {
                            LastSelectIndex = 0;
                        }

                        PhotoCollection = new ObservableCollection<PhotoDisplaySupport>(PictureFileList.Select((Item) => new PhotoDisplaySupport(Item)));
                        Flip.ItemsSource = PhotoCollection;

                        if (!await PhotoCollection[LastSelectIndex].ReplaceThumbnailBitmapAsync().ConfigureAwait(true))
                        {
                            CouldnotLoadTip.Visibility = Visibility.Visible;
                        }

                        for (int i = LastSelectIndex - 5 > 0 ? LastSelectIndex - 5 : 0; i <= (LastSelectIndex + 5 < PhotoCollection.Count - 1 ? LastSelectIndex + 5 : PhotoCollection.Count - 1) && !Cancellation.IsCancellationRequested; i++)
                        {
                            await PhotoCollection[i].GenerateThumbnailAsync().ConfigureAwait(true);
                        }

                        if (!Cancellation.IsCancellationRequested)
                        {
                            Flip.SelectedIndex = LastSelectIndex;
                            Flip.SelectionChanged += Flip_SelectionChanged;
                            Flip.SelectionChanged += Flip_SelectionChanged1;

                            await EnterAnimation.BeginAsync().ConfigureAwait(true);
                        }
                    }
                }
                else
                {
                    throw new FileNotFoundException();
                }
            }
            catch (Exception ex)
            {
                CouldnotLoadTip.Visibility = Visibility.Visible;
                LogTracer.Log(ex, "An error was threw when initialize PhotoViewer");
            }
            finally
            {
                ExitLocker.Set();
            }
        }

        private void Flip_SelectionChanged1(object sender, SelectionChangedEventArgs e)
        {
            if (Flip.SelectedIndex == -1)
            {
                return;
            }

            if (LastSelectIndex > Flip.SelectedIndex)
            {
                _ = Flip.ContainerFromItem(Flip.SelectedIndex + 1).FindChildOfType<ScrollViewer>()?.ChangeView(null, null, 1);
            }
            else
            {
                if (Flip.SelectedIndex > 0)
                {
                    _ = Flip.ContainerFromIndex(Flip.SelectedIndex - 1).FindChildOfType<ScrollViewer>()?.ChangeView(null, null, 1);
                }
            }

            Behavior.InitAnimation(InitOption.AroundImage);

            LastSelectIndex = Flip.SelectedIndex;
        }

        protected async override void OnNavigatedFrom(NavigationEventArgs e)
        {
            if (e.NavigationMode == NavigationMode.Back)
            {
                Cancellation?.Cancel();

                await Task.Run(() =>
                {
                    ExitLocker.WaitOne();
                }).ConfigureAwait(true);

                ExitLocker.Dispose();
                ExitLocker = null;
                Cancellation.Dispose();
                Cancellation = null;
                Behavior.Detach();
                PhotoCollection?.Clear();
                PhotoCollection = null;
                SelectedPhotoPath = string.Empty;
                Flip.SelectionChanged -= Flip_SelectionChanged;
                Flip.SelectionChanged -= Flip_SelectionChanged1;
                Flip.Opacity = 0;
            }
        }

        private async void Flip_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (Flip.SelectedIndex == -1)
            {
                return;
            }

            CouldnotLoadTip.Visibility = Visibility.Collapsed;

            LoadQueue.Enqueue(Flip.SelectedIndex);

            if (Interlocked.Exchange(ref LockResource, 1) == 0)
            {
                try
                {
                    ExitLocker.Reset();

                    while (LoadQueue.Count != 0)
                    {
                        int CurrentIndex = LoadQueue.Dequeue();

                        if (!await PhotoCollection[CurrentIndex].ReplaceThumbnailBitmapAsync().ConfigureAwait(true) && CurrentIndex == Flip.SelectedIndex)
                        {
                            CouldnotLoadTip.Visibility = Visibility.Visible;
                        }

                        for (int i = CurrentIndex - 5 > 0 ? CurrentIndex - 5 : 0; i <= (CurrentIndex + 5 < PhotoCollection.Count - 1 ? CurrentIndex + 5 : PhotoCollection.Count - 1) && !Cancellation.IsCancellationRequested; i++)
                        {
                            await PhotoCollection[i].GenerateThumbnailAsync().ConfigureAwait(true);
                        }
                    }
                }
                finally
                {
                    _ = Interlocked.Exchange(ref LockResource, 0);
                    ExitLocker.Set();
                }
            }
        }

        private void ScrollViewerMain_DoubleTapped(object sender, Windows.UI.Xaml.Input.DoubleTappedRoutedEventArgs e)
        {
            if (e.PointerDeviceType != Windows.Devices.Input.PointerDeviceType.Touch)
            {
                ScrollViewer Viewer = (ScrollViewer)sender;
                Point TapPoint = e.GetPosition(Viewer);
                if (Math.Abs(Viewer.ZoomFactor - 1.0) < 1E-06)
                {
                    var ImageInSide = Viewer.FindChildOfType<Image>();
                    _ = Viewer.ChangeView(TapPoint.X, TapPoint.Y - (Viewer.ActualHeight - ImageInSide.ActualHeight), 2);
                }
                else
                {
                    _ = Viewer.ChangeView(null, null, 1);
                }
            }
        }

        private void ScrollViewerMain_PointerMoved(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            ScrollViewer Viewer = (ScrollViewer)sender;

            if (Viewer.ZoomFactor != 1 && e.Pointer.PointerDeviceType == Windows.Devices.Input.PointerDeviceType.Mouse)
            {
                var Point = e.GetCurrentPoint(Viewer);
                if (Point.Properties.IsLeftButtonPressed)
                {
                    var Position = Point.Position;

                    Viewer.ChangeView(OriginHorizonOffset + (OriginMousePosition.X - Position.X), OriginVerticalOffset + (OriginMousePosition.Y - Position.Y), null);
                }
            }
        }

        private void ScrollViewerMain_PointerPressed(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            ScrollViewer Viewer = (ScrollViewer)sender;

            if (Viewer.ZoomFactor != 1 && e.Pointer.PointerDeviceType == Windows.Devices.Input.PointerDeviceType.Mouse)
            {
                Window.Current.CoreWindow.PointerCursor = new CoreCursor(CoreCursorType.Hand, 0);
                var Point = e.GetCurrentPoint(Viewer);
                if (Point.Properties.IsLeftButtonPressed)
                {
                    OriginMousePosition = Point.Position;
                    OriginHorizonOffset = Viewer.HorizontalOffset;
                    OriginVerticalOffset = Viewer.VerticalOffset;
                }
            }
        }

        private void ScrollViewerMain_PointerReleased(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            Window.Current.CoreWindow.PointerCursor = new CoreCursor(CoreCursorType.Arrow, 0);
        }

        private async void ImageRotate_Click(object sender, RoutedEventArgs e)
        {
            PhotoDisplaySupport Item = PhotoCollection[Flip.SelectedIndex];

            if (Flip.ContainerFromItem(Item) is DependencyObject Container && Container.FindChildOfType<ScrollViewer>() is ScrollViewer Viewer)
            {
                Storyboard Story = new Storyboard();

                switch (Item.RotateAngle % 360)
                {
                    case 0:
                    case 180:
                        {
                            DoubleAnimation WidthAnimation = new DoubleAnimation
                            {
                                From = Flip.ActualWidth,
                                To = Flip.ActualHeight,
                                Duration = TimeSpan.FromMilliseconds(500),
                                EnableDependentAnimation = true,
                                EasingFunction = new CircleEase { EasingMode = EasingMode.EaseOut }
                            };

                            Storyboard.SetTarget(WidthAnimation, Viewer);
                            Storyboard.SetTargetProperty(WidthAnimation, "Width");

                            Story.Children.Add(WidthAnimation);

                            await Task.WhenAll(Story.BeginAsync(), Viewer.Rotate(Item.RotateAngle += 90).StartAsync()).ConfigureAwait(true);

                            break;
                        }
                    case 90:
                    case 270:
                        {
                            DoubleAnimation HeightAnimation = new DoubleAnimation
                            {
                                From = Flip.ActualHeight,
                                To = Flip.ActualWidth,
                                Duration = TimeSpan.FromMilliseconds(500),
                                EnableDependentAnimation = true,
                                EasingFunction = new CircleEase { EasingMode = EasingMode.EaseOut }
                            };

                            Storyboard.SetTarget(HeightAnimation, Viewer);
                            Storyboard.SetTargetProperty(HeightAnimation, "Width");

                            Story.Children.Add(HeightAnimation);

                            await Task.WhenAll(Story.BeginAsync(), Viewer.Rotate(Item.RotateAngle += 90).StartAsync()).ConfigureAwait(true);

                            break;
                        }
                }
            }
        }

        private async void TranscodeImage_Click(object sender, RoutedEventArgs e)
        {
            FileSystemStorageItemBase Item = PhotoCollection[Flip.SelectedIndex].PhotoFile;

            TranscodeImageDialog Dialog = null;
            using (IRandomAccessStream OriginStream = await Item.GetRandomAccessStreamFromFileAsync(FileAccessMode.Read).ConfigureAwait(true))
            {
                BitmapDecoder Decoder = await BitmapDecoder.CreateAsync(OriginStream);
                Dialog = new TranscodeImageDialog(Decoder.PixelWidth, Decoder.PixelHeight);
            }

            if (await Dialog.ShowAsync().ConfigureAwait(true) == ContentDialogResult.Primary)
            {
                TranscodeLoadingControl.IsLoading = true;

                await GeneralTransformer.TranscodeFromImageAsync(Item, Dialog.TargetFile, Dialog.IsEnableScale, Dialog.ScaleWidth, Dialog.ScaleHeight, Dialog.InterpolationMode).ConfigureAwait(true);

                await Task.Delay(1000).ConfigureAwait(true);

                TranscodeLoadingControl.IsLoading = false;
            }
        }

        private async void Delete_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                PhotoDisplaySupport Item = PhotoCollection[Flip.SelectedIndex];
                Item.PhotoFile.PermanentDelete();
                PhotoCollection.Remove(Item);
                Behavior.InitAnimation(InitOption.Full);
            }
            catch (Exception)
            {
                QueueContentDialog dialog = new QueueContentDialog
                {
                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                    Content = Globalization.GetString("QueueDialog_DeleteItemError_Content"),
                    CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                };
                _ = await dialog.ShowAsync().ConfigureAwait(true);
            }
        }

        private void Adjust_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (AnimationController.Current.IsEnableAnimation)
                {
                    Frame.Navigate(typeof(CropperPage), Flip.SelectedItem, new DrillInNavigationTransitionInfo());
                }
                else
                {
                    Frame.Navigate(typeof(CropperPage), Flip.SelectedItem, new SuppressNavigationTransitionInfo());
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, "An error was threw when navigating to CropperPage");
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

                    _ = await Dialog.ShowAsync().ConfigureAwait(false);
                }
                else
                {
                    if (Flip.SelectedItem is PhotoDisplaySupport Photo)
                    {
                        if (await Photo.PhotoFile.GetStorageItemAsync().ConfigureAwait(true) is StorageFile File)
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

                                    _ = await Dialog.ShowAsync().ConfigureAwait(false);
                                }
                                else
                                {
                                    QueueContentDialog Dialog = new QueueContentDialog
                                    {
                                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                        Content = Globalization.GetString("QueueDialog_SetWallpaperFailure_Content"),
                                        CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                                    };

                                    _ = await Dialog.ShowAsync().ConfigureAwait(false);
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

                            _ = await Dialog.ShowAsync().ConfigureAwait(false);
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

                _ = await Dialog.ShowAsync().ConfigureAwait(false);
            }
        }
    }
}
