using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace USBManager
{
    public sealed partial class SearchPage : Page
    {
        ObservableCollection<RemovableDeviceStorageItem> SearchResult;

        public static SearchPage ThisPage { get; private set; }
        public List<IStorageItem> ResultList
        {
            set
            {
                SearchResult.Clear();

                foreach (var Item in value)
                {
                    SearchResult.Add(new RemovableDeviceStorageItem(Item));
                }
            }
        }

        public SearchPage()
        {
            InitializeComponent();
            ThisPage = this;
            SearchResult = new ObservableCollection<RemovableDeviceStorageItem>();
            SearchResultList.ItemsSource = SearchResult;
            Loaded += SearchPage_Loaded;
        }

        private void SearchPage_Loaded(object sender, RoutedEventArgs e)
        {
            if (SearchResult.Count == 0)
            {
                HasItem.Visibility = Visibility.Visible;
                SearchResultList.Visibility = Visibility.Collapsed;
            }
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            HasItem.Visibility = Visibility.Collapsed;
            SearchResultList.Visibility = Visibility.Visible;

            List<IStorageItem> List = e.Parameter as List<IStorageItem>;
            foreach (var Item in List)
            {
                SearchResult.Add(new RemovableDeviceStorageItem(Item));
            }
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            SearchResult.Clear();
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
