using RX_Explorer.Class;
using SharedLibrary;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace RX_Explorer.Dialog
{
    public sealed partial class DecompressDialog : QueueContentDialog
    {
        public Encoding CurrentEncoding { get; private set; }

        private readonly ObservableCollection<Encoding> AvailableEncodings = new ObservableCollection<Encoding>();

        public string ExtractLocation { get; private set; }

        public DecompressDialog(string CurrentFolderPath, bool ShowEncodingOption = true)
        {
            InitializeComponent();

            LocationText.Text = CurrentFolderPath;

            if (ShowEncodingOption)
            {
                Loading += DecompressDialog_Loading;
            }
            else
            {
                EncodingOption.Visibility = Visibility.Collapsed;
            }
        }

        private async void DecompressDialog_Loading(FrameworkElement sender, object args)
        {
            try
            {
                using (AuxiliaryTrustProcessController.Exclusive Exclusive = await AuxiliaryTrustProcessController.GetControllerExclusiveAsync())
                {
                    AvailableEncodings.AddRange(await Exclusive.Controller.GetAllEncodingsAsync());
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, "Could not get all encodings, fallback to base encodings");
                AvailableEncodings.AddRange(Encoding.GetEncodings().Select((Info) => Info.GetEncoding()));
            }

            EncodingOption.IsEnabled = true;
            EncodingOption.SelectedItem = AvailableEncodings.FirstOrDefault((Enco) => Enco.CodePage == Encoding.UTF8.CodePage);
        }

        private async void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            ContentDialogButtonClickDeferral Deferral = args.GetDeferral();

            try
            {
                ExtractLocation = LocationText.Text;

                switch (await FileSystemStorageItemBase.OpenAsync(ExtractLocation))
                {
                    case FileSystemStorageFolder:
                        {
                            break;
                        }
                    case FileSystemStorageFile:
                        {
                            args.Cancel = true;
                            break;
                        }
                    default:
                        {
                            if (await FileSystemStorageItemBase.CreateNewAsync(ExtractLocation, CreateType.Folder, CreateOption.Skip) is not FileSystemStorageFolder)
                            {
                                args.Cancel = true;
                            }

                            break;
                        }
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"An exception was threw in the primiary button of {nameof(DecompressDialog)}");
            }
            finally
            {
                Deferral.Complete();
            }
        }

        private void EncodingOption_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (EncodingOption.SelectedItem is Encoding Encoding)
            {
                CurrentEncoding = Encoding;
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
                LocationText.Text = Folder.Path;
            }
        }
    }
}
