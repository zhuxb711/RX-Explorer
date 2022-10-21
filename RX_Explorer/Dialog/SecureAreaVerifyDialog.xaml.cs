using RX_Explorer.Class;
using RX_Explorer.View;
using Windows.UI.Xaml.Controls;


namespace RX_Explorer.Dialog
{
    public sealed partial class SecureAreaVerifyDialog : QueueContentDialog
    {
        public SecureAreaVerifyDialog()
        {
            InitializeComponent();
        }

        private void QueueContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            if (SecureArea.UnlockPassword != PrimaryPassword.Password)
            {
                args.Cancel = true;
                ErrorTip.IsOpen = true;
            }
        }
    }
}
