﻿using ComputerVision;
using Microsoft.Toolkit.Deferred;
using Microsoft.Toolkit.Uwp.UI.Controls;
using Nito.AsyncEx;
using RX_Explorer.Class;
using RX_Explorer.Dialog;
using RX_Explorer.Interface;
using RX_Explorer.SeparateWindow.PropertyWindow;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using TinyPinyin;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Core;
using Windows.ApplicationModel.DataTransfer;
using Windows.Devices.Input;
using Windows.Foundation;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.System;
using Windows.UI.Core;
using Windows.UI.Input;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;
using Package = Windows.ApplicationModel.Package;
using RefreshRequestedEventArgs = RX_Explorer.Class.RefreshRequestedEventArgs;

namespace RX_Explorer.View
{
    public sealed partial class SearchPage : Page
    {
        private string LastPressString;
        private bool BlockKeyboardShortCutInput;
        private DateTimeOffset LastPressTime;

        private CancellationTokenSource SearchCancellation;
        private CancellationTokenSource DelayDragCancellation;
        private CancellationTokenSource DelaySelectionCancellation;
        private CancellationTokenSource DelayTooltipCancellation;
        private CancellationTokenSource ContextMenuCancellation;

        private ListViewBaseSelectionExtension SelectionExtension;
        private readonly PointerEventHandler PointerPressedEventHandler;
        private readonly PointerEventHandler PointerReleasedEventHandler;
        private readonly ObservableCollection<FileSystemStorageItemBase> SearchResult = new ObservableCollection<FileSystemStorageItemBase>();
        private readonly SignalContext SignalControl = new SignalContext();
        private readonly ListViewColumnWidthSaver ColumnWidthSaver = new ListViewColumnWidthSaver(ListViewLocation.Search);
        private readonly InterlockedNoReentryExecution HeaderClickExecution = new InterlockedNoReentryExecution();
        private readonly FilterController ListViewHeaderFilter = new FilterController();
        private readonly SortIndicatorController ListViewHeaderSortIndicator = new SortIndicatorController();
        private readonly AsyncLock KeyboardFindLocationLocker = new AsyncLock();

