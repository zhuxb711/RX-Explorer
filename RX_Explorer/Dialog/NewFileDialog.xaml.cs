using RX_Explorer.Class;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Windows.UI.Xaml.Controls;

namespace RX_Explorer.Dialog
{
    public sealed partial class NewFileDialog : QueueContentDialog
    {
        public string NewFileName { get; private set; }

        public NewFileDialog()
        {
            InitializeComponent();

            Extension.Items.Add($"{Globalization.GetString("File_Type_TXT_Description")}(.txt)");
            Extension.Items.Add($"{Globalization.GetString("File_Type_Compress_Description")}(.zip)");
            Extension.Items.Add($"{Globalization.GetString("File_Type_RTF_Description")}(.rtf)");
            Extension.Items.Add($"{Globalization.GetString("Link_Admin_DisplayType")}(.lnk)");
            Extension.Items.Add("Microsoft Word(.docx)");
            Extension.Items.Add("Microsoft PowerPoint(.pptx)");
            Extension.Items.Add("Microsoft Excel(.xlsx)");
        }

        private void QueueContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            bool IsNameEmpty = string.IsNullOrWhiteSpace(NewFileNameTextBox.Text);
            bool IsExtensionEmpty = string.IsNullOrWhiteSpace(Extension.Text);

            if (IsNameEmpty && IsExtensionEmpty)
            {
                args.Cancel = true;
                InvalidNameTip.IsOpen = true;
                InvalidInputTip.IsOpen = true;
            }
            else
            {
                if (!IsNameEmpty && !FileSystemItemNameChecker.IsValid(NewFileNameTextBox.Text))
                {
                    args.Cancel = true;
                    InvalidNameTip.IsOpen = true;
                }
                else
                {
                    if (IsExtensionEmpty)
                    {
                        NewFileName = NewFileNameTextBox.Text;
                    }
                    else
                    {
                        NewFileName = NewFileNameTextBox.Text + Regex.Match(Extension.SelectedItem.ToString(), @"\.\w+").Value;
                    }
                }
            }
        }

        private void Extension_TextSubmitted(ComboBox sender, ComboBoxTextSubmittedEventArgs args)
        {
            if (sender.Items.All((Item) => Item.ToString() != args.Text))
            {
                if (args.Text.Length <= 1 || !args.Text.StartsWith(".") || args.Text.LastIndexOf(".") != 0)
                {
                    InvalidInputTip.IsOpen = true;
                    args.Handled = true;
                }
            }
        }

        private void TypeQuestion_PointerPressed(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            TypeTip.IsOpen = true;
        }

        private void NewFileNameTextBox_BeforeTextChanging(TextBox sender, TextBoxBeforeTextChangingEventArgs args)
        {
            if (args.NewText.Any((Item) => Path.GetInvalidFileNameChars().Contains(Item)))
            {
                args.Cancel = true;

                InvalidCharTip.IsOpen = true;
            }
        }
    }
}
