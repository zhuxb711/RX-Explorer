using ICSharpCode.SharpZipLib.Zip;
using OpenCV;
using RX_Explorer.Class;
using RX_Explorer.Dialog;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.ApplicationModel.DataTransfer;
using Windows.ApplicationModel.DataTransfer.DragDrop;
using Windows.Devices.Radios;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.System;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;
using ZXing;
using ZXing.QrCode;
using ZXing.QrCode.Internal;
using TreeViewNode = Microsoft.UI.Xaml.Controls.TreeViewNode;

namespace RX_Explorer
{
    public sealed partial class FilePresenter : Page
    {
        public ObservableCollection<FileSystemStorageItem> FileCollection { get; private set; }
        private static FileSystemStorageItem[] CopyFiles;
        private static FileSystemStorageItem[] MoveFiles;
        public static List<string> CopyAndMoveRecord { get; private set; } = new List<string>();

        private readonly Dictionary<SortTarget, SortDirection> SortMap = new Dictionary<SortTarget, SortDirection>
        {
            {SortTarget.Name,SortDirection.Ascending },
            {SortTarget.Type,SortDirection.Ascending },
            {SortTarget.ModifiedTime,SortDirection.Ascending },
            {SortTarget.Size,SortDirection.Ascending }
        };

        private FileControl FileControlInstance;

        private int DropLock = 0;

        private int ViewDropLock = 0;

        private volatile FileSystemStorageItem StayInItem;

        private readonly DispatcherTimer PointerHoverTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(700) };

        private ListViewBase itemPresenter;
        public ListViewBase ItemPresenter
        {
            get
            {
                return itemPresenter;
            }
            set
            {
                if (value != itemPresenter)
                {
                    itemPresenter = value;

                    if (value is GridView)
                    {
                        GridViewControl.Visibility = Visibility.Visible;
                        ListViewControl.Visibility = Visibility.Collapsed;
                        GridViewControl.ItemsSource = FileCollection;
                        ListViewControl.ItemsSource = null;
                    }
                    else
                    {
                        ListViewControl.Visibility = Visibility.Visible;
                        GridViewControl.Visibility = Visibility.Collapsed;
                        ListViewControl.ItemsSource = FileCollection;
                        GridViewControl.ItemsSource = null;
                    }
                }
            }
        }

        WiFiShareProvider WiFiProvider;
        FileSystemStorageItem TabTarget = null;

        public FileSystemStorageItem SelectedItem
        {
            get
            {
                return ItemPresenter.SelectedItem as FileSystemStorageItem;
            }
            set
            {
                ItemPresenter.SelectedItem = value;
            }
        }

        public List<FileSystemStorageItem> SelectedItems
        {
            get
            {
                return ItemPresenter.SelectedItems.Select((Item) => Item as FileSystemStorageItem).ToList();
            }
        }

        public FilePresenter()
        {
            InitializeComponent();

            FileCollection = new ObservableCollection<FileSystemStorageItem>();
            FileCollection.CollectionChanged += FileCollection_CollectionChanged;

            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            ZipStrings.CodePage = 936;

            Application.Current.Suspending += Current_Suspending;
            Loaded += FilePresenter_Loaded;
            Unloaded += FilePresenter_Unloaded;

            PointerHoverTimer.Tick += Timer_Tick;
        }

        private void FilePresenter_Unloaded(object sender, RoutedEventArgs e)
        {
            CoreWindow.GetForCurrentThread().KeyDown -= Window_KeyDown;
        }

        private void FilePresenter_Loaded(object sender, RoutedEventArgs e)
        {
            CoreWindow.GetForCurrentThread().KeyDown += Window_KeyDown;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            if (e.Parameter is FileControl Instance)
            {
                FileControlInstance = Instance;

                if (!TabViewContainer.ThisPage.FFInstanceContainer.ContainsKey(Instance))
                {
                    TabViewContainer.ThisPage.FFInstanceContainer.Add(Instance, this);
                }
            }
        }

        private async void Window_KeyDown(CoreWindow sender, KeyEventArgs args)
        {
            var WindowInstance = CoreWindow.GetForCurrentThread();
            var CtrlState = WindowInstance.GetKeyState(VirtualKey.Control);
            var ShiftState = WindowInstance.GetKeyState(VirtualKey.Shift);

            if (!FileControlInstance.IsSearchOrPathBoxFocused && !QueueContentDialog.IsRunningOrWaiting)
            {
                args.Handled = true;

                switch (args.VirtualKey)
                {
                    case VirtualKey.Space when SelectedItem != null && SettingControl.IsQuicklookAvailable && SettingControl.IsQuicklookEnable:
                        {
                            await FullTrustExcutorController.ViewWithQuicklook(SelectedItem.Path).ConfigureAwait(false);
                            break;
                        }
                    case VirtualKey.Delete:
                        {
                            Delete_Click(null, null);
                            break;
                        }
                    case VirtualKey.F2:
                        {
                            Rename_Click(null, null);
                            break;
                        }
                    case VirtualKey.F5:
                        {
                            Refresh_Click(null, null);
                            break;
                        }
                    case VirtualKey.Enter when SelectedItem is FileSystemStorageItem Item:
                        {
                            await EnterSelectedItem(Item).ConfigureAwait(false);
                            break;
                        }
                    case VirtualKey.Back when FileControlInstance.Nav.CurrentSourcePageType.Name == nameof(FilePresenter) && !QueueContentDialog.IsRunningOrWaiting && FileControlInstance.GoBackRecord.IsEnabled:
                        {
                            FileControlInstance.GoBackRecord_Click(null, null);
                            break;
                        }
                    case VirtualKey.L when CtrlState.HasFlag(CoreVirtualKeyStates.Down):
                        {
                            FileControlInstance.AddressBox.Focus(FocusState.Programmatic);
                            break;
                        }
                    case VirtualKey.V when CtrlState.HasFlag(CoreVirtualKeyStates.Down):
                        {
                            Paste_Click(null, null);
                            break;
                        }
                    case VirtualKey.A when CtrlState.HasFlag(CoreVirtualKeyStates.Down) && SelectedItem == null:
                        {
                            ItemPresenter.SelectAll();
                            break;
                        }
                    case VirtualKey.C when CtrlState.HasFlag(CoreVirtualKeyStates.Down):
                        {
                            Copy_Click(null, null);
                            break;
                        }
                    case VirtualKey.X when CtrlState.HasFlag(CoreVirtualKeyStates.Down):
                        {
                            Cut_Click(null, null);
                            break;
                        }
                    case VirtualKey.D when CtrlState.HasFlag(CoreVirtualKeyStates.Down):
                        {
                            Delete_Click(null, null);
                            break;
                        }
                    case VirtualKey.F when CtrlState.HasFlag(CoreVirtualKeyStates.Down):
                        {
                            FileControlInstance.GlobeSearch.Focus(FocusState.Programmatic);
                            break;
                        }
                    case VirtualKey.N when ShiftState.HasFlag(CoreVirtualKeyStates.Down) && CtrlState.HasFlag(CoreVirtualKeyStates.Down):
                        {
                            CreateFolder_Click(null, null);
                            break;
                        }
                    case VirtualKey.Z when CtrlState.HasFlag(CoreVirtualKeyStates.Down) && CopyAndMoveRecord.Count > 0:
                        {
                            await Ctrl_Z_Click().ConfigureAwait(false);
                            break;
                        }
                }
            }
        }

        private void Current_Suspending(object sender, SuspendingEventArgs e)
        {
            WiFiProvider?.Dispose();
        }

