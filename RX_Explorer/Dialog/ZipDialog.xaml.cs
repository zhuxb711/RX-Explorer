using RX_Explorer.Class;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;


namespace RX_Explorer.Dialog
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

                ZipMethod.Items.Add(Globalization.GetString("Zip_Dialog_Level_1"));
                ZipMethod.Items.Add(Globalization.GetString("Zip_Dialog_Level_2"));
                ZipMethod.Items.Add(Globalization.GetString("Zip_Dialog_Level_3"));

                ZipCryption.SelectedIndex = 0;
                ZipMethod.SelectedIndex = 1;
            }
            else
            {
                Title = Globalization.GetString("Zip_Dialog_Password_Title");
                Pass.PlaceholderText = Globalization.GetString("Zip_Dialog_Password_PlaceholderText");

                FName.Visibility = Visibility.Collapsed;
                ZipMethod.Visibility = Visibility.Collapsed;
                EnableCryption.Visibility = Visibility.Collapsed;
            }

            Loaded += ZipDialog_Loaded;
        }

        private void ZipDialog_Loaded(object sender, RoutedEventArgs e)
        {
            if (!IsZip)
            {
                Pass.Visibility = Visibility.Visible;
            }
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

                IsCryptionEnable = EnableCryption.IsChecked.GetValueOrDefault();

                Password = Pass.Password;

                switch (ZipMethod.SelectedItem.ToString())
                {
                    case "Max":
                    case "最大":
                        {
                            Level = CompressionLevel.Max;
                            break;
                        }
                    case "Standard":
                    case "标准":
                        {
                            Level = CompressionLevel.Standard;
                            break;
                        }
                    case "Package only":
                    case "仅存储":
                        {
                            Level = CompressionLevel.PackageOnly;
                            break;
                        }
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
