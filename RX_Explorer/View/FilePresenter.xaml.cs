using ComputerVision;
using Microsoft.Toolkit.Deferred;
using Microsoft.Toolkit.Uwp.Helpers;
using Microsoft.Toolkit.Uwp.UI.Controls;
using Microsoft.UI.Xaml.Controls;
using RX_Explorer.Class;
using RX_Explorer.CustomControl;
using RX_Explorer.Dialog;
using RX_Explorer.SeparateWindow.PropertyWindow;
using ShareClassLibrary;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.ApplicationModel.DataTransfer;
using Windows.ApplicationModel.DataTransfer.DragDrop;
using Windows.Devices.Input;
using Windows.Devices.Radios;
using Windows.Foundation;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.Storage.Streams;
using Windows.System;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.Input;
using Windows.UI.WindowManagement;
using Windows.UI.WindowManagement.Preview;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Media.Imaging;
using ZXing;
using ZXing.QrCode;
using ZXing.QrCode.Internal;
using CommandBarFlyout = Microsoft.UI.Xaml.Controls.CommandBarFlyout;
using NavigationViewItem = Microsoft.UI.Xaml.Controls.NavigationViewItem;
using TreeViewNode = Microsoft.UI.Xaml.Controls.TreeViewNode;

namespace RX_Explorer
{
    public sealed partial class FilePresenter : Page, IDisposable
    {
        public ObservableCollection<FileSystemStorageItemBase> FileCollection { get; }
        private ObservableCollection<FileSystemStorageGroupItem> GroupCollection { get; }

        private readonly ListViewHeaderController ListViewDetailHeader = new ListViewHeaderController();

        private WeakReference<FileControl> WeakToFileControl;

        public FileControl Container
        {
            get
            {
                if (WeakToFileControl != null)
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
                else
                {
                    return null;
                }
            }
            set
            {
                WeakToFileControl = new WeakReference<FileControl>(value);
                AreaWatcher.SetTreeView(value.FolderTree);
            }
        }

        public List<ValueTuple<string, string>> GoAndBackRecord { get; } = new List<ValueTuple<string, string>>();

        public int RecordIndex { get; set; }

        private StorageAreaWatcher AreaWatcher;

        private SemaphoreSlim EnterLock;
        private SemaphoreSlim CollectionChangeLock;

        private readonly PointerEventHandler PointerPressedEventHandler;
        private readonly PointerEventHandler PointerReleasedEventHandler;

        private ListViewBase itemPresenter;

        public ListViewBase ItemPresenter
        {
            get => itemPresenter;
            set
            {
                if (value != itemPresenter)
                {
                    itemPresenter?.RemoveHandler(PointerReleasedEvent, PointerReleasedEventHandler);
                    itemPresenter?.RemoveHandler(PointerPressedEvent, PointerPressedEventHandler);
                    itemPresenter = value;
                    itemPresenter.AddHandler(PointerPressedEvent, PointerPressedEventHandler, true);
                    itemPresenter.AddHandler(PointerReleasedEvent, PointerReleasedEventHandler, true);

                    SelectionExtention?.Dispose();
                    SelectionExtention = new ListViewBaseSelectionExtention(itemPresenter, DrawRectangle);

                    if (itemPresenter is GridView)
                    {
                        if (ListViewSemantic != null)
                        {
                            ListViewSemantic.Visibility = Visibility.Collapsed;
                            ListCollectionVS.Source = null;
                        }

                        if (GridCollectionVS.Source == null)
                        {
                            GridCollectionVS.Source = IsGroupedEnable ? GroupCollection : FileCollection;
                        }

                        GridViewSemantic.Visibility = Visibility.Visible;
                    }
                    else
                    {
                        if (GridViewSemantic != null)
                        {
                            GridViewSemantic.Visibility = Visibility.Collapsed;
                            GridCollectionVS.Source = null;
                        }

                        if (ListCollectionVS.Source == null)
                        {
                            ListCollectionVS.Source = IsGroupedEnable ? GroupCollection : FileCollection;
                        }

                        ListViewSemantic.Visibility = Visibility.Visible;
                    }
                }
            }
        }

        private volatile FileSystemStorageFolder currentFolder;
        public FileSystemStorageFolder CurrentFolder
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

                    if (value is RootStorageFolder)
                    {
                        Container.GoParentFolder.IsEnabled = false;
                        AreaWatcher?.StopWatchDirectory();
                    }
                    else
                    {
                        Container.GoParentFolder.IsEnabled = value.Path.StartsWith(@"\\", StringComparison.OrdinalIgnoreCase) ? !value.Path.Equals(Path.GetPathRoot(value.Path), StringComparison.OrdinalIgnoreCase) : true;
                        AreaWatcher?.StartWatchDirectory(value.Path);
                    }

                    Container.GlobeSearch.PlaceholderText = $"{Globalization.GetString("SearchBox_PlaceholderText")} {value.DisplayName}";
                    Container.GoBackRecord.IsEnabled = RecordIndex > 0;
                    Container.GoForwardRecord.IsEnabled = RecordIndex < GoAndBackRecord.Count - 1;
                    Container.CurrentTabItem.Header = string.IsNullOrEmpty(value.DisplayName) ? $"<{Globalization.GetString("UnknownText")}>" : value.DisplayName;

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
        private CancellationTokenSource DelaySelectionCancel;
        private CancellationTokenSource DelayDragCancel;
        private int CurrentViewModeIndex = -1;
        private bool GroupedEnable;

        private CollectionViewSource CurrentCVS
        {
            get
            {
                return ItemPresenter is GridView ? GridCollectionVS : ListCollectionVS;
            }
        }

        private bool IsGroupedEnable
        {
            get
            {
                return GroupedEnable;
            }
            set
            {
                if (GroupedEnable != value)
                {
                    ListCollectionVS.IsSourceGrouped = value;
                    GridCollectionVS.IsSourceGrouped = value;
                    GroupedEnable = value;

                    if (value)
                    {
                        CurrentCVS.Source = GroupCollection;
                    }
                    else
                    {
                        CurrentCVS.Source = FileCollection;
                    }
                }
            }
        }

        public FileSystemStorageItemBase SelectedItem
        {
            get
            {
                return ItemPresenter?.SelectedItem as FileSystemStorageItemBase;
            }
            set
            {
                if (ItemPresenter != null)
                {
                    ItemPresenter.SelectedItem = value;

                    if (value != null)
                    {
                        (ItemPresenter.ContainerFromItem(value) as SelectorItem)?.Focus(FocusState.Programmatic);
                    }
                }
            }
        }

        public List<FileSystemStorageItemBase> SelectedItems
        {
            get
            {
                if (ItemPresenter != null)
                {
                    return ItemPresenter.SelectedItems.OfType<FileSystemStorageItemBase>().ToList();
                }
                else
                {
                    return new List<FileSystemStorageItemBase>(0);
                }
            }
        }

        public FilePresenter()
        {
            InitializeComponent();

            GroupCollection = new ObservableCollection<FileSystemStorageGroupItem>();

            FileCollection = new ObservableCollection<FileSystemStorageItemBase>();
            FileCollection.CollectionChanged += FileCollection_CollectionChanged;

            ListViewDetailHeader.Filter.RefreshListRequested += Filter_RefreshListRequested;

            PointerPressedEventHandler = new PointerEventHandler(ViewControl_PointerPressed);
            PointerReleasedEventHandler = new PointerEventHandler(ViewControl_PointerReleased);

            AreaWatcher = new StorageAreaWatcher(FileCollection);

            EnterLock = new SemaphoreSlim(1, 1);
            CollectionChangeLock = new SemaphoreSlim(1, 1);

            CoreWindow Window = CoreWindow.GetForCurrentThread();
            Window.KeyDown += FilePresenter_KeyDown;
            Window.Dispatcher.AcceleratorKeyActivated += Dispatcher_AcceleratorKeyActivated;

            Loaded += FilePresenter_Loaded;
            RootFolderControl.EnterActionRequested += RootFolderControl_EnterActionRequested;

            Application.Current.Suspending += Current_Suspending;
            Application.Current.Resuming += Current_Resuming;
            SortCollectionGenerator.SortStateChanged += Current_SortStateChanged;
            GroupCollectionGenerator.GroupStateChanged += GroupCollectionGenerator_GroupStateChanged;
            ViewModeController.ViewModeChanged += Current_ViewModeChanged;
        }

        private void GroupCollectionGenerator_GroupStateChanged(object sender, GroupCollectionGenerator.GroupStateChangedEventArgs args)
        {
            if (args.Path.Equals(CurrentFolder.Path, StringComparison.OrdinalIgnoreCase))
            {
                if (args.Target == GroupTarget.None)
                {
                    GroupAsc.IsEnabled = false;
                    GroupDesc.IsEnabled = false;

                    GroupCollection.Clear();

                    IsGroupedEnable = false;
                }
                else
                {
                    GroupAsc.IsEnabled = true;
                    GroupDesc.IsEnabled = true;

                    GroupCollection.Clear();

                    foreach (FileSystemStorageGroupItem GroupItem in GroupCollectionGenerator.GetGroupedCollection(FileCollection, args.Target, args.Direction))
                    {
                        GroupCollection.Add(GroupItem);
                    }

                    IsGroupedEnable = true;
                }
            }
        }

        private async void Dispatcher_AcceleratorKeyActivated(CoreDispatcher sender, AcceleratorKeyEventArgs args)
        {
            if (Container.CurrentPresenter == this
                && args.KeyStatus.IsMenuKeyDown
                && Container.Frame.CurrentSourcePageType == typeof(FileControl)
                && Container.Frame == TabViewContainer.CurrentNavigationControl
                && MainPage.ThisPage.NavView.SelectedItem is NavigationViewItem NavItem
                && Convert.ToString(NavItem.Content) == Globalization.GetString("MainPage_PageDictionary_Home_Label"))
            {
                switch (args.VirtualKey)
                {
                    case VirtualKey.Left:
                        {
                            args.Handled = true;

                            if (Container.GoBackRecord.IsEnabled)
                            {
                                Container.GoBackRecord_Click(null, null);
                            }
                            break;
                        }
                    case VirtualKey.Right:
                        {
                            args.Handled = true;

                            if (Container.GoForwardRecord.IsEnabled)
                            {
                                Container.GoForwardRecord_Click(null, null);
                            }
                            break;
                        }
                    case VirtualKey.Enter when SelectedItems.Count == 1:
                        {
                            args.Handled = true;

                            AppWindow NewWindow = await AppWindow.TryCreateAsync();
                            NewWindow.RequestSize(new Size(420, 600));
                            NewWindow.RequestMoveRelativeToCurrentViewContent(new Point(Window.Current.Bounds.Width / 2 - 200, Window.Current.Bounds.Height / 2 - 300));
                            NewWindow.PersistedStateId = "Properties";
                            NewWindow.Title = Globalization.GetString("Properties_Window_Title");
                            NewWindow.TitleBar.ExtendsContentIntoTitleBar = true;
                            NewWindow.TitleBar.ButtonForegroundColor = AppThemeController.Current.Theme == ElementTheme.Dark ? Colors.White : Colors.Black;
                            NewWindow.TitleBar.ButtonBackgroundColor = Colors.Transparent;
                            NewWindow.TitleBar.ButtonInactiveBackgroundColor = Colors.Transparent;

                            ElementCompositionPreview.SetAppWindowContent(NewWindow, new PropertyBase(NewWindow, SelectedItem));
                            WindowManagementPreview.SetPreferredMinSize(NewWindow, new Size(420, 600));

                            await NewWindow.TryShowAsync();
                            break;
                        }
                }
            }
        }

        private async void FilePresenter_KeyDown(CoreWindow sender, KeyEventArgs args)
        {
            if (Container.CurrentPresenter == this
                && CurrentFolder is not RootStorageFolder
                && Container.Frame.CurrentSourcePageType == typeof(FileControl)
                && Container.Frame == TabViewContainer.CurrentNavigationControl
                && MainPage.ThisPage.NavView.SelectedItem is NavigationViewItem NavItem
                && Convert.ToString(NavItem.Content) == Globalization.GetString("MainPage_PageDictionary_Home_Label"))
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
                                    if (await Exclusive.Controller.CheckIfQuicklookIsAvaliableAsync())
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
                                await EnterSelectedItemAsync(Item).ConfigureAwait(false);
                                break;
                            }
                        case VirtualKey.Back when Container.GoBackRecord.IsEnabled:
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
                        case VirtualKey.A when CtrlState.HasFlag(CoreVirtualKeyStates.Down):
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
                        case VirtualKey.C when CtrlState.HasFlag(CoreVirtualKeyStates.Down) && SelectedItems.Count > 0:
                            {
                                Copy_Click(null, null);
                                break;
                            }
                        case VirtualKey.X when CtrlState.HasFlag(CoreVirtualKeyStates.Down) && SelectedItems.Count > 0:
                            {
                                Cut_Click(null, null);
                                break;
                            }
                        case VirtualKey.Delete when SelectedItems.Count > 0:
                        case VirtualKey.D when CtrlState.HasFlag(CoreVirtualKeyStates.Down) && SelectedItems.Count > 0:
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
                                await Launcher.LaunchFolderPathAsync(CurrentFolder.Path);
                                break;
                            }
                        case VirtualKey.T when ShiftState.HasFlag(CoreVirtualKeyStates.Down):
                            {
                                OpenInTerminal_Click(null, null);
                                break;
                            }
                        case VirtualKey.T when CtrlState.HasFlag(CoreVirtualKeyStates.Down) && SelectedItems.Count <= 1:
                            {
                                CloseAllFlyout();

                                if (SelectedItem is FileSystemStorageFolder)
                                {
                                    await TabViewContainer.ThisPage.CreateNewTabAsync(SelectedItem.Path);
                                }
                                else
                                {
                                    await TabViewContainer.ThisPage.CreateNewTabAsync();
                                }

                                break;
                            }
                        case VirtualKey.Q when CtrlState.HasFlag(CoreVirtualKeyStates.Down) && SelectedItems.Count == 1:
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
                        case VirtualKey.B when CtrlState.HasFlag(CoreVirtualKeyStates.Down) && SelectedItems.Count == 1 && SelectedItem is FileSystemStorageFolder Folder:
                            {
                                await Container.CreateNewBladeAsync(Folder.Path);
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

        private async void Current_ViewModeChanged(object sender, ViewModeController.ViewModeChangedEventArgs e)
        {
            if (e.Path.Equals(CurrentFolder?.Path, StringComparison.OrdinalIgnoreCase) && CurrentViewModeIndex != e.Index)
            {
                EventDeferral Deferral = e.GetDeferral();

                try
                {
                    CurrentViewModeIndex = e.Index;

                    switch (e.Index)
                    {
                        case 0:
                            {
                                FindName("GridViewSemantic");

                                ItemPresenter = GridViewControl;

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
                                        await Task.Delay(200);
                                    }
                                }

                                break;
                            }
                        case 1:
                            {
                                FindName("ListViewSemantic");
                                ItemPresenter = ListViewControl;
                                break;
                            }
                        case 2:
                            {
                                FindName("GridViewSemantic");

                                ItemPresenter = GridViewControl;

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
                                        await Task.Delay(200);
                                    }
                                }

                                break;
                            }
                        case 3:
                            {
                                FindName("GridViewSemantic");

                                ItemPresenter = GridViewControl;

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
                                        await Task.Delay(200);
                                    }
                                }

