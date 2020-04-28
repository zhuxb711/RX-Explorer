using FileManager.Class;
using System.IO;
using Windows.UI.Xaml.Controls;

namespace FileManager.Dialog
{
    public sealed partial class RenameDialog : QueueContentDialog
    {
        private readonly string OriginName;

        public string DesireName { get; private set; }

        public RenameDialog(string Name)
        {
            InitializeComponent();
            RenameText.Text = Name;
            OriginName = Name;
            Preview.Text = $"{OriginName}\r⋙⋙   ⋙⋙   ⋙⋙\r{OriginName}";
            Loaded += RenameDialog_Loaded;
        }

        private void RenameDialog_Loaded(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            RenameText.SelectAll();
        }

        private void QueueContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            if (RenameText.Text.IndexOfAny(Path.GetInvalidFileNameChars()) != -1)
            {
                args.Cancel = true;
                InvalidCharTip.IsOpen = true;
            }
            else if(string.IsNullOrWhiteSpace(RenameText.Text))
            {
                args.Cancel = true;
                InvalidCharTip.IsOpen = true;
            }
            else
            {
                DesireName = RenameText.Text;
            }
        }

        private void RenameText_TextChanged(object sender, TextChangedEventArgs e)
        {
            Preview.Text = $"{OriginName}\r⋙⋙   ⋙⋙   ⋙⋙\r{RenameText.Text}";
        }
    }
}