        private void FileCollection_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (e.Action != System.Collections.Specialized.NotifyCollectionChangedAction.Reset)
            {
                HasFile.Visibility = FileCollection.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        /// <summary>
        /// 关闭右键菜单
        /// </summary>
        private void Restore()
        {
            FileFlyout.Hide();
            FolderFlyout.Hide();
            EmptyFlyout.Hide();
            MixedFlyout.Hide();
        }

        private async Task Ctrl_Z_Click()
        {
            await LoadingActivation(true, Globalization.GetString("Progress_Tip_Undoing")).ConfigureAwait(true);

            bool IsItemNotFound = false;

            foreach (string Record in CopyAndMoveRecord)
            {
                string[] SplitGroup = Record.Split("||", StringSplitOptions.RemoveEmptyEntries);

                try
                {
                    switch (SplitGroup[1])
                    {
                        case "Move":
                            {
                                if (FileControlInstance.CurrentFolder.Path == Path.GetDirectoryName(SplitGroup[3]))
                                {
                                    StorageFolder OriginFolder = await StorageFolder.GetFolderFromPathAsync(Path.GetDirectoryName(SplitGroup[0]));

                                    switch (SplitGroup[2])
                                    {
                                        case "File":
                                            {
                                                if ((await FileControlInstance.CurrentFolder.TryGetItemAsync(Path.GetFileName(SplitGroup[3]))) is StorageFile File)
                                                {
                                                    await File.MoveAsync(OriginFolder, File.Name, NameCollisionOption.GenerateUniqueName);
                                                }
                                                else
                                                {
                                                    IsItemNotFound = true;
                                                }
                                                break;
                                            }
                                        case "Folder":
                                            {
                                                StorageFolder Folder = await StorageFolder.GetFolderFromPathAsync(SplitGroup[3]);

                                                StorageFolder NewFolder = await OriginFolder.CreateFolderAsync(Folder.Name, CreationCollisionOption.OpenIfExists);
                                                await Folder.MoveSubFilesAndSubFoldersAsync(NewFolder).ConfigureAwait(true);
                                                await Folder.DeleteAllSubFilesAndFolders().ConfigureAwait(true);
                                                await Folder.DeleteAsync();

                                                if (!SettingControl.IsDetachTreeViewAndPresenter)
                                                {
                                                    await FileControlInstance.FolderTree.RootNodes[0].UpdateAllSubNode().ConfigureAwait(true);
                                                }
                                                break;
                                            }
                                    }

                                    if (FileCollection.FirstOrDefault((Item) => Item.Path == SplitGroup[3]) is FileSystemStorageItem Item)
                                    {
                                        FileCollection.Remove(Item);
                                    }
                                }
                                else if (FileControlInstance.CurrentFolder.Path == Path.GetDirectoryName(SplitGroup[0]))
                                {
                                    switch (SplitGroup[2])
                                    {
                                        case "File":
                                            {
                                                StorageFolder TargetFolder = await StorageFolder.GetFolderFromPathAsync(Path.GetDirectoryName(SplitGroup[3]));
                                                if ((await TargetFolder.TryGetItemAsync(Path.GetFileName(SplitGroup[3]))) is StorageFile File)
                                                {
                                                    await File.MoveAsync(FileControlInstance.CurrentFolder, File.Name, NameCollisionOption.GenerateUniqueName);
                                                    FileCollection.Add(new FileSystemStorageItem(File, await File.GetSizeRawDataAsync().ConfigureAwait(true), await File.GetThumbnailBitmapAsync().ConfigureAwait(true), await File.GetModifiedTimeAsync().ConfigureAwait(true)));
                                                }
                                                else
                                                {
                                                    IsItemNotFound = true;
                                                }

                                                break;
                                            }
                                        case "Folder":
                                            {
                                                StorageFolder Folder = await StorageFolder.GetFolderFromPathAsync(SplitGroup[3]);
                                                StorageFolder NewFolder = await FileControlInstance.CurrentFolder.CreateFolderAsync(Folder.Name, CreationCollisionOption.OpenIfExists);
                                                await Folder.MoveSubFilesAndSubFoldersAsync(NewFolder).ConfigureAwait(true);
                                                await Folder.DeleteAllSubFilesAndFolders().ConfigureAwait(true);
                                                await Folder.DeleteAsync();

                                                if (!SettingControl.IsDetachTreeViewAndPresenter)
                                                {
                                                    await FileControlInstance.FolderTree.RootNodes[0].UpdateAllSubNode().ConfigureAwait(true);
                                                }

                                                if (FileCollection.All((Item) => Item.Path != NewFolder.Path))
                                                {
                                                    FileCollection.Add(new FileSystemStorageItem(NewFolder, await NewFolder.GetModifiedTimeAsync().ConfigureAwait(true)));
                                                }
                                                break;
                                            }
                                    }
                                }
                                else
                                {
                                    StorageFolder OriginFolder = await StorageFolder.GetFolderFromPathAsync(Path.GetDirectoryName(SplitGroup[0]));

                                    switch (SplitGroup[2])
                                    {
                                        case "File":
                                            {
                                                StorageFile File = await StorageFile.GetFileFromPathAsync(SplitGroup[3]);
                                                await File.MoveAsync(OriginFolder, File.Name, NameCollisionOption.GenerateUniqueName);

                                                break;
                                            }
                                        case "Folder":
                                            {
                                                StorageFolder Folder = await StorageFolder.GetFolderFromPathAsync(SplitGroup[3]);
                                                StorageFolder NewFolder = await OriginFolder.CreateFolderAsync(Folder.Name, CreationCollisionOption.OpenIfExists);
                                                await Folder.MoveSubFilesAndSubFoldersAsync(NewFolder).ConfigureAwait(true);
                                                await Folder.DeleteAllSubFilesAndFolders().ConfigureAwait(true);
                                                await Folder.DeleteAsync();

                                                if (!SettingControl.IsDetachTreeViewAndPresenter)
                                                {
                                                    await FileControlInstance.FolderTree.RootNodes[0].UpdateAllSubNode().ConfigureAwait(true);
                                                }
                                                break;
                                            }
                                    }
                                }

                                break;
                            }
                        case "Copy":
                            {
                                if (FileControlInstance.CurrentFolder.Path == Path.GetDirectoryName(SplitGroup[3]))
                                {
                                    switch (SplitGroup[2])
                                    {
                                        case "File":
                                            {
                                                if ((await FileControlInstance.CurrentFolder.TryGetItemAsync(Path.GetFileName(SplitGroup[3]))) is StorageFile File)
                                                {
                                                    await File.DeleteAsync(StorageDeleteOption.PermanentDelete);
                                                }

                                                break;
                                            }
                                        case "Folder":
                                            {
                                                StorageFolder Folder = await StorageFolder.GetFolderFromPathAsync(SplitGroup[3]);
                                                await Folder.DeleteAllSubFilesAndFolders().ConfigureAwait(true);
                                                await Folder.DeleteAsync();

                                                if (!SettingControl.IsDetachTreeViewAndPresenter)
                                                {
                                                    await FileControlInstance.FolderTree.RootNodes[0].UpdateAllSubNode().ConfigureAwait(true);
                                                }
                                                break;
                                            }
                                    }

                                    if (FileCollection.FirstOrDefault((Item) => Item.Path == SplitGroup[3]) is FileSystemStorageItem Item)
                                    {
                                        FileCollection.Remove(Item);
                                    }
                                }
                                else
                                {
                                    StorageFile File = await StorageFile.GetFileFromPathAsync(SplitGroup[3]);
                                    await File.DeleteAsync(StorageDeleteOption.PermanentDelete);
                                }
                                break;
                            }
                    }
                }
                catch
                {
                    IsItemNotFound = true;
                }
            }

            if (IsItemNotFound)
            {
                QueueContentDialog Dialog = new QueueContentDialog
                {
                    Title = Globalization.GetString("Common_Dialog_WarningTitle"),
                    Content = Globalization.GetString("QueueDialog_UndoFailure_Content"),
                    CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                };
                _ = await Dialog.ShowAsync().ConfigureAwait(false);
            }

            await LoadingActivation(false).ConfigureAwait(true);
        }

        public void Copy_Click(object sender, RoutedEventArgs e)
        {
            Restore();

            if (MoveFiles != null)
            {
                MoveFiles = null;
            }

            CopyFiles = SelectedItems.ToArray();
        }

        public async void Paste_Click(object sender, RoutedEventArgs e)
        {
            Restore();

            if (MoveFiles != null)
            {
                await LoadingActivation(true, Globalization.GetString("Progress_Tip_Moving")).ConfigureAwait(true);

                if (Path.GetDirectoryName(MoveFiles.FirstOrDefault().Path) == FileControlInstance.CurrentFolder.Path)
                {
                    goto FLAG;
                }

                bool IsItemNotFound = false;
                bool IsUnauthorized = false;
                bool IsSpaceError = false;
                bool IsCaptured = false;
                CopyAndMoveRecord.Clear();

                foreach (FileSystemStorageItem StorageItem in MoveFiles)
                {
                    try
                    {
                        IStorageItem Item = await StorageItem.GetStorageItem().ConfigureAwait(true);

                        if (Item is StorageFile File)
                        {
                            if (!await File.CheckExist().ConfigureAwait(true))
                            {
                                IsItemNotFound = true;
                                continue;
                            }

                            CopyAndMoveRecord.Add($"{StorageItem.Path}||Move||File||{Path.Combine(FileControlInstance.CurrentFolder.Path, StorageItem.Name)}");

                            await File.MoveAsync(FileControlInstance.CurrentFolder, File.Name, NameCollisionOption.GenerateUniqueName);
                            if (FileCollection.Count > 0)
                            {
                                int Index = FileCollection.IndexOf(FileCollection.FirstOrDefault((Item) => Item.StorageType == StorageItemTypes.File));
                                if (Index == -1)
                                {
                                    FileCollection.Add(new FileSystemStorageItem(File, await File.GetSizeRawDataAsync().ConfigureAwait(true), await File.GetThumbnailBitmapAsync().ConfigureAwait(true), await File.GetModifiedTimeAsync().ConfigureAwait(true)));
                                }
                                else
                                {
                                    FileCollection.Insert(Index, new FileSystemStorageItem(File, await File.GetSizeRawDataAsync().ConfigureAwait(true), await File.GetThumbnailBitmapAsync().ConfigureAwait(true), await File.GetModifiedTimeAsync().ConfigureAwait(true)));
                                }
                            }
                            else
                            {
                                FileCollection.Add(new FileSystemStorageItem(File, await File.GetSizeRawDataAsync().ConfigureAwait(true), await File.GetThumbnailBitmapAsync().ConfigureAwait(true), await File.GetModifiedTimeAsync().ConfigureAwait(true)));
                            }
                        }
                        else if (Item is StorageFolder Folder)
                        {
                            if (!await Folder.CheckExist().ConfigureAwait(true))
                            {
                                IsItemNotFound = true;
                                continue;
                            }

                            CopyAndMoveRecord.Add($"{StorageItem.Path}||Move||Folder||{Path.Combine(FileControlInstance.CurrentFolder.Path, StorageItem.Name)}");

                            StorageFolder NewFolder = await FileControlInstance.CurrentFolder.CreateFolderAsync(Folder.Name, CreationCollisionOption.OpenIfExists);
                            await Folder.MoveSubFilesAndSubFoldersAsync(NewFolder).ConfigureAwait(true);
                            await Folder.DeleteAsync(StorageDeleteOption.PermanentDelete);

                            if (FileCollection.Where((It) => It.StorageType == StorageItemTypes.Folder).All((Item) => Item.Name != NewFolder.Name))
                            {
                                FileCollection.Insert(0, new FileSystemStorageItem(NewFolder, await NewFolder.GetModifiedTimeAsync().ConfigureAwait(true)));
                            }

                            if (!SettingControl.IsDetachTreeViewAndPresenter)
                            {
                                await FileControlInstance.FolderTree.RootNodes[0].UpdateAllSubNode().ConfigureAwait(true);
                            }
                        }
                    }
                    catch (UnauthorizedAccessException)
                    {
                        IsUnauthorized = true;
                    }
                    catch (System.Runtime.InteropServices.COMException)
                    {
                        IsSpaceError = true;
                    }
                    catch (Exception)
                    {
                        IsCaptured = true;
                    }
                }

                if (IsItemNotFound)
                {
                    QueueContentDialog Dialog = new QueueContentDialog
                    {
                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                        Content = Globalization.GetString("QueueDialog_MoveFailForNotExist_Content"),
                        CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                    };
                    _ = await Dialog.ShowAsync().ConfigureAwait(true);
                }
                else if (IsUnauthorized)
                {
                    QueueContentDialog dialog = new QueueContentDialog
                    {
                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                        Content = Globalization.GetString("QueueDialog_UnauthorizedPaste_Content"),
                        PrimaryButtonText = Globalization.GetString("Common_Dialog_NowButton"),
                        CloseButtonText = Globalization.GetString("Common_Dialog_LaterButton")
                    };

                    if (await dialog.ShowAsync().ConfigureAwait(true) == ContentDialogResult.Primary)
                    {
                        _ = await Launcher.LaunchFolderAsync(FileControlInstance.CurrentFolder);
                    }
                }
                else if (IsSpaceError)
                {
                    QueueContentDialog QueueContenDialog = new QueueContentDialog
                    {
                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                        Content = Globalization.GetString("QueueDialog_MoveFail_FreeSpaceCritical_Content"),
                        CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                    };

                    _ = await QueueContenDialog.ShowAsync().ConfigureAwait(true);
                }
                else if (IsCaptured)
                {
                    QueueContentDialog dialog = new QueueContentDialog
                    {
                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                        Content = Globalization.GetString("QueueDialog_MoveFail_Captured_Content"),
                        CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                    };

                    _ = await dialog.ShowAsync().ConfigureAwait(true);
                }
            }
            else if (CopyFiles != null)
            {
                await LoadingActivation(true, Globalization.GetString("Progress_Tip_Copying")).ConfigureAwait(true);

                bool IsItemNotFound = false;
                bool IsUnauthorized = false;
                bool IsSpaceError = false;
                CopyAndMoveRecord.Clear();

                foreach (FileSystemStorageItem StorageItem in CopyFiles)
                {
                    try
                    {
                        IStorageItem Item = await StorageItem.GetStorageItem().ConfigureAwait(true);

                        if (Item is StorageFile File)
                        {
                            if (!await File.CheckExist().ConfigureAwait(true))
                            {
                                IsItemNotFound = true;
                                continue;
                            }

                            CopyAndMoveRecord.Add($"{StorageItem.Path}||Copy||File||{Path.Combine(FileControlInstance.CurrentFolder.Path, StorageItem.Name)}");

                            StorageFile NewFile = await File.CopyAsync(FileControlInstance.CurrentFolder, File.Name, NameCollisionOption.GenerateUniqueName);
                            if (FileCollection.Count > 0)
                            {
                                int Index = FileCollection.IndexOf(FileCollection.FirstOrDefault((Item) => Item.StorageType == StorageItemTypes.File));
                                if (Index == -1)
                                {
                                    FileCollection.Add(new FileSystemStorageItem(NewFile, await NewFile.GetSizeRawDataAsync().ConfigureAwait(true), await NewFile.GetThumbnailBitmapAsync().ConfigureAwait(true), await NewFile.GetModifiedTimeAsync().ConfigureAwait(true)));
                                }
                                else
                                {
                                    FileCollection.Insert(Index, new FileSystemStorageItem(NewFile, await NewFile.GetSizeRawDataAsync().ConfigureAwait(true), await NewFile.GetThumbnailBitmapAsync().ConfigureAwait(true), await NewFile.GetModifiedTimeAsync().ConfigureAwait(true)));
                                }
                            }
                            else
                            {
                                FileCollection.Add(new FileSystemStorageItem(NewFile, await NewFile.GetSizeRawDataAsync().ConfigureAwait(true), await NewFile.GetThumbnailBitmapAsync().ConfigureAwait(true), await NewFile.GetModifiedTimeAsync().ConfigureAwait(true)));
                            }
                        }
                        else if (Item is StorageFolder Folder)
                        {
                            if (!await Folder.CheckExist().ConfigureAwait(true))
                            {
                                IsItemNotFound = true;
                                continue;
                            }

                            CopyAndMoveRecord.Add($"{StorageItem.Path}||Copy||Folder||{Path.Combine(FileControlInstance.CurrentFolder.Path, StorageItem.Name)}");

                            StorageFolder NewFolder = await FileControlInstance.CurrentFolder.CreateFolderAsync(Folder.Name, CreationCollisionOption.OpenIfExists);
                            await Folder.CopySubFilesAndSubFoldersAsync(NewFolder).ConfigureAwait(true);

                            if (FileCollection.Where((It) => It.StorageType == StorageItemTypes.Folder).All((Item) => Item.Name != NewFolder.Name))
                            {
                                FileCollection.Insert(0, new FileSystemStorageItem(NewFolder, await NewFolder.GetModifiedTimeAsync().ConfigureAwait(true)));
                            }

                            if (!SettingControl.IsDetachTreeViewAndPresenter)
                            {
                                await FileControlInstance.FolderTree.RootNodes[0].UpdateAllSubNode().ConfigureAwait(true);
                            }
                        }
                    }
                    catch (UnauthorizedAccessException)
                    {
                        IsUnauthorized = true;
                    }
                    catch (System.Runtime.InteropServices.COMException)
                    {
                        IsSpaceError = true;
                    }
                }

                if (IsItemNotFound)
                {
                    QueueContentDialog Dialog = new QueueContentDialog
                    {
                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                        Content = Globalization.GetString("QueueDialog_CopyFailForNotExist_Content"),
                        CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                    };

                    _ = await Dialog.ShowAsync().ConfigureAwait(true);
                }
                else if (IsUnauthorized)
                {
                    QueueContentDialog dialog = new QueueContentDialog
                    {
                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                        Content = Globalization.GetString("QueueDialog_UnauthorizedPaste_Content"),
                        PrimaryButtonText = Globalization.GetString("Common_Dialog_NowButton"),
                        CloseButtonText = Globalization.GetString("Common_Dialog_LaterButton")
                    };

                    if (await dialog.ShowAsync().ConfigureAwait(true) == ContentDialogResult.Primary)
                    {
                        _ = await Launcher.LaunchFolderAsync(FileControlInstance.CurrentFolder);
                    }
                }
                else if (IsSpaceError)
                {
                    QueueContentDialog QueueContenDialog = new QueueContentDialog
                    {
                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                        Content = Globalization.GetString("QueueDialog_CopyFail_FreeSpaceCritical_Content"),
                        CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                    };

                    _ = await QueueContenDialog.ShowAsync().ConfigureAwait(true);
                }
            }

        FLAG:
            MoveFiles = null;
            CopyFiles = null;
            Paste.IsEnabled = false;

            await LoadingActivation(false).ConfigureAwait(false);
        }

        public void Cut_Click(object sender, RoutedEventArgs e)
        {
            Restore();

            if (CopyFiles != null)
            {
                CopyFiles = null;
            }

            MoveFiles = SelectedItems.ToArray();
        }

        public async void Delete_Click(object sender, RoutedEventArgs e)
        {
            Restore();

            bool IsItemNotFound = false;
            bool IsUnauthorized = false;
            bool IsCaptured = false;

            if (SelectedItems.Count == 1)
            {
                FileSystemStorageItem ItemToDelete = SelectedItems.FirstOrDefault();

                QueueContentDialog QueueContenDialog = new QueueContentDialog
                {
                    Title = Globalization.GetString("Common_Dialog_WarningTitle"),
                    PrimaryButtonText = Globalization.GetString("Common_Dialog_ContinueButton"),
                    Content = Globalization.GetString("QueueDialog_DeleteFile_Content"),
                    CloseButtonText = Globalization.GetString("Common_Dialog_CancelButton")
                };

                if ((await QueueContenDialog.ShowAsync().ConfigureAwait(true)) == ContentDialogResult.Primary)
                {
                    await LoadingActivation(true, Globalization.GetString("Progress_Tip_Deleting")).ConfigureAwait(true);

                    if ((await ItemToDelete.GetStorageItem().ConfigureAwait(true)) is StorageFile File)
                    {
                        if (!await File.CheckExist().ConfigureAwait(true))
                        {
                            IsItemNotFound = true;
                        }

                        try
                        {
                            await File.DeleteAsync(StorageDeleteOption.PermanentDelete);

                            FileCollection.Remove(ItemToDelete);
                        }
                        catch (UnauthorizedAccessException)
                        {
                            IsUnauthorized = true;
                        }
                        catch (Exception)
                        {
                            IsCaptured = true;
                        }
                    }
                    else if ((await ItemToDelete.GetStorageItem().ConfigureAwait(true)) is StorageFolder Folder)
                    {
                        if (!await Folder.CheckExist().ConfigureAwait(true))
                        {
                            IsItemNotFound = true;
                        }

                        try
                        {
                            await Folder.DeleteAllSubFilesAndFolders().ConfigureAwait(true);
                            await Folder.DeleteAsync(StorageDeleteOption.PermanentDelete);

                            FileCollection.Remove(ItemToDelete);

                            if (!SettingControl.IsDetachTreeViewAndPresenter)
                            {
                                await FileControlInstance.FolderTree.RootNodes[0].UpdateAllSubNode().ConfigureAwait(true);
                            }
                        }
                        catch (UnauthorizedAccessException)
                        {
                            IsUnauthorized = true;
                        }
                        catch (Exception)
                        {
                            IsCaptured = true;
                        }
                    }
                }
            }
            else
            {
                QueueContentDialog QueueContenDialog = new QueueContentDialog
                {
                    Title = Globalization.GetString("Common_Dialog_WarningTitle"),
                    PrimaryButtonText = Globalization.GetString("Common_Dialog_ContinueButton"),
                    Content = Globalization.GetString("QueueDialog_DeleteFiles_Content"),
                    CloseButtonText = Globalization.GetString("Common_Dialog_CancelButton")
                };

                if ((await QueueContenDialog.ShowAsync().ConfigureAwait(true)) == ContentDialogResult.Primary)
                {
                    await LoadingActivation(true, Globalization.GetString("Progress_Tip_Deleting")).ConfigureAwait(true);

                    foreach (FileSystemStorageItem ItemToDelete in SelectedItems)
                    {
                        if ((await ItemToDelete.GetStorageItem().ConfigureAwait(true)) is StorageFile File)
                        {
                            if (!await File.CheckExist().ConfigureAwait(true))
                            {
                                IsItemNotFound = true;
                            }

                            try
                            {
                                await File.DeleteAsync(StorageDeleteOption.PermanentDelete);

                                FileCollection.Remove(ItemToDelete);
                            }
                            catch (UnauthorizedAccessException)
                            {
                                IsUnauthorized = true;
                            }
                            catch (Exception)
                            {
                                IsCaptured = true;
                            }
                        }
                        else if ((await ItemToDelete.GetStorageItem().ConfigureAwait(true)) is StorageFolder Folder)
                        {
                            if (!await Folder.CheckExist().ConfigureAwait(true))
                            {
                                IsItemNotFound = true;
                            }

                            try
                            {
                                await Folder.DeleteAllSubFilesAndFolders().ConfigureAwait(true);
                                await Folder.DeleteAsync(StorageDeleteOption.PermanentDelete);

                                FileCollection.Remove(ItemToDelete);

                                if (!SettingControl.IsDetachTreeViewAndPresenter)
                                {
                                    await FileControlInstance.FolderTree.RootNodes[0].UpdateAllSubNode().ConfigureAwait(true);
                                }
                            }
                            catch (UnauthorizedAccessException)
                            {
                                IsUnauthorized = true;
                            }
                            catch (Exception)
                            {
                                IsCaptured = true;
                            }
                        }
                    }
                }
            }

            if (IsItemNotFound)
            {
                QueueContentDialog Dialog = new QueueContentDialog
                {
                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                    Content = Globalization.GetString("QueueDialog_DeleteItemError_Content"),
                    CloseButtonText = Globalization.GetString("Common_Dialog_RefreshButton")
                };
                _ = await Dialog.ShowAsync().ConfigureAwait(true);

                await FileControlInstance.DisplayItemsInFolder(FileControlInstance.CurrentNode, true).ConfigureAwait(true);
            }
            else if (IsUnauthorized)
            {
                QueueContentDialog dialog = new QueueContentDialog
                {
                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                    Content = Globalization.GetString("QueueDialog_UnauthorizedPaste_Content"),
                    PrimaryButtonText = Globalization.GetString("Common_Dialog_NowButton"),
                    CloseButtonText = Globalization.GetString("Common_Dialog_LaterButton")
                };

                if (await dialog.ShowAsync().ConfigureAwait(true) == ContentDialogResult.Primary)
                {
                    _ = await Launcher.LaunchFolderAsync(FileControlInstance.CurrentFolder);
                }
            }
            else if (IsCaptured)
            {
                QueueContentDialog dialog = new QueueContentDialog
                {
                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                    Content = Globalization.GetString("QueueDialog_MoveFail_Captured_Content"),
                    CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                };

                _ = await dialog.ShowAsync().ConfigureAwait(true);
            }

            await LoadingActivation(false).ConfigureAwait(false);
        }

        /// <summary>
        /// 激活或关闭正在加载提示
        /// </summary>
        /// <param name="IsLoading">激活或关闭</param>
        /// <param name="Info">提示内容</param>
        /// <param name="DisableProbarIndeterminate">是否使用条状进度条替代圆形进度条</param>
        public async Task LoadingActivation(bool IsLoading, string Info = null)
        {
            if (IsLoading)
            {
                if (HasFile.Visibility == Visibility.Visible)
                {
                    HasFile.Visibility = Visibility.Collapsed;
                }

                ProgressInfo.Text = Info + "...";

                MainPage.ThisPage.IsAnyTaskRunning = true;
            }
            else
            {
                await Task.Delay(1000).ConfigureAwait(true);
                MainPage.ThisPage.IsAnyTaskRunning = false;
            }

            LoadingControl.IsLoading = IsLoading;
        }

        public async void Rename_Click(object sender, RoutedEventArgs e)
        {
            Restore();

            if (SelectedItems.Count > 1)
            {
                QueueContentDialog Dialog = new QueueContentDialog
                {
                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                    Content = Globalization.GetString("QueueDialog_RenameNumError_Content"),
                    CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                };

                _ = await Dialog.ShowAsync().ConfigureAwait(true);
                return;
            }

            if (SelectedItem is FileSystemStorageItem RenameItem)
            {
                if ((await RenameItem.GetStorageItem().ConfigureAwait(true)) is StorageFile File)
                {
                    if (!await File.CheckExist().ConfigureAwait(true))
                    {
                        QueueContentDialog Dialog = new QueueContentDialog
                        {
                            Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                            Content = Globalization.GetString("QueueDialog_LocateFileFailure_Content"),
                            CloseButtonText = Globalization.GetString("Common_Dialog_RefreshButton")
                        };
                        _ = await Dialog.ShowAsync().ConfigureAwait(true);

                        if (SettingControl.IsDetachTreeViewAndPresenter)
                        {
                            await FileControlInstance.DisplayItemsInFolder(FileControlInstance.CurrentFolder, true).ConfigureAwait(false);
                        }
                        else
                        {
                            await FileControlInstance.DisplayItemsInFolder(FileControlInstance.CurrentNode, true).ConfigureAwait(false);
                        }
                        return;
                    }

                    RenameDialog dialog = new RenameDialog(RenameItem.Name);
                    if ((await dialog.ShowAsync().ConfigureAwait(true)) == ContentDialogResult.Primary)
                    {
                        if (dialog.DesireName == RenameItem.Type)
                        {
                            QueueContentDialog content = new QueueContentDialog
                            {
                                Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                Content = Globalization.GetString("QueueDialog_EmptyFileName_Content"),
                                CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                            };
                            await content.ShowAsync().ConfigureAwait(true);
                            return;
                        }

                        try
                        {
                            await RenameItem.RenameAsync(dialog.DesireName).ConfigureAwait(true);
                        }
                        catch (UnauthorizedAccessException)
                        {
                            QueueContentDialog Dialog = new QueueContentDialog
                            {
                                Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                Content = Globalization.GetString("QueueDialog_UnauthorizedRenameFile_Content"),
                                PrimaryButtonText = Globalization.GetString("Common_Dialog_NowButton"),
                                CloseButtonText = Globalization.GetString("Common_Dialog_LaterButton")
                            };

                            if (await Dialog.ShowAsync().ConfigureAwait(true) == ContentDialogResult.Primary)
                            {
                                _ = await Launcher.LaunchFolderAsync(FileControlInstance.CurrentFolder);
                            }
                        }
                    }
                }
                else if ((await RenameItem.GetStorageItem().ConfigureAwait(true)) is StorageFolder Folder)
                {
                    if (!await Folder.CheckExist().ConfigureAwait(true))
                    {
                        QueueContentDialog Dialog = new QueueContentDialog
                        {
                            Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                            Content = Globalization.GetString("QueueDialog_LocateFolderFailure_Content"),
                            CloseButtonText = Globalization.GetString("Common_Dialog_LaterButton")
                        };
                        _ = await Dialog.ShowAsync().ConfigureAwait(true);

                        if (SettingControl.IsDetachTreeViewAndPresenter)
                        {
                            await FileControlInstance.DisplayItemsInFolder(FileControlInstance.CurrentFolder, true).ConfigureAwait(false);
                        }
                        else
                        {
                            await FileControlInstance.DisplayItemsInFolder(FileControlInstance.CurrentNode, true).ConfigureAwait(false);
                        }
                        return;
                    }

                    RenameDialog dialog = new RenameDialog(RenameItem.Name);
                    if ((await dialog.ShowAsync().ConfigureAwait(true)) == ContentDialogResult.Primary)
                    {
                        if (string.IsNullOrWhiteSpace(dialog.DesireName))
                        {
                            QueueContentDialog content = new QueueContentDialog
                            {
                                Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                Content = Globalization.GetString("QueueDialog_EmptyFolderName_Content"),
                                CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                            };
                            await content.ShowAsync().ConfigureAwait(true);

                            return;
                        }

                        try
                        {
                            if (SettingControl.IsDetachTreeViewAndPresenter)
                            {
                                await RenameItem.RenameAsync(dialog.DesireName).ConfigureAwait(true);
                            }
                            else
                            {
                                if (FileControlInstance.CurrentNode.IsExpanded)
                                {
                                    IList<TreeViewNode> ChildCollection = FileControlInstance.CurrentNode.Children;
                                    TreeViewNode TargetNode = FileControlInstance.CurrentNode.Children.Where((Fold) => (Fold.Content as StorageFolder).Name == RenameItem.Name).FirstOrDefault();
                                    int index = FileControlInstance.CurrentNode.Children.IndexOf(TargetNode);

                                    await RenameItem.RenameAsync(dialog.DesireName).ConfigureAwait(true);

                                    if (TargetNode.HasUnrealizedChildren)
                                    {
                                        ChildCollection.Insert(index, new TreeViewNode()
                                        {
                                            Content = (await RenameItem.GetStorageItem().ConfigureAwait(true)) as StorageFolder,
                                            HasUnrealizedChildren = true,
                                            IsExpanded = false
                                        });
                                        ChildCollection.Remove(TargetNode);
                                    }
                                    else if (TargetNode.HasChildren)
                                    {
                                        TreeViewNode NewNode = new TreeViewNode()
                                        {
                                            Content = (await RenameItem.GetStorageItem().ConfigureAwait(true)) as StorageFolder,
                                            HasUnrealizedChildren = false,
                                            IsExpanded = false
                                        };

                                        foreach (var SubNode in TargetNode.Children)
                                        {
                                            NewNode.Children.Add(SubNode);
                                        }

                                        ChildCollection.Insert(index, NewNode);
                                        ChildCollection.Remove(TargetNode);
                                    }
                                    else
                                    {
                                        ChildCollection.Insert(index, new TreeViewNode()
                                        {
                                            Content = (await RenameItem.GetStorageItem().ConfigureAwait(true)) as StorageFolder,
                                            HasUnrealizedChildren = false,
                                            IsExpanded = false
                                        });
                                        ChildCollection.Remove(TargetNode);
                                    }
                                }
                                else
                                {
                                    await RenameItem.RenameAsync(dialog.DesireName).ConfigureAwait(true);
                                }
                            }
                        }
                        catch (UnauthorizedAccessException)
                        {
                            QueueContentDialog Dialog = new QueueContentDialog
                            {
                                Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                Content = Globalization.GetString("QueueDialog_UnauthorizedRenameFolder_Content"),
                                PrimaryButtonText = Globalization.GetString("Common_Dialog_NowButton"),
                                CloseButtonText = Globalization.GetString("Common_Dialog_LaterButton")
                            };

                            if (await Dialog.ShowAsync().ConfigureAwait(true) == ContentDialogResult.Primary)
                            {
                                _ = await Launcher.LaunchFolderAsync(FileControlInstance.CurrentFolder);
                            }
                        }
                    }
                }
            }
        }

        public async void BluetoothShare_Click(object sender, RoutedEventArgs e)
        {
            Restore();

            StorageFile ShareFile = (await SelectedItem.GetStorageItem().ConfigureAwait(true)) as StorageFile;

            if (!await ShareFile.CheckExist().ConfigureAwait(true))
            {
                QueueContentDialog Dialog = new QueueContentDialog
                {
                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                    Content = Globalization.GetString("QueueDialog_LocateFileFailure_Content"),
                    CloseButtonText = Globalization.GetString("Common_Dialog_RefreshButton")
                };
                _ = await Dialog.ShowAsync().ConfigureAwait(true);

                if (SettingControl.IsDetachTreeViewAndPresenter)
                {
                    await FileControlInstance.DisplayItemsInFolder(FileControlInstance.CurrentFolder, true).ConfigureAwait(false);
                }
                else
                {
                    await FileControlInstance.DisplayItemsInFolder(FileControlInstance.CurrentNode, true).ConfigureAwait(false);
                }
                return;
            }

            IReadOnlyList<Radio> RadioDevice = await Radio.GetRadiosAsync();

            if (RadioDevice.Any((Device) => Device.Kind == RadioKind.Bluetooth && Device.State == RadioState.On))
            {
                BluetoothUI Bluetooth = new BluetoothUI();
                if ((await Bluetooth.ShowAsync().ConfigureAwait(true)) == ContentDialogResult.Primary)
                {
                    BluetoothFileTransfer FileTransfer = new BluetoothFileTransfer(ShareFile);

                    _ = await FileTransfer.ShowAsync().ConfigureAwait(true);
                }
            }
            else
            {
                QueueContentDialog dialog = new QueueContentDialog
                {
                    Title = Globalization.GetString("Common_Dialog_TipTitle"),
                    Content = Globalization.GetString("QueueDialog_OpenBluetooth_Content"),
                    CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                };
                _ = await dialog.ShowAsync().ConfigureAwait(true);
            }
        }

        private void ViewControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            MixZip.IsEnabled = true;
            if (SelectedItems.Any((Item) => Item.StorageType != StorageItemTypes.Folder))
            {
                if (SelectedItems.All((Item) => Item.StorageType == StorageItemTypes.File))
                {
                    if (SelectedItems.All((Item) => Item.Type == ".zip"))
                    {
                        MixZip.Label = Globalization.GetString("Operate_Text_Decompression");
                    }
                    else if (SelectedItems.All((Item) => Item.Type != ".zip"))
                    {
                        MixZip.Label = Globalization.GetString("Operate_Text_Compression");
                    }
                    else
                    {
                        MixZip.IsEnabled = false;
                    }
                }
                else
                {
                    if (SelectedItems.Where((It) => It.StorageType == StorageItemTypes.File).Any((Item) => Item.Type == ".zip"))
                    {
                        MixZip.IsEnabled = false;
                    }
                    else
                    {
                        MixZip.Label = Globalization.GetString("Operate_Text_Compression");
                    }
                }
            }
            else
            {
                MixZip.Label = Globalization.GetString("Operate_Text_Compression");
            }

            if (SelectedItem is FileSystemStorageItem Item)
            {
                if (Item.StorageType == StorageItemTypes.File)
                {
                    Transcode.IsEnabled = false;
                    VideoEdit.IsEnabled = false;
                    VideoMerge.IsEnabled = false;
                    ChooseOtherApp.IsEnabled = true;
                    RunWithSystemAuthority.IsEnabled = false;

                    Zip.Label = Globalization.GetString("Operate_Text_Compression");

                    switch (Item.Type)
                    {
                        case ".zip":
                            {
                                Zip.Label = Globalization.GetString("Operate_Text_Decompression");
                                break;
                            }
                        case ".mp4":
                        case ".wmv":
                            {
                                VideoEdit.IsEnabled = true;
                                Transcode.IsEnabled = true;
                                VideoMerge.IsEnabled = true;
                                break;
                            }
                        case ".mkv":
                        case ".m4a":
                        case ".mov":
                            {
                                Transcode.IsEnabled = true;
                                break;
                            }
                        case ".mp3":
                        case ".flac":
                        case ".wma":
                        case ".alac":
                        case ".png":
                        case ".bmp":
                        case ".jpg":
                        case ".heic":
                        case ".gif":
                        case ".tiff":
                            {
                                Transcode.IsEnabled = true;
                                break;
                            }
                        case ".exe":
                            {
                                ChooseOtherApp.IsEnabled = false;
                                RunWithSystemAuthority.IsEnabled = true;
                                break;
                            }
                    }
                }
            }
        }

