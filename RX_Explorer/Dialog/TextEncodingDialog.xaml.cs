using RX_Explorer.Class;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

// The Content Dialog item template is documented at https://go.microsoft.com/fwlink/?LinkId=234238

namespace RX_Explorer.Dialog
{
    public sealed partial class TextEncodingDialog : QueueContentDialog
    {
        private readonly ObservableCollection<Encoding> Encodings;
        private readonly FileSystemStorageFile TextFile;

        public Encoding UserSelectedEncoding { get; private set; }

        public TextEncodingDialog(FileSystemStorageFile TextFile, IEnumerable<Encoding> Encodings)
        {
            InitializeComponent();

            this.TextFile = TextFile;
            this.Encodings = new ObservableCollection<Encoding>(Encodings);

            Loading += TextEncodingDialog_Loading;
        }

        private async void TextEncodingDialog_Loading(FrameworkElement sender, object args)
        {
            Encoding DetectedEncoding = await DetectEncodingFromFileAsync();

            if (DetectedEncoding != null)
            {
                if (Encodings.FirstOrDefault((Enco) => Enco.CodePage == DetectedEncoding.CodePage) is Encoding Coding)
                {
                    EncodingCombo.SelectedItem = Coding;
                }
                else
                {
                    EncodingCombo.SelectedItem = Encodings.FirstOrDefault((Enco) => Enco.CodePage == Encoding.UTF8.CodePage);
                }
            }
            else
            {
                EncodingCombo.SelectedItem = Encodings.FirstOrDefault((Enco) => Enco.CodePage == Encoding.UTF8.CodePage);
            }
        }

        private void EncodingCombo_TextSubmitted(ComboBox sender, ComboBoxTextSubmittedEventArgs args)
        {
            try
            {
                if (Encodings.FirstOrDefault((Enco) => Enco.EncodingName == args.Text) is Encoding ExistCoding)
                {
                    if (UserSelectedEncoding.CodePage != ExistCoding.CodePage)
                    {
                        UserSelectedEncoding = ExistCoding;
                    }
                }
                else
                {
                    if (int.TryParse(args.Text, out int CodePage))
                    {
                        UserSelectedEncoding = Encoding.GetEncoding(CodePage);
                    }
                    else
                    {
                        UserSelectedEncoding = Encoding.GetEncoding(args.Text);
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

        private async Task<Encoding> DetectEncodingFromFileAsync()
        {
            try
            {
                using (FileStream DetectStream = await TextFile.GetFileStreamFromFileAsync(AccessMode.Read))
                {
                    return await Task.Run(() =>
                    {
                        using (StreamReader Reader = new StreamReader(DetectStream, Encoding.Default, true))
                        {
                            Reader.Read();

                            return Reader.CurrentEncoding;
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, "Could not detect the encoding of file");
                return null;
            }
        }

        private void EncodingCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (EncodingCombo.SelectedItem is Encoding Enco)
            {
                UserSelectedEncoding = Enco;
            }
        }
    }
}
