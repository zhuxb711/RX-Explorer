using RX_Explorer.Class;
using RX_Explorer.Dialog;
using RX_Explorer.Interface;
using RX_Explorer.SeparateWindow.PropertyWindow;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.System;
using Windows.UI.Core;
using Windows.UI.Input;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Navigation;

namespace RX_Explorer.View
{
    public sealed partial class RecycleBin : Page
    {
        private SortTarget currentSortTarget;
        private SortTarget CurrentSortTarget
        {
            get
            {
                return currentSortTarget;
            }
            set
            {
                switch (value)
                {
                    case SortTarget.Name:
                        {
                            NameSortIndicator.Visibility = Visibility.Visible;
                            OriginPathSortIndicator.Visibility = Visibility.Collapsed;
                            DeleteDateSortIndicator.Visibility = Visibility.Collapsed;
                            TypeSortIndicator.Visibility = Visibility.Collapsed;
                            SizeSortIndicator.Visibility = Visibility.Collapsed;
                            break;
                        }
                    case SortTarget.Type:
                        {
                            NameSortIndicator.Visibility = Visibility.Collapsed;
                            OriginPathSortIndicator.Visibility = Visibility.Collapsed;
                            DeleteDateSortIndicator.Visibility = Visibility.Collapsed;
                            TypeSortIndicator.Visibility = Visibility.Visible;
                            SizeSortIndicator.Visibility = Visibility.Collapsed;
                            break;
                        }
                    case SortTarget.ModifiedTime:
                        {
                            NameSortIndicator.Visibility = Visibility.Collapsed;
                            OriginPathSortIndicator.Visibility = Visibility.Collapsed;
                            DeleteDateSortIndicator.Visibility = Visibility.Visible;
                            TypeSortIndicator.Visibility = Visibility.Collapsed;
                            SizeSortIndicator.Visibility = Visibility.Collapsed;
                            break;
                        }
                    case SortTarget.Size:
                        {
                            NameSortIndicator.Visibility = Visibility.Collapsed;
                            OriginPathSortIndicator.Visibility = Visibility.Collapsed;
                            DeleteDateSortIndicator.Visibility = Visibility.Collapsed;
                            TypeSortIndicator.Visibility = Visibility.Collapsed;
                            SizeSortIndicator.Visibility = Visibility.Visible;
                            break;
                        }
                    case SortTarget.OriginPath:
                        {
                            NameSortIndicator.Visibility = Visibility.Collapsed;
                            OriginPathSortIndicator.Visibility = Visibility.Visible;
                            DeleteDateSortIndicator.Visibility = Visibility.Collapsed;
                            TypeSortIndicator.Visibility = Visibility.Collapsed;
                            SizeSortIndicator.Visibility = Visibility.Collapsed;
                            break;
                        }
                }

                currentSortTarget = value;
            }
        }

        private SortDirection sortDirection;
        private SortDirection CurrentSortDirection
        {
            get
            {
                return sortDirection;
            }
            set
            {
                switch (CurrentSortTarget)
                {
                    case SortTarget.Name:
                        {
                            NameSortIndicator.Child = new FontIcon { Glyph = value == SortDirection.Ascending ? "\uF0AD" : "\uF0AE" };
                            break;
                        }
                    case SortTarget.Type:
                        {
                            TypeSortIndicator.Child = new FontIcon { Glyph = value == SortDirection.Ascending ? "\uF0AD" : "\uF0AE" };
                            break;
                        }
                    case SortTarget.ModifiedTime:
                        {
                            DeleteDateSortIndicator.Child = new FontIcon { Glyph = value == SortDirection.Ascending ? "\uF0AD" : "\uF0AE" };
                            break;
                        }
                    case SortTarget.Size:
                        {
                            SizeSortIndicator.Child = new FontIcon { Glyph = value == SortDirection.Ascending ? "\uF0AD" : "\uF0AE" };
                            break;
                        }
                    case SortTarget.OriginPath:
                        {
                            OriginPathSortIndicator.Child = new FontIcon { Glyph = value == SortDirection.Ascending ? "\uF0AD" : "\uF0AE" };
                            break;
                        }
                }

                sortDirection = value;
            }
        }

