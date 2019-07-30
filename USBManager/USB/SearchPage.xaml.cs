using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Storage;
using Windows.Storage.Search;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace USBManager
{
    public sealed partial class SearchPage : Page
    {
        ObservableCollection<RemovableDeviceStorageItem> SearchResult;
        StorageItemQueryResult ItemQuery;
        CancellationTokenSource Cancellation;

        public static SearchPage ThisPage { get; private set; }

        public string SetSearchTarget
        {
            set
            {
                SearchResult.Clear();

                QueryOptions NewOption = ItemQuery.GetCurrentQueryOptions();
                NewOption.ApplicationSearchFilter = "System.FileName:*" + value + "*";
                ItemQuery.ApplyNewQueryOptions(NewOption);

                SearchPage_Loaded(null, null);
            }
        }

        public SearchPage()
        {
            InitializeComponent();
            ThisPage = this;
            SearchResult = new ObservableCollection<RemovableDeviceStorageItem>();
            SearchResultList.ItemsSource = SearchResult;
        }

        private async void SearchPage_Loaded(object sender, RoutedEventArgs e)
        {
            Loaded -= SearchPage_Loaded;

            uint MaxSearchNum = uint.Parse(ApplicationData.Current.LocalSettings.Values["SetSearchResultMaxNum"] as string);

            LoadingControl.IsLoading = true;
            IReadOnlyList<IStorageItem> SearchItems = null;

            try
            {
                Cancellation = new CancellationTokenSource();

                IAsyncOperation<IReadOnlyList<IStorageItem>> SearchAsync = ItemQuery.GetItemsAsync(0, MaxSearchNum);
                Cancellation.Token.Register((SearchOperation) =>
                {
                    (SearchOperation as IAsyncOperation<IReadOnlyList<IStorageItem>>).Cancel();
                }, SearchAsync);

                SearchItems = await SearchAsync;
            }
            catch (TaskCanceledException)
            {
                HasItem.Visibility = Visibility.Visible;
                SearchResultList.Visibility = Visibility.Collapsed;
                LoadingControl.IsLoading = false;
                return;
            }
            finally
            {
                Cancellation.Dispose();
                Cancellation = null;
            }

            await Task.Delay(500);

            LoadingControl.IsLoading = false;

            if (SearchItems.Count == 0)
            {
                HasItem.Visibility = Visibility.Visible;
                SearchResultList.Visibility = Visibility.Collapsed;
                LoadingControl.IsLoading = false;
                return;
            }

            List<IStorageItem> SortResult = new List<IStorageItem>(SearchItems.Count);
            SortResult.AddRange(SearchItems.Where((Item) => Item.IsOfType(StorageItemTypes.Folder)));
            SortResult.AddRange(SearchItems.Where((Item) => Item.IsOfType(StorageItemTypes.File)));

            foreach (var Item in SortResult)
            {
                SearchResult.Add(new RemovableDeviceStorageItem(Item));
            }
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            HasItem.Visibility = Visibility.Collapsed;
            SearchResultList.Visibility = Visibility.Visible;

            ItemQuery = e.Parameter as StorageItemQueryResult;
            Loaded += SearchPage_Loaded;
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            SearchResult.Clear();
            Cancellation?.Cancel();
        }

        private async void Location_Click(object sender, RoutedEventArgs e)
        {
            var RemoveFile = SearchResultList.SelectedItem as RemovableDeviceStorageItem;

            if (RemoveFile.ContentType == ContentType.Folder)
            {
                foreach (var USBDevice in from Device in USBControl.ThisPage.FolderTree.RootNodes[0].Children
                                          where Path.GetPathRoot(RemoveFile.Folder.Path) == (Device.Content as StorageFolder).Path
                                          select Device)
                {
                    USBControl.ThisPage.CurrentNode = await FindFolderLocationInTree(USBDevice, RemoveFile.Folder.FolderRelativeId);
                    USBControl.ThisPage.CurrentFolder = USBControl.ThisPage.CurrentNode.Content as StorageFolder;
                    await USBControl.ThisPage.DisplayItemsInFolder(USBControl.ThisPage.CurrentNode);
                }
            }
            else
            {
                foreach (var USBDevice in from Device in USBControl.ThisPage.FolderTree.RootNodes[0].Children
                                          where Path.GetPathRoot(RemoveFile.File.Path) == (Device.Content as StorageFolder).Path
                                          select Device)
                {
                    USBControl.ThisPage.CurrentNode = await FindFolderLocationInTree(USBDevice, (await RemoveFile.File.GetParentAsync()).FolderRelativeId);
                    USBControl.ThisPage.CurrentFolder = USBControl.ThisPage.CurrentNode.Content as StorageFolder;
                    await USBControl.ThisPage.DisplayItemsInFolder(USBControl.ThisPage.CurrentNode);
                }
            }
        }

        private async void Attribute_Click(object sender, RoutedEventArgs e)
        {
            IList<object> SelectedGroup = SearchResultList.SelectedItems;
            if (SelectedGroup.Count != 1)
            {
                ContentDialog dialog = new ContentDialog
                {
                    Title = "错误",
                    Content = "仅允许查看单个文件属性，请重试",
                    CloseButtonText = "确定",
                    Background = Resources["SystemControlChromeHighAcrylicWindowMediumBrush"] as Brush
                };
                await dialog.ShowAsync();
            }
            else
            {
                RemovableDeviceStorageItem Device = SelectedGroup.FirstOrDefault() as RemovableDeviceStorageItem;
                if (Device.File != null)
                {
                    AttributeDialog Dialog = new AttributeDialog(Device.File);
                    await Dialog.ShowAsync();
                }
                else if (Device.Folder != null)
                {
                    AttributeDialog Dialog = new AttributeDialog(Device.Folder);
                    await Dialog.ShowAsync();
                }
            }
        }

        private async Task<TreeViewNode> FindFolderLocationInTree(TreeViewNode Node, string RelativeId)
        {
            if ((Node.Content as StorageFolder).FolderRelativeId == RelativeId)
            {
                return Node;
            }

            bool IsChangeExpandState = false;

            if (Node.HasUnrealizedChildren)
            {
                IsChangeExpandState = true;
                Node.IsExpanded = true;
            }
            else
            {
                USBControl.ThisPage.ExpandLocker.Set();
            }

            await Task.Run(() =>
            {
                USBControl.ThisPage.ExpandLocker.WaitOne();
            });

            if (Node.HasChildren)
            {
                while (Node.Children.Count == 0)
                {
                    await Task.Delay(100);
                }

                foreach (var SubNode in Node.Children)
                {
                    if ((SubNode.Content as StorageFolder).FolderRelativeId == RelativeId)
                    {
                        return SubNode;
                    }
                    else
                    {
                        TreeViewNode Result = await FindFolderLocationInTree(SubNode, RelativeId);
                        if (Result != null)
                        {
                            return Result;
                        }
                    }
                }
            }

            if (IsChangeExpandState)
            {
                Node.IsExpanded = false;
            }
            return null;
        }

        private void SearchResultList_RightTapped(object sender, Windows.UI.Xaml.Input.RightTappedRoutedEventArgs e)
        {
            RemovableDeviceStorageItem Context = (e.OriginalSource as FrameworkElement)?.DataContext as RemovableDeviceStorageItem;
            SearchResultList.SelectedIndex = SearchResult.IndexOf(Context);
            e.Handled = true;
        }

        private void SearchResultList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Location.IsEnabled = SearchResultList.SelectedIndex != -1;
        }
    }
}
