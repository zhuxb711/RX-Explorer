using RX_Explorer.Class;
using RX_Explorer.Dialog;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Navigation;

// https://go.microsoft.com/fwlink/?LinkId=234238 上介绍了“空白页”项模板

namespace RX_Explorer.View
{
    /// <summary>
    /// 可用于自身或导航至 Frame 内部的空白页。
    /// </summary>
    public sealed partial class RecycleBin : Page
    {
        private readonly Dictionary<SortTarget, SortDirection> SortMap = new Dictionary<SortTarget, SortDirection>
        {
            {SortTarget.Name,SortDirection.Ascending },
            {SortTarget.Type,SortDirection.Ascending },
            {SortTarget.ModifiedTime,SortDirection.Ascending },
            {SortTarget.Size,SortDirection.Ascending },
            {SortTarget.OriginPath,SortDirection.Ascending }
        };

        private ObservableCollection<FileSystemStorageItem> FileCollection = new ObservableCollection<FileSystemStorageItem>();

        public RecycleBin()
        {
            InitializeComponent();
        }

        protected async override void OnNavigatedTo(NavigationEventArgs e)
        {
            foreach (FileSystemStorageItem Item in (await FullTrustExcutorController.Current.GetRecycleBinItemsAsync().ConfigureAwait(true)).SortList(SortTarget.Name, SortDirection.Ascending))
            {
                FileCollection.Add(Item);
            }
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            FileCollection.Clear();
        }

        private void ListViewControl_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if ((e.OriginalSource as FrameworkElement)?.DataContext == null)
            {
                ListViewControl.SelectedItem = null;
            }
        }

