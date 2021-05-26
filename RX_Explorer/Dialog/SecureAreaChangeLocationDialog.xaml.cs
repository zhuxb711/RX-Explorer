using RX_Explorer.Class;
using System;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.UI.Xaml.Controls;

namespace RX_Explorer.Dialog
{
    public sealed partial class SecureAreaChangeLocationDialog : QueueContentDialog
    {
        private bool IsResetLocation;

        public SecureAreaChangeLocationDialog()
        {
            InitializeComponent();
            StorageLocation.Text = Convert.ToString(ApplicationData.Current.LocalSettings.Values["SecureAreaStorageLocation"]);
        }

        private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            if(!IsResetLocation)
            {
                args.Cancel = true;
                MustResetPathTip.IsOpen = true;
            }
        }

        private async void ChangeLocation_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            FolderPicker Picker = new FolderPicker
            {
                ViewMode = PickerViewMode.List,
                SuggestedStartLocation = PickerLocationId.ComputerFolder
            };
            Picker.FileTypeFilter.Add("*");

            if (await Picker.PickSingleFolderAsync() is StorageFolder Folder)
            {
                StorageLocation.Text = Folder.Path;
                ApplicationData.Current.LocalSettings.Values["SecureAreaStorageLocation"] = Folder.Path;
                IsResetLocation = true;
            }
        }
    }
}
