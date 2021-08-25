using RX_Explorer.Class;
using System;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace RX_Explorer.Dialog
{
    public sealed partial class NewCompressionItemPickerDialog : QueueContentDialog
    {
        public StorageFile PickedFile { get; private set; }

        public string NewName { get; private set; }

        private NewCompressionItemType ItemType;

        public NewCompressionItemPickerDialog(NewCompressionItemType ItemType)
        {
            InitializeComponent();

            if (ItemType == NewCompressionItemType.Directory)
            {
                PickFile.Visibility = Visibility.Collapsed;
            }

            this.ItemType = ItemType;
        }

        private async void PickFile_Click(object sender, RoutedEventArgs e)
        {
            FileOpenPicker Picker = new FileOpenPicker
            {
                SuggestedStartLocation = PickerLocationId.ComputerFolder,
                ViewMode = PickerViewMode.List
            };
            Picker.FileTypeFilter.Add("*");

            if (await Picker.PickSingleFileAsync() is StorageFile File)
            {
                PickedFile = File;
                NameBox.Text = File.Name;

                if (!string.IsNullOrWhiteSpace(NewName))
                {
                    IsPrimaryButtonEnabled = true;
                }
            }
        }

        private void NameBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            NewName = NameBox.Text;
            IsPrimaryButtonEnabled = !string.IsNullOrWhiteSpace(NewName) && (ItemType != NewCompressionItemType.File || PickedFile != null);
        }
    }
}
