using Microsoft.Toolkit.Uwp.UI.Controls;
using Microsoft.UI.Xaml.Controls;
using RX_Explorer.Class;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Data.Xml.Dom;
using Windows.Services.Store;
using Windows.Storage;
using Windows.System;
using Windows.UI.Core;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Media.Imaging;
using AnimationController = RX_Explorer.Class.AnimationController;
using NavigationViewItem = Microsoft.UI.Xaml.Controls.NavigationViewItem;
using SymbolIconSource = Microsoft.UI.Xaml.Controls.SymbolIconSource;
using TabView = Microsoft.UI.Xaml.Controls.TabView;
using TabViewTabCloseRequestedEventArgs = Microsoft.UI.Xaml.Controls.TabViewTabCloseRequestedEventArgs;

namespace RX_Explorer
{
    public sealed partial class TabViewContainer : Page
    {
        public static Frame CurrentNavigationControl { get; private set; }

        public ObservableCollection<TabViewItem> TabCollection { get; }

        public static TabViewContainer Current { get; private set; }

        private CancellationTokenSource DelayPreviewCancel;

        private readonly DispatcherTimer PreviewTimer;

        public readonly LayoutModeController LayoutModeControl;

        public TabViewContainer()
        {
            InitializeComponent();

            Current = this;

            PreviewTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(3)
            };

            TabCollection = new ObservableCollection<TabViewItem>();

            LayoutModeControl = new LayoutModeController();

            PreviewTimer.Tick += PreviewTimer_Tick;
            Loaded += TabViewContainer_Loaded;
            Application.Current.Suspending += Current_Suspending;
            CoreWindow.GetForCurrentThread().PointerPressed += TabViewContainer_PointerPressed;
            CoreWindow.GetForCurrentThread().KeyDown += TabViewContainer_KeyDown;
            CommonAccessCollection.LibraryNotFound += CommonAccessCollection_LibraryNotFound;
            QueueTaskController.ListItemSource.CollectionChanged += ListItemSource_CollectionChanged;
            QueueTaskController.ProgressChanged += QueueTaskController_ProgressChanged;

            if (ApplicationData.Current.LocalSettings.Values["ShouldPinTaskList"] is bool ShouldPin)
            {
                if (ShouldPin)
                {
                    TaskListPanel.DisplayMode = SplitViewDisplayMode.Inline;
                    TaskListPanel.IsPaneOpen = true;

                    PinTaskListPanel.Content = new Viewbox
                    {
                        Child = new FontIcon
                        {
                            Glyph = "\uE77A"
                        }
                    };
                }
                else
                {
                    TaskListPanel.DisplayMode = SplitViewDisplayMode.Overlay;
                    TaskListPanel.IsPaneOpen = false;

                    PinTaskListPanel.Content = new Viewbox
                    {
                        Child = new FontIcon
                        {
                            Glyph = "\uE840"
                        }
                    };
                }
            }
            else
            {
                TaskListPanel.DisplayMode = SplitViewDisplayMode.Overlay;

                ApplicationData.Current.LocalSettings.Values["ShouldPinTaskList"] = false;

                PinTaskListPanel.Content = new Viewbox
                {
                    Child = new FontIcon
                    {
                        Glyph = "\uE840"
                    }
                };
            }
        }

        private void Current_Suspending(object sender, Windows.ApplicationModel.SuspendingEventArgs e)
        {
            if (StartupModeController.Mode == StartupMode.LastOpenedTab)
            {
                SaveLastOpenedTab();
            }
        }

        private void SaveLastOpenedTab()
        {
            List<string[]> PathList = new List<string[]>();

            foreach (TabViewItem Item in TabCollection)
            {
                if (Item.Tag is FileControl Control)
                {
                    if (Control.BladeViewer.Items.Count == 0)
                    {
                        if (Item.Content is Frame frame && frame.Tag is string[] InitPathArray)
                        {
                            PathList.Add(InitPathArray);
                        }
                    }
                    else
                    {
                        PathList.Add(Control.BladeViewer.Items.Cast<BladeItem>()
                                                              .Select((Blade) => Blade.Content)
                                                              .OfType<FilePresenter>()
                                                              .Select((Presenter) => Presenter.CurrentFolder?.Path)
                                                              .Where((Path) => !string.IsNullOrWhiteSpace(Path))
                                                              .ToArray());
                    }
                }
                else
                {
                    PathList.Add(Array.Empty<string>());
                }
            }

            StartupModeController.SetLastOpenedPath(PathList);
        }

