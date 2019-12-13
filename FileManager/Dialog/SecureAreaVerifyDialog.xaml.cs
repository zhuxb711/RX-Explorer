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
            if (string.IsNullOrWhiteSpace(PrimaryPassword.Password))
            {
                args.Cancel = true;
                return;
            }

            if (UnlockPassword != PrimaryPassword.Password)
            {
                args.Cancel = true;
                ErrorTip.IsOpen = true;
            }
        }
    }
}
