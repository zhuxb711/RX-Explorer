using Microsoft.Toolkit.Deferred;
using RX_Explorer.Class;
using RX_Explorer.Dialog;
using RX_Explorer.Interface;
using RX_Explorer.SeparateWindow.PropertyWindow;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Core;
using Windows.ApplicationModel.DataTransfer;
using Windows.Devices.Input;
using Windows.Foundation;
using Windows.Storage;
using Windows.System;
using Windows.UI.Core;
using Windows.UI.Input;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Navigation;
using Package = Windows.ApplicationModel.Package;
using RefreshRequestedEventArgs = RX_Explorer.Class.RefreshRequestedEventArgs;

namespace RX_Explorer.View
{
    public sealed partial class SearchPage : Page
    {
        private CancellationTokenSource SearchCancellation;
        private CancellationTokenSource DelayDragCancellation;
        private CancellationTokenSource DelaySelectionCancellation;
        private CancellationTokenSource DelayTooltipCancellation;
        private CancellationTokenSource ContextMenuCancellation;

        private ListViewBaseSelectionExtension SelectionExtension;
        private readonly PointerEventHandler PointerPressedEventHandler;
        private readonly PointerEventHandler PointerReleasedEventHandler;
        private readonly ListViewHeaderController ListViewDetailHeader = new ListViewHeaderController();
        private readonly ObservableCollection<FileSystemStorageItemBase> SearchResult = new ObservableCollection<FileSystemStorageItemBase>();
        private readonly SignalContext SignalControl = new SignalContext();

        private SortTarget STarget;
        private SortDirection SDirection;

        private DateTimeOffset LastPressTime;
        private string LastPressString;
        private bool BlockKeyboardShortCutInput;
        private int HeaderClickLocker;

        public SearchPage()
        {
            InitializeComponent();

            ListViewDetailHeader.Filter.RefreshListRequested += Filter_RefreshListRequested;

            PointerPressedEventHandler = new PointerEventHandler(ViewControl_PointerPressed);
            PointerReleasedEventHandler = new PointerEventHandler(ViewControl_PointerReleased);

            Loaded += SearchPage_Loaded;
            Unloaded += SearchPage_Unloaded;
        }

        private void SearchPage_Unloaded(object sender, RoutedEventArgs e)
        {
            CoreApplication.MainView.CoreWindow.KeyDown -= SearchPage_KeyDown;
        }

        private void SearchPage_Loaded(object sender, RoutedEventArgs e)
        {
            CoreApplication.MainView.CoreWindow.KeyDown += SearchPage_KeyDown;
        }

        private async void Filter_RefreshListRequested(object sender, RefreshRequestedEventArgs e)
        {
            SearchResult.Clear();

            foreach (FileSystemStorageItemBase Item in await SortCollectionGenerator.GetSortedCollectionAsync(e.FilterCollection, STarget, SDirection))
            {
                SearchResult.Add(Item);
            }
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            if (e.Parameter is SearchOptions Parameters)
            {
                SearchResultList.AddHandler(PointerPressedEvent, PointerPressedEventHandler, true);
                SearchResultList.AddHandler(PointerReleasedEvent, PointerReleasedEventHandler, true);

                SearchCancellation = new CancellationTokenSource();
                SelectionExtension = new ListViewBaseSelectionExtension(SearchResultList, DrawRectangle);

                if (e.NavigationMode == NavigationMode.New)
                {
                    await SearchAsync(Parameters, SearchCancellation.Token).ConfigureAwait(false);
                }
            }
        }

        private void CloseAllFlyout()
        {
            try
            {
                SingleCommandFlyout.Hide();
                MixCommandFlyout.Hide();
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, "Could not close the flyout for unknown reason");
            }
        }

