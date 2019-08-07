using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace FileManager
{
    public sealed partial class AESDialog : ContentDialog
    {
        public string Key { get; set; }

        public bool IsDeleteChecked { get; set; }

        public int KeySize { get; private set; }

        private bool IsEncrypt;

        public AESDialog(bool IsEncrypt, string Name)
        {
            InitializeComponent();
            this.IsEncrypt = IsEncrypt;
            KeySelector.SelectedIndex = 0;
            FileName.Text = "文件名：" + Name;
            if (!IsEncrypt)
            {
                Title = "AES解密";
                PasswordControl.Header = "解密密码";
                KeySelector.Visibility = Visibility.Collapsed;
                Check.Content = "解密完成后删除源文件";
            }
        }

        private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            if (IsEncrypt)
            {
                if (KeySelector.SelectedIndex == 0)
                {
                    KeySize = 128;
                }
                else if (KeySelector.SelectedIndex == 1)
                {
                    KeySize = 256;
                }
            }

            if ((Key = PasswordControl.Password) == "")
            {
                args.Cancel = true;
                return;
            }

            //若密码长度不够则自动用0补齐
            if (KeySize == 128)
            {
                if (Key.Length < 16)
                {
                    Key = Key.PadRight(16, '0');
                }
            }
            else if (KeySize == 256)
            {
                if (Key.Length < 32)
                {
                    Key = Key.PadRight(32, '0');
                }
            }
        }
    }
}
