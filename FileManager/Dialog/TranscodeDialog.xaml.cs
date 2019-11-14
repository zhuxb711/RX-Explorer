using System.IO;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace FileManager
{
    public sealed partial class TranscodeDialog : QueueContentDialog
    {
        private StorageFile SourceFile;

        public string MediaTranscodeEncodingProfile
        {
            get
            {
                if (MainPage.ThisPage.CurrentLanguage == LanguageEnum.Chinese)
                {
                    switch (Format.SelectedItem.ToString())
                    {
                        case "MKV(.mkv)格式":
                            return "MKV";
                        case "AVI(.avi)格式":
                            return "AVI";
                        case "WMV(.wmv)格式":
                            return "WMV";
                        case "MP4(.mp4)格式":
                            return "MP4";
                        case "ALAC(.alac)格式":
                            return "ALAC";
                        case "AAC(.m4a)格式":
                            return "M4A";
                        case "WMA(.wma)格式":
                            return "WMA";
                        case "MP3(.mp3)格式":
                            return "MP3";
                    }
                }
                else
                {
                    switch (Format.SelectedItem.ToString())
                    {
                        case "MKV(.mkv)":
                            return "MKV";
                        case "AVI(.avi)":
                            return "AVI";
                        case "WMV(.wmv)":
                            return "WMV";
                        case "MP4(.mp4)":
                            return "MP4";
                        case "ALAC(.alac)":
                            return "ALAC";
                        case "AAC(.m4a)":
                            return "M4A";
                        case "WMA(.wma)":
                            return "WMA";
                        case "MP3(.mp3)":
                            return "MP3";
                    }
                }
                return string.Empty;
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
            if (MainPage.ThisPage.CurrentLanguage == LanguageEnum.Chinese)
            {
                switch (SourceFile.FileType)
                {
                    case ".mp4":
                        {
                            Format.Items.Add("MKV(.mkv)格式");
                            Format.Items.Add("AVI(.avi)格式");
                            Format.Items.Add("WMV(.wmv)格式");
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
                            Format.Items.Add("MP4(.mp4)格式");
                            Format.Items.Add("AVI(.avi)格式");
                            Format.Items.Add("WMV(.wmv)格式");
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
                            Format.Items.Add("MKV(.mkv)格式");
                            Format.Items.Add("MP4(.mp4)格式");
                            Format.Items.Add("WMV(.wmv)格式");
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
                            Format.Items.Add("MKV(.mkv)格式");
                            Format.Items.Add("MP4(.mp4)格式");
                            Format.Items.Add("AVI(.avi)格式");
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
                            Format.Items.Add("MKV(.mkv)格式");
                            Format.Items.Add("MP4(.mp4)格式");
                            Format.Items.Add("AVI(.avi)格式");
                            Format.Items.Add("WMV(.wmv)格式");
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
                            Format.Items.Add("ALAC(.alac)格式");
                            Format.Items.Add("AAC(.m4a)格式");
                            Format.Items.Add("MP3(.mp3)格式");
                            Format.Items.Add("WMA(.wma)格式");
                            Quality.Items.Add("High");
                            Quality.Items.Add("Medium");
                            Quality.Items.Add("Low");
                            break;
                        }
                    case ".alac":
                        {
                            Format.Items.Add("AAC(.m4a)格式");
                            Format.Items.Add("MP3(.mp3)格式");
                            Format.Items.Add("WMA(.wma)格式");
                            Quality.Items.Add("High");
                            Quality.Items.Add("Medium");
                            Quality.Items.Add("Low");
                            break;
                        }
                    case ".m4a":
                        {
                            Format.Items.Add("ALAC(.alac)格式");
                            Format.Items.Add("MP3(.mp3)格式");
                            Format.Items.Add("WMA(.wma)格式");
                            Quality.Items.Add("High");
                            Quality.Items.Add("Medium");
                            Quality.Items.Add("Low");
                            break;
                        }
                    case ".mp3":
                        {
                            Format.Items.Add("ALAC(.alac)格式");
                            Format.Items.Add("AAC(.m4a)格式");
                            Format.Items.Add("WMA(.wma)格式");
                            Quality.Items.Add("High");
                            Quality.Items.Add("Medium");
                            Quality.Items.Add("Low");
                            break;
                        }
                    case ".wma":
                        {
                            Format.Items.Add("ALAC(.alac)格式");
                            Format.Items.Add("AAC(.m4a)格式");
                            Format.Items.Add("MP3(.mp3)格式");
                            Quality.Items.Add("High");
                            Quality.Items.Add("Medium");
                            Quality.Items.Add("Low");
                            break;
                        }
                }
            }
            else
            {
                switch (SourceFile.FileType)
                {
                    case ".mp4":
                        {
                            Format.Items.Add("MKV(.mkv)");
                            Format.Items.Add("AVI(.avi)");
                            Format.Items.Add("WMV(.wmv)");
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
                            Format.Items.Add("MP4(.mp4)");
                            Format.Items.Add("AVI(.avi)");
                            Format.Items.Add("WMV(.wmv)");
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
                            Format.Items.Add("MKV(.mkv)");
                            Format.Items.Add("MP4(.mp4)");
                            Format.Items.Add("WMV(.wmv)");
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
                            Format.Items.Add("MKV(.mkv)");
                            Format.Items.Add("MP4(.mp4)");
                            Format.Items.Add("AVI(.avi)");
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
                            Format.Items.Add("MKV(.mkv)");
                            Format.Items.Add("MP4(.mp4)");
                            Format.Items.Add("AVI(.avi)");
                            Format.Items.Add("WMV(.wmv)");
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
                            Format.Items.Add("ALAC(.alac)");
                            Format.Items.Add("AAC(.m4a)");
                            Format.Items.Add("MP3(.mp3)");
                            Format.Items.Add("WMA(.wma)");
                            Quality.Items.Add("High");
                            Quality.Items.Add("Medium");
                            Quality.Items.Add("Low");
                            break;
                        }
                    case ".alac":
                        {
                            Format.Items.Add("AAC(.m4a)");
                            Format.Items.Add("MP3(.mp3)");
                            Format.Items.Add("WMA(.wma)");
                            Quality.Items.Add("High");
                            Quality.Items.Add("Medium");
                            Quality.Items.Add("Low");
                            break;
                        }
                    case ".m4a":
                        {
                            Format.Items.Add("ALAC(.alac)");
                            Format.Items.Add("MP3(.mp3)");
                            Format.Items.Add("WMA(.wma)");
                            Quality.Items.Add("High");
                            Quality.Items.Add("Medium");
                            Quality.Items.Add("Low");
                            break;
                        }
                    case ".mp3":
                        {
                            Format.Items.Add("ALAC(.alac)");
                            Format.Items.Add("AAC(.m4a)");
                            Format.Items.Add("WMA(.wma)");
                            Quality.Items.Add("High");
                            Quality.Items.Add("Medium");
                            Quality.Items.Add("Low");
                            break;
                        }
                    case ".wma":
                        {
                            Format.Items.Add("ALAC(.alac)");
                            Format.Items.Add("AAC(.m4a)");
                            Format.Items.Add("MP3(.mp3)");
                            Quality.Items.Add("High");
                            Quality.Items.Add("Medium");
                            Quality.Items.Add("Low");
                            break;
                        }
                }
            }
            Format.SelectedIndex = 0;
            Quality.SelectedIndex = 0;
        }

        private void Quality_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (MainPage.ThisPage.CurrentLanguage == LanguageEnum.Chinese)
            {
                switch (Quality.SelectedItem.ToString())
                {
                    case "UHD2160p":
                        Info.Text = "分辨率3840 X 2160，30FPS";
                        break;
                    case "HD1080p":
                        Info.Text = "分辨率1920 X 1080，30FPS";
                        break;
                    case "HD720p":
                        Info.Text = "分辨率1280 X 720，30FPS";
                        break;
                    case "WVGA":
                        Info.Text = "分辨率800 X 480，30FPS";
                        break;
                    case "VGA":
                        Info.Text = "分辨率640 X 480，30FPS";
                        break;
                    case "QVGA":
                        Info.Text = "分辨率320 X 240，30FPS";
                        break;
                    case "High":
                        Info.Text = "比特率192kbps，采样率48khz";
                        break;
                    case "Medium":
                        Info.Text = "比特率128kbps，采样率44.1khz";
                        break;
                    case "Low":
                        Info.Text = "比特率96kbps，采样率44.1khz";
                        break;
                }
            }
            else
            {
                switch (Quality.SelectedItem.ToString())
                {
                    case "UHD2160p":
                        Info.Text = "Resolution 3840 X 2160，30FPS";
                        break;
                    case "HD1080p":
                        Info.Text = "Resolution 1920 X 1080，30FPS";
                        break;
                    case "HD720p":
                        Info.Text = "Resolution 1280 X 720，30FPS";
                        break;
                    case "WVGA":
                        Info.Text = "Resolution 800 X 480，30FPS";
                        break;
                    case "VGA":
                        Info.Text = "Resolution 640 X 480，30FPS";
                        break;
                    case "QVGA":
                        Info.Text = "Resolution 320 X 240，30FPS";
                        break;
                    case "High":
                        Info.Text = "Bit-Rate 192kbps，Sampling-Rate 48khz";
                        break;
                    case "Medium":
                        Info.Text = "Bit-Rate 128kbps，Sampling-Rate 44.1khz";
                        break;
                    case "Low":
                        Info.Text = "Bit-Rate 96kbps，Sampling-Rate 44.1khz";
                        break;
                }
            }
        }
    }
}
