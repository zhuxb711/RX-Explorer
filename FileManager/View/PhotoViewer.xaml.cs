using AnimationEffectProvider;
using Microsoft.Toolkit.Uwp.UI.Animations;
using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.Storage.Search;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;

namespace FileManager
{
    public sealed partial class PhotoViewer : Page
    {
        ObservableCollection<PhotoDisplaySupport> PhotoCollection = new ObservableCollection<PhotoDisplaySupport>();
        StorageFileQueryResult QueryResult;
        AnimationFlipViewBehavior Behavior = new AnimationFlipViewBehavior();
        string SelectedPhotoID;
        int LastSelectIndex;
        double OriginHorizonOffset;
        double OriginVerticalOffset;
        Point OriginMousePosition;
        bool IsNavigateToCropperPage = false;

        public PhotoViewer()
        {
            InitializeComponent();
            Loaded += PhotoViewer_Loaded;
        }

        private async void PhotoViewer_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                if (IsNavigateToCropperPage)
                {
                    IsNavigateToCropperPage = false;
                    await PhotoCollection[Flip.SelectedIndex].UpdateImage();
                    return;
                }

                LoadingControl.IsLoading = true;

                Behavior.Attach(Flip);

                QueryOptions Options = new QueryOptions(CommonFolderQuery.DefaultQuery)
                {
                    FolderDepth = FolderDepth.Shallow,
                    IndexerOption = IndexerOption.UseIndexerWhenAvailable,
                    ApplicationSearchFilter = "System.Kind:=System.Kind#Picture"
                };
                Options.SetThumbnailPrefetch(ThumbnailMode.SingleItem, 200, ThumbnailOptions.UseCurrentScale);
                QueryResult = FileControl.ThisPage.CurrentFolder.CreateFileQueryWithOptions(Options);

                ProBar.Maximum = await QueryResult.GetItemCountAsync();
                ProBar.Value = 0;

                var FileCollection = await QueryResult.GetFilesAsync();
                foreach (var File in FileCollection)
                {
                    using (StorageItemThumbnail ThumbnailStream = await File.GetThumbnailAsync(ThumbnailMode.SingleItem))
                    {
                        BitmapImage ImageSource = new BitmapImage();
                        await ImageSource.SetSourceAsync(ThumbnailStream);
                        PhotoCollection.Add(new PhotoDisplaySupport(ImageSource, File));

                        ProBar.Value++;

                        if (File.FolderRelativeId == SelectedPhotoID)
                        {
                            Flip.SelectedIndex = PhotoCollection.Count - 1;
                            LastSelectIndex = Flip.SelectedIndex;
                        }
                    }
                }

                Flip.SelectionChanged += Flip_SelectionChanged;

                await Task.Delay(500);
                await PhotoCollection[LastSelectIndex].ReplaceThumbnailBitmap();

                LoadingControl.IsLoading = false;
                OpacityAnimation.Begin();
            }
            catch (Exception ex)
            {
                ExceptionTracer.RequestBlueScreen(ex);
            }
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            if (e.Parameter is string ID)
            {
                SelectedPhotoID = ID;
            }
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            if (IsNavigateToCropperPage)
            {
                return;
            }

            Flip.Opacity = 0;
            Behavior.Detach();
            PhotoCollection.Clear();
            SelectedPhotoID = string.Empty;
            Flip.SelectionChanged -= Flip_SelectionChanged;
            QueryResult = null;
        }

        private async void Flip_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            PhotoDisplaySupport Photo = null;
            if (Interlocked.Exchange(ref Photo, Flip.SelectedItem as PhotoDisplaySupport) == null && Photo != null)
            {
                if (LastSelectIndex > Flip.SelectedIndex)
                {
                    _ = Flip.ContainerFromIndex(Flip.SelectedIndex + 1).FindChildOfType<ScrollViewer>()?.ChangeView(null, null, 1);
                }
                else
                {
                    if (Flip.SelectedIndex > 0)
                    {
                        _ = Flip.ContainerFromIndex(Flip.SelectedIndex - 1).FindChildOfType<ScrollViewer>()?.ChangeView(null, null, 1);
                    }
                }
                await Photo.ReplaceThumbnailBitmap();
                Behavior.InitAnimation(InitOption.AroundImage);
                LastSelectIndex = Flip.SelectedIndex;
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
            await Viewer.Rotate(Item.RotateAngle += 90).StartAsync();
        }

        private async void TranscodeImage_Click(object sender, RoutedEventArgs e)
        {
            StorageFile OriginFile = PhotoCollection[Flip.SelectedIndex].PhotoFile;
            TranscodeImageDialog Dialog = null;
            using (var OriginStream = await OriginFile.OpenAsync(FileAccessMode.Read))
            {
                BitmapDecoder Decoder = await BitmapDecoder.CreateAsync(OriginStream);
                Dialog = new TranscodeImageDialog(Decoder.PixelWidth, Decoder.PixelHeight);
            }

            if (await Dialog.ShowAsync() == ContentDialogResult.Primary)
            {
                TranscodeLoadingControl.IsLoading = true;
                await GeneralTransformer.TranscodeFromImageAsync(OriginFile, Dialog.TargetFile, Dialog.IsEnableScale, Dialog.ScaleWidth, Dialog.ScaleHeight, Dialog.InterpolationMode);
                await Task.Delay(1000);
                TranscodeLoadingControl.IsLoading = false;
            }
        }

        private async void Delete_Click(object sender, RoutedEventArgs e)
        {
            QueueContentDialog Dialog;
            if (Globalization.Language == LanguageEnum.Chinese)
            {
                Dialog = new QueueContentDialog
                {
                    Title = "警告",
                    Content = "此操作将永久删除该图像文件",
                    PrimaryButtonText = "继续",
                    CloseButtonText = "取消"
                };
            }
            else
            {
                Dialog = new QueueContentDialog
                {
                    Title = "Warning",
                    Content = "This action will permanently delete the image file",
                    PrimaryButtonText = "Continue",
                    CloseButtonText = "Cancel"
                };
            }

            if ((await Dialog.ShowAsync()) == ContentDialogResult.Primary)
            {
                PhotoDisplaySupport Item = PhotoCollection[Flip.SelectedIndex];
                await Item.PhotoFile.DeleteAsync(StorageDeleteOption.PermanentDelete);
                PhotoCollection.Remove(Item);
                Behavior.InitAnimation(InitOption.Full);
            }
        }

        private void Adjust_Click(object sender, RoutedEventArgs e)
        {
            IsNavigateToCropperPage = true;
            try
            {
                FileControl.ThisPage.Nav.Navigate(typeof(CropperPage), Flip.SelectedItem, new DrillInNavigationTransitionInfo());
            }
            catch(Exception ex)
            {
                ExceptionTracer.RequestBlueScreen(ex);
            }
        }
    }
}
