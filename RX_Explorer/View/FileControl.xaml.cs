using HtmlAgilityPack;
using Microsoft.Toolkit.Uwp.UI.Animations;
using Microsoft.UI.Xaml.Controls;
using RX_Explorer.Class;
using RX_Explorer.Dialog;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.ApplicationModel.DataTransfer;
using Windows.ApplicationModel.DataTransfer.DragDrop;
using Windows.Foundation;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.System;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Navigation;
using RefreshContainer = Microsoft.UI.Xaml.Controls.RefreshContainer;
using TreeView = Microsoft.UI.Xaml.Controls.TreeView;
using TreeViewCollapsedEventArgs = Microsoft.UI.Xaml.Controls.TreeViewCollapsedEventArgs;
using TreeViewExpandingEventArgs = Microsoft.UI.Xaml.Controls.TreeViewExpandingEventArgs;
using TreeViewItemInvokedEventArgs = Microsoft.UI.Xaml.Controls.TreeViewItemInvokedEventArgs;
using TreeViewNode = Microsoft.UI.Xaml.Controls.TreeViewNode;

namespace RX_Explorer
{
    public sealed partial class FileControl : Page, IDisposable
    {
        private int TextChangeLockResource;

        private int AddressButtonLockResource;

        private int NavigateLockResource;

        private int DropLockResource;

        private string AddressBoxTextBackup;

        private volatile FileSystemStorageItemBase currentFolder;

        private SemaphoreSlim EnterLock;

        private StorageAreaWatcher AreaWatcher;

        public FileSystemStorageItemBase CurrentFolder
        {
            get => currentFolder;
            set
            {
                if (value != null)
                {
                    AreaWatcher.StartWatchDirectory(value.Path, SettingControl.IsDisplayHiddenItem);

                    UpdateAddressButton(value.Path);

                    GlobeSearch.PlaceholderText = $"{Globalization.GetString("SearchBox_PlaceholderText")} {value.Name}";
                    GoParentFolder.IsEnabled = value.Path != Path.GetPathRoot(value.Path);
                    GoBackRecord.IsEnabled = RecordIndex > 0;
                    GoForwardRecord.IsEnabled = RecordIndex < GoAndBackRecord.Count - 1;

                    if (TabItem != null)
                    {
                        TabItem.Header = string.IsNullOrEmpty(value.DisplayName) ? $"<{Globalization.GetString("UnknownText")}>" : value.DisplayName;
                    }
                }

                TaskBarController.SetText(value?.DisplayName);

                currentFolder = value;
            }
        }

        private int RecordIndex
        {
            get => recordIndex;
            set => recordIndex = value;
        }

        private List<ValueTuple<string, string>> GoAndBackRecord = new List<ValueTuple<string, string>>();
        private ObservableCollection<string> AddressButtonList = new ObservableCollection<string>();
        private ObservableCollection<string> AddressExtentionList = new ObservableCollection<string>();
        private volatile int recordIndex;
        private bool IsBackOrForwardAction;

        private TabViewItem TabItem
        {
            get
            {
                if (WeakToTabItem.TryGetTarget(out TabViewItem Tab))
                {
                    return Tab;
                }
                else
                {
                    return null;
                }
            }
        }

        private WeakReference<TabViewItem> WeakToTabItem;

        public FileControl()
        {
            InitializeComponent();

            Presenter.WeakToFileControl = new WeakReference<FileControl>(this);

            ItemDisplayMode.Items.Add(Globalization.GetString("FileControl_ItemDisplayMode_Tiles"));
            ItemDisplayMode.Items.Add(Globalization.GetString("FileControl_ItemDisplayMode_Details"));
            ItemDisplayMode.Items.Add(Globalization.GetString("FileControl_ItemDisplayMode_List"));
            ItemDisplayMode.Items.Add(Globalization.GetString("FileControl_ItemDisplayMode_Large_Icon"));
            ItemDisplayMode.Items.Add(Globalization.GetString("FileControl_ItemDisplayMode_Medium_Icon"));
            ItemDisplayMode.Items.Add(Globalization.GetString("FileControl_ItemDisplayMode_Small_Icon"));

            EverythingTip.Subtitle = Globalization.GetString("EverythingQuestionSubtitle");

            Loaded += FileControl_Loaded;
        }

