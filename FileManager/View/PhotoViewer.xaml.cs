using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
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
        ObservableCollection<PhotoDisplaySupport> PhotoCollection;
        string SelectedPhotoID;
        StorageFile DisplayFile;

        public PhotoViewer()
        {
            InitializeComponent();
            Loaded += PhotoViewer_Loaded;
        }

        private async void PhotoViewer_Loaded(object sender, RoutedEventArgs e)
        {
            PhotoCollection = new ObservableCollection<PhotoDisplaySupport>();
            ImageList.ItemsSource = PhotoCollection;

            QueryOptions Options = new QueryOptions(CommonFileQuery.DefaultQuery, null)
            {
                FolderDepth = FolderDepth.Shallow,
                IndexerOption = IndexerOption.UseIndexerWhenAvailable
            };

            Options.SetThumbnailPrefetch(ThumbnailMode.PicturesView, 250, ThumbnailOptions.ResizeThumbnail);

            StorageFileQueryResult QueryResult = FileControl.ThisPage.CurrentFolder.CreateFileQueryWithOptions(Options);

            var FileCollection = await QueryResult.GetFilesAsync();

            PhotoDisplaySupport SelectedPhoto = null;

            foreach (StorageFile File in FileCollection.Where(File => File.FileType == ".png" || File.FileType == ".jpg" || File.FileType == ".jpeg" || File.FileType == ".bmp").Select(File => File))
            {
                using (var Thumbnail = await File.GetThumbnailAsync(ThumbnailMode.PicturesView))
                {
                    PhotoCollection.Add(new PhotoDisplaySupport(Thumbnail, File));
                }

                if (File.FolderRelativeId == SelectedPhotoID)
                {
                    SelectedPhoto = PhotoCollection.Last();
                }
            }

            await Task.Delay(800);
            ImageList.ScrollIntoViewSmoothly(SelectedPhoto, ScrollIntoViewAlignment.Leading);
            ImageList.SelectedItem = SelectedPhoto;

            await Task.Delay(500);
            ChangeDisplayImage(ImageList.SelectedItem as PhotoDisplaySupport);
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
            PhotoCollection.Clear();
            PhotoCollection = null;
            SelectedPhotoID = string.Empty;
            DisplayImage.Source = null;
            FileName.Text = "";
            DisplayFile = null;
        }

        private void ImageList_ItemClick(object sender, ItemClickEventArgs e)
        {
            var SelectedPhoto = e.ClickedItem as PhotoDisplaySupport;
            if (SelectedPhoto.PhotoFile.FolderRelativeId != SelectedPhotoID)
            {
                ChangeDisplayImage(SelectedPhoto);
            }
        }

        /// <summary>
        /// 使用动画效果更改当前显示的图片
        /// </summary>
        /// <param name="e">需要显示的图片</param>
        private void ChangeDisplayImage(PhotoDisplaySupport e)
        {
            FileName.Text = e.FileName;
            DisplayFile = e.PhotoFile;
            DisplayImage.Opacity = 0;

            Image image = ((ImageList.ContainerFromItem(e) as ListViewItem).ContentTemplateRoot as FrameworkElement).FindName("Photo") as Image;
            ConnectedAnimationService.GetForCurrentView().PrepareToAnimate("PhotoAnimation", image).Configuration = new BasicConnectedAnimationConfiguration();

            ConnectedAnimationService.GetForCurrentView().DefaultDuration = TimeSpan.FromMilliseconds(600);

            FadeOut.Begin();
            SelectedPhotoID = e.PhotoFile.FolderRelativeId;
        }

        private async void FadeOut_Completed(object sender, object e)
        {
            using (var stream = await DisplayFile.OpenAsync(FileAccessMode.Read))
            {
                var bitmap = new BitmapImage();
                DisplayImage.Source = bitmap;
                await bitmap.SetSourceAsync(stream);
            }

            try
            {
                ConnectedAnimation animation = ConnectedAnimationService.GetForCurrentView().GetAnimation("PhotoAnimation");
                animation?.TryStart(DisplayImage);
            }
            catch (Exception) { }
            finally
            {
                FadeIn.Begin();
                DisplayImage.Opacity = 1;
            }
        }
    }
}
