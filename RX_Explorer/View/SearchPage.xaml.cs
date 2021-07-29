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
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.Storage;
using Windows.System;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.Input;
using Windows.UI.WindowManagement;
using Windows.UI.WindowManagement.Preview;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Navigation;
using Package = Windows.ApplicationModel.Package;

namespace RX_Explorer
{
    public sealed partial class SearchPage : Page
    {
        private WeakReference<FileControl> WeakToFileControl;
        private CancellationTokenSource Cancellation;
        private ListViewBaseSelectionExtention SelectionExtention;
        private readonly PointerEventHandler PointerPressedEventHandler;
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
        }

        private void Filter_RefreshListRequested(object sender, FilterController.RefreshRequestedEventArgs e)
        {
            SearchResult.Clear();

            foreach (FileSystemStorageItemBase Item in SortCollectionGenerator.GetSortedCollection(e.FilterCollection, STarget, SDirection))
            {
                SearchResult.Add(Item);
            }
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            if (e.Parameter is Tuple<FileControl, SearchOptions> Parameters)
            {
                CoreWindow.GetForCurrentThread().KeyDown += SearchPage_KeyDown;

                SearchResultList.AddHandler(PointerPressedEvent, PointerPressedEventHandler, true);
                SelectionExtention = new ListViewBaseSelectionExtention(SearchResultList, DrawRectangle);

                if (e.NavigationMode == NavigationMode.New)
                {
                    WeakToFileControl = new WeakReference<FileControl>(Parameters.Item1);
                    await Initialize(Parameters.Item2).ConfigureAwait(false);
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
                    case VirtualKey.Space when SettingControl.IsQuicklookEnable && SearchResultList.SelectedItems.Count <= 1:
                        {
                            using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableController())
                            {
                                if (await Exclusive.Controller.CheckIfQuicklookIsAvaliableAsync())
                                {
                                    if (SearchResultList.SelectedItem is FileSystemStorageItemBase Item)
                                    {
                                        await Exclusive.Controller.ViewWithQuicklookAsync(Item.Path);
                                    }
                                }
                            }

                            break;
                        }
                }
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

                        if (Group.Any() && (SearchResultList.SelectedItem == null || !Group.Contains(SearchResultList.SelectedItem)))
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

        private async Task Initialize(SearchOptions Options)
        {
            STarget = SortTarget.Name;
            SDirection = SortDirection.Ascending;

            HasItem.Visibility = Visibility.Collapsed;

            CancellationTokenSource Cancellation = new CancellationTokenSource();

            try
            {
                this.Cancellation = Cancellation;

                SearchStatus.Text = Globalization.GetString("SearchProcessingText");
                SearchStatusBar.Visibility = Visibility.Visible;

                switch (Options.EngineCategory)
                {
                    case SearchCategory.BuiltInEngine:
                        {
                            IReadOnlyList<FileSystemStorageItemBase> Result = await Options.SearchFolder.SearchAsync(Options.SearchText,
                                                                                                                     Options.DeepSearch,
                                                                                                                     SettingControl.IsDisplayHiddenItem,
                                                                                                                     SettingControl.IsDisplayProtectedSystemItems,
                                                                                                                     Options.UseRegexExpression,
                                                                                                                     Options.UseAQSExpression,
                                                                                                                     Options.UseIndexerOnly,
                                                                                                                     Options.IgnoreCase,
                                                                                                                     Cancellation.Token);

                            if (Result.Count == 0)
                            {
                                HasItem.Visibility = Visibility.Visible;
                            }
                            else
                            {
                                foreach (FileSystemStorageItemBase Item in SortCollectionGenerator.GetSortedCollection(Result, SortTarget.Name, SortDirection.Ascending))
                                {
                                    if (Cancellation.IsCancellationRequested)
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
                            using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableController())
                            {
                                IReadOnlyList<FileSystemStorageItemBase> SearchItems = await Exclusive.Controller.SearchByEverythingAsync(Options.DeepSearch ? string.Empty : Options.SearchFolder.Path,
                                                                                                                                          Options.SearchText,
                                                                                                                                          Options.UseRegexExpression,
                                                                                                                                          Options.IgnoreCase,
                                                                                                                                          Options.NumLimit);

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

                ListViewDetailHeader.Filter.SetDataSource(SearchResult);
                SearchStatus.Text = $"{Globalization.GetString("SearchCompletedText")} ({SearchResult.Count} {Globalization.GetString("Items_Description")})";
                SearchStatusBar.Visibility = Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"An error was threw in {nameof(Initialize)}");
            }
            finally
            {
                Cancellation.Dispose();

                if (this.Cancellation == Cancellation)
                {
                    this.Cancellation = null;
                }
            }
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            CoreWindow.GetForCurrentThread().KeyDown -= SearchPage_KeyDown;

            Cancellation?.Cancel();

            SearchResultList.RemoveHandler(PointerPressedEvent, PointerPressedEventHandler);

            SelectionExtention.Dispose();
            SelectionExtention = null;

            if (e.NavigationMode == NavigationMode.Back)
            {
                SearchResult.Clear();
            }
        }

        private void ViewControl_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if ((e.OriginalSource as FrameworkElement)?.DataContext is FileSystemStorageItemBase Item)
            {
                PointerPoint PointerInfo = e.GetCurrentPoint(null);

                if ((e.OriginalSource as FrameworkElement).FindParentOfType<SelectorItem>() != null)
                {
                    if (e.KeyModifiers == VirtualKeyModifiers.None && SearchResultList.SelectionMode != ListViewSelectionMode.Multiple)
                    {
                        if (SearchResultList.SelectedItems.Contains(Item))
                        {
                            SelectionExtention.Disable();
                        }
                        else
                        {
                            if (PointerInfo.Properties.IsLeftButtonPressed)
                            {
                                SearchResultList.SelectedItem = Item;
                            }

                            if (e.OriginalSource is ListViewItemPresenter || (e.OriginalSource is TextBlock Block && Block.Name == "EmptyTextblock"))
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
            }
            else
            {
                SearchResultList.SelectedItem = null;
                SelectionExtention.Enable();
            }
        }

        private async void Location_Click(object sender, RoutedEventArgs e)
        {
            if (SearchResultList.SelectedItem is FileSystemStorageItemBase Item)
            {
                try
                {
                    string ParentFolderPath = Path.GetDirectoryName(Item.Path);

                    if (WeakToFileControl.TryGetTarget(out FileControl Control))
                    {
                        Frame.GoBack();

                        await Control.CurrentPresenter.DisplayItemsInFolder(ParentFolderPath);

                        await JumpListController.Current.AddItemAsync(JumpListGroup.Recent, ParentFolderPath);

                        if (Control.CurrentPresenter.FileCollection.FirstOrDefault((SItem) => SItem == Item) is FileSystemStorageItemBase Target)
                        {
                            Control.CurrentPresenter.ItemPresenter.ScrollIntoView(Target);
                            Control.CurrentPresenter.SelectedItem = Target;
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

                    _ = await dialog.ShowAsync();
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

        private void SearchResultList_RightTapped(object sender, RightTappedRoutedEventArgs e)
        {
            if (e.PointerDeviceType == Windows.Devices.Input.PointerDeviceType.Mouse)
            {
                if ((e.OriginalSource as FrameworkElement)?.DataContext is FileSystemStorageItemBase Context)
                {
                    if (SearchResultList.SelectedItems.Count > 1 && SearchResultList.SelectedItems.Contains(Context))
                    {
                        SearchResultList.ContextFlyout = MixCommandFlyout;
                    }
                    else
                    {
                        SearchResultList.ContextFlyout = SingleCommandFlyout;
                        SearchResultList.SelectedItem = Context;
                    }
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

        private void SearchResultList_Holding(object sender, HoldingRoutedEventArgs e)
        {
            if (e.HoldingState == HoldingState.Started)
            {
                if (SearchResultList.SelectedItems.Count > 1)
                {
                    SearchResultList.ContextFlyout = MixCommandFlyout;
                }
                else
                {
                    if ((e.OriginalSource as FrameworkElement)?.DataContext is FileSystemStorageItemBase Context)
                    {
                        SearchResultList.ContextFlyout = SingleCommandFlyout;
                        SearchResultList.SelectedItem = Context;
                    }
                    else
                    {
                        SearchResultList.ContextFlyout = null;
                    }
                }

                e.Handled = true;
            }
        }

        private void SearchResultList_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (!args.InRecycleQueue)
            {
                args.RegisterUpdateCallback(async (s, e) =>
                {
                    if (e.Item is FileSystemStorageItemBase Item)
                    {
                        await Item.LoadAsync().ConfigureAwait(false);
                    }
                });
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

        private async void SearchResultList_DoubleTapped(object sender, Windows.UI.Xaml.Input.DoubleTappedRoutedEventArgs e)
        {
            if ((e.OriginalSource as FrameworkElement).DataContext is FileSystemStorageItemBase Item)
            {
                await LaunchSelectedItem(Item);
            }
        }

        private async Task LaunchSelectedItem(FileSystemStorageItemBase Item)
        {
            try
            {
                switch (Item)
                {
                    case FileSystemStorageFile File:
                        {
                            if (!await FileSystemStorageItemBase.CheckExistAsync(File.Path))
                            {
                                QueueContentDialog Dialog = new QueueContentDialog
                                {
                                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                    Content = Globalization.GetString("QueueDialog_LocateFileFailure_Content"),
                                    CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                                };

                                _ = await Dialog.ShowAsync();

                                return;
                            }

                            switch (File.Type.ToLower())
                            {
                                case ".exe":
                                case ".bat":
                                case ".msi":
                                    {
                                        using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableController())
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
                                        using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableController())
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
                                                            if (WeakToFileControl.TryGetTarget(out FileControl Control))
                                                            {
                                                                Frame.GoBack();

                                                                await Control.CurrentPresenter.DisplayItemsInFolder(LinkItem.LinkTargetPath);

                                                                await JumpListController.Current.AddItemAsync(JumpListGroup.Recent, LinkItem.LinkTargetPath);

                                                                if (Control.CurrentPresenter.FileCollection.FirstOrDefault((SItem) => SItem == LinkItem) is FileSystemStorageItemBase Target)
                                                                {
                                                                    Control.CurrentPresenter.ItemPresenter.ScrollIntoView(Target);
                                                                    Control.CurrentPresenter.SelectedItem = Target;
                                                                }
                                                            }

                                                            break;
                                                        }
                                                    case FileSystemStorageFile:
                                                        {
                                                            await LinkItem.LaunchAsync();
                                                            break;
                                                        }
                                                }
                                            }
                                            else
                                            {
                                                await LinkItem.LaunchAsync();
                                            }
                                        }

                                        break;
                                    }
                                case ".url":
                                    {
                                        if (File is UrlStorageFile UrlItem)
                                        {
                                            await UrlItem.LaunchAsync();
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
                                                        using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableController())
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
                                                    using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableController())
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
                                                using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableController())
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
                                                            using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableController())
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
                                                        using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableController())
                                                        {
                                                            if (!await Exclusive.Controller.LaunchUWPFromAUMIDAsync(Info.AppUserModelId, File.Path))
                                                            {
                                                                QueueContentDialog Dialog = new QueueContentDialog
                                                                {
                                                                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                                                    Content = Globalization.GetString("QueueDialog_UnableAccessFile_Content"),
                                                                    CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                                                                };

                                                                _ = await Dialog.ShowAsync();
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
                            if (await FileSystemStorageItemBase.CheckExistAsync(Folder.Path))
                            {
                                if (WeakToFileControl.TryGetTarget(out FileControl Control))
                                {
                                    Frame.GoBack();

                                    await Control.CurrentPresenter.DisplayItemsInFolder(Folder);

                                    await JumpListController.Current.AddItemAsync(JumpListGroup.Recent, Folder.Path);

                                    if (Control.CurrentPresenter.FileCollection.FirstOrDefault((SItem) => SItem == Folder) is FileSystemStorageItemBase Target)
                                    {
                                        Control.CurrentPresenter.ItemPresenter.ScrollIntoView(Target);
                                        Control.CurrentPresenter.SelectedItem = Target;
                                    }
                                }
                            }
                            else
                            {
                                QueueContentDialog Dialog = new QueueContentDialog
                                {
                                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                    Content = Globalization.GetString("QueueDialog_LocateFolderFailure_Content"),
                                    CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                                };

                                _ = await Dialog.ShowAsync();
                            }

                            break;
                        }
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"An error was threw in {nameof(LaunchSelectedItem)}");

                QueueContentDialog dialog = new QueueContentDialog
                {
                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                    Content = Globalization.GetString("QueueDialog_LocateFolderFailure_Content"),
                    CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                };

                _ = await dialog.ShowAsync();
            }
        }

        private async void Open_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();

            if (SearchResultList.SelectedItem is FileSystemStorageItemBase Item)
            {
                await LaunchSelectedItem(Item);
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
                    Clipboard.SetContent(await SearchResultList.SelectedItems.Cast<FileSystemStorageItemBase>().GetAsDataPackageAsync(DataPackageOperation.Copy));
                }
                catch
                {
                    QueueContentDialog Dialog = new QueueContentDialog
                    {
                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                        Content = Globalization.GetString("QueueDialog_UnableAccessClipboard_Content"),
                        CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                    };

                    await Dialog.ShowAsync().ConfigureAwait(false);
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
                    Clipboard.SetContent(await SearchResultList.SelectedItems.Cast<FileSystemStorageItemBase>().GetAsDataPackageAsync(DataPackageOperation.Move));
                }
                catch
                {
                    QueueContentDialog Dialog = new QueueContentDialog
                    {
                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                        Content = Globalization.GetString("QueueDialog_UnableAccessClipboard_Content"),
                        CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                    };

                    await Dialog.ShowAsync().ConfigureAwait(false);
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
    }
}
