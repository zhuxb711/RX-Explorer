using Windows.UI.Xaml.Controls;

namespace FileManager
{
    public sealed partial class RenameDialog : ContentDialog
    {
        public RenameDialog(string FileDisplayName, string Type)
        {
            InitializeComponent();
            Text.Text = FileDisplayName;
            Text.SelectAll();
            FileName = FileDisplayName + Type;
            this.Type = Type;
            Preview.Text = FileName + "\r⋙⋙   ⋙⋙   ⋙⋙\r" + Text.Text + Type;
        }

        public RenameDialog(string FolderName)
        {
            InitializeComponent();
            Text.Text = FolderName;
            FileName = FolderName;
            Text.SelectAll();
            Type = "";
            Preview.Text = FileName + "\r⋙⋙   ⋙⋙   ⋙⋙\r" + Text.Text;
        }

        string FileName;
        string Type;
        public string DesireName { get; private set; }

        private void Text_TextChanging(TextBox sender, TextBoxTextChangingEventArgs args)
        {
            Preview.Text = FileName + "\r⋙⋙   ⋙⋙   ⋙⋙\r" + Text.Text + Type;
        }

        private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            DesireName = Text.Text + Type;
        }
    }
}