        public SearchPage()
        {
            InitializeComponent();

            ListViewHeaderFilter.RefreshListRequested += Filter_RefreshListRequested;

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

            foreach (FileSystemStorageItemBase Item in await SortedCollectionGenerator.GetSortedCollectionAsync(e.FilterCollection, ListViewHeaderSortIndicator.Target, ListViewHeaderSortIndicator.Direction, SortStyle.None))
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
                    bool CtrlDown = sender.GetKeyState(VirtualKey.Control).HasFlag(CoreVirtualKeyStates.Down);
                    bool ShiftDown = sender.GetKeyState(VirtualKey.Shift).HasFlag(CoreVirtualKeyStates.Down);

                    switch (args.VirtualKey)
                    {
                        case VirtualKey.Enter:
                            {
                                Open_Click(null, null);
                                break;
                            }
                        case VirtualKey.L when CtrlDown:
                            {
                                Location_Click(null, null);
                                break;
                            }
                        case VirtualKey.A when CtrlDown:
                            {
                                SearchResultList.SelectAll();
                                break;
                            }
                        case VirtualKey.C when CtrlDown:
                            {
                                Copy_Click(null, null);
                                break;
                            }
                        case VirtualKey.X when CtrlDown:
                            {
                                Cut_Click(null, null);
                                break;
                            }
                        case VirtualKey.Delete:
                        case VirtualKey.D when CtrlDown:
                            {
                                Delete_Click(null, null);
                                break;
                            }
                        case VirtualKey.Space when !SettingPage.IsOpened
                                                   && SearchResultList.SelectedItems.Count == 1:
                            {
                                if (SettingPage.IsQuicklookEnabled)
                                {
                                    using (AuxiliaryTrustProcessController.Exclusive Exclusive = await AuxiliaryTrustProcessController.GetControllerExclusiveAsync(Priority: PriorityLevel.High))
                                    {
                                        if (await Exclusive.Controller.CheckQuicklookAvailableAsync())
                                        {
                                            if (SearchResultList.SelectedItem is FileSystemStorageItemBase Item)
                                            {
                                                await Exclusive.Controller.ToggleQuicklookWindowAsync(Item.Path);
                                            }
                                        }
                                    }
                                }
                                else if (SettingPage.IsSeerEnabled)
                                {
                                    using (AuxiliaryTrustProcessController.Exclusive Exclusive = await AuxiliaryTrustProcessController.GetControllerExclusiveAsync(Priority: PriorityLevel.High))
                                    {
                                        if (await Exclusive.Controller.CheckSeerAvailableAsync())
                                        {
                                            if (SearchResultList.SelectedItem is FileSystemStorageItemBase Item)
                                            {
                                                await Exclusive.Controller.ToggleSeerWindowAsync(Item.Path);
                                            }
                                        }
                                    }
                                }

                                break;
                            }
                        default:
                            {
                                if (!CtrlDown && !ShiftDown)
                                {
                                    if (Regex.IsMatch(((char)args.VirtualKey).ToString(), @"[A-Z0-9]", RegexOptions.IgnoreCase))
                                    {
                                        args.Handled = true;

                                        using (await KeyboardFindLocationLocker.LockAsync())
                                        {
                                            string NewKey = Convert.ToChar(args.VirtualKey).ToString();

                                            try
                                            {
                                                if (LastPressString != NewKey && (DateTimeOffset.Now - LastPressTime).TotalMilliseconds < 1200)
                                                {
                                                    try
                                                    {
                                                        IReadOnlyList<FileSystemStorageItemBase> Group = SearchResult.Where((Item) => (Regex.IsMatch(Item.DisplayName, "[\\u3400-\\u4db5\\u4e00-\\u9fd5]") ? PinyinHelper.GetPinyin(Item.DisplayName, string.Empty) : Item.DisplayName).StartsWith(LastPressString + NewKey, StringComparison.OrdinalIgnoreCase)).ToArray();

                                                        if (Group.Count > 0 && !Group.Contains(SearchResultList.SelectedItem))
                                                        {
                                                            await SearchResultList.SelectAndScrollIntoViewSmoothlyAsync(Group[0]);
                                                        }
                                                    }
                                                    finally
                                                    {
                                                        LastPressString += NewKey;
                                                    }
                                                }
                                                else
                                                {
                                                    try
                                                    {
                                                        IReadOnlyList<FileSystemStorageItemBase> GroupItems = SearchResult.Where((Item) => (Regex.IsMatch(Item.DisplayName, "[\\u3400-\\u4db5\\u4e00-\\u9fd5]") ? PinyinHelper.GetPinyin(Item.DisplayName, string.Empty) : Item.DisplayName).StartsWith(NewKey, StringComparison.OrdinalIgnoreCase)).ToArray();

                                                        if (GroupItems.Count > 0)
                                                        {
                                                            await SearchResultList.SelectAndScrollIntoViewSmoothlyAsync(GroupItems[(GroupItems.FindIndex(SearchResultList.SelectedItem) + 1) % GroupItems.Count]);
                                                        }
                                                    }
                                                    finally
                                                    {
                                                        LastPressString = NewKey;
                                                    }
                                                }
                                            }
                                            finally
                                            {
                                                LastPressTime = DateTimeOffset.Now;
                                            }
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

        private async Task SearchAsync(SearchOptions Options, CancellationToken CancelToken = default)
        {
            RootGrid.DataContext = Options;
            ListViewHeaderSortIndicator.Target = SortTarget.Name;
            ListViewHeaderSortIndicator.Direction = SortDirection.Ascending;
            SearchStatus.Text = $"{Globalization.GetString("SearchProcessingText")} \"{Options.SearchText}\"";
            SearchStatusBar.Visibility = Visibility.Visible;
            HasItem.Visibility = Visibility.Collapsed;
            SearchStatus.Visibility = Visibility.Visible;

            if (SettingPage.IsSearchHistoryEnabled)
            {
                SQLite.Current.SetSearchHistory(Options.SearchText);
            }

            try
            {
                switch (Options.EngineCategory)
                {
                    case SearchCategory.BuiltInEngine:
                        {
                            await foreach (FileSystemStorageItemBase Item in Options.SearchFolder.SearchAsync(Options.SearchText,
                                                                                                              Options.DeepSearch,
                                                                                                              SettingPage.IsDisplayHiddenItemsEnabled,
                                                                                                              SettingPage.IsDisplayProtectedSystemItemsEnabled,
                                                                                                              Options.UseRegexExpression,
                                                                                                              Options.UseAQSExpression,
                                                                                                              Options.UseIndexerOnly,
                                                                                                              Options.IgnoreCase,
                                                                                                              CancelToken))
                            {
                                await SignalControl.TrapOnSignalAsync();

                                SearchResult.Insert(await SortedCollectionGenerator.SearchInsertLocationAsync(SearchResult, Item, ListViewHeaderSortIndicator.Target, ListViewHeaderSortIndicator.Direction, SortStyle.None), Item);

                                if (SearchResult.Count % 25 == 0)
                                {
                                    await ListViewHeaderFilter.SetDataSourceAsync(SearchResult);
                                }
                            }

                            break;
                        }
                    case SearchCategory.EverythingEngine:
                        {
                            IReadOnlyList<string> SearchItems;

                            using (AuxiliaryTrustProcessController.Exclusive Exclusive = await AuxiliaryTrustProcessController.GetControllerExclusiveAsync())
                            {
                                SearchItems = await Exclusive.Controller.SearchByEverythingAsync(Options.DeepSearch ? string.Empty : Options.SearchFolder.Path,
                                                                                                 Options.SearchText,
                                                                                                 Options.UseRegexExpression,
                                                                                                 Options.IgnoreCase);
                            }

                            await foreach (FileSystemStorageItemBase Item in FileSystemStorageItemBase.OpenInBatchAsync(SearchItems, CancelToken).OfType<FileSystemStorageItemBase>())
                            {
                                await SignalControl.TrapOnSignalAsync();

                                SearchResult.Insert(await SortedCollectionGenerator.SearchInsertLocationAsync(SearchResult, Item, ListViewHeaderSortIndicator.Target, ListViewHeaderSortIndicator.Direction, SortStyle.None), Item);

                                if (SearchResult.Count % 10 == 0)
                                {
                                    await ListViewHeaderFilter.SetDataSourceAsync(SearchResult);
                                }
                            }

                            break;
                        }
                }

                await ListViewHeaderFilter.SetDataSourceAsync(SearchResult);
            }
            catch (OperationCanceledException)
            {
                SearchResult.Clear();
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"An exception was threw in {nameof(SearchAsync)}");
            }
            finally
            {
                SignalControl.MarkAsCompleted();
                SearchStatusBar.Visibility = Visibility.Collapsed;
                SearchStatus.Text = $"{Globalization.GetString("SearchCompletedText")} ({SearchResult.Count} {Globalization.GetString("Items_Description")})";

                if (SearchResult.Count == 0)
                {
                    HasItem.Visibility = Visibility.Visible;
                }
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
                SearchStatus.Visibility = Visibility.Collapsed;
            }
        }

        private void ViewControl_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            Window.Current.CoreWindow.PointerCursor = new CoreCursor(CoreCursorType.Arrow, 0);

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
                else if (Element.FindParentOfType<GridSplitter>() is not null || Element.FindParentOfType<Button>() is not null)
                {
                    SearchResultList.SelectedItem = null;
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
                    await TabViewContainer.Current.CreateNewTabAsync(Path.GetDirectoryName(Item.Path));

                    for (int Retry = 0; Retry < 3; Retry++)
                    {
                        await Task.Delay(500);

                        if (TabViewContainer.Current.CurrentTabRenderer.Presenters.SingleOrDefault() is FilePresenter Presenter)
                        {
                            if (Presenter.FileCollection.FirstOrDefault((SItem) => SItem == Item) is FileSystemStorageItemBase Target)
                            {
                                Presenter.SelectedItem = Target;
                                Presenter.ItemPresenter.ScrollIntoView(Target, ScrollIntoViewAlignment.Leading);
                                break;
                            }
                        }
                    }

                    switch (SearchResultList.SelectedItem)
                    {
                        case FileSystemStorageFile:
                            {
                                await JumpListController.AddItemAsync(JumpListGroup.Recent, Path.GetDirectoryName(Item.Path));
                                break;
                            }
                        case FileSystemStorageFolder:
                            {
                                await JumpListController.AddItemAsync(JumpListGroup.Recent, Item.Path);
                                break;
                            }
                    }
                }
                catch (Exception ex)
                {
                    LogTracer.Log(ex, $"An exception was threw in {nameof(Location_Click)}");

                    await new CommonContentDialog
                    {
                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                        Content = Globalization.GetString("QueueDialog_LocateFolderFailure_Content"),
                        CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                    }.ShowAsync();
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
                    CommonContentDialog Dialog = new CommonContentDialog
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

                if (Item is not INotWin32StorageItem)
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
                                PointerPoint Point = e.GetCurrentPoint(SearchResultList);

                                using (AuxiliaryTrustProcessController.Exclusive Exclusive = await AuxiliaryTrustProcessController.GetControllerExclusiveAsync(Priority: PriorityLevel.High))
                                {
                                    string Tooltip = await Exclusive.Controller.GetTooltipTextAsync(Item.Path, Token);

                                    if (!MixCommandFlyout.IsOpen
                                        && !SingleCommandFlyout.IsOpen
                                        && !Token.IsCancellationRequested
                                        && !string.IsNullOrWhiteSpace(Tooltip)
                                        && !QueueContentDialog.IsRunningOrWaiting)
                                    {
                                        TooltipFlyout.Hide();
                                        TooltipFlyoutText.Text = Tooltip;
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
                switch (SettingPage.DefaultDragBehaivor)
                {
                    case DragBehaivor.Copy:
                        {
                            args.AllowedOperations = DataPackageOperation.Copy;
                            break;
                        }
                    case DragBehaivor.Move:
                        {
                            args.AllowedOperations = DataPackageOperation.Move;
                            break;
                        }
                    default:
                        {
                            args.AllowedOperations = DataPackageOperation.Copy | DataPackageOperation.Move | DataPackageOperation.Link;
                            break;
                        }
                }

                await args.Data.SetStorageItemDataAsync(SearchResultList.SelectedItems.Cast<FileSystemStorageItemBase>().ToArray());

                if (SearchResultList.SelectedItems.Count() > 1)
                {
                    if (SearchResultList.SelectedItems.OfType<INotWin32StorageItem>().Any())
                    {
                        Uri DefaultThumbnailUri = new Uri(AppThemeController.Current.Theme == ElementTheme.Dark
                                                            ? "ms-appx:///Assets/MultiItems_White.png"
                                                            : "ms-appx:///Assets/MultiItems_Black.png");

                        BitmapImage DefaultThumbnailImage = new BitmapImage(DefaultThumbnailUri)
                        {
                            DecodePixelHeight = 80,
                            DecodePixelWidth = 80,
                            DecodePixelType = DecodePixelType.Logical
                        };

                        args.DragUI.SetContentFromBitmapImage(DefaultThumbnailImage);
                    }
                    else
                    {
                        DataPackageView View = args.Data.GetView();

                        if (View.Contains(StandardDataFormats.StorageItems))
                        {
                            args.DragUI.SetContentFromDataPackage();
                        }
                        else
                        {
                            Uri DefaultThumbnailUri = new Uri(AppThemeController.Current.Theme == ElementTheme.Dark
                                                            ? "ms-appx:///Assets/MultiItems_White.png"
                                                            : "ms-appx:///Assets/MultiItems_Black.png");

                            BitmapImage DefaultThumbnailImage = new BitmapImage(DefaultThumbnailUri)
                            {
                                DecodePixelHeight = 80,
                                DecodePixelWidth = 80,
                                DecodePixelType = DecodePixelType.Logical
                            };

                            args.DragUI.SetContentFromBitmapImage(DefaultThumbnailImage);
                        }
                    }
                }
                else
                {
                    switch (SearchResultList.SelectedItems.SingleOrDefault())
                    {
                        case INotWin32StorageItem:
                            {
                                Uri DefaultThumbnailUri = new Uri(AppThemeController.Current.Theme == ElementTheme.Dark
                                                                    ? "ms-appx:///Assets/SingleItem_White.png"
                                                                    : "ms-appx:///Assets/SingleItem_Black.png");

                                BitmapImage DefaultThumbnailImage = new BitmapImage(DefaultThumbnailUri)
                                {
                                    DecodePixelHeight = 80,
                                    DecodePixelWidth = 80,
                                    DecodePixelType = DecodePixelType.Logical
                                };

                                args.DragUI.SetContentFromBitmapImage(DefaultThumbnailImage);

                                break;
                            }
                        case FileSystemStorageItemBase Item:
                            {
                                DataPackageView View = args.Data.GetView();

                                if (View.Contains(StandardDataFormats.StorageItems))
                                {
                                    args.DragUI.SetContentFromDataPackage();
                                }
                                else
                                {
                                    BitmapDecoder Decoder = await BitmapDecoder.CreateAsync(await Item.GetThumbnailRawStreamAsync(ThumbnailMode.ListView));

                                    using (SoftwareBitmap OriginBitmap = await Decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied))
                                    {
                                        args.DragUI.SetContentFromSoftwareBitmap(ComputerVisionProvider.GenenateResizedThumbnail(OriginBitmap, 80, 80));
                                    }
                                }

                                break;
                            }
                        default:
                            {
                                break;
                            }
                    }
                }
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
            try
            {
                if (sender is Button Btn)
                {
                    await HeaderClickExecution.ExecuteAsync(async () =>
                    {
                        using (IDisposable Disposable = await SignalControl.SignalAndWaitTrappedAsync())
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

                            if (ListViewHeaderSortIndicator.Target == CTarget)
                            {
                                ListViewHeaderSortIndicator.Direction = ListViewHeaderSortIndicator.Direction == SortDirection.Ascending ? SortDirection.Descending : SortDirection.Ascending;
                            }
                            else
                            {
                                ListViewHeaderSortIndicator.Target = CTarget;
                                ListViewHeaderSortIndicator.Direction = SortDirection.Ascending;
                            }

                            SearchResult.AddRange(await SortedCollectionGenerator.GetSortedCollectionAsync(SearchResult.DuplicateAndClear(), ListViewHeaderSortIndicator.Target, ListViewHeaderSortIndicator.Direction, SortStyle.None));
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"An exception was threw in {nameof(ListHeader_Click)}");
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
                                            using (AuxiliaryTrustProcessController.Exclusive Exclusive = await AuxiliaryTrustProcessController.GetControllerExclusiveAsync())
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
                                            using (AuxiliaryTrustProcessController.Exclusive Exclusive = await AuxiliaryTrustProcessController.GetControllerExclusiveAsync())
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
                                                                await JumpListController.AddItemAsync(JumpListGroup.Recent, LinkItem.LinkTargetPath);
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
                                                            using (AuxiliaryTrustProcessController.Exclusive Exclusive = await AuxiliaryTrustProcessController.GetControllerExclusiveAsync())
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
                                                        using (AuxiliaryTrustProcessController.Exclusive Exclusive = await AuxiliaryTrustProcessController.GetControllerExclusiveAsync())
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
                                                    using (AuxiliaryTrustProcessController.Exclusive Exclusive = await AuxiliaryTrustProcessController.GetControllerExclusiveAsync())
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
                                                        using (AuxiliaryTrustProcessController.Exclusive Exclusive = await AuxiliaryTrustProcessController.GetControllerExclusiveAsync())
                                                        {
                                                            if (!await Exclusive.Controller.LaunchFromAppModelIdAsync(Info.AppUserModelId, File.Path))
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
                                await JumpListController.AddItemAsync(JumpListGroup.Recent, Folder.Path);
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
                CommonContentDialog Dialog = new CommonContentDialog
                {
                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                    Content = Globalization.GetString("QueueDialog_LocateFileFailure_Content"),
                    CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                };

                await Dialog.ShowAsync();
            }
            catch (DirectoryNotFoundException)
            {
                CommonContentDialog Dialog = new CommonContentDialog
                {
                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                    Content = Globalization.GetString("QueueDialog_LocateFolderFailure_Content"),
                    CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                };

                await Dialog.ShowAsync();
            }
            catch (LaunchProgramException)
            {
                CommonContentDialog Dialog = new CommonContentDialog
                {
                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                    Content = Globalization.GetString("QueueDialog_LaunchFailed_Content"),
                    CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                };

                await Dialog.ShowAsync();
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"An exception was threw in {nameof(OpenSelectedItemAsync)}");

                await new CommonContentDialog
                {
                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                    Content = Globalization.GetString("QueueDialog_LocateFolderFailure_Content"),
                    CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                }.ShowAsync();
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

                    CommonContentDialog Dialog = new CommonContentDialog
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

                    CommonContentDialog Dialog = new CommonContentDialog
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
                bool PermanentDelete = Window.Current.CoreWindow.GetKeyState(VirtualKey.Shift).HasFlag(CoreVirtualKeyStates.Down) | SettingPage.IsAvoidRecycleBinEnabled;

                if (SettingPage.IsDoubleConfirmOnDeletionEnabled)
                {
                    CommonContentDialog Dialog = new CommonContentDialog
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

                if (ExecuteDelete)
                {
                    OperationListDeleteModel Model = new OperationListDeleteModel(PathList, PermanentDelete);

                    QueueTaskController.RegisterPostAction(Model, async (s, e) =>
                    {
                        EventDeferral Deferral = e.GetDeferral();

                        try
                        {
                            await Dispatcher.RunAndWaitAsyncTask(CoreDispatcherPriority.Low, async () =>
                            {
                                foreach (FilePresenter Presenter in TabViewContainer.Current.TabCollection.Select((Tab) => Tab.Content)
                                                                                                          .Cast<Frame>()
                                                                                                          .Select((Frame) => Frame.Content)
                                                                                                          .Cast<TabItemContentRenderer>()
                                                                                                          .SelectMany((Renderer) => Renderer.Presenters))
                                {
                                    if (Presenter.CurrentFolder is LabelCollectionVirtualFolder CollectionFolder)
                                    {
                                        foreach (string Path in PathList.Where((Path) => SQLite.Current.GetLabelKindFromPath(Path) == CollectionFolder.Kind))
                                        {
                                            await Presenter.AreaWatcher.InvokeRemovedEventManuallyAsync(new FileRemovedDeferredEventArgs(Path));
                                        }
                                    }
                                    else if (Presenter.CurrentFolder is INotWin32StorageFolder)
                                    {
                                        foreach (string Path in PathList.Where((Path) => Presenter.CurrentFolder.Path.Equals(System.IO.Path.GetDirectoryName(Path), StringComparison.OrdinalIgnoreCase)))
                                        {
                                            await Presenter.AreaWatcher.InvokeRemovedEventManuallyAsync(new FileRemovedDeferredEventArgs(Path));
                                        }
                                    }
                                }
                            });

                            foreach (string Path in PathList)
                            {
                                SQLite.Current.DeleteLabelKindByPath(Path);
                            }
                        }
                        catch (Exception ex)
                        {
                            LogTracer.Log(ex, $"Failed to execute the post action delegate of {nameof(Delete_Click)}");
                        }
                        finally
                        {
                            Deferral.Complete();
                        }
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
            if (!SettingPage.IsOpened
                && SearchResultList.SelectedItems.Count == 1
                && SearchResultList.SelectedItem is FileSystemStorageItemBase Item)
            {
                try
                {
                    if (SettingPage.IsQuicklookEnabled)
                    {
                        using (AuxiliaryTrustProcessController.Exclusive Exclusive = await AuxiliaryTrustProcessController.GetControllerExclusiveAsync(Priority: PriorityLevel.High))
                        {
                            if (await Exclusive.Controller.CheckQuicklookWindowVisibleAsync())
                            {
                                if (!string.IsNullOrEmpty(Item.Path))
                                {
                                    await Exclusive.Controller.SwitchQuicklookWindowAsync(Item.Path);
                                }
                            }
                        }
                    }
                    else if (SettingPage.IsSeerEnabled)
                    {
                        using (AuxiliaryTrustProcessController.Exclusive Exclusive = await AuxiliaryTrustProcessController.GetControllerExclusiveAsync(Priority: PriorityLevel.High))
                        {
                            if (await Exclusive.Controller.CheckSeerWindowVisibleAsync())
                            {
                                if (!string.IsNullOrEmpty(Item.Path))
                                {
                                    await Exclusive.Controller.SwitchSeerWindowAsync(Item.Path);
                                }
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

            IReadOnlyList<FileSystemStorageItemBase> SelectedItemsCopy = SearchResultList.SelectedItems.Cast<FileSystemStorageItemBase>().ToArray();

            if (SelectedItemsCopy.Count > 0)
            {
                RenameDialog Dialog = new RenameDialog(SelectedItemsCopy);

                if ((await Dialog.ShowAsync()) == ContentDialogResult.Primary)
                {
                    if (SelectedItemsCopy.Count == 1)
                    {
                        string ItemPath = SelectedItemsCopy.Single().Path;
                        string OriginName = Path.GetFileName(ItemPath);
                        string NewName = Dialog.DesireNameMap[OriginName];
                        string FolderPath = Path.GetDirectoryName(ItemPath);

                        if (OriginName != NewName)
                        {
                            if (!OriginName.Equals(NewName, StringComparison.OrdinalIgnoreCase)
                            && await FileSystemStorageItemBase.CheckExistsAsync(Path.Combine(FolderPath, NewName)))
                            {
                                CommonContentDialog Dialog1 = new CommonContentDialog
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

                            OperationListRenameModel Model = new OperationListRenameModel(ItemPath, Path.Combine(FolderPath, NewName));

                            QueueTaskController.RegisterPostAction(Model, async (s, e) =>
                            {
                                EventDeferral Deferral = e.GetDeferral();

                                try
                                {
                                    if (e.Status == OperationStatus.Completed && e.Parameter is string NewName)
                                    {
                                        await Dispatcher.RunAndWaitAsyncTask(CoreDispatcherPriority.Low, async () =>
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
                                                if (Presenter.CurrentFolder is LabelCollectionVirtualFolder CollectionFolder)
                                                {
                                                    if (SQLite.Current.GetLabelKindFromPath(ItemPath) == CollectionFolder.Kind)
                                                    {
                                                        await Presenter.AreaWatcher.InvokeRemovedEventManuallyAsync(new FileRemovedDeferredEventArgs(ItemPath));
                                                    }
                                                }
                                                else if (Presenter.CurrentFolder is INotWin32StorageFolder && Presenter.CurrentFolder.Path.Equals(FolderPath, StringComparison.OrdinalIgnoreCase))
                                                {
                                                    await Presenter.AreaWatcher.InvokeRenamedEventManuallyAsync(new FileRenamedDeferredEventArgs(ItemPath, NewName));
                                                }
                                            }
                                        });
                                    }
                                }
                                catch (Exception ex)
                                {
                                    LogTracer.Log(ex, $"Failed to execute the post action delegate of {nameof(Rename_Click)}");
                                }
                                finally
                                {
                                    Deferral.Complete();
                                }
                            });

                            QueueTaskController.EnqueueRenameOpeartion(Model);
                        }
                    }
                    else
                    {
                        foreach (FileSystemStorageItemBase OriginItem in SelectedItemsCopy.Where((Item) => Item.Name != Dialog.DesireNameMap[Item.Name]))
                        {
                            string FolderPath = Path.GetDirectoryName(OriginItem.Path);

                            OperationListRenameModel Model = new OperationListRenameModel(OriginItem.Path, Path.Combine(FolderPath, Dialog.DesireNameMap[OriginItem.Name]));

                            QueueTaskController.RegisterPostAction(Model, async (s, e) =>
                            {
                                EventDeferral Deferral = e.GetDeferral();

                                try
                                {
                                    if (e.Status == OperationStatus.Completed && e.Parameter is string NewName)
                                    {
                                        await Dispatcher.RunAndWaitAsyncTask(CoreDispatcherPriority.Low, async () =>
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
                                                if (Presenter.CurrentFolder is LabelCollectionVirtualFolder CollectionFolder)
                                                {
                                                    if (SQLite.Current.GetLabelKindFromPath(OriginItem.Path) == CollectionFolder.Kind)
                                                    {
                                                        await Presenter.AreaWatcher.InvokeRemovedEventManuallyAsync(new FileRemovedDeferredEventArgs(OriginItem.Path));
                                                    }
                                                }
                                                else if (Presenter.CurrentFolder is INotWin32StorageFolder && Presenter.CurrentFolder.Path.Equals(FolderPath, StringComparison.OrdinalIgnoreCase))
                                                {
                                                    await Presenter.AreaWatcher.InvokeRenamedEventManuallyAsync(new FileRenamedDeferredEventArgs(OriginItem.Path, NewName));
                                                }
                                            }
                                        });
                                    }
                                }
                                catch (Exception ex)
                                {
                                    LogTracer.Log(ex, $"Failed to execute the post action delegate of {nameof(Rename_Click)}");
                                }
                                finally
                                {
                                    Deferral.Complete();
                                }
                            });

                            QueueTaskController.EnqueueRenameOpeartion(Model);
                        }
                    }
                }
            }
        }
    }
}
