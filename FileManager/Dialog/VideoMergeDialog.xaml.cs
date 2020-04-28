using FileManager.Class;
using System;
using Windows.Media.Editing;
using Windows.Media.MediaProperties;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace FileManager.Dialog
{
    public sealed partial class VideoMergeDialog : QueueContentDialog
    {
        StorageFile SourceFile;
        StorageFile MergeFile;

        public MediaComposition Composition { get; private set; }

        public MediaEncodingProfile MediaEncoding { get; private set; }

        public string ExportFileType { get; private set; }

        public VideoMergeDialog(StorageFile SourceFile)
        {
            InitializeComponent();
            this.SourceFile = SourceFile;

            if (Globalization.Language == LanguageEnum.Chinese)
            {
                EncodingProfile.Items.Add("MP4编码(.mp4)");
                EncodingProfile.Items.Add("WMV编码(.wmv)");
                EncodingProfile.Items.Add("MKV编码(.mkv)");
            }
            else
            {
                EncodingProfile.Items.Add("MP4 Encoding(.mp4)");
                EncodingProfile.Items.Add("WMV Encoding(.wmv)");
                EncodingProfile.Items.Add("MKV Encoding(.mkv)");
            }

            EncodingQuality.Items.Add("2160p");
            EncodingQuality.Items.Add("1080p");
            EncodingQuality.Items.Add("720p");
            EncodingQuality.Items.Add("480p");

            EncodingProfile.SelectedIndex = 0;
            EncodingQuality.SelectedIndex = 0;

            Loading += VideoMergeDialog_Loading;
        }

        private async void VideoMergeDialog_Loading(FrameworkElement sender, object args)
        {
            SourceThumbnail.Source = await SourceFile.GetThumbnailBitmapAsync().ConfigureAwait(true);
            SourceFileName.Text = SourceFile.Name;
        }

        private async void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            var Deferral = args.GetDeferral();

            if (MergeFile == null)
            {
                EmptyTip.IsOpen = true;
                args.Cancel = true;
                Deferral.Complete();
                return;
            }

            VideoEncodingQuality Quality = default;

            switch (EncodingQuality.SelectedIndex)
            {
                case 0:
                    {
                        Quality = VideoEncodingQuality.Uhd2160p;
                        break;
                    }
                case 1:
                    {
                        Quality = VideoEncodingQuality.HD1080p;
                        break;
                    }
                case 2:
                    {
                        Quality = VideoEncodingQuality.HD720p;
                        break;
                    }
                case 3:
                    {
                        Quality = VideoEncodingQuality.Wvga;
                        break;
                    }
            }
            switch (EncodingProfile.SelectedIndex)
            {
                case 0:
                    {
                        MediaEncoding = MediaEncodingProfile.CreateMp4(Quality);
                        ExportFileType = ".mp4";
                        break;
                    }
                case 1:
                    {
                        MediaEncoding = MediaEncodingProfile.CreateWmv(Quality);
                        ExportFileType = ".wmv";
                        break;
                    }
                case 2:
                    {
                        MediaEncoding = MediaEncodingProfile.CreateHevc(Quality);
                        ExportFileType = ".mkv";
                        break;
                    }
            }

            MediaClip SourceVideoClip = await MediaClip.CreateFromFileAsync(SourceFile);
            MediaClip MergeVideoClip = await MediaClip.CreateFromFileAsync(MergeFile);
            Composition = new MediaComposition();
            Composition.Clips.Add(SourceVideoClip);
            Composition.Clips.Add(MergeVideoClip);

            Deferral.Complete();
        }

        private async void SelectClipButton_Click(object sender, RoutedEventArgs e)
        {
            FileOpenPicker Picker = new FileOpenPicker
            {
                SuggestedStartLocation = PickerLocationId.ComputerFolder,
                ViewMode = PickerViewMode.Thumbnail
            };
            Picker.FileTypeFilter.Add(".mp4");
            Picker.FileTypeFilter.Add(".wmv");
            if ((await Picker.PickSingleFileAsync()) is StorageFile MergeFile)
            {
                SelectClipButton.Visibility = Visibility.Collapsed;
                ClipThumbnail.Visibility = Visibility.Visible;
                this.MergeFile = MergeFile;
                ClipName.Text = MergeFile.Name;
                ClipThumbnail.Source = await MergeFile.GetThumbnailBitmapAsync().ConfigureAwait(false);
            }
        }
    }
}