        private void ViewControl_PointerPressed(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if ((e.OriginalSource as FrameworkElement)?.DataContext == null)
            {
                SelectedItem = null;
                FileControlInstance.IsSearchOrPathBoxFocused = false;
            }
        }

        private void ViewControl_RightTapped(object sender, Windows.UI.Xaml.Input.RightTappedRoutedEventArgs e)
        {
            if (e.PointerDeviceType == Windows.Devices.Input.PointerDeviceType.Mouse)
            {
                if (ItemPresenter is GridView)
                {
                    if ((e.OriginalSource as FrameworkElement)?.DataContext is FileSystemStorageItem Context)
                    {
                        if (SelectedItems.Count <= 1 || !SelectedItems.Contains(Context))
                        {
                            ItemPresenter.ContextFlyout = Context.StorageType == StorageItemTypes.Folder ? FolderFlyout : FileFlyout;
                            SelectedItem = Context;
                        }
                        else
                        {
                            ItemPresenter.ContextFlyout = MixedFlyout;
                        }
                    }
                    else
                    {
                        SelectedItem = null;
                        ItemPresenter.ContextFlyout = EmptyFlyout;
                    }
                }
                else
                {
                    if (e.OriginalSource is ListViewItemPresenter || (e.OriginalSource as FrameworkElement).Name == "EmptyTextblock")
                    {
                        SelectedItem = null;
                        ItemPresenter.ContextFlyout = EmptyFlyout;
                    }
                    else
                    {
                        if ((e.OriginalSource as FrameworkElement)?.DataContext is FileSystemStorageItem Context)
                        {
                            if (SelectedItems.Count <= 1 || !SelectedItems.Contains(Context))
                            {
                                ItemPresenter.ContextFlyout = Context.StorageType == StorageItemTypes.Folder ? FolderFlyout : FileFlyout;
                                SelectedItem = Context;
                            }
                            else
                            {
                                ItemPresenter.ContextFlyout = MixedFlyout;
                            }
                        }
                        else
                        {
                            SelectedItem = null;
                            ItemPresenter.ContextFlyout = EmptyFlyout;
                        }
                    }
                }
            }

            e.Handled = true;
        }

        public async void Attribute_Click(object sender, RoutedEventArgs e)
        {
            Restore();

            StorageFile Device = (await SelectedItem.GetStorageItem().ConfigureAwait(true)) as StorageFile;

            if (!await Device.CheckExist().ConfigureAwait(true))
            {
                QueueContentDialog dialog = new QueueContentDialog
                {
                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                    Content = Globalization.GetString("QueueDialog_LocateFileFailure_Content"),
                    CloseButtonText = Globalization.GetString("Common_Dialog_RefreshButton")
                };
                _ = await dialog.ShowAsync().ConfigureAwait(true);

                if (SettingControl.IsDetachTreeViewAndPresenter)
                {
                    await FileControlInstance.DisplayItemsInFolder(FileControlInstance.CurrentFolder, true).ConfigureAwait(false);
                }
                else
                {
                    await FileControlInstance.DisplayItemsInFolder(FileControlInstance.CurrentNode, true).ConfigureAwait(false);
                }
                return;
            }

            PropertyDialog Dialog = new PropertyDialog(Device);
            _ = await Dialog.ShowAsync().ConfigureAwait(true);
        }

        public async void Zip_Click(object sender, RoutedEventArgs e)
        {
            Restore();

            StorageFile Item = (await SelectedItem.GetStorageItem().ConfigureAwait(true)) as StorageFile;

            if (!await Item.CheckExist().ConfigureAwait(true))
            {
                QueueContentDialog Dialog = new QueueContentDialog
                {
                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                    Content = Globalization.GetString("QueueDialog_LocateFileFailure_Content"),
                    CloseButtonText = Globalization.GetString("Common_Dialog_RefreshButton")
                };
                _ = await Dialog.ShowAsync().ConfigureAwait(true);

                if (SettingControl.IsDetachTreeViewAndPresenter)
                {
                    await FileControlInstance.DisplayItemsInFolder(FileControlInstance.CurrentFolder, true).ConfigureAwait(false);
                }
                else
                {
                    await FileControlInstance.DisplayItemsInFolder(FileControlInstance.CurrentNode, true).ConfigureAwait(false);
                }
                return;
            }

            if (Item.FileType == ".zip")
            {
                if ((await UnZipAsync(Item).ConfigureAwait(true)) is StorageFolder NewFolder)
                {
                    if (FileCollection.Where((Item) => Item.StorageType == StorageItemTypes.Folder).All((Folder) => Folder.Name != NewFolder.Name))
                    {
                        FileCollection.Insert(0, new FileSystemStorageItem(NewFolder, await NewFolder.GetModifiedTimeAsync().ConfigureAwait(true)));

                        if (!SettingControl.IsDetachTreeViewAndPresenter)
                        {
                            await FileControlInstance.FolderTree.RootNodes[0].UpdateAllSubNode().ConfigureAwait(true);
                        }
                    }
                }
            }
            else
            {
                ZipDialog dialog = new ZipDialog(true, Item.DisplayName);

                if ((await dialog.ShowAsync().ConfigureAwait(true)) == ContentDialogResult.Primary)
                {
                    await LoadingActivation(true, Globalization.GetString("Progress_Tip_Compressing")).ConfigureAwait(true);

                    if (dialog.IsCryptionEnable)
                    {
                        await CreateZipAsync(Item, dialog.FileName, (int)dialog.Level, true, dialog.Key, dialog.Password).ConfigureAwait(true);
                    }
                    else
                    {
                        await CreateZipAsync(Item, dialog.FileName, (int)dialog.Level).ConfigureAwait(true);
                    }

                    await LoadingActivation(false).ConfigureAwait(false);
                }
            }
        }

        /// <summary>
        /// 执行ZIP解压功能
        /// </summary>
        /// <param name="ZFileList">ZIP文件</param>
        /// <returns>无</returns>
        private async Task<StorageFolder> UnZipAsync(StorageFile ZFile)
        {
            StorageFolder NewFolder = null;
            using (Stream ZipFileStream = await ZFile.OpenStreamForReadAsync().ConfigureAwait(true))
            using (ZipFile ZipEntries = new ZipFile(ZipFileStream))
            {
                ZipEntries.IsStreamOwner = false;

                if (ZipEntries.Count == 0)
                {
                    return null;
                }

                try
                {
                    if (ZipEntries[0].IsCrypted)
                    {
                        ZipDialog Dialog = new ZipDialog(false);
                        if ((await Dialog.ShowAsync().ConfigureAwait(true)) == ContentDialogResult.Primary)
                        {
                            await LoadingActivation(true, Globalization.GetString("Progress_Tip_Extracting")).ConfigureAwait(true);
                            ZipEntries.Password = Dialog.Password;
                        }
                        else
                        {
                            return null;
                        }
                    }
                    else
                    {
                        await LoadingActivation(true, Globalization.GetString("Progress_Tip_Extracting")).ConfigureAwait(true);
                    }

                    NewFolder = await FileControlInstance.CurrentFolder.CreateFolderAsync(Path.GetFileNameWithoutExtension(ZFile.Name), CreationCollisionOption.OpenIfExists);

                    foreach (ZipEntry Entry in ZipEntries)
                    {
                        using (Stream ZipEntryStream = ZipEntries.GetInputStream(Entry))
                        {
                            StorageFile NewFile = null;

                            if (Entry.Name.Contains("/"))
                            {
                                string[] SplitFolderPath = Entry.Name.Split('/', StringSplitOptions.RemoveEmptyEntries);
                                StorageFolder TempFolder = NewFolder;
                                for (int i = 0; i < SplitFolderPath.Length - 1; i++)
                                {
                                    TempFolder = await TempFolder.CreateFolderAsync(SplitFolderPath[i], CreationCollisionOption.OpenIfExists);
                                }

                                if (Entry.Name.Last() == '/')
                                {
                                    await TempFolder.CreateFolderAsync(SplitFolderPath.Last(), CreationCollisionOption.OpenIfExists);
                                    continue;
                                }
                                else
                                {
                                    NewFile = await TempFolder.CreateFileAsync(SplitFolderPath.Last(), CreationCollisionOption.ReplaceExisting);
                                }
                            }
                            else
                            {
                                NewFile = await NewFolder.CreateFileAsync(Entry.Name, CreationCollisionOption.ReplaceExisting);
                            }

                            using (Stream NewFileStream = await NewFile.OpenStreamForWriteAsync().ConfigureAwait(true))
                            {
                                await ZipEntryStream.CopyToAsync(NewFileStream).ConfigureAwait(true);
                            }
                        }
                    }
                }
                catch (UnauthorizedAccessException)
                {
                    QueueContentDialog dialog = new QueueContentDialog
                    {
                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                        Content = Globalization.GetString("QueueDialog_UnauthorizedDecompression_Content"),
                        PrimaryButtonText = Globalization.GetString("Common_Dialog_NowButton"),
                        CloseButtonText = Globalization.GetString("Common_Dialog_LaterButton")
                    };

                    if (await dialog.ShowAsync().ConfigureAwait(true) == ContentDialogResult.Primary)
                    {
                        _ = await Launcher.LaunchFolderAsync(FileControlInstance.CurrentFolder);
                    }
                }
                catch (Exception e)
                {
                    QueueContentDialog dialog = new QueueContentDialog
                    {
                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                        Content = Globalization.GetString("QueueDialog_DecompressionError_Content") + e.Message,
                        CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                    };
                    _ = await dialog.ShowAsync().ConfigureAwait(true);
                }
                finally
                {
                    await LoadingActivation(false).ConfigureAwait(false);
                }
            }

            return NewFolder;
        }

