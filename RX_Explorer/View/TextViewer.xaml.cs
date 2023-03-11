using RX_Explorer.Class;
using RX_Explorer.Dialog;
using SharedLibrary;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Navigation;


namespace RX_Explorer.View
{
    public sealed partial class TextViewer : Page
    {
        private string TextFilePath;
        private Encoding SaveEncoding;
        private CancellationTokenSource Cancellation;
        private IReadOnlyDictionary<int, TextLineData> LineData;

        public TextViewer()
        {
            InitializeComponent();
        }

        private async Task InitializeAsync(FileSystemStorageFile TextFile, CancellationToken CancelToken = default)
        {
            Title.Text = TextFile.Name;
            TextFilePath = TextFile.Path;

            TextEncodingDialog EncodingDialog = new TextEncodingDialog(TextFile);

            if (await EncodingDialog.ShowAsync() == ContentDialogResult.Primary)
            {
                Encoding TextEncoding = EncodingDialog.UserSelectedEncoding;

                EncodingDisplay.Text = TextEncoding.EncodingName;
                LineColumnDisplay.Text = $"{Globalization.GetString("LineDescription")} 1, {Globalization.GetString("ColumnDescription")} 1";

                await LoadTextFromFileWithEncodingAsync(TextFile, TextEncoding, CancelToken);
            }
            else if (Frame.CanGoBack)
            {
                Frame.GoBack();
            }
        }

        protected async override void OnNavigatedTo(NavigationEventArgs e)
        {
            EncodingDisplay.Text = string.Empty;
            LineBreakDisplay.Text = string.Empty;
            LineColumnDisplay.Text = string.Empty;

            Cancellation = new CancellationTokenSource();

            if (e?.Parameter is FileSystemStorageFile TextFile)
            {
                await InitializeAsync(TextFile, Cancellation.Token);
            }
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            TabViewContainer.Current.CurrentTabRenderer?.SetLoadingTipsStatus(false);

            Cancellation?.Cancel();
            Cancellation?.Dispose();

            EditText.Text = string.Empty;
        }

        private async Task LoadTextFromFileWithEncodingAsync(FileSystemStorageFile File, Encoding TextEncoding, CancellationToken CancelToken)
        {
            TabViewContainer.Current.CurrentTabRenderer?.SetLoadingTipsStatus(true);

            try
            {
                Stream TextStream = null;

                switch (File.Type.ToLower())
                {
                    case ".sle":
                        {
                            Stream Stream = await File.GetStreamFromFileAsync(AccessMode.Read);
                            SLEInputStream SLEStream = new SLEInputStream(Stream, new UTF8Encoding(false), KeyGenerator.GetMD5WithLength(SettingPage.SecureAreaUnlockPassword, 16));

                            if (SLEStream.Header.Core.Version >= SLEVersion.SLE150)
                            {
                                TextStream = SLEStream;
                            }
                            else
                            {
                                Stream.Dispose();
                                SLEStream.Dispose();
                                throw new NotSupportedException();
                            }

                            break;
                        }
                    case ".txt":
                        {
                            TextStream = await File.GetStreamFromFileAsync(AccessMode.Read);
                            break;
                        }
                    default:
                        {
                            throw new NotSupportedException();
                        }
                }

                try
                {
                    if (!CancelToken.IsCancellationRequested)
                    {
                        if (TextStream is SLEInputStream)
                        {
                            Save.IsEnabled = false;
                        }

                        SaveEncoding = TextEncoding;

                        using (StreamReader Reader = new StreamReader(TextStream, TextEncoding, true, 1024, true))
                        {
                            EditText.Text = await Reader.ReadToEndAsync();
                        }

                        TextStream.Seek(0, SeekOrigin.Begin);

                        using (BinaryReader Reader = new BinaryReader(TextStream, TextEncoding, true))
                        {
                            LineData = await CollectLineDataAsync(new string(TextEncoding.GetChars(Reader.ReadBytes(Convert.ToInt32(TextStream.Length)))));
                            LineBreakDisplay.Text = string.Join(" \\ ", LineData.Values.Select((Data) => Data.LineBreakDescription).Where((Text) => !string.IsNullOrEmpty(Text)).Distinct());
                        }

                        if (LineBreakDisplay.Text.Contains("\\"))
                        {
                            CommonContentDialog Dialog = new CommonContentDialog
                            {
                                Title = Globalization.GetString("Common_Dialog_WarningTitle"),
                                Content = Globalization.GetString("QueueDialog_MixLineEnding_Content"),
                                CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                            };

                            await Dialog.ShowAsync();
                        }

                        await Task.Delay(500);
                    }
                }
                finally
                {
                    TextStream?.Dispose();
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, "Could not load the content in file");

                await new CommonContentDialog
                {
                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                    Content = Globalization.GetString("QueueDialog_UnableReadWriteFile_Content"),
                    CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                }.ShowAsync();

                if (Frame.CanGoBack)
                {
                    Frame.GoBack();
                }
            }
            finally
            {
                TabViewContainer.Current.CurrentTabRenderer?.SetLoadingTipsStatus(false);
            }
        }

