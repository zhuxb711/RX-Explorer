using RX_Explorer.Class;
using Windows.UI.Xaml.Controls;


namespace RX_Explorer.Dialog
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
    }
}