        private async void PreviewTimer_Tick(object sender, object e)
        {
            if (MainPage.Current.NavView.SelectedItem is NavigationViewItem NavItem
                && Convert.ToString(NavItem.Content) == Globalization.GetString("MainPage_PageDictionary_Home_Label")
                && SettingControl.IsTabPreviewEnabled)
            {
                try
                {
                    PreviewTimer.Stop();

                    if (TabViewControl.SelectedItem is TabViewItem Item && Item.Content is UIElement Element)
                    {
                        RenderTargetBitmap PreviewBitmap = new RenderTargetBitmap();

                        await PreviewBitmap.RenderAsync(Element, 750, 450);

                        if (FlyoutBase.GetAttachedFlyout(Item) is Flyout PreviewFlyout)
                        {
                            if (PreviewFlyout.Content is Image PreviewImage)
                            {
                                PreviewImage.Source = PreviewBitmap;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogTracer.Log(ex, "Could not render a preview image");
                }
                finally
                {
                    PreviewTimer.Start();
                }
            }
        }

        private async void QueueTaskController_ProgressChanged(object sender, System.ComponentModel.ProgressChangedEventArgs e)
        {
            TaskListProgress.Value = e.ProgressPercentage;

            if (e.ProgressPercentage >= 100)
            {
                await Task.Delay(800).ContinueWith((_) => TaskListProgress.Visibility = Visibility.Collapsed, TaskScheduler.FromCurrentSynchronizationContext());
            }
            else
            {
                TaskListProgress.Visibility = Visibility.Visible;
                TaskListBadge.Value = QueueTaskController.ListItemSource.Count((Item) => Item.Status is OperationStatus.Preparing or OperationStatus.Processing or OperationStatus.Waiting or OperationStatus.NeedAttention);
            }
        }

        private void ListItemSource_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            EmptyTip.Visibility = QueueTaskController.ListItemSource.Count > 0 ? Visibility.Collapsed : Visibility.Visible;
            TaskListBadge.Value = QueueTaskController.ListItemSource.Count((Item) => Item.Status is OperationStatus.Preparing or OperationStatus.Processing or OperationStatus.Waiting or OperationStatus.NeedAttention);
        }

        private async void CommonAccessCollection_LibraryNotFound(object sender, IEnumerable<string> ErrorList)
        {
            StringBuilder Builder = new StringBuilder();

            foreach (string Message in ErrorList)
            {
                Builder.AppendLine($"   {Message}");
            }

            QueueContentDialog dialog = new QueueContentDialog
            {
                Title = Globalization.GetString("Common_Dialog_WarningTitle"),
                Content = Globalization.GetString("QueueDialog_PinFolderNotFound_Content") + Builder.ToString(),
                CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
            };

            await dialog.ShowAsync();
        }

        private async void TabViewContainer_KeyDown(CoreWindow sender, KeyEventArgs args)
        {
            if (!QueueContentDialog.IsRunningOrWaiting)
            {
                CoreVirtualKeyStates CtrlState = sender.GetKeyState(VirtualKey.Control);

                switch (args.VirtualKey)
                {
                    case VirtualKey.W when CtrlState.HasFlag(CoreVirtualKeyStates.Down) && TabViewControl.SelectedItem is TabViewItem Tab:
                        {
                            args.Handled = true;

                            await CleanUpAndRemoveTabItem(Tab);

                            break;
                        }
                    case VirtualKey.PageUp when CtrlState.HasFlag(CoreVirtualKeyStates.Down) && TabCollection.Count > 1:
                        {
                            args.Handled = true;

                            if (TabViewControl.SelectedIndex > 0)
                            {
                                TabViewControl.SelectedIndex--;
                            }
                            else
                            {
                                TabViewControl.SelectedIndex = TabCollection.Count - 1;
                            }

                            break;
                        }
                    case VirtualKey.PageDown when CtrlState.HasFlag(CoreVirtualKeyStates.Down) && TabCollection.Count > 1:
                        {
                            args.Handled = true;

                            if (TabViewControl.SelectedIndex < TabCollection.Count - 1)
                            {
                                TabViewControl.SelectedIndex++;
                            }
                            else
                            {
                                TabViewControl.SelectedIndex = 0;
                            }

                            break;
                        }
                    default:
                        {
                            if (CurrentNavigationControl?.Content is FileControl Control && Control.CurrentPresenter?.CurrentFolder is RootStorageFolder)
                            {
                                Home HomeControl = Control.CurrentPresenter.RootFolderControl;

                                switch (args.VirtualKey)
                                {
                                    case VirtualKey.Space when SettingControl.IsQuicklookEnabled:
                                        {
                                            args.Handled = true;

                                            using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableController())
                                            {
                                                if (await Exclusive.Controller.CheckIfQuicklookIsAvaliableAsync())
                                                {
                                                    if (HomeControl.DriveGrid.SelectedItem is DriveDataBase Device && !string.IsNullOrEmpty(Device.Path))
                                                    {
                                                        await Exclusive.Controller.ViewWithQuicklookAsync(Device.Path);
                                                    }
                                                    else if (HomeControl.LibraryGrid.SelectedItem is LibraryStorageFolder Library && !string.IsNullOrEmpty(Library.Path))
                                                    {
                                                        await Exclusive.Controller.ViewWithQuicklookAsync(Library.Path);
                                                    }
                                                }
                                            }

                                            break;
                                        }
                                    case VirtualKey.B when CtrlState.HasFlag(CoreVirtualKeyStates.Down):
                                        {
                                            if (HomeControl.DriveGrid.SelectedItem is DriveDataBase Drive)
                                            {
                                                args.Handled = true;

                                                if (string.IsNullOrEmpty(Drive.Path))
                                                {
                                                    QueueContentDialog Dialog = new QueueContentDialog
                                                    {
                                                        Title = Globalization.GetString("Common_Dialog_TipTitle"),
                                                        Content = Globalization.GetString("QueueDialog_CouldNotAccess_Content"),
                                                        CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                                                    };

                                                    await Dialog.ShowAsync();
                                                }
                                                else
                                                {
                                                    if (await MSStoreHelper.Current.CheckPurchaseStatusAsync())
                                                    {
                                                        await Control.CreateNewBladeAsync(Drive.Path);
                                                    }
                                                }
                                            }
                                            else if (HomeControl.LibraryGrid.SelectedItem is LibraryStorageFolder Library)
                                            {
                                                args.Handled = true;

                                                if (string.IsNullOrEmpty(Library.Path))
                                                {
                                                    QueueContentDialog Dialog = new QueueContentDialog
                                                    {
                                                        Title = Globalization.GetString("Common_Dialog_TipTitle"),
                                                        Content = Globalization.GetString("QueueDialog_CouldNotAccess_Content"),
                                                        CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                                                    };

                                                    await Dialog.ShowAsync();
                                                }
                                                else
                                                {
                                                    if (await MSStoreHelper.Current.CheckPurchaseStatusAsync())
                                                    {
                                                        await Control.CreateNewBladeAsync(Library.Path);
                                                    }
                                                }
                                            }

                                            break;
                                        }
                                    case VirtualKey.Q when CtrlState.HasFlag(CoreVirtualKeyStates.Down):
                                        {
                                            if (HomeControl.DriveGrid.SelectedItem is DriveDataBase Drive)
                                            {
                                                args.Handled = true;

                                                if (string.IsNullOrEmpty(Drive.Path))
                                                {
                                                    QueueContentDialog Dialog = new QueueContentDialog
                                                    {
                                                        Title = Globalization.GetString("Common_Dialog_TipTitle"),
                                                        Content = Globalization.GetString("QueueDialog_CouldNotAccess_Content"),
                                                        CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                                                    };

                                                    await Dialog.ShowAsync();
                                                }
                                                else
                                                {
                                                    await Launcher.LaunchUriAsync(new Uri($"rx-explorer:{Uri.EscapeDataString(JsonSerializer.Serialize(new List<string[]> { new string[] { Drive.Path } }))}"));
                                                }
                                            }
                                            else if (HomeControl.LibraryGrid.SelectedItem is LibraryStorageFolder Library)
                                            {
                                                args.Handled = true;

                                                if (string.IsNullOrEmpty(Library.Path))
                                                {
                                                    QueueContentDialog Dialog = new QueueContentDialog
                                                    {
                                                        Title = Globalization.GetString("Common_Dialog_TipTitle"),
                                                        Content = Globalization.GetString("QueueDialog_CouldNotAccess_Content"),
                                                        CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                                                    };

                                                    await Dialog.ShowAsync();
                                                }
                                                else
                                                {
                                                    await Launcher.LaunchUriAsync(new Uri($"rx-explorer:{Uri.EscapeDataString(JsonSerializer.Serialize(new List<string[]> { new string[] { Library.Path } }))}"));
                                                }
                                            }

                                            break;
                                        }
                                    case VirtualKey.T when CtrlState.HasFlag(CoreVirtualKeyStates.Down):
                                        {
                                            if (HomeControl.DriveGrid.SelectedItem is DriveDataBase Drive)
                                            {
                                                args.Handled = true;

                                                if (string.IsNullOrEmpty(Drive.Path))
                                                {
                                                    QueueContentDialog Dialog = new QueueContentDialog
                                                    {
                                                        Title = Globalization.GetString("Common_Dialog_TipTitle"),
                                                        Content = Globalization.GetString("QueueDialog_CouldNotAccess_Content"),
                                                        CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                                                    };

                                                    await Dialog.ShowAsync();
                                                }
                                                else
                                                {
                                                    await CreateNewTabAsync(Drive.Path);
                                                }
                                            }
                                            else if (HomeControl.LibraryGrid.SelectedItem is LibraryStorageFolder Library)
                                            {
                                                args.Handled = true;

                                                if (string.IsNullOrEmpty(Library.Path))
                                                {
                                                    QueueContentDialog Dialog = new QueueContentDialog
                                                    {
                                                        Title = Globalization.GetString("Common_Dialog_TipTitle"),
                                                        Content = Globalization.GetString("QueueDialog_CouldNotAccess_Content"),
                                                        CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                                                    };

                                                    await Dialog.ShowAsync();
                                                }
                                                else
                                                {
                                                    await CreateNewTabAsync(Library.Path);
                                                }
                                            }

                                            break;
                                        }
                                    case VirtualKey.Enter:
                                        {
                                            if (HomeControl.DriveGrid.SelectedItem is DriveDataBase Drive)
                                            {
                                                args.Handled = true;

                                                if (string.IsNullOrEmpty(Drive.Path))
                                                {
                                                    QueueContentDialog Dialog = new QueueContentDialog
                                                    {
                                                        Title = Globalization.GetString("Common_Dialog_TipTitle"),
                                                        Content = Globalization.GetString("QueueDialog_CouldNotAccess_Content"),
                                                        CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                                                    };

                                                    await Dialog.ShowAsync();
                                                }
                                                else
                                                {
                                                    await HomeControl.OpenTargetFolder(Drive.Path);
                                                }
                                            }
                                            else if (HomeControl.LibraryGrid.SelectedItem is LibraryStorageFolder Library)
                                            {
                                                args.Handled = true;

                                                if (string.IsNullOrEmpty(Library.Path))
                                                {
                                                    QueueContentDialog Dialog = new QueueContentDialog
                                                    {
                                                        Title = Globalization.GetString("Common_Dialog_TipTitle"),
                                                        Content = Globalization.GetString("QueueDialog_CouldNotAccess_Content"),
                                                        CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                                                    };

                                                    await Dialog.ShowAsync();
                                                }
                                                else
                                                {
                                                    await HomeControl.OpenTargetFolder(Library.Path);
                                                }
                                            }

                                            break;
                                        }
                                    case VirtualKey.F5:
                                        {
                                            args.Handled = true;

                                            await CommonAccessCollection.LoadDriveAsync(true);

                                            break;
                                        }
                                    case VirtualKey.T when CtrlState.HasFlag(CoreVirtualKeyStates.Down):
                                        {
                                            args.Handled = true;

                                            await CreateNewTabAsync();

                                            break;
                                        }
                                }
                            }

                            break;
                        }
                }
            }
        }

        private void TabViewContainer_PointerPressed(CoreWindow sender, PointerEventArgs args)
        {
            bool BackButtonPressed = args.CurrentPoint.Properties.IsXButton1Pressed;
            bool ForwardButtonPressed = args.CurrentPoint.Properties.IsXButton2Pressed;

            if (CurrentNavigationControl?.Content is FileControl Control)
            {
                if (BackButtonPressed)
                {
                    args.Handled = true;

                    if (!QueueContentDialog.IsRunningOrWaiting)
                    {
                        if (Control.GoBackRecord.IsEnabled)
                        {
                            Control.GoBackRecord_Click(null, null);
                        }
                        else
                        {
                            MainPage.Current.NavView_BackRequested(null, null);
                        }
                    }
                }
                else if (ForwardButtonPressed)
                {
                    args.Handled = true;

                    if (!QueueContentDialog.IsRunningOrWaiting && Control.GoForwardRecord.IsEnabled)
                    {
                        Control.GoForwardRecord_Click(null, null);
                    }
                }
            }
            else
            {
                if (BackButtonPressed)
                {
                    args.Handled = true;

                    MainPage.Current.NavView_BackRequested(null, null);
                }
                else if (ForwardButtonPressed)
                {
                    args.Handled = true;
                }
            }
        }

        public async Task CreateNewTabAsync(List<string[]> BulkTabWithPath)
        {
            try
            {
                foreach (string[] PathArray in BulkTabWithPath)
                {
                    TabCollection.Add(await CreateNewTabCoreAsync(PathArray));
                }

                TabViewControl.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, "Error happened when try to create a new tab");
            }
        }

