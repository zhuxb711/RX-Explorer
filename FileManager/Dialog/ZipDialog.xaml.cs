using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;


namespace FileManager
{
    public sealed partial class ZipDialog : QueueContentDialog
    {
        /// <summary>
        /// 获取文件名
        /// </summary>
        public string FileName { get; private set; }

        /// <summary>
        /// 获取密码
        /// </summary>
        public string Password { get; private set; }

        /// <summary>
        /// 获取密钥长度
        /// </summary>
        public KeySize Key { get; private set; }

        /// <summary>
        /// 获取压缩等级
        /// </summary>
        public CompressionLevel Level { get; private set; }

        /// <summary>
        /// 获取是否启用加密
        /// </summary>
        public bool IsCryptionEnable { get; private set; }

        private bool IsZip;

        public ZipDialog(bool IsZip, string FileName = null)
        {
            InitializeComponent();
            this.IsZip = IsZip;
            if (IsZip)
            {
                FName.Text = FileName + ".zip";
                FName.SelectAll();
                ZipCryption.SelectedIndex = 0;
                ZipMethod.SelectedIndex = 2;
            }
            else
            {
                if (Globalization.Language == LanguageEnum.Chinese)
                {
                    Title = "需要解压密码";
                    Pass.PlaceholderText = "输入解密密码";
                }
                else
                {
                    Title = "Password required";
                    Pass.PlaceholderText = "Enter decompression password";
                }

                FName.Visibility = Visibility.Collapsed;
                ZipMethod.Visibility = Visibility.Collapsed;
                EnableCryption.Visibility = Visibility.Collapsed;
            }

            if (Globalization.Language == LanguageEnum.Chinese)
            {
                ZipMethod.Items.Add("最大");
                ZipMethod.Items.Add("较大");
                ZipMethod.Items.Add("标准");
                ZipMethod.Items.Add("较小");
                ZipMethod.Items.Add("最小");
            }
            else
            {
                ZipMethod.Items.Add("Max");
                ZipMethod.Items.Add("Higher");
                ZipMethod.Items.Add("Standard");
                ZipMethod.Items.Add("Lower");
                ZipMethod.Items.Add("Min");
            }

            Loaded += (s, e) =>
            {
                if (!IsZip)
                {
                    Pass.Visibility = Visibility.Visible;
                }
            };
        }

        private void QueueContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            if (IsZip)
            {
                if ((bool)EnableCryption.IsChecked && (string.IsNullOrEmpty(FName.Text) || string.IsNullOrEmpty(Pass.Password)))
                {
                    args.Cancel = true;
                    return;
                }
                else if (string.IsNullOrEmpty(FName.Text))
                {
                    args.Cancel = true;
                    return;
                }

                if (FName.Text.EndsWith(".zip"))
                {
                    FileName = FName.Text;
                }
                else
                {
                    FileName = FName.Text + ".zip";
                }

                IsCryptionEnable = (bool)EnableCryption.IsChecked;
                Password = Pass.Password;
                switch (ZipMethod.SelectedItem as string)
                {
                    case "Max": Level = CompressionLevel.Max; break;
                    case "Higher": Level = CompressionLevel.AboveStandard; break;
                    case "Standard": Level = CompressionLevel.Standard; break;
                    case "Lower": Level = CompressionLevel.BelowStandard; break;
                    case "Min": Level = CompressionLevel.PackOnly; break;
                }

                switch (ZipCryption.SelectedItem as string)
                {
                    case "AES-128": Key = KeySize.AES128; break;
                    case "AES-256": Key = KeySize.AES256; break;
                }
            }
            else
            {
                if (string.IsNullOrEmpty(Pass.Password))
                {
                    args.Cancel = true;
                    return;
                }
                else
                {
                    Password = Pass.Password;
                }
            }
        }
    }
}
