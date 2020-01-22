using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel;
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

                    string PlaceText = string.Empty;

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

                    GoParentFolder.IsEnabled = CurrentNode != FolderTree.RootNodes[0];
                    GoBackRecord.IsEnabled = RecordIndex > 0;
                    GoForwardRecord.IsEnabled = RecordIndex < GoAndBackRecord.Count - 1;
                }

                currentnode = value;
            }
        }

        private int TextChangeLockResource = 0;

        private int AddressButtonLockResource = 0;

        private string AddressBoxTextBackup;

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
        private ObservableCollection<string> AddressButtonList = new ObservableCollection<string>();
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
                    if (CurrentFolder == null)
                    {
                        string RootPath = Path.GetPathRoot(Folder.Path);

                        StorageFolder DriveRootFolder = await StorageFolder.GetFolderFromPathAsync(RootPath);
                        AddressButtonList.Add(DriveRootFolder.DisplayName);

                        PathAnalysis Analysis = new PathAnalysis(Folder.Path, RootPath);

                        while (Analysis.HasNextLevel)
                        {
                            AddressButtonList.Add(Analysis.NextRelativePath());
                        }
                    }
                    else
                    {
                        string OriginalString = string.Join("\\", AddressButtonList.Skip(1));
                        string ActualString = Path.Combine(Path.GetPathRoot(CurrentFolder.Path), OriginalString);
                        List<string> IntersectList = Folder.Path.Split('\\', StringSplitOptions.RemoveEmptyEntries).Intersect(ActualString.Split('\\', StringSplitOptions.RemoveEmptyEntries)).ToList();
                        if (IntersectList.Count == 0)
                        {
                            AddressButtonList.Clear();

                            string RootPath = Path.GetPathRoot(Folder.Path);

                            StorageFolder DriveRootFolder = await StorageFolder.GetFolderFromPathAsync(RootPath);
                            AddressButtonList.Add(DriveRootFolder.DisplayName);

                            PathAnalysis Analysis = new PathAnalysis(Folder.Path, RootPath);

                            while (Analysis.HasNextLevel)
                            {
                                AddressButtonList.Add(Analysis.NextRelativePath());
                            }
                        }
                        else
                        {
                            for (int i = AddressButtonList.Count - 1; i >= 0; i--)
                            {
                                if (AddressButtonList[i] != IntersectList.Last())
                                {
                                    if (AddressButtonList.Count == 1)
                                    {
                                        break;
                                    }
                                    else
                                    {
                                        AddressButtonList.RemoveAt(i);
                                    }
                                }
                                else
                                {
                                    break;
                                }
                            }

                            List<string> ExceptList = Folder.Path.Split('\\', StringSplitOptions.RemoveEmptyEntries).Except(IntersectList).ToList();

                            foreach (string SubPath in ExceptList)
                            {
                                AddressButtonList.Add(SubPath);
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
                    if (AddressButtonScrollViewer.ActualWidth < AddressButtonScrollViewer.ExtentWidth)
                    {
                        AddressButtonScrollViewer.ChangeView(AddressButtonScrollViewer.ExtentWidth, null, null);
                    }

                    _ = Interlocked.Exchange(ref AddressButtonLockResource, 0);
                }
            }
        }

        private async void OpenTargetFolder(StorageFolder Folder)
        {
            FilePresenter.ThisPage.FileCollection.Clear();
            FolderTree.RootNodes.Clear();
            FilePresenter.ThisPage.HasFile.Visibility = Visibility.Collapsed;

            StorageFolder RootFolder = await StorageFolder.GetFolderFromPathAsync(Path.GetPathRoot(Folder.Path));

            uint ItemCount = await RootFolder.CreateFolderQuery(CommonFolderQuery.DefaultQuery).GetItemCountAsync();
            TreeViewNode RootNode = new TreeViewNode
            {
                Content = RootFolder,
                IsExpanded = ItemCount != 0,
                HasUnrealizedChildren = ItemCount != 0
            };
            FolderTree.RootNodes.Add(RootNode);

            await FillTreeNode(RootNode);


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
                await DisplayItemsInFolder(TargetNode);
            }
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            if (e.Parameter is StorageFolder TargetFolder)
            {
                TreeViewNode RootNode = await Initialize(TargetFolder);
                await DisplayItemsInFolder(RootNode);

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
            AddressButtonList.Clear();
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
            FilePresenter.ThisPage.HasFile.Visibility = Visibility.Collapsed;

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
        private async Task<TreeViewNode> Initialize(StorageFolder InitFolder)
        {
            if (InitFolder != null)
            {
                FolderTree.RootNodes.Clear();

                uint ItemCount = await InitFolder.CreateFolderQuery(CommonFolderQuery.DefaultQuery).GetItemCountAsync();
                TreeViewNode RootNode = new TreeViewNode
                {
                    Content = InitFolder,
                    IsExpanded = ItemCount != 0,
                    HasUnrealizedChildren = ItemCount != 0
                };
                FolderTree.RootNodes.Add(RootNode);

                CancelToken?.Dispose();
                CancelToken = new CancellationTokenSource();
                Locker = new AutoResetEvent(false);
                FolderExpandCancel = new CancellationTokenSource();
                ExitLocker = new ManualResetEvent(true);

                await FillTreeNode(RootNode);

                return RootNode;
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// 向特定TreeViewNode节点下添加子节点
        /// </summary>
        /// <param name="Node">节点</param>
        /// <returns></returns>
        public async Task FillTreeNode(TreeViewNode Node)
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
            catch (Exception ex)
            {
                ExceptionTracer.RequestBlueScreen(ex);
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

            try
            {
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
                    await FolderTree.SelectNode(CurrentNode);

                    //当处于USB其他附加功能的页面时，若点击文件目录则自动执行返回导航
                    if (Nav.CurrentSourcePageType.Name != "FilePresenter")
                    {
                        Nav.GoBack();
                    }

                    FilePresenter.ThisPage.FileCollection.Clear();

                    QueryOptions Options = new QueryOptions(CommonFolderQuery.DefaultQuery)
                    {
                        FolderDepth = FolderDepth.Shallow,
                        IndexerOption = IndexerOption.UseIndexerWhenAvailable,
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

                await DisplayItemsInFolder(Node);

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
            FilePresenter.ThisPage.LoadingControl.Focus(FocusState.Programmatic);

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
                ApplicationData.Current.LocalSettings.Values["ExcutePath"] = ExcutePath;
                await FullTrustProcessLauncher.LaunchFullTrustProcessForCurrentAppAsync();
                return;
            }

            if (string.Equals(QueryText, "Cmd", StringComparison.OrdinalIgnoreCase))
            {
                string ExcutePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32\\cmd.exe");
                ApplicationData.Current.LocalSettings.Values["ExcutePath"] = ExcutePath;
                await FullTrustProcessLauncher.LaunchFullTrustProcessForCurrentAppAsync();
                return;
            }

            try
            {
                if (Path.IsPathRooted(QueryText) && ThisPC.ThisPage.HardDeviceList.Any((Drive) => Drive.Folder.Path == Path.GetPathRoot(QueryText)))
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
                        _ = await dialog.ShowAsync();
                    }
                    else
                    {
                        QueueContentDialog dialog = new QueueContentDialog
                        {
                            Title = "Error",
                            Content = $"Unable to locate the path: \r\"{QueryText}\"",
                            CloseButtonText = "Confirm",
                        };
                        _ = await dialog.ShowAsync();
                    }
                }
            }
            catch (Exception)
            {
                try
                {
                    StorageFolder Folder = await StorageFolder.GetFolderFromPathAsync(QueryText);
                    TreeViewNode RootNode = FolderTree.RootNodes[0];

                    if (QueryText.StartsWith((RootNode.Content as StorageFolder).Path))
                    {
                        TreeViewNode TargetNode = await FindFolderLocationInTree(RootNode, new PathAnalysis(Folder.Path, (RootNode.Content as StorageFolder).Path));
                        if (TargetNode != null)
                        {
                            await DisplayItemsInFolder(TargetNode);

                            await SQLite.Current.SetPathHistoryAsync(Folder.Path);
                        }
                    }
                    else
                    {
                        await SQLite.Current.SetPathHistoryAsync(Folder.Path);

                        OpenTargetFolder(Folder);
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
                        _ = await dialog.ShowAsync();
                    }
                    else
                    {
                        QueueContentDialog dialog = new QueueContentDialog
                        {
                            Title = "Error",
                            Content = $"Unable to locate the path: \r\"{QueryText}\"",
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

            await FolderTree.SelectNode(Node);

            string NextPathLevel = Analysis.NextFullPath();

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
            AddressBoxTextBackup = sender.Text;

            if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
            {
                if (Path.IsPathRooted(sender.Text)
                    && Path.GetDirectoryName(sender.Text) is string DirectoryName
                    && ThisPC.ThisPage.HardDeviceList.Any((Drive) => Drive.Folder.Path == Path.GetPathRoot(sender.Text)))
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

        public async void GoParentFolder_Click(object sender, RoutedEventArgs e)
        {
            if ((await CurrentFolder.GetParentAsync()) is StorageFolder ParentFolder)
            {
                var ParenetNode = await FindFolderLocationInTree(FolderTree.RootNodes[0], new PathAnalysis(ParentFolder.Path, (FolderTree.RootNodes[0].Content as StorageFolder).Path));
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
                        await DisplayItemsInFolder(TargetNode);

                        await SQLite.Current.SetPathHistoryAsync(Folder.Path);
                    }
                }
                else
                {
                    await SQLite.Current.SetPathHistoryAsync(Folder.Path);

                    OpenTargetFolder(Folder);
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
                        await DisplayItemsInFolder(TargetNode);

                        await SQLite.Current.SetPathHistoryAsync(Folder.Path);
                    }
                }
                else
                {
                    await SQLite.Current.SetPathHistoryAsync(Folder.Path);

                    OpenTargetFolder(Folder);
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

            if (string.IsNullOrEmpty(AddressBox.Text))
            {
                AddressBox.Text = CurrentFolder.Path;
            }
            AddressButtonScrollViewer.Visibility = Visibility.Collapsed;

            AddressBox.ItemsSource = await SQLite.Current.GetRelatedPathHistoryAsync();
        }

        private void ItemDisplayMode_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ApplicationData.Current.LocalSettings.Values["FilePresenterDisplayMode"] = ItemDisplayMode.SelectedIndex;

            switch (ItemDisplayMode.SelectedIndex)
            {
                case 0:
                    {
                        FilePresenter.ThisPage.GridViewControl.ItemTemplate = FilePresenter.ThisPage.TileDataTemplate;

                        if (!FilePresenter.ThisPage.UseGridOrList)
                        {
                            FilePresenter.ThisPage.UseGridOrList = true;
                        }
                        break;
                    }
                case 1:
                    {
                        FilePresenter.ThisPage.ListViewControl.HeaderTemplate = FilePresenter.ThisPage.ListHeaderDataTemplate;
                        FilePresenter.ThisPage.ListViewControl.ItemTemplate = FilePresenter.ThisPage.ListViewDetailDataTemplate;
                        FilePresenter.ThisPage.ListViewControl.ItemsSource = FilePresenter.ThisPage.FileCollection;

                        if (FilePresenter.ThisPage.UseGridOrList)
                        {
                            FilePresenter.ThisPage.UseGridOrList = false;
                        }
                        break;
                    }

                case 2:
                    {
                        FilePresenter.ThisPage.ListViewControl.HeaderTemplate = null;
                        FilePresenter.ThisPage.ListViewControl.ItemTemplate = FilePresenter.ThisPage.ListViewSimpleDataTemplate;
                        FilePresenter.ThisPage.ListViewControl.ItemsSource = FilePresenter.ThisPage.FileCollection;

                        if (FilePresenter.ThisPage.UseGridOrList)
                        {
                            FilePresenter.ThisPage.UseGridOrList = false;
                        }
                        break;
                    }
                case 3:
                    {
                        FilePresenter.ThisPage.GridViewControl.ItemTemplate = FilePresenter.ThisPage.LargeImageDataTemplate;

                        if (!FilePresenter.ThisPage.UseGridOrList)
                        {
                            FilePresenter.ThisPage.UseGridOrList = true;
                        }
                        break;
                    }
                case 4:
                    {
                        FilePresenter.ThisPage.GridViewControl.ItemTemplate = FilePresenter.ThisPage.MediumImageDataTemplate;

                        if (!FilePresenter.ThisPage.UseGridOrList)
                        {
                            FilePresenter.ThisPage.UseGridOrList = true;
                        }
                        break;
                    }
                case 5:
                    {
                        FilePresenter.ThisPage.GridViewControl.ItemTemplate = FilePresenter.ThisPage.SmallImageDataTemplate;

                        if (!FilePresenter.ThisPage.UseGridOrList)
                        {
                            FilePresenter.ThisPage.UseGridOrList = true;
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
            string OriginalString = string.Join("\\", AddressButtonList.Take(AddressButtonList.IndexOf(((Button)sender).Content.ToString()) + 1).Skip(1));
            string ActualString = Path.Combine(Path.GetPathRoot(CurrentFolder.Path), OriginalString);

            if (ActualString == CurrentFolder.Path)
            {
                return;
            }

            StorageFolder Folder = await StorageFolder.GetFolderFromPathAsync(ActualString);
            TreeViewNode RootNode = FolderTree.RootNodes[0];

            if (ActualString.StartsWith((RootNode.Content as StorageFolder).Path))
            {
                TreeViewNode TargetNode = await FindFolderLocationInTree(RootNode, new PathAnalysis(Folder.Path, (RootNode.Content as StorageFolder).Path));
                if (TargetNode != null)
                {
                    await DisplayItemsInFolder(TargetNode);

                    await SQLite.Current.SetPathHistoryAsync(Folder.Path);
                }
            }
            else
            {
                await SQLite.Current.SetPathHistoryAsync(Folder.Path);

                OpenTargetFolder(Folder);
            }
        }
    }

}
