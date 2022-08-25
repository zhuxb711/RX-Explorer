using Nito.AsyncEx;
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
        private readonly DriveDataBase RenameDrive;
        private readonly IReadOnlyList<FileSystemStorageItemBase> RenameFileList;
        private readonly AsyncLock TextChangeLock = new AsyncLock();
        private readonly CancellationTokenSource Cancellation = new CancellationTokenSource();

        public Dictionary<string, string> DesireNameMap { get; } = new Dictionary<string, string>();

        public RenameDialog(DriveDataBase RootDrive) : this()
        {
            RenameDrive = RootDrive ?? throw new ArgumentException("Argument could not be empty", nameof(RootDrive));
            RenameText.Text = RootDrive.Name;
        }

        public RenameDialog(FileSystemStorageItemBase RenameItem) : this(new FileSystemStorageItemBase[] { RenameItem })
        {

        }

        public RenameDialog(IEnumerable<FileSystemStorageItemBase> RenameItems) : this()
        {
            if (!(RenameItems?.Any()).GetValueOrDefault())
            {
                throw new ArgumentException("Argument could not be empty", nameof(RenameItems));
            }

            RenameFileList = RenameItems.ToList();
            RenameText.Text = RenameFileList.First().Name;
        }

        private RenameDialog()
        {
            InitializeComponent();
            Loaded += RenameDialog_Loaded;
            Closed += RenameDialog_Closed;
        }

        private void RenameDialog_Closed(ContentDialog sender, ContentDialogClosedEventArgs args)
        {
            Cancellation.Cancel();
            Cancellation.Dispose();
        }

        private void RenameDialog_Loaded(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            DesireNameMap.Clear();

            if (RenameDrive != null)
            {
                string DriveName = RenameDrive.Name;
                DesireNameMap.Add(DriveName, DriveName);
                Preview.Text = $"{DriveName}\r⋙⋙   ⋙⋙   ⋙⋙\r{DriveName}";
            }
            else
            {
                if (RenameFileList.Count > 1)
                {
                    foreach (FileSystemStorageItemBase Item in RenameFileList)
                    {
                        DesireNameMap.Add(Item.Name, Item.Name);
                    }

                    Preview.Text = string.Join(Environment.NewLine + Environment.NewLine, DesireNameMap.Select((Pair) => $"{Pair.Key}\r⋙⋙   ⋙⋙   ⋙⋙\r{Pair.Value}"));
                }
                else
                {
                    string OriginName = RenameFileList.Single().Name;
                    DesireNameMap.Add(OriginName, OriginName);
                    Preview.Text = $"{OriginName}\r⋙⋙   ⋙⋙   ⋙⋙\r{OriginName}";
                }
            }
        }

        private void QueueContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            if (RenameDrive != null)
            {
                if (string.IsNullOrWhiteSpace(RenameText.Text))
                {
                    args.Cancel = true;
                    InvalidNameTip.IsOpen = true;
                }
            }
            else if (!FileSystemItemNameChecker.IsValid(RenameText.Text))
            {
                args.Cancel = true;
                InvalidNameTip.IsOpen = true;
            }
        }

        private async void RenameText_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(RenameText.Text) || !FileSystemItemNameChecker.IsValid(RenameText.Text))
            {
                PreviewArea.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
            }
            else
            {
                using (await TextChangeLock.LockAsync(Cancellation.Token))
                {
                    try
                    {
                        DesireNameMap.Clear();
                        PreviewArea.Visibility = Windows.UI.Xaml.Visibility.Visible;

                        if (RenameDrive != null)
                        {
                            string DriveName = RenameDrive.Name;
                            DesireNameMap.Add(DriveName, RenameText.Text);
                            Preview.Text = $"{DriveName}\r⋙⋙   ⋙⋙   ⋙⋙\r{RenameText.Text}";
                        }
                        else
                        {
                            FileSystemStorageItemBase BaseItem = RenameFileList.First();

                            if (RenameFileList.Count > 1)
                            {
                                if (RenameText.Text == BaseItem.Name)
                                {
                                    foreach (FileSystemStorageItemBase Item in RenameFileList)
                                    {
                                        DesireNameMap.Add(Item.Name, Item.Name);
                                    }
                                }
                                else
                                {
                                    StorageItemTypes BaseTypes = BaseItem switch
                                    {
                                        FileSystemStorageFile => StorageItemTypes.File,
                                        FileSystemStorageFolder => StorageItemTypes.Folder,
                                        _ => StorageItemTypes.None
                                    };

                                    HashSet<string> ExceptPath = new HashSet<string>();

                                    foreach (FileSystemStorageItemBase Item in RenameFileList)
                                    {
                                        string UniquePath = await GenerateUniquePath(Path.Combine(Path.GetDirectoryName(BaseItem.Path), Path.GetFileNameWithoutExtension(RenameText.Text) + Path.GetExtension(Item.Path)), BaseTypes, ExceptPath);
                                        string UniqueName = Path.GetFileName(UniquePath);

                                        ExceptPath.Add(UniquePath);
                                        DesireNameMap.Add(Item.Name, UniqueName);
                                    }
                                }

                                Preview.Text = string.Join(Environment.NewLine + Environment.NewLine, DesireNameMap.Select((Pair) => $"{Pair.Key}\r⋙⋙   ⋙⋙   ⋙⋙\r{Pair.Value}"));
                            }
                            else
                            {
                                string OriginName = BaseItem.Name;
                                DesireNameMap.Add(OriginName, RenameText.Text);
                                Preview.Text = $"{OriginName}\r⋙⋙   ⋙⋙   ⋙⋙\r{RenameText.Text}";
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        LogTracer.Log(ex, $"An exception was threw in {nameof(RenameText_TextChanged)}");
                    }
                }
            }
        }

        private static async Task<string> GenerateUniquePath(string ItemPath, StorageItemTypes Type, IEnumerable<string> ExceptPath)
        {
            string UniquePath = ItemPath;
            string Name = Type == StorageItemTypes.Folder ? Path.GetFileName(UniquePath) : Path.GetFileNameWithoutExtension(UniquePath);
            string Extension = Type == StorageItemTypes.Folder ? string.Empty : Path.GetExtension(UniquePath);
            string DirectoryPath = Path.GetDirectoryName(UniquePath);

            for (ushort Count = 1; await FileSystemStorageItemBase.CheckExistsAsync(UniquePath) || ExceptPath.Contains(UniquePath); Count++)
            {
                if (Regex.IsMatch(Name, @".*\(\d+\)"))
                {
                    UniquePath = Path.Combine(DirectoryPath, $"{Name.Substring(0, Name.LastIndexOf("(", StringComparison.InvariantCultureIgnoreCase))}({Count}){Extension}");
                }
                else
                {
                    UniquePath = Path.Combine(DirectoryPath, $"{Name} ({Count}){Extension}");
                }
            }

            return UniquePath;
        }

        private void RenameText_GotFocus(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            if (RenameDrive != null)
            {
                RenameText.SelectAll();
            }
            else if (RenameFileList.First() is FileSystemStorageFile File)
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
    }
}
