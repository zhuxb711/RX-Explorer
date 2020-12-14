using RX_Explorer.Class;
using System;
using System.IO;
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
        private FileSystemStorageItemBase TextFile;

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
                using (FileStream Stream = TextFile.GetFileStreamFromFile(AccessMode.Read))
                using (StreamReader Reader = new StreamReader(Stream))
                {
                    Text.Text = await Reader.ReadToEndAsync().ConfigureAwait(true);
                }
            }
            catch (ArgumentOutOfRangeException)
            {
                Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
                Encoding GBKEncoding = Encoding.GetEncoding("GBK");

                using (FileStream Stream = TextFile.GetFileStreamFromFile(AccessMode.Read))
                using (StreamReader Reader = new StreamReader(Stream, GBKEncoding))
                {
                    Text.Text = await Reader.ReadToEndAsync().ConfigureAwait(true);
                }
            }
            finally
            {
                await Task.Delay(500).ConfigureAwait(true);
                LoadingControl.IsLoading = false;
                MainPage.ThisPage.IsAnyTaskRunning = false;
            }
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            if (e?.Parameter is FileSystemStorageItemBase Parameters)
            {
                TextFile = Parameters;
                Title.Text = TextFile.Name;

                await Initialize().ConfigureAwait(false);
            }
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            TextFile = null;
            Text.Text = string.Empty;
        }

        private async void Save_Click(object sender, RoutedEventArgs e)
        {
            using (FileStream Stream = TextFile.GetFileStreamFromFile(AccessMode.Write))
            using(StreamWriter Writer = new StreamWriter(Stream))
            {
                await Writer.WriteAsync(Text.Text).ConfigureAwait(true);
            }

            Frame.GoBack();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            Frame.GoBack();
        }
    }
}