        /// <summary>
        /// 执行ZIP文件创建功能
        /// </summary>
        /// <param name="FileList">待压缩文件</param>
        /// <param name="NewZipName">生成的Zip文件名</param>
        /// <param name="ZipLevel">压缩等级</param>
        /// <param name="EnableCryption">是否启用加密</param>
        /// <param name="Size">AES加密密钥长度</param>
        /// <param name="Password">密码</param>
        /// <returns>无</returns>
        private async Task CreateZipAsync(IStorageItem ZipTarget, string NewZipName, int ZipLevel, bool EnableCryption = false, KeySize Size = KeySize.None, string Password = null)
        {
            try
            {
                StorageFile Newfile = await FileControlInstance.CurrentFolder.CreateFileAsync(NewZipName, CreationCollisionOption.GenerateUniqueName);

                using (Stream NewFileStream = await Newfile.OpenStreamForWriteAsync().ConfigureAwait(true))
                using (ZipOutputStream OutputStream = new ZipOutputStream(NewFileStream))
                {
                    OutputStream.IsStreamOwner = false;
                    OutputStream.SetLevel(ZipLevel);
                    OutputStream.UseZip64 = UseZip64.Dynamic;

                    if (EnableCryption)
                    {
                        OutputStream.Password = Password;
                    }

                    try
                    {
                        if (ZipTarget is StorageFile ZipFile)
                        {
                            if (EnableCryption)
                            {
                                using (Stream FileStream = await ZipFile.OpenStreamForReadAsync().ConfigureAwait(true))
                                {
                                    ZipEntry NewEntry = new ZipEntry(ZipFile.Name)
                                    {
                                        DateTime = DateTime.Now,
                                        AESKeySize = (int)Size,
                                        IsCrypted = true,
                                        CompressionMethod = CompressionMethod.Deflated,
                                        Size = FileStream.Length
                                    };

                                    OutputStream.PutNextEntry(NewEntry);

                                    await FileStream.CopyToAsync(OutputStream).ConfigureAwait(true);
                                }
                            }
                            else
                            {
                                using (Stream FileStream = await ZipFile.OpenStreamForReadAsync().ConfigureAwait(true))
                                {
                                    ZipEntry NewEntry = new ZipEntry(ZipFile.Name)
                                    {
                                        DateTime = DateTime.Now,
                                        CompressionMethod = CompressionMethod.Deflated,
                                        Size = FileStream.Length
                                    };

                                    OutputStream.PutNextEntry(NewEntry);

                                    await FileStream.CopyToAsync(OutputStream).ConfigureAwait(true);
                                }
                            }
                        }
                        else if (ZipTarget is StorageFolder ZipFolder)
                        {
                            await ZipFolderCore(ZipFolder, OutputStream, ZipFolder.Name, EnableCryption, Size, Password).ConfigureAwait(true);
                        }

                        await OutputStream.FlushAsync().ConfigureAwait(true);
                        OutputStream.Finish();
                    }
                    catch (Exception e)
                    {
                        QueueContentDialog dialog = new QueueContentDialog
                        {
                            Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                            Content = Globalization.GetString("QueueDialog_CompressionError_Content") + e.Message,
                            CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                        };
                        _ = await dialog.ShowAsync().ConfigureAwait(true);
                    }
                }

                if (FileCollection.FirstOrDefault((Item) => Item.StorageType == StorageItemTypes.File) is FileSystemStorageItem Item)
                {
                    FileCollection.Insert(FileCollection.IndexOf(Item), new FileSystemStorageItem(Newfile, await Newfile.GetSizeRawDataAsync().ConfigureAwait(true), await Newfile.GetThumbnailBitmapAsync().ConfigureAwait(true), await Newfile.GetModifiedTimeAsync().ConfigureAwait(true)));
                }
                else
                {
                    FileCollection.Add(new FileSystemStorageItem(Newfile, await Newfile.GetSizeRawDataAsync().ConfigureAwait(true), await Newfile.GetThumbnailBitmapAsync().ConfigureAwait(true), await Newfile.GetModifiedTimeAsync().ConfigureAwait(true)));
                }
            }
            catch (UnauthorizedAccessException)
            {
                QueueContentDialog dialog = new QueueContentDialog
                {
                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                    Content = Globalization.GetString("QueueDialog_UnauthorizedCompression_Content"),
                    PrimaryButtonText = Globalization.GetString("Common_Dialog_NowButton"),
                    CloseButtonText = Globalization.GetString("Common_Dialog_LaterButton")
                };

                if (await dialog.ShowAsync().ConfigureAwait(true) == ContentDialogResult.Primary)
                {
                    _ = await Launcher.LaunchFolderAsync(FileControlInstance.CurrentFolder);
                }
            }
        }

        /// <summary>
        /// 执行ZIP文件创建功能
        /// </summary>
        /// <param name="FileList">待压缩文件</param>
        /// <param name="NewZipName">生成的Zip文件名</param>
        /// <param name="ZipLevel">压缩等级</param>
        /// <param name="EnableCryption">是否启用加密</param>
        /// <param name="Size">AES加密密钥长度</param>
        /// <param name="Password">密码</param>
        /// <returns>无</returns>
        private async Task CreateZipAsync(IEnumerable<FileSystemStorageItem> ZipItemGroup, string NewZipName, int ZipLevel, bool EnableCryption = false, KeySize Size = KeySize.None, string Password = null)
        {
            try
            {
                StorageFile Newfile = await FileControlInstance.CurrentFolder.CreateFileAsync(NewZipName, CreationCollisionOption.GenerateUniqueName);

                using (Stream NewFileStream = await Newfile.OpenStreamForWriteAsync().ConfigureAwait(true))
                using (ZipOutputStream OutputStream = new ZipOutputStream(NewFileStream))
                {
                    OutputStream.IsStreamOwner = false;
                    OutputStream.SetLevel(ZipLevel);
                    OutputStream.UseZip64 = UseZip64.Dynamic;

                    if (EnableCryption)
                    {
                        OutputStream.Password = Password;
                    }

                    try
                    {
                        foreach (FileSystemStorageItem StorageItem in ZipItemGroup)
                        {
                            if (await StorageItem.GetStorageItem().ConfigureAwait(true) is StorageFile ZipFile)
                            {
                                if (EnableCryption)
                                {
                                    using (Stream FileStream = await ZipFile.OpenStreamForReadAsync().ConfigureAwait(true))
                                    {
                                        ZipEntry NewEntry = new ZipEntry(ZipFile.Name)
                                        {
                                            DateTime = DateTime.Now,
                                            AESKeySize = (int)Size,
                                            IsCrypted = true,
                                            CompressionMethod = CompressionMethod.Deflated,
                                            Size = FileStream.Length
                                        };

                                        OutputStream.PutNextEntry(NewEntry);

                                        await FileStream.CopyToAsync(OutputStream).ConfigureAwait(true);
                                    }
                                }
                                else
                                {
                                    using (Stream FileStream = await ZipFile.OpenStreamForReadAsync().ConfigureAwait(true))
                                    {
                                        ZipEntry NewEntry = new ZipEntry(ZipFile.Name)
                                        {
                                            DateTime = DateTime.Now,
                                            CompressionMethod = CompressionMethod.Deflated,
                                            Size = FileStream.Length
                                        };

                                        OutputStream.PutNextEntry(NewEntry);

                                        await FileStream.CopyToAsync(OutputStream).ConfigureAwait(true);
                                    }
                                }
                            }
                            else if (await StorageItem.GetStorageItem().ConfigureAwait(true) is StorageFolder ZipFolder)
                            {
                                await ZipFolderCore(ZipFolder, OutputStream, ZipFolder.Name, EnableCryption, Size, Password).ConfigureAwait(true);
                            }
                        }

                        await OutputStream.FlushAsync().ConfigureAwait(true);
                        OutputStream.Finish();
                    }
                    catch (Exception e)
                    {
                        QueueContentDialog dialog = new QueueContentDialog
                        {
                            Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                            Content = Globalization.GetString("QueueDialog_CompressionError_Content") + e.Message,
                            CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                        };
                        _ = await dialog.ShowAsync().ConfigureAwait(true);
                    }
                }

                if (FileCollection.FirstOrDefault((Item) => Item.StorageType == StorageItemTypes.File) is FileSystemStorageItem Item)
                {
                    FileCollection.Insert(FileCollection.IndexOf(Item), new FileSystemStorageItem(Newfile, await Newfile.GetSizeRawDataAsync().ConfigureAwait(true), await Newfile.GetThumbnailBitmapAsync().ConfigureAwait(true), await Newfile.GetModifiedTimeAsync().ConfigureAwait(true)));
                }
                else
                {
                    FileCollection.Add(new FileSystemStorageItem(Newfile, await Newfile.GetSizeRawDataAsync().ConfigureAwait(true), await Newfile.GetThumbnailBitmapAsync().ConfigureAwait(true), await Newfile.GetModifiedTimeAsync().ConfigureAwait(true)));
                }
            }
            catch (UnauthorizedAccessException)
            {
                QueueContentDialog dialog = new QueueContentDialog
                {
                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                    Content = Globalization.GetString("QueueDialog_UnauthorizedCompression_Content"),
                    PrimaryButtonText = Globalization.GetString("Common_Dialog_NowButton"),
                    CloseButtonText = Globalization.GetString("Common_Dialog_LaterButton")
                };

                if (await dialog.ShowAsync().ConfigureAwait(true) == ContentDialogResult.Primary)
                {
                    _ = await Launcher.LaunchFolderAsync(FileControlInstance.CurrentFolder);
                }
            }
        }

        private async Task ZipFolderCore(StorageFolder Folder, ZipOutputStream OutputStream, string BaseFolderName, bool EnableCryption = false, KeySize Size = KeySize.None, string Password = null)
        {
            IReadOnlyList<IStorageItem> ItemsCollection = await Folder.GetItemsAsync();

            if (ItemsCollection.Count == 0)
            {
                if (!string.IsNullOrEmpty(BaseFolderName))
                {
                    ZipEntry NewEntry = new ZipEntry(BaseFolderName);
                    OutputStream.PutNextEntry(NewEntry);
                    OutputStream.CloseEntry();
                }
            }
            else
            {
                foreach (IStorageItem Item in ItemsCollection)
                {
                    if (Item is StorageFolder InnerFolder)
                    {
                        if (EnableCryption)
                        {
                            await ZipFolderCore(InnerFolder, OutputStream, $"{BaseFolderName}/{InnerFolder.Name}", true, Size, Password).ConfigureAwait(false);
                        }
                        else
                        {
                            await ZipFolderCore(InnerFolder, OutputStream, $"{BaseFolderName}/{InnerFolder.Name}").ConfigureAwait(false);
                        }
                    }
                    else if (Item is StorageFile InnerFile)
                    {
                        if (EnableCryption)
                        {
                            using (Stream FileStream = await InnerFile.OpenStreamForReadAsync().ConfigureAwait(true))
                            {
                                ZipEntry NewEntry = new ZipEntry($"{BaseFolderName}/{InnerFile.Name}")
                                {
                                    DateTime = DateTime.Now,
                                    AESKeySize = (int)Size,
                                    IsCrypted = true,
                                    CompressionMethod = CompressionMethod.Deflated,
                                    Size = FileStream.Length
                                };

                                OutputStream.PutNextEntry(NewEntry);

                                await FileStream.CopyToAsync(OutputStream).ConfigureAwait(false);

                                OutputStream.CloseEntry();
                            }
                        }
                        else
                        {
                            using (Stream FileStream = await InnerFile.OpenStreamForReadAsync().ConfigureAwait(true))
                            {
                                ZipEntry NewEntry = new ZipEntry($"{BaseFolderName}/{InnerFile.Name}")
                                {
                                    DateTime = DateTime.Now,
                                    CompressionMethod = CompressionMethod.Deflated,
                                    Size = FileStream.Length
                                };

                                OutputStream.PutNextEntry(NewEntry);

                                await FileStream.CopyToAsync(OutputStream).ConfigureAwait(false);

                                OutputStream.CloseEntry();
                            }
                        }
                    }
                }
            }
        }

        private async void ViewControl_DoubleTapped(object sender, Windows.UI.Xaml.Input.DoubleTappedRoutedEventArgs e)
        {
            if (SettingControl.IsInputFromPrimaryButton && (e.OriginalSource as FrameworkElement)?.DataContext is FileSystemStorageItem ReFile)
            {
                await EnterSelectedItem(ReFile).ConfigureAwait(false);
            }
        }

        public async void Transcode_Click(object sender, RoutedEventArgs e)
        {
            Restore();

            if ((await SelectedItem.GetStorageItem().ConfigureAwait(true)) is StorageFile Source)
            {
                if (!await Source.CheckExist().ConfigureAwait(true))
                {
                    QueueContentDialog Dialog = new QueueContentDialog
                    {
                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                        Content = Globalization.GetString("QueueDialog_LocateFileFailure_Content"),
                        CloseButtonText = Globalization.GetString("Common_Dialog_RefreshButton")
                    };
                    _ = await Dialog.ShowAsync().ConfigureAwait(true);

                    await FileControlInstance.DisplayItemsInFolder(FileControlInstance.CurrentNode, true).ConfigureAwait(false);
                    return;
                }

                if (GeneralTransformer.IsAnyTransformTaskRunning)
                {
                    QueueContentDialog Dialog = new QueueContentDialog
                    {
                        Title = Globalization.GetString("Common_Dialog_TipTitle"),
                        Content = Globalization.GetString("QueueDialog_TaskWorking_Content"),
                        CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                    };
                    _ = await Dialog.ShowAsync().ConfigureAwait(true);

                    return;
                }

                switch (Source.FileType)
                {
                    case ".mkv":
                    case ".mp4":
                    case ".mp3":
                    case ".flac":
                    case ".wma":
                    case ".wmv":
                    case ".m4a":
                    case ".mov":
                    case ".alac":
                        {
                            TranscodeDialog dialog = new TranscodeDialog(Source);

                            if ((await dialog.ShowAsync().ConfigureAwait(true)) == ContentDialogResult.Primary)
                            {
                                try
                                {
                                    StorageFile DestinationFile = await FileControlInstance.CurrentFolder.CreateFileAsync(Source.DisplayName + "." + dialog.MediaTranscodeEncodingProfile.ToLower(), CreationCollisionOption.GenerateUniqueName);

                                    await GeneralTransformer.TranscodeFromAudioOrVideoAsync(Source, DestinationFile, dialog.MediaTranscodeEncodingProfile, dialog.MediaTranscodeQuality, dialog.SpeedUp).ConfigureAwait(true);

                                    if (Path.GetDirectoryName(DestinationFile.Path) == FileControlInstance.CurrentFolder.Path && ApplicationData.Current.LocalSettings.Values["MediaTranscodeStatus"] is string Status && Status == "Success")
                                    {
                                        FileCollection.Add(new FileSystemStorageItem(DestinationFile, await DestinationFile.GetSizeRawDataAsync().ConfigureAwait(true), await DestinationFile.GetThumbnailBitmapAsync().ConfigureAwait(true), await DestinationFile.GetModifiedTimeAsync().ConfigureAwait(true)));
                                    }
                                }
                                catch (UnauthorizedAccessException)
                                {
                                    QueueContentDialog Dialog = new QueueContentDialog
                                    {
                                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                        Content = Globalization.GetString("QueueDialog_UnauthorizedCreateNewFile_Content"),
                                        PrimaryButtonText = Globalization.GetString("Common_Dialog_NowButton"),
                                        CloseButtonText = Globalization.GetString("Common_Dialog_LaterButton")
                                    };

                                    if (await Dialog.ShowAsync().ConfigureAwait(true) == ContentDialogResult.Primary)
                                    {
                                        _ = await Launcher.LaunchFolderAsync(FileControlInstance.CurrentFolder);
                                    }
                                }
                            }

                            break;
                        }
                    case ".png":
                    case ".bmp":
                    case ".jpg":
                    case ".heic":
                    case ".tiff":
                        {
                            TranscodeImageDialog Dialog = null;
                            using (IRandomAccessStream OriginStream = await Source.OpenAsync(FileAccessMode.Read))
                            {
                                BitmapDecoder Decoder = await BitmapDecoder.CreateAsync(OriginStream);
                                Dialog = new TranscodeImageDialog(Decoder.PixelWidth, Decoder.PixelHeight);
                            }

                            if (await Dialog.ShowAsync().ConfigureAwait(true) == ContentDialogResult.Primary)
                            {
                                await LoadingActivation(true, Globalization.GetString("Progress_Tip_Transcoding")).ConfigureAwait(true);

                                await GeneralTransformer.TranscodeFromImageAsync(Source, Dialog.TargetFile, Dialog.IsEnableScale, Dialog.ScaleWidth, Dialog.ScaleHeight, Dialog.InterpolationMode).ConfigureAwait(true);

                                await LoadingActivation(false).ConfigureAwait(true);
                            }
                            break;
                        }
                }
            }
        }

        public async void FolderOpen_Click(object sender, RoutedEventArgs e)
        {
            Restore();

            if (SelectedItem is FileSystemStorageItem Item)
            {
                await EnterSelectedItem(Item).ConfigureAwait(false);
            }
        }

        public async void FolderProperty_Click(object sender, RoutedEventArgs e)
        {
            Restore();

            StorageFolder Device = (await SelectedItem.GetStorageItem().ConfigureAwait(true)) as StorageFolder;
            if (!await Device.CheckExist().ConfigureAwait(true))
            {
                QueueContentDialog dialog = new QueueContentDialog
                {
                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                    Content = Globalization.GetString("QueueDialog_LocateFileFailure_Content"),
                    CloseButtonText = Globalization.GetString("Common_Dialog_RefreshButton")
                };
                _ = await dialog.ShowAsync().ConfigureAwait(true);

                if (SettingControl.IsDetachTreeViewAndPresenter)
                {
                    await FileControlInstance.DisplayItemsInFolder(FileControlInstance.CurrentFolder, true).ConfigureAwait(false);
                }
                else
                {
                    await FileControlInstance.DisplayItemsInFolder(FileControlInstance.CurrentNode, true).ConfigureAwait(false);
                }
                return;
            }

            PropertyDialog Dialog = new PropertyDialog(Device);
            _ = await Dialog.ShowAsync().ConfigureAwait(true);
        }

