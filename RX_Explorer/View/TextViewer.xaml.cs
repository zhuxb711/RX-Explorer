using RX_Explorer.Class;
using System;
using System.Collections.Generic;
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
        private FileSystemStorageItemBase TextFile;

        private Encoding CurrentEncoding;

        private readonly ObservableCollection<Encoding> AvailableEncoding = new ObservableCollection<Encoding>();

        public TextViewer()
        {
            InitializeComponent();
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            Loaded += TextViewer_Loaded;

            try
            {
                if (Globalization.CurrentLanguage == LanguageEnum.Chinese_Simplified)
                {
                    AvailableEncoding.Add(Encoding.GetEncoding("GBK"));
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, "Could not load GBK encoding");
            }

            foreach (Encoding Coding in Encoding.GetEncodings().Select((Info) => Info.GetEncoding()))
            {
                AvailableEncoding.Add(Coding);
            }
        }

        private async void TextViewer_Loaded(object sender, RoutedEventArgs e)
        {
            LoadingControl.IsLoading = true;
            await Initialize().ConfigureAwait(true);
            await Task.Delay(500).ConfigureAwait(true);
            LoadingControl.IsLoading = false;
        }

        private async Task Initialize()
        {
            Encoding DetectedEncoding = await DetectEncodingFromFileAsync().ConfigureAwait(true);

            if (DetectedEncoding != null)
            {
                if (AvailableEncoding.FirstOrDefault((Enco) => Enco.CodePage == DetectedEncoding.CodePage) is Encoding Coding)
                {
                    EncodingProfile.SelectedItem = Coding;
                }
                else
                {
                    EncodingProfile.SelectedItem = AvailableEncoding.FirstOrDefault((Enco) => Enco.CodePage == Encoding.UTF8.CodePage);
                }
            }
            else
            {
                EncodingProfile.SelectedItem = AvailableEncoding.FirstOrDefault((Enco) => Enco.CodePage == Encoding.UTF8.CodePage);
            }
        }

        private Task<Encoding> DetectEncodingFromFileAsync()
        {
            return Task.Run(() =>
            {
                try
                {
                    using (FileStream DetectStream = TextFile.GetFileStreamFromFile(AccessMode.Read))
                    using (StreamReader Reader = new StreamReader(DetectStream, Encoding.Default, true))
                    {
                        Reader.Read();

                        return Reader.CurrentEncoding;
                    }
                }
                catch (Exception ex)
                {
                    LogTracer.Log(ex, "Could not detect the encoding of file");
                    return null;
                }
            });
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            if (e?.Parameter is FileSystemStorageItemBase Parameters)
            {
                TextFile = Parameters;
                Title.Text = TextFile.Name;
            }
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            TextFile = null;
            Text.Text = string.Empty;
            EncodingProfile.SelectedIndex = -1;
        }

        private async void Save_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (CurrentEncoding != null)
                {
                    try
                    {
                        if (await FileSystemStorageItemBase.CreateAsync(TextFile.Path, StorageItemTypes.File, CreateOption.ReplaceExisting).ConfigureAwait(true) is FileSystemStorageItemBase Item)
                        {
                            using (FileStream Stream = Item.GetFileStreamFromFile(AccessMode.Write))
                            using (StreamWriter Writer = new StreamWriter(Stream, CurrentEncoding))
                            {
                                await Writer.WriteAsync(Text.Text).ConfigureAwait(true);
                            }
                        }
                        else
                        {
                            throw new FileNotFoundException();
                        }
                    }
                    catch
                    {
                        QueueContentDialog Dialog = new QueueContentDialog
                        {
                            Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                            Content = Globalization.GetString("QueueDialog_CouldReadWriteFile_Content"),
                            CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                        };

                        await Dialog.ShowAsync().ConfigureAwait(true);
                    }

                    Frame.GoBack();
                }
                else
                {
                    InvalidTip.IsOpen = true;
                }
            }
            catch
            {
                InvalidTip.IsOpen = true;
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            Frame.GoBack();
        }

        private async void EncodingProfile_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (EncodingProfile.SelectedItem is Encoding Coding)
            {
                CurrentEncoding = Coding;
                await LoadTextWithEncoding(CurrentEncoding).ConfigureAwait(false);
            }
        }

        private void EncodingProfile_TextSubmitted(ComboBox sender, ComboBoxTextSubmittedEventArgs args)
        {
            try
            {
                if (AvailableEncoding.FirstOrDefault((Enco) => Enco.EncodingName == args.Text) is Encoding ExistCoding)
                {
                    if (CurrentEncoding != ExistCoding)
                    {
                        CurrentEncoding = ExistCoding;
                        _ = LoadTextWithEncoding(CurrentEncoding);
                    }
                }
                else
                {
                    if (int.TryParse(args.Text, out int CodePage))
                    {
                        CurrentEncoding = Encoding.GetEncoding(CodePage);
                        _ = LoadTextWithEncoding(CurrentEncoding);
                    }
                    else
                    {
                        CurrentEncoding = Encoding.GetEncoding(args.Text);
                        _ = LoadTextWithEncoding(CurrentEncoding);
                    }
                }

                args.Handled = false;
            }
            catch
            {
                args.Handled = true;
                InvalidTip.IsOpen = true;
            }
        }

        private async Task LoadTextWithEncoding(Encoding Enco)
        {
            LoadingControl.IsLoading = true;

            try
            {
                using (FileStream Stream = TextFile.GetFileStreamFromFile(AccessMode.Read))
                using (StreamReader Reader = new StreamReader(Stream, Enco, false))
                {
                    Text.Text = await Reader.ReadToEndAsync().ConfigureAwait(true);
                }
            }
            catch
            {
                QueueContentDialog Dialog = new QueueContentDialog
                {
                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                    Content = Globalization.GetString("QueueDialog_CouldReadWriteFile_Content"),
                    CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                };

                await Dialog.ShowAsync().ConfigureAwait(true);
            }

            await Task.Delay(500).ConfigureAwait(true);
            LoadingControl.IsLoading = false;
        }
    }
}