        private async void Save_Click(object sender, RoutedEventArgs e)
        {
            TabViewContainer.Current.CurrentTabRenderer?.SetLoadingTipsStatus(true);

            try
            {
                if (await FileSystemStorageItemBase.CreateNewAsync(TextFilePath, CreateType.File, CollisionOptions.OverrideOnCollision) is FileSystemStorageFile File)
                {
                    using (Stream Stream = await File.GetStreamFromFileAsync(AccessMode.Write))
                    using (StreamWriter Writer = new StreamWriter(Stream, SaveEncoding))
                    {
                        await Writer.WriteAsync(EditText.Text.Replace("\r", Environment.NewLine));
                        await Writer.FlushAsync();
                    }
                }
                else
                {
                    throw new UnauthorizedAccessException();
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, "Could not save the content to file");

                CommonContentDialog Dialog = new CommonContentDialog
                {
                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                    Content = Globalization.GetString("QueueDialog_UnableReadWriteFile_Content"),
                    CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                };

                await Dialog.ShowAsync();
            }
            finally
            {
                TabViewContainer.Current.CurrentTabRenderer?.SetLoadingTipsStatus(false);
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            if (Frame.CanGoBack)
            {
                Frame.GoBack();
            }
        }

        private void EditText_PreviewKeyDown(object sender, KeyRoutedEventArgs e)
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

        private void EditText_SelectionChanged(object sender, RoutedEventArgs e)
        {
            int Counter = 0;
            int ActualSelectionIndex = EditText.SelectionStart + EditText.SelectionLength;

            foreach (KeyValuePair<int, TextLineData> Data in LineData)
            {
                int NewCounter = Counter + Data.Value.LineText.Length + 1;

                if (NewCounter > ActualSelectionIndex)
                {
                    LineColumnDisplay.Text = $"{Globalization.GetString("LineDescription")} {Data.Key + 1}, {Globalization.GetString("ColumnDescription")} {ActualSelectionIndex - Counter + 1}";

                    if (EditText.SelectionLength > 0)
                    {
                        LineColumnDisplay.Text += $" ({Globalization.GetString("TextViewer_SelectedItem").Replace("{ItemNum}", EditText.SelectionLength.ToString())})";
                    }

                    break;
                }

                Counter = NewCounter;
            }
        }

        private Task<IReadOnlyDictionary<int, TextLineData>> CollectLineDataAsync(string Text)
        {
            return Task.Run<IReadOnlyDictionary<int, TextLineData>>(() =>
            {
                Dictionary<int, TextLineData> Result = new Dictionary<int, TextLineData>();

                int LineIndex = 0;
                StringBuilder Builder = new StringBuilder();

                for (int Index = 0; Index < Text.Length; Index++)
                {
                    switch (Text[Index])
                    {
                        case '\r':
                            {
                                if (Index + 1 < Text.Length)
                                {
                                    if (Text[Index + 1] == '\n')
                                    {
                                        Result.Add(LineIndex++, new TextLineData("\r\n", Builder.ToString()));
                                    }
                                    else
                                    {
                                        Result.Add(LineIndex++, new TextLineData("\r", Builder.ToString()));
                                    }
                                }
                                else
                                {
                                    Result.Add(LineIndex++, new TextLineData("\r", Builder.ToString()));
                                }

                                Builder.Clear();

                                break;
                            }
                        case '\n':
                            {
                                if (Index - 1 >= 0)
                                {
                                    if (Text[Index - 1] != '\r')
                                    {
                                        Result.Add(LineIndex++, new TextLineData("\n", Builder.ToString()));
                                    }
                                }
                                else
                                {
                                    Result.Add(LineIndex++, new TextLineData("\n", Builder.ToString()));
                                }

                                Builder.Clear();

                                break;
                            }
                        default:
                            {
                                Builder.Append(Text[Index]);
                                break;
                            }
                    }
                }

                if (Builder.Length > 0)
                {
                    Result.Add(LineIndex, new TextLineData(Result.Count > 0 ? string.Empty : "\r\n", Builder.ToString()));
                }

                return Result;
            });
        }
    }
}
