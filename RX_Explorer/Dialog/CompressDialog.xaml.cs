using RX_Explorer.Class;
using System.IO;
using Windows.UI.Xaml.Controls;


namespace RX_Explorer.Dialog
{
    public sealed partial class CompressDialog : QueueContentDialog
    {
        /// <summary>
        /// 获取文件名
        /// </summary>
        public string FileName { get; private set; }

        /// <summary>
        /// 获取压缩等级
        /// </summary>
        public CompressionLevel Level { get; private set; }

        public CompressionType Type { get; private set; }

        private readonly string SuggestName;

        public CompressDialog(bool ShouldDisplayGzip, string SuggestName)
        {
            InitializeComponent();

            this.SuggestName = SuggestName;

            Initialize(ShouldDisplayGzip);
        }

        public CompressDialog(bool ShouldDisplayGzip) : this(ShouldDisplayGzip, null)
        {

        }

        private void Initialize(bool ShouldDisplayGzip)
        {
            CompressionType.Items.Add("Zip");
            CompressionType.Items.Add("Tar");

            if (ShouldDisplayGzip)
            {
                CompressionType.Items.Add("GZip");
            }

            CompressionType.SelectedIndex = 0;

            CompressLevel.Items.Add(Globalization.GetString("Compression_Dialog_Level_1"));
            CompressLevel.Items.Add(Globalization.GetString("Compression_Dialog_Level_2"));
            CompressLevel.Items.Add(Globalization.GetString("Compression_Dialog_Level_3"));

            CompressLevel.SelectedIndex = 1;
        }

        private void QueueContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            if (string.IsNullOrEmpty(FName.Text))
            {
                args.Cancel = true;
                return;
            }

            switch (CompressionType.SelectedIndex)
            {
                case 0:
                    {
                        FileName = FName.Text.EndsWith(".zip", System.StringComparison.OrdinalIgnoreCase) ? FName.Text : $"{FName.Text}.zip";
                        break;
                    }
                case 1:
                    {
                        FileName = FName.Text.EndsWith(".tar", System.StringComparison.OrdinalIgnoreCase) ? FName.Text : $"{FName.Text}.tar";
                        break;
                    }
                case 2:
                    {
                        FileName = FName.Text.EndsWith(".gz", System.StringComparison.OrdinalIgnoreCase) ? FName.Text : $"{FName.Text}.gz";
                        break;
                    }
            }

            switch (CompressLevel.SelectedIndex)
            {
                case 0:
                    {
                        Level = CompressionLevel.Max;
                        break;
                    }
                case 1:
                    {
                        Level = CompressionLevel.Standard;
                        break;
                    }
                case 2:
                    {
                        Level = CompressionLevel.PackageOnly;
                        break;
                    }
                default:
                    {
                        Level = CompressionLevel.Standard;
                        break;
                    }
            }
        }

        private void CompressionType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            switch (CompressionType.SelectedIndex)
            {
                case 0:
                    {
                        FName.Text = $"{(string.IsNullOrEmpty(SuggestName) ? Globalization.GetString("Compression_Admin_Name_Text") : SuggestName)}.zip";
                        FName.Select(0, FName.Text.Length - 4);
                        Type = Class.CompressionType.Zip;
                        CompressLevel.Visibility = Windows.UI.Xaml.Visibility.Visible;

                        break;
                    }
                case 1:
                    {
                        FName.Text = $"{(string.IsNullOrEmpty(SuggestName) ? Globalization.GetString("Compression_Admin_Name_Text") : SuggestName)}.tar";
                        FName.Select(0, FName.Text.Length - 4);
                        Type = Class.CompressionType.Tar;
                        CompressLevel.Visibility = Windows.UI.Xaml.Visibility.Collapsed;

                        break;
                    }
                case 2:
                    {
                        FName.Text = $"{(string.IsNullOrEmpty(SuggestName) ? Globalization.GetString("Compression_Admin_Name_Text") : SuggestName)}.gz";
                        FName.Select(0, FName.Text.Length - 7);
                        Type = Class.CompressionType.Gzip;
                        CompressLevel.Visibility = Windows.UI.Xaml.Visibility.Visible;

                        break;
                    }
            }
        }
    }
}
