using Windows.UI;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace FileManager
{
    public sealed partial class FeedBackDialog : QueueContentDialog
    {
        public FeedBackDialog()
        {
            InitializeComponent();
        }

        public FeedBackDialog(string Title,string Suggestion)
        {
            InitializeComponent();

            TitleName = Title;
            FeedBack = Suggestion;
        }

        public string TitleName { get; private set; }

        public string FeedBack { get; private set; }

        private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            if (string.IsNullOrWhiteSpace(TitleName) && string.IsNullOrWhiteSpace(FeedBack))
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
            else if (string.IsNullOrWhiteSpace(FeedBack))
            {
                FeedBox.BorderBrush = new SolidColorBrush(Colors.Red);
                TitleBox.BorderBrush = new SolidColorBrush(Colors.Gray);
                args.Cancel = true;
            }
        }
    }
}
