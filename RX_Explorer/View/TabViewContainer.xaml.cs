using Microsoft.Toolkit.Deferred;
using Microsoft.UI.Xaml.Controls;
using RX_Explorer.Class;
using RX_Explorer.Dialog;
using RX_Explorer.SeparateWindow.PropertyWindow;
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
using System.Timers;
using Windows.ApplicationModel.Core;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.Services.Store;
using Windows.Storage.Streams;
using Windows.System;
using Windows.UI.Core;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Media.Imaging;
using NavigationViewItem = Microsoft.UI.Xaml.Controls.NavigationViewItem;
using SymbolIconSource = Microsoft.UI.Xaml.Controls.SymbolIconSource;
using TabView = Microsoft.UI.Xaml.Controls.TabView;
using TabViewTabCloseRequestedEventArgs = Microsoft.UI.Xaml.Controls.TabViewTabCloseRequestedEventArgs;
using Timer = System.Timers.Timer;

namespace RX_Explorer.View
{
    public sealed partial class TabViewContainer : Page
    {
        public static TabViewContainer Current { get; private set; }

        public TabItemContentRenderer CurrentTabRenderer { get; private set; }

        public LayoutModeController LayoutModeControl { get; } = new LayoutModeController();

        public ObservableCollection<TabViewItem> TabCollection { get; } = new ObservableCollection<TabViewItem>();

        private readonly Timer PreviewTimer = new Timer(5000)
        {
            AutoReset = true,
            Enabled = true
        };

        private CancellationTokenSource DelayPreviewCancel;

        public TabViewContainer()
        {
            InitializeComponent();

            Current = this;

            Loaded += TabViewContainer_Loaded;
            PreviewTimer.Elapsed += PreviewTimer_Tick;
            TabCollection.CollectionChanged += TabCollection_CollectionChanged;

            CoreApplication.MainView.CoreWindow.Dispatcher.AcceleratorKeyActivated += Dispatcher_AcceleratorKeyActivated;
            CoreApplication.MainView.CoreWindow.PointerPressed += TabViewContainer_PointerPressed;
            CoreApplication.MainView.CoreWindow.KeyDown += TabViewContainer_KeyDown;
            CommonAccessCollection.LibraryNotFound += CommonAccessCollection_LibraryNotFound;
            QueueTaskController.ListItemSource.CollectionChanged += ListItemSource_CollectionChanged;
            QueueTaskController.ProgressChanged += QueueTaskController_ProgressChanged;
        }

        private async void TabCollection_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            await FullTrustProcessController.SetExpectedControllerNumAsync(TabCollection.Count);
        }

        private async void PreviewTimer_Tick(object sender, ElapsedEventArgs e)
        {
            if (SettingPage.IsTabPreviewEnabled)
            {
                PreviewTimer.Enabled = false;

                await Dispatcher.RunAsync(CoreDispatcherPriority.Low, async () =>
                {
                    try
                    {
                        if (TabViewControl.SelectedItem is TabViewItem Item
                            && Item.IsLoaded
                            && Item.Content is UIElement Element)
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
                        PreviewTimer.Enabled = true;
                    }
                });
            }
        }