        public async void WIFIShare_Click(object sender, RoutedEventArgs e)
        {
            Restore();

            StorageFile Item = (await SelectedItem.GetStorageItem().ConfigureAwait(true)) as StorageFile;

            if (!await Item.CheckExist().ConfigureAwait(true))
            {
                QueueContentDialog Dialog = new QueueContentDialog
                {
                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                    Content = Globalization.GetString("QueueDialog_LocateFileFailure_Content"),
                    CloseButtonText = Globalization.GetString("Common_Dialog_RefreshButton")
                };
                _ = await Dialog.ShowAsync().ConfigureAwait(true);

                if (SettingControl.IsDetachTreeViewAndPresenter)
                {
                    await FileControlInstance.DisplayItemsInFolder(FileControlInstance.CurrentFolder, true).ConfigureAwait(false);
                }
                else
                {
                    await FileControlInstance.DisplayItemsInFolder(FileControlInstance.CurrentNode, true).ConfigureAwait(false);
                }
                return;
            }

            if (QRTeachTip.IsOpen)
            {
                QRTeachTip.IsOpen = false;
            }

            while (WiFiProvider != null)
            {
                await Task.Delay(300).ConfigureAwait(true);
            }

            WiFiProvider = new WiFiShareProvider();
            WiFiProvider.ThreadExitedUnexpectly += WiFiProvider_ThreadExitedUnexpectly;

            string Hash = Item.Path.ComputeMD5Hash();
            QRText.Text = WiFiProvider.CurrentUri + Hash;
            WiFiProvider.FilePathMap = new KeyValuePair<string, string>(Hash, Item.Path);

            QrCodeEncodingOptions options = new QrCodeEncodingOptions()
            {
                DisableECI = true,
                CharacterSet = "UTF-8",
                Width = 250,
                Height = 250,
                ErrorCorrection = ErrorCorrectionLevel.Q
            };

            BarcodeWriter Writer = new BarcodeWriter
            {
                Format = BarcodeFormat.QR_CODE,
                Options = options
            };

            WriteableBitmap Bitmap = Writer.Write(QRText.Text);
            using (SoftwareBitmap PreTransImage = SoftwareBitmap.CreateCopyFromBuffer(Bitmap.PixelBuffer, BitmapPixelFormat.Bgra8, 250, 250))
            using (SoftwareBitmap TransferImage = new SoftwareBitmap(BitmapPixelFormat.Bgra8, 400, 250, BitmapAlphaMode.Premultiplied))
            {
                OpenCVLibrary.ExtendImageBorder(PreTransImage, TransferImage, Colors.White, 0, 75, 75, 0);
                SoftwareBitmapSource Source = new SoftwareBitmapSource();
                QRImage.Source = Source;
                await Source.SetBitmapAsync(TransferImage);
            }

            QRTeachTip.Target = ItemPresenter.ContainerFromItem(SelectedItem) as FrameworkElement;

            QRTeachTip.IsOpen = true;

            await WiFiProvider.StartToListenRequest().ConfigureAwait(false);
        }

