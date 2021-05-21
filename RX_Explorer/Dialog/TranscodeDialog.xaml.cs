using RX_Explorer.Class;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace RX_Explorer.Dialog
{
    public sealed partial class TranscodeDialog : QueueContentDialog
    {
        private StorageFile SourceFile;

        public string MediaTranscodeEncodingProfile
        {
            get
            {
                string FormatText = Globalization.GetString("Transcode_Dialog_Format_Text");

                return (Format.SelectedItem.ToString().Replace(FormatText, string.Empty).Trim()) switch
                {
                    "MKV(.mkv)" => "MKV",
                    "AVI(.avi)" => "AVI",
                    "WMV(.wmv)" => "WMV",
                    "MP4(.mp4)" => "MP4",
                    "ALAC(.alac)" => "ALAC",
                    "AAC(.m4a)" => "M4A",
                    "WMA(.wma)" => "WMA",
                    "MP3(.mp3)" => "MP3",
                    _ => string.Empty,
                };
            }
        }

        public string MediaTranscodeQuality
        {
            get
            {
                return Quality.SelectedItem.ToString();
            }
        }

        public bool SpeedUp
        {
            get
            {
                return SpeedUpEnable.IsChecked.GetValueOrDefault();
            }
        }

        public TranscodeDialog(StorageFile SourceFile)
        {
            InitializeComponent();
            Loaded += TranscodeDialog_Loaded;
            this.SourceFile = SourceFile;
        }

        private void TranscodeDialog_Loaded(object sender, RoutedEventArgs e)
        {
            string FormatText = Globalization.GetString("Transcode_Dialog_Format_Text");

            switch (SourceFile.FileType.ToLower())
            {
                case ".mp4":
                    {
                        Format.Items.Add($"MP4(.mp4) {FormatText}");
                        Format.Items.Add($"MKV(.mkv) {FormatText}");
                        Format.Items.Add($"AVI(.avi) {FormatText}");
                        Format.Items.Add($"WMV(.wmv) {FormatText}");
                        Quality.Items.Add("UHD2160p");
                        Quality.Items.Add("HD1080p");
                        Quality.Items.Add("HD720p");
                        Quality.Items.Add("WVGA");
                        Quality.Items.Add("VGA");
                        Quality.Items.Add("QVGA");
                        break;
                    }
                case ".mkv":
                    {
                        Format.Items.Add($"MP4(.mp4) {FormatText}");
                        Format.Items.Add($"AVI(.avi) {FormatText}");
                        Format.Items.Add($"WMV(.wmv) {FormatText}");
                        Quality.Items.Add("UHD2160p");
                        Quality.Items.Add("HD1080p");
                        Quality.Items.Add("HD720p");
                        Quality.Items.Add("WVGA");
                        Quality.Items.Add("VGA");
                        Quality.Items.Add("QVGA");
                        break;
                    }
                case ".avi":
                    {
                        Format.Items.Add($"MKV(.mkv) {FormatText}");
                        Format.Items.Add($"MP4(.mp4) {FormatText}");
                        Format.Items.Add($"WMV(.wmv) {FormatText}");
                        Quality.Items.Add("UHD2160p");
                        Quality.Items.Add("HD1080p");
                        Quality.Items.Add("HD720p");
                        Quality.Items.Add("WVGA");
                        Quality.Items.Add("VGA");
                        Quality.Items.Add("QVGA");
                        break;
                    }
                case ".wmv":
                    {
                        Format.Items.Add($"WMV(.wmv) {FormatText}");
                        Format.Items.Add($"MKV(.mkv) {FormatText}");
                        Format.Items.Add($"MP4(.mp4) {FormatText}");
                        Format.Items.Add($"AVI(.avi) {FormatText}");
                        Quality.Items.Add("UHD2160p");
                        Quality.Items.Add("HD1080p");
                        Quality.Items.Add("HD720p");
                        Quality.Items.Add("WVGA");
                        Quality.Items.Add("VGA");
                        Quality.Items.Add("QVGA");
                        break;
                    }
                case ".mov":
                    {
                        Format.Items.Add($"MKV(.mkv) {FormatText}");
                        Format.Items.Add($"MP4(.mp4) {FormatText}");
                        Format.Items.Add($"AVI(.avi) {FormatText}");
                        Format.Items.Add($"WMV(.wmv) {FormatText}");
                        Quality.Items.Add("UHD2160p");
                        Quality.Items.Add("HD1080p");
                        Quality.Items.Add("HD720p");
                        Quality.Items.Add("WVGA");
                        Quality.Items.Add("VGA");
                        Quality.Items.Add("QVGA");
                        break;
                    }
                case ".flac":
                    {
                        Format.Items.Add($"ALAC(.alac) {FormatText}");
                        Format.Items.Add($"AAC(.m4a) {FormatText}");
                        Format.Items.Add($"MP3(.mp3) {FormatText}");
                        Format.Items.Add($"WMA(.wma) {FormatText}");
                        Quality.Items.Add("High");
                        Quality.Items.Add("Medium");
                        Quality.Items.Add("Low");
                        break;
                    }
                case ".alac":
                    {
                        Format.Items.Add($"AAC(.m4a) {FormatText}");
                        Format.Items.Add($"MP3(.mp3) {FormatText}");
                        Format.Items.Add($"WMA(.wma) {FormatText}");
                        Quality.Items.Add("High");
                        Quality.Items.Add("Medium");
                        Quality.Items.Add("Low");
                        break;
                    }
                case ".m4a":
                    {
                        Format.Items.Add($"ALAC(.alac) {FormatText}");
                        Format.Items.Add($"MP3(.mp3) {FormatText}");
                        Format.Items.Add($"WMA(.wma) {FormatText}");
                        Quality.Items.Add("High");
                        Quality.Items.Add("Medium");
                        Quality.Items.Add("Low");
                        break;
                    }
                case ".mp3":
                    {
                        Format.Items.Add($"ALAC(.alac) {FormatText}");
                        Format.Items.Add($"AAC(.m4a) {FormatText}");
                        Format.Items.Add($"WMA(.wma) {FormatText}");
                        Quality.Items.Add("High");
                        Quality.Items.Add("Medium");
                        Quality.Items.Add("Low");
                        break;
                    }
                case ".wma":
                    {
                        Format.Items.Add($"ALAC(.alac) {FormatText}");
                        Format.Items.Add($"AAC(.m4a) {FormatText}");
                        Format.Items.Add($"MP3(.mp3) {FormatText}");
                        Quality.Items.Add("High");
                        Quality.Items.Add("Medium");
                        Quality.Items.Add("Low");
                        break;
                    }
            }

            Format.SelectedIndex = 0;
            Quality.SelectedIndex = 0;
        }

        private void Quality_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            switch (Quality.SelectedItem.ToString())
            {
                case "UHD2160p":
                    Info.Text = $"{Globalization.GetString("FileProperty_Resolution")}3840 X 2160，30FPS";
                    break;
                case "HD1080p":
                    Info.Text = $"{Globalization.GetString("FileProperty_Resolution")}分辨率1920 X 1080，30FPS";
                    break;
                case "HD720p":
                    Info.Text = $"{Globalization.GetString("FileProperty_Resolution")}1280 X 720，30FPS";
                    break;
                case "WVGA":
                    Info.Text = $"{Globalization.GetString("FileProperty_Resolution")}800 X 480，30FPS";
                    break;
                case "VGA":
                    Info.Text = $"{Globalization.GetString("FileProperty_Resolution")}640 X 480，30FPS";
                    break;
                case "QVGA":
                    Info.Text = $"{Globalization.GetString("FileProperty_Resolution")}320 X 240，30FPS";
                    break;
                case "High":
                    Info.Text = $"{Globalization.GetString("FileProperty_Bitrate")}192kbps，{Globalization.GetString("Transcode_Simaple_Rate_Text")}48khz";
                    break;
                case "Medium":
                    Info.Text = $"{Globalization.GetString("FileProperty_Bitrate")}128kbps，{Globalization.GetString("Transcode_Simaple_Rate_Text")}44.1khz";
                    break;
                case "Low":
                    Info.Text = $"{Globalization.GetString("FileProperty_Bitrate")}96kbps，{Globalization.GetString("Transcode_Simaple_Rate_Text")}44.1khz";
                    break;
            }
        }
    }
}
