using FileManager.Class;
using FileManager.Dialog;
using Microsoft.Toolkit.Uwp.UI.Animations;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.ApplicationModel.DataTransfer.DragDrop;
using Windows.Foundation;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.Storage.Search;
using Windows.System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;
using TreeView = Microsoft.UI.Xaml.Controls.TreeView;
using TreeViewCollapsedEventArgs = Microsoft.UI.Xaml.Controls.TreeViewCollapsedEventArgs;
using TreeViewExpandingEventArgs = Microsoft.UI.Xaml.Controls.TreeViewExpandingEventArgs;
using TreeViewItemInvokedEventArgs = Microsoft.UI.Xaml.Controls.TreeViewItemInvokedEventArgs;
using TreeViewNode = Microsoft.UI.Xaml.Controls.TreeViewNode;

namespace FileManager
{
    public sealed partial class FileControl : Page
    {
        private TreeViewNode currentnode;

        public TreeViewNode CurrentNode
        {
            get
            {
                return currentnode;
            }
            set
            {
                if (value != null && value.Content is StorageFolder Folder)
                {
                    UpdateAddressButton(Folder);

                    string PlaceText;
                    if (Folder.DisplayName.Length > 22)
                    {
                        PlaceText = Folder.DisplayName.Substring(0, 22) + "...";
                    }
                    else
                    {
                        PlaceText = Folder.DisplayName;
                    }

                    GlobeSearch.PlaceholderText = Globalization.Language == LanguageEnum.Chinese
                         ? "搜索 " + PlaceText
                         : "Search " + PlaceText;

                    GoParentFolder.IsEnabled = value != FolderTree.RootNodes[0];
                    GoBackRecord.IsEnabled = RecordIndex > 0;
                    GoForwardRecord.IsEnabled = RecordIndex < GoAndBackRecord.Count - 1;

                    if (TabItem != null)
                    {
                        TabItem.Header = Folder.DisplayName;
                    }
                }

                currentnode = value;
            }
        }

        private int TextChangeLockResource = 0;

        private int AddressButtonLockResource = 0;

        private int NavigateLockResource = 0;

        private int DropLockResource = 0;

        private string AddressBoxTextBackup;

        private StorageFolder currentFolder;

        public StorageFolder CurrentFolder
        {
            get
            {
                if (SettingControl.IsDetachTreeViewAndPresenter)
                {
                    return currentFolder ?? (CurrentNode?.Content as StorageFolder);
                }
                else
                {
                    return CurrentNode?.Content as StorageFolder;
                }
            }
            set
            {
                if (SettingControl.IsDetachTreeViewAndPresenter)
                {
                    if (value != null)
                    {
                        UpdateAddressButton(value);

                        string PlaceText;
                        if (value.DisplayName.Length > 22)
                        {
                            PlaceText = value.DisplayName.Substring(0, 22) + "...";
                        }
                        else
                        {
                            PlaceText = value.DisplayName;
                        }

                        GlobeSearch.PlaceholderText = Globalization.Language == LanguageEnum.Chinese
                             ? "搜索 " + PlaceText
                             : "Search " + PlaceText;

                        GoParentFolder.IsEnabled = value.Path != Path.GetPathRoot(value.Path);
                        GoBackRecord.IsEnabled = RecordIndex > 0;
                        GoForwardRecord.IsEnabled = RecordIndex < GoAndBackRecord.Count - 1;

                        if (TabItem != null)
                        {
                            TabItem.Header = value.DisplayName;
                        }
                    }
                }

                currentFolder = value;
            }
        }

        public bool IsSearchOrPathBoxFocused { get; set; } = false;

        private bool IsAdding = false;
        private CancellationTokenSource CancelToken;
        private CancellationTokenSource FolderExpandCancel;
        private ManualResetEvent ExitLocker;
        private AutoResetEvent Locker;
        private List<StorageFolder> GoAndBackRecord = new List<StorageFolder>();
        private ObservableCollection<AddressBlock> AddressButtonList = new ObservableCollection<AddressBlock>();
        private ObservableCollection<string> AddressExtentionList = new ObservableCollection<string>();
        private int RecordIndex = 0;
        private bool IsBackOrForwardAction = false;

        private Microsoft.UI.Xaml.Controls.TabViewItem TabItem;

        public FileControl()
        {
            InitializeComponent();
            try
            {
                Nav.Navigate(typeof(FilePresenter), new Tuple<FileControl, Frame>(this, Nav), new DrillInNavigationTransitionInfo());

                if (Globalization.Language == LanguageEnum.Chinese)
                {
                    ItemDisplayMode.Items.Add("平铺");
                    ItemDisplayMode.Items.Add("详细信息");
                    ItemDisplayMode.Items.Add("列表");
                    ItemDisplayMode.Items.Add("大图标");
                    ItemDisplayMode.Items.Add("中图标");
                    ItemDisplayMode.Items.Add("小图标");
                }
                else
                {
                    ItemDisplayMode.Items.Add("Tiles");
                    ItemDisplayMode.Items.Add("Details");
                    ItemDisplayMode.Items.Add("List");
                    ItemDisplayMode.Items.Add("Large icons");
                    ItemDisplayMode.Items.Add("Medium icons");
                    ItemDisplayMode.Items.Add("Small icons");
                }

                if (ApplicationData.Current.LocalSettings.Values["FilePresenterDisplayMode"] is int Index)
                {
                    ItemDisplayMode.SelectedIndex = Index;
                }
                else
                {
                    ApplicationData.Current.LocalSettings.Values["FilePresenterDisplayMode"] = 0;
                    ItemDisplayMode.SelectedIndex = 0;
                }
            }
            catch (Exception ex)
            {
                ExceptionTracer.RequestBlueScreen(ex);
            }
        }

