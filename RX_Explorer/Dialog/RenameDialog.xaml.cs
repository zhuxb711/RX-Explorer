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

        private IStorageItem SItem;

        private HyperlinkStorageItem Item;

        public RenameDialog(IStorageItem SItem)
        {
            InitializeComponent();

            this.SItem = SItem ?? throw new ArgumentNullException(nameof(SItem), "Argument could not be null");
            RenameText.Text = SItem.Name;
            Preview.Text = $"{SItem.Name}\r⋙⋙   ⋙⋙   ⋙⋙\r{SItem.Name}";
            Loaded += RenameDialog_Loaded;
        }

        public RenameDialog(HyperlinkStorageItem Item)
        {
            InitializeComponent();
            
            this.Item = Item ?? throw new ArgumentNullException(nameof(Item), "Argument could not be null");
            RenameText.Text = Item.Name;
            Preview.Text = $"{Item.Name}\r⋙⋙   ⋙⋙   ⋙⋙\r{Item.Name}";
            Loaded += RenameDialog_Loaded;
        }

        private void RenameDialog_Loaded(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            if (SItem != null)
            {
                if (SItem.IsOfType(StorageItemTypes.File))
                {
                    if (SItem.Name != Path.GetExtension(SItem.Name))
                    {
                        RenameText.Select(0, SItem.Name.Length - Path.GetExtension(SItem.Name).Length);
                    }
                }
                else
                {
                    RenameText.SelectAll();
                }
            }
            else if (Item != null)
            {
                if (Item.Name != Path.GetExtension(Item.Name))
                {
                    RenameText.Select(0, Item.Name.Length - Path.GetExtension(Item.Name).Length);
                }
            }
        }

        private void QueueContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            if (!FileSystemItemNameChecker.IsValid(RenameText.Text))
            {
                args.Cancel = true;
                InvalidNameTip.IsOpen = true;
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
            if (SItem != null)
            {
                Preview.Text = $"{SItem.Name}\r⋙⋙   ⋙⋙   ⋙⋙\r{RenameText.Text}";
            }
            else if (Item != null)
            {
                Preview.Text = $"{Item.Name}\r⋙⋙   ⋙⋙   ⋙⋙\r{RenameText.Text}";
            }
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
