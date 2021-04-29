using RX_Explorer.Class;
using Windows.UI;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace RX_Explorer.Dialog
{
    public sealed partial class FeedBackDialog : QueueContentDialog
    {
        public string TitleName { get; private set; }

        public string Suggestion { get; private set; }

        public FeedBackDialog()
        {
            InitializeComponent();
        }

        public FeedBackDialog(string Title, string Suggestion)
        {
            InitializeComponent();

            TitleName = Title;
            this.Suggestion = Suggestion;
        }

        public FeedBackDialog(string Title)
        {
            InitializeComponent();

            TitleName = Title;
        }

        private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            if (string.IsNullOrWhiteSpace(TitleName) && string.IsNullOrWhiteSpace(Suggestion))
            {
                TitleBox.BorderBrush = new SolidColorBrush(Colors.Red);
                FeedBox.BorderBrush = new SolidColorBrush(Colors.Red);
                args.Cancel = true;
            }
            else if (string.IsNullOrWhiteSpace(TitleName))
            {
                TitleBox.BorderBrush = new SolidColorBrush(Colors.Red);
                FeedBox.BorderBrush = new SolidColorBrush(Colors.Gray);
                args.Cancel = true;
            }
            else if (string.IsNullOrWhiteSpace(Suggestion))
            {
                FeedBox.BorderBrush = new SolidColorBrush(Colors.Red);
                TitleBox.BorderBrush = new SolidColorBrush(Colors.Gray);
                args.Cancel = true;
            }
        }
    }
}
