using RX_Explorer.Class;
using Windows.UI.Xaml.Controls;

namespace RX_Explorer.Dialog
{

    public sealed partial class PdfPasswordDialog : QueueContentDialog
    {
        public string Password { get; private set; }

        public PdfPasswordDialog()
        {
            InitializeComponent();
        }

        private void QueueContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            if (string.IsNullOrEmpty(PdfPassword.Password))
            {
                args.Cancel = true;
            }
            else
            {
                Password = PdfPassword.Password;
            }
        }
    }
}
