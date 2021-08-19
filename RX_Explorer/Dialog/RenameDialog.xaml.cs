using RX_Explorer.Class;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.UI.Xaml.Controls;

namespace RX_Explorer.Dialog
{
    public sealed partial class RenameDialog : QueueContentDialog
    {
        public Dictionary<string, string> DesireNameMap { get; private set; } = new Dictionary<string, string>();

        private readonly IEnumerable<FileSystemStorageItemBase> RenameItems;
        
        private readonly SemaphoreSlim TextChangeLock = new SemaphoreSlim(1,1);

        public RenameDialog(FileSystemStorageItemBase RenameItems) : this(new FileSystemStorageItemBase[] { RenameItems })
        {

        }

        public RenameDialog(IEnumerable<FileSystemStorageItemBase> RenameItems)
        {
            if (RenameItems == null || !RenameItems.Any())
            {
                throw new ArgumentException("Argument could not be empty", nameof(RenameItems));
            }

            InitializeComponent();

            this.RenameItems = RenameItems;

            RenameText.Text = RenameItems.First().Name;

            Loaded += RenameDialog_Loaded;
            Closed += RenameDialog_Closed;
        }

        private void RenameDialog_Closed(ContentDialog sender, ContentDialogClosedEventArgs args)
        {
            TextChangeLock.Dispose();
        }

        private async void RenameDialog_Loaded(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            DesireNameMap.Clear();

            if (RenameItems.Count() > 1)
            {
                FileSystemStorageItemBase BaseItem = RenameItems.First();

                StorageItemTypes BaseTypes = BaseItem switch
                {
                    FileSystemStorageFile => StorageItemTypes.File,
                    FileSystemStorageFolder => StorageItemTypes.Folder,
                    _ => StorageItemTypes.None
                };

                HashSet<string> ExceptPath = new HashSet<string>();
                List<string> StringArray = new List<string>();

                foreach (string Name in RenameItems.Select((Item) => Item.Name))
                {
                    string UniquePath = await GenerateUniquePath(BaseItem.Path, BaseTypes, ExceptPath);
                    ExceptPath.Add(UniquePath);
                    StringArray.Add($"{Name}\r⋙⋙   ⋙⋙   ⋙⋙\r{Path.GetFileName(UniquePath)}");
                    DesireNameMap.Add(Name, Path.GetFileName(UniquePath));
                }

                Preview.Text = string.Join(Environment.NewLine + Environment.NewLine, StringArray);
            }
            else
            {
                string OriginName = RenameItems.First().Name;

                DesireNameMap.Add(OriginName, OriginName);
                Preview.Text = $"{OriginName}\r⋙⋙   ⋙⋙   ⋙⋙\r{OriginName}";
            }

            if (RenameItems.First() is FileSystemStorageFile File)
            {
                if (File.Name != Path.GetExtension(File.Name))
                {
                    RenameText.Select(0, File.Name.Length - Path.GetExtension(File.Name).Length);
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
        }

        private async void RenameText_TextChanged(object sender, TextChangedEventArgs e)
        {
            await TextChangeLock.WaitAsync();

            try
            {
                DesireNameMap.Clear();

                if (RenameItems.Count() > 1)
                {
                    FileSystemStorageItemBase BaseItem = RenameItems.First();

                    StorageItemTypes BaseTypes = BaseItem switch
                    {
                        FileSystemStorageFile => StorageItemTypes.File,
                        FileSystemStorageFolder => StorageItemTypes.Folder,
                        _ => StorageItemTypes.None
                    };

                    HashSet<string> ExceptPath = new HashSet<string>();
                    List<string> StringArray = new List<string>();

                    foreach (string Name in RenameItems.Select((Item) => Item.Name))
                    {
                        string UniquePath = await GenerateUniquePath(Path.Combine(Path.GetDirectoryName(BaseItem.Path), RenameText.Text), BaseTypes, ExceptPath);
                        ExceptPath.Add(UniquePath);
                        StringArray.Add($"{Name}\r⋙⋙   ⋙⋙   ⋙⋙\r{Path.GetFileName(UniquePath)}");
                        DesireNameMap.Add(Name, Path.GetFileName(UniquePath));
                    }

                    Preview.Text = string.Join(Environment.NewLine + Environment.NewLine, StringArray);
                }
                else
                {
                    string OriginName = RenameItems.First().Name;

                    DesireNameMap.Add(OriginName, RenameText.Text);
                    Preview.Text = $"{OriginName}\r⋙⋙   ⋙⋙   ⋙⋙\r{RenameText.Text}";
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex);
            }
            finally
            {
                TextChangeLock.Release();
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

        private static async Task<string> GenerateUniquePath(string ItemPath, StorageItemTypes Type, IEnumerable<string> ExceptPath)
        {
            string UniquePath = ItemPath;

            switch (Type)
            {
                case StorageItemTypes.File:
                    {
                        string NameWithoutExt = Path.GetFileNameWithoutExtension(ItemPath);
                        string Extension = Path.GetExtension(ItemPath);
                        string Directory = Path.GetDirectoryName(ItemPath);

                        for (ushort Count = 1; await FileSystemStorageItemBase.CheckExistAsync(UniquePath) || ExceptPath.Contains(UniquePath); Count++)
                        {
                            if (Regex.IsMatch(NameWithoutExt, @".*\(\d+\)"))
                            {
                                UniquePath = Path.Combine(Directory, $"{NameWithoutExt.Substring(0, NameWithoutExt.LastIndexOf("(", StringComparison.InvariantCultureIgnoreCase))}({Count}){Extension}");
                            }
                            else
                            {
                                UniquePath = Path.Combine(Directory, $"{NameWithoutExt} ({Count}){Extension}");
                            }
                        }

                        break;
                    }
                case StorageItemTypes.Folder:
                    {
                        string Directory = Path.GetDirectoryName(ItemPath);
                        string Name = Path.GetFileName(ItemPath);

                        for (ushort Count = 1; await FileSystemStorageItemBase.CheckExistAsync(UniquePath) || ExceptPath.Contains(UniquePath); Count++)
                        {
                            if (Regex.IsMatch(Name, @".*\(\d+\)"))
                            {
                                UniquePath = Path.Combine(Directory, $"{Name.Substring(0, Name.LastIndexOf("(", StringComparison.InvariantCultureIgnoreCase))}({Count})");
                            }
                            else
                            {
                                UniquePath = Path.Combine(Directory, $"{Name} ({Count})");
                            }
                        }

                        break;
                    }
                default:
                    {
                        break;
                    }
            }

            return UniquePath;
        }
    }
}
