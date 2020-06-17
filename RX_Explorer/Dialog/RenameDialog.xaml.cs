using RX_Explorer.Class;
using System.IO;
using System.Linq;
using Windows.Storage;
using Windows.UI.Xaml.Controls;

namespace RX_Explorer.Dialog
{
    public sealed partial class RenameDialog : QueueContentDialog
    {
        public string DesireName { get; private set; }

        private IStorageItem Item;

        public RenameDialog(IStorageItem Item)
        {
            InitializeComponent();
            this.Item = Item;
            RenameText.Text = Item.Name;
            Preview.Text = $"{Item.Name}\r⋙⋙   ⋙⋙   ⋙⋙\r{Item.Name}";
            Loaded += RenameDialog_Loaded;
        }

        private void RenameDialog_Loaded(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            if (Item.IsOfType(StorageItemTypes.File))
            {
                if (Item.Name != Path.GetExtension(Item.Name))
                {
                    RenameText.Select(0, Item.Name.Length - Path.GetExtension(Item.Name).Length);
                }
            }
            else
            {
                RenameText.SelectAll();
            }
        }

        private void QueueContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            if (RenameText.Text.Any((Char) => Path.GetInvalidFileNameChars().Contains(Char)))
            {
                args.Cancel = true;
                InvalidCharTip.IsOpen = true;
            }
            else if (string.IsNullOrWhiteSpace(RenameText.Text))
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
            Preview.Text = $"{Item.Name}\r⋙⋙   ⋙⋙   ⋙⋙\r{RenameText.Text}";
        }
    }
}
