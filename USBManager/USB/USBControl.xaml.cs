using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.Storage.Search;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;


namespace USBManager
{
    public sealed partial class USBControl : Page
    {
        public TreeViewNode CurrentNode { get; private set; }
        public StorageFolder CurrentFolder { get; private set; }
        public static USBControl ThisPage { get; private set; }
        private bool IsAdding = false;
        private string RootFolderId;
        private CancellationTokenSource CancelToken;
        private AutoResetEvent Locker;
        public FileSystemTracker FolderTracker;
        public FileSystemTracker FileTracker;


        public USBControl()
        {
            InitializeComponent();
            ThisPage = this;
            InitializeTreeView();
            Nav.Navigate(typeof(USBFilePresenter), Nav, new DrillInNavigationTransitionInfo());

            Application.Current.Suspending += Current_Suspending;
            FolderTracker = new FileSystemTracker(FolderTree.RootNodes.FirstOrDefault());
            FolderTracker.Created += FolderTracker_Created;
            FolderTracker.Deleted += FolderTracker_Deleted;
            FolderTracker.Renamed += FolderTracker_Renamed;
            Loaded += USBControl_Loaded;
        }

        private void Current_Suspending(object sender, Windows.ApplicationModel.SuspendingEventArgs e)
        {
            if (FileTracker != null)
            {
                FileTracker.Created -= FileTracker_Created;
                FileTracker.Deleted -= FileTracker_Deleted;
                FileTracker.Renamed -= FileTracker_Renamed;
                FileTracker.Dispose();
                FileTracker = null;
            }

            if (FolderTracker != null)
            {
                FolderTracker.Created -= FolderTracker_Created;
                FolderTracker.Deleted -= FolderTracker_Deleted;
                FolderTracker.Renamed -= FolderTracker_Renamed;
                FolderTracker.Dispose();
                FolderTracker = null;
            }
        }

        private void USBControl_Loaded(object sender, RoutedEventArgs e)
        {
            CancelToken = new CancellationTokenSource();
            Locker = new AutoResetEvent(false);
        }

        private async void FolderTracker_Renamed(object sender, FileSystemRenameSet e)
        {
            foreach (var SubNode in from StorageFolder OldFolder in e.ToDeleteFileList
                                    from SubNode in
                                        from SubNode in e.ParentNode.Children
                                        where (SubNode.Content as StorageFolder).FolderRelativeId == OldFolder.FolderRelativeId
                                        select SubNode
                                    select SubNode)
            {
                e.ParentNode.Children.Remove(SubNode);
            }

            foreach (StorageFolder NewFolder in e.ToAddFileList)
            {
                e.ParentNode.Children.Add(new TreeViewNode
                {
                    Content = NewFolder,
                    HasUnrealizedChildren = (await NewFolder.GetFoldersAsync()).Count != 0
                });
            }
        }

        private void FolderTracker_Deleted(object sender, FileSystemChangeSet e)
        {
            foreach (StorageFolder OldFolder in e.StorageItems)
            {
                foreach (var SubNode in from SubNode in e.ParentNode.Children
                                        where (SubNode.Content as StorageFolder).FolderRelativeId == OldFolder.FolderRelativeId
                                        select SubNode)
                {
                    if (FolderTree.SelectedNodes.FirstOrDefault() == SubNode)
                    {
                        USBFilePresenter.ThisPage.FileCollection.Clear();
                        USBFilePresenter.ThisPage.HasFile.Visibility = Visibility.Visible;
                    }
                    e.ParentNode.Children.Remove(SubNode);
                }
            }
        }

        private async void FolderTracker_Created(object sender, FileSystemChangeSet e)
        {
            foreach (StorageFolder NewFolder in e.StorageItems)
            {
                e.ParentNode.Children.Add(new TreeViewNode
                {
                    Content = NewFolder,
                    HasUnrealizedChildren = (await NewFolder.GetFoldersAsync()).Count != 0
                });
            }
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            Locker.Dispose();
            CancelToken.Dispose();
        }

