using System;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;


namespace RX_Explorer
{
    public sealed partial class TextViewer : Page
    {
        private StorageFile SFile;
        private FileControl FileControlInstance;

        public TextViewer()
        {
            InitializeComponent();
        }

        private async Task Initialize()
        {
            LoadingControl.IsLoading = true;
            MainPage.ThisPage.IsAnyTaskRunning = true;

            try
            {
                string FileText = await FileIO.ReadTextAsync(SFile);

                Text.Text = FileText;

                await Task.Delay(500).ConfigureAwait(true);
            }
            catch (ArgumentOutOfRangeException)
            {
                IBuffer buffer = await FileIO.ReadBufferAsync(SFile);
                DataReader reader = DataReader.FromBuffer(buffer);
                byte[] fileContent = new byte[reader.UnconsumedBufferLength];
                reader.ReadBytes(fileContent);
                Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
                Encoding GBKEncoding = Encoding.GetEncoding("GBK");

                string FileText = GBKEncoding.GetString(fileContent);

                Text.Text = FileText;

                await Task.Delay(500).ConfigureAwait(true);
            }
            finally
            {
                LoadingControl.IsLoading = false;
                MainPage.ThisPage.IsAnyTaskRunning = false;
            }
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            if (e?.Parameter is Tuple<FileControl, StorageFile> Parameters)
            {
                FileControlInstance = Parameters.Item1;
                SFile = Parameters.Item2;
                Title.Text = SFile.Name;

                await Initialize().ConfigureAwait(false);
            }
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            SFile = null;
            Text.Text = string.Empty;
        }

        private async void Save_Click(object sender, RoutedEventArgs e)
        {
            StorageFolder Folder = await SFile.GetParentAsync();
            StorageFile NewFile = await Folder.CreateFileAsync(SFile.Name, CreationCollisionOption.ReplaceExisting);
            await FileIO.WriteTextAsync(NewFile, Text.Text);
            FileControlInstance.Nav.GoBack();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            FileControlInstance.Nav.GoBack();
        }
    }
}
