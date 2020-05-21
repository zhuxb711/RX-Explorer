using RX_Explorer.Class;
using System;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.UI.Xaml.Controls;

namespace RX_Explorer.Dialog
{
    public sealed partial class UserFolderDialog : QueueContentDialog
    {
        public StorageFolder MissingFolder { get; private set; }

        public UserFolderDialog(string MissingFolder)
        {
            InitializeComponent();
            UserCombo.Items.Add(MissingFolder);
            UserCombo.SelectedIndex = 0;
        }

        private void QueueContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            if (MissingFolder == null)
            {
                NotReferTip.IsOpen = true;
                args.Cancel = true;
            }
        }

        private async void ReferButton_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            FolderPicker Picker = new FolderPicker
            {
                SuggestedStartLocation = PickerLocationId.ComputerFolder,
                ViewMode = PickerViewMode.Thumbnail
            };
            Picker.FileTypeFilter.Add("*");

            MissingFolder = await Picker.PickSingleFolderAsync();
        }
    }
}
