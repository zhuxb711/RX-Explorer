using System.Collections.Generic;
using System.Linq;
using Windows.Storage;
using Windows.UI.Xaml.Controls;

namespace FileManager
{
    public sealed partial class UserFolderDialog : ContentDialog
    {
        public UserFolderDialog(IEnumerable<StorageFolder> PotentialUsers)
        {
            InitializeComponent();
            UserCombo.ItemsSource = PotentialUsers;
            UserCombo.SelectedIndex = 0;
        }

        public StorageFolder Result { get; private set; }

        private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            Result = UserCombo.SelectedItem as StorageFolder;
        }
    }
}
