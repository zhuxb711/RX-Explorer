using FileManager.Class;
using FileManager.Dialog;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.Storage;
using Windows.Storage.Search;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;
using TreeViewNode = Microsoft.UI.Xaml.Controls.TreeViewNode;

namespace FileManager
{
    public sealed partial class SearchPage : Page
    {
        private ObservableCollection<FileSystemStorageItem> SearchResult;
        private StorageItemQueryResult ItemQuery;
        private CancellationTokenSource Cancellation;
        private FileControl FileControlInstance;

        public QueryOptions SetSearchTarget
        {
            get
            {
                return ItemQuery.GetCurrentQueryOptions();
            }
            set
            {
                SearchResult.Clear();

                ItemQuery.ApplyNewQueryOptions(value);

                _ = Initialize();
            }
        }

        public SearchPage()
        {
            InitializeComponent();
            SearchResult = new ObservableCollection<FileSystemStorageItem>();
            SearchResultList.ItemsSource = SearchResult;
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            if (e.Parameter is Tuple<FileControl, StorageItemQueryResult> Parameters)
            {
                FileControlInstance = Parameters.Item1;
                ItemQuery = Parameters.Item2;

                if (!TabViewContainer.ThisPage.FSInstanceContainer.ContainsKey(FileControlInstance))
                {
                    TabViewContainer.ThisPage.FSInstanceContainer.Add(FileControlInstance, this);
                }

                await Initialize().ConfigureAwait(false);
            }
        }

        private async Task Initialize()
        {
            HasItem.Visibility = Visibility.Collapsed;

            LoadingControl.IsLoading = true;
            MainPage.ThisPage.IsAnyTaskRunning = true;

            IReadOnlyList<IStorageItem> SearchItems = null;

            try
            {
                Cancellation = new CancellationTokenSource();
                Cancellation.Token.Register(() =>
                {
                    HasItem.Visibility = Visibility.Visible;
                    SearchResultList.Visibility = Visibility.Collapsed;
                    LoadingControl.IsLoading = false;
                    MainPage.ThisPage.IsAnyTaskRunning = false;
                });

                IAsyncOperation<IReadOnlyList<IStorageItem>> SearchAsync = ItemQuery.GetItemsAsync(0, 100);

                SearchItems = await SearchAsync.AsTask(Cancellation.Token).ConfigureAwait(true);
            }
            catch (TaskCanceledException)
            {
                return;
            }
            finally
            {
                Cancellation?.Dispose();
                Cancellation = null;
            }

            await Task.Delay(1500).ConfigureAwait(true);

            LoadingControl.IsLoading = false;
            MainPage.ThisPage.IsAnyTaskRunning = false;

            if (SearchItems.Count == 0)
            {
                HasItem.Visibility = Visibility.Visible;
            }
            else
            {
                foreach (var Item in SearchItems)
                {
                    if (Item is StorageFile File)
                    {
                        SearchResult.Add(new FileSystemStorageItem(File, await Item.GetSizeRawDataAsync().ConfigureAwait(true), await Item.GetThumbnailBitmapAsync().ConfigureAwait(true) ?? new BitmapImage(new Uri("ms-appx:///Assets/DocIcon.png")), await Item.GetModifiedTimeAsync().ConfigureAwait(true)));
                    }
                    else if (Item is StorageFolder Folder)
                    {
                        SearchResult.Add(new FileSystemStorageItem(Folder, await Item.GetModifiedTimeAsync().ConfigureAwait(true)));
                    }
                }
            }
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            SearchResult.Clear();
            Cancellation?.Cancel();
        }

