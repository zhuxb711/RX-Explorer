using System.Linq;
using System.Text.RegularExpressions;
using Windows.UI.Xaml.Controls;

namespace FileManager
{
    public sealed partial class NewFileDialog : QueueContentDialog
    {
        public string NewFileName { get; private set; }

        public NewFileDialog()
        {
            InitializeComponent();
            if (Globalization.Language == LanguageEnum.Chinese)
            {
                Extension.Items.Add("文本文件(.txt)");
                Extension.Items.Add("压缩文件(.zip)");
                Extension.Items.Add("RTF文件(.rtf)");
                Extension.Items.Add("Microsoft Word文件(.docx)");
                Extension.Items.Add("Microsoft PowerPoint文件(.pptx)");
                Extension.Items.Add("Microsoft Excel文件(.xlsx)");
            }
            else
            {
                Extension.Items.Add("Text file(.txt)");
                Extension.Items.Add("Compressed file(.zip)");
                Extension.Items.Add("RTF file(.rtf)");
                Extension.Items.Add("Microsoft Word(.docx)");
                Extension.Items.Add("Microsoft PowerPoint(.pptx)");
                Extension.Items.Add("Microsoft Excel(.xlsx)");
            }
        }

        private void QueueContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            if (string.IsNullOrWhiteSpace(NewFileNameTextBox.Text))
            {
                args.Cancel = true;
            }
            else
            {
                if (string.IsNullOrWhiteSpace(Extension.Text))
                {
                    InvalidInputTip.IsOpen = true;
                    args.Cancel = true;
                }
                else
                {
                    NewFileName = NewFileNameTextBox.Text + Regex.Match(Extension.SelectedItem.ToString(), @"\.\w+").Value;
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
    }
}
