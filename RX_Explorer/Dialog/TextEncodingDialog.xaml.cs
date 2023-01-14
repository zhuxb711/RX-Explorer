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
        private readonly ObservableCollection<TextEncodingModel> AvailableEncodings = new ObservableCollection<TextEncodingModel>();
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
            Loaded += TextEncodingDialog_Loaded;
        }

        private async void TextEncodingDialog_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                IReadOnlyList<Encoding> AllEncodingList = await GetAllEncodingsAsync();

                AvailableEncodings.AddRange(AllEncodingList.Select((Encoding) => new TextEncodingModel(Encoding)));

                if (AvailableEncodings.Count == 0)
                {
                    AvailableEncodings.Add(new TextEncodingModel(Encoding.UTF8));
                }

                if (await DetectEncodingFromFileAsync() is Encoding DetectedEncoding)
                {
                    if (AvailableEncodings.FirstOrDefault((Model) => Model.TextEncoding.CodePage == DetectedEncoding.CodePage) is TextEncodingModel Model)
                    {
                        EncodingComboBox.SelectedItem = Model;
                    }
                    else
                    {
                        TextEncodingModel NewModel = new TextEncodingModel(DetectedEncoding);

                        int Index = Array.IndexOf(AvailableEncodings.Select((Model) => Model.TextEncoding).Append(DetectedEncoding).OrderByFastStringSortAlgorithm((Encoding) => Encoding.EncodingName, SortDirection.Ascending).ToArray(), DetectedEncoding);

                        if (Index >= 0 && Index <= AvailableEncodings.Count)
                        {
                            AvailableEncodings.Insert(Index, NewModel);
                        }
                        else
                        {
                            AvailableEncodings.Add(NewModel);
                        }

                        EncodingComboBox.SelectedItem = NewModel;
                    }
                }
                else
                {
                    EncodingComboBox.SelectedItem = AvailableEncodings.FirstOrDefault((Model) => Model.TextEncoding.CodePage == Encoding.UTF8.CodePage);
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

            return Encoding.GetEncodings().Select((Info) => Info.GetEncoding()).ToArray();
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
            if (e.AddedItems.SingleOrDefault() is TextEncodingModel Model)
            {
                UserSelectedEncoding = Model.TextEncoding;
            }
        }
    }
}