        /// <summary>
        /// 执行文件目录的初始化，查找USB设备
        /// </summary>
        private async void InitializeTreeView()
        {
            StorageFolder RemovableFolder = KnownFolders.RemovableDevices;
            RootFolderId = RemovableFolder.FolderRelativeId;
            if (RemovableFolder != null)
            {
                TreeViewNode RemovableNode = new TreeViewNode
                {
                    Content = RemovableFolder,
                    IsExpanded = true,
                    HasUnrealizedChildren = true
                };
                FolderTree.RootNodes.Add(RemovableNode);
                await FillTreeNode(RemovableNode);
                if (RemovableNode.Children.Count == 0)
                {
                    RemovableNode.Children.Add(new TreeViewNode() { Content = new EmptyDeviceDisplay() });
                }
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
            if (folder.FolderRelativeId == RootFolderId)
            {
                //若当前节点为根节点，且在根节点下无任何文件夹被发现，说明无USB设备插入
                //因此清除根文件夹下的节点
                if (StorageFolderList.Count == 0)
                {
                    Node.Children.Clear();
                }
            }

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
            if ((args.Node.Content as StorageFolder).FolderRelativeId == RootFolderId)
            {
                if (args.Node.Children.Count == 0)
                {
                    args.Node.Children.Add(new TreeViewNode() { Content = new EmptyDeviceDisplay() });
                }
            }
        }

        /// <summary>
        /// 获得指定文件的缩略图图像
        /// </summary>
        /// <param name="file">文件</param>
        /// <returns>缩略图图像</returns>
        public async Task<BitmapImage> GetThumbnailAsync(StorageFile file)
        {
            var Thumbnail = await file.GetThumbnailAsync(ThumbnailMode.ListView);
            if (Thumbnail == null)
            {
                return null;
            }

            BitmapImage bitmapImage = new BitmapImage
            {
                DecodePixelHeight = 60,
                DecodePixelWidth = 60
            };
            await bitmapImage.SetSourceAsync(Thumbnail);
            return bitmapImage;
        }

        private async void FileTree_ItemInvoked(TreeView sender, TreeViewItemInvokedEventArgs args)
        {
            /*
             * 同一文件夹内可能存在大量文件
             * 因此切换不同文件夹时极有可能遍历文件夹仍未完成
             * 此处激活取消指令，等待当前遍历结束，再开始下一次文件遍历
             * 确保不会出现异常
             */
            //防止多次点击同一文件夹导致的多重查找            
            if ((args.InvokedItem as TreeViewNode).Content is StorageFolder folder)
            {
                if (folder.FolderRelativeId == CurrentFolder?.FolderRelativeId)
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
                        }
                    });
                }
                IsAdding = true;

                CurrentFolder = folder;
                CurrentNode = args.InvokedItem as TreeViewNode;
                USBFilePresenter.ThisPage.DisplayNode = CurrentNode;

                //当处于USB其他附加功能的页面时，若点击文件目录则自动执行返回导航
                if (Nav.CurrentSourcePageType.Name != "USBFilePresenter")
                {
                    Nav.GoBack();
                }

                USBFilePresenter.ThisPage.FileCollection.Clear();

                QueryOptions Options = new QueryOptions(CommonFileQuery.DefaultQuery, null)
                {
                    FolderDepth = FolderDepth.Shallow,
                    IndexerOption = IndexerOption.UseIndexerWhenAvailable
                };

                Options.SetThumbnailPrefetch(ThumbnailMode.ListView, 60, ThumbnailOptions.ResizeThumbnail);
                Options.SetPropertyPrefetch(PropertyPrefetchOptions.BasicProperties, new string[] { "System.Size" });

                var FileQuery = folder.CreateFileQueryWithOptions(Options);

                var FileList = await FileQuery.GetFilesAsync();

                USBFilePresenter.ThisPage.HasFile.Visibility = FileList.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

                if (FileTracker != null)
                {
                    FileTracker.Created -= FileTracker_Created;
                    FileTracker.Deleted -= FileTracker_Deleted;
                    FileTracker.Renamed -= FileTracker_Renamed;
                    FileTracker.Dispose();
                    FileTracker = null;
                }

                FileTracker = new FileSystemTracker(FileQuery);
                FileTracker.Created += FileTracker_Created;
                FileTracker.Deleted += FileTracker_Deleted;
                FileTracker.Renamed += FileTracker_Renamed;

