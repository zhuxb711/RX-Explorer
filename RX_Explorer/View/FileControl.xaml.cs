using Microsoft.Toolkit.Uwp.UI.Animations;
using RX_Explorer.Class;
using RX_Explorer.Dialog;
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

namespace RX_Explorer
{
    public sealed partial class FileControl : Page, IDisposable
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
                FolderTree.SelectNode(value);

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

                    GlobeSearch.PlaceholderText = $"{Globalization.GetString("SearchBox_PlaceholderText")} {PlaceText}";

                    GoParentFolder.IsEnabled = !FolderTree.RootNodes.Contains(value);
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

                        GlobeSearch.PlaceholderText = $"{Globalization.GetString("SearchBox_PlaceholderText")} {PlaceText}";

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

        private int RecordIndex
        {
            get
            {
                return recordIndex;
            }
            set
            {
                lock (SyncRootProvider.SyncRoot)
                {
                    recordIndex = value;
                }
            }
        }

        public bool IsSearchOrPathBoxFocused { get; set; } = false;

        private CancellationTokenSource ExpandCancel = new CancellationTokenSource();
        private List<StorageFolder> GoAndBackRecord = new List<StorageFolder>();
        private ObservableCollection<AddressBlock> AddressButtonList = new ObservableCollection<AddressBlock>();
        private ObservableCollection<string> AddressExtentionList = new ObservableCollection<string>();
        private volatile int recordIndex = 0;
        private bool IsBackOrForwardAction = false;
        private SemaphoreSlim DisplayLock = new SemaphoreSlim(1);
        private Microsoft.UI.Xaml.Controls.TabViewItem TabItem;

        public FileControl()
        {
            InitializeComponent();

            try
            {
                if (AnimationController.Current.IsEnableAnimation)
                {
                    Nav.Navigate(typeof(FilePresenter), this, new DrillInNavigationTransitionInfo());
                }
                else
                {
                    Nav.Navigate(typeof(FilePresenter), this, new SuppressNavigationTransitionInfo());
                }

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
                QueueContentDialog dialog = new QueueContentDialog
                {
                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                    Content = Globalization.GetString("QueueDialog_LocateFolderFailure_Content"),
                    CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                };
                _ = await dialog.ShowAsync().ConfigureAwait(true);
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

                GlobeSearch.PlaceholderText = $"{Globalization.GetString("SearchBox_PlaceholderText")} {PlaceText}";

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

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            while (Nav.CanGoBack)
            {
                Nav.GoBack();
            }

            AddressButtonList.Clear();
            ExpandCancel?.Cancel();

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
                FolderTree.RootNodes.Clear();
                TreeViewNode RootNode = new TreeViewNode
                {
                    Content = InitFolder,
                    IsExpanded = false,
                    HasUnrealizedChildren = WIN_Native_API.CheckContainsAnyItem(InitFolder.Path, ItemFilters.Folder)
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
                ExpandCancel = new CancellationTokenSource();

                StorageFolder folder = Node.Content as StorageFolder;

                try
                {
                    if (WIN_Native_API.CheckContainsAnyItem(folder.Path, ItemFilters.Folder))
                    {
                        await foreach (StorageFolder SubFolder in WIN_Native_API.GetStorageItemsWithInnerContent(folder, ItemFilters.Folder))
                        {
                            TreeViewNode NewNode = new TreeViewNode
                            {
                                Content = SubFolder,
                                HasUnrealizedChildren = WIN_Native_API.CheckContainsAnyItem(SubFolder.Path, ItemFilters.Folder)
                            };

                            Node.Children.Add(NewNode);

                            if (ExpandCancel.IsCancellationRequested)
                            {
                                break;
                            }
                        }

                        if (!ExpandCancel.IsCancellationRequested)
                        {
                            Node.HasUnrealizedChildren = false;
                        }
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
                    if (ExpandCancel.IsCancellationRequested)
                    {
                        Node.Children.Clear();
                    }

                    ExpandCancel.Dispose();
                    ExpandCancel = null;
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

            await DisplayLock.WaitAsync().ConfigureAwait(true);

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
                            return;
                        }
                    }

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

                    List<FileSystemStorageItem> ItemList = WIN_Native_API.GetStorageItems(Folder, ItemFilters.File | ItemFilters.Folder).SortList(SortTarget.Name, SortDirection.Ascending);

                    Presenter.HasFile.Visibility = ItemList.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

                    for (int i = 0; i < ItemList.Count; i++)
                    {
                        TabViewContainer.ThisPage.FFInstanceContainer[this].FileCollection.Add(ItemList[i]);
                    }
                }
            }
            catch (Exception ex)
            {
                ExceptionTracer.RequestBlueScreen(ex);
            }
            finally
            {
                DisplayLock.Release();
            }
        }