        private void FileControl_Loaded(object sender, RoutedEventArgs e)
        {
            if (ApplicationData.Current.LocalSettings.Values["DetachTreeViewAndPresenter"] is bool Enable)
            {
                TreeViewGridCol.Width = Enable ? new GridLength(0) : new GridLength(2, GridUnitType.Star);
            }
            else
            {
                ApplicationData.Current.LocalSettings.Values["DetachTreeViewAndPresenter"] = false;
                TreeViewGridCol.Width = new GridLength(2, GridUnitType.Star);
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

        /// <summary>
        /// 激活或关闭正在加载提示
        /// </summary>
        /// <param name="IsLoading">激活或关闭</param>
        /// <param name="Info">提示内容</param>
        public async Task LoadingActivation(bool IsLoading, string Info = null)
        {
            if (LoadingControl.IsLoading == IsLoading)
            {
                return;
            }

            if (IsLoading)
            {
                if (Presenter.HasFile.Visibility == Visibility.Visible)
                {
                    Presenter.HasFile.Visibility = Visibility.Collapsed;
                }

                ProBar.IsIndeterminate = true;
                ProBar.Value = 0;
                ProgressInfo.Text = Info + "...";

                MainPage.ThisPage.IsAnyTaskRunning = true;
            }
            else
            {
                await Task.Delay(500).ConfigureAwait(true);
                MainPage.ThisPage.IsAnyTaskRunning = false;
            }

            LoadingControl.IsLoading = IsLoading;
        }

        private async void UpdateAddressButton(string Path)
        {
            if (Interlocked.Exchange(ref AddressButtonLockResource, 1) == 0)
            {
                try
                {
                    if (string.IsNullOrEmpty(Path))
                    {
                        return;
                    }

                    string RootPath = System.IO.Path.GetPathRoot(Path);

                    StorageFolder DriveRootFolder = await StorageFolder.GetFolderFromPathAsync(RootPath);

                    if (AddressButtonList.Count == 0)
                    {
                        AddressButtonList.Add(DriveRootFolder.DisplayName);

                        PathAnalysis Analysis = new PathAnalysis(Path, RootPath);

                        while (Analysis.HasNextLevel)
                        {
                            AddressButtonList.Add(Analysis.NextRelativePath());
                        }
                    }
                    else
                    {
                        string OriginalString = string.Join("\\", AddressButtonList.Skip(1));
                        string ActualString = System.IO.Path.Combine(System.IO.Path.GetPathRoot(CurrentFolder.Path), OriginalString);

                        List<string> IntersectList = new List<string>();
                        string[] FolderSplit = Path.Split('\\', StringSplitOptions.RemoveEmptyEntries);
                        string[] ActualSplit = ActualString.Split('\\', StringSplitOptions.RemoveEmptyEntries);

                        for (int i = 0; i < FolderSplit.Length && i < ActualSplit.Length; i++)
                        {
                            if (FolderSplit[i] == ActualSplit[i])
                            {
                                IntersectList.Add(FolderSplit[i]);
                            }
                            else
                            {
                                break;
                            }
                        }

                        if (IntersectList.Count == 0)
                        {
                            AddressButtonList.Clear();

                            AddressButtonList.Add(DriveRootFolder.DisplayName);

                            PathAnalysis Analysis = new PathAnalysis(Path, RootPath);

                            while (Analysis.HasNextLevel)
                            {
                                AddressButtonList.Add(Analysis.NextRelativePath());
                            }
                        }
                        else
                        {
                            for (int i = AddressButtonList.Count - 1; i >= IntersectList.Count; i--)
                            {
                                AddressButtonList.RemoveAt(i);
                            }

                            List<string> ExceptList = Path.Split('\\', StringSplitOptions.RemoveEmptyEntries).ToList();

                            ExceptList.RemoveRange(0, IntersectList.Count);

                            foreach (string SubPath in ExceptList)
                            {
                                AddressButtonList.Add(SubPath);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogTracer.Log(ex, $"{ nameof(UpdateAddressButton)} throw an exception");
                }
                finally
                {
                    AddressButtonContainer.UpdateLayout();

                    if (AddressButtonContainer.IsLoaded)
                    {
                        ScrollViewer Viewer = AddressButtonContainer.FindChildOfType<ScrollViewer>();

                        if (Viewer.ActualWidth < Viewer.ExtentWidth)
                        {
                            Viewer.ChangeView(Viewer.ExtentWidth, null, null);
                        }
                    }
                    else
                    {
                        AddressButtonContainer.Loaded += AddressButtonContainer_Loaded;
                    }

                    _ = Interlocked.Exchange(ref AddressButtonLockResource, 0);
                }
            }
        }

        private void AddressButtonContainer_Loaded(object sender, RoutedEventArgs e)
        {
            AddressButtonContainer.Loaded -= AddressButtonContainer_Loaded;

            ScrollViewer Viewer = AddressButtonContainer.FindChildOfType<ScrollViewer>();

            if (Viewer.ActualWidth < Viewer.ExtentWidth)
            {
                Viewer.ChangeView(Viewer.ExtentWidth, null, null);
            }
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            if (Interlocked.Exchange(ref NavigateLockResource, 1) == 0)
            {
                try
                {
                    if (e.NavigationMode == NavigationMode.New && e?.Parameter is Tuple<WeakReference<TabViewItem>, string> Parameters)
                    {
                        Application.Current.Suspending += Current_Suspending;
                        Application.Current.Resuming += Current_Resuming;
                        Frame.Navigated += Frame_Navigated;

                        if (Parameters.Item1 != null)
                        {
                            WeakToTabItem = Parameters.Item1;
                        }

                        AreaWatcher = new StorageAreaWatcher(Presenter.FileCollection, FolderTree);
                        EnterLock = new SemaphoreSlim(1, 1);

                        if (!CommonAccessCollection.FrameFileControlDic.ContainsKey(Frame))
                        {
                            CommonAccessCollection.FrameFileControlDic.Add(Frame, this);
                        }

                        await Initialize(Parameters.Item2).ConfigureAwait(false);
                    }
                }
                catch (Exception ex)
                {
                    LogTracer.Log(ex);
                }
                finally
                {
                    _ = Interlocked.Exchange(ref NavigateLockResource, 0);
                }
            }
        }

        private void Frame_Navigated(object sender, NavigationEventArgs e)
        {
            TabItem.Header = e.Content switch
            {
                PhotoViewer _ => Globalization.GetString("BuildIn_PhotoViewer_Description"),
                PdfReader _ => Globalization.GetString("BuildIn_PdfReader_Description"),
                MediaPlayer _ => Globalization.GetString("BuildIn_MediaPlayer_Description"),
                TextViewer _ => Globalization.GetString("BuildIn_TextViewer_Description"),
                CropperPage _ => Globalization.GetString("BuildIn_CropperPage_Description"),
                SearchPage _ => Globalization.GetString("BuildIn_SearchPage_Description"),
                _ => string.IsNullOrEmpty(CurrentFolder?.Name) ? $"<{Globalization.GetString("UnknownText")}>" : CurrentFolder?.Name,
            };
        }

        protected override async void OnNavigatedFrom(NavigationEventArgs e)
        {
            if (e.NavigationMode == NavigationMode.Back)
            {
                try
                {
                    await Task.Run(() => SpinWait.SpinUntil(() => Interlocked.Exchange(ref NavigateLockResource, 1) == 0)).ConfigureAwait(true);

                    Dispose();
                }
                catch (Exception ex)
                {
                    LogTracer.Log(ex);
                }
                finally
                {
                    _ = Interlocked.Exchange(ref NavigateLockResource, 0);
                }
            }
        }

        /// <summary>
        /// 执行文件目录的初始化
        /// </summary>
        public async Task Initialize(string InitFolderPath)
        {
            if (!string.IsNullOrEmpty(InitFolderPath))
            {
                FolderTree.RootNodes.Clear();

                string PathRoot = Path.GetPathRoot(InitFolderPath);

                StorageFolder RootFolder = await StorageFolder.GetFolderFromPathAsync(PathRoot);

                bool HasAnyFolder = WIN_Native_API.CheckContainsAnyItem(PathRoot, ItemFilters.Folder);

                TreeViewNode RootNode = new TreeViewNode
                {
                    Content = new TreeViewNodeContent(RootFolder),
                    IsExpanded = HasAnyFolder,
                    HasUnrealizedChildren = HasAnyFolder
                };

                FolderTree.RootNodes.Add(RootNode);

                FolderTree.SelectNode(RootNode);

                Task[] InitTasks = HasAnyFolder
                    ? (new Task[] { FillTreeNode(RootNode), DisplayItemsInFolder(InitFolderPath) })
                    : (new Task[] { DisplayItemsInFolder(InitFolderPath) });

                await Task.WhenAll(InitTasks).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// 向特定TreeViewNode节点下添加子节点
        /// </summary>
        /// <param name="Node">节点</param>
        /// <returns></returns>
        public async Task FillTreeNode(TreeViewNode Node)
        {
            if (Node == null)
            {
                throw new ArgumentNullException(nameof(Node), "Parameter could not be null");
            }

            if (Node.Content is TreeViewNodeContent Content)
            {
                try
                {
                    List<string> StorageItemPath = WIN_Native_API.GetStorageItemsAndReturnPath(Content.Path, SettingControl.IsDisplayHiddenItem, ItemFilters.Folder);

                    for (int i = 0; i < StorageItemPath.Count && Node.IsExpanded && Node.CanTraceToRootNode(FolderTree.RootNodes.FirstOrDefault()); i++)
                    {
                        await Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                        {
                            TreeViewNode NewNode = new TreeViewNode
                            {
                                Content = new TreeViewNodeContent(StorageItemPath[i]),
                                HasUnrealizedChildren = WIN_Native_API.CheckContainsAnyItem(StorageItemPath[i], ItemFilters.Folder)
                            };

                            Node.Children.Add(NewNode);
                        });
                    }
                }
                catch (Exception ex)
                {
                    LogTracer.Log(ex, $"An error was threw in { nameof(FillTreeNode)}");
                }
                finally
                {
                    if (!Node.IsExpanded)
                    {
                        Node.Children.Clear();
                    }
                }
            }
        }

        private async void FolderTree_Expanding(TreeView sender, TreeViewExpandingEventArgs args)
        {
            await FillTreeNode(args.Node).ConfigureAwait(false);
        }

        private async void FolderTree_ItemInvoked(TreeView sender, TreeViewItemInvokedEventArgs args)
        {
            if (args.InvokedItem is TreeViewNode Node)
            {
                await DisplayItemsInFolder(Node).ConfigureAwait(false);
            }
        }

        public Task DisplayItemsInFolder(TreeViewNode Node, bool ForceRefresh = false)
        {
            if (Node == null)
            {
                throw new ArgumentNullException(nameof(Node), "Parameter could not be null");
            }

            FolderTree.SelectNode(Node);

            if (Node.Content is TreeViewNodeContent Content)
            {
                return DisplayItemsInFolderCore(Content.Path, ForceRefresh);
            }
            else
            {
                return Task.FromException(new Exception("Node.Context must be TreeViewNodeContent"));
            }
        }

        private async Task DisplayItemsInFolderCore(string FolderPath, bool ForceRefresh = false)
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

                if (IsBackOrForwardAction)
                {
                    IsBackOrForwardAction = false;
                }
                else if (!ForceRefresh)
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
                            GoAndBackRecord[GoAndBackRecord.Count - 1] = (GoAndBackRecord[GoAndBackRecord.Count - 1].Item1, Presenter.SelectedItems.Count > 1 ? string.Empty : (Presenter.SelectedItem?.Path ?? string.Empty));
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
                        Content = Globalization.GetString("QueueDialog_UnableAccessFile_Content"),
                        CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                    };

                    _ = await dialog.ShowAsync().ConfigureAwait(true);

                    return;
                }

                Presenter.FileCollection.Clear();

                PathConfiguration Config = await SQLite.Current.GetPathConfiguration(FolderPath).ConfigureAwait(true);

                await SortCollectionGenerator.Current.ModifySortWayAsync(FolderPath, Config.SortColumn, Config.SortDirection).ConfigureAwait(true);

                ItemDisplayMode.SelectedIndex = Config.DisplayModeIndex.GetValueOrDefault();

                List<FileSystemStorageItemBase> ItemList = SortCollectionGenerator.Current.GetSortedCollection(CurrentFolder.GetChildrenItems(SettingControl.IsDisplayHiddenItem));

                Presenter.HasFile.Visibility = ItemList.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
                Presenter.StatusTips.Text = Globalization.GetString("FilePresenterBottomStatusTip_TotalItem").Replace("{ItemNum}", ItemList.Count.ToString());

                if (Presenter.ListViewControl?.Header is ListViewHeaderController Instance)
                {
                    Instance.Filter.SetDataSource(ItemList);
                }

                for (int i = 0; i < ItemList.Count; i++)
                {
                    Presenter.FileCollection.Add(ItemList[i]);
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

        public Task DisplayItemsInFolder(FileSystemStorageItemBase Folder, bool ForceRefresh = false)
        {
            if (Folder == null)
            {
                throw new ArgumentNullException(nameof(Folder), "Parameter could not be null or empty");
            }

            return DisplayItemsInFolderCore(Folder.Path, ForceRefresh);
        }

        public Task DisplayItemsInFolder(string FolderPath, bool ForceRefresh = false)
        {
            return DisplayItemsInFolderCore(FolderPath, ForceRefresh);
        }

        private async void FolderDelete_Click(object sender, RoutedEventArgs e)
        {
            RightTabFlyout.Hide();

            if (!WIN_Native_API.CheckExist(CurrentFolder.Path))
            {
                QueueContentDialog dialog = new QueueContentDialog
                {
                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                    Content = Globalization.GetString("QueueDialog_LocateFolderFailure_Content"),
                    CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                };

                _ = await dialog.ShowAsync().ConfigureAwait(true);

                return;
            }

            if (Window.Current.CoreWindow.GetKeyState(VirtualKey.Shift).HasFlag(CoreVirtualKeyStates.Down))
            {
                await LoadingActivation(true, Globalization.GetString("Progress_Tip_Deleting")).ConfigureAwait(true);

            Retry:
                try
                {
                    await FullTrustProcessController.Current.DeleteAsync(CurrentFolder.Path, true).ConfigureAwait(true);

                    await DisplayItemsInFolder(Path.GetDirectoryName(CurrentFolder.Path)).ConfigureAwait(true);

                    await FolderTree.RootNodes[0].UpdateAllSubNodeAsync().ConfigureAwait(true);
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
                catch (FileNotFoundException)
                {
                    QueueContentDialog Dialog = new QueueContentDialog
                    {
                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                        Content = Globalization.GetString("QueueDialog_DeleteItemError_Content"),
                        CloseButtonText = Globalization.GetString("Common_Dialog_RefreshButton")
                    };

                    _ = await Dialog.ShowAsync().ConfigureAwait(true);

                    await DisplayItemsInFolder(Path.GetDirectoryName(CurrentFolder.Path)).ConfigureAwait(true);
                }
                catch (InvalidOperationException)
                {
                    QueueContentDialog dialog = new QueueContentDialog
                    {
                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                        Content = Globalization.GetString("QueueDialog_UnauthorizedDelete_Content"),
                        PrimaryButtonText = Globalization.GetString("Common_Dialog_GrantButton"),
                        CloseButtonText = Globalization.GetString("Common_Dialog_CancelButton")
                    };

                    if (await dialog.ShowAsync().ConfigureAwait(true) == ContentDialogResult.Primary)
                    {
                        if (await FullTrustProcessController.Current.SwitchToAdminModeAsync().ConfigureAwait(true))
                        {
                            goto Retry;
                        }
                        else
                        {
                            QueueContentDialog ErrorDialog = new QueueContentDialog
                            {
                                Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                Content = Globalization.GetString("QueueDialog_DenyElevation_Content"),
                                CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                            };

                            _ = await ErrorDialog.ShowAsync().ConfigureAwait(true);
                        }
                    }
                }
                catch (Exception)
                {
                    QueueContentDialog Dialog = new QueueContentDialog
                    {
                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                        Content = Globalization.GetString("QueueDialog_DeleteItemError_Content"),
                        CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                    };
                    _ = await Dialog.ShowAsync().ConfigureAwait(true);
                }

                await LoadingActivation(false).ConfigureAwait(true);
            }
            else
            {
                DeleteDialog QueueContenDialog = new DeleteDialog(Globalization.GetString("QueueDialog_DeleteFolder_Content"));

                if (await QueueContenDialog.ShowAsync().ConfigureAwait(true) == ContentDialogResult.Primary)
                {
                    await LoadingActivation(true, Globalization.GetString("Progress_Tip_Deleting")).ConfigureAwait(true);

                Retry:
                    try
                    {
                        await FullTrustProcessController.Current.DeleteAsync(CurrentFolder.Path, QueueContenDialog.IsPermanentDelete).ConfigureAwait(true);

                        await DisplayItemsInFolder(Path.GetDirectoryName(CurrentFolder.Path)).ConfigureAwait(true);

                        await FolderTree.RootNodes[0].UpdateAllSubNodeAsync().ConfigureAwait(true);
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
                    catch (FileNotFoundException)
                    {
                        QueueContentDialog Dialog = new QueueContentDialog
                        {
                            Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                            Content = Globalization.GetString("QueueDialog_DeleteItemError_Content"),
                            CloseButtonText = Globalization.GetString("Common_Dialog_RefreshButton")
                        };

                        _ = await Dialog.ShowAsync().ConfigureAwait(true);

                        await DisplayItemsInFolder(Path.GetDirectoryName(CurrentFolder.Path)).ConfigureAwait(true);
                    }
                    catch (InvalidOperationException)
                    {
                        QueueContentDialog dialog = new QueueContentDialog
                        {
                            Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                            Content = Globalization.GetString("QueueDialog_UnauthorizedDelete_Content"),
                            PrimaryButtonText = Globalization.GetString("Common_Dialog_GrantButton"),
                            CloseButtonText = Globalization.GetString("Common_Dialog_CancelButton")
                        };

                        if (await dialog.ShowAsync().ConfigureAwait(true) == ContentDialogResult.Primary)
                        {
                            if (await FullTrustProcessController.Current.SwitchToAdminModeAsync().ConfigureAwait(true))
                            {
                                goto Retry;
                            }
                            else
                            {
                                QueueContentDialog ErrorDialog = new QueueContentDialog
                                {
                                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                    Content = Globalization.GetString("QueueDialog_DenyElevation_Content"),
                                    CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                                };

                                _ = await ErrorDialog.ShowAsync().ConfigureAwait(true);
                            }
                        }
                    }
                    catch (Exception)
                    {
                        QueueContentDialog Dialog = new QueueContentDialog
                        {
                            Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                            Content = Globalization.GetString("QueueDialog_DeleteItemError_Content"),
                            CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                        };
                        _ = await Dialog.ShowAsync().ConfigureAwait(true);
                    }

                    await LoadingActivation(false).ConfigureAwait(true);
                }
            }
        }

        private void FolderTree_Collapsed(TreeView sender, TreeViewCollapsedEventArgs args)
        {
            args.Node.Children.Clear();
        }

        private async void FolderTree_RightTapped(object sender, Windows.UI.Xaml.Input.RightTappedRoutedEventArgs e)
        {
            if (e.PointerDeviceType == Windows.Devices.Input.PointerDeviceType.Mouse)
            {
                if ((e.OriginalSource as FrameworkElement)?.DataContext is TreeViewNode Node)
                {
                    if (FolderTree.RootNodes.Contains(Node))
                    {
                        FolderCopy.IsEnabled = false;
                        FolderCut.IsEnabled = false;
                        FolderDelete.IsEnabled = false;
                        FolderRename.IsEnabled = false;
                    }
                    else
                    {
                        FolderCopy.IsEnabled = true;
                        FolderCut.IsEnabled = true;
                        FolderDelete.IsEnabled = true;
                        FolderRename.IsEnabled = true;
                    }

                    FolderTree.ContextFlyout = RightTabFlyout;

                    await DisplayItemsInFolder(Node).ConfigureAwait(false);
                }
                else
                {
                    FolderTree.ContextFlyout = null;
                }
            }
        }

        private async void FolderRename_Click(object sender, RoutedEventArgs e)
        {
            RightTabFlyout.Hide();

            if (!WIN_Native_API.CheckExist(CurrentFolder.Path))
            {
                QueueContentDialog ErrorDialog = new QueueContentDialog
                {
                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                    Content = Globalization.GetString("QueueDialog_LocateFolderFailure_Content"),
                    CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                };

                _ = await ErrorDialog.ShowAsync().ConfigureAwait(true);

                return;
            }

            RenameDialog dialog = new RenameDialog(CurrentFolder);

            if ((await dialog.ShowAsync().ConfigureAwait(true)) == ContentDialogResult.Primary)
            {
                if (WIN_Native_API.CheckExist(Path.Combine(Path.GetDirectoryName(CurrentFolder.Path), dialog.DesireName)))
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

            Retry:
                try
                {
                    await FullTrustProcessController.Current.RenameAsync(CurrentFolder.Path, dialog.DesireName).ConfigureAwait(true);

                    await FolderTree.RootNodes[0].UpdateAllSubNodeAsync().ConfigureAwait(true);

                    if (await FileSystemStorageItemBase.OpenAsync(Path.Combine(Path.GetDirectoryName(CurrentFolder.Path), dialog.DesireName)).ConfigureAwait(true) is FileSystemStorageItemBase NewFolder)
                    {
                        CurrentFolder = NewFolder;
                    }
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
                        PrimaryButtonText = Globalization.GetString("Common_Dialog_GrantButton"),
                        CloseButtonText = Globalization.GetString("Common_Dialog_CancelButton")
                    };

                    if (await UnauthorizeDialog.ShowAsync().ConfigureAwait(true) == ContentDialogResult.Primary)
                    {
                        if (await FullTrustProcessController.Current.SwitchToAdminModeAsync().ConfigureAwait(true))
                        {
                            goto Retry;
                        }
                        else
                        {
                            QueueContentDialog ErrorDialog = new QueueContentDialog
                            {
                                Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                Content = Globalization.GetString("QueueDialog_DenyElevation_Content"),
                                CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                            };

                            _ = await ErrorDialog.ShowAsync().ConfigureAwait(true);
                        }
                    }
                }
                catch (Exception)
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

        private async void CreateFolder_Click(object sender, RoutedEventArgs e)
        {
            if (WIN_Native_API.CheckExist(CurrentFolder.Path))
            {
                if (await FileSystemStorageItemBase.CreateAsync(Path.Combine(CurrentFolder.Path, Globalization.GetString("Create_NewFolder_Admin_Name")), StorageItemTypes.Folder, CreateOption.GenerateUniqueName).ConfigureAwait(true) == null)
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
                QueueContentDialog dialog = new QueueContentDialog
                {
                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                    Content = Globalization.GetString("QueueDialog_LocateFolderFailure_Content"),
                    CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                };

                _ = await dialog.ShowAsync().ConfigureAwait(true);
            }
        }

        private async void FolderAttribute_Click(object sender, RoutedEventArgs e)
        {
            if (!WIN_Native_API.CheckExist(CurrentFolder.Path))
            {
                QueueContentDialog dialog = new QueueContentDialog
                {
                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                    Content = Globalization.GetString("QueueDialog_LocateFolderFailure_Content"),
                    CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                };
                _ = await dialog.ShowAsync().ConfigureAwait(true);

                return;
            }

            if (FolderTree.RootNodes.Any((Node) => (Node.Content as TreeViewNodeContent).Path.Equals(CurrentFolder.Path, StringComparison.OrdinalIgnoreCase)))
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

        private async void GlobeSearch_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
        {
            if (string.IsNullOrWhiteSpace(args.QueryText))
            {
                return;
            }

            if (await MSStoreHelper.Current.CheckPurchaseStatusAsync().ConfigureAwait(true))
            {
                if (Package.Current.Id.Architecture == ProcessorArchitecture.X64 || Package.Current.Id.Architecture == ProcessorArchitecture.X86 || Package.Current.Id.Architecture == ProcessorArchitecture.X86OnArm64)
                {
                    SearchInEverythingEngine.IsEnabled = await FullTrustProcessController.Current.CheckIfEverythingIsAvailableAsync().ConfigureAwait(true);
                }
                else
                {
                    SearchInEverythingEngine.IsEnabled = false;
                }
            }
            else
            {
                SearchInEverythingEngine.IsEnabled = false;
            }

            FlyoutBase.ShowAttachedFlyout(sender);

            await SQLite.Current.SetSearchHistoryAsync(args.QueryText).ConfigureAwait(false);
        }

        private async void GlobeSearch_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            if (!string.IsNullOrWhiteSpace(sender.Text))
            {
                if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
                {
                    sender.ItemsSource = await SQLite.Current.GetRelatedSearchHistoryAsync(sender.Text).ConfigureAwait(true);
                }
            }
        }

        private async void GlobeSearch_GotFocus(object sender, RoutedEventArgs e)
        {
            MainPage.ThisPage.IsAnyTaskRunning = true;

            if (string.IsNullOrEmpty(GlobeSearch.Text))
            {
                GlobeSearch.ItemsSource = await SQLite.Current.GetRelatedSearchHistoryAsync(string.Empty).ConfigureAwait(true);
            }
        }

        private void GlobeSearch_LostFocus(object sender, RoutedEventArgs e)
        {
            MainPage.ThisPage.IsAnyTaskRunning = false;
        }

        private async void AddressBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
        {
            LoadingControl.Focus(FocusState.Programmatic);

            string QueryText = string.Empty;

            if (args.ChosenSuggestion == null)
            {
                if (string.IsNullOrEmpty(AddressBoxTextBackup))
                {
                    return;
                }
                else
                {
                    QueryText = AddressBoxTextBackup;
                }
            }
            else
            {
                QueryText = args.ChosenSuggestion.ToString();
            }

            QueryText = QueryText.TrimEnd('\\');

            if (QueryText == CurrentFolder.Path)
            {
                return;
            }

            if (string.Equals(QueryText, "Powershell", StringComparison.OrdinalIgnoreCase) || string.Equals(QueryText, "Powershell.exe", StringComparison.OrdinalIgnoreCase))
            {
                string ExcutePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "WindowsPowerShell\\v1.0\\powershell.exe");

            Retry1:
                try
                {
                    await FullTrustProcessController.Current.RunAsync(ExcutePath, true, false, false, "-NoExit", "-Command", "Set-Location", CurrentFolder.Path).ConfigureAwait(false);
                }
                catch (InvalidOperationException)
                {
                    QueueContentDialog UnauthorizeDialog = new QueueContentDialog
                    {
                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                        Content = Globalization.GetString("QueueDialog_UnauthorizedExecute_Content"),
                        PrimaryButtonText = Globalization.GetString("Common_Dialog_GrantButton"),
                        CloseButtonText = Globalization.GetString("Common_Dialog_CancelButton")
                    };

                    if (await UnauthorizeDialog.ShowAsync().ConfigureAwait(true) == ContentDialogResult.Primary)
                    {
                        if (await FullTrustProcessController.Current.SwitchToAdminModeAsync().ConfigureAwait(true))
                        {
                            goto Retry1;
                        }
                        else
                        {
                            QueueContentDialog ErrorDialog = new QueueContentDialog
                            {
                                Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                Content = Globalization.GetString("QueueDialog_DenyElevation_Content"),
                                CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                            };

                            _ = await ErrorDialog.ShowAsync().ConfigureAwait(true);
                        }
                    }
                }

                return;
            }

            if (string.Equals(QueryText, "Cmd", StringComparison.OrdinalIgnoreCase) || string.Equals(QueryText, "Cmd.exe", StringComparison.OrdinalIgnoreCase))
            {
                string ExcutePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe");

            Retry2:
                try
                {
                    await FullTrustProcessController.Current.RunAsync(ExcutePath, true, false, false, "/k", "cd", "/d", CurrentFolder.Path).ConfigureAwait(false);
                }
                catch (InvalidOperationException)
                {
                    QueueContentDialog UnauthorizeDialog = new QueueContentDialog
                    {
                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                        Content = Globalization.GetString("QueueDialog_UnauthorizedExecute_Content"),
                        PrimaryButtonText = Globalization.GetString("Common_Dialog_GrantButton"),
                        CloseButtonText = Globalization.GetString("Common_Dialog_CancelButton")
                    };

                    if (await UnauthorizeDialog.ShowAsync().ConfigureAwait(true) == ContentDialogResult.Primary)
                    {
                        if (await FullTrustProcessController.Current.SwitchToAdminModeAsync().ConfigureAwait(true))
                        {
                            goto Retry2;
                        }
                        else
                        {
                            QueueContentDialog ErrorDialog = new QueueContentDialog
                            {
                                Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                Content = Globalization.GetString("QueueDialog_DenyElevation_Content"),
                                CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                            };

                            _ = await ErrorDialog.ShowAsync().ConfigureAwait(true);
                        }
                    }
                }

                return;
            }

            if (string.Equals(QueryText, "Wt", StringComparison.OrdinalIgnoreCase) || string.Equals(QueryText, "Wt.exe", StringComparison.OrdinalIgnoreCase))
            {
                switch (await Launcher.QueryUriSupportAsync(new Uri("ms-windows-store:"), LaunchQuerySupportType.Uri, "Microsoft.WindowsTerminal_8wekyb3d8bbwe"))
                {
                    case LaunchQuerySupportStatus.Available:
                    case LaunchQuerySupportStatus.NotSupported:
                        {
                        Retry:
                            try
                            {
                                await FullTrustProcessController.Current.RunAsync("wt.exe", false, false, false, "/d", CurrentFolder.Path).ConfigureAwait(false);
                            }
                            catch (InvalidOperationException)
                            {
                                QueueContentDialog UnauthorizeDialog = new QueueContentDialog
                                {
                                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                    Content = Globalization.GetString("QueueDialog_UnauthorizedExecute_Content"),
                                    PrimaryButtonText = Globalization.GetString("Common_Dialog_GrantButton"),
                                    CloseButtonText = Globalization.GetString("Common_Dialog_CancelButton")
                                };

                                if (await UnauthorizeDialog.ShowAsync().ConfigureAwait(true) == ContentDialogResult.Primary)
                                {
                                    if (await FullTrustProcessController.Current.SwitchToAdminModeAsync().ConfigureAwait(true))
                                    {
                                        goto Retry;
                                    }
                                    else
                                    {
                                        QueueContentDialog ErrorDialog = new QueueContentDialog
                                        {
                                            Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                            Content = Globalization.GetString("QueueDialog_DenyElevation_Content"),
                                            CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                                        };

                                        _ = await ErrorDialog.ShowAsync().ConfigureAwait(true);
                                    }
                                }
                            }

                            break;
                        }
                }

                return;
            }

            string ProtentialPath1 = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), QueryText);
            string ProtentialPath2 = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), QueryText);
            string ProtentialPath3 = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.SystemX86), QueryText);

            if (ProtentialPath1 != QueryText && WIN_Native_API.CheckExist(ProtentialPath1))
            {
                if (await FileSystemStorageItemBase.OpenAsync(ProtentialPath1).ConfigureAwait(true) is FileSystemStorageItemBase Item)
                {
                    await Presenter.EnterSelectedItem(Item).ConfigureAwait(true);

                    if (Item.StorageType == StorageItemTypes.Folder)
                    {
                        await SQLite.Current.SetPathHistoryAsync(Item.Path).ConfigureAwait(true);
                    }
                }

                return;
            }
            else if (ProtentialPath2 != QueryText && WIN_Native_API.CheckExist(ProtentialPath2))
            {
                if (await FileSystemStorageItemBase.OpenAsync(ProtentialPath2).ConfigureAwait(true) is FileSystemStorageItemBase Item)
                {
                    await Presenter.EnterSelectedItem(Item).ConfigureAwait(true);

                    if (Item.StorageType == StorageItemTypes.Folder)
                    {
                        await SQLite.Current.SetPathHistoryAsync(Item.Path).ConfigureAwait(true);
                    }
                }

                return;
            }
            else if (ProtentialPath3 != QueryText && WIN_Native_API.CheckExist(ProtentialPath3))
            {
                if (await FileSystemStorageItemBase.OpenAsync(ProtentialPath3).ConfigureAwait(true) is FileSystemStorageItemBase Item)
                {
                    await Presenter.EnterSelectedItem(Item).ConfigureAwait(true);

                    if (Item.StorageType == StorageItemTypes.Folder)
                    {
                        await SQLite.Current.SetPathHistoryAsync(Item.Path).ConfigureAwait(true);
                    }
                }

                return;
            }

            try
            {
                QueryText = await CommonEnvironmentVariables.ReplaceVariableAndGetActualPath(QueryText).ConfigureAwait(true);

                if (Path.IsPathRooted(QueryText) && CommonAccessCollection.HardDeviceList.FirstOrDefault((Drive) => Drive.Folder.Path.Equals(Path.GetPathRoot(QueryText), StringComparison.OrdinalIgnoreCase)) is HardDeviceInfo Device)
                {
                    if (Device.IsLockedByBitlocker)
                    {
                    Retry:
                        BitlockerPasswordDialog Dialog = new BitlockerPasswordDialog();

                        if (await Dialog.ShowAsync().ConfigureAwait(true) == ContentDialogResult.Primary)
                        {
                            await FullTrustProcessController.Current.RunAsync("powershell.exe", true, true, true, "-Command", $"$BitlockerSecureString = ConvertTo-SecureString '{Dialog.Password}' -AsPlainText -Force;", $"Unlock-BitLocker -MountPoint '{Device.Folder.Path}' -Password $BitlockerSecureString").ConfigureAwait(true);

                            StorageFolder DeviceFolder = await StorageFolder.GetFolderFromPathAsync(Device.Folder.Path);

                            BasicProperties Properties = await DeviceFolder.GetBasicPropertiesAsync();
                            IDictionary<string, object> PropertiesRetrieve = await Properties.RetrievePropertiesAsync(new string[] { "System.Capacity", "System.FreeSpace", "System.Volume.FileSystem", "System.Volume.BitLockerProtection" });

                            HardDeviceInfo NewDevice = new HardDeviceInfo(DeviceFolder, await DeviceFolder.GetThumbnailBitmapAsync().ConfigureAwait(true), PropertiesRetrieve, Device.DriveType);

                            if (!NewDevice.IsLockedByBitlocker)
                            {
                                int Index = CommonAccessCollection.HardDeviceList.IndexOf(Device);
                                CommonAccessCollection.HardDeviceList.Remove(Device);
                                CommonAccessCollection.HardDeviceList.Insert(Index, NewDevice);
                            }
                            else
                            {
                                QueueContentDialog UnlockFailedDialog = new QueueContentDialog
                                {
                                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                    Content = Globalization.GetString("QueueDialog_UnlockBitlockerFailed_Content"),
                                    PrimaryButtonText = Globalization.GetString("Common_Dialog_RetryButton"),
                                    CloseButtonText = Globalization.GetString("Common_Dialog_CancelButton")
                                };

                                if (await UnlockFailedDialog.ShowAsync().ConfigureAwait(true) == ContentDialogResult.Primary)
                                {
                                    goto Retry;
                                }
                                else
                                {
                                    return;
                                }
                            }
                        }
                        else
                        {
                            return;
                        }
                    }

                    if (await FileSystemStorageItemBase.OpenAsync(QueryText).ConfigureAwait(true) is FileSystemStorageItemBase Item)
                    {
                        if (Item.StorageType == StorageItemTypes.File)
                        {
                            await Presenter.EnterSelectedItem(Item).ConfigureAwait(true);
                        }
                        else
                        {
                            if (Path.GetPathRoot(QueryText) != Path.GetPathRoot(CurrentFolder.Path))
                            {
                                await Initialize(Item.Path).ConfigureAwait(true);
                            }
                            else
                            {
                                await DisplayItemsInFolder(Item.Path).ConfigureAwait(true);
                            }

                            await SQLite.Current.SetPathHistoryAsync(Item.Path).ConfigureAwait(true);

                            await JumpListController.Current.AddItem(JumpListGroup.Recent, Item.Path).ConfigureAwait(true);
                        }
                    }
                    else
                    {
                        QueueContentDialog dialog = new QueueContentDialog
                        {
                            Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                            Content = $"{Globalization.GetString("QueueDialog_LocatePathFailure_Content")} \r\"{QueryText}\"",
                            CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton"),
                        };

                        _ = await dialog.ShowAsync().ConfigureAwait(true);
                    }
                }
                else
                {
                    QueueContentDialog dialog = new QueueContentDialog
                    {
                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                        Content = $"{Globalization.GetString("QueueDialog_LocatePathFailure_Content")} \r\"{QueryText}\"",
                        CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton"),
                    };

                    _ = await dialog.ShowAsync().ConfigureAwait(true);
                }
            }
            catch (Exception)
            {
                QueueContentDialog dialog = new QueueContentDialog
                {
                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                    Content = $"{Globalization.GetString("QueueDialog_LocatePathFailure_Content")} \r\"{QueryText}\"",
                    CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton"),
                };

                _ = await dialog.ShowAsync().ConfigureAwait(true);
            }
        }

        private async void AddressBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            AddressBoxTextBackup = sender.Text;

            if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
            {
                if (Path.IsPathRooted(sender.Text) && CommonAccessCollection.HardDeviceList.Any((Drive) => Drive.Folder.Path.Equals(Path.GetPathRoot(sender.Text), StringComparison.OrdinalIgnoreCase)))
                {
                    if (Interlocked.Exchange(ref TextChangeLockResource, 1) == 0)
                    {
                        try
                        {
                            if (args.CheckCurrent())
                            {
                                string DirectoryPath = Path.GetPathRoot(sender.Text) == sender.Text ? sender.Text : Path.GetDirectoryName(sender.Text);
                                string FileName = Path.GetFileName(sender.Text);

                                if (await FileSystemStorageItemBase.OpenAsync(DirectoryPath).ConfigureAwait(true) is FileSystemStorageItemBase Item)
                                {
                                    if (string.IsNullOrEmpty(FileName))
                                    {
                                        sender.ItemsSource = Item.GetChildrenItems(SettingControl.IsDisplayHiddenItem).Take(20).Select((It) => It.Path);
                                    }
                                    else
                                    {
                                        sender.ItemsSource = Item.GetChildrenItems(SettingControl.IsDisplayHiddenItem).Where((Item) => Item.Name.StartsWith(FileName, StringComparison.OrdinalIgnoreCase)).Take(20).Select((It) => It.Path);
                                    }
                                }
                                else
                                {
                                    sender.ItemsSource = null;
                                }
                            }
                        }
                        catch (Exception)
                        {
                            sender.ItemsSource = null;
                        }
                        finally
                        {
                            _ = Interlocked.Exchange(ref TextChangeLockResource, 0);
                        }
                    }
                }
            }
        }

