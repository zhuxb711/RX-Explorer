using ComputerVision;
using Microsoft.Toolkit.Uwp.UI.Controls;
using Microsoft.UI.Xaml.Controls;
using RX_Explorer.Class;
using RX_Explorer.Dialog;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.ApplicationModel.DataTransfer;
using Windows.ApplicationModel.DataTransfer.DragDrop;
using Windows.Data.Xml.Dom;
using Windows.Devices.Input;
using Windows.Devices.Radios;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.System;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.Input;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Media.Imaging;
using ZXing;
using ZXing.QrCode;
using ZXing.QrCode.Internal;
using TreeViewNode = Microsoft.UI.Xaml.Controls.TreeViewNode;

namespace RX_Explorer
{
    public sealed partial class FilePresenter : Page, IDisposable
    {
        public ObservableCollection<FileSystemStorageItemBase> FileCollection { get; private set; }

        private FileControl Container
        {
            get
            {
                if (WeakToFileControl.TryGetTarget(out FileControl Instance))
                {
                    return Instance;
                }
                else
                {
                    return null;
                }
            }
        }

        public List<ValueTuple<string, string>> GoAndBackRecord { get; } = new List<ValueTuple<string, string>>();

        public int RecordIndex { get; set; }

        private StorageAreaWatcher AreaWatcher;

        private WeakReference<FileControl> weakToFileControl;
        public WeakReference<FileControl> WeakToFileControl
        {
            get
            {
                return weakToFileControl;
            }
            set
            {
                if (value != null && value.TryGetTarget(out FileControl Control))
                {
                    AreaWatcher.SetTreeView(Control.FolderTree);
                }

                weakToFileControl = value;
            }
        }

        private SemaphoreSlim EnterLock;

        private readonly PointerEventHandler PointerPressedEventHandler;

        private ListViewBase itemPresenter;

        public ListViewBase ItemPresenter
        {
            get => itemPresenter;
            set
            {
                if (value != itemPresenter)
                {
                    itemPresenter?.RemoveHandler(PointerPressedEvent, PointerPressedEventHandler);
                    itemPresenter = value;
                    itemPresenter.AddHandler(PointerPressedEvent, PointerPressedEventHandler, true);

                    SelectionExtention?.Dispose();
                    SelectionExtention = new ListViewBaseSelectionExtention(itemPresenter, DrawRectangle);

                    if (itemPresenter is GridView)
                    {
                        if (ListViewControl != null)
                        {
                            ListViewControl.Visibility = Visibility.Collapsed;
                            ListViewControl.ItemsSource = null;
                        }

                        GridViewControl.ItemsSource = FileCollection;
                        GridViewControl.Visibility = Visibility.Visible;
                    }
                    else
                    {
                        if (GridViewControl != null)
                        {
                            GridViewControl.Visibility = Visibility.Collapsed;
                            GridViewControl.ItemsSource = null;
                        }

                        ListViewControl.ItemsSource = FileCollection;
                        ListViewControl.Visibility = Visibility.Visible;

                        if (itemPresenter.Header == null)
                        {
                            ListViewHeaderController Header = ListViewHeaderController.Create();
                            Header.Filter.RefreshListRequested += Filter_RefreshListRequested;
                            itemPresenter.Header = Header;
                        }
                    }
                }
            }
        }

        private volatile FileSystemStorageItemBase currentFolder;
        public FileSystemStorageItemBase CurrentFolder
        {
            get
            {
                return currentFolder;
            }
            set
            {
                if (value != null)
                {
                    Container.UpdateAddressButton(value.Path);

                    Container.GlobeSearch.PlaceholderText = $"{Globalization.GetString("SearchBox_PlaceholderText")} {value.Name}";
                    Container.GoParentFolder.IsEnabled = value.Path != Path.GetPathRoot(value.Path);
                    Container.GoBackRecord.IsEnabled = RecordIndex > 0;
                    Container.GoForwardRecord.IsEnabled = RecordIndex < GoAndBackRecord.Count - 1;

                    if (Container.TabItem != null)
                    {
                        Container.TabItem.Header = string.IsNullOrEmpty(value.DisplayName) ? $"<{Globalization.GetString("UnknownText")}>" : value.DisplayName;
                    }

                    AreaWatcher?.StartWatchDirectory(value.Path, SettingControl.IsDisplayHiddenItem);

                    if (this.FindParentOfType<BladeItem>() is BladeItem Parent)
                    {
                        Parent.Header = value.DisplayName;
                    }
                }

                TaskBarController.SetText(value?.DisplayName);

                currentFolder = value;
            }
        }

        private WiFiShareProvider WiFiProvider;
        private ListViewBaseSelectionExtention SelectionExtention;
        private FileSystemStorageItemBase TabTarget;
        private DateTimeOffset LastPressTime;
        private string LastPressString;
        private CancellationTokenSource DelayRenameCancel;
        private CancellationTokenSource DelayEnterCancel;
        private int CurrentViewModeIndex = -1;

        public FileSystemStorageItemBase SelectedItem
        {
            get => ItemPresenter.SelectedItem as FileSystemStorageItemBase;
            set
            {
                ItemPresenter.SelectedItem = value;

                if (value != null)
                {
                    (ItemPresenter.ContainerFromItem(value) as SelectorItem)?.Focus(FocusState.Programmatic);
                }
            }
        }

        public List<FileSystemStorageItemBase> SelectedItems => ItemPresenter.SelectedItems.Select((Item) => Item as FileSystemStorageItemBase).ToList();

        public FilePresenter()
        {
            InitializeComponent();

            FileCollection = new ObservableCollection<FileSystemStorageItemBase>();
            FileCollection.CollectionChanged += FileCollection_CollectionChanged;

            PointerPressedEventHandler = new PointerEventHandler(ViewControl_PointerPressed);

            AreaWatcher = new StorageAreaWatcher(FileCollection);
            EnterLock = new SemaphoreSlim(1, 1);

            Loaded += FilePresenter_Loaded;
            Unloaded += FilePresenter_Unloaded;

            Application.Current.Suspending += Current_Suspending;
            Application.Current.Resuming += Current_Resuming;
            SortCollectionGenerator.Current.SortWayChanged += Current_SortWayChanged;
            ViewModeController.ViewModeChanged += Current_ViewModeChanged;

            TryUnlock.IsEnabled = Package.Current.Id.Architecture == ProcessorArchitecture.X64 || Package.Current.Id.Architecture == ProcessorArchitecture.X86 || Package.Current.Id.Architecture == ProcessorArchitecture.X86OnArm64;
        }

        private async void Current_ViewModeChanged(object sender, ViewModeController.ViewModeChangedEventArgs e)
        {
            if (e.Path == CurrentFolder?.Path && CurrentViewModeIndex != e.Index)
            {
                switch (e.Index)
                {
                    case 0:
                        {
                            ItemPresenter = FindName("GridViewControl") as ListViewBase;

                            GridViewControl.ItemTemplate = GridViewTileDataTemplate;
                            GridViewControl.ItemsPanel = HorizontalGridViewPanel;

                            while (true)
                            {
                                if (GridViewControl.FindChildOfType<ScrollViewer>() is ScrollViewer Scroll)
                                {
                                    Scroll.HorizontalScrollMode = ScrollMode.Disabled;
                                    Scroll.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
                                    Scroll.VerticalScrollMode = ScrollMode.Auto;
                                    Scroll.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
                                    break;
                                }
                                else
                                {
                                    await Task.Delay(200).ConfigureAwait(true);
                                }
                            }

                            break;
                        }
                    case 1:
                        {
                            ItemPresenter = FindName("ListViewControl") as ListViewBase;
                            break;
                        }
                    case 2:
                        {
                            ItemPresenter = FindName("GridViewControl") as ListViewBase;

                            GridViewControl.ItemTemplate = GridViewListDataTemplate;
                            GridViewControl.ItemsPanel = VerticalGridViewPanel;

                            while (true)
                            {
                                if (GridViewControl.FindChildOfType<ScrollViewer>() is ScrollViewer Scroll)
                                {
                                    Scroll.HorizontalScrollMode = ScrollMode.Auto;
                                    Scroll.HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;
                                    Scroll.VerticalScrollMode = ScrollMode.Disabled;
                                    Scroll.VerticalScrollBarVisibility = ScrollBarVisibility.Disabled;
                                    break;
                                }
                                else
                                {
                                    await Task.Delay(200).ConfigureAwait(true);
                                }
                            }

                            break;
                        }
                    case 3:
                        {
                            ItemPresenter = FindName("GridViewControl") as ListViewBase;

                            GridViewControl.ItemTemplate = GridViewLargeImageDataTemplate;
                            GridViewControl.ItemsPanel = HorizontalGridViewPanel;

                            while (true)
                            {
                                if (GridViewControl.FindChildOfType<ScrollViewer>() is ScrollViewer Scroll)
                                {
                                    Scroll.HorizontalScrollMode = ScrollMode.Disabled;
                                    Scroll.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
                                    Scroll.VerticalScrollMode = ScrollMode.Auto;
                                    Scroll.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
                                    break;
                                }
                                else
                                {
                                    await Task.Delay(200).ConfigureAwait(true);
                                }
                            }

                            break;
                        }
                    case 4:
                        {
                            ItemPresenter = FindName("GridViewControl") as ListViewBase;

                            GridViewControl.ItemTemplate = GridViewMediumImageDataTemplate;
                            GridViewControl.ItemsPanel = HorizontalGridViewPanel;

                            while (true)
                            {
                                if (GridViewControl.FindChildOfType<ScrollViewer>() is ScrollViewer Scroll)
                                {
                                    Scroll.HorizontalScrollMode = ScrollMode.Disabled;
                                    Scroll.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
                                    Scroll.VerticalScrollMode = ScrollMode.Auto;
                                    Scroll.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
                                    break;
                                }
                                else
                                {
                                    await Task.Delay(200).ConfigureAwait(true);
                                }
                            }

                            break;
                        }
                    case 5:
                        {
                            ItemPresenter = FindName("GridViewControl") as ListViewBase;

                            GridViewControl.ItemTemplate = GridViewSmallImageDataTemplate;
                            GridViewControl.ItemsPanel = HorizontalGridViewPanel;

                            while (true)
                            {
                                if (GridViewControl.FindChildOfType<ScrollViewer>() is ScrollViewer Scroll)
                                {
                                    Scroll.HorizontalScrollMode = ScrollMode.Disabled;
                                    Scroll.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
                                    Scroll.VerticalScrollMode = ScrollMode.Auto;
                                    Scroll.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
                                    break;
                                }
                                else
                                {
                                    await Task.Delay(200).ConfigureAwait(true);
                                }
                            }

                            break;
                        }
                }

                await SQLite.Current.SetPathConfiguration(new PathConfiguration(CurrentFolder.Path, e.Index)).ConfigureAwait(false);

                CurrentViewModeIndex = e.Index;
            }
        }

        private void Current_SortWayChanged(object sender, string fromPath)
        {
            if (fromPath == CurrentFolder.Path)
            {
                if (ItemPresenter.Header is ListViewHeaderController Controller)
                {
                    Controller.Indicator.SetIndicatorStatus(SortCollectionGenerator.Current.SortTarget, SortCollectionGenerator.Current.SortDirection);
                }

                FileSystemStorageItemBase[] ItemList = SortCollectionGenerator.Current.GetSortedCollection(FileCollection).ToArray();

                FileCollection.Clear();

                foreach (FileSystemStorageItemBase Item in ItemList)
                {
                    FileCollection.Add(Item);
                }
            }
        }

        private async Task DisplayItemsInFolderCore(string FolderPath, bool ForceRefresh = false, bool SkipNavigationRecord = false)
        {
            await EnterLock.WaitAsync().ConfigureAwait(true);

            if (string.IsNullOrWhiteSpace(FolderPath))
            {
                throw new ArgumentNullException(nameof(FolderPath), "Parameter could not be null or empty");
            }

            try
            {
                if (!ForceRefresh)
                {
                    if (FolderPath == CurrentFolder?.Path)
                    {
                        return;
                    }
                }

                if (!SkipNavigationRecord && !ForceRefresh)
                {
                    if (RecordIndex != GoAndBackRecord.Count - 1 && GoAndBackRecord.Count != 0)
                    {
                        GoAndBackRecord.RemoveRange(RecordIndex + 1, GoAndBackRecord.Count - RecordIndex - 1);
                    }

                    if (GoAndBackRecord.Count > 0)
                    {
                        string ParentPath = Path.GetDirectoryName(FolderPath);

                        if (ParentPath == GoAndBackRecord[GoAndBackRecord.Count - 1].Item1)
                        {
                            GoAndBackRecord[GoAndBackRecord.Count - 1] = (ParentPath, FolderPath);
                        }
                        else
                        {
                            GoAndBackRecord[GoAndBackRecord.Count - 1] = (GoAndBackRecord[GoAndBackRecord.Count - 1].Item1, SelectedItems.Count > 1 ? string.Empty : (SelectedItem?.Path ?? string.Empty));
                        }
                    }

                    GoAndBackRecord.Add((FolderPath, string.Empty));

                    RecordIndex = GoAndBackRecord.Count - 1;
                }

                if (await FileSystemStorageItemBase.OpenAsync(FolderPath, ItemFilters.Folder).ConfigureAwait(true) is FileSystemStorageItemBase Item)
                {
                    CurrentFolder = Item;
                }
                else
                {
                    QueueContentDialog dialog = new QueueContentDialog
                    {
                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                        Content = $"{Globalization.GetString("QueueDialog_LocatePathFailure_Content")} \r\"{FolderPath}\"",
                        CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton"),
                    };

                    _ = await dialog.ShowAsync().ConfigureAwait(true);

                    return;
                }

                if (Container.FolderTree.SelectedNode == null && Container.FolderTree.RootNodes.FirstOrDefault((Node) => (Node.Content as TreeViewNodeContent)?.Path == Path.GetPathRoot(FolderPath)) is TreeViewNode RootNode)
                {
                    Container.FolderTree.SelectNodeAndScrollToVertical(RootNode);
                }

                FileCollection.Clear();

                await Container.ViewModeControl.SetCurrentPathAsync(FolderPath).ConfigureAwait(true);

                PathConfiguration Config = await SQLite.Current.GetPathConfiguration(FolderPath).ConfigureAwait(true);

                List<FileSystemStorageItemBase> ChildItems = await CurrentFolder.GetChildrenItemsAsync(SettingControl.IsDisplayHiddenItem).ConfigureAwait(true);

                if (ChildItems.Count > 0)
                {
                    HasFile.Visibility = Visibility.Collapsed;

                    foreach (FileSystemStorageItemBase SubItem in SortCollectionGenerator.Current.GetSortedCollection(ChildItems, Config.SortColumn, Config.SortDirection))
                    {
                        FileCollection.Add(SubItem);
                    }
                }
                else
                {
                    HasFile.Visibility = Visibility.Visible;
                }

                StatusTips.Text = Globalization.GetString("FilePresenterBottomStatusTip_TotalItem").Replace("{ItemNum}", FileCollection.Count.ToString());

                if (ItemPresenter?.Header is ListViewHeaderController Instance)
                {
                    Instance.Filter.SetDataSource(FileCollection);
                    Instance.Indicator.SetIndicatorStatus(Config.SortColumn.GetValueOrDefault(), Config.SortDirection.GetValueOrDefault());
                }
            }
            catch (Exception ex)
            {
                QueueContentDialog Dialog = new QueueContentDialog
                {
                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                    Content = $"{Globalization.GetString("QueueDialog_AccessFolderFailure_Content")} {ex.Message}",
                    CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                };

                _ = await Dialog.ShowAsync().ConfigureAwait(true);
            }
            finally
            {
                EnterLock.Release();
            }
        }

        public Task DisplayItemsInFolder(FileSystemStorageItemBase Folder, bool ForceRefresh = false, bool SkipNavigationRecord = false)
        {
            if (Folder == null)
            {
                throw new ArgumentNullException(nameof(Folder), "Parameter could not be null or empty");
            }

            return DisplayItemsInFolderCore(Folder.Path, ForceRefresh, SkipNavigationRecord);
        }

        public Task DisplayItemsInFolder(string FolderPath, bool ForceRefresh = false, bool SkipNavigationRecord = false)
        {
            return DisplayItemsInFolderCore(FolderPath, ForceRefresh, SkipNavigationRecord);
        }

        private void Presenter_PointerWheelChanged(object sender, PointerRoutedEventArgs e)
        {
            int Delta = e.GetCurrentPoint(null).Properties.MouseWheelDelta;

            if (Window.Current.CoreWindow.GetKeyState(VirtualKey.Control).HasFlag(CoreVirtualKeyStates.Down))
            {
                if (Delta > 0)
                {
                    if (Container.ViewModeControl.ViewModeIndex > 0)
                    {
                        Container.ViewModeControl.ViewModeIndex--;
                    }
                }
                else
                {
                    if (Container.ViewModeControl.ViewModeIndex < ViewModeController.SelectionSource.Length - 1)
                    {
                        Container.ViewModeControl.ViewModeIndex++;
                    }
                }

                e.Handled = true;
            }
        }

        private void Current_Resuming(object sender, object e)
        {
            AreaWatcher.StartWatchDirectory(AreaWatcher.CurrentLocation, SettingControl.IsDisplayHiddenItem);
        }

        private void Current_Suspending(object sender, SuspendingEventArgs e)
        {
            AreaWatcher.StopWatchDirectory();
        }

        private void FilePresenter_Unloaded(object sender, RoutedEventArgs e)
        {
            CoreWindow.GetForCurrentThread().KeyDown -= Window_KeyDown;
            Dispatcher.AcceleratorKeyActivated -= Dispatcher_AcceleratorKeyActivated;
        }

        private void FilePresenter_Loaded(object sender, RoutedEventArgs e)
        {
            CoreWindow.GetForCurrentThread().KeyDown += Window_KeyDown;
            Dispatcher.AcceleratorKeyActivated += Dispatcher_AcceleratorKeyActivated;

            if (this.FindParentOfType<BladeItem>() is BladeItem Parent)
            {
                Parent.Header = CurrentFolder?.DisplayName;
            }
        }

        private void Dispatcher_AcceleratorKeyActivated(CoreDispatcher sender, AcceleratorKeyEventArgs args)
        {
            if (args.KeyStatus.IsMenuKeyDown)
            {
                switch (args.VirtualKey)
                {
                    case VirtualKey.Left:
                        {
                            Container.GoBackRecord_Click(null, null);
                            break;
                        }
                    case VirtualKey.Right:
                        {
                            Container.GoForwardRecord_Click(null, null);
                            break;
                        }
                }
            }
        }

