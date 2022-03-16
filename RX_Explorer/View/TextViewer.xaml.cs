using RX_Explorer.Class;
using RX_Explorer.Dialog;
using ShareClassLibrary;
using System;
using System.IO;
using System.Text;
using System.Threading;
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
        private CancellationTokenSource Cancellation;

        public TextViewer()
        {
            InitializeComponent();
        }

        private async Task InitializeAsync(FileSystemStorageFile TextFile, CancellationToken CancelToken)
        {
            Title.Text = TextFile.Name;
            TextFilePath = TextFile.Path;

            TextEncodingDialog EncodingDialog = new TextEncodingDialog(TextFile);

            if (await EncodingDialog.ShowAsync() == ContentDialogResult.Primary)
            {
                await LoadTextFromFileWithEncoding(TextFile, EncodingDialog.UserSelectedEncoding, CancelToken);
            }
            else if (Frame.CanGoBack)
            {
                Frame.GoBack();
            }
        }

        protected async override void OnNavigatedTo(NavigationEventArgs e)
        {
            Cancellation = new CancellationTokenSource();

            if (e?.Parameter is FileSystemStorageFile TextFile)
            {
                await InitializeAsync(TextFile, Cancellation.Token);
            }
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            TabViewContainer.CurrentTabRenderer?.SetLoadingTipsStatus(false);

            Cancellation?.Cancel();
            Cancellation?.Dispose();

            EditText.Text = string.Empty;
        }

        private async Task LoadTextFromFileWithEncoding(FileSystemStorageFile File, Encoding TextEncoding, CancellationToken CancelToken)
        {
            TabViewContainer.CurrentTabRenderer?.SetLoadingTipsStatus(true);

            try
            {
                Stream TextStream = null;

                switch (File.Type.ToLower())
                {
                    case ".sle":
                        {
                            Stream Stream = await File.GetStreamFromFileAsync(AccessMode.Read, OptimizeOption.RandomAccess);

                            SLEHeader Header = SLEHeader.GetHeader(Stream);

                            if (Header.Version >= SLEVersion.Version_1_5_0)
                            {
                                TextStream = new SLEInputStream(Stream, SecureArea.AESKey);
                            }

                            break;
                        }
                    case ".txt":
                        {
                            TextStream = await File.GetStreamFromFileAsync(AccessMode.Read, OptimizeOption.Sequential);
                            break;
                        }
                }

                if (CancelToken.IsCancellationRequested)
                {
                    TextStream?.Dispose();
                }
                else
                {
                    try
                    {
                        if (TextStream == null)
                        {
                            throw new NotSupportedException();
                        }
                        else if (TextStream is SLEInputStream)
                        {
                            Save.IsEnabled = false;
                        }

                        SaveEncoding = TextEncoding;

                        using (StreamReader Reader = new StreamReader(TextStream, TextEncoding, false))
                        {
                            EditText.Text = await Reader.ReadToEndAsync();
                        }

                        await Task.Delay(1000);
                    }
                    finally
                    {
                        TabViewContainer.CurrentTabRenderer?.SetLoadingTipsStatus(false);
                    }
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, "Could not load the content in file");

                await new QueueContentDialog
                {
                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                    Content = Globalization.GetString("QueueDialog_CouldReadWriteFile_Content"),
                    CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                }.ShowAsync();

                if (Frame.CanGoBack)
                {
                    Frame.GoBack();
                }
            }
        }

        private async void Save_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (await FileSystemStorageItemBase.CreateNewAsync(TextFilePath, StorageItemTypes.File, CreateOption.ReplaceExisting) is FileSystemStorageFile File)
                {
                    using (Stream Stream = await File.GetStreamFromFileAsync(AccessMode.Write, OptimizeOption.Sequential))
                    using (StreamWriter Writer = new StreamWriter(Stream, SaveEncoding))
                    {
                        await Writer.WriteAsync(EditText.Text);
                        await Writer.FlushAsync();
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
                if (Frame.CanGoBack)
                {
                    Frame.GoBack();
                }
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            if (Frame.CanGoBack)
            {
                Frame.GoBack();
            }
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
