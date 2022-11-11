using Microsoft.Toolkit.Uwp.UI.Controls;
using RX_Explorer.Class;
using RX_Explorer.Interface;
using RX_Explorer.SeparateWindow.PropertyWindow;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.Core;
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
        private readonly PointerEventHandler PointerPressedHandler;
        private readonly ListViewColumnWidthSaver ColumnWidthSaver = new ListViewColumnWidthSaver(ListViewLocation.RecycleBin);
        private readonly ObservableCollection<IRecycleStorageItem> FileCollection = new ObservableCollection<IRecycleStorageItem>();
        private readonly SortIndicatorController ListViewHeaderSortIndicator = new SortIndicatorController();

        private ListViewBaseSelectionExtension SelectionExtension;
        private CancellationTokenSource DelaySelectionCancellation;

        public RecycleBin()
        {
            InitializeComponent();
            PointerPressedHandler = new PointerEventHandler(ListViewControl_PointerPressed);
            Loaded += RecycleBin_Loaded;
            Unloaded += RecycleBin_Unloaded;
        }

        private void RecycleBin_Unloaded(object sender, RoutedEventArgs e)
        {
            CoreApplication.MainView.CoreWindow.KeyDown -= RecycleBin_KeyDown;
        }

        private void RecycleBin_Loaded(object sender, RoutedEventArgs e)
        {
            CoreApplication.MainView.CoreWindow.KeyDown += RecycleBin_KeyDown;
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
            SelectionExtension = new ListViewBaseSelectionExtension(ListViewControl, DrawRectangle);
            ListViewHeaderSortIndicator.Target = SortTarget.Name;
            ListViewHeaderSortIndicator.Direction = SortDirection.Ascending;

            ControlLoading(true, Globalization.GetString("Progress_Tip_Loading"));

            try
            {
                using (AuxiliaryTrustProcessController.Exclusive Exclusive = await AuxiliaryTrustProcessController.GetControllerExclusiveAsync())
                {
                    FileCollection.AddRange(await SortedCollectionGenerator.GetSortedCollectionAsync(await Exclusive.Controller.GetRecycleBinItemsAsync(), SortTarget.Name, SortDirection.Ascending, SortStyle.None));
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
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"An exception was threw in {nameof(OnNavigatedTo)}");
            }
            finally
            {
                ControlLoading(false);
            }
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            SelectionExtension?.Dispose();

            DelaySelectionCancellation?.Cancel();
            DelaySelectionCancellation?.Dispose();
            DelaySelectionCancellation = null;

            ListViewControl.RemoveHandler(PointerPressedEvent, PointerPressedHandler);

            FileCollection.Clear();
        }

        private void ListViewControl_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            Window.Current.CoreWindow.PointerCursor = new CoreCursor(CoreCursorType.Arrow, 0);

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
                                    SelectionExtension.Disable();
                                }
                                else
                                {
                                    if (PointerInfo.Properties.IsLeftButtonPressed)
                                    {
                                        ListViewControl.SelectedItem = Item;
                                    }

                                    if (e.OriginalSource is ListViewItemPresenter)
                                    {
                                        SelectionExtension.Enable();
                                    }
                                    else
                                    {
                                        SelectionExtension.Disable();
                                    }
                                }
                            }
                            else
                            {
                                SelectionExtension.Disable();
                            }
                        }
                        else
                        {
                            SelectionExtension.Disable();
                        }
                    }
                }
                else if (Element.FindParentOfType<ScrollBar>() is ScrollBar)
                {
                    SelectionExtension.Disable();
                }
                else if (Element.FindParentOfType<GridSplitter>() is not null || Element.FindParentOfType<Button>() is not null)
                {
                    ListViewControl.SelectedItem = null;
                    SelectionExtension.Disable();
                }
                else
                {
                    ListViewControl.SelectedItem = null;
                    SelectionExtension.Enable();
                }
            }
            else
            {
                ListViewControl.SelectedItem = null;
                SelectionExtension.Enable();
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
                        try
                        {
                            await Item.LoadAsync().ConfigureAwait(false);
                        }
                        catch (Exception ex)
                        {
                            LogTracer.Log(ex, $"Could not load the storage item, StorageType: {Item.GetType().FullName}, Path: {Item.Path}");
                        }
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

        private async void ListViewControl_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
        {
            if (ListViewControl.SelectedItem is FileSystemStorageItemBase Item)
            {
                PropertiesWindowBase NewWindow = await PropertiesWindowBase.CreateAsync(Item);
                await NewWindow.ShowAsync(new Point(Window.Current.Bounds.Width / 2 - 200, Window.Current.Bounds.Height / 2 - 300));
            }
        }

        private async void ListHeader_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button Btn)
            {
                SortTarget Target = Btn.Name switch
                {
                    "ListHeaderName" => SortTarget.Name,
                    "ListHeaderOriginLocation" => SortTarget.OriginPath,
                    "ListHeaderRecycleDate" => SortTarget.RecycleDate,
                    "ListHeaderType" => SortTarget.Type,
                    "ListHeaderSize" => SortTarget.Size,
                    _ => SortTarget.Name
                };

                if (ListViewHeaderSortIndicator.Target == Target)
                {
                    ListViewHeaderSortIndicator.Direction = ListViewHeaderSortIndicator.Direction == SortDirection.Ascending ? SortDirection.Descending : SortDirection.Ascending;
                }
                else
                {
                    ListViewHeaderSortIndicator.Target = Target;
                    ListViewHeaderSortIndicator.Direction = SortDirection.Ascending;
                }

                FileCollection.AddRange(await SortedCollectionGenerator.GetSortedCollectionAsync(FileCollection.DuplicateAndClear(), ListViewHeaderSortIndicator.Target, ListViewHeaderSortIndicator.Direction, Target == SortTarget.Type ? SortStyle.UseFileSystemStyle : SortStyle.None));
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
            IReadOnlyList<IRecycleStorageItem> SelectedItems = ListViewControl.SelectedItems.Cast<IRecycleStorageItem>().ToArray();

            if (SelectedItems.Count > 0)
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
                    Queue<string> ErrorList = new Queue<string>();

                    foreach (IRecycleStorageItem Item in SelectedItems)
                    {
                        if (await Item.DeleteAsync())
                        {
                            FileCollection.Remove(Item);
                        }
                        else
                        {
                            ErrorList.Enqueue(Item.Name);
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

                        await Dialog.ShowAsync();
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

                try
                {
                    using (AuxiliaryTrustProcessController.Exclusive Exclusive = await AuxiliaryTrustProcessController.GetControllerExclusiveAsync())
                    {
                        if (await Exclusive.Controller.EmptyRecycleBinAsync())
                        {
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
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogTracer.Log(ex, $"An exception was threw in {nameof(ClearRecycleBin_Click)}");
                }
                finally
                {
                    ControlLoading(false);
                }
            }
        }

        private async void RestoreRecycle_Click(object sender, RoutedEventArgs e)
        {
            IReadOnlyList<IRecycleStorageItem> SelectedItems = ListViewControl.SelectedItems.Cast<IRecycleStorageItem>().ToArray();

            if (SelectedItems.Count > 0)
            {
                ControlLoading(true, Globalization.GetString("RecycleBinRestoreText"));

                Queue<string> ErrorList = new Queue<string>();

                foreach (IRecycleStorageItem Item in SelectedItems.Cast<IRecycleStorageItem>())
                {
                    if (await Item.RestoreAsync())
                    {
                        FileCollection.Remove(Item);
                    }
                    else
                    {
                        ErrorList.Enqueue(Item.Name);
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

                    await Dialog.ShowAsync();
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

            try
            {
                using (AuxiliaryTrustProcessController.Exclusive Exclusive = await AuxiliaryTrustProcessController.GetControllerExclusiveAsync())
                {
                    FileCollection.AddRange(await SortedCollectionGenerator.GetSortedCollectionAsync(await Exclusive.Controller.GetRecycleBinItemsAsync(), SortTarget.Name, SortDirection.Ascending, SortStyle.None));
                }

                if (FileCollection.Count > 0)
                {
                    HasFile.Visibility = Visibility.Collapsed;
                    ClearRecycleBin.IsEnabled = true;
                }
                else
                {
                    HasFile.Visibility = Visibility.Visible;
                    ClearRecycleBin.IsEnabled = false;
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"An exception was threw in {nameof(Refresh_Click)}");
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

        private void ListViewControl_ContextRequested(UIElement sender, ContextRequestedEventArgs args)
        {
            args.Handled = true;

            if (args.TryGetPosition(sender, out Point Position))
            {
                if ((args.OriginalSource as FrameworkElement)?.DataContext is IRecycleStorageItem Context)
                {
                    if (!SettingPage.IsDoubleClickEnabled)
                    {
                        DelaySelectionCancellation?.Cancel();
                    }

                    if (ListViewControl.SelectedItems.Count > 1 && ListViewControl.SelectedItems.Contains(Context))
                    {
                        SelectFlyout.ShowAt(ListViewControl, new FlyoutShowOptions
                        {
                            Position = Position,
                            Placement = FlyoutPlacementMode.BottomEdgeAlignedLeft,
                            ShowMode = FlyoutShowMode.Standard
                        });
                    }
                    else
                    {
                        if (ListViewControl.SelectedItem == Context)
                        {
                            SelectFlyout.ShowAt(ListViewControl, new FlyoutShowOptions
                            {
                                Position = Position,
                                Placement = FlyoutPlacementMode.BottomEdgeAlignedLeft,
                                ShowMode = FlyoutShowMode.Standard
                            });
                        }
                        else
                        {
                            if (args.OriginalSource is TextBlock)
                            {
                                ListViewControl.SelectedItem = Context;

                                SelectFlyout.ShowAt(ListViewControl, new FlyoutShowOptions
                                {
                                    Position = Position,
                                    Placement = FlyoutPlacementMode.BottomEdgeAlignedLeft,
                                    ShowMode = FlyoutShowMode.Standard
                                });
                            }
                            else
                            {
                                ListViewControl.SelectedItem = null;

                                EmptyFlyout.ShowAt(ListViewControl, new FlyoutShowOptions
                                {
                                    Position = Position,
                                    Placement = FlyoutPlacementMode.BottomEdgeAlignedLeft,
                                    ShowMode = FlyoutShowMode.Standard
                                });
                            }
                        }
                    }
                }
                else
                {
                    ListViewControl.SelectedItem = null;

                    EmptyFlyout.ShowAt(ListViewControl, new FlyoutShowOptions
                    {
                        Position = Position,
                        Placement = FlyoutPlacementMode.BottomEdgeAlignedLeft,
                        ShowMode = FlyoutShowMode.Standard
                    });
                }
            }
        }

        private void ListViewControl_ContextCanceled(UIElement sender, RoutedEventArgs args)
        {
            try
            {
                SelectFlyout.Hide();
                EmptyFlyout.Hide();
            }
            catch (Exception)
            {
                // No need to handle this exception
            }
        }
    }
}
