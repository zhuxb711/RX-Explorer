using Windows.UI.Xaml.Controls;

namespace FileManager
{
    public sealed partial class NewFileDialog : QueueContentDialog
    {
        public string NewFileName { get; private set; }

        public NewFileDialog()
        {
            InitializeComponent();
        }

        private void QueueContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            if (string.IsNullOrWhiteSpace(NewFileNameTextBox.Text))
            {
                args.Cancel = true;
            }
        }
    }
}
