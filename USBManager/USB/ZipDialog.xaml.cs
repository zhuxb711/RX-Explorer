using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;


namespace USBManager
{
    public sealed partial class ZipDialog : ContentDialog
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
                if (FileName != null)
                {
                    FName.Text = FileName + ".zip";
                }
                FName.SelectAll();
                ZipCryption.SelectedIndex = 0;
                ZipMethod.SelectedIndex = 2;
            }
            else
            {
                Title = "需要解压密码";
                FName.Visibility = Visibility.Collapsed;
                Pass.PlaceholderText = "输入解密密码";
                ZipMethod.Visibility = Visibility.Collapsed;
                EnableCryption.Visibility = Visibility.Collapsed;
            }
            Loaded += (s, e) =>
            {
                if (!IsZip)
                {
                    Pass.Visibility = Visibility.Visible;
                }
            };
        }

        private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            if (IsZip)
            {
                if ((bool)EnableCryption.IsChecked && (FName.Text == "" || Pass.Password == ""))
                {
                    args.Cancel = true;
                    return;
                }
                else if (FName.Text == "")
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
                    case "最大": Level = CompressionLevel.Max; break;
                    case "较大": Level = CompressionLevel.AboveStandard; break;
                    case "标准": Level = CompressionLevel.Standard; break;
                    case "较低": Level = CompressionLevel.BelowStandard; break;
                    case "仅存档": Level = CompressionLevel.PackOnly; break;
                }

                switch (ZipCryption.SelectedItem as string)
                {
                    case "AES-128": Key = KeySize.AES128; break;
                    case "AES-256": Key = KeySize.AES256; break;
                }
            }
            else
            {
                if (Pass.Password == "")
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