        public async Task CreateNewTabAsync(params string[] PathArray)
        {
            try
            {
                TabCollection.Add(await CreateNewTabCoreAsync(PathArray));
                TabViewControl.SelectedItem = TabCollection.LastOrDefault();
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, "Error happened when try to create a new tab");
            }
        }

        public async Task CreateNewTabAsync(int InsertIndex, params string[] PathArray)
        {
            int Index = Math.Min(Math.Max(0, InsertIndex), TabCollection.Count);

            try
            {
                TabCollection.Insert(Index, await CreateNewTabCoreAsync(PathArray));
                TabViewControl.SelectedIndex = Index;
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, "Error happened when try to create a new tab");
            }
        }

        private async void TabViewContainer_Loaded(object sender, RoutedEventArgs e)
        {
            Loaded -= TabViewContainer_Loaded;

            PreviewTimer.Start();

            if ((MainPage.Current.ActivatePathArray?.Count).GetValueOrDefault() == 0)
            {
                await CreateNewTabAsync();
            }
            else
            {
                await CreateNewTabAsync(MainPage.Current.ActivatePathArray);
            }

            if (TabViewControl.FindChildOfName<Button>("AddButton") is Button AddBtn)
            {
                AddBtn.IsTabStop = false;
            }

            List<Task> LoadTaskList = new List<Task>(3)
            {
                CommonAccessCollection.LoadQuickStartItemsAsync(),
                CommonAccessCollection.LoadDriveAsync()
            };

            if (SettingControl.LibraryExpanderIsExpanded)
            {
                LoadTaskList.Add(CommonAccessCollection.LoadLibraryFoldersAsync());
            }

            await Task.WhenAll(LoadTaskList).ConfigureAwait(false);
        }

        private async void TabViewControl_TabCloseRequested(TabView sender, TabViewTabCloseRequestedEventArgs args)
        {
            await CleanUpAndRemoveTabItem(args.Tab);
        }

        private async void TabViewControl_AddTabButtonClick(TabView sender, object args)
        {
            await CreateNewTabAsync();
        }

        private async Task<TabViewItem> CreateNewTabCoreAsync(params string[] PathForNewTab)
        {
            FullTrustProcessController.RequestResizeController(TabCollection.Count + 1);

            Frame BaseFrame = new Frame();

            TabViewItem Item = new TabViewItem
            {
                IconSource = new SymbolIconSource { Symbol = Symbol.Document },
                IsTabStop = false,
                AllowDrop = true,
                IsDoubleTapEnabled = true,
                Content = BaseFrame,
                HeaderTemplate = TabViewItemHeaderTemplate
            };
            Item.DragEnter += Item_DragEnter;
            Item.PointerEntered += Item_PointerEntered;
            Item.PointerExited += Item_PointerExited;
            Item.PointerPressed += Item_PointerPressed;
            Item.PointerCanceled += Item_PointerCanceled;
            Item.DoubleTapped += Item_DoubleTapped;

            Flyout PreviewFlyout = new Flyout
            {
                FlyoutPresenterStyle = new Style(typeof(FlyoutPresenter)),
                Content = new Image
                {
                    Stretch = Stretch.Uniform,
                    Height = 300,
                    Width = 500
                }
            };
            PreviewFlyout.FlyoutPresenterStyle.Setters.Add(new Setter(MaxHeightProperty, 320));
            PreviewFlyout.FlyoutPresenterStyle.Setters.Add(new Setter(MaxWidthProperty, 520));
            PreviewFlyout.FlyoutPresenterStyle.Setters.Add(new Setter(PaddingProperty, 0));
            PreviewFlyout.FlyoutPresenterStyle.Setters.Add(new Setter(CornerRadiusProperty, (CornerRadius)Application.Current.Resources["CustomCornerRadius"]));

            FlyoutBase.SetAttachedFlyout(Item, PreviewFlyout);

            List<string> ValidPathArray = new List<string>();

            foreach (string Path in PathForNewTab.Where((Path) => !string.IsNullOrWhiteSpace(Path)))
            {
                if (Path.Equals(RootStorageFolder.Instance.Path, StringComparison.OrdinalIgnoreCase) || await FileSystemStorageItemBase.CheckExistAsync(Path))
                {
                    ValidPathArray.Add(Path);
                }
            }

            if (ValidPathArray.Count == 0)
            {
                Item.Header = RootStorageFolder.Instance.DisplayName;
                ValidPathArray.Add(RootStorageFolder.Instance.Path);
            }
            else
            {
                string HeaderText = Path.GetFileName(ValidPathArray.Last());

                if (string.IsNullOrEmpty(HeaderText))
                {
                    Item.Header = $"<{Globalization.GetString("UnknownText")}>";
                }
                else
                {
                    Item.Header = HeaderText;
                }
            }

            BaseFrame.Tag = ValidPathArray.ToArray();

            if (AnimationController.Current.IsEnableAnimation)
            {
                BaseFrame.Navigate(typeof(FileControl), new Tuple<TabViewItem, string[]>(Item, ValidPathArray.ToArray()), new DrillInNavigationTransitionInfo());
            }
            else
            {
                BaseFrame.Navigate(typeof(FileControl), new Tuple<TabViewItem, string[]>(Item, ValidPathArray.ToArray()), new SuppressNavigationTransitionInfo());
            }

            return Item;
        }

        private void Item_PointerCanceled(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            DelayPreviewCancel?.Cancel();
        }

        private void Item_PointerExited(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            DelayPreviewCancel?.Cancel();
        }

        private void Item_PointerEntered(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (SettingControl.IsTabPreviewEnabled)
            {
                DelayPreviewCancel?.Cancel();
                DelayPreviewCancel?.Dispose();
                DelayPreviewCancel = new CancellationTokenSource();

                Task.Delay(1000).ContinueWith((task, input) =>
                {
                    try
                    {
                        if (input is (CancellationTokenSource CancelSource, TabViewItem Item))
                        {
                            if (!CancelSource.IsCancellationRequested)
                            {
                                if (FlyoutBase.GetAttachedFlyout(Item) is Flyout PreviewFlyout)
                                {
                                    if (PreviewFlyout.Content is Image PreviewImage && PreviewImage.Source != null)
                                    {
                                        PreviewFlyout.ShowAt(Item, new FlyoutShowOptions
                                        {
                                            Placement = FlyoutPlacementMode.BottomEdgeAlignedLeft,
                                            ShowMode = FlyoutShowMode.TransientWithDismissOnPointerMoveAway
                                        });
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        LogTracer.Log(ex, "Could not render a preview image");
                    }
                }, (DelayPreviewCancel, (TabViewItem)sender), TaskScheduler.FromCurrentSynchronizationContext());
            }
        }

        private async void Item_DoubleTapped(object sender, Windows.UI.Xaml.Input.DoubleTappedRoutedEventArgs e)
        {
            if (sender is TabViewItem Tab)
            {
                await CleanUpAndRemoveTabItem(Tab).ConfigureAwait(false);
            }
        }

        private async void Item_PointerPressed(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (e.GetCurrentPoint(null).Properties.IsMiddleButtonPressed)
            {
                if (sender is TabViewItem Tab)
                {
                    await CleanUpAndRemoveTabItem(Tab).ConfigureAwait(false);
                }
            }
        }

        private async void Item_DragEnter(object sender, DragEventArgs e)
        {
            DragOperationDeferral Deferral = e.GetDeferral();

            try
            {
                if (e.DataView.Contains(StandardDataFormats.StorageItems))
                {
                    if (e.OriginalSource is TabViewItem Item)
                    {
                        TabViewControl.SelectedItem = Item;
                    }
                }
                else if (e.DataView.Contains(StandardDataFormats.Text))
                {
                    string XmlText = await e.DataView.GetTextAsync();

                    if (XmlText.Contains("RX-Explorer"))
                    {
                        XmlDocument Document = new XmlDocument();
                        Document.LoadXml(XmlText);

                        IXmlNode KindNode = Document.SelectSingleNode("/RX-Explorer/Kind");

                        if (KindNode?.InnerText == "RX-Explorer-TabItem")
                        {
                            e.AcceptedOperation = DataPackageOperation.Link;
                        }
                    }
                    else
                    {
                        e.AcceptedOperation = DataPackageOperation.None;
                    }
                }
            }
            catch
            {
                e.AcceptedOperation = DataPackageOperation.None;
            }
            finally
            {
                Deferral.Complete();
            }
        }

        private void TabViewControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            foreach (Frame Nav in TabCollection.Select((Item) => Item.Content as Frame))
            {
                Nav.Navigated -= Nav_Navigated;
            }

            if (TabViewControl.SelectedItem is TabViewItem Item)
            {
                if (Item.Content is Frame ContentFrame)
                {
                    CurrentNavigationControl = ContentFrame;
                    CurrentNavigationControl.Navigated += Nav_Navigated;

                    if (ContentFrame.Content is FileControl Control)
                    {
                        switch(Control.CurrentPresenter?.CurrentFolder)
                        {
                            case RootStorageFolder:
                                {
                                    LayoutModeControl.IsEnabled = false;
                                    break;
                                }
                            case FileSystemStorageFolder CurrentFolder:
                                {
                                    PathConfiguration Config = SQLite.Current.GetPathConfiguration(CurrentFolder.Path);

                                    LayoutModeControl.IsEnabled = true;
                                    LayoutModeControl.CurrentPath = CurrentFolder.Path;
                                    LayoutModeControl.ViewModeIndex = Config.DisplayModeIndex.GetValueOrDefault();
                                    break;
                                }
                        }
                    }
                }

                TaskBarController.SetText(Convert.ToString(Item.Header));
                MainPage.Current.NavView.IsBackEnabled = (MainPage.Current.SettingControl?.IsOpened).GetValueOrDefault() || CurrentNavigationControl.CanGoBack;
            }
        }

        private void Nav_Navigated(object sender, Windows.UI.Xaml.Navigation.NavigationEventArgs e)
        {
            MainPage.Current.NavView.IsBackEnabled = CurrentNavigationControl.CanGoBack;
        }

        private void TabViewControl_TabDragStarting(TabView sender, TabViewTabDragStartingEventArgs args)
        {
            XmlDocument Document = new XmlDocument();

            XmlElement RootElement = Document.CreateElement("RX-Explorer");
            Document.AppendChild(RootElement);

            XmlElement KindElement = Document.CreateElement("Kind");
            KindElement.InnerText = "RX-Explorer-TabItem";
            RootElement.AppendChild(KindElement);

            XmlElement ItemElement = Document.CreateElement("Item");
            RootElement.AppendChild(ItemElement);

            if (args.Tab.Content is Frame frame)
            {
                if (frame.Content is Home)
                {
                    ItemElement.InnerText = "Home||";
                }
                else
                {
                    if (args.Tab.Tag is FileControl Control)
                    {
                        ItemElement.InnerText = $"FileControl||{string.Join("||", Control.BladeViewer.Items.Cast<BladeItem>().Select((Item) => ((Item.Content as FilePresenter)?.CurrentFolder?.Path)))}";
                    }
                    else
                    {
                        args.Cancel = true;
                    }
                }
            }

            args.Data.SetText(Document.GetXml());
        }

        private async void TabViewControl_TabDragCompleted(TabView sender, TabViewTabDragCompletedEventArgs args)
        {
            if (args.DropResult == DataPackageOperation.Link)
            {
                await CleanUpAndRemoveTabItem(args.Tab).ConfigureAwait(false);
            }
        }

        private async void TabViewControl_TabDroppedOutside(TabView sender, TabViewTabDroppedOutsideEventArgs args)
        {
            if (sender.TabItems.Count > 1)
            {
                if (args.Tab.Content is Frame frame)
                {
                    if (frame.Content is Home)
                    {
                        await CleanUpAndRemoveTabItem(args.Tab);
                        await Launcher.LaunchUriAsync(new Uri($"rx-explorer:"));
                    }
                    else if (args.Tab.Tag is FileControl Control)
                    {
                        string StartupArgument = Uri.EscapeDataString(JsonSerializer.Serialize(new List<string[]>
                        {
                            Control.BladeViewer.Items.Cast<BladeItem>()
                                                     .Select((Item) => ((Item.Content as FilePresenter)?.CurrentFolder?.Path))
                                                     .ToArray()
                        }));

                        await CleanUpAndRemoveTabItem(args.Tab);
                        await Launcher.LaunchUriAsync(new Uri($"rx-explorer:{StartupArgument}"));
                    }
                }
            }
        }

        private async void TabViewControl_TabStripDragOver(object sender, DragEventArgs e)
        {
            DragOperationDeferral Deferral = e.GetDeferral();

            try
            {
                if (e.DataView.Contains(StandardDataFormats.Text))
                {
                    string XmlText = await e.DataView.GetTextAsync();

                    if (XmlText.Contains("RX-Explorer"))
                    {
                        XmlDocument Document = new XmlDocument();
                        Document.LoadXml(XmlText);

                        IXmlNode KindNode = Document.SelectSingleNode("/RX-Explorer/Kind");

                        if (KindNode?.InnerText == "RX-Explorer-TabItem")
                        {
                            e.AcceptedOperation = DataPackageOperation.Link;
                            e.Handled = true;
                        }
                    }
                    else
                    {
                        e.AcceptedOperation = DataPackageOperation.None;
                    }
                }
            }
            catch
            {
                e.AcceptedOperation = DataPackageOperation.None;
            }
            finally
            {
                Deferral.Complete();
            }
        }

        private async void TabViewControl_TabStripDrop(object sender, DragEventArgs e)
        {
            DragOperationDeferral Deferral = e.GetDeferral();

            try
            {
                if (e.DataView.Contains(StandardDataFormats.Text))
                {
                    string XmlText = await e.DataView.GetTextAsync();

                    if (XmlText.Contains("RX-Explorer"))
                    {
                        XmlDocument Document = new XmlDocument();
                        Document.LoadXml(XmlText);

                        IXmlNode KindNode = Document.SelectSingleNode("/RX-Explorer/Kind");

                        if (KindNode?.InnerText == "RX-Explorer-TabItem" && Document.SelectSingleNode("/RX-Explorer/Item") is IXmlNode ItemNode)
                        {
                            string[] Split = ItemNode.InnerText.Split("||", StringSplitOptions.RemoveEmptyEntries);

                            int InsertIndex = TabCollection.Count;

                            for (int i = 0; i < TabCollection.Count; i++)
                            {
                                TabViewItem Item = TabViewControl.ContainerFromIndex(i) as TabViewItem;

                                Windows.Foundation.Point Position = e.GetPosition(Item);

                                if (Position.X < Item.ActualWidth)
                                {
                                    if (Position.X < Item.ActualWidth / 2)
                                    {
                                        InsertIndex = i;
                                        break;
                                    }
                                    else
                                    {
                                        InsertIndex = i + 1;
                                        break;
                                    }
                                }
                            }

                            switch (Split[0])
                            {
                                case "Home":
                                    {
                                        await CreateNewTabAsync(InsertIndex);
                                        break;
                                    }
                                case "FileControl":
                                    {
                                        await CreateNewTabAsync(InsertIndex, Split.Skip(1).ToArray());
                                        break;
                                    }
                            }

                            e.Handled = true;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, "Error happened when try to drop a tab");
            }
            finally
            {
                Deferral.Complete();
            }
        }

        public async Task CleanUpAndRemoveTabItem(TabViewItem Tab)
        {
            if (Tab == null)
            {
                throw new ArgumentNullException(nameof(Tab), "Argument could not be null");
            }

            TabCollection.Remove(Tab);

            if (Tab.Content is Frame BaseFrame)
            {
                while (BaseFrame.CanGoBack)
                {
                    BaseFrame.GoBack();
                }
            }

            if (Tab.Tag is FileControl Control)
            {
                Control.Dispose();
            }

            Tab.DragEnter -= Item_DragEnter;
            Tab.PointerEntered -= Item_PointerEntered;
            Tab.PointerExited -= Item_PointerExited;
            Tab.PointerPressed -= Item_PointerPressed;
            Tab.PointerCanceled -= Item_PointerCanceled;
            Tab.DoubleTapped -= Item_DoubleTapped;
            Tab.Content = null;

            FullTrustProcessController.RequestResizeController(TabCollection.Count);

            if (TabCollection.Count == 0)
            {
                await ApplicationView.GetForCurrentView().TryConsolidateAsync();
            }
        }

        private void TabViewControl_PointerWheelChanged(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if ((e.OriginalSource as FrameworkElement).FindParentOfType<TabViewItem>() is TabViewItem)
            {
                int Delta = e.GetCurrentPoint(Frame).Properties.MouseWheelDelta;

                if (Delta > 0)
                {
                    if (TabViewControl.SelectedIndex > 0)
                    {
                        TabViewControl.SelectedIndex -= 1;
                    }
                }
                else
                {
                    if (TabViewControl.SelectedIndex < TabCollection.Count - 1)
                    {
                        TabViewControl.SelectedIndex += 1;
                    }
                }

                e.Handled = true;
            }
        }

        private void TabViewControl_RightTapped(object sender, Windows.UI.Xaml.Input.RightTappedRoutedEventArgs e)
        {
            if ((e.OriginalSource as FrameworkElement).FindParentOfType<TabViewItem>() is TabViewItem Item)
            {
                TabViewControl.SelectedItem = Item;

                FlyoutShowOptions Option = new FlyoutShowOptions
                {
                    Position = e.GetPosition(Item)
                };

                TabCommandFlyout?.ShowAt(Item, Option);
            }
        }

        private async void CloseThisTab_Click(object sender, RoutedEventArgs e)
        {
            if (TabViewControl.SelectedItem is TabViewItem Item)
            {
                await CleanUpAndRemoveTabItem(Item);
            }
        }

        private async void CloseButThis_Click(object sender, RoutedEventArgs e)
        {
            if (TabViewControl.SelectedItem is TabViewItem Item)
            {
                List<TabViewItem> ToBeRemoveList = TabCollection.ToList();

                ToBeRemoveList.Remove(Item);

                foreach (TabViewItem RemoveItem in ToBeRemoveList)
                {
                    await CleanUpAndRemoveTabItem(RemoveItem);
                }
            }
        }

        private void TaskListPanelButton_Click(object sender, RoutedEventArgs e)
        {
            TaskListPanel.IsPaneOpen = true;
        }

        private void PinTaskListPanel_Click(object sender, RoutedEventArgs e)
        {
            if (TaskListPanel.DisplayMode == SplitViewDisplayMode.Overlay)
            {
                TaskListPanel.DisplayMode = SplitViewDisplayMode.Inline;

                PinTaskListPanel.Content = new Viewbox
                {
                    Child = new FontIcon
                    {
                        Glyph = "\uE77A"
                    }
                };

                ApplicationData.Current.LocalSettings.Values["ShouldPinTaskList"] = true;
            }
            else
            {
                TaskListPanel.DisplayMode = SplitViewDisplayMode.Overlay;

                PinTaskListPanel.Content = new Viewbox
                {
                    Child = new FontIcon
                    {
                        Glyph = "\uE840"
                    }
                };

                ApplicationData.Current.LocalSettings.Values["ShouldPinTaskList"] = false;
            }
        }

        private void CancelTaskButton_Click(object sender, RoutedEventArgs e)
        {
            if (((Button)sender).DataContext is OperationListBaseModel Model)
            {
                if (Model.CanBeCancelled)
                {
                    Model.UpdateStatus(OperationStatus.Cancelling);
                }
            }
        }

        private void RemoveTaskButton_Click(object sender, RoutedEventArgs e)
        {
            if (((Button)sender).DataContext is OperationListBaseModel Model)
            {
                QueueTaskController.ListItemSource.Remove(Model);
            }
        }

        private void ClearTaskListPanel_Click(object sender, RoutedEventArgs e)
        {
            foreach (OperationListBaseModel Model in QueueTaskController.ListItemSource.Where((Item) => Item.Status is OperationStatus.Cancelled or OperationStatus.Completed or OperationStatus.Error).ToArray())
            {
                QueueTaskController.ListItemSource.Remove(Model);
            }
        }

        private async void CloseTabOnRight_Click(object sender, RoutedEventArgs e)
        {
            if (TabViewControl.SelectedItem is TabViewItem Item)
            {
                int CurrentIndex = TabCollection.IndexOf(Item);

                foreach (TabViewItem RemoveItem in TabCollection.Skip(CurrentIndex + 1).Reverse().ToArray())
                {
                    await CleanUpAndRemoveTabItem(RemoveItem);
                }
            }
        }

        private async void VerticalSplitViewButton_Click(object sender, RoutedEventArgs e)
        {
            if (CurrentNavigationControl?.Content is FileControl Control)
            {
                if (await MSStoreHelper.Current.CheckPurchaseStatusAsync())
                {
                    await Control.CreateNewBladeAsync(Control.CurrentPresenter.CurrentFolder.Path).ConfigureAwait(false);
                }
                else
                {
                    VerticalSplitTip.IsOpen = true;
                }
            }
        }

        private async void VerticalSplitTip_ActionButtonClick(TeachingTip sender, object args)
        {
            sender.IsOpen = false;

            switch (await MSStoreHelper.Current.PurchaseAsync())
            {
                case StorePurchaseStatus.Succeeded:
                    {
                        QueueContentDialog QueueContenDialog = new QueueContentDialog
                        {
                            Title = Globalization.GetString("Common_Dialog_TipTitle"),
                            Content = Globalization.GetString("QueueDialog_Store_PurchaseSuccess_Content"),
                            CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                        };

                        await QueueContenDialog.ShowAsync();

                        break;
                    }
                case StorePurchaseStatus.AlreadyPurchased:
                    {
                        QueueContentDialog QueueContenDialog = new QueueContentDialog
                        {
                            Title = Globalization.GetString("Common_Dialog_TipTitle"),
                            Content = Globalization.GetString("QueueDialog_Store_AlreadyPurchase_Content"),
                            CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                        };

                        await QueueContenDialog.ShowAsync();

                        break;
                    }
                case StorePurchaseStatus.NotPurchased:
                    {
                        QueueContentDialog QueueContenDialog = new QueueContentDialog
                        {
                            Title = Globalization.GetString("Common_Dialog_TipTitle"),
                            Content = Globalization.GetString("QueueDialog_Store_NotPurchase_Content"),
                            CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                        };

                        await QueueContenDialog.ShowAsync();

                        break;
                    }
                default:
                    {
                        QueueContentDialog QueueContenDialog = new QueueContentDialog
                        {
                            Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                            Content = Globalization.GetString("QueueDialog_Store_NetworkError_Content"),
                            CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                        };

                        await QueueContenDialog.ShowAsync();

                        break;
                    }
            }
        }
    }
}