                                break;
                            }
                        case 4:
                            {
                                FindName("GridViewSemantic");

                                ItemPresenter = GridViewControl;

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
                                        await Task.Delay(200);
                                    }
                                }

                                break;
                            }
                        case 5:
                            {
                                FindName("GridViewSemantic");

                                ItemPresenter = GridViewControl;

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
                                        await Task.Delay(200);
                                    }
                                }

                                break;
                            }
                    }

                    SQLite.Current.SetPathConfiguration(new PathConfiguration(CurrentFolder.Path, e.Index));
                }
                catch (Exception ex)
                {
                    LogTracer.Log(ex, "Switch DisplayMode could not be completed successfully");
                }
                finally
                {
                    Deferral.Complete();
                }
            }
        }

        private void Current_SortStateChanged(object sender, SortCollectionGenerator.SortStateChangedEventArgs args)
        {
            if (args.Path.Equals(CurrentFolder.Path, StringComparison.OrdinalIgnoreCase))
            {
                ListViewDetailHeader.Indicator.SetIndicatorStatus(args.Target, args.Direction);

                if (IsGroupedEnable)
                {
                    foreach (FileSystemStorageGroupItem GroupItem in GroupCollection)
                    {
                        FileSystemStorageItemBase[] SortedGroupItem = SortCollectionGenerator.GetSortedCollection(GroupItem, args.Target, args.Direction).ToArray();

                        GroupItem.Clear();

                        foreach (FileSystemStorageItemBase Item in SortedGroupItem)
                        {
                            GroupItem.Add(Item);
                        }
                    }
                }

                FileSystemStorageItemBase[] ItemList = SortCollectionGenerator.GetSortedCollection(FileCollection, args.Target, args.Direction).ToArray();

                FileCollection.Clear();

                foreach (FileSystemStorageItemBase Item in ItemList)
                {
                    FileCollection.Add(Item);
                }
            }
        }

        private async Task<bool> DisplayItemsInFolderCore(string FolderPath, bool ForceRefresh = false, bool SkipNavigationRecord = false)
        {
            await EnterLock.WaitAsync();

            try
            {
                if (string.IsNullOrWhiteSpace(FolderPath))
                {
                    throw new ArgumentNullException(nameof(FolderPath), "Parameter could not be null or empty");
                }

                if (!ForceRefresh && FolderPath == CurrentFolder?.Path)
                {
                    return false;
                }

                if (!SkipNavigationRecord && !ForceRefresh)
                {
                    if (GoAndBackRecord.Count > 0)
                    {
                        if (RecordIndex != GoAndBackRecord.Count - 1)
                        {
                            GoAndBackRecord.RemoveRange(RecordIndex + 1, GoAndBackRecord.Count - RecordIndex - 1);
                        }

                        string ParentPath = Path.GetDirectoryName(FolderPath);

                        if (!string.IsNullOrEmpty(ParentPath))
                        {
                            if (ParentPath.Equals(GoAndBackRecord[GoAndBackRecord.Count - 1].Item1, StringComparison.OrdinalIgnoreCase))
                            {
                                GoAndBackRecord[GoAndBackRecord.Count - 1] = (ParentPath, FolderPath);
                            }
                            else
                            {
                                GoAndBackRecord[GoAndBackRecord.Count - 1] = (GoAndBackRecord[GoAndBackRecord.Count - 1].Item1, SelectedItems.Count > 1 ? string.Empty : ((SelectedItem?.Path) ?? string.Empty));
                            }
                        }
                    }

                    GoAndBackRecord.Add((FolderPath, string.Empty));

                    RecordIndex = GoAndBackRecord.Count - 1;
                }

                if (FolderPath.Equals(RootStorageFolder.Instance.Path, StringComparison.OrdinalIgnoreCase))
                {
                    CurrentFolder = RootStorageFolder.Instance;
                    Container.ViewModeComboBox.IsEnabled = false;
                    RootFolderControl.Visibility = Visibility.Visible;
                    FileCollection.Clear();
                    GroupCollection.Clear();
                }
                else
                {
                    if (!await FileSystemStorageItemBase.CheckExistAsync(FolderPath))
                    {
                        QueueContentDialog dialog = new QueueContentDialog
                        {
                            Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                            Content = Globalization.GetString("QueueDialog_LocateFolderFailure_Content"),
                            CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton"),
                        };

                        await dialog.ShowAsync();

                        return false;
                    }

                    Container.ViewModeComboBox.IsEnabled = true;
                    RootFolderControl.Visibility = Visibility.Collapsed;

                    SQLite.Current.SetPathHistory(FolderPath);

                    CurrentFolder = await FileSystemStorageItemBase.OpenAsync(FolderPath) as FileSystemStorageFolder;

                    if (Container.FolderTree.SelectedNode == null && Container.FolderTree.RootNodes.FirstOrDefault((Node) => (Node.Content as TreeViewNodeContent)?.Path == Path.GetPathRoot(FolderPath)) is TreeViewNode RootNode)
                    {
                        Container.FolderTree.SelectNodeAndScrollToVertical(RootNode);
                    }

                    FileCollection.Clear();

                    PathConfiguration Config = SQLite.Current.GetPathConfiguration(FolderPath);

                    await Container.ViewModeControl.SetCurrentViewMode(Config.Path, Config.DisplayModeIndex.GetValueOrDefault());

                    IReadOnlyList<FileSystemStorageItemBase> ChildItems = await CurrentFolder.GetChildItemsAsync(SettingControl.IsDisplayHiddenItem, SettingControl.IsDisplayProtectedSystemItems);

                    if (ChildItems.Count > 0)
                    {
                        HasFile.Visibility = Visibility.Collapsed;

                        if (Config.GroupTarget != GroupTarget.None)
                        {
                            GroupCollection.Clear();

                            foreach (FileSystemStorageGroupItem GroupItem in GroupCollectionGenerator.GetGroupedCollection(ChildItems, Config.GroupTarget.GetValueOrDefault(), GroupDirection.Ascending))
                            {
                                GroupCollection.Add(new FileSystemStorageGroupItem(GroupItem.Key, SortCollectionGenerator.GetSortedCollection(GroupItem, Config.SortTarget.GetValueOrDefault(), Config.SortDirection.GetValueOrDefault())));
                            }

                            IsGroupedEnable = true;
                        }
                        else
                        {
                            GroupCollection.Clear();
                            IsGroupedEnable = false;
                        }

                        FileCollection.AddRange(SortCollectionGenerator.GetSortedCollection(ChildItems, Config.SortTarget.GetValueOrDefault(), Config.SortDirection.GetValueOrDefault()));
                    }
                    else
                    {
                        HasFile.Visibility = Visibility.Visible;
                    }

                    StatusTips.Text = Globalization.GetString("FilePresenterBottomStatusTip_TotalItem").Replace("{ItemNum}", FileCollection.Count.ToString());

                    ListViewDetailHeader.Filter.SetDataSource(FileCollection);
                    ListViewDetailHeader.Indicator.SetIndicatorStatus(Config.SortTarget.GetValueOrDefault(), Config.SortDirection.GetValueOrDefault());
                }

                return true;
            }
            finally
            {
                EnterLock.Release();
            }
        }

        public Task<bool> DisplayItemsInFolder(FileSystemStorageFolder Folder, bool ForceRefresh = false, bool SkipNavigationRecord = false)
        {
            if (Folder == null)
            {
                throw new ArgumentNullException(nameof(Folder), "Parameter could not be null or empty");
            }

            return DisplayItemsInFolderCore(Folder.Path, ForceRefresh, SkipNavigationRecord);
        }

        public Task<bool> DisplayItemsInFolder(string FolderPath, bool ForceRefresh = false, bool SkipNavigationRecord = false)
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
            AreaWatcher.StartWatchDirectory(AreaWatcher.CurrentLocation);
        }

        private void Current_Suspending(object sender, SuspendingEventArgs e)
        {
            AreaWatcher.StopWatchDirectory();
        }

        private void FilePresenter_Loaded(object sender, RoutedEventArgs e)
        {
            if (this.FindParentOfType<BladeItem>() is BladeItem Parent)
            {
                Parent.Header = CurrentFolder?.DisplayName;
            }
        }

        private void NavigateToStorageItem(VirtualKey Key)
        {
            if (Key >= VirtualKey.Number0 && Key <= VirtualKey.Z)
            {
                string SearchString = Convert.ToChar(Key).ToString();

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

        private async void FileCollection_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            PathConfiguration Config = SQLite.Current.GetPathConfiguration(CurrentFolder.Path);

            await CollectionChangeLock.WaitAsync();

            try
            {
                switch (e.Action)
                {
                    case NotifyCollectionChangedAction.Add:
                        {
                            IEnumerable<FileSystemStorageItemBase> GroupExpandedCollection = GroupCollection.SelectMany((Group) => Group);

                            foreach (FileSystemStorageItemBase Item in e.NewItems)
                            {
                                if (GroupExpandedCollection.All((ExistItem) => ExistItem != Item))
                                {
                                    string Key = GroupCollectionGenerator.SearchGroupBelonging(Item, Config.GroupTarget.GetValueOrDefault());

                                    if (GroupCollection.FirstOrDefault((Item) => Item.Key == Key) is FileSystemStorageGroupItem GroupItem)
                                    {
                                        int Index = SortCollectionGenerator.SearchInsertLocation(GroupItem, Item, Config.SortTarget.GetValueOrDefault(), Config.SortDirection.GetValueOrDefault());

                                        if (Index >= 0)
                                        {
                                            GroupItem.Insert(Index, Item);
                                        }
                                        else
                                        {
                                            GroupItem.Add(Item);
                                        }
                                    }
                                }
                            }

                            break;
                        }
                    case NotifyCollectionChangedAction.Remove:
                        {
                            IEnumerable<FileSystemStorageItemBase> GroupExpandedCollection = GroupCollection.SelectMany((Group) => Group);

                            foreach (FileSystemStorageItemBase Item in e.OldItems)
                            {
                                if (GroupExpandedCollection.Any((ExistItem) => ExistItem == Item))
                                {
                                    string Key = GroupCollectionGenerator.SearchGroupBelonging(Item, Config.GroupTarget.GetValueOrDefault());

                                    if (GroupCollection.FirstOrDefault((Item) => Item.Key == Key) is FileSystemStorageGroupItem GroupItem)
                                    {
                                        GroupItem.Remove(Item);
                                    }
                                }
                            }

                            break;
                        }
                    case NotifyCollectionChangedAction.Replace:
                        {
                            IEnumerable<FileSystemStorageItemBase> GroupExpandedCollection = GroupCollection.SelectMany((Group) => Group);

                            foreach (FileSystemStorageItemBase Item in e.OldItems)
                            {
                                if (GroupExpandedCollection.Any((ExistItem) => ExistItem == Item))
                                {
                                    string Key = GroupCollectionGenerator.SearchGroupBelonging(Item, Config.GroupTarget.GetValueOrDefault());

                                    if (GroupCollection.FirstOrDefault((Item) => Item.Key == Key) is FileSystemStorageGroupItem GroupItem)
                                    {
                                        GroupItem.Remove(Item);
                                    }
                                }
                            }

                            foreach (FileSystemStorageItemBase Item in e.NewItems)
                            {
                                if (GroupExpandedCollection.All((ExistItem) => ExistItem != Item))
                                {
                                    string Key = GroupCollectionGenerator.SearchGroupBelonging(Item, Config.GroupTarget.GetValueOrDefault());

                                    if (GroupCollection.FirstOrDefault((Item) => Item.Key == Key) is FileSystemStorageGroupItem GroupItem)
                                    {
                                        int Index = SortCollectionGenerator.SearchInsertLocation(GroupItem, Item, Config.SortTarget.GetValueOrDefault(), Config.SortDirection.GetValueOrDefault());

                                        if (Index >= 0)
                                        {
                                            GroupItem.Insert(Index, Item);
                                        }
                                        else
                                        {
                                            GroupItem.Add(Item);
                                        }
                                    }
                                }
                            }

                            break;
                        }
                }

                if (e.Action != NotifyCollectionChangedAction.Reset)
                {
                    HasFile.Visibility = FileCollection.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex);
            }
            finally
            {
                CollectionChangeLock.Release();
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
            LinkItemFlyout.Hide();
        }

        private async Task Ctrl_Z_Click()
        {
            if (OperationRecorder.Current.Count > 0)
            {
                try
                {
                    List<string> RecordList = OperationRecorder.Current.Pop();

                    if (RecordList.Count > 0)
                    {
                        IEnumerable<string[]> SplitGroup = RecordList.Select((Item) => Item.Split("||", StringSplitOptions.RemoveEmptyEntries));

                        IEnumerable<string> OriginFolderPathList = SplitGroup.Select((Item) => Path.GetDirectoryName(Item[0]));

                        string OriginFolderPath = OriginFolderPathList.FirstOrDefault();

                        if (OriginFolderPathList.All((Item) => Item.Equals(OriginFolderPath, StringComparison.OrdinalIgnoreCase)))
                        {
                            IEnumerable<string> UndoModeList = SplitGroup.Select((Item) => Item[1]);

                            string UndoMode = UndoModeList.FirstOrDefault();

                            if (UndoModeList.All((Mode) => Mode.Equals(UndoMode, StringComparison.OrdinalIgnoreCase)))
                            {
                                switch (UndoMode)
                                {
                                    case "Delete":
                                        {
                                            QueueTaskController.EnqueueUndoOpeartion(OperationKind.Delete, SplitGroup.Select((Item) => Item[0]).ToArray(), OnCompleted: async (s, e) =>
                                            {
                                                if (!SettingControl.IsDetachTreeViewAndPresenter)
                                                {
                                                    foreach (TreeViewNode RootNode in Container.FolderTree.RootNodes)
                                                    {
                                                        await RootNode.UpdateAllSubNodeAsync();
                                                    }
                                                }
                                            });

                                            break;
                                        }
                                    case "Move":
                                        {
                                            QueueTaskController.EnqueueUndoOpeartion(OperationKind.Move, SplitGroup.Select((Item) => Item[2]).ToArray(), OriginFolderPath, OnCompleted: async (s, e) =>
                                            {
                                                if (!SettingControl.IsDetachTreeViewAndPresenter)
                                                {
                                                    foreach (TreeViewNode RootNode in Container.FolderTree.RootNodes)
                                                    {
                                                        await RootNode.UpdateAllSubNodeAsync();
                                                    }
                                                }
                                            });

                                            break;
                                        }
                                    case "Copy":
                                        {
                                            QueueTaskController.EnqueueUndoOpeartion(OperationKind.Copy, SplitGroup.Select((Item) => Item[2]).ToArray(), OnCompleted: async (s, e) =>
                                            {
                                                if (!SettingControl.IsDetachTreeViewAndPresenter)
                                                {
                                                    foreach (TreeViewNode RootNode in Container.FolderTree.RootNodes)
                                                    {
                                                        await RootNode.UpdateAllSubNodeAsync();
                                                    }
                                                }
                                            });

                                            break;
                                        }
                                }
                            }
                            else
                            {
                                throw new Exception("Undo data format is invalid");
                            }
                        }
                        else
                        {
                            throw new Exception("Undo data format is invalid");
                        }
                    }
                    else
                    {
                        throw new Exception("Undo data format is invalid");
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

                    _ = await Dialog.ShowAsync();
                }
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
                    Clipboard.SetContent(await SelectedItemsCopy.GetAsDataPackageAsync(DataPackageOperation.Copy));

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

                    await Dialog.ShowAsync().ConfigureAwait(false);
                }
            }
        }

        private async void Paste_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();

            try
            {
                DataPackageView Package = Clipboard.GetContent();

                IReadOnlyList<string> PathList = await Package.GetAsPathListAsync();

                if (PathList.Count > 0)
                {
                    if (Package.RequestedOperation.HasFlag(DataPackageOperation.Move))
                    {
                        if (PathList.All((Path) => System.IO.Path.GetDirectoryName(Path) != CurrentFolder.Path))
                        {
                            QueueTaskController.EnqueueMoveOpeartion(PathList, CurrentFolder.Path);
                        }
                    }
                    else if (Package.RequestedOperation.HasFlag(DataPackageOperation.Copy))
                    {
                        QueueTaskController.EnqueueCopyOpeartion(PathList, CurrentFolder.Path);
                    }
                }
            }
            catch (Exception ex) when (ex.HResult is unchecked((int)0x80040064) or unchecked((int)0x8004006A))
            {
                QueueTaskController.EnqueueRemoteCopyOpeartion(CurrentFolder.Path);
            }
            catch
            {
                QueueContentDialog dialog = new QueueContentDialog
                {
                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                    Content = Globalization.GetString("QueueDialog_FailToGetClipboardError_Content"),
                    CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                };

                _ = await dialog.ShowAsync();
            }
            finally
            {
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
                    Clipboard.SetContent(await SelectedItemsCopy.GetAsDataPackageAsync(DataPackageOperation.Move));

                    FileCollection.Where((Item) => Item.ThumbnailOpacity != 1d).ToList().ForEach((Item) => Item.SetThumbnailOpacity(ThumbnailStatus.Normal));
                    SelectedItemsCopy.ForEach((Item) => Item.SetThumbnailOpacity(ThumbnailStatus.ReducedOpacity));
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
                //We should take the path of what we want to delete first. Or we might delete some items incorrectly
                string[] PathList = SelectedItems.Select((Item) => Item.Path).ToArray();

                bool ExecuteDelete = false;

                if (ApplicationData.Current.LocalSettings.Values["DeleteConfirmSwitch"] is bool DeleteConfirm)
                {
                    if (DeleteConfirm)
                    {
                        DeleteDialog QueueContenDialog = new DeleteDialog(Globalization.GetString("QueueDialog_DeleteFiles_Content"));

                        if (await QueueContenDialog.ShowAsync() == ContentDialogResult.Primary)
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

                    if (await QueueContenDialog.ShowAsync() == ContentDialogResult.Primary)
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
                    foreach ((TabViewItem Tab, BladeItem[] Blades) in TabViewContainer.ThisPage.TabCollection.Where((Tab) => Tab.Tag is FileControl)
                                                                                                             .Select((Tab) => (Tab, (Tab.Tag as FileControl).BladeViewer.Items.Cast<BladeItem>().ToArray())).ToArray())
                    {
                        foreach (string DeletePath in PathList)
                        {
                            if (Blades.Select((BItem) => (BItem.Content as FilePresenter)?.CurrentFolder?.Path)
                                      .All((BladePath) => BladePath.StartsWith(DeletePath, StringComparison.OrdinalIgnoreCase)))
                            {
                                await TabViewContainer.ThisPage.CleanUpAndRemoveTabItem(Tab);
                            }
                            else
                            {
                                foreach (BladeItem BItem in Blades.Where((Item) => ((Item.Content as FilePresenter).CurrentFolder?.Path.StartsWith(DeletePath, StringComparison.OrdinalIgnoreCase)).GetValueOrDefault()))
                                {
                                    await (Tab.Tag as FileControl).CloseBladeAsync(BItem);
                                }
                            }
                        }
                    }

                    QueueTaskController.EnqueueDeleteOpeartion(PathList, PermanentDelete);
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

                    _ = await Dialog.ShowAsync();
                }
                else
                {
                    FileSystemStorageItemBase RenameItem = SelectedItem;

                    RenameDialog dialog = new RenameDialog(RenameItem);

                    if ((await dialog.ShowAsync()) == ContentDialogResult.Primary)
                    {
                        if (!RenameItem.Name.Equals(dialog.DesireName, StringComparison.OrdinalIgnoreCase) && await FileSystemStorageItemBase.CheckExistAsync(Path.Combine(CurrentFolder.Path, dialog.DesireName)))
                        {
                            QueueContentDialog Dialog = new QueueContentDialog
                            {
                                Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                Content = Globalization.GetString("QueueDialog_RenameExist_Content"),
                                PrimaryButtonText = Globalization.GetString("Common_Dialog_ContinueButton"),
                                CloseButtonText = Globalization.GetString("Common_Dialog_CancelButton")
                            };

                            if (await Dialog.ShowAsync() != ContentDialogResult.Primary)
                            {
                                return;
                            }
                        }

                        try
                        {
                            await RenameItem.RenameAsync(dialog.DesireName);
                        }
                        catch (FileLoadException)
                        {
                            QueueContentDialog LoadExceptionDialog = new QueueContentDialog
                            {
                                Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                Content = Globalization.GetString("QueueDialog_FileOccupied_Content"),
                                CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton"),
                            };

                            _ = await LoadExceptionDialog.ShowAsync();
                        }
                        catch (Exception)
                        {
                            QueueContentDialog UnauthorizeDialog = new QueueContentDialog
                            {
                                Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                Content = Globalization.GetString("QueueDialog_UnauthorizedRenameFile_Content"),
                                PrimaryButtonText = Globalization.GetString("Common_Dialog_ConfirmButton"),
                                CloseButtonText = Globalization.GetString("Common_Dialog_CancelButton")
                            };

                            await UnauthorizeDialog.ShowAsync();
                        }
                    }
                }
            }
        }

        private async void BluetoothShare_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();

            if (await SelectedItem.GetStorageItemAsync() is StorageFile ShareFile)
            {
                if (!await FileSystemStorageItemBase.CheckExistAsync(ShareFile.Path))
                {
                    QueueContentDialog Dialog = new QueueContentDialog
                    {
                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                        Content = Globalization.GetString("QueueDialog_LocateFileFailure_Content"),
                        CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                    };

                    _ = await Dialog.ShowAsync();

                    return;
                }

                IReadOnlyList<Radio> RadioDevice = await Radio.GetRadiosAsync();

                if (RadioDevice.Any((Device) => Device.Kind == RadioKind.Bluetooth && Device.State == RadioState.On))
                {
                    BluetoothUI Bluetooth = new BluetoothUI();
                    if ((await Bluetooth.ShowAsync()) == ContentDialogResult.Primary)
                    {
                        BluetoothFileTransfer FileTransfer = new BluetoothFileTransfer(ShareFile);

                        _ = await FileTransfer.ShowAsync();
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
                    _ = await dialog.ShowAsync();
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

                _ = await Dialog.ShowAsync();
            }
        }

        private void ViewControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            DelayRenameCancel?.Cancel();

            List<FileSystemStorageItemBase> SelectedItemsCopy = SelectedItems;

            if (SelectedItemsCopy.Count == 1 && SelectedItemsCopy.First() is FileSystemStorageFile File)
            {
                FileEdit.IsEnabled = false;
                FileShare.IsEnabled = true;

                ChooseOtherApp.IsEnabled = true;
                RunWithSystemAuthority.IsEnabled = false;

                switch (File.Type.ToLower())
                {
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

            string[] StatusTipsSplit = StatusTips.Text.Split("  |  ", StringSplitOptions.RemoveEmptyEntries);

            if (SelectedItemsCopy.Count > 0)
            {
                string SizeInfo = string.Empty;

                if (SelectedItemsCopy.All((Item) => Item is FileSystemStorageFile))
                {
                    ulong TotalSize = 0;

                    foreach (ulong Size in SelectedItemsCopy.Cast<FileSystemStorageFile>().Select((Item) => Item.SizeRaw).ToArray())
                    {
                        TotalSize += Size;
                    }

                    SizeInfo = $"  |  {TotalSize.GetFileSizeDescription()}";
                }

                if (StatusTipsSplit.Length > 0)
                {
                    StatusTips.Text = $"{StatusTipsSplit[0]}  |  {Globalization.GetString("FilePresenterBottomStatusTip_SelectedItem").Replace("{ItemNum}", SelectedItemsCopy.Count.ToString())}{SizeInfo}";
                }
                else
                {
                    StatusTips.Text += $"  |  {Globalization.GetString("FilePresenterBottomStatusTip_SelectedItem").Replace("{ItemNum}", SelectedItemsCopy.Count.ToString())}{SizeInfo}";
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

        private void ViewControl_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            DelayDragCancel?.Cancel();
        }

        private void ViewControl_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (e.OriginalSource is FrameworkElement Element && Element.DataContext is FileSystemStorageItemBase Item)
            {
                if (Element.FindParentOfType<TextBox>() == null)
                {
                    PointerPoint PointerInfo = e.GetCurrentPoint(null);

                    if (PointerInfo.Properties.IsMiddleButtonPressed && Item is FileSystemStorageFolder)
                    {
                        SelectionExtention.Disable();
                        SelectedItem = Item;
                        _ = TabViewContainer.ThisPage.CreateNewTabAsync(null, Item.Path);
                    }
                    else if (Element.FindParentOfType<SelectorItem>() is SelectorItem SItem)
                    {
                        if (e.KeyModifiers == VirtualKeyModifiers.None && ItemPresenter.SelectionMode != ListViewSelectionMode.Multiple)
                        {
                            if (SelectedItems.Contains(Item))
                            {
                                SelectionExtention.Disable();

                                DelayDragCancel?.Cancel();
                                DelayDragCancel?.Dispose();
                                DelayDragCancel = new CancellationTokenSource();

                                Task.Delay(300).ContinueWith((task, input) =>
                                {
                                    if (input is (CancellationTokenSource Cancel, UIElement Item, PointerPoint Point) && !Cancel.IsCancellationRequested)
                                    {
                                        _ = Item.StartDragAsync(Point);
                                    }
                                }, (DelayDragCancel, SItem, e.GetCurrentPoint(SItem)), TaskScheduler.FromCurrentSynchronizationContext());
                            }
                            else
                            {
                                if (PointerInfo.Properties.IsLeftButtonPressed)
                                {
                                    SelectedItem = Item;
                                }

                                if (e.OriginalSource is Grid || (e.OriginalSource is TextBlock Block && Block.Name == "EmptyTextblock"))
                                {
                                    SelectionExtention.Enable();
                                }
                                else
                                {
                                    SelectionExtention.Disable();

                                    DelayDragCancel?.Cancel();
                                    DelayDragCancel?.Dispose();
                                    DelayDragCancel = new CancellationTokenSource();

                                    Task.Delay(300).ContinueWith((task, input) =>
                                    {
                                        if (input is (CancellationTokenSource Cancel, UIElement Item, PointerPoint Point) && !Cancel.IsCancellationRequested)
                                        {
                                            _ = Item.StartDragAsync(Point);
                                        }
                                    }, (DelayDragCancel, SItem, e.GetCurrentPoint(SItem)), TaskScheduler.FromCurrentSynchronizationContext());
                                }
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
                    SelectionExtention.Disable();
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
                            await ItemPresenter.SetCommandBarFlyoutWithExtraContextMenuItems(MixedFlyout, e.GetPosition((FrameworkElement)sender));
                        }
                        else
                        {
                            SelectedItem = Context;

                            switch (Context)
                            {
                                case LinkStorageFile:
                                    {
                                        await ItemPresenter.SetCommandBarFlyoutWithExtraContextMenuItems(LinkItemFlyout, e.GetPosition((FrameworkElement)sender));
                                        break;
                                    }
                                case FileSystemStorageFolder:
                                    {
                                        await ItemPresenter.SetCommandBarFlyoutWithExtraContextMenuItems(FolderFlyout, e.GetPosition((FrameworkElement)sender));
                                        break;
                                    }
                                case FileSystemStorageFile:
                                    {
                                        await ItemPresenter.SetCommandBarFlyoutWithExtraContextMenuItems(FileFlyout, e.GetPosition((FrameworkElement)sender));
                                        break;
                                    }
                            }
                        }
                    }
                    else
                    {
                        SelectedItem = null;
                        await ItemPresenter.SetCommandBarFlyoutWithExtraContextMenuItems(EmptyFlyout, e.GetPosition((FrameworkElement)sender));
                    }
                }
                else
                {
                    if (e.OriginalSource is FrameworkElement Element)
                    {
                        if (Element.Name == "EmptyTextblock")
                        {
                            SelectedItem = null;
                            await ItemPresenter.SetCommandBarFlyoutWithExtraContextMenuItems(EmptyFlyout, e.GetPosition((FrameworkElement)sender));
                        }
                        else
                        {
                            if (Element.DataContext is FileSystemStorageItemBase Context)
                            {
                                if (SelectedItems.Count > 1 && SelectedItems.Contains(Context))
                                {
                                    await ItemPresenter.SetCommandBarFlyoutWithExtraContextMenuItems(MixedFlyout, e.GetPosition((FrameworkElement)sender));
                                }
                                else
                                {
                                    if (SelectedItem == Context && SettingControl.IsDoubleClickEnable)
                                    {
                                        switch (Context)
                                        {
                                            case LinkStorageFile:
                                                {
                                                    await ItemPresenter.SetCommandBarFlyoutWithExtraContextMenuItems(LinkItemFlyout, e.GetPosition((FrameworkElement)sender));
                                                    break;
                                                }
                                            case FileSystemStorageFolder:
                                                {
                                                    await ItemPresenter.SetCommandBarFlyoutWithExtraContextMenuItems(FolderFlyout, e.GetPosition((FrameworkElement)sender));
                                                    break;
                                                }
                                            case FileSystemStorageFile:
                                                {
                                                    await ItemPresenter.SetCommandBarFlyoutWithExtraContextMenuItems(FileFlyout, e.GetPosition((FrameworkElement)sender));
                                                    break;
                                                }
                                        }
                                    }
                                    else
                                    {
                                        if (e.OriginalSource is TextBlock)
                                        {
                                            SelectedItem = Context;

                                            switch (Context)
                                            {
                                                case LinkStorageFile:
                                                    {
                                                        await ItemPresenter.SetCommandBarFlyoutWithExtraContextMenuItems(LinkItemFlyout, e.GetPosition((FrameworkElement)sender));
                                                        break;
                                                    }
                                                case FileSystemStorageFolder:
                                                    {
                                                        await ItemPresenter.SetCommandBarFlyoutWithExtraContextMenuItems(FolderFlyout, e.GetPosition((FrameworkElement)sender));
                                                        break;
                                                    }
                                                case FileSystemStorageFile:
                                                    {
                                                        await ItemPresenter.SetCommandBarFlyoutWithExtraContextMenuItems(FileFlyout, e.GetPosition((FrameworkElement)sender));
                                                        break;
                                                    }
                                            }
                                        }
                                        else
                                        {
                                            SelectedItem = null;
                                            await ItemPresenter.SetCommandBarFlyoutWithExtraContextMenuItems(EmptyFlyout, e.GetPosition((FrameworkElement)sender));
                                        }
                                    }
                                }
                            }
                            else
                            {
                                SelectedItem = null;
                                await ItemPresenter.SetCommandBarFlyoutWithExtraContextMenuItems(EmptyFlyout, e.GetPosition((FrameworkElement)sender));
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

            AppWindow NewWindow = await AppWindow.TryCreateAsync();
            NewWindow.RequestSize(new Size(420, 600));
            NewWindow.RequestMoveRelativeToCurrentViewContent(new Point(Window.Current.Bounds.Width / 2 - 200, Window.Current.Bounds.Height / 2 - 300));
            NewWindow.PersistedStateId = "Properties";
            NewWindow.Title = Globalization.GetString("Properties_Window_Title");
            NewWindow.TitleBar.ExtendsContentIntoTitleBar = true;
            NewWindow.TitleBar.ButtonForegroundColor = AppThemeController.Current.Theme == ElementTheme.Dark ? Colors.White : Colors.Black;
            NewWindow.TitleBar.ButtonBackgroundColor = Colors.Transparent;
            NewWindow.TitleBar.ButtonInactiveBackgroundColor = Colors.Transparent;

            ElementCompositionPreview.SetAppWindowContent(NewWindow, new PropertyBase(NewWindow, SelectedItem));
            WindowManagementPreview.SetPreferredMinSize(NewWindow, new Size(420, 600));

            await NewWindow.TryShowAsync();
        }

        private async void Compression_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();

            if (SelectedItem is FileSystemStorageFile File)
            {
                CompressDialog Dialog = new CompressDialog(File);

                if ((await Dialog.ShowAsync()) == ContentDialogResult.Primary)
                {
                    QueueTaskController.EnqueueCompressionOpeartion(Dialog.Type, Dialog.Algorithm, Dialog.Level, File.Path, Path.Combine(CurrentFolder.Path, Dialog.FileName));
                }
            }
        }

        private async void Decompression_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();

            if (SelectedItem is FileSystemStorageFile File)
            {
                if (File.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)
                    || File.Name.EndsWith(".tar", StringComparison.OrdinalIgnoreCase)
                    || File.Name.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase)
                    || File.Name.EndsWith(".tgz", StringComparison.OrdinalIgnoreCase)
                    || File.Name.EndsWith(".tar.bz2", StringComparison.OrdinalIgnoreCase)
                    || File.Name.EndsWith(".gz", StringComparison.OrdinalIgnoreCase)
                    || File.Name.EndsWith(".bz2", StringComparison.OrdinalIgnoreCase)
                    || File.Name.EndsWith(".rar", StringComparison.OrdinalIgnoreCase))

                {
                    QueueTaskController.EnqueueDecompressionOpeartion(File.Path, CurrentFolder.Path, (sender as FrameworkElement)?.Name == "DecompressionOption2");
                }
                else
                {
                    QueueContentDialog dialog = new QueueContentDialog
                    {
                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                        Content = Globalization.GetString("QueueDialog_FileTypeIncorrect_Content"),
                        CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                    };

                    await dialog.ShowAsync();
                }
            }
        }

        private async void ViewControl_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
        {
            e.Handled = true;

            DelayRenameCancel?.Cancel();

            if ((e.OriginalSource as FrameworkElement)?.DataContext is FileSystemStorageItemBase ReFile)
            {
                if (CoreWindow.GetForCurrentThread().GetKeyState(VirtualKey.Menu).HasFlag(CoreVirtualKeyStates.Down))
                {
                    AppWindow NewWindow = await AppWindow.TryCreateAsync();
                    NewWindow.RequestSize(new Size(420, 600));
                    NewWindow.RequestMoveRelativeToCurrentViewContent(new Point(Window.Current.Bounds.Width / 2 - 200, Window.Current.Bounds.Height / 2 - 300));
                    NewWindow.PersistedStateId = "Properties";
                    NewWindow.Title = Globalization.GetString("Properties_Window_Title");
                    NewWindow.TitleBar.ExtendsContentIntoTitleBar = true;
                    NewWindow.TitleBar.ButtonForegroundColor = AppThemeController.Current.Theme == ElementTheme.Dark ? Colors.White : Colors.Black;
                    NewWindow.TitleBar.ButtonBackgroundColor = Colors.Transparent;
                    NewWindow.TitleBar.ButtonInactiveBackgroundColor = Colors.Transparent;

                    ElementCompositionPreview.SetAppWindowContent(NewWindow, new PropertyBase(NewWindow, ReFile));
                    WindowManagementPreview.SetPreferredMinSize(NewWindow, new Size(420, 600));

                    await NewWindow.TryShowAsync();
                }
                else
                {
                    await EnterSelectedItemAsync(ReFile).ConfigureAwait(false);
                }
            }
            else if (e.OriginalSource is Grid)
            {
                if (Path.GetPathRoot(CurrentFolder?.Path).Equals(CurrentFolder?.Path, StringComparison.OrdinalIgnoreCase))
                {
                    await DisplayItemsInFolder(RootStorageFolder.Instance);
                }
                else if (Container.GoParentFolder.IsEnabled)
                {
                    Container.GoParentFolder_Click(null, null);
                }
            }
        }

        private async void Transcode_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();

            if (!await FileSystemStorageItemBase.CheckExistAsync(SelectedItem.Path))
            {
                QueueContentDialog Dialog = new QueueContentDialog
                {
                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                    Content = Globalization.GetString("QueueDialog_LocateFileFailure_Content"),
                    CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                };

                _ = await Dialog.ShowAsync();

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
                _ = await Dialog.ShowAsync();

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
                        if ((await SelectedItem.GetStorageItemAsync()) is StorageFile Source)
                        {
                            TranscodeDialog dialog = new TranscodeDialog(Source);

                            if ((await dialog.ShowAsync()) == ContentDialogResult.Primary)
                            {
                                try
                                {
                                    string DestFilePath = Path.Combine(CurrentFolder.Path, $"{Path.GetFileNameWithoutExtension(Source.Path)}.{dialog.MediaTranscodeEncodingProfile.ToLower()}");

                                    if (await FileSystemStorageItemBase.CreateAsync(DestFilePath, StorageItemTypes.File, CreateOption.GenerateUniqueName) is FileSystemStorageItemBase Item)
                                    {
                                        if (await Item.GetStorageItemAsync() is StorageFile DestinationFile)
                                        {
                                            await GeneralTransformer.TranscodeFromAudioOrVideoAsync(Source, DestinationFile, dialog.MediaTranscodeEncodingProfile, dialog.MediaTranscodeQuality, dialog.SpeedUp);
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

                                    await Dialog.ShowAsync();
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

                            _ = await Dialog.ShowAsync();
                        }

                        break;
                    }
                case ".png":
                case ".bmp":
                case ".jpg":
                case ".heic":
                case ".tiff":
                    {
                        if (SelectedItem is FileSystemStorageFile File)
                        {
                            TranscodeImageDialog Dialog = null;

                            using (IRandomAccessStream OriginStream = await File.GetRandomAccessStreamFromFileAsync(FileAccessMode.Read))
                            {
                                BitmapDecoder Decoder = await BitmapDecoder.CreateAsync(OriginStream);
                                Dialog = new TranscodeImageDialog(Decoder.PixelWidth, Decoder.PixelHeight);
                            }

                            if (await Dialog.ShowAsync() == ContentDialogResult.Primary)
                            {
                                await Container.LoadingActivation(true, Globalization.GetString("Progress_Tip_Transcoding"));

                                await GeneralTransformer.TranscodeFromImageAsync(File, Dialog.TargetFile, Dialog.IsEnableScale, Dialog.ScaleWidth, Dialog.ScaleHeight, Dialog.InterpolationMode);

                                await Container.LoadingActivation(false);
                            }
                        }

                        break;
                    }
            }
        }

        private async void FolderProperty_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();

            AppWindow NewWindow = await AppWindow.TryCreateAsync();
            NewWindow.RequestSize(new Size(420, 600));
            NewWindow.RequestMoveRelativeToCurrentViewContent(new Point(Window.Current.Bounds.Width / 2 - 200, Window.Current.Bounds.Height / 2 - 300));
            NewWindow.PersistedStateId = "Properties";
            NewWindow.Title = Globalization.GetString("Properties_Window_Title");
            NewWindow.TitleBar.ExtendsContentIntoTitleBar = true;
            NewWindow.TitleBar.ButtonForegroundColor = AppThemeController.Current.Theme == ElementTheme.Dark ? Colors.White : Colors.Black;
            NewWindow.TitleBar.ButtonBackgroundColor = Colors.Transparent;
            NewWindow.TitleBar.ButtonInactiveBackgroundColor = Colors.Transparent;

            ElementCompositionPreview.SetAppWindowContent(NewWindow, new PropertyBase(NewWindow, SelectedItem));
            WindowManagementPreview.SetPreferredMinSize(NewWindow, new Size(420, 600));

            await NewWindow.TryShowAsync();
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
                });

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

                await Task.Delay(500);

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
                _ = await dialog.ShowAsync();
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

            if (await FileSystemStorageItemBase.CheckExistAsync(CurrentFolder.Path))
            {
                if (CurrentFolder.Path.Equals(Path.GetPathRoot(CurrentFolder.Path), StringComparison.OrdinalIgnoreCase)
                     && CommonAccessCollection.DriveList.FirstOrDefault((Drive) => Drive.Path.Equals(CurrentFolder.Path, StringComparison.OrdinalIgnoreCase)) is DriveDataBase Data)
                {
                    DeviceInfoDialog Dialog = new DeviceInfoDialog(Data);
                    await Dialog.ShowAsync();
                }
                else
                {
                    AppWindow NewWindow = await AppWindow.TryCreateAsync();
                    NewWindow.RequestSize(new Size(420, 600));
                    NewWindow.RequestMoveRelativeToCurrentViewContent(new Point(Window.Current.Bounds.Width / 2 - 200, Window.Current.Bounds.Height / 2 - 300));
                    NewWindow.PersistedStateId = "Properties";
                    NewWindow.Title = Globalization.GetString("Properties_Window_Title");
                    NewWindow.TitleBar.ExtendsContentIntoTitleBar = true;
                    NewWindow.TitleBar.ButtonForegroundColor = AppThemeController.Current.Theme == ElementTheme.Dark ? Colors.White : Colors.Black;
                    NewWindow.TitleBar.ButtonBackgroundColor = Colors.Transparent;
                    NewWindow.TitleBar.ButtonInactiveBackgroundColor = Colors.Transparent;

                    ElementCompositionPreview.SetAppWindowContent(NewWindow, new PropertyBase(NewWindow, CurrentFolder));
                    WindowManagementPreview.SetPreferredMinSize(NewWindow, new Size(420, 600));

                    await NewWindow.TryShowAsync();
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

                _ = await Dialog.ShowAsync();
            }
        }

        private async void ItemOpen_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();

            if (SelectedItem is FileSystemStorageItemBase ReFile)
            {
                await EnterSelectedItemAsync(ReFile).ConfigureAwait(false);
            }
        }

        private void QRText_GotFocus(object sender, RoutedEventArgs e)
        {
            ((TextBox)sender).SelectAll();
        }

        private async void CreateFolder_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();

            if (await FileSystemStorageItemBase.CheckExistAsync(CurrentFolder.Path))
            {
                if (await FileSystemStorageItemBase.CreateAsync(Path.Combine(CurrentFolder.Path, Globalization.GetString("Create_NewFolder_Admin_Name")), StorageItemTypes.Folder, CreateOption.GenerateUniqueName) is FileSystemStorageItemBase NewFolder)
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
                                    EditBox.BeforeTextChanging += EditBox_BeforeTextChanging;
                                    EditBox.PreviewKeyDown += EditBox_PreviewKeyDown;
                                    EditBox.LostFocus += EditBox_LostFocus;
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
                            await Task.Delay(500);
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

                    await dialog.ShowAsync();
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

                _ = await Dialog.ShowAsync();
            }
        }

        private async void EmptyFlyout_Opening(object sender, object e)
        {
            try
            {
                DataPackageView Package = Clipboard.GetContent();

                if (await Package.CheckIfContainsAvailableDataAsync())
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
                if (!await FileSystemStorageItemBase.CheckExistAsync(SelectedItem.Path))
                {
                    QueueContentDialog Dialog = new QueueContentDialog
                    {
                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                        Content = Globalization.GetString("QueueDialog_LocateFileFailure_Content"),
                        CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                    };

                    _ = await Dialog.ShowAsync();
                }
                else
                {
                    if ((await SelectedItem.GetStorageItemAsync()) is StorageFile ShareItem)
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

                        _ = await Dialog.ShowAsync();
                    }
                }
            }
        }

        private async void Refresh_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();

            try
            {
                if (await FileSystemStorageItemBase.CheckExistAsync(CurrentFolder.Path))
                {
                    await DisplayItemsInFolder(CurrentFolder, true);
                }
                else
                {
                    QueueContentDialog Dialog = new QueueContentDialog
                    {
                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                        Content = Globalization.GetString("QueueDialog_LocateFolderFailure_Content"),
                        CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                    };

                    _ = await Dialog.ShowAsync();
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
                    await EnterSelectedItemAsync(ReFile).ConfigureAwait(false);
                }
            }
        }

        public async Task EnterSelectedItem(string Path, bool RunAsAdministrator = false)
        {
            FileSystemStorageItemBase Item = await FileSystemStorageItemBase.OpenAsync(Path);

            await EnterSelectedItemAsync(Item, RunAsAdministrator).ConfigureAwait(false);
        }

        public async Task EnterSelectedItemAsync(FileSystemStorageItemBase ReFile, bool RunAsAdministrator = false)
        {
            if (Interlocked.Exchange(ref TabTarget, ReFile) == null)
            {
                try
                {
                    switch (TabTarget)
                    {
                        case FileSystemStorageFile File:
                            {
                                if (!await FileSystemStorageItemBase.CheckExistAsync(File.Path))
                                {
                                    QueueContentDialog Dialog = new QueueContentDialog
                                    {
                                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                        Content = Globalization.GetString("QueueDialog_LocateFileFailure_Content"),
                                        CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                                    };

                                    _ = await Dialog.ShowAsync();

                                    return;
                                }

                                switch (File.Type.ToLower())
                                {
                                    case ".exe":
                                    case ".bat":
                                    case ".msi":
                                        {
                                            using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableController())
                                            {
                                                if (!await Exclusive.Controller.RunAsync(File.Path, Path.GetDirectoryName(File.Path), WindowState.Normal, RunAsAdministrator))
                                                {
                                                    QueueContentDialog Dialog = new QueueContentDialog
                                                    {
                                                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                                        Content = Globalization.GetString("QueueDialog_LaunchFailed_Content"),
                                                        CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                                                    };

                                                    await Dialog.ShowAsync();
                                                }
                                            }

                                            break;
                                        }
                                    case ".msc":
                                        {
                                            using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableController())
                                            {
                                                if (!await Exclusive.Controller.RunAsync("powershell.exe", string.Empty, WindowState.Normal, false, true, false, "-Command", File.Path))
                                                {
                                                    QueueContentDialog Dialog = new QueueContentDialog
                                                    {
                                                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                                        Content = Globalization.GetString("QueueDialog_LaunchFailed_Content"),
                                                        CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                                                    };

                                                    await Dialog.ShowAsync();
                                                }
                                            }

                                            break;
                                        }
                                    case ".lnk":
                                        {
                                            if (File is LinkStorageFile Item)
                                            {
                                                if (Item.LinkType == ShellLinkType.Normal)
                                                {
                                                    switch (await FileSystemStorageItemBase.OpenAsync(Item.LinkTargetPath))
                                                    {
                                                        case FileSystemStorageFolder:
                                                            {
                                                                await DisplayItemsInFolder(Item.LinkTargetPath);
                                                                break;
                                                            }
                                                        case FileSystemStorageFile:
                                                            {
                                                                await Item.LaunchAsync();
                                                                break;
                                                            }
                                                    }
                                                }
                                                else
                                                {
                                                    await Item.LaunchAsync();
                                                }
                                            }

                                            break;
                                        }
                                    case ".url":
                                        {
                                            if (File is UrlStorageFile Item)
                                            {
                                                await Item.LaunchAsync();
                                            }

                                            break;
                                        }
                                    default:
                                        {
                                            string AdminExecutablePath = SQLite.Current.GetDefaultProgramPickerRecord(File.Type);

                                            if (string.IsNullOrEmpty(AdminExecutablePath) || AdminExecutablePath == Package.Current.Id.FamilyName)
                                            {
                                                if (!TryOpenInternally(File))
                                                {
                                                    if (await File.GetStorageItemAsync() is StorageFile SFile)
                                                    {
                                                        if (!await Launcher.LaunchFileAsync(SFile))
                                                        {
                                                            using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableController())
                                                            {
                                                                if (!await Exclusive.Controller.RunAsync(File.Path))
                                                                {
                                                                    QueueContentDialog Dialog = new QueueContentDialog
                                                                    {
                                                                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                                                        Content = Globalization.GetString("QueueDialog_LaunchFailed_Content"),
                                                                        CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                                                                    };

                                                                    await Dialog.ShowAsync();
                                                                }
                                                            }
                                                        }
                                                    }
                                                    else
                                                    {
                                                        using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableController())
                                                        {
                                                            if (!await Exclusive.Controller.RunAsync(File.Path))
                                                            {
                                                                QueueContentDialog Dialog = new QueueContentDialog
                                                                {
                                                                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                                                    Content = Globalization.GetString("QueueDialog_LaunchFailed_Content"),
                                                                    CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                                                                };

                                                                await Dialog.ShowAsync();
                                                            }
                                                        }
                                                    }
                                                }
                                            }
                                            else
                                            {
                                                if (Path.IsPathRooted(AdminExecutablePath))
                                                {
                                                    using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableController())
                                                    {
                                                        if (!await Exclusive.Controller.RunAsync(AdminExecutablePath, Path.GetDirectoryName(AdminExecutablePath), Parameters: File.Path))
                                                        {
                                                            QueueContentDialog Dialog = new QueueContentDialog
                                                            {
                                                                Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                                                Content = Globalization.GetString("QueueDialog_LaunchFailed_Content"),
                                                                CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                                                            };

                                                            await Dialog.ShowAsync();
                                                        }
                                                    }
                                                }
                                                else
                                                {
                                                    if ((await Launcher.FindFileHandlersAsync(File.Type)).FirstOrDefault((Item) => Item.PackageFamilyName == AdminExecutablePath) is AppInfo Info)
                                                    {
                                                        if (await File.GetStorageItemAsync() is StorageFile InnerFile)
                                                        {
                                                            if (!await Launcher.LaunchFileAsync(InnerFile, new LauncherOptions { TargetApplicationPackageFamilyName = Info.PackageFamilyName, DisplayApplicationPicker = false }))
                                                            {
                                                                using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableController())
                                                                {
                                                                    if (!await Exclusive.Controller.LaunchUWPFromAUMIDAsync(Info.AppUserModelId, File.Path))
                                                                    {
                                                                        LogTracer.Log("Launch UWP failed and fall back to open ProgramPickerDialog");

                                                                        await OpenFileWithProgramPicker(File);
                                                                    }
                                                                }
                                                            }
                                                        }
                                                        else
                                                        {
                                                            using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableController())
                                                            {
                                                                if (!await Exclusive.Controller.LaunchUWPFromAUMIDAsync(Info.AppUserModelId, File.Path))
                                                                {
                                                                    QueueContentDialog Dialog = new QueueContentDialog
                                                                    {
                                                                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                                                        Content = Globalization.GetString("QueueDialog_UnableAccessFile_Content"),
                                                                        CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                                                                    };

                                                                    await Dialog.ShowAsync();
                                                                }
                                                            }
                                                        }
                                                    }
                                                    else
                                                    {
                                                        await OpenFileWithProgramPicker(File);
                                                    }
                                                }
                                            }

                                            break;
                                        }
                                }

                                break;
                            }
                        case FileSystemStorageFolder Folder:
                            {
                                await DisplayItemsInFolder(Folder);
                                break;
                            }
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

        private bool TryOpenInternally(FileSystemStorageFile File)
        {
            Type InternalType = File.Type.ToLower() switch
            {
                ".jpg" or ".png" or ".bmp" => typeof(PhotoViewer),
                ".mkv" or ".mp4" or ".mp3" or
                ".flac" or ".wma" or ".wmv" or
                ".m4a" or ".mov" or ".alac" => typeof(MediaPlayer),
                ".txt" => typeof(TextViewer),
                ".pdf" => typeof(PdfReader),
                _ => null
            };


            if (InternalType != null)
            {
                NavigationTransitionInfo NavigationTransition = AnimationController.Current.IsEnableAnimation
                                                ? new DrillInNavigationTransitionInfo()
                                                : new SuppressNavigationTransitionInfo();

                Container.Frame.Navigate(InternalType, File, NavigationTransition);

                return true;
            }
            else
            {
                return false;
            }
        }

        private async Task OpenFileWithProgramPicker(FileSystemStorageFile File)
        {
            ProgramPickerDialog Dialog = new ProgramPickerDialog(File);

            if (await Dialog.ShowAsync() == ContentDialogResult.Primary)
            {
                if (Dialog.SelectedProgram.Path.Equals(Package.Current.Id.FamilyName, StringComparison.OrdinalIgnoreCase))
                {
                    TryOpenInternally(File);
                }
                else
                {
                    if (Path.IsPathRooted(Dialog.SelectedProgram.Path))
                    {
                        using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableController())
                        {
                            if (!await Exclusive.Controller.RunAsync(Dialog.SelectedProgram.Path, Path.GetDirectoryName(Dialog.SelectedProgram.Path), Parameters: File.Path))
                            {
                                QueueContentDialog Dialog1 = new QueueContentDialog
                                {
                                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                    Content = Globalization.GetString("QueueDialog_LaunchFailed_Content"),
                                    CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                                };

                                await Dialog1.ShowAsync();
                            }
                        }
                    }
                    else if (await File.GetStorageItemAsync() is StorageFile InnerFile)
                    {
                        if (!await Launcher.LaunchFileAsync(InnerFile, new LauncherOptions { TargetApplicationPackageFamilyName = Dialog.SelectedProgram.Path, DisplayApplicationPicker = false }))
                        {
                            using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableController())
                            {
                                if (!await Exclusive.Controller.LaunchUWPFromPfnAsync(Dialog.SelectedProgram.Path, File.Path))
                                {
                                    QueueContentDialog Dialog1 = new QueueContentDialog
                                    {
                                        Title = Globalization.GetString("Common_Dialog_TipTitle"),
                                        Content = Globalization.GetString("QueueDialog_OpenFailure_Content"),
                                        PrimaryButtonText = Globalization.GetString("QueueDialog_OpenFailure_PrimaryButton"),
                                        CloseButtonText = Globalization.GetString("Common_Dialog_CancelButton")
                                    };

                                    if (await Dialog1.ShowAsync() == ContentDialogResult.Primary)
                                    {
                                        if (!await Launcher.LaunchFileAsync(InnerFile))
                                        {
                                            await Launcher.LaunchFileAsync(InnerFile, new LauncherOptions { DisplayApplicationPicker = true });
                                        }
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableController())
                        {
                            if (!await Exclusive.Controller.LaunchUWPFromPfnAsync(Dialog.SelectedProgram.Path, File.Path))
                            {
                                QueueContentDialog Dialog1 = new QueueContentDialog
                                {
                                    Title = Globalization.GetString("Common_Dialog_TipTitle"),
                                    Content = Globalization.GetString("QueueDialog_OpenFailure_Content"),
                                    PrimaryButtonText = Globalization.GetString("QueueDialog_OpenFailure_PrimaryButton"),
                                    CloseButtonText = Globalization.GetString("Common_Dialog_CancelButton")
                                };
                            }
                        }
                    }
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

                _ = await Dialog.ShowAsync();
            }
            else
            {
                if ((await SelectedItem.GetStorageItemAsync()) is StorageFile File)
                {
                    VideoEditDialog Dialog = new VideoEditDialog(File);

                    if ((await Dialog.ShowAsync()) == ContentDialogResult.Primary)
                    {
                        if (await CurrentFolder.GetStorageItemAsync() is StorageFolder Folder)
                        {
                            StorageFile ExportFile = await Folder.CreateFileAsync($"{File.DisplayName} - {Globalization.GetString("Crop_Image_Name_Tail")}{Dialog.ExportFileType}", CreationCollisionOption.GenerateUniqueName);

                            await GeneralTransformer.GenerateCroppedVideoFromOriginAsync(ExportFile, Dialog.Composition, Dialog.MediaEncoding, Dialog.TrimmingPreference);
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

                    _ = await Dialog.ShowAsync();
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

                _ = await Dialog.ShowAsync();

                return;
            }

            if ((await SelectedItem.GetStorageItemAsync()) is StorageFile Item)
            {
                VideoMergeDialog Dialog = new VideoMergeDialog(Item);

                if ((await Dialog.ShowAsync()) == ContentDialogResult.Primary)
                {
                    if (await CurrentFolder.GetStorageItemAsync() is StorageFolder Folder)
                    {
                        StorageFile ExportFile = await Folder.CreateFileAsync($"{Item.DisplayName} - {Globalization.GetString("Merge_Image_Name_Tail")}{Dialog.ExportFileType}", CreationCollisionOption.GenerateUniqueName);

                        await GeneralTransformer.GenerateMergeVideoFromOriginAsync(ExportFile, Dialog.Composition, Dialog.MediaEncoding);
                    }
                }
            }
        }

        private async void ChooseOtherApp_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();

            if (SelectedItem is FileSystemStorageFile File)
            {
                await OpenFileWithProgramPicker(File);
            }
        }

        private async void RunWithSystemAuthority_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();

            if (SelectedItem != null)
            {
                await EnterSelectedItemAsync(SelectedItem, true).ConfigureAwait(false);
            }
        }

        private void ListHeaderName_Click(object sender, RoutedEventArgs e)
        {
            PathConfiguration Config = SQLite.Current.GetPathConfiguration(CurrentFolder.Path);

            if (Config.SortDirection == SortDirection.Ascending)
            {
                SortCollectionGenerator.SavePathSortState(CurrentFolder.Path, SortTarget.Name, SortDirection.Descending);
            }
            else
            {
                SortCollectionGenerator.SavePathSortState(CurrentFolder.Path, SortTarget.Name, SortDirection.Ascending);
            }
        }

        private void ListHeaderModifiedTime_Click(object sender, RoutedEventArgs e)
        {
            PathConfiguration Config = SQLite.Current.GetPathConfiguration(CurrentFolder.Path);

            if (Config.SortDirection == SortDirection.Ascending)
            {
                SortCollectionGenerator.SavePathSortState(CurrentFolder.Path, SortTarget.ModifiedTime, SortDirection.Descending);
            }
            else
            {
                SortCollectionGenerator.SavePathSortState(CurrentFolder.Path, SortTarget.ModifiedTime, SortDirection.Ascending);
            }
        }

        private void ListHeaderType_Click(object sender, RoutedEventArgs e)
        {
            PathConfiguration Config = SQLite.Current.GetPathConfiguration(CurrentFolder.Path);

            if (Config.SortDirection == SortDirection.Ascending)
            {
                SortCollectionGenerator.SavePathSortState(CurrentFolder.Path, SortTarget.Type, SortDirection.Descending);
            }
            else
            {
                SortCollectionGenerator.SavePathSortState(CurrentFolder.Path, SortTarget.Type, SortDirection.Ascending);
            }
        }

        private void ListHeaderSize_Click(object sender, RoutedEventArgs e)
        {
            PathConfiguration Config = SQLite.Current.GetPathConfiguration(CurrentFolder.Path);

            if (Config.SortDirection == SortDirection.Ascending)
            {
                SortCollectionGenerator.SavePathSortState(CurrentFolder.Path, SortTarget.Size, SortDirection.Descending);
            }
            else
            {
                SortCollectionGenerator.SavePathSortState(CurrentFolder.Path, SortTarget.Size, SortDirection.Ascending);
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

            if ((await Dialog.ShowAsync()) == ContentDialogResult.Primary)
            {
                try
                {
                    switch (Path.GetExtension(Dialog.NewFileName))
                    {
                        case ".zip":
                            {
                                await SpecialTypeGenerator.Current.CreateZipFile(CurrentFolder.Path, Dialog.NewFileName);
                                break;
                            }
                        case ".rtf":
                            {
                                await SpecialTypeGenerator.Current.CreateRtfFile(CurrentFolder.Path, Dialog.NewFileName);
                                break;
                            }
                        case ".xlsx":
                            {
                                await SpecialTypeGenerator.Current.CreateExcelFile(CurrentFolder.Path, Dialog.NewFileName);
                                break;
                            }
                        case ".lnk":
                            {
                                LinkOptionsDialog dialog = new LinkOptionsDialog();

                                if (await dialog.ShowAsync() == ContentDialogResult.Primary)
                                {
                                    using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableController())
                                    {
                                        if (!await Exclusive.Controller.CreateLinkAsync(Path.Combine(CurrentFolder.Path, Dialog.NewFileName), dialog.Path, dialog.WorkDirectory, dialog.WindowState, dialog.HotKey, dialog.Comment, dialog.Arguments))
                                        {
                                            throw new UnauthorizedAccessException();
                                        }
                                    }
                                }

                                break;
                            }
                        default:
                            {
                                if (await FileSystemStorageItemBase.CreateAsync(Path.Combine(CurrentFolder.Path, Dialog.NewFileName), StorageItemTypes.File, CreateOption.GenerateUniqueName) == null)
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

                    await dialog.ShowAsync();
                }
            }
        }

        private async void CompressFolder_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();

            if (SelectedItem is FileSystemStorageFolder Folder)
            {
                if (!await FileSystemStorageItemBase.CheckExistAsync(Folder.Path))
                {
                    QueueContentDialog Dialog = new QueueContentDialog
                    {
                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                        Content = Globalization.GetString("QueueDialog_LocateFolderFailure_Content"),
                        CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                    };

                    _ = await Dialog.ShowAsync();

                    return;
                }

                CompressDialog dialog = new CompressDialog(Folder);

                if ((await dialog.ShowAsync()) == ContentDialogResult.Primary)
                {
                    QueueTaskController.EnqueueCompressionOpeartion(dialog.Type, dialog.Algorithm, dialog.Level, Folder.Path, Path.Combine(CurrentFolder.Path, dialog.FileName));
                }
            }
        }

        private async void ViewControl_DragOver(object sender, DragEventArgs e)
        {
            DragOperationDeferral Deferral = e.GetDeferral();

            try
            {
                e.Handled = true;

                if (Container.BladeViewer.FindChildOfType<ScrollViewer>() is ScrollViewer Viewer)
                {
                    double XOffset = e.GetPosition(Container.BladeViewer).X;
                    double ScrollThreshold = Math.Min((Viewer.ActualWidth - 200) / 2, 100);
                    double HorizontalRightScrollThreshold = Viewer.ActualWidth - ScrollThreshold;
                    double HorizontalLeftScrollThreshold = ScrollThreshold;

                    if (XOffset > HorizontalRightScrollThreshold)
                    {
                        Viewer.ChangeView(Viewer.HorizontalOffset + XOffset - HorizontalRightScrollThreshold, null, null, false);
                    }
                    else if (XOffset < HorizontalLeftScrollThreshold)
                    {
                        Viewer.ChangeView(Viewer.HorizontalOffset + XOffset - HorizontalLeftScrollThreshold, null, null, false);
                    }
                }

                Container.CurrentPresenter = this;

                if (await e.DataView.CheckIfContainsAvailableDataAsync())
                {
                    if (e.Modifiers.HasFlag(DragDropModifiers.Control))
                    {
                        e.AcceptedOperation = DataPackageOperation.Move;
                        e.DragUIOverride.Caption = $"{Globalization.GetString("Drag_Tip_MoveTo")} \"{CurrentFolder.Name}\"";
                    }
                    else
                    {
                        e.AcceptedOperation = DataPackageOperation.Copy;
                        e.DragUIOverride.Caption = $"{Globalization.GetString("Drag_Tip_CopyTo")} \"{CurrentFolder.Name}\"";
                    }

                    e.DragUIOverride.IsContentVisible = true;
                    e.DragUIOverride.IsCaptionVisible = true;
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
                DelayEnterCancel?.Cancel();

                e.Handled = true;

                IReadOnlyList<string> PathList = await e.DataView.GetAsPathListAsync();

                if (PathList.Count > 0)
                {
                    switch ((sender as SelectorItem).Content)
                    {
                        case FileSystemStorageFolder Folder:
                            {
                                switch (e.AcceptedOperation)
                                {
                                    case DataPackageOperation.Copy:
                                        {
                                            TaskCompletionSource<bool> CompletionSource = new TaskCompletionSource<bool>();

                                            void OnFinished(object s, EventArgs e)
                                            {
                                                CompletionSource.TrySetResult(true);
                                            }

                                            QueueTaskController.EnqueueCopyOpeartion(PathList, Folder.Path, OnFinished, OnFinished, OnFinished);

                                            await CompletionSource.Task;

                                            break;
                                        }
                                    case DataPackageOperation.Move:
                                        {
                                            TaskCompletionSource<bool> CompletionSource = new TaskCompletionSource<bool>();

                                            void OnFinished(object s, EventArgs e)
                                            {
                                                CompletionSource.TrySetResult(true);
                                            }

                                            QueueTaskController.EnqueueMoveOpeartion(PathList, Folder.Path, OnFinished, OnFinished, OnFinished);

                                            await CompletionSource.Task;

                                            break;
                                        }
                                }

                                break;
                            }
                        case FileSystemStorageFile File when File.Type.Equals(".exe", StringComparison.OrdinalIgnoreCase):
                            {
                                using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableController())
                                {
                                    if (!await Exclusive.Controller.RunAsync(File.Path, Path.GetDirectoryName(File.Path), WindowState.Normal, Parameters: PathList.ToArray()))
                                    {
                                        QueueContentDialog Dialog = new QueueContentDialog
                                        {
                                            Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                            Content = Globalization.GetString("QueueDialog_LaunchFailed_Content"),
                                            CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                                        };

                                        await Dialog.ShowAsync();
                                    }
                                }

                                break;
                            }
                    }
                }
            }
            catch (Exception ex) when (ex.HResult is unchecked((int)0x80040064) or unchecked((int)0x8004006A))
            {
                if ((sender as SelectorItem).Content is FileSystemStorageItemBase Item)
                {
                    QueueTaskController.EnqueueRemoteCopyOpeartion(Item.Path);
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

                _ = await dialog.ShowAsync();
            }
            finally
            {
                Deferral.Complete();
            }
        }

        private void ViewControl_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                args.ItemContainer.AllowDrop = false;

                args.ItemContainer.DragStarting -= ItemContainer_DragStarting;
                args.ItemContainer.Drop -= ItemContainer_Drop;
                args.ItemContainer.DragOver -= ItemContainer_DragOver;
                args.ItemContainer.PointerEntered -= ItemContainer_PointerEntered;
                args.ItemContainer.PointerExited -= ItemContainer_PointerExited;
                args.ItemContainer.DragEnter -= ItemContainer_DragEnter;
                args.ItemContainer.PointerCanceled -= ItemContainer_PointerCanceled;
                args.ItemContainer.DragLeave -= ItemContainer_DragLeave;
            }
            else
            {
                switch (args.Item)
                {
                    case FileSystemStorageFolder:
                        {
                            args.ItemContainer.AllowDrop = true;
                            args.ItemContainer.DragEnter += ItemContainer_DragEnter;
                            args.ItemContainer.DragLeave += ItemContainer_DragLeave;
                            break;
                        }
                    case FileSystemStorageFile File when File.Type.Equals(".exe", StringComparison.OrdinalIgnoreCase):
                        {
                            args.ItemContainer.AllowDrop = true;
                            break;
                        }
                }

                args.ItemContainer.Drop += ItemContainer_Drop;
                args.ItemContainer.DragOver += ItemContainer_DragOver;
                args.ItemContainer.DragStarting += ItemContainer_DragStarting;
                args.ItemContainer.PointerEntered += ItemContainer_PointerEntered;
                args.ItemContainer.PointerExited += ItemContainer_PointerExited;
                args.ItemContainer.PointerCanceled += ItemContainer_PointerCanceled;

                args.RegisterUpdateCallback(async (s, e) =>
                {
                    if (e.Item is FileSystemStorageItemBase Item)
                    {
                        switch (Container.ViewModeControl.ViewModeIndex)
                        {
                            case 0:
                            case 1:
                            case 2:
                                {
                                    Item.SetThumbnailMode(ThumbnailMode.ListView);
                                    break;
                                }
                            default:
                                {
                                    Item.SetThumbnailMode(ThumbnailMode.SingleItem);
                                    break;
                                }
                        }

                        await Item.LoadAsync().ConfigureAwait(false);
                    }
                });
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

                Task.Delay(2000).ContinueWith((task, input) =>
                {
                    try
                    {
                        if (input is CancellationTokenSource Cancel && !Cancel.IsCancellationRequested)
                        {
                            _ = EnterSelectedItemAsync(Item);
                        }
                    }
                    catch (Exception ex)
                    {
                        LogTracer.Log(ex, "An exception was thew in DelayEnterProcess");
                    }
                }, DelayEnterCancel, TaskScheduler.FromCurrentSynchronizationContext());
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

                await args.Data.SetupDataPackageAsync(DragList);
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
            DragOperationDeferral Deferral = e.GetDeferral();

            try
            {
                e.Handled = true;

                if (Container.BladeViewer.FindChildOfType<ScrollViewer>() is ScrollViewer Viewer)
                {
                    double XOffset = e.GetPosition(Container.BladeViewer).X;
                    double HorizontalRightScrollThreshold = Viewer.ActualWidth - 50;
                    double HorizontalLeftScrollThreshold = 50;

                    if (XOffset > HorizontalRightScrollThreshold)
                    {
                        Viewer.ChangeView(Viewer.HorizontalOffset + XOffset - HorizontalRightScrollThreshold, null, null, false);
                    }
                    else if (XOffset < HorizontalLeftScrollThreshold)
                    {
                        Viewer.ChangeView(Viewer.HorizontalOffset + XOffset - HorizontalLeftScrollThreshold, null, null, false);
                    }
                }

                switch ((sender as SelectorItem)?.Content)
                {
                    case FileSystemStorageFolder Folder:
                        {
                            if (await e.DataView.CheckIfContainsAvailableDataAsync())
                            {
                                if (e.Modifiers.HasFlag(DragDropModifiers.Control))
                                {
                                    e.AcceptedOperation = DataPackageOperation.Move;
                                    e.DragUIOverride.Caption = $"{Globalization.GetString("Drag_Tip_MoveTo")} \"{Folder.Name}\"";
                                }
                                else
                                {
                                    e.AcceptedOperation = DataPackageOperation.Copy;
                                    e.DragUIOverride.Caption = $"{Globalization.GetString("Drag_Tip_CopyTo")} \"{Folder.Name}\"";
                                }

                                e.DragUIOverride.IsContentVisible = true;
                                e.DragUIOverride.IsCaptionVisible = true;
                            }
                            else
                            {
                                e.AcceptedOperation = DataPackageOperation.None;
                            }

                            break;
                        }
                    case FileSystemStorageFile File when File.Type.Equals(".exe", StringComparison.OrdinalIgnoreCase):
                        {
                            IReadOnlyList<string> PathArray = await e.DataView.GetAsPathListAsync();

                            if (PathArray.Any() && PathArray.All((Path) => !Path.Equals(File.Path, StringComparison.OrdinalIgnoreCase)))
                            {
                                e.AcceptedOperation = DataPackageOperation.Link;
                                e.DragUIOverride.Caption = Globalization.GetString("Drag_Tip_RunWith").Replace("{Placeholder}", $"\"{File.Name}\"");

                                e.DragUIOverride.IsContentVisible = true;
                                e.DragUIOverride.IsCaptionVisible = true;
                            }
                            else
                            {
                                e.AcceptedOperation = DataPackageOperation.None;
                            }

                            break;
                        }
                    default:
                        {
                            e.AcceptedOperation = DataPackageOperation.None;
                            break;
                        }
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

        private void ItemContainer_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            if ((e.OriginalSource as FrameworkElement)?.DataContext is FileSystemStorageItemBase Item)
            {
                if (!SettingControl.IsDoubleClickEnable
                    && ItemPresenter.SelectionMode != ListViewSelectionMode.Multiple
                    && !SelectedItems.Contains(Item)
                    && !e.KeyModifiers.HasFlag(VirtualKeyModifiers.Control)
                    && !e.KeyModifiers.HasFlag(VirtualKeyModifiers.Shift)
                    && !Container.BlockKeyboardShortCutInput)
                {
                    DelaySelectionCancel?.Cancel();
                    DelaySelectionCancel?.Dispose();
                    DelaySelectionCancel = new CancellationTokenSource();

                    Task.Delay(700).ContinueWith((task, input) =>
                    {
                        if (input is CancellationTokenSource Cancel && !Cancel.IsCancellationRequested)
                        {
                            SelectedItem = Item;
                        }
                    }, DelaySelectionCancel, TaskScheduler.FromCurrentSynchronizationContext());
                }
            }
        }

        private void ItemContainer_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            DelaySelectionCancel?.Cancel();
        }

        private void ItemContainer_PointerCanceled(object sender, PointerRoutedEventArgs e)
        {
            DelayEnterCancel?.Cancel();
            DelayRenameCancel?.Cancel();
            DelaySelectionCancel?.Cancel();
        }

        private async void ViewControl_Drop(object sender, DragEventArgs e)
        {
            DragOperationDeferral Deferral = e.GetDeferral();

            try
            {
                e.Handled = true;

                IReadOnlyList<string> PathList = await e.DataView.GetAsPathListAsync();

                if (PathList.Count > 0)
                {
                    switch (e.AcceptedOperation)
                    {
                        case DataPackageOperation.Copy:
                            {
                                TaskCompletionSource<bool> CompletionSource = new TaskCompletionSource<bool>();

                                void OnFinished(object s, EventArgs e)
                                {
                                    CompletionSource.TrySetResult(true);
                                }

                                QueueTaskController.EnqueueCopyOpeartion(PathList, CurrentFolder.Path, OnFinished, OnFinished, OnFinished);

                                await CompletionSource.Task;

                                break;
                            }
                        case DataPackageOperation.Move:
                            {
                                if (PathList.All((Item) => Path.GetDirectoryName(Item) != CurrentFolder.Path))
                                {
                                    TaskCompletionSource<bool> CompletionSource = new TaskCompletionSource<bool>();

                                    void OnFinished(object s, EventArgs e)
                                    {
                                        CompletionSource.TrySetResult(true);
                                    }

                                    QueueTaskController.EnqueueMoveOpeartion(PathList, CurrentFolder.Path, OnFinished, OnFinished, OnFinished);

                                    await CompletionSource.Task;
                                }

                                break;
                            }
                    }
                }
            }
            catch (Exception ex) when (ex.HResult is unchecked((int)0x80040064) or unchecked((int)0x8004006A))
            {
                QueueTaskController.EnqueueRemoteCopyOpeartion(CurrentFolder.Path);
            }
            catch
            {
                QueueContentDialog dialog = new QueueContentDialog
                {
                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                    Content = Globalization.GetString("QueueDialog_FailToGetClipboardError_Content"),
                    CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                };

                _ = await dialog.ShowAsync();
            }
            finally
            {
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
                            await ItemPresenter.SetCommandBarFlyoutWithExtraContextMenuItems(MixedFlyout, e.GetPosition((FrameworkElement)sender));
                        }
                        else
                        {
                            SelectedItem = Context;

                            switch (Context)
                            {
                                case LinkStorageFile:
                                    {
                                        await ItemPresenter.SetCommandBarFlyoutWithExtraContextMenuItems(LinkItemFlyout, e.GetPosition((FrameworkElement)sender));
                                        break;
                                    }
                                case FileSystemStorageFile:
                                    {
                                        await ItemPresenter.SetCommandBarFlyoutWithExtraContextMenuItems(FileFlyout, e.GetPosition((FrameworkElement)sender));
                                        break;
                                    }
                                case FileSystemStorageFolder:
                                    {
                                        await ItemPresenter.SetCommandBarFlyoutWithExtraContextMenuItems(FolderFlyout, e.GetPosition((FrameworkElement)sender));
                                        break;
                                    }
                            }
                        }
                    }
                    else
                    {
                        SelectedItem = null;
                        await ItemPresenter.SetCommandBarFlyoutWithExtraContextMenuItems(EmptyFlyout, e.GetPosition((FrameworkElement)sender));
                    }
                }
                else
                {
                    if (e.OriginalSource is FrameworkElement Element)
                    {
                        if (Element.Name == "EmptyTextblock")
                        {
                            SelectedItem = null;
                            await ItemPresenter.SetCommandBarFlyoutWithExtraContextMenuItems(EmptyFlyout, e.GetPosition((FrameworkElement)sender));
                        }
                        else
                        {
                            if (Element.DataContext is FileSystemStorageItemBase Context)
                            {
                                if (SelectedItems.Count > 1 && SelectedItems.Contains(Context))
                                {
                                    await ItemPresenter.SetCommandBarFlyoutWithExtraContextMenuItems(MixedFlyout, e.GetPosition((FrameworkElement)sender));
                                }
                                else
                                {
                                    if (SelectedItem == Context && SettingControl.IsDoubleClickEnable)
                                    {
                                        switch (Context)
                                        {
                                            case LinkStorageFile:
                                                {
                                                    await ItemPresenter.SetCommandBarFlyoutWithExtraContextMenuItems(LinkItemFlyout, e.GetPosition((FrameworkElement)sender));
                                                    break;
                                                }
                                            case FileSystemStorageFile:
                                                {
                                                    await ItemPresenter.SetCommandBarFlyoutWithExtraContextMenuItems(FileFlyout, e.GetPosition((FrameworkElement)sender));
                                                    break;
                                                }
                                            case FileSystemStorageFolder:
                                                {
                                                    await ItemPresenter.SetCommandBarFlyoutWithExtraContextMenuItems(FolderFlyout, e.GetPosition((FrameworkElement)sender));
                                                    break;
                                                }
                                        }
                                    }
                                    else
                                    {
                                        if (e.OriginalSource is TextBlock)
                                        {
                                            SelectedItem = Context;

                                            switch (Context)
                                            {
                                                case LinkStorageFile:
                                                    {
                                                        await ItemPresenter.SetCommandBarFlyoutWithExtraContextMenuItems(LinkItemFlyout, e.GetPosition((FrameworkElement)sender));
                                                        break;
                                                    }
                                                case FileSystemStorageFile:
                                                    {
                                                        await ItemPresenter.SetCommandBarFlyoutWithExtraContextMenuItems(FileFlyout, e.GetPosition((FrameworkElement)sender));
                                                        break;
                                                    }
                                                case FileSystemStorageFolder:
                                                    {
                                                        await ItemPresenter.SetCommandBarFlyoutWithExtraContextMenuItems(FolderFlyout, e.GetPosition((FrameworkElement)sender));
                                                        break;
                                                    }
                                            }
                                        }
                                        else
                                        {
                                            SelectedItem = null;
                                            await ItemPresenter.SetCommandBarFlyoutWithExtraContextMenuItems(EmptyFlyout, e.GetPosition((FrameworkElement)sender));
                                        }
                                    }
                                }
                            }
                            else
                            {
                                SelectedItem = null;
                                await ItemPresenter.SetCommandBarFlyoutWithExtraContextMenuItems(EmptyFlyout, e.GetPosition((FrameworkElement)sender));
                            }
                        }
                    }
                }
            }
        }

        private async void MixDecompression_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();

            if (SelectedItems.Any((Item) => Item is LinkStorageFile))
            {
                QueueContentDialog Dialog = new QueueContentDialog
                {
                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                    Content = Globalization.GetString("QueueDialog_LinkIsNotAllowInMixZip_Content"),
                    CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                };

                _ = await Dialog.ShowAsync();

                return;
            }

            if (SelectedItems.All((Item) => Item.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)
                                            || Item.Name.EndsWith(".tar", StringComparison.OrdinalIgnoreCase)
                                            || Item.Name.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase)
                                            || Item.Name.EndsWith(".tgz", StringComparison.OrdinalIgnoreCase)
                                            || Item.Name.EndsWith(".tar.bz2", StringComparison.OrdinalIgnoreCase)
                                            || Item.Name.EndsWith(".gz", StringComparison.OrdinalIgnoreCase)
                                            || Item.Name.EndsWith(".bz2", StringComparison.OrdinalIgnoreCase)
                                            || Item.Name.EndsWith(".rar", StringComparison.OrdinalIgnoreCase)))
            {
                QueueTaskController.EnqueueDecompressionOpeartion(SelectedItems.Select((Item) => Item.Path), CurrentFolder.Path, (sender as FrameworkElement)?.Name == "MixDecompressIndie");
            }
            else
            {
                QueueContentDialog Dialog = new QueueContentDialog
                {
                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                    Content = Globalization.GetString("QueueDialog_FileTypeIncorrect_Content"),
                    CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                };

                await Dialog.ShowAsync();
            }
        }

        private async void MixCompression_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();

            if (SelectedItems.Any((Item) => Item is LinkStorageFile))
            {
                QueueContentDialog dialog = new QueueContentDialog
                {
                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                    Content = Globalization.GetString("QueueDialog_LinkIsNotAllowInMixZip_Content"),
                    CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                };

                _ = await dialog.ShowAsync();

                return;
            }

            CompressDialog Dialog = new CompressDialog();

            if ((await Dialog.ShowAsync()) == ContentDialogResult.Primary)
            {
                QueueTaskController.EnqueueCompressionOpeartion(Dialog.Type, Dialog.Algorithm, Dialog.Level, SelectedItems.Select((Item) => Item.Path), Path.Combine(CurrentFolder.Path, Dialog.FileName));
            }
        }

        private async void OpenInTerminal_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();

            if (SQLite.Current.GetTerminalProfileByName(Convert.ToString(ApplicationData.Current.LocalSettings.Values["DefaultTerminal"])) is TerminalProfile Profile)
            {
                using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableController())
                {
                    if (!await Exclusive.Controller.RunAsync(Profile.Path, string.Empty, WindowState.Normal, Profile.RunAsAdmin, false, false, Regex.Matches(Profile.Argument, "[^ \"]+|\"[^\"]*\"").Select((Mat) => Mat.Value.Contains("[CurrentLocation]") ? Mat.Value.Replace("[CurrentLocation]", CurrentFolder.Path) : Mat.Value).ToArray()))
                    {
                        QueueContentDialog Dialog = new QueueContentDialog
                        {
                            Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                            Content = Globalization.GetString("QueueDialog_LaunchFailed_Content"),
                            CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                        };

                        await Dialog.ShowAsync();
                    }
                }
            }
        }

        private async void OpenFolderInNewTab_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();

            if (SelectedItem is FileSystemStorageFolder Folder)
            {
                await TabViewContainer.ThisPage.CreateNewTabAsync(null, Folder.Path);
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
                        DelayRenameCancel?.Cancel();
                        DelayRenameCancel?.Dispose();
                        DelayRenameCancel = new CancellationTokenSource();

                        Task.Delay(1200).ContinueWith((task, input) =>
                        {
                            if (input is CancellationTokenSource Cancel && !Cancel.IsCancellationRequested)
                            {
                                NameLabel.Visibility = Visibility.Collapsed;

                                if ((NameLabel.Parent as FrameworkElement)?.FindName("NameEditBox") is TextBox EditBox)
                                {
                                    EditBox.BeforeTextChanging += EditBox_BeforeTextChanging;
                                    EditBox.PreviewKeyDown += EditBox_PreviewKeyDown;
                                    EditBox.LostFocus += EditBox_LostFocus;
                                    EditBox.Text = NameLabel.Text;
                                    EditBox.Visibility = Visibility.Visible;
                                    EditBox.Focus(FocusState.Programmatic);
                                }

                                Container.BlockKeyboardShortCutInput = true;
                            }
                        }, DelayRenameCancel, TaskScheduler.FromCurrentSynchronizationContext());
                    }
                }
            }
        }

        private void EditBox_PreviewKeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Enter)
            {
                ItemPresenter.Focus(FocusState.Programmatic);
            }
        }

        private void EditBox_BeforeTextChanging(TextBox sender, TextBoxBeforeTextChangingEventArgs args)
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

        private async void EditBox_LostFocus(object sender, RoutedEventArgs e)
        {
            TextBox NameEditBox = (TextBox)sender;

            NameEditBox.LostFocus -= EditBox_LostFocus;
            NameEditBox.PreviewKeyDown -= EditBox_PreviewKeyDown;
            NameEditBox.BeforeTextChanging -= EditBox_BeforeTextChanging;

            if ((NameEditBox?.Parent as FrameworkElement)?.FindName("NameLabel") is TextBlock NameLabel && NameEditBox.DataContext is FileSystemStorageItemBase CurrentEditItem)
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

                    if (!CurrentEditItem.Name.Equals(NameEditBox.Text, StringComparison.OrdinalIgnoreCase) && await FileSystemStorageItemBase.CheckExistAsync(Path.Combine(CurrentFolder.Path, NameEditBox.Text)))
                    {
                        QueueContentDialog Dialog = new QueueContentDialog
                        {
                            Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                            Content = Globalization.GetString("QueueDialog_RenameExist_Content"),
                            PrimaryButtonText = Globalization.GetString("Common_Dialog_ContinueButton"),
                            CloseButtonText = Globalization.GetString("Common_Dialog_CancelButton")
                        };

                        if (await Dialog.ShowAsync() != ContentDialogResult.Primary)
                        {
                            return;
                        }
                    }

                    try
                    {
                        await CurrentEditItem.RenameAsync(NameEditBox.Text);
                    }
                    catch (FileLoadException)
                    {
                        QueueContentDialog LoadExceptionDialog = new QueueContentDialog
                        {
                            Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                            Content = Globalization.GetString("QueueDialog_FileOccupied_Content"),
                            CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton"),
                        };

                        _ = await LoadExceptionDialog.ShowAsync();
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

                        await UnauthorizeDialog.ShowAsync();
                    }
                }
                catch
                {
                    QueueContentDialog UnauthorizeDialog = new QueueContentDialog
                    {
                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                        Content = Globalization.GetString("QueueDialog_UnauthorizedRenameFile_Content"),
                        PrimaryButtonText = Globalization.GetString("Common_Dialog_NowButton"),
                        CloseButtonText = Globalization.GetString("Common_Dialog_LaterButton")
                    };

                    await UnauthorizeDialog.ShowAsync();
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

            if (SelectedItem is FileSystemStorageFolder Folder)
            {
                await Launcher.LaunchUriAsync(new Uri($"rx-explorer:{Uri.EscapeDataString(Folder.Path)}"));
            }
        }

        private async void Undo_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();

            await Ctrl_Z_Click().ConfigureAwait(false);
        }

        private void OrderByName_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();
            SortCollectionGenerator.SavePathSortState(CurrentFolder.Path, SortTarget.Name, SortDesc.IsChecked ? SortDirection.Descending : SortDirection.Ascending);
        }

        private void OrderByTime_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();
            SortCollectionGenerator.SavePathSortState(CurrentFolder.Path, SortTarget.ModifiedTime, SortDesc.IsChecked ? SortDirection.Descending : SortDirection.Ascending);
        }

        private void OrderByType_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();
            SortCollectionGenerator.SavePathSortState(CurrentFolder.Path, SortTarget.Type, SortDesc.IsChecked ? SortDirection.Descending : SortDirection.Ascending);
        }

        private void OrderBySize_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();
            SortCollectionGenerator.SavePathSortState(CurrentFolder.Path, SortTarget.Size, SortDesc.IsChecked ? SortDirection.Descending : SortDirection.Ascending);
        }

        private void SortDesc_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();

            PathConfiguration Config = SQLite.Current.GetPathConfiguration(CurrentFolder.Path);
            SortCollectionGenerator.SavePathSortState(CurrentFolder.Path, Config.SortTarget.GetValueOrDefault(), SortDirection.Descending);
        }

        private void SortAsc_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();

            PathConfiguration Config = SQLite.Current.GetPathConfiguration(CurrentFolder.Path);
            SortCollectionGenerator.SavePathSortState(CurrentFolder.Path, Config.SortTarget.GetValueOrDefault(), SortDirection.Ascending);
        }

        private void SortMenuFlyout_Opening(object sender, object e)
        {
            PathConfiguration Configuration = SQLite.Current.GetPathConfiguration(CurrentFolder.Path);

            if (Configuration.SortDirection == SortDirection.Ascending)
            {
                SortDesc.IsChecked = false;
                SortAsc.IsChecked = true;
            }
            else
            {
                SortAsc.IsChecked = false;
                SortDesc.IsChecked = true;
            }

            switch (Configuration.SortTarget)
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
                }
                else
                {
                    bool IsEnablePaste = false;

                    try
                    {
                        IsEnablePaste = await Clipboard.GetContent().CheckIfContainsAvailableDataAsync();
                    }
                    catch
                    {
                        IsEnablePaste = false;
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
                        IsEnabled = OperationRecorder.Current.Count > 0
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
                }
            }
        }

        private void ListHeader_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
        {
            e.Handled = true;
        }

        private async void LnkOpenLocation_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedItem is LinkStorageFile Item)
            {
                if (Item.LinkTargetPath == Globalization.GetString("UnknownText") || Item.LinkType == ShellLinkType.UWP)
                {
                    QueueContentDialog Dialog = new QueueContentDialog
                    {
                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                        Content = Globalization.GetString("QueueDialog_LocateFileFailure_Content"),
                        CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                    };

                    await Dialog.ShowAsync();
                }
                else
                {
                    try
                    {
                        if (await FileSystemStorageItemBase.OpenAsync(Path.GetDirectoryName(Item.LinkTargetPath)) is FileSystemStorageFolder ParentFolder)
                        {
                            await DisplayItemsInFolder(ParentFolder);

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

                            _ = await Dialog.ShowAsync();
                        }
                    }
                    catch
                    {
                        QueueContentDialog Dialog = new QueueContentDialog
                        {
                            Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                            Content = Globalization.GetString("QueueDialog_LocateFileFailure_Content"),
                            CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                        };

                        await Dialog.ShowAsync();
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
            if (e.OriginalSource is not TextBox)
            {
                switch (e.Key)
                {
                    case VirtualKey.Space:
                        {
                            e.Handled = true;
                            break;
                        }
                }
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

        private void Filter_RefreshListRequested(object sender, FilterController.RefreshRequestedEventArgs args)
        {
            PathConfiguration Config = SQLite.Current.GetPathConfiguration(CurrentFolder.Path);

            if (IsGroupedEnable)
            {
                GroupCollection.Clear();

                foreach (FileSystemStorageGroupItem GroupItem in GroupCollectionGenerator.GetGroupedCollection(args.FilterCollection, Config.GroupTarget.GetValueOrDefault(), Config.GroupDirection.GetValueOrDefault()))
                {
                    GroupCollection.Add(new FileSystemStorageGroupItem(GroupItem.Key, SortCollectionGenerator.GetSortedCollection(GroupItem, Config.SortTarget.GetValueOrDefault(), Config.SortDirection.GetValueOrDefault())));
                }
            }

            FileCollection.Clear();

            foreach (FileSystemStorageItemBase Item in args.FilterCollection)
            {
                FileCollection.Add(Item);
            }
        }

        private async void OpenFolderInVerticalSplitView_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();

            if (SelectedItem != null)
            {
                await Container.CreateNewBladeAsync(SelectedItem.Path).ConfigureAwait(false);
            }
        }

        private void DecompressionOptionFlyout_Opening(object sender, object e)
        {
            string DecompressionFolderName = SelectedItem.Name.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase)
                                                                ? SelectedItem.Name.Substring(0, SelectedItem.Name.Length - 7)
                                                                : (SelectedItem.Name.EndsWith(".tar.bz2", StringComparison.OrdinalIgnoreCase)
                                                                                        ? SelectedItem.Name.Substring(0, SelectedItem.Name.Length - 8)
                                                                                        : Path.GetFileNameWithoutExtension(SelectedItem.Name));

            if (string.IsNullOrEmpty(DecompressionFolderName))
            {
                DecompressionFolderName = Globalization.GetString("Operate_Text_CreateFolder");
            }

            DecompressionOption2.Text = $"{Globalization.GetString("DecompressTo")} \"{DecompressionFolderName}\\\"";

            ToolTipService.SetToolTip(DecompressionOption2, new ToolTip
            {
                Content = DecompressionOption2.Text
            });
        }

        private async void DecompressOption_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedItem is FileSystemStorageFile File)
            {
                CloseAllFlyout();

                if (!await FileSystemStorageItemBase.CheckExistAsync(File.Path))
                {
                    QueueContentDialog dialog = new QueueContentDialog
                    {
                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                        Content = Globalization.GetString("QueueDialog_LocateFileFailure_Content"),
                        CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                    };

                    _ = await dialog.ShowAsync();

                    return;
                }


                if (SelectedItems.All((Item) => Item.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)
                                                || Item.Name.EndsWith(".tar", StringComparison.OrdinalIgnoreCase)
                                                || Item.Name.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase)
                                                || Item.Name.EndsWith(".tgz", StringComparison.OrdinalIgnoreCase)
                                                || Item.Name.EndsWith(".tar.bz2", StringComparison.OrdinalIgnoreCase)
                                                || Item.Name.EndsWith(".bz2", StringComparison.OrdinalIgnoreCase)
                                                || Item.Name.EndsWith(".gz", StringComparison.OrdinalIgnoreCase)
                                                || Item.Name.EndsWith(".rar", StringComparison.OrdinalIgnoreCase)))
                {
                    DecompressDialog Dialog = new DecompressDialog(Path.GetDirectoryName(File.Path));

                    if (await Dialog.ShowAsync() == ContentDialogResult.Primary)
                    {
                        FileSystemStorageFolder TargetFolder = await FileSystemStorageItemBase.CreateAsync(Path.Combine(Dialog.ExtractLocation, File.Name.Split(".")[0]), StorageItemTypes.Folder, CreateOption.GenerateUniqueName) as FileSystemStorageFolder;

                        if (TargetFolder == null)
                        {
                            QueueContentDialog dialog = new QueueContentDialog
                            {
                                Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                Content = Globalization.GetString("QueueDialog_UnauthorizedDecompression_Content"),
                                PrimaryButtonText = Globalization.GetString("Common_Dialog_NowButton"),
                                CloseButtonText = Globalization.GetString("Common_Dialog_LaterButton")
                            };

                            await dialog.ShowAsync();
                        }
                        else
                        {
                            QueueTaskController.EnqueueDecompressionOpeartion(File.Path, TargetFolder.Path, false, Dialog.CurrentEncoding);
                        }
                    }
                }
                else
                {
                    QueueContentDialog dialog = new QueueContentDialog
                    {
                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                        Content = Globalization.GetString("QueueDialog_FileTypeIncorrect_Content"),
                        CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                    };

                    await dialog.ShowAsync();
                }
            }
        }

        private async void MixDecompressOption_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();

            if (SelectedItems.Any((Item) => Item is LinkStorageFile))
            {
                QueueContentDialog Dialog = new QueueContentDialog
                {
                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                    Content = Globalization.GetString("QueueDialog_LinkIsNotAllowInMixZip_Content"),
                    CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                };

                _ = await Dialog.ShowAsync();

                return;
            }


            if (SelectedItems.All((Item) => Item.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)
                                            || Item.Name.EndsWith(".tar", StringComparison.OrdinalIgnoreCase)
                                            || Item.Name.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase)
                                            || Item.Name.EndsWith(".tgz", StringComparison.OrdinalIgnoreCase)
                                            || Item.Name.EndsWith(".tar.bz2", StringComparison.OrdinalIgnoreCase)
                                            || Item.Name.EndsWith(".bz2", StringComparison.OrdinalIgnoreCase)
                                            || Item.Name.EndsWith(".gz", StringComparison.OrdinalIgnoreCase)
                                            || Item.Name.EndsWith(".rar", StringComparison.OrdinalIgnoreCase)))
            {
                DecompressDialog Dialog = new DecompressDialog(CurrentFolder.Path);

                if (await Dialog.ShowAsync() == ContentDialogResult.Primary)
                {
                    QueueTaskController.EnqueueDecompressionOpeartion(SelectedItems.Select((Item) => Item.Path), Dialog.ExtractLocation, true, Dialog.CurrentEncoding);
                }


            }
            else
            {
                QueueContentDialog dialog = new QueueContentDialog
                {
                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                    Content = Globalization.GetString("QueueDialog_FileTypeIncorrect_Content"),
                    CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                };

                await dialog.ShowAsync();


            }
        }

        private void UnTag_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();

            SelectedItem.SetColorAsNormal();
            SQLite.Current.DeleteFileColor(SelectedItem.Path);
        }


        private void MixUnTag_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();

            foreach (FileSystemStorageItemBase Item in SelectedItems)
            {
                Item.SetColorAsNormal();
                SQLite.Current.DeleteFileColor(Item.Path);
            }
        }

        private void Color_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();

            Color ForegroundColor = ((Windows.UI.Xaml.Media.SolidColorBrush)((AppBarButton)sender).Foreground).Color;

            SelectedItem.SetColorAsSpecific(ForegroundColor);
            SQLite.Current.SetFileColor(SelectedItem.Path, ForegroundColor.ToHex());
        }

        private void ColorTag_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            if (e.Pointer.PointerDeviceType == PointerDeviceType.Mouse && sender is FrameworkElement Element)
            {
                if (Element.FindParentOfType<AppBarElementContainer>() is AppBarElementContainer Container)
                {
                    Container.Visibility = Visibility.Collapsed;
                }
            }
        }

        private void ColorTag_Tapped(object sender, TappedRoutedEventArgs e)
        {
            if (e.PointerDeviceType != PointerDeviceType.Mouse && sender is FrameworkElement Element)
            {
                if (Element.FindParentOfType<AppBarElementContainer>() is AppBarElementContainer Container)
                {
                    Container.Visibility = Visibility.Collapsed;
                }
            }
        }

        private void CommandBarFlyout_Closed(object sender, object e)
        {
            if (sender is CommandBarFlyout Flyout)
            {
                if (Flyout.PrimaryCommands.OfType<AppBarElementContainer>().FirstOrDefault((Container) => !string.IsNullOrEmpty(Container.Name)) is AppBarElementContainer Container)
                {
                    Container.Visibility = Visibility.Visible;
                }
            }
        }

        private void ColorBarBack_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            if (e.Pointer.PointerDeviceType == PointerDeviceType.Mouse && sender is FrameworkElement Element)
            {
                if (Element.FindParentOfType<AppBarElementContainer>() is AppBarElementContainer Container)
                {
                    Container.Visibility = Visibility.Collapsed;
                }
            }
        }

        private void ColorBarBack_Tapped(object sender, TappedRoutedEventArgs e)
        {
            if (e.PointerDeviceType != PointerDeviceType.Mouse && sender is FrameworkElement Element)
            {
                if (Element.FindParentOfType<AppBarElementContainer>() is AppBarElementContainer Container)
                {
                    Container.Visibility = Visibility.Collapsed;
                }
            }
        }

        private void MixColor_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();

            Color ForegroundColor = ((Windows.UI.Xaml.Media.SolidColorBrush)((AppBarButton)sender).Foreground).Color;

            foreach (FileSystemStorageItemBase Item in SelectedItems)
            {
                Item.SetColorAsSpecific(ForegroundColor);
                SQLite.Current.SetFileColor(Item.Path, ForegroundColor.ToHex());
            }
        }

        private async void MixOpen_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();

            if (SelectedItems.Count > 0)
            {
                foreach (FileSystemStorageItemBase Item in SelectedItems)
                {
                    switch (Item)
                    {
                        case FileSystemStorageFolder Folder:
                            {
                                await Container.CreateNewBladeAsync(Folder.Path);
                                break;
                            }
                        case FileSystemStorageFile File:
                            {
                                await EnterSelectedItemAsync(File);
                                break;
                            }
                    }
                }
            }
        }

        private void GroupMenuFlyout_Opening(object sender, object e)
        {
            PathConfiguration Configuration = SQLite.Current.GetPathConfiguration(CurrentFolder.Path);

            if (Configuration.GroupDirection == GroupDirection.Ascending)
            {
                GroupDesc.IsChecked = false;
                GroupAsc.IsChecked = true;
            }
            else
            {
                GroupAsc.IsChecked = false;
                GroupDesc.IsChecked = true;
            }

            switch (Configuration.GroupTarget)
            {
                case GroupTarget.None:
                    {
                        GroupAsc.IsEnabled = false;
                        GroupDesc.IsEnabled = false;
                        GroupByType.IsChecked = false;
                        GroupByTime.IsChecked = false;
                        GroupBySize.IsChecked = false;
                        GroupByName.IsChecked = false;
                        GroupByNone.IsChecked = true;
                        break;
                    }
                case GroupTarget.Name:
                    {
                        GroupAsc.IsEnabled = true;
                        GroupDesc.IsEnabled = true;
                        GroupByType.IsChecked = false;
                        GroupByTime.IsChecked = false;
                        GroupBySize.IsChecked = false;
                        GroupByName.IsChecked = true;
                        GroupByNone.IsChecked = false;
                        break;
                    }
                case GroupTarget.Type:
                    {
                        GroupAsc.IsEnabled = true;
                        GroupDesc.IsEnabled = true;
                        GroupByTime.IsChecked = false;
                        GroupBySize.IsChecked = false;
                        GroupByName.IsChecked = false;
                        GroupByType.IsChecked = true;
                        GroupByNone.IsChecked = false;
                        break;
                    }
                case GroupTarget.ModifiedTime:
                    {
                        GroupAsc.IsEnabled = true;
                        GroupDesc.IsEnabled = true;
                        GroupBySize.IsChecked = false;
                        GroupByName.IsChecked = false;
                        GroupByType.IsChecked = false;
                        GroupByTime.IsChecked = true;
                        GroupByNone.IsChecked = false;
                        break;
                    }
                case GroupTarget.Size:
                    {
                        GroupAsc.IsEnabled = true;
                        GroupDesc.IsEnabled = true;
                        GroupByName.IsChecked = false;
                        GroupByType.IsChecked = false;
                        GroupByTime.IsChecked = false;
                        GroupBySize.IsChecked = true;
                        GroupByNone.IsChecked = false;
                        break;
                    }
            }
        }

        private void GroupByName_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();
            GroupCollectionGenerator.SavePathGroupState(CurrentFolder.Path, GroupTarget.Name, GroupAsc.IsChecked ? GroupDirection.Ascending : GroupDirection.Descending);
        }

        private void GroupByTime_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();
            GroupCollectionGenerator.SavePathGroupState(CurrentFolder.Path, GroupTarget.ModifiedTime, GroupAsc.IsChecked ? GroupDirection.Ascending : GroupDirection.Descending);
        }

        private void GroupByType_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();
            GroupCollectionGenerator.SavePathGroupState(CurrentFolder.Path, GroupTarget.Type, GroupAsc.IsChecked ? GroupDirection.Ascending : GroupDirection.Descending);
        }

        private void GroupBySize_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();
            GroupCollectionGenerator.SavePathGroupState(CurrentFolder.Path, GroupTarget.Size, GroupAsc.IsChecked ? GroupDirection.Ascending : GroupDirection.Descending);
        }

        private void GroupAsc_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();

            PathConfiguration Config = SQLite.Current.GetPathConfiguration(CurrentFolder.Path);
            GroupCollectionGenerator.SavePathGroupState(CurrentFolder.Path, Config.GroupTarget.GetValueOrDefault(), GroupDirection.Ascending);
        }

        private void GroupDesc_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();

            PathConfiguration Config = SQLite.Current.GetPathConfiguration(CurrentFolder.Path);
            GroupCollectionGenerator.SavePathGroupState(CurrentFolder.Path, Config.GroupTarget.GetValueOrDefault(), GroupDirection.Descending);
        }

        private void GroupNone_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();
            GroupCollectionGenerator.SavePathGroupState(CurrentFolder.Path, GroupTarget.None, GroupDirection.Ascending);
        }

        private async void RootFolderControl_EnterActionRequested(object sender, string Path)
        {
            await DisplayItemsInFolder(Path);
        }

        private void SendToFlyout_Opening(object sender, object e)
        {
            if (sender is MenuFlyout Flyout)
            {
                foreach (MenuFlyoutItemWithImage Item in Flyout.Items)
                {
                    Item.Click -= SendToItem_Click;
                }

                Flyout.Items.Clear();

                MenuFlyoutItemWithImage SendDocumentItem = new MenuFlyoutItemWithImage
                {
                    Name = "SendDocumentItem",
                    Text = Globalization.GetString("SendTo_Document"),
                    ImageIcon = new BitmapImage(new Uri("ms-appx:///Assets/DocumentIcon.ico")),
                    MinWidth = 150,
                    MaxWidth = 350
                };
                SendDocumentItem.Click += SendToItem_Click;

                Flyout.Items.Add(SendDocumentItem);

                MenuFlyoutItemWithImage SendLinkItem = new MenuFlyoutItemWithImage
                {
                    Name = "SendLinkItem",
                    Text = Globalization.GetString("SendTo_CreateDesktopShortcut"),
                    ImageIcon = new BitmapImage(new Uri("ms-appx:///Assets/DesktopIcon.ico")),
                    MinWidth = 150,
                    MaxWidth = 350
                };
                SendLinkItem.Click += SendToItem_Click;

                Flyout.Items.Add(SendLinkItem);

                foreach (DriveDataBase RemovableDrive in CommonAccessCollection.DriveList.Where((Drive) => (Drive.DriveType == DriveType.Removable || Drive.DriveType == DriveType.Network) && !string.IsNullOrEmpty(Drive.Path)))
                {
                    MenuFlyoutItemWithImage SendRemovableDriveItem = new MenuFlyoutItemWithImage
                    {
                        Name = "SendRemovableItem",
                        Text = $"{(string.IsNullOrEmpty(RemovableDrive.DisplayName) ? RemovableDrive.Path : RemovableDrive.DisplayName)}",
                        ImageIcon = RemovableDrive.Thumbnail,
                        MinWidth = 150,
                        MaxWidth = 350,
                        Tag = RemovableDrive.Path
                    };
                    SendRemovableDriveItem.Click += SendToItem_Click;

                    Flyout.Items.Add(SendRemovableDriveItem);
                }
            }
        }

        private async void SendToItem_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();

            if (sender is MenuFlyoutItemWithImage Item)
            {
                FileSystemStorageItemBase SItem = SelectedItem;

                switch (Item.Name)
                {
                    case "SendLinkItem":
                        {
                            string DesktopPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);

                            if (await FileSystemStorageItemBase.CheckExistAsync(DesktopPath))
                            {
                                using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableController())
                                {
                                    if (!await Exclusive.Controller.CreateLinkAsync(Path.Combine(DesktopPath, $"{(SItem is FileSystemStorageFolder ? SItem.Name : Path.GetFileNameWithoutExtension(SItem.Name))}.lnk"),
                                                                                    SItem.Path,
                                                                                    string.Empty,
                                                                                    WindowState.Normal,
                                                                                    0,
                                                                                    string.Empty))
                                    {
                                        QueueContentDialog Dialog = new QueueContentDialog
                                        {
                                            Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                            Content = Globalization.GetString("QueueDialog_UnauthorizedCreateNewFile_Content"),
                                            CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                                        };

                                        await Dialog.ShowAsync();
                                    }
                                }
                            }
                            else
                            {
                                try
                                {
                                    IReadOnlyList<User> UserList = await User.FindAllAsync();

                                    UserDataPaths DataPath = UserList.FirstOrDefault((User) => User.AuthenticationStatus == UserAuthenticationStatus.LocallyAuthenticated && User.Type == UserType.LocalUser) is User CurrentUser
                                                             ? UserDataPaths.GetForUser(CurrentUser)
                                                             : UserDataPaths.GetDefault();

                                    if (await FileSystemStorageItemBase.CheckExistAsync(DataPath.Desktop))
                                    {
                                        using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableController())
                                        {
                                            if (!await Exclusive.Controller.CreateLinkAsync(Path.Combine(DataPath.Desktop, $"{(SItem is FileSystemStorageFolder ? SItem.Name : Path.GetFileNameWithoutExtension(SItem.Name))}.lnk"),
                                                                                            SItem.Path,
                                                                                            string.Empty,
                                                                                            WindowState.Normal,
                                                                                            0,
                                                                                            string.Empty))
                                            {
                                                QueueContentDialog Dialog = new QueueContentDialog
                                                {
                                                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                                    Content = Globalization.GetString("QueueDialog_UnauthorizedCreateNewFile_Content"),
                                                    CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                                                };

                                                await Dialog.ShowAsync();
                                            }
                                        }
                                    }
                                    else
                                    {
                                        LogTracer.Log($"Could not execute \"Send to\" command because desktop path \"{DataPath.Desktop}\" is not exists");
                                    }
                                }
                                catch (Exception ex)
                                {
                                    LogTracer.Log(ex, "Could not get desktop path from UserDataPaths");
                                }
                            }

                            break;
                        }
                    case "SendDocumentItem":
                        {
                            string DocumentPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

                            if (await FileSystemStorageItemBase.CheckExistAsync(DocumentPath))
                            {
                                QueueTaskController.EnqueueCopyOpeartion(SItem.Path, DocumentPath);
                            }
                            else
                            {
                                try
                                {
                                    IReadOnlyList<User> UserList = await User.FindAllAsync();

                                    UserDataPaths DataPath = UserList.FirstOrDefault((User) => User.AuthenticationStatus == UserAuthenticationStatus.LocallyAuthenticated && User.Type == UserType.LocalUser) is User CurrentUser
                                                             ? UserDataPaths.GetForUser(CurrentUser)
                                                             : UserDataPaths.GetDefault();

                                    if (await FileSystemStorageItemBase.CheckExistAsync(DataPath.Documents))
                                    {
                                        QueueTaskController.EnqueueCopyOpeartion(SItem.Path, DataPath.Documents);
                                    }
                                    else
                                    {
                                        LogTracer.Log($"Could not execute \"Send to\" command because document path \"{DataPath.Documents}\" is not exists");
                                    }
                                }
                                catch (Exception ex)
                                {
                                    LogTracer.Log(ex, "Could not get document path from UserDataPaths");
                                }
                            }

                            break;
                        }
                    case "SendRemovableItem":
                        {
                            if (Item.Tag is string RemovablePath)
                            {
                                QueueTaskController.EnqueueCopyOpeartion(SItem.Path, RemovablePath);
                            }

                            break;
                        }
                }
            }
        }

        private async void StatusTips_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
        {
            if (Path.GetPathRoot(CurrentFolder?.Path).Equals(CurrentFolder?.Path, StringComparison.OrdinalIgnoreCase))
            {
                await DisplayItemsInFolder(RootStorageFolder.Instance);
            }
            else if (Container.GoParentFolder.IsEnabled)
            {
                Container.GoParentFolder_Click(null, null);
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
            DelaySelectionCancel?.Dispose();
            DelayDragCancel?.Dispose();
            EnterLock?.Dispose();
            CollectionChangeLock?.Dispose();

            AreaWatcher = null;
            WiFiProvider = null;
            SelectionExtention = null;
            DelayRenameCancel = null;
            DelayEnterCancel = null;
            DelaySelectionCancel = null;
            DelayDragCancel = null;
            EnterLock = null;
            CollectionChangeLock = null;

            RecordIndex = 0;
            GoAndBackRecord.Clear();

            FileCollection.CollectionChanged -= FileCollection_CollectionChanged;
            ListViewDetailHeader.Filter.RefreshListRequested -= Filter_RefreshListRequested;
            RootFolderControl.EnterActionRequested -= RootFolderControl_EnterActionRequested;

            CoreWindow Window = CoreWindow.GetForCurrentThread();
            Window.KeyDown -= FilePresenter_KeyDown;
            Window.Dispatcher.AcceleratorKeyActivated -= Dispatcher_AcceleratorKeyActivated;

            Application.Current.Suspending -= Current_Suspending;
            Application.Current.Resuming -= Current_Resuming;
            SortCollectionGenerator.SortStateChanged -= Current_SortStateChanged;
            GroupCollectionGenerator.GroupStateChanged -= GroupCollectionGenerator_GroupStateChanged;
            ViewModeController.ViewModeChanged -= Current_ViewModeChanged;
        }
    }
}

