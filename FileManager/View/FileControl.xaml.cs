using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.Storage.Search;
using Windows.System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;


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
                currentnode = value;
                if (currentnode != null)
                {
                    MainPage.ThisPage.GlobeSearch.PlaceholderText = "搜索 " + (currentnode.Content as StorageFolder).DisplayName;
                }
            }
        }

        public StorageFolder CurrentFolder
        {
            get
            {
                return CurrentNode?.Content as StorageFolder;
            }
        }

        public static FileControl ThisPage { get; private set; }
        private bool IsAdding = false;
        private CancellationTokenSource CancelToken;
        private AutoResetEvent Locker;
        public AutoResetEvent ExpandLocker;
        public bool ExpenderLockerReleaseRequest = false;

        public FileControl()
        {
            InitializeComponent();
            ThisPage = this;
            Nav.Navigate(typeof(FilePresenter), Nav, new DrillInNavigationTransitionInfo());

            Loaded += FileControl_Loaded;
        }

        private async void FileControl_Loaded(object sender, RoutedEventArgs e)
        {
            CancelToken = new CancellationTokenSource();
            Locker = new AutoResetEvent(false);
            ExpandLocker = new AutoResetEvent(false);

            while (true)
            {
                var Node = FolderTree.RootNodes.FirstOrDefault();
                if (Node == null)
                {
                    await Task.Delay(200);
                    continue;
                }
                else
                {
                    (FolderTree.ContainerFromNode(Node) as TreeViewItem).IsSelected = true;
                    await DisplayItemsInFolder(Node);
                    break;
                }
            }
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            StorageFolder TargetFolder = e.Parameter as StorageFolder;
            InitializeTreeView(TargetFolder);

            MainPage.ThisPage.GlobeSearch.Visibility = Visibility.Visible;
            MainPage.ThisPage.GlobeSearch.PlaceholderText = "搜索 " + TargetFolder.DisplayName;
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            Locker.Dispose();
            ExpandLocker.Dispose();
            CancelToken.Dispose();

            CurrentNode = null;

            MainPage.ThisPage.GlobeSearch.Visibility = Visibility.Collapsed;

            FolderTree.RootNodes.Clear();
            FilePresenter.ThisPage.FileCollection.Clear();
            FilePresenter.ThisPage.HasFile.Visibility = Visibility.Visible;

        }

        /// <summary>
        /// 执行文件目录的初始化
        /// </summary>
        private async void InitializeTreeView(StorageFolder InitFolder)
        {
            if (InitFolder != null)
            {
                var SubFolders = await InitFolder.GetFoldersAsync();
                TreeViewNode RootNode = new TreeViewNode
                {
                    Content = InitFolder,
                    IsExpanded = SubFolders.Count != 0,
                    HasUnrealizedChildren = SubFolders.Count != 0
                };
                FolderTree.RootNodes.Add(RootNode);
                await FillTreeNode(RootNode);
            }
        }

        /// <summary>
        /// 向特定TreeViewNode节点下添加子节点
        /// </summary>
        /// <param name="Node">节点</param>
        /// <returns></returns>
        private async Task FillTreeNode(TreeViewNode Node)
        {
            StorageFolder folder;
            if (Node.HasUnrealizedChildren == true)
            {
                folder = Node.Content as StorageFolder;
            }
            else
            {
                return;
            }

            IReadOnlyList<StorageFolder> StorageFolderList = await folder.GetFoldersAsync();

            if (StorageFolderList.Count == 0)
            {
                return;
            }

            foreach (var SubFolder in StorageFolderList)
            {
                IReadOnlyList<StorageFolder> SubSubStorageFolderList = await SubFolder.GetFoldersAsync();

                TreeViewNode NewNode = new TreeViewNode
                {
                    Content = SubFolder,
                    HasUnrealizedChildren = SubSubStorageFolderList.Count != 0
                };

                Node.Children.Add(NewNode);
            }
            Node.HasUnrealizedChildren = false;
        }

        private async void FileTree_Expanding(TreeView sender, TreeViewExpandingEventArgs args)
        {
            if (args.Node.HasUnrealizedChildren)
            {
                await FillTreeNode(args.Node);
            }

            if (ExpenderLockerReleaseRequest)
            {
                ExpenderLockerReleaseRequest = false;
                ExpandLocker.Set();
            }
        }

        private async void FileTree_ItemInvoked(TreeView sender, TreeViewItemInvokedEventArgs args)
        {
            await DisplayItemsInFolder(args.InvokedItem as TreeViewNode);
        }

        public async Task DisplayItemsInFolder(TreeViewNode Node)
        {
            /*
             * 同一文件夹内可能存在大量文件
             * 因此切换不同文件夹时极有可能遍历文件夹仍未完成
             * 此处激活取消指令，等待当前遍历结束，再开始下一次文件遍历
             * 确保不会出现异常
             */
            //防止多次点击同一文件夹导致的多重查找            
            if (Node.Content is StorageFolder folder)
            {
                if (folder.FolderRelativeId == CurrentFolder?.FolderRelativeId && !MainPage.ThisPage.IsNowSearching)
                {
                    IsAdding = false;
                    return;
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
                    });
                }
                IsAdding = true;

                if (MainPage.ThisPage.IsNowSearching)
                {
                    MainPage.ThisPage.IsNowSearching = false;
                }

                CurrentNode = Node;
                FilePresenter.ThisPage.DisplayNode = CurrentNode;

                //当处于USB其他附加功能的页面时，若点击文件目录则自动执行返回导航
                if (Nav.CurrentSourcePageType.Name != "FilePresenter")
                {
                    Nav.GoBack();
                }

                FilePresenter.ThisPage.FileCollection.Clear();

                QueryOptions Options = new QueryOptions(CommonFileQuery.DefaultQuery, null)
                {
                    FolderDepth = FolderDepth.Shallow,
                    IndexerOption = IndexerOption.UseIndexerWhenAvailable
                };

                Options.SetThumbnailPrefetch(ThumbnailMode.ListView, 60, ThumbnailOptions.ResizeThumbnail);

                StorageItemQueryResult ItemQuery = folder.CreateItemQueryWithOptions(Options);

                IReadOnlyList<IStorageItem> FileList = null;
                try
                {
                    FilePresenter.ThisPage.FileCollection.HasMoreItems = false;
                    FileList = await ItemQuery.GetItemsAsync(0, 50).AsTask(CancelToken.Token);
                    await FilePresenter.ThisPage.FileCollection.SetStorageItemQuery(ItemQuery);
                }
                catch (TaskCanceledException)
                {
                    goto FLAG;
                }

                FilePresenter.ThisPage.HasFile.Visibility = FileList.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

                for (int i = 0; i < FileList.Count && !CancelToken.IsCancellationRequested; i++)
                {
                    var Item = FileList[i];
                    var Size = await Item.GetSizeDescriptionAsync();
                    var Thumbnail = await Item.GetThumbnailBitmapAsync() ?? new BitmapImage(new Uri("ms-appx:///Assets/DocIcon.png"));
                    var ModifiedTime = await Item.GetModifiedTimeAsync();
                    FilePresenter.ThisPage.FileCollection.Add(new FileSystemStorageItem(FileList[i], Size, Thumbnail, ModifiedTime));
                }
            }

        FLAG:
            if (CancelToken.IsCancellationRequested)
            {
                Locker.Set();
            }
            else
            {
                IsAdding = false;
            }
        }

        private async void FolderDelete_Click(object sender, RoutedEventArgs e)
        {
            if (CurrentNode == null)
            {
                return;
            }

            ContentDialog contentDialog = new ContentDialog
            {
                Title = "警告",
                Content = "    此操作将永久删除该文件夹内的所有内容\r\r    是否继续？",
                PrimaryButtonText = "继续",
                CloseButtonText = "取消",
                Background = Application.Current.Resources["DialogAcrylicBrush"] as Brush
            };
            if (await contentDialog.ShowAsync() == ContentDialogResult.Primary)
            {
                try
                {
                    await CurrentFolder.DeleteAllSubFilesAndFolders();
                    await CurrentFolder.DeleteAsync(StorageDeleteOption.PermanentDelete);

                    FilePresenter.ThisPage.FileCollection.Remove(FilePresenter.ThisPage.FileCollection.Where((Item) => Item.RelativeId == CurrentFolder.FolderRelativeId).FirstOrDefault());

                    if (FilePresenter.ThisPage.DisplayNode == CurrentNode)
                    {
                        FilePresenter.ThisPage.FileCollection.Clear();
                        FilePresenter.ThisPage.HasFile.Visibility = Visibility.Visible;
                    }

                    TreeViewNode ParentNode = CurrentNode.Parent;
                    ParentNode.Children.Remove(CurrentNode);

                    while (true)
                    {
                        if (FolderTree.ContainerFromNode(ParentNode) is TreeViewItem Item)
                        {
                            Item.IsSelected = true;
                            await DisplayItemsInFolder(ParentNode);
                            break;
                        }
                        else
                        {
                            await Task.Delay(200);
                        }
                    }
                    CurrentNode = ParentNode;
                }
                catch (UnauthorizedAccessException)
                {
                    ContentDialog dialog = new ContentDialog
                    {
                        Title = "错误",
                        Content = "RX无权删除此文件夹，可能是您无权访问此文件夹\r\r是否立即进入系统文件管理器进行相应操作？",
                        PrimaryButtonText = "立刻",
                        CloseButtonText = "稍后",
                        Background = Application.Current.Resources["DialogAcrylicBrush"] as Brush
                    };
                    if (await dialog.ShowAsync() == ContentDialogResult.Primary)
                    {
                        _ = await Launcher.LaunchFolderAsync(CurrentFolder);
                    }
                }
                catch (Exception)
                {
                    ContentDialog Dialog = new ContentDialog
                    {
                        Title = "错误",
                        Content = "删除文件夹时出现错误",
                        CloseButtonText = "确定",
                        Background = Application.Current.Resources["DialogAcrylicBrush"] as Brush
                    };
                    _ = await Dialog.ShowAsync();
                }
            }
        }

        private void FileTree_Collapsed(TreeView sender, TreeViewCollapsedEventArgs args)
        {
            args.Node.Children.Clear();
            args.Node.HasUnrealizedChildren = true;
        }

        private async void FolderTree_RightTapped(object sender, Windows.UI.Xaml.Input.RightTappedRoutedEventArgs e)
        {
            if ((e.OriginalSource as FrameworkElement)?.DataContext is TreeViewNode Node)
            {
                (FolderTree.ContainerFromNode(Node) as TreeViewItem).IsSelected = true;
                await DisplayItemsInFolder(Node);
                CurrentNode = Node;

                if (FolderTree.RootNodes.Contains(CurrentNode))
                {
                    CreateFolder.IsEnabled = true;
                    FolderDelete.IsEnabled = false;
                    FolderRename.IsEnabled = false;
                }
                else
                {
                    CreateFolder.IsEnabled = true;
                }
            }
            else
            {
                CreateFolder.IsEnabled = false;
            }
        }

        private async void FolderRename_Click(object sender, RoutedEventArgs e)
        {
            if (CurrentNode == null)
            {
                return;
            }
            var Folder = CurrentFolder;
            RenameDialog renameDialog = new RenameDialog(Folder.Name);
            if (await renameDialog.ShowAsync() == ContentDialogResult.Primary)
            {
                if (renameDialog.DesireName == "")
                {
                    ContentDialog content = new ContentDialog
                    {
                        Title = "错误",
                        Content = "文件夹名不能为空，重命名失败",
                        CloseButtonText = "确定",
                        Background = Application.Current.Resources["DialogAcrylicBrush"] as Brush
                    };
                    await content.ShowAsync();
                    return;
                }

                try
                {
                    await Folder.RenameAsync(renameDialog.DesireName, NameCollisionOption.GenerateUniqueName);
                    StorageFolder ReCreateFolder = await StorageFolder.GetFolderFromPathAsync(Folder.Path);

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
                        var NewNode = new TreeViewNode()
                        {
                            Content = ReCreateFolder,
                            HasUnrealizedChildren = false,
                            IsExpanded = true
                        };

                        foreach (var SubNode in CurrentNode.Children)
                        {
                            NewNode.Children.Add(SubNode);
                        }

                        ChildCollection.Insert(index, NewNode);
                        ChildCollection.Remove(CurrentNode);
                        await NewNode.UpdateAllSubNodeFolder();
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
                }
                catch (UnauthorizedAccessException)
                {
                    ContentDialog dialog = new ContentDialog
                    {
                        Title = "错误",
                        Content = "RX无权重命名此文件夹，可能是您无权访问此文件夹\r\r是否立即进入系统文件管理器进行相应操作？",
                        PrimaryButtonText = "立刻",
                        CloseButtonText = "稍后",
                        Background = Application.Current.Resources["DialogAcrylicBrush"] as Brush
                    };
                    if (await dialog.ShowAsync() == ContentDialogResult.Primary)
                    {
                        _ = await Launcher.LaunchFolderAsync(CurrentFolder);
                    }
                }
            }
        }

        private async void CreateFolder_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var NewFolder = await CurrentFolder.CreateFolderAsync("新建文件夹", CreationCollisionOption.GenerateUniqueName);

                var Size = await NewFolder.GetSizeDescriptionAsync();
                var Thumbnail = await NewFolder.GetThumbnailBitmapAsync() ?? new BitmapImage(new Uri("ms-appx:///Assets/DocIcon.png"));
                var ModifiedTime = await NewFolder.GetModifiedTimeAsync();

                FilePresenter.ThisPage.FileCollection.Insert(0, new FileSystemStorageItem(NewFolder, Size, Thumbnail, ModifiedTime));

                if (CurrentNode.IsExpanded || !CurrentNode.HasChildren)
                {
                    CurrentNode.Children.Add(new TreeViewNode
                    {
                        Content = NewFolder,
                        HasUnrealizedChildren = false
                    });
                }
                CurrentNode.IsExpanded = true;
            }
            catch (UnauthorizedAccessException)
            {
                ContentDialog dialog = new ContentDialog
                {
                    Title = "错误",
                    Content = "RX无权在此创建文件夹，可能是您无权访问此文件夹\r\r是否立即进入系统文件管理器进行相应操作？",
                    PrimaryButtonText = "立刻",
                    CloseButtonText = "稍后",
                    Background = Application.Current.Resources["DialogAcrylicBrush"] as Brush
                };
                if (await dialog.ShowAsync() == ContentDialogResult.Primary)
                {
                    _ = await Launcher.LaunchFolderAsync(CurrentFolder);
                }
            }
        }
    }

}
