using ComputerVision;
using ICSharpCode.SharpZipLib.Zip;
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

        private CancellationTokenSource HashCancellation;

        public StorageAreaWatcher AreaWatcher { get; private set; }

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

            AreaWatcher = new StorageAreaWatcher(FileCollection);

            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            ZipStrings.CodePage = 936;

            Application.Current.Suspending += Current_Suspending;
            Application.Current.Resuming += Current_Resuming;

            Loaded += FilePresenter_Loaded;
            Unloaded += FilePresenter_Unloaded;

            PointerHoverTimer.Tick += Timer_Tick;
        }

        private void Current_Resuming(object sender, object e)
        {
            AreaWatcher.SetCurrentLocation(FileControlInstance.CurrentFolder);
        }

        private void Current_Suspending(object sender, SuspendingEventArgs e)
        {
            WiFiProvider?.Dispose();
            AreaWatcher.SetCurrentLocation(Folder: null);
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
                            await FullTrustExcutorController.Current.ViewWithQuicklookAsync(SelectedItem.Path).ConfigureAwait(false);
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
                    case VirtualKey.Z when CtrlState.HasFlag(CoreVirtualKeyStates.Down) && TabViewContainer.CopyAndMoveRecord.Count > 0:
                        {
                            await Ctrl_Z_Click().ConfigureAwait(false);
                            break;
                        }
                }
            }
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

            foreach (string Record in TabViewContainer.CopyAndMoveRecord)
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
                                                    await FullTrustExcutorController.Current.MoveAsync(File, OriginFolder).ConfigureAwait(true);
                                                }
                                                else
                                                {
                                                    IsItemNotFound = true;
                                                }

                                                break;
                                            }
                                        case "Folder":
                                            {
                                                if ((await FileControlInstance.CurrentFolder.TryGetItemAsync(Path.GetFileName(SplitGroup[3]))) is StorageFolder Folder)
                                                {
                                                    await FullTrustExcutorController.Current.MoveAsync(Folder, OriginFolder).ConfigureAwait(true);

                                                    if (!SettingControl.IsDetachTreeViewAndPresenter && FileControlInstance.CurrentNode.IsExpanded)
                                                    {
                                                        await FileControlInstance.CurrentNode.UpdateAllSubNode().ConfigureAwait(true);
                                                    }
                                                }
                                                else
                                                {
                                                    IsItemNotFound = true;
                                                }

                                                break;
                                            }
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
                                                    await FullTrustExcutorController.Current.MoveAsync(File, FileControlInstance.CurrentFolder).ConfigureAwait(true);
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

                                                await FullTrustExcutorController.Current.MoveAsync(Folder, FileControlInstance.CurrentFolder).ConfigureAwait(true);

                                                if (!SettingControl.IsDetachTreeViewAndPresenter && FileControlInstance.CurrentNode.IsExpanded)
                                                {
                                                    await FileControlInstance.CurrentNode.UpdateAllSubNode().ConfigureAwait(true);
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

                                                await FullTrustExcutorController.Current.MoveAsync(File, OriginFolder).ConfigureAwait(true);

                                                break;
                                            }
                                        case "Folder":
                                            {
                                                StorageFolder Folder = await StorageFolder.GetFolderFromPathAsync(SplitGroup[3]);

                                                await FullTrustExcutorController.Current.MoveAsync(Folder, OriginFolder).ConfigureAwait(true);

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
                                                    await FullTrustExcutorController.Current.DeleteAsync(File, true).ConfigureAwait(true);
                                                }

                                                break;
                                            }
                                        case "Folder":
                                            {
                                                await FullTrustExcutorController.Current.DeleteAsync(SplitGroup[3], true).ConfigureAwait(true);

                                                if (!SettingControl.IsDetachTreeViewAndPresenter && FileControlInstance.CurrentNode.IsExpanded)
                                                {
                                                    await FileControlInstance.CurrentNode.UpdateAllSubNode().ConfigureAwait(true);
                                                }

                                                break;
                                            }
                                    }
                                }
                                else
                                {
                                    await FullTrustExcutorController.Current.DeleteAsync(SplitGroup[3], true).ConfigureAwait(true);
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

        public async void Copy_Click(object sender, RoutedEventArgs e)
        {
            Restore();

            Clipboard.Clear();

            List<IStorageItem> TempList = new List<IStorageItem>(SelectedItems.Count);
            foreach (var Item in SelectedItems)
            {
                TempList.Add(await Item.GetStorageItem().ConfigureAwait(true));
            }

            DataPackage Package = new DataPackage
            {
                RequestedOperation = DataPackageOperation.Copy
            };
            Package.SetStorageItems(TempList, false);

            Clipboard.SetContent(Package);
        }

        public async void Paste_Click(object sender, RoutedEventArgs e)
        {
            Restore();

            DataPackageView Package = Clipboard.GetContent();

            if (Package.Contains(StandardDataFormats.StorageItems))
            {
                IReadOnlyList<IStorageItem> ItemList = await Package.GetStorageItemsAsync();

                if (Package.RequestedOperation.HasFlag(DataPackageOperation.Move))
                {
                    await LoadingActivation(true, Globalization.GetString("Progress_Tip_Moving")).ConfigureAwait(true);

                    if (ItemList.Any((Item) => Path.GetDirectoryName(Item.Path) == FileControlInstance.CurrentFolder.Path))
                    {
                        goto FLAG;
                    }

                    bool IsItemNotFound = false;
                    bool IsUnauthorized = false;
                    bool IsCaptured = false;

                    TabViewContainer.CopyAndMoveRecord.Clear();

                    try
                    {
                        await FullTrustExcutorController.Current.MoveAsync(ItemList, FileControlInstance.CurrentFolder).ConfigureAwait(true);

                        if (!SettingControl.IsDetachTreeViewAndPresenter && FileControlInstance.CurrentNode.IsExpanded)
                        {
                            await FileControlInstance.CurrentNode.UpdateAllSubNode().ConfigureAwait(true);
                        }

                        foreach (IStorageItem StorageItem in ItemList)
                        {
                            if (StorageItem is StorageFile File)
                            {
                                TabViewContainer.CopyAndMoveRecord.Add($"{StorageItem.Path}||Move||File||{Path.Combine(FileControlInstance.CurrentFolder.Path, StorageItem.Name)}");
                            }
                            else if (StorageItem is StorageFolder Folder)
                            {
                                TabViewContainer.CopyAndMoveRecord.Add($"{StorageItem.Path}||Move||Folder||{Path.Combine(FileControlInstance.CurrentFolder.Path, StorageItem.Name)}");
                            }
                        }
                    }
                    catch (FileNotFoundException)
                    {
                        IsItemNotFound = true;
                    }
                    catch (FileCaputureException)
                    {
                        IsCaptured = true;
                    }
                    catch (Exception)
                    {
                        IsUnauthorized = true;
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
                    else if (IsCaptured)
                    {
                        QueueContentDialog dialog = new QueueContentDialog
                        {
                            Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                            Content = Globalization.GetString("QueueDialog_Item_Captured_Content"),
                            CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                        };

                        _ = await dialog.ShowAsync().ConfigureAwait(true);
                    }
                }
                else if (Package.RequestedOperation.HasFlag(DataPackageOperation.Copy))
                {
                    await LoadingActivation(true, Globalization.GetString("Progress_Tip_Copying")).ConfigureAwait(true);

                    bool IsItemNotFound = false;
                    bool IsUnauthorized = false;

                    TabViewContainer.CopyAndMoveRecord.Clear();

                    try
                    {
                        await FullTrustExcutorController.Current.CopyAsync(ItemList, FileControlInstance.CurrentFolder).ConfigureAwait(true);

                        if (!SettingControl.IsDetachTreeViewAndPresenter && FileControlInstance.CurrentNode.IsExpanded)
                        {
                            await FileControlInstance.CurrentNode.UpdateAllSubNode().ConfigureAwait(true);
                        }

                        foreach (IStorageItem StorageItem in ItemList)
                        {
                            if (StorageItem is StorageFile File)
                            {
                                TabViewContainer.CopyAndMoveRecord.Add($"{StorageItem.Path}||Copy||File||{Path.Combine(FileControlInstance.CurrentFolder.Path, StorageItem.Name)}");
                            }
                            else if (StorageItem is StorageFolder Folder)
                            {
                                TabViewContainer.CopyAndMoveRecord.Add($"{StorageItem.Path}||Copy||Folder||{Path.Combine(FileControlInstance.CurrentFolder.Path, StorageItem.Name)}");
                            }
                        }
                    }
                    catch (FileNotFoundException)
                    {
                        IsItemNotFound = true;
                    }
                    catch (Exception)
                    {
                        IsUnauthorized = true;
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
                }
            }

        FLAG:
            await LoadingActivation(false).ConfigureAwait(false);
        }

        public async void Cut_Click(object sender, RoutedEventArgs e)
        {
            Restore();

            Clipboard.Clear();

            List<IStorageItem> TempList = new List<IStorageItem>(SelectedItems.Count);
            foreach (var Item in SelectedItems)
            {
                TempList.Add(await Item.GetStorageItem().ConfigureAwait(true));
            }

            DataPackage Package = new DataPackage
            {
                RequestedOperation = DataPackageOperation.Move
            };
            Package.SetStorageItems(TempList, false);

            Clipboard.SetContent(Package);
        }

        public async void Delete_Click(object sender, RoutedEventArgs e)
        {
            Restore();

            DeleteDialog QueueContenDialog = new DeleteDialog(Globalization.GetString("QueueDialog_DeleteFiles_Content"));

            if ((await QueueContenDialog.ShowAsync().ConfigureAwait(true)) == ContentDialogResult.Primary)
            {
                await LoadingActivation(true, Globalization.GetString("Progress_Tip_Deleting")).ConfigureAwait(true);

                bool IsItemNotFound = false;
                bool IsUnauthorized = false;
                bool IsCaptured = false;

                try
                {
                    await FullTrustExcutorController.Current.DeleteAsync(SelectedItems.Select((Item) => Item.Path), QueueContenDialog.IsPermanentDelete).ConfigureAwait(true);

                    if (!SettingControl.IsDetachTreeViewAndPresenter)
                    {
                        await FileControlInstance.CurrentNode.UpdateAllSubNode().ConfigureAwait(true);
                    }
                }
                catch (FileNotFoundException)
                {
                    IsItemNotFound = true;
                }
                catch (FileCaputureException)
                {
                    IsCaptured = true;
                }
                catch (Exception)
                {
                    IsUnauthorized = true;
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
                        Content = Globalization.GetString("QueueDialog_UnauthorizedDelete_Content"),
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
                        Content = Globalization.GetString("QueueDialog_Item_Captured_Content"),
                        CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                    };

                    _ = await dialog.ShowAsync().ConfigureAwait(true);
                }

                await LoadingActivation(false).ConfigureAwait(false);
            }
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

                    RenameDialog dialog = new RenameDialog(File);
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
                            await (await RenameItem.GetStorageItem().ConfigureAwait(true)).RenameAsync(dialog.DesireName);
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

                    RenameDialog dialog = new RenameDialog(Folder);
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
                                await (await RenameItem.GetStorageItem().ConfigureAwait(true)).RenameAsync(dialog.DesireName);
                            }
                            else
                            {
                                if (FileControlInstance.CurrentNode.IsExpanded)
                                {
                                    IList<TreeViewNode> ChildCollection = FileControlInstance.CurrentNode.Children;
                                    TreeViewNode TargetNode = FileControlInstance.CurrentNode.Children.Where((Fold) => (Fold.Content as StorageFolder).Name == RenameItem.Name).FirstOrDefault();
                                    int index = FileControlInstance.CurrentNode.Children.IndexOf(TargetNode);

                                    await (await RenameItem.GetStorageItem().ConfigureAwait(true)).RenameAsync(dialog.DesireName);

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
                                    await (await RenameItem.GetStorageItem().ConfigureAwait(true)).RenameAsync(dialog.DesireName);
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
                    if (e.OriginalSource is ListViewItemPresenter || (e.OriginalSource as FrameworkElement)?.Name == "EmptyTextblock")
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
                if ((await UnZipAsync(Item).ConfigureAwait(true)) != null)
                {
                    if (!SettingControl.IsDetachTreeViewAndPresenter && FileControlInstance.CurrentNode.IsExpanded)
                    {
                        await FileControlInstance.CurrentNode.UpdateAllSubNode().ConfigureAwait(true);
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

            if ((await SelectedItem.GetStorageItem().ConfigureAwait(true)) is StorageFile Item)
            {
                if (QRTeachTip.IsOpen)
                {
                    QRTeachTip.IsOpen = false;
                }

                await Task.Run(() =>
                {
                    SpinWait.SpinUntil(() => WiFiProvider == null);
                }).ConfigureAwait(true);

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
                using (SoftwareBitmap TransferImage = ComputerVisionProvider.ExtendImageBorder(PreTransImage, Colors.White, 0, 75, 75, 0))
                {
                    SoftwareBitmapSource Source = new SoftwareBitmapSource();
                    QRImage.Source = Source;
                    await Source.SetBitmapAsync(TransferImage);
                }

                await Task.Delay(500).ConfigureAwait(true);

                QRTeachTip.Target = ItemPresenter.ContainerFromItem(SelectedItem) as FrameworkElement;
                QRTeachTip.IsOpen = true;

                await WiFiProvider.StartToListenRequest().ConfigureAwait(false);
            }
            else
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
            }
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

                if (!SettingControl.IsDetachTreeViewAndPresenter && FileControlInstance.CurrentNode.IsExpanded)
                {
                    await FileControlInstance.CurrentNode.UpdateAllSubNode().ConfigureAwait(true);
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
            try
            {
                if (Clipboard.GetContent().Contains(StandardDataFormats.StorageItems))
                {
                    Paste.IsEnabled = true;
                }
                else
                {
                    Paste.IsEnabled = false;
                }
            }
            catch
            {

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
                                        await FullTrustExcutorController.Current.RunAsync(Path, TabTarget.Path).ConfigureAwait(true);
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
                                                    Title = Globalization.GetString("Common_Dialog_TipTitle"),
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
                                                Title = Globalization.GetString("Common_Dialog_TipTitle"),
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
                                            await FullTrustExcutorController.Current.RunAsAdministratorAsync(TabTarget.Path).ConfigureAwait(false);
                                        }
                                        else
                                        {
                                            await FullTrustExcutorController.Current.RunAsync(TabTarget.Path).ConfigureAwait(false);
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
                                                    Title = Globalization.GetString("Common_Dialog_TipTitle"),
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

                        if (SettingControl.IsDetachTreeViewAndPresenter)
                        {
                            await FileControlInstance.DisplayItemsInFolder(Folder).ConfigureAwait(true);
                        }
                        else
                        {
                            if (FileControlInstance.CurrentNode == null)
                            {
                                FileControlInstance.CurrentNode = FileControlInstance.FolderTree.RootNodes[0];
                            }

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
                            Title = Globalization.GetString("Common_Dialog_TipTitle"),
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

                List<FileSystemStorageItem> SortResult = FileCollection.SortList(SortTarget.Name, SortDirection.Descending);
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

                List<FileSystemStorageItem> SortResult = FileCollection.SortList(SortTarget.Name, SortDirection.Ascending);
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

                List<FileSystemStorageItem> SortResult = FileCollection.SortList(SortTarget.ModifiedTime, SortDirection.Descending);
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

                List<FileSystemStorageItem> SortResult = FileCollection.SortList(SortTarget.ModifiedTime, SortDirection.Ascending);
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

                List<FileSystemStorageItem> SortResult = FileCollection.SortList(SortTarget.Type, SortDirection.Descending);
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

                List<FileSystemStorageItem> SortResult = FileCollection.SortList(SortTarget.Type, SortDirection.Ascending);
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

                List<FileSystemStorageItem> SortResult = FileCollection.SortList(SortTarget.Size, SortDirection.Descending);
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

                List<FileSystemStorageItem> SortResult = FileCollection.SortList(SortTarget.Size, SortDirection.Ascending);
                FileCollection.Clear();

                foreach (FileSystemStorageItem Item in SortResult)
                {
                    FileCollection.Add(Item);
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
                if (e.DataView.Contains(StandardDataFormats.StorageItems))
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

                        TabViewContainer.CopyAndMoveRecord.Clear();

                        switch (e.AcceptedOperation)
                        {
                            case DataPackageOperation.Copy:
                                {
                                    bool IsItemNotFound = false;
                                    bool IsUnauthorized = false;

                                    await LoadingActivation(true, Globalization.GetString("Progress_Tip_Copying")).ConfigureAwait(true);

                                    try
                                    {
                                        await FullTrustExcutorController.Current.CopyAsync(DragItemList, FileControlInstance.CurrentFolder).ConfigureAwait(true);

                                        if (!SettingControl.IsDetachTreeViewAndPresenter)
                                        {
                                            await FileControlInstance.FolderTree.RootNodes[0].UpdateAllSubNode().ConfigureAwait(true);
                                        }

                                        foreach (IStorageItem Item in DragItemList)
                                        {
                                            if (Item is StorageFile File)
                                            {
                                                TabViewContainer.CopyAndMoveRecord.Add($"{File.Path}||Copy||File||{Path.Combine(TargetFolder.Path, File.Name)}");
                                            }
                                            else if (Item is StorageFolder Folder)
                                            {
                                                TabViewContainer.CopyAndMoveRecord.Add($"{Folder.Path}||Copy||Folder||{Path.Combine(TargetFolder.Path, Folder.Name)}");
                                            }
                                        }
                                    }
                                    catch (FileNotFoundException)
                                    {
                                        IsItemNotFound = true;
                                    }
                                    catch (Exception)
                                    {
                                        IsUnauthorized = true;
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

                                    break;
                                }
                            case DataPackageOperation.Move:
                                {
                                    await LoadingActivation(true, Globalization.GetString("Progress_Tip_Moving")).ConfigureAwait(true);

                                    bool IsItemNotFound = false;
                                    bool IsUnauthorized = false;
                                    bool IsCaptured = false;

                                    try
                                    {
                                        await FullTrustExcutorController.Current.MoveAsync(DragItemList, TargetFolder).ConfigureAwait(true);

                                        if (!SettingControl.IsDetachTreeViewAndPresenter)
                                        {
                                            await FileControlInstance.FolderTree.RootNodes[0].UpdateAllSubNode().ConfigureAwait(true);
                                        }

                                        foreach (IStorageItem Item in DragItemList)
                                        {
                                            if (Item.IsOfType(StorageItemTypes.File))
                                            {
                                                TabViewContainer.CopyAndMoveRecord.Add($"{Item.Path}||Move||File||{Path.Combine(TargetFolder.Path, Item.Name)}");
                                            }
                                            else
                                            {
                                                TabViewContainer.CopyAndMoveRecord.Add($"{Item.Path}||Move||Folder||{Path.Combine(TargetFolder.Path, Item.Name)}");
                                            }
                                        }
                                    }
                                    catch (FileNotFoundException)
                                    {
                                        IsItemNotFound = true;
                                    }
                                    catch (FileCaputureException)
                                    {
                                        IsCaptured = true;
                                    }
                                    catch (Exception)
                                    {
                                        IsUnauthorized = true;
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
                                    else if (IsCaptured)
                                    {
                                        QueueContentDialog dialog = new QueueContentDialog
                                        {
                                            Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                            Content = Globalization.GetString("QueueDialog_Item_Captured_Content"),
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
                        e.DragUIOverride.Caption = $"{Globalization.GetString("Drag_Tip_CopyTo")} {Item.Name}";
                    }
                    else
                    {
                        e.AcceptedOperation = DataPackageOperation.Move;
                        e.DragUIOverride.Caption = $"{Globalization.GetString("Drag_Tip_MoveTo")} {Item.Name}";
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
                if (e.DataView.Contains(StandardDataFormats.StorageItems))
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

                        TabViewContainer.CopyAndMoveRecord.Clear();

                        switch (e.AcceptedOperation)
                        {
                            case DataPackageOperation.Copy:
                                {
                                    bool IsItemNotFound = false;
                                    bool IsUnauthorized = false;

                                    await LoadingActivation(true, Globalization.GetString("Progress_Tip_Copying")).ConfigureAwait(true);

                                    try
                                    {
                                        await FullTrustExcutorController.Current.CopyAsync(DragItemList, TargetFolder).ConfigureAwait(true);

                                        if (!SettingControl.IsDetachTreeViewAndPresenter && FileControlInstance.CurrentNode.IsExpanded)
                                        {
                                            await FileControlInstance.CurrentNode.UpdateAllSubNode().ConfigureAwait(true);
                                        }

                                        foreach (IStorageItem Item in DragItemList)
                                        {
                                            if (Item.IsOfType(StorageItemTypes.File))
                                            {
                                                TabViewContainer.CopyAndMoveRecord.Add($"{Item.Path}||Copy||File||{Path.Combine(TargetFolder.Path, Item.Name)}");
                                            }
                                            else
                                            {
                                                TabViewContainer.CopyAndMoveRecord.Add($"{Item.Path}||Copy||Folder||{Path.Combine(TargetFolder.Path, Item.Name)}");
                                            }
                                        }
                                    }
                                    catch (FileNotFoundException)
                                    {
                                        IsItemNotFound = true;
                                    }
                                    catch (Exception)
                                    {
                                        IsUnauthorized = true;
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

                                    break;
                                }
                            case DataPackageOperation.Move:
                                {
                                    await LoadingActivation(true, Globalization.GetString("Progress_Tip_Moving")).ConfigureAwait(true);

                                    bool IsItemNotFound = false;
                                    bool IsUnauthorized = false;
                                    bool IsCaptured = false;

                                    try
                                    {
                                        await FullTrustExcutorController.Current.MoveAsync(DragItemList, TargetFolder).ConfigureAwait(true);

                                        if (!SettingControl.IsDetachTreeViewAndPresenter && FileControlInstance.CurrentNode.IsExpanded)
                                        {
                                            await FileControlInstance.CurrentNode.UpdateAllSubNode().ConfigureAwait(true);
                                        }

                                        foreach (IStorageItem Item in DragItemList)
                                        {
                                            if (Item.IsOfType(StorageItemTypes.File))
                                            {
                                                TabViewContainer.CopyAndMoveRecord.Add($"{Item.Path}||Move||File||{Path.Combine(TargetFolder.Path, Item.Name)}");
                                            }
                                            else
                                            {
                                                TabViewContainer.CopyAndMoveRecord.Add($"{Item.Path}||Move||Folder||{Path.Combine(TargetFolder.Path, Item.Name)}");
                                            }
                                        }
                                    }
                                    catch (FileCaputureException)
                                    {
                                        IsCaptured = true;
                                    }
                                    catch (FileNotFoundException)
                                    {
                                        IsItemNotFound = true;
                                    }
                                    catch (Exception)
                                    {
                                        IsUnauthorized = true;
                                    }

                                    if (!SettingControl.IsDetachTreeViewAndPresenter && DragItemList.Any((Item) => Item.IsOfType(StorageItemTypes.Folder)))
                                    {
                                        if (TabViewContainer.ThisPage.TabViewControl.TabItems.Select((Tab) => ((Tab as Microsoft.UI.Xaml.Controls.TabViewItem)?.Content as Frame)?.Content as FileControl).Where((Control) => Control != null).FirstOrDefault((Control) => Control.CurrentFolder.Path == Path.GetDirectoryName(DragItemList[0].Path)) is FileControl Control)
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
                                    else if (IsCaptured)
                                    {
                                        QueueContentDialog dialog = new QueueContentDialog
                                        {
                                            Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                            Content = Globalization.GetString("QueueDialog_Item_Captured_Content"),
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
                    if (e.OriginalSource is ListViewItemPresenter || (e.OriginalSource as FrameworkElement)?.Name == "EmptyTextblock")
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

                    if ((await UnZipAsync(File).ConfigureAwait(true)) != null)
                    {
                        if (!SettingControl.IsDetachTreeViewAndPresenter && FileControlInstance.CurrentNode.IsExpanded)
                        {
                            await FileControlInstance.CurrentNode.UpdateAllSubNode().ConfigureAwait(true);
                        }
                    }
                }
            }

        }

        public async void TryUnlock_Click(object sender, RoutedEventArgs e)
        {
            Restore();

            if (SelectedItem is FileSystemStorageItem Item && Item.StorageType == StorageItemTypes.File)
            {
                try
                {
                    await LoadingActivation(true, Globalization.GetString("Progress_Tip_Unlock")).ConfigureAwait(true);

                    if (await FullTrustExcutorController.Current.TryUnlockFileOccupy(Item.Path).ConfigureAwait(true))
                    {
                        QueueContentDialog Dialog = new QueueContentDialog
                        {
                            Title = Globalization.GetString("Common_Dialog_TipTitle"),
                            Content = Globalization.GetString("QueueDialog_Unlock_Success_Content"),
                            CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                        };
                        _ = await Dialog.ShowAsync().ConfigureAwait(true);
                    }
                    else
                    {
                        QueueContentDialog Dialog = new QueueContentDialog
                        {
                            Title = Globalization.GetString("Common_Dialog_TipTitle"),
                            Content = Globalization.GetString("QueueDialog_Unlock_Failure_Content"),
                            CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                        };
                        _ = await Dialog.ShowAsync().ConfigureAwait(true);
                    }
                }
                catch (FileNotFoundException)
                {
                    QueueContentDialog Dialog = new QueueContentDialog
                    {
                        Title = Globalization.GetString("Common_Dialog_TipTitle"),
                        Content = Globalization.GetString("QueueDialog_Unlock_FileNotFound_Content"),
                        CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                    };
                    _ = await Dialog.ShowAsync().ConfigureAwait(true);

                }
                catch (UnlockException)
                {
                    QueueContentDialog Dialog = new QueueContentDialog
                    {
                        Title = Globalization.GetString("Common_Dialog_TipTitle"),
                        Content = Globalization.GetString("QueueDialog_Unlock_NoLock_Content"),
                        CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                    };
                    _ = await Dialog.ShowAsync().ConfigureAwait(true);
                }
                catch
                {
                    QueueContentDialog Dialog = new QueueContentDialog
                    {
                        Title = Globalization.GetString("Common_Dialog_TipTitle"),
                        Content = Globalization.GetString("QueueDialog_Unlock_UnexpectedError_Content"),
                        CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                    };
                    _ = await Dialog.ShowAsync().ConfigureAwait(true);
                }
                finally
                {
                    await LoadingActivation(false).ConfigureAwait(false);
                }
            }
        }

        public async void CalculateHash_Click(object sender, RoutedEventArgs e)
        {
            Restore();

            try
            {
                if (HashTeachTip.IsOpen)
                {
                    HashTeachTip.IsOpen = false;
                }

                await Task.Run(() =>
                {
                    SpinWait.SpinUntil(() => HashCancellation == null);
                }).ConfigureAwait(true);

                if ((await SelectedItem.GetStorageItem().ConfigureAwait(true)) is StorageFile Item)
                {
                    Hash_Crc32.IsEnabled = false;
                    Hash_SHA1.IsEnabled = false;
                    Hash_SHA256.IsEnabled = false;
                    Hash_MD5.IsEnabled = false;

                    Hash_Crc32.Text = string.Empty;
                    Hash_SHA1.Text = string.Empty;
                    Hash_SHA256.Text = string.Empty;
                    Hash_MD5.Text = string.Empty;

                    await Task.Delay(500).ConfigureAwait(true);
                    HashTeachTip.Target = ItemPresenter.ContainerFromItem(SelectedItem) as FrameworkElement;
                    HashTeachTip.IsOpen = true;

                    using (HashCancellation = new CancellationTokenSource())
                    {
                        var task1 = Item.ComputeSHA256Hash(HashCancellation.Token);
                        Hash_SHA256.IsEnabled = true;

                        var task2 = Item.ComputeCrc32Hash(HashCancellation.Token);
                        Hash_Crc32.IsEnabled = true;

                        var task4 = Item.ComputeMD5Hash(HashCancellation.Token);
                        Hash_MD5.IsEnabled = true;

                        var task3 = Item.ComputeSHA1Hash(HashCancellation.Token);
                        Hash_SHA1.IsEnabled = true;

                        Hash_MD5.Text = await task4.ConfigureAwait(true);
                        Hash_Crc32.Text = await task2.ConfigureAwait(true);
                        Hash_SHA1.Text = await task3.ConfigureAwait(true);
                        Hash_SHA256.Text = await task1.ConfigureAwait(true);
                    }
                }
                else
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
                }
            }
            catch
            {

            }
            finally
            {
                HashCancellation = null;
            }
        }

        private void Hash_Crc32_Copy_Click(object sender, RoutedEventArgs e)
        {
            DataPackage Package = new DataPackage();
            Package.SetText(Hash_Crc32.Text);
            Clipboard.SetContent(Package);
        }

        private void Hash_SHA1_Copy_Click(object sender, RoutedEventArgs e)
        {
            DataPackage Package = new DataPackage();
            Package.SetText(Hash_SHA1.Text);
            Clipboard.SetContent(Package);
        }

        private void Hash_SHA256_Copy_Click(object sender, RoutedEventArgs e)
        {
            DataPackage Package = new DataPackage();
            Package.SetText(Hash_SHA256.Text);
            Clipboard.SetContent(Package);
        }

        private void Hash_MD5_Copy_Click(object sender, RoutedEventArgs e)
        {
            DataPackage Package = new DataPackage();
            Package.SetText(Hash_MD5.Text);
            Clipboard.SetContent(Package);
        }

        private void HashTeachTip_Closing(Microsoft.UI.Xaml.Controls.TeachingTip sender, Microsoft.UI.Xaml.Controls.TeachingTipClosingEventArgs args)
        {
            HashCancellation?.Cancel();
        }

        public async void OpenInTerminal_Click(object sender, RoutedEventArgs e)
        {
            switch (await Launcher.QueryUriSupportAsync(new Uri("ms-windows-store:"), LaunchQuerySupportType.Uri, "Microsoft.WindowsTerminal_8wekyb3d8bbwe"))
            {
                case LaunchQuerySupportStatus.Available:
                case LaunchQuerySupportStatus.NotSupported:
                    {
                        await FullTrustExcutorController.Current.RunAsync("wt.exe", $"/d {FileControlInstance.CurrentFolder.Path}").ConfigureAwait(false);
                        break;
                    }
                default:
                    {
                        string ExcutePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "WindowsPowerShell\\v1.0\\powershell.exe");
                        await FullTrustExcutorController.Current.RunAsAdministratorAsync(ExcutePath, $"-NoExit -Command \"Set-Location '{FileControlInstance.CurrentFolder.Path}'\"").ConfigureAwait(false);
                        break;
                    }
            }
        }
    }
}

