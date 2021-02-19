using RX_Explorer.Class;
using RX_Explorer.Dialog;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.System;
using Windows.UI.Core;
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

        private readonly ObservableCollection<RecycleStorageItem> FileCollection = new ObservableCollection<RecycleStorageItem>();

        private ListViewBaseSelectionExtention SelectionExtention;

        public RecycleBin()
        {
            InitializeComponent();
            Loaded += RecycleBin_Loaded;
            Unloaded += RecycleBin_Unloaded;
        }

        private void RecycleBin_Unloaded(object sender, RoutedEventArgs e)
        {
            SelectionExtention?.Dispose();
            CoreWindow.GetForCurrentThread().KeyDown -= RecycleBin_KeyDown;
        }

        private void RecycleBin_Loaded(object sender, RoutedEventArgs e)
        {
            CoreWindow.GetForCurrentThread().KeyDown += RecycleBin_KeyDown;
            SelectionExtention = new ListViewBaseSelectionExtention(ListViewControl, DrawRectangle);
        }

        private void RecycleBin_KeyDown(CoreWindow sender, KeyEventArgs args)
        {
            if (!LoadingControl.IsLoading)
            {
                CoreVirtualKeyStates CtrlState = sender.GetKeyState(VirtualKey.Control);

                switch (args.VirtualKey)
                {
                    case VirtualKey.A when CtrlState.HasFlag(CoreVirtualKeyStates.Down):
                        {
                            ListViewControl.SelectAll();
                            break;
                        }
                    case VirtualKey.Delete:
                    case VirtualKey.D when CtrlState.HasFlag(CoreVirtualKeyStates.Down):
                        {
                            PermanentDelete_Click(null, null);
                            break;
                        }
                }
            }
        }

        protected async override void OnNavigatedTo(NavigationEventArgs e)
        {
            using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableController())
            {
                foreach (RecycleStorageItem Item in SortCollectionGenerator.Current.GetSortedCollection(await Exclusive.Controller.GetRecycleBinItemsAsync().ConfigureAwait(true), SortTarget.Name, SortDirection.Ascending))
                {
                    FileCollection.Add(Item);
                }
            }

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

            SelectionExtention.Enable();
        }

        private async void ListViewControl_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                args.ItemContainer.AllowFocusOnInteraction = true;
            }
            else
            {
                if (args.Item is FileSystemStorageItemBase Item)
                {
                    if (Item.StorageType == StorageItemTypes.File)
                    {
                        await Item.LoadMorePropertyAsync().ConfigureAwait(true);
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
                    if ((e.OriginalSource as FrameworkElement)?.DataContext is FileSystemStorageItemBase Item)
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
            if (ListViewControl.SelectedItem is RecycleStorageItem Item)
            {
                PropertyDialog Dialog = new PropertyDialog(Item);
                await Dialog.ShowAsync().ConfigureAwait(false);
            }
        }

        private void ListViewControl_RightTapped(object sender, RightTappedRoutedEventArgs e)
        {
            if (e.PointerDeviceType == Windows.Devices.Input.PointerDeviceType.Mouse)
            {
                if (e.OriginalSource is FrameworkElement Element)
                {
                    if (Element.Name == "EmptyTextblock")
                    {
                        ListViewControl.SelectedItem = null;
                        ListViewControl.ContextFlyout = EmptyFlyout;
                    }
                    else
                    {
                        if (Element.DataContext is RecycleStorageItem Context)
                        {
                            if (ListViewControl.SelectedItems.Count > 1 && ListViewControl.SelectedItems.Contains(Context))
                            {
                                ListViewControl.ContextFlyout = SelectFlyout;
                            }
                            else
                            {
                                if (ListViewControl.SelectedItem as RecycleStorageItem == Context)
                                {
                                    ListViewControl.ContextFlyout = SelectFlyout;
                                }
                                else
                                {
                                    if (e.OriginalSource is TextBlock)
                                    {
                                        ListViewControl.SelectedItem = Context;
                                        ListViewControl.ContextFlyout = SelectFlyout;
                                    }
                                    else
                                    {
                                        ListViewControl.SelectedItem = null;
                                        ListViewControl.ContextFlyout = EmptyFlyout;
                                    }
                                }
                            }
                        }
                        else
                        {
                            ListViewControl.SelectedItem = null;
                            ListViewControl.ContextFlyout = EmptyFlyout;
                        }
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

                RecycleStorageItem[] SortResult = SortCollectionGenerator.Current.GetSortedCollection(FileCollection, SortTarget.Name, SortDirection.Descending).ToArray();

                FileCollection.Clear();

                foreach (RecycleStorageItem Item in SortResult)
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

                RecycleStorageItem[] SortResult = SortCollectionGenerator.Current.GetSortedCollection(FileCollection, SortTarget.Name, SortDirection.Ascending).ToArray();

                FileCollection.Clear();

                foreach (RecycleStorageItem Item in SortResult)
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

                RecycleStorageItem[] SortResult = SortCollectionGenerator.Current.GetSortedCollection(FileCollection, SortTarget.ModifiedTime, SortDirection.Descending).ToArray();

                FileCollection.Clear();

                foreach (RecycleStorageItem Item in SortResult)
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

                RecycleStorageItem[] SortResult = SortCollectionGenerator.Current.GetSortedCollection(FileCollection, SortTarget.ModifiedTime, SortDirection.Ascending).ToArray();

                FileCollection.Clear();

                foreach (RecycleStorageItem Item in SortResult)
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

                RecycleStorageItem[] SortResult = SortCollectionGenerator.Current.GetSortedCollection(FileCollection, SortTarget.Type, SortDirection.Descending).ToArray();

                FileCollection.Clear();

                foreach (RecycleStorageItem Item in SortResult)
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

                RecycleStorageItem[] SortResult = SortCollectionGenerator.Current.GetSortedCollection(FileCollection, SortTarget.Type, SortDirection.Ascending).ToArray();

                FileCollection.Clear();

                foreach (RecycleStorageItem Item in SortResult)
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

                RecycleStorageItem[] SortResult = SortCollectionGenerator.Current.GetSortedCollection(FileCollection, SortTarget.Size, SortDirection.Descending).ToArray();

                FileCollection.Clear();

                foreach (RecycleStorageItem Item in SortResult)
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

                RecycleStorageItem[] SortResult = SortCollectionGenerator.Current.GetSortedCollection(FileCollection, SortTarget.Size, SortDirection.Ascending).ToArray();

                FileCollection.Clear();

                foreach (RecycleStorageItem Item in SortResult)
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

                RecycleStorageItem[] SortResult = SortCollectionGenerator.Current.GetSortedCollection(FileCollection, SortTarget.OriginPath, SortDirection.Descending).ToArray();

                FileCollection.Clear();

                foreach (RecycleStorageItem Item in SortResult)
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

                RecycleStorageItem[] SortResult = SortCollectionGenerator.Current.GetSortedCollection(FileCollection, SortTarget.OriginPath, SortDirection.Ascending).ToArray();

                FileCollection.Clear();

                foreach (RecycleStorageItem Item in SortResult)
                {
                    FileCollection.Add(Item);
                }
            }
        }

        private async void PropertyButton_Click(object sender, RoutedEventArgs e)
        {
            if (ListViewControl.SelectedItem is RecycleStorageItem Item)
            {
                PropertyDialog Dialog = new PropertyDialog(Item);
                await Dialog.ShowAsync().ConfigureAwait(false);
            }
        }

        private async void PermanentDelete_Click(object sender, RoutedEventArgs e)
        {
            if (ListViewControl.SelectedItems.Count > 0)
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

                    using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableController())
                    {
                        foreach (RecycleStorageItem Item in ListViewControl.SelectedItems.ToList())
                        {
                            if (await Exclusive.Controller.DeleteItemInRecycleBinAsync(Item.Path).ConfigureAwait(true))
                            {
                                FileCollection.Remove(Item);
                            }
                            else
                            {
                                ErrorList.Add(Item.Name);
                            }
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

                if (FileCollection.Count == 0)
                {
                    HasFile.Visibility = Visibility.Visible;
                    ClearRecycleBin.IsEnabled = false;
                }
            }
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

                using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableController())
                {
                    if (await Exclusive.Controller.EmptyRecycleBinAsync().ConfigureAwait(true))
                    {
                        await ActivateLoading(false).ConfigureAwait(true);

                        FileCollection.Clear();

                        HasFile.Visibility = Visibility.Visible;
                        ClearRecycleBin.IsEnabled = false;
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

                        await ActivateLoading(false).ConfigureAwait(true);
                    }
                }
            }
        }

        private async void RestoreRecycle_Click(object sender, RoutedEventArgs e)
        {
            if (ListViewControl.SelectedItems.Count > 0)
            {
                await ActivateLoading(true, Globalization.GetString("RecycleBinRestoreText")).ConfigureAwait(true);

                List<string> ErrorList = new List<string>();

                using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableController())
                {
                    foreach (RecycleStorageItem Item in ListViewControl.SelectedItems.ToList())
                    {
                        if (await Exclusive.Controller.RestoreItemInRecycleBinAsync(Item.Path).ConfigureAwait(true))
                        {
                            FileCollection.Remove(Item);
                        }
                        else
                        {
                            ErrorList.Add(Item.Name);
                        }
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

                if (FileCollection.Count == 0)
                {
                    HasFile.Visibility = Visibility.Visible;
                    ClearRecycleBin.IsEnabled = false;
                }
            }
        }

        private async Task ActivateLoading(bool IsLoading, string Message = null)
        {
            if (IsLoading)
            {
                ProgressInfo.Text = $"{Message}...";
                LoadingControl.IsLoading = true;
            }
            else
            {
                await Task.Delay(1000).ConfigureAwait(true);
                LoadingControl.IsLoading = false;
            }
        }

        private async void Refresh_Click(object sender, RoutedEventArgs e)
        {
            FileCollection.Clear();

            using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableController())
            {
                foreach (RecycleStorageItem Item in SortCollectionGenerator.Current.GetSortedCollection(await Exclusive.Controller.GetRecycleBinItemsAsync().ConfigureAwait(true), SortTarget.Name, SortDirection.Ascending))
                {
                    FileCollection.Add(Item);
                }
            }

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
