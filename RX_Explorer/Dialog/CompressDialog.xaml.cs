using RX_Explorer.Class;
using System;
using System.Collections.Generic;
using System.IO;
using Windows.UI.Xaml.Controls;


namespace RX_Explorer.Dialog
{
    public sealed partial class CompressDialog : QueueContentDialog
    {
        /// <summary>
        /// 获取文件名
        /// </summary>
        public string FileName { get; private set; }

        private readonly FileSystemStorageItemBase OriginItem;

        private static readonly IReadOnlyDictionary<CompressionAlgorithm, string> AlgorithmExtensionMap = new Dictionary<CompressionAlgorithm, string>
        {
            { CompressionAlgorithm.None, string.Empty },
            { CompressionAlgorithm.GZip, ".gz" },
            { CompressionAlgorithm.BZip2, ".bz2" }
        };

        /// <summary>
        /// 获取压缩等级
        /// </summary>
        public CompressionLevel Level { get; private set; } = CompressionLevel.Undefine;

        public CompressionAlgorithm Algorithm { get; private set; } = CompressionAlgorithm.None;

        public CompressionType Type { get; private set; }

        public CompressDialog(FileSystemStorageItemBase OriginItem)
        {
            InitializeComponent();

            this.OriginItem = OriginItem;

            Initialize(OriginItem is FileSystemStorageFile);
        }

        public CompressDialog() : this(null)
        {

        }

        private void Initialize(bool ShouldDisplayGzip)
        {
            CType.Items.Add("Zip");
            CType.Items.Add("Tar");

            if (ShouldDisplayGzip)
            {
                CType.Items.Add("GZip");
                CType.Items.Add("BZip2");
            }

            CType.SelectedIndex = 0;

            CompressLevel.Items.Add(Globalization.GetString("Compression_Dialog_Level_1"));
            CompressLevel.Items.Add(Globalization.GetString("Compression_Dialog_Level_2"));
            CompressLevel.Items.Add(Globalization.GetString("Compression_Dialog_Level_3"));

            CompressLevel.SelectedIndex = 1;
        }

        private void QueueContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            if (string.IsNullOrEmpty(FName.Text))
            {
                args.Cancel = true;
                return;
            }

            switch (CType.SelectedIndex)
            {
                case 0:
                    {
                        FileName = FName.Text.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) ? FName.Text : $"{FName.Text}.zip";
                        break;
                    }
                case 1:
                    {
                        if (Algorithm == CompressionAlgorithm.None)
                        {
                            FileName = FName.Text.EndsWith(".tar", StringComparison.OrdinalIgnoreCase) ? FName.Text : $"{FName.Text}.tar";
                        }
                        else
                        {
                            string Suffix = $".tar{AlgorithmExtensionMap[Algorithm].ToLower()}";
                            FileName = FName.Text.EndsWith(Suffix, StringComparison.OrdinalIgnoreCase) ? FName.Text : FName.Text + Suffix;
                        }
                        break;
                    }
                case 2:
                    {
                        FileName = FName.Text.EndsWith(".gz", StringComparison.OrdinalIgnoreCase) ? FName.Text : $"{FName.Text}.gz";
                        break;
                    }
                case 3:
                    {
                        FileName = FName.Text.EndsWith(".bz2", StringComparison.OrdinalIgnoreCase) ? FName.Text : $"{FName.Text}.bz2";
                        break;
                    }
            }

