using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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
using TreeViewItem = Microsoft.UI.Xaml.Controls.TreeViewItem;
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
                currentnode = value;
                if (currentnode != null && currentnode.Content is StorageFolder Folder)
                {
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

                    AddressBox.Text = Folder.Path;

                    GoParentFolder.IsEnabled = CurrentNode != FolderTree.RootNodes[0];
                    GoBackRecord.IsEnabled = RecordIndex > 0;
                    GoForwardRecord.IsEnabled = RecordIndex < GoAndBackRecord.Count - 1;
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

        public bool IsSearchOrPathBoxFocused = false;

        public static FileControl ThisPage { get; private set; }
        private bool IsAdding = false;
        private CancellationTokenSource CancelToken;
        private CancellationTokenSource FolderExpandCancel;
        private AutoResetEvent Locker;
        private ManualResetEvent ExitLocker;
        private static List<StorageFolder> GoAndBackRecord = new List<StorageFolder>();
        private static int RecordIndex = 0;
        private static bool IsBackOrForwardAction = false;

        private string ToDeleteFolderName;

        public FileControl()
        {
            InitializeComponent();
            ThisPage = this;
            try
            {
                Nav.Navigate(typeof(FilePresenter), Nav, new DrillInNavigationTransitionInfo());
            }
            catch (Exception ex)
            {
                ExceptionTracer.RequestBlueScreen(ex);
            }

            Loaded += FileControl_Loaded;
        }

        public FileControl(StorageFolder Folder)
        {
            InitializeComponent();
            ThisPage = this;

            try
            {
                Nav.Navigate(typeof(FilePresenter), Nav, new DrillInNavigationTransitionInfo());
                OpenTargetFolder(Folder);
            }
            catch (Exception ex)
            {
                ExceptionTracer.RequestBlueScreen(ex);
            }
        }

        private async void OpenTargetFolder(StorageFolder Folder)
        {
            if (CancelToken != null)
            {
                CancelToken.Dispose();
                CancelToken = null;
            }
            CancelToken = new CancellationTokenSource();

            Locker = new AutoResetEvent(false);

            await InitializeTreeView(await StorageFolder.GetFolderFromPathAsync(Path.GetPathRoot(Folder.Path)));

            var RootNode = FolderTree.RootNodes[0];
            TreeViewNode TargetNode = await FindFolderLocationInTree(RootNode, new PathAnalysis(Folder.Path, string.Empty));
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
                    _ = await dialog.ShowAsync();
                }
                else
                {
                    QueueContentDialog dialog = new QueueContentDialog
                    {
                        Title = "Error",
                        Content = "Unable to locate folder, which may have been deleted or moved",
                        CloseButtonText = "Confirm"
                    };
                    _ = await dialog.ShowAsync();
                }
            }
            else
            {
                await TargetNode.SelectNode(FolderTree);
            }
        }

        private void FileControl_Loaded(object sender, RoutedEventArgs e)
        {
            if (CancelToken != null)
            {
                CancelToken.Dispose();
                CancelToken = null;
            }
            CancelToken = new CancellationTokenSource();

            Locker = new AutoResetEvent(false);
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            if (e.Parameter is StorageFolder TargetFolder)
            {
                await InitializeTreeView(TargetFolder);

                string PlaceText;
                if (TargetFolder.DisplayName.Length > 18)
                {
                    PlaceText = TargetFolder.DisplayName.Substring(0, 18) + "...";
                }
                else
                {
                    PlaceText = TargetFolder.DisplayName;
                }
                GlobeSearch.PlaceholderText = Globalization.Language == LanguageEnum.Chinese
                    ? "搜索 " + PlaceText
                    : "Search " + PlaceText;
            }
        }

        protected override async void OnNavigatedFrom(NavigationEventArgs e)
        {
            while (Nav.CanGoBack)
            {
                Nav.GoBack();
            }

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

            FolderTree.RootNodes.Clear();
            FilePresenter.ThisPage.FileCollection.Clear();
            FilePresenter.ThisPage.HasFile.Visibility = Visibility.Visible;

            RecordIndex = 0;
            GoAndBackRecord.Clear();
            IsBackOrForwardAction = false;
            GoBackRecord.IsEnabled = false;
            GoForwardRecord.IsEnabled = false;
            GoParentFolder.IsEnabled = false;
        }

        /// <summary>
        /// 执行文件目录的初始化
        /// </summary>
        private async Task InitializeTreeView(StorageFolder InitFolder)
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

                var FillTreeTask = FillTreeNode(RootNode);
                var EnumFileTask = DisplayItemsInFolder(RootNode);

                await Task.WhenAll(FillTreeTask, EnumFileTask);
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
            if (Node.HasUnrealizedChildren)
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

        private async void FolderTree_Expanding(TreeView sender, TreeViewExpandingEventArgs args)
        {
            if (args.Node.HasUnrealizedChildren)
            {
                await FillTreeNode(args.Node);
            }
        }

        private async void FolderTree_ItemInvoked(TreeView sender, TreeViewItemInvokedEventArgs args)
        {
            await DisplayItemsInFolder(args.InvokedItem as TreeViewNode);
        }

        public async Task DisplayItemsInFolder(TreeViewNode Node, bool ForceRefresh = false)
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
                if (!ForceRefresh)
                {
                    if (folder.FolderRelativeId == CurrentFolder?.FolderRelativeId && Nav.CurrentSourcePageType == typeof(FilePresenter))
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
                    });
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
                    GoAndBackRecord.Add(folder);
                    RecordIndex = GoAndBackRecord.Count - 1;
                }

                CurrentNode = Node;

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

                Options.SetThumbnailPrefetch(ThumbnailMode.ListView, 100, ThumbnailOptions.ResizeThumbnail);
                Options.SetPropertyPrefetch(PropertyPrefetchOptions.BasicProperties, new string[] { "System.ItemTypeText", "System.ItemNameDisplayWithoutExtension", "System.FileName", "System.Size", "System.DateModified" });

                StorageItemQueryResult ItemQuery = folder.CreateItemQueryWithOptions(Options);

                IReadOnlyList<IStorageItem> FileList = null;
                try
                {
                    FilePresenter.ThisPage.FileCollection.HasMoreItems = false;
                    FileList = await ItemQuery.GetItemsAsync(0, 100).AsTask(CancelToken.Token);
                    await FilePresenter.ThisPage.FileCollection.SetStorageItemQueryAsync(ItemQuery);
                }
                catch (TaskCanceledException)
                {
                    goto FLAG;
                }

                FilePresenter.ThisPage.HasFile.Visibility = FileList.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

                for (int i = 0; i < FileList.Count && !CancelToken.IsCancellationRequested; i++)
                {
                    var Item = FileList[i];
                    if (Item is StorageFile)
                    {
                        var Size = await Item.GetSizeDescriptionAsync();
                        var Thumbnail = await Item.GetThumbnailBitmapAsync() ?? new BitmapImage(new Uri("ms-appx:///Assets/DocIcon.png"));
                        var ModifiedTime = await Item.GetModifiedTimeAsync();
                        FilePresenter.ThisPage.FileCollection.Add(new FileSystemStorageItem(FileList[i], Size, Thumbnail, ModifiedTime));
                    }
                    else
                    {
                        if (ToDeleteFolderName != Item.Name)
                        {
                            var Thumbnail = await Item.GetThumbnailBitmapAsync() ?? new BitmapImage(new Uri("ms-appx:///Assets/DocIcon.png"));
                            FilePresenter.ThisPage.FileCollection.Add(new FileSystemStorageItem(FileList[i], string.Empty, Thumbnail, string.Empty));
                        }
                        else
                        {
                            ToDeleteFolderName = null;
                        }
                    }
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

            if (!await CurrentFolder.CheckExist())
            {
                if (Globalization.Language == LanguageEnum.Chinese)
                {
                    QueueContentDialog Dialog = new QueueContentDialog
                    {
                        Title = "错误",
                        Content = "无法找到对应的文件夹，该文件夹可能已被移动或删除",
                        CloseButtonText = "刷新"
                    };
                    _ = await Dialog.ShowAsync();
                }
                else
                {
                    QueueContentDialog Dialog = new QueueContentDialog
                    {
                        Title = "Error",
                        Content = "Could not find the corresponding folder, it may have been moved or deleted",
                        CloseButtonText = "Refresh"
                    };
                    _ = await Dialog.ShowAsync();
                }
                await DisplayItemsInFolder(CurrentNode, true);
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

            if (await QueueContenDialog.ShowAsync() == ContentDialogResult.Primary)
            {
                try
                {
                    await CurrentFolder.DeleteAllSubFilesAndFolders();
                    await CurrentFolder.DeleteAsync(StorageDeleteOption.PermanentDelete);

                    FilePresenter.ThisPage.FileCollection.Remove(FilePresenter.ThisPage.FileCollection.Where((Item) => Item.RelativeId == CurrentFolder.FolderRelativeId).FirstOrDefault());

                    TreeViewNode ParentNode = CurrentNode.Parent;
                    ParentNode.Children.Remove(CurrentNode);

                    await ParentNode.SelectNode(FolderTree);

                    ToDeleteFolderName = CurrentFolder.Name;
                    await DisplayItemsInFolder(ParentNode);

                    CurrentNode = ParentNode;
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

                    if (await dialog.ShowAsync() == ContentDialogResult.Primary)
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
                    _ = await Dialog.ShowAsync();
                }
            }
        }

        private async void FolderTree_Collapsed(TreeView sender, TreeViewCollapsedEventArgs args)
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

                await Node.SelectNode(FolderTree);

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

            if (!await CurrentFolder.CheckExist())
            {
                if (Globalization.Language == LanguageEnum.Chinese)
                {
                    QueueContentDialog Dialog = new QueueContentDialog
                    {
                        Title = "错误",
                        Content = "无法找到对应的文件夹，该文件夹可能已被移动或删除",
                        CloseButtonText = "刷新"
                    };
                    _ = await Dialog.ShowAsync();
                }
                else
                {
                    QueueContentDialog Dialog = new QueueContentDialog
                    {
                        Title = "Error",
                        Content = "Could not find the corresponding folder, it may have been moved or deleted",
                        CloseButtonText = "Refresh"
                    };
                    _ = await Dialog.ShowAsync();
                }
                await DisplayItemsInFolder(CurrentNode, true);
                return;
            }

            var Folder = CurrentFolder;
            RenameDialog renameDialog = new RenameDialog(Folder.Name);
            if (await renameDialog.ShowAsync() == ContentDialogResult.Primary)
            {
                if (renameDialog.DesireName == "")
                {
                    if (Globalization.Language == LanguageEnum.Chinese)
                    {
                        QueueContentDialog content = new QueueContentDialog
                        {
                            Title = "错误",
                            Content = "文件夹名不能为空，重命名失败",
                            CloseButtonText = "确定"
                        };
                        _ = await content.ShowAsync();
                    }
                    else
                    {
                        QueueContentDialog content = new QueueContentDialog
                        {
                            Title = "Error",
                            Content = "Folder name cannot be empty, rename failed",
                            CloseButtonText = "Confirm"
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

                    if (await dialog.ShowAsync() == ContentDialogResult.Primary)
                    {
                        _ = await Launcher.LaunchFolderAsync(CurrentFolder);
                    }
                }
            }
        }

        private async void CreateFolder_Click(object sender, RoutedEventArgs e)
        {
            if (!await CurrentFolder.CheckExist())
            {
                if (Globalization.Language == LanguageEnum.Chinese)
                {
                    QueueContentDialog Dialog = new QueueContentDialog
                    {
                        Title = "错误",
                        Content = "无法找到对应的文件夹，该文件夹可能已被移动或删除",
                        CloseButtonText = "刷新"
                    };
                    _ = await Dialog.ShowAsync();
                }
                else
                {
                    QueueContentDialog Dialog = new QueueContentDialog
                    {
                        Title = "Error",
                        Content = "Could not find the corresponding folder, it may have been moved or deleted",
                        CloseButtonText = "Refresh"
                    };
                    _ = await Dialog.ShowAsync();
                }
                return;
            }

            try
            {
                var NewFolder = Globalization.Language == LanguageEnum.Chinese
                    ? await CurrentFolder.CreateFolderAsync("新建文件夹", CreationCollisionOption.GenerateUniqueName)
                    : await CurrentFolder.CreateFolderAsync("New folder", CreationCollisionOption.GenerateUniqueName);

                var Size = await NewFolder.GetSizeDescriptionAsync();
                var Thumbnail = await NewFolder.GetThumbnailBitmapAsync() ?? new BitmapImage(new Uri("ms-appx:///Assets/DocIcon.png"));
                var ModifiedTime = await NewFolder.GetModifiedTimeAsync();

                FilePresenter.ThisPage.FileCollection.Add(new FileSystemStorageItem(NewFolder, Size, Thumbnail, ModifiedTime));

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

                if (await dialog.ShowAsync() == ContentDialogResult.Primary)
                {
                    _ = await Launcher.LaunchFolderAsync(CurrentFolder);
                }
            }
        }

        private async void FolderAttribute_Click(object sender, RoutedEventArgs e)
        {
            if (!await CurrentFolder.CheckExist())
            {
                if (Globalization.Language == LanguageEnum.Chinese)
                {
                    QueueContentDialog Dialog = new QueueContentDialog
                    {
                        Title = "错误",
                        Content = "无法找到对应的文件夹，该文件夹可能已被移动或删除",
                        CloseButtonText = "刷新"
                    };
                    _ = await Dialog.ShowAsync();
                }
                else
                {
                    QueueContentDialog Dialog = new QueueContentDialog
                    {
                        Title = "Error",
                        Content = "Could not find the corresponding folder, it may have been moved or deleted",
                        CloseButtonText = "Refresh"
                    };
                    _ = await Dialog.ShowAsync();
                }
                await DisplayItemsInFolder(CurrentNode, true);
                return;
            }

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

            if (!await folder.CheckExist())
            {
                if (Globalization.Language == LanguageEnum.Chinese)
                {
                    QueueContentDialog Dialog = new QueueContentDialog
                    {
                        Title = "错误",
                        Content = "无法找到对应的文件夹，该文件夹可能已被移动或删除",
                        CloseButtonText = "刷新"
                    };
                    _ = await Dialog.ShowAsync();
                }
                else
                {
                    QueueContentDialog Dialog = new QueueContentDialog
                    {
                        Title = "Error",
                        Content = "Could not find the corresponding folder, it may have been moved or deleted",
                        CloseButtonText = "Refresh"
                    };
                    _ = await Dialog.ShowAsync();
                }
                await DisplayItemsInFolder(CurrentNode, true);
                return;
            }

            if (ThisPC.ThisPage.LibraryFolderList.Any((Folder) => Folder.Folder.Path == folder.Path))
            {
                QueueContentDialog dialog = new QueueContentDialog
                {
                    Title = "提示",
                    Content = "此文件夹已经添加到主界面了，不能重复添加哦",
                    CloseButtonText = "知道了"
                };
                _ = await dialog.ShowAsync();
            }
            else
            {
                BitmapImage Thumbnail = await folder.GetThumbnailBitmapAsync();
                ThisPC.ThisPage.LibraryFolderList.Add(new LibraryFolder(folder, Thumbnail, LibrarySource.UserCustom));
                await SQLite.Current.SetFolderLibraryAsync(folder.Path);
            }
        }

        private async void GlobeSearch_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
        {
            if (string.IsNullOrWhiteSpace(args.QueryText))
            {
                return;
            }

            FlyoutBase.ShowAttachedFlyout(sender);

            await SQLite.Current.SetSearchHistoryAsync(args.QueryText);
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
                sender.ItemsSource = await SQLite.Current.GetRelatedSearchHistoryAsync(sender.Text);
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

                    Nav.Navigate(typeof(SearchPage), FileQuery, new DrillInNavigationTransitionInfo());
                }
                else
                {
                    SearchPage.ThisPage.SetSearchTarget = Options;
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
                GlobeSearch.ItemsSource = await SQLite.Current.GetRelatedSearchHistoryAsync(string.Empty);
            }
        }

        private async void AddressBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
        {
            try
            {
                StorageFile File = await StorageFile.GetFileFromPathAsync(args.QueryText);
                if (!await Launcher.LaunchFileAsync(File))
                {
                    LauncherOptions options = new LauncherOptions
                    {
                        DisplayApplicationPicker = true
                    };
                    _ = await Launcher.LaunchFileAsync(File, options);
                }
            }
            catch (Exception)
            {
                try
                {
                    StorageFolder Folder = await StorageFolder.GetFolderFromPathAsync(args.QueryText);

                    if (args.QueryText.StartsWith((FolderTree.RootNodes.First().Content as StorageFolder).Path))
                    {
                        var RootNode = FolderTree.RootNodes[0];
                        TreeViewNode TargetNode = await FindFolderLocationInTree(RootNode, new PathAnalysis(Folder.Path, (FolderTree.RootNodes[0].Content as StorageFolder).Path));
                        if (TargetNode != null)
                        {
                            await DisplayItemsInFolder(TargetNode);

                            await SQLite.Current.SetPathHistoryAsync(Folder.Path);
                        }
                    }
                    else
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


                        FolderTree.RootNodes.Clear();
                        FilePresenter.ThisPage.FileCollection.Clear();
                        FilePresenter.ThisPage.HasFile.Visibility = Visibility.Visible;

                        await SQLite.Current.SetPathHistoryAsync(Folder.Path);

                        MainPage.ThisPage.Nav.Content = new FileControl(Folder);
                    }
                }
                catch (Exception)
                {
                    if (Globalization.Language == LanguageEnum.Chinese)
                    {
                        QueueContentDialog dialog = new QueueContentDialog
                        {
                            Title = "错误",
                            Content = "无法找到路径为: \r" + args.QueryText + "的文件夹",
                            CloseButtonText = "确定"
                        };
                        _ = await dialog.ShowAsync();
                    }
                    else
                    {
                        QueueContentDialog dialog = new QueueContentDialog
                        {
                            Title = "Error",
                            Content = "Unable to locate folder: " + args.QueryText,
                            CloseButtonText = "Confirm",
                        };
                        _ = await dialog.ShowAsync();
                    }
                }
            }
        }

        private async Task<TreeViewNode> FindFolderLocationInTree(TreeViewNode Node, PathAnalysis Analysis)
        {
            if (Node.HasUnrealizedChildren && !Node.IsExpanded)
            {
                Node.IsExpanded = true;
            }

            await Node.SelectNode(FolderTree);

            string NextPathLevel = Analysis.NextPathLevel();

            if (NextPathLevel == Analysis.FullPath)
            {
                if ((Node.Content as StorageFolder).Path == NextPathLevel)
                {
                    return Node;
                }
                else
                {
                    while (true)
                    {
                        var TargetNode = Node.Children.Where((SubNode) => (SubNode.Content as StorageFolder).Path == NextPathLevel).FirstOrDefault();
                        if (TargetNode != null)
                        {
                            return TargetNode;
                        }
                        else
                        {
                            await Task.Delay(200);
                        }
                    }
                }
            }
            else
            {
                if ((Node.Content as StorageFolder).Path == NextPathLevel)
                {
                    return await FindFolderLocationInTree(Node, Analysis);
                }
                else
                {
                    while (true)
                    {
                        var TargetNode = Node.Children.Where((SubNode) => (SubNode.Content as StorageFolder).Path == NextPathLevel).FirstOrDefault();
                        if (TargetNode != null)
                        {
                            return await FindFolderLocationInTree(TargetNode, Analysis);
                        }
                        else
                        {
                            await Task.Delay(200);
                        }
                    }
                }
            }
        }

        private async void AddressBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
            {
                sender.ItemsSource = await SQLite.Current.GetRelatedPathHistoryAsync(sender.Text);
            }
        }

        public async void GoParentFolder_Click(object sender, RoutedEventArgs e)
        {
            if ((await CurrentFolder.GetParentAsync()) is StorageFolder ParentFolder)
            {
                var ParenetNode = await FindFolderLocationInTree(FolderTree.RootNodes[0], new PathAnalysis(ParentFolder.Path, (FolderTree.RootNodes[0].Content as StorageFolder).Path));
                await ParenetNode.SelectNode(FolderTree);
                await DisplayItemsInFolder(ParenetNode);
            }
        }

        private async void GoBackRecord_Click(object sender, RoutedEventArgs e)
        {
            RecordIndex--;
            string Path = GoAndBackRecord[RecordIndex].Path;
            try
            {
                StorageFolder Folder = await StorageFolder.GetFolderFromPathAsync(Path);

                IsBackOrForwardAction = true;
                if (Path.StartsWith((FolderTree.RootNodes.First().Content as StorageFolder).Path))
                {
                    var RootNode = FolderTree.RootNodes[0];
                    TreeViewNode TargetNode = await FindFolderLocationInTree(RootNode, new PathAnalysis(Folder.Path, (FolderTree.RootNodes[0].Content as StorageFolder).Path));
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
                            _ = await dialog.ShowAsync();
                        }
                        else
                        {
                            QueueContentDialog dialog = new QueueContentDialog
                            {
                                Title = "Error",
                                Content = "Unable to locate folder, which may have been deleted or moved",
                                CloseButtonText = "Confirm",
                            };
                            _ = await dialog.ShowAsync();
                        }
                    }
                    else
                    {
                        await TargetNode.SelectNode(FolderTree);

                        await DisplayItemsInFolder(TargetNode);

                        await SQLite.Current.SetPathHistoryAsync(Folder.Path);
                    }
                }
                else
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


                    FolderTree.RootNodes.Clear();
                    FilePresenter.ThisPage.FileCollection.Clear();
                    FilePresenter.ThisPage.HasFile.Visibility = Visibility.Visible;

                    await SQLite.Current.SetPathHistoryAsync(Folder.Path);

                    MainPage.ThisPage.Nav.Content = new FileControl(Folder);
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
                    _ = await dialog.ShowAsync();
                }
                else
                {
                    QueueContentDialog dialog = new QueueContentDialog
                    {
                        Title = "Error",
                        Content = "Unable to locate folder: " + Path,
                        CloseButtonText = "Confirm"
                    };
                    _ = await dialog.ShowAsync();
                }
                RecordIndex++;
            }
        }

        private async void GoForwardRecord_Click(object sender, RoutedEventArgs e)
        {
            RecordIndex++;
            string Path = GoAndBackRecord[RecordIndex].Path;
            try
            {
                StorageFolder Folder = await StorageFolder.GetFolderFromPathAsync(Path);

                IsBackOrForwardAction = true;
                if (Path.StartsWith((FolderTree.RootNodes.First().Content as StorageFolder).Path))
                {
                    var RootNode = FolderTree.RootNodes[0];
                    TreeViewNode TargetNode = await FindFolderLocationInTree(RootNode, new PathAnalysis(Folder.Path, (FolderTree.RootNodes[0].Content as StorageFolder).Path));
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
                            _ = await dialog.ShowAsync();
                        }
                        else
                        {
                            QueueContentDialog dialog = new QueueContentDialog
                            {
                                Title = "Error",
                                Content = "Unable to locate folder, which may have been deleted or moved",
                                CloseButtonText = "Confirm"
                            };
                            _ = await dialog.ShowAsync();
                        }
                    }
                    else
                    {
                        await TargetNode.SelectNode(FolderTree);

                        await DisplayItemsInFolder(TargetNode);

                        await SQLite.Current.SetPathHistoryAsync(Folder.Path);
                    }
                }
                else
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


                    FolderTree.RootNodes.Clear();
                    FilePresenter.ThisPage.FileCollection.Clear();
                    FilePresenter.ThisPage.HasFile.Visibility = Visibility.Visible;

                    await SQLite.Current.SetPathHistoryAsync(Folder.Path);

                    MainPage.ThisPage.Nav.Content = new FileControl(Folder);
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
                    _ = await dialog.ShowAsync();
                }
                else
                {
                    QueueContentDialog dialog = new QueueContentDialog
                    {
                        Title = "Error",
                        Content = "Unable to locate folder: " + Path,
                        CloseButtonText = "Confirm"
                    };
                    _ = await dialog.ShowAsync();
                }
                RecordIndex--;
            }
        }

        private async void AddressBox_GotFocus(object sender, RoutedEventArgs e)
        {
            IsSearchOrPathBoxFocused = true;

            AddressBox.ItemsSource = await SQLite.Current.GetRelatedPathHistoryAsync(string.Empty);
        }
    }

}
