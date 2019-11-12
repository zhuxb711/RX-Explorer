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
                    var Folder = currentnode.Content as StorageFolder;
                    string PlaceText;
                    if (Folder.DisplayName.Length > 18)
                    {
                        PlaceText = Folder.DisplayName.Substring(0, 18) + "...";
                    }
                    else
                    {
                        PlaceText = Folder.DisplayName;
                    }

                    MainPage.ThisPage.GlobeSearch.PlaceholderText = MainPage.ThisPage.CurrentLanguage == LanguageEnum.Chinese
                         ? "搜索 " + PlaceText
                         : "Search " + PlaceText;
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
        private CancellationTokenSource FolderExpandCancel;
        private AutoResetEvent Locker;
        private ManualResetEvent ExitLocker;

        public FileControl()
        {
            InitializeComponent();
            ThisPage = this;
            Nav.Navigate(typeof(FilePresenter), Nav, new DrillInNavigationTransitionInfo());

            Loaded += FileControl_Loaded;
        }

        private async void FileControl_Loaded(object sender, RoutedEventArgs e)
        {
            if (CancelToken != null)
            {
                CancelToken.Dispose();
                CancelToken = null;
            }
            CancelToken = new CancellationTokenSource();

            Locker = new AutoResetEvent(false);

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
                    if (!(FolderTree.ContainerFromNode(Node) is TreeViewItem Container))
                    {
                        await Task.Delay(200);
                        continue;
                    }

                    Container.IsSelected = true;
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

            string PlaceText;
            if (TargetFolder.DisplayName.Length > 18)
            {
                PlaceText = TargetFolder.DisplayName.Substring(0, 18) + "...";
            }
            else
            {
                PlaceText = TargetFolder.DisplayName;
            }
            MainPage.ThisPage.GlobeSearch.PlaceholderText = MainPage.ThisPage.CurrentLanguage == LanguageEnum.Chinese
                ? "搜索 " + PlaceText
                : "Search " + PlaceText;
        }

        protected override async void OnNavigatedFrom(NavigationEventArgs e)
        {
            Locker.Dispose();

            FolderExpandCancel.Cancel();

            await Task.Run(() =>
            {
                ExitLocker.WaitOne();
            });

            ExitLocker.Dispose();
            ExitLocker = null;
            FolderExpandCancel.Dispose();
            FolderExpandCancel = null;

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

                FolderExpandCancel = new CancellationTokenSource();
                ExitLocker = new ManualResetEvent(true);

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

                uint FolderCount = await FolderQuery.GetItemCountAsync();

                if (FolderCount == 0)
                {
                    return;
                }
                else
                {
                    for (uint i = 0; i < FolderCount && !FolderExpandCancel.IsCancellationRequested; i += 50)
                    {
                        IReadOnlyList<StorageFolder> StorageFolderList = await FolderQuery.GetFoldersAsync(i, 50).AsTask(FolderExpandCancel.Token);

                        foreach (var SubFolder in StorageFolderList)
                        {
                            StorageFolderQueryResult SubFolderQuery = SubFolder.CreateFolderQueryWithOptions(Options);
                            uint Count = await SubFolderQuery.GetItemCountAsync().AsTask(FolderExpandCancel.Token);

                            TreeViewNode NewNode = new TreeViewNode
                            {
                                Content = SubFolder,
                                HasUnrealizedChildren = Count != 0
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
            finally
            {
                ExitLocker.Set();
            }
        }

        private async void FileTree_Expanding(TreeView sender, TreeViewExpandingEventArgs args)
        {
            if (args.Node.HasUnrealizedChildren)
            {
                await FillTreeNode(args.Node);
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

                QueryOptions Options = new QueryOptions(CommonFolderQuery.DefaultQuery)
                {
                    FolderDepth = FolderDepth.Shallow,
                    IndexerOption = IndexerOption.UseIndexerWhenAvailable
                };

                Options.SetThumbnailPrefetch(ThumbnailMode.ListView, 60, ThumbnailOptions.ResizeThumbnail);
                Options.SetPropertyPrefetch(PropertyPrefetchOptions.BasicProperties, new string[] { "System.ItemTypeText", "System.ItemNameDisplayWithoutExtension", "System.FileName", "System.Size", "System.DateModified" });

                StorageItemQueryResult ItemQuery = folder.CreateItemQueryWithOptions(Options);

                IReadOnlyList<IStorageItem> FileList = null;
                try
                {
                    FilePresenter.ThisPage.FileCollection.HasMoreItems = false;
                    FileList = await ItemQuery.GetItemsAsync(0, 100).AsTask(CancelToken.Token);
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

            QueueContentDialog QueueContenDialog;
            if (MainPage.ThisPage.CurrentLanguage == LanguageEnum.Chinese)
            {
                QueueContenDialog = new QueueContentDialog
                {
                    Title = "警告",
                    Content = "此操作将永久删除该文件夹内的所有内容\r\r是否继续？",
                    PrimaryButtonText = "继续",
                    CloseButtonText = "取消",
                    Background = Application.Current.Resources["DialogAcrylicBrush"] as Brush
                };
            }
            else
            {
                QueueContenDialog = new QueueContentDialog
                {
                    Title = "Warning",
                    Content = "This will permanently delete everything in the folder\r\rWhether to continue ？",
                    PrimaryButtonText = "Continue",
                    CloseButtonText = "Cancel",
                    Background = Application.Current.Resources["DialogAcrylicBrush"] as Brush
                };
            }

            if (await QueueContenDialog.ShowAsync() == ContentDialogResult.Primary)
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
                            Item.StartBringIntoView(new BringIntoViewOptions { AnimationDesired = true, VerticalAlignmentRatio = 0.5 });
                            await DisplayItemsInFolder(ParentNode);
                            break;
                        }
                        else
                        {
                            await Task.Delay(300);
                        }
                    }
                    CurrentNode = ParentNode;
                }
                catch (UnauthorizedAccessException)
                {
                    QueueContentDialog dialog;
                    if (MainPage.ThisPage.CurrentLanguage == LanguageEnum.Chinese)
                    {
                        dialog = new QueueContentDialog
                        {
                            Title = "错误",
                            Content = "RX无权删除此文件夹，可能是您无权访问此文件夹\r\r是否立即进入系统文件管理器进行相应操作？",
                            PrimaryButtonText = "立刻",
                            CloseButtonText = "稍后",
                            Background = Application.Current.Resources["DialogAcrylicBrush"] as Brush
                        };
                    }
                    else
                    {
                        dialog = new QueueContentDialog
                        {
                            Title = "Error",
                            Content = "RX does not have permission to delete this folder, it may be that you do not have access to this folder\r\rEnter the system file manager immediately ？",
                            PrimaryButtonText = "Enter",
                            CloseButtonText = "Later",
                            Background = Application.Current.Resources["DialogAcrylicBrush"] as Brush
                        };
                    }

                    if (await dialog.ShowAsync() == ContentDialogResult.Primary)
                    {
                        _ = await Launcher.LaunchFolderAsync(CurrentFolder);
                    }
                }
                catch (Exception)
                {
                    QueueContentDialog Dialog;
                    if (MainPage.ThisPage.CurrentLanguage == LanguageEnum.Chinese)
                    {
                        Dialog = new QueueContentDialog
                        {
                            Title = "错误",
                            Content = "删除文件夹时出现错误",
                            CloseButtonText = "确定",
                            Background = Application.Current.Resources["DialogAcrylicBrush"] as Brush
                        };
                    }
                    else
                    {
                        Dialog = new QueueContentDialog
                        {
                            Title = "Error",
                            Content = "An error occurred while deleting the folder",
                            CloseButtonText = "Confirm",
                            Background = Application.Current.Resources["DialogAcrylicBrush"] as Brush
                        };
                    }
                    _ = await Dialog.ShowAsync();
                }
            }
        }

        private async void FileTree_Collapsed(TreeView sender, TreeViewCollapsedEventArgs args)
        {
            FolderExpandCancel.Cancel();

            await Task.Run(() =>
            {
                ExitLocker.WaitOne();
            });

            FolderExpandCancel.Dispose();
            FolderExpandCancel = new CancellationTokenSource();

            args.Node.Children.Clear();
            args.Node.HasUnrealizedChildren = true;
        }

        private async void FolderTree_RightTapped(object sender, Windows.UI.Xaml.Input.RightTappedRoutedEventArgs e)
        {
            if ((e.OriginalSource as FrameworkElement)?.DataContext is TreeViewNode Node)
            {
                FolderTree.ContextFlyout = RightTabFlyout;

                (FolderTree.ContainerFromNode(Node) as TreeViewItem).IsSelected = true;
                await DisplayItemsInFolder(Node);
                CurrentNode = Node;

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
                    if (MainPage.ThisPage.CurrentLanguage == LanguageEnum.Chinese)
                    {
                        QueueContentDialog content = new QueueContentDialog
                        {
                            Title = "错误",
                            Content = "文件夹名不能为空，重命名失败",
                            CloseButtonText = "确定",
                            Background = Application.Current.Resources["DialogAcrylicBrush"] as Brush
                        };
                        _ = await content.ShowAsync();
                    }
                    else
                    {
                        QueueContentDialog content = new QueueContentDialog
                        {
                            Title = "Error",
                            Content = "Folder name cannot be empty, rename failed",
                            CloseButtonText = "Confirm",
                            Background = Application.Current.Resources["DialogAcrylicBrush"] as Brush
                        };
                        _ = await content.ShowAsync();
                    }
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
                    QueueContentDialog dialog;
                    if (MainPage.ThisPage.CurrentLanguage == LanguageEnum.Chinese)
                    {
                        dialog = new QueueContentDialog
                        {
                            Title = "错误",
                            Content = "RX无权重命名此文件夹，可能是您无权访问此文件夹\r\r是否立即进入系统文件管理器进行相应操作？",
                            PrimaryButtonText = "立刻",
                            CloseButtonText = "稍后",
                            Background = Application.Current.Resources["DialogAcrylicBrush"] as Brush
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
                            Background = Application.Current.Resources["DialogAcrylicBrush"] as Brush
                        };
                    }

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
                var NewFolder = MainPage.ThisPage.CurrentLanguage == LanguageEnum.Chinese
                    ? await CurrentFolder.CreateFolderAsync("新建文件夹", CreationCollisionOption.GenerateUniqueName)
                    : await CurrentFolder.CreateFolderAsync("New folder", CreationCollisionOption.GenerateUniqueName);

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
                QueueContentDialog dialog;
                if (MainPage.ThisPage.CurrentLanguage == LanguageEnum.Chinese)
                {
                    dialog = new QueueContentDialog
                    {
                        Title = "错误",
                        Content = "RX无权在此创建文件夹，可能是您无权访问此文件夹\r\r是否立即进入系统文件管理器进行相应操作？",
                        PrimaryButtonText = "立刻",
                        CloseButtonText = "稍后",
                        Background = Application.Current.Resources["DialogAcrylicBrush"] as Brush
                    };
                }
                else
                {
                    dialog = new QueueContentDialog
                    {
                        Title = "Error",
                        Content = "RX does not have permission to create folder, it may be that you do not have access to this folder\r\rEnter the system file manager immediately ？",
                        PrimaryButtonText = "Enter",
                        CloseButtonText = "Later",
                        Background = Application.Current.Resources["DialogAcrylicBrush"] as Brush
                    };
                }

                if (await dialog.ShowAsync() == ContentDialogResult.Primary)
                {
                    _ = await Launcher.LaunchFolderAsync(CurrentFolder);
                }
            }
        }

        private async void FolderAttribute_Click(object sender, RoutedEventArgs e)
        {
            if (CurrentNode == FolderTree.RootNodes.FirstOrDefault())
            {
                if (ThisPC.ThisPage.HardDeviceList.FirstOrDefault((Device) => Device.Name == CurrentFolder.DisplayName) is HardDeviceInfo Info)
                {
                    DeviceInfoDialog dialog = new DeviceInfoDialog(Info);
                    _ = await dialog.ShowAsync();
                }
                else
                {
                    AttributeDialog Dialog = new AttributeDialog(CurrentFolder);
                    _ = await Dialog.ShowAsync();
                }
            }
            else
            {
                AttributeDialog Dialog = new AttributeDialog(CurrentFolder);
                _ = await Dialog.ShowAsync();
            }
        }

        private async void FolderAdd_Click(object sender, RoutedEventArgs e)
        {
            StorageFolder folder = CurrentFolder;
            if (ThisPC.ThisPage.LibraryFolderList.Any((Folder) => Folder.Folder.Path == folder.Path))
            {
                QueueContentDialog dialog = new QueueContentDialog
                {
                    Title = "提示",
                    Content = "此文件夹已经添加到主界面了，不能重复添加哦",
                    CloseButtonText = "知道了",
                    Background = Application.Current.Resources["DialogAcrylicBrush"] as Brush
                };
                _ = await dialog.ShowAsync();
            }
            else
            {
                BitmapImage Thumbnail = await folder.GetThumbnailBitmapAsync();
                ThisPC.ThisPage.LibraryFolderList.Add(new LibraryFolder(folder, Thumbnail, LibrarySource.UserAdded));
                await SQLite.Current.SetFolderLibraryAsync(folder.Path);
            }
        }
    }

}
