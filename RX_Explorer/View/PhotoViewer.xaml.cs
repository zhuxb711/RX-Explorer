using AnimationEffectProvider;
using Microsoft.Toolkit.Uwp.UI;
using Microsoft.Toolkit.Uwp.UI.Animations;
using RX_Explorer.Class;
using RX_Explorer.Dialog;
using System;
using System.Collections.Concurrent;
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
using Windows.UI.Input;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;

namespace RX_Explorer
{
    public sealed partial class PhotoViewer : Page
    {
        private ObservableCollection<PhotoDisplaySupport> PhotoCollection = new ObservableCollection<PhotoDisplaySupport>();
        private AnimationFlipViewBehavior Behavior = new AnimationFlipViewBehavior();
        private int LastSelectIndex;
        private double OriginHorizonOffset;
        private double OriginVerticalOffset;
        private Point OriginMousePosition;
        private CancellationTokenSource Cancellation;
        private ConcurrentQueue<int> LoadQueue;
        private int LockResource;

        public PhotoViewer()
        {
            InitializeComponent();
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            if (e.NavigationMode == NavigationMode.New && e.Parameter is FileSystemStorageFile File)
            {
                await Initialize(File).ConfigureAwait(false);
            }
        }

        private async Task Initialize(FileSystemStorageFile File)
        {
            try
            {
                Cancellation = new CancellationTokenSource();
                LoadQueue = new ConcurrentQueue<int>();

                Behavior.Attach(Flip);

                if (File.Type.Equals(".sle", StringComparison.OrdinalIgnoreCase))
                {
                    using (FileStream Stream = await File.GetStreamFromFileAsync(AccessMode.Read))
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

                                PhotoCollection.Add(new PhotoDisplaySupport(Image));
                            }

                            if (!Cancellation.IsCancellationRequested)
                            {
                                EnterAnimation.Begin();
                            }
                        }
                        else
                        {
                            throw new NotSupportedException();
                        }
                    }
                }
                else if (File.Type.Equals(".png", StringComparison.OrdinalIgnoreCase) || File.Type.Equals(".bmp", StringComparison.OrdinalIgnoreCase) || File.Type.Equals(".jpg", StringComparison.OrdinalIgnoreCase))
                {
                    Adjust.IsEnabled = true;
                    SetAsWallpaper.IsEnabled = true;

                    if (await FileSystemStorageItemBase.OpenAsync(Path.GetDirectoryName(File.Path)) is FileSystemStorageFolder Item)
                    {
                        FileSystemStorageFile[] PictureFileList = (await Item.GetChildItemsAsync(SettingControl.IsDisplayHiddenItem, SettingControl.IsDisplayProtectedSystemItems, Filter: BasicFilters.File, AdvanceFilter: (Name) =>
                        {
                            string Extension = Path.GetExtension(Name);
                            return Extension.Equals(".png", StringComparison.OrdinalIgnoreCase) || Extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase) || Extension.Equals(".bmp", StringComparison.OrdinalIgnoreCase);
                        })).Cast<FileSystemStorageFile>().ToArray();

                        if (PictureFileList.Length == 0)
                        {
                            QueueContentDialog Dialog = new QueueContentDialog
                            {
                                Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                Content = Globalization.GetString("Queue_Dialog_ImageReadError_Content"),
                                CloseButtonText = Globalization.GetString("Common_Dialog_GoBack")
                            };

                            await Dialog.ShowAsync();

                            Frame.GoBack();
                        }
                        else
                        {
                            int LastSelectIndex = Array.FindIndex(PictureFileList, (Photo) => Photo.Path.Equals(File.Path, StringComparison.OrdinalIgnoreCase));

                            if (LastSelectIndex < 0 || LastSelectIndex >= PictureFileList.Length)
                            {
                                LastSelectIndex = 0;
                            }

                            foreach (PhotoDisplaySupport Photo in PictureFileList.Select((Item) => new PhotoDisplaySupport(Item)))
                            {
                                PhotoCollection.Add(Photo);
                            }

                            if (!await PhotoCollection[LastSelectIndex].ReplaceThumbnailBitmapAsync())
                            {
                                CouldnotLoadTip.Visibility = Visibility.Visible;
                            }

                            for (int i = LastSelectIndex - 5 > 0 ? LastSelectIndex - 5 : 0; i <= (LastSelectIndex + 5 < PhotoCollection.Count - 1 ? LastSelectIndex + 5 : PhotoCollection.Count - 1) && !Cancellation.IsCancellationRequested; i++)
                            {
                                await PhotoCollection[i].GenerateThumbnailAsync();
                            }

                            if (!Cancellation.IsCancellationRequested)
                            {
                                Flip.SelectedIndex = LastSelectIndex;
                                Flip.SelectionChanged += Flip_SelectionChanged;
                                Flip.SelectionChanged += Flip_SelectionChanged1;

                                EnterAnimation.Begin();
                            }
                        }
                    }
                    else
                    {
                        throw new FileNotFoundException();
                    }
                }
            }
            catch (Exception ex)
            {
                CouldnotLoadTip.Visibility = Visibility.Visible;
                LogTracer.Log(ex, "An error was threw when initialize PhotoViewer");
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

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            if (e.NavigationMode == NavigationMode.Back)
            {
                Cancellation?.Cancel();
                Cancellation?.Dispose();
                Behavior.Detach();
                PhotoCollection.Clear();
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
                    while (LoadQueue.TryDequeue(out int CurrentIndex))
                    {
                        if (!await PhotoCollection[CurrentIndex].ReplaceThumbnailBitmapAsync() && CurrentIndex == Flip.SelectedIndex)
                        {
                            CouldnotLoadTip.Visibility = Visibility.Visible;
                        }

                        for (int i = CurrentIndex - 5 > 0 ? CurrentIndex - 5 : 0; i <= (CurrentIndex + 5 < PhotoCollection.Count - 1 ? CurrentIndex + 5 : PhotoCollection.Count - 1) && !Cancellation.IsCancellationRequested; i++)
                        {
                            await PhotoCollection[i].GenerateThumbnailAsync();
                        }
                    }
                }
                finally
                {
                    Interlocked.Exchange(ref LockResource, 0);
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
                        Point Position = Point.Position;

                        Viewer.ChangeView(OriginHorizonOffset + (OriginMousePosition.X - Position.X), OriginVerticalOffset + (OriginMousePosition.Y - Position.Y), null);
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
            PhotoDisplaySupport Item = PhotoCollection[Flip.SelectedIndex];

            if (Flip.ContainerFromItem(Item) is FlipViewItem Container
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
            FileSystemStorageFile Item = PhotoCollection[Flip.SelectedIndex].PhotoFile;

            TranscodeImageDialog Dialog = null;
            using (IRandomAccessStream OriginStream = await Item.GetRandomAccessStreamFromFileAsync(FileAccessMode.Read))
            {
                BitmapDecoder Decoder = await BitmapDecoder.CreateAsync(OriginStream);
                Dialog = new TranscodeImageDialog(Decoder.PixelWidth, Decoder.PixelHeight);
            }

            if (await Dialog.ShowAsync() == ContentDialogResult.Primary)
            {
                TranscodeLoadingControl.IsLoading = true;

                await GeneralTransformer.TranscodeFromImageAsync(Item, Dialog.TargetFile, Dialog.IsEnableScale, Dialog.ScaleWidth, Dialog.ScaleHeight, Dialog.InterpolationMode);
                await Task.Delay(500);

                TranscodeLoadingControl.IsLoading = false;
            }
        }

        private async void Delete_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                PhotoDisplaySupport Item = PhotoCollection[Flip.SelectedIndex];
                await Item.PhotoFile.DeleteAsync(true);
                PhotoCollection.Remove(Item);
                Behavior.InitAnimation(InitOption.Full);
            }
            catch (Exception)
            {
                QueueContentDialog Dialog = new QueueContentDialog
                {
                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                    Content = Globalization.GetString("QueueDialog_DeleteItemError_Content"),
                    CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                };

                _ = await Dialog.ShowAsync();
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
