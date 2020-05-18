using AnimationEffectProvider;
using FileManager.Class;
using FileManager.Dialog;
using Microsoft.Toolkit.Uwp.UI.Animations;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;

namespace FileManager
{
    public sealed partial class PhotoViewer : Page
    {
        ObservableCollection<PhotoDisplaySupport> PhotoCollection;
        AnimationFlipViewBehavior Behavior = new AnimationFlipViewBehavior();
        string SelectedPhotoName;
        int LastSelectIndex;
        double OriginHorizonOffset;
        double OriginVerticalOffset;
        Point OriginMousePosition;
        bool IsNavigateToCropperPage = false;
        FileControl FileControlInstance;
        CancellationTokenSource Cancellation;
        Queue<int> LoadQueue;
        private int LockResource = 0;
        ManualResetEvent ExitLocker;

        public PhotoViewer()
        {
            InitializeComponent();
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            if (e.Parameter is Tuple<FileControl, string> Parameters)
            {
                FileControlInstance = Parameters.Item1;
                SelectedPhotoName = Parameters.Item2;

                await Initialize().ConfigureAwait(false);
            }
        }

        private async Task Initialize()
        {
            if (IsNavigateToCropperPage)
            {
                IsNavigateToCropperPage = false;
                await PhotoCollection[Flip.SelectedIndex].UpdateImage().ConfigureAwait(true);
                return;
            }

            try
            {
                ExitLocker = new ManualResetEvent(false);
                Cancellation = new CancellationTokenSource();
                LoadQueue = new Queue<int>();

                MainPage.ThisPage.IsAnyTaskRunning = true;

                Behavior.Attach(Flip);

                List<FileSystemStorageItem> FileList = WIN_Native_API.GetStorageItems(FileControlInstance.CurrentFolder, ItemFilter.File).Where((Item) => Item.Type.Equals(".png", StringComparison.OrdinalIgnoreCase) || Item.Type.Equals(".jpg", StringComparison.OrdinalIgnoreCase) || Item.Type.Equals(".bmp", StringComparison.OrdinalIgnoreCase)).ToList();

                int LastSelectIndex = FileList.FindIndex((Photo) => Photo.Name == SelectedPhotoName);
                if (LastSelectIndex < 0 || LastSelectIndex >= FileList.Count)
                {
                    LastSelectIndex = 0;
                }

                if (FileList.Count == 0)
                {
                    QueueContentDialog Dialog = new QueueContentDialog
                    {
                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                        Content = Globalization.GetString("Queue_Dialog_ImageReadError_Content"),
                        CloseButtonText = Globalization.GetString("Common_Dialog_GoBack")
                    };
                    _ = await Dialog.ShowAsync().ConfigureAwait(true);

                    FileControlInstance.Nav.GoBack();
                    return;
                }

                PhotoCollection = new ObservableCollection<PhotoDisplaySupport>(FileList.Select((Item) => new PhotoDisplaySupport(Item)));
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

                    await Task.Delay(1000).ConfigureAwait(true);
                }
            }
            catch (Exception ex)
            {
                ExceptionTracer.RequestBlueScreen(ex);
            }
            finally
            {
                MainPage.ThisPage.IsAnyTaskRunning = false;
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
            if (IsNavigateToCropperPage)
            {
                return;
            }

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
            PhotoCollection.Clear();
            PhotoCollection = null;
            SelectedPhotoName = string.Empty;
            Flip.SelectionChanged -= Flip_SelectionChanged;
            Flip.SelectionChanged -= Flip_SelectionChanged1;
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
                Window.Current.CoreWindow.PointerCursor = new Windows.UI.Core.CoreCursor(Windows.UI.Core.CoreCursorType.Hand, 0);
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
            Window.Current.CoreWindow.PointerCursor = new Windows.UI.Core.CoreCursor(Windows.UI.Core.CoreCursorType.Arrow, 0);
        }

        private async void ImageRotate_Click(object sender, RoutedEventArgs e)
        {
            PhotoDisplaySupport Item = PhotoCollection[Flip.SelectedIndex];
            ScrollViewer Viewer = Flip.ContainerFromItem(Item).FindChildOfType<ScrollViewer>();

            Viewer.RenderTransformOrigin = new Point(0.5, 0.5);
            await Viewer.Rotate(Item.RotateAngle += 90).StartAsync().ConfigureAwait(false);
        }

        private async void TranscodeImage_Click(object sender, RoutedEventArgs e)
        {
            if ((await PhotoCollection[Flip.SelectedIndex].PhotoFile.GetStorageItem().ConfigureAwait(true)) is StorageFile OriginFile)
            {
                TranscodeImageDialog Dialog = null;
                using (IRandomAccessStream OriginStream = await OriginFile.OpenAsync(FileAccessMode.Read))
                {
                    BitmapDecoder Decoder = await BitmapDecoder.CreateAsync(OriginStream);
                    Dialog = new TranscodeImageDialog(Decoder.PixelWidth, Decoder.PixelHeight);
                }

                if (await Dialog.ShowAsync().ConfigureAwait(true) == ContentDialogResult.Primary)
                {
                    TranscodeLoadingControl.IsLoading = true;
                    MainPage.ThisPage.IsAnyTaskRunning = true;

                    await GeneralTransformer.TranscodeFromImageAsync(OriginFile, Dialog.TargetFile, Dialog.IsEnableScale, Dialog.ScaleWidth, Dialog.ScaleHeight, Dialog.InterpolationMode).ConfigureAwait(true);

                    await Task.Delay(1000).ConfigureAwait(true);

                    TranscodeLoadingControl.IsLoading = false;
                    MainPage.ThisPage.IsAnyTaskRunning = false;
                }
            }
        }

        private async void Delete_Click(object sender, RoutedEventArgs e)
        {
            QueueContentDialog Dialog = new QueueContentDialog
            {
                Title = Globalization.GetString("Common_Dialog_WarningTitle"),
                PrimaryButtonText = Globalization.GetString("Common_Dialog_ContinueButton"),
                Content = Globalization.GetString("QueueDialog_DeleteFile_Content"),
                CloseButtonText = Globalization.GetString("Common_Dialog_CancelButton")
            };

            if ((await Dialog.ShowAsync().ConfigureAwait(true)) == ContentDialogResult.Primary)
            {
                PhotoDisplaySupport Item = PhotoCollection[Flip.SelectedIndex];
                await (await Item.PhotoFile.GetStorageItem().ConfigureAwait(true)).DeleteAsync(StorageDeleteOption.PermanentDelete);
                PhotoCollection.Remove(Item);
                Behavior.InitAnimation(InitOption.Full);
            }
        }

        private void Adjust_Click(object sender, RoutedEventArgs e)
        {
            IsNavigateToCropperPage = true;
            try
            {
                FileControlInstance.Nav.Navigate(typeof(CropperPage), new Tuple<Frame, object>(FileControlInstance.Nav, Flip.SelectedItem), new DrillInNavigationTransitionInfo());
            }
            catch (Exception ex)
            {
                ExceptionTracer.RequestBlueScreen(ex);
            }
        }
    }
}
