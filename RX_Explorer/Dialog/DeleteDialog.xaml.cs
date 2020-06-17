using RX_Explorer.Class;
using Windows.UI.Xaml.Controls;

namespace RX_Explorer.Dialog
{
    public sealed partial class DeleteDialog : QueueContentDialog
    {
        public bool IsPermanentDelete { get; private set; }

        public DeleteDialog(string Text)
        {
            InitializeComponent();
            DisplayText.Text = Text;
        }

        private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            IsPermanentDelete = PermanentDelete.IsChecked.GetValueOrDefault();
        }
    }
}
