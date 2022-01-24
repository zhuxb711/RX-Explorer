using RX_Explorer.Class;
using System;
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
                using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableControllerAsync())
                {
                    AvailableEncodings.AddRange(await Exclusive.Controller.GetAllEncodingsAsync());
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, "Could not get all encodings, fallback to base encodings");
                AvailableEncodings.AddRange(Encoding.GetEncodings().Select((Info) => Info.GetEncoding()));
            }

            EncodingOption.SelectedItem = AvailableEncodings.FirstOrDefault((Enco) => Enco.CodePage == Encoding.UTF8.CodePage);
        }

        private async void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            ContentDialogButtonClickDeferral Deferral = args.GetDeferral();

            try
            {
                ExtractLocation = LocationText.Text;

                if (await FileSystemStorageItemBase.OpenAsync(ExtractLocation) is not FileSystemStorageFolder)
                {
                    if (await FileSystemStorageItemBase.CheckExistsAsync(ExtractLocation) == false)
                    {
                        await FileSystemStorageItemBase.CreateNewAsync(ExtractLocation, StorageItemTypes.Folder, CreateOption.OpenIfExist).ConfigureAwait(false);
                    }
                    else
                    {
                        args.Cancel = true;
                    }
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex);
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