                foreach (var file in FileList)
                {
                    if (CancelToken.IsCancellationRequested)
                    {
                        goto FLAG;
                    }

                    IDictionary<string, object> PropertyResults = await file.Properties.RetrievePropertiesAsync(new string[] { "System.Size" });
                    ulong PropertiesSize = Convert.ToUInt64(PropertyResults["System.Size"]);
                    string Size = GetSizeDescription(PropertiesSize);

                    BitmapImage Thumbnail = await GetThumbnailAsync(file);

                    USBFilePresenter.ThisPage.FileCollection.Add(new RemovableDeviceFile(file, Thumbnail, Size));
                }
            }

        FLAG:
            if (CancelToken.IsCancellationRequested)
            {
                CancelToken.Dispose();
                CancelToken = new CancellationTokenSource();
                Locker.Set();
            }
            else
            {
                IsAdding = false;
            }
        }

        private async void FileTracker_Renamed(object sender, FileSystemRenameSet e)
        {
            for (int i = 0; i < e.ToDeleteFileList.Count; i++)
            {
                for (int j = 0; j < USBFilePresenter.ThisPage.FileCollection.Count; j++)
                {
                    if (USBFilePresenter.ThisPage.FileCollection[j].RelativeId == ((StorageFile)e.ToDeleteFileList[i]).FolderRelativeId)
                    {
                        USBFilePresenter.ThisPage.FileCollection.RemoveAt(j);
                        j--;
                    }
                }
            }

            foreach (StorageFile ExceptFile in e.ToAddFileList)
            {
                BitmapImage Thumbnail = await GetThumbnailAsync(ExceptFile);
                USBFilePresenter.ThisPage.FileCollection.Add(new RemovableDeviceFile(ExceptFile, Thumbnail, await ExceptFile.GetSizeDescriptionAsync()));
            }
        }

        private void FileTracker_Deleted(object sender, FileSystemChangeSet e)
        {
            for (int i = 0; i < e.StorageItems.Count; i++)
            {
                for (int j = 0; j < USBFilePresenter.ThisPage.FileCollection.Count; j++)
                {
                    RemovableDeviceFile DeviceFile = USBFilePresenter.ThisPage.FileCollection[j];
                    if (DeviceFile.RelativeId == ((StorageFile)e.StorageItems[i]).FolderRelativeId)
                    {
                        USBFilePresenter.ThisPage.FileCollection.Remove(DeviceFile);
                        j--;
                    }
                }
            }
        }

        private async void FileTracker_Created(object sender, FileSystemChangeSet e)
        {
            foreach (StorageFile ExceptFile in e.StorageItems)
            {
                BitmapImage Thumbnail = await GetThumbnailAsync(ExceptFile);

                USBFilePresenter.ThisPage.FileCollection.Add(new RemovableDeviceFile(ExceptFile, Thumbnail, await ExceptFile.GetSizeDescriptionAsync()));
            }
        }

        /// <summary>
        /// 从文件大小获取标准描述
        /// </summary>
        /// <param name="PropertiesSize">文件大小</param>
        /// <returns></returns>
        private string GetSizeDescription(ulong PropertiesSize)
        {
            return PropertiesSize / 1024f < 1024 ? Math.Round(PropertiesSize / 1024f, 2).ToString() + " KB" : (PropertiesSize / 1048576f >= 1024 ? Math.Round(PropertiesSize / 1073741824f, 2).ToString() + " GB" : Math.Round(PropertiesSize / 1048576f, 2).ToString() + " MB");
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
                CloseButtonText = "取消"
            };
            if (await contentDialog.ShowAsync() == ContentDialogResult.Primary)
            {
                try
                {
                    FileTracker?.PauseDetection();
                    FolderTracker?.PauseDetection();

                    StorageFolder Folder = CurrentNode.Content as StorageFolder;
                    await DeleteAllSubFilesAndFolders(Folder);
                    await Folder.DeleteAsync((bool)ApplicationData.Current.LocalSettings.Values["EnableDirectDelete"] ? StorageDeleteOption.PermanentDelete : StorageDeleteOption.Default);

                    if (USBFilePresenter.ThisPage.DisplayNode == CurrentNode)
                    {
                        USBFilePresenter.ThisPage.FileCollection.Clear();
                        USBFilePresenter.ThisPage.HasFile.Visibility = Visibility.Visible;
                    }

                    TreeViewNode ParentNode = CurrentNode.Parent;
                    ParentNode.Children.Remove(CurrentNode);
                    CurrentNode = ParentNode;
                }
                catch (Exception)
                {
                    ContentDialog Dialog = new ContentDialog
                    {
                        Title = "错误",
                        Content = "删除文件夹时出现错误",
                        CloseButtonText = "确定"
                    };
                    _ = await Dialog.ShowAsync();
                }
                finally
                {
                    FileTracker?.ResumeDetection();
                    FolderTracker?.ResumeDetection();
                }
            }
        }

        private async Task DeleteAllSubFilesAndFolders(StorageFolder Folder)
        {
            IReadOnlyList<IStorageItem> ItemList = await Folder.GetItemsAsync();
            foreach (var Item in ItemList)
            {
                if (Item is StorageFolder folder)
                {
                    await DeleteAllSubFilesAndFolders(folder);
                }
                else
                {
                    await Item.DeleteAsync((bool)ApplicationData.Current.LocalSettings.Values["EnableDirectDelete"] ? StorageDeleteOption.PermanentDelete : StorageDeleteOption.Default);
                }
            }
        }

        private async Task UpdateAllSubNodeFolder(TreeViewNode ParentNode)
        {
            StorageFolder ParentFolder = ParentNode.Content as StorageFolder;
            foreach (var Package in ParentNode.Children.Select((SubNode) => new { (SubNode.Content as StorageFolder).Name, SubNode }))
            {
                Package.SubNode.Content = await ParentFolder.GetFolderAsync(Package.Name);

                if (Package.SubNode.HasChildren)
                {
                    await UpdateAllSubNodeFolder(Package.SubNode);
                }
            }
        }

        private void FileTree_Collapsed(TreeView sender, TreeViewCollapsedEventArgs args)
        {
            args.Node.Children.Clear();
            args.Node.HasUnrealizedChildren = true;
        }

        private void FolderTree_RightTapped(object sender, Windows.UI.Xaml.Input.RightTappedRoutedEventArgs e)
        {
            if ((CurrentNode = (e.OriginalSource as FrameworkElement)?.DataContext as TreeViewNode) != null)
            {
                CreateFolder.IsEnabled = FolderTree.RootNodes[0] != CurrentNode;
                if (FolderTree.RootNodes[0].Children.Contains(CurrentNode))
                {
                    FolderDelete.IsEnabled = false;
                    FolderRename.IsEnabled = false;
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
            var Folder = CurrentNode.Content as StorageFolder;
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
                        Background = Resources["SystemControlChromeHighAcrylicWindowMediumBrush"] as Brush
                    };
                    await content.ShowAsync();
                    return;
                }
                FileTracker?.PauseDetection();
                FolderTracker?.PauseDetection();

                await Folder.RenameAsync(renameDialog.DesireName, NameCollisionOption.GenerateUniqueName);

                var ChildCollection = CurrentNode.Parent.Children;
                int index = CurrentNode.Parent.Children.IndexOf(CurrentNode);

                if (CurrentNode.HasUnrealizedChildren)
                {
                    ChildCollection.Insert(index, new TreeViewNode()
                    {
                        Content = Folder,
                        HasUnrealizedChildren = true,
                        IsExpanded = false
                    });
                    ChildCollection.Remove(CurrentNode);
                }
                else if (CurrentNode.HasChildren)
                {
                    var NewNode = new TreeViewNode()
                    {
                        Content = Folder,
                        HasUnrealizedChildren = false,
                        IsExpanded = true
                    };

                    foreach (var SubNode in CurrentNode.Children)
                    {
                        NewNode.Children.Add(SubNode);
                    }

                    ChildCollection.Insert(index, NewNode);
                    ChildCollection.Remove(CurrentNode);
                    await UpdateAllSubNodeFolder(NewNode);
                }


                FileTracker?.ResumeDetection();
                FolderTracker?.ResumeDetection();
            }
        }

        private async void CreateFolder_Click(object sender, RoutedEventArgs e)
        {
            FileTracker?.PauseDetection();
            FolderTracker?.PauseDetection();

            var CurrentFolder = (CurrentNode.Content as StorageFolder);
            var NewFolder = await CurrentFolder.CreateFolderAsync("新建文件夹", CreationCollisionOption.GenerateUniqueName);

            if (CurrentNode.IsExpanded || !CurrentNode.HasChildren)
            {
                CurrentNode.Children.Add(new TreeViewNode
                {
                    Content = NewFolder,
                    HasUnrealizedChildren = false
                });
            }
            CurrentNode.IsExpanded = true;

            FileTracker?.ResumeDetection();
            FolderTracker?.ResumeDetection();
        }
    }

}
