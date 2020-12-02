using RX_Explorer.Class;
using Windows.UI.Xaml.Controls;

namespace RX_Explorer.Dialog
{
    public sealed partial class BitlockerPasswordDialog : QueueContentDialog
    {
        public string Password { get; private set; }

        public BitlockerPasswordDialog()
        {
            InitializeComponent();
        }

        private void QueueContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            if (string.IsNullOrWhiteSpace(Password))
            {
                EmptyTip.IsOpen = true;
                args.Cancel = true;
            }
        }
    }
}