        private readonly ObservableCollection<IRecycleStorageItem> FileCollection = new ObservableCollection<IRecycleStorageItem>();

        private ListViewBaseSelectionExtention SelectionExtention;

        private readonly PointerEventHandler PointerPressedHandler;

        private CancellationTokenSource DelaySelectionCancellation;

        public RecycleBin()
        {
            InitializeComponent();
            PointerPressedHandler = new PointerEventHandler(ListViewControl_PointerPressed);
        }

        private void RecycleBin_KeyDown(CoreWindow sender, KeyEventArgs args)
        {
            if (!LoadingControl.IsLoading)
            {
                SelectFlyout.Hide();
                EmptyFlyout.Hide();

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
                    case VirtualKey.R when CtrlState.HasFlag(CoreVirtualKeyStates.Down):
                        {
                            RestoreRecycle_Click(null, null);
                            break;
                        }
                    case VirtualKey.E when CtrlState.HasFlag(CoreVirtualKeyStates.Down):
                        {
                            ClearRecycleBin_Click(null, null);
                            break;
                        }
                }
            }
        }

        protected async override void OnNavigatedTo(NavigationEventArgs e)
        {
            ListViewControl.AddHandler(PointerPressedEvent, PointerPressedHandler, true);
            CoreWindow.GetForCurrentThread().KeyDown += RecycleBin_KeyDown;
            SelectionExtention = new ListViewBaseSelectionExtention(ListViewControl, DrawRectangle);
            CurrentSortTarget = SortTarget.Name;
            CurrentSortDirection = SortDirection.Ascending;

            ControlLoading(true, Globalization.GetString("Progress_Tip_Loading"));

            using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableControllerAsync())
            {
                IReadOnlyList<IRecycleStorageItem> Result = await Exclusive.Controller.GetRecycleBinItemsAsync();
                FileCollection.AddRange(SortCollectionGenerator.GetSortedCollection(Result, SortTarget.Name, SortDirection.Ascending));
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

            ControlLoading(false);
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            SelectionExtention?.Dispose();

            DelaySelectionCancellation?.Cancel();
            DelaySelectionCancellation?.Dispose();
            DelaySelectionCancellation = null;

            CoreWindow.GetForCurrentThread().KeyDown -= RecycleBin_KeyDown;
            ListViewControl.RemoveHandler(PointerPressedEvent, PointerPressedHandler);

            FileCollection.Clear();
        }

