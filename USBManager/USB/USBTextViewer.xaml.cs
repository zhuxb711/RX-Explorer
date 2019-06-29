using System;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.Storage.Streams;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;


namespace USBManager
{
    public sealed partial class USBTextViewer : Page
    {
        private RemovableDeviceFile SFile;
        public USBTextViewer()
        {
            InitializeComponent();
            Loaded += USBTextViewer_Loaded;
        }

        private async void USBTextViewer_Loaded(object sender, RoutedEventArgs e)
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
            if (e.Parameter is RemovableDeviceFile SFile)
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

        public async Task<string> GetSize(StorageFile file)
        {
            BasicProperties Properties = await file.GetBasicPropertiesAsync();
            return Properties.Size / 1024f < 1024 ? Math.Round(Properties.Size / 1024f, 2).ToString() + " KB" :
            (Properties.Size / 1048576f >= 1024 ? Math.Round(Properties.Size / 1073741824f, 2).ToString() + " GB" :
            Math.Round(Properties.Size / 1048576f, 2).ToString() + " MB");
        }

        private async void Save_Click(object sender, RoutedEventArgs e)
        {
            StorageFolder Folder = await SFile.File.GetParentAsync();
            StorageFile NewFile = await Folder.CreateFileAsync(SFile.Name, CreationCollisionOption.ReplaceExisting);
            await FileIO.WriteTextAsync(NewFile, Text.Text);
            SFile.FileUpdateRequested(NewFile, await GetSize(NewFile));
            USBControl.ThisPage.Nav.GoBack();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            USBControl.ThisPage.Nav.GoBack();
        }
    }
}