        private async void Window_KeyDown(CoreWindow sender, KeyEventArgs args)
        {
            if (Container.CurrentPresenter == this)
            {
                CoreVirtualKeyStates CtrlState = sender.GetKeyState(VirtualKey.Control);
                CoreVirtualKeyStates ShiftState = sender.GetKeyState(VirtualKey.Shift);

                if (!QueueContentDialog.IsRunningOrWaiting && !Container.BlockKeyboardShortCutInput)
                {
                    args.Handled = true;

                    if (!CtrlState.HasFlag(CoreVirtualKeyStates.Down) && !ShiftState.HasFlag(CoreVirtualKeyStates.Down))
                    {
                        NavigateToStorageItem(args.VirtualKey);
                    }

                    switch (args.VirtualKey)
                    {
                        case VirtualKey.Space when SettingControl.IsQuicklookEnable:
                            {
                                using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableController())
                                {
                                    if (await Exclusive.Controller.CheckIfQuicklookIsAvaliableAsync().ConfigureAwait(true))
                                    {
                                        string ViewPathWithQuicklook;

                                        if (string.IsNullOrEmpty(SelectedItem?.Path))
                                        {
                                            if (!string.IsNullOrEmpty(CurrentFolder?.Path))
                                            {
                                                ViewPathWithQuicklook = CurrentFolder.Path;
                                            }
                                            else
                                            {
                                                break;
                                            }
                                        }
                                        else
                                        {
                                            ViewPathWithQuicklook = SelectedItem.Path;
                                        }

                                        await Exclusive.Controller.ViewWithQuicklookAsync(ViewPathWithQuicklook).ConfigureAwait(false);
                                    }
                                }

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
                        case VirtualKey.Enter when SelectedItems.Count == 1 && SelectedItem is FileSystemStorageItemBase Item:
                            {
                                await EnterSelectedItem(Item).ConfigureAwait(false);
                                break;
                            }
                        case VirtualKey.Back:
                            {
                                Container.GoBackRecord_Click(null, null);
                                break;
                            }
                        case VirtualKey.L when CtrlState.HasFlag(CoreVirtualKeyStates.Down):
                            {
                                Container.AddressBox.Focus(FocusState.Programmatic);
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
                        case VirtualKey.C when CtrlState.HasFlag(CoreVirtualKeyStates.Down) && ShiftState.HasFlag(CoreVirtualKeyStates.Down):
                            {
                                Clipboard.Clear();

                                DataPackage Package = new DataPackage
                                {
                                    RequestedOperation = DataPackageOperation.Copy
                                };

                                Package.SetText(SelectedItem?.Path ?? CurrentFolder?.Path ?? string.Empty);

                                Clipboard.SetContent(Package);
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
                        case VirtualKey.Delete:
                        case VirtualKey.D when CtrlState.HasFlag(CoreVirtualKeyStates.Down):
                            {
                                Delete_Click(null, null);
                                break;
                            }
                        case VirtualKey.F when CtrlState.HasFlag(CoreVirtualKeyStates.Down):
                            {
                                Container.GlobeSearch.Focus(FocusState.Programmatic);
                                break;
                            }
                        case VirtualKey.N when ShiftState.HasFlag(CoreVirtualKeyStates.Down) && CtrlState.HasFlag(CoreVirtualKeyStates.Down):
                            {
                                CreateFolder_Click(null, null);
                                break;
                            }
                        case VirtualKey.Z when CtrlState.HasFlag(CoreVirtualKeyStates.Down) && OperationRecorder.Current.Count > 0:
                            {
                                await Ctrl_Z_Click().ConfigureAwait(false);
                                break;
                            }
                        case VirtualKey.E when ShiftState.HasFlag(CoreVirtualKeyStates.Down) && CurrentFolder != null:
                            {
                                _ = await Launcher.LaunchFolderPathAsync(CurrentFolder.Path);
                                break;
                            }
                        case VirtualKey.T when ShiftState.HasFlag(CoreVirtualKeyStates.Down):
                            {
                                OpenInTerminal_Click(null, null);
                                break;
                            }
                        case VirtualKey.T when CtrlState.HasFlag(CoreVirtualKeyStates.Down):
                            {
                                CloseAllFlyout();

                                if (SelectedItem is FileSystemStorageItemBase Item)
                                {
                                    if (Item.StorageType == StorageItemTypes.Folder)
                                    {
                                        await TabViewContainer.ThisPage.CreateNewTabAsync(Item.Path).ConfigureAwait(true);
                                    }
                                }
                                else
                                {
                                    await TabViewContainer.ThisPage.CreateNewTabAsync().ConfigureAwait(true);
                                }

                                break;
                            }
                        case VirtualKey.Q when CtrlState.HasFlag(CoreVirtualKeyStates.Down):
                            {
                                OpenFolderInNewWindow_Click(null, null);
                                break;
                            }
                        case VirtualKey.Up:
                        case VirtualKey.Down:
                            {
                                if (SelectedItem == null)
                                {
                                    SelectedItem = FileCollection.FirstOrDefault();
                                }

                                break;
                            }
                        case VirtualKey.B when CtrlState.HasFlag(CoreVirtualKeyStates.Down) && SelectedItem != null:
                            {
                                await Container.CreateNewBlade(SelectedItem.Path).ConfigureAwait(true);
                                break;
                            }
                        default:
                            {
                                args.Handled = false;
                                break;
                            }
                    }
                }
            }
        }

        private void NavigateToStorageItem(VirtualKey Key)
        {
            char Input = Convert.ToChar(Key);

            if (char.IsLetterOrDigit(Input))
            {
                string SearchString = Input.ToString();

                try
                {
                    if (LastPressString != SearchString && (DateTimeOffset.Now - LastPressTime).TotalMilliseconds < 1200)
                    {
                        SearchString = LastPressString + SearchString;

                        IEnumerable<FileSystemStorageItemBase> Group = FileCollection.Where((Item) => Item.Name.StartsWith(SearchString, StringComparison.OrdinalIgnoreCase));

                        if (Group.Any() && (SelectedItem == null || !Group.Contains(SelectedItem)))
                        {
                            SelectedItem = Group.FirstOrDefault();
                            ItemPresenter.ScrollIntoView(SelectedItem);
                        }
                    }
                    else
                    {
                        IEnumerable<FileSystemStorageItemBase> Group = FileCollection.Where((Item) => Item.Name.StartsWith(SearchString, StringComparison.OrdinalIgnoreCase));

                        if (Group.Any())
                        {
                            if (SelectedItem != null)
                            {
                                FileSystemStorageItemBase[] ItemArray = Group.ToArray();

                                int NextIndex = Array.IndexOf(ItemArray, SelectedItem);

                                if (NextIndex != -1)
                                {
                                    if (NextIndex < ItemArray.Length - 1)
                                    {
                                        SelectedItem = ItemArray[NextIndex + 1];
                                    }
                                    else
                                    {
                                        SelectedItem = ItemArray.FirstOrDefault();
                                    }
                                }
                                else
                                {
                                    SelectedItem = ItemArray.FirstOrDefault();
                                }
                            }
                            else
                            {
                                SelectedItem = Group.FirstOrDefault();
                            }

                            ItemPresenter.ScrollIntoView(SelectedItem);
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogTracer.Log(ex, $"{nameof(NavigateToStorageItem)} throw an exception");
                }
                finally
                {
                    LastPressString = SearchString;
                    LastPressTime = DateTimeOffset.Now;
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
        private void CloseAllFlyout()
        {
            FileFlyout.Hide();
            FolderFlyout.Hide();
            EmptyFlyout.Hide();
            MixedFlyout.Hide();
            LnkItemFlyout.Hide();
        }

        private async Task Ctrl_Z_Click()
        {
            if (OperationRecorder.Current.Count > 0)
            {
                await Container.LoadingActivation(true, Globalization.GetString("Progress_Tip_Undoing")).ConfigureAwait(true);

                try
                {
                    using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableController())
                    {
                        foreach (string Action in OperationRecorder.Current.Pop())
                        {
                            string[] SplitGroup = Action.Split("||", StringSplitOptions.RemoveEmptyEntries);

                            switch (SplitGroup[1])
                            {
                                case "Move":
                                    {
                                        if (CurrentFolder.Path.Equals(Path.GetDirectoryName(SplitGroup[3]), StringComparison.OrdinalIgnoreCase))
                                        {
                                            if (await FileSystemStorageItemBase.OpenAsync(Path.GetDirectoryName(SplitGroup[0]), ItemFilters.Folder).ConfigureAwait(true) is FileSystemStorageItemBase OriginFolder)
                                            {
                                                switch (SplitGroup[2])
                                                {
                                                    case "File":
                                                        {
                                                            if (Path.GetExtension(SplitGroup[3]) == ".lnk")
                                                            {
                                                                await Exclusive.Controller.MoveAsync(SplitGroup[3], OriginFolder.Path, (s, arg) =>
                                                                {
                                                                    if (Container.ProBar.Value < arg.ProgressPercentage)
                                                                    {
                                                                        Container.ProBar.IsIndeterminate = false;
                                                                        Container.ProBar.Value = arg.ProgressPercentage;
                                                                    }
                                                                }, true).ConfigureAwait(true);
                                                            }
                                                            else if (await FileSystemStorageItemBase.OpenAsync(Path.Combine(CurrentFolder.Path, Path.GetFileName(SplitGroup[3])), ItemFilters.File).ConfigureAwait(true) is FileSystemStorageItemBase Item)
                                                            {
                                                                await Exclusive.Controller.MoveAsync(Item.Path, OriginFolder.Path, (s, arg) =>
                                                                {
                                                                    if (Container.ProBar.Value < arg.ProgressPercentage)
                                                                    {
                                                                        Container.ProBar.IsIndeterminate = false;
                                                                        Container.ProBar.Value = arg.ProgressPercentage;
                                                                    }
                                                                }, true).ConfigureAwait(true);
                                                            }
                                                            else
                                                            {
                                                                throw new FileNotFoundException();
                                                            }

                                                            break;
                                                        }
                                                    case "Folder":
                                                        {
                                                            if (await FileSystemStorageItemBase.OpenAsync(Path.Combine(CurrentFolder.Path, Path.GetFileName(SplitGroup[3])), ItemFilters.Folder).ConfigureAwait(true) is FileSystemStorageItemBase Item)
                                                            {
                                                                await Exclusive.Controller.MoveAsync(Item.Path, OriginFolder.Path, (s, arg) =>
                                                                {
                                                                    if (Container.ProBar.Value < arg.ProgressPercentage)
                                                                    {
                                                                        Container.ProBar.IsIndeterminate = false;
                                                                        Container.ProBar.Value = arg.ProgressPercentage;
                                                                    }
                                                                }, true).ConfigureAwait(true);
                                                            }
                                                            else
                                                            {
                                                                throw new FileNotFoundException();
                                                            }

                                                            break;
                                                        }
                                                }
                                            }
                                            else
                                            {
                                                throw new DirectoryNotFoundException();
                                            }
                                        }
                                        else if (CurrentFolder.Path.Equals(Path.GetDirectoryName(SplitGroup[0]), StringComparison.OrdinalIgnoreCase))
                                        {
                                            switch (SplitGroup[2])
                                            {
                                                case "File":
                                                    {
                                                        if (Path.GetExtension(SplitGroup[3]) == ".lnk")
                                                        {
                                                            await Exclusive.Controller.MoveAsync(SplitGroup[3], CurrentFolder.Path, (s, arg) =>
                                                            {
                                                                if (Container.ProBar.Value < arg.ProgressPercentage)
                                                                {
                                                                    Container.ProBar.IsIndeterminate = false;
                                                                    Container.ProBar.Value = arg.ProgressPercentage;
                                                                }
                                                            }, true).ConfigureAwait(true);
                                                        }
                                                        else
                                                        {
                                                            string TargetFolderPath = Path.GetDirectoryName(SplitGroup[3]);

                                                            if (await FileSystemStorageItemBase.CheckExist(TargetFolderPath).ConfigureAwait(true))
                                                            {
                                                                if (await FileSystemStorageItemBase.OpenAsync(Path.Combine(TargetFolderPath, Path.GetFileName(SplitGroup[3])), ItemFilters.File).ConfigureAwait(true) is FileSystemStorageItemBase Item)
                                                                {
                                                                    await Exclusive.Controller.MoveAsync(Item.Path, CurrentFolder.Path, (s, arg) =>
                                                                    {
                                                                        if (Container.ProBar.Value < arg.ProgressPercentage)
                                                                        {
                                                                            Container.ProBar.IsIndeterminate = false;
                                                                            Container.ProBar.Value = arg.ProgressPercentage;
                                                                        }
                                                                    }, true).ConfigureAwait(true);
                                                                }
                                                                else
                                                                {
                                                                    throw new FileNotFoundException();
                                                                }
                                                            }
                                                            else
                                                            {
                                                                throw new DirectoryNotFoundException();
                                                            }
                                                        }

                                                        break;
                                                    }
                                                case "Folder":
                                                    {
                                                        if (await FileSystemStorageItemBase.OpenAsync(SplitGroup[3], ItemFilters.Folder).ConfigureAwait(true) is FileSystemStorageItemBase Item)
                                                        {
                                                            await Exclusive.Controller.MoveAsync(Item.Path, CurrentFolder.Path, (s, arg) =>
                                                            {
                                                                if (Container.ProBar.Value < arg.ProgressPercentage)
                                                                {
                                                                    Container.ProBar.IsIndeterminate = false;
                                                                    Container.ProBar.Value = arg.ProgressPercentage;
                                                                }
                                                            }, true).ConfigureAwait(true);
                                                        }
                                                        else
                                                        {
                                                            throw new DirectoryNotFoundException();
                                                        }

                                                        break;
                                                    }
                                            }
                                        }
                                        else
                                        {
                                            if (await FileSystemStorageItemBase.OpenAsync(Path.GetDirectoryName(SplitGroup[0]), ItemFilters.Folder).ConfigureAwait(true) is FileSystemStorageItemBase OriginFolder)
                                            {
                                                switch (SplitGroup[2])
                                                {
                                                    case "File":
                                                        {
                                                            if (Path.GetExtension(SplitGroup[3]) == ".lnk")
                                                            {
                                                                await Exclusive.Controller.MoveAsync(SplitGroup[3], OriginFolder.Path, (s, arg) =>
                                                                {
                                                                    if (Container.ProBar.Value < arg.ProgressPercentage)
                                                                    {
                                                                        Container.ProBar.IsIndeterminate = false;
                                                                        Container.ProBar.Value = arg.ProgressPercentage;
                                                                    }
                                                                }, true).ConfigureAwait(true);
                                                            }
                                                            else
                                                            {
                                                                if (await FileSystemStorageItemBase.OpenAsync(SplitGroup[3], ItemFilters.File).ConfigureAwait(true) is FileSystemStorageItemBase File)
                                                                {
                                                                    await Exclusive.Controller.MoveAsync(File.Path, OriginFolder.Path, (s, arg) =>
                                                                    {
                                                                        if (Container.ProBar.Value < arg.ProgressPercentage)
                                                                        {
                                                                            Container.ProBar.IsIndeterminate = false;
                                                                            Container.ProBar.Value = arg.ProgressPercentage;
                                                                        }
                                                                    }, true).ConfigureAwait(true);
                                                                }
                                                                else
                                                                {
                                                                    throw new FileNotFoundException();
                                                                }
                                                            }

                                                            break;
                                                        }
                                                    case "Folder":
                                                        {
                                                            if (await FileSystemStorageItemBase.OpenAsync(SplitGroup[3], ItemFilters.Folder).ConfigureAwait(true) is FileSystemStorageItemBase Folder)
                                                            {
                                                                await Exclusive.Controller.MoveAsync(Folder.Path, OriginFolder.Path, (s, arg) =>
                                                                {
                                                                    if (Container.ProBar.Value < arg.ProgressPercentage)
                                                                    {
                                                                        Container.ProBar.IsIndeterminate = false;
                                                                        Container.ProBar.Value = arg.ProgressPercentage;
                                                                    }
                                                                }, true).ConfigureAwait(true);
                                                            }
                                                            else
                                                            {
                                                                throw new DirectoryNotFoundException();
                                                            }

                                                            if (!SettingControl.IsDetachTreeViewAndPresenter)
                                                            {
                                                                foreach (TreeViewNode RootNode in Container.FolderTree.RootNodes)
                                                                {
                                                                    await RootNode.UpdateAllSubNodeAsync().ConfigureAwait(true);
                                                                }
                                                            }

                                                            break;
                                                        }
                                                }
                                            }
                                            else
                                            {
                                                throw new DirectoryNotFoundException();
                                            }
                                        }

                                        break;
                                    }
                                case "Copy":
                                    {
                                        if (CurrentFolder.Path.Equals(Path.GetDirectoryName(SplitGroup[3]), StringComparison.OrdinalIgnoreCase))
                                        {
                                            switch (SplitGroup[2])
                                            {
                                                case "File":
                                                    {
                                                        if (Path.GetExtension(SplitGroup[3]) == ".lnk")
                                                        {
                                                            await Exclusive.Controller.DeleteAsync(SplitGroup[3], true, (s, arg) =>
                                                            {
                                                                if (Container.ProBar.Value < arg.ProgressPercentage)
                                                                {
                                                                    Container.ProBar.IsIndeterminate = false;
                                                                    Container.ProBar.Value = arg.ProgressPercentage;
                                                                }
                                                            }, true).ConfigureAwait(true);
                                                        }
                                                        else if (await FileSystemStorageItemBase.OpenAsync(Path.Combine(CurrentFolder.Path, Path.GetFileName(SplitGroup[3])), ItemFilters.File).ConfigureAwait(true) is FileSystemStorageItemBase File)
                                                        {
                                                            await Exclusive.Controller.DeleteAsync(File.Path, true, (s, arg) =>
                                                            {
                                                                if (Container.ProBar.Value < arg.ProgressPercentage)
                                                                {
                                                                    Container.ProBar.IsIndeterminate = false;
                                                                    Container.ProBar.Value = arg.ProgressPercentage;
                                                                }
                                                            }, true).ConfigureAwait(true);
                                                        }
                                                        else
                                                        {
                                                            throw new FileNotFoundException();
                                                        }

                                                        break;
                                                    }
                                                case "Folder":
                                                    {
                                                        if (await FileSystemStorageItemBase.OpenAsync(Path.Combine(CurrentFolder.Path, Path.GetFileName(SplitGroup[3])), ItemFilters.Folder).ConfigureAwait(true) is FileSystemStorageItemBase Folder)
                                                        {
                                                            await Exclusive.Controller.DeleteAsync(Folder.Path, true, (s, arg) =>
                                                            {
                                                                if (Container.ProBar.Value < arg.ProgressPercentage)
                                                                {
                                                                    Container.ProBar.IsIndeterminate = false;
                                                                    Container.ProBar.Value = arg.ProgressPercentage;
                                                                }
                                                            }, true).ConfigureAwait(true);
                                                        }
                                                        else
                                                        {
                                                            throw new DirectoryNotFoundException();
                                                        }

                                                        break;
                                                    }
                                            }
                                        }
                                        else
                                        {
                                            await Exclusive.Controller.DeleteAsync(SplitGroup[3], true, (s, arg) =>
                                            {
                                                if (Container.ProBar.Value < arg.ProgressPercentage)
                                                {
                                                    Container.ProBar.IsIndeterminate = false;
                                                    Container.ProBar.Value = arg.ProgressPercentage;
                                                }
                                            }, true).ConfigureAwait(true);

                                            if (!SettingControl.IsDetachTreeViewAndPresenter)
                                            {
                                                foreach (TreeViewNode RootNode in Container.FolderTree.RootNodes)
                                                {
                                                    await RootNode.UpdateAllSubNodeAsync().ConfigureAwait(true);
                                                }
                                            }
                                        }
                                        break;
                                    }
                                case "Delete":
                                    {
                                        if ((await Exclusive.Controller.GetRecycleBinItemsAsync().ConfigureAwait(true)).FirstOrDefault((Item) => Item.OriginPath == SplitGroup[0]) is FileSystemStorageItemBase Item)
                                        {
                                            if (!await Exclusive.Controller.RestoreItemInRecycleBinAsync(Item.Path).ConfigureAwait(true))
                                            {
                                                QueueContentDialog Dialog = new QueueContentDialog
                                                {
                                                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                                    Content = $"{Globalization.GetString("QueueDialog_RecycleBinRestoreError_Content")} {Environment.NewLine}{Item.Name}",
                                                    CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                                                };
                                                _ = Dialog.ShowAsync().ConfigureAwait(true);
                                            }
                                        }
                                        else
                                        {
                                            QueueContentDialog Dialog = new QueueContentDialog
                                            {
                                                Title = Globalization.GetString("Common_Dialog_WarningTitle"),
                                                Content = Globalization.GetString("QueueDialog_UndoFailure_Content"),
                                                CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                                            };
                                            _ = await Dialog.ShowAsync().ConfigureAwait(true);
                                        }
                                        break;
                                    }
                            }
                        }
                    }
                }
                catch
                {
                    QueueContentDialog Dialog = new QueueContentDialog
                    {
                        Title = Globalization.GetString("Common_Dialog_WarningTitle"),
                        Content = Globalization.GetString("QueueDialog_UndoFailure_Content"),
                        CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                    };
                    _ = await Dialog.ShowAsync().ConfigureAwait(true);
                }

                await Container.LoadingActivation(false).ConfigureAwait(false);
            }
        }

        private async void Copy_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();

            List<FileSystemStorageItemBase> SelectedItemsCopy = SelectedItems;

            if (SelectedItemsCopy.Count > 0)
            {
                try
                {
                    Clipboard.Clear();

                    DataPackage Package = new DataPackage
                    {
                        RequestedOperation = DataPackageOperation.Copy
                    };

                    IEnumerable<FileSystemStorageItemBase> StorageItems = SelectedItemsCopy.Where((Item) => Item is not (HyperlinkStorageItem or HiddenStorageItem));

                    if (StorageItems.Any())
                    {
                        List<IStorageItem> TempItemList = new List<IStorageItem>();

                        foreach (FileSystemStorageItemBase Item in StorageItems)
                        {
                            if (await Item.GetStorageItemAsync().ConfigureAwait(true) is IStorageItem It)
                            {
                                TempItemList.Add(It);
                            }
                        }

                        if (TempItemList.Count > 0)
                        {
                            Package.SetStorageItems(TempItemList, false);
                        }
                    }

                    IEnumerable<FileSystemStorageItemBase> NotStorageItems = SelectedItemsCopy.Where((Item) => Item is HyperlinkStorageItem or HiddenStorageItem);

                    if (NotStorageItems.Any())
                    {
                        XmlDocument Document = new XmlDocument();

                        XmlElement RootElemnt = Document.CreateElement("RX-Explorer");
                        Document.AppendChild(RootElemnt);

                        XmlElement KindElement = Document.CreateElement("Kind");
                        KindElement.InnerText = "RX-Explorer-TransferNotStorageItem";
                        RootElemnt.AppendChild(KindElement);

                        foreach (FileSystemStorageItemBase Item in NotStorageItems)
                        {
                            XmlElement InnerElement = Document.CreateElement("Item");
                            InnerElement.InnerText = Item.Path;
                            RootElemnt.AppendChild(InnerElement);
                        }

                        Package.SetText(Document.GetXml());
                    }

                    Clipboard.SetContent(Package);

                    FileCollection.Where((Item) => Item.ThumbnailOpacity != 1d).ToList().ForEach((Item) => Item.SetThumbnailOpacity(ThumbnailStatus.Normal));
                }
                catch
                {
                    QueueContentDialog Dialog = new QueueContentDialog
                    {
                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                        Content = Globalization.GetString("QueueDialog_UnableAccessClipboard_Content"),
                        CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                    };
                    _ = await Dialog.ShowAsync().ConfigureAwait(false);
                }
            }
        }

        private async void Paste_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();

            try
            {
                DataPackageView Package = Clipboard.GetContent();

                List<string> PathList = new List<string>();

                if (Package.Contains(StandardDataFormats.StorageItems))
                {
                    IReadOnlyList<IStorageItem> ItemList = await Package.GetStorageItemsAsync();
                    PathList.AddRange(ItemList.Select((Item) => Item.Path));
                }

                if (Package.Contains(StandardDataFormats.Text))
                {
                    string XmlText = await Package.GetTextAsync();

                    if (XmlText.Contains("RX-Explorer"))
                    {
                        XmlDocument Document = new XmlDocument();
                        Document.LoadXml(XmlText);

                        IXmlNode KindNode = Document.SelectSingleNode("/RX-Explorer/Kind");

                        if (KindNode?.InnerText == "RX-Explorer-TransferNotStorageItem")
                        {
                            PathList.AddRange(Document.SelectNodes("/RX-Explorer/Item").Select((Node) => Node.InnerText));
                        }
                    }
                }

                if (PathList.Count > 0)
                {
                    if (Package.RequestedOperation.HasFlag(DataPackageOperation.Move))
                    {
                        if (PathList.All((Path) => System.IO.Path.GetDirectoryName(Path) != CurrentFolder.Path))
                        {
                            await Container.LoadingActivation(true, Globalization.GetString("Progress_Tip_Moving")).ConfigureAwait(true);

                            using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableController())
                            {
                                try
                                {
                                    await Exclusive.Controller.MoveAsync(PathList, CurrentFolder.Path, (s, arg) =>
                                    {
                                        if (Container.ProBar.Value < arg.ProgressPercentage)
                                        {
                                            Container.ProBar.IsIndeterminate = false;
                                            Container.ProBar.Value = arg.ProgressPercentage;
                                        }
                                    }).ConfigureAwait(true);
                                }
                                catch (FileNotFoundException)
                                {
                                    QueueContentDialog dialog = new QueueContentDialog
                                    {
                                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                        Content = Globalization.GetString("QueueDialog_MoveFailForNotExist_Content"),
                                        CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                                    };

                                    _ = await dialog.ShowAsync().ConfigureAwait(true);
                                }
                                catch (FileCaputureException)
                                {
                                    QueueContentDialog dialog = new QueueContentDialog
                                    {
                                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                        Content = Globalization.GetString("QueueDialog_Item_Captured_Content"),
                                        CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                                    };

                                    _ = await dialog.ShowAsync().ConfigureAwait(true);
                                }
                                catch (InvalidOperationException)
                                {
                                    QueueContentDialog dialog = new QueueContentDialog
                                    {
                                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                        Content = Globalization.GetString("QueueDialog_UnauthorizedPaste_Content"),
                                        PrimaryButtonText = Globalization.GetString("Common_Dialog_ConfirmButton"),
                                        CloseButtonText = Globalization.GetString("Common_Dialog_CancelButton")
                                    };

                                    if (await dialog.ShowAsync().ConfigureAwait(true) == ContentDialogResult.Primary)
                                    {
                                        await Launcher.LaunchFolderPathAsync(CurrentFolder.Path);
                                    }
                                }
                                catch (Exception)
                                {
                                    QueueContentDialog dialog = new QueueContentDialog
                                    {
                                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                        Content = Globalization.GetString("QueueDialog_MoveFailUnexpectError_Content"),
                                        CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                                    };

                                    _ = await dialog.ShowAsync().ConfigureAwait(true);
                                }
                            }
                        }
                    }
                    else if (Package.RequestedOperation.HasFlag(DataPackageOperation.Copy))
                    {
                        await Container.LoadingActivation(true, Globalization.GetString("Progress_Tip_Copying")).ConfigureAwait(true);

                        using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableController())
                        {
                            try
                            {
                                await Exclusive.Controller.CopyAsync(PathList, CurrentFolder.Path, (s, arg) =>
                                {
                                    if (Container.ProBar.Value < arg.ProgressPercentage)
                                    {
                                        Container.ProBar.IsIndeterminate = false;
                                        Container.ProBar.Value = arg.ProgressPercentage;
                                    }
                                }).ConfigureAwait(true);
                            }
                            catch (FileNotFoundException)
                            {
                                QueueContentDialog Dialog = new QueueContentDialog
                                {
                                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                    Content = Globalization.GetString("QueueDialog_CopyFailForNotExist_Content"),
                                    CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                                };

                                _ = await Dialog.ShowAsync().ConfigureAwait(true);
                            }
                            catch (InvalidOperationException)
                            {
                                QueueContentDialog dialog = new QueueContentDialog
                                {
                                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                    Content = Globalization.GetString("QueueDialog_UnauthorizedPaste_Content"),
                                    PrimaryButtonText = Globalization.GetString("Common_Dialog_ConfirmButton"),
                                    CloseButtonText = Globalization.GetString("Common_Dialog_CancelButton")
                                };

                                if (await dialog.ShowAsync().ConfigureAwait(true) == ContentDialogResult.Primary)
                                {
                                    await Launcher.LaunchFolderPathAsync(CurrentFolder.Path);
                                }
                            }
                            catch (Exception)
                            {
                                QueueContentDialog dialog = new QueueContentDialog
                                {
                                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                    Content = Globalization.GetString("QueueDialog_CopyFailUnexpectError_Content"),
                                    CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                                };

                                _ = await dialog.ShowAsync().ConfigureAwait(true);
                            }
                        }
                    }
                }
            }
            catch (Exception ex) when (ex.HResult == unchecked((int)0x80040064))
            {
                using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableController())
                {
                    await Container.LoadingActivation(true, Globalization.GetString("Progress_Tip_Copying")).ConfigureAwait(true);

                    if (await Exclusive.Controller.PasteRemoteFile(CurrentFolder.Path).ConfigureAwait(true))
                    {
                        await Container.LoadingActivation(false).ConfigureAwait(true);
                    }
                    else
                    {
                        await Container.LoadingActivation(false).ConfigureAwait(true);

                        QueueContentDialog dialog = new QueueContentDialog
                        {
                            Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                            Content = Globalization.GetString("QueueDialog_CopyFailUnexpectError_Content"),
                            CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                        };

                        _ = await dialog.ShowAsync().ConfigureAwait(true);
                    }
                }
            }
            catch
            {
                QueueContentDialog dialog = new QueueContentDialog
                {
                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                    Content = Globalization.GetString("QueueDialog_FailToGetClipboardError_Content"),
                    CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                };

                _ = await dialog.ShowAsync().ConfigureAwait(true);
            }
            finally
            {
                await Container.LoadingActivation(false).ConfigureAwait(true);
                FileCollection.Where((Item) => Item.ThumbnailOpacity != 1d).ToList().ForEach((Item) => Item.SetThumbnailOpacity(ThumbnailStatus.Normal));
            }
        }

        private async void Cut_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();

            List<FileSystemStorageItemBase> SelectedItemsCopy = SelectedItems;

            if (SelectedItemsCopy.Count > 0)
            {
                try
                {
                    Clipboard.Clear();

                    DataPackage Package = new DataPackage
                    {
                        RequestedOperation = DataPackageOperation.Move
                    };

                    IEnumerable<FileSystemStorageItemBase> StorageItems = SelectedItemsCopy.Where((Item) => Item is not (HyperlinkStorageItem or HiddenStorageItem));

                    if (StorageItems.Any())
                    {
                        List<IStorageItem> TempItemList = new List<IStorageItem>();

                        foreach (FileSystemStorageItemBase Item in StorageItems)
                        {
                            if (await Item.GetStorageItemAsync().ConfigureAwait(true) is IStorageItem It)
                            {
                                TempItemList.Add(It);
                            }
                        }

                        if (TempItemList.Count > 0)
                        {
                            Package.SetStorageItems(TempItemList, false);
                        }
                    }

                    IEnumerable<FileSystemStorageItemBase> NotStorageItems = SelectedItemsCopy.Where((Item) => Item is HyperlinkStorageItem or HiddenStorageItem);

                    if (NotStorageItems.Any())
                    {
                        XmlDocument Document = new XmlDocument();

                        XmlElement RootElemnt = Document.CreateElement("RX-Explorer");
                        Document.AppendChild(RootElemnt);

                        XmlElement KindElement = Document.CreateElement("Kind");
                        KindElement.InnerText = "RX-Explorer-TransferNotStorageItem";
                        RootElemnt.AppendChild(KindElement);

                        foreach (FileSystemStorageItemBase Item in NotStorageItems)
                        {
                            XmlElement InnerElement = Document.CreateElement("Item");
                            InnerElement.InnerText = Item.Path;
                            RootElemnt.AppendChild(InnerElement);
                        }

                        Package.SetText(Document.GetXml());
                    }

                    Clipboard.SetContent(Package);

                    FileCollection.Where((Item) => Item.ThumbnailOpacity != 1d).ToList().ForEach((Item) => Item.SetThumbnailOpacity(ThumbnailStatus.Normal));
                    SelectedItemsCopy.ForEach((Item) => Item.SetThumbnailOpacity(ThumbnailStatus.ReduceOpacity));
                }
                catch
                {
                    QueueContentDialog Dialog = new QueueContentDialog
                    {
                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                        Content = Globalization.GetString("QueueDialog_UnableAccessClipboard_Content"),
                        CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                    };
                    _ = await Dialog.ShowAsync().ConfigureAwait(false);
                }
            }
        }