        private void ListViewControl_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (e.OriginalSource is FrameworkElement Element)
            {
                if (Element.DataContext is IRecycleStorageItem Item)
                {
                    PointerPoint PointerInfo = e.GetCurrentPoint(null);

                    if ((e.OriginalSource as FrameworkElement).FindParentOfType<SelectorItem>() != null)
                    {
                        if (ListViewControl.SelectionMode != ListViewSelectionMode.Multiple)
                        {
                            if (e.KeyModifiers == VirtualKeyModifiers.None)
                            {
                                if (ListViewControl.SelectedItems.Contains(Item))
                                {
                                    SelectionExtention.Disable();
                                }
                                else
                                {
                                    if (PointerInfo.Properties.IsLeftButtonPressed)
                                    {
                                        ListViewControl.SelectedItem = Item;
                                    }

                                    if (e.OriginalSource is ListViewItemPresenter)
                                    {
                                        SelectionExtention.Enable();
                                    }
                                    else
                                    {
                                        SelectionExtention.Disable();
                                    }
                                }
                            }
                            else
                            {
                                SelectionExtention.Disable();
                            }
                        }
                        else
                        {
                            SelectionExtention.Disable();
                        }
                    }
                }
                else if (Element.FindParentOfType<ScrollBar>() is ScrollBar)
                {
                    SelectionExtention.Disable();
                }
                else
                {
                    ListViewControl.SelectedItem = null;
                    SelectionExtention.Enable();
                }
            }
            else
            {
                ListViewControl.SelectedItem = null;
                SelectionExtention.Enable();
            }
        }

        private void ListViewControl_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                args.ItemContainer.PointerEntered -= ItemContainer_PointerEntered;
                args.ItemContainer.PointerExited -= ItemContainer_PointerExited;
                args.ItemContainer.PointerCanceled -= ItemContainer_PointerCanceled;
            }
            else
            {
                args.ItemContainer.PointerEntered += ItemContainer_PointerEntered;
                args.ItemContainer.PointerExited += ItemContainer_PointerExited;
                args.ItemContainer.PointerCanceled += ItemContainer_PointerCanceled;

                args.RegisterUpdateCallback(async (s, e) =>
                {
                    if (e.Item is FileSystemStorageItemBase Item)
                    {
                        await Item.LoadAsync().ConfigureAwait(false);
                    }
                });
            }
        }

        private void ItemContainer_PointerCanceled(object sender, PointerRoutedEventArgs e)
        {
            DelaySelectionCancellation?.Cancel();
        }

        private void ItemContainer_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            DelaySelectionCancellation?.Cancel();
        }

        private void ItemContainer_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            if ((sender as SelectorItem)?.Content is FileSystemStorageItemBase Item)
            {
                if (!SettingPage.IsDoubleClickEnabled
                && !ListViewControl.SelectedItems.Contains(Item)
                && !e.KeyModifiers.HasFlag(VirtualKeyModifiers.Control)
                && !e.KeyModifiers.HasFlag(VirtualKeyModifiers.Shift))
                {
                    DelaySelectionCancellation?.Cancel();
                    DelaySelectionCancellation?.Dispose();
                    DelaySelectionCancellation = new CancellationTokenSource();

                    Task.Delay(800).ContinueWith((task, input) =>
                    {
                        if (input is CancellationToken Token && !Token.IsCancellationRequested)
                        {
                            ListViewControl.SelectedItem = Item;
                        }
                    }, DelaySelectionCancellation.Token, TaskScheduler.FromCurrentSynchronizationContext());
                }
            }
        }

        private void ListViewControl_Holding(object sender, HoldingRoutedEventArgs e)
        {
            if (e.HoldingState == HoldingState.Started)
            {
                if (!SettingPage.IsDoubleClickEnabled)
                {
                    DelaySelectionCancellation?.Cancel();
                }

                if (e.OriginalSource is ListViewItemPresenter)
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
            if (ListViewControl.SelectedItem is FileSystemStorageItemBase Item)
            {
                PropertiesWindowBase NewWindow = await PropertiesWindowBase.CreateAsync(Item);
                await NewWindow.ShowAsync(new Point(Window.Current.Bounds.Width / 2 - 200, Window.Current.Bounds.Height / 2 - 300));
            }
        }

        private void ListViewControl_RightTapped(object sender, RightTappedRoutedEventArgs e)
        {
            if (e.PointerDeviceType == Windows.Devices.Input.PointerDeviceType.Mouse)
            {
                if (!SettingPage.IsDoubleClickEnabled)
                {
                    DelaySelectionCancellation?.Cancel();
                }

                if (e.OriginalSource is FrameworkElement Element)
                {
                    if (Element.DataContext is IRecycleStorageItem Context)
                    {
                        if (ListViewControl.SelectedItems.Count > 1 && ListViewControl.SelectedItems.Contains(Context))
                        {
                            ListViewControl.ContextFlyout = SelectFlyout;
                        }
                        else
                        {
                            if (ListViewControl.SelectedItem as IRecycleStorageItem == Context)
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

        private void ListHeader_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button Btn)
            {
                SortTarget Target = Btn.Name switch
                {
                    "ListHeaderName" => SortTarget.Name,
                    "ListHeaderOriginLocation" => SortTarget.OriginPath,
                    "ListHeaderModifiedTime" => SortTarget.ModifiedTime,
                    "ListHeaderType" => SortTarget.Type,
                    "ListHeaderSize" => SortTarget.Size,
                    _ => SortTarget.Name
                };

                if (CurrentSortTarget == Target)
                {
                    CurrentSortDirection = CurrentSortDirection == SortDirection.Ascending ? SortDirection.Descending : SortDirection.Ascending;
                }
                else
                {
                    CurrentSortTarget = Target;
                    CurrentSortDirection = SortDirection.Ascending;
                }

                IRecycleStorageItem[] SortResult = SortCollectionGenerator.GetSortedCollection(FileCollection, CurrentSortTarget, CurrentSortDirection).ToArray();

                FileCollection.Clear();

                foreach (IRecycleStorageItem Item in SortResult)
                {
                    FileCollection.Add(Item);
                }
            }
        }

        private async void PropertyButton_Click(object sender, RoutedEventArgs e)
        {
            if (ListViewControl.SelectedItem is FileSystemStorageItemBase Item)
            {
                PropertiesWindowBase NewWindow = await PropertiesWindowBase.CreateAsync(Item);
                await NewWindow.ShowAsync(new Point(Window.Current.Bounds.Width / 2 - 200, Window.Current.Bounds.Height / 2 - 300));
            }
        }

        private async void PermanentDelete_Click(object sender, RoutedEventArgs e)
        {
            if (ListViewControl.SelectedItems.Count > 0)
            {
                ControlLoading(true, Globalization.GetString("RecycleBinDeleteText"));

                QueueContentDialog QueueContenDialog = new QueueContentDialog
                {
                    Title = Globalization.GetString("Common_Dialog_WarningTitle"),
                    Content = Globalization.GetString("QueueDialog_DeleteFile_Content"),
                    PrimaryButtonText = Globalization.GetString("Common_Dialog_ContinueButton"),
                    CloseButtonText = Globalization.GetString("Common_Dialog_CancelButton")
                };

                if ((await QueueContenDialog.ShowAsync()) == ContentDialogResult.Primary)
                {
                    List<string> ErrorList = new List<string>();

                    foreach (IRecycleStorageItem Item in ListViewControl.SelectedItems.ToList())
                    {
                        if (await Item.DeleteAsync())
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
                        _ = Dialog.ShowAsync();
                    }
                }

                ControlLoading(false);

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

            if (await Dialog.ShowAsync() == ContentDialogResult.Primary)
            {
                ControlLoading(true, Globalization.GetString("RecycleBinEmptyingText"));

                using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableControllerAsync())
                {
                    if (await Exclusive.Controller.EmptyRecycleBinAsync())
                    {
                        ControlLoading(false);

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

                        await dialog.ShowAsync();

                        ControlLoading(false);
                    }
                }
            }
        }

        private async void RestoreRecycle_Click(object sender, RoutedEventArgs e)
        {
            if (ListViewControl.SelectedItems.Count > 0)
            {
                ControlLoading(true, Globalization.GetString("RecycleBinRestoreText"));

                List<string> ErrorList = new List<string>();

                foreach (IRecycleStorageItem Item in ListViewControl.SelectedItems.ToList())
                {
                    if (await Item.RestoreAsync())
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
                    _ = Dialog.ShowAsync();
                }

                ControlLoading(false);

                if (FileCollection.Count == 0)
                {
                    HasFile.Visibility = Visibility.Visible;
                    ClearRecycleBin.IsEnabled = false;
                }
            }
        }

        private async void ControlLoading(bool IsLoading, string Message = null)
        {
            if (IsLoading)
            {
                ProgressInfo.Text = $"{Message}...";
                LoadingControl.IsLoading = true;
            }
            else
            {
                await Task.Delay(500);
                LoadingControl.IsLoading = false;
            }
        }

        private async void Refresh_Click(object sender, RoutedEventArgs e)
        {
            FileCollection.Clear();

            using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableControllerAsync())
            {
                foreach (IRecycleStorageItem Item in SortCollectionGenerator.GetSortedCollection(await Exclusive.Controller.GetRecycleBinItemsAsync(), SortTarget.Name, SortDirection.Ascending))
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