        public async void GoParentFolder_Click(object sender, RoutedEventArgs e)
        {
            if (Interlocked.Exchange(ref NavigateLockResource, 1) == 0)
            {
                try
                {
                    if (GoParentFolder.IsEnabled)
                    {
                        string CurrentFolderPath = CurrentFolder.Path;
                        string DirectoryPath = Path.GetDirectoryName(CurrentFolderPath);

                        if (!string.IsNullOrWhiteSpace(DirectoryPath))
                        {
                            await DisplayItemsInFolder(Path.GetDirectoryName(CurrentFolder.Path)).ConfigureAwait(true);

                            if (Presenter.FileCollection.Where((Item) => Item.StorageType == StorageItemTypes.Folder).FirstOrDefault((Item) => Item.Path.Equals(CurrentFolderPath, StringComparison.OrdinalIgnoreCase)) is FileSystemStorageItemBase Folder)
                            {
                                Presenter.SelectedItem = Folder;
                                Presenter.ItemPresenter.ScrollIntoView(Folder, ScrollIntoViewAlignment.Leading);
                            }
                        }
                        else
                        {
                            GoParentFolder.IsEnabled = false;
                        }
                    }
                }
                finally
                {
                    _ = Interlocked.Exchange(ref NavigateLockResource, 0);
                }
            }
        }

