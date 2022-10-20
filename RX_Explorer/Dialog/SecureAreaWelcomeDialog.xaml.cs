using RX_Explorer.Class;
using Windows.UI.Xaml.Controls;


namespace RX_Explorer.Dialog
{
    public sealed partial class SecureAreaWelcomeDialog : QueueContentDialog
    {
        public string Password { get; private set; }

        public bool IsEnableWindowsHello { get; private set; }

        public SLEKeySize EncryptionKeySize { get; private set; }

        public SecureAreaWelcomeDialog()
        {
            InitializeComponent();

            Loading += SecureAreaWelcomeDialog_Loading;

            SecureLevel.Items.Add($"AES-128bit ({Globalization.GetString("SecureArea_AES_128Level_Description")})");
            SecureLevel.Items.Add($"AES-256bit ({Globalization.GetString("SecureArea_AES_256Level_Description")})");
            SecureLevel.SelectedIndex = 0;
        }

        private async void SecureAreaWelcomeDialog_Loading(Windows.UI.Xaml.FrameworkElement sender, object args)
        {
            if (await WindowsHelloAuthenticator.CheckSupportAsync())
            {
                UseWinHel.IsEnabled = true;
            }
        }

        private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            if (string.IsNullOrWhiteSpace(PrimaryPassword.Password))
            {
                EmptyTip.Target = PrimaryPassword;
                EmptyTip.IsOpen = true;
                args.Cancel = true;
                return;
            }

            if (string.IsNullOrWhiteSpace(ConfirmPassword.Password))
            {
                EmptyTip.Target = ConfirmPassword;
                EmptyTip.IsOpen = true;
                args.Cancel = true;
                return;
            }

            if (PrimaryPassword.Password != ConfirmPassword.Password)
            {
                PasswordErrorTip.IsOpen = true;
                args.Cancel = true;
                return;
            }

            Password = PrimaryPassword.Password;
            IsEnableWindowsHello = UseWinHel.IsChecked.GetValueOrDefault();
            EncryptionKeySize = SecureLevel.SelectedIndex == 0 ? SLEKeySize.AES128 : SLEKeySize.AES256;
        }

        private async void UseWinHel_Checked(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            switch (await WindowsHelloAuthenticator.RegisterUserAsync())
            {
                case AuthenticatorState.RegisterSuccess:
                    {
                        WindowsHelloPassed.Visibility = Windows.UI.Xaml.Visibility.Visible;
                        WindowsHelloFailed.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
                        break;
                    }
                default:
                    {
                        WindowsHelloPassed.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
                        WindowsHelloFailed.Visibility = Windows.UI.Xaml.Visibility.Visible;
                        UseWinHel.IsChecked = false;
                        break;
                    }
            }
        }

        private async void UseWinHel_Unchecked(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            await WindowsHelloAuthenticator.DeleteUserAsync().ConfigureAwait(false);
        }
    }
}
