using RX_Explorer.Class;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace RX_Explorer.Dialog
{
    public sealed partial class TextEncodingDialog : QueueContentDialog
    {
        private readonly ObservableCollection<Encoding> AvailableEncodings = new ObservableCollection<Encoding>();
        private readonly FileSystemStorageFile TextFile;

        public Encoding UserSelectedEncoding { get; private set; }

        public TextEncodingDialog(FileSystemStorageFile TextFile) : this()
        {
            this.TextFile = TextFile;
        }

        public TextEncodingDialog()
        {
            InitializeComponent();
            Loading += TextEncodingDialog_Loading;
        }

        private async void TextEncodingDialog_Loading(FrameworkElement sender, object args)
        {
            AvailableEncodings.AddRange(await GetAllEncodingsAsync());

            Encoding DetectedEncoding = await DetectEncodingFromFileAsync();

            if (DetectedEncoding != null)
            {
                if (AvailableEncodings.FirstOrDefault((Enco) => Enco.CodePage == DetectedEncoding.CodePage) is Encoding Coding)
                {
                    EncodingCombo.SelectedItem = Coding;
                }
                else
                {
                    EncodingCombo.SelectedItem = AvailableEncodings.FirstOrDefault((Enco) => Enco.CodePage == Encoding.UTF8.CodePage);
                }
            }
            else
            {
                EncodingCombo.SelectedItem = AvailableEncodings.FirstOrDefault((Enco) => Enco.CodePage == Encoding.UTF8.CodePage);
            }
        }

        private async Task<IReadOnlyList<Encoding>> GetAllEncodingsAsync()
        {
            try
            {
                using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableControllerAsync())
                {
                    return await Exclusive.Controller.GetAllEncodingsAsync();
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, "Could not get all encodings, fallback to base encodings");
                return Encoding.GetEncodings().Select((Info) => Info.GetEncoding()).ToList();
            }
        }

        private async Task<Encoding> DetectEncodingFromFileAsync()
        {
            if (TextFile != null)
            {
                try
                {
                    using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableControllerAsync())
                    {
                        return await Exclusive.Controller.DetectEncodingAsync(TextFile.Path);
                    }
                }
                catch (Exception ex)
                {
                    LogTracer.Log(ex, "Could not detect the encoding of file");
                }
            }

            return null;
        }

        private void EncodingCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (EncodingCombo.SelectedItem is Encoding Encoding)
            {
                UserSelectedEncoding = Encoding;
            }
        }
    }
}