        public async void GoBackRecord_Click(object sender, RoutedEventArgs e)
        {
            if (Interlocked.Exchange(ref NavigateLockResource, 1) == 0)
            {
                string Path = string.Empty;
                string SelectedPath = string.Empty;

                try
                {
                    if (GoBackRecord.IsEnabled)
                    {
                        GoAndBackRecord[RecordIndex] = (GoAndBackRecord[RecordIndex].Item1, Presenter.SelectedItems.Count > 1 ? string.Empty : (Presenter.SelectedItem?.Path ?? string.Empty));

                        (Path, SelectedPath) = GoAndBackRecord[--RecordIndex];

                        if (WIN_Native_API.CheckExist(Path))
                        {
                            IsBackOrForwardAction = true;

                            await DisplayItemsInFolder(Path).ConfigureAwait(true);

                            await SQLite.Current.SetPathHistoryAsync(Path).ConfigureAwait(true);

                            if (!string.IsNullOrEmpty(SelectedPath) && Presenter.FileCollection.FirstOrDefault((Item) => Item.Path.Equals(SelectedPath, StringComparison.OrdinalIgnoreCase)) is FileSystemStorageItemBase Item)
                            {
                                Presenter.SelectedItem = Item;
                                Presenter.ItemPresenter.ScrollIntoView(Item, ScrollIntoViewAlignment.Leading);
                            }
                        }
                        else
                        {
                            QueueContentDialog dialog = new QueueContentDialog
                            {
                                Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                Content = $"{Globalization.GetString("QueueDialog_LocatePathFailure_Content")} \r\"{Path}\"",
                                CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton"),
                            };

                            _ = await dialog.ShowAsync().ConfigureAwait(true);

                            RecordIndex++;
                        }
                    }
                }
                catch (Exception)
                {
                    QueueContentDialog dialog = new QueueContentDialog
                    {
                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                        Content = $"{Globalization.GetString("QueueDialog_LocatePathFailure_Content")} \r\"{Path}\"",
                        CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton"),
                    };

                    _ = await dialog.ShowAsync().ConfigureAwait(true);

                }
                finally
                {
                    _ = Interlocked.Exchange(ref NavigateLockResource, 0);
                }
            }
        }

