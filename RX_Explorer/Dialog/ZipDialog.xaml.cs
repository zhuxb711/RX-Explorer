using RX_Explorer.Class;
using System.IO;
using Windows.UI.Xaml.Controls;


namespace RX_Explorer.Dialog
{
    public sealed partial class ZipDialog : QueueContentDialog
    {
        /// <summary>
        /// 获取文件名
        /// </summary>
        public string FileName { get; private set; }

        /// <summary>
        /// 获取压缩等级
        /// </summary>
        public CompressionLevel Level { get; private set; }

        public ZipDialog(FileSystemStorageItemBase StorageItem)
        {
            InitializeComponent();

            if (StorageItem.StorageType == Windows.Storage.StorageItemTypes.File)
            {
                FName.Text = $"{Path.GetFileNameWithoutExtension(StorageItem.Name)}.zip";
            }
            else
            {
                FName.Text = $"{StorageItem.Name}.zip";
            }

            if (FName.Text != Path.GetExtension(FName.Text))
            {
                FName.Select(0, FName.Text.Length - 4);
            }

            ZipMethod.Items.Add(Globalization.GetString("Zip_Dialog_Level_1"));
            ZipMethod.Items.Add(Globalization.GetString("Zip_Dialog_Level_2"));
            ZipMethod.Items.Add(Globalization.GetString("Zip_Dialog_Level_3"));

            ZipMethod.SelectedIndex = 1;
        }

        public ZipDialog()
        {
            InitializeComponent();

            FName.Text = $"{Globalization.GetString("Zip_Admin_Name_Text")}.zip";
            FName.Select(0, FName.Text.Length - 4);

            ZipMethod.Items.Add(Globalization.GetString("Zip_Dialog_Level_1"));
            ZipMethod.Items.Add(Globalization.GetString("Zip_Dialog_Level_2"));
            ZipMethod.Items.Add(Globalization.GetString("Zip_Dialog_Level_3"));

            ZipMethod.SelectedIndex = 1;
        }

        private void QueueContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            if (string.IsNullOrEmpty(FName.Text))
            {
                args.Cancel = true;
                return;
            }

            FileName = FName.Text.EndsWith(".zip", System.StringComparison.OrdinalIgnoreCase) ? FName.Text : $"{FName.Text}.zip";

            if (ZipMethod.SelectedItem.ToString() == Globalization.GetString("Zip_Dialog_Level_1"))
            {
                Level = CompressionLevel.Max;
            }
            else if (ZipMethod.SelectedItem.ToString() == Globalization.GetString("Zip_Dialog_Level_2"))
            {
                Level = CompressionLevel.Standard;
            }
            else if (ZipMethod.SelectedItem.ToString() == Globalization.GetString("Zip_Dialog_Level_3"))
            {
                Level = CompressionLevel.PackageOnly;
            }
            else
            {
                Level = CompressionLevel.Standard;
            }
        }
    }
}
