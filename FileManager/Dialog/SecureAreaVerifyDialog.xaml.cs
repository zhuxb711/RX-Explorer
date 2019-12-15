using Windows.UI.Xaml.Controls;


namespace FileManager
{
    public sealed partial class SecureAreaVerifyDialog : QueueContentDialog
    {
        private string UnlockPassword;

        public SecureAreaVerifyDialog(string UnlockPassword)
        {
            InitializeComponent();
            this.UnlockPassword = UnlockPassword;
        }

        private void QueueContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            if (UnlockPassword != PrimaryPassword.Password)
            {
                args.Cancel = true;
                ErrorTip.IsOpen = true;
            }
        }

        private void QueueContentDialog_KeyDown(object sender, Windows.UI.Xaml.Input.KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                if (UnlockPassword != PrimaryPassword.Password)
                {
                    ErrorTip.IsOpen = true;
                    return;
                }

                Close(ContentDialogResult.Primary);
            }
        }
    }
}
