using RX_Explorer.Class;
using RX_Explorer.Dialog;
using ShareClassLibrary;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;


namespace RX_Explorer.View
{
    public sealed partial class TextViewer : Page
    {
        private string TextFilePath;

        private Encoding SaveEncoding;

        public TextViewer()
        {
            InitializeComponent();
        }

        private async Task Initialize(FileSystemStorageFile TextFile)
        {
            Title.Text = TextFile.Name;
            TextFilePath = TextFile.Path;

            TextEncodingDialog EncodingDialog = new TextEncodingDialog(TextFile);

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

        private async Task LoadTextFromFileWithEncoding(FileSystemStorageFile File, Encoding TextEncoding)
        {
            LoadingControl.IsLoading = true;

            try
            {
                Stream TextStream = null;

                if (File.Type.Equals(".sle", StringComparison.OrdinalIgnoreCase))
                {
                    FileStream Stream = await File.GetStreamFromFileAsync(AccessMode.Read, OptimizeOption.RandomAccess);

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
                    TextStream = await File.GetStreamFromFileAsync(AccessMode.Read, OptimizeOption.Sequential);
                }
                else
                {
                    throw new NotSupportedException();
                }

                using (StreamReader Reader = new StreamReader(TextStream, TextEncoding, false))
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
                    using (FileStream Stream = await File.GetStreamFromFileAsync(AccessMode.Write, OptimizeOption.Sequential))
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

        private void EditText_PreviewKeyDown(object sender, Windows.UI.Xaml.Input.KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Tab)
            {
                e.Handled = true;

                if (sender is TextBox Box)
                {
                    int CursorPoint = Box.SelectionStart;

                    if (Box.FindChildOfType<ScrollViewer>() is ScrollViewer Viewer)
                    {
                        double CurrentVOffset = Viewer.VerticalOffset;
                        double CurrentHOffset = Viewer.HorizontalOffset;

                        Box.Text = Box.Text.Insert(CursorPoint, "    ");
                        Box.SelectionStart = CursorPoint + 4;

                        Viewer.ChangeView(CurrentHOffset, CurrentVOffset, null, true);
                    }
                }
            }
        }
    }
}