        private async void WiFiProvider_ThreadExitedUnexpectly(object sender, Exception e)
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
            {
                QRTeachTip.IsOpen = false;

                QueueContentDialog dialog = new QueueContentDialog
                {
                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                    Content = Globalization.GetString("QueueDialog_WiFiError_Content") + e.Message,
                    CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                };
                _ = await dialog.ShowAsync().ConfigureAwait(true);
            });
        }

        private void CopyLinkButton_Click(object sender, RoutedEventArgs e)
        {
            DataPackage Package = new DataPackage();
            Package.SetText(QRText.Text);
            Clipboard.SetContent(Package);
        }

        public async void UseSystemFileMananger_Click(object sender, RoutedEventArgs e)
        {
            Restore();

            _ = await Launcher.LaunchFolderAsync(FileControlInstance.CurrentFolder);
        }

        public async void ParentProperty_Click(object sender, RoutedEventArgs e)
        {
            if (!await FileControlInstance.CurrentFolder.CheckExist().ConfigureAwait(true))
            {
                QueueContentDialog Dialog = new QueueContentDialog
                {
                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                    Content = Globalization.GetString("QueueDialog_LocateFolderFailure_Content"),
                    CloseButtonText = Globalization.GetString("Common_Dialog_RefreshButton")
                };
                _ = await Dialog.ShowAsync().ConfigureAwait(true);
                return;
            }

            if (FileControlInstance.CurrentFolder.Path == Path.GetPathRoot(FileControlInstance.CurrentFolder.Path))
            {
                if (TabViewContainer.ThisPage.HardDeviceList.FirstOrDefault((Device) => Device.Name == FileControlInstance.CurrentFolder.DisplayName) is HardDeviceInfo Info)
                {
                    DeviceInfoDialog dialog = new DeviceInfoDialog(Info);
                    _ = await dialog.ShowAsync().ConfigureAwait(true);
                }
                else
                {
                    PropertyDialog Dialog = new PropertyDialog(FileControlInstance.CurrentFolder);
                    _ = await Dialog.ShowAsync().ConfigureAwait(true);
                }
            }
            else
            {
                PropertyDialog Dialog = new PropertyDialog(FileControlInstance.CurrentFolder);
                _ = await Dialog.ShowAsync().ConfigureAwait(true);
            }
        }

        public async void FileOpen_Click(object sender, RoutedEventArgs e)
        {
            Restore();

            if (SelectedItem is FileSystemStorageItem ReFile)
            {
                await EnterSelectedItem(ReFile).ConfigureAwait(false);
            }
        }

        private void QRText_GotFocus(object sender, RoutedEventArgs e)
        {
            ((TextBox)sender).SelectAll();
        }

        public async void AddToLibray_Click(object sender, RoutedEventArgs e)
        {
            Restore();

            StorageFolder folder = (await SelectedItem.GetStorageItem().ConfigureAwait(true)) as StorageFolder;

            if (!await folder.CheckExist().ConfigureAwait(true))
            {
                QueueContentDialog Dialog = new QueueContentDialog
                {
                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                    Content = Globalization.GetString("QueueDialog_LocateFolderFailure_Content"),
                    CloseButtonText = Globalization.GetString("Common_Dialog_RefreshButton")
                };
                _ = await Dialog.ShowAsync().ConfigureAwait(true);

                if (SettingControl.IsDetachTreeViewAndPresenter)
                {
                    await FileControlInstance.DisplayItemsInFolder(FileControlInstance.CurrentFolder, true).ConfigureAwait(false);
                }
                else
                {
                    await FileControlInstance.DisplayItemsInFolder(FileControlInstance.CurrentNode, true).ConfigureAwait(false);
                }
                return;
            }

            if (TabViewContainer.ThisPage.LibraryFolderList.Any((Folder) => Folder.Folder.Path == folder.Path))
            {
                QueueContentDialog dialog = new QueueContentDialog
                {
                    Title = Globalization.GetString("Common_Dialog_TipTitle"),
                    Content = Globalization.GetString("QueueDialog_RepeatAddToHomePage_Content"),
                    CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                };
                _ = await dialog.ShowAsync().ConfigureAwait(true);
            }
            else
            {
                TabViewContainer.ThisPage.LibraryFolderList.Add(new LibraryFolder(folder, await folder.GetThumbnailBitmapAsync().ConfigureAwait(true)));
                await SQLite.Current.SetLibraryPathAsync(folder.Path, LibraryType.UserCustom).ConfigureAwait(false);
            }
        }

        public async void CreateFolder_Click(object sender, RoutedEventArgs e)
        {
            Restore();

            if (!await FileControlInstance.CurrentFolder.CheckExist().ConfigureAwait(true))
            {
                QueueContentDialog Dialog = new QueueContentDialog
                {
                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                    Content = Globalization.GetString("QueueDialog_LocateFolderFailure_Content"),
                    CloseButtonText = Globalization.GetString("Common_Dialog_RefreshButton")
                };
                _ = await Dialog.ShowAsync().ConfigureAwait(true);

                return;
            }

            try
            {
                StorageFolder NewFolder = await FileControlInstance.CurrentFolder.CreateFolderAsync(Globalization.GetString("Create_NewFolder_Admin_Name"), CreationCollisionOption.GenerateUniqueName);

                FileCollection.Insert(0, new FileSystemStorageItem(NewFolder, await NewFolder.GetModifiedTimeAsync().ConfigureAwait(true)));

                if (!SettingControl.IsDetachTreeViewAndPresenter)
                {
                    await FileControlInstance.FolderTree.RootNodes[0].UpdateAllSubNode().ConfigureAwait(true);
                }
            }
            catch
            {
                QueueContentDialog dialog = new QueueContentDialog
                {
                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                    Content = Globalization.GetString("QueueDialog_UnauthorizedCreateFolder_Content"),
                    PrimaryButtonText = Globalization.GetString("Common_Dialog_NowButton"),
                    CloseButtonText = Globalization.GetString("Common_Dialog_LaterButton")
                };

                if (await dialog.ShowAsync().ConfigureAwait(true) == ContentDialogResult.Primary)
                {
                    _ = await Launcher.LaunchFolderAsync(FileControlInstance.CurrentFolder);
                }
            }
        }

        private void EmptyFlyout_Opening(object sender, object e)
        {
            if (MoveFiles != null || CopyFiles != null)
            {
                Paste.IsEnabled = true;
            }
        }

        public async void SystemShare_Click(object sender, RoutedEventArgs e)
        {
            Restore();

            if ((await SelectedItem.GetStorageItem().ConfigureAwait(true)) is StorageFile ShareItem)
            {
                if (!await ShareItem.CheckExist().ConfigureAwait(true))
                {
                    QueueContentDialog Dialog = new QueueContentDialog
                    {
                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                        Content = Globalization.GetString("QueueDialog_LocateFileFailure_Content"),
                        CloseButtonText = Globalization.GetString("Common_Dialog_RefreshButton")
                    };
                    _ = await Dialog.ShowAsync().ConfigureAwait(true);

                    if (SettingControl.IsDetachTreeViewAndPresenter)
                    {
                        await FileControlInstance.DisplayItemsInFolder(FileControlInstance.CurrentFolder, true).ConfigureAwait(false);
                    }
                    else
                    {
                        await FileControlInstance.DisplayItemsInFolder(FileControlInstance.CurrentNode, true).ConfigureAwait(false);
                    }
                    return;
                }

                DataTransferManager.GetForCurrentView().DataRequested += (s, args) =>
                {
                    DataPackage Package = new DataPackage();
                    Package.Properties.Title = ShareItem.DisplayName;
                    Package.Properties.Description = ShareItem.DisplayType;
                    Package.SetStorageItems(new StorageFile[] { ShareItem });
                    args.Request.Data = Package;
                };

                DataTransferManager.ShowShareUI();
            }
        }

        public async void Refresh_Click(object sender, RoutedEventArgs e)
        {
            Restore();

            if (!await FileControlInstance.CurrentFolder.CheckExist().ConfigureAwait(true))
            {
                QueueContentDialog Dialog = new QueueContentDialog
                {
                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                    Content = Globalization.GetString("QueueDialog_LocateFolderFailure_Content"),
                    CloseButtonText = Globalization.GetString("Common_Dialog_RefreshButton")
                };
                _ = await Dialog.ShowAsync().ConfigureAwait(true);
                return;
            }

            if (SettingControl.IsDetachTreeViewAndPresenter)
            {
                await FileControlInstance.DisplayItemsInFolder(FileControlInstance.CurrentFolder, true).ConfigureAwait(false);
            }
            else
            {
                await FileControlInstance.DisplayItemsInFolder(FileControlInstance.CurrentNode, true).ConfigureAwait(false);
            }
        }

        private async void ViewControl_ItemClick(object sender, ItemClickEventArgs e)
        {
            FileControlInstance.IsSearchOrPathBoxFocused = false;

            if (!SettingControl.IsDoubleClickEnable && e.ClickedItem is FileSystemStorageItem ReFile)
            {
                await EnterSelectedItem(ReFile).ConfigureAwait(false);
            }
        }

        private async Task EnterSelectedItem(FileSystemStorageItem ReFile, bool RunAsAdministrator = false)
        {
            try
            {
                if (Interlocked.Exchange(ref TabTarget, ReFile) == null)
                {
                    if ((await TabTarget.GetStorageItem().ConfigureAwait(true)) is StorageFile File)
                    {
                        if (!await File.CheckExist().ConfigureAwait(true))
                        {
                            QueueContentDialog Dialog = new QueueContentDialog
                            {
                                Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                Content = Globalization.GetString("QueueDialog_LocateFileFailure_Content"),
                                CloseButtonText = Globalization.GetString("Common_Dialog_RefreshButton")
                            };
                            _ = await Dialog.ShowAsync().ConfigureAwait(true);

                            await FileControlInstance.DisplayItemsInFolder(FileControlInstance.CurrentNode, true).ConfigureAwait(false);

                            Interlocked.Exchange(ref TabTarget, null);
                            return;
                        }

                        string AdminExcuteProgram = null;
                        if (ApplicationData.Current.LocalSettings.Values["AdminProgramForExcute"] is string ProgramExcute)
                        {
                            string SaveUnit = ProgramExcute.Split(';', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault((Item) => Item.Split('|')[0] == TabTarget.Type);
                            if (!string.IsNullOrEmpty(SaveUnit))
                            {
                                AdminExcuteProgram = SaveUnit.Split('|')[1];
                            }
                        }

                        if (!string.IsNullOrEmpty(AdminExcuteProgram) && AdminExcuteProgram != Globalization.GetString("RX_BuildIn_Viewer_Name"))
                        {
                            bool IsExcuted = false;
                            foreach (string Path in await SQLite.Current.GetProgramPickerRecordAsync(TabTarget.Type).ConfigureAwait(true))
                            {
                                try
                                {
                                    StorageFile ExcuteFile = await StorageFile.GetFileFromPathAsync(Path);

                                    string AppName = Convert.ToString((await ExcuteFile.Properties.RetrievePropertiesAsync(new string[] { "System.FileDescription" }))["System.FileDescription"]);

                                    if (AppName == AdminExcuteProgram || ExcuteFile.DisplayName == AdminExcuteProgram)
                                    {
                                        await FullTrustExcutorController.Run(Path, TabTarget.Path).ConfigureAwait(true);
                                        IsExcuted = true;
                                        break;
                                    }
                                }
                                catch (Exception)
                                {
                                    await SQLite.Current.DeleteProgramPickerRecordAsync(TabTarget.Type, Path).ConfigureAwait(true);
                                }
                            }

                            if (!IsExcuted)
                            {
                                if ((await Launcher.FindFileHandlersAsync(TabTarget.Type)).FirstOrDefault((Item) => Item.DisplayInfo.DisplayName == AdminExcuteProgram) is AppInfo Info)
                                {
                                    if (!await Launcher.LaunchFileAsync(File, new LauncherOptions { TargetApplicationPackageFamilyName = Info.PackageFamilyName, DisplayApplicationPicker = false }))
                                    {
                                        ProgramPickerDialog Dialog = new ProgramPickerDialog(File);
                                        if (await Dialog.ShowAsync().ConfigureAwait(true) == ContentDialogResult.Primary)
                                        {
                                            if (Dialog.OpenFailed)
                                            {
                                                QueueContentDialog dialog = new QueueContentDialog
                                                {
                                                    Title = Globalization.GetString("Commom_Dialog_TipTitle"),
                                                    Content = Globalization.GetString("QueueDialog_OpenFailure_Content"),
                                                    PrimaryButtonText = Globalization.GetString("QueueDialog_OpenFailure_PrimaryButton"),
                                                    CloseButtonText = Globalization.GetString("Common_Dialog_CancelButton")
                                                };

                                                if (await dialog.ShowAsync().ConfigureAwait(true) == ContentDialogResult.Primary)
                                                {
                                                    if (!await Launcher.LaunchFileAsync(File))
                                                    {
                                                        LauncherOptions options = new LauncherOptions
                                                        {
                                                            DisplayApplicationPicker = true
                                                        };
                                                        _ = await Launcher.LaunchFileAsync(File, options);
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                                else
                                {
                                    ProgramPickerDialog Dialog = new ProgramPickerDialog(File);
                                    if (await Dialog.ShowAsync().ConfigureAwait(true) == ContentDialogResult.Primary)
                                    {
                                        if (Dialog.OpenFailed)
                                        {
                                            QueueContentDialog dialog = new QueueContentDialog
                                            {
                                                Title = Globalization.GetString("Commom_Dialog_TipTitle"),
                                                Content = Globalization.GetString("QueueDialog_OpenFailure_Content"),
                                                PrimaryButtonText = Globalization.GetString("QueueDialog_OpenFailure_PrimaryButton"),
                                                CloseButtonText = Globalization.GetString("Common_Dialog_CancelButton")
                                            };

                                            if (await dialog.ShowAsync().ConfigureAwait(true) == ContentDialogResult.Primary)
                                            {
                                                if (!await Launcher.LaunchFileAsync(File))
                                                {
                                                    LauncherOptions options = new LauncherOptions
                                                    {
                                                        DisplayApplicationPicker = true
                                                    };
                                                    _ = await Launcher.LaunchFileAsync(File, options);
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        else
                        {
                            switch (File.FileType)
                            {
                                case ".jpg":
                                case ".png":
                                case ".bmp":
                                    {
                                        if (AnimationController.Current.IsEnableAnimation)
                                        {
                                            FileControlInstance.Nav.Navigate(typeof(PhotoViewer), new Tuple<FileControl, string>(FileControlInstance, File.Name), new DrillInNavigationTransitionInfo());
                                        }
                                        else
                                        {
                                            FileControlInstance.Nav.Navigate(typeof(PhotoViewer), new Tuple<FileControl, string>(FileControlInstance, File.Name), new SuppressNavigationTransitionInfo());
                                        }
                                        break;
                                    }
                                case ".mkv":
                                case ".mp4":
                                case ".mp3":
                                case ".flac":
                                case ".wma":
                                case ".wmv":
                                case ".m4a":
                                case ".mov":
                                case ".alac":
                                    {
                                        if (AnimationController.Current.IsEnableAnimation)
                                        {
                                            FileControlInstance.Nav.Navigate(typeof(MediaPlayer), File, new DrillInNavigationTransitionInfo());
                                        }
                                        else
                                        {
                                            FileControlInstance.Nav.Navigate(typeof(MediaPlayer), File, new SuppressNavigationTransitionInfo());
                                        }
                                        break;
                                    }
                                case ".txt":
                                    {
                                        if (AnimationController.Current.IsEnableAnimation)
                                        {
                                            FileControlInstance.Nav.Navigate(typeof(TextViewer), new Tuple<FileControl, FileSystemStorageItem>(FileControlInstance, TabTarget), new DrillInNavigationTransitionInfo());
                                        }
                                        else
                                        {
                                            FileControlInstance.Nav.Navigate(typeof(TextViewer), new Tuple<FileControl, FileSystemStorageItem>(FileControlInstance, TabTarget), new SuppressNavigationTransitionInfo());
                                        }
                                        break;
                                    }
                                case ".pdf":
                                    {
                                        if (AnimationController.Current.IsEnableAnimation)
                                        {
                                            FileControlInstance.Nav.Navigate(typeof(PdfReader), new Tuple<Frame, StorageFile>(FileControlInstance.Nav, File), new DrillInNavigationTransitionInfo());
                                        }
                                        else
                                        {
                                            FileControlInstance.Nav.Navigate(typeof(PdfReader), new Tuple<Frame, StorageFile>(FileControlInstance.Nav, File), new SuppressNavigationTransitionInfo());
                                        }
                                        break;
                                    }
                                case ".exe":
                                    {
                                        if (RunAsAdministrator)
                                        {
                                            await FullTrustExcutorController.RunAsAdministrator(TabTarget.Path).ConfigureAwait(false);
                                        }
                                        else
                                        {
                                            await FullTrustExcutorController.Run(TabTarget.Path).ConfigureAwait(false);
                                        }
                                        break;
                                    }
                                default:
                                    {
                                        ProgramPickerDialog Dialog = new ProgramPickerDialog(File);
                                        if (await Dialog.ShowAsync().ConfigureAwait(true) == ContentDialogResult.Primary)
                                        {
                                            if (Dialog.OpenFailed)
                                            {
                                                QueueContentDialog dialog = new QueueContentDialog
                                                {
                                                    Title = Globalization.GetString("Commom_Dialog_TipTitle"),
                                                    Content = Globalization.GetString("QueueDialog_OpenFailure_Content"),
                                                    PrimaryButtonText = Globalization.GetString("QueueDialog_OpenFailure_PrimaryButton"),
                                                    CloseButtonText = Globalization.GetString("Common_Dialog_CancelButton")
                                                };

                                                if (await dialog.ShowAsync().ConfigureAwait(true) == ContentDialogResult.Primary)
                                                {
                                                    if (!await Launcher.LaunchFileAsync(File))
                                                    {
                                                        LauncherOptions options = new LauncherOptions
                                                        {
                                                            DisplayApplicationPicker = true
                                                        };
                                                        _ = await Launcher.LaunchFileAsync(File, options);
                                                    }
                                                }
                                            }
                                        }
                                        break;
                                    }
                            }
                        }
                    }
                    else if ((await TabTarget.GetStorageItem().ConfigureAwait(true)) is StorageFolder Folder)
                    {
                        if (!await Folder.CheckExist().ConfigureAwait(true))
                        {
                            QueueContentDialog Dialog = new QueueContentDialog
                            {
                                Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                Content = Globalization.GetString("QueueDialog_LocateFolderFailure_Content"),
                                CloseButtonText = Globalization.GetString("Common_Dialog_RefreshButton")
                            };
                            _ = await Dialog.ShowAsync().ConfigureAwait(true);

                            if (SettingControl.IsDetachTreeViewAndPresenter)
                            {
                                await FileControlInstance.DisplayItemsInFolder(FileControlInstance.CurrentFolder, true).ConfigureAwait(false);
                            }
                            else
                            {
                                await FileControlInstance.DisplayItemsInFolder(FileControlInstance.CurrentNode, true).ConfigureAwait(false);
                            }

                            Interlocked.Exchange(ref TabTarget, null);
                            return;
                        }

                        if (FileControlInstance.CurrentNode != null)
                        {
                            if (SettingControl.IsDetachTreeViewAndPresenter)
                            {
                                await FileControlInstance.DisplayItemsInFolder(Folder).ConfigureAwait(true);
                            }
                            else
                            {
                                if (!FileControlInstance.CurrentNode.IsExpanded)
                                {
                                    FileControlInstance.CurrentNode.IsExpanded = true;
                                }

                                TreeViewNode TargetNode = await FileControlInstance.FolderTree.RootNodes[0].FindFolderLocationInTree(new PathAnalysis(TabTarget.Path, (FileControlInstance.FolderTree.RootNodes[0].Content as StorageFolder).Path)).ConfigureAwait(true);

                                if (TargetNode != null)
                                {
                                    await FileControlInstance.DisplayItemsInFolder(TargetNode).ConfigureAwait(true);
                                }
                            }
                        }
                    }

                    Interlocked.Exchange(ref TabTarget, null);
                }
            }
            catch (Exception ex)
            {
                ExceptionTracer.RequestBlueScreen(ex);
            }
        }

        public async void VideoEdit_Click(object sender, RoutedEventArgs e)
        {
            Restore();

            if (GeneralTransformer.IsAnyTransformTaskRunning)
            {
                QueueContentDialog Dialog = new QueueContentDialog
                {
                    Title = Globalization.GetString("Common_Dialog_TipTitle"),
                    Content = Globalization.GetString("QueueDialog_TaskWorking_Content"),
                    CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                };
                _ = await Dialog.ShowAsync().ConfigureAwait(true);

                return;
            }

            if ((await SelectedItem.GetStorageItem().ConfigureAwait(true)) is StorageFile File)
            {
                VideoEditDialog Dialog = new VideoEditDialog(File);
                if ((await Dialog.ShowAsync().ConfigureAwait(true)) == ContentDialogResult.Primary)
                {
                    StorageFile ExportFile = await FileControlInstance.CurrentFolder.CreateFileAsync($"{File.DisplayName} - {Globalization.GetString("Crop_Image_Name_Tail")}{Dialog.ExportFileType}", CreationCollisionOption.GenerateUniqueName);
                    await GeneralTransformer.GenerateCroppedVideoFromOriginAsync(ExportFile, Dialog.Composition, Dialog.MediaEncoding, Dialog.TrimmingPreference).ConfigureAwait(true);
                    if (Path.GetDirectoryName(ExportFile.Path) == FileControlInstance.CurrentFolder.Path && ApplicationData.Current.LocalSettings.Values["MediaCropStatus"] is string Status && Status == "Success")
                    {
                        FileCollection.Add(new FileSystemStorageItem(ExportFile, await ExportFile.GetSizeRawDataAsync().ConfigureAwait(true), await ExportFile.GetThumbnailBitmapAsync().ConfigureAwait(true), await ExportFile.GetModifiedTimeAsync().ConfigureAwait(true)));
                    }
                }
            }
        }

        public async void VideoMerge_Click(object sender, RoutedEventArgs e)
        {
            Restore();

            if (GeneralTransformer.IsAnyTransformTaskRunning)
            {
                QueueContentDialog Dialog = new QueueContentDialog
                {
                    Title = Globalization.GetString("Common_Dialog_TipTitle"),
                    Content = Globalization.GetString("QueueDialog_TaskWorking_Content"),
                    CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                };

                _ = await Dialog.ShowAsync().ConfigureAwait(true);

                return;
            }

            if ((await SelectedItem.GetStorageItem().ConfigureAwait(true)) is StorageFile Item)
            {
                VideoMergeDialog Dialog = new VideoMergeDialog(Item);
                if ((await Dialog.ShowAsync().ConfigureAwait(true)) == ContentDialogResult.Primary)
                {
                    StorageFile ExportFile = await FileControlInstance.CurrentFolder.CreateFileAsync($"{Item.DisplayName} - {Globalization.GetString("Merge_Image_Name_Tail")}{Dialog.ExportFileType}", CreationCollisionOption.GenerateUniqueName);
                    await GeneralTransformer.GenerateMergeVideoFromOriginAsync(ExportFile, Dialog.Composition, Dialog.MediaEncoding).ConfigureAwait(true);
                    if (Path.GetDirectoryName(ExportFile.Path) == FileControlInstance.CurrentFolder.Path && ApplicationData.Current.LocalSettings.Values["MediaMergeStatus"] is string Status && Status == "Success")
                    {
                        FileCollection.Add(new FileSystemStorageItem(ExportFile, await ExportFile.GetSizeRawDataAsync().ConfigureAwait(true), await ExportFile.GetThumbnailBitmapAsync().ConfigureAwait(true), await ExportFile.GetModifiedTimeAsync().ConfigureAwait(true)));
                    }
                }
            }
        }

        public async void ChooseOtherApp_Click(object sender, RoutedEventArgs e)
        {
            Restore();

            if ((await SelectedItem.GetStorageItem().ConfigureAwait(true)) is StorageFile Item)
            {
                ProgramPickerDialog Dialog = new ProgramPickerDialog(Item);
                if (await Dialog.ShowAsync().ConfigureAwait(true) == ContentDialogResult.Primary)
                {
                    if (Dialog.OpenFailed)
                    {
                        QueueContentDialog dialog = new QueueContentDialog
                        {
                            Title = Globalization.GetString("Commom_Dialog_TipTitle"),
                            Content = Globalization.GetString("QueueDialog_OpenFailure_Content"),
                            PrimaryButtonText = Globalization.GetString("QueueDialog_OpenFailure_PrimaryButton"),
                            CloseButtonText = Globalization.GetString("Common_Dialog_CancelButton")
                        };

                        if (await dialog.ShowAsync().ConfigureAwait(true) == ContentDialogResult.Primary)
                        {
                            if (!await Launcher.LaunchFileAsync(Item))
                            {
                                LauncherOptions options = new LauncherOptions
                                {
                                    DisplayApplicationPicker = true
                                };
                                _ = await Launcher.LaunchFileAsync(Item, options);
                            }
                        }
                    }
                    else if (Dialog.ContinueUseInnerViewer)
                    {
                        await EnterSelectedItem(SelectedItem).ConfigureAwait(false);
                    }
                }
            }
        }

        public async void RunWithSystemAuthority_Click(object sender, RoutedEventArgs e)
        {
            Restore();

            if (SelectedItem != null)
            {
                await EnterSelectedItem(SelectedItem, true).ConfigureAwait(false);
            }
        }

        private void ListHeaderName_Click(object sender, RoutedEventArgs e)
        {
            if (SortMap[SortTarget.Name] == SortDirection.Ascending)
            {
                SortMap[SortTarget.Name] = SortDirection.Descending;
                SortMap[SortTarget.Type] = SortDirection.Ascending;
                SortMap[SortTarget.ModifiedTime] = SortDirection.Ascending;
                SortMap[SortTarget.Size] = SortDirection.Ascending;

                List<FileSystemStorageItem> SortResult = SortList(FileCollection, SortTarget.Name, SortDirection.Descending);
                FileCollection.Clear();

                foreach (FileSystemStorageItem Item in SortResult)
                {
                    FileCollection.Add(Item);
                }
            }
            else
            {
                SortMap[SortTarget.Name] = SortDirection.Ascending;
                SortMap[SortTarget.Type] = SortDirection.Ascending;
                SortMap[SortTarget.ModifiedTime] = SortDirection.Ascending;
                SortMap[SortTarget.Size] = SortDirection.Ascending;

                List<FileSystemStorageItem> SortResult = SortList(FileCollection, SortTarget.Name, SortDirection.Ascending);
                FileCollection.Clear();

                foreach (FileSystemStorageItem Item in SortResult)
                {
                    FileCollection.Add(Item);
                }
            }
        }

        private void ListHeaderModifiedTime_Click(object sender, RoutedEventArgs e)
        {
            if (SortMap[SortTarget.ModifiedTime] == SortDirection.Ascending)
            {
                SortMap[SortTarget.ModifiedTime] = SortDirection.Descending;
                SortMap[SortTarget.Type] = SortDirection.Ascending;
                SortMap[SortTarget.Name] = SortDirection.Ascending;
                SortMap[SortTarget.Size] = SortDirection.Ascending;

                List<FileSystemStorageItem> SortResult = SortList(FileCollection, SortTarget.ModifiedTime, SortDirection.Descending);
                FileCollection.Clear();

                foreach (FileSystemStorageItem Item in SortResult)
                {
                    FileCollection.Add(Item);
                }
            }
            else
            {
                SortMap[SortTarget.ModifiedTime] = SortDirection.Ascending;
                SortMap[SortTarget.Type] = SortDirection.Ascending;
                SortMap[SortTarget.Name] = SortDirection.Ascending;
                SortMap[SortTarget.Size] = SortDirection.Ascending;

                List<FileSystemStorageItem> SortResult = SortList(FileCollection, SortTarget.ModifiedTime, SortDirection.Ascending);
                FileCollection.Clear();

                foreach (FileSystemStorageItem Item in SortResult)
                {
                    FileCollection.Add(Item);
                }
            }
        }

        private void ListHeaderType_Click(object sender, RoutedEventArgs e)
        {
            if (SortMap[SortTarget.Type] == SortDirection.Ascending)
            {
                SortMap[SortTarget.Type] = SortDirection.Descending;
                SortMap[SortTarget.Name] = SortDirection.Ascending;
                SortMap[SortTarget.ModifiedTime] = SortDirection.Ascending;
                SortMap[SortTarget.Size] = SortDirection.Ascending;

                List<FileSystemStorageItem> SortResult = SortList(FileCollection, SortTarget.Type, SortDirection.Descending);
                FileCollection.Clear();

                foreach (FileSystemStorageItem Item in SortResult)
                {
                    FileCollection.Add(Item);
                }
            }
            else
            {
                SortMap[SortTarget.Type] = SortDirection.Ascending;
                SortMap[SortTarget.Name] = SortDirection.Ascending;
                SortMap[SortTarget.ModifiedTime] = SortDirection.Ascending;
                SortMap[SortTarget.Size] = SortDirection.Ascending;

                List<FileSystemStorageItem> SortResult = SortList(FileCollection, SortTarget.Type, SortDirection.Ascending);
                FileCollection.Clear();

                foreach (FileSystemStorageItem Item in SortResult)
                {
                    FileCollection.Add(Item);
                }
            }
        }

        private void ListHeaderSize_Click(object sender, RoutedEventArgs e)
        {
            if (SortMap[SortTarget.Size] == SortDirection.Ascending)
            {
                SortMap[SortTarget.Size] = SortDirection.Descending;
                SortMap[SortTarget.Type] = SortDirection.Ascending;
                SortMap[SortTarget.ModifiedTime] = SortDirection.Ascending;
                SortMap[SortTarget.Name] = SortDirection.Ascending;

                List<FileSystemStorageItem> SortResult = SortList(FileCollection, SortTarget.Size, SortDirection.Descending);
                FileCollection.Clear();

                foreach (FileSystemStorageItem Item in SortResult)
                {
                    FileCollection.Add(Item);
                }

            }
            else
            {
                SortMap[SortTarget.Size] = SortDirection.Ascending;
                SortMap[SortTarget.Type] = SortDirection.Ascending;
                SortMap[SortTarget.ModifiedTime] = SortDirection.Ascending;
                SortMap[SortTarget.Name] = SortDirection.Ascending;

                List<FileSystemStorageItem> SortResult = SortList(FileCollection, SortTarget.Size, SortDirection.Ascending);
                FileCollection.Clear();

                foreach (FileSystemStorageItem Item in SortResult)
                {
                    FileCollection.Add(Item);
                }
            }
        }


        public List<FileSystemStorageItem> SortList(IEnumerable<FileSystemStorageItem> FileCollection, SortTarget Target, SortDirection Direction)
        {
            switch (Target)
            {
                case SortTarget.Name:
                    {
                        if (Direction == SortDirection.Ascending)
                        {
                            List<FileSystemStorageItem> FolderSortList = FileCollection.Where((It) => It.StorageType == StorageItemTypes.Folder).OrderBy((Item) => Item.Name).ToList();
                            List<FileSystemStorageItem> FileSortList = FileCollection.Where((It) => It.StorageType == StorageItemTypes.File).OrderBy((Item) => Item.Name).ToList();

                            return new List<FileSystemStorageItem>(FolderSortList.Concat(FileSortList));
                        }
                        else
                        {
                            List<FileSystemStorageItem> FolderSortList = FileCollection.Where((It) => It.StorageType == StorageItemTypes.Folder).OrderByDescending((Item) => Item.Name).ToList();
                            List<FileSystemStorageItem> FileSortList = FileCollection.Where((It) => It.StorageType == StorageItemTypes.File).OrderByDescending((Item) => Item.Name).ToList();

                            return new List<FileSystemStorageItem>(FileSortList.Concat(FolderSortList));
                        }
                    }

                case SortTarget.Type:
                    {
                        if (Direction == SortDirection.Ascending)
                        {
                            List<FileSystemStorageItem> FolderSortList = FileCollection.Where((It) => It.StorageType == StorageItemTypes.Folder).OrderBy((Item) => Item.Type).ToList();
                            List<FileSystemStorageItem> FileSortList = FileCollection.Where((It) => It.StorageType == StorageItemTypes.File).OrderBy((Item) => Item.Type).ToList();

                            return new List<FileSystemStorageItem>(FolderSortList.Concat(FileSortList));
                        }
                        else
                        {
                            List<FileSystemStorageItem> FolderSortList = FileCollection.Where((It) => It.StorageType == StorageItemTypes.Folder).OrderByDescending((Item) => Item.Type).ToList();
                            List<FileSystemStorageItem> FileSortList = FileCollection.Where((It) => It.StorageType == StorageItemTypes.File).OrderByDescending((Item) => Item.Type).ToList();

                            return new List<FileSystemStorageItem>(FileSortList.Concat(FolderSortList));
                        }
                    }
                case SortTarget.ModifiedTime:
                    {
                        if (Direction == SortDirection.Ascending)
                        {
                            List<FileSystemStorageItem> FolderSortList = FileCollection.Where((It) => It.StorageType == StorageItemTypes.Folder).OrderBy((Item) => Item.ModifiedTimeRaw).ToList();
                            List<FileSystemStorageItem> FileSortList = FileCollection.Where((It) => It.StorageType == StorageItemTypes.File).OrderBy((Item) => Item.ModifiedTimeRaw).ToList();

                            return new List<FileSystemStorageItem>(FolderSortList.Concat(FileSortList));
                        }
                        else
                        {
                            List<FileSystemStorageItem> FolderSortList = FileCollection.Where((It) => It.StorageType == StorageItemTypes.Folder).OrderByDescending((Item) => Item.ModifiedTimeRaw).ToList();
                            List<FileSystemStorageItem> FileSortList = FileCollection.Where((It) => It.StorageType == StorageItemTypes.File).OrderByDescending((Item) => Item.ModifiedTimeRaw).ToList();

                            return new List<FileSystemStorageItem>(FileSortList.Concat(FolderSortList));
                        }
                    }
                case SortTarget.Size:
                    {
                        if (Direction == SortDirection.Ascending)
                        {
                            List<FileSystemStorageItem> FolderSortList = FileCollection.Where((It) => It.StorageType == StorageItemTypes.Folder).OrderBy((Item) => Item.SizeRaw).ToList();
                            List<FileSystemStorageItem> FileSortList = FileCollection.Where((It) => It.StorageType == StorageItemTypes.File).OrderBy((Item) => Item.SizeRaw).ToList();

                            return new List<FileSystemStorageItem>(FolderSortList.Concat(FileSortList));
                        }
                        else
                        {
                            List<FileSystemStorageItem> FolderSortList = FileCollection.Where((It) => It.StorageType == StorageItemTypes.Folder).OrderByDescending((Item) => Item.SizeRaw).ToList();
                            List<FileSystemStorageItem> FileSortList = FileCollection.Where((It) => It.StorageType == StorageItemTypes.File).OrderByDescending((Item) => Item.SizeRaw).ToList();

                            return new List<FileSystemStorageItem>(FileSortList.Concat(FolderSortList));
                        }
                    }
                default:
                    {
                        return null;
                    }
            }
        }

        private void QRTeachTip_Closing(Microsoft.UI.Xaml.Controls.TeachingTip sender, Microsoft.UI.Xaml.Controls.TeachingTipClosingEventArgs args)
        {
            QRImage.Source = null;
            WiFiProvider.Dispose();
            WiFiProvider = null;
        }

        public async void CreateFile_Click(object sender, RoutedEventArgs e)
        {
            Restore();

            NewFileDialog Dialog = new NewFileDialog();
            if ((await Dialog.ShowAsync().ConfigureAwait(true)) == ContentDialogResult.Primary)
            {
                try
                {
                    StorageFile NewFile = null;

                    switch (Path.GetExtension(Dialog.NewFileName))
                    {
                        case ".zip":
                            {
                                NewFile = await SpecialTypeGenerator.Current.CreateZipAsync(FileControlInstance.CurrentFolder, Dialog.NewFileName).ConfigureAwait(true);
                                break;
                            }
                        case ".rtf":
                            {
                                NewFile = await SpecialTypeGenerator.Current.CreateRtfAsync(FileControlInstance.CurrentFolder, Dialog.NewFileName).ConfigureAwait(true);
                                break;
                            }
                        case ".xlsx":
                            {
                                NewFile = await SpecialTypeGenerator.Current.CreateExcelAsync(FileControlInstance.CurrentFolder, Dialog.NewFileName).ConfigureAwait(true);
                                break;
                            }
                        default:
                            {
                                NewFile = await FileControlInstance.CurrentFolder.CreateFileAsync(Dialog.NewFileName, CreationCollisionOption.GenerateUniqueName);
                                break;
                            }
                    }

                    if (NewFile == null)
                    {
                        throw new UnauthorizedAccessException();
                    }

                    int Index = FileCollection.IndexOf(FileCollection.FirstOrDefault((Item) => Item.StorageType == StorageItemTypes.File));
                    if (Index == -1)
                    {
                        FileCollection.Add(new FileSystemStorageItem(NewFile, await NewFile.GetSizeRawDataAsync().ConfigureAwait(true), await NewFile.GetThumbnailBitmapAsync().ConfigureAwait(true), await NewFile.GetModifiedTimeAsync().ConfigureAwait(true)));
                    }
                    else
                    {
                        FileCollection.Insert(Index, new FileSystemStorageItem(NewFile, await NewFile.GetSizeRawDataAsync().ConfigureAwait(true), await NewFile.GetThumbnailBitmapAsync().ConfigureAwait(true), await NewFile.GetModifiedTimeAsync().ConfigureAwait(true)));
                    }
                }
                catch (UnauthorizedAccessException)
                {
                    QueueContentDialog dialog = new QueueContentDialog
                    {
                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                        Content = Globalization.GetString("QueueDialog_UnauthorizedCreateNewFile_Content"),
                        PrimaryButtonText = Globalization.GetString("Common_Dialog_NowButton"),
                        CloseButtonText = Globalization.GetString("Common_Dialog_LaterButton")
                    };

                    if (await dialog.ShowAsync().ConfigureAwait(true) == ContentDialogResult.Primary)
                    {
                        _ = await Launcher.LaunchFolderAsync(FileControlInstance.CurrentFolder);
                    }
                }
            }
        }

        public async void CompressFolder_Click(object sender, RoutedEventArgs e)
        {
            Restore();

            StorageFolder Item = (await SelectedItem.GetStorageItem().ConfigureAwait(true)) as StorageFolder;

            if (!await Item.CheckExist().ConfigureAwait(true))
            {
                QueueContentDialog Dialog = new QueueContentDialog
                {
                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                    Content = Globalization.GetString("QueueDialog_LocateFolderFailure_Content"),
                    CloseButtonText = Globalization.GetString("Common_Dialog_RefreshButton")
                };
                _ = await Dialog.ShowAsync().ConfigureAwait(true);

                if (SettingControl.IsDetachTreeViewAndPresenter)
                {
                    await FileControlInstance.DisplayItemsInFolder(FileControlInstance.CurrentFolder, true).ConfigureAwait(false);
                }
                else
                {
                    await FileControlInstance.DisplayItemsInFolder(FileControlInstance.CurrentNode, true).ConfigureAwait(false);
                }
                return;
            }

            ZipDialog dialog = new ZipDialog(true, Item.DisplayName);

            if ((await dialog.ShowAsync().ConfigureAwait(true)) == ContentDialogResult.Primary)
            {
                await LoadingActivation(true, Globalization.GetString("Progress_Tip_Compressing")).ConfigureAwait(true);

                if (dialog.IsCryptionEnable)
                {
                    await CreateZipAsync(Item, dialog.FileName, (int)dialog.Level, true, dialog.Key, dialog.Password).ConfigureAwait(true);
                }
                else
                {
                    await CreateZipAsync(Item, dialog.FileName, (int)dialog.Level).ConfigureAwait(true);
                }
            }

            await LoadingActivation(false).ConfigureAwait(true);
        }

        private async void GridViewControl_DragItemsStarting(object sender, DragItemsStartingEventArgs e)
        {
            if (e.Items.Count == 0)
            {
                return;
            }

            List<IStorageItem> TempList = new List<IStorageItem>(e.Items.Count);
            foreach (object obj in e.Items)
            {
                TempList.Add(await (obj as FileSystemStorageItem).GetStorageItem().ConfigureAwait(true));
            }
            e.Data.SetStorageItems(TempList, false);
        }

        private void ViewControl_DragOver(object sender, DragEventArgs e)
        {
            if (e.DataView.Contains(StandardDataFormats.StorageItems))
            {
                if (e.Modifiers.HasFlag(DragDropModifiers.Control))
                {
                    e.AcceptedOperation = DataPackageOperation.Copy;
                    e.DragUIOverride.Caption = $"{Globalization.GetString("Drag_Tip_CopyTo")} {FileControlInstance.CurrentFolder.DisplayName}";
                }
                else
                {
                    e.AcceptedOperation = DataPackageOperation.Move;
                    e.DragUIOverride.Caption = $"{Globalization.GetString("Drag_Tip_MoveTo")} {FileControlInstance.CurrentFolder.DisplayName}";
                }

                e.DragUIOverride.IsContentVisible = true;
                e.DragUIOverride.IsCaptionVisible = true;
            }
            else
            {
                e.AcceptedOperation = DataPackageOperation.None;
            }
        }

        private async void Item_Drop(object sender, DragEventArgs e)
        {
            var Deferral = e.GetDeferral();

            if (Interlocked.Exchange(ref DropLock, 1) == 0)
            {
                List<IStorageItem> DragItemList = (await e.DataView.GetStorageItemsAsync()).ToList();

                try
                {
                    StorageFolder TargetFolder = (await ((sender as SelectorItem).Content as FileSystemStorageItem).GetStorageItem().ConfigureAwait(true)) as StorageFolder;

                    if (DragItemList.Contains(TargetFolder))
                    {
                        QueueContentDialog Dialog = new QueueContentDialog
                        {
                            Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                            Content = Globalization.GetString("QueueDialog_DragIncludeFolderError"),
                            CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                        };
                        _ = await Dialog.ShowAsync().ConfigureAwait(true);

                        return;
                    }

                    CopyAndMoveRecord.Clear();

                    switch (e.AcceptedOperation)
                    {
                        case DataPackageOperation.Copy:
                            {
                                bool IsItemNotFound = false;
                                bool IsUnauthorized = false;
                                bool IsSpaceError = false;

                                await LoadingActivation(true, Globalization.GetString("Progress_Tip_Copying")).ConfigureAwait(true);

                                foreach (IStorageItem Item in DragItemList)
                                {
                                    try
                                    {
                                        if (Item is StorageFile File)
                                        {
                                            if (!await File.CheckExist().ConfigureAwait(true))
                                            {
                                                IsItemNotFound = true;
                                                continue;
                                            }

                                            CopyAndMoveRecord.Add($"{File.Path}||Copy||File||{Path.Combine(TargetFolder.Path, File.Name)}");

                                            _ = await File.CopyAsync(TargetFolder, Item.Name, NameCollisionOption.GenerateUniqueName);
                                        }
                                        else if (Item is StorageFolder Folder)
                                        {
                                            if (!await Folder.CheckExist().ConfigureAwait(true))
                                            {
                                                IsItemNotFound = true;
                                                continue;
                                            }

                                            CopyAndMoveRecord.Add($"{Folder.Path}||Copy||Folder||{Path.Combine(TargetFolder.Path, Folder.Name)}");

                                            StorageFolder NewFolder = await TargetFolder.CreateFolderAsync(Item.Name, CreationCollisionOption.GenerateUniqueName);
                                            await Folder.CopySubFilesAndSubFoldersAsync(NewFolder).ConfigureAwait(true);

                                            if (!SettingControl.IsDetachTreeViewAndPresenter && FileControlInstance.CurrentNode.IsExpanded)
                                            {
                                                await FileControlInstance.FolderTree.RootNodes[0].UpdateAllSubNode().ConfigureAwait(true);
                                            }
                                        }
                                    }
                                    catch (UnauthorizedAccessException)
                                    {
                                        IsUnauthorized = true;
                                    }
                                    catch (System.Runtime.InteropServices.COMException)
                                    {
                                        IsSpaceError = true;
                                    }
                                }

                                if (IsItemNotFound)
                                {
                                    QueueContentDialog Dialog = new QueueContentDialog
                                    {
                                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                        Content = Globalization.GetString("QueueDialog_CopyFailForNotExist_Content"),
                                        CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                                    };
                                    _ = await Dialog.ShowAsync().ConfigureAwait(true);
                                }
                                else if (IsUnauthorized)
                                {
                                    QueueContentDialog dialog = new QueueContentDialog
                                    {
                                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                        Content = Globalization.GetString("QueueDialog_UnauthorizedPaste_Content"),
                                        PrimaryButtonText = Globalization.GetString("Common_Dialog_NowButton"),
                                        CloseButtonText = Globalization.GetString("Common_Dialog_LaterButton")
                                    };

                                    if (await dialog.ShowAsync().ConfigureAwait(true) == ContentDialogResult.Primary)
                                    {
                                        _ = await Launcher.LaunchFolderAsync(FileControlInstance.CurrentFolder);
                                    }
                                }
                                else if (IsSpaceError)
                                {
                                    QueueContentDialog QueueContenDialog = new QueueContentDialog
                                    {
                                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                        Content = Globalization.GetString("QueueDialog_CopyFail_FreeSpaceCritical_Content"),
                                        CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                                    };
                                    _ = await QueueContenDialog.ShowAsync().ConfigureAwait(true);
                                }

                                break;
                            }
                        case DataPackageOperation.Move:
                            {
                                await LoadingActivation(true, Globalization.GetString("Progress_Tip_Moving")).ConfigureAwait(true);

                                bool IsItemNotFound = false;
                                bool IsUnauthorized = false;
                                bool IsSpaceError = false;
                                bool IsCaptured = false;

                                foreach (IStorageItem Item in DragItemList)
                                {
                                    try
                                    {
                                        if (Item is StorageFile File)
                                        {
                                            if (!await File.CheckExist().ConfigureAwait(true))
                                            {
                                                IsItemNotFound = true;
                                                continue;
                                            }

                                            CopyAndMoveRecord.Add($"{File.Path}||Move||File||{Path.Combine(TargetFolder.Path, File.Name)}");

                                            await File.MoveAsync(TargetFolder, Item.Name, NameCollisionOption.GenerateUniqueName);
                                            FileCollection.Remove(FileCollection.FirstOrDefault((It) => It.Path == Item.Path));
                                        }
                                        else if (Item is StorageFolder Folder)
                                        {
                                            if (!await Folder.CheckExist().ConfigureAwait(true))
                                            {
                                                IsItemNotFound = true;
                                                continue;
                                            }

                                            CopyAndMoveRecord.Add($"{Folder.Path}||Move||Folder||{Path.Combine(TargetFolder.Path, Folder.Name)}");

                                            StorageFolder NewFolder = await TargetFolder.CreateFolderAsync(Item.Name, CreationCollisionOption.OpenIfExists);
                                            await Folder.MoveSubFilesAndSubFoldersAsync(NewFolder).ConfigureAwait(true);
                                            await Folder.DeleteAllSubFilesAndFolders().ConfigureAwait(true);
                                            await Folder.DeleteAsync(StorageDeleteOption.PermanentDelete);
                                            FileCollection.Remove(FileCollection.FirstOrDefault((It) => It.Path == Item.Path));

                                            if (!SettingControl.IsDetachTreeViewAndPresenter && FileControlInstance.CurrentNode.IsExpanded)
                                            {
                                                await FileControlInstance.FolderTree.RootNodes[0].UpdateAllSubNode().ConfigureAwait(true);
                                            }
                                        }
                                    }
                                    catch (UnauthorizedAccessException)
                                    {
                                        IsUnauthorized = true;
                                    }
                                    catch (System.Runtime.InteropServices.COMException)
                                    {
                                        IsSpaceError = true;
                                    }
                                    catch (Exception)
                                    {
                                        IsCaptured = true;
                                    }
                                }

                                if (IsItemNotFound)
                                {
                                    QueueContentDialog Dialog = new QueueContentDialog
                                    {
                                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                        Content = Globalization.GetString("QueueDialog_MoveFailForNotExist_Content"),
                                        CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                                    };

                                    _ = await Dialog.ShowAsync().ConfigureAwait(true);
                                }
                                else if (IsUnauthorized)
                                {
                                    QueueContentDialog dialog = new QueueContentDialog
                                    {
                                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                        Content = Globalization.GetString("QueueDialog_UnauthorizedPaste_Content"),
                                        PrimaryButtonText = Globalization.GetString("Common_Dialog_NowButton"),
                                        CloseButtonText = Globalization.GetString("Common_Dialog_LaterButton")
                                    };

                                    if (await dialog.ShowAsync().ConfigureAwait(true) == ContentDialogResult.Primary)
                                    {
                                        _ = await Launcher.LaunchFolderAsync(FileControlInstance.CurrentFolder);
                                    }
                                }
                                else if (IsSpaceError)
                                {
                                    QueueContentDialog QueueContenDialog = new QueueContentDialog
                                    {
                                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                        Content = Globalization.GetString("QueueDialog_MoveFail_FreeSpaceCritical_Content"),
                                        CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                                    };
                                    _ = await QueueContenDialog.ShowAsync().ConfigureAwait(true);
                                }
                                else if (IsCaptured)
                                {
                                    QueueContentDialog dialog = new QueueContentDialog
                                    {
                                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                        Content = Globalization.GetString("QueueDialog_MoveFail_Captured_Content"),
                                        CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                                    };

                                    _ = await dialog.ShowAsync().ConfigureAwait(true);
                                }

                                break;
                            }
                    }
                }
                finally
                {
                    DragItemList.Clear();
                    await LoadingActivation(false).ConfigureAwait(true);
                    e.Handled = true;
                    Deferral.Complete();
                    _ = Interlocked.Exchange(ref DropLock, 0);
                }
            }
        }


        private async void ListViewControl_DragItemsStarting(object sender, DragItemsStartingEventArgs e)
        {
            if (e.Items.Count == 0)
            {
                return;
            }

            List<IStorageItem> TempList = new List<IStorageItem>(e.Items.Count);
            foreach (object obj in e.Items)
            {
                TempList.Add(await (obj as FileSystemStorageItem).GetStorageItem().ConfigureAwait(true));
            }
            e.Data.SetStorageItems(TempList, false);
        }

        private void ViewControl_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                args.ItemContainer.AllowDrop = false;
                args.ItemContainer.AllowFocusOnInteraction = true;
                args.ItemContainer.Drop -= Item_Drop;
                args.ItemContainer.DragEnter -= ItemContainer_DragEnter;
                args.ItemContainer.PointerEntered -= ItemContainer_PointerEntered;
                args.ItemContainer.PointerExited -= ItemContainer_PointerExited;
            }
            else
            {
                if (args.Item is FileSystemStorageItem Item)
                {
                    if (Item.StorageType == StorageItemTypes.File && Item.Thumbnail == null)
                    {
                        _ = Item.LoadMoreProperty();
                    }

                    if (Item.StorageType == StorageItemTypes.Folder)
                    {
                        args.ItemContainer.AllowDrop = true;
                        args.ItemContainer.Drop += Item_Drop;
                        args.ItemContainer.DragEnter += ItemContainer_DragEnter;
                    }

                    args.ItemContainer.AllowFocusOnInteraction = false;

                    args.ItemContainer.PointerEntered += ItemContainer_PointerEntered;
                    args.ItemContainer.PointerExited += ItemContainer_PointerExited;
                }
            }
        }

        private void ItemContainer_DragEnter(object sender, DragEventArgs e)
        {
            if (e.DataView.Contains(StandardDataFormats.StorageItems))
            {
                if (sender is SelectorItem)
                {
                    FileSystemStorageItem Item = (sender as SelectorItem).Content as FileSystemStorageItem;

                    if (e.Modifiers.HasFlag(DragDropModifiers.Control))
                    {
                        e.AcceptedOperation = DataPackageOperation.Copy;
                        e.DragUIOverride.Caption = $"{Globalization.GetString("Drag_Tip_CopyTo")} {Item.DisplayName}";
                    }
                    else
                    {
                        e.AcceptedOperation = DataPackageOperation.Move;
                        e.DragUIOverride.Caption = $"{Globalization.GetString("Drag_Tip_MoveTo")} {Item.DisplayName}";
                    }

                    e.DragUIOverride.IsContentVisible = true;
                    e.DragUIOverride.IsCaptionVisible = true;
                }
            }
            else
            {
                e.AcceptedOperation = DataPackageOperation.None;
            }
        }

        private void ItemContainer_PointerExited(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (!SettingControl.IsDoubleClickEnable)
            {
                PointerHoverTimer.Stop();
            }
        }

        private void ItemContainer_PointerEntered(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (!SettingControl.IsDoubleClickEnable)
            {
                if ((e.OriginalSource as FrameworkElement)?.DataContext is FileSystemStorageItem Item)
                {
                    StayInItem = Item;

                    PointerHoverTimer.Start();
                }
            }
        }

        private void Timer_Tick(object sender, object e)
        {
            PointerHoverTimer.Stop();
            SelectedItem = StayInItem;
        }

        private async void ViewControl_Drop(object sender, DragEventArgs e)
        {
            var Deferral = e.GetDeferral();

            if (Interlocked.Exchange(ref ViewDropLock, 1) == 0)
            {
                List<IStorageItem> DragItemList = (await e.DataView.GetStorageItemsAsync()).ToList();

                try
                {
                    StorageFolder TargetFolder = FileControlInstance.CurrentFolder;

                    if (TargetFolder.Path == Path.GetDirectoryName(DragItemList[0].Path))
                    {
                        return;
                    }

                    if (DragItemList.Contains(TargetFolder))
                    {
                        QueueContentDialog Dialog = new QueueContentDialog
                        {
                            Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                            Content = Globalization.GetString("QueueDialog_DragIncludeFolderError"),
                            CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                        };
                        _ = await Dialog.ShowAsync().ConfigureAwait(true);

                        return;
                    }

                    CopyAndMoveRecord.Clear();

                    switch (e.AcceptedOperation)
                    {
                        case DataPackageOperation.Copy:
                            {
                                bool IsItemNotFound = false;
                                bool IsUnauthorized = false;
                                bool IsSpaceError = false;

                                await LoadingActivation(true, Globalization.GetString("Progress_Tip_Copying")).ConfigureAwait(true);

                                foreach (IStorageItem Item in DragItemList)
                                {
                                    try
                                    {
                                        if (Item is StorageFile File)
                                        {
                                            if (!await File.CheckExist().ConfigureAwait(true))
                                            {
                                                IsItemNotFound = true;
                                                continue;
                                            }

                                            CopyAndMoveRecord.Add($"{File.Path}||Copy||File||{Path.Combine(TargetFolder.Path, File.Name)}");

                                            StorageFile NewFile = await File.CopyAsync(TargetFolder, Item.Name, NameCollisionOption.GenerateUniqueName);
                                            FileCollection.Add(new FileSystemStorageItem(NewFile, await NewFile.GetSizeRawDataAsync().ConfigureAwait(true), await NewFile.GetThumbnailBitmapAsync().ConfigureAwait(true), await NewFile.GetModifiedTimeAsync().ConfigureAwait(true)));
                                        }
                                        else if (Item is StorageFolder Folder)
                                        {
                                            if (!await Folder.CheckExist().ConfigureAwait(true))
                                            {
                                                IsItemNotFound = true;
                                                continue;
                                            }

                                            CopyAndMoveRecord.Add($"{Folder.Path}||Copy||Folder||{Path.Combine(TargetFolder.Path, Folder.Name)}");

                                            StorageFolder NewFolder = await TargetFolder.CreateFolderAsync(Item.Name, CreationCollisionOption.GenerateUniqueName);
                                            await Folder.CopySubFilesAndSubFoldersAsync(NewFolder).ConfigureAwait(true);
                                            FileCollection.Add(new FileSystemStorageItem(NewFolder, await NewFolder.GetModifiedTimeAsync().ConfigureAwait(true)));

                                            if (!SettingControl.IsDetachTreeViewAndPresenter)
                                            {
                                                await FileControlInstance.FolderTree.RootNodes[0].UpdateAllSubNode().ConfigureAwait(true);
                                            }
                                        }
                                    }
                                    catch (UnauthorizedAccessException)
                                    {
                                        IsUnauthorized = true;
                                    }
                                    catch (System.Runtime.InteropServices.COMException)
                                    {
                                        IsSpaceError = true;
                                    }
                                }

                                if (IsItemNotFound)
                                {
                                    QueueContentDialog Dialog = new QueueContentDialog
                                    {
                                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                        Content = Globalization.GetString("QueueDialog_CopyFailForNotExist_Content"),
                                        CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                                    };
                                    _ = await Dialog.ShowAsync().ConfigureAwait(true);
                                }
                                else if (IsUnauthorized)
                                {
                                    QueueContentDialog dialog = new QueueContentDialog
                                    {
                                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                        Content = Globalization.GetString("QueueDialog_UnauthorizedPaste_Content"),
                                        PrimaryButtonText = Globalization.GetString("Common_Dialog_NowButton"),
                                        CloseButtonText = Globalization.GetString("Common_Dialog_LaterButton")
                                    };

                                    if (await dialog.ShowAsync().ConfigureAwait(true) == ContentDialogResult.Primary)
                                    {
                                        _ = await Launcher.LaunchFolderAsync(FileControlInstance.CurrentFolder);
                                    }
                                }
                                else if (IsSpaceError)
                                {
                                    QueueContentDialog QueueContenDialog = new QueueContentDialog
                                    {
                                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                        Content = Globalization.GetString("QueueDialog_CopyFail_FreeSpaceCritical_Content"),
                                        CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                                    };
                                    _ = await QueueContenDialog.ShowAsync().ConfigureAwait(true);
                                }

                                break;
                            }
                        case DataPackageOperation.Move:
                            {
                                await LoadingActivation(true, Globalization.GetString("Progress_Tip_Moving")).ConfigureAwait(true);

                                bool IsItemNotFound = false;
                                bool IsUnauthorized = false;
                                bool IsSpaceError = false;
                                bool IsCaptured = false;

                                foreach (IStorageItem Item in DragItemList)
                                {
                                    try
                                    {
                                        if (Item is StorageFile File)
                                        {
                                            if (!await File.CheckExist().ConfigureAwait(true))
                                            {
                                                IsItemNotFound = true;
                                                continue;
                                            }

                                            CopyAndMoveRecord.Add($"{File.Path}||Move||File||{Path.Combine(TargetFolder.Path, File.Name)}");

                                            await File.MoveAsync(TargetFolder, Item.Name, NameCollisionOption.GenerateUniqueName);
                                            StorageFile NewFile = await StorageFile.GetFileFromPathAsync(File.Path);
                                            FileCollection.Add(new FileSystemStorageItem(NewFile, await NewFile.GetSizeRawDataAsync().ConfigureAwait(true), await NewFile.GetThumbnailBitmapAsync().ConfigureAwait(true), await NewFile.GetModifiedTimeAsync().ConfigureAwait(true)));
                                        }
                                        else if (Item is StorageFolder Folder)
                                        {
                                            if (!await Folder.CheckExist().ConfigureAwait(true))
                                            {
                                                IsItemNotFound = true;
                                                continue;
                                            }

                                            CopyAndMoveRecord.Add($"{Folder.Path}||Move||Folder||{Path.Combine(TargetFolder.Path, Folder.Name)}");

                                            StorageFolder NewFolder = await TargetFolder.CreateFolderAsync(Item.Name, CreationCollisionOption.OpenIfExists);
                                            await Folder.MoveSubFilesAndSubFoldersAsync(NewFolder).ConfigureAwait(true);
                                            await Folder.DeleteAllSubFilesAndFolders().ConfigureAwait(true);
                                            await Folder.DeleteAsync(StorageDeleteOption.PermanentDelete);

                                            if (FileCollection.All((Item) => Item.Name != NewFolder.Name))
                                            {
                                                FileCollection.Add(new FileSystemStorageItem(NewFolder, await NewFolder.GetModifiedTimeAsync().ConfigureAwait(true)));
                                            }

                                            if (!SettingControl.IsDetachTreeViewAndPresenter)
                                            {
                                                await FileControlInstance.FolderTree.RootNodes[0].UpdateAllSubNode().ConfigureAwait(true);
                                            }
                                        }
                                    }
                                    catch (UnauthorizedAccessException)
                                    {
                                        IsUnauthorized = true;
                                    }
                                    catch (System.Runtime.InteropServices.COMException)
                                    {
                                        IsSpaceError = true;
                                    }
                                    catch (Exception)
                                    {
                                        IsCaptured = true;
                                    }
                                }

                                if (!SettingControl.IsDetachTreeViewAndPresenter && DragItemList.Any((Item) => Item.IsOfType(StorageItemTypes.Folder)))
                                {
                                    FileControl Control = TabViewContainer.ThisPage.TabViewControl.TabItems.Select((Tab) => ((Tab as Microsoft.UI.Xaml.Controls.TabViewItem)?.Content as Frame)?.Content as FileControl).Where((Control) => Control != null).FirstOrDefault((Control) => Control.CurrentFolder.Path == Path.GetDirectoryName(DragItemList[0].Path));
                                    if (Control != null)
                                    {
                                        await Control.CurrentNode.UpdateAllSubNode().ConfigureAwait(true);
                                        TabViewContainer.ThisPage.FFInstanceContainer[Control].Refresh_Click(null, null);
                                    }
                                }

                                if (IsItemNotFound)
                                {
                                    QueueContentDialog Dialog = new QueueContentDialog
                                    {
                                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                        Content = Globalization.GetString("QueueDialog_MoveFailForNotExist_Content"),
                                        CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                                    };

                                    _ = await Dialog.ShowAsync().ConfigureAwait(true);
                                }
                                else if (IsUnauthorized)
                                {
                                    QueueContentDialog dialog = new QueueContentDialog
                                    {
                                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                        Content = Globalization.GetString("QueueDialog_UnauthorizedPaste_Content"),
                                        PrimaryButtonText = Globalization.GetString("Common_Dialog_NowButton"),
                                        CloseButtonText = Globalization.GetString("Common_Dialog_LaterButton")
                                    };

                                    if (await dialog.ShowAsync().ConfigureAwait(true) == ContentDialogResult.Primary)
                                    {
                                        _ = await Launcher.LaunchFolderAsync(FileControlInstance.CurrentFolder);
                                    }
                                }
                                else if (IsSpaceError)
                                {
                                    QueueContentDialog QueueContenDialog = new QueueContentDialog
                                    {
                                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                        Content = Globalization.GetString("QueueDialog_MoveFail_FreeSpaceCritical_Content"),
                                        CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                                    };
                                    _ = await QueueContenDialog.ShowAsync().ConfigureAwait(true);
                                }
                                else if (IsCaptured)
                                {
                                    QueueContentDialog dialog = new QueueContentDialog
                                    {
                                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                        Content = Globalization.GetString("QueueDialog_MoveFail_Captured_Content"),
                                        CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                                    };

                                    _ = await dialog.ShowAsync().ConfigureAwait(true);
                                }

                                break;
                            }
                    }
                }
                finally
                {
                    DragItemList.Clear();
                    await LoadingActivation(false).ConfigureAwait(true);
                    e.Handled = true;
                    Deferral.Complete();
                    _ = Interlocked.Exchange(ref ViewDropLock, 0);
                }
            }

        }

        private void ViewControl_Holding(object sender, Windows.UI.Xaml.Input.HoldingRoutedEventArgs e)
        {
            if (e.HoldingState == Windows.UI.Input.HoldingState.Started)
            {
                if (ItemPresenter is GridView)
                {
                    if ((e.OriginalSource as FrameworkElement)?.DataContext is FileSystemStorageItem Context)
                    {
                        if (SelectedItems.Count <= 1 || !SelectedItems.Contains(Context))
                        {
                            ItemPresenter.ContextFlyout = Context.StorageType == StorageItemTypes.Folder ? FolderFlyout : FileFlyout;
                            SelectedItem = Context;
                        }
                        else
                        {
                            ItemPresenter.ContextFlyout = MixedFlyout;
                        }
                    }
                    else
                    {
                        SelectedItem = null;
                        ItemPresenter.ContextFlyout = EmptyFlyout;
                    }
                }
                else
                {
                    if (e.OriginalSource is ListViewItemPresenter || (e.OriginalSource as FrameworkElement).Name == "EmptyTextblock")
                    {
                        SelectedItem = null;
                        ItemPresenter.ContextFlyout = EmptyFlyout;
                    }
                    else
                    {
                        if ((e.OriginalSource as FrameworkElement)?.DataContext is FileSystemStorageItem Context)
                        {
                            if (SelectedItems.Count <= 1 || !SelectedItems.Contains(Context))
                            {
                                ItemPresenter.ContextFlyout = Context.StorageType == StorageItemTypes.Folder ? FolderFlyout : FileFlyout;
                                SelectedItem = Context;
                            }
                            else
                            {
                                ItemPresenter.ContextFlyout = MixedFlyout;
                            }
                        }
                        else
                        {
                            SelectedItem = null;
                            ItemPresenter.ContextFlyout = EmptyFlyout;
                        }
                    }
                }
            }
        }

        public async void MixZip_Click(object sender, RoutedEventArgs e)
        {
            Restore();

            foreach (FileSystemStorageItem Item in SelectedItems)
            {
                if (Item.StorageType == StorageItemTypes.Folder)
                {
                    StorageFolder Folder = (await Item.GetStorageItem().ConfigureAwait(true)) as StorageFolder;

                    if (!await Folder.CheckExist().ConfigureAwait(true))
                    {
                        QueueContentDialog Dialog = new QueueContentDialog
                        {
                            Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                            Content = Globalization.GetString("QueueDialog_LocateFolderFailure_Content"),
                            CloseButtonText = Globalization.GetString("Common_Dialog_RefreshButton")
                        };
                        _ = await Dialog.ShowAsync().ConfigureAwait(true);

                        if (SettingControl.IsDetachTreeViewAndPresenter)
                        {
                            await FileControlInstance.DisplayItemsInFolder(FileControlInstance.CurrentFolder, true).ConfigureAwait(false);
                        }
                        else
                        {
                            await FileControlInstance.DisplayItemsInFolder(FileControlInstance.CurrentNode, true).ConfigureAwait(false);
                        }
                        return;
                    }
                }
                else
                {
                    StorageFile File = (await Item.GetStorageItem().ConfigureAwait(true)) as StorageFile;

                    if (!await File.CheckExist().ConfigureAwait(true))
                    {
                        QueueContentDialog Dialog = new QueueContentDialog
                        {
                            Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                            Content = Globalization.GetString("QueueDialog_LocateFileFailure_Content"),
                            CloseButtonText = Globalization.GetString("Common_Dialog_RefreshButton")
                        };
                        _ = await Dialog.ShowAsync().ConfigureAwait(true);

                        if (SettingControl.IsDetachTreeViewAndPresenter)
                        {
                            await FileControlInstance.DisplayItemsInFolder(FileControlInstance.CurrentFolder, true).ConfigureAwait(false);
                        }
                        else
                        {
                            await FileControlInstance.DisplayItemsInFolder(FileControlInstance.CurrentNode, true).ConfigureAwait(false);
                        }
                        return;
                    }
                }
            }

            bool IsCompress = false;
            if (SelectedItems.All((Item) => Item.StorageType == StorageItemTypes.File))
            {
                if (SelectedItems.All((Item) => Item.Type == ".zip"))
                {
                    IsCompress = false;
                }
                else if (SelectedItems.All((Item) => Item.Type != ".zip"))
                {
                    IsCompress = true;
                }
                else
                {
                    return;
                }
            }
            else if (SelectedItems.All((Item) => Item.StorageType == StorageItemTypes.Folder))
            {
                IsCompress = true;
            }
            else
            {
                if (SelectedItems.Where((It) => It.StorageType == StorageItemTypes.File).All((Item) => Item.Type != ".zip"))
                {
                    IsCompress = true;
                }
                else
                {
                    return;
                }
            }

            if (IsCompress)
            {
                ZipDialog dialog = new ZipDialog(true, Globalization.GetString("Zip_Admin_Name_Text"));

                if ((await dialog.ShowAsync().ConfigureAwait(true)) == ContentDialogResult.Primary)
                {
                    await LoadingActivation(true, Globalization.GetString("Progress_Tip_Compressing")).ConfigureAwait(true);

                    if (dialog.IsCryptionEnable)
                    {
                        await CreateZipAsync(SelectedItems, dialog.FileName, (int)dialog.Level, true, dialog.Key, dialog.Password).ConfigureAwait(true);
                    }
                    else
                    {
                        await CreateZipAsync(SelectedItems, dialog.FileName, (int)dialog.Level).ConfigureAwait(true);
                    }

                    await LoadingActivation(false).ConfigureAwait(true);
                }
            }
            else
            {
                foreach (FileSystemStorageItem Item in SelectedItems)
                {
                    StorageFile File = (await Item.GetStorageItem().ConfigureAwait(true)) as StorageFile;

                    if ((await UnZipAsync(File).ConfigureAwait(true)) is StorageFolder NewFolder)
                    {
                        if (FileCollection.Where((Item) => Item.StorageType == StorageItemTypes.Folder).All((Folder) => Folder.Name != NewFolder.Name))
                        {
                            FileCollection.Insert(0, new FileSystemStorageItem(NewFolder, await NewFolder.GetModifiedTimeAsync().ConfigureAwait(true)));

                            if (!SettingControl.IsDetachTreeViewAndPresenter)
                            {
                                await FileControlInstance.FolderTree.RootNodes[0].UpdateAllSubNode().ConfigureAwait(true);
                            }
                        }
                    }
                }
            }

        }
    }
}

