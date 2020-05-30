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
using Windows.Foundation;
using Windows.Storage;
using Windows.Storage.Search;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;
using TreeViewNode = Microsoft.UI.Xaml.Controls.TreeViewNode;

namespace RX_Explorer
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
            if (SearchResultList.SelectedItem is FileSystemStorageItem Item)
            {
                if (Item.StorageType == StorageItemTypes.Folder)
                {
                    try
                    {
                        if (SettingControl.IsDetachTreeViewAndPresenter)
                        {
                            StorageFolder Folder = (await Item.GetStorageItem().ConfigureAwait(true)) as StorageFolder;

                            await FileControlInstance.DisplayItemsInFolder(Folder).ConfigureAwait(false);
                        }
                        else
                        {
                            TreeViewNode TargetNode = await FileControlInstance.FolderTree.RootNodes[0].FindFolderLocationInTree(new PathAnalysis(Item.Path, (FileControlInstance.FolderTree.RootNodes[0].Content as StorageFolder).Path)).ConfigureAwait(true);
                            if (TargetNode != null)
                            {
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
                        QueueContentDialog dialog = new QueueContentDialog
                        {
                            Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                            Content = Globalization.GetString("QueueDialog_LocateFolderFailure_Content"),
                            CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                        };
                        _ = await dialog.ShowAsync().ConfigureAwait(true);
                    }
                }
                else
                {
                    try
                    {
                        StorageFile File = (await Item.GetStorageItem().ConfigureAwait(true)) as StorageFile;

                        if (SettingControl.IsDetachTreeViewAndPresenter)
                        {
                            await FileControlInstance.DisplayItemsInFolder(await File.GetParentAsync()).ConfigureAwait(false);
                        }
                        else
                        {
                            TreeViewNode CurrentNode = await FileControlInstance.FolderTree.RootNodes[0].FindFolderLocationInTree(new PathAnalysis(Path.GetDirectoryName(Item.Path), (FileControlInstance.FolderTree.RootNodes[0].Content as StorageFolder).Path)).ConfigureAwait(true);

                            await FileControlInstance.DisplayItemsInFolder(CurrentNode).ConfigureAwait(false);
                        }
                    }
                    catch (FileNotFoundException)
                    {
                        QueueContentDialog dialog = new QueueContentDialog
                        {
                            Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                            Content = Globalization.GetString("QueueDialog_LocateFileFailure_Content"),
                            CloseButtonText = Globalization.GetString("Common_Dialog_RefreshButton")
                        };
                        _ = await dialog.ShowAsync().ConfigureAwait(true);
                    }
                }
            }
        }

        private async void Attribute_Click(object sender, RoutedEventArgs e)
        {
            FileSystemStorageItem Device = SearchResultList.SelectedItems.FirstOrDefault() as FileSystemStorageItem;
            PropertyDialog Dialog = new PropertyDialog(await Device.GetStorageItem());
            _ = await Dialog.ShowAsync().ConfigureAwait(true);
        }

        private void SearchResultList_RightTapped(object sender, Windows.UI.Xaml.Input.RightTappedRoutedEventArgs e)
        {
            if (e.PointerDeviceType == Windows.Devices.Input.PointerDeviceType.Mouse)
            {
                FileSystemStorageItem Context = (e.OriginalSource as FrameworkElement)?.DataContext as FileSystemStorageItem;
                SearchResultList.SelectedIndex = SearchResult.IndexOf(Context);
                e.Handled = true;
            }
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

        private void SearchResultList_Holding(object sender, Windows.UI.Xaml.Input.HoldingRoutedEventArgs e)
        {
            if (e.HoldingState == Windows.UI.Input.HoldingState.Started)
            {
                FileSystemStorageItem Context = (e.OriginalSource as FrameworkElement)?.DataContext as FileSystemStorageItem;
                SearchResultList.SelectedIndex = SearchResult.IndexOf(Context);
            }
        }
    }
}