        private async void QueueTaskController_ProgressChanged(object sender, ProgressChangedDeferredArgs e)
        {
            EventDeferral Deferral = e.GetDeferral();

            try
            {
                await TaskBarController.SetTaskBarProgressAsync(e.ProgressValue);

                await Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                {
                    TaskListProgress.Value = e.ProgressValue;

                    if (e.ProgressValue >= 100)
                    {
                        _ = Task.Delay(800).ContinueWith((_) => TaskListProgress.Visibility = Visibility.Collapsed, TaskScheduler.FromCurrentSynchronizationContext());
                    }
                    else
                    {
                        TaskListProgress.Visibility = Visibility.Visible;
                        TaskListBadge.Value = QueueTaskController.ListItemSource.Count((Item) => Item.Status is OperationStatus.Preparing or OperationStatus.Processing or OperationStatus.Waiting or OperationStatus.NeedAttention);
                    }
                });
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, "Could not update the progress as expected");
            }
            finally
            {
                Deferral.Complete();
            }
        }

        private void ListItemSource_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            TaskListBadge.Value = QueueTaskController.ListItemSource.Count((Item) => Item.Status is OperationStatus.Preparing or OperationStatus.Processing or OperationStatus.Waiting or OperationStatus.NeedAttention);
        }

        private async void CommonAccessCollection_LibraryNotFound(object sender, IEnumerable<string> ErrorList)
        {
            QueueContentDialog Dialog = new QueueContentDialog
            {
                Title = Globalization.GetString("Common_Dialog_WarningTitle"),
                Content = Globalization.GetString("QueueDialog_PinFolderNotFound_Content") + Environment.NewLine + string.Join(Environment.NewLine, ErrorList),
                PrimaryButtonText = Globalization.GetString("Common_Dialog_ConfirmButton"),
                CloseButtonText = Globalization.GetString("Common_Dialog_CancelButton")
            };

            if (await Dialog.ShowAsync() == ContentDialogResult.Primary)
            {
                foreach (string ErrorPath in ErrorList)
                {
                    SQLite.Current.DeleteLibraryFolder(ErrorPath);
                }
            }
        }

        private async void Dispatcher_AcceleratorKeyActivated(CoreDispatcher sender, AcceleratorKeyEventArgs args)
        {
            if (Enum.GetName(typeof(CoreAcceleratorKeyEventType), args.EventType).Contains("KeyUp")
                && args.KeyStatus.IsMenuKeyDown
                && CurrentTabRenderer.RendererFrame.Content is FileControl Control
                && !Control.ShouldNotAcceptShortcutKeyInput
                && !QueueContentDialog.IsRunningOrWaiting
                && MainPage.Current.NavView.SelectedItem is NavigationViewItem NavItem
                && Convert.ToString(NavItem.Content) == Globalization.GetString("MainPage_PageDictionary_Home_Label")
                )
            {
                switch (args.VirtualKey)
                {
                    case VirtualKey.Left:
                        {
                            args.Handled = true;

                            await Control.ExecuteGoBackActionIfAvailable();

                            break;
                        }
                    case VirtualKey.Right:
                        {
                            args.Handled = true;

                            await Control.ExecuteGoForwardActionIfAvailable();

                            break;
                        }
                    case VirtualKey.Enter:
                        {
                            args.Handled = true;

                            PropertiesWindowBase NewWindow = null;

                            if (Control.CurrentPresenter.CurrentFolder is RootStorageFolder)
                            {
                                Home HomeControl = Control.CurrentPresenter.RootFolderControl;

                                if (HomeControl.LibraryGrid.SelectedItem is LibraryStorageFolder LibFolder)
                                {
                                    NewWindow = await PropertiesWindowBase.CreateAsync(LibFolder);
                                }
                                else if (HomeControl.DriveGrid.SelectedItem is DriveDataBase Drive)
                                {
                                    NewWindow = await PropertiesWindowBase.CreateAsync(Drive);
                                }
                            }
                            else if (Control.CurrentPresenter.SelectedItems.Count > 0)
                            {
                                NewWindow = await PropertiesWindowBase.CreateAsync(Control.CurrentPresenter.SelectedItems.Cast<FileSystemStorageItemBase>().ToArray());
                            }

                            if (NewWindow != null)
                            {
                                await NewWindow.ShowAsync(new Point(Window.Current.Bounds.Width / 2 - 200, Window.Current.Bounds.Height / 2 - 300));
                            }

                            break;
                        }
                }
            }
        }

        private async void TabViewContainer_KeyDown(CoreWindow sender, KeyEventArgs args)
        {
            try
            {
                if (!QueueContentDialog.IsRunningOrWaiting)
                {
                    bool CtrlDown = sender.GetKeyState(VirtualKey.Control).HasFlag(CoreVirtualKeyStates.Down);
                    bool ShiftDown = sender.GetKeyState(VirtualKey.Shift).HasFlag(CoreVirtualKeyStates.Down);

                    switch (args.VirtualKey)
                    {
                        case VirtualKey.Tab when CtrlDown && ShiftDown && TabCollection.Count > 1:
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
                        case VirtualKey.Tab when CtrlDown && TabCollection.Count > 1:
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
                        case VirtualKey.W when CtrlDown && TabViewControl.SelectedItem is TabViewItem Tab:
                            {
                                args.Handled = true;

                                await CleanUpAndRemoveTabItem(Tab);

                                break;
                            }
                        case VirtualKey.PageUp when CtrlDown && TabCollection.Count > 1:
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
                        case VirtualKey.PageDown when CtrlDown && TabCollection.Count > 1:
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
                                if (CurrentTabRenderer?.RendererFrame.Content is FileControl Control && Control.CurrentPresenter?.CurrentFolder is RootStorageFolder)
                                {
                                    Home HomeControl = Control.CurrentPresenter.RootFolderControl;

                                    switch (args.VirtualKey)
                                    {
                                        case VirtualKey.U when CtrlDown:
                                            {
                                                if (HomeControl.DriveGrid.SelectedItem is LockedDriveData LockedDrive)
                                                {
                                                Retry:
                                                    try
                                                    {
                                                        BitlockerPasswordDialog Dialog = new BitlockerPasswordDialog();

                                                        if (await Dialog.ShowAsync() == ContentDialogResult.Primary)
                                                        {
                                                            if (!await LockedDrive.UnlockAsync(Dialog.Password))
                                                            {
                                                                throw new UnlockDriveFailedException();
                                                            }

                                                            if (await DriveDataBase.CreateAsync(LockedDrive) is DriveDataBase RefreshedDrive)
                                                            {
                                                                if (RefreshedDrive is LockedDriveData)
                                                                {
                                                                    throw new UnlockDriveFailedException();
                                                                }
                                                                else
                                                                {
                                                                    int Index = CommonAccessCollection.DriveList.IndexOf(LockedDrive);

                                                                    if (Index >= 0)
                                                                    {
                                                                        CommonAccessCollection.DriveList.Remove(LockedDrive);
                                                                        CommonAccessCollection.DriveList.Insert(Index, RefreshedDrive);
                                                                    }
                                                                    else
                                                                    {
                                                                        CommonAccessCollection.DriveList.Add(RefreshedDrive);
                                                                    }
                                                                }
                                                            }
                                                            else
                                                            {
                                                                throw new UnauthorizedAccessException(LockedDrive.Path);
                                                            }
                                                        }
                                                    }
                                                    catch (UnlockDriveFailedException)
                                                    {
                                                        QueueContentDialog UnlockFailedDialog = new QueueContentDialog
                                                        {
                                                            Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                                            Content = Globalization.GetString("QueueDialog_UnlockBitlockerFailed_Content"),
                                                            PrimaryButtonText = Globalization.GetString("Common_Dialog_RetryButton"),
                                                            CloseButtonText = Globalization.GetString("Common_Dialog_CancelButton")
                                                        };

                                                        if (await UnlockFailedDialog.ShowAsync() == ContentDialogResult.Primary)
                                                        {
                                                            goto Retry;
                                                        }
                                                        else
                                                        {
                                                            return;
                                                        }
                                                    }
                                                }

                                                break;
                                            }
                                        case VirtualKey.Space when SettingPage.IsQuicklookEnabled && !SettingPage.IsOpened:
                                            {
                                                args.Handled = true;

                                                using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableControllerAsync())
                                                {
                                                    if (await Exclusive.Controller.CheckIfQuicklookIsAvaliableAsync())
                                                    {
                                                        if (HomeControl.DriveGrid.SelectedItem is DriveDataBase Device && !string.IsNullOrEmpty(Device.Path))
                                                        {
                                                            await Exclusive.Controller.ToggleQuicklookAsync(Device.Path);
                                                        }
                                                        else if (HomeControl.LibraryGrid.SelectedItem is LibraryStorageFolder Library && !string.IsNullOrEmpty(Library.Path))
                                                        {
                                                            await Exclusive.Controller.ToggleQuicklookAsync(Library.Path);
                                                        }
                                                    }
                                                }

                                                break;
                                            }
                                        case VirtualKey.B when CtrlDown:
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
                                        case VirtualKey.Q when CtrlDown:
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
                                        case VirtualKey.T when CtrlDown:
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

                                                    HomeControl.OpenTargetFolder(string.IsNullOrEmpty(Drive.Path) ? Drive.DeviceId : Drive.Path);
                                                }
                                                else if (HomeControl.LibraryGrid.SelectedItem is LibraryStorageFolder Library)
                                                {
                                                    args.Handled = true;

                                                    HomeControl.OpenTargetFolder(Library.Path);
                                                }

                                                break;
                                            }
                                        case VirtualKey.F5:
                                            {
                                                args.Handled = true;

                                                await CommonAccessCollection.LoadDriveAsync(true);

                                                break;
                                            }
                                        case VirtualKey.T when CtrlDown:
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
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"An exception was threw in {nameof(TabViewContainer_KeyDown)}");
            }
        }

        private async void TabViewContainer_PointerPressed(CoreWindow sender, PointerEventArgs args)
        {
            bool BackButtonPressed = args.CurrentPoint.Properties.IsXButton1Pressed;
            bool ForwardButtonPressed = args.CurrentPoint.Properties.IsXButton2Pressed;

            if (CurrentTabRenderer?.RendererFrame.Content is FileControl Control)
            {
                if (BackButtonPressed)
                {
                    args.Handled = true;

                    if (!QueueContentDialog.IsRunningOrWaiting)
                    {
                        if (Control.GoBackRecord.IsEnabled)
                        {
                            await Control.ExecuteGoBackActionIfAvailable();
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

                    if (Control.GoForwardRecord.IsEnabled)
                    {
                        await Control.ExecuteGoForwardActionIfAvailable();
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
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, "An exception was threw when trying to create a new tab");
            }
            finally
            {
                await Task.Delay(200).ContinueWith((_) => TabViewControl.SelectedIndex = TabCollection.Count - 1, TaskScheduler.FromCurrentSynchronizationContext());
            }
        }

        public async Task CreateNewTabAsync(params string[] PathArray)
        {
            try
            {
                TabCollection.Add(await CreateNewTabCoreAsync(PathArray));
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, "An exception was threw when trying to create a new tab");
            }
            finally
            {
                await Task.Delay(200).ContinueWith((_) => TabViewControl.SelectedIndex = TabCollection.Count - 1, TaskScheduler.FromCurrentSynchronizationContext());
            }
        }

        public async Task CreateNewTabAsync(int InsertIndex, params string[] PathArray)
        {
            int Index = Math.Min(Math.Max(0, InsertIndex), TabCollection.Count);

            try
            {
                TabCollection.Insert(Index, await CreateNewTabCoreAsync(PathArray));
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, "An exception was threw when trying to create a new tab");
            }
            finally
            {
                await Task.Delay(200).ContinueWith((_) => TabViewControl.SelectedIndex = Index, TaskScheduler.FromCurrentSynchronizationContext());
            }
        }

        private async void TabViewContainer_Loaded(object sender, RoutedEventArgs e)
        {
            Loaded -= TabViewContainer_Loaded;

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

            if (SettingPage.LibraryExpanderIsExpanded)
            {
                LoadTaskList.Add(CommonAccessCollection.LoadLibraryFoldersAsync());
            }

            await Task.WhenAll(LoadTaskList).ContinueWith((_) => PreviewTimer.Start(), TaskScheduler.FromCurrentSynchronizationContext()).ConfigureAwait(false);
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
            TextBlock Header = new TextBlock
            {
                TextTrimming = TextTrimming.CharacterEllipsis,
                TextWrapping = TextWrapping.NoWrap,
                FontFamily = Application.Current.Resources["ContentControlThemeFontFamily"] as FontFamily
            };

            TabViewItem Tab = new TabViewItem
            {
                IsTabStop = false,
                AllowDrop = true,
                IsDoubleTapEnabled = true,
                IconSource = new SymbolIconSource { Symbol = Symbol.Document },
                Header = Header
            };
            Tab.DragEnter += Tab_DragEnter;
            Tab.PointerEntered += Tab_PointerEntered;
            Tab.PointerExited += Tab_PointerExited;
            Tab.PointerPressed += Tab_PointerPressed;
            Tab.PointerCanceled += Tab_PointerCanceled;
            Tab.DoubleTapped += Tab_DoubleTapped;

            TextBlock HeaderTooltipText = new TextBlock();

            HeaderTooltipText.SetBinding(TextBlock.TextProperty, new Binding
            {
                Source = Header,
                Path = new PropertyPath("Text"),
                Mode = BindingMode.OneWay
            });

            ToolTip HeaderTooltip = new ToolTip
            {
                Content = HeaderTooltipText
            };

            HeaderTooltip.SetBinding(VisibilityProperty, new Binding
            {
                Source = Header,
                Path = new PropertyPath("IsTextTrimmed"),
                Mode = BindingMode.OneWay
            });

            ToolTipService.SetToolTip(Tab, HeaderTooltip);

            Style PreviewFlyoutStyle = new Style(typeof(FlyoutPresenter));
            PreviewFlyoutStyle.Setters.Add(new Setter(MaxHeightProperty, 400));
            PreviewFlyoutStyle.Setters.Add(new Setter(MaxWidthProperty, 600));
            PreviewFlyoutStyle.Setters.Add(new Setter(PaddingProperty, 0));
            PreviewFlyoutStyle.Setters.Add(new Setter(CornerRadiusProperty, (CornerRadius)Application.Current.Resources["CustomCornerRadius"]));

            Flyout PreviewFlyout = new Flyout
            {
                FlyoutPresenterStyle = PreviewFlyoutStyle,
                Content = new Image
                {
                    Stretch = Stretch.Uniform,
                    Height = 380,
                    Width = 580
                }
            };

            FlyoutBase.SetAttachedFlyout(Tab, PreviewFlyout);

            List<string> ValidPathArray = new List<string>(PathForNewTab.Length);

            foreach (string Path in PathForNewTab.Where((Path) => !string.IsNullOrWhiteSpace(Path)))
            {
                if (RootStorageFolder.Current.Path.Equals(Path, StringComparison.OrdinalIgnoreCase)
                    || await FileSystemStorageItemBase.CheckExistsAsync(Path))
                {
                    ValidPathArray.Add(Path);
                }
            }

            if (Tab.Header is TextBlock HeaderBlock)
            {
                switch (ValidPathArray.Count)
                {
                    case 0:
                        {
                            HeaderBlock.Text = RootStorageFolder.Current.DisplayName;
                            break;
                        }
                    case 1:
                        {
                            string Path = ValidPathArray.First();

                            if (RootStorageFolder.Current.Path.Equals(Path, StringComparison.OrdinalIgnoreCase))
                            {
                                HeaderBlock.Text = RootStorageFolder.Current.DisplayName;
                            }
                            else
                            {
                                string HeaderText = System.IO.Path.GetFileName(Path);

                                if (string.IsNullOrEmpty(HeaderText))
                                {
                                    HeaderBlock.Text = $"<{Globalization.GetString("UnknownText")}>";
                                }
                                else
                                {
                                    HeaderBlock.Text = HeaderText;
                                }
                            }

                            break;
                        }
                    default:
                        {
                            HeaderBlock.Text = string.Join(" | ", ValidPathArray.Select((Path) => RootStorageFolder.Current.Path.Equals(Path, StringComparison.OrdinalIgnoreCase) ? RootStorageFolder.Current.DisplayName : System.IO.Path.GetFileName(Path)));
                            break;
                        }
                }
            }

            Tab.Content = new Frame { Content = new TabItemContentRenderer(Tab, ValidPathArray.ToArray()) };

            return Tab;
        }

        private void Tab_DragEnter(object sender, DragEventArgs e)
        {
            try
            {
                e.Handled = true;
                e.AcceptedOperation = DataPackageOperation.None;

                if (e.DataView.Contains(StandardDataFormats.StorageItems)
                    || e.DataView.Contains(ExtendedDataFormats.CompressionItems)
                    || e.DataView.Contains(ExtendedDataFormats.NotSupportedStorageItem))
                {
                    if (e.OriginalSource is TabViewItem Item)
                    {
                        TabViewControl.SelectedItem = Item;
                    }
                }
                else if (e.DataView.Contains(ExtendedDataFormats.TabItem))
                {
                    e.AcceptedOperation = DataPackageOperation.Copy;
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, "An exception was threw when trying fetch the clipboard data");
            }
        }

        private void Tab_PointerCanceled(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            DelayPreviewCancel?.Cancel();
        }

        private void Tab_PointerExited(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            DelayPreviewCancel?.Cancel();
        }

        private void Tab_PointerEntered(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (sender is TabViewItem Item && SettingPage.IsTabPreviewEnabled)
            {
                DelayPreviewCancel?.Cancel();
                DelayPreviewCancel?.Dispose();
                DelayPreviewCancel = new CancellationTokenSource();

                Task.Delay(1000).ContinueWith((task, input) =>
                {
                    try
                    {
                        if (input is (CancellationToken CancelToken, TabViewItem Item))
                        {
                            if (!CancelToken.IsCancellationRequested)
                            {
                                if (FlyoutBase.GetAttachedFlyout(Item) is Flyout PreviewFlyout)
                                {
                                    if (PreviewFlyout.Content is Image PreviewImage && PreviewImage.Source != null)
                                    {
                                        PreviewFlyout.ShowAt(Item, new FlyoutShowOptions
                                        {
                                            Placement = FlyoutPlacementMode.Bottom,
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
                }, (DelayPreviewCancel.Token, Item), TaskScheduler.FromCurrentSynchronizationContext());
            }
        }

        private async void Tab_DoubleTapped(object sender, Windows.UI.Xaml.Input.DoubleTappedRoutedEventArgs e)
        {
            if (sender is TabViewItem Tab)
            {
                await CleanUpAndRemoveTabItem(Tab);
            }
        }

        private async void Tab_PointerPressed(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (e.GetCurrentPoint(null).Properties.IsMiddleButtonPressed)
            {
                if (sender is TabViewItem Tab)
                {
                    await CleanUpAndRemoveTabItem(Tab);
                }
            }
        }

        private void TabViewControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (TabViewControl.SelectedItem is TabViewItem Tab)
            {
                if (Tab.Header is TextBlock HeaderBlock)
                {
                    TaskBarController.SetText(HeaderBlock.Text);
                }

                if (Tab.Content is Frame RootFrame && RootFrame.Content is TabItemContentRenderer Renderer)
                {
                    CurrentTabRenderer = Renderer;

                    MainPage.Current.NavView.IsBackEnabled = Renderer.RendererFrame.CanGoBack;

                    if (Renderer.RendererFrame.Content is FileControl Control)
                    {
                        switch (Control.CurrentPresenter?.CurrentFolder)
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
            }
        }

        private void TabViewControl_TabDragStarting(TabView sender, TabViewTabDragStartingEventArgs args)
        {
            args.Data.RequestedOperation = DataPackageOperation.Copy;

            if (args.Tab.Content is Frame RootFrame && RootFrame.Content is TabItemContentRenderer Renderer)
            {
                if (Renderer.RendererFrame.Content is Home)
                {
                    args.Data.SetData(ExtendedDataFormats.TabItem, new MemoryStream(Encoding.Unicode.GetBytes(JsonSerializer.Serialize(Array.Empty<string>()))).AsRandomAccessStream());
                }
                else if (Renderer.Presenters.Any())
                {
                    args.Data.SetData(ExtendedDataFormats.TabItem, new MemoryStream(Encoding.Unicode.GetBytes(JsonSerializer.Serialize(Renderer.Presenters.Select((Presenter) => Presenter.CurrentFolder?.Path)))).AsRandomAccessStream());
                }
                else
                {
                    args.Cancel = true;
                }
            }
        }

        private void TabViewControl_TabStripDragOver(object sender, DragEventArgs e)
        {
            try
            {
                e.Handled = true;
                e.AcceptedOperation = DataPackageOperation.None;

                if (e.DataView.Contains(ExtendedDataFormats.TabItem))
                {
                    e.AcceptedOperation = DataPackageOperation.Copy;
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, "An exception was threw when trying fetch the clipboard data");
            }
        }

        private async void TabViewControl_TabStripDrop(object sender, DragEventArgs e)
        {
            DragOperationDeferral Deferral = e.GetDeferral();

            try
            {
                e.Handled = true;

                if (e.DataView.Contains(ExtendedDataFormats.TabItem))
                {
                    if (await e.DataView.GetDataAsync(ExtendedDataFormats.TabItem) is IRandomAccessStream RandomStream)
                    {
                        using (StreamReader Reader = new StreamReader(RandomStream.AsStreamForRead(), Encoding.Unicode, true, 512, true))
                        {
                            string RawText = Reader.ReadToEnd();

                            if (!string.IsNullOrEmpty(RawText))
                            {
                                IEnumerable<string> PathArray = JsonSerializer.Deserialize<IEnumerable<string>>(RawText);

                                int InsertIndex = TabCollection.Count;

                                for (int i = 0; i < TabCollection.Count; i++)
                                {
                                    if (TabViewControl.ContainerFromIndex(i) is TabViewItem Tab)
                                    {
                                        Point Position = e.GetPosition(Tab);

                                        if (Position.X < Tab.ActualWidth)
                                        {
                                            if (Position.X < Tab.ActualWidth / 2)
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
                                }

                                if (PathArray.Any())
                                {
                                    await CreateNewTabAsync(InsertIndex, PathArray.Where((Path) => !string.IsNullOrWhiteSpace(Path)).ToArray());
                                }
                                else
                                {
                                    await CreateNewTabAsync(InsertIndex);
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, "An exception was threw when trying to drop a tab");
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

            try
            {
                if (TabCollection.Remove(Tab))
                {
                    if (Tab.Content is Frame RootFrame && RootFrame.Content is TabItemContentRenderer Renderer)
                    {
                        while (Renderer.RendererFrame.CanGoBack)
                        {
                            Renderer.RendererFrame.GoBack(new SuppressNavigationTransitionInfo());
                        }

                        Renderer.Dispose();
                    }

                    Tab.DragEnter -= Tab_DragEnter;
                    Tab.PointerEntered -= Tab_PointerEntered;
                    Tab.PointerExited -= Tab_PointerExited;
                    Tab.PointerPressed -= Tab_PointerPressed;
                    Tab.PointerCanceled -= Tab_PointerCanceled;
                    Tab.DoubleTapped -= Tab_DoubleTapped;
                    Tab.Content = null;

                    if (TabCollection.Count == 0)
                    {
                        if (StartupModeController.Mode == StartupMode.LastOpenedTab)
                        {
                            StartupModeController.SetLastOpenedPath(Enumerable.Empty<string[]>());
                        }

                        if (!await ApplicationView.GetForCurrentView().TryConsolidateAsync())
                        {
                            Application.Current.Exit();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, "Could not close the tab and cleanup the resource correctly");
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

                TabCommandFlyout?.ShowAt(Item, new FlyoutShowOptions
                {
                    Position = e.GetPosition(Item),
                    Placement = FlyoutPlacementMode.BottomEdgeAlignedLeft,
                    ShowMode = FlyoutShowMode.Standard
                });
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
                IReadOnlyList<TabViewItem> ToBeRemoveList = TabCollection.ToList();

                foreach (TabViewItem RemoveItem in ToBeRemoveList.Except(new TabViewItem[] { Item }))
                {
                    await CleanUpAndRemoveTabItem(RemoveItem);
                }
            }
        }

        private void TaskListPanelButton_Click(object sender, RoutedEventArgs e)
        {
            if (TabViewControl.SelectedItem is TabViewItem Tab && Tab.Content is Frame RootFrame && RootFrame.Content is TabItemContentRenderer Renderer)
            {
                Renderer.SetPanelOpenStatus(true);
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
            if (CurrentTabRenderer?.RendererFrame.Content is FileControl Control)
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

        private void TabViewControl_PreviewKeyDown(object sender, Windows.UI.Xaml.Input.KeyRoutedEventArgs e)
        {
            CoreWindow Window = CoreApplication.MainView.CoreWindow;

            bool CtrlDown = Window.GetKeyState(VirtualKey.Control).HasFlag(CoreVirtualKeyStates.Down);
            bool ShiftDown = Window.GetKeyState(VirtualKey.Shift).HasFlag(CoreVirtualKeyStates.Down);

            switch (e.Key)
            {
                case VirtualKey.Tab when ((CtrlDown && ShiftDown) || CtrlDown) && TabCollection.Count > 1:
                    {
                        e.Handled = true;
                        break;
                    }
            }
        }

        private void ViewModeList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ViewModeFlyout.IsOpen)
            {
                ViewModeFlyout.Hide();
            }
        }
    }
}
