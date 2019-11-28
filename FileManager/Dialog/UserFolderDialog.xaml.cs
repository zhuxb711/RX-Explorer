using System;
using System.Collections.Generic;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.UI.Xaml.Controls;

namespace FileManager
{
    public sealed partial class UserFolderDialog : QueueContentDialog
    {
        public Dictionary<string, StorageFolder> FolderPair { get; private set; } = new Dictionary<string, StorageFolder>();

        public UserFolderDialog(IEnumerable<string> MissingFolderGroup)
        {
            InitializeComponent();
            UserCombo.ItemsSource = MissingFolderGroup;
            UserCombo.SelectedIndex = 0;
        }

        private void QueueContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            bool IsAllRefered = true;
            foreach (string Item in UserCombo.Items)
            {
                if (!FolderPair.ContainsKey(Item))
                {
                    IsAllRefered = false;
                    break;
                }
            }

            if (!IsAllRefered)
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

            StorageFolder Folder = await Picker.PickSingleFolderAsync();

            if (Folder != null)
            {
                string Key = UserCombo.SelectedItem.ToString();
                if (FolderPair.ContainsKey(Key))
                {
                    FolderPair.Remove(Key);
                    FolderPair.Add(Key, Folder);
                }
                else
                {
                    FolderPair.Add(Key, Folder);
                }
            }
        }
    }
}
