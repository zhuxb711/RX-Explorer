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

        private string TextFilePath;

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

        private async Task Initialize(FileSystemStorageFile TextFile)
        {
            Title.Text = TextFile.Name;
            TextFilePath = TextFile.Path;

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
                await Initialize(TextFile);
            }
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            EditText.Text = string.Empty;
        }

        private async Task LoadTextFromFileWithEncoding(FileSystemStorageFile File, Encoding Enco)
        {
            LoadingControl.IsLoading = true;

            try
            {
                Stream TextStream = null;

                if (File.Type.Equals(".sle", StringComparison.OrdinalIgnoreCase))
                {
                    FileStream Stream = await File.GetStreamFromFileAsync(AccessMode.Read);

                    SLEHeader Header = SLEHeader.GetHeader(Stream);

                    if (Header.Version >= SLEVersion.Version_1_5_0)
                    {
                        TextStream = new SLEInputStream(Stream, SecureArea.AESKey);
                    }
                    else
                    {
                        throw new NotSupportedException();
                    }
                }
                else if (File.Type.Equals(".txt", StringComparison.OrdinalIgnoreCase))
                {
                    Save.IsEnabled = true;
                    TextStream = await File.GetStreamFromFileAsync(AccessMode.Read);
                }
                else
                {
                    throw new NotSupportedException();
                }

                using (StreamReader Reader = new StreamReader(TextStream, Enco, false))
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
                if (await FileSystemStorageItemBase.CreateNewAsync(TextFilePath, StorageItemTypes.File, CreateOption.ReplaceExisting) is FileSystemStorageFile File)
                {
                    using (FileStream Stream = await File.GetStreamFromFileAsync(AccessMode.Write))
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
            catch (Exception ex)
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
