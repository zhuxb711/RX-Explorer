using RX_Explorer.Class;
using System;
using System.IO;
using System.Linq;
using Windows.Storage;
using Windows.UI.Xaml.Controls;

namespace RX_Explorer.Dialog
{
    public sealed partial class RenameDialog : QueueContentDialog
    {
        public string DesireName { get; private set; }

        private readonly FileSystemStorageItemBase Item;

        public RenameDialog(FileSystemStorageItemBase Item)
        {
            InitializeComponent();
            
            this.Item = Item ?? throw new ArgumentNullException(nameof(Item), "Argument could not be null");
            RenameText.Text = Item.Name;
            Preview.Text = $"{Item.Name}\r⋙⋙   ⋙⋙   ⋙⋙\r{Item.Name}";
            Loaded += RenameDialog_Loaded;
        }

        private void RenameDialog_Loaded(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            if (Item is FileSystemStorageFile)
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
            if (!FileSystemItemNameChecker.IsValid(RenameText.Text))
            {
                args.Cancel = true;
                InvalidNameTip.IsOpen = true;
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

        private void RenameText_BeforeTextChanging(TextBox sender, TextBoxBeforeTextChangingEventArgs args)
        {
            if (args.NewText.Any((Item) => Path.GetInvalidFileNameChars().Contains(Item)))
            {
                args.Cancel = true;
                InvalidCharTip.IsOpen = true;
            }
        }
    }
}
