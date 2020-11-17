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
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace RX_Explorer
{
    public sealed partial class SearchPage : Page
    {
        private readonly ObservableCollection<FileSystemStorageItemBase> SearchResult = new ObservableCollection<FileSystemStorageItemBase>();
        private WeakReference<FileControl> WeakToFileControl;
        private CancellationTokenSource Cancellation;
        private readonly Dictionary<SortTarget, SortDirection> SortMap = new Dictionary<SortTarget, SortDirection>
        {
            {SortTarget.Name,SortDirection.Ascending },
            {SortTarget.Type,SortDirection.Ascending },
            {SortTarget.ModifiedTime,SortDirection.Ascending },
            {SortTarget.Size,SortDirection.Ascending },
            {SortTarget.Path,SortDirection.Ascending }
        };


        public SearchPage()
        {
            InitializeComponent();
            SearchResultList.ItemsSource = SearchResult;
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            if (e?.Parameter is Tuple<WeakReference<FileControl>, bool> Parameters)
            {
                WeakToFileControl = Parameters.Item1;

                await Initialize(Parameters.Item2).ConfigureAwait(false);
            }
        }

        private async Task Initialize(bool SearchShallow)
        {
            HasItem.Visibility = Visibility.Collapsed;

            LoadingControl.IsLoading = true;
            MainPage.ThisPage.IsAnyTaskRunning = true;

            try
            {
                Cancellation = new CancellationTokenSource();

                if (WeakToFileControl.TryGetTarget(out FileControl Control))
                {
                    string CurrentPath = Control.CurrentFolder.Path;
                    string SearchTarget = Control.GlobeSearch.Text;

                    List<FileSystemStorageItemBase> SearchItems = await Task.Run(() => WIN_Native_API.Search(CurrentPath, SearchTarget, !SearchShallow, SettingControl.IsDisplayHiddenItem, Cancellation.Token)).ConfigureAwait(true);

                    await Task.Delay(500).ConfigureAwait(true);

                    LoadingControl.IsLoading = false;

                    if (Cancellation.IsCancellationRequested)
                    {
                        HasItem.Visibility = Visibility.Visible;
                        SearchResultList.Visibility = Visibility.Collapsed;
                    }
                    else
                    {
                        if (SearchItems.Count == 0)
                        {
                            HasItem.Visibility = Visibility.Visible;
                        }
                        else
                        {
                            foreach (FileSystemStorageItemBase Item in SortCollectionGenerator.Current.GetSortedCollection(SearchItems))
                            {
                                SearchResult.Add(Item);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"An error was threw in {nameof(Initialize)}");
            }
            finally
            {
                MainPage.ThisPage.IsAnyTaskRunning = false;

                Cancellation.Dispose();
                Cancellation = null;
            }
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            Cancellation?.Cancel();
            SearchResult.Clear();
        }

        private async void Location_Click(object sender, RoutedEventArgs e)
        {
            if (SearchResultList.SelectedItem is FileSystemStorageItemBase Item)
            {
                try
                {
                    StorageFolder ParentFolder = await StorageFolder.GetFolderFromPathAsync(Path.GetDirectoryName(Item.Path));

                    if (WeakToFileControl.TryGetTarget(out FileControl Control))
                    {
                        Frame.GoBack();

                        await Control.OpenTargetFolder(ParentFolder).ConfigureAwait(true);

                        await JumpListController.Current.AddItem(JumpListGroup.Recent, ParentFolder).ConfigureAwait(true);

                        if (Control.Presenter.FileCollection.FirstOrDefault((SItem) => SItem.Path == Item.Path) is FileSystemStorageItemBase Target)
                        {
                            Control.Presenter.ItemPresenter.ScrollIntoView(Target);
                            Control.Presenter.SelectedItem = Target;
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogTracer.Log(ex, $"An error was threw in {nameof(Location_Click)}");

                    QueueContentDialog dialog = new QueueContentDialog
                    {
                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                        Content = Globalization.GetString("QueueDialog_LocateFolderFailure_Content"),
                        CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                    };

                    _ = await dialog.ShowAsync().ConfigureAwait(true);
                }
            }
        }

        private async void Attribute_Click(object sender, RoutedEventArgs e)
        {
            if (SearchResultList.SelectedItem is FileSystemStorageItemBase Item)
            {
                PropertyDialog Dialog = new PropertyDialog(Item);
                _ = await Dialog.ShowAsync().ConfigureAwait(true);
            }
        }

        private void SearchResultList_RightTapped(object sender, Windows.UI.Xaml.Input.RightTappedRoutedEventArgs e)
        {
            if (e.PointerDeviceType == Windows.Devices.Input.PointerDeviceType.Mouse)
            {
                if ((e.OriginalSource as FrameworkElement)?.DataContext is FileSystemStorageItemBase Context)
                {
                    SearchResultList.ContextFlyout = SearchCommandFlyout;
                    SearchResultList.SelectedItem = Context;
                }
                else
                {
                    SearchResultList.ContextFlyout = null;
                }

                e.Handled = true;
            }
        }

        private void CopyPath_Click(object sender, RoutedEventArgs e)
        {
            if (SearchResultList.SelectedItem is FileSystemStorageItemBase SelectItem)
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
                if ((e.OriginalSource as FrameworkElement)?.DataContext is FileSystemStorageItemBase Context)
                {
                    SearchResultList.ContextFlyout = SearchCommandFlyout;
                    SearchResultList.SelectedItem = Context;
                }
                else
                {
                    SearchResultList.ContextFlyout = null;
                }

                e.Handled = true;
            }
        }

        private void SearchResultList_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            args.RegisterUpdateCallback(async (s, e) =>
            {
                if (e.Item is FileSystemStorageItemBase Item)
                {
                    await Item.LoadMoreProperty().ConfigureAwait(false);
                }
            });
        }

        private void ListHeaderSize_Click(object sender, RoutedEventArgs e)
        {
            if (SortMap[SortTarget.Size] == SortDirection.Ascending)
            {
                SortMap[SortTarget.Size] = SortDirection.Descending;
                SortMap[SortTarget.Name] = SortDirection.Ascending;
                SortMap[SortTarget.ModifiedTime] = SortDirection.Ascending;
                SortMap[SortTarget.Path] = SortDirection.Ascending;
                SortMap[SortTarget.Type] = SortDirection.Ascending;

                List<FileSystemStorageItemBase> SortResult = SortCollectionGenerator.Current.GetSortedCollection(SearchResult, SortTarget.Size, SortDirection.Descending);

                SearchResult.Clear();

                foreach (FileSystemStorageItemBase Item in SortResult)
                {
                    SearchResult.Add(Item);
                }
            }
            else
            {
                SortMap[SortTarget.Size] = SortDirection.Ascending;
                SortMap[SortTarget.Name] = SortDirection.Ascending;
                SortMap[SortTarget.ModifiedTime] = SortDirection.Ascending;
                SortMap[SortTarget.Path] = SortDirection.Ascending;
                SortMap[SortTarget.Type] = SortDirection.Ascending;

                List<FileSystemStorageItemBase> SortResult = SortCollectionGenerator.Current.GetSortedCollection(SearchResult, SortTarget.Size, SortDirection.Ascending);

                SearchResult.Clear();

                foreach (FileSystemStorageItemBase Item in SortResult)
                {
                    SearchResult.Add(Item);
                }
            }
        }

        private void ListHeaderType_Click(object sender, RoutedEventArgs e)
        {
            if (SortMap[SortTarget.Type] == SortDirection.Ascending)
            {
                SortMap[SortTarget.Type] = SortDirection.Descending;
                SortMap[SortTarget.Name] = SortDirection.Ascending;
                SortMap[SortTarget.ModifiedTime] = SortDirection.Ascending;
                SortMap[SortTarget.Path] = SortDirection.Ascending;
                SortMap[SortTarget.Size] = SortDirection.Ascending;

                List<FileSystemStorageItemBase> SortResult = SortCollectionGenerator.Current.GetSortedCollection(SearchResult, SortTarget.Type, SortDirection.Descending);

                SearchResult.Clear();

                foreach (FileSystemStorageItemBase Item in SortResult)
                {
                    SearchResult.Add(Item);
                }
            }
            else
            {
                SortMap[SortTarget.Type] = SortDirection.Ascending;
                SortMap[SortTarget.Name] = SortDirection.Ascending;
                SortMap[SortTarget.ModifiedTime] = SortDirection.Ascending;
                SortMap[SortTarget.Path] = SortDirection.Ascending;
                SortMap[SortTarget.Size] = SortDirection.Ascending;

                List<FileSystemStorageItemBase> SortResult = SortCollectionGenerator.Current.GetSortedCollection(SearchResult, SortTarget.Type, SortDirection.Ascending);

                SearchResult.Clear();

                foreach (FileSystemStorageItemBase Item in SortResult)
                {
                    SearchResult.Add(Item);
                }
            }
        }

        private void ListHeaderModifyDate_Click(object sender, RoutedEventArgs e)
        {
            if (SortMap[SortTarget.ModifiedTime] == SortDirection.Ascending)
            {
                SortMap[SortTarget.ModifiedTime] = SortDirection.Descending;
                SortMap[SortTarget.Name] = SortDirection.Ascending;
                SortMap[SortTarget.Type] = SortDirection.Ascending;
                SortMap[SortTarget.Path] = SortDirection.Ascending;
                SortMap[SortTarget.Size] = SortDirection.Ascending;

                List<FileSystemStorageItemBase> SortResult = SortCollectionGenerator.Current.GetSortedCollection(SearchResult, SortTarget.ModifiedTime, SortDirection.Descending);

                SearchResult.Clear();

                foreach (FileSystemStorageItemBase Item in SortResult)
                {
                    SearchResult.Add(Item);
                }
            }
            else
            {
                SortMap[SortTarget.ModifiedTime] = SortDirection.Ascending;
                SortMap[SortTarget.Name] = SortDirection.Ascending;
                SortMap[SortTarget.Type] = SortDirection.Ascending;
                SortMap[SortTarget.Path] = SortDirection.Ascending;
                SortMap[SortTarget.Size] = SortDirection.Ascending;

                List<FileSystemStorageItemBase> SortResult = SortCollectionGenerator.Current.GetSortedCollection(SearchResult, SortTarget.ModifiedTime, SortDirection.Ascending);

                SearchResult.Clear();

                foreach (FileSystemStorageItemBase Item in SortResult)
                {
                    SearchResult.Add(Item);
                }
            }
        }

        private void ListHeaderPath_Click(object sender, RoutedEventArgs e)
        {
            if (SortMap[SortTarget.Path] == SortDirection.Ascending)
            {
                SortMap[SortTarget.Path] = SortDirection.Descending;
                SortMap[SortTarget.Name] = SortDirection.Ascending;
                SortMap[SortTarget.Type] = SortDirection.Ascending;
                SortMap[SortTarget.ModifiedTime] = SortDirection.Ascending;
                SortMap[SortTarget.Size] = SortDirection.Ascending;

                List<FileSystemStorageItemBase> SortResult = SortCollectionGenerator.Current.GetSortedCollection(SearchResult, SortTarget.Path, SortDirection.Descending);

                SearchResult.Clear();

                foreach (FileSystemStorageItemBase Item in SortResult)
                {
                    SearchResult.Add(Item);
                }
            }
            else
            {
                SortMap[SortTarget.Path] = SortDirection.Ascending;
                SortMap[SortTarget.Name] = SortDirection.Ascending;
                SortMap[SortTarget.Type] = SortDirection.Ascending;
                SortMap[SortTarget.ModifiedTime] = SortDirection.Ascending;
                SortMap[SortTarget.Size] = SortDirection.Ascending;

                List<FileSystemStorageItemBase> SortResult = SortCollectionGenerator.Current.GetSortedCollection(SearchResult, SortTarget.Path, SortDirection.Ascending);

                SearchResult.Clear();

                foreach (FileSystemStorageItemBase Item in SortResult)
                {
                    SearchResult.Add(Item);
                }
            }
        }

        private void ListHeaderName_Click(object sender, RoutedEventArgs e)
        {
            if (SortMap[SortTarget.Name] == SortDirection.Ascending)
            {
                SortMap[SortTarget.Name] = SortDirection.Descending;
                SortMap[SortTarget.Type] = SortDirection.Ascending;
                SortMap[SortTarget.ModifiedTime] = SortDirection.Ascending;
                SortMap[SortTarget.Size] = SortDirection.Ascending;
                SortMap[SortTarget.Path] = SortDirection.Ascending;

                List<FileSystemStorageItemBase> SortResult = SortCollectionGenerator.Current.GetSortedCollection(SearchResult, SortTarget.Name, SortDirection.Descending);

                SearchResult.Clear();

                foreach (FileSystemStorageItemBase Item in SortResult)
                {
                    SearchResult.Add(Item);
                }
            }
            else
            {
                SortMap[SortTarget.Name] = SortDirection.Ascending;
                SortMap[SortTarget.Type] = SortDirection.Ascending;
                SortMap[SortTarget.ModifiedTime] = SortDirection.Ascending;
                SortMap[SortTarget.Size] = SortDirection.Ascending;
                SortMap[SortTarget.Path] = SortDirection.Ascending;

                List<FileSystemStorageItemBase> SortResult = SortCollectionGenerator.Current.GetSortedCollection(SearchResult, SortTarget.Name, SortDirection.Ascending);

                SearchResult.Clear();

                foreach (FileSystemStorageItemBase Item in SortResult)
                {
                    SearchResult.Add(Item);
                }
            }
        }
    }
}