        public async void GoForwardRecord_Click(object sender, RoutedEventArgs e)
        {
            if (Interlocked.Exchange(ref NavigateLockResource, 1) == 0)
            {
                string Path = string.Empty;
                string SelectedPath = string.Empty;

                try
                {
                    if (GoForwardRecord.IsEnabled)
                    {
                        GoAndBackRecord[RecordIndex] = (GoAndBackRecord[RecordIndex].Item1, Presenter.SelectedItems.Count > 1 ? string.Empty : (Presenter.SelectedItem?.Path ?? string.Empty));

                        (Path, SelectedPath) = GoAndBackRecord[++RecordIndex];

                        if (WIN_Native_API.CheckExist(Path))
                        {
                            IsBackOrForwardAction = true;

                            await DisplayItemsInFolder(Path).ConfigureAwait(true);

                            await SQLite.Current.SetPathHistoryAsync(Path).ConfigureAwait(true);

                            if (!string.IsNullOrEmpty(SelectedPath) && Presenter.FileCollection.FirstOrDefault((Item) => Item.Path.Equals(SelectedPath, StringComparison.OrdinalIgnoreCase)) is FileSystemStorageItemBase Item)
                            {
                                Presenter.SelectedItem = Item;
                                Presenter.ItemPresenter.ScrollIntoView(Item, ScrollIntoViewAlignment.Leading);
                            }
                        }
                        else
                        {
                            QueueContentDialog dialog = new QueueContentDialog
                            {
                                Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                Content = $"{Globalization.GetString("QueueDialog_LocatePathFailure_Content")} \r\"{Path}\"",
                                CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton"),
                            };

                            _ = await dialog.ShowAsync().ConfigureAwait(true);

                            RecordIndex--;
                        }
                    }
                }
                catch (Exception)
                {
                    QueueContentDialog dialog = new QueueContentDialog
                    {
                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                        Content = $"{Globalization.GetString("QueueDialog_LocatePathFailure_Content")} \r\"{Path}\"",
                        CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton"),
                    };
                    _ = await dialog.ShowAsync().ConfigureAwait(true);

                    RecordIndex--;
                }
                finally
                {
                    _ = Interlocked.Exchange(ref NavigateLockResource, 0);
                }
            }
        }

        private async void AddressBox_GotFocus(object sender, RoutedEventArgs e)
        {
            MainPage.ThisPage.IsAnyTaskRunning = true;

            if (string.IsNullOrEmpty(AddressBox.Text))
            {
                AddressBox.Text = CurrentFolder.Path;
            }

            AddressButtonContainer.Visibility = Visibility.Collapsed;

            AddressBox.FindChildOfType<TextBox>()?.SelectAll();

            AddressBox.ItemsSource = await SQLite.Current.GetRelatedPathHistoryAsync().ConfigureAwait(true);
        }

