using RX_Explorer.Class;
using Windows.System;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace RX_Explorer.Dialog
{
    public sealed partial class DeleteDialog : QueueContentDialog
    {
        public bool IsPermanentDelete { get; private set; }

        public DeleteDialog(string Text, bool IsPermanentDelete = false)
        {
            InitializeComponent();
            PermanentDelete.IsChecked = IsPermanentDelete;
            DisplayText.Text = Text;
        }

        private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            IsPermanentDelete = PermanentDelete.IsChecked.GetValueOrDefault();
        }
    }
}