        private async void UpdateAddressButton(StorageFolder Folder)
        {
            if (Interlocked.Exchange(ref AddressButtonLockResource, 1) == 0)
            {
                try
                {
                    if (string.IsNullOrEmpty(Folder.Path))
                    {
                        return;
                    }

                    if (CurrentFolder == null)
                    {
                        string RootPath = Path.GetPathRoot(Folder.Path);

                        StorageFolder DriveRootFolder = await StorageFolder.GetFolderFromPathAsync(RootPath);
                        AddressButtonList.Add(new AddressBlock(DriveRootFolder.DisplayName));

                        PathAnalysis Analysis = new PathAnalysis(Folder.Path, RootPath);

                        while (Analysis.HasNextLevel)
                        {
                            AddressButtonList.Add(new AddressBlock(Analysis.NextRelativePath()));
                        }
                    }
                    else
                    {
                        string OriginalString = string.Join("\\", AddressButtonList.Skip(1));
                        string ActualString = Path.Combine(Path.GetPathRoot(CurrentFolder.Path), OriginalString);

                        List<string> IntersectList = new List<string>();
                        string[] FolderSplit = Folder.Path.Split('\\', StringSplitOptions.RemoveEmptyEntries);
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

                            string RootPath = Path.GetPathRoot(Folder.Path);

                            StorageFolder DriveRootFolder = await StorageFolder.GetFolderFromPathAsync(RootPath);
                            AddressButtonList.Add(new AddressBlock(DriveRootFolder.DisplayName));

                            PathAnalysis Analysis = new PathAnalysis(Folder.Path, RootPath);

                            while (Analysis.HasNextLevel)
                            {
                                AddressButtonList.Add(new AddressBlock(Analysis.NextRelativePath()));
                            }
                        }
                        else
                        {
                            for (int i = AddressButtonList.Count - 1; i >= IntersectList.Count; i--)
                            {
                                AddressButtonList.RemoveAt(i);
                            }

                            List<string> ExceptList = Folder.Path.Split('\\', StringSplitOptions.RemoveEmptyEntries).ToList();

                            ExceptList.RemoveRange(0, IntersectList.Count);

                            foreach (string SubPath in ExceptList)
                            {
                                AddressButtonList.Add(new AddressBlock(SubPath));
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    ExceptionTracer.RequestBlueScreen(ex);
                }
                finally
                {
                    AddressButtonScrollViewer.UpdateLayout();

                    if (AddressButtonScrollViewer.ActualWidth < AddressButtonScrollViewer.ExtentWidth)
                    {
                        AddressButtonScrollViewer.ChangeView(AddressButtonScrollViewer.ExtentWidth, null, null);
                    }

                    _ = Interlocked.Exchange(ref AddressButtonLockResource, 0);
                }
            }
        }

        private async Task OpenTargetFolder(StorageFolder Folder)
        {
            TabViewContainer.ThisPage.FFInstanceContainer[this].FileCollection.Clear();
            TabViewContainer.ThisPage.FFInstanceContainer[this].HasFile.Visibility = Visibility.Collapsed;

            FolderTree.RootNodes.Clear();
            StorageFolder RootFolder = await StorageFolder.GetFolderFromPathAsync(Path.GetPathRoot(Folder.Path));
            uint ItemCount = await RootFolder.CreateFolderQuery(CommonFolderQuery.DefaultQuery).GetItemCountAsync();
            TreeViewNode RootNode = new TreeViewNode
            {
                Content = RootFolder,
                IsExpanded = ItemCount != 0,
                HasUnrealizedChildren = ItemCount != 0
            };
            FolderTree.RootNodes.Add(RootNode);

            await FillTreeNode(RootNode).ConfigureAwait(true);

            TreeViewNode TargetNode = await RootNode.FindFolderLocationInTree(new PathAnalysis(Folder.Path, string.Empty)).ConfigureAwait(true);
            if (TargetNode == null)
            {
                if (Globalization.Language == LanguageEnum.Chinese)
                {
                    QueueContentDialog dialog = new QueueContentDialog
                    {
                        Title = "错误",
                        Content = "无法定位文件夹，该文件夹可能已被删除或移动",
                        CloseButtonText = "确定"
                    };
                    _ = await dialog.ShowAsync().ConfigureAwait(true);
                }
                else
                {
                    QueueContentDialog dialog = new QueueContentDialog
                    {
                        Title = "Error",
                        Content = "Unable to locate folder, which may have been deleted or moved",
                        CloseButtonText = "Confirm"
                    };
                    _ = await dialog.ShowAsync().ConfigureAwait(true);
                }
            }
            else
            {
                await DisplayItemsInFolder(TargetNode).ConfigureAwait(false);
            }
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            if (e.Parameter is Tuple<Microsoft.UI.Xaml.Controls.TabViewItem, StorageFolder, ThisPC> Parameters)
            {
                string PlaceText = Parameters.Item2.DisplayName.Length > 18 ? Parameters.Item2.DisplayName.Substring(0, 18) + "..." : Parameters.Item2.DisplayName;

                GlobeSearch.PlaceholderText = Globalization.Language == LanguageEnum.Chinese ? "搜索 " + PlaceText : "Search " + PlaceText;

                if (Parameters.Item1 != null)
                {
                    TabItem = Parameters.Item1;
                }

                if (Parameters.Item3 != null && !TabViewContainer.ThisPage.TFInstanceContainer.ContainsKey(Parameters.Item3))
                {
                    TabViewContainer.ThisPage.TFInstanceContainer.Add(Parameters.Item3, this);
                }

                await Initialize(Parameters.Item2).ConfigureAwait(false);
            }
        }

        protected async override void OnNavigatedFrom(NavigationEventArgs e)
        {
            while (Nav.CanGoBack)
            {
                Nav.GoBack();
            }

            Locker.Dispose();
            AddressButtonList.Clear();
            FolderExpandCancel.Cancel();

            await Task.Run(() =>
            {
                ExitLocker.WaitOne();
            }).ConfigureAwait(true);

            ExitLocker.Dispose();
            ExitLocker = null;

            FolderExpandCancel.Dispose();
            FolderExpandCancel = null;

            FolderTree.RootNodes.Clear();
            TabViewContainer.ThisPage.FFInstanceContainer[this].FileCollection.Clear();
            TabViewContainer.ThisPage.FFInstanceContainer[this].HasFile.Visibility = Visibility.Collapsed;

            RecordIndex = 0;
            GoAndBackRecord.Clear();
            IsBackOrForwardAction = false;
            GoBackRecord.IsEnabled = false;
            GoForwardRecord.IsEnabled = false;
            GoParentFolder.IsEnabled = false;

            CurrentNode = null;
            CurrentFolder = null;
        }

        /// <summary>
        /// 执行文件目录的初始化
        /// </summary>
        private async Task Initialize(StorageFolder InitFolder)
        {
            if (InitFolder != null)
            {
                CancelToken?.Dispose();
                CancelToken = new CancellationTokenSource();
                Locker = new AutoResetEvent(false);
                FolderExpandCancel = new CancellationTokenSource();
                ExitLocker = new ManualResetEvent(true);

                FolderTree.RootNodes.Clear();
                TreeViewNode RootNode = new TreeViewNode
                {
                    Content = InitFolder,
                    IsExpanded = false,
                    HasUnrealizedChildren = (await InitFolder.GetFoldersAsync(CommonFolderQuery.DefaultQuery, 0, 1)).Count != 0
                };
                FolderTree.RootNodes.Add(RootNode);

                if (SettingControl.IsDetachTreeViewAndPresenter)
                {
                    await DisplayItemsInFolder(InitFolder, true).ConfigureAwait(true);
                }
                else
                {
                    await DisplayItemsInFolder(RootNode, true).ConfigureAwait(true);
                }
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

            if (Node.HasUnrealizedChildren)
            {
                StorageFolder folder = Node.Content as StorageFolder;

                try
                {
                    ExitLocker.Reset();

                    QueryOptions Options = new QueryOptions(CommonFolderQuery.DefaultQuery)
                    {
                        FolderDepth = FolderDepth.Shallow,
                        IndexerOption = IndexerOption.UseIndexerWhenAvailable
                    };
                    Options.SetPropertyPrefetch(PropertyPrefetchOptions.BasicProperties, new string[] { "System.FolderNameDisplay" });

                    StorageFolderQueryResult FolderQuery = folder.CreateFolderQueryWithOptions(Options);

                    if (WIN_Native_API.CheckContainsAnyItem(folder.Path, ItemFilter.Folder))
                    {
                        for (uint i = 0; !FolderExpandCancel.IsCancellationRequested; i += 50)
                        {
                            IReadOnlyList<StorageFolder> StorageFolderList = await FolderQuery.GetFoldersAsync(i, 50).AsTask(FolderExpandCancel.Token).ConfigureAwait(true);

                            if (StorageFolderList.Count == 0)
                            {
                                break;
                            }

                            foreach (var SubFolder in StorageFolderList)
                            {
                                TreeViewNode NewNode = new TreeViewNode
                                {
                                    Content = SubFolder,
                                    HasUnrealizedChildren = WIN_Native_API.CheckContainsAnyItem(SubFolder.Path, ItemFilter.Folder)
                                };

                                Node.Children.Add(NewNode);
                            }
                        }
                        Node.HasUnrealizedChildren = false;
                    }
                }
                catch (TaskCanceledException)
                {
                    return;
                }
                catch (Exception ex)
                {
                    ExceptionTracer.RequestBlueScreen(ex);
                }
                finally
                {
                    ExitLocker.Set();
                }
            }
        }

        private async void FolderTree_Expanding(TreeView sender, TreeViewExpandingEventArgs args)
        {
            if (args.Node.HasUnrealizedChildren)
            {
                await FillTreeNode(args.Node).ConfigureAwait(false);
            }
        }

        private async void FolderTree_ItemInvoked(TreeView sender, TreeViewItemInvokedEventArgs args)
        {
            await DisplayItemsInFolder(args.InvokedItem as TreeViewNode).ConfigureAwait(false);
        }

        public async Task DisplayItemsInFolder(TreeViewNode Node, bool ForceRefresh = false)
        {
            if (Node == null)
            {
                throw new ArgumentNullException(nameof(Node), "Parameter could not be null");
            }

            FolderTree.SelectNode(Node);

            try
            {
                if (Node.Content is StorageFolder Folder)
                {
                    while (Nav.CurrentSourcePageType != typeof(FilePresenter))
                    {
                        Nav.GoBack();
                    }

                    if (!ForceRefresh)
                    {
                        if (Folder.Path == CurrentFolder?.Path)
                        {
                            IsAdding = false;
                            return;
                        }
                    }

                    if (IsAdding)
                    {
                        await Task.Run(() =>
                        {
                            lock (SyncRootProvider.SyncRoot)
                            {
                                CancelToken.Cancel();
                                Locker.WaitOne();
                                CancelToken.Dispose();
                                CancelToken = new CancellationTokenSource();
                            }
                        }).ConfigureAwait(true);
                    }
                    IsAdding = true;

                    if (IsBackOrForwardAction)
                    {
                        IsBackOrForwardAction = false;
                    }
                    else
                    {
                        if (RecordIndex != GoAndBackRecord.Count - 1 && GoAndBackRecord.Count != 0)
                        {
                            GoAndBackRecord.RemoveRange(RecordIndex + 1, GoAndBackRecord.Count - RecordIndex - 1);
                        }
                        GoAndBackRecord.Add(Folder);
                        RecordIndex = GoAndBackRecord.Count - 1;
                    }

                    CurrentNode = Node;

                    FilePresenter Presenter = TabViewContainer.ThisPage.FFInstanceContainer[this];

                    Presenter.FileCollection.Clear();

                    List<FileSystemStorageItem> ItemList = Presenter.SortList(WIN_Native_API.GetStorageItems(Folder, ItemFilter.File | ItemFilter.Folder), SortTarget.Name, SortDirection.Ascending);

                    Presenter.HasFile.Visibility = ItemList.Count == 0 ? Visibility.Visible : Visibility.Collapsed;


                    for (int i = 0; i < ItemList.Count && !CancelToken.IsCancellationRequested; i++)
                    {
                        TabViewContainer.ThisPage.FFInstanceContainer[this].FileCollection.Add(ItemList[i]);
                    }
                }

                if (CancelToken.IsCancellationRequested)
                {
                    Locker.Set();
                }
                else
                {
                    IsAdding = false;
                }
            }
            catch (Exception ex)
            {
                ExceptionTracer.RequestBlueScreen(ex);
            }
        }

        public async Task DisplayItemsInFolder(StorageFolder Folder, bool ForceRefresh = false)
        {
            if (Folder == null)
            {
                throw new ArgumentNullException(nameof(Folder), "Parameter could not be null");
            }

            try
            {
                while (Nav.CurrentSourcePageType != typeof(FilePresenter))
                {
                    Nav.GoBack();
                }

                if (!ForceRefresh)
                {
                    if (Folder.Path == CurrentFolder?.Path)
                    {
                        IsAdding = false;
                        return;
                    }
                }

                if (IsAdding)
                {
                    await Task.Run(() =>
                    {
                        lock (SyncRootProvider.SyncRoot)
                        {
                            CancelToken.Cancel();
                            Locker.WaitOne();
                            CancelToken.Dispose();
                            CancelToken = new CancellationTokenSource();
                        }
                    }).ConfigureAwait(true);
                }
                IsAdding = true;

                if (IsBackOrForwardAction)
                {
                    IsBackOrForwardAction = false;
                }
                else
                {
                    if (RecordIndex != GoAndBackRecord.Count - 1 && GoAndBackRecord.Count != 0)
                    {
                        GoAndBackRecord.RemoveRange(RecordIndex + 1, GoAndBackRecord.Count - RecordIndex - 1);
                    }
                    GoAndBackRecord.Add(Folder);
                    RecordIndex = GoAndBackRecord.Count - 1;
                }

                CurrentFolder = Folder;

                FilePresenter Presenter = TabViewContainer.ThisPage.FFInstanceContainer[this];

                Presenter.FileCollection.Clear();

                List<FileSystemStorageItem> ItemList = Presenter.SortList(WIN_Native_API.GetStorageItems(Folder, ItemFilter.File | ItemFilter.Folder), SortTarget.Name, SortDirection.Ascending);

                Presenter.HasFile.Visibility = ItemList.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

                for (int i = 0; i < ItemList.Count && !CancelToken.IsCancellationRequested; i++)
                {
                    Presenter.FileCollection.Add(ItemList[i]);
                }

                if (CancelToken.IsCancellationRequested)
                {
                    Locker.Set();
                }
                else
                {
                    IsAdding = false;
                }
            }
            catch (Exception ex)
            {
                ExceptionTracer.RequestBlueScreen(ex);
            }
        }

        private async void FolderDelete_Click(object sender, RoutedEventArgs e)
        {
            if (CurrentNode == null)
            {
                return;
            }

            if (!await CurrentNode.CheckExist().ConfigureAwait(true))
            {
                if (Globalization.Language == LanguageEnum.Chinese)
                {
                    QueueContentDialog Dialog = new QueueContentDialog
                    {
                        Title = "错误",
                        Content = "无法找到对应的文件夹，该文件夹可能已被移动或删除",
                        CloseButtonText = "刷新"
                    };
                    _ = await Dialog.ShowAsync().ConfigureAwait(true);
                }
                else
                {
                    QueueContentDialog Dialog = new QueueContentDialog
                    {
                        Title = "Error",
                        Content = "Could not find the corresponding folder, it may have been moved or deleted",
                        CloseButtonText = "Refresh"
                    };
                    _ = await Dialog.ShowAsync().ConfigureAwait(true);
                }

                await DisplayItemsInFolder(CurrentNode, true).ConfigureAwait(false);

                return;
            }

            QueueContentDialog QueueContenDialog;
            if (Globalization.Language == LanguageEnum.Chinese)
            {
                QueueContenDialog = new QueueContentDialog
                {
                    Title = "警告",
                    Content = "此操作将永久删除该文件夹内的所有内容\r\r是否继续？",
                    PrimaryButtonText = "继续",
                    CloseButtonText = "取消"
                };
            }
            else
            {
                QueueContenDialog = new QueueContentDialog
                {
                    Title = "Warning",
                    Content = "This will permanently delete everything in the folder\r\rWhether to continue ？",
                    PrimaryButtonText = "Continue",
                    CloseButtonText = "Cancel"
                };
            }

            if (await QueueContenDialog.ShowAsync().ConfigureAwait(true) == ContentDialogResult.Primary)
            {
                try
                {
                    await CurrentFolder.DeleteAllSubFilesAndFolders().ConfigureAwait(true);
                    await CurrentFolder.DeleteAsync(StorageDeleteOption.PermanentDelete);

                    TabViewContainer.ThisPage.FFInstanceContainer[this].FileCollection.Remove(TabViewContainer.ThisPage.FFInstanceContainer[this].FileCollection.Where((Item) => Item.Path == CurrentFolder.Path).FirstOrDefault());

                    await DisplayItemsInFolder(CurrentNode.Parent).ConfigureAwait(true);

                    await FolderTree.RootNodes[0].UpdateAllSubNode().ConfigureAwait(true);
                }
                catch (UnauthorizedAccessException)
                {
                    QueueContentDialog dialog;
                    if (Globalization.Language == LanguageEnum.Chinese)
                    {
                        dialog = new QueueContentDialog
                        {
                            Title = "错误",
                            Content = "RX无权删除此文件夹，可能是您无权访问此文件夹\r\r是否立即进入系统文件管理器进行相应操作？",
                            PrimaryButtonText = "立刻",
                            CloseButtonText = "稍后"
                        };
                    }
                    else
                    {
                        dialog = new QueueContentDialog
                        {
                            Title = "Error",
                            Content = "RX does not have permission to delete this folder, it may be that you do not have access to this folder\r\rEnter the system file manager immediately ？",
                            PrimaryButtonText = "Enter",
                            CloseButtonText = "Later"
                        };
                    }

                    if (await dialog.ShowAsync().ConfigureAwait(true) == ContentDialogResult.Primary)
                    {
                        _ = await Launcher.LaunchFolderAsync(CurrentFolder);
                    }
                }
                catch (Exception)
                {
                    QueueContentDialog Dialog;
                    if (Globalization.Language == LanguageEnum.Chinese)
                    {
                        Dialog = new QueueContentDialog
                        {
                            Title = "错误",
                            Content = "删除文件夹时出现错误",
                            CloseButtonText = "确定"
                        };
                    }
                    else
                    {
                        Dialog = new QueueContentDialog
                        {
                            Title = "Error",
                            Content = "An error occurred while deleting the folder",
                            CloseButtonText = "Confirm"
                        };
                    }
                    _ = await Dialog.ShowAsync().ConfigureAwait(true);
                }
            }
        }

        private async void FolderTree_Collapsed(TreeView sender, TreeViewCollapsedEventArgs args)
        {
            FolderExpandCancel.Cancel();

            await Task.Run(() =>
            {
                ExitLocker.WaitOne();
            }).ConfigureAwait(true);

            FolderExpandCancel.Dispose();
            FolderExpandCancel = new CancellationTokenSource();

            args.Node.Children.Clear();
            args.Node.HasUnrealizedChildren = true;
        }

        private async void FolderTree_RightTapped(object sender, Windows.UI.Xaml.Input.RightTappedRoutedEventArgs e)
        {
            if (e.PointerDeviceType == Windows.Devices.Input.PointerDeviceType.Mouse)
            {
                if ((e.OriginalSource as FrameworkElement)?.DataContext is TreeViewNode Node)
                {
                    FolderTree.ContextFlyout = RightTabFlyout;

                    await DisplayItemsInFolder(Node).ConfigureAwait(true);

                    if (FolderTree.RootNodes.Contains(CurrentNode))
                    {
                        FolderDelete.IsEnabled = false;
                        FolderRename.IsEnabled = false;
                        FolderAdd.IsEnabled = false;
                    }
                    else
                    {
                        FolderDelete.IsEnabled = true;
                        FolderRename.IsEnabled = true;
                        FolderAdd.IsEnabled = true;
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
            if (CurrentNode == null)
            {
                return;
            }

            if (!await CurrentNode.CheckExist().ConfigureAwait(true))
            {
                if (Globalization.Language == LanguageEnum.Chinese)
                {
                    QueueContentDialog Dialog = new QueueContentDialog
                    {
                        Title = "错误",
                        Content = "无法找到对应的文件夹，该文件夹可能已被移动或删除",
                        CloseButtonText = "刷新"
                    };
                    _ = await Dialog.ShowAsync().ConfigureAwait(true);
                }
                else
                {
                    QueueContentDialog Dialog = new QueueContentDialog
                    {
                        Title = "Error",
                        Content = "Could not find the corresponding folder, it may have been moved or deleted",
                        CloseButtonText = "Refresh"
                    };
                    _ = await Dialog.ShowAsync().ConfigureAwait(true);
                }

                await DisplayItemsInFolder(CurrentNode, true).ConfigureAwait(false);

                return;
            }

            RenameDialog renameDialog = new RenameDialog(CurrentFolder.Name);
            if (await renameDialog.ShowAsync().ConfigureAwait(true) == ContentDialogResult.Primary)
            {
                if (string.IsNullOrEmpty(renameDialog.DesireName))
                {
                    if (Globalization.Language == LanguageEnum.Chinese)
                    {
                        QueueContentDialog content = new QueueContentDialog
                        {
                            Title = "错误",
                            Content = "文件夹名不能为空，重命名失败",
                            CloseButtonText = "确定"
                        };
                        _ = await content.ShowAsync().ConfigureAwait(true);
                    }
                    else
                    {
                        QueueContentDialog content = new QueueContentDialog
                        {
                            Title = "Error",
                            Content = "Folder name cannot be empty, rename failed",
                            CloseButtonText = "Confirm"
                        };
                        _ = await content.ShowAsync().ConfigureAwait(true);
                    }
                    return;
                }

                try
                {
                    await CurrentFolder.RenameAsync(renameDialog.DesireName, NameCollisionOption.GenerateUniqueName);
                    StorageFolder ReCreateFolder = await StorageFolder.GetFolderFromPathAsync(CurrentFolder.Path);

                    var ChildCollection = CurrentNode.Parent.Children;
                    int index = CurrentNode.Parent.Children.IndexOf(CurrentNode);

                    if (CurrentNode.HasUnrealizedChildren)
                    {
                        ChildCollection.Insert(index, new TreeViewNode()
                        {
                            Content = ReCreateFolder,
                            HasUnrealizedChildren = true,
                            IsExpanded = false
                        });
                        ChildCollection.Remove(CurrentNode);
                    }
                    else if (CurrentNode.HasChildren)
                    {
                        TreeViewNode NewNode = new TreeViewNode()
                        {
                            Content = ReCreateFolder,
                            HasUnrealizedChildren = false,
                            IsExpanded = false
                        };

                        foreach (var SubNode in CurrentNode.Children)
                        {
                            NewNode.Children.Add(SubNode);
                        }

                        ChildCollection.Insert(index, NewNode);
                        ChildCollection.Remove(CurrentNode);
                    }
                    else
                    {
                        ChildCollection.Insert(index, new TreeViewNode()
                        {
                            Content = ReCreateFolder,
                            HasUnrealizedChildren = false,
                            IsExpanded = false
                        });
                        ChildCollection.Remove(CurrentNode);
                    }

                    UpdateAddressButton(CurrentFolder);
                }
                catch (UnauthorizedAccessException)
                {
                    QueueContentDialog dialog;
                    if (Globalization.Language == LanguageEnum.Chinese)
                    {
                        dialog = new QueueContentDialog
                        {
                            Title = "错误",
                            Content = "RX无权重命名此文件夹，可能是您无权访问此文件夹\r\r是否立即进入系统文件管理器进行相应操作？",
                            PrimaryButtonText = "立刻",
                            CloseButtonText = "稍后"
                        };
                    }
                    else
                    {
                        dialog = new QueueContentDialog
                        {
                            Title = "Error",
                            Content = "RX does not have permission to rename this folder, it may be that you do not have access to this folder\r\rEnter the system file manager immediately ？",
                            PrimaryButtonText = "Enter",
                            CloseButtonText = "Later",
                        };
                    }

                    if (await dialog.ShowAsync().ConfigureAwait(true) == ContentDialogResult.Primary)
                    {
                        _ = await Launcher.LaunchFolderAsync(CurrentFolder);
                    }
                }
            }
        }

        private async void CreateFolder_Click(object sender, RoutedEventArgs e)
        {
            if (!await CurrentFolder.CheckExist().ConfigureAwait(true))
            {
                if (Globalization.Language == LanguageEnum.Chinese)
                {
                    QueueContentDialog Dialog = new QueueContentDialog
                    {
                        Title = "错误",
                        Content = "无法找到对应的文件夹，该文件夹可能已被移动或删除",
                        CloseButtonText = "刷新"
                    };
                    _ = await Dialog.ShowAsync().ConfigureAwait(true);
                }
                else
                {
                    QueueContentDialog Dialog = new QueueContentDialog
                    {
                        Title = "Error",
                        Content = "Could not find the corresponding folder, it may have been moved or deleted",
                        CloseButtonText = "Refresh"
                    };
                    _ = await Dialog.ShowAsync().ConfigureAwait(true);
                }
                return;
            }

            try
            {
                StorageFolder NewFolder = Globalization.Language == LanguageEnum.Chinese
                    ? await CurrentFolder.CreateFolderAsync("新建文件夹", CreationCollisionOption.GenerateUniqueName)
                    : await CurrentFolder.CreateFolderAsync("New folder", CreationCollisionOption.GenerateUniqueName);

                TabViewContainer.ThisPage.FFInstanceContainer[this].FileCollection.Insert(0, new FileSystemStorageItem(NewFolder, await NewFolder.GetModifiedTimeAsync().ConfigureAwait(true)));

                await FolderTree.RootNodes[0].UpdateAllSubNode().ConfigureAwait(true);
            }
            catch (UnauthorizedAccessException)
            {
                QueueContentDialog dialog;
                if (Globalization.Language == LanguageEnum.Chinese)
                {
                    dialog = new QueueContentDialog
                    {
                        Title = "错误",
                        Content = "RX无权在此创建文件夹，可能是您无权访问此文件夹\r\r是否立即进入系统文件管理器进行相应操作？",
                        PrimaryButtonText = "立刻",
                        CloseButtonText = "稍后"
                    };
                }
                else
                {
                    dialog = new QueueContentDialog
                    {
                        Title = "Error",
                        Content = "RX does not have permission to create folder, it may be that you do not have access to this folder\r\rEnter the system file manager immediately ？",
                        PrimaryButtonText = "Enter",
                        CloseButtonText = "Later"
                    };
                }

                if (await dialog.ShowAsync().ConfigureAwait(true) == ContentDialogResult.Primary)
                {
                    _ = await Launcher.LaunchFolderAsync(CurrentFolder);
                }
            }
        }

        private async void FolderAttribute_Click(object sender, RoutedEventArgs e)
        {
            if (!await CurrentFolder.CheckExist().ConfigureAwait(true))
            {
                if (Globalization.Language == LanguageEnum.Chinese)
                {
                    QueueContentDialog Dialog = new QueueContentDialog
                    {
                        Title = "错误",
                        Content = "无法找到对应的文件夹，该文件夹可能已被移动或删除",
                        CloseButtonText = "刷新"
                    };
                    _ = await Dialog.ShowAsync().ConfigureAwait(true);
                }
                else
                {
                    QueueContentDialog Dialog = new QueueContentDialog
                    {
                        Title = "Error",
                        Content = "Could not find the corresponding folder, it may have been moved or deleted",
                        CloseButtonText = "Refresh"
                    };
                    _ = await Dialog.ShowAsync().ConfigureAwait(true);
                }

                await DisplayItemsInFolder(CurrentNode, true).ConfigureAwait(false);
                return;
            }

            if (CurrentNode == FolderTree.RootNodes.FirstOrDefault())
            {
                if (TabViewContainer.ThisPage.HardDeviceList.FirstOrDefault((Device) => Device.Name == CurrentFolder.DisplayName) is HardDeviceInfo Info)
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

        private async void FolderAdd_Click(object sender, RoutedEventArgs e)
        {
            StorageFolder folder = CurrentFolder;

            if (!await folder.CheckExist().ConfigureAwait(true))
            {
                if (Globalization.Language == LanguageEnum.Chinese)
                {
                    QueueContentDialog Dialog = new QueueContentDialog
                    {
                        Title = "错误",
                        Content = "无法找到对应的文件夹，该文件夹可能已被移动或删除",
                        CloseButtonText = "刷新"
                    };
                    _ = await Dialog.ShowAsync().ConfigureAwait(true);
                }
                else
                {
                    QueueContentDialog Dialog = new QueueContentDialog
                    {
                        Title = "Error",
                        Content = "Could not find the corresponding folder, it may have been moved or deleted",
                        CloseButtonText = "Refresh"
                    };
                    _ = await Dialog.ShowAsync().ConfigureAwait(true);
                }

                await DisplayItemsInFolder(CurrentNode, true).ConfigureAwait(false);
                return;
            }

            if (TabViewContainer.ThisPage.LibraryFolderList.Any((Folder) => Folder.Folder.Path == folder.Path))
            {
                QueueContentDialog dialog = new QueueContentDialog
                {
                    Title = "提示",
                    Content = "此文件夹已经添加到主界面了，不能重复添加哦",
                    CloseButtonText = "知道了"
                };
                _ = await dialog.ShowAsync().ConfigureAwait(true);
            }
            else
            {
                BitmapImage Thumbnail = await folder.GetThumbnailBitmapAsync().ConfigureAwait(true);
                TabViewContainer.ThisPage.LibraryFolderList.Add(new LibraryFolder(folder, Thumbnail, LibrarySource.UserCustom));
                await SQLite.Current.SetFolderLibraryAsync(folder.Path).ConfigureAwait(false);
            }
        }

        private async void GlobeSearch_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
        {
            if (string.IsNullOrWhiteSpace(args.QueryText))
            {
                return;
            }

            FlyoutBase.ShowAttachedFlyout(sender);

            await SQLite.Current.SetSearchHistoryAsync(args.QueryText).ConfigureAwait(false);
        }

        private async void GlobeSearch_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            if (string.IsNullOrWhiteSpace(sender.Text))
            {
                if (Nav.CurrentSourcePageType == typeof(SearchPage))
                {
                    Nav.GoBack();
                }
                return;
            }

            if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
            {
                sender.ItemsSource = await SQLite.Current.GetRelatedSearchHistoryAsync(sender.Text).ConfigureAwait(true);
            }
        }

        private void SearchConfirm_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                SearchFlyout.Hide();

                if (ApplicationData.Current.LocalSettings.Values["LaunchSearchTips"] == null)
                {
                    ApplicationData.Current.LocalSettings.Values["LaunchSearchTips"] = true;
                    SearchTip.IsOpen = true;
                }

                QueryOptions Options;
                if ((bool)ShallowRadio.IsChecked)
                {
                    Options = new QueryOptions(CommonFolderQuery.DefaultQuery)
                    {
                        FolderDepth = FolderDepth.Shallow,
                        IndexerOption = IndexerOption.UseIndexerWhenAvailable,
                        ApplicationSearchFilter = "System.FileName:*" + GlobeSearch.Text + "*"
                    };
                }
                else
                {
                    Options = new QueryOptions(CommonFolderQuery.DefaultQuery)
                    {
                        FolderDepth = FolderDepth.Deep,
                        IndexerOption = IndexerOption.UseIndexerWhenAvailable,
                        ApplicationSearchFilter = "System.FileName:*" + GlobeSearch.Text + "*"
                    };
                }

                Options.SetThumbnailPrefetch(ThumbnailMode.ListView, 100, ThumbnailOptions.ResizeThumbnail);
                Options.SetPropertyPrefetch(PropertyPrefetchOptions.BasicProperties, new string[] { "System.ItemTypeText", "System.ItemNameDisplayWithoutExtension", "System.FileName", "System.Size", "System.DateModified" });

                if (Nav.CurrentSourcePageType.Name != "SearchPage")
                {
                    StorageItemQueryResult FileQuery = CurrentFolder.CreateItemQueryWithOptions(Options);

                    Nav.Navigate(typeof(SearchPage), new Tuple<FileControl, StorageItemQueryResult>(this, FileQuery), new DrillInNavigationTransitionInfo());
                }
                else
                {
                    TabViewContainer.ThisPage.FSInstanceContainer[this].SetSearchTarget = Options;
                }
            }
            catch (Exception ex)
            {
                ExceptionTracer.RequestBlueScreen(ex);
            }
        }

        private void SearchCancel_Click(object sender, RoutedEventArgs e)
        {
            SearchFlyout.Hide();
        }

        private void SearchFlyout_Opened(object sender, object e)
        {
            _ = SearchConfirm.Focus(FocusState.Programmatic);
        }

        private async void GlobeSearch_GotFocus(object sender, RoutedEventArgs e)
        {
            IsSearchOrPathBoxFocused = true;
            if (string.IsNullOrEmpty(GlobeSearch.Text))
            {
                GlobeSearch.ItemsSource = await SQLite.Current.GetRelatedSearchHistoryAsync(string.Empty).ConfigureAwait(true);
            }
        }

        private async void AddressBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
        {
            TabViewContainer.ThisPage.FFInstanceContainer[this].LoadingControl.Focus(FocusState.Programmatic);

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

            if (string.Equals(QueryText, "Powershell", StringComparison.OrdinalIgnoreCase))
            {
                string ExcutePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32\\WindowsPowerShell\\v1.0\\powershell.exe");
                await FullTrustExcutorController.RunAsAdministrator(ExcutePath, $"-NoExit -Command \"Set-Location '{CurrentFolder.Path.Replace("\\", "/")}'\"").ConfigureAwait(false);
                return;
            }

            if (string.Equals(QueryText, "Cmd", StringComparison.OrdinalIgnoreCase))
            {
                string ExcutePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32\\cmd.exe");
                await FullTrustExcutorController.RunAsAdministrator(ExcutePath, $"/k cd /d {CurrentFolder.Path}").ConfigureAwait(false);
                return;
            }

            try
            {
                if (Path.IsPathRooted(QueryText) && TabViewContainer.ThisPage.HardDeviceList.Any((Drive) => Drive.Folder.Path == Path.GetPathRoot(QueryText)))
                {
                    StorageFile File = await StorageFile.GetFileFromPathAsync(QueryText);
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
                    if (Globalization.Language == LanguageEnum.Chinese)
                    {
                        QueueContentDialog dialog = new QueueContentDialog
                        {
                            Title = "错误",
                            Content = $"无法找到路径: \r\"{QueryText}\"",
                            CloseButtonText = "确定"
                        };
                        _ = await dialog.ShowAsync().ConfigureAwait(true);
                    }
                    else
                    {
                        QueueContentDialog dialog = new QueueContentDialog
                        {
                            Title = "Error",
                            Content = $"Unable to locate the path: \r\"{QueryText}\"",
                            CloseButtonText = "Confirm",
                        };
                        _ = await dialog.ShowAsync().ConfigureAwait(true);
                    }
                }
            }
            catch (Exception)
            {
                try
                {
                    StorageFolder Folder = await StorageFolder.GetFolderFromPathAsync(QueryText);

                    if (SettingControl.IsDetachTreeViewAndPresenter)
                    {
                        await DisplayItemsInFolder(Folder).ConfigureAwait(false);

                        await SQLite.Current.SetPathHistoryAsync(Folder.Path).ConfigureAwait(false);
                    }
                    else
                    {
                        if (QueryText.StartsWith((FolderTree.RootNodes[0].Content as StorageFolder).Path))
                        {
                            TreeViewNode TargetNode = await FolderTree.RootNodes[0].FindFolderLocationInTree(new PathAnalysis(Folder.Path, (FolderTree.RootNodes[0].Content as StorageFolder).Path)).ConfigureAwait(true);
                            if (TargetNode != null)
                            {
                                await DisplayItemsInFolder(TargetNode).ConfigureAwait(false);

                                await SQLite.Current.SetPathHistoryAsync(Folder.Path).ConfigureAwait(false);
                            }
                        }
                        else
                        {
                            await OpenTargetFolder(Folder).ConfigureAwait(false);

                            await SQLite.Current.SetPathHistoryAsync(Folder.Path).ConfigureAwait(false);
                        }
                    }
                }
                catch (Exception)
                {
                    if (Globalization.Language == LanguageEnum.Chinese)
                    {
                        QueueContentDialog dialog = new QueueContentDialog
                        {
                            Title = "错误",
                            Content = $"无法找到路径: \r\"{QueryText}\"",
                            CloseButtonText = "确定"
                        };
                        _ = await dialog.ShowAsync().ConfigureAwait(true);
                    }
                    else
                    {
                        QueueContentDialog dialog = new QueueContentDialog
                        {
                            Title = "Error",
                            Content = $"Unable to locate the path: \r\"{QueryText}\"",
                            CloseButtonText = "Confirm",
                        };
                        _ = await dialog.ShowAsync().ConfigureAwait(true);
                    }
                }
            }
        }

        private async void AddressBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            AddressBoxTextBackup = sender.Text;

            if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
            {
                if (Path.IsPathRooted(sender.Text)
                    && Path.GetDirectoryName(sender.Text) is string DirectoryName
                    && TabViewContainer.ThisPage.HardDeviceList.Any((Drive) => Drive.Folder.Path == Path.GetPathRoot(sender.Text)))
                {
                    if (Interlocked.Exchange(ref TextChangeLockResource, 1) == 0)
                    {
                        try
                        {
                            StorageFolder Folder = await StorageFolder.GetFolderFromPathAsync(DirectoryName);
                            if (args.CheckCurrent())
                            {
                                QueryOptions Options = new QueryOptions(CommonFolderQuery.DefaultQuery)
                                {
                                    FolderDepth = FolderDepth.Shallow,
                                    IndexerOption = IndexerOption.UseIndexerWhenAvailable,
                                    ApplicationSearchFilter = $"System.FileName:{Path.GetFileName(sender.Text)}*"
                                };
                                IReadOnlyList<StorageFolder> SearchResult = await Folder.CreateFolderQueryWithOptions(Options).GetFoldersAsync(0, 10);

                                if (args.CheckCurrent())
                                {
                                    sender.ItemsSource = SearchResult.Select((Item) => Item.Path);
                                }
                                else
                                {
                                    sender.ItemsSource = null;
                                }
                            }
                            else
                            {
                                sender.ItemsSource = null;
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

        private async void GoParentFolder_Click(object sender, RoutedEventArgs e)
        {
            if (Interlocked.Exchange(ref NavigateLockResource, 1) == 0)
            {
                try
                {
                    if (SettingControl.IsDetachTreeViewAndPresenter)
                    {
                        if ((await CurrentFolder.GetParentAsync()) is StorageFolder ParentFolder)
                        {
                            await DisplayItemsInFolder(ParentFolder).ConfigureAwait(false);
                        }
                    }
                    else
                    {
                        if ((await CurrentFolder.GetParentAsync()) is StorageFolder ParentFolder)
                        {
                            TreeViewNode ParenetNode = await FolderTree.RootNodes[0].FindFolderLocationInTree(new PathAnalysis(ParentFolder.Path, (FolderTree.RootNodes[0].Content as StorageFolder).Path)).ConfigureAwait(true);
                            await DisplayItemsInFolder(ParenetNode).ConfigureAwait(false);
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
                string Path = GoAndBackRecord[--RecordIndex].Path;
                try
                {
                    StorageFolder Folder = await StorageFolder.GetFolderFromPathAsync(Path);

                    IsBackOrForwardAction = true;
                    if (SettingControl.IsDetachTreeViewAndPresenter)
                    {
                        await DisplayItemsInFolder(Folder).ConfigureAwait(false);

                        await SQLite.Current.SetPathHistoryAsync(Folder.Path).ConfigureAwait(false);
                    }
                    else
                    {
                        if (Path.StartsWith((FolderTree.RootNodes.First().Content as StorageFolder).Path))
                        {

                            TreeViewNode TargetNode = await FolderTree.RootNodes[0].FindFolderLocationInTree(new PathAnalysis(Folder.Path, (FolderTree.RootNodes[0].Content as StorageFolder).Path)).ConfigureAwait(true);
                            if (TargetNode == null)
                            {
                                if (Globalization.Language == LanguageEnum.Chinese)
                                {
                                    QueueContentDialog dialog = new QueueContentDialog
                                    {
                                        Title = "错误",
                                        Content = "无法定位文件夹，该文件夹可能已被删除或移动",
                                        CloseButtonText = "确定",
                                    };
                                    _ = await dialog.ShowAsync().ConfigureAwait(true);
                                }
                                else
                                {
                                    QueueContentDialog dialog = new QueueContentDialog
                                    {
                                        Title = "Error",
                                        Content = "Unable to locate folder, which may have been deleted or moved",
                                        CloseButtonText = "Confirm",
                                    };
                                    _ = await dialog.ShowAsync().ConfigureAwait(true);
                                }
                            }
                            else
                            {
                                await DisplayItemsInFolder(TargetNode).ConfigureAwait(false);

                                await SQLite.Current.SetPathHistoryAsync(Folder.Path).ConfigureAwait(false);
                            }
                        }
                        else
                        {
                            await OpenTargetFolder(Folder).ConfigureAwait(false);

                            await SQLite.Current.SetPathHistoryAsync(Folder.Path).ConfigureAwait(false);
                        }
                    }
                }
                catch (Exception)
                {
                    if (Globalization.Language == LanguageEnum.Chinese)
                    {
                        QueueContentDialog dialog = new QueueContentDialog
                        {
                            Title = "错误",
                            Content = "无法找到以下文件夹，路径为: \r" + Path,
                            CloseButtonText = "确定"
                        };
                        _ = await dialog.ShowAsync().ConfigureAwait(true);
                    }
                    else
                    {
                        QueueContentDialog dialog = new QueueContentDialog
                        {
                            Title = "Error",
                            Content = "Unable to locate folder: " + Path,
                            CloseButtonText = "Confirm"
                        };
                        _ = await dialog.ShowAsync().ConfigureAwait(true);
                    }
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
                string Path = GoAndBackRecord[++RecordIndex].Path;
                try
                {
                    StorageFolder Folder = await StorageFolder.GetFolderFromPathAsync(Path);

                    IsBackOrForwardAction = true;
                    if (SettingControl.IsDetachTreeViewAndPresenter)
                    {
                        await DisplayItemsInFolder(Folder).ConfigureAwait(false);

                        await SQLite.Current.SetPathHistoryAsync(Folder.Path).ConfigureAwait(false);
                    }
                    else
                    {
                        if (Path.StartsWith((FolderTree.RootNodes.First().Content as StorageFolder).Path))
                        {

                            TreeViewNode TargetNode = await FolderTree.RootNodes[0].FindFolderLocationInTree(new PathAnalysis(Folder.Path, (FolderTree.RootNodes[0].Content as StorageFolder).Path)).ConfigureAwait(true);
                            if (TargetNode == null)
                            {
                                if (Globalization.Language == LanguageEnum.Chinese)
                                {
                                    QueueContentDialog dialog = new QueueContentDialog
                                    {
                                        Title = "错误",
                                        Content = "无法定位文件夹，该文件夹可能已被删除或移动",
                                        CloseButtonText = "确定"
                                    };
                                    _ = await dialog.ShowAsync().ConfigureAwait(true);
                                }
                                else
                                {
                                    QueueContentDialog dialog = new QueueContentDialog
                                    {
                                        Title = "Error",
                                        Content = "Unable to locate folder, which may have been deleted or moved",
                                        CloseButtonText = "Confirm"
                                    };
                                    _ = await dialog.ShowAsync().ConfigureAwait(true);
                                }
                            }
                            else
                            {
                                await DisplayItemsInFolder(TargetNode).ConfigureAwait(false);

                                await SQLite.Current.SetPathHistoryAsync(Folder.Path).ConfigureAwait(false);
                            }
                        }
                        else
                        {
                            await OpenTargetFolder(Folder).ConfigureAwait(false);

                            await SQLite.Current.SetPathHistoryAsync(Folder.Path).ConfigureAwait(false);
                        }
                    }
                }
                catch (Exception)
                {
                    if (Globalization.Language == LanguageEnum.Chinese)
                    {
                        QueueContentDialog dialog = new QueueContentDialog
                        {
                            Title = "错误",
                            Content = "无法找到以下文件夹，路径为: \r" + Path,
                            CloseButtonText = "确定"
                        };
                        _ = await dialog.ShowAsync().ConfigureAwait(true);
                    }
                    else
                    {
                        QueueContentDialog dialog = new QueueContentDialog
                        {
                            Title = "Error",
                            Content = "Unable to locate folder: " + Path,
                            CloseButtonText = "Confirm"
                        };
                        _ = await dialog.ShowAsync().ConfigureAwait(true);
                    }
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
            IsSearchOrPathBoxFocused = true;

            if (string.IsNullOrEmpty(AddressBox.Text))
            {
                AddressBox.Text = CurrentFolder.Path;
            }
            AddressButtonScrollViewer.Visibility = Visibility.Collapsed;

            AddressBox.ItemsSource = await SQLite.Current.GetRelatedPathHistoryAsync().ConfigureAwait(true);
        }

        private void ItemDisplayMode_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ApplicationData.Current.LocalSettings.Values["FilePresenterDisplayMode"] = ItemDisplayMode.SelectedIndex;

            switch (ItemDisplayMode.SelectedIndex)
            {
                case 0:
                    {
                        TabViewContainer.ThisPage.FFInstanceContainer[this].GridViewControl.ItemTemplate = TabViewContainer.ThisPage.FFInstanceContainer[this].TileDataTemplate;

                        if (!TabViewContainer.ThisPage.FFInstanceContainer[this].UseGridOrList)
                        {
                            TabViewContainer.ThisPage.FFInstanceContainer[this].UseGridOrList = true;
                        }
                        break;
                    }
                case 1:
                    {
                        TabViewContainer.ThisPage.FFInstanceContainer[this].ListHeader.ContentTemplate = TabViewContainer.ThisPage.FFInstanceContainer[this].ListHeaderDataTemplate;
                        TabViewContainer.ThisPage.FFInstanceContainer[this].ListViewControl.ItemTemplate = TabViewContainer.ThisPage.FFInstanceContainer[this].ListViewDetailDataTemplate;
                        TabViewContainer.ThisPage.FFInstanceContainer[this].ListViewControl.ItemsSource = TabViewContainer.ThisPage.FFInstanceContainer[this].FileCollection;

                        if (TabViewContainer.ThisPage.FFInstanceContainer[this].UseGridOrList)
                        {
                            TabViewContainer.ThisPage.FFInstanceContainer[this].UseGridOrList = false;
                        }
                        break;
                    }

                case 2:
                    {
                        TabViewContainer.ThisPage.FFInstanceContainer[this].ListHeader.ContentTemplate = null;
                        TabViewContainer.ThisPage.FFInstanceContainer[this].ListViewControl.ItemTemplate = TabViewContainer.ThisPage.FFInstanceContainer[this].ListViewSimpleDataTemplate;
                        TabViewContainer.ThisPage.FFInstanceContainer[this].ListViewControl.ItemsSource = TabViewContainer.ThisPage.FFInstanceContainer[this].FileCollection;

                        if (TabViewContainer.ThisPage.FFInstanceContainer[this].UseGridOrList)
                        {
                            TabViewContainer.ThisPage.FFInstanceContainer[this].UseGridOrList = false;
                        }
                        break;
                    }
                case 3:
                    {
                        TabViewContainer.ThisPage.FFInstanceContainer[this].GridViewControl.ItemTemplate = TabViewContainer.ThisPage.FFInstanceContainer[this].LargeImageDataTemplate;

                        if (!TabViewContainer.ThisPage.FFInstanceContainer[this].UseGridOrList)
                        {
                            TabViewContainer.ThisPage.FFInstanceContainer[this].UseGridOrList = true;
                        }
                        break;
                    }
                case 4:
                    {
                        TabViewContainer.ThisPage.FFInstanceContainer[this].GridViewControl.ItemTemplate = TabViewContainer.ThisPage.FFInstanceContainer[this].MediumImageDataTemplate;

                        if (!TabViewContainer.ThisPage.FFInstanceContainer[this].UseGridOrList)
                        {
                            TabViewContainer.ThisPage.FFInstanceContainer[this].UseGridOrList = true;
                        }
                        break;
                    }
                case 5:
                    {
                        TabViewContainer.ThisPage.FFInstanceContainer[this].GridViewControl.ItemTemplate = TabViewContainer.ThisPage.FFInstanceContainer[this].SmallImageDataTemplate;

                        if (!TabViewContainer.ThisPage.FFInstanceContainer[this].UseGridOrList)
                        {
                            TabViewContainer.ThisPage.FFInstanceContainer[this].UseGridOrList = true;
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
            AddressButtonScrollViewer.Visibility = Visibility.Visible;
        }

        private async void AddressButton_Click(object sender, RoutedEventArgs e)
        {
            string OriginalString = string.Join("\\", AddressButtonList.Take(AddressButtonList.IndexOf(((Button)sender).DataContext as AddressBlock) + 1).Skip(1));
            string ActualString = Path.Combine(Path.GetPathRoot(CurrentFolder.Path), OriginalString);

            if (ActualString == CurrentFolder.Path)
            {
                return;
            }

            if (SettingControl.IsDetachTreeViewAndPresenter)
            {
                try
                {
                    StorageFolder TargetFolder = await StorageFolder.GetFolderFromPathAsync(ActualString);
                    await DisplayItemsInFolder(TargetFolder).ConfigureAwait(false);
                    await SQLite.Current.SetPathHistoryAsync(ActualString).ConfigureAwait(false);
                }
                catch
                {
                    if (Globalization.Language == LanguageEnum.Chinese)
                    {
                        QueueContentDialog dialog = new QueueContentDialog
                        {
                            Title = "错误",
                            Content = "无法找到以下文件夹，路径为: \r" + ActualString,
                            CloseButtonText = "确定"
                        };
                        _ = await dialog.ShowAsync().ConfigureAwait(true);
                    }
                    else
                    {
                        QueueContentDialog dialog = new QueueContentDialog
                        {
                            Title = "Error",
                            Content = "Unable to locate folder: " + ActualString,
                            CloseButtonText = "Confirm"
                        };
                        _ = await dialog.ShowAsync().ConfigureAwait(true);
                    }
                }
            }
            else
            {
                if (ActualString.StartsWith((FolderTree.RootNodes[0].Content as StorageFolder).Path))
                {
                    if ((await FolderTree.RootNodes[0].FindFolderLocationInTree(new PathAnalysis(ActualString, (FolderTree.RootNodes[0].Content as StorageFolder).Path)).ConfigureAwait(true)) is TreeViewNode TargetNode)
                    {
                        await DisplayItemsInFolder(TargetNode).ConfigureAwait(false);

                        await SQLite.Current.SetPathHistoryAsync(ActualString).ConfigureAwait(false);
                    }
                    else
                    {
                        if (Globalization.Language == LanguageEnum.Chinese)
                        {
                            QueueContentDialog dialog = new QueueContentDialog
                            {
                                Title = "错误",
                                Content = "无法找到以下文件夹，路径为: \r" + ActualString,
                                CloseButtonText = "确定"
                            };
                            _ = await dialog.ShowAsync().ConfigureAwait(true);
                        }
                        else
                        {
                            QueueContentDialog dialog = new QueueContentDialog
                            {
                                Title = "Error",
                                Content = "Unable to locate folder: " + ActualString,
                                CloseButtonText = "Confirm"
                            };
                            _ = await dialog.ShowAsync().ConfigureAwait(true);
                        }
                    }
                }
                else
                {
                    try
                    {
                        StorageFolder Folder = await StorageFolder.GetFolderFromPathAsync(ActualString);

                        await OpenTargetFolder(Folder).ConfigureAwait(false);

                        await SQLite.Current.SetPathHistoryAsync(ActualString).ConfigureAwait(false);
                    }
                    catch
                    {
                        if (Globalization.Language == LanguageEnum.Chinese)
                        {
                            QueueContentDialog dialog = new QueueContentDialog
                            {
                                Title = "错误",
                                Content = "无法找到以下文件夹，路径为: \r" + ActualString,
                                CloseButtonText = "确定"
                            };
                            _ = await dialog.ShowAsync().ConfigureAwait(true);
                        }
                        else
                        {
                            QueueContentDialog dialog = new QueueContentDialog
                            {
                                Title = "Error",
                                Content = "Unable to locate folder: " + ActualString,
                                CloseButtonText = "Confirm"
                            };
                            _ = await dialog.ShowAsync().ConfigureAwait(true);
                        }
                    }
                }
            }
        }

        private async void AddressExtention_Click(object sender, RoutedEventArgs e)
        {
            Button Btn = sender as Button;
            TextBlock StateText = Btn.Content as TextBlock;

            AddressExtentionList.Clear();

            string OriginalString = string.Join("\\", AddressButtonList.Take(AddressButtonList.IndexOf(Btn.DataContext as AddressBlock) + 1).Skip(1));
            string ActualString = Path.Combine(Path.GetPathRoot(CurrentFolder.Path), OriginalString);

            List<FileSystemStorageItem> ItemList = WIN_Native_API.GetStorageItems(ActualString, ItemFilter.Folder);

            foreach (string SubFolderName in ItemList.Where((It) => It.StorageType == StorageItemTypes.Folder).Select((Item) => Item.Name))
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
            await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                AddressExtentionFlyout.Hide();
            });

            if (!string.IsNullOrEmpty(e.ClickedItem.ToString()))
            {
                string OriginalString = string.Join("\\", AddressButtonList.Take(AddressButtonList.IndexOf(AddressExtentionFlyout.Target.DataContext as AddressBlock) + 1).Skip(1));
                string ActualString = Path.Combine(Path.GetPathRoot(CurrentFolder.Path), OriginalString);

                string TargetPath = Path.Combine(ActualString, e.ClickedItem.ToString());

                if (SettingControl.IsDetachTreeViewAndPresenter)
                {
                    try
                    {
                        StorageFolder TargetFolder = await StorageFolder.GetFolderFromPathAsync(TargetPath);

                        await DisplayItemsInFolder(TargetFolder).ConfigureAwait(false);
                    }
                    catch
                    {
                        if (Globalization.Language == LanguageEnum.Chinese)
                        {
                            QueueContentDialog dialog = new QueueContentDialog
                            {
                                Title = "错误",
                                Content = "无法找到以下文件夹，路径为: \r" + TargetPath,
                                CloseButtonText = "确定"
                            };
                            _ = await dialog.ShowAsync().ConfigureAwait(true);
                        }
                        else
                        {
                            QueueContentDialog dialog = new QueueContentDialog
                            {
                                Title = "Error",
                                Content = "Unable to locate folder: " + TargetPath,
                                CloseButtonText = "Confirm"
                            };
                            _ = await dialog.ShowAsync().ConfigureAwait(true);
                        }
                    }
                }
                else
                {
                    if (TargetPath.StartsWith((FolderTree.RootNodes[0].Content as StorageFolder).Path))
                    {
                        TreeViewNode TargetNode = await FolderTree.RootNodes[0].FindFolderLocationInTree(new PathAnalysis(TargetPath, (FolderTree.RootNodes[0].Content as StorageFolder).Path)).ConfigureAwait(true);
                        if (TargetNode != null)
                        {
                            await DisplayItemsInFolder(TargetNode).ConfigureAwait(false);
                        }
                        else
                        {
                            if (Globalization.Language == LanguageEnum.Chinese)
                            {
                                QueueContentDialog dialog = new QueueContentDialog
                                {
                                    Title = "错误",
                                    Content = "无法找到以下文件夹，路径为: \r" + TargetPath,
                                    CloseButtonText = "确定"
                                };
                                _ = await dialog.ShowAsync().ConfigureAwait(true);
                            }
                            else
                            {
                                QueueContentDialog dialog = new QueueContentDialog
                                {
                                    Title = "Error",
                                    Content = "Unable to locate folder: " + TargetPath,
                                    CloseButtonText = "Confirm"
                                };
                                _ = await dialog.ShowAsync().ConfigureAwait(true);
                            }
                        }
                    }
                    else
                    {
                        try
                        {
                            StorageFolder Folder = await StorageFolder.GetFolderFromPathAsync(TargetPath);

                            await OpenTargetFolder(Folder).ConfigureAwait(false);
                        }
                        catch
                        {
                            if (Globalization.Language == LanguageEnum.Chinese)
                            {
                                QueueContentDialog dialog = new QueueContentDialog
                                {
                                    Title = "错误",
                                    Content = "无法找到以下文件夹，路径为: \r" + TargetPath,
                                    CloseButtonText = "确定"
                                };
                                _ = await dialog.ShowAsync().ConfigureAwait(true);
                            }
                            else
                            {
                                QueueContentDialog dialog = new QueueContentDialog
                                {
                                    Title = "Error",
                                    Content = "Unable to locate folder: " + TargetPath,
                                    CloseButtonText = "Confirm"
                                };
                                _ = await dialog.ShowAsync().ConfigureAwait(true);
                            }
                        }
                    }
                }
            }
        }

        private async void AddressButton_Drop(object sender, DragEventArgs e)
        {
            string OriginalString = string.Join("\\", AddressButtonList.Take(AddressButtonList.IndexOf(((Button)sender).DataContext as AddressBlock) + 1).Skip(1));
            string ActualPath = Path.Combine(Path.GetPathRoot(CurrentFolder.Path), OriginalString);

            if (Interlocked.Exchange(ref DropLockResource, 1) == 0)
            {
                try
                {
                    if (ActualPath == CurrentFolder.Path)
                    {
                        if (Globalization.Language == LanguageEnum.Chinese)
                        {
                            QueueContentDialog Dialog = new QueueContentDialog
                            {
                                Title = "错误",
                                Content = "目标文件夹和源文件夹相同",
                                CloseButtonText = "确定"
                            };
                            _ = await Dialog.ShowAsync().ConfigureAwait(true);
                        }
                        else
                        {
                            QueueContentDialog Dialog = new QueueContentDialog
                            {
                                Title = "Error",
                                Content = "The target folder is the same as the source folder",
                                CloseButtonText = "Got it"
                            };
                            _ = await Dialog.ShowAsync().ConfigureAwait(true);
                        }

                        return;
                    }

                    FilePresenter.CopyAndMoveRecord.Clear();

                    List<IStorageItem> DragItemList = (await e.DataView.GetStorageItemsAsync()).ToList();

                    StorageFolder TargetFolder = await StorageFolder.GetFolderFromPathAsync(ActualPath);

                    switch (e.AcceptedOperation)
                    {
                        case DataPackageOperation.Copy:
                            {
                                await TabViewContainer.ThisPage.FFInstanceContainer[this].LoadingActivation(true, Globalization.Language == LanguageEnum.Chinese ? "正在复制" : "Copying").ConfigureAwait(true);

                                bool IsItemNotFound = false;
                                bool IsUnauthorized = false;
                                bool IsSpaceError = false;

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

                                            FilePresenter.CopyAndMoveRecord.Add($"{File.Path}||Copy||File||{Path.Combine(TargetFolder.Path, File.Name)}");

                                            _ = await File.CopyAsync(TargetFolder, Item.Name, NameCollisionOption.GenerateUniqueName);
                                        }
                                        else if (Item is StorageFolder Folder)
                                        {
                                            if (!await Folder.CheckExist().ConfigureAwait(true))
                                            {
                                                IsItemNotFound = true;
                                                continue;
                                            }

                                            FilePresenter.CopyAndMoveRecord.Add($"{Folder.Path}||Copy||Folder||{Path.Combine(TargetFolder.Path, Folder.Name)}");

                                            StorageFolder NewFolder = await TargetFolder.CreateFolderAsync(Item.Name, CreationCollisionOption.GenerateUniqueName);
                                            await Folder.CopySubFilesAndSubFoldersAsync(NewFolder).ConfigureAwait(true);

                                            if (!SettingControl.IsDetachTreeViewAndPresenter && ActualPath.StartsWith((FolderTree.RootNodes[0].Content as StorageFolder).Path))
                                            {
                                                await FolderTree.RootNodes[0].UpdateAllSubNode().ConfigureAwait(true);
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
                                    if (Globalization.Language == LanguageEnum.Chinese)
                                    {
                                        QueueContentDialog Dialog = new QueueContentDialog
                                        {
                                            Title = "错误",
                                            Content = "部分文件不存在，无法复制到指定位置",
                                            CloseButtonText = "确定"
                                        };
                                        _ = await Dialog.ShowAsync().ConfigureAwait(true);
                                    }
                                    else
                                    {
                                        QueueContentDialog Dialog = new QueueContentDialog
                                        {
                                            Title = "Error",
                                            Content = "Some files do not exist and cannot be copyed to the specified location",
                                            CloseButtonText = "Got it"
                                        };
                                        _ = await Dialog.ShowAsync().ConfigureAwait(true);
                                    }
                                }
                                else if (IsUnauthorized)
                                {
                                    if (Globalization.Language == LanguageEnum.Chinese)
                                    {
                                        QueueContentDialog dialog = new QueueContentDialog
                                        {
                                            Title = "错误",
                                            Content = "RX无权将文件粘贴至此处，可能是您无权访问此文件\r\r是否立即进入系统文件管理器进行相应操作？",
                                            PrimaryButtonText = "立刻",
                                            CloseButtonText = "稍后"
                                        };
                                        if (await dialog.ShowAsync().ConfigureAwait(true) == ContentDialogResult.Primary)
                                        {
                                            _ = await Launcher.LaunchFolderAsync(CurrentFolder);
                                        }
                                    }
                                    else
                                    {
                                        QueueContentDialog dialog = new QueueContentDialog
                                        {
                                            Title = "Error",
                                            Content = "RX does not have permission to paste, it may be that you do not have access to this folder\r\rEnter the system file manager immediately ？",
                                            PrimaryButtonText = "Enter",
                                            CloseButtonText = "Later"
                                        };
                                        if (await dialog.ShowAsync().ConfigureAwait(true) == ContentDialogResult.Primary)
                                        {
                                            _ = await Launcher.LaunchFolderAsync(CurrentFolder);
                                        }
                                    }
                                }
                                else if (IsSpaceError)
                                {
                                    if (Globalization.Language == LanguageEnum.Chinese)
                                    {
                                        QueueContentDialog QueueContenDialog = new QueueContentDialog
                                        {
                                            Title = "错误",
                                            Content = "因设备剩余空间大小不足，部分文件无法复制",
                                            CloseButtonText = "确定"
                                        };
                                        _ = await QueueContenDialog.ShowAsync().ConfigureAwait(true);
                                    }
                                    else
                                    {
                                        QueueContentDialog QueueContenDialog = new QueueContentDialog
                                        {
                                            Title = "Error",
                                            Content = "Some files cannot be copyed due to insufficient free space on the device",
                                            CloseButtonText = "Confirm"
                                        };
                                        _ = await QueueContenDialog.ShowAsync().ConfigureAwait(true);
                                    }
                                }

                                break;
                            }
                        case DataPackageOperation.Move:
                            {
                                await TabViewContainer.ThisPage.FFInstanceContainer[this].LoadingActivation(true, Globalization.Language == LanguageEnum.Chinese ? "正在剪切" : "Cutting").ConfigureAwait(true);

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

                                            FilePresenter.CopyAndMoveRecord.Add($"{File.Path}||Move||File||{Path.Combine(TargetFolder.Path, File.Name)}");

                                            await File.MoveAsync(TargetFolder, Item.Name, NameCollisionOption.GenerateUniqueName);
                                            TabViewContainer.ThisPage.FFInstanceContainer[this].FileCollection.Remove(TabViewContainer.ThisPage.FFInstanceContainer[this].FileCollection.FirstOrDefault((It) => It.Path == Item.Path));
                                        }
                                        else if (Item is StorageFolder Folder)
                                        {
                                            if (!await Folder.CheckExist().ConfigureAwait(true))
                                            {
                                                IsItemNotFound = true;
                                                continue;
                                            }

                                            FilePresenter.CopyAndMoveRecord.Add($"{Folder.Path}||Move||Folder||{Path.Combine(TargetFolder.Path, Folder.Name)}");

                                            StorageFolder NewFolder = await TargetFolder.CreateFolderAsync(Item.Name, CreationCollisionOption.OpenIfExists);
                                            await Folder.MoveSubFilesAndSubFoldersAsync(NewFolder).ConfigureAwait(true);
                                            await Folder.DeleteAsync(StorageDeleteOption.PermanentDelete);
                                            TabViewContainer.ThisPage.FFInstanceContainer[this].FileCollection.Remove(TabViewContainer.ThisPage.FFInstanceContainer[this].FileCollection.FirstOrDefault((It) => It.Path == Item.Path));

                                            if (!SettingControl.IsDetachTreeViewAndPresenter && ActualPath.StartsWith((FolderTree.RootNodes[0].Content as StorageFolder).Path))
                                            {
                                                await FolderTree.RootNodes[0].UpdateAllSubNode().ConfigureAwait(true);
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
                                    if (Globalization.Language == LanguageEnum.Chinese)
                                    {
                                        QueueContentDialog Dialog = new QueueContentDialog
                                        {
                                            Title = "错误",
                                            Content = "部分文件不存在，无法移动到指定位置",
                                            CloseButtonText = "确定"
                                        };
                                        _ = await Dialog.ShowAsync().ConfigureAwait(true);
                                    }
                                    else
                                    {
                                        QueueContentDialog Dialog = new QueueContentDialog
                                        {
                                            Title = "Error",
                                            Content = "Some files do not exist and cannot be moved to the specified location",
                                            CloseButtonText = "Got it"
                                        };
                                        _ = await Dialog.ShowAsync().ConfigureAwait(true);
                                    }
                                }
                                else if (IsUnauthorized)
                                {
                                    if (Globalization.Language == LanguageEnum.Chinese)
                                    {
                                        QueueContentDialog dialog = new QueueContentDialog
                                        {
                                            Title = "错误",
                                            Content = "RX无权将文件粘贴至此处，可能是您无权访问此文件\r\r是否立即进入系统文件管理器进行相应操作？",
                                            PrimaryButtonText = "立刻",
                                            CloseButtonText = "稍后"
                                        };
                                        if (await dialog.ShowAsync().ConfigureAwait(true) == ContentDialogResult.Primary)
                                        {
                                            _ = await Launcher.LaunchFolderAsync(CurrentFolder);
                                        }
                                    }
                                    else
                                    {
                                        QueueContentDialog dialog = new QueueContentDialog
                                        {
                                            Title = "Error",
                                            Content = "RX does not have permission to paste, it may be that you do not have access to this folder\r\rEnter the system file manager immediately ？",
                                            PrimaryButtonText = "Enter",
                                            CloseButtonText = "Later"
                                        };
                                        if (await dialog.ShowAsync().ConfigureAwait(true) == ContentDialogResult.Primary)
                                        {
                                            _ = await Launcher.LaunchFolderAsync(CurrentFolder);
                                        }
                                    }
                                }
                                else if (IsSpaceError)
                                {
                                    if (Globalization.Language == LanguageEnum.Chinese)
                                    {
                                        QueueContentDialog QueueContenDialog = new QueueContentDialog
                                        {
                                            Title = "错误",
                                            Content = "因设备剩余空间大小不足，部分文件无法移动",
                                            CloseButtonText = "确定"
                                        };
                                        _ = await QueueContenDialog.ShowAsync().ConfigureAwait(true);
                                    }
                                    else
                                    {
                                        QueueContentDialog QueueContenDialog = new QueueContentDialog
                                        {
                                            Title = "Error",
                                            Content = "Some files cannot be moved due to insufficient free space on the device",
                                            CloseButtonText = "Confirm"
                                        };
                                        _ = await QueueContenDialog.ShowAsync().ConfigureAwait(true);
                                    }
                                }
                                else if (IsCaptured)
                                {
                                    QueueContentDialog dialog;

                                    if (Globalization.Language == LanguageEnum.Chinese)
                                    {
                                        dialog = new QueueContentDialog
                                        {
                                            Title = "错误",
                                            Content = "部分文件正在被其他应用程序使用，因此无法移动",
                                            CloseButtonText = "确定"
                                        };
                                    }
                                    else
                                    {
                                        dialog = new QueueContentDialog
                                        {
                                            Title = "Error",
                                            Content = "Some files are in use by other applications and cannot be moved",
                                            CloseButtonText = "Got it"
                                        };
                                    }

                                    _ = await dialog.ShowAsync().ConfigureAwait(true);
                                }

                                break;
                            }
                    }
                }
                finally
                {
                    await TabViewContainer.ThisPage.FFInstanceContainer[this].LoadingActivation(false).ConfigureAwait(true);
                    e.Handled = true;
                    _ = Interlocked.Exchange(ref DropLockResource, 0);
                }
            }
        }

        private void AddressButton_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Modifiers.HasFlag(DragDropModifiers.Control))
            {
                e.AcceptedOperation = DataPackageOperation.Copy;
                e.DragUIOverride.Caption = $"复制到 {(e.OriginalSource as Button).Content}";
            }
            else
            {
                e.AcceptedOperation = DataPackageOperation.Move;
                e.DragUIOverride.Caption = $"移动到 {(e.OriginalSource as Button).Content}";
            }

            e.DragUIOverride.IsContentVisible = true;
            e.DragUIOverride.IsCaptionVisible = true;
        }

        private async void FolderTree_Holding(object sender, Windows.UI.Xaml.Input.HoldingRoutedEventArgs e)
        {
            if (e.HoldingState == Windows.UI.Input.HoldingState.Started)
            {
                if ((e.OriginalSource as FrameworkElement)?.DataContext is TreeViewNode Node)
                {
                    FolderTree.ContextFlyout = RightTabFlyout;

                    await DisplayItemsInFolder(Node).ConfigureAwait(true);

                    if (FolderTree.RootNodes.Contains(CurrentNode))
                    {
                        FolderDelete.IsEnabled = false;
                        FolderRename.IsEnabled = false;
                        FolderAdd.IsEnabled = false;
                    }
                    else
                    {
                        FolderDelete.IsEnabled = true;
                        FolderRename.IsEnabled = true;
                        FolderAdd.IsEnabled = true;
                    }
                }
                else
                {
                    FolderTree.ContextFlyout = null;
                }
            }
        }

        private void BottomCommandBar_Opening(object sender, object e)
        {
            BottomCommandBar.PrimaryCommands.Clear();
            BottomCommandBar.SecondaryCommands.Clear();

            FilePresenter Instance = TabViewContainer.ThisPage.FFInstanceContainer[this];

            if (Instance.SelectedItem is FileSystemStorageItem Item)
            {
                AppBarButton CopyButton = new AppBarButton
                {
                    Icon = new SymbolIcon(Symbol.Copy),
                    Label = Globalization.Language == LanguageEnum.Chinese ? "复制" : "Copy"
                };
                CopyButton.Click += Instance.Copy_Click;
                BottomCommandBar.PrimaryCommands.Add(CopyButton);

                AppBarButton CutButton = new AppBarButton
                {
                    Icon = new SymbolIcon(Symbol.Cut),
                    Label = Globalization.Language == LanguageEnum.Chinese ? "剪切" : "Cut"
                };
                CutButton.Click += Instance.Cut_Click;
                BottomCommandBar.PrimaryCommands.Add(CutButton);

                AppBarButton DeleteButton = new AppBarButton
                {
                    Icon = new SymbolIcon(Symbol.Delete),
                    Label = Globalization.Language == LanguageEnum.Chinese ? "删除" : "Delete"
                };
                DeleteButton.Click += Instance.Delete_Click;
                BottomCommandBar.PrimaryCommands.Add(DeleteButton);

                AppBarButton RenameButton = new AppBarButton
                {
                    Icon = new SymbolIcon(Symbol.Rename),
                    Label = Globalization.Language == LanguageEnum.Chinese ? "重命名" : "Rename"
                };
                RenameButton.Click += Instance.Rename_Click;
                BottomCommandBar.PrimaryCommands.Add(RenameButton);

                if (Item.StorageType == StorageItemTypes.File)
                {
                    AppBarButton OpenButton = new AppBarButton
                    {
                        Icon = new SymbolIcon(Symbol.OpenFile),
                        Label = Globalization.Language == LanguageEnum.Chinese ? "打开" : "Open"
                    };
                    OpenButton.Click += Instance.FileOpen_Click;
                    BottomCommandBar.SecondaryCommands.Add(OpenButton);

                    MenuFlyout OpenFlyout = new MenuFlyout();
                    MenuFlyoutItem AdminItem = new MenuFlyoutItem
                    {
                        Icon = new SymbolIcon(Symbol.Admin),
                        Text = Globalization.Language == LanguageEnum.Chinese ? "以管理员权限打开" : "Open as administrator",
                        IsEnabled = Instance.RunWithSystemAuthority.IsEnabled
                    };
                    AdminItem.Click += Instance.RunWithSystemAuthority_Click;
                    OpenFlyout.Items.Add(AdminItem);

                    MenuFlyoutItem OtherItem = new MenuFlyoutItem
                    {
                        Icon = new SymbolIcon(Symbol.SwitchApps),
                        Text = Globalization.Language == LanguageEnum.Chinese ? "选择其他应用" : "Choose another app",
                        IsEnabled = Instance.ChooseOtherApp.IsEnabled
                    };
                    OtherItem.Click += Instance.ChooseOtherApp_Click;
                    OpenFlyout.Items.Add(OtherItem);

                    BottomCommandBar.SecondaryCommands.Add(new AppBarButton
                    {
                        Icon = new SymbolIcon(Symbol.AllApps),
                        Label = Globalization.Language == LanguageEnum.Chinese ? "打开方式..." : "Open with...",
                        Flyout = OpenFlyout
                    });

                    BottomCommandBar.SecondaryCommands.Add(new AppBarSeparator());

                    MenuFlyout EditFlyout = new MenuFlyout();
                    MenuFlyoutItem MontageItem = new MenuFlyoutItem
                    {
                        Icon = new FontIcon { Glyph = "\uE177" },
                        Text = Globalization.Language == LanguageEnum.Chinese ? "剪辑" : "Montage",
                        IsEnabled = Instance.VideoEdit.IsEnabled
                    };
                    MontageItem.Click += Instance.VideoEdit_Click;
                    EditFlyout.Items.Add(MontageItem);

                    MenuFlyoutItem MergeItem = new MenuFlyoutItem
                    {
                        Icon = new FontIcon { Glyph = "\uE11E" },
                        Text = Globalization.Language == LanguageEnum.Chinese ? "合并" : "Merge",
                        IsEnabled = Instance.VideoMerge.IsEnabled
                    };
                    MergeItem.Click += Instance.VideoMerge_Click;
                    EditFlyout.Items.Add(MergeItem);

                    MenuFlyoutItem TranscodeItem = new MenuFlyoutItem
                    {
                        Icon = new FontIcon { Glyph = "\uE1CA" },
                        Text = Globalization.Language == LanguageEnum.Chinese ? "转码" : "Transcode",
                        IsEnabled = Instance.Transcode.IsEnabled
                    };
                    TranscodeItem.Click += Instance.Transcode_Click;
                    EditFlyout.Items.Add(TranscodeItem);

                    BottomCommandBar.SecondaryCommands.Add(new AppBarButton
                    {
                        Icon = new SymbolIcon(Symbol.Edit),
                        Label = Globalization.Language == LanguageEnum.Chinese ? "编辑" : "Edit",
                        Flyout = EditFlyout
                    });

                    MenuFlyout ShareFlyout = new MenuFlyout();
                    MenuFlyoutItem SystemShareItem = new MenuFlyoutItem
                    {
                        Icon = new SymbolIcon(Symbol.Share),
                        Text = Globalization.Language == LanguageEnum.Chinese ? "系统共享" : "System share"
                    };
                    SystemShareItem.Click += Instance.SystemShare_Click;
                    ShareFlyout.Items.Add(SystemShareItem);

                    MenuFlyoutItem WIFIShareItem = new MenuFlyoutItem
                    {
                        Icon = new FontIcon { Glyph = "\uE701" },
                        Text = Globalization.Language == LanguageEnum.Chinese ? "WIFI共享" : "WIFI"
                    };
                    WIFIShareItem.Click += Instance.WIFIShare_Click;
                    ShareFlyout.Items.Add(WIFIShareItem);

                    MenuFlyoutItem BluetoothShare = new MenuFlyoutItem
                    {
                        Icon = new FontIcon { Glyph = "\uE702" },
                        Text = Globalization.Language == LanguageEnum.Chinese ? "蓝牙共享" : "Bluetooth"
                    };
                    BluetoothShare.Click += Instance.BluetoothShare_Click;
                    ShareFlyout.Items.Add(BluetoothShare);

                    BottomCommandBar.SecondaryCommands.Add(new AppBarButton
                    {
                        Icon = new SymbolIcon(Symbol.Share),
                        Label = Globalization.Language == LanguageEnum.Chinese ? "共享" : "Share",
                        Flyout = ShareFlyout
                    });

                    AppBarButton CompressionButton = new AppBarButton
                    {
                        Icon = new SymbolIcon(Symbol.Bookmarks),
                        Label = Instance.Zip.Label,
                    };
                    CompressionButton.Click += Instance.Zip_Click;
                    BottomCommandBar.SecondaryCommands.Add(CompressionButton);

                    BottomCommandBar.SecondaryCommands.Add(new AppBarSeparator());

                    AppBarButton PropertyButton = new AppBarButton
                    {
                        Icon = new SymbolIcon(Symbol.Tag),
                        Label = Globalization.Language == LanguageEnum.Chinese ? "属性" : "Property",
                    };
                    PropertyButton.Click += Instance.Attribute_Click;
                    BottomCommandBar.SecondaryCommands.Add(PropertyButton);
                }
                else
                {
                    AppBarButton OpenButton = new AppBarButton
                    {
                        Icon = new SymbolIcon(Symbol.BackToWindow),
                        Label = Globalization.Language == LanguageEnum.Chinese ? "打开" : "Open",
                    };
                    OpenButton.Click += Instance.FolderOpen_Click;
                    BottomCommandBar.SecondaryCommands.Add(OpenButton);

                    AppBarButton CompressionButton = new AppBarButton
                    {
                        Icon = new SymbolIcon(Symbol.Bookmarks),
                        Label = Globalization.Language == LanguageEnum.Chinese ? "压缩" : "Compression",
                    };
                    CompressionButton.Click += Instance.CompressFolder_Click;
                    BottomCommandBar.SecondaryCommands.Add(CompressionButton);

                    AppBarButton PinButton = new AppBarButton
                    {
                        Icon = new SymbolIcon(Symbol.Add),
                        Label = Globalization.Language == LanguageEnum.Chinese ? "固定至主界面" : "Pin to homepage"
                    };
                    PinButton.Click += Instance.AddToLibray_Click;
                    BottomCommandBar.SecondaryCommands.Add(PinButton);

                    AppBarButton PropertyButton = new AppBarButton
                    {
                        Icon = new SymbolIcon(Symbol.Tag),
                        Label = Globalization.Language == LanguageEnum.Chinese ? "属性" : "Property",
                    };
                    PropertyButton.Click += Instance.FolderProperty_Click;
                    BottomCommandBar.SecondaryCommands.Add(PropertyButton);
                }
            }
            else
            {
                AppBarButton PasteButton = new AppBarButton
                {
                    Icon = new SymbolIcon(Symbol.Paste),
                    Label = Globalization.Language == LanguageEnum.Chinese ? "粘贴" : "Paste"
                };
                PasteButton.Click += Instance.Paste_Click;
                BottomCommandBar.PrimaryCommands.Add(PasteButton);

                AppBarButton RefreshButton = new AppBarButton
                {
                    Icon = new SymbolIcon(Symbol.Refresh),
                    Label = Globalization.Language == LanguageEnum.Chinese ? "刷新" : "Refresh"
                };
                RefreshButton.Click += Instance.Refresh_Click;
                BottomCommandBar.PrimaryCommands.Add(RefreshButton);

                AppBarButton WinExButton = new AppBarButton
                {
                    Icon = new SymbolIcon(Symbol.BackToWindow),
                    Label = Globalization.Language == LanguageEnum.Chinese ? "使用系统管理器打开" : "Open in Windows Explorer"
                };
                WinExButton.Click += Instance.UseSystemFileMananger_Click;
                BottomCommandBar.PrimaryCommands.Add(WinExButton);

                MenuFlyout NewFlyout = new MenuFlyout();
                MenuFlyoutItem CreateFileItem = new MenuFlyoutItem
                {
                    Icon = new SymbolIcon(Symbol.Page2),
                    Text = Globalization.Language == LanguageEnum.Chinese ? "新建文件" : "Create file",
                    MinWidth = 150
                };
                CreateFileItem.Click += Instance.CreateFile_Click;
                NewFlyout.Items.Add(CreateFileItem);

                MenuFlyoutItem CreateFolder = new MenuFlyoutItem
                {
                    Icon = new SymbolIcon(Symbol.NewFolder),
                    Text = Globalization.Language == LanguageEnum.Chinese ? "新建文件夹" : "Create folder",
                    MinWidth = 150
                };
                CreateFolder.Click += Instance.CreateFolder_Click;
                NewFlyout.Items.Add(CreateFolder);

                BottomCommandBar.SecondaryCommands.Add(new AppBarButton
                {
                    Icon = new SymbolIcon(Symbol.Add),
                    Label = Globalization.Language == LanguageEnum.Chinese ? "新建" : "Create",
                    Flyout = NewFlyout
                });

                BottomCommandBar.SecondaryCommands.Add(new AppBarSeparator());

                AppBarButton PropertyButton = new AppBarButton
                {
                    Icon = new SymbolIcon(Symbol.Tag),
                    Label = Globalization.Language == LanguageEnum.Chinese ? "属性" : "Property",
                };
                PropertyButton.Click += Instance.ParentProperty_Click;
                BottomCommandBar.SecondaryCommands.Add(PropertyButton);
            }
        }
    }

}