        private async void Location_Click(object sender, RoutedEventArgs e)
        {
            if (SearchResultList.SelectedItem is FileSystemStorageItem RemoveFile)
            {
                if (RemoveFile.StorageType == StorageItemTypes.Folder)
                {
                    try
                    {
                        if (SettingControl.IsDetachTreeViewAndPresenter)
                        {
                            StorageFolder Folder = await StorageFolder.GetFolderFromPathAsync(RemoveFile.Path);

                            await FileControlInstance.DisplayItemsInFolder(Folder).ConfigureAwait(false);
                        }
                        else
                        {
                            TreeViewNode TargetNode = await FindFolderLocationInTree(FileControlInstance.FolderTree.RootNodes[0], new PathAnalysis(RemoveFile.Path, (FileControlInstance.FolderTree.RootNodes[0].Content as StorageFolder).Path)).ConfigureAwait(true);
                            if (TargetNode != null)
                            {
                                FileControlInstance.FolderTree.SelectNode(TargetNode);
                                await FileControlInstance.DisplayItemsInFolder(TargetNode).ConfigureAwait(false);
                            }
                            else
                            {
                                throw new Exception();
                            }
                        }
                    }
                    catch
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
                }
                else
                {
                    try
                    {
                        StorageFile File = await StorageFile.GetFileFromPathAsync(RemoveFile.Path);

                        if (SettingControl.IsDetachTreeViewAndPresenter)
                        {
                            await FileControlInstance.DisplayItemsInFolder(await File.GetParentAsync()).ConfigureAwait(false);
                        }
                        else
                        {
                            TreeViewNode CurrentNode = await FindFolderLocationInTree(FileControlInstance.FolderTree.RootNodes[0], new PathAnalysis((await ((StorageFile)await RemoveFile.GetStorageItem()).GetParentAsync()).Path, (FileControlInstance.FolderTree.RootNodes[0].Content as StorageFolder).Path)).ConfigureAwait(true);

                            FileControlInstance.FolderTree.SelectNode(CurrentNode);

                            await FileControlInstance.DisplayItemsInFolder(CurrentNode).ConfigureAwait(false);
                        }
                    }
                    catch (FileNotFoundException)
                    {
                        if (Globalization.Language == LanguageEnum.Chinese)
                        {
                            QueueContentDialog dialog = new QueueContentDialog
                            {
                                Title = "错误",
                                Content = "无法定位文件，该文件可能已被删除或移动",
                                CloseButtonText = "确定"
                            };
                            _ = await dialog.ShowAsync().ConfigureAwait(true);
                        }
                        else
                        {
                            QueueContentDialog dialog = new QueueContentDialog
                            {
                                Title = "Error",
                                Content = "Unable to locate file, which may have been deleted or moved",
                                CloseButtonText = "Confirm"
                            };
                            _ = await dialog.ShowAsync().ConfigureAwait(true);
                        }
                    }
                }
            }
        }

        private async void Attribute_Click(object sender, RoutedEventArgs e)
        {
            FileSystemStorageItem Device = SearchResultList.SelectedItems.FirstOrDefault() as FileSystemStorageItem;
            AttributeDialog Dialog = new AttributeDialog(await Device.GetStorageItem());
            _ = await Dialog.ShowAsync().ConfigureAwait(true);
        }

        private async Task<TreeViewNode> FindFolderLocationInTree(TreeViewNode Node, PathAnalysis Analysis)
        {
            if (Node.HasUnrealizedChildren && !Node.IsExpanded)
            {
                Node.IsExpanded = true;
            }

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
                            await Task.Delay(300).ConfigureAwait(true);
                        }
                    }
                }
            }
            else
            {
                if ((Node.Content as StorageFolder).Path == NextPathLevel)
                {
                    return await FindFolderLocationInTree(Node, Analysis).ConfigureAwait(true);
                }
                else
                {
                    while (true)
                    {
                        var TargetNode = Node.Children.Where((SubNode) => (SubNode.Content as StorageFolder).Path == NextPathLevel).FirstOrDefault();
                        if (TargetNode != null)
                        {
                            return await FindFolderLocationInTree(TargetNode, Analysis).ConfigureAwait(true);
                        }
                        else
                        {
                            await Task.Delay(300).ConfigureAwait(true);
                        }
                    }
                }
            }
        }

        private void SearchResultList_RightTapped(object sender, Windows.UI.Xaml.Input.RightTappedRoutedEventArgs e)
        {
            FileSystemStorageItem Context = (e.OriginalSource as FrameworkElement)?.DataContext as FileSystemStorageItem;
            SearchResultList.SelectedIndex = SearchResult.IndexOf(Context);
            e.Handled = true;
        }

        private void SearchResultList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Location.IsEnabled = SearchResultList.SelectedIndex != -1;
        }

        private void CopyPath_Click(object sender, RoutedEventArgs e)
        {
            if (SearchResultList.SelectedItem is FileSystemStorageItem SelectItem)
            {
                DataPackage Package = new DataPackage();
                Package.SetText(SelectItem.Path);
                Clipboard.SetContent(Package);
            }
        }
    }
}