        private async void Delete_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();

            if (SelectedItems.Count > 0)
            {
                bool ExecuteDelete = false;

                if (ApplicationData.Current.LocalSettings.Values["DeleteConfirmSwitch"] is bool DeleteConfirm)
                {
                    if (DeleteConfirm)
                    {
                        DeleteDialog QueueContenDialog = new DeleteDialog(Globalization.GetString("QueueDialog_DeleteFiles_Content"));

                        if (await QueueContenDialog.ShowAsync().ConfigureAwait(true) == ContentDialogResult.Primary)
                        {
                            ExecuteDelete = true;
                        }
                    }
                    else
                    {
                        ExecuteDelete = true;
                    }
                }
                else
                {
                    DeleteDialog QueueContenDialog = new DeleteDialog(Globalization.GetString("QueueDialog_DeleteFiles_Content"));

                    if (await QueueContenDialog.ShowAsync().ConfigureAwait(true) == ContentDialogResult.Primary)
                    {
                        ExecuteDelete = true;
                    }
                }

                bool PermanentDelete = Window.Current.CoreWindow.GetKeyState(VirtualKey.Shift).HasFlag(CoreVirtualKeyStates.Down);

                if (ApplicationData.Current.LocalSettings.Values["AvoidRecycleBin"] is bool IsAvoidRecycleBin)
                {
                    PermanentDelete |= IsAvoidRecycleBin;
                }

                if (ExecuteDelete)
                {
                    await Container.LoadingActivation(true, Globalization.GetString("Progress_Tip_Deleting")).ConfigureAwait(true);

                    try
                    {
                        using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableController())
                        {
                            IEnumerable<string> PathList = SelectedItems.Select((Item) => Item.Path);

                            await Exclusive.Controller.DeleteAsync(PathList, PermanentDelete, (s, arg) =>
                            {
                                if (Container.ProBar.Value < arg.ProgressPercentage)
                                {
                                    Container.ProBar.IsIndeterminate = false;
                                    Container.ProBar.Value = arg.ProgressPercentage;
                                }
                            }).ConfigureAwait(true);
                        }
                    }
                    catch (FileNotFoundException)
                    {
                        QueueContentDialog Dialog = new QueueContentDialog
                        {
                            Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                            Content = Globalization.GetString("QueueDialog_DeleteItemError_Content"),
                            CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                        };

                        _ = await Dialog.ShowAsync().ConfigureAwait(true);
                    }
                    catch (FileCaputureException)
                    {
                        QueueContentDialog dialog = new QueueContentDialog
                        {
                            Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                            Content = Globalization.GetString("QueueDialog_Item_Captured_Content"),
                            CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                        };

                        _ = await dialog.ShowAsync().ConfigureAwait(true);
                    }
                    catch (InvalidOperationException)
                    {
                        QueueContentDialog dialog = new QueueContentDialog
                        {
                            Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                            Content = Globalization.GetString("QueueDialog_UnauthorizedDelete_Content"),
                            PrimaryButtonText = Globalization.GetString("Common_Dialog_ConfirmButton"),
                            CloseButtonText = Globalization.GetString("Common_Dialog_CancelButton")
                        };

                        if (await dialog.ShowAsync().ConfigureAwait(true) == ContentDialogResult.Primary)
                        {
                            await Launcher.LaunchFolderPathAsync(CurrentFolder.Path);
                        }
                    }
                    catch (Exception)
                    {
                        QueueContentDialog dialog = new QueueContentDialog
                        {
                            Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                            Content = Globalization.GetString("QueueDialog_DeleteFailUnexpectError_Content"),
                            CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                        };

                        _ = await dialog.ShowAsync().ConfigureAwait(true);
                    }

                    await Container.LoadingActivation(false).ConfigureAwait(false);
                }
            }
        }

        private async void Rename_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();

            if (SelectedItems.Count > 0)
            {
                if (SelectedItems.Count > 1)
                {
                    QueueContentDialog Dialog = new QueueContentDialog
                    {
                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                        Content = Globalization.GetString("QueueDialog_RenameNumError_Content"),
                        CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                    };

                    _ = await Dialog.ShowAsync().ConfigureAwait(true);
                }
                else
                {
                    FileSystemStorageItemBase RenameItem = SelectedItem;

                    RenameDialog dialog = new RenameDialog(RenameItem);

                    if ((await dialog.ShowAsync().ConfigureAwait(true)) == ContentDialogResult.Primary)
                    {
                        if (await FileSystemStorageItemBase.CheckExist(Path.Combine(Path.GetDirectoryName(RenameItem.Path), dialog.DesireName)).ConfigureAwait(true))
                        {
                            QueueContentDialog Dialog = new QueueContentDialog
                            {
                                Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                Content = Globalization.GetString("QueueDialog_RenameExist_Content"),
                                PrimaryButtonText = Globalization.GetString("Common_Dialog_ContinueButton"),
                                CloseButtonText = Globalization.GetString("Common_Dialog_CancelButton")
                            };

                            if (await Dialog.ShowAsync().ConfigureAwait(true) != ContentDialogResult.Primary)
                            {
                                return;
                            }
                        }

                        using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableController())
                        {
                            try
                            {
                                await Exclusive.Controller.RenameAsync(RenameItem.Path, dialog.DesireName).ConfigureAwait(true);
                            }
                            catch (FileLoadException)
                            {
                                QueueContentDialog LoadExceptionDialog = new QueueContentDialog
                                {
                                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                    Content = Globalization.GetString("QueueDialog_FileOccupied_Content"),
                                    CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton"),
                                };

                                _ = await LoadExceptionDialog.ShowAsync().ConfigureAwait(true);
                            }
                            catch (InvalidOperationException)
                            {
                                QueueContentDialog UnauthorizeDialog = new QueueContentDialog
                                {
                                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                    Content = Globalization.GetString("QueueDialog_UnauthorizedRenameFile_Content"),
                                    PrimaryButtonText = Globalization.GetString("Common_Dialog_ConfirmButton"),
                                    CloseButtonText = Globalization.GetString("Common_Dialog_CancelButton")
                                };

                                if (await UnauthorizeDialog.ShowAsync().ConfigureAwait(true) == ContentDialogResult.Primary)
                                {
                                    await Launcher.LaunchFolderPathAsync(CurrentFolder.Path);
                                }
                            }
                            catch
                            {
                                QueueContentDialog Dialog = new QueueContentDialog
                                {
                                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                    Content = Globalization.GetString("QueueDialog_UnauthorizedRename_StartExplorer_Content"),
                                    PrimaryButtonText = Globalization.GetString("Common_Dialog_NowButton"),
                                    CloseButtonText = Globalization.GetString("Common_Dialog_LaterButton")
                                };

                                if (await Dialog.ShowAsync().ConfigureAwait(true) == ContentDialogResult.Primary)
                                {
                                    _ = await Launcher.LaunchFolderPathAsync(CurrentFolder.Path);
                                }
                            }
                        }
                    }
                }
            }
        }

        private async void BluetoothShare_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();

            if (await SelectedItem.GetStorageItemAsync().ConfigureAwait(true) is StorageFile ShareFile)
            {
                if (!await FileSystemStorageItemBase.CheckExist(ShareFile.Path).ConfigureAwait(true))
                {
                    QueueContentDialog Dialog = new QueueContentDialog
                    {
                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                        Content = Globalization.GetString("QueueDialog_LocateFileFailure_Content"),
                        CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                    };

                    _ = await Dialog.ShowAsync().ConfigureAwait(true);

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
            else
            {
                QueueContentDialog Dialog = new QueueContentDialog
                {
                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                    Content = Globalization.GetString("QueueDialog_UnableAccessFile_Content"),
                    CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                };

                _ = await Dialog.ShowAsync().ConfigureAwait(true);
            }
        }

        private void ViewControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            DelayRenameCancel?.Cancel();

            foreach (SelectorItem RemovedItem in e.RemovedItems.Select((Item) => ItemPresenter.ContainerFromItem(Item)).OfType<SelectorItem>())
            {
                RemovedItem.CanDrag = false;
            }

            foreach (SelectorItem SelectedItem in e.AddedItems.Select((Item) => ItemPresenter.ContainerFromItem(Item)).OfType<SelectorItem>())
            {
                SelectedItem.CanDrag = true;
            }

            ItemPresenter.UpdateLayout();

            if (SelectedItems.Any((Item) => Item.StorageType != StorageItemTypes.Folder))
            {
                if (SelectedItems.All((Item) => Item.StorageType == StorageItemTypes.File))
                {
                    if (SelectedItems.All((Item) => Item.Type.Equals(".zip", StringComparison.OrdinalIgnoreCase)) || SelectedItems.All((Item) => Item.Type.Equals(".tar", StringComparison.OrdinalIgnoreCase)) || SelectedItems.All((Item) => Item.Type.EndsWith(".gz", StringComparison.OrdinalIgnoreCase)))
                    {
                        MixCompression.Label = Globalization.GetString("Operate_Text_Decompression");
                    }
                    else
                    {
                        MixCompression.Label = Globalization.GetString("Operate_Text_Compression");
                    }
                }
                else
                {
                    MixCompression.Label = Globalization.GetString("Operate_Text_Compression");
                }
            }
            else
            {
                MixCompression.Label = Globalization.GetString("Operate_Text_Compression");
            }

            if (SelectedItem is FileSystemStorageItemBase Item)
            {
                if (Item.StorageType == StorageItemTypes.File)
                {
                    FileTool.IsEnabled = true;
                    FileEdit.IsEnabled = false;
                    FileShare.IsEnabled = true;

                    ChooseOtherApp.IsEnabled = true;
                    RunWithSystemAuthority.IsEnabled = false;

                    Compression.Label = Globalization.GetString("Operate_Text_Compression");

                    switch (Item.Type.ToLower())
                    {
                        case ".tar":
                        case ".gz":
                        case ".zip":
                            {
                                Compression.Label = Globalization.GetString("Operate_Text_Decompression");
                                break;
                            }
                        case ".mp4":
                        case ".wmv":
                            {
                                FileEdit.IsEnabled = true;
                                Transcode.IsEnabled = true;
                                VideoEdit.IsEnabled = true;
                                VideoMerge.IsEnabled = true;
                                break;
                            }
                        case ".mkv":
                        case ".m4a":
                        case ".mov":
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
                                FileEdit.IsEnabled = true;
                                VideoEdit.IsEnabled = false;
                                VideoMerge.IsEnabled = false;
                                Transcode.IsEnabled = true;
                                break;
                            }
                        case ".exe":
                            {
                                ChooseOtherApp.IsEnabled = false;
                                RunWithSystemAuthority.IsEnabled = true;
                                break;
                            }
                        case ".msi":
                        case ".bat":
                            {
                                RunWithSystemAuthority.IsEnabled = true;
                                break;
                            }
                        case ".msc":
                            {
                                ChooseOtherApp.IsEnabled = false;
                                break;
                            }
                    }
                }
            }

            string[] StatusTipsSplit = StatusTips.Text.Split("  |  ", StringSplitOptions.RemoveEmptyEntries);

            if (SelectedItems.Count > 0)
            {
                string SizeInfo = string.Empty;

                if (SelectedItems.All((Item) => Item.StorageType == StorageItemTypes.File))
                {
                    ulong TotalSize = 0;
                    foreach (ulong Size in SelectedItems.Select((Item) => Item.SizeRaw).ToArray())
                    {
                        TotalSize += Size;
                    }

                    SizeInfo = $"  |  {TotalSize.ToFileSizeDescription()}";
                }

                if (StatusTipsSplit.Length > 0)
                {
                    StatusTips.Text = $"{StatusTipsSplit[0]}  |  {Globalization.GetString("FilePresenterBottomStatusTip_SelectedItem").Replace("{ItemNum}", SelectedItems.Count.ToString())}{SizeInfo}";
                }
                else
                {
                    StatusTips.Text += $"  |  {Globalization.GetString("FilePresenterBottomStatusTip_SelectedItem").Replace("{ItemNum}", SelectedItems.Count.ToString())}{SizeInfo}";
                }
            }
            else
            {
                if (StatusTipsSplit.Length > 0)
                {
                    StatusTips.Text = StatusTipsSplit[0];
                }
            }
        }

        private void ViewControl_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if ((e.OriginalSource as FrameworkElement)?.DataContext is FileSystemStorageItemBase Item)
            {
                PointerPoint PointerInfo = e.GetCurrentPoint(null);

                if (PointerInfo.Properties.IsMiddleButtonPressed && Item.StorageType == StorageItemTypes.Folder)
                {
                    SelectionExtention.Disable();
                    SelectedItem = Item;
                    _ = TabViewContainer.ThisPage.CreateNewTabAsync(null, Item.Path);
                }
                else if ((e.OriginalSource as FrameworkElement).FindParentOfType<SelectorItem>() != null)
                {
                    if (ItemPresenter.SelectionMode != ListViewSelectionMode.Multiple)
                    {
                        if (e.KeyModifiers == VirtualKeyModifiers.None)
                        {
                            if (SelectedItems.Contains(Item))
                            {
                                SelectionExtention.Disable();
                            }
                            else
                            {
                                if (PointerInfo.Properties.IsLeftButtonPressed)
                                {
                                    SelectedItem = Item;
                                }

                                if (e.OriginalSource is ListViewItemPresenter || (e.OriginalSource is TextBlock Block && Block.Name == "EmptyTextblock"))
                                {
                                    SelectionExtention.Enable();
                                }
                                else
                                {
                                    SelectionExtention.Disable();
                                }
                            }
                        }
                        else
                        {
                            SelectionExtention.Disable();
                        }
                    }
                    else
                    {
                        SelectionExtention.Disable();
                    }
                }
            }
            else
            {
                SelectedItem = null;
                SelectionExtention.Enable();
            }
        }

        private async void ViewControl_RightTapped(object sender, RightTappedRoutedEventArgs e)
        {
            if (e.PointerDeviceType == PointerDeviceType.Mouse)
            {
                e.Handled = true;
                Container.BlockKeyboardShortCutInput = true;

                if (ItemPresenter is GridView)
                {
                    if ((e.OriginalSource as FrameworkElement)?.DataContext is FileSystemStorageItemBase Context)
                    {
                        if (SelectedItems.Count > 1 && SelectedItems.Contains(Context))
                        {
                            await ItemPresenter.SetCommandBarFlyoutWithExtraContextMenuItems(MixedFlyout, e.GetPosition((FrameworkElement)sender)).ConfigureAwait(true);
                        }
                        else
                        {
                            SelectedItem = Context;

                            if (Context is HyperlinkStorageItem)
                            {
                                await ItemPresenter.SetCommandBarFlyoutWithExtraContextMenuItems(LnkItemFlyout, e.GetPosition((FrameworkElement)sender)).ConfigureAwait(true);
                            }
                            else
                            {
                                await ItemPresenter.SetCommandBarFlyoutWithExtraContextMenuItems(Context.StorageType == StorageItemTypes.Folder ? FolderFlyout : FileFlyout, e.GetPosition((FrameworkElement)sender)).ConfigureAwait(true);
                            }
                        }
                    }
                    else
                    {
                        SelectedItem = null;
                        await ItemPresenter.SetCommandBarFlyoutWithExtraContextMenuItems(EmptyFlyout, e.GetPosition((FrameworkElement)sender)).ConfigureAwait(true);
                    }
                }
                else
                {
                    if (e.OriginalSource is FrameworkElement Element)
                    {
                        if (Element.Name == "EmptyTextblock")
                        {
                            SelectedItem = null;
                            await ItemPresenter.SetCommandBarFlyoutWithExtraContextMenuItems(EmptyFlyout, e.GetPosition((FrameworkElement)sender)).ConfigureAwait(true);
                        }
                        else
                        {
                            if (Element.DataContext is FileSystemStorageItemBase Context)
                            {
                                if (SelectedItems.Count > 1 && SelectedItems.Contains(Context))
                                {
                                    await ItemPresenter.SetCommandBarFlyoutWithExtraContextMenuItems(MixedFlyout, e.GetPosition((FrameworkElement)sender)).ConfigureAwait(true);
                                }
                                else
                                {
                                    if (SelectedItem == Context)
                                    {
                                        if (Context is HyperlinkStorageItem)
                                        {
                                            await ItemPresenter.SetCommandBarFlyoutWithExtraContextMenuItems(LnkItemFlyout, e.GetPosition((FrameworkElement)sender)).ConfigureAwait(true);
                                        }
                                        else
                                        {
                                            await ItemPresenter.SetCommandBarFlyoutWithExtraContextMenuItems(Context.StorageType == StorageItemTypes.Folder ? FolderFlyout : FileFlyout, e.GetPosition((FrameworkElement)sender)).ConfigureAwait(true);
                                        }
                                    }
                                    else
                                    {
                                        if (e.OriginalSource is TextBlock)
                                        {
                                            SelectedItem = Context;

                                            if (Context is HyperlinkStorageItem)
                                            {
                                                await ItemPresenter.SetCommandBarFlyoutWithExtraContextMenuItems(LnkItemFlyout, e.GetPosition((FrameworkElement)sender)).ConfigureAwait(true);
                                            }
                                            else
                                            {
                                                await ItemPresenter.SetCommandBarFlyoutWithExtraContextMenuItems(Context.StorageType == StorageItemTypes.Folder ? FolderFlyout : FileFlyout, e.GetPosition((FrameworkElement)sender)).ConfigureAwait(true);
                                            }
                                        }
                                        else
                                        {
                                            SelectedItem = null;
                                            await ItemPresenter.SetCommandBarFlyoutWithExtraContextMenuItems(EmptyFlyout, e.GetPosition((FrameworkElement)sender)).ConfigureAwait(true);
                                        }
                                    }
                                }
                            }
                            else
                            {
                                SelectedItem = null;
                                await ItemPresenter.SetCommandBarFlyoutWithExtraContextMenuItems(EmptyFlyout, e.GetPosition((FrameworkElement)sender)).ConfigureAwait(true);
                            }
                        }
                    }
                }

                Container.BlockKeyboardShortCutInput = false;
            }
        }

        private async void FileProperty_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();

            PropertyDialog Dialog = new PropertyDialog(SelectedItem);
            _ = await Dialog.ShowAsync().ConfigureAwait(true);
        }

        private async void Compression_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();

            if (SelectedItem is FileSystemStorageItemBase Item)
            {
                if (!await FileSystemStorageItemBase.CheckExist(Item.Path).ConfigureAwait(true))
                {
                    QueueContentDialog Dialog = new QueueContentDialog
                    {
                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                        Content = Globalization.GetString("QueueDialog_LocateFileFailure_Content"),
                        CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                    };

                    _ = await Dialog.ShowAsync().ConfigureAwait(true);

                    return;
                }

                switch (Item.Type.ToLower())
                {
                    case ".zip":
                        {
                            await Container.LoadingActivation(true, Globalization.GetString("Progress_Tip_Extracting")).ConfigureAwait(true);

                            try
                            {
                                await CompressionUtil.ExtractZipAsync(Item, async (s, e) =>
                                {
                                    await Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                                    {
                                        if (Container.ProBar.Value < e.ProgressPercentage)
                                        {
                                            Container.ProBar.IsIndeterminate = false;
                                            Container.ProBar.Value = e.ProgressPercentage;
                                        }
                                    });
                                }).ConfigureAwait(true);
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
                                    _ = await Launcher.LaunchFolderPathAsync(CurrentFolder.Path);
                                }
                            }
                            catch (NotImplementedException)
                            {
                                QueueContentDialog Dialog = new QueueContentDialog
                                {
                                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                    Content = Globalization.GetString("QueueDialog_CanNotDecompressEncrypted_Content"),
                                    CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                                };

                                _ = await Dialog.ShowAsync().ConfigureAwait(true);
                            }
                            catch (Exception ex)
                            {
                                QueueContentDialog dialog = new QueueContentDialog
                                {
                                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                    Content = Globalization.GetString("QueueDialog_DecompressionError_Content") + ex.Message,
                                    CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                                };

                                _ = await dialog.ShowAsync().ConfigureAwait(true);
                            }

                            await Container.LoadingActivation(false).ConfigureAwait(true);

                            break;
                        }
                    case ".tar":
                        {
                            await Container.LoadingActivation(true, Globalization.GetString("Progress_Tip_Extracting")).ConfigureAwait(true);

                            try
                            {
                                await CompressionUtil.ExtractTarAsync(Item, async (s, e) =>
                                {
                                    await Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                                    {
                                        if (Container.ProBar.Value < e.ProgressPercentage)
                                        {
                                            Container.ProBar.IsIndeterminate = false;
                                            Container.ProBar.Value = e.ProgressPercentage;
                                        }
                                    });
                                }).ConfigureAwait(true);
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
                                    _ = await Launcher.LaunchFolderPathAsync(CurrentFolder.Path);
                                }
                            }
                            catch (Exception ex)
                            {
                                QueueContentDialog dialog = new QueueContentDialog
                                {
                                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                    Content = Globalization.GetString("QueueDialog_DecompressionError_Content") + ex.Message,
                                    CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                                };

                                _ = await dialog.ShowAsync().ConfigureAwait(true);
                            }

                            await Container.LoadingActivation(false).ConfigureAwait(true);

                            break;
                        }
                    case ".gz":
                        {
                            await Container.LoadingActivation(true, Globalization.GetString("Progress_Tip_Extracting")).ConfigureAwait(true);

                            try
                            {
                                await CompressionUtil.ExtractGZipAsync(Item, async (s, e) =>
                                {
                                    await Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                                    {
                                        if (Container.ProBar.Value < e.ProgressPercentage)
                                        {
                                            Container.ProBar.IsIndeterminate = false;
                                            Container.ProBar.Value = e.ProgressPercentage;
                                        }
                                    });
                                }).ConfigureAwait(true);
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
                                    _ = await Launcher.LaunchFolderPathAsync(CurrentFolder.Path);
                                }
                            }
                            catch (Exception ex)
                            {
                                QueueContentDialog dialog = new QueueContentDialog
                                {
                                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                    Content = Globalization.GetString("QueueDialog_DecompressionError_Content") + ex.Message,
                                    CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                                };

                                _ = await dialog.ShowAsync().ConfigureAwait(true);
                            }

                            await Container.LoadingActivation(false).ConfigureAwait(true);

                            break;
                        }
                    default:
                        {
                            CompressDialog dialog = new CompressDialog(true, Path.GetFileNameWithoutExtension(Item.Name));

                            if ((await dialog.ShowAsync().ConfigureAwait(true)) == ContentDialogResult.Primary)
                            {
                                await Container.LoadingActivation(true, Globalization.GetString("Progress_Tip_Compressing")).ConfigureAwait(true);

                                try
                                {
                                    switch (dialog.Type)
                                    {
                                        case CompressionType.Zip:
                                            {
                                                await CompressionUtil.CreateZipAsync(Item, Path.Combine(CurrentFolder.Path, dialog.FileName), (int)dialog.Level, async (s, e) =>
                                                {
                                                    await Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                                                    {
                                                        if (Container.ProBar.Value < e.ProgressPercentage)
                                                        {
                                                            Container.ProBar.IsIndeterminate = false;
                                                            Container.ProBar.Value = e.ProgressPercentage;
                                                        }
                                                    });
                                                }).ConfigureAwait(true);

                                                break;
                                            }
                                        case CompressionType.Tar:
                                            {
                                                await CompressionUtil.CreateTarAsync(Item, Path.Combine(CurrentFolder.Path, dialog.FileName), async (s, e) =>
                                                {
                                                    await Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                                                    {
                                                        if (Container.ProBar.Value < e.ProgressPercentage)
                                                        {
                                                            Container.ProBar.IsIndeterminate = false;
                                                            Container.ProBar.Value = e.ProgressPercentage;
                                                        }
                                                    });
                                                }).ConfigureAwait(true);

                                                break;
                                            }
                                        case CompressionType.Gzip:
                                            {
                                                await CompressionUtil.CreateGzipAsync(Item, Path.Combine(CurrentFolder.Path, dialog.FileName), (int)dialog.Level, async (s, e) =>
                                                {
                                                    await Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                                                    {
                                                        if (Container.ProBar.Value < e.ProgressPercentage)
                                                        {
                                                            Container.ProBar.IsIndeterminate = false;
                                                            Container.ProBar.Value = e.ProgressPercentage;
                                                        }
                                                    });
                                                }).ConfigureAwait(true);

                                                break;
                                            }
                                    }
                                }
                                catch (UnauthorizedAccessException)
                                {
                                    QueueContentDialog dialog1 = new QueueContentDialog
                                    {
                                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                        Content = Globalization.GetString("QueueDialog_UnauthorizedCompression_Content"),
                                        PrimaryButtonText = Globalization.GetString("Common_Dialog_NowButton"),
                                        CloseButtonText = Globalization.GetString("Common_Dialog_LaterButton")
                                    };

                                    if (await dialog1.ShowAsync().ConfigureAwait(true) == ContentDialogResult.Primary)
                                    {
                                        _ = await Launcher.LaunchFolderPathAsync(CurrentFolder.Path);
                                    }
                                }
                                catch (Exception ex)
                                {
                                    QueueContentDialog dialog2 = new QueueContentDialog
                                    {
                                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                        Content = Globalization.GetString("QueueDialog_CompressionError_Content") + ex.Message,
                                        CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                                    };

                                    _ = await dialog2.ShowAsync().ConfigureAwait(true);
                                }

                                await Container.LoadingActivation(false).ConfigureAwait(false);
                            }

                            break;
                        }
                }
            }
        }

        private async void ViewControl_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
        {
            e.Handled = true;

            DelayRenameCancel?.Cancel();

            if ((e.OriginalSource as FrameworkElement)?.DataContext is FileSystemStorageItemBase ReFile)
            {
                await EnterSelectedItem(ReFile).ConfigureAwait(false);
            }
            else if (e.OriginalSource is Grid)
            {
                if (Path.GetPathRoot(CurrentFolder?.Path) == CurrentFolder?.Path)
                {
                    MainPage.ThisPage.NavView_BackRequested(null, null);
                }
                else
                {
                    Container.GoParentFolder_Click(null, null);
                }
            }
        }

        private async void Transcode_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();

            if (!await FileSystemStorageItemBase.CheckExist(SelectedItem.Path).ConfigureAwait(true))
            {
                QueueContentDialog Dialog = new QueueContentDialog
                {
                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                    Content = Globalization.GetString("QueueDialog_LocateFileFailure_Content"),
                    CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                };

                _ = await Dialog.ShowAsync().ConfigureAwait(true);

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

            switch (SelectedItem.Type.ToLower())
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
                        if ((await SelectedItem.GetStorageItemAsync().ConfigureAwait(true)) is StorageFile Source)
                        {
                            TranscodeDialog dialog = new TranscodeDialog(Source);

                            if ((await dialog.ShowAsync().ConfigureAwait(true)) == ContentDialogResult.Primary)
                            {
                                try
                                {
                                    string DestFilePath = Path.Combine(CurrentFolder.Path, $"{Source.Path}.{dialog.MediaTranscodeEncodingProfile.ToLower()}");

                                    if (await FileSystemStorageItemBase.CreateAsync(DestFilePath, StorageItemTypes.File, CreateOption.GenerateUniqueName).ConfigureAwait(true) is FileSystemStorageItemBase Item)
                                    {
                                        if (await Item.GetStorageItemAsync().ConfigureAwait(true) is StorageFile DestinationFile)
                                        {
                                            await GeneralTransformer.TranscodeFromAudioOrVideoAsync(Source, DestinationFile, dialog.MediaTranscodeEncodingProfile, dialog.MediaTranscodeQuality, dialog.SpeedUp).ConfigureAwait(true);
                                        }
                                        else
                                        {
                                            throw new FileNotFoundException();
                                        }
                                    }
                                    else
                                    {
                                        throw new FileNotFoundException();
                                    }
                                }
                                catch (Exception)
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
                                        _ = await Launcher.LaunchFolderPathAsync(CurrentFolder.Path);
                                    }
                                }
                            }
                        }
                        else
                        {
                            QueueContentDialog Dialog = new QueueContentDialog
                            {
                                Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                Content = Globalization.GetString("QueueDialog_UnableAccessFile_Content"),
                                CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                            };

                            _ = await Dialog.ShowAsync().ConfigureAwait(true);
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
                        using (IRandomAccessStream OriginStream = await SelectedItem.GetRandomAccessStreamFromFileAsync(FileAccessMode.Read).ConfigureAwait(true))
                        {
                            BitmapDecoder Decoder = await BitmapDecoder.CreateAsync(OriginStream);
                            Dialog = new TranscodeImageDialog(Decoder.PixelWidth, Decoder.PixelHeight);
                        }

                        if (await Dialog.ShowAsync().ConfigureAwait(true) == ContentDialogResult.Primary)
                        {
                            await Container.LoadingActivation(true, Globalization.GetString("Progress_Tip_Transcoding")).ConfigureAwait(true);

                            await GeneralTransformer.TranscodeFromImageAsync(SelectedItem, Dialog.TargetFile, Dialog.IsEnableScale, Dialog.ScaleWidth, Dialog.ScaleHeight, Dialog.InterpolationMode).ConfigureAwait(true);

                            await Container.LoadingActivation(false).ConfigureAwait(true);
                        }
                        break;
                    }
            }
        }

        private async void FolderProperty_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();

            if (!await FileSystemStorageItemBase.CheckExist(SelectedItem.Path).ConfigureAwait(true))
            {
                QueueContentDialog dialog = new QueueContentDialog
                {
                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                    Content = Globalization.GetString("QueueDialog_LocateFileFailure_Content"),
                    CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                };

                _ = await dialog.ShowAsync().ConfigureAwait(true);

                return;
            }

            PropertyDialog Dialog = new PropertyDialog(SelectedItem);

            _ = await Dialog.ShowAsync().ConfigureAwait(false);
        }

        private async void WIFIShare_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();

            if (SelectedItem != null)
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

                using (MD5 MD5Alg = MD5.Create())
                {
                    string Hash = MD5Alg.GetHash(SelectedItem.Path);
                    QRText.Text = WiFiProvider.CurrentUri + Hash;
                    WiFiProvider.FilePathMap = new KeyValuePair<string, string>(Hash, SelectedItem.Path);
                }

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

        private async void UseSystemFileMananger_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();

            _ = await Launcher.LaunchFolderPathAsync(CurrentFolder.Path);
        }

        private async void ParentProperty_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();

            if (!await FileSystemStorageItemBase.CheckExist(CurrentFolder.Path).ConfigureAwait(true))
            {
                QueueContentDialog Dialog = new QueueContentDialog
                {
                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                    Content = Globalization.GetString("QueueDialog_LocateFolderFailure_Content"),
                    CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                };

                _ = await Dialog.ShowAsync().ConfigureAwait(true);

                return;
            }

            if (CurrentFolder.Path.Equals(Path.GetPathRoot(CurrentFolder.Path), StringComparison.OrdinalIgnoreCase))
            {
                if (CommonAccessCollection.HardDeviceList.FirstOrDefault((Device) => Device.Folder.Path.Equals(CurrentFolder.Path, StringComparison.OrdinalIgnoreCase)) is HardDeviceInfo Info)
                {
                    DeviceInfoDialog dialog = new DeviceInfoDialog(Info);
                    _ = await dialog.ShowAsync().ConfigureAwait(true);
                }
                else
                {
                    PropertyDialog Dialog = new PropertyDialog(CurrentFolder);
                    _ = await Dialog.ShowAsync().ConfigureAwait(true);
                }
            }
            else
            {
                PropertyDialog Dialog = new PropertyDialog(CurrentFolder);
                _ = await Dialog.ShowAsync().ConfigureAwait(true);
            }
        }

        private async void ItemOpen_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();

            if (SelectedItem is FileSystemStorageItemBase ReFile)
            {
                await EnterSelectedItem(ReFile).ConfigureAwait(false);
            }
        }

        private void QRText_GotFocus(object sender, RoutedEventArgs e)
        {
            ((TextBox)sender).SelectAll();
        }

        private async void CreateFolder_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();

            if (await FileSystemStorageItemBase.CheckExist(CurrentFolder.Path).ConfigureAwait(true))
            {
                if (await FileSystemStorageItemBase.CreateAsync(Path.Combine(CurrentFolder.Path, Globalization.GetString("Create_NewFolder_Admin_Name")), StorageItemTypes.Folder, CreateOption.GenerateUniqueName).ConfigureAwait(true) is FileSystemStorageItemBase NewFolder)
                {
                    while (true)
                    {
                        if (FileCollection.FirstOrDefault((Item) => Item == NewFolder) is FileSystemStorageItemBase NewItem)
                        {
                            ItemPresenter.UpdateLayout();

                            ItemPresenter.ScrollIntoView(NewItem);

                            SelectedItem = NewItem;

                            if ((ItemPresenter.ContainerFromItem(NewItem) as SelectorItem)?.ContentTemplateRoot is FrameworkElement Element)
                            {
                                if (Element.FindName("NameLabel") is TextBlock NameLabel)
                                {
                                    NameLabel.Visibility = Visibility.Collapsed;
                                }

                                if (Element.FindName("NameEditBox") is TextBox EditBox)
                                {
                                    EditBox.Tag = NewItem;
                                    EditBox.Text = NewItem.Name;
                                    EditBox.Visibility = Visibility.Visible;
                                    EditBox.Focus(FocusState.Programmatic);
                                    EditBox.SelectAll();
                                }

                                Container.BlockKeyboardShortCutInput = true;
                            }

                            break;
                        }
                        else
                        {
                            await Task.Delay(500).ConfigureAwait(true);
                        }
                    }
                }
                else
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
                        _ = await Launcher.LaunchFolderPathAsync(CurrentFolder.Path);
                    }
                }
            }
            else
            {
                QueueContentDialog Dialog = new QueueContentDialog
                {
                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                    Content = Globalization.GetString("QueueDialog_LocateFolderFailure_Content"),
                    CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                };

                _ = await Dialog.ShowAsync().ConfigureAwait(true);
            }
        }

        private async void EmptyFlyout_Opening(object sender, object e)
        {
            try
            {
                DataPackageView Package = Clipboard.GetContent();

                if (Package.Contains(StandardDataFormats.StorageItems))
                {
                    Paste.IsEnabled = true;
                }
                else if (Package.Contains(StandardDataFormats.Text))
                {
                    string XmlText = await Package.GetTextAsync();

                    if (XmlText.Contains("RX-Explorer"))
                    {
                        XmlDocument Document = new XmlDocument();
                        Document.LoadXml(XmlText);
                        IXmlNode KindNode = Document.SelectSingleNode("/RX-Explorer/Kind");

                        if (KindNode?.InnerText == "RX-Explorer-TransferNotStorageItem")
                        {
                            Paste.IsEnabled = true;
                        }
                        else
                        {
                            Paste.IsEnabled = false;
                        }
                    }
                    else
                    {
                        Paste.IsEnabled = false;
                    }
                }
                else
                {
                    Paste.IsEnabled = false;
                }
            }
            catch
            {
                Paste.IsEnabled = false;
            }

            if (OperationRecorder.Current.Count > 0)
            {
                Undo.IsEnabled = true;
            }
            else
            {
                Undo.IsEnabled = false;
            }
        }

        private async void SystemShare_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();

            if (SelectedItem != null)
            {
                if (!await FileSystemStorageItemBase.CheckExist(SelectedItem.Path).ConfigureAwait(true))
                {
                    QueueContentDialog Dialog = new QueueContentDialog
                    {
                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                        Content = Globalization.GetString("QueueDialog_LocateFileFailure_Content"),
                        CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                    };

                    _ = await Dialog.ShowAsync().ConfigureAwait(true);
                }
                else
                {
                    if ((await SelectedItem.GetStorageItemAsync().ConfigureAwait(true)) is StorageFile ShareItem)
                    {
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
                    else
                    {
                        QueueContentDialog Dialog = new QueueContentDialog
                        {
                            Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                            Content = Globalization.GetString("QueueDialog_UnableAccessFile_Content"),
                            CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                        };

                        _ = await Dialog.ShowAsync().ConfigureAwait(true);
                    }
                }
            }
        }

        private async void Refresh_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();

            try
            {
                if (await FileSystemStorageItemBase.CheckExist(CurrentFolder.Path).ConfigureAwait(true))
                {
                    await DisplayItemsInFolder(CurrentFolder, true).ConfigureAwait(true);
                }
                else
                {
                    QueueContentDialog Dialog = new QueueContentDialog
                    {
                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                        Content = Globalization.GetString("QueueDialog_LocateFolderFailure_Content"),
                        CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                    };

                    _ = await Dialog.ShowAsync().ConfigureAwait(true);
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"{ nameof(Refresh_Click)} throw an exception");
            }
        }

        private async void ViewControl_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (!SettingControl.IsDoubleClickEnable && ItemPresenter.SelectionMode != ListViewSelectionMode.Multiple && e.ClickedItem is FileSystemStorageItemBase ReFile)
            {
                CoreVirtualKeyStates CtrlState = Window.Current.CoreWindow.GetKeyState(VirtualKey.Control);
                CoreVirtualKeyStates ShiftState = Window.Current.CoreWindow.GetKeyState(VirtualKey.Shift);

                if (!CtrlState.HasFlag(CoreVirtualKeyStates.Down) && !ShiftState.HasFlag(CoreVirtualKeyStates.Down))
                {
                    await EnterSelectedItem(ReFile).ConfigureAwait(false);
                }
            }
        }

        public async Task EnterSelectedItem(string Path, bool RunAsAdministrator = false)
        {
            FileSystemStorageItemBase Item = await FileSystemStorageItemBase.OpenAsync(Path).ConfigureAwait(true);

            await EnterSelectedItem(Item, RunAsAdministrator).ConfigureAwait(false);
        }

        public async Task EnterSelectedItem(FileSystemStorageItemBase ReFile, bool RunAsAdministrator = false)
        {
            if (Interlocked.Exchange(ref TabTarget, ReFile) == null)
            {
                try
                {
                    if (TabTarget.StorageType == StorageItemTypes.File)
                    {
                        if (!await FileSystemStorageItemBase.CheckExist(TabTarget.Path).ConfigureAwait(true))
                        {
                            QueueContentDialog Dialog = new QueueContentDialog
                            {
                                Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                Content = Globalization.GetString("QueueDialog_LocateFileFailure_Content"),
                                CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                            };

                            _ = await Dialog.ShowAsync().ConfigureAwait(true);

                            return;
                        }

                        string AdminExecutablePath = await SQLite.Current.GetDefaultProgramPickerRecordAsync(TabTarget.Type).ConfigureAwait(true);

                        using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableController())
                        {
                            if (!string.IsNullOrEmpty(AdminExecutablePath) && AdminExecutablePath != Package.Current.Id.FamilyName)
                            {
                                if (Path.IsPathRooted(AdminExecutablePath))
                                {
                                    try
                                    {
                                        await Exclusive.Controller.RunAsync(AdminExecutablePath, false, false, false, TabTarget.Path).ConfigureAwait(true);
                                    }
                                    catch (InvalidOperationException)
                                    {
                                        QueueContentDialog UnauthorizeDialog = new QueueContentDialog
                                        {
                                            Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                            Content = Globalization.GetString("QueueDialog_UnauthorizedExecute_Content"),
                                            PrimaryButtonText = Globalization.GetString("Common_Dialog_ConfirmButton"),
                                            CloseButtonText = Globalization.GetString("Common_Dialog_CancelButton")
                                        };

                                        if (await UnauthorizeDialog.ShowAsync().ConfigureAwait(true) == ContentDialogResult.Primary)
                                        {
                                            await Launcher.LaunchFolderPathAsync(CurrentFolder.Path);
                                        }
                                    }
                                }
                                else
                                {
                                    if ((await Launcher.FindFileHandlersAsync(TabTarget.Type)).FirstOrDefault((Item) => Item.PackageFamilyName == AdminExecutablePath) is AppInfo Info)
                                    {
                                        if (await TabTarget.GetStorageItemAsync().ConfigureAwait(true) is StorageFile File)
                                        {
                                            if (!await Launcher.LaunchFileAsync(File, new LauncherOptions { TargetApplicationPackageFamilyName = Info.PackageFamilyName, DisplayApplicationPicker = false }))
                                            {
                                                ProgramPickerDialog Dialog = new ProgramPickerDialog(TabTarget);

                                                if (await Dialog.ShowAsync().ConfigureAwait(true) == ContentDialogResult.Primary)
                                                {
                                                    if (Dialog.SelectedProgram.Path == Package.Current.Id.FamilyName)
                                                    {
                                                        switch (TabTarget.Type.ToLower())
                                                        {
                                                            case ".jpg":
                                                            case ".png":
                                                            case ".bmp":
                                                                {
                                                                    if (AnimationController.Current.IsEnableAnimation)
                                                                    {
                                                                        Container.Frame.Navigate(typeof(PhotoViewer), TabTarget.Path, new DrillInNavigationTransitionInfo());
                                                                    }
                                                                    else
                                                                    {
                                                                        Container.Frame.Navigate(typeof(PhotoViewer), TabTarget.Path, new SuppressNavigationTransitionInfo());
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
                                                                        Container.Frame.Navigate(typeof(MediaPlayer), File, new DrillInNavigationTransitionInfo());
                                                                    }
                                                                    else
                                                                    {
                                                                        Container.Frame.Navigate(typeof(MediaPlayer), File, new SuppressNavigationTransitionInfo());
                                                                    }
                                                                    break;
                                                                }
                                                            case ".txt":
                                                                {
                                                                    if (AnimationController.Current.IsEnableAnimation)
                                                                    {
                                                                        Container.Frame.Navigate(typeof(TextViewer), File, new DrillInNavigationTransitionInfo());
                                                                    }
                                                                    else
                                                                    {
                                                                        Container.Frame.Navigate(typeof(TextViewer), File, new SuppressNavigationTransitionInfo());
                                                                    }
                                                                    break;
                                                                }
                                                            case ".pdf":
                                                                {
                                                                    if (AnimationController.Current.IsEnableAnimation)
                                                                    {
                                                                        Container.Frame.Navigate(typeof(PdfReader), File, new DrillInNavigationTransitionInfo());
                                                                    }
                                                                    else
                                                                    {
                                                                        Container.Frame.Navigate(typeof(PdfReader), File, new SuppressNavigationTransitionInfo());
                                                                    }
                                                                    break;
                                                                }
                                                        }
                                                    }
                                                    else
                                                    {
                                                        if (Path.IsPathRooted(Dialog.SelectedProgram.Path))
                                                        {
                                                            await Exclusive.Controller.RunAsync(Dialog.SelectedProgram.Path, false, false, false, TabTarget.Path).ConfigureAwait(true);
                                                        }
                                                        else
                                                        {
                                                            if (!await Launcher.LaunchFileAsync(File, new LauncherOptions { TargetApplicationPackageFamilyName = Dialog.SelectedProgram.Path, DisplayApplicationPicker = false }))
                                                            {
                                                                if (ApplicationData.Current.LocalSettings.Values["AdminProgramForExcute"] is string ProgramExcute1)
                                                                {
                                                                    ApplicationData.Current.LocalSettings.Values["AdminProgramForExcute"] = ProgramExcute1.Replace($"{TabTarget.Type}|{TabTarget.Name};", string.Empty);
                                                                }

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
                                        }
                                        else
                                        {
                                            QueueContentDialog Dialog = new QueueContentDialog
                                            {
                                                Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                                Content = Globalization.GetString("QueueDialog_UnableAccessFile_Content"),
                                                CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                                            };

                                            _ = await Dialog.ShowAsync().ConfigureAwait(true);
                                        }
                                    }
                                    else
                                    {
                                        ProgramPickerDialog Dialog = new ProgramPickerDialog(TabTarget);

                                        if (await Dialog.ShowAsync().ConfigureAwait(true) == ContentDialogResult.Primary)
                                        {
                                            if (Dialog.SelectedProgram.Path == Package.Current.Id.FamilyName)
                                            {
                                                switch (TabTarget.Type.ToLower())
                                                {
                                                    case ".jpg":
                                                    case ".png":
                                                    case ".bmp":
                                                        {
                                                            if (AnimationController.Current.IsEnableAnimation)
                                                            {
                                                                Container.Frame.Navigate(typeof(PhotoViewer), TabTarget.Path, new DrillInNavigationTransitionInfo());
                                                            }
                                                            else
                                                            {
                                                                Container.Frame.Navigate(typeof(PhotoViewer), TabTarget.Path, new SuppressNavigationTransitionInfo());
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
                                                        {
                                                            if (AnimationController.Current.IsEnableAnimation)
                                                            {
                                                                Container.Frame.Navigate(typeof(MediaPlayer), TabTarget, new DrillInNavigationTransitionInfo());
                                                            }
                                                            else
                                                            {
                                                                Container.Frame.Navigate(typeof(MediaPlayer), TabTarget, new SuppressNavigationTransitionInfo());
                                                            }
                                                            break;
                                                        }
                                                    case ".txt":
                                                        {
                                                            if (AnimationController.Current.IsEnableAnimation)
                                                            {
                                                                Container.Frame.Navigate(typeof(TextViewer), TabTarget, new DrillInNavigationTransitionInfo());
                                                            }
                                                            else
                                                            {
                                                                Container.Frame.Navigate(typeof(TextViewer), TabTarget, new SuppressNavigationTransitionInfo());
                                                            }
                                                            break;
                                                        }
                                                    case ".pdf":
                                                        {
                                                            if (AnimationController.Current.IsEnableAnimation)
                                                            {
                                                                Container.Frame.Navigate(typeof(PdfReader), TabTarget, new DrillInNavigationTransitionInfo());
                                                            }
                                                            else
                                                            {
                                                                Container.Frame.Navigate(typeof(PdfReader), TabTarget, new SuppressNavigationTransitionInfo());
                                                            }
                                                            break;
                                                        }
                                                }
                                            }
                                            else
                                            {
                                                if (Path.IsPathRooted(Dialog.SelectedProgram.Path))
                                                {
                                                    await Exclusive.Controller.RunAsync(Dialog.SelectedProgram.Path, false, false, false, TabTarget.Path).ConfigureAwait(true);
                                                }
                                                else
                                                {
                                                    if (await TabTarget.GetStorageItemAsync().ConfigureAwait(true) is StorageFile File)
                                                    {
                                                        if (!await Launcher.LaunchFileAsync(File, new LauncherOptions { TargetApplicationPackageFamilyName = Dialog.SelectedProgram.Path, DisplayApplicationPicker = false }))
                                                        {
                                                            if (ApplicationData.Current.LocalSettings.Values["AdminProgramForExcute"] is string ProgramExcute1)
                                                            {
                                                                ApplicationData.Current.LocalSettings.Values["AdminProgramForExcute"] = ProgramExcute1.Replace($"{TabTarget.Type}|{TabTarget.Name};", string.Empty);
                                                            }

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
                                                    else
                                                    {
                                                        QueueContentDialog dialog = new QueueContentDialog
                                                        {
                                                            Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                                            Content = Globalization.GetString("QueueDialog_UnableAccessFile_Content"),
                                                            CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                                                        };

                                                        _ = await dialog.ShowAsync().ConfigureAwait(true);
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                            else
                            {
                                switch (TabTarget.Type.ToLower())
                                {
                                    case ".jpg":
                                    case ".png":
                                    case ".bmp":
                                        {
                                            if (AnimationController.Current.IsEnableAnimation)
                                            {
                                                Container.Frame.Navigate(typeof(PhotoViewer), TabTarget.Path, new DrillInNavigationTransitionInfo());
                                            }
                                            else
                                            {
                                                Container.Frame.Navigate(typeof(PhotoViewer), TabTarget.Path, new SuppressNavigationTransitionInfo());
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
                                        {
                                            if (AnimationController.Current.IsEnableAnimation)
                                            {
                                                Container.Frame.Navigate(typeof(MediaPlayer), TabTarget, new DrillInNavigationTransitionInfo());
                                            }
                                            else
                                            {
                                                Container.Frame.Navigate(typeof(MediaPlayer), TabTarget, new SuppressNavigationTransitionInfo());
                                            }
                                            break;
                                        }
                                    case ".txt":
                                        {
                                            if (AnimationController.Current.IsEnableAnimation)
                                            {
                                                Container.Frame.Navigate(typeof(TextViewer), TabTarget, new DrillInNavigationTransitionInfo());
                                            }
                                            else
                                            {
                                                Container.Frame.Navigate(typeof(TextViewer), TabTarget, new SuppressNavigationTransitionInfo());
                                            }
                                            break;
                                        }
                                    case ".pdf":
                                        {
                                            if (AnimationController.Current.IsEnableAnimation)
                                            {
                                                Container.Frame.Navigate(typeof(PdfReader), TabTarget, new DrillInNavigationTransitionInfo());
                                            }
                                            else
                                            {
                                                Container.Frame.Navigate(typeof(PdfReader), TabTarget, new SuppressNavigationTransitionInfo());
                                            }
                                            break;
                                        }
                                    case ".lnk":
                                        {
                                            if (TabTarget is HyperlinkStorageItem Item)
                                            {
                                                if (WIN_Native_API.CheckType(Item.LinkTargetPath) == StorageItemTypes.Folder)
                                                {
                                                    await DisplayItemsInFolder(Item.LinkTargetPath).ConfigureAwait(true);
                                                }
                                                else
                                                {
                                                    await Item.LaunchAsync().ConfigureAwait(true);
                                                }
                                            }

                                            break;
                                        }
                                    case ".exe":
                                    case ".bat":
                                    case ".msi":
                                        {
                                            await Exclusive.Controller.RunAsync(TabTarget.Path, RunAsAdministrator).ConfigureAwait(true);

                                            break;
                                        }
                                    case ".msc":
                                        {
                                            await Exclusive.Controller.RunAsync("powershell.exe", false, true, false, "-Command", TabTarget.Path).ConfigureAwait(true);

                                            break;
                                        }
                                    default:
                                        {
                                            ProgramPickerDialog Dialog = new ProgramPickerDialog(TabTarget);

                                            if (await Dialog.ShowAsync().ConfigureAwait(true) == ContentDialogResult.Primary)
                                            {
                                                if (Dialog.SelectedProgram.Path == Package.Current.Id.FamilyName)
                                                {
                                                    switch (TabTarget.Type.ToLower())
                                                    {
                                                        case ".jpg":
                                                        case ".png":
                                                        case ".bmp":
                                                            {
                                                                if (AnimationController.Current.IsEnableAnimation)
                                                                {
                                                                    Container.Frame.Navigate(typeof(PhotoViewer), TabTarget.Path, new DrillInNavigationTransitionInfo());
                                                                }
                                                                else
                                                                {
                                                                    Container.Frame.Navigate(typeof(PhotoViewer), TabTarget.Path, new SuppressNavigationTransitionInfo());
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
                                                            {
                                                                if (AnimationController.Current.IsEnableAnimation)
                                                                {
                                                                    Container.Frame.Navigate(typeof(MediaPlayer), TabTarget, new DrillInNavigationTransitionInfo());
                                                                }
                                                                else
                                                                {
                                                                    Container.Frame.Navigate(typeof(MediaPlayer), TabTarget, new SuppressNavigationTransitionInfo());
                                                                }
                                                                break;
                                                            }
                                                        case ".txt":
                                                            {
                                                                if (AnimationController.Current.IsEnableAnimation)
                                                                {
                                                                    Container.Frame.Navigate(typeof(TextViewer), TabTarget, new DrillInNavigationTransitionInfo());
                                                                }
                                                                else
                                                                {
                                                                    Container.Frame.Navigate(typeof(TextViewer), TabTarget, new SuppressNavigationTransitionInfo());
                                                                }
                                                                break;
                                                            }
                                                        case ".pdf":
                                                            {
                                                                if (AnimationController.Current.IsEnableAnimation)
                                                                {
                                                                    Container.Frame.Navigate(typeof(PdfReader), TabTarget, new DrillInNavigationTransitionInfo());
                                                                }
                                                                else
                                                                {
                                                                    Container.Frame.Navigate(typeof(PdfReader), TabTarget, new SuppressNavigationTransitionInfo());
                                                                }
                                                                break;
                                                            }
                                                    }
                                                }
                                                else
                                                {
                                                    if (Path.IsPathRooted(Dialog.SelectedProgram.Path))
                                                    {
                                                        await Exclusive.Controller.RunAsync(Dialog.SelectedProgram.Path, false, false, false, TabTarget.Path).ConfigureAwait(true);
                                                    }
                                                    else
                                                    {
                                                        if (await TabTarget.GetStorageItemAsync().ConfigureAwait(true) is StorageFile File)
                                                        {
                                                            if (!await Launcher.LaunchFileAsync(File, new LauncherOptions { TargetApplicationPackageFamilyName = Dialog.SelectedProgram.Path, DisplayApplicationPicker = false }))
                                                            {
                                                                if (ApplicationData.Current.LocalSettings.Values["AdminProgramForExcute"] is string ProgramExcute1)
                                                                {
                                                                    ApplicationData.Current.LocalSettings.Values["AdminProgramForExcute"] = ProgramExcute1.Replace($"{TabTarget.Type}|{TabTarget.Name};", string.Empty);
                                                                }

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
                                                        else
                                                        {
                                                            QueueContentDialog dialog = new QueueContentDialog
                                                            {
                                                                Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                                                Content = Globalization.GetString("QueueDialog_UnableAccessFile_Content"),
                                                                CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                                                            };

                                                            _ = await dialog.ShowAsync().ConfigureAwait(true);
                                                        }
                                                    }
                                                }
                                            }
                                            break;
                                        }
                                }
                            }
                        }
                    }
                    else
                    {
                        if (await FileSystemStorageItemBase.CheckExist(TabTarget.Path).ConfigureAwait(true))
                        {
                            await DisplayItemsInFolder(TabTarget).ConfigureAwait(true);
                        }
                        else
                        {
                            QueueContentDialog Dialog = new QueueContentDialog
                            {
                                Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                Content = Globalization.GetString("QueueDialog_LocateFolderFailure_Content"),
                                CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                            };

                            _ = await Dialog.ShowAsync().ConfigureAwait(true);
                        }
                    }
                }
                catch (InvalidOperationException)
                {
                    QueueContentDialog UnauthorizeDialog = new QueueContentDialog
                    {
                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                        Content = Globalization.GetString("QueueDialog_UnauthorizedExecute_Content"),
                        PrimaryButtonText = Globalization.GetString("Common_Dialog_ConfirmButton"),
                        CloseButtonText = Globalization.GetString("Common_Dialog_CancelButton")
                    };

                    if (await UnauthorizeDialog.ShowAsync().ConfigureAwait(true) == ContentDialogResult.Primary)
                    {
                        await Launcher.LaunchFolderPathAsync(CurrentFolder.Path);
                    }
                }
                catch (Exception ex)
                {
                    LogTracer.Log(ex, $"{nameof(EnterSelectedItem)} throw an exception");
                }
                finally
                {
                    Interlocked.Exchange(ref TabTarget, null);
                }
            }
        }

        private async void VideoEdit_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();

            if (GeneralTransformer.IsAnyTransformTaskRunning)
            {
                QueueContentDialog Dialog = new QueueContentDialog
                {
                    Title = Globalization.GetString("Common_Dialog_TipTitle"),
                    Content = Globalization.GetString("QueueDialog_TaskWorking_Content"),
                    CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                };

                _ = await Dialog.ShowAsync().ConfigureAwait(true);
            }
            else
            {
                if ((await SelectedItem.GetStorageItemAsync().ConfigureAwait(true)) is StorageFile File)
                {
                    VideoEditDialog Dialog = new VideoEditDialog(File);

                    if ((await Dialog.ShowAsync().ConfigureAwait(true)) == ContentDialogResult.Primary)
                    {
                        if (await CurrentFolder.GetStorageItemAsync().ConfigureAwait(true) is StorageFolder Folder)
                        {
                            StorageFile ExportFile = await Folder.CreateFileAsync($"{File.DisplayName} - {Globalization.GetString("Crop_Image_Name_Tail")}{Dialog.ExportFileType}", CreationCollisionOption.GenerateUniqueName);
                            await GeneralTransformer.GenerateCroppedVideoFromOriginAsync(ExportFile, Dialog.Composition, Dialog.MediaEncoding, Dialog.TrimmingPreference).ConfigureAwait(true);
                        }
                    }
                }
                else
                {
                    QueueContentDialog Dialog = new QueueContentDialog
                    {
                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                        Content = Globalization.GetString("QueueDialog_UnableAccessFile_Content"),
                        CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                    };

                    _ = await Dialog.ShowAsync().ConfigureAwait(true);
                }
            }
        }

        private async void VideoMerge_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();

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

            if ((await SelectedItem.GetStorageItemAsync().ConfigureAwait(true)) is StorageFile Item)
            {
                VideoMergeDialog Dialog = new VideoMergeDialog(Item);

                if ((await Dialog.ShowAsync().ConfigureAwait(true)) == ContentDialogResult.Primary)
                {
                    if (await CurrentFolder.GetStorageItemAsync().ConfigureAwait(true) is StorageFolder Folder)
                    {
                        StorageFile ExportFile = await Folder.CreateFileAsync($"{Item.DisplayName} - {Globalization.GetString("Merge_Image_Name_Tail")}{Dialog.ExportFileType}", CreationCollisionOption.GenerateUniqueName);

                        await GeneralTransformer.GenerateMergeVideoFromOriginAsync(ExportFile, Dialog.Composition, Dialog.MediaEncoding).ConfigureAwait(true);
                    }
                }
            }
        }

        private async void ChooseOtherApp_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();

            ProgramPickerDialog Dialog = new ProgramPickerDialog(SelectedItem);

            if (await Dialog.ShowAsync().ConfigureAwait(true) == ContentDialogResult.Primary)
            {
                if (Dialog.SelectedProgram.Path == Package.Current.Id.FamilyName)
                {
                    switch (SelectedItem.Type.ToLower())
                    {
                        case ".jpg":
                        case ".png":
                        case ".bmp":
                            {
                                if (AnimationController.Current.IsEnableAnimation)
                                {
                                    Container.Frame.Navigate(typeof(PhotoViewer), SelectedItem.Path, new DrillInNavigationTransitionInfo());
                                }
                                else
                                {
                                    Container.Frame.Navigate(typeof(PhotoViewer), SelectedItem.Path, new SuppressNavigationTransitionInfo());
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
                            {
                                if (AnimationController.Current.IsEnableAnimation)
                                {
                                    Container.Frame.Navigate(typeof(MediaPlayer), SelectedItem, new DrillInNavigationTransitionInfo());
                                }
                                else
                                {
                                    Container.Frame.Navigate(typeof(MediaPlayer), SelectedItem, new SuppressNavigationTransitionInfo());
                                }
                                break;
                            }
                        case ".txt":
                            {
                                if (AnimationController.Current.IsEnableAnimation)
                                {
                                    Container.Frame.Navigate(typeof(TextViewer), SelectedItem, new DrillInNavigationTransitionInfo());
                                }
                                else
                                {
                                    Container.Frame.Navigate(typeof(TextViewer), SelectedItem, new SuppressNavigationTransitionInfo());
                                }
                                break;
                            }
                        case ".pdf":
                            {
                                if (AnimationController.Current.IsEnableAnimation)
                                {
                                    Container.Frame.Navigate(typeof(PdfReader), SelectedItem, new DrillInNavigationTransitionInfo());
                                }
                                else
                                {
                                    Container.Frame.Navigate(typeof(PdfReader), SelectedItem, new SuppressNavigationTransitionInfo());
                                }
                                break;
                            }
                    }
                }
                else
                {
                    if (Path.IsPathRooted(Dialog.SelectedProgram.Path))
                    {
                        using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableController())
                        {
                            try
                            {
                                await Exclusive.Controller.RunAsync(Dialog.SelectedProgram.Path, false, false, false, SelectedItem.Path).ConfigureAwait(true);
                            }
                            catch (InvalidOperationException)
                            {
                                QueueContentDialog UnauthorizeDialog = new QueueContentDialog
                                {
                                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                    Content = Globalization.GetString("QueueDialog_UnauthorizedExecute_Content"),
                                    PrimaryButtonText = Globalization.GetString("Common_Dialog_ConfirmButton"),
                                    CloseButtonText = Globalization.GetString("Common_Dialog_CancelButton")
                                };

                                if (await UnauthorizeDialog.ShowAsync().ConfigureAwait(true) == ContentDialogResult.Primary)
                                {
                                    await Launcher.LaunchFolderPathAsync(CurrentFolder.Path);
                                }
                            }
                        }
                    }
                    else
                    {
                        if ((await SelectedItem.GetStorageItemAsync().ConfigureAwait(true)) is StorageFile Item)
                        {
                            if (!await Launcher.LaunchFileAsync(Item, new LauncherOptions { TargetApplicationPackageFamilyName = Dialog.SelectedProgram.Path, DisplayApplicationPicker = false }))
                            {
                                if (ApplicationData.Current.LocalSettings.Values["AdminProgramForExcute"] is string ProgramExcute)
                                {
                                    ApplicationData.Current.LocalSettings.Values["AdminProgramForExcute"] = ProgramExcute.Replace($"{Item.FileType}|{Item.Name};", string.Empty);
                                }

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
                                        LauncherOptions Options = new LauncherOptions
                                        {
                                            DisplayApplicationPicker = true
                                        };

                                        _ = await Launcher.LaunchFileAsync(Item, Options);
                                    }
                                }
                            }
                        }
                        else
                        {
                            QueueContentDialog dialog = new QueueContentDialog
                            {
                                Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                Content = Globalization.GetString("QueueDialog_UnableAccessFile_Content"),
                                CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                            };

                            _ = await dialog.ShowAsync().ConfigureAwait(true);
                        }
                    }
                }
            }
        }

        private async void RunWithSystemAuthority_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();

            if (SelectedItem != null)
            {
                await EnterSelectedItem(SelectedItem, true).ConfigureAwait(false);
            }
        }

        private async void ListHeaderName_Click(object sender, RoutedEventArgs e)
        {
            if (SortCollectionGenerator.Current.SortDirection == SortDirection.Ascending)
            {
                await SortCollectionGenerator.Current.ModifySortWayAsync(CurrentFolder.Path, SortTarget.Name, SortDirection.Descending).ConfigureAwait(true);
            }
            else
            {
                await SortCollectionGenerator.Current.ModifySortWayAsync(CurrentFolder.Path, SortTarget.Name, SortDirection.Ascending).ConfigureAwait(true);
            }
        }

        private async void ListHeaderModifiedTime_Click(object sender, RoutedEventArgs e)
        {
            if (SortCollectionGenerator.Current.SortDirection == SortDirection.Ascending)
            {
                await SortCollectionGenerator.Current.ModifySortWayAsync(CurrentFolder.Path, SortTarget.ModifiedTime, SortDirection.Descending).ConfigureAwait(true);
            }
            else
            {
                await SortCollectionGenerator.Current.ModifySortWayAsync(CurrentFolder.Path, SortTarget.ModifiedTime, SortDirection.Ascending).ConfigureAwait(true);
            }
        }

        private async void ListHeaderType_Click(object sender, RoutedEventArgs e)
        {
            if (SortCollectionGenerator.Current.SortDirection == SortDirection.Ascending)
            {
                await SortCollectionGenerator.Current.ModifySortWayAsync(CurrentFolder.Path, SortTarget.Type, SortDirection.Descending).ConfigureAwait(true);
            }
            else
            {
                await SortCollectionGenerator.Current.ModifySortWayAsync(CurrentFolder.Path, SortTarget.Type, SortDirection.Ascending).ConfigureAwait(true);
            }
        }

        private async void ListHeaderSize_Click(object sender, RoutedEventArgs e)
        {
            if (SortCollectionGenerator.Current.SortDirection == SortDirection.Ascending)
            {
                await SortCollectionGenerator.Current.ModifySortWayAsync(CurrentFolder.Path, SortTarget.Size, SortDirection.Descending).ConfigureAwait(true);
            }
            else
            {
                await SortCollectionGenerator.Current.ModifySortWayAsync(CurrentFolder.Path, SortTarget.Size, SortDirection.Ascending).ConfigureAwait(true);
            }
        }

        private void QRTeachTip_Closing(TeachingTip sender, TeachingTipClosingEventArgs args)
        {
            QRImage.Source = null;
            WiFiProvider.Dispose();
            WiFiProvider = null;
        }

        private async void CreateFile_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();

            NewFileDialog Dialog = new NewFileDialog();

            if ((await Dialog.ShowAsync().ConfigureAwait(true)) == ContentDialogResult.Primary)
            {
                try
                {
                    switch (Path.GetExtension(Dialog.NewFileName))
                    {
                        case ".zip":
                            {
                                await SpecialTypeGenerator.Current.CreateZipFile(CurrentFolder.Path, Dialog.NewFileName).ConfigureAwait(true);
                                break;
                            }
                        case ".rtf":
                            {
                                await SpecialTypeGenerator.Current.CreateRtfFile(CurrentFolder.Path, Dialog.NewFileName).ConfigureAwait(true);
                                break;
                            }
                        case ".xlsx":
                            {
                                await SpecialTypeGenerator.Current.CreateExcelFile(CurrentFolder.Path, Dialog.NewFileName).ConfigureAwait(true);
                                break;
                            }
                        case ".lnk":
                            {
                                LinkOptionsDialog dialog = new LinkOptionsDialog();

                                if (await dialog.ShowAsync().ConfigureAwait(true) == ContentDialogResult.Primary)
                                {
                                    using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableController())
                                    {
                                        if (!await Exclusive.Controller.CreateLinkAsync(Path.Combine(CurrentFolder.Path, Dialog.NewFileName), dialog.Path, dialog.Description, dialog.Arguments).ConfigureAwait(true))
                                        {
                                            throw new UnauthorizedAccessException();
                                        }
                                    }
                                }

                                break;
                            }
                        default:
                            {
                                if (await FileSystemStorageItemBase.CreateAsync(Path.Combine(CurrentFolder.Path, Dialog.NewFileName), StorageItemTypes.File, CreateOption.GenerateUniqueName).ConfigureAwait(true) == null)
                                {
                                    throw new UnauthorizedAccessException();
                                }

                                break;
                            }
                    }
                }
                catch (Exception)
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
                        _ = await Launcher.LaunchFolderPathAsync(CurrentFolder.Path);
                    }
                }
            }
        }

        private async void CompressFolder_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();

            if (SelectedItem is FileSystemStorageItemBase Item)
            {
                if (!await FileSystemStorageItemBase.CheckExist(Item.Path).ConfigureAwait(true))
                {
                    QueueContentDialog Dialog = new QueueContentDialog
                    {
                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                        Content = Globalization.GetString("QueueDialog_LocateFolderFailure_Content"),
                        CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                    };

                    _ = await Dialog.ShowAsync().ConfigureAwait(true);

                    return;
                }

                CompressDialog dialog = new CompressDialog(false, Path.GetFileNameWithoutExtension(Item.Path));

                if ((await dialog.ShowAsync().ConfigureAwait(true)) == ContentDialogResult.Primary)
                {
                    await Container.LoadingActivation(true, Globalization.GetString("Progress_Tip_Compressing")).ConfigureAwait(true);

                    try
                    {
                        switch (dialog.Type)
                        {
                            case CompressionType.Zip:
                                {
                                    await CompressionUtil.CreateZipAsync(Item, Path.Combine(CurrentFolder.Path, dialog.FileName), (int)dialog.Level, async (s, e) =>
                                    {
                                        await Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                                        {
                                            if (Container.ProBar.Value < e.ProgressPercentage)
                                            {
                                                Container.ProBar.IsIndeterminate = false;
                                                Container.ProBar.Value = e.ProgressPercentage;
                                            }
                                        });
                                    }).ConfigureAwait(true);

                                    break;
                                }
                            case CompressionType.Tar:
                                {
                                    await CompressionUtil.CreateTarAsync(Item, Path.Combine(CurrentFolder.Path, dialog.FileName), async (s, e) =>
                                    {
                                        await Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                                        {
                                            if (Container.ProBar.Value < e.ProgressPercentage)
                                            {
                                                Container.ProBar.IsIndeterminate = false;
                                                Container.ProBar.Value = e.ProgressPercentage;
                                            }
                                        });
                                    }).ConfigureAwait(true);

                                    break;
                                }
                        }
                    }
                    catch (UnauthorizedAccessException)
                    {
                        QueueContentDialog dialog1 = new QueueContentDialog
                        {
                            Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                            Content = Globalization.GetString("QueueDialog_UnauthorizedCompression_Content"),
                            PrimaryButtonText = Globalization.GetString("Common_Dialog_NowButton"),
                            CloseButtonText = Globalization.GetString("Common_Dialog_LaterButton")
                        };

                        if (await dialog1.ShowAsync().ConfigureAwait(true) == ContentDialogResult.Primary)
                        {
                            _ = await Launcher.LaunchFolderPathAsync(CurrentFolder.Path);
                        }
                    }
                    catch (Exception ex)
                    {
                        QueueContentDialog dialog2 = new QueueContentDialog
                        {
                            Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                            Content = Globalization.GetString("QueueDialog_CompressionError_Content") + ex.Message,
                            CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                        };

                        _ = await dialog2.ShowAsync().ConfigureAwait(true);
                    }

                    await Container.LoadingActivation(false).ConfigureAwait(true);
                }
            }
        }

        private async void ViewControl_DragOver(object sender, DragEventArgs e)
        {
            var Deferral = e.GetDeferral();

            try
            {
                Container.CurrentPresenter = this;

                if (e.DataView.Contains(StandardDataFormats.StorageItems))
                {
                    if (e.Modifiers.HasFlag(DragDropModifiers.Control))
                    {
                        e.AcceptedOperation = DataPackageOperation.Move;
                        e.DragUIOverride.Caption = $"{Globalization.GetString("Drag_Tip_MoveTo")} {CurrentFolder.Name}";
                    }
                    else
                    {
                        e.AcceptedOperation = DataPackageOperation.Copy;
                        e.DragUIOverride.Caption = $"{Globalization.GetString("Drag_Tip_CopyTo")} {CurrentFolder.Name}";
                    }

                    e.DragUIOverride.IsContentVisible = true;
                    e.DragUIOverride.IsCaptionVisible = true;
                }
                else if (e.DataView.Contains(StandardDataFormats.Text))
                {
                    string XmlText = await e.DataView.GetTextAsync();

                    if (XmlText.Contains("RX-Explorer"))
                    {
                        XmlDocument Document = new XmlDocument();
                        Document.LoadXml(XmlText);

                        IXmlNode KindNode = Document.SelectSingleNode("/RX-Explorer/Kind");

                        if (KindNode?.InnerText == "RX-Explorer-TransferNotStorageItem")
                        {
                            if (e.Modifiers.HasFlag(DragDropModifiers.Control))
                            {
                                e.AcceptedOperation = DataPackageOperation.Copy;
                                e.DragUIOverride.Caption = $"{Globalization.GetString("Drag_Tip_CopyTo")} {CurrentFolder.Name}";
                            }
                            else
                            {
                                e.AcceptedOperation = DataPackageOperation.Move;
                                e.DragUIOverride.Caption = $"{Globalization.GetString("Drag_Tip_MoveTo")} {CurrentFolder.Name}";
                            }
                        }
                        else
                        {
                            e.AcceptedOperation = DataPackageOperation.None;
                        }
                    }
                    else
                    {
                        e.AcceptedOperation = DataPackageOperation.None;
                    }
                }
                else
                {
                    e.AcceptedOperation = DataPackageOperation.None;
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex);
            }
            finally
            {
                Deferral.Complete();
            }
        }

        private async void ItemContainer_Drop(object sender, DragEventArgs e)
        {
            DragOperationDeferral Deferral = e.GetDeferral();

            try
            {
                List<string> PathList = new List<string>();

                if (e.DataView.Contains(StandardDataFormats.StorageItems))
                {
                    IReadOnlyList<IStorageItem> ItemList = await e.DataView.GetStorageItemsAsync();
                    PathList.AddRange(ItemList.Select((Item) => Item.Path));
                }

                if (e.DataView.Contains(StandardDataFormats.Text))
                {
                    string XmlText = await e.DataView.GetTextAsync();

                    if (XmlText.Contains("RX-Explorer"))
                    {
                        XmlDocument Document = new XmlDocument();
                        Document.LoadXml(XmlText);

                        IXmlNode KindNode = Document.SelectSingleNode("/RX-Explorer/Kind");

                        if (KindNode?.InnerText == "RX-Explorer-TransferNotStorageItem")
                        {
                            PathList.AddRange(Document.SelectNodes("/RX-Explorer/Item").Select((Node) => Node.InnerText));
                        }
                    }
                }

                if (PathList.Count > 0)
                {
                    if ((sender as SelectorItem).Content is FileSystemStorageItemBase Item)
                    {
                        using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableController())
                        {
                            switch (e.AcceptedOperation)
                            {
                                case DataPackageOperation.Copy:
                                    {
                                        await Container.LoadingActivation(true, Globalization.GetString("Progress_Tip_Copying")).ConfigureAwait(true);

                                        try
                                        {
                                            await Exclusive.Controller.CopyAsync(PathList, Item.Path, (s, arg) =>
                                            {
                                                if (Container.ProBar.Value < arg.ProgressPercentage)
                                                {
                                                    Container.ProBar.IsIndeterminate = false;
                                                    Container.ProBar.Value = arg.ProgressPercentage;
                                                }
                                            }).ConfigureAwait(true);
                                        }
                                        catch (FileNotFoundException)
                                        {
                                            QueueContentDialog Dialog = new QueueContentDialog
                                            {
                                                Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                                Content = Globalization.GetString("QueueDialog_CopyFailForNotExist_Content"),
                                                CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                                            };

                                            _ = await Dialog.ShowAsync().ConfigureAwait(true);
                                        }
                                        catch (InvalidOperationException)
                                        {
                                            QueueContentDialog dialog = new QueueContentDialog
                                            {
                                                Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                                Content = Globalization.GetString("QueueDialog_UnauthorizedPaste_Content"),
                                                PrimaryButtonText = Globalization.GetString("Common_Dialog_ConfirmButton"),
                                                CloseButtonText = Globalization.GetString("Common_Dialog_CancelButton")
                                            };

                                            if (await dialog.ShowAsync().ConfigureAwait(true) == ContentDialogResult.Primary)
                                            {
                                                await Launcher.LaunchFolderPathAsync(CurrentFolder.Path);
                                            }
                                        }
                                        catch (Exception)
                                        {
                                            QueueContentDialog dialog = new QueueContentDialog
                                            {
                                                Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                                Content = Globalization.GetString("QueueDialog_CopyFailUnexpectError_Content"),
                                                CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                                            };

                                            _ = await dialog.ShowAsync().ConfigureAwait(true);
                                        }

                                        break;
                                    }
                                case DataPackageOperation.Move:
                                    {
                                        await Container.LoadingActivation(true, Globalization.GetString("Progress_Tip_Moving")).ConfigureAwait(true);

                                        try
                                        {
                                            await Exclusive.Controller.MoveAsync(PathList, Item.Path, (s, arg) =>
                                            {
                                                if (Container.ProBar.Value < arg.ProgressPercentage)
                                                {
                                                    Container.ProBar.IsIndeterminate = false;
                                                    Container.ProBar.Value = arg.ProgressPercentage;
                                                }
                                            }).ConfigureAwait(true);
                                        }
                                        catch (FileNotFoundException)
                                        {
                                            QueueContentDialog Dialog = new QueueContentDialog
                                            {
                                                Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                                Content = Globalization.GetString("QueueDialog_MoveFailForNotExist_Content"),
                                                CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                                            };

                                            _ = await Dialog.ShowAsync().ConfigureAwait(true);
                                        }
                                        catch (FileCaputureException)
                                        {
                                            QueueContentDialog dialog = new QueueContentDialog
                                            {
                                                Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                                Content = Globalization.GetString("QueueDialog_Item_Captured_Content"),
                                                CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                                            };

                                            _ = await dialog.ShowAsync().ConfigureAwait(true);
                                        }
                                        catch (InvalidOperationException)
                                        {
                                            QueueContentDialog dialog = new QueueContentDialog
                                            {
                                                Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                                Content = Globalization.GetString("QueueDialog_UnauthorizedPaste_Content"),
                                                PrimaryButtonText = Globalization.GetString("Common_Dialog_ConfirmButton"),
                                                CloseButtonText = Globalization.GetString("Common_Dialog_CancelButton")
                                            };

                                            if (await dialog.ShowAsync().ConfigureAwait(true) == ContentDialogResult.Primary)
                                            {
                                                await Launcher.LaunchFolderPathAsync(CurrentFolder.Path);
                                            }
                                        }
                                        catch (Exception)
                                        {
                                            QueueContentDialog dialog = new QueueContentDialog
                                            {
                                                Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                                Content = Globalization.GetString("QueueDialog_MoveFailUnexpectError_Content"),
                                                CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                                            };

                                            _ = await dialog.ShowAsync().ConfigureAwait(true);
                                        }

                                        break;
                                    }
                            }
                        }
                    }
                }
            }
            catch (Exception ex) when (ex.HResult == unchecked((int)0x80040064))
            {
                QueueContentDialog dialog = new QueueContentDialog
                {
                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                    Content = Globalization.GetString("QueueDialog_CopyFromUnsupportedArea_Content"),
                    CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                };

                _ = await dialog.ShowAsync().ConfigureAwait(true);
            }
            catch
            {
                QueueContentDialog dialog = new QueueContentDialog
                {
                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                    Content = Globalization.GetString("QueueDialog_FailToGetClipboardError_Content"),
                    CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                };

                _ = await dialog.ShowAsync().ConfigureAwait(true);
            }
            finally
            {
                await Container.LoadingActivation(false).ConfigureAwait(true);
                e.Handled = true;
                Deferral.Complete();
            }
        }

        private void ViewControl_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                args.ItemContainer.DragStarting -= ItemContainer_DragStarting;
                args.ItemContainer.Drop -= ItemContainer_Drop;
                args.ItemContainer.DragOver -= ItemContainer_DragOver;
                args.ItemContainer.PointerEntered -= ItemContainer_PointerEntered;
                args.ItemContainer.DragEnter -= ItemContainer_DragEnter;
            }
            else
            {
                args.ItemContainer.UseSystemFocusVisuals = false;

                if (args.Item is FileSystemStorageItemBase Item)
                {
                    if (Item.StorageType == StorageItemTypes.Folder)
                    {
                        args.ItemContainer.AllowDrop = true;
                        args.ItemContainer.Drop += ItemContainer_Drop;
                        args.ItemContainer.DragOver += ItemContainer_DragOver;
                        args.ItemContainer.DragEnter += ItemContainer_DragEnter;
                        args.ItemContainer.DragLeave += ItemContainer_DragLeave;
                    }

                    args.ItemContainer.DragStarting += ItemContainer_DragStarting;
                    args.ItemContainer.PointerEntered += ItemContainer_PointerEntered;

                    args.RegisterUpdateCallback(async (s, e) =>
                    {
                        if (e.Item is FileSystemStorageItemBase Item)
                        {
                            await Item.LoadMorePropertyAsync().ConfigureAwait(false);
                        }
                    });
                }
            }
        }

        private void ItemContainer_DragLeave(object sender, DragEventArgs e)
        {
            DelayEnterCancel?.Cancel();
        }

        private void ItemContainer_DragEnter(object sender, DragEventArgs e)
        {
            if (sender is SelectorItem Selector && Selector.Content is FileSystemStorageItemBase Item)
            {
                DelayEnterCancel?.Cancel();
                DelayEnterCancel?.Dispose();
                DelayEnterCancel = new CancellationTokenSource();

                Task.Delay(1500).ContinueWith((task, obj) =>
                {
                    try
                    {
                        ValueTuple<CancellationTokenSource, FileSystemStorageItemBase> Tuple = (ValueTuple<CancellationTokenSource, FileSystemStorageItemBase>)obj;

                        if (!Tuple.Item1.IsCancellationRequested)
                        {
                            _ = EnterSelectedItem(Tuple.Item2);
                        }
                    }
                    catch (Exception ex)
                    {
                        LogTracer.Log(ex, "An exception was thew in DelayEnterProcess");
                    }
                }, new ValueTuple<CancellationTokenSource, FileSystemStorageItemBase>(DelayEnterCancel, Item), TaskScheduler.FromCurrentSynchronizationContext());
            }
        }

        private async void ItemContainer_DragStarting(UIElement sender, DragStartingEventArgs args)
        {
            DragOperationDeferral Deferral = args.GetDeferral();

            try
            {
                DelayRenameCancel?.Cancel();

                List<FileSystemStorageItemBase> DragList = SelectedItems;

                foreach (FileSystemStorageItemBase Item in DragList)
                {
                    if (ItemPresenter.ContainerFromItem(Item) is SelectorItem SItem && SItem.ContentTemplateRoot.FindChildOfType<TextBox>() is TextBox NameEditBox)
                    {
                        NameEditBox.Visibility = Visibility.Collapsed;
                    }
                }

                IEnumerable<FileSystemStorageItemBase> StorageItems = DragList.Where((Item) => Item is not (HyperlinkStorageItem or HiddenStorageItem));

                if (StorageItems.Any())
                {
                    List<IStorageItem> TempList = new List<IStorageItem>();

                    foreach (FileSystemStorageItemBase StorageItem in StorageItems)
                    {
                        if (await StorageItem.GetStorageItemAsync().ConfigureAwait(true) is IStorageItem Item)
                        {
                            TempList.Add(Item);
                        }
                    }

                    if (TempList.Count > 0)
                    {
                        args.Data.SetStorageItems(TempList, false);
                    }
                }

                IEnumerable<FileSystemStorageItemBase> NotStorageItems = DragList.Where((Item) => Item is HyperlinkStorageItem or HiddenStorageItem);

                if (NotStorageItems.Any())
                {
                    XmlDocument Document = new XmlDocument();

                    XmlElement RootElemnt = Document.CreateElement("RX-Explorer");
                    Document.AppendChild(RootElemnt);

                    XmlElement KindElement = Document.CreateElement("Kind");
                    KindElement.InnerText = "RX-Explorer-TransferNotStorageItem";
                    RootElemnt.AppendChild(KindElement);

                    foreach (FileSystemStorageItemBase Item in NotStorageItems)
                    {
                        XmlElement InnerElement = Document.CreateElement("Item");
                        InnerElement.InnerText = Item.Path;
                        RootElemnt.AppendChild(InnerElement);
                    }

                    args.Data.SetText(Document.GetXml());
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex);
            }
            finally
            {
                Deferral.Complete();
            }
        }

        private async void ItemContainer_DragOver(object sender, DragEventArgs e)
        {
            var Deferral = e.GetDeferral();

            try
            {
                if ((sender as SelectorItem)?.Content is FileSystemStorageItemBase Item)
                {
                    if (e.DataView.Contains(StandardDataFormats.StorageItems))
                    {
                        if (e.Modifiers.HasFlag(DragDropModifiers.Control))
                        {
                            e.AcceptedOperation = DataPackageOperation.Move;
                            e.DragUIOverride.Caption = $"{Globalization.GetString("Drag_Tip_MoveTo")} {Item.Name}";
                        }
                        else
                        {
                            e.AcceptedOperation = DataPackageOperation.Copy;
                            e.DragUIOverride.Caption = $"{Globalization.GetString("Drag_Tip_CopyTo")} {Item.Name}";
                        }

                        e.DragUIOverride.IsContentVisible = true;
                        e.DragUIOverride.IsCaptionVisible = true;
                    }
                    else if (e.DataView.Contains(StandardDataFormats.Text))
                    {
                        string XmlText = await e.DataView.GetTextAsync();

                        if (XmlText.Contains("RX-Explorer"))
                        {
                            XmlDocument Document = new XmlDocument();
                            Document.LoadXml(XmlText);

                            IXmlNode KindNode = Document.SelectSingleNode("/RX-Explorer/Kind");

                            if (KindNode?.InnerText == "RX-Explorer-TransferNotStorageItem")
                            {
                                if (e.Modifiers.HasFlag(DragDropModifiers.Control))
                                {
                                    e.AcceptedOperation = DataPackageOperation.Move;
                                    e.DragUIOverride.Caption = $"{Globalization.GetString("Drag_Tip_MoveTo")} {Item.Name}";
                                }
                                else
                                {
                                    e.AcceptedOperation = DataPackageOperation.Copy;
                                    e.DragUIOverride.Caption = $"{Globalization.GetString("Drag_Tip_CopyTo")} {Item.Name}";
                                }
                            }
                            else
                            {
                                e.AcceptedOperation = DataPackageOperation.None;
                            }
                        }
                        else
                        {
                            e.AcceptedOperation = DataPackageOperation.None;
                        }
                    }
                    else
                    {
                        e.AcceptedOperation = DataPackageOperation.None;
                    }
                }
                else
                {
                    e.AcceptedOperation = DataPackageOperation.None;
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex);
            }
            finally
            {
                Deferral.Complete();
                e.Handled = true;
            }
        }

        private void ItemContainer_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            if (!SettingControl.IsDoubleClickEnable
                && ItemPresenter.SelectionMode != ListViewSelectionMode.Multiple
                && SelectedItems.Count <= 1
                && e.KeyModifiers != VirtualKeyModifiers.Control
                && e.KeyModifiers != VirtualKeyModifiers.Shift
                && !Container.BlockKeyboardShortCutInput)
            {
                if ((e.OriginalSource as FrameworkElement)?.DataContext is FileSystemStorageItemBase Item)
                {
                    SelectedItem = Item;
                }
            }
        }

        private async void ViewControl_Drop(object sender, DragEventArgs e)
        {
            DragOperationDeferral Deferral = e.GetDeferral();

            try
            {
                List<string> PathList = new List<string>();

                if (e.DataView.Contains(StandardDataFormats.StorageItems))
                {
                    IReadOnlyList<IStorageItem> ItemList = await e.DataView.GetStorageItemsAsync();
                    PathList.AddRange(ItemList.Select((Item) => Item.Path));
                }

                if (e.DataView.Contains(StandardDataFormats.Text))
                {
                    string XmlText = await e.DataView.GetTextAsync();

                    if (XmlText.Contains("RX-Explorer"))
                    {
                        XmlDocument Document = new XmlDocument();
                        Document.LoadXml(XmlText);

                        IXmlNode KindNode = Document.SelectSingleNode("/RX-Explorer/Kind");

                        if (KindNode?.InnerText == "RX-Explorer-TransferNotStorageItem")
                        {
                            PathList.AddRange(Document.SelectNodes("/RX-Explorer/Item").Select((Node) => Node.InnerText));
                        }
                    }
                }

                if (PathList.Count > 0)
                {
                    using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableController())
                    {
                        switch (e.AcceptedOperation)
                        {
                            case DataPackageOperation.Copy:
                                {
                                    await Container.LoadingActivation(true, Globalization.GetString("Progress_Tip_Copying")).ConfigureAwait(true);

                                    try
                                    {
                                        await Exclusive.Controller.CopyAsync(PathList, CurrentFolder.Path, (s, arg) =>
                                        {
                                            if (Container.ProBar.Value < arg.ProgressPercentage)
                                            {
                                                Container.ProBar.IsIndeterminate = false;
                                                Container.ProBar.Value = arg.ProgressPercentage;
                                            }
                                        }).ConfigureAwait(true);
                                    }
                                    catch (FileNotFoundException)
                                    {
                                        QueueContentDialog Dialog = new QueueContentDialog
                                        {
                                            Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                            Content = Globalization.GetString("QueueDialog_CopyFailForNotExist_Content"),
                                            CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                                        };

                                        _ = await Dialog.ShowAsync().ConfigureAwait(true);
                                    }
                                    catch (InvalidOperationException)
                                    {
                                        QueueContentDialog dialog = new QueueContentDialog
                                        {
                                            Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                            Content = Globalization.GetString("QueueDialog_UnauthorizedPaste_Content"),
                                            PrimaryButtonText = Globalization.GetString("Common_Dialog_ConfirmButton"),
                                            CloseButtonText = Globalization.GetString("Common_Dialog_CancelButton")
                                        };

                                        if (await dialog.ShowAsync().ConfigureAwait(true) == ContentDialogResult.Primary)
                                        {
                                            await Launcher.LaunchFolderPathAsync(CurrentFolder.Path);
                                        }
                                    }
                                    catch (Exception)
                                    {
                                        QueueContentDialog dialog = new QueueContentDialog
                                        {
                                            Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                            Content = Globalization.GetString("QueueDialog_CopyFailUnexpectError_Content"),
                                            CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                                        };

                                        _ = await dialog.ShowAsync().ConfigureAwait(true);
                                    }

                                    break;
                                }
                            case DataPackageOperation.Move:
                                {
                                    if (PathList.All((Item) => Path.GetDirectoryName(Item) != CurrentFolder.Path))
                                    {
                                        await Container.LoadingActivation(true, Globalization.GetString("Progress_Tip_Moving")).ConfigureAwait(true);

                                        try
                                        {
                                            await Exclusive.Controller.MoveAsync(PathList, CurrentFolder.Path, (s, arg) =>
                                            {
                                                if (Container.ProBar.Value < arg.ProgressPercentage)
                                                {
                                                    Container.ProBar.IsIndeterminate = false;
                                                    Container.ProBar.Value = arg.ProgressPercentage;
                                                }
                                            }).ConfigureAwait(true);
                                        }
                                        catch (FileNotFoundException)
                                        {
                                            QueueContentDialog Dialog = new QueueContentDialog
                                            {
                                                Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                                Content = Globalization.GetString("QueueDialog_MoveFailForNotExist_Content"),
                                                CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                                            };

                                            _ = await Dialog.ShowAsync().ConfigureAwait(true);
                                        }
                                        catch (FileCaputureException)
                                        {
                                            QueueContentDialog dialog = new QueueContentDialog
                                            {
                                                Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                                Content = Globalization.GetString("QueueDialog_Item_Captured_Content"),
                                                CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                                            };

                                            _ = await dialog.ShowAsync().ConfigureAwait(true);
                                        }
                                        catch (InvalidOperationException)
                                        {
                                            QueueContentDialog dialog = new QueueContentDialog
                                            {
                                                Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                                Content = Globalization.GetString("QueueDialog_UnauthorizedPaste_Content"),
                                                PrimaryButtonText = Globalization.GetString("Common_Dialog_ConfirmButton"),
                                                CloseButtonText = Globalization.GetString("Common_Dialog_CancelButton")
                                            };

                                            if (await dialog.ShowAsync().ConfigureAwait(true) == ContentDialogResult.Primary)
                                            {
                                                await Launcher.LaunchFolderPathAsync(CurrentFolder.Path);
                                            }
                                        }
                                        catch (Exception)
                                        {
                                            QueueContentDialog dialog = new QueueContentDialog
                                            {
                                                Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                                Content = Globalization.GetString("QueueDialog_MoveFailUnexpectError_Content"),
                                                CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                                            };

                                            _ = await dialog.ShowAsync().ConfigureAwait(true);
                                        }
                                    }

                                    break;
                                }
                        }
                    }
                }
            }
            catch (Exception ex) when (ex.HResult == unchecked((int)0x80040064))
            {
                QueueContentDialog dialog = new QueueContentDialog
                {
                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                    Content = Globalization.GetString("QueueDialog_CopyFromUnsupportedArea_Content"),
                    CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                };

                _ = await dialog.ShowAsync().ConfigureAwait(true);
            }
            catch
            {
                QueueContentDialog dialog = new QueueContentDialog
                {
                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                    Content = Globalization.GetString("QueueDialog_FailToGetClipboardError_Content"),
                    CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                };

                _ = await dialog.ShowAsync().ConfigureAwait(true);
            }
            finally
            {
                await Container.LoadingActivation(false).ConfigureAwait(true);
                e.Handled = true;
                Deferral.Complete();
            }
        }

        private async void ViewControl_Holding(object sender, HoldingRoutedEventArgs e)
        {
            if (e.HoldingState == HoldingState.Started)
            {
                e.Handled = true;

                if (ItemPresenter is GridView)
                {
                    if ((e.OriginalSource as FrameworkElement)?.DataContext is FileSystemStorageItemBase Context)
                    {
                        if (SelectedItems.Count > 1 && SelectedItems.Contains(Context))
                        {
                            await ItemPresenter.SetCommandBarFlyoutWithExtraContextMenuItems(MixedFlyout, e.GetPosition((FrameworkElement)sender)).ConfigureAwait(true);
                        }
                        else
                        {
                            SelectedItem = Context;

                            if (Context is HyperlinkStorageItem)
                            {
                                await ItemPresenter.SetCommandBarFlyoutWithExtraContextMenuItems(LnkItemFlyout, e.GetPosition((FrameworkElement)sender)).ConfigureAwait(true);
                            }
                            else
                            {
                                await ItemPresenter.SetCommandBarFlyoutWithExtraContextMenuItems(Context.StorageType == StorageItemTypes.Folder ? FolderFlyout : FileFlyout, e.GetPosition((FrameworkElement)sender)).ConfigureAwait(true);
                            }
                        }
                    }
                    else
                    {
                        SelectedItem = null;

                        await ItemPresenter.SetCommandBarFlyoutWithExtraContextMenuItems(EmptyFlyout, e.GetPosition((FrameworkElement)sender)).ConfigureAwait(true);
                    }
                }
                else
                {
                    if (e.OriginalSource is FrameworkElement Element)
                    {
                        if (Element.Name == "EmptyTextblock")
                        {
                            SelectedItem = null;
                            await ItemPresenter.SetCommandBarFlyoutWithExtraContextMenuItems(EmptyFlyout, e.GetPosition((FrameworkElement)sender)).ConfigureAwait(true);
                        }
                        else
                        {
                            if (Element.DataContext is FileSystemStorageItemBase Context)
                            {
                                if (SelectedItems.Count > 1 && SelectedItems.Contains(Context))
                                {
                                    await ItemPresenter.SetCommandBarFlyoutWithExtraContextMenuItems(MixedFlyout, e.GetPosition((FrameworkElement)sender)).ConfigureAwait(true);
                                }
                                else
                                {
                                    if (SelectedItem == Context)
                                    {
                                        if (Context is HyperlinkStorageItem)
                                        {
                                            await ItemPresenter.SetCommandBarFlyoutWithExtraContextMenuItems(LnkItemFlyout, e.GetPosition((FrameworkElement)sender)).ConfigureAwait(true);
                                        }
                                        else
                                        {
                                            await ItemPresenter.SetCommandBarFlyoutWithExtraContextMenuItems(Context.StorageType == StorageItemTypes.Folder ? FolderFlyout : FileFlyout, e.GetPosition((FrameworkElement)sender)).ConfigureAwait(true);
                                        }
                                    }
                                    else
                                    {
                                        if (e.OriginalSource is TextBlock)
                                        {
                                            SelectedItem = Context;

                                            if (Context is HyperlinkStorageItem)
                                            {
                                                await ItemPresenter.SetCommandBarFlyoutWithExtraContextMenuItems(LnkItemFlyout, e.GetPosition((FrameworkElement)sender)).ConfigureAwait(true);
                                            }
                                            else
                                            {
                                                await ItemPresenter.SetCommandBarFlyoutWithExtraContextMenuItems(Context.StorageType == StorageItemTypes.Folder ? FolderFlyout : FileFlyout, e.GetPosition((FrameworkElement)sender)).ConfigureAwait(true);
                                            }
                                        }
                                        else
                                        {
                                            SelectedItem = null;
                                            await ItemPresenter.SetCommandBarFlyoutWithExtraContextMenuItems(EmptyFlyout, e.GetPosition((FrameworkElement)sender)).ConfigureAwait(true);
                                        }
                                    }
                                }
                            }
                            else
                            {
                                SelectedItem = null;
                                await ItemPresenter.SetCommandBarFlyoutWithExtraContextMenuItems(EmptyFlyout, e.GetPosition((FrameworkElement)sender)).ConfigureAwait(true);
                            }
                        }
                    }
                }
            }
        }

        private async void MixCompression_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();

            if (SelectedItems.Any((Item) => Item is HyperlinkStorageItem))
            {
                QueueContentDialog Dialog = new QueueContentDialog
                {
                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                    Content = Globalization.GetString("QueueDialog_LinkIsNotAllowInMixZip_Content"),
                    CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                };

                _ = await Dialog.ShowAsync().ConfigureAwait(true);

                return;
            }

            foreach (FileSystemStorageItemBase Item in SelectedItems.Where((Item) => Item.StorageType == StorageItemTypes.Folder))
            {
                if (!await FileSystemStorageItemBase.CheckExist(Item.Path).ConfigureAwait(true))
                {
                    QueueContentDialog Dialog = new QueueContentDialog
                    {
                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                        Content = Globalization.GetString("QueueDialog_LocateFolderFailure_Content"),
                        CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                    };

                    _ = await Dialog.ShowAsync().ConfigureAwait(true);

                    return;
                }
            }

            foreach (FileSystemStorageItemBase Item in SelectedItems.Where((Item) => Item.StorageType == StorageItemTypes.File))
            {
                if (!await FileSystemStorageItemBase.CheckExist(Item.Path).ConfigureAwait(true))
                {
                    QueueContentDialog Dialog = new QueueContentDialog
                    {
                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                        Content = Globalization.GetString("QueueDialog_LocateFileFailure_Content"),
                        CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                    };

                    _ = await Dialog.ShowAsync().ConfigureAwait(true);

                    return;
                }
            }

            bool IsCompress = true;

            if (SelectedItems.All((Item) => Item.StorageType == StorageItemTypes.File)
                && (SelectedItems.All((Item) => Item.Type.Equals(".zip", StringComparison.OrdinalIgnoreCase)) || SelectedItems.All((Item) => Item.Type.Equals(".tar", StringComparison.OrdinalIgnoreCase)) || SelectedItems.All((Item) => Item.Type.Equals(".gz", StringComparison.OrdinalIgnoreCase))))
            {
                IsCompress = false;
            }

            if (IsCompress)
            {
                CompressDialog dialog = new CompressDialog(false);

                if ((await dialog.ShowAsync().ConfigureAwait(true)) == ContentDialogResult.Primary)
                {
                    await Container.LoadingActivation(true, Globalization.GetString("Progress_Tip_Compressing")).ConfigureAwait(true);

                    try
                    {
                        switch (dialog.Type)
                        {
                            case CompressionType.Zip:
                                {
                                    await CompressionUtil.CreateZipAsync(SelectedItems, Path.Combine(CurrentFolder.Path, dialog.FileName), (int)dialog.Level, async (s, e) =>
                                    {
                                        await Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                                        {
                                            if (Container.ProBar.Value < e.ProgressPercentage)
                                            {
                                                Container.ProBar.IsIndeterminate = false;
                                                Container.ProBar.Value = e.ProgressPercentage;
                                            }
                                        });
                                    }).ConfigureAwait(true);

                                    break;
                                }
                            case CompressionType.Tar:
                                {
                                    await CompressionUtil.CreateTarAsync(SelectedItems, Path.Combine(CurrentFolder.Path, dialog.FileName), async (s, e) =>
                                    {
                                        await Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                                        {
                                            if (Container.ProBar.Value < e.ProgressPercentage)
                                            {
                                                Container.ProBar.IsIndeterminate = false;
                                                Container.ProBar.Value = e.ProgressPercentage;
                                            }
                                        });
                                    }).ConfigureAwait(true);

                                    break;
                                }
                        }
                    }
                    catch (UnauthorizedAccessException)
                    {
                        QueueContentDialog dialog1 = new QueueContentDialog
                        {
                            Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                            Content = Globalization.GetString("QueueDialog_UnauthorizedCompression_Content"),
                            PrimaryButtonText = Globalization.GetString("Common_Dialog_NowButton"),
                            CloseButtonText = Globalization.GetString("Common_Dialog_LaterButton")
                        };

                        if (await dialog1.ShowAsync().ConfigureAwait(true) == ContentDialogResult.Primary)
                        {
                            _ = await Launcher.LaunchFolderPathAsync(CurrentFolder.Path);
                        }
                    }
                    catch (Exception ex)
                    {
                        QueueContentDialog dialog2 = new QueueContentDialog
                        {
                            Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                            Content = Globalization.GetString("QueueDialog_CompressionError_Content") + ex.Message,
                            CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                        };

                        _ = await dialog2.ShowAsync().ConfigureAwait(true);
                    }

                    await Container.LoadingActivation(false).ConfigureAwait(true);
                }
            }
            else
            {
                await Container.LoadingActivation(true, Globalization.GetString("Progress_Tip_Extracting")).ConfigureAwait(true);

                try
                {
                    if (SelectedItems.All((Item) => Item.Type.Equals(".zip", StringComparison.OrdinalIgnoreCase)))
                    {
                        await CompressionUtil.ExtractZipAsync(SelectedItems, async (s, e) =>
                        {
                            await Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                            {
                                if (Container.ProBar.Value < e.ProgressPercentage)
                                {
                                    Container.ProBar.IsIndeterminate = false;
                                    Container.ProBar.Value = e.ProgressPercentage;
                                }
                            });
                        }).ConfigureAwait(true);
                    }
                    else if (SelectedItems.All((Item) => Item.Type.Equals(".tar", StringComparison.OrdinalIgnoreCase)))
                    {
                        await CompressionUtil.ExtractTarAsync(SelectedItems, async (s, e) =>
                        {
                            await Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                            {
                                if (Container.ProBar.Value < e.ProgressPercentage)
                                {
                                    Container.ProBar.IsIndeterminate = false;
                                    Container.ProBar.Value = e.ProgressPercentage;
                                }
                            });
                        }).ConfigureAwait(true);
                    }
                    else if (SelectedItems.All((Item) => Item.Type.Equals(".gz", StringComparison.OrdinalIgnoreCase)))
                    {
                        await CompressionUtil.ExtractGZipAsync(SelectedItems, async (s, e) =>
                        {
                            await Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                            {
                                if (Container.ProBar.Value < e.ProgressPercentage)
                                {
                                    Container.ProBar.IsIndeterminate = false;
                                    Container.ProBar.Value = e.ProgressPercentage;
                                }
                            });
                        }).ConfigureAwait(true);
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
                        _ = await Launcher.LaunchFolderPathAsync(CurrentFolder.Path);
                    }
                }
                catch (NotImplementedException)
                {
                    QueueContentDialog Dialog = new QueueContentDialog
                    {
                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                        Content = Globalization.GetString("QueueDialog_CanNotDecompressEncrypted_Content"),
                        CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                    };

                    _ = await Dialog.ShowAsync().ConfigureAwait(true);
                }
                catch (Exception ex)
                {
                    QueueContentDialog dialog = new QueueContentDialog
                    {
                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                        Content = Globalization.GetString("QueueDialog_DecompressionError_Content") + ex.Message,
                        CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                    };

                    _ = await dialog.ShowAsync().ConfigureAwait(true);
                }

                await Container.LoadingActivation(false).ConfigureAwait(true);
            }
        }

        private async void TryUnlock_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();

            if (SelectedItem is FileSystemStorageItemBase Item && Item.StorageType == StorageItemTypes.File)
            {
                try
                {
                    await Container.LoadingActivation(true, Globalization.GetString("Progress_Tip_Unlock")).ConfigureAwait(true);

                    using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableController())
                    {
                        if (await Exclusive.Controller.TryUnlockFileOccupy(Item.Path).ConfigureAwait(true))
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
                    await Container.LoadingActivation(false).ConfigureAwait(false);
                }
            }
        }

        private async void CalculateHash_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();

            if (await FileSystemStorageItemBase.CheckExist(SelectedItem.Path).ConfigureAwait(true))
            {
                try
                {
                    if (HashTeachTip.IsOpen)
                    {
                        HashTeachTip.IsOpen = false;
                    }

                    Hash_SHA1.IsEnabled = false;
                    Hash_SHA256.IsEnabled = false;
                    Hash_MD5.IsEnabled = false;

                    Hash_SHA1.Text = string.Empty;
                    Hash_SHA256.Text = string.Empty;
                    Hash_MD5.Text = string.Empty;

                    await Task.Delay(500).ConfigureAwait(true);

                    HashTeachTip.Target = ItemPresenter.ContainerFromItem(SelectedItem) as FrameworkElement;
                    HashTeachTip.IsOpen = true;

                    using (CancellationTokenSource HashCancellation = new CancellationTokenSource())
                    {
                        try
                        {
                            HashTeachTip.Tag = HashCancellation;

                            using (FileStream Stream1 = await SelectedItem.GetFileStreamFromFileAsync(AccessMode.Read).ConfigureAwait(true))
                            using (FileStream Stream2 = await SelectedItem.GetFileStreamFromFileAsync(AccessMode.Read).ConfigureAwait(true))
                            using (FileStream Stream3 = await SelectedItem.GetFileStreamFromFileAsync(AccessMode.Read).ConfigureAwait(true))
                            using (SHA256 SHA256Alg = SHA256.Create())
                            using (MD5 MD5Alg = MD5.Create())
                            using (SHA1 SHA1Alg = SHA1.Create())
                            {
                                Task Task1 = SHA256Alg.GetHashAsync(Stream1, HashCancellation.Token).ContinueWith((beforeTask) =>
                                {
                                    Hash_SHA256.Text = beforeTask.Result;
                                    Hash_SHA256.IsEnabled = true;
                                }, CancellationToken.None, TaskContinuationOptions.OnlyOnRanToCompletion, TaskScheduler.FromCurrentSynchronizationContext());

                                Task Task2 = MD5Alg.GetHashAsync(Stream2, HashCancellation.Token).ContinueWith((beforeTask) =>
                                {
                                    Hash_MD5.Text = beforeTask.Result;
                                    Hash_MD5.IsEnabled = true;
                                }, CancellationToken.None, TaskContinuationOptions.OnlyOnRanToCompletion, TaskScheduler.FromCurrentSynchronizationContext());

                                Task Task3 = SHA1Alg.GetHashAsync(Stream3, HashCancellation.Token).ContinueWith((beforeTask) =>
                                {
                                    Hash_SHA1.Text = beforeTask.Result;
                                    Hash_SHA1.IsEnabled = true;
                                }, CancellationToken.None, TaskContinuationOptions.OnlyOnRanToCompletion, TaskScheduler.FromCurrentSynchronizationContext());

                                await Task.WhenAll(Task1, Task2, Task3).ConfigureAwait(true);
                            }
                        }
                        finally
                        {
                            HashTeachTip.Tag = null;
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogTracer.Log(ex, "Error: CalculateHash failed");
                }
            }
            else
            {
                QueueContentDialog Dialog = new QueueContentDialog
                {
                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                    Content = Globalization.GetString("QueueDialog_LocateFileFailure_Content"),
                    CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                };

                _ = await Dialog.ShowAsync().ConfigureAwait(true);
            }
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

        private void HashTeachTip_Closing(TeachingTip sender, TeachingTipClosingEventArgs args)
        {
            if (sender.Tag is CancellationTokenSource Source)
            {
                Source.Cancel();
            }
        }

        private async void OpenInTerminal_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();

            if (await SQLite.Current.GetTerminalProfileByName(Convert.ToString(ApplicationData.Current.LocalSettings.Values["DefaultTerminal"])).ConfigureAwait(true) is TerminalProfile Profile)
            {
                using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableController())
                {
                    try
                    {
                        await Exclusive.Controller.RunAsync(Profile.Path, Profile.RunAsAdmin, false, false, Regex.Matches(Profile.Argument, "[^ \"]+|\"[^\"]*\"").Select((Mat) => Mat.Value.Contains("[CurrentLocation]") ? Mat.Value.Replace("[CurrentLocation]", CurrentFolder.Path) : Mat.Value).ToArray()).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        LogTracer.Log(ex, "An exception was threw when running terminal");
                    }
                }
            }
        }

        private async void OpenFolderInNewTab_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();

            if (SelectedItem is FileSystemStorageItemBase Item && Item.StorageType == StorageItemTypes.Folder)
            {
                await TabViewContainer.ThisPage.CreateNewTabAsync(null, Item.Path).ConfigureAwait(true);
            }
        }

        private void NameLabel_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            TextBlock NameLabel = (TextBlock)sender;

            if ((e.GetCurrentPoint(NameLabel).Properties.IsLeftButtonPressed || e.Pointer.PointerDeviceType != PointerDeviceType.Mouse) && SettingControl.IsDoubleClickEnable)
            {
                if ((e.OriginalSource as FrameworkElement)?.DataContext is FileSystemStorageItemBase Item)
                {
                    if (SelectedItem == Item)
                    {
                        DelayRenameCancel?.Dispose();
                        DelayRenameCancel = new CancellationTokenSource();

                        Task.Delay(1000).ContinueWith((task) =>
                        {
                            if (DelayRenameCancel != null && !DelayRenameCancel.IsCancellationRequested)
                            {
                                NameLabel.Visibility = Visibility.Collapsed;

                                if ((NameLabel.Parent as FrameworkElement).FindName("NameEditBox") is TextBox EditBox)
                                {
                                    EditBox.Tag = SelectedItem;
                                    EditBox.Text = NameLabel.Text;
                                    EditBox.Visibility = Visibility.Visible;
                                    EditBox.Focus(FocusState.Programmatic);
                                }

                                Container.BlockKeyboardShortCutInput = true;
                            }
                        }, TaskScheduler.FromCurrentSynchronizationContext());
                    }
                }
            }
        }

        private async void NameEditBox_LostFocus(object sender, RoutedEventArgs e)
        {
            TextBox NameEditBox = (TextBox)sender;

            if ((NameEditBox?.Parent as FrameworkElement)?.FindName("NameLabel") is TextBlock NameLabel && NameEditBox.Tag is FileSystemStorageItemBase CurrentEditItem)
            {
                try
                {
                    if (!FileSystemItemNameChecker.IsValid(NameEditBox.Text))
                    {
                        InvalidNameTip.Target = NameLabel;
                        InvalidNameTip.IsOpen = true;
                        return;
                    }

                    if (CurrentEditItem.Name == NameEditBox.Text)
                    {
                        return;
                    }

                    if (await FileSystemStorageItemBase.CheckExist(Path.Combine(Path.GetDirectoryName(CurrentEditItem.Path), NameEditBox.Text)).ConfigureAwait(true))
                    {
                        QueueContentDialog Dialog = new QueueContentDialog
                        {
                            Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                            Content = Globalization.GetString("QueueDialog_RenameExist_Content"),
                            PrimaryButtonText = Globalization.GetString("Common_Dialog_ContinueButton"),
                            CloseButtonText = Globalization.GetString("Common_Dialog_CancelButton")
                        };

                        if (await Dialog.ShowAsync().ConfigureAwait(true) != ContentDialogResult.Primary)
                        {
                            return;
                        }
                    }

                    using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableController())
                    {
                        try
                        {
                            await Exclusive.Controller.RenameAsync(CurrentEditItem.Path, NameEditBox.Text).ConfigureAwait(true);
                        }
                        catch (FileLoadException)
                        {
                            QueueContentDialog LoadExceptionDialog = new QueueContentDialog
                            {
                                Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                Content = Globalization.GetString("QueueDialog_FileOccupied_Content"),
                                CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton"),
                            };

                            _ = await LoadExceptionDialog.ShowAsync().ConfigureAwait(true);
                        }
                        catch (InvalidOperationException)
                        {
                            QueueContentDialog UnauthorizeDialog = new QueueContentDialog
                            {
                                Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                Content = Globalization.GetString("QueueDialog_UnauthorizedRenameFile_Content"),
                                PrimaryButtonText = Globalization.GetString("Common_Dialog_ConfirmButton"),
                                CloseButtonText = Globalization.GetString("Common_Dialog_CancelButton")
                            };

                            if (await UnauthorizeDialog.ShowAsync().ConfigureAwait(true) == ContentDialogResult.Primary)
                            {
                                await Launcher.LaunchFolderPathAsync(CurrentFolder.Path);
                            }
                        }
                    }
                }
                catch
                {
                    QueueContentDialog UnauthorizeDialog = new QueueContentDialog
                    {
                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                        Content = Globalization.GetString("QueueDialog_UnauthorizedRename_StartExplorer_Content"),
                        PrimaryButtonText = Globalization.GetString("Common_Dialog_NowButton"),
                        CloseButtonText = Globalization.GetString("Common_Dialog_LaterButton")
                    };

                    if (await UnauthorizeDialog.ShowAsync().ConfigureAwait(true) == ContentDialogResult.Primary)
                    {
                        _ = await Launcher.LaunchFolderPathAsync(CurrentFolder.Path);
                    }
                }
                finally
                {
                    NameEditBox.Visibility = Visibility.Collapsed;

                    NameLabel.Visibility = Visibility.Visible;

                    Container.BlockKeyboardShortCutInput = false;
                }
            }
        }

        private void GetFocus_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            ItemPresenter.Focus(FocusState.Programmatic);
        }

        private async void OpenFolderInNewWindow_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();

            if (SelectedItem is FileSystemStorageItemBase Item && Item.StorageType == StorageItemTypes.Folder)
            {
                await Launcher.LaunchUriAsync(new Uri($"rx-explorer:{Uri.EscapeDataString(Item.Path)}"));
            }
        }

        private async void Undo_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();

            await Ctrl_Z_Click().ConfigureAwait(false);
        }

        private void NameEditBox_BeforeTextChanging(TextBox sender, TextBoxBeforeTextChangingEventArgs args)
        {
            if (args.NewText.Any((Item) => Path.GetInvalidFileNameChars().Contains(Item)))
            {
                args.Cancel = true;

                if ((sender.Parent as FrameworkElement).FindName("NameLabel") is TextBlock NameLabel)
                {
                    InvalidCharTip.Target = NameLabel;
                    InvalidCharTip.IsOpen = true;
                }
            }
        }

        private async void OrderByName_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();

            await SortCollectionGenerator.Current.ModifySortWayAsync(CurrentFolder.Path, SortTarget.Name, Desc.IsChecked ? SortDirection.Descending : SortDirection.Ascending).ConfigureAwait(true);
        }

        private async void OrderByTime_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();

            await SortCollectionGenerator.Current.ModifySortWayAsync(CurrentFolder.Path, SortTarget.ModifiedTime, Desc.IsChecked ? SortDirection.Descending : SortDirection.Ascending).ConfigureAwait(true);
        }

        private async void OrderByType_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();

            await SortCollectionGenerator.Current.ModifySortWayAsync(CurrentFolder.Path, SortTarget.Type, Desc.IsChecked ? SortDirection.Descending : SortDirection.Ascending).ConfigureAwait(true);
        }

        private async void OrderBySize_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();

            await SortCollectionGenerator.Current.ModifySortWayAsync(CurrentFolder.Path, SortTarget.Size, Desc.IsChecked ? SortDirection.Descending : SortDirection.Ascending).ConfigureAwait(true);
        }

        private async void Desc_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();

            await SortCollectionGenerator.Current.ModifySortWayAsync(CurrentFolder.Path, SortDirection: SortDirection.Descending).ConfigureAwait(true);
        }

        private async void Asc_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();

            await SortCollectionGenerator.Current.ModifySortWayAsync(CurrentFolder.Path, SortDirection: SortDirection.Ascending).ConfigureAwait(true);
        }

        private void SortMenuFlyout_Opening(object sender, object e)
        {
            if (SortCollectionGenerator.Current.SortDirection == SortDirection.Ascending)
            {
                Desc.IsChecked = false;
                Asc.IsChecked = true;
            }
            else
            {
                Asc.IsChecked = false;
                Desc.IsChecked = true;
            }

            switch (SortCollectionGenerator.Current.SortTarget)
            {
                case SortTarget.Name:
                    {
                        OrderByType.IsChecked = false;
                        OrderByTime.IsChecked = false;
                        OrderBySize.IsChecked = false;
                        OrderByName.IsChecked = true;
                        break;
                    }
                case SortTarget.Type:
                    {
                        OrderByTime.IsChecked = false;
                        OrderBySize.IsChecked = false;
                        OrderByName.IsChecked = false;
                        OrderByType.IsChecked = true;
                        break;
                    }
                case SortTarget.ModifiedTime:
                    {
                        OrderBySize.IsChecked = false;
                        OrderByName.IsChecked = false;
                        OrderByType.IsChecked = false;
                        OrderByTime.IsChecked = true;
                        break;
                    }
                case SortTarget.Size:
                    {
                        OrderByName.IsChecked = false;
                        OrderByType.IsChecked = false;
                        OrderByTime.IsChecked = false;
                        OrderBySize.IsChecked = true;
                        break;
                    }
            }
        }

        private async void BottomCommandBar_Opening(object sender, object e)
        {
            BottomCommandBar.PrimaryCommands.Clear();
            BottomCommandBar.SecondaryCommands.Clear();

            AppBarButton MultiSelectButton = new AppBarButton
            {
                Icon = new FontIcon { Glyph = "\uE762" },
                Label = Globalization.GetString("Operate_Text_MultiSelect")
            };
            MultiSelectButton.Click += MulSelect_Click;
            BottomCommandBar.PrimaryCommands.Add(MultiSelectButton);

            if (SelectedItems.Count > 1)
            {
                AppBarButton CopyButton = new AppBarButton
                {
                    Icon = new SymbolIcon(Symbol.Copy),
                    Label = Globalization.GetString("Operate_Text_Copy")
                };
                CopyButton.Click += Copy_Click;
                BottomCommandBar.PrimaryCommands.Add(CopyButton);

                AppBarButton CutButton = new AppBarButton
                {
                    Icon = new SymbolIcon(Symbol.Cut),
                    Label = Globalization.GetString("Operate_Text_Cut")
                };
                CutButton.Click += Cut_Click;
                BottomCommandBar.PrimaryCommands.Add(CutButton);

                AppBarButton DeleteButton = new AppBarButton
                {
                    Icon = new SymbolIcon(Symbol.Delete),
                    Label = Globalization.GetString("Operate_Text_Delete")
                };
                DeleteButton.Click += Delete_Click;
                BottomCommandBar.PrimaryCommands.Add(DeleteButton);

                bool EnableMixZipButton = true;
                string MixZipButtonText = Globalization.GetString("Operate_Text_Compression");

                if (SelectedItems.Any((Item) => Item.StorageType != StorageItemTypes.Folder))
                {
                    if (SelectedItems.All((Item) => Item.StorageType == StorageItemTypes.File))
                    {
                        if (SelectedItems.All((Item) => Item.Type.ToLower() == ".zip"))
                        {
                            MixZipButtonText = Globalization.GetString("Operate_Text_Decompression");
                        }
                        else if (SelectedItems.All((Item) => Item.Type.ToLower() != ".zip"))
                        {
                            MixZipButtonText = Globalization.GetString("Operate_Text_Compression");
                        }
                        else
                        {
                            EnableMixZipButton = false;
                        }
                    }
                    else
                    {
                        if (SelectedItems.Where((It) => It.StorageType == StorageItemTypes.File).Any((Item) => Item.Type.ToLower() == ".zip"))
                        {
                            EnableMixZipButton = false;
                        }
                        else
                        {
                            MixZipButtonText = Globalization.GetString("Operate_Text_Compression");
                        }
                    }
                }
                else
                {
                    MixZipButtonText = Globalization.GetString("Operate_Text_Compression");
                }

                AppBarButton CompressionButton = new AppBarButton
                {
                    Icon = new SymbolIcon(Symbol.Bookmarks),
                    Label = MixZipButtonText,
                    IsEnabled = EnableMixZipButton
                };
                CompressionButton.Click += MixCompression_Click;
                BottomCommandBar.SecondaryCommands.Add(CompressionButton);
            }
            else
            {
                if (SelectedItem is FileSystemStorageItemBase Item)
                {
                    AppBarButton CopyButton = new AppBarButton
                    {
                        Icon = new SymbolIcon(Symbol.Copy),
                        Label = Globalization.GetString("Operate_Text_Copy")
                    };
                    CopyButton.Click += Copy_Click;
                    BottomCommandBar.PrimaryCommands.Add(CopyButton);

                    AppBarButton CutButton = new AppBarButton
                    {
                        Icon = new SymbolIcon(Symbol.Cut),
                        Label = Globalization.GetString("Operate_Text_Cut")
                    };
                    CutButton.Click += Cut_Click;
                    BottomCommandBar.PrimaryCommands.Add(CutButton);

                    AppBarButton DeleteButton = new AppBarButton
                    {
                        Icon = new SymbolIcon(Symbol.Delete),
                        Label = Globalization.GetString("Operate_Text_Delete")
                    };
                    DeleteButton.Click += Delete_Click;
                    BottomCommandBar.PrimaryCommands.Add(DeleteButton);

                    AppBarButton RenameButton = new AppBarButton
                    {
                        Icon = new SymbolIcon(Symbol.Rename),
                        Label = Globalization.GetString("Operate_Text_Rename")
                    };
                    RenameButton.Click += Rename_Click;
                    BottomCommandBar.PrimaryCommands.Add(RenameButton);

                    if (Item.StorageType == StorageItemTypes.File)
                    {
                        AppBarButton OpenButton = new AppBarButton
                        {
                            Icon = new SymbolIcon(Symbol.OpenFile),
                            Label = Globalization.GetString("Operate_Text_Open")
                        };
                        OpenButton.Click += ItemOpen_Click;
                        BottomCommandBar.SecondaryCommands.Add(OpenButton);

                        MenuFlyout OpenFlyout = new MenuFlyout();
                        MenuFlyoutItem AdminItem = new MenuFlyoutItem
                        {
                            Icon = new FontIcon { Glyph = "\uEA0D" },
                            Text = Globalization.GetString("Operate_Text_OpenAsAdministrator"),
                            IsEnabled = RunWithSystemAuthority.IsEnabled
                        };
                        AdminItem.Click += RunWithSystemAuthority_Click;
                        OpenFlyout.Items.Add(AdminItem);

                        MenuFlyoutItem OtherItem = new MenuFlyoutItem
                        {
                            Icon = new SymbolIcon(Symbol.SwitchApps),
                            Text = Globalization.GetString("Operate_Text_ChooseAnotherApp"),
                            IsEnabled = ChooseOtherApp.IsEnabled
                        };
                        OtherItem.Click += ChooseOtherApp_Click;
                        OpenFlyout.Items.Add(OtherItem);

                        BottomCommandBar.SecondaryCommands.Add(new AppBarButton
                        {
                            Icon = new SymbolIcon(Symbol.OpenWith),
                            Label = Globalization.GetString("Operate_Text_OpenWith"),
                            Flyout = OpenFlyout
                        });

                        BottomCommandBar.SecondaryCommands.Add(new AppBarSeparator());

                        MenuFlyout ToolFlyout = new MenuFlyout();
                        MenuFlyoutItem UnLock = new MenuFlyoutItem
                        {
                            Icon = new FontIcon { Glyph = "\uE785" },
                            Text = Globalization.GetString("Operate_Text_Unlock")
                        };
                        UnLock.Click += TryUnlock_Click;
                        ToolFlyout.Items.Add(UnLock);

                        MenuFlyoutItem Hash = new MenuFlyoutItem
                        {
                            Icon = new FontIcon { Glyph = "\uE2B2" },
                            Text = Globalization.GetString("Operate_Text_ComputeHash")
                        };
                        Hash.Click += CalculateHash_Click;
                        ToolFlyout.Items.Add(Hash);

                        BottomCommandBar.SecondaryCommands.Add(new AppBarButton
                        {
                            Icon = new FontIcon { Glyph = "\uE90F" },
                            Label = Globalization.GetString("Operate_Text_Tool"),
                            IsEnabled = FileTool.IsEnabled,
                            Flyout = ToolFlyout
                        });

                        MenuFlyout EditFlyout = new MenuFlyout();
                        MenuFlyoutItem MontageItem = new MenuFlyoutItem
                        {
                            Icon = new FontIcon { Glyph = "\uE177" },
                            Text = Globalization.GetString("Operate_Text_Montage"),
                            IsEnabled = VideoEdit.IsEnabled
                        };
                        MontageItem.Click += VideoEdit_Click;
                        EditFlyout.Items.Add(MontageItem);

                        MenuFlyoutItem MergeItem = new MenuFlyoutItem
                        {
                            Icon = new FontIcon { Glyph = "\uE11E" },
                            Text = Globalization.GetString("Operate_Text_Merge"),
                            IsEnabled = VideoMerge.IsEnabled
                        };
                        MergeItem.Click += VideoMerge_Click;
                        EditFlyout.Items.Add(MergeItem);

                        MenuFlyoutItem TranscodeItem = new MenuFlyoutItem
                        {
                            Icon = new FontIcon { Glyph = "\uE1CA" },
                            Text = Globalization.GetString("Operate_Text_Transcode"),
                            IsEnabled = Transcode.IsEnabled
                        };
                        TranscodeItem.Click += Transcode_Click;
                        EditFlyout.Items.Add(TranscodeItem);

                        BottomCommandBar.SecondaryCommands.Add(new AppBarButton
                        {
                            Icon = new SymbolIcon(Symbol.Edit),
                            Label = Globalization.GetString("Operate_Text_Edit"),
                            IsEnabled = FileEdit.IsEnabled,
                            Flyout = EditFlyout
                        });

                        MenuFlyout ShareFlyout = new MenuFlyout();
                        MenuFlyoutItem SystemShareItem = new MenuFlyoutItem
                        {
                            Icon = new SymbolIcon(Symbol.Share),
                            Text = Globalization.GetString("Operate_Text_SystemShare")
                        };
                        SystemShareItem.Click += SystemShare_Click;
                        ShareFlyout.Items.Add(SystemShareItem);

                        MenuFlyoutItem WIFIShareItem = new MenuFlyoutItem
                        {
                            Icon = new FontIcon { Glyph = "\uE701" },
                            Text = Globalization.GetString("Operate_Text_WIFIShare")
                        };
                        WIFIShareItem.Click += WIFIShare_Click;
                        ShareFlyout.Items.Add(WIFIShareItem);

                        MenuFlyoutItem BluetoothShare = new MenuFlyoutItem
                        {
                            Icon = new FontIcon { Glyph = "\uE702" },
                            Text = Globalization.GetString("Operate_Text_BluetoothShare")
                        };
                        BluetoothShare.Click += BluetoothShare_Click;
                        ShareFlyout.Items.Add(BluetoothShare);

                        BottomCommandBar.SecondaryCommands.Add(new AppBarButton
                        {
                            Icon = new SymbolIcon(Symbol.Share),
                            Label = Globalization.GetString("Operate_Text_Share"),
                            IsEnabled = FileShare.IsEnabled,
                            Flyout = ShareFlyout
                        });

                        BottomCommandBar.SecondaryCommands.Add(new AppBarSeparator());

                        AppBarButton CompressionButton = new AppBarButton
                        {
                            Icon = new SymbolIcon(Symbol.Bookmarks),
                            Label = Compression.Label
                        };
                        CompressionButton.Click += Compression_Click;
                        BottomCommandBar.SecondaryCommands.Add(CompressionButton);

                        BottomCommandBar.SecondaryCommands.Add(new AppBarSeparator());

                        AppBarButton PropertyButton = new AppBarButton
                        {
                            Icon = new SymbolIcon(Symbol.Tag),
                            Label = Globalization.GetString("Operate_Text_Property")
                        };
                        PropertyButton.Click += FileProperty_Click;
                        BottomCommandBar.SecondaryCommands.Add(PropertyButton);
                    }
                    else
                    {
                        AppBarButton OpenButton = new AppBarButton
                        {
                            Icon = new SymbolIcon(Symbol.BackToWindow),
                            Label = Globalization.GetString("Operate_Text_Open")
                        };
                        OpenButton.Click += ItemOpen_Click;
                        BottomCommandBar.SecondaryCommands.Add(OpenButton);

                        AppBarButton NewWindowButton = new AppBarButton
                        {
                            Icon = new FontIcon { Glyph = "\uE727" },
                            Label = Globalization.GetString("Operate_Text_NewWindow")
                        };
                        NewWindowButton.Click += OpenFolderInNewWindow_Click;
                        BottomCommandBar.SecondaryCommands.Add(NewWindowButton);

                        AppBarButton NewTabButton = new AppBarButton
                        {
                            Icon = new FontIcon { Glyph = "\uF7ED" },
                            Label = Globalization.GetString("Operate_Text_NewTab")
                        };
                        NewTabButton.Click += OpenFolderInNewTab_Click;
                        BottomCommandBar.SecondaryCommands.Add(NewTabButton);

                        AppBarButton CompressionButton = new AppBarButton
                        {
                            Icon = new SymbolIcon(Symbol.Bookmarks),
                            Label = Globalization.GetString("Operate_Text_Compression")
                        };
                        CompressionButton.Click += CompressFolder_Click;
                        BottomCommandBar.SecondaryCommands.Add(CompressionButton);

                        BottomCommandBar.SecondaryCommands.Add(new AppBarSeparator());

                        AppBarButton PropertyButton = new AppBarButton
                        {
                            Icon = new SymbolIcon(Symbol.Tag),
                            Label = Globalization.GetString("Operate_Text_Property")
                        };
                        PropertyButton.Click += FolderProperty_Click;
                        BottomCommandBar.SecondaryCommands.Add(PropertyButton);
                    }
                }
                else
                {
                    bool IsEnablePaste = false;

                    try
                    {
                        DataPackageView Package = Clipboard.GetContent();

                        if (Package.Contains(StandardDataFormats.StorageItems))
                        {
                            IsEnablePaste = true;
                        }
                        else if (Package.Contains(StandardDataFormats.Text))
                        {
                            string XmlText = await Package.GetTextAsync();

                            if (XmlText.Contains("RX-Explorer"))
                            {
                                XmlDocument Document = new XmlDocument();
                                Document.LoadXml(XmlText);

                                IXmlNode KindNode = Document.SelectSingleNode("/RX-Explorer/Kind");

                                if (KindNode?.InnerText == "RX-Explorer-TransferNotStorageItem")
                                {
                                    IsEnablePaste = true;
                                }
                            }
                        }
                    }
                    catch
                    {
                        IsEnablePaste = false;
                    }

                    bool IsEnableUndo = false;

                    if (OperationRecorder.Current.Count > 0)
                    {
                        IsEnableUndo = true;
                    }
                    else
                    {
                        IsEnableUndo = false;
                    }

                    AppBarButton PasteButton = new AppBarButton
                    {
                        Icon = new SymbolIcon(Symbol.Paste),
                        Label = Globalization.GetString("Operate_Text_Paste"),
                        IsEnabled = IsEnablePaste
                    };
                    PasteButton.Click += Paste_Click;
                    BottomCommandBar.PrimaryCommands.Add(PasteButton);

                    AppBarButton UndoButton = new AppBarButton
                    {
                        Icon = new SymbolIcon(Symbol.Undo),
                        Label = Globalization.GetString("Operate_Text_Undo"),
                        IsEnabled = IsEnableUndo
                    };
                    UndoButton.Click += Undo_Click;
                    BottomCommandBar.PrimaryCommands.Add(UndoButton);

                    AppBarButton RefreshButton = new AppBarButton
                    {
                        Icon = new SymbolIcon(Symbol.Refresh),
                        Label = Globalization.GetString("Operate_Text_Refresh")
                    };
                    RefreshButton.Click += Refresh_Click;
                    BottomCommandBar.PrimaryCommands.Add(RefreshButton);

                    MenuFlyout NewFlyout = new MenuFlyout();
                    MenuFlyoutItem CreateFileItem = new MenuFlyoutItem
                    {
                        Icon = new SymbolIcon(Symbol.Page2),
                        Text = Globalization.GetString("Operate_Text_CreateFile"),
                        MinWidth = 150
                    };
                    CreateFileItem.Click += CreateFile_Click;
                    NewFlyout.Items.Add(CreateFileItem);

                    MenuFlyoutItem CreateFolder = new MenuFlyoutItem
                    {
                        Icon = new SymbolIcon(Symbol.NewFolder),
                        Text = Globalization.GetString("Operate_Text_CreateFolder"),
                        MinWidth = 150
                    };
                    CreateFolder.Click += CreateFolder_Click;
                    NewFlyout.Items.Add(CreateFolder);

                    BottomCommandBar.SecondaryCommands.Add(new AppBarButton
                    {
                        Icon = new SymbolIcon(Symbol.Add),
                        Label = Globalization.GetString("Operate_Text_Create"),
                        Flyout = NewFlyout
                    });

                    bool DescCheck = false;
                    bool AscCheck = false;
                    bool NameCheck = false;
                    bool TimeCheck = false;
                    bool TypeCheck = false;
                    bool SizeCheck = false;

                    if (SortCollectionGenerator.Current.SortDirection == SortDirection.Ascending)
                    {
                        DescCheck = false;
                        AscCheck = true;
                    }
                    else
                    {
                        AscCheck = false;
                        DescCheck = true;
                    }

                    switch (SortCollectionGenerator.Current.SortTarget)
                    {
                        case SortTarget.Name:
                            {
                                TypeCheck = false;
                                TimeCheck = false;
                                SizeCheck = false;
                                NameCheck = true;
                                break;
                            }
                        case SortTarget.Type:
                            {
                                TimeCheck = false;
                                SizeCheck = false;
                                NameCheck = false;
                                TypeCheck = true;
                                break;
                            }
                        case SortTarget.ModifiedTime:
                            {
                                SizeCheck = false;
                                NameCheck = false;
                                TypeCheck = false;
                                TimeCheck = true;
                                break;
                            }
                        case SortTarget.Size:
                            {
                                NameCheck = false;
                                TypeCheck = false;
                                TimeCheck = false;
                                SizeCheck = true;
                                break;
                            }
                    }

                    MenuFlyout SortFlyout = new MenuFlyout();

                    RadioMenuFlyoutItem SortName = new RadioMenuFlyoutItem
                    {
                        Text = Globalization.GetString("Operate_Text_SortTarget_Name"),
                        IsChecked = NameCheck
                    };
                    SortName.Click += OrderByName_Click;
                    SortFlyout.Items.Add(SortName);

                    RadioMenuFlyoutItem SortTime = new RadioMenuFlyoutItem
                    {
                        Text = Globalization.GetString("Operate_Text_SortTarget_Time"),
                        IsChecked = TimeCheck
                    };
                    SortTime.Click += OrderByTime_Click;
                    SortFlyout.Items.Add(SortTime);

                    RadioMenuFlyoutItem SortType = new RadioMenuFlyoutItem
                    {
                        Text = Globalization.GetString("Operate_Text_SortTarget_Type"),
                        IsChecked = TypeCheck
                    };
                    SortType.Click += OrderByType_Click;
                    SortFlyout.Items.Add(SortType);

                    RadioMenuFlyoutItem SortSize = new RadioMenuFlyoutItem
                    {
                        Text = Globalization.GetString("Operate_Text_SortTarget_Size"),
                        IsChecked = SizeCheck
                    };
                    SortSize.Click += OrderBySize_Click;
                    SortFlyout.Items.Add(SortSize);

                    SortFlyout.Items.Add(new MenuFlyoutSeparator());

                    RadioMenuFlyoutItem Asc = new RadioMenuFlyoutItem
                    {
                        Text = Globalization.GetString("Operate_Text_SortDirection_Asc"),
                        IsChecked = AscCheck
                    };
                    Asc.Click += Asc_Click;
                    SortFlyout.Items.Add(Asc);

                    RadioMenuFlyoutItem Desc = new RadioMenuFlyoutItem
                    {
                        Text = Globalization.GetString("Operate_Text_SortDirection_Desc"),
                        IsChecked = DescCheck
                    };
                    Desc.Click += Desc_Click;
                    SortFlyout.Items.Add(Desc);

                    BottomCommandBar.SecondaryCommands.Add(new AppBarButton
                    {
                        Icon = new SymbolIcon(Symbol.Sort),
                        Label = Globalization.GetString("Operate_Text_Sort"),
                        Flyout = SortFlyout
                    });

                    BottomCommandBar.SecondaryCommands.Add(new AppBarSeparator());

                    AppBarButton WinExButton = new AppBarButton
                    {
                        Icon = new FontIcon { Glyph = "\uEC50" },
                        Label = Globalization.GetString("Operate_Text_OpenInWinExplorer")
                    };
                    WinExButton.Click += UseSystemFileMananger_Click;
                    BottomCommandBar.SecondaryCommands.Add(WinExButton);

                    AppBarButton TerminalButton = new AppBarButton
                    {
                        Icon = new FontIcon { Glyph = "\uE756" },
                        Label = Globalization.GetString("Operate_Text_OpenInTerminal")
                    };
                    TerminalButton.Click += OpenInTerminal_Click;
                    BottomCommandBar.SecondaryCommands.Add(TerminalButton);

                    BottomCommandBar.SecondaryCommands.Add(new AppBarSeparator());

                    AppBarButton PropertyButton = new AppBarButton
                    {
                        Icon = new SymbolIcon(Symbol.Tag),
                        Label = Globalization.GetString("Operate_Text_Property")
                    };
                    PropertyButton.Click += ParentProperty_Click;
                    BottomCommandBar.SecondaryCommands.Add(PropertyButton);
                }
            }
        }

        private void ListHeader_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
        {
            e.Handled = true;
        }

        private async void LnkOpenLocation_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedItem is HyperlinkStorageItem Item)
            {
                if (Item.LinkTargetPath == Globalization.GetString("UnknownText") || Item.LinkType == ShellLinkType.UWP)
                {
                    QueueContentDialog Dialog = new QueueContentDialog
                    {
                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                        Content = Globalization.GetString("QueueDialog_LocateFileFailure_Content"),
                        CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                    };

                    _ = await Dialog.ShowAsync().ConfigureAwait(true);
                }
                else
                {
                    if (await FileSystemStorageItemBase.OpenAsync(Path.GetDirectoryName(Item.LinkTargetPath), ItemFilters.Folder).ConfigureAwait(true) is FileSystemStorageItemBase ParentFolder)
                    {
                        await DisplayItemsInFolder(ParentFolder).ConfigureAwait(true);

                        if (FileCollection.FirstOrDefault((SItem) => SItem.Path.Equals(Item.LinkTargetPath, StringComparison.OrdinalIgnoreCase)) is FileSystemStorageItemBase Target)
                        {
                            ItemPresenter.ScrollIntoView(Target);
                            SelectedItem = Target;
                        }
                    }
                    else
                    {
                        QueueContentDialog Dialog = new QueueContentDialog
                        {
                            Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                            Content = Globalization.GetString("QueueDialog_LocateFileFailure_Content"),
                            CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                        };

                        _ = await Dialog.ShowAsync().ConfigureAwait(true);
                    }
                }
            }
        }

        private void MulSelect_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();

            if (ItemPresenter.SelectionMode == ListViewSelectionMode.Extended)
            {
                ItemPresenter.SelectionMode = ListViewSelectionMode.Multiple;
            }
            else
            {
                ItemPresenter.SelectionMode = ListViewSelectionMode.Extended;
            }
        }

        private void ViewControl_PreviewKeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Space)
            {
                e.Handled = true;
            }
        }

        private void ListHeaderRelativePanel_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            if ((sender as FrameworkElement).FindChildOfName<Button>("NameFilterHeader") is Button NameFilterBtn)
            {
                NameFilterBtn.Visibility = Visibility.Visible;
            }
            else if ((sender as FrameworkElement).FindChildOfName<Button>("ModTimeFilterHeader") is Button ModTimeFilterBtn)
            {
                ModTimeFilterBtn.Visibility = Visibility.Visible;
            }
            else if ((sender as FrameworkElement).FindChildOfName<Button>("TypeFilterHeader") is Button TypeFilterBtn)
            {
                TypeFilterBtn.Visibility = Visibility.Visible;
            }
            else if ((sender as FrameworkElement).FindChildOfName<Button>("SizeFilterHeader") is Button SizeFilterBtn)
            {
                SizeFilterBtn.Visibility = Visibility.Visible;
            }
        }

        private void ListHeaderRelativePanel_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            if ((sender as FrameworkElement).FindChildOfName<Button>("NameFilterHeader") is Button NameFilterBtn)
            {
                if (!NameFilterBtn.Flyout.IsOpen)
                {
                    NameFilterBtn.Visibility = Visibility.Collapsed;
                }
            }
            else if ((sender as FrameworkElement).FindChildOfName<Button>("ModTimeFilterHeader") is Button ModTimeFilterBtn)
            {
                if (!ModTimeFilterBtn.Flyout.IsOpen)
                {
                    ModTimeFilterBtn.Visibility = Visibility.Collapsed;
                }
            }
            else if ((sender as FrameworkElement).FindChildOfName<Button>("TypeFilterHeader") is Button TypeFilterBtn)
            {
                if (!TypeFilterBtn.Flyout.IsOpen)
                {
                    TypeFilterBtn.Visibility = Visibility.Collapsed;
                }
            }
            else if ((sender as FrameworkElement).FindChildOfName<Button>("SizeFilterHeader") is Button SizeFilterBtn)
            {
                if (!SizeFilterBtn.Flyout.IsOpen)
                {
                    SizeFilterBtn.Visibility = Visibility.Collapsed;
                }
            }
        }

        private void FilterFlyout_Closing(FlyoutBase sender, FlyoutBaseClosingEventArgs args)
        {
            Container.BlockKeyboardShortCutInput = false;
            sender.Target.Visibility = Visibility.Collapsed;
        }

        private void FilterFlyout_Opened(object sender, object e)
        {
            Container.BlockKeyboardShortCutInput = true;
        }

        private void Filter_RefreshListRequested(object sender, IEnumerable<FileSystemStorageItemBase> e)
        {
            FileCollection.Clear();

            foreach (var Item in e)
            {
                FileCollection.Add(Item);
            }
        }

        public void Dispose()
        {
            FileCollection.Clear();

            AreaWatcher?.Dispose();
            WiFiProvider?.Dispose();
            SelectionExtention?.Dispose();
            DelayRenameCancel?.Dispose();
            DelayEnterCancel?.Dispose();
            EnterLock?.Dispose();

            AreaWatcher = null;
            WiFiProvider = null;
            SelectionExtention = null;
            DelayRenameCancel = null;
            DelayEnterCancel = null;
            EnterLock = null;

            RecordIndex = 0;
            GoAndBackRecord.Clear();

            Application.Current.Suspending -= Current_Suspending;
            Application.Current.Resuming -= Current_Resuming;
            SortCollectionGenerator.Current.SortWayChanged -= Current_SortWayChanged;
            ViewModeController.ViewModeChanged -= Current_ViewModeChanged;
        }

        private async void OpenFolderInVerticalSplitView_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();

            if (SelectedItem != null)
            {
                await Container.CreateNewBlade(SelectedItem.Path).ConfigureAwait(false);
            }
        }

        private void NameEditBox_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Enter)
            {
                e.Handled = true;
                ItemPresenter.Focus(FocusState.Programmatic);
            }
        }
    }
}

