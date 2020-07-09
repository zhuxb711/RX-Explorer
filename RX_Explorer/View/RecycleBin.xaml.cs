using RX_Explorer.Class;
using RX_Explorer.Dialog;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Navigation;

namespace RX_Explorer.View
{
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
            FileCollection.CollectionChanged += FileCollection_CollectionChanged;
        }

        private void FileCollection_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (FileCollection.Count == 0)
            {
                HasFile.Visibility = Visibility.Visible;
                ClearRecycleBin.IsEnabled = false;
            }
            else
            {
                HasFile.Visibility = Visibility.Collapsed;
                ClearRecycleBin.IsEnabled = true;
            }
        }

        protected async override void OnNavigatedTo(NavigationEventArgs e)
        {
            foreach (FileSystemStorageItem Item in (await FullTrustExcutorController.Current.GetRecycleBinItemsAsync().ConfigureAwait(true)).SortList(SortTarget.Name, SortDirection.Ascending))
            {
                FileCollection.Add(Item);
            }

            if (FileCollection.Count == 0)
            {
                HasFile.Visibility = Visibility.Visible;
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
                    if (Item.StorageType == StorageItemTypes.File)
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
                if (e.OriginalSource is ListViewItemPresenter || (e.OriginalSource as FrameworkElement)?.Name == "EmptyTextblock")
                {
                    ListViewControl.SelectedItem = null;
                    ListViewControl.ContextFlyout = EmptyFlyout;
                }
                else
                {
                    if ((e.OriginalSource as FrameworkElement)?.DataContext is FileSystemStorageItem Item)
                    {
                        ListViewControl.ContextFlyout = SelectFlyout;
                        ListViewControl.SelectedItem = Item;
                    }
                    else
                    {
                        ListViewControl.SelectedItem = null;
                        ListViewControl.ContextFlyout = EmptyFlyout;
                    }
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
                if (e.OriginalSource is ListViewItemPresenter || (e.OriginalSource as FrameworkElement)?.Name == "EmptyTextblock")
                {
                    ListViewControl.SelectedItem = null;
                    ListViewControl.ContextFlyout = EmptyFlyout;
                }
                else
                {
                    if ((e.OriginalSource as FrameworkElement)?.DataContext is FileSystemStorageItem Item)
                    {
                        ListViewControl.ContextFlyout = SelectFlyout;
                        ListViewControl.SelectedItem = Item;
                    }
                    else
                    {
                        ListViewControl.SelectedItem = null;
                        ListViewControl.ContextFlyout = EmptyFlyout;
                    }
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
            await ActivateLoading(true, Globalization.GetString("RecycleBinDeleteText")).ConfigureAwait(true);

            QueueContentDialog QueueContenDialog = new QueueContentDialog
            {
                Title = Globalization.GetString("Common_Dialog_WarningTitle"),
                Content = Globalization.GetString("QueueDialog_DeleteFile_Content"),
                PrimaryButtonText = Globalization.GetString("Common_Dialog_ContinueButton"),
                CloseButtonText = Globalization.GetString("Common_Dialog_CancelButton")
            };

            if ((await QueueContenDialog.ShowAsync().ConfigureAwait(true)) == ContentDialogResult.Primary)
            {
                List<string> ErrorList = new List<string>();

                foreach (FileSystemStorageItem Item in ListViewControl.SelectedItems)
                {
                    if (await FullTrustExcutorController.Current.DeleteItemInRecycleBinAsync(Item.Path).ConfigureAwait(true))
                    {
                        FileCollection.Remove(Item);
                    }
                    else
                    {
                        ErrorList.Add(Item.Name);
                    }
                }

                if (ErrorList.Count > 0)
                {
                    QueueContentDialog Dialog = new QueueContentDialog
                    {
                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                        Content = $"{Globalization.GetString("QueueDialog_RecycleBinDeleteError_Content")} {Environment.NewLine}{string.Join(Environment.NewLine, ErrorList)}",
                        CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                    };
                    _ = Dialog.ShowAsync().ConfigureAwait(true);
                }
            }

            await ActivateLoading(false).ConfigureAwait(true);
        }

        private async void ClearRecycleBin_Click(object sender, RoutedEventArgs e)
        {
            QueueContentDialog Dialog = new QueueContentDialog
            {
                Title = Globalization.GetString("Common_Dialog_WarningTitle"),
                Content = Globalization.GetString("QueueDialog_EmptyRecycleBin_Content"),
                PrimaryButtonText = Globalization.GetString("Common_Dialog_ContinueButton"),
                CloseButtonText = Globalization.GetString("Common_Dialog_CancelButton")
            };

            if (await Dialog.ShowAsync().ConfigureAwait(true) == ContentDialogResult.Primary)
            {
                await ActivateLoading(true, Globalization.GetString("RecycleBinEmptyingText")).ConfigureAwait(true);

                if (await FullTrustExcutorController.Current.EmptyRecycleBinAsync().ConfigureAwait(true))
                {
                    FileCollection.Clear();
                }
                else
                {
                    QueueContentDialog dialog = new QueueContentDialog
                    {
                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                        Content = Globalization.GetString("QueueDialog_RecycleBinEmptyError_Content"),
                        CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                    };

                    _ = await dialog.ShowAsync().ConfigureAwait(true);
                }

                await ActivateLoading(false).ConfigureAwait(true);
            }
        }

        private async void RestoreRecycle_Click(object sender, RoutedEventArgs e)
        {
            await ActivateLoading(true, Globalization.GetString("RecycleBinRestoreText")).ConfigureAwait(true);

            List<string> ErrorList = new List<string>();

            foreach (FileSystemStorageItem Item in ListViewControl.SelectedItems)
            {
                if (await FullTrustExcutorController.Current.RestoreItemInRecycleBinAsync(Item.Path).ConfigureAwait(true))
                {
                    FileCollection.Remove(Item);
                }
                else
                {
                    ErrorList.Add(Item.Name);
                }
            }

            if (ErrorList.Count > 0)
            {
                QueueContentDialog Dialog = new QueueContentDialog
                {
                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                    Content = $"{Globalization.GetString("QueueDialog_RecycleBinRestoreError_Content")} {Environment.NewLine}{string.Join(Environment.NewLine, ErrorList)}",
                    CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                };
                _ = Dialog.ShowAsync().ConfigureAwait(true);
            }

            await ActivateLoading(false).ConfigureAwait(true);
        }

        private async Task ActivateLoading(bool IsLoading, string Message = null)
        {
            if (IsLoading)
            {
                ProgressInfo.Text = $"{Message}...";
                LoadingControl.IsLoading = true;
                MainPage.ThisPage.IsAnyTaskRunning = true;
            }
            else
            {
                await Task.Delay(1000).ConfigureAwait(true);
                LoadingControl.IsLoading = false;
                MainPage.ThisPage.IsAnyTaskRunning = false;
            }
        }

        private async void Refresh_Click(object sender, RoutedEventArgs e)
        {
            FileCollection.Clear();

            foreach (FileSystemStorageItem Item in (await FullTrustExcutorController.Current.GetRecycleBinItemsAsync().ConfigureAwait(true)).SortList(SortTarget.Name, SortDirection.Ascending))
            {
                FileCollection.Add(Item);
            }

            if (FileCollection.Count == 0)
            {
                HasFile.Visibility = Visibility.Visible;
            }
        }

        private void ListViewControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ListViewControl.SelectedItems.Count > 1)
            {
                PropertyButton.IsEnabled = false;
            }
            else
            {
                PropertyButton.IsEnabled = true;
            }
        }
    }
}
