using RX_Explorer.Class;
using RX_Explorer.SeparateWindow.PropertyWindow;
using ShareClassLibrary;
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

        private ListViewBaseSelectionExtension SelectionExtension;
        private readonly PointerEventHandler PointerPressedEventHandler;
        private readonly PointerEventHandler PointerReleasedEventHandler;
        private readonly ListViewHeaderController ListViewDetailHeader = new ListViewHeaderController();
        private readonly ObservableCollection<FileSystemStorageItemBase> SearchResult = new ObservableCollection<FileSystemStorageItemBase>();
        private bool BlockKeyboardShortCutInput;

        private SortTarget STarget;
        private SortDirection SDirection;

        private DateTimeOffset LastPressTime;
        private string LastPressString;

        public SearchPage()
        {
            InitializeComponent();

            ListViewDetailHeader.Filter.RefreshListRequested += Filter_RefreshListRequested;

            PointerPressedEventHandler = new PointerEventHandler(ViewControl_PointerPressed);
            PointerReleasedEventHandler = new PointerEventHandler(ViewControl_PointerReleased);
        }

        private void Filter_RefreshListRequested(object sender, RefreshRequestedEventArgs e)
        {
            SearchResult.Clear();

            foreach (FileSystemStorageItemBase Item in SortCollectionGenerator.GetSortedCollection(e.FilterCollection, STarget, SDirection))
            {
                SearchResult.Add(Item);
            }
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            if (e.Parameter is SearchOptions Parameters)
            {
                CoreApplication.MainView.CoreWindow.KeyDown += SearchPage_KeyDown;

                SearchResultList.AddHandler(PointerPressedEvent, PointerPressedEventHandler, true);
                SearchResultList.AddHandler(PointerReleasedEvent, PointerReleasedEventHandler, true);

                SelectionExtension = new ListViewBaseSelectionExtension(SearchResultList, DrawRectangle);

                if (e.NavigationMode == NavigationMode.New)
                {
                    await SearchAsync(Parameters).ConfigureAwait(false);
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

        private async Task SearchAsync(SearchOptions Options)
        {
            STarget = SortTarget.Name;
            SDirection = SortDirection.Ascending;
            HasItem.Visibility = Visibility.Collapsed;

            if (SettingPage.IsSearchHistoryEnabled)
            {
                SQLite.Current.SetSearchHistory(Options.SearchText);
            }

            CancellationTokenSource SearchCancellation = new CancellationTokenSource();

            try
            {
                this.SearchCancellation = SearchCancellation;

                SearchStatus.Text = $"{Globalization.GetString("SearchProcessingText")} \"{Options.SearchText}\"";
                SearchStatusBar.Visibility = Visibility.Visible;

                switch (Options.EngineCategory)
                {
                    case SearchCategory.BuiltInEngine:
                        {
                            IReadOnlyList<FileSystemStorageItemBase> Result = await Options.SearchFolder.SearchAsync(Options.SearchText,
                                                                                                                     Options.DeepSearch,
                                                                                                                     SettingPage.IsShowHiddenFilesEnabled,
                                                                                                                     SettingPage.IsDisplayProtectedSystemItems,
                                                                                                                     Options.UseRegexExpression,
                                                                                                                     Options.UseAQSExpression,
                                                                                                                     Options.UseIndexerOnly,
                                                                                                                     Options.IgnoreCase,
                                                                                                                     SearchCancellation.Token);

                            if (Result.Count == 0)
                            {
                                HasItem.Visibility = Visibility.Visible;
                            }
                            else
                            {
                                foreach (FileSystemStorageItemBase Item in SortCollectionGenerator.GetSortedCollection(Result, SortTarget.Name, SortDirection.Ascending))
                                {
                                    if (SearchCancellation.IsCancellationRequested)
                                    {
                                        HasItem.Visibility = Visibility.Visible;
                                        break;
                                    }
                                    else
                                    {
                                        SearchResult.Add(Item);
                                    }
                                }
                            }

                            break;
                        }
                    case SearchCategory.EverythingEngine:
                        {
                            using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableControllerAsync())
                            {
                                IReadOnlyList<FileSystemStorageItemBase> SearchItems = await Exclusive.Controller.SearchByEverythingAsync(Options.DeepSearch ? string.Empty : Options.SearchFolder.Path,
                                                                                                                                          Options.SearchText,
                                                                                                                                          Options.UseRegexExpression,
                                                                                                                                          Options.IgnoreCase);

                                if (SearchItems.Count == 0)
                                {
                                    HasItem.Visibility = Visibility.Visible;
                                }
                                else
                                {
                                    SearchResult.AddRange(SortCollectionGenerator.GetSortedCollection(SearchItems, SortTarget.Name, SortDirection.Ascending));
                                }
                            }

                            break;
                        }
                }

                await ListViewDetailHeader.Filter.SetDataSourceAsync(SearchResult);

                SearchStatusBar.Visibility = Visibility.Collapsed;
                SearchStatus.Text = $"{Globalization.GetString("SearchCompletedText")} ({SearchResult.Count} {Globalization.GetString("Items_Description")})";
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"An error was threw in {nameof(SearchAsync)}");
            }
            finally
            {
                SearchCancellation.Dispose();

                if (this.SearchCancellation == SearchCancellation)
                {
                    this.SearchCancellation = null;
                }
            }
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            CoreApplication.MainView.CoreWindow.KeyDown -= SearchPage_KeyDown;

            SearchResultList.RemoveHandler(PointerPressedEvent, PointerPressedEventHandler);
            SearchResultList.RemoveHandler(PointerReleasedEvent, PointerReleasedEventHandler);

            SearchCancellation?.Cancel();
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
            if (SearchResultList.SelectedItem is FileSystemStorageItemBase Item)
            {
                PropertiesWindowBase NewWindow = await PropertiesWindowBase.CreateAsync(Item);
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
                                TooltipFlyoutText.Text = await Exclusive.Controller.GetTooltipTextAsync(Item.Path);

                                if (!string.IsNullOrWhiteSpace(TooltipFlyoutText.Text)
                                    && !Token.IsCancellationRequested)
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

        private void ListHeader_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button Btn)
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

                FileSystemStorageItemBase[] SortResult = SortCollectionGenerator.GetSortedCollection(SearchResult, STarget, SDirection).ToArray();

                SearchResult.Clear();

                foreach (FileSystemStorageItemBase Item in SortResult)
                {
                    SearchResult.Add(Item);
                }
            }
        }

        private bool TryOpenInternally(FileSystemStorageFile File)
        {
            Type InternalType = File.Type.ToLower() switch
            {
                ".jpg" or ".png" or ".bmp" => typeof(PhotoViewer),
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

                    await LaunchSelectedItemAsync(Item);
                }
            }
        }

        private async Task LaunchSelectedItemAsync(FileSystemStorageItemBase Item)
        {
            try
            {
                switch (Item)
                {
                    case FileSystemStorageFile File:
                        {
                            if (!await FileSystemStorageItemBase.CheckExistsAsync(File.Path))
                            {
                                QueueContentDialog Dialog = new QueueContentDialog
                                {
                                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                    Content = Globalization.GetString("QueueDialog_LocateFileFailure_Content"),
                                    CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                                };

                                await Dialog.ShowAsync();

                                return;
                            }

                            switch (File.Type.ToLower())
                            {
                                case ".exe":
                                case ".bat":
                                case ".msi":
                                    {
                                        using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableControllerAsync())
                                        {
                                            if (!await Exclusive.Controller.RunAsync(File.Path, Path.GetDirectoryName(File.Path)))
                                            {
                                                QueueContentDialog Dialog = new QueueContentDialog
                                                {
                                                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                                    Content = Globalization.GetString("QueueDialog_LaunchFailed_Content"),
                                                    CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                                                };

                                                await Dialog.ShowAsync();
                                            }
                                        }

                                        break;
                                    }
                                case ".msc":
                                    {
                                        using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableControllerAsync())
                                        {
                                            if (!await Exclusive.Controller.RunAsync("powershell.exe", string.Empty, WindowState.Normal, false, true, false, "-Command", File.Path))
                                            {
                                                QueueContentDialog Dialog = new QueueContentDialog
                                                {
                                                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                                    Content = Globalization.GetString("QueueDialog_LaunchFailed_Content"),
                                                    CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                                                };

                                                await Dialog.ShowAsync();
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
                                                                QueueContentDialog Dialog = new QueueContentDialog
                                                                {
                                                                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                                                    Content = Globalization.GetString("QueueDialog_LaunchFailed_Content"),
                                                                    CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                                                                };

                                                                await Dialog.ShowAsync();
                                                            }
                                                            break;
                                                        }
                                                }
                                            }
                                            else
                                            {
                                                if (!await LinkItem.LaunchAsync())
                                                {
                                                    QueueContentDialog Dialog = new QueueContentDialog
                                                    {
                                                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                                        Content = Globalization.GetString("QueueDialog_LaunchFailed_Content"),
                                                        CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                                                    };

                                                    await Dialog.ShowAsync();
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
                                                QueueContentDialog Dialog = new QueueContentDialog
                                                {
                                                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                                    Content = Globalization.GetString("QueueDialog_LaunchFailed_Content"),
                                                    CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                                                };

                                                await Dialog.ShowAsync();
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
                                                                QueueContentDialog Dialog = new QueueContentDialog
                                                                {
                                                                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                                                    Content = Globalization.GetString("QueueDialog_LaunchFailed_Content"),
                                                                    CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                                                                };

                                                                await Dialog.ShowAsync();
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
                                                            QueueContentDialog Dialog = new QueueContentDialog
                                                            {
                                                                Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                                                Content = Globalization.GetString("QueueDialog_LaunchFailed_Content"),
                                                                CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                                                            };

                                                            await Dialog.ShowAsync();
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
                                                        QueueContentDialog Dialog = new QueueContentDialog
                                                        {
                                                            Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                                            Content = Globalization.GetString("QueueDialog_LaunchFailed_Content"),
                                                            CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                                                        };

                                                        await Dialog.ShowAsync();
                                                    }
                                                }
                                            }
                                            else
                                            {
                                                if ((await Launcher.FindFileHandlersAsync(File.Type)).FirstOrDefault((Item) => Item.PackageFamilyName == AdminExecutablePath) is AppInfo Info)
                                                {
                                                    if (await File.GetStorageItemAsync() is StorageFile InnerFile)
                                                    {
                                                        if (!await Launcher.LaunchFileAsync(InnerFile, new LauncherOptions { TargetApplicationPackageFamilyName = Info.PackageFamilyName, DisplayApplicationPicker = false }))
                                                        {
                                                            using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableControllerAsync())
                                                            {
                                                                if (!await Exclusive.Controller.LaunchUWPFromAUMIDAsync(Info.AppUserModelId, File.Path))
                                                                {
                                                                    QueueContentDialog Dialog = new QueueContentDialog
                                                                    {
                                                                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                                                        Content = Globalization.GetString("QueueDialog_LaunchFailed_Content"),
                                                                        CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                                                                    };

                                                                    await Dialog.ShowAsync();
                                                                }
                                                            }
                                                        }
                                                    }
                                                    else
                                                    {
                                                        using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableControllerAsync())
                                                        {
                                                            if (!await Exclusive.Controller.LaunchUWPFromAUMIDAsync(Info.AppUserModelId, File.Path))
                                                            {
                                                                QueueContentDialog Dialog = new QueueContentDialog
                                                                {
                                                                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                                                    Content = Globalization.GetString("QueueDialog_UnableAccessFile_Content"),
                                                                    CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                                                                };

                                                                await Dialog.ShowAsync();
                                                            }
                                                        }
                                                    }
                                                }
                                            }
                                        }

                                        break;
                                    }
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
                                QueueContentDialog Dialog = new QueueContentDialog
                                {
                                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                    Content = Globalization.GetString("QueueDialog_LocateFolderFailure_Content"),
                                    CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                                };

                                await Dialog.ShowAsync();
                            }

                            break;
                        }
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"An error was threw in {nameof(LaunchSelectedItemAsync)}");

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

                await LaunchSelectedItemAsync(Item);
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
                    QueueTaskController.EnqueueDeleteOpeartion(PathList, PermanentDelete);
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

                await LaunchSelectedItemAsync(Item);
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

        private void SearchResultList_ContextRequested(UIElement sender, ContextRequestedEventArgs args)
        {
            if (args.TryGetPosition(sender, out Point Position))
            {
                args.Handled = true;

                if (!SettingPage.IsDoubleClickEnabled)
                {
                    DelaySelectionCancellation?.Cancel();
                }

                if ((args.OriginalSource as FrameworkElement)?.DataContext is FileSystemStorageItemBase Context)
                {
                    if (SearchResultList.SelectedItems.Count > 1 && SearchResultList.SelectedItems.Contains(Context))
                    {
                        MixCommandFlyout.ShowAt(SearchResultList, new FlyoutShowOptions
                        {
                            Position = Position,
                            Placement = FlyoutPlacementMode.BottomEdgeAlignedLeft,
                            ShowMode = FlyoutShowMode.Standard
                        });
                    }
                    else
                    {
                        SearchResultList.SelectedItem = Context;

                        SingleCommandFlyout.ShowAt(SearchResultList, new FlyoutShowOptions
                        {
                            Position = Position,
                            Placement = FlyoutPlacementMode.BottomEdgeAlignedLeft,
                            ShowMode = FlyoutShowMode.Standard
                        });
                    }
                }
            }
        }

        private void SearchResultList_ContextCanceled(UIElement sender, RoutedEventArgs args)
        {
            CloseAllFlyout();
        }
    }
}
