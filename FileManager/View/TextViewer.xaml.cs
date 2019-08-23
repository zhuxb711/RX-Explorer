using System;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.Storage.Streams;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;


namespace FileManager
{
    public sealed partial class TextViewer : Page
    {
        private FileSystemStorageItem SFile;
        public TextViewer()
        {
            InitializeComponent();
            Loaded += TextViewer_Loaded;
        }

        private async void TextViewer_Loaded(object sender, RoutedEventArgs e)
        {
            LoadingControl.IsLoading = true;
            try
            {
                string FileText = await FileIO.ReadTextAsync(SFile.File);

                Text.Text = FileText;

                await Task.Delay(500);
                LoadingControl.IsLoading = false;
            }
            catch (ArgumentOutOfRangeException)
            {
                IBuffer buffer = await FileIO.ReadBufferAsync(SFile.File);
                DataReader reader = DataReader.FromBuffer(buffer);
                byte[] fileContent = new byte[reader.UnconsumedBufferLength];
                reader.ReadBytes(fileContent);
                Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
                Encoding GBKEncoding = Encoding.GetEncoding("GBK");

                string FileText = GBKEncoding.GetString(fileContent);

                Text.Text = FileText;

                await Task.Delay(500);
                LoadingControl.IsLoading = false;
            }
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            if (e.Parameter is FileSystemStorageItem SFile)
            {
                this.SFile = SFile;
                Title.Text = SFile.Name;
            }
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            SFile = null;
            Text.Text = string.Empty;
        }

        private async void Save_Click(object sender, RoutedEventArgs e)
        {
            StorageFolder Folder = await SFile.File.GetParentAsync();
            StorageFile NewFile = await Folder.CreateFileAsync(SFile.Name, CreationCollisionOption.ReplaceExisting);
            await FileIO.WriteTextAsync(NewFile, Text.Text);
            await SFile.UpdateRequested(NewFile);
            FileControl.ThisPage.Nav.GoBack();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            FileControl.ThisPage.Nav.GoBack();
        }
    }
}