        private async void ItemDisplayMode_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            switch (ItemDisplayMode.SelectedIndex)
            {
                case 0:
                    {
                        Presenter.ItemPresenter = (Presenter.FindName("GridViewRefreshContainer") as RefreshContainer)?.Content as ListViewBase;

                        Presenter.GridViewControl.ItemTemplate = Presenter.GridViewTileDataTemplate;
                        Presenter.GridViewControl.ItemsPanel = Presenter.HorizontalGridViewPanel;

                        while (true)
                        {
                            if (Presenter.GridViewControl.FindChildOfType<ScrollViewer>() is ScrollViewer Scroll)
                            {
                                Scroll.HorizontalScrollMode = ScrollMode.Disabled;
                                Scroll.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
                                Scroll.VerticalScrollMode = ScrollMode.Auto;
                                Scroll.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
                                break;
                            }
                            else
                            {
                                await Task.Delay(300).ConfigureAwait(true);
                            }
                        }

                        break;
                    }
                case 1:
                    {
                        Presenter.ItemPresenter = (Presenter.FindName("ListViewRefreshContainer") as RefreshContainer)?.Content as ListViewBase;
                        break;
                    }
                case 2:
                    {
                        Presenter.ItemPresenter = (Presenter.FindName("GridViewRefreshContainer") as RefreshContainer)?.Content as ListViewBase;

                        Presenter.GridViewControl.ItemTemplate = Presenter.GridViewListDataTemplate;
                        Presenter.GridViewControl.ItemsPanel = Presenter.VerticalGridViewPanel;

                        while (true)
                        {
                            if (Presenter.GridViewControl.FindChildOfType<ScrollViewer>() is ScrollViewer Scroll)
                            {
                                Scroll.HorizontalScrollMode = ScrollMode.Auto;
                                Scroll.HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;
                                Scroll.VerticalScrollMode = ScrollMode.Disabled;
                                Scroll.VerticalScrollBarVisibility = ScrollBarVisibility.Disabled;
                                break;
                            }
                            else
                            {
                                await Task.Delay(300).ConfigureAwait(true);
                            }
                        }

                        break;
                    }
                case 3:
                    {
                        Presenter.ItemPresenter = (Presenter.FindName("GridViewRefreshContainer") as RefreshContainer)?.Content as ListViewBase;

                        Presenter.GridViewControl.ItemTemplate = Presenter.GridViewLargeImageDataTemplate;
                        Presenter.GridViewControl.ItemsPanel = Presenter.HorizontalGridViewPanel;

                        while (true)
                        {
                            if (Presenter.GridViewControl.FindChildOfType<ScrollViewer>() is ScrollViewer Scroll)
                            {
                                Scroll.HorizontalScrollMode = ScrollMode.Disabled;
                                Scroll.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
                                Scroll.VerticalScrollMode = ScrollMode.Auto;
                                Scroll.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
                                break;
                            }
                            else
                            {
                                await Task.Delay(300).ConfigureAwait(true);
                            }
                        }

                        break;
                    }
                case 4:
                    {
                        Presenter.ItemPresenter = (Presenter.FindName("GridViewRefreshContainer") as RefreshContainer)?.Content as ListViewBase;

                        Presenter.GridViewControl.ItemTemplate = Presenter.GridViewMediumImageDataTemplate;
                        Presenter.GridViewControl.ItemsPanel = Presenter.HorizontalGridViewPanel;

                        while (true)
                        {
                            if (Presenter.GridViewControl.FindChildOfType<ScrollViewer>() is ScrollViewer Scroll)
                            {
                                Scroll.HorizontalScrollMode = ScrollMode.Disabled;
                                Scroll.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
                                Scroll.VerticalScrollMode = ScrollMode.Auto;
                                Scroll.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
                                break;
                            }
                            else
                            {
                                await Task.Delay(300).ConfigureAwait(true);
                            }
                        }

                        break;
                    }
                case 5:
                    {
                        Presenter.ItemPresenter = (Presenter.FindName("GridViewRefreshContainer") as RefreshContainer)?.Content as ListViewBase;

                        Presenter.GridViewControl.ItemTemplate = Presenter.GridViewSmallImageDataTemplate;
                        Presenter.GridViewControl.ItemsPanel = Presenter.HorizontalGridViewPanel;

                        while (true)
                        {
                            if (Presenter.GridViewControl.FindChildOfType<ScrollViewer>() is ScrollViewer Scroll)
                            {
                                Scroll.HorizontalScrollMode = ScrollMode.Disabled;
                                Scroll.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
                                Scroll.VerticalScrollMode = ScrollMode.Auto;
                                Scroll.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
                                break;
                            }
                            else
                            {
                                await Task.Delay(300).ConfigureAwait(true);
                            }
                        }

                        break;
                    }
            }

            await SQLite.Current.SetPathConfiguration(new PathConfiguration(CurrentFolder.Path, ItemDisplayMode.SelectedIndex)).ConfigureAwait(false);
        }