        public async Task DisplayItemsInFolder(StorageFolder Folder, bool ForceRefresh = false)
        {
            if (Folder == null)
            {
                throw new ArgumentNullException(nameof(Folder), "Parameter could not be null");
            }

            await DisplayLock.WaitAsync().ConfigureAwait(true);

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
                        return;
                    }
                }

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

                List<FileSystemStorageItem> ItemList = WIN_Native_API.GetStorageItems(Folder, ItemFilters.File | ItemFilters.Folder).SortList(SortTarget.Name, SortDirection.Ascending);

                Presenter.HasFile.Visibility = ItemList.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

                for (int i = 0; i < ItemList.Count; i++)
                {
                    Presenter.FileCollection.Add(ItemList[i]);
                }
            }
            catch (Exception ex)
            {
                ExceptionTracer.RequestBlueScreen(ex);
            }
            finally
            {
                DisplayLock.Release();
            }
        }

        private async void FolderDelete_Click(object sender, RoutedEventArgs e)
        {
            if (CurrentNode == null)
            {
                return;
            }

            DeleteDialog QueueContenDialog = new DeleteDialog(Globalization.GetString("QueueDialog_DeleteFolder_Content"));

            if (await QueueContenDialog.ShowAsync().ConfigureAwait(true) == ContentDialogResult.Primary)
            {
                try
                {
                    await FullTrustExcutorController.Current.DeleteAsync(CurrentFolder, QueueContenDialog.IsPermanentDelete).ConfigureAwait(true);

                    TabViewContainer.ThisPage.FFInstanceContainer[this].FileCollection.Remove(TabViewContainer.ThisPage.FFInstanceContainer[this].FileCollection.Where((Item) => Item.Path == CurrentFolder.Path).FirstOrDefault());

                    await DisplayItemsInFolder(CurrentNode.Parent).ConfigureAwait(true);

                    await FolderTree.RootNodes[0].UpdateAllSubNode().ConfigureAwait(true);
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

                    await DisplayItemsInFolder(CurrentNode, true).ConfigureAwait(false);
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
            }
        }

        private void FolderTree_Collapsed(TreeView sender, TreeViewCollapsedEventArgs args)
        {
            ExpandCancel?.Cancel();

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
                QueueContentDialog dialog = new QueueContentDialog
                {
                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                    Content = Globalization.GetString("QueueDialog_LocateFolderFailure_Content"),
                    CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                };
                _ = await dialog.ShowAsync().ConfigureAwait(true);

                await DisplayItemsInFolder(CurrentNode, true).ConfigureAwait(false);

                return;
            }

            RenameDialog renameDialog = new RenameDialog(CurrentFolder);
            if (await renameDialog.ShowAsync().ConfigureAwait(true) == ContentDialogResult.Primary)
            {
                if (string.IsNullOrEmpty(renameDialog.DesireName))
                {
                    QueueContentDialog content = new QueueContentDialog
                    {
                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                        Content = Globalization.GetString("QueueDialog_EmptyFolderName_Content"),
                        CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                    };
                    _ = await content.ShowAsync().ConfigureAwait(true);
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
                    QueueContentDialog dialog = new QueueContentDialog
                    {
                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                        Content = Globalization.GetString("QueueDialog_UnauthorizedRenameFolder_Content"),
                        PrimaryButtonText = Globalization.GetString("Common_Dialog_NowButton"),
                        CloseButtonText = Globalization.GetString("Common_Dialog_LaterButton"),
                    };

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
                StorageFolder NewFolder = await CurrentFolder.CreateFolderAsync(Globalization.GetString("Create_NewFolder_Admin_Name"), CreationCollisionOption.GenerateUniqueName);

                TabViewContainer.ThisPage.FFInstanceContainer[this].FileCollection.Insert(0, new FileSystemStorageItem(NewFolder, await NewFolder.GetModifiedTimeAsync().ConfigureAwait(true)));

                await FolderTree.RootNodes[0].UpdateAllSubNode().ConfigureAwait(true);
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
            if (!await CurrentFolder.CheckExist().ConfigureAwait(true))
            {
                QueueContentDialog dialog = new QueueContentDialog
                {
                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                    Content = Globalization.GetString("QueueDialog_LocateFolderFailure_Content"),
                    CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                };
                _ = await dialog.ShowAsync().ConfigureAwait(true);

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
                QueueContentDialog dialog = new QueueContentDialog
                {
                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                    Content = Globalization.GetString("QueueDialog_LocateFolderFailure_Content"),
                    CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                };
                _ = await dialog.ShowAsync().ConfigureAwait(true);

                await DisplayItemsInFolder(CurrentNode, true).ConfigureAwait(false);
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
                BitmapImage Thumbnail = await folder.GetThumbnailBitmapAsync().ConfigureAwait(true);
                TabViewContainer.ThisPage.LibraryFolderList.Add(new LibraryFolder(folder, Thumbnail));
                await SQLite.Current.SetLibraryPathAsync(folder.Path, LibraryType.UserCustom).ConfigureAwait(false);
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

                    if (AnimationController.Current.IsEnableAnimation)
                    {
                        Nav.Navigate(typeof(SearchPage), new Tuple<FileControl, StorageItemQueryResult>(this, FileQuery), new DrillInNavigationTransitionInfo());
                    }
                    else
                    {
                        Nav.Navigate(typeof(SearchPage), new Tuple<FileControl, StorageItemQueryResult>(this, FileQuery), new SuppressNavigationTransitionInfo());
                    }
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

            if (string.Equals(QueryText, "Powershell", StringComparison.OrdinalIgnoreCase) || string.Equals(QueryText, "Powershell.exe", StringComparison.OrdinalIgnoreCase))
            {
                string ExcutePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "WindowsPowerShell\\v1.0\\powershell.exe");
                await FullTrustExcutorController.Current.RunAsAdministratorAsync(ExcutePath, $"-NoExit -Command \"Set-Location '{CurrentFolder.Path}'\"").ConfigureAwait(false);
                return;
            }

            if (string.Equals(QueryText, "Cmd", StringComparison.OrdinalIgnoreCase) || string.Equals(QueryText, "Cmd.exe", StringComparison.OrdinalIgnoreCase))
            {
                string ExcutePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe");
                await FullTrustExcutorController.Current.RunAsAdministratorAsync(ExcutePath, $"/k cd /d {CurrentFolder.Path}").ConfigureAwait(false);
                return;
            }

            if (string.Equals(QueryText, "Wt", StringComparison.OrdinalIgnoreCase) || string.Equals(QueryText, "Wt.exe", StringComparison.OrdinalIgnoreCase))
            {
                LaunchQuerySupportStatus CheckResult = await Launcher.QueryUriSupportAsync(new Uri("ms-windows-store:"), LaunchQuerySupportType.Uri, "Microsoft.WindowsTerminal_8wekyb3d8bbwe");
                switch (CheckResult)
                {
                    case LaunchQuerySupportStatus.Available:
                    case LaunchQuerySupportStatus.NotSupported:
                        {
                            await FullTrustExcutorController.Current.RunAsync("explorer.exe", @"shell:AppsFolder\Microsoft.WindowsTerminal_8wekyb3d8bbwe!App").ConfigureAwait(false);
                            return;
                        }
                }
            }

            string ProtentialPath1 = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), QueryText.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? QueryText.ToLower() : $"{QueryText.ToLower()}.exe");
            string ProtentialPath2 = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), QueryText.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? QueryText.ToLower() : $"{QueryText.ToLower()}.exe");
            string ProtentialPath3 = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.SystemX86), QueryText.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? QueryText.ToLower() : $"{QueryText.ToLower()}.exe");

            if (WIN_Native_API.CheckExist(ProtentialPath1))
            {
                await FullTrustExcutorController.Current.RunAsAdministratorAsync(ProtentialPath1, string.Empty).ConfigureAwait(false);
                return;
            }
            else if (WIN_Native_API.CheckExist(ProtentialPath2))
            {
                await FullTrustExcutorController.Current.RunAsAdministratorAsync(ProtentialPath2, string.Empty).ConfigureAwait(false);
                return;
            }
            else if (WIN_Native_API.CheckExist(ProtentialPath3))
            {
                await FullTrustExcutorController.Current.RunAsAdministratorAsync(ProtentialPath3, string.Empty).ConfigureAwait(false);
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
                    QueueContentDialog dialog = new QueueContentDialog
                    {
                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                        Content = $"{Globalization.GetString("QueueDialog_LocatePathFailure_Content")} \r\"{QueryText}\"",
                        CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton"),
                    };
                    _ = await dialog.ShowAsync().ConfigureAwait(true);
                }
            }
        }

        private void AddressBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
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
                            if (args.CheckCurrent())
                            {
                                sender.ItemsSource = WIN_Native_API.GetStorageItems(DirectoryName, ItemFilters.Folder).Where((Item) => Item.Name.StartsWith(Path.GetFileName(sender.Text), StringComparison.OrdinalIgnoreCase)).Select((It) => It.Path);
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
                if (RecordIndex == 0)
                {
                    _ = Interlocked.Exchange(ref NavigateLockResource, 0);
                    return;
                }

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
                                QueueContentDialog dialog = new QueueContentDialog
                                {
                                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                    Content = Globalization.GetString("QueueDialog_LocateFolderFailure_Content"),
                                    CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                                };
                                _ = await dialog.ShowAsync().ConfigureAwait(true);
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
                                QueueContentDialog dialog = new QueueContentDialog
                                {
                                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                    Content = Globalization.GetString("QueueDialog_LocateFolderFailure_Content"),
                                    CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                                };
                                _ = await dialog.ShowAsync().ConfigureAwait(true);
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

                        TabViewContainer.ThisPage.FFInstanceContainer[this].ItemPresenter = TabViewContainer.ThisPage.FFInstanceContainer[this].GridViewControl;
                        break;
                    }
                case 1:
                    {
                        TabViewContainer.ThisPage.FFInstanceContainer[this].ListHeader.ContentTemplate = TabViewContainer.ThisPage.FFInstanceContainer[this].ListHeaderDataTemplate;
                        TabViewContainer.ThisPage.FFInstanceContainer[this].ListViewControl.ItemTemplate = TabViewContainer.ThisPage.FFInstanceContainer[this].ListViewDetailDataTemplate;
                        TabViewContainer.ThisPage.FFInstanceContainer[this].ListViewControl.ItemsSource = TabViewContainer.ThisPage.FFInstanceContainer[this].FileCollection;

                        TabViewContainer.ThisPage.FFInstanceContainer[this].ItemPresenter = TabViewContainer.ThisPage.FFInstanceContainer[this].ListViewControl;
                        break;
                    }

                case 2:
                    {
                        TabViewContainer.ThisPage.FFInstanceContainer[this].ListHeader.ContentTemplate = null;
                        TabViewContainer.ThisPage.FFInstanceContainer[this].ListViewControl.ItemTemplate = TabViewContainer.ThisPage.FFInstanceContainer[this].ListViewSimpleDataTemplate;
                        TabViewContainer.ThisPage.FFInstanceContainer[this].ListViewControl.ItemsSource = TabViewContainer.ThisPage.FFInstanceContainer[this].FileCollection;

                        TabViewContainer.ThisPage.FFInstanceContainer[this].ItemPresenter = TabViewContainer.ThisPage.FFInstanceContainer[this].ListViewControl;
                        break;
                    }
                case 3:
                    {
                        TabViewContainer.ThisPage.FFInstanceContainer[this].GridViewControl.ItemTemplate = TabViewContainer.ThisPage.FFInstanceContainer[this].LargeImageDataTemplate;

                        TabViewContainer.ThisPage.FFInstanceContainer[this].ItemPresenter = TabViewContainer.ThisPage.FFInstanceContainer[this].GridViewControl;
                        break;
                    }
                case 4:
                    {
                        TabViewContainer.ThisPage.FFInstanceContainer[this].GridViewControl.ItemTemplate = TabViewContainer.ThisPage.FFInstanceContainer[this].MediumImageDataTemplate;

                        TabViewContainer.ThisPage.FFInstanceContainer[this].ItemPresenter = TabViewContainer.ThisPage.FFInstanceContainer[this].GridViewControl;
                        break;
                    }
                case 5:
                    {
                        TabViewContainer.ThisPage.FFInstanceContainer[this].GridViewControl.ItemTemplate = TabViewContainer.ThisPage.FFInstanceContainer[this].SmallImageDataTemplate;

                        TabViewContainer.ThisPage.FFInstanceContainer[this].ItemPresenter = TabViewContainer.ThisPage.FFInstanceContainer[this].GridViewControl;
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
                    QueueContentDialog dialog = new QueueContentDialog
                    {
                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                        Content = $"{Globalization.GetString("QueueDialog_LocatePathFailure_Content")} \r\"{ActualString}\"",
                        CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton"),
                    };
                    _ = await dialog.ShowAsync().ConfigureAwait(true);
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
                        QueueContentDialog dialog = new QueueContentDialog
                        {
                            Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                            Content = $"{Globalization.GetString("QueueDialog_LocatePathFailure_Content")} \r\"{ActualString}\"",
                            CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton"),
                        };
                        _ = await dialog.ShowAsync().ConfigureAwait(true);
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
                        QueueContentDialog dialog = new QueueContentDialog
                        {
                            Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                            Content = $"{Globalization.GetString("QueueDialog_LocatePathFailure_Content")} \r\"{ActualString}\"",
                            CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton"),
                        };
                        _ = await dialog.ShowAsync().ConfigureAwait(true);
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

            List<FileSystemStorageItem> ItemList = WIN_Native_API.GetStorageItems(ActualString, ItemFilters.Folder);

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
                        QueueContentDialog dialog = new QueueContentDialog
                        {
                            Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                            Content = $"{Globalization.GetString("QueueDialog_LocatePathFailure_Content")} \r\"{TargetPath}\"",
                            CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton"),
                        };
                        _ = await dialog.ShowAsync().ConfigureAwait(true);
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
                            QueueContentDialog dialog = new QueueContentDialog
                            {
                                Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                Content = $"{Globalization.GetString("QueueDialog_LocatePathFailure_Content")} \r\"{TargetPath}\"",
                                CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton"),
                            };
                            _ = await dialog.ShowAsync().ConfigureAwait(true);
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
                        QueueContentDialog Dialog = new QueueContentDialog
                        {
                            Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                            Content = Globalization.GetString("QueueDialog_SameFolder_Content"),
                            CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                        };
                        _ = await Dialog.ShowAsync().ConfigureAwait(true);

                        return;
                    }

                    TabViewContainer.CopyAndMoveRecord.Clear();

                    List<IStorageItem> DragItemList = (await e.DataView.GetStorageItemsAsync()).ToList();

                    StorageFolder TargetFolder = await StorageFolder.GetFolderFromPathAsync(ActualPath);

                    switch (e.AcceptedOperation)
                    {
                        case DataPackageOperation.Copy:
                            {
                                await TabViewContainer.ThisPage.FFInstanceContainer[this].LoadingActivation(true, Globalization.GetString("Progress_Tip_Copying")).ConfigureAwait(true);

                                bool IsItemNotFound = false;
                                bool IsUnauthorized = false;

                                foreach (IStorageItem Item in DragItemList)
                                {
                                    try
                                    {
                                        if (Item is StorageFile File)
                                        {
                                            TabViewContainer.CopyAndMoveRecord.Add($"{File.Path}||Copy||File||{Path.Combine(TargetFolder.Path, File.Name)}");

                                            await FullTrustExcutorController.Current.CopyAsync(File, TargetFolder).ConfigureAwait(true);
                                        }
                                        else if (Item is StorageFolder Folder)
                                        {
                                            TabViewContainer.CopyAndMoveRecord.Add($"{Folder.Path}||Copy||Folder||{Path.Combine(TargetFolder.Path, Folder.Name)}");

                                            await FullTrustExcutorController.Current.CopyAsync(Folder, TargetFolder).ConfigureAwait(true);

                                            if (!SettingControl.IsDetachTreeViewAndPresenter && ActualPath.StartsWith((FolderTree.RootNodes[0].Content as StorageFolder).Path))
                                            {
                                                await FolderTree.RootNodes[0].UpdateAllSubNode().ConfigureAwait(true);
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
                                        break;
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
                                        _ = await Launcher.LaunchFolderAsync(CurrentFolder);
                                    }
                                }

                                break;
                            }
                        case DataPackageOperation.Move:
                            {
                                await TabViewContainer.ThisPage.FFInstanceContainer[this].LoadingActivation(true, Globalization.GetString("Progress_Tip_Moving")).ConfigureAwait(true);

                                bool IsItemNotFound = false;
                                bool IsUnauthorized = false;
                                bool IsCaptured = false;

                                foreach (IStorageItem Item in DragItemList)
                                {
                                    try
                                    {
                                        if (Item is StorageFile File)
                                        {
                                            TabViewContainer.CopyAndMoveRecord.Add($"{File.Path}||Move||File||{Path.Combine(TargetFolder.Path, File.Name)}");
                                            await FullTrustExcutorController.Current.MoveAsync(File, TargetFolder).ConfigureAwait(true);
                                            TabViewContainer.ThisPage.FFInstanceContainer[this].FileCollection.Remove(TabViewContainer.ThisPage.FFInstanceContainer[this].FileCollection.FirstOrDefault((It) => It.Path == Item.Path));
                                        }
                                        else if (Item is StorageFolder Folder)
                                        {
                                            TabViewContainer.CopyAndMoveRecord.Add($"{Folder.Path}||Move||Folder||{Path.Combine(TargetFolder.Path, Folder.Name)}");

                                            await FullTrustExcutorController.Current.MoveAsync(Folder, TargetFolder).ConfigureAwait(true);

                                            TabViewContainer.ThisPage.FFInstanceContainer[this].FileCollection.Remove(TabViewContainer.ThisPage.FFInstanceContainer[this].FileCollection.FirstOrDefault((It) => It.Path == Item.Path));

                                            if (!SettingControl.IsDetachTreeViewAndPresenter && ActualPath.StartsWith((FolderTree.RootNodes[0].Content as StorageFolder).Path))
                                            {
                                                await FolderTree.RootNodes[0].UpdateAllSubNode().ConfigureAwait(true);
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
                                        _ = await Launcher.LaunchFolderAsync(CurrentFolder);
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
                    await TabViewContainer.ThisPage.FFInstanceContainer[this].LoadingActivation(false).ConfigureAwait(true);
                    e.Handled = true;
                    _ = Interlocked.Exchange(ref DropLockResource, 0);
                }
            }
        }

        private void AddressButton_DragEnter(object sender, DragEventArgs e)
        {
            if (e.DataView.Contains(StandardDataFormats.StorageItems))
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

            if (Instance.SelectedItems.Count > 1)
            {
                AppBarButton CopyButton = new AppBarButton
                {
                    Icon = new SymbolIcon(Symbol.Copy),
                    Label = Globalization.GetString("Operate_Text_Copy")
                };
                CopyButton.Click += Instance.Copy_Click;
                BottomCommandBar.PrimaryCommands.Add(CopyButton);

                AppBarButton CutButton = new AppBarButton
                {
                    Icon = new SymbolIcon(Symbol.Cut),
                    Label = Globalization.GetString("Operate_Text_Cut")
                };
                CutButton.Click += Instance.Cut_Click;
                BottomCommandBar.PrimaryCommands.Add(CutButton);

                AppBarButton DeleteButton = new AppBarButton
                {
                    Icon = new SymbolIcon(Symbol.Delete),
                    Label = Globalization.GetString("Operate_Text_Delete")
                };
                DeleteButton.Click += Instance.Delete_Click;
                BottomCommandBar.PrimaryCommands.Add(DeleteButton);

                bool EnableMixZipButton = true;
                string MixZipButtonText = Globalization.GetString("Operate_Text_Compression");

                if (Instance.SelectedItems.Any((Item) => Item.StorageType != StorageItemTypes.Folder))
                {
                    if (Instance.SelectedItems.All((Item) => Item.StorageType == StorageItemTypes.File))
                    {
                        if (Instance.SelectedItems.All((Item) => Item.Type == ".zip"))
                        {
                            MixZipButtonText = Globalization.GetString("Operate_Text_Decompression");
                        }
                        else if (Instance.SelectedItems.All((Item) => Item.Type != ".zip"))
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
                        if (Instance.SelectedItems.Where((It) => It.StorageType == StorageItemTypes.File).Any((Item) => Item.Type == ".zip"))
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
                CompressionButton.Click += Instance.MixZip_Click;
                BottomCommandBar.SecondaryCommands.Add(CompressionButton);
            }
            else
            {
                if (Instance.SelectedItem is FileSystemStorageItem Item)
                {
                    AppBarButton CopyButton = new AppBarButton
                    {
                        Icon = new SymbolIcon(Symbol.Copy),
                        Label = Globalization.GetString("Operate_Text_Copy")
                    };
                    CopyButton.Click += Instance.Copy_Click;
                    BottomCommandBar.PrimaryCommands.Add(CopyButton);

                    AppBarButton CutButton = new AppBarButton
                    {
                        Icon = new SymbolIcon(Symbol.Cut),
                        Label = Globalization.GetString("Operate_Text_Cut")
                    };
                    CutButton.Click += Instance.Cut_Click;
                    BottomCommandBar.PrimaryCommands.Add(CutButton);

                    AppBarButton DeleteButton = new AppBarButton
                    {
                        Icon = new SymbolIcon(Symbol.Delete),
                        Label = Globalization.GetString("Operate_Text_Delete")
                    };
                    DeleteButton.Click += Instance.Delete_Click;
                    BottomCommandBar.PrimaryCommands.Add(DeleteButton);

                    AppBarButton RenameButton = new AppBarButton
                    {
                        Icon = new SymbolIcon(Symbol.Rename),
                        Label = Globalization.GetString("Operate_Text_Rename")
                    };
                    RenameButton.Click += Instance.Rename_Click;
                    BottomCommandBar.PrimaryCommands.Add(RenameButton);

                    if (Item.StorageType == StorageItemTypes.File)
                    {
                        AppBarButton OpenButton = new AppBarButton
                        {
                            Icon = new SymbolIcon(Symbol.OpenFile),
                            Label = Globalization.GetString("Operate_Text_Open")
                        };
                        OpenButton.Click += Instance.FileOpen_Click;
                        BottomCommandBar.SecondaryCommands.Add(OpenButton);

                        MenuFlyout OpenFlyout = new MenuFlyout();
                        MenuFlyoutItem AdminItem = new MenuFlyoutItem
                        {
                            Icon = new SymbolIcon(Symbol.Admin),
                            Text = Globalization.GetString("Operate_Text_OpenAsAdministrator"),
                            IsEnabled = Instance.RunWithSystemAuthority.IsEnabled
                        };
                        AdminItem.Click += Instance.RunWithSystemAuthority_Click;
                        OpenFlyout.Items.Add(AdminItem);

                        MenuFlyoutItem OtherItem = new MenuFlyoutItem
                        {
                            Icon = new SymbolIcon(Symbol.SwitchApps),
                            Text = Globalization.GetString("Operate_Text_ChooseAnotherApp"),
                            IsEnabled = Instance.ChooseOtherApp.IsEnabled
                        };
                        OtherItem.Click += Instance.ChooseOtherApp_Click;
                        OpenFlyout.Items.Add(OtherItem);

                        BottomCommandBar.SecondaryCommands.Add(new AppBarButton
                        {
                            Icon = new SymbolIcon(Symbol.AllApps),
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
                        UnLock.Click += Instance.TryUnlock_Click;
                        ToolFlyout.Items.Add(UnLock);

                        MenuFlyoutItem Hash = new MenuFlyoutItem
                        {
                            Icon = new FontIcon { Glyph = "\uE2B2" },
                            Text = Globalization.GetString("Operate_Text_ComputeHash")
                        };
                        Hash.Click += Instance.CalculateHash_Click;
                        ToolFlyout.Items.Add(Hash);

                        BottomCommandBar.SecondaryCommands.Add(new AppBarButton
                        {
                            Icon = new FontIcon { Glyph = "\uE90F" },
                            Label = Globalization.GetString("Operate_Text_Tool"),
                            Flyout = ToolFlyout
                        });

                        MenuFlyout EditFlyout = new MenuFlyout();
                        MenuFlyoutItem MontageItem = new MenuFlyoutItem
                        {
                            Icon = new FontIcon { Glyph = "\uE177" },
                            Text = Globalization.GetString("Operate_Text_Montage"),
                            IsEnabled = Instance.VideoEdit.IsEnabled
                        };
                        MontageItem.Click += Instance.VideoEdit_Click;
                        EditFlyout.Items.Add(MontageItem);

                        MenuFlyoutItem MergeItem = new MenuFlyoutItem
                        {
                            Icon = new FontIcon { Glyph = "\uE11E" },
                            Text = Globalization.GetString("Operate_Text_Merge"),
                            IsEnabled = Instance.VideoMerge.IsEnabled
                        };
                        MergeItem.Click += Instance.VideoMerge_Click;
                        EditFlyout.Items.Add(MergeItem);

                        MenuFlyoutItem TranscodeItem = new MenuFlyoutItem
                        {
                            Icon = new FontIcon { Glyph = "\uE1CA" },
                            Text = Globalization.GetString("Operate_Text_Transcode"),
                            IsEnabled = Instance.Transcode.IsEnabled
                        };
                        TranscodeItem.Click += Instance.Transcode_Click;
                        EditFlyout.Items.Add(TranscodeItem);

                        BottomCommandBar.SecondaryCommands.Add(new AppBarButton
                        {
                            Icon = new SymbolIcon(Symbol.Edit),
                            Label = Globalization.GetString("Operate_Text_Edit"),
                            Flyout = EditFlyout
                        });

                        MenuFlyout ShareFlyout = new MenuFlyout();
                        MenuFlyoutItem SystemShareItem = new MenuFlyoutItem
                        {
                            Icon = new SymbolIcon(Symbol.Share),
                            Text = Globalization.GetString("Operate_Text_SystemShare")
                        };
                        SystemShareItem.Click += Instance.SystemShare_Click;
                        ShareFlyout.Items.Add(SystemShareItem);

                        MenuFlyoutItem WIFIShareItem = new MenuFlyoutItem
                        {
                            Icon = new FontIcon { Glyph = "\uE701" },
                            Text = Globalization.GetString("Operate_Text_WIFIShare")
                        };
                        WIFIShareItem.Click += Instance.WIFIShare_Click;
                        ShareFlyout.Items.Add(WIFIShareItem);

                        MenuFlyoutItem BluetoothShare = new MenuFlyoutItem
                        {
                            Icon = new FontIcon { Glyph = "\uE702" },
                            Text = Globalization.GetString("Operate_Text_BluetoothShare")
                        };
                        BluetoothShare.Click += Instance.BluetoothShare_Click;
                        ShareFlyout.Items.Add(BluetoothShare);

                        BottomCommandBar.SecondaryCommands.Add(new AppBarButton
                        {
                            Icon = new SymbolIcon(Symbol.Share),
                            Label = Globalization.GetString("Operate_Text_Share"),
                            Flyout = ShareFlyout
                        });

                        AppBarButton CompressionButton = new AppBarButton
                        {
                            Icon = new SymbolIcon(Symbol.Bookmarks),
                            Label = Instance.Zip.Label
                        };
                        CompressionButton.Click += Instance.Zip_Click;
                        BottomCommandBar.SecondaryCommands.Add(CompressionButton);

                        BottomCommandBar.SecondaryCommands.Add(new AppBarSeparator());

                        AppBarButton PropertyButton = new AppBarButton
                        {
                            Icon = new SymbolIcon(Symbol.Tag),
                            Label = Globalization.GetString("Operate_Text_Property")
                        };
                        PropertyButton.Click += Instance.Attribute_Click;
                        BottomCommandBar.SecondaryCommands.Add(PropertyButton);
                    }
                    else
                    {
                        AppBarButton OpenButton = new AppBarButton
                        {
                            Icon = new SymbolIcon(Symbol.BackToWindow),
                            Label = Globalization.GetString("Operate_Text_Open")
                        };
                        OpenButton.Click += Instance.FolderOpen_Click;
                        BottomCommandBar.SecondaryCommands.Add(OpenButton);

                        AppBarButton CompressionButton = new AppBarButton
                        {
                            Icon = new SymbolIcon(Symbol.Bookmarks),
                            Label = Globalization.GetString("Operate_Text_Compression")
                        };
                        CompressionButton.Click += Instance.CompressFolder_Click;
                        BottomCommandBar.SecondaryCommands.Add(CompressionButton);

                        AppBarButton PinButton = new AppBarButton
                        {
                            Icon = new SymbolIcon(Symbol.Add),
                            Label = Globalization.GetString("Operate_Text_PinToHome")
                        };
                        PinButton.Click += Instance.AddToLibray_Click;
                        BottomCommandBar.SecondaryCommands.Add(PinButton);

                        AppBarButton PropertyButton = new AppBarButton
                        {
                            Icon = new SymbolIcon(Symbol.Tag),
                            Label = Globalization.GetString("Operate_Text_Property")
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
                        Label = Globalization.GetString("Operate_Text_Paste")
                    };
                    PasteButton.Click += Instance.Paste_Click;
                    BottomCommandBar.PrimaryCommands.Add(PasteButton);

                    AppBarButton RefreshButton = new AppBarButton
                    {
                        Icon = new SymbolIcon(Symbol.Refresh),
                        Label = Globalization.GetString("Operate_Text_Refresh")
                    };
                    RefreshButton.Click += Instance.Refresh_Click;
                    BottomCommandBar.PrimaryCommands.Add(RefreshButton);

                    AppBarButton WinExButton = new AppBarButton
                    {
                        Icon = new SymbolIcon(Symbol.BackToWindow),
                        Label = Globalization.GetString("Operate_Text_OpenInWinExplorer")
                    };
                    WinExButton.Click += Instance.UseSystemFileMananger_Click;
                    BottomCommandBar.PrimaryCommands.Add(WinExButton);

                    MenuFlyout NewFlyout = new MenuFlyout();
                    MenuFlyoutItem CreateFileItem = new MenuFlyoutItem
                    {
                        Icon = new SymbolIcon(Symbol.Page2),
                        Text = Globalization.GetString("Operate_Text_CreateFile"),
                        MinWidth = 150
                    };
                    CreateFileItem.Click += Instance.CreateFile_Click;
                    NewFlyout.Items.Add(CreateFileItem);

                    MenuFlyoutItem CreateFolder = new MenuFlyoutItem
                    {
                        Icon = new SymbolIcon(Symbol.NewFolder),
                        Text = Globalization.GetString("Operate_Text_CreateFolder"),
                        MinWidth = 150
                    };
                    CreateFolder.Click += Instance.CreateFolder_Click;
                    NewFlyout.Items.Add(CreateFolder);

                    BottomCommandBar.SecondaryCommands.Add(new AppBarButton
                    {
                        Icon = new SymbolIcon(Symbol.Add),
                        Label = Globalization.GetString("Operate_Text_Create"),
                        Flyout = NewFlyout
                    });

                    BottomCommandBar.SecondaryCommands.Add(new AppBarSeparator());

                    AppBarButton PropertyButton = new AppBarButton
                    {
                        Icon = new SymbolIcon(Symbol.Tag),
                        Label = Globalization.GetString("Operate_Text_Property")
                    };
                    PropertyButton.Click += Instance.ParentProperty_Click;
                    BottomCommandBar.SecondaryCommands.Add(PropertyButton);
                }
            }
        }

        public void Dispose()
        {
            ExpandCancel?.Dispose();
            ExpandCancel = null;
            DisplayLock?.Dispose();
            DisplayLock = null;
        }
    }

}
