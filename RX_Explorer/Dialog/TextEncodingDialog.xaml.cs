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
        private Encoding userSelectedEncoding;
        public Encoding UserSelectedEncoding
        {
            get => userSelectedEncoding ?? Encoding.UTF8;
            private set => userSelectedEncoding = value;
        }

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
            try
            {
                AvailableEncodings.AddRange(await GetAllEncodingsAsync());

                if (AvailableEncodings.Count == 0)
                {
                    AvailableEncodings.Add(Encoding.UTF8);
                }

                if (await DetectEncodingFromFileAsync() is Encoding DetectedEncoding)
                {
                    if (AvailableEncodings.FirstOrDefault((Enco) => Enco.CodePage == DetectedEncoding.CodePage) is Encoding Coding)
                    {
                        EncodingComboBox.SelectedItem = Coding;
                    }
                    else
                    {
                        int Index = AvailableEncodings.Append(DetectedEncoding).OrderByFastStringSortAlgorithm((Encoding) => Encoding.EncodingName, SortDirection.Ascending).ToList().IndexOf(DetectedEncoding);

                        if (Index >= 0 && Index <= AvailableEncodings.Count)
                        {
                            AvailableEncodings.Insert(Index, DetectedEncoding);
                        }
                        else
                        {
                            AvailableEncodings.Add(DetectedEncoding);
                        }

                        EncodingComboBox.SelectedItem = DetectedEncoding;
                    }
                }
                else
                {
                    EncodingComboBox.SelectedItem = AvailableEncodings.FirstOrDefault((Enco) => Enco.CodePage == Encoding.UTF8.CodePage);
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, "Unexpected exception was threw in loading the text encoding dialog");
            }
            finally
            {
                EncodingComboBox.IsEnabled = true;
            }
        }

        private async Task<IReadOnlyList<Encoding>> GetAllEncodingsAsync()
        {
            try
            {
                using (AuxiliaryTrustProcessController.Exclusive Exclusive = await AuxiliaryTrustProcessController.GetControllerExclusiveAsync(Priority: PriorityLevel.High))
                {
                    return await Exclusive.Controller.GetAllEncodingsAsync();
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, "Could not get all encodings, fallback to base encodings");
            }

            return Encoding.GetEncodings().Select((Info) => Info.GetEncoding()).ToList();
        }

        private async Task<Encoding> DetectEncodingFromFileAsync()
        {
            if (TextFile != null)
            {
                try
                {
                    if (TextFile.Size > 0)
                    {
                        using (AuxiliaryTrustProcessController.Exclusive Exclusive = await AuxiliaryTrustProcessController.GetControllerExclusiveAsync(Priority: PriorityLevel.High))
                        {
                            return await Exclusive.Controller.DetectEncodingAsync(TextFile.Path);
                        }
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
            if (e.AddedItems.SingleOrDefault() is Encoding Encoding)
            {
                UserSelectedEncoding = Encoding;
            }
        }
    }
}