        private void AddressBox_KeyDown(object sender, Windows.UI.Xaml.Input.KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Tab)
            {
                string FirstTip = AddressBox.Items.FirstOrDefault()?.ToString();

                if (!string.IsNullOrEmpty(FirstTip))
                {
                    AddressBox.Text = FirstTip;
                }

                e.Handled = true;
            }
        }

        private void AddressBox_LostFocus(object sender, RoutedEventArgs e)
        {
            AddressBox.Text = string.Empty;
            AddressButtonContainer.Visibility = Visibility.Visible;
            MainPage.ThisPage.IsAnyTaskRunning = false;
        }

        private async void AddressButton_Click(object sender, RoutedEventArgs e)
        {
            string OriginalString = string.Join("\\", AddressButtonList.Take(AddressButtonList.IndexOf(Convert.ToString(((Button)sender).Content)) + 1).Skip(1));
            string ActualString = Path.Combine(Path.GetPathRoot(CurrentFolder.Path), OriginalString);

            if (ActualString == CurrentFolder.Path)
            {
                return;
            }

            try
            {
                await DisplayItemsInFolder(ActualString).ConfigureAwait(true);
                await SQLite.Current.SetPathHistoryAsync(ActualString).ConfigureAwait(true);
            }
            catch
            {
                QueueContentDialog dialog = new QueueContentDialog
                {
                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                    Content = $"{Globalization.GetString("QueueDialog_LocatePathFailure_Content")} \r\"{ActualString}\"",
                    CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton"),
                };
                _ = await dialog.ShowAsync().ConfigureAwait(true);
            }
        }

        private async void AddressExtention_Click(object sender, RoutedEventArgs e)
        {
            Button Btn = sender as Button;
            TextBlock StateText = Btn.Content as TextBlock;

            AddressExtentionList.Clear();

            string OriginalString = string.Join("\\", AddressButtonList.Take(AddressButtonList.IndexOf(Convert.ToString(Btn.DataContext)) + 1).Skip(1));
            string ActualString = Path.Combine(Path.GetPathRoot(CurrentFolder.Path), OriginalString);

            List<string> ItemList = WIN_Native_API.GetStorageItemsAndReturnPath(ActualString, SettingControl.IsDisplayHiddenItem, ItemFilters.Folder);

            foreach (string SubFolderName in ItemList.Select((Item) => Path.GetFileName(Item)))
            {
                AddressExtentionList.Add(SubFolderName);
            }

            if (AddressExtentionList.Count != 0)
            {
                StateText.RenderTransformOrigin = new Point(0.55, 0.6);
                await StateText.Rotate(90, duration: 150).StartAsync().ConfigureAwait(true);

                FlyoutBase.SetAttachedFlyout(Btn, AddressExtentionFlyout);
                FlyoutBase.ShowAttachedFlyout(Btn);
            }
        }

        private async void AddressExtentionFlyout_Closing(FlyoutBase sender, FlyoutBaseClosingEventArgs args)
        {
            AddressExtentionList.Clear();

            await ((sender.Target as Button).Content as FrameworkElement).Rotate(0, duration: 150).StartAsync().ConfigureAwait(false);
        }

        private async void AddressExtensionSubFolderList_ItemClick(object sender, ItemClickEventArgs e)
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                AddressExtentionFlyout.Hide();
            });

            if (!string.IsNullOrEmpty(e.ClickedItem.ToString()))
            {
                string OriginalString = string.Join("\\", AddressButtonList.Take(AddressButtonList.IndexOf(Convert.ToString(AddressExtentionFlyout.Target.FindParentOfType<StackPanel>()?.FindChildOfName<Button>("AddressButton")?.Content)) + 1).Skip(1));
                string ActualString = Path.Combine(Path.GetPathRoot(CurrentFolder.Path), OriginalString);

                string TargetPath = Path.Combine(ActualString, e.ClickedItem.ToString());

                try
                {
                    await DisplayItemsInFolder(TargetPath).ConfigureAwait(true);
                }
                catch
                {
                    QueueContentDialog dialog = new QueueContentDialog
                    {
                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                        Content = $"{Globalization.GetString("QueueDialog_LocatePathFailure_Content")} \r\"{TargetPath}\"",
                        CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton"),
                    };
                    _ = await dialog.ShowAsync().ConfigureAwait(true);
                }
            }
        }

        private async void AddressButton_Drop(object sender, DragEventArgs e)
        {
            string OriginalString = string.Join("\\", AddressButtonList.Take(AddressButtonList.IndexOf(Convert.ToString(((Button)sender).Content)) + 1).Skip(1));
            string ActualPath = Path.Combine(Path.GetPathRoot(CurrentFolder.Path), OriginalString);

            if (Interlocked.Exchange(ref DropLockResource, 1) == 0)
            {
                try
                {
                    if (e.DataView.Contains(StandardDataFormats.Html))
                    {
                        string Html = await e.DataView.GetHtmlFormatAsync();
                        string Fragment = HtmlFormatHelper.GetStaticFragment(Html);

                        HtmlDocument Document = new HtmlDocument();
                        Document.LoadHtml(Fragment);
                        HtmlNode HeadNode = Document.DocumentNode.SelectSingleNode("/head");

                        if (HeadNode?.InnerText == "RX-Explorer-TransferNotStorageItem")
                        {
                            HtmlNodeCollection BodyNode = Document.DocumentNode.SelectNodes("/p");
                            List<string> LinkAndHiddenItemsPath = BodyNode.Select((Node) => Node.InnerText).ToList();

                            StorageFolder TargetFolder = await StorageFolder.GetFolderFromPathAsync(ActualPath);

                            switch (e.AcceptedOperation)
                            {
                                case DataPackageOperation.Copy:
                                    {
                                        await LoadingActivation(true, Globalization.GetString("Progress_Tip_Copying")).ConfigureAwait(true);

                                    Retry:
                                        try
                                        {
                                            await FullTrustProcessController.Current.CopyAsync(LinkAndHiddenItemsPath, TargetFolder.Path, (s, arg) =>
                                            {
                                                if (ProBar.Value < arg.ProgressPercentage)
                                                {
                                                    ProBar.IsIndeterminate = false;
                                                    ProBar.Value = arg.ProgressPercentage;
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
                                                PrimaryButtonText = Globalization.GetString("Common_Dialog_GrantButton"),
                                                CloseButtonText = Globalization.GetString("Common_Dialog_CancelButton")
                                            };

                                            if (await dialog.ShowAsync().ConfigureAwait(true) == ContentDialogResult.Primary)
                                            {
                                                if (await FullTrustProcessController.Current.SwitchToAdminModeAsync().ConfigureAwait(true))
                                                {
                                                    goto Retry;
                                                }
                                                else
                                                {
                                                    QueueContentDialog ErrorDialog = new QueueContentDialog
                                                    {
                                                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                                        Content = Globalization.GetString("QueueDialog_DenyElevation_Content"),
                                                        CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                                                    };

                                                    _ = await ErrorDialog.ShowAsync().ConfigureAwait(true);
                                                }
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
                                        if (LinkAndHiddenItemsPath.All((Item) => Path.GetDirectoryName(Item) == ActualPath))
                                        {
                                            return;
                                        }

                                        await LoadingActivation(true, Globalization.GetString("Progress_Tip_Moving")).ConfigureAwait(true);

                                    Retry:
                                        try
                                        {
                                            await FullTrustProcessController.Current.MoveAsync(LinkAndHiddenItemsPath, TargetFolder.Path, (s, arg) =>
                                            {
                                                if (ProBar.Value < arg.ProgressPercentage)
                                                {
                                                    ProBar.IsIndeterminate = false;
                                                    ProBar.Value = arg.ProgressPercentage;
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
                                                PrimaryButtonText = Globalization.GetString("Common_Dialog_GrantButton"),
                                                CloseButtonText = Globalization.GetString("Common_Dialog_CancelButton")
                                            };

                                            if (await dialog.ShowAsync().ConfigureAwait(true) == ContentDialogResult.Primary)
                                            {
                                                if (await FullTrustProcessController.Current.SwitchToAdminModeAsync().ConfigureAwait(true))
                                                {
                                                    goto Retry;
                                                }
                                                else
                                                {
                                                    QueueContentDialog ErrorDialog = new QueueContentDialog
                                                    {
                                                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                                        Content = Globalization.GetString("QueueDialog_DenyElevation_Content"),
                                                        CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                                                    };

                                                    _ = await ErrorDialog.ShowAsync().ConfigureAwait(true);
                                                }
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

                    if (e.DataView.Contains(StandardDataFormats.StorageItems))
                    {
                        List<IStorageItem> DragItemList = (await e.DataView.GetStorageItemsAsync()).ToList();

                        switch (e.AcceptedOperation)
                        {
                            case DataPackageOperation.Copy:
                                {
                                    await LoadingActivation(true, Globalization.GetString("Progress_Tip_Copying")).ConfigureAwait(true);

                                Retry:
                                    try
                                    {
                                        await FullTrustProcessController.Current.CopyAsync(DragItemList.Select((Item) => Item.Path), ActualPath, (s, arg) =>
                                          {
                                              if (ProBar.Value < arg.ProgressPercentage)
                                              {
                                                  ProBar.IsIndeterminate = false;
                                                  ProBar.Value = arg.ProgressPercentage;
                                              }
                                          }).ConfigureAwait(true);

                                        if (!SettingControl.IsDetachTreeViewAndPresenter)
                                        {
                                            await FolderTree.RootNodes[0].UpdateAllSubNodeAsync().ConfigureAwait(true);
                                        }
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
                                            PrimaryButtonText = Globalization.GetString("Common_Dialog_GrantButton"),
                                            CloseButtonText = Globalization.GetString("Common_Dialog_CancelButton")
                                        };

                                        if (await dialog.ShowAsync().ConfigureAwait(true) == ContentDialogResult.Primary)
                                        {
                                            if (await FullTrustProcessController.Current.SwitchToAdminModeAsync().ConfigureAwait(true))
                                            {
                                                goto Retry;
                                            }
                                            else
                                            {
                                                QueueContentDialog ErrorDialog = new QueueContentDialog
                                                {
                                                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                                    Content = Globalization.GetString("QueueDialog_DenyElevation_Content"),
                                                    CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                                                };

                                                _ = await ErrorDialog.ShowAsync().ConfigureAwait(true);
                                            }
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
                                    if (DragItemList.Select((Item) => Item.Path).All((Item) => Path.GetDirectoryName(Item) == ActualPath))
                                    {
                                        return;
                                    }

                                    await LoadingActivation(true, Globalization.GetString("Progress_Tip_Moving")).ConfigureAwait(true);

                                Retry:
                                    try
                                    {
                                        await FullTrustProcessController.Current.MoveAsync(DragItemList.Select((Item) => Item.Path), ActualPath, (s, arg) =>
                                          {
                                              if (ProBar.Value < arg.ProgressPercentage)
                                              {
                                                  ProBar.IsIndeterminate = false;
                                                  ProBar.Value = arg.ProgressPercentage;
                                              }
                                          }).ConfigureAwait(true);

                                        if (!SettingControl.IsDetachTreeViewAndPresenter)
                                        {
                                            await FolderTree.RootNodes[0].UpdateAllSubNodeAsync().ConfigureAwait(true);
                                        }
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
                                            PrimaryButtonText = Globalization.GetString("Common_Dialog_GrantButton"),
                                            CloseButtonText = Globalization.GetString("Common_Dialog_CancelButton")
                                        };

                                        if (await dialog.ShowAsync().ConfigureAwait(true) == ContentDialogResult.Primary)
                                        {
                                            if (await FullTrustProcessController.Current.SwitchToAdminModeAsync().ConfigureAwait(true))
                                            {
                                                goto Retry;
                                            }
                                            else
                                            {
                                                QueueContentDialog ErrorDialog = new QueueContentDialog
                                                {
                                                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                                    Content = Globalization.GetString("QueueDialog_DenyElevation_Content"),
                                                    CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                                                };

                                                _ = await ErrorDialog.ShowAsync().ConfigureAwait(true);
                                            }
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
                finally
                {
                    await LoadingActivation(false).ConfigureAwait(true);
                    e.Handled = true;
                    _ = Interlocked.Exchange(ref DropLockResource, 0);
                }
            }
        }

        private async void AddressButton_DragOver(object sender, DragEventArgs e)
        {
            var Deferral = e.GetDeferral();

            try
            {
                if (e.OriginalSource is Button Btn)
                {
                    if (e.DataView.Contains(StandardDataFormats.StorageItems))
                    {
                        if (e.Modifiers.HasFlag(DragDropModifiers.Control))
                        {
                            e.AcceptedOperation = DataPackageOperation.Copy;
                            e.DragUIOverride.Caption = $"{Globalization.GetString("Drag_Tip_CopyTo")} {Btn.Content}";
                        }
                        else
                        {
                            e.AcceptedOperation = DataPackageOperation.Move;
                            e.DragUIOverride.Caption = $"{Globalization.GetString("Drag_Tip_MoveTo")} {Btn.Content}";
                        }

                        e.DragUIOverride.IsContentVisible = true;
                        e.DragUIOverride.IsCaptionVisible = true;
                    }
                    else if (e.DataView.Contains(StandardDataFormats.Html))
                    {
                        string Html = await e.DataView.GetHtmlFormatAsync();
                        string Fragment = HtmlFormatHelper.GetStaticFragment(Html);

                        HtmlDocument Document = new HtmlDocument();
                        Document.LoadHtml(Fragment);
                        HtmlNode HeadNode = Document.DocumentNode.SelectSingleNode("/head");

                        if (HeadNode?.InnerText == "RX-Explorer-TransferNotStorageItem")
                        {
                            if (e.Modifiers.HasFlag(DragDropModifiers.Control))
                            {
                                e.AcceptedOperation = DataPackageOperation.Copy;
                                e.DragUIOverride.Caption = $"{Globalization.GetString("Drag_Tip_CopyTo")} {Btn.Content}";
                            }
                            else
                            {
                                e.AcceptedOperation = DataPackageOperation.Move;
                                e.DragUIOverride.Caption = $"{Globalization.GetString("Drag_Tip_MoveTo")} {Btn.Content}";
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

        private async void FolderTree_Holding(object sender, Windows.UI.Xaml.Input.HoldingRoutedEventArgs e)
        {
            if (e.HoldingState == Windows.UI.Input.HoldingState.Started)
            {
                if ((e.OriginalSource as FrameworkElement)?.DataContext is TreeViewNode Node)
                {
                    if (FolderTree.RootNodes.Contains(Node))
                    {
                        FolderDelete.IsEnabled = false;
                        FolderRename.IsEnabled = false;
                    }
                    else
                    {
                        FolderDelete.IsEnabled = true;
                        FolderRename.IsEnabled = true;
                    }

                    FolderTree.ContextFlyout = RightTabFlyout;

                    await DisplayItemsInFolder(Node).ConfigureAwait(false);
                }
                else
                {
                    FolderTree.ContextFlyout = null;
                }
            }
        }

        public void Dispose()
        {
            AddressButtonList.Clear();

            FolderTree.RootNodes.Clear();

            Presenter.FileCollection.Clear();
            Presenter.HasFile.Visibility = Visibility.Collapsed;

            Application.Current.Suspending -= Current_Suspending;
            Application.Current.Resuming -= Current_Resuming;
            Frame.Navigated -= Frame_Navigated;

            RecordIndex = 0;

            GoAndBackRecord.Clear();

            IsBackOrForwardAction = false;
            GoBackRecord.IsEnabled = false;
            GoForwardRecord.IsEnabled = false;
            GoParentFolder.IsEnabled = false;

            CurrentFolder = null;

            EnterLock.Dispose();
            AreaWatcher.Dispose();
        }

        private void Presenter_PointerWheelChanged(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            Frame Frame = sender as Frame;

            int Delta = e.GetCurrentPoint(Frame).Properties.MouseWheelDelta;

            if (Window.Current.CoreWindow.GetKeyState(VirtualKey.Control).HasFlag(CoreVirtualKeyStates.Down))
            {
                if (Delta > 0)
                {
                    if (ItemDisplayMode.SelectedIndex > 0)
                    {
                        ItemDisplayMode.SelectedIndex -= 1;
                    }
                }
                else
                {
                    if (ItemDisplayMode.SelectedIndex < ItemDisplayMode.Items.Count - 1)
                    {
                        ItemDisplayMode.SelectedIndex += 1;
                    }
                }

                e.Handled = true;
            }
        }

        private async void FolderCut_Click(object sender, RoutedEventArgs e)
        {
            if (CurrentFolder != null)
            {
                try
                {
                    RightTabFlyout.Hide();

                    Clipboard.Clear();

                    DataPackage Package = new DataPackage
                    {
                        RequestedOperation = DataPackageOperation.Move
                    };

                    if (CurrentFolder is HyperlinkStorageItem or HiddenStorageItem)
                    {
                        Package.SetHtmlFormat(HtmlFormatHelper.CreateHtmlFormat($"<head>RX-Explorer-TransferNotStorageItem</head><p>{CurrentFolder.Path}</p>"));
                    }
                    else
                    {
                        if (await CurrentFolder.GetStorageItem().ConfigureAwait(true) is IStorageItem Item)
                        {
                            Package.SetStorageItems(new IStorageItem[] { Item }, false);
                        }
                    }

                    Clipboard.SetContent(Package);
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

        private async void FolderCopy_Click(object sender, RoutedEventArgs e)
        {
            if (CurrentFolder != null)
            {
                try
                {
                    RightTabFlyout.Hide();

                    Clipboard.Clear();

                    DataPackage Package = new DataPackage
                    {
                        RequestedOperation = DataPackageOperation.Copy
                    };

                    if (CurrentFolder is HyperlinkStorageItem or HiddenStorageItem)
                    {
                        Package.SetHtmlFormat(HtmlFormatHelper.CreateHtmlFormat($"<head>RX-Explorer-TransferNotStorageItem</head><p>{CurrentFolder.Path}</p>"));
                    }
                    else
                    {
                        if (await CurrentFolder.GetStorageItem().ConfigureAwait(true) is IStorageItem Item)
                        {
                            Package.SetStorageItems(new IStorageItem[] { Item }, false);
                        }
                    }

                    Clipboard.SetContent(Package);
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

        private async void OpenFolderInNewWindow_Click(object sender, RoutedEventArgs e)
        {
            if (CurrentFolder != null)
            {
                await Launcher.LaunchUriAsync(new Uri($"rx-explorer:{Uri.EscapeDataString(CurrentFolder.Path)}"));
            }
        }

        private void OpenFolderInNewTab_Click(object sender, RoutedEventArgs e)
        {
            if (CurrentFolder != null)
            {
                TabViewContainer.ThisPage.CreateNewTabAndOpenTargetFolder(CurrentFolder.Path);
            }
        }

        private void SearchEngineConfirm_Click(object sender, RoutedEventArgs e)
        {
            SearchEngineFlyout.Hide();

            if (SearchInDefaultEngine.IsChecked.GetValueOrDefault())
            {
                if (AnimationController.Current.IsEnableAnimation)
                {
                    Frame.Navigate(typeof(SearchPage), new Tuple<WeakReference<FileControl>, SearchCategory, bool?, bool?, bool?, uint?>(new WeakReference<FileControl>(this), BuiltInSearchAllSubFolders.IsChecked.GetValueOrDefault() ? SearchCategory.BuiltInEngine_Deep : SearchCategory.BuiltInEngine_Shallow, BuiltInEngineIgnoreCase.IsChecked, BuiltInEngineIncludeRegex.IsChecked, null, null), new DrillInNavigationTransitionInfo());
                }
                else
                {
                    Frame.Navigate(typeof(SearchPage), new Tuple<WeakReference<FileControl>, SearchCategory, bool?, bool?, bool?, uint?>(new WeakReference<FileControl>(this), BuiltInSearchAllSubFolders.IsChecked.GetValueOrDefault() ? SearchCategory.BuiltInEngine_Deep : SearchCategory.BuiltInEngine_Shallow, BuiltInEngineIgnoreCase.IsChecked, BuiltInEngineIncludeRegex.IsChecked, null, null), new SuppressNavigationTransitionInfo());
                }
            }
            else
            {
                if (AnimationController.Current.IsEnableAnimation)
                {
                    Frame.Navigate(typeof(SearchPage), new Tuple<WeakReference<FileControl>, SearchCategory, bool?, bool?, bool?, uint?>(new WeakReference<FileControl>(this), SearchCategory.EverythingEngine, EverythingEngineIgnoreCase.IsChecked, EverythingEngineIncludeRegex.IsChecked, EverythingEngineSearchGloble.IsChecked, Convert.ToUInt32(EverythingEngineResultLimit.SelectedItem)), new DrillInNavigationTransitionInfo());
                }
                else
                {
                    Frame.Navigate(typeof(SearchPage), new Tuple<WeakReference<FileControl>, SearchCategory, bool?, bool?, bool?, uint?>(new WeakReference<FileControl>(this), SearchCategory.EverythingEngine, EverythingEngineIgnoreCase.IsChecked, EverythingEngineIncludeRegex.IsChecked, EverythingEngineSearchGloble.IsChecked, Convert.ToUInt32(EverythingEngineResultLimit.SelectedItem)), new SuppressNavigationTransitionInfo());
                }
            }
        }

        private void SearchEngineCancel_Click(object sender, RoutedEventArgs e)
        {
            SearchEngineFlyout.Hide();
        }

        private void SearchEngineFlyout_Opened(object sender, object e)
        {
            MainPage.ThisPage.IsAnyTaskRunning = true;
            SearchEngineConfirm.Focus(FocusState.Programmatic);
        }

        private void SearchEngineFlyout_Closed(object sender, object e)
        {
            MainPage.ThisPage.IsAnyTaskRunning = false;
        }

        private void EverythingQuestion_Tapped(object sender, Windows.UI.Xaml.Input.TappedRoutedEventArgs e)
        {
            EverythingTip.IsOpen = true;
        }

        private void SeachEngineOptionSave_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox Box)
            {
                switch (Box.Name)
                {
                    case "EverythingEngineIgnoreCase":
                        {
                            ApplicationData.Current.LocalSettings.Values["EverythingEngineIgnoreCase"] = true;
                            break;
                        }
                    case "EverythingEngineIncludeRegex":
                        {
                            ApplicationData.Current.LocalSettings.Values["EverythingEngineIncludeRegex"] = true;
                            break;
                        }
                    case "EverythingEngineSearchGloble":
                        {
                            ApplicationData.Current.LocalSettings.Values["EverythingEngineSearchGloble"] = true;
                            break;
                        }
                    case "BuiltInEngineIgnoreCase":
                        {
                            ApplicationData.Current.LocalSettings.Values["BuiltInEngineIgnoreCase"] = true;
                            break;
                        }
                    case "BuiltInEngineIncludeRegex":
                        {
                            ApplicationData.Current.LocalSettings.Values["BuiltInEngineIncludeRegex"] = true;
                            break;
                        }
                    case "BuiltInSearchAllSubFolders":
                        {
                            ApplicationData.Current.LocalSettings.Values["BuiltInSearchAllSubFolders"] = true;
                            break;
                        }
                }
            }
        }

        private void SeachEngineOptionSave_UnChecked(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox Box)
            {
                switch (Box.Name)
                {
                    case "EverythingEngineIgnoreCase":
                        {
                            ApplicationData.Current.LocalSettings.Values["EverythingEngineIgnoreCase"] = false;
                            break;
                        }
                    case "EverythingEngineIncludeRegex":
                        {
                            ApplicationData.Current.LocalSettings.Values["EverythingEngineIncludeRegex"] = false;
                            break;
                        }
                    case "EverythingEngineSearchGloble":
                        {
                            ApplicationData.Current.LocalSettings.Values["EverythingEngineSearchGloble"] = false;
                            break;
                        }
                    case "BuiltInEngineIgnoreCase":
                        {
                            ApplicationData.Current.LocalSettings.Values["BuiltInEngineIgnoreCase"] = false;
                            break;
                        }
                    case "BuiltInEngineIncludeRegex":
                        {
                            ApplicationData.Current.LocalSettings.Values["BuiltInEngineIncludeRegex"] = false;
                            break;
                        }
                    case "BuiltInSearchAllSubFolders":
                        {
                            ApplicationData.Current.LocalSettings.Values["BuiltInSearchAllSubFolders"] = false;
                            break;
                        }
                }
            }
        }

        private void SearchEngineChoiceSave_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is RadioButton Btn)
            {
                switch (Btn.Name)
                {
                    case "SearchInDefaultEngine":
                        {
                            ApplicationData.Current.LocalSettings.Values["SearchEngineChoice"] = "Default";
                            break;
                        }
                    case "SearchInEverythingEngine":
                        {
                            ApplicationData.Current.LocalSettings.Values["SearchEngineChoice"] = "Everything";
                            break;
                        }
                }
            }
        }

        private void SearchEngineFlyout_Opening(object sender, object e)
        {
            if (ApplicationData.Current.LocalSettings.Values.TryGetValue("SearchEngineChoice", out object Choice))
            {
                if (Convert.ToString(Choice) == "Default")
                {
                    SearchInDefaultEngine.IsChecked = true;
                    SearchInEverythingEngine.IsChecked = false;
                }
                else
                {
                    if (SearchInEverythingEngine.IsEnabled)
                    {
                        SearchInEverythingEngine.IsChecked = true;
                        SearchInDefaultEngine.IsChecked = false;
                    }
                    else
                    {
                        SearchInDefaultEngine.IsChecked = true;
                        SearchInEverythingEngine.IsChecked = false;
                    }
                }
            }
            else
            {
                SearchInDefaultEngine.IsChecked = true;
                SearchInEverythingEngine.IsChecked = false;
            }

            if (ApplicationData.Current.LocalSettings.Values.TryGetValue("EverythingEngineIgnoreCase", out object EverythingIgnoreCase))
            {
                EverythingEngineIgnoreCase.IsChecked = Convert.ToBoolean(EverythingIgnoreCase);
            }
            else
            {
                EverythingEngineIgnoreCase.IsChecked = true;
            }

            if (ApplicationData.Current.LocalSettings.Values.TryGetValue("EverythingEngineIncludeRegex", out object EverythingIncludeRegex))
            {
                EverythingEngineIncludeRegex.IsChecked = Convert.ToBoolean(EverythingIncludeRegex);
            }
            else
            {
                EverythingEngineIncludeRegex.IsChecked = false;
            }

            if (ApplicationData.Current.LocalSettings.Values.TryGetValue("EverythingEngineSearchGloble", out object EverythingSearchGloble))
            {
                EverythingEngineSearchGloble.IsChecked = Convert.ToBoolean(EverythingSearchGloble);
            }
            else
            {
                EverythingEngineSearchGloble.IsChecked = false;
            }

            if (ApplicationData.Current.LocalSettings.Values.TryGetValue("BuiltInEngineIgnoreCase", out object BuiltInIgnoreCase))
            {
                BuiltInEngineIgnoreCase.IsChecked = Convert.ToBoolean(BuiltInIgnoreCase);
            }
            else
            {
                BuiltInEngineIgnoreCase.IsChecked = true;
            }

            if (ApplicationData.Current.LocalSettings.Values.TryGetValue("BuiltInEngineIncludeRegex", out object BuiltInIncludeRegex))
            {
                BuiltInEngineIncludeRegex.IsChecked = Convert.ToBoolean(BuiltInIncludeRegex);
            }
            else
            {
                BuiltInEngineIncludeRegex.IsChecked = false;
            }

            if (ApplicationData.Current.LocalSettings.Values.TryGetValue("BuiltInSearchAllSubFolders", out object BuiltInSearchSubFolders))
            {
                BuiltInSearchAllSubFolders.IsChecked = Convert.ToBoolean(BuiltInSearchSubFolders);
            }
            else
            {
                BuiltInSearchAllSubFolders.IsChecked = false;
            }
        }
    }
}