        private void ListViewControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }

        private void ListViewControl_ItemClick(object sender, ItemClickEventArgs e)
        {

        }

        private void ListViewControl_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                args.ItemContainer.AllowFocusOnInteraction = true;
            }
            else
            {
                if (args.Item is FileSystemStorageItem Item)
                {
                    if (Item.StorageType == StorageItemTypes.File && Item.Thumbnail == null)
                    {
                        _ = Item.LoadMoreProperty();
                    }

                    args.ItemContainer.AllowFocusOnInteraction = false;
                }
            }
        }

        private void ListViewControl_Holding(object sender, HoldingRoutedEventArgs e)
        {
            if (e.HoldingState == Windows.UI.Input.HoldingState.Started)
            {
                if ((e.OriginalSource as FrameworkElement)?.DataContext is FileSystemStorageItem Item)
                {
                    ListViewControl.ContextFlyout = SelectFlyout;
                    ListViewControl.SelectedItem = Item;
                }
                else
                {
                    ListViewControl.ContextFlyout = null;
                    ListViewControl.SelectedItem = null;
                }
            }
        }

        private async void ListViewControl_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
        {
            if (ListViewControl.SelectedItem is FileSystemStorageItem Item)
            {
                PropertyDialog Dialog = new PropertyDialog(await Item.GetStorageItem().ConfigureAwait(true), Path.GetFileName(Item.RecycleItemOriginPath));
                await Dialog.ShowAsync().ConfigureAwait(false);
            }
        }

        private void ListViewControl_RightTapped(object sender, RightTappedRoutedEventArgs e)
        {
            if (e.PointerDeviceType == Windows.Devices.Input.PointerDeviceType.Mouse)
            {
                if ((e.OriginalSource as FrameworkElement)?.DataContext is FileSystemStorageItem Item)
                {
                    ListViewControl.ContextFlyout = SelectFlyout;
                    ListViewControl.SelectedItem = Item;
                }
                else
                {
                    ListViewControl.ContextFlyout = null;
                    ListViewControl.SelectedItem = null;
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
                SortMap[SortTarget.OriginPath] = SortDirection.Ascending;

                List<FileSystemStorageItem> SortResult = FileCollection.SortList(SortTarget.Name, SortDirection.Descending);
                FileCollection.Clear();

                foreach (FileSystemStorageItem Item in SortResult)
                {
                    FileCollection.Add(Item);
                }
            }
            else
            {
                SortMap[SortTarget.Name] = SortDirection.Ascending;
                SortMap[SortTarget.Type] = SortDirection.Ascending;
                SortMap[SortTarget.ModifiedTime] = SortDirection.Ascending;
                SortMap[SortTarget.Size] = SortDirection.Ascending;
                SortMap[SortTarget.OriginPath] = SortDirection.Ascending;

                List<FileSystemStorageItem> SortResult = FileCollection.SortList(SortTarget.Name, SortDirection.Ascending);
                FileCollection.Clear();

                foreach (FileSystemStorageItem Item in SortResult)
                {
                    FileCollection.Add(Item);
                }
            }
        }

        private void ListHeaderModifiedTime_Click(object sender, RoutedEventArgs e)
        {
            if (SortMap[SortTarget.ModifiedTime] == SortDirection.Ascending)
            {
                SortMap[SortTarget.ModifiedTime] = SortDirection.Descending;
                SortMap[SortTarget.Type] = SortDirection.Ascending;
                SortMap[SortTarget.Name] = SortDirection.Ascending;
                SortMap[SortTarget.Size] = SortDirection.Ascending;
                SortMap[SortTarget.OriginPath] = SortDirection.Ascending;

                List<FileSystemStorageItem> SortResult = FileCollection.SortList(SortTarget.ModifiedTime, SortDirection.Descending);
                FileCollection.Clear();

                foreach (FileSystemStorageItem Item in SortResult)
                {
                    FileCollection.Add(Item);
                }
            }
            else
            {
                SortMap[SortTarget.ModifiedTime] = SortDirection.Ascending;
                SortMap[SortTarget.Type] = SortDirection.Ascending;
                SortMap[SortTarget.Name] = SortDirection.Ascending;
                SortMap[SortTarget.Size] = SortDirection.Ascending;
                SortMap[SortTarget.OriginPath] = SortDirection.Ascending;

                List<FileSystemStorageItem> SortResult = FileCollection.SortList(SortTarget.ModifiedTime, SortDirection.Ascending);
                FileCollection.Clear();

                foreach (FileSystemStorageItem Item in SortResult)
                {
                    FileCollection.Add(Item);
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
                SortMap[SortTarget.Size] = SortDirection.Ascending;
                SortMap[SortTarget.OriginPath] = SortDirection.Ascending;

                List<FileSystemStorageItem> SortResult = FileCollection.SortList(SortTarget.Type, SortDirection.Descending);
                FileCollection.Clear();

                foreach (FileSystemStorageItem Item in SortResult)
                {
                    FileCollection.Add(Item);
                }
            }
            else
            {
                SortMap[SortTarget.Type] = SortDirection.Ascending;
                SortMap[SortTarget.Name] = SortDirection.Ascending;
                SortMap[SortTarget.ModifiedTime] = SortDirection.Ascending;
                SortMap[SortTarget.Size] = SortDirection.Ascending;
                SortMap[SortTarget.OriginPath] = SortDirection.Ascending;

                List<FileSystemStorageItem> SortResult = FileCollection.SortList(SortTarget.Type, SortDirection.Ascending);
                FileCollection.Clear();

                foreach (FileSystemStorageItem Item in SortResult)
                {
                    FileCollection.Add(Item);
                }
            }
        }

        private void ListHeaderSize_Click(object sender, RoutedEventArgs e)
        {
            if (SortMap[SortTarget.Size] == SortDirection.Ascending)
            {
                SortMap[SortTarget.Size] = SortDirection.Descending;
                SortMap[SortTarget.Type] = SortDirection.Ascending;
                SortMap[SortTarget.ModifiedTime] = SortDirection.Ascending;
                SortMap[SortTarget.Name] = SortDirection.Ascending;
                SortMap[SortTarget.OriginPath] = SortDirection.Ascending;

                List<FileSystemStorageItem> SortResult = FileCollection.SortList(SortTarget.Size, SortDirection.Descending);
                FileCollection.Clear();

                foreach (FileSystemStorageItem Item in SortResult)
                {
                    FileCollection.Add(Item);
                }

            }
            else
            {
                SortMap[SortTarget.Size] = SortDirection.Ascending;
                SortMap[SortTarget.Type] = SortDirection.Ascending;
                SortMap[SortTarget.ModifiedTime] = SortDirection.Ascending;
                SortMap[SortTarget.Name] = SortDirection.Ascending;
                SortMap[SortTarget.OriginPath] = SortDirection.Ascending;

                List<FileSystemStorageItem> SortResult = FileCollection.SortList(SortTarget.Size, SortDirection.Ascending);
                FileCollection.Clear();

                foreach (FileSystemStorageItem Item in SortResult)
                {
                    FileCollection.Add(Item);
                }
            }
        }

        private void ListHeaderOriginLocation_Click(object sender, RoutedEventArgs e)
        {
            if (SortMap[SortTarget.OriginPath] == SortDirection.Ascending)
            {
                SortMap[SortTarget.OriginPath] = SortDirection.Descending;
                SortMap[SortTarget.Size] = SortDirection.Ascending;
                SortMap[SortTarget.Type] = SortDirection.Ascending;
                SortMap[SortTarget.ModifiedTime] = SortDirection.Ascending;
                SortMap[SortTarget.Name] = SortDirection.Ascending;

                List<FileSystemStorageItem> SortResult = FileCollection.SortList(SortTarget.OriginPath, SortDirection.Descending);
                FileCollection.Clear();

                foreach (FileSystemStorageItem Item in SortResult)
                {
                    FileCollection.Add(Item);
                }

            }
            else
            {
                SortMap[SortTarget.OriginPath] = SortDirection.Ascending;
                SortMap[SortTarget.Size] = SortDirection.Ascending;
                SortMap[SortTarget.Type] = SortDirection.Ascending;
                SortMap[SortTarget.ModifiedTime] = SortDirection.Ascending;
                SortMap[SortTarget.Name] = SortDirection.Ascending;

                List<FileSystemStorageItem> SortResult = FileCollection.SortList(SortTarget.OriginPath, SortDirection.Ascending);
                FileCollection.Clear();

                foreach (FileSystemStorageItem Item in SortResult)
                {
                    FileCollection.Add(Item);
                }
            }
        }

        private async void PropertyButton_Click(object sender, RoutedEventArgs e)
        {
            if (ListViewControl.SelectedItem is FileSystemStorageItem Item)
            {
                PropertyDialog Dialog = new PropertyDialog(await Item.GetStorageItem().ConfigureAwait(true), Path.GetFileName(Item.RecycleItemOriginPath));
                await Dialog.ShowAsync().ConfigureAwait(false);
            }
        }

        private async void PermanentDelete_Click(object sender, RoutedEventArgs e)
        {
            if (ListViewControl.SelectedItem is FileSystemStorageItem Item)
            {
                FileCollection.Remove(Item);
                await (await Item.GetStorageItem().ConfigureAwait(false))?.DeleteAsync(StorageDeleteOption.PermanentDelete);
            }
        }

        private async void ClearRecycleBin_Click(object sender, RoutedEventArgs e)
        {
            QueueContentDialog Dialog = new QueueContentDialog
            {
                Title = "警告",
                Content = "此操作将清空回收站且无法恢复，是否继续?",
                PrimaryButtonText = "确定",
                CloseButtonText = "取消"
            };
            if (await Dialog.ShowAsync().ConfigureAwait(false) == ContentDialogResult.Primary)
            {
                await FullTrustExcutorController.Current.EmptyRecycleBinAsync().ConfigureAwait(false);
            }
        }

        private void RedoRecycle_Click(object sender, RoutedEventArgs e)
        {

        }
    }
}
