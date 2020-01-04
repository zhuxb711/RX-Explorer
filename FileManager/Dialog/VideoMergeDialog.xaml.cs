using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace FileManager
{
    public sealed partial class VideoMergeDialog : QueueContentDialog
    {
        StorageFile File;

        public VideoMergeDialog(StorageFile File)
        {
            InitializeComponent();
            this.File = File;
            Loading += VideoMergeDialog_Loading;
        }

        private async void VideoMergeDialog_Loading(FrameworkElement sender, object args)
        {
            SourceThumbnail.Source = await File.GetThumbnailBitmapAsync();
            SourceFileName.Text = File.Name;
        }

        private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
        }

        private void SelectClipButton_Click(object sender, RoutedEventArgs e)
        {
            FileOpenPicker Picker = new FileOpenPicker
            {
                SuggestedStartLocation = PickerLocationId.ComputerFolder,
                ViewMode = PickerViewMode.Thumbnail
            };
            Picker.FileTypeFilter.Add("*.mp4");
            Picker.FileTypeFilter.Add("*.wmv");
        }
    }
}
