using RX_Explorer.Class;
using System;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace RX_Explorer.Dialog
{
    public sealed partial class LinkOptionsDialog : QueueContentDialog
    {
        public string Path { get; private set; }

        public string Argument { get; private set; }

        public string Description { get; private set; }

        public LinkOptionsDialog()
        {
            InitializeComponent();
        }

        private async void BrowserFileButton_Click(object sender, RoutedEventArgs e)
        {
            FileOpenPicker Picker = new FileOpenPicker
            {
                SuggestedStartLocation = PickerLocationId.Desktop,
                ViewMode = PickerViewMode.List
            };
            Picker.FileTypeFilter.Add("*");

            if (await Picker.PickSingleFileAsync() is StorageFile File)
            {
                TargetPath.Text = File.Path;
            }
        }

        private async void BrowserFolderButton_Click(object sender, RoutedEventArgs e)
        {
            FolderPicker Picker = new FolderPicker
            {
                SuggestedStartLocation = PickerLocationId.Desktop,
                ViewMode = PickerViewMode.List
            };
            Picker.FileTypeFilter.Add("*");

            if (await Picker.PickSingleFolderAsync() is StorageFolder Folder)
            {
                TargetPath.Text = Folder.Path;
            }
        }

        private void QueueContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            if (string.IsNullOrWhiteSpace(TargetPath.Text))
            {
                args.Cancel = true;
                EmptyTip.IsOpen = true;
            }
            else
            {
                Path = TargetPath.Text;

                if (!string.IsNullOrWhiteSpace(LinkArgument.Text))
                {
                    Argument = LinkArgument.Text;
                }

                if (!string.IsNullOrWhiteSpace(LinkDescription.Text))
                {
                    Description = LinkDescription.Text;
                }
            }
        }
    }
}
