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

        private volatile StorageFolder currentFolder;

        private SemaphoreSlim EnterLock;

        private StorageAreaWatcher AreaWatcher;

        public StorageFolder CurrentFolder
        {
            get
            {
                return currentFolder;
            }
            set
            {
                if (value != null)
                {
                    AreaWatcher.StartWatchDirectory(value.Path, SettingControl.IsDisplayHiddenItem);

                    UpdateAddressButton(value.Path);

                    string PlaceText = value.DisplayName.Length > 15 ? $"{value.DisplayName.Substring(0, 15)}..." : value.DisplayName;

                    GlobeSearch.PlaceholderText = $"{Globalization.GetString("SearchBox_PlaceholderText")} {PlaceText}";
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

            if (ApplicationData.Current.LocalSettings.Values["FilePresenterDisplayMode"] is int Index)
            {
                ItemDisplayMode.SelectedIndex = Index;
            }
            else
            {
                ApplicationData.Current.LocalSettings.Values["FilePresenterDisplayMode"] = 1;
                ItemDisplayMode.SelectedIndex = 1;
            }

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

        private void Current_Suspending(object sender, Windows.ApplicationModel.SuspendingEventArgs e)
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

                    if (CurrentFolder == null)
                    {
                        string RootPath = System.IO.Path.GetPathRoot(Path);

                        StorageFolder DriveRootFolder = await StorageFolder.GetFolderFromPathAsync(RootPath);
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

                            string RootPath = System.IO.Path.GetPathRoot(Path);

                            StorageFolder DriveRootFolder = await StorageFolder.GetFolderFromPathAsync(RootPath);
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

                    while (!AddressButtonContainer.IsLoaded)
                    {
                        await Task.Delay(500).ConfigureAwait(true);
                    }

                    ScrollViewer Viewer = AddressButtonContainer.FindChildOfType<ScrollViewer>();

                    if (Viewer.ActualWidth < Viewer.ExtentWidth)
                    {
                        Viewer.ChangeView(Viewer.ExtentWidth, null, null);
                    }

                    _ = Interlocked.Exchange(ref AddressButtonLockResource, 0);
                }
            }
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            if (Interlocked.Exchange(ref NavigateLockResource, 1) == 0)
            {
                try
                {
                    if (e.NavigationMode == NavigationMode.New && e?.Parameter is Tuple<WeakReference<TabViewItem>, StorageFolder> Parameters)
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
                _ => string.IsNullOrEmpty(CurrentFolder?.DisplayName) ? $"<{Globalization.GetString("UnknownText")}>" : CurrentFolder?.DisplayName,
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
        public async Task Initialize(StorageFolder InitFolder)
        {
            if (InitFolder != null)
            {
                FolderTree.RootNodes.Clear();

                string PathRoot = Path.GetPathRoot(InitFolder.Path);

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
                    ? (new Task[] { FillTreeNode(RootNode), DisplayItemsInFolder(InitFolder, true) })
                    : (new Task[] { DisplayItemsInFolder(InitFolder, true) });

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
                    List<string> StorageItemPath = WIN_Native_API.GetStorageItemsPath(Content.Path, SettingControl.IsDisplayHiddenItem, ItemFilters.Folder);

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
                if (WIN_Native_API.CheckIfHidden((Node.Content as TreeViewNodeContent).Path))
                {
                    QueueContentDialog Dialog = new QueueContentDialog
                    {
                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                        Content = Globalization.GetString("QueueDialog_ItemHidden_Content"),
                        CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                    };

                    _ = await Dialog.ShowAsync().ConfigureAwait(false);
                }
                else
                {
                    await DisplayItemsInFolder(Node).ConfigureAwait(false);
                }
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
                        if (Path.GetDirectoryName(FolderPath) == GoAndBackRecord[GoAndBackRecord.Count - 1].Item1)
                        {
                            GoAndBackRecord[GoAndBackRecord.Count - 1] = (GoAndBackRecord[GoAndBackRecord.Count - 1].Item1, FolderPath);
                        }
                        else
                        {
                            GoAndBackRecord[GoAndBackRecord.Count - 1] = (GoAndBackRecord[GoAndBackRecord.Count - 1].Item1, Presenter.SelectedItems.Count > 1 ? string.Empty : (Presenter.SelectedItem?.Path ?? string.Empty));
                        }
                    }

                    GoAndBackRecord.Add((FolderPath, string.Empty));

                    RecordIndex = GoAndBackRecord.Count - 1;
                }

                CurrentFolder = await StorageFolder.GetFolderFromPathAsync(FolderPath);

                Presenter.FileCollection.Clear();

                List<FileSystemStorageItemBase> ItemList = SortCollectionGenerator.Current.GetSortedCollection(WIN_Native_API.GetStorageItems(FolderPath, SettingControl.IsDisplayHiddenItem, ItemFilters.File | ItemFilters.Folder));

                Presenter.HasFile.Visibility = ItemList.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
                Presenter.StatusTips.Text = Globalization.GetString("FilePresenterBottomStatusTip_TotalItem").Replace("{ItemNum}", ItemList.Count.ToString());

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

        public Task DisplayItemsInFolder(StorageFolder Folder, bool ForceRefresh = false)
        {
            if (Folder == null)
            {
                throw new ArgumentNullException(nameof(Folder), "Parameter could not be null or empty");
            }

            return DisplayItemsInFolderCore(Folder.Path, ForceRefresh);
        }

        private async void FolderDelete_Click(object sender, RoutedEventArgs e)
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

            if (Window.Current.CoreWindow.GetKeyState(VirtualKey.Shift).HasFlag(CoreVirtualKeyStates.Down))
            {
                await LoadingActivation(true, Globalization.GetString("Progress_Tip_Deleting")).ConfigureAwait(true);

            Retry:
                try
                {
                    await FullTrustProcessController.Current.DeleteAsync(CurrentFolder, true).ConfigureAwait(true);

                    if (await CurrentFolder.GetParentAsync() is StorageFolder ParentFolder)
                    {
                        await DisplayItemsInFolder(ParentFolder).ConfigureAwait(true);
                    }

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

                    if (await CurrentFolder.GetParentAsync() is StorageFolder ParentFolder)
                    {
                        await DisplayItemsInFolder(ParentFolder).ConfigureAwait(true);
                    }
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
                        await FullTrustProcessController.Current.DeleteAsync(CurrentFolder, QueueContenDialog.IsPermanentDelete).ConfigureAwait(true);

                        if (await CurrentFolder.GetParentAsync() is StorageFolder ParentFolder)
                        {
                            await DisplayItemsInFolder(ParentFolder).ConfigureAwait(true);
                        }

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

                        if (await CurrentFolder.GetParentAsync() is StorageFolder ParentFolder)
                        {
                            await DisplayItemsInFolder(ParentFolder).ConfigureAwait(true);
                        }
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
                    if (WIN_Native_API.CheckIfHidden((Node.Content as TreeViewNodeContent).Path))
                    {
                        FolderTree.ContextFlyout = null;
                    }
                    else
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
                }
                else
                {
                    FolderTree.ContextFlyout = null;
                }
            }
        }

        private async void FolderRename_Click(object sender, RoutedEventArgs e)
        {
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
                try
                {
                    await CurrentFolder.RenameAsync(dialog.DesireName);

                    (FolderTree.SelectedNode.Content as TreeViewNodeContent).Update(CurrentFolder);

                    UpdateAddressButton(CurrentFolder.Path);
                }
                catch (UnauthorizedAccessException)
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
                        _ = await Launcher.LaunchFolderAsync(CurrentFolder);
                    }
                }
                catch (FileLoadException)
                {
                    QueueContentDialog Dialog = new QueueContentDialog
                    {
                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                        Content = Globalization.GetString("QueueDialog_FolderOccupied_Content"),
                        CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton"),
                    };

                    _ = await Dialog.ShowAsync().ConfigureAwait(true);
                }
                catch
                {
                    QueueContentDialog Dialog = new QueueContentDialog
                    {
                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                        Content = Globalization.GetString("QueueDialog_RenameExist_Content"),
                        PrimaryButtonText = Globalization.GetString("Common_Dialog_ContinueButton"),
                        CloseButtonText = Globalization.GetString("Common_Dialog_CancelButton")
                    };

                    if (await Dialog.ShowAsync().ConfigureAwait(true) == ContentDialogResult.Primary)
                    {
                        await CurrentFolder.RenameAsync(dialog.DesireName, NameCollisionOption.GenerateUniqueName);

                        (FolderTree.SelectedNode.Content as TreeViewNodeContent).Update(CurrentFolder);

                        UpdateAddressButton(CurrentFolder.Path);
                    }
                }
            }
        }

        private async void CreateFolder_Click(object sender, RoutedEventArgs e)
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

            try
            {
                _ = await CurrentFolder.CreateFolderAsync(Globalization.GetString("Create_NewFolder_Admin_Name"), CreationCollisionOption.GenerateUniqueName);
            }
            catch (UnauthorizedAccessException)
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
                    _ = await Launcher.LaunchFolderAsync(CurrentFolder);
                }
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

            if (FolderTree.RootNodes.Any((Node) => (Node.Content as TreeViewNodeContent).Path == CurrentFolder.Path))
            {
                if (CommonAccessCollection.HardDeviceList.FirstOrDefault((Device) => Device.Name == CurrentFolder.DisplayName) is HardDeviceInfo Info)
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

            if (Package.Current.Id.Architecture == ProcessorArchitecture.X64 || Package.Current.Id.Architecture == ProcessorArchitecture.X86 || Package.Current.Id.Architecture == ProcessorArchitecture.X86OnArm64)
            {
                SearchInEverythingEngine.IsEnabled = await FullTrustProcessController.Current.CheckIfEverythingIsAvailableAsync().ConfigureAwait(true);
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
                if (WIN_Native_API.GetStorageItem(ProtentialPath1) is FileSystemStorageItemBase Item)
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
                if (WIN_Native_API.GetStorageItem(ProtentialPath2) is FileSystemStorageItemBase Item)
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
                if (WIN_Native_API.GetStorageItem(ProtentialPath3) is FileSystemStorageItemBase Item)
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


                if (Path.IsPathRooted(QueryText) && CommonAccessCollection.HardDeviceList.FirstOrDefault((Drive) => Drive.Folder.Path == Path.GetPathRoot(QueryText)) is HardDeviceInfo Device)
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

                    if (WIN_Native_API.GetStorageItem(QueryText) is FileSystemStorageItemBase Item)
                    {
                        if (Item.StorageType == StorageItemTypes.File)
                        {
                            StorageFile File = (await Item.GetStorageItem().ConfigureAwait(true)) as StorageFile;

                            if (!await Launcher.LaunchFileAsync(File))
                            {
                                LauncherOptions options = new LauncherOptions
                                {
                                    DisplayApplicationPicker = true
                                };

                                _ = await Launcher.LaunchFileAsync(File, options);
                            }
                        }
                        else
                        {
                            StorageFolder Folder = (await Item.GetStorageItem().ConfigureAwait(true)) as StorageFolder;

                            if (Path.GetPathRoot(QueryText) != Path.GetPathRoot(CurrentFolder.Path))
                            {
                                await Initialize(Folder).ConfigureAwait(true);
                            }
                            else
                            {
                                await DisplayItemsInFolder(Folder).ConfigureAwait(true);
                            }

                            await SQLite.Current.SetPathHistoryAsync(Folder.Path).ConfigureAwait(true);

                            await JumpListController.Current.AddItem(JumpListGroup.Recent, Folder).ConfigureAwait(true);
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

        private void AddressBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            AddressBoxTextBackup = sender.Text;

            if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
            {
                if (Path.IsPathRooted(sender.Text) && CommonAccessCollection.HardDeviceList.Any((Drive) => Drive.Folder.Path == Path.GetPathRoot(sender.Text)))
                {
                    if (Interlocked.Exchange(ref TextChangeLockResource, 1) == 0)
                    {
                        try
                        {
                            if (args.CheckCurrent())
                            {
                                string DirectoryPath = Path.GetPathRoot(sender.Text) == sender.Text ? sender.Text : Path.GetDirectoryName(sender.Text);
                                string FileName = Path.GetFileName(sender.Text);

                                if (string.IsNullOrEmpty(FileName))
                                {
                                    sender.ItemsSource = WIN_Native_API.GetStorageItems(DirectoryPath, false, ItemFilters.Folder).Take(20).Select((It) => It.Path);
                                }
                                else
                                {
                                    sender.ItemsSource = WIN_Native_API.GetStorageItems(DirectoryPath, false, ItemFilters.Folder).Where((Item) => Item.Name.StartsWith(FileName, StringComparison.OrdinalIgnoreCase)).Take(20).Select((It) => It.Path);
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
                            if (WIN_Native_API.CheckIfHidden(DirectoryPath))
                            {
                                QueueContentDialog Dialog = new QueueContentDialog
                                {
                                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                    Content = Globalization.GetString("QueueDialog_ItemHidden_Content"),
                                    CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                                };

                                _ = await Dialog.ShowAsync().ConfigureAwait(false);

                                return;
                            }

                            if ((await CurrentFolder.GetParentAsync()) is StorageFolder ParentFolder)
                            {
                                await DisplayItemsInFolder(ParentFolder).ConfigureAwait(true);
                            }

                            if (Presenter.FileCollection.Where((Item) => Item.StorageType == StorageItemTypes.Folder).FirstOrDefault((Item) => Item.Path == CurrentFolderPath) is FileSystemStorageItemBase Folder)
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

                        if (WIN_Native_API.CheckIfHidden(Path))
                        {
                            QueueContentDialog Dialog = new QueueContentDialog
                            {
                                Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                Content = Globalization.GetString("QueueDialog_ItemHidden_Content"),
                                CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                            };

                            _ = await Dialog.ShowAsync().ConfigureAwait(false);

                            _ = Interlocked.Exchange(ref NavigateLockResource, 0);

                            RecordIndex++;
                            return;
                        }

                        StorageFolder Folder = await StorageFolder.GetFolderFromPathAsync(Path);

                        IsBackOrForwardAction = true;

                        await DisplayItemsInFolder(Folder).ConfigureAwait(true);

                        await SQLite.Current.SetPathHistoryAsync(Folder.Path).ConfigureAwait(true);

                        if (!string.IsNullOrEmpty(SelectedPath) && Presenter.FileCollection.FirstOrDefault((Item) => Item.Path == SelectedPath) is FileSystemStorageItemBase Item)
                        {
                            Presenter.SelectedItem = Item;
                            Presenter.ItemPresenter.ScrollIntoView(Folder, ScrollIntoViewAlignment.Leading);
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

                    RecordIndex++;
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

                        if (WIN_Native_API.CheckIfHidden(Path))
                        {
                            QueueContentDialog Dialog = new QueueContentDialog
                            {
                                Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                Content = Globalization.GetString("QueueDialog_ItemHidden_Content"),
                                CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                            };

                            _ = await Dialog.ShowAsync().ConfigureAwait(false);

                            _ = Interlocked.Exchange(ref NavigateLockResource, 0);

                            RecordIndex--;
                            return;
                        }

                        StorageFolder Folder = await StorageFolder.GetFolderFromPathAsync(Path);

                        IsBackOrForwardAction = true;

                        await DisplayItemsInFolder(Folder).ConfigureAwait(true);

                        await SQLite.Current.SetPathHistoryAsync(Folder.Path).ConfigureAwait(true);

                        if (!string.IsNullOrEmpty(SelectedPath) && Presenter.FileCollection.FirstOrDefault((Item) => Item.Path == SelectedPath) is FileSystemStorageItemBase Item)
                        {
                            Presenter.SelectedItem = Item;
                            Presenter.ItemPresenter.ScrollIntoView(Folder, ScrollIntoViewAlignment.Leading);
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

            AddressBox.ItemsSource = await SQLite.Current.GetRelatedPathHistoryAsync().ConfigureAwait(true);
        }

        private async void ItemDisplayMode_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ApplicationData.Current.LocalSettings.Values["FilePresenterDisplayMode"] = ItemDisplayMode.SelectedIndex;

            switch (ItemDisplayMode.SelectedIndex)
            {
                case 0:
                    {
                        Presenter.ItemPresenter = (Presenter.FindName("GridViewRefreshContainer") as RefreshContainer)?.Content as ListViewBase;

                        Presenter.GridViewControl.ItemTemplate = Presenter.GridViewTileDataTemplate;
                        Presenter.GridViewControl.ItemsPanel = Presenter.HorizontalGridViewPanel;

                        if (Presenter.GridViewControl.FindChildOfType<ScrollViewer>() is ScrollViewer Scroll)
                        {
                            Scroll.HorizontalScrollMode = ScrollMode.Disabled;
                            Scroll.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
                            Scroll.VerticalScrollMode = ScrollMode.Auto;
                            Scroll.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
                        }
                        else
                        {
                            await Task.Delay(300).ConfigureAwait(true);
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

                        if (Presenter.GridViewControl.FindChildOfType<ScrollViewer>() is ScrollViewer Scroll)
                        {
                            Scroll.HorizontalScrollMode = ScrollMode.Disabled;
                            Scroll.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
                            Scroll.VerticalScrollMode = ScrollMode.Auto;
                            Scroll.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
                        }
                        else
                        {
                            await Task.Delay(300).ConfigureAwait(true);
                        }

                        break;
                    }
                case 4:
                    {
                        Presenter.ItemPresenter = (Presenter.FindName("GridViewRefreshContainer") as RefreshContainer)?.Content as ListViewBase;

                        Presenter.GridViewControl.ItemTemplate = Presenter.GridViewMediumImageDataTemplate;
                        Presenter.GridViewControl.ItemsPanel = Presenter.HorizontalGridViewPanel;

                        if (Presenter.GridViewControl.FindChildOfType<ScrollViewer>() is ScrollViewer Scroll)
                        {
                            Scroll.HorizontalScrollMode = ScrollMode.Disabled;
                            Scroll.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
                            Scroll.VerticalScrollMode = ScrollMode.Auto;
                            Scroll.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
                        }
                        else
                        {
                            await Task.Delay(300).ConfigureAwait(true);
                        }

                        break;
                    }
                case 5:
                    {
                        Presenter.ItemPresenter = (Presenter.FindName("GridViewRefreshContainer") as RefreshContainer)?.Content as ListViewBase;

                        Presenter.GridViewControl.ItemTemplate = Presenter.GridViewSmallImageDataTemplate;
                        Presenter.GridViewControl.ItemsPanel = Presenter.HorizontalGridViewPanel;

                        if (Presenter.GridViewControl.FindChildOfType<ScrollViewer>() is ScrollViewer Scroll)
                        {
                            Scroll.HorizontalScrollMode = ScrollMode.Disabled;
                            Scroll.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
                            Scroll.VerticalScrollMode = ScrollMode.Auto;
                            Scroll.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
                        }
                        else
                        {
                            await Task.Delay(300).ConfigureAwait(true);
                        }

                        break;
                    }
            }
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

            if (WIN_Native_API.CheckIfHidden(ActualString))
            {
                QueueContentDialog Dialog = new QueueContentDialog
                {
                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                    Content = Globalization.GetString("QueueDialog_ItemHidden_Content"),
                    CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                };

                _ = await Dialog.ShowAsync().ConfigureAwait(false);

                return;
            }

            try
            {
                StorageFolder TargetFolder = await StorageFolder.GetFolderFromPathAsync(ActualString);
                await DisplayItemsInFolder(TargetFolder).ConfigureAwait(true);
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

            List<string> ItemList = WIN_Native_API.GetStorageItemsPath(ActualString, SettingControl.IsDisplayHiddenItem, ItemFilters.Folder);

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

                if (WIN_Native_API.CheckIfHidden(TargetPath))
                {
                    QueueContentDialog Dialog = new QueueContentDialog
                    {
                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                        Content = Globalization.GetString("QueueDialog_ItemHidden_Content"),
                        CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                    };

                    _ = await Dialog.ShowAsync().ConfigureAwait(false);
                    return;
                }

                try
                {
                    StorageFolder TargetFolder = await StorageFolder.GetFolderFromPathAsync(TargetPath);

                    await DisplayItemsInFolder(TargetFolder).ConfigureAwait(true);
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

            bool IsHiddenTarget = false;

            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                IsHiddenTarget = WIN_Native_API.CheckIfHidden(ActualPath);
            });

            if (IsHiddenTarget)
            {
                QueueContentDialog Dialog = new QueueContentDialog
                {
                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                    Content = Globalization.GetString("QueueDialog_ItemHidden_Content"),
                    CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                };

                _ = await Dialog.ShowAsync().ConfigureAwait(false);
                return;
            }

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
                            List<string> LinkItemsPath = BodyNode.Select((Node) => Node.InnerText).ToList();

                            StorageFolder TargetFolder = await StorageFolder.GetFolderFromPathAsync(ActualPath);

                            switch (e.AcceptedOperation)
                            {
                                case DataPackageOperation.Copy:
                                    {
                                        await LoadingActivation(true, Globalization.GetString("Progress_Tip_Copying")).ConfigureAwait(true);

                                    Retry:
                                        try
                                        {
                                            await FullTrustProcessController.Current.CopyAsync(LinkItemsPath, TargetFolder.Path, (s, arg) =>
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
                                        if (LinkItemsPath.All((Item) => Path.GetDirectoryName(Item) == ActualPath))
                                        {
                                            return;
                                        }

                                        await LoadingActivation(true, Globalization.GetString("Progress_Tip_Moving")).ConfigureAwait(true);

                                    Retry:
                                        try
                                        {
                                            await FullTrustProcessController.Current.MoveAsync(LinkItemsPath, TargetFolder.Path, (s, arg) =>
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

                        StorageFolder TargetFolder = await StorageFolder.GetFolderFromPathAsync(ActualPath);

                        switch (e.AcceptedOperation)
                        {
                            case DataPackageOperation.Copy:
                                {
                                    await LoadingActivation(true, Globalization.GetString("Progress_Tip_Copying")).ConfigureAwait(true);

                                Retry:
                                    try
                                    {
                                        await FullTrustProcessController.Current.CopyAsync(DragItemList, TargetFolder, (s, arg) =>
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
                                        await FullTrustProcessController.Current.MoveAsync(DragItemList, TargetFolder, (s, arg) =>
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

        private void AddressButton_DragOver(object sender, DragEventArgs e)
        {
            if (e.DataView.Contains(StandardDataFormats.StorageItems) || e.DataView.Contains(StandardDataFormats.Html))
            {
                if (e.Modifiers.HasFlag(DragDropModifiers.Control))
                {
                    e.AcceptedOperation = DataPackageOperation.Copy;
                    e.DragUIOverride.Caption = $"{Globalization.GetString("Drag_Tip_CopyTo")} {(e.OriginalSource as Button).Content}";
                }
                else
                {
                    e.AcceptedOperation = DataPackageOperation.Move;
                    e.DragUIOverride.Caption = $"{Globalization.GetString("Drag_Tip_MoveTo")} {(e.OriginalSource as Button).Content}";
                }

                e.DragUIOverride.IsContentVisible = true;
                e.DragUIOverride.IsCaptionVisible = true;
            }
            else
            {
                e.AcceptedOperation = DataPackageOperation.None;
            }
        }

        private async void FolderTree_Holding(object sender, Windows.UI.Xaml.Input.HoldingRoutedEventArgs e)
        {
            if (e.HoldingState == Windows.UI.Input.HoldingState.Started)
            {
                if ((e.OriginalSource as FrameworkElement)?.DataContext is TreeViewNode Node)
                {
                    if (WIN_Native_API.CheckIfHidden((Node.Content as TreeViewNodeContent).Path))
                    {
                        FolderTree.ContextFlyout = null;
                    }
                    else
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

            if (Presenter.ListViewControl?.Header is SortIndicatorController Instance)
            {
                SortIndicatorController.RemoveInstance(Instance);
            }
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
            try
            {
                if (CurrentFolder != null)
                {
                    Clipboard.Clear();

                    DataPackage Package = new DataPackage
                    {
                        RequestedOperation = DataPackageOperation.Move
                    };

                    Package.SetStorageItems(new IStorageItem[] { CurrentFolder }, false);

                    Clipboard.SetContent(Package);
                }
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

        private async void FolderCopy_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (CurrentFolder != null)
                {
                    Clipboard.Clear();

                    DataPackage Package = new DataPackage
                    {
                        RequestedOperation = DataPackageOperation.Copy
                    };

                    Package.SetStorageItems(new IStorageItem[] { CurrentFolder }, false);

                    Clipboard.SetContent(Package);
                }
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

        private async void OpenFolderInNewWindow_Click(object sender, RoutedEventArgs e)
        {
            if (CurrentFolder != null)
            {
                await Launcher.LaunchUriAsync(new Uri($"rx-explorer:{Uri.EscapeDataString(CurrentFolder.Path)}"));
            }
        }

        private async void OpenFolderInNewTab_Click(object sender, RoutedEventArgs e)
        {
            if (CurrentFolder != null)
            {
                await TabViewContainer.ThisPage.CreateNewTabAndOpenTargetFolder(CurrentFolder.Path).ConfigureAwait(false);
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
            _ = SearchEngineConfirm.Focus(FocusState.Programmatic);
        }

        private void SearchEngineFlyout_Closed(object sender, object e)
        {
            MainPage.ThisPage.IsAnyTaskRunning = false;
        }
    }
}
