using RX_Explorer.Class;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace RX_Explorer.Dialog
{
    public sealed partial class DecompressDialog : QueueContentDialog, INotifyPropertyChanged
    {
        public Encoding CurrentEncoding { get; private set; } = Encoding.Default;

        private readonly ObservableCollection<Encoding> AvailableEncoding;

        public event PropertyChangedEventHandler PropertyChanged;

        public string ExtractLocation { get; private set; }

        public DecompressDialog(string CurrentFolderPath)
        {
            InitializeComponent();

            ExtractLocation = CurrentFolderPath;
            AvailableEncoding = new ObservableCollection<Encoding>();

            EncodingOption.ItemsSource = AvailableEncoding;

            Loading += DecompressDialog_Loading;
        }

        private void DecompressDialog_Loading(FrameworkElement sender, object args)
        {
            try
            {
                Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

                foreach (EncodingInfo Enco in Encoding.GetEncodings())
                {
                    if (Enco.CodePage == Encoding.Default.CodePage)
                    {
                        AvailableEncoding.Insert(0, Enco.GetEncoding());
                    }
                    else
                    {
                        AvailableEncoding.Add(Enco.GetEncoding());
                    }
                }

                AvailableEncoding.Add(Encoding.GetEncoding(936));

                EncodingOption.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex);
            }
        }

        private async void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            var Deferral = args.GetDeferral();

            try
            {
                if (CurrentEncoding == null)
                {
                    args.Cancel = true;
                    InvalidTip.IsOpen = true;
                }
                else if (await FileSystemStorageItemBase.OpenAsync(ExtractLocation) is not FileSystemStorageFolder)
                {
                    if(await FileSystemStorageItemBase.CheckExistAsync(ExtractLocation) == false)
                    {
                        await FileSystemStorageItemBase.CreateNewAsync(ExtractLocation,StorageItemTypes.Folder,CreateOption.OpenIfExist).ConfigureAwait(false);
                    }
                    else 
                    { 
                        args.Cancel = true;
                    }
                }
            }
            catch(Exception ex)
            {
                LogTracer.Log(ex);
            }
            finally
            {
                Deferral.Complete();
            }
        }

        private void EncodingOption_TextSubmitted(ComboBox sender, ComboBoxTextSubmittedEventArgs args)
        {
            try
            {
                if (AvailableEncoding.FirstOrDefault((Enco) => Enco.EncodingName == args.Text) is Encoding ExistCoding)
                {
                    CurrentEncoding = ExistCoding;
                }
                else
                {
                    if (int.TryParse(args.Text, out int CodePage))
                    {
                        CurrentEncoding = Encoding.GetEncoding(CodePage);
                    }
                    else
                    {
                        CurrentEncoding = Encoding.GetEncoding(args.Text);
                    }
                }

                args.Handled = false;
            }
            catch
            {
                CurrentEncoding = null;
                InvalidTip.IsOpen = true;
                args.Handled = true;
            }
        }

        private void EncodingOption_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (EncodingOption.SelectedIndex > 0 && EncodingOption.SelectedIndex < AvailableEncoding.Count)
            {
                CurrentEncoding = EncodingOption.SelectedItem as Encoding;
            }
        }

        private async void SelectLocationButton_Click(object sender, RoutedEventArgs e)
        {
            FolderPicker Picker = new FolderPicker
            {
                SuggestedStartLocation = PickerLocationId.ComputerFolder,
                ViewMode = PickerViewMode.List
            };

            Picker.FileTypeFilter.Add("*");

            if (await Picker.PickSingleFolderAsync() is StorageFolder Folder)
            {
                ExtractLocation = Folder.Path;
                OnPropertyChanged(nameof(ExtractLocation));
            }
        }

        private void OnPropertyChanged([CallerMemberName] string PropertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(PropertyName));
        }
    }
}
