using RX_Explorer.Class;
using System;
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

        private readonly string SuggestName;

        /// <summary>
        /// 获取压缩等级
        /// </summary>
        public CompressionLevel Level { get; private set; }

        public TarCompressionType TarType { get; private set; } = TarCompressionType.None;

        public CompressionType Type { get; private set; }

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

            TarCompressionAlgorithm.Items.Add(TarCompressionType.Gz.ToString());
            TarCompressionAlgorithm.Items.Add(TarCompressionType.Bz2.ToString());
            TarCompressionAlgorithm.Items.Add(TarCompressionType.None.ToString());

            //SharpCompress暂时不支付tar.xz
            //TarCompressionAlgorithm.Items.Add(TarCompressionType.Xz.ToString());

            
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
                        FileName = FName.Text.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) ? FName.Text : $"{FName.Text}.zip";
                        break;
                    }
                case 1:
                    {
                        if (TarType==TarCompressionType.None)
                        {
                            FileName = FName.Text.EndsWith(".tar", StringComparison.OrdinalIgnoreCase) ? FName.Text : $"{FName.Text}.tar";
                            
                        }
                        else 
                        {
                            string Suffix = ".tar." + TarType.ToString().ToLower();
                            FileName = FName.Text.EndsWith(Suffix, StringComparison.OrdinalIgnoreCase) ? FName.Text : FName.Text + Suffix;
                             
                        }
                        break;
                    }
                case 2:
                    {
                        FileName = FName.Text.EndsWith(".gz", StringComparison.OrdinalIgnoreCase) ? FName.Text : $"{FName.Text}.gz";
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
                        FName.Text = $"{(string.IsNullOrEmpty(SuggestName) ? Globalization.GetString("Compression_Admin_Name_Text") : Path.GetFileNameWithoutExtension(SuggestName))}.zip";
                        FName.Select(0, FName.Text.Length - 4);
                        Type = Class.CompressionType.Zip;
                        CompressLevel.Visibility = Windows.UI.Xaml.Visibility.Visible;
                        TarCompressionAlgorithm.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
                         

                        break;
                    }
                case 1:
                    {
                        FName.Text = $"{(string.IsNullOrEmpty(SuggestName) ? Globalization.GetString("Compression_Admin_Name_Text") : Path.GetFileNameWithoutExtension(SuggestName))}.tar.gz";
                        FName.Select(0, FName.Text.Length - 4);
                        Type = Class.CompressionType.Tar;
                        CompressLevel.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
                        TarCompressionAlgorithm.Visibility = Windows.UI.Xaml.Visibility.Visible;
                        TarCompressionAlgorithm.SelectedIndex = 0;



                        break;
                    }
                case 2:
                    {
                        FName.Text = $"{(string.IsNullOrEmpty(SuggestName) ? Globalization.GetString("Compression_Admin_Name_Text") : SuggestName)}.gz";
                        FName.Select(0, FName.Text.Length - 7);
                        Type = Class.CompressionType.Gzip;
                        CompressLevel.Visibility = Windows.UI.Xaml.Visibility.Visible;
                        TarCompressionAlgorithm.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
                         
                        break;
                    }
            }
        }

     

        private void TarCompressionType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            TarType = (TarCompressionType)Enum.Parse(typeof(TarCompressionType),TarCompressionAlgorithm.SelectedValue.ToString());
            if (TarType == TarCompressionType.None)
            {
                FName.Text = $"{(string.IsNullOrEmpty(SuggestName) ? Globalization.GetString("Compression_Admin_Name_Text") : Path.GetFileNameWithoutExtension(SuggestName))}.tar";

            }
            else
            {
                string Suffix = ".tar." + TarType.ToString().ToLower();
                FName.Text = (string.IsNullOrEmpty(SuggestName) ? Globalization.GetString("Compression_Admin_Name_Text") : Path.GetFileNameWithoutExtension(SuggestName))+Suffix;

            }
        }
    }
}
