using RX_Explorer.Class;
using RX_Explorer.Dialog;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;


namespace RX_Explorer
{
    public sealed partial class TextViewer : Page
    {
        private readonly ObservableCollection<Encoding> Encodings = new ObservableCollection<Encoding>();

        private FileSystemStorageFile TextFile;

        private Encoding SaveEncoding;

        public TextViewer()
        {
            InitializeComponent();

            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            try
            {
                if (Globalization.CurrentLanguage == LanguageEnum.Chinese_Simplified)
                {
                    Encodings.Add(Encoding.GetEncoding("GBK"));
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, "Could not load GBK encoding");
            }

            foreach (Encoding Coding in Encoding.GetEncodings().Select((Info) => Info.GetEncoding()))
            {
                Encodings.Add(Coding);
            }
        }

        private async Task Initialize()
        {
            TextEncodingDialog EncodingDialog = new TextEncodingDialog(TextFile, Encodings);

            if (await EncodingDialog.ShowAsync() == ContentDialogResult.Primary)
            {
                SaveEncoding = EncodingDialog.UserSelectedEncoding;
                await LoadTextFromFileWithEncoding(TextFile, SaveEncoding).ConfigureAwait(false);
            }
            else
            {
                Frame.GoBack();
            }
        }

        protected async override void OnNavigatedTo(NavigationEventArgs e)
        {
            if (e?.Parameter is FileSystemStorageFile TextFile)
            {
                Title.Text = TextFile.Name;
                this.TextFile = TextFile;

                await Initialize();
            }
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            TextFile = null;
            EditText.Text = string.Empty;
        }

        private async Task LoadTextFromFileWithEncoding(FileSystemStorageFile File, Encoding Enco)
        {
            LoadingControl.IsLoading = true;
            await Task.Delay(500);

            try
            {
                using (FileStream Stream = await File.GetFileStreamFromFileAsync(AccessMode.Read))
                using (StreamReader Reader = new StreamReader(Stream, Enco, false))
                {
                    EditText.Text = await Reader.ReadToEndAsync();
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, "Could not load the content in file");

                QueueContentDialog Dialog = new QueueContentDialog
                {
                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                    Content = Globalization.GetString("QueueDialog_CouldReadWriteFile_Content"),
                    CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                };

                await Dialog.ShowAsync();
            }

            await Task.Delay(500);
            LoadingControl.IsLoading = false;
        }

        private async void Save_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (await FileSystemStorageItemBase.CreateAsync(TextFile.Path, StorageItemTypes.File, CreateOption.ReplaceExisting) is FileSystemStorageFile File)
                {
                    using (FileStream Stream = await File.GetFileStreamFromFileAsync(AccessMode.Write))
                    using (StreamWriter Writer = new StreamWriter(Stream, SaveEncoding))
                    {
                        await Writer.WriteAsync(EditText.Text);
                    }
                }
                else
                {
                    throw new FileNotFoundException();
                }
            }
            catch(Exception ex)
            {
                LogTracer.Log(ex, "Could not save the content to file");

                QueueContentDialog Dialog = new QueueContentDialog
                {
                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                    Content = Globalization.GetString("QueueDialog_CouldReadWriteFile_Content"),
                    CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                };

                await Dialog.ShowAsync();
            }
            finally
            {
                Frame.GoBack();
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            Frame.GoBack();
        }
    }
}