        private async void SearchPage_KeyDown(CoreWindow sender, KeyEventArgs args)
        {
            CloseAllFlyout();

            try
            {
                if (!BlockKeyboardShortCutInput)
                {
                    CoreVirtualKeyStates CtrlState = sender.GetKeyState(VirtualKey.Control);
                    CoreVirtualKeyStates ShiftState = sender.GetKeyState(VirtualKey.Shift);

                    if (!CtrlState.HasFlag(CoreVirtualKeyStates.Down) && !ShiftState.HasFlag(CoreVirtualKeyStates.Down))
                    {
                        NavigateToStorageItem(args.VirtualKey);
                    }

                    switch (args.VirtualKey)
                    {
                        case VirtualKey.Enter:
                            {
                                Open_Click(null, null);
                                break;
                            }
                        case VirtualKey.L when CtrlState.HasFlag(CoreVirtualKeyStates.Down):
                            {
                                Location_Click(null, null);
                                break;
                            }
                        case VirtualKey.A when CtrlState.HasFlag(CoreVirtualKeyStates.Down):
                            {
                                SearchResultList.SelectAll();
                                break;
                            }
                        case VirtualKey.C when CtrlState.HasFlag(CoreVirtualKeyStates.Down):
                            {
                                Copy_Click(null, null);
                                break;
                            }
                        case VirtualKey.X when CtrlState.HasFlag(CoreVirtualKeyStates.Down):
                            {
                                Cut_Click(null, null);
                                break;
                            }
                        case VirtualKey.Delete:
                        case VirtualKey.D when CtrlState.HasFlag(CoreVirtualKeyStates.Down):
                            {
                                Delete_Click(null, null);
                                break;
                            }
                        case VirtualKey.Space when SettingPage.IsQuicklookEnabled
                                                   && !SettingPage.IsOpened
                                                   && SearchResultList.SelectedItems.Count == 1:
                            {
                                using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableControllerAsync())
                                {
                                    if (await Exclusive.Controller.CheckIfQuicklookIsAvaliableAsync())
                                    {
                                        if (SearchResultList.SelectedItem is FileSystemStorageItemBase Item)
                                        {
                                            await Exclusive.Controller.ToggleQuicklookAsync(Item.Path);
                                        }
                                    }
                                }

                                break;
                            }
                    }
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"An exception was threw in {nameof(SearchPage_KeyDown)}");
            }
        }

        private void NavigateToStorageItem(VirtualKey Key)
        {
            if (Key >= VirtualKey.Number0 && Key <= VirtualKey.Z)
            {
                string SearchString = Convert.ToChar(Key).ToString();

                try
                {
                    if (LastPressString != SearchString && (DateTimeOffset.Now - LastPressTime).TotalMilliseconds < 1200)
                    {
                        SearchString = LastPressString + SearchString;

                        IEnumerable<FileSystemStorageItemBase> Group = SearchResult.Where((Item) => Item.Name.StartsWith(SearchString, StringComparison.OrdinalIgnoreCase));

                        if (Group.Any() && !Group.Contains(SearchResultList.SelectedItem))
                        {
                            SearchResultList.SelectedItem = Group.FirstOrDefault();
                            SearchResultList.ScrollIntoView(SearchResultList.SelectedItem);
                        }
                    }
                    else
                    {
                        IEnumerable<FileSystemStorageItemBase> Group = SearchResult.Where((Item) => Item.Name.StartsWith(SearchString, StringComparison.OrdinalIgnoreCase));

                        if (Group.Any())
                        {
                            if (SearchResultList.SelectedItem != null)
                            {
                                FileSystemStorageItemBase[] ItemArray = Group.ToArray();

                                int NextIndex = Array.IndexOf(ItemArray, SearchResultList.SelectedItem);

                                if (NextIndex != -1)
                                {
                                    if (NextIndex < ItemArray.Length - 1)
                                    {
                                        SearchResultList.SelectedItem = ItemArray[NextIndex + 1];
                                    }
                                    else
                                    {
                                        SearchResultList.SelectedItem = ItemArray.FirstOrDefault();
                                    }
                                }
                                else
                                {
                                    SearchResultList.SelectedItem = ItemArray.FirstOrDefault();
                                }
                            }
                            else
                            {
                                SearchResultList.SelectedItem = Group.FirstOrDefault();
                            }

                            SearchResultList.ScrollIntoView(SearchResultList.SelectedItem);
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogTracer.Log(ex, $"{nameof(NavigateToStorageItem)} throw an exception");
                }
                finally
                {
                    LastPressString = SearchString;
                    LastPressTime = DateTimeOffset.Now;
                }
            }
        }

        private async Task SearchAsync(SearchOptions Options, CancellationToken CancelToken)
        {
            STarget = SortTarget.Name;
            SDirection = SortDirection.Ascending;
            HasItem.Visibility = Visibility.Collapsed;

            if (SettingPage.IsSearchHistoryEnabled)
            {
                SQLite.Current.SetSearchHistory(Options.SearchText);
            }

            try
            {
                RootGrid.DataContext = Options;
                SearchStatus.Text = $"{Globalization.GetString("SearchProcessingText")} \"{Options.SearchText}\"";
                SearchStatusBar.Visibility = Visibility.Visible;

                switch (Options.EngineCategory)
                {
                    case SearchCategory.BuiltInEngine:
                        {
                            await foreach (FileSystemStorageItemBase Item in Options.SearchFolder.SearchAsync(Options.SearchText,
                                                                                                              Options.DeepSearch,
                                                                                                              SettingPage.IsShowHiddenFilesEnabled,
                                                                                                              SettingPage.IsDisplayProtectedSystemItems,
                                                                                                              Options.UseRegexExpression,
                                                                                                              Options.UseAQSExpression,
                                                                                                              Options.UseIndexerOnly,
                                                                                                              Options.IgnoreCase,
                                                                                                              CancelToken))
                            {
                                await SignalControl.TrapOnSignalAsync();

                                int Index = await SortCollectionGenerator.SearchInsertLocationAsync(SearchResult, Item, STarget, SDirection);

                                if (Index >= 0)
                                {
                                    SearchResult.Insert(Index, Item);
                                }
                                else
                                {
                                    SearchResult.Add(Item);
                                }

                                if (SearchResult.Count % 50 == 0)
                                {
                                    await ListViewDetailHeader.Filter.SetDataSourceAsync(SearchResult);
                                }
                            }

                            break;
                        }
                    case SearchCategory.EverythingEngine:
                        {
                            IReadOnlyList<string> SearchItems;

                            using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableControllerAsync())
                            {
                                SearchItems = await Exclusive.Controller.SearchByEverythingAsync(Options.DeepSearch ? string.Empty : Options.SearchFolder.Path,
                                                                                                 Options.SearchText,
                                                                                                 Options.UseRegexExpression,
                                                                                                 Options.IgnoreCase);
                            }

                            await foreach (FileSystemStorageItemBase Item in FileSystemStorageItemBase.OpenInBatchAsync(SearchItems, CancelToken))
                            {
                                await SignalControl.TrapOnSignalAsync();

                                int Index = await SortCollectionGenerator.SearchInsertLocationAsync(SearchResult, Item, STarget, SDirection);

                                if (Index >= 0)
                                {
                                    SearchResult.Insert(Index, Item);
                                }
                                else
                                {
                                    SearchResult.Add(Item);
                                }

                                if (SearchResult.Count % 50 == 0)
                                {
                                    await ListViewDetailHeader.Filter.SetDataSourceAsync(SearchResult);
                                }
                            }

                            break;
                        }
                }

                if (CancelToken.IsCancellationRequested)
                {
                    SearchResult.Clear();
                }

                if (SearchResult.Count == 0)
                {
                    HasItem.Visibility = Visibility.Visible;
                }

                await ListViewDetailHeader.Filter.SetDataSourceAsync(SearchResult);
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"An error was threw in {nameof(SearchAsync)}");
            }
            finally
            {
                SignalControl.SetCompleted();
                SearchStatusBar.Visibility = Visibility.Collapsed;
                SearchStatus.Text = $"{Globalization.GetString("SearchCompletedText")} ({SearchResult.Count} {Globalization.GetString("Items_Description")})";
            }
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            SearchResultList.RemoveHandler(PointerPressedEvent, PointerPressedEventHandler);
            SearchResultList.RemoveHandler(PointerReleasedEvent, PointerReleasedEventHandler);

            SearchCancellation?.Cancel();
            SearchCancellation?.Dispose();

            SelectionExtension.Dispose();
            SelectionExtension = null;

            DelayDragCancellation?.Cancel();
            DelayDragCancellation?.Dispose();
            DelayDragCancellation = null;

            DelaySelectionCancellation?.Cancel();
            DelaySelectionCancellation?.Dispose();
            DelaySelectionCancellation = null;

            DelayTooltipCancellation?.Cancel();
            DelayTooltipCancellation?.Dispose();
            DelayTooltipCancellation = null;

            if (e.NavigationMode == NavigationMode.Back)
            {
                SearchResult.Clear();
            }
        }

        private void ViewControl_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (e.OriginalSource is FrameworkElement Element)
            {
                if (Element.DataContext is FileSystemStorageItemBase Item)
                {
                    PointerPoint PointerInfo = e.GetCurrentPoint(null);

                    if (Element.FindParentOfType<SelectorItem>() is SelectorItem SItem)
                    {
                        if (e.KeyModifiers == VirtualKeyModifiers.None && SearchResultList.SelectionMode != ListViewSelectionMode.Multiple)
                        {
                            if (SearchResultList.SelectedItems.Contains(Item))
                            {
                                SelectionExtension.Disable();

                                if (e.Pointer.PointerDeviceType == PointerDeviceType.Mouse)
                                {
                                    DelayDragCancellation?.Cancel();
                                    DelayDragCancellation?.Dispose();
                                    DelayDragCancellation = new CancellationTokenSource();

                                    Task.Delay(300).ContinueWith(async (task, input) =>
                                    {
                                        try
                                        {
                                            if (input is (CancellationToken Token, UIElement Item, PointerPoint Point) && !Token.IsCancellationRequested)
                                            {
                                                await Item.StartDragAsync(Point);
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            LogTracer.Log(ex, "Could not start drag item");
                                        }
                                    }, (DelayDragCancellation.Token, SItem, e.GetCurrentPoint(SItem)), TaskScheduler.FromCurrentSynchronizationContext());
                                }
                            }
                            else
                            {
                                if (PointerInfo.Properties.IsLeftButtonPressed)
                                {
                                    SearchResultList.SelectedItem = Item;
                                }

                                switch (Element)
                                {
                                    case Grid:
                                    case ListViewItemPresenter:
                                        {
                                            SelectionExtension.Enable();
                                            DelayTooltipCancellation?.Cancel();
                                            break;
                                        }
                                    default:
                                        {
                                            SelectionExtension.Disable();

                                            if (e.Pointer.PointerDeviceType == PointerDeviceType.Mouse)
                                            {
                                                DelayDragCancellation?.Cancel();
                                                DelayDragCancellation?.Dispose();
                                                DelayDragCancellation = new CancellationTokenSource();

                                                Task.Delay(300).ContinueWith(async (task, input) =>
                                                {
                                                    try
                                                    {
                                                        if (input is (CancellationToken Token, UIElement Item, PointerPoint Point) && !Token.IsCancellationRequested)
                                                        {
                                                            await Item.StartDragAsync(Point);
                                                        }
                                                    }
                                                    catch (Exception ex)
                                                    {
                                                        LogTracer.Log(ex, "Could not start drag item");
                                                    }
                                                }, (DelayDragCancellation.Token, SItem, e.GetCurrentPoint(SItem)), TaskScheduler.FromCurrentSynchronizationContext());
                                            }

                                            break;
                                        }
                                }
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
                else
                {
                    SearchResultList.SelectedItem = null;
                    SelectionExtension.Enable();
                }
            }
            else
            {
                SearchResultList.SelectedItem = null;
                SelectionExtension.Enable();
            }
        }

        private void ViewControl_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            DelayDragCancellation?.Cancel();
        }

        private async void Location_Click(object sender, RoutedEventArgs e)
        {
            if (SearchResultList.SelectedItem is FileSystemStorageItemBase Item)
            {
                try
                {
                    await TabViewContainer.Current.CreateNewTabAsync(Item.Path);
                    await JumpListController.Current.AddItemAsync(JumpListGroup.Recent, Path.GetDirectoryName(Item.Path));
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

                    await dialog.ShowAsync();
                }
            }
        }

        private async void Attribute_Click(object sender, RoutedEventArgs e)
        {
            if (SearchResultList.SelectedItems.Count > 0)
            {
                PropertiesWindowBase NewWindow = await PropertiesWindowBase.CreateAsync(SearchResultList.SelectedItems.Cast<FileSystemStorageItemBase>().ToArray());
                await NewWindow.ShowAsync(new Point(Window.Current.Bounds.Width / 2 - 200, Window.Current.Bounds.Height / 2 - 300));
            }
        }

        private async void CopyPath_Click(object sender, RoutedEventArgs e)
        {
            if (SearchResultList.SelectedItem is FileSystemStorageItemBase SelectItem)
            {
                try
                {
                    DataPackage Package = new DataPackage();
                    Package.SetText(SelectItem.Path);
                    Clipboard.SetContent(Package);
                }
                catch
                {
                    QueueContentDialog Dialog = new QueueContentDialog
                    {
                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                        Content = Globalization.GetString("QueueDialog_UnableAccessClipboard_Content"),
                        CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                    };

                    await Dialog.ShowAsync();
                }
            }
        }

        private void SearchResultList_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                args.ItemContainer.DragStarting -= ItemContainer_DragStarting;
                args.ItemContainer.PointerEntered -= ItemContainer_PointerEntered;
                args.ItemContainer.PointerExited -= ItemContainer_PointerExited;
                args.ItemContainer.PointerCanceled -= ItemContainer_PointerCanceled;
            }
            else
            {
                args.ItemContainer.DragStarting += ItemContainer_DragStarting;
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
            DelayTooltipCancellation?.Cancel();
        }

        private void ItemContainer_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            DelaySelectionCancellation?.Cancel();
            DelayTooltipCancellation?.Cancel();
        }

        private void ItemContainer_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            if ((sender as SelectorItem)?.Content is FileSystemStorageItemBase Item)
            {
                if (!SettingPage.IsDoubleClickEnabled
                    && !SearchResultList.SelectedItems.Contains(Item)
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
                            SearchResultList.SelectedItem = Item;
                        }
                    }, DelaySelectionCancellation.Token, TaskScheduler.FromCurrentSynchronizationContext());
                }

                if (Item is not IMTPStorageItem)
                {
                    DelayTooltipCancellation?.Cancel();
                    DelayTooltipCancellation?.Dispose();
                    DelayTooltipCancellation = new CancellationTokenSource();

                    Task.Delay(800).ContinueWith(async (task, input) =>
                    {
                        try
                        {
                            if (input is CancellationToken Token && !Token.IsCancellationRequested)
                            {
                                TooltipFlyout.Hide();

                                using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableControllerAsync())
                                {
                                    TooltipFlyoutText.Text = await Exclusive.Controller.GetTooltipTextAsync(Item.Path, Token);

                                    if (!string.IsNullOrWhiteSpace(TooltipFlyoutText.Text)
                                        && !Token.IsCancellationRequested
                                        && !MixCommandFlyout.IsOpen
                                        && !SingleCommandFlyout.IsOpen)
                                    {
                                        PointerPoint Point = e.GetCurrentPoint(SearchResultList);

                                        TooltipFlyout.ShowAt(SearchResultList, new FlyoutShowOptions
                                        {
                                            Position = new Point(Point.Position.X, Point.Position.Y + 25),
                                            ShowMode = FlyoutShowMode.TransientWithDismissOnPointerMoveAway,
                                            Placement = FlyoutPlacementMode.BottomEdgeAlignedLeft
                                        });
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            LogTracer.Log(ex, "An exception was threw when generate the tooltip flyout");
                        }
                    }, DelayTooltipCancellation.Token, TaskScheduler.FromCurrentSynchronizationContext());
                }
            }
        }

        private async void ItemContainer_DragStarting(UIElement sender, DragStartingEventArgs args)
        {
            DragOperationDeferral Deferral = args.GetDeferral();

            try
            {
                await args.Data.SetStorageItemDataAsync(SearchResultList.SelectedItems.Cast<FileSystemStorageItemBase>().ToArray());
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex);
            }
            finally
            {
                Deferral.Complete();
            }
        }

        private void FilterFlyout_Closing(FlyoutBase sender, FlyoutBaseClosingEventArgs args)
        {
            BlockKeyboardShortCutInput = false;

            if (sender.Target is FrameworkElement Element)
            {
                Element.Visibility = Visibility.Collapsed;
            }
        }

        private void FilterFlyout_Opened(object sender, object e)
        {
            BlockKeyboardShortCutInput = true;
        }

        private void ListHeaderRelativePanel_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            if ((sender as FrameworkElement).FindChildOfName<Button>("NameFilterHeader") is Button NameFilterBtn)
            {
                NameFilterBtn.Visibility = Visibility.Visible;
            }
            else if ((sender as FrameworkElement).FindChildOfName<Button>("ModTimeFilterHeader") is Button ModTimeFilterBtn)
            {
                ModTimeFilterBtn.Visibility = Visibility.Visible;
            }
            else if ((sender as FrameworkElement).FindChildOfName<Button>("TypeFilterHeader") is Button TypeFilterBtn)
            {
                TypeFilterBtn.Visibility = Visibility.Visible;
            }
            else if ((sender as FrameworkElement).FindChildOfName<Button>("SizeFilterHeader") is Button SizeFilterBtn)
            {
                SizeFilterBtn.Visibility = Visibility.Visible;
            }
        }

        private void ListHeader_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
        {
            e.Handled = true;
        }

        private void ListHeaderRelativePanel_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            if ((sender as FrameworkElement).FindChildOfName<Button>("NameFilterHeader") is Button NameFilterBtn)
            {
                if (!NameFilterBtn.Flyout.IsOpen)
                {
                    NameFilterBtn.Visibility = Visibility.Collapsed;
                }
            }
            else if ((sender as FrameworkElement).FindChildOfName<Button>("ModTimeFilterHeader") is Button ModTimeFilterBtn)
            {
                if (!ModTimeFilterBtn.Flyout.IsOpen)
                {
                    ModTimeFilterBtn.Visibility = Visibility.Collapsed;
                }
            }
            else if ((sender as FrameworkElement).FindChildOfName<Button>("TypeFilterHeader") is Button TypeFilterBtn)
            {
                if (!TypeFilterBtn.Flyout.IsOpen)
                {
                    TypeFilterBtn.Visibility = Visibility.Collapsed;
                }
            }
            else if ((sender as FrameworkElement).FindChildOfName<Button>("SizeFilterHeader") is Button SizeFilterBtn)
            {
                if (!SizeFilterBtn.Flyout.IsOpen)
                {
                    SizeFilterBtn.Visibility = Visibility.Collapsed;
                }
            }
        }

        private async void ListHeader_Click(object sender, RoutedEventArgs e)
        {
            if (Interlocked.Exchange(ref HeaderClickLocker, 1) == 0)
            {
                try
                {
                    if (sender is Button Btn)
                    {
                        using (EndUsageNotification Disposable = await SignalControl.SignalAndWaitTrappedAsync())
                        {
                            SortTarget CTarget = Btn.Name switch
                            {
                                "ListHeaderName" => SortTarget.Name,
                                "ListHeaderModifiedTime" => SortTarget.ModifiedTime,
                                "ListHeaderType" => SortTarget.Type,
                                "ListHeaderPath" => SortTarget.Path,
                                "ListHeaderSize" => SortTarget.Size,
                                _ => throw new NotSupportedException()
                            };

                            if (STarget == CTarget)
                            {
                                SDirection = SDirection == SortDirection.Ascending ? SortDirection.Descending : SortDirection.Ascending;
                            }
                            else
                            {
                                STarget = CTarget;
                                SDirection = SortDirection.Ascending;
                            }

                            ListViewDetailHeader.Indicator.SetIndicatorStatus(STarget, SDirection);

                            IReadOnlyList<FileSystemStorageItemBase> SortResult = (await SortCollectionGenerator.GetSortedCollectionAsync(SearchResult, STarget, SDirection)).ToList();

                            SearchResult.Clear();
                            SearchResult.AddRange(SortResult);
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogTracer.Log(ex, $"An exception was threw in {nameof(ListHeader_Click)}");
                }
                finally
                {
                    Interlocked.Exchange(ref HeaderClickLocker, 0);
                }
            }
        }

        private bool TryOpenInternally(FileSystemStorageFile File)
        {
            Type InternalType = File.Type.ToLower() switch
            {
                ".jpg" or ".jpeg" or ".png" or ".bmp" => typeof(PhotoViewer),
                ".mkv" or ".mp4" or ".mp3" or
                ".flac" or ".wma" or ".wmv" or
                ".m4a" or ".mov" or ".alac" => typeof(MediaPlayer),
                ".txt" => typeof(TextViewer),
                ".pdf" => typeof(PdfReader),
                ".zip" => typeof(CompressionViewer),
                _ => null
            };

            if (InternalType != null)
            {
                NavigationTransitionInfo NavigationTransition = AnimationController.Current.IsEnableAnimation
                                                            ? new DrillInNavigationTransitionInfo()
                                                            : new SuppressNavigationTransitionInfo();

                Frame.Navigate(InternalType, File, NavigationTransition);

                return true;
            }
            else
            {
                return false;
            }
        }

        private async void SearchResultList_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
        {
            if (e.OriginalSource is FrameworkElement Element)
            {
                if (Element.FindParentOfType<SelectorItem>()?.Content is FileSystemStorageItemBase Item)
                {
                    DelayDragCancellation?.Cancel();
                    DelaySelectionCancellation?.Cancel();
                    DelayTooltipCancellation?.Cancel();

                    await OpenSelectedItemAsync(Item);
                }
            }
        }

        private async Task OpenSelectedItemAsync(FileSystemStorageItemBase Item)
        {
            try
            {
                switch (Item)
                {
                    case FileSystemStorageFile File:
                        {
                            if (await FileSystemStorageItemBase.CheckExistsAsync(File.Path))
                            {
                                switch (File.Type.ToLower())
                                {
                                    case ".exe":
                                    case ".bat":
                                    case ".msi":
                                    case ".cmd":
                                        {
                                            using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableControllerAsync())
                                            {
                                                if (!await Exclusive.Controller.RunAsync(File.Path, Path.GetDirectoryName(File.Path)))
                                                {
                                                    throw new LaunchProgramException();
                                                }
                                            }

                                            break;
                                        }
                                    case ".msc":
                                        {
                                            using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableControllerAsync())
                                            {
                                                if (!await Exclusive.Controller.RunAsync("powershell.exe", CreateNoWindow: true, Parameters: new string[] { "-Command", File.Path }))
                                                {
                                                    throw new LaunchProgramException();
                                                }
                                            }

                                            break;
                                        }
                                    case ".lnk":
                                        {
                                            if (File is LinkStorageFile LinkItem)
                                            {
                                                if (LinkItem.LinkType == ShellLinkType.Normal)
                                                {
                                                    switch (await FileSystemStorageItemBase.OpenAsync(LinkItem.LinkTargetPath))
                                                    {
                                                        case FileSystemStorageFolder:
                                                            {
                                                                await TabViewContainer.Current.CreateNewTabAsync(LinkItem.LinkTargetPath);
                                                                await JumpListController.Current.AddItemAsync(JumpListGroup.Recent, LinkItem.LinkTargetPath);
                                                                break;
                                                            }
                                                        case FileSystemStorageFile:
                                                            {
                                                                if (!await LinkItem.LaunchAsync())
                                                                {
                                                                    throw new LaunchProgramException();
                                                                }
                                                                break;
                                                            }
                                                    }
                                                }
                                                else
                                                {
                                                    if (!await LinkItem.LaunchAsync())
                                                    {
                                                        throw new LaunchProgramException();
                                                    }
                                                }
                                            }

                                            break;
                                        }
                                    case ".url":
                                        {
                                            if (File is UrlStorageFile UrlItem)
                                            {
                                                if (!await UrlItem.LaunchAsync())
                                                {
                                                    throw new LaunchProgramException();
                                                }
                                            }

                                            break;
                                        }
                                    default:
                                        {
                                            string AdminExecutablePath = SQLite.Current.GetDefaultProgramPickerRecord(File.Type);

                                            if (string.IsNullOrEmpty(AdminExecutablePath) || AdminExecutablePath == Package.Current.Id.FamilyName)
                                            {
                                                if (!TryOpenInternally(File))
                                                {
                                                    if (await File.GetStorageItemAsync() is StorageFile SFile)
                                                    {
                                                        if (!await Launcher.LaunchFileAsync(SFile))
                                                        {
                                                            using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableControllerAsync())
                                                            {
                                                                if (!await Exclusive.Controller.RunAsync(File.Path))
                                                                {
                                                                    throw new LaunchProgramException();
                                                                }
                                                            }
                                                        }
                                                    }
                                                    else
                                                    {
                                                        using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableControllerAsync())
                                                        {
                                                            if (!await Exclusive.Controller.RunAsync(File.Path))
                                                            {
                                                                throw new LaunchProgramException();
                                                            }
                                                        }
                                                    }
                                                }
                                            }
                                            else
                                            {
                                                if (Path.IsPathRooted(AdminExecutablePath))
                                                {
                                                    using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableControllerAsync())
                                                    {
                                                        if (!await Exclusive.Controller.RunAsync(AdminExecutablePath, Path.GetDirectoryName(AdminExecutablePath), Parameters: File.Path))
                                                        {
                                                            throw new LaunchProgramException();
                                                        }
                                                    }
                                                }
                                                else
                                                {
                                                    if ((await Launcher.FindFileHandlersAsync(File.Type)).FirstOrDefault((Item) => Item.PackageFamilyName == AdminExecutablePath) is AppInfo Info)
                                                    {
                                                        using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableControllerAsync())
                                                        {
                                                            if (!await Exclusive.Controller.LaunchUWPFromAUMIDAsync(Info.AppUserModelId, File.Path))
                                                            {
                                                                throw new LaunchProgramException();
                                                            }
                                                        }
                                                    }
                                                }
                                            }

                                            break;
                                        }
                                }
                            }
                            else
                            {
                                throw new FileNotFoundException();
                            }

                            break;
                        }
                    case FileSystemStorageFolder Folder:
                        {
                            if (await FileSystemStorageItemBase.CheckExistsAsync(Folder.Path))
                            {
                                await TabViewContainer.Current.CreateNewTabAsync(Folder.Path);
                                await JumpListController.Current.AddItemAsync(JumpListGroup.Recent, Folder.Path);
                            }
                            else
                            {
                                throw new DirectoryNotFoundException();
                            }

                            break;
                        }
                }
            }
            catch (FileNotFoundException)
            {
                QueueContentDialog Dialog = new QueueContentDialog
                {
                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                    Content = Globalization.GetString("QueueDialog_LocateFileFailure_Content"),
                    CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                };

                await Dialog.ShowAsync();
            }
            catch (DirectoryNotFoundException)
            {
                QueueContentDialog Dialog = new QueueContentDialog
                {
                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                    Content = Globalization.GetString("QueueDialog_LocateFolderFailure_Content"),
                    CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                };

                await Dialog.ShowAsync();
            }
            catch (LaunchProgramException)
            {
                QueueContentDialog Dialog = new QueueContentDialog
                {
                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                    Content = Globalization.GetString("QueueDialog_LaunchFailed_Content"),
                    CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                };

                await Dialog.ShowAsync();
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"An error was threw in {nameof(OpenSelectedItemAsync)}");

                QueueContentDialog dialog = new QueueContentDialog
                {
                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                    Content = Globalization.GetString("QueueDialog_LocateFolderFailure_Content"),
                    CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                };

                await dialog.ShowAsync();
            }
        }

        private async void Open_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();

            if (SearchResultList.SelectedItem is FileSystemStorageItemBase Item)
            {
                DelayDragCancellation?.Cancel();
                DelaySelectionCancellation?.Cancel();
                DelayTooltipCancellation?.Cancel();

                await OpenSelectedItemAsync(Item);
            }
        }

        private async void Copy_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();

            if (SearchResultList.SelectedItems.Count > 0)
            {
                try
                {
                    Clipboard.Clear();

                    DataPackage Package = new DataPackage
                    {
                        RequestedOperation = DataPackageOperation.Copy
                    };

                    await Package.SetStorageItemDataAsync(SearchResultList.SelectedItems.Cast<FileSystemStorageItemBase>().ToArray());

                    Clipboard.SetContent(Package);
                }
                catch (Exception ex)
                {
                    LogTracer.Log(ex, "Could not put the file into clipboard");

                    QueueContentDialog Dialog = new QueueContentDialog
                    {
                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                        Content = Globalization.GetString("QueueDialog_UnableAccessClipboard_Content"),
                        CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                    };

                    await Dialog.ShowAsync();
                }
            }
        }

        private async void Cut_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();

            if (SearchResultList.SelectedItems.Count > 0)
            {
                try
                {
                    Clipboard.Clear();

                    DataPackage Package = new DataPackage
                    {
                        RequestedOperation = DataPackageOperation.Move
                    };

                    await Package.SetStorageItemDataAsync(SearchResultList.SelectedItems.Cast<FileSystemStorageItemBase>().ToArray());

                    Clipboard.SetContent(Package);
                }
                catch (Exception ex)
                {
                    LogTracer.Log(ex, "Could not put the file into clipboard");

                    QueueContentDialog Dialog = new QueueContentDialog
                    {
                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                        Content = Globalization.GetString("QueueDialog_UnableAccessClipboard_Content"),
                        CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                    };

                    await Dialog.ShowAsync();
                }
            }
        }

        private async void Delete_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();

            if (SearchResultList.SelectedItems.Count > 0)
            {
                string[] PathList = SearchResultList.SelectedItems.Cast<FileSystemStorageItemBase>().Select((Item) => Item.Path).ToArray();

                bool ExecuteDelete = false;
                bool PermanentDelete = Window.Current.CoreWindow.GetKeyState(VirtualKey.Shift).HasFlag(CoreVirtualKeyStates.Down);

                if (ApplicationData.Current.LocalSettings.Values["DeleteConfirmSwitch"] is bool DeleteConfirm)
                {
                    if (DeleteConfirm)
                    {
                        QueueContentDialog Dialog = new QueueContentDialog
                        {
                            Title = Globalization.GetString("Common_Dialog_WarningTitle"),
                            PrimaryButtonText = Globalization.GetString("Common_Dialog_ContinueButton"),
                            CloseButtonText = Globalization.GetString("Common_Dialog_CancelButton"),
                            Content = PermanentDelete ? Globalization.GetString("QueueDialog_DeleteFilesPermanent_Content") : Globalization.GetString("QueueDialog_DeleteFiles_Content")
                        };

                        if (await Dialog.ShowAsync() == ContentDialogResult.Primary)
                        {
                            ExecuteDelete = true;
                        }
                    }
                    else
                    {
                        ExecuteDelete = true;
                    }
                }
                else
                {
                    QueueContentDialog Dialog = new QueueContentDialog
                    {
                        Title = Globalization.GetString("Common_Dialog_WarningTitle"),
                        PrimaryButtonText = Globalization.GetString("Common_Dialog_ContinueButton"),
                        CloseButtonText = Globalization.GetString("Common_Dialog_CancelButton"),
                        Content = PermanentDelete ? Globalization.GetString("QueueDialog_DeleteFilesPermanent_Content") : Globalization.GetString("QueueDialog_DeleteFiles_Content")
                    };

                    if (await Dialog.ShowAsync() == ContentDialogResult.Primary)
                    {
                        ExecuteDelete = true;
                    }
                }

                if (ApplicationData.Current.LocalSettings.Values["AvoidRecycleBin"] is bool IsAvoidRecycleBin)
                {
                    PermanentDelete |= IsAvoidRecycleBin;
                }

                if (ExecuteDelete)
                {
                    OperationListDeleteModel Model = new OperationListDeleteModel(PathList, PermanentDelete);

                    QueueTaskController.RegisterPostAction(Model, async (s, e) =>
                    {
                        EventDeferral Deferral = e.GetDeferral();

                        await Dispatcher.RunAsync(CoreDispatcherPriority.Low, async () =>
                        {
                            try
                            {
                                foreach (FilePresenter Presenter in TabViewContainer.Current.TabCollection.Select((Tab) => Tab.Content)
                                                                                                          .Cast<Frame>()
                                                                                                          .Select((Frame) => Frame.Content)
                                                                                                          .Cast<TabItemContentRenderer>()
                                                                                                          .SelectMany((Renderer) => Renderer.Presenters))
                                {
                                    if (Presenter.CurrentFolder is MTPStorageFolder MTPFolder)
                                    {
                                        foreach (string Path in PathList.Where((Path) => System.IO.Path.GetDirectoryName(Path).Equals(MTPFolder.Path, StringComparison.OrdinalIgnoreCase)))
                                        {
                                            await Presenter.AreaWatcher.InvokeRemovedEventManuallyAsync(new FileRemovedDeferredEventArgs(Path));
                                        }
                                    }
                                }
                            }
                            finally
                            {
                                Deferral.Complete();
                            }
                        });
                    });

                    QueueTaskController.EnqueueDeleteOpeartion(Model);
                }
            }
        }

        private void MultiSelect_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();

            if (SearchResultList.SelectionMode == ListViewSelectionMode.Extended)
            {
                SearchResultList.SelectionMode = ListViewSelectionMode.Multiple;
            }
            else
            {
                SearchResultList.SelectionMode = ListViewSelectionMode.Extended;
            }
        }

        private void SearchResultList_PreviewKeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.OriginalSource is not TextBox)
            {
                switch (e.Key)
                {
                    case VirtualKey.Space:
                        {
                            e.Handled = true;
                            break;
                        }
                }
            }
        }

        private async void SearchResultList_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (!SettingPage.IsDoubleClickEnabled && e.ClickedItem is FileSystemStorageItemBase Item)
            {
                DelayDragCancellation?.Cancel();
                DelaySelectionCancellation?.Cancel();
                DelayTooltipCancellation?.Cancel();

                await OpenSelectedItemAsync(Item);
            }
        }

        private async void SearchResultList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (SettingPage.IsQuicklookEnabled
                && !SettingPage.IsOpened
                && e.AddedItems.Count == 1
                && e.AddedItems.First() is FileSystemStorageItemBase Item)
            {
                try
                {
                    using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableControllerAsync())
                    {
                        if (await Exclusive.Controller.CheckIfQuicklookIsAvaliableAsync())
                        {
                            if (!string.IsNullOrEmpty(Item.Path))
                            {
                                await Exclusive.Controller.SwitchQuicklookAsync(Item.Path);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogTracer.Log(ex, $"An exception was threw in {nameof(SearchResultList_SelectionChanged)}");
                }
            }
        }

        private async void SearchResultList_ContextRequested(UIElement sender, ContextRequestedEventArgs args)
        {
            args.Handled = true;

            if ((args.OriginalSource as FrameworkElement)?.DataContext is FileSystemStorageItemBase Context)
            {
                if (args.TryGetPosition(sender, out Point Position))
                {
                    if (!SettingPage.IsDoubleClickEnabled)
                    {
                        DelaySelectionCancellation?.Cancel();
                    }

                    ContextMenuCancellation?.Cancel();
                    ContextMenuCancellation?.Dispose();
                    ContextMenuCancellation = new CancellationTokenSource();

                    if (SearchResultList.SelectedItems.Count > 1 && SearchResultList.SelectedItems.Contains(Context))
                    {
                        string[] SelectedItemPaths = SearchResultList.SelectedItems.Cast<FileSystemStorageItemBase>().Select((Item) => Item.Path).ToArray();

                        if (SelectedItemPaths.Skip(1).All((Item) => Path.GetDirectoryName(Item).Equals(Path.GetDirectoryName(SelectedItemPaths[0]), StringComparison.OrdinalIgnoreCase)))
                        {
                            await MixCommandFlyout.ShowCommandBarFlyoutWithExtraContextMenuItems(SearchResultList,
                                                                                                 Position,
                                                                                                 ContextMenuCancellation.Token,
                                                                                                 SelectedItemPaths);
                        }
                        else
                        {
                            DefaultCommandFlyout.ShowAt(SearchResultList, new FlyoutShowOptions
                            {
                                Position = Position,
                                Placement = FlyoutPlacementMode.BottomEdgeAlignedLeft,
                                ShowMode = FlyoutShowMode.Standard
                            });
                        }
                    }
                    else
                    {
                        SearchResultList.SelectedItem = Context;

                        await SingleCommandFlyout.ShowCommandBarFlyoutWithExtraContextMenuItems(SearchResultList,
                                                                                                Position,
                                                                                                ContextMenuCancellation.Token,
                                                                                                Context.Path);
                    }
                }
            }
        }

        private void SearchResultList_ContextCanceled(UIElement sender, RoutedEventArgs args)
        {
            CloseAllFlyout();
        }

        private async void Rename_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();

            IReadOnlyList<FileSystemStorageItemBase> SelectedItemsCopy = SearchResultList.SelectedItems.Cast<FileSystemStorageItemBase>().ToList();

            if (SelectedItemsCopy.Count > 0)
            {
                RenameDialog Dialog = new RenameDialog(SelectedItemsCopy);

                if ((await Dialog.ShowAsync()) == ContentDialogResult.Primary)
                {
                    if (SelectedItemsCopy.Count == 1)
                    {
                        string OriginName = SelectedItemsCopy.First().Name;
                        string NewName = Dialog.DesireNameMap[OriginName];
                        string FolderPath = Path.GetDirectoryName(SelectedItemsCopy.First().Path);

                        if (OriginName != NewName)
                        {
                            if (!OriginName.Equals(NewName, StringComparison.OrdinalIgnoreCase)
                            && await FileSystemStorageItemBase.CheckExistsAsync(Path.Combine(FolderPath, NewName)))
                            {
                                QueueContentDialog Dialog1 = new QueueContentDialog
                                {
                                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                    Content = Globalization.GetString("QueueDialog_RenameExist_Content"),
                                    PrimaryButtonText = Globalization.GetString("Common_Dialog_ContinueButton"),
                                    CloseButtonText = Globalization.GetString("Common_Dialog_CancelButton")
                                };

                                if (await Dialog1.ShowAsync() != ContentDialogResult.Primary)
                                {
                                    return;
                                }
                            }

                            OperationListRenameModel Model = new OperationListRenameModel(SelectedItemsCopy.First().Path, Path.Combine(FolderPath, NewName));

                            QueueTaskController.RegisterPostAction(Model, async (s, e) =>
                            {
                                EventDeferral Deferral = e.GetDeferral();

                                await Dispatcher.RunAsync(CoreDispatcherPriority.Low, async () =>
                                {
                                    try
                                    {
                                        if (e.Status == OperationStatus.Completed && e.Parameter is string NewName)
                                        {
                                            if (await FileSystemStorageItemBase.OpenAsync(Path.Combine(FolderPath, NewName)) is FileSystemStorageItemBase NewItem)
                                            {
                                                if (SearchResult.FirstOrDefault((Item) => Item.Name == OriginName) is FileSystemStorageItemBase OldItem)
                                                {
                                                    int Index = SearchResult.IndexOf(OldItem);
                                                    SearchResult.Remove(OldItem);
                                                    SearchResult.Insert(Index, NewItem);
                                                }
                                            }

                                            foreach (FilePresenter Presenter in TabViewContainer.Current.TabCollection.Select((Tab) => Tab.Content)
                                                                                                                      .Cast<Frame>()
                                                                                                                      .Select((Frame) => Frame.Content)
                                                                                                                      .Cast<TabItemContentRenderer>()
                                                                                                                      .SelectMany((Renderer) => Renderer.Presenters))
                                            {
                                                if (Presenter.CurrentFolder is MTPStorageFolder MTPFolder && MTPFolder.Path.Equals(FolderPath, StringComparison.OrdinalIgnoreCase))
                                                {
                                                    await Presenter.AreaWatcher.InvokeRenamedEventManuallyAsync(new FileRenamedDeferredEventArgs(SelectedItemsCopy.First().Path, NewName));
                                                }
                                            }
                                        }
                                    }
                                    finally
                                    {
                                        Deferral.Complete();
                                    }
                                });
                            });

                            QueueTaskController.EnqueueRenameOpeartion(Model);
                        }
                    }
                    else
                    {
                        foreach (FileSystemStorageItemBase OriginItem in SelectedItemsCopy.Where((Item)=>Item.Name != Dialog.DesireNameMap[Item.Name]))
                        {
                            string FolderPath = Path.GetDirectoryName(OriginItem.Path);

                            OperationListRenameModel Model = new OperationListRenameModel(OriginItem.Path, Path.Combine(FolderPath, Dialog.DesireNameMap[OriginItem.Name]));

                            QueueTaskController.RegisterPostAction(Model, async (s, e) =>
                            {
                                EventDeferral Deferral = e.GetDeferral();

                                await Dispatcher.RunAsync(CoreDispatcherPriority.Low, async () =>
                                {
                                    try
                                    {
                                        if (e.Status == OperationStatus.Completed && e.Parameter is string NewName)
                                        {
                                            if (await FileSystemStorageItemBase.OpenAsync(Path.Combine(FolderPath, NewName)) is FileSystemStorageItemBase NewItem)
                                            {
                                                if (SearchResult.FirstOrDefault((Item) => Item.Name == OriginItem.Name) is FileSystemStorageItemBase OldItem)
                                                {
                                                    int Index = SearchResult.IndexOf(OldItem);
                                                    SearchResult.Remove(OldItem);
                                                    SearchResult.Insert(Index, NewItem);
                                                }
                                            }

                                            foreach (FilePresenter Presenter in TabViewContainer.Current.TabCollection.Select((Tab) => Tab.Content)
                                                                                                                      .Cast<Frame>()
                                                                                                                      .Select((Frame) => Frame.Content)
                                                                                                                      .Cast<TabItemContentRenderer>()
                                                                                                                      .SelectMany((Renderer) => Renderer.Presenters))
                                            {
                                                if (Presenter.CurrentFolder is MTPStorageFolder MTPFolder && MTPFolder.Path.Equals(FolderPath, StringComparison.OrdinalIgnoreCase))
                                                {
                                                    await Presenter.AreaWatcher.InvokeRenamedEventManuallyAsync(new FileRenamedDeferredEventArgs(OriginItem.Path, NewName));
                                                }
                                            }
                                        }
                                    }
                                    finally
                                    {
                                        Deferral.Complete();
                                    }
                                });
                            });

                            QueueTaskController.EnqueueRenameOpeartion(Model);
                        }
                    }
                }
            }
        }
    }
}