            switch (CompressLevel.SelectedIndex)
            {
                case 0:
                    {
                        Level = CompressionLevel.Max;
                        break;
                    }
                case 1:
                    {
                        Level = CompressionLevel.Standard;
                        break;
                    }
                case 2:
                    {
                        Level = CompressionLevel.PackageOnly;
                        break;
                    }
            }
        }

        private void CType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            switch (CType.SelectedIndex)
            {
                case 0:
                    {
                        FName.Text = $"{(OriginItem == null ? Globalization.GetString("Compression_Admin_Name_Text") : (OriginItem is FileSystemStorageFile ? Path.GetFileNameWithoutExtension(OriginItem.Name) : OriginItem.Name))}.zip";
                        Type = CompressionType.Zip;
                        CompressLevel.Visibility = Windows.UI.Xaml.Visibility.Visible;
                        CAlgorithm.Visibility = Windows.UI.Xaml.Visibility.Visible;
                        CAlgorithm.Items.Clear();
                        CAlgorithm.Items.Add(CompressionAlgorithm.Deflated.ToString());
                        CAlgorithm.Items.Add(CompressionAlgorithm.None.ToString());
                        CAlgorithm.SelectedIndex = 0;

                        break;
                    }
                case 1:
                    {
                        FName.Text = $"{(OriginItem == null ? Globalization.GetString("Compression_Admin_Name_Text") : (OriginItem is FileSystemStorageFile ? Path.GetFileNameWithoutExtension(OriginItem.Name) : OriginItem.Name))}.tar";
                        Type = CompressionType.Tar;
                        CompressLevel.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
                        CAlgorithm.Visibility = Windows.UI.Xaml.Visibility.Visible;
                        CAlgorithm.Items.Clear();
                        CAlgorithm.Items.Add(CompressionAlgorithm.GZip.ToString());
                        CAlgorithm.Items.Add(CompressionAlgorithm.BZip2.ToString());
                        CAlgorithm.Items.Add(CompressionAlgorithm.None.ToString());
                        CAlgorithm.SelectedIndex = 0;

                        break;
                    }
                case 2:
                    {
                        FName.Text = $"{(OriginItem == null ? Globalization.GetString("Compression_Admin_Name_Text") : OriginItem.Name)}.gz";
                        Type = CompressionType.Gzip;
                        CompressLevel.Visibility = Windows.UI.Xaml.Visibility.Visible;
                        CAlgorithm.Visibility = Windows.UI.Xaml.Visibility.Collapsed;

                        break;
                    }
                case 3:
                    {
                        FName.Text = $"{(OriginItem == null ? Globalization.GetString("Compression_Admin_Name_Text") : OriginItem.Name)}.bz2";
                        Type = CompressionType.BZip2;
                        CompressLevel.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
                        CAlgorithm.Visibility = Windows.UI.Xaml.Visibility.Collapsed;

                        break;
                    }
            }
        }



        private void CAlgorithm_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CAlgorithm.SelectedIndex >= 0)
            {
                Algorithm = Enum.Parse<CompressionAlgorithm>(CAlgorithm.SelectedItem.ToString());

                switch (CType.SelectedIndex)
                {
                    case 0:
                        {
                            if (Algorithm == CompressionAlgorithm.None)
                            {
                                CompressLevel.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
                            }
                            else
                            {
                                CompressLevel.Visibility = Windows.UI.Xaml.Visibility.Visible;
                            }

                            break;
                        }
                    case > 0:
                        {
                            CompressLevel.IsEnabled = true;
                            CompressLevel.Visibility = Algorithm == CompressionAlgorithm.GZip ? Windows.UI.Xaml.Visibility.Visible : Windows.UI.Xaml.Visibility.Collapsed;
                            FName.Text = $"{(OriginItem == null ? Globalization.GetString("Compression_Admin_Name_Text") : (OriginItem is FileSystemStorageFile ? Path.GetFileNameWithoutExtension(OriginItem.Name) : OriginItem.Name))}.tar{AlgorithmExtensionMap[Algorithm].ToLower()}";
                            break;
                        }
                }
            }
        }

        private void FName_GotFocus(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            switch (CType.SelectedIndex)
            {
                case 0:
                case 3:
                    {
                        FName.Select(0, FName.Text.Length - 4);
                        break;
                    }
                case 1:
                    {
                        switch (CAlgorithm.SelectedIndex)
                        {
                            case 0:
                                {
                                    FName.Select(0, FName.Text.Length - 7);
                                    break;
                                }
                            case 1:
                                {
                                    FName.Select(0, FName.Text.Length - 8);
                                    break;
                                }
                            case 2:
                                {
                                    FName.Select(0, FName.Text.Length - 4);
                                    break;
                                }
                        }
                        break;
                    }
                case 2:
                    {
                        FName.Select(0, FName.Text.Length - 3);
                        break;
                    }
            }
        }
    }
}
