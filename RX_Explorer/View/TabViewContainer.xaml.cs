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
using Windows.ApplicationModel;
using Windows.ApplicationModel.Core;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.Services.Store;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.System;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;
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

        public IReadOnlyList<string[]> OpenedPathList
        {
            get
            {
                List<string[]> PathList = new List<string[]>(TabCollection.Count);

                foreach (TabItemContentRenderer Renderer in TabCollection.Select((Tab) => Tab.Content)
                                                                         .Cast<Frame>()
                                                                         .Select((Frame) => Frame.Content)
                                                                         .Cast<TabItemContentRenderer>())
                {
                    string[] CurrentPathArray = Renderer.Presenters.Select((Presenter) => Presenter.CurrentFolder?.Path)
                                                                   .Where((Path) => !string.IsNullOrWhiteSpace(Path))
                                                                   .ToArray();

                    if (CurrentPathArray.Length > 0)
                    {
                        PathList.Add(CurrentPathArray);
                    }
                    else
                    {
                        PathList.Add(Renderer.InitializePaths.ToArray());
                    }
                }

                return PathList;
            }
        }

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
            Loaded += TabViewContainer_Loaded1;
            Unloaded += TabViewContainer_Unloaded;
            PreviewTimer.Elapsed += PreviewTimer_Tick;
            TabCollection.CollectionChanged += TabCollection_CollectionChanged;

            CommonAccessCollection.LibraryNotFound += CommonAccessCollection_LibraryNotFound;
            QueueTaskController.ListItemSource.CollectionChanged += ListItemSource_CollectionChanged;
            QueueTaskController.ProgressChanged += QueueTaskController_ProgressChanged;
        }

        private void TabViewContainer_Unloaded(object sender, RoutedEventArgs e)
        {
            CoreApplication.MainView.CoreWindow.KeyDown -= TabViewContainer_KeyDown;
            CoreApplication.MainView.CoreWindow.Dispatcher.AcceleratorKeyActivated -= Dispatcher_AcceleratorKeyActivated;
        }

        private void TabViewContainer_Loaded1(object sender, RoutedEventArgs e)
        {
            CoreApplication.MainView.CoreWindow.KeyDown += TabViewContainer_KeyDown;
            CoreApplication.MainView.CoreWindow.Dispatcher.AcceleratorKeyActivated += Dispatcher_AcceleratorKeyActivated;
        }

        private async void TabCollection_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            await AuxiliaryTrustProcessController.SetExpectedControllerNumAsync(TabCollection.Count);
            await MonitorTrustProcessController.SetRecoveryDataAsync(JsonSerializer.Serialize(OpenedPathList));
        }

        private async void PreviewTimer_Tick(object sender, ElapsedEventArgs e)
        {
            if (SettingPage.IsTabPreviewEnabled)
            {
                PreviewTimer.Enabled = false;

                try
                {
                    await Dispatcher.RunAndWaitAsyncTask(CoreDispatcherPriority.Low, async () =>
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
                    });
                }
                catch (Exception)
                {
                    LogTracer.Log("Could not render a preview image for the tab control");
                }
                finally
                {
                    PreviewTimer.Enabled = true;
                }
            }
        }

        private async void QueueTaskController_ProgressChanged(object sender, ProgressChangedDeferredArgs e)
        {
            EventDeferral Deferral = e.GetDeferral();

            try
            {
                await TaskBarController.SetTaskBarProgressAsync(e.ProgressValue);

                await Dispatcher.RunAndWaitAsyncTask(CoreDispatcherPriority.Low, async () =>
                {
                    TaskListProgress.Value = e.ProgressValue;

                    if (e.ProgressValue >= 100)
                    {
                        await Task.Delay(800).ContinueWith((_) =>
                        {
                            TaskBarController.SetBadge(0);
                            TaskListProgress.Visibility = Visibility.Collapsed;
                        }, TaskScheduler.FromCurrentSynchronizationContext());
                    }
                    else
                    {
                        TaskListProgress.Visibility = Visibility.Visible;
                        TaskListBadge.Value = QueueTaskController.ListItemSource.Count((Item) => Item.Status is OperationStatus.Preparing or OperationStatus.Processing or OperationStatus.Waiting or OperationStatus.NeedAttention);

                        if (AppInstance.GetInstances().Count > 1)
                        {
                            TaskBarController.SetBadge(0);
                        }
                        else
                        {
                            TaskBarController.SetBadge(Convert.ToUInt32(TaskListBadge.Value));
                        }
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
            int CurrentTaskNumber = QueueTaskController.ListItemSource.Count((Item) => Item.Status is OperationStatus.Preparing or OperationStatus.Processing or OperationStatus.Waiting or OperationStatus.NeedAttention);

            if (TaskListBadge.Value != CurrentTaskNumber)
            {
                TaskListBadge.Value = CurrentTaskNumber;
                TaskBarController.SetBadge(Convert.ToUInt32(CurrentTaskNumber));
            }
        }

        private async void CommonAccessCollection_LibraryNotFound(object sender, IEnumerable<string> ErrorList)
        {
            CommonContentDialog Dialog = new CommonContentDialog
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
                    SQLite.Current.DeleteLibraryFolderRecord(ErrorPath);
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
                && !SettingPage.IsOpened
                && MainPage.Current.NavView.SelectedItem is NavigationViewItem NavItem
                && Convert.ToString(NavItem.Content) == Globalization.GetString("MainPage_PageDictionary_Home_Label"))
            {
                switch (args.VirtualKey)
                {
                    case VirtualKey.Left:
                        {
                            args.Handled = true;

                            if (!Control.ShouldNotAcceptShortcutKeyInput)
                            {
                                await Control.ExecuteGoBackActionIfAvailableAsync();
                            }

                            break;
                        }
                    case VirtualKey.Right:
                        {
                            args.Handled = true;

                            if (!Control.ShouldNotAcceptShortcutKeyInput)
                            {
                                await Control.ExecuteGoForwardActionIfAvailableAsync();
                            }

                            break;
                        }
                    case VirtualKey.Enter:
                        {
                            args.Handled = true;

                            PropertiesWindowBase NewWindow = null;

                            if (Control.CurrentPresenter.CurrentFolder is RootVirtualFolder)
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
                            else if (Control.CurrentPresenter.SelectedItems.Any())
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
                if (!QueueContentDialog.IsRunningOrWaiting && !SettingPage.IsOpened)
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
                        case VirtualKey.Back:
                            {
                                args.Handled = true;

                                if (CurrentTabRenderer?.RendererFrame is Frame BaseFrame)
                                {
                                    if (BaseFrame.Content is FileControl Control && !Control.ShouldNotAcceptShortcutKeyInput)
                                    {
                                        await Control.ExecuteGoBackActionIfAvailableAsync();
                                    }
                                }

                                break;
                            }
                        case VirtualKey.Escape:
                            {
                                args.Handled = true;

                                bool ShouldSuppressNavigateBack = false;

                                if (SettingPage.IsQuicklookEnabled)
                                {
                                    using (AuxiliaryTrustProcessController.Exclusive Exclusive = await AuxiliaryTrustProcessController.GetControllerExclusiveAsync(Priority: PriorityLevel.High))
                                    {
                                        if (await Exclusive.Controller.CheckQuicklookWindowVisibleAsync())
                                        {
                                            ShouldSuppressNavigateBack = await Exclusive.Controller.CloseQuicklookWindowAsync();
                                        }
                                    }
                                }
                                else if (SettingPage.IsSeerEnabled)
                                {
                                    using (AuxiliaryTrustProcessController.Exclusive Exclusive = await AuxiliaryTrustProcessController.GetControllerExclusiveAsync(Priority: PriorityLevel.High))
                                    {
                                        if (await Exclusive.Controller.CheckSeerWindowVisibleAsync())
                                        {
                                            ShouldSuppressNavigateBack = await Exclusive.Controller.CloseSeerWindowAsync();
                                        }
                                    }
                                }

                                if (!ShouldSuppressNavigateBack)
                                {
                                    if (CurrentTabRenderer?.RendererFrame is Frame BaseFrame)
                                    {
                                        if (BaseFrame.CanGoBack)
                                        {
                                            BaseFrame.GoBack();
                                        }
                                    }
                                }

                                break;
                            }
                        default:
                            {
                                if (CurrentTabRenderer?.RendererFrame.Content is FileControl Control && Control.CurrentPresenter?.CurrentFolder is RootVirtualFolder)
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
                                                        CommonContentDialog UnlockFailedDialog = new CommonContentDialog
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
                                        case VirtualKey.Space when !SettingPage.IsOpened:
                                            {
                                                args.Handled = true;

                                                if (SettingPage.IsQuicklookEnabled)
                                                {
                                                    using (AuxiliaryTrustProcessController.Exclusive Exclusive = await AuxiliaryTrustProcessController.GetControllerExclusiveAsync(Priority: PriorityLevel.High))
                                                    {
                                                        if (await Exclusive.Controller.CheckQuicklookAvailableAsync())
                                                        {
                                                            if (HomeControl.DriveGrid.SelectedItem is DriveDataBase Device && !string.IsNullOrEmpty(Device.Path))
                                                            {
                                                                await Exclusive.Controller.ToggleQuicklookWindowAsync(Device.Path);
                                                            }
                                                            else if (HomeControl.LibraryGrid.SelectedItem is LibraryStorageFolder Library && !string.IsNullOrEmpty(Library.Path))
                                                            {
                                                                await Exclusive.Controller.ToggleQuicklookWindowAsync(Library.Path);
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
                                                            if (HomeControl.DriveGrid.SelectedItem is DriveDataBase Device && !string.IsNullOrEmpty(Device.Path))
                                                            {
                                                                await Exclusive.Controller.ToggleSeerWindowAsync(Device.Path);
                                                            }
                                                            else if (HomeControl.LibraryGrid.SelectedItem is LibraryStorageFolder Library && !string.IsNullOrEmpty(Library.Path))
                                                            {
                                                                await Exclusive.Controller.ToggleSeerWindowAsync(Library.Path);
                                                            }
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
                                                        CommonContentDialog Dialog = new CommonContentDialog
                                                        {
                                                            Title = Globalization.GetString("Common_Dialog_TipTitle"),
                                                            Content = Globalization.GetString("QueueDialog_CouldNotAccess_Content"),
                                                            CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                                                        };

                                                        await Dialog.ShowAsync();
                                                    }
                                                    else
                                                    {
                                                        if (await MSStoreHelper.CheckPurchaseStatusAsync())
                                                        {
                                                            await Control.CreateNewBladeAsync(Drive.DriveFolder);
                                                        }
                                                    }
                                                }
                                                else if (HomeControl.LibraryGrid.SelectedItem is LibraryStorageFolder Library)
                                                {
                                                    args.Handled = true;

                                                    if (string.IsNullOrEmpty(Library.Path))
                                                    {
                                                        CommonContentDialog Dialog = new CommonContentDialog
                                                        {
                                                            Title = Globalization.GetString("Common_Dialog_TipTitle"),
                                                            Content = Globalization.GetString("QueueDialog_CouldNotAccess_Content"),
                                                            CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                                                        };

                                                        await Dialog.ShowAsync();
                                                    }
                                                    else
                                                    {
                                                        if (await MSStoreHelper.CheckPurchaseStatusAsync())
                                                        {
                                                            await Control.CreateNewBladeAsync(Library);
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
                                                        CommonContentDialog Dialog = new CommonContentDialog
                                                        {
                                                            Title = Globalization.GetString("Common_Dialog_TipTitle"),
                                                            Content = Globalization.GetString("QueueDialog_CouldNotAccess_Content"),
                                                            CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                                                        };

                                                        await Dialog.ShowAsync();
                                                    }
                                                    else
                                                    {
                                                        await Launcher.LaunchUriAsync(new Uri($"rx-explorer-uwp:{Uri.EscapeDataString(JsonSerializer.Serialize(new List<string[]> { new string[] { Drive.Path } }))}"));
                                                    }
                                                }
                                                else if (HomeControl.LibraryGrid.SelectedItem is LibraryStorageFolder Library)
                                                {
                                                    args.Handled = true;

                                                    if (string.IsNullOrEmpty(Library.Path))
                                                    {
                                                        CommonContentDialog Dialog = new CommonContentDialog
                                                        {
                                                            Title = Globalization.GetString("Common_Dialog_TipTitle"),
                                                            Content = Globalization.GetString("QueueDialog_CouldNotAccess_Content"),
                                                            CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                                                        };

                                                        await Dialog.ShowAsync();
                                                    }
                                                    else
                                                    {
                                                        await Launcher.LaunchUriAsync(new Uri($"rx-explorer-uwp:{Uri.EscapeDataString(JsonSerializer.Serialize(new List<string[]> { new string[] { Library.Path } }))}"));
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
                                                        CommonContentDialog Dialog = new CommonContentDialog
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
                                                        CommonContentDialog Dialog = new CommonContentDialog
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

        public async Task CreateNewTabAsync(IEnumerable<string[]> BulkTabWithPath)
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
                await Task.Delay(300).ContinueWith((_) => TabViewControl.SelectedIndex = TabCollection.Count - 1, TaskScheduler.FromCurrentSynchronizationContext());
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
                await Task.Delay(300).ContinueWith((_) => TabViewControl.SelectedIndex = TabCollection.Count - 1, TaskScheduler.FromCurrentSynchronizationContext());
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
                await Task.Delay(300).ContinueWith((_) => TabViewControl.SelectedIndex = Index, TaskScheduler.FromCurrentSynchronizationContext());
            }
        }

        private async void TabViewContainer_Loaded(object sender, RoutedEventArgs e)
        {
            Loaded -= TabViewContainer_Loaded;

            if (SettingPage.ApplicationUIStyle == UIStyle.Clearly)
            {
                Window.Current.SetTitleBar(TitleBar);
                SettingButton.Visibility = Visibility.Visible;
                AltGoBackButton.Visibility = Visibility.Visible;
                RecycleButton.Visibility = Visibility.Visible;
                SecureAreaButton.Visibility = Visibility.Visible;
                FunctionButtonArea.Padding = new Thickness(0, 0, CoreApplication.GetCurrentView().TitleBar.SystemOverlayRightInset, 0);
            }

            if ((MainPage.Current.ActivatePaths?.Count).GetValueOrDefault() == 0)
            {
                await CreateNewTabAsync();
            }
            else
            {
                await CreateNewTabAsync(MainPage.Current.ActivatePaths);
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

            if (SettingPage.IsLibraryExpanderExpanded)
            {
                LoadTaskList.Add(CommonAccessCollection.LoadLibraryFoldersAsync());
            }

            await Task.WhenAll(LoadTaskList).ContinueWith((_) => PreviewTimer.Start(), TaskScheduler.FromCurrentSynchronizationContext());
        }

        private async void TabViewControl_TabCloseRequested(TabView sender, TabViewTabCloseRequestedEventArgs args)
        {
            await CleanUpAndRemoveTabItem(args.Tab);
        }

        private async void TabViewControl_AddTabButtonClick(TabView sender, object args)
        {
            await CreateNewTabAsync();
        }

        private async Task<TabViewItem> CreateNewTabCoreAsync(params string[] InitializePathArray)
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
            Tab.DragOver += Tab_DragOver;
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

            IReadOnlyList<FileSystemStorageItemBase> ValidStorageItem = await FileSystemStorageItemBase.OpenInBatchAsync(InitializePathArray.Where((Path) => !string.IsNullOrWhiteSpace(Path))).OfType<FileSystemStorageItemBase>().ToListAsync();

            if (Tab.Header is TextBlock HeaderBlock)
            {
                HeaderBlock.Text = ValidStorageItem.Count switch
                {
                    0 => RootVirtualFolder.Current.DisplayName,
                    1 => ValidStorageItem[0].DisplayName,
                    _ => string.Join(" | ", ValidStorageItem.Select((Item) => Item.DisplayName))
                };
            }

            TabItemContentRenderer Renderer = new TabItemContentRenderer(Tab, ValidStorageItem.Select((Item) => Item.Path).ToArray());
            Renderer.RendererFrame.Navigated += RendererFrame_Navigated;

            Tab.Content = new Frame
            {
                Content = Renderer
            };

            return Tab;
        }

        private void Tab_DragOver(object sender, DragEventArgs e)
        {
            try
            {
                e.Handled = true;

                if (e.DataView.Contains(ExtendedDataFormats.TabItem))
                {
                    e.AcceptedOperation = DataPackageOperation.Move;
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, "An exception was threw when trying fetch the clipboard data");
            }
        }

        private void Tab_DragEnter(object sender, DragEventArgs e)
        {
            try
            {
                if (e.OriginalSource is TabViewItem Item)
                {
                    if (e.DataView.Contains(StandardDataFormats.StorageItems)
                        || e.DataView.Contains(ExtendedDataFormats.CompressionItems)
                        || e.DataView.Contains(ExtendedDataFormats.NotSupportedStorageItem)
                        || e.DataView.Contains(ExtendedDataFormats.FileDrop))
                    {
                        TabViewControl.SelectedItem = Item;
                    }
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, "An exception was threw when trying fetch the clipboard data");
            }
        }

        private void RendererFrame_Navigated(object sender, NavigationEventArgs e)
        {
            if (sender is Frame frame)
            {
                LayoutModeControl.IsEnabled = e.Content is FileControl;

                switch (SettingPage.ApplicationUIStyle)
                {
                    case UIStyle.Normal:
                        {
                            MainPage.Current.NavView.IsBackEnabled = frame.CanGoBack;
                            break;
                        }
                    case UIStyle.Clearly:
                        {
                            AltGoBackButton.IsEnabled = frame.CanGoBack;
                            break;
                        }
                }
            }
        }

        private void Tab_PointerCanceled(object sender, PointerRoutedEventArgs e)
        {
            DelayPreviewCancel?.Cancel();
        }

        private void Tab_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            DelayPreviewCancel?.Cancel();
        }

        private void Tab_PointerEntered(object sender, PointerRoutedEventArgs e)
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

        private async void Tab_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
        {
            if (sender is TabViewItem Tab)
            {
                await CleanUpAndRemoveTabItem(Tab);
            }
        }

        private async void Tab_PointerPressed(object sender, PointerRoutedEventArgs e)
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
                            case RootVirtualFolder:
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

        private async void TabViewControl_TabDragStarting(TabView sender, TabViewTabDragStartingEventArgs args)
        {
            DelayPreviewCancel?.Cancel();

            if (TabCollection.Count > 1)
            {
                sender.CanReorderTabs = true;
                args.Data.RequestedOperation = DataPackageOperation.Move;

                if (args.Tab.Content is Frame RootFrame && RootFrame.Content is TabItemContentRenderer Renderer)
                {
                    if (Renderer.RendererFrame.Content is Home)
                    {
                        args.Data.SetData(ExtendedDataFormats.TabItem, await Helper.CreateRandomAccessStreamAsync(Encoding.Unicode.GetBytes(JsonSerializer.Serialize(Array.Empty<string>()))));
                    }
                    else if (Renderer.Presenters.Any())
                    {
                        args.Data.SetData(ExtendedDataFormats.TabItem, await Helper.CreateRandomAccessStreamAsync(Encoding.Unicode.GetBytes(JsonSerializer.Serialize(Renderer.Presenters.Select((Presenter) => Presenter.CurrentFolder?.Path).Where((Path) => !string.IsNullOrWhiteSpace(Path)).ToArray()))));
                    }
                    else
                    {
                        args.Cancel = true;
                    }
                }
                else
                {
                    args.Cancel = true;
                }
            }
            else
            {
                args.Cancel = true;
            }
        }

        private void TabViewControl_TabStripDragOver(object sender, DragEventArgs e)
        {
            try
            {
                e.Handled = true;

                if (e.DataView.Contains(ExtendedDataFormats.TabItem))
                {
                    e.AcceptedOperation = DataPackageOperation.Move;
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
                    ApplicationData.Current.LocalSettings.Values["TabItemDropTargetApplicationId"] = AppInstanceIdContainer.CurrentId;

                    if (await e.DataView.GetDataAsync(ExtendedDataFormats.TabItem) is IRandomAccessStream RandomStream)
                    {
                        RandomStream.Seek(0);

                        using (StreamReader Reader = new StreamReader(RandomStream.AsStreamForRead(), Encoding.Unicode, true, 512, true))
                        {
                            string RawText = Reader.ReadToEnd();

                            if (!string.IsNullOrEmpty(RawText))
                            {
                                int InsertIndex = TabCollection.Count;

                                for (int i = 0; i < TabCollection.Count; i++)
                                {
                                    if (TabViewControl.ContainerFromIndex(i) is TabViewItem Tab)
                                    {
                                        double XPosition = e.GetPosition(Tab).X;

                                        if (XPosition < Tab.ActualWidth)
                                        {
                                            InsertIndex = i;

                                            if (XPosition >= Tab.ActualWidth / 2)
                                            {
                                                InsertIndex++;
                                            }

                                            break;
                                        }
                                    }
                                }

                                IEnumerable<string> PathList = JsonSerializer.Deserialize<IEnumerable<string>>(RawText);

                                if (PathList.Any())
                                {
                                    await CreateNewTabAsync(InsertIndex, PathList.Where((Path) => !string.IsNullOrWhiteSpace(Path)).ToArray());
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
                    Tab.DragOver -= Tab_DragOver;
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

                        await MonitorTrustProcessController.StopMonitorAsync();

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

        private void TabViewControl_PointerWheelChanged(object sender, PointerRoutedEventArgs e)
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
                IReadOnlyList<TabViewItem> ToBeRemoveList = TabCollection.ToArray();

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
            if (await MSStoreHelper.CheckPurchaseStatusAsync())
            {
                if (CurrentTabRenderer?.RendererFrame.Content is FileControl Control)
                {
                    if (Control.CurrentPresenter?.CurrentFolder is FileSystemStorageFolder Folder)
                    {
                        await Control.CreateNewBladeAsync(Folder);
                    }
                }
            }
            else
            {
                VerticalSplitTip.IsOpen = true;
            }
        }

        private async void VerticalSplitTip_ActionButtonClick(TeachingTip sender, object args)
        {
            sender.IsOpen = false;

            switch (await MSStoreHelper.PurchaseAsync())
            {
                case StorePurchaseStatus.Succeeded:
                    {
                        CommonContentDialog QueueContenDialog = new CommonContentDialog
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
                        CommonContentDialog QueueContenDialog = new CommonContentDialog
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
                        CommonContentDialog QueueContenDialog = new CommonContentDialog
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
                        CommonContentDialog QueueContenDialog = new CommonContentDialog
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

        private void TabViewControl_PreviewKeyDown(object sender, KeyRoutedEventArgs e)
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

        private async void TabViewControl_Loaded(object sender, RoutedEventArgs e)
        {
            for (int Retry = 0; Retry < 3; Retry++)
            {
                if (TabViewControl.FindChildOfName<ContentPresenter>("TabContentPresenter") is ContentPresenter Presenter)
                {
                    Presenter.Background = new SolidColorBrush(Colors.Transparent);
                    break;
                }

                await Task.Delay(300);
            }
        }

        private async void SettingButton_Click(object sender, RoutedEventArgs e)
        {
            if (!SettingPage.IsOpened)
            {
                await MainPage.Current.Settings.ShowAsync();
            }
        }

        private void AltGoBackButton_Click(object sender, RoutedEventArgs e)
        {
            if ((CurrentTabRenderer?.RendererFrame.CanGoBack).GetValueOrDefault())
            {
                CurrentTabRenderer.RendererFrame.GoBack();
            }
        }

        private void RecycleButton_Click(object sender, RoutedEventArgs e)
        {
            CurrentTabRenderer.RendererFrame.Navigate(typeof(RecycleBin), null, new DrillInNavigationTransitionInfo());
        }

        private async void TabViewControl_TabDragCompleted(TabView sender, TabViewTabDragCompletedEventArgs args)
        {
            sender.CanReorderTabs = false;

            if (args.Tab is TabViewItem Tab)
            {
                if (args.DropResult == DataPackageOperation.None)
                {
                    if (Tab.Content is Frame BaseFrame && BaseFrame.Content is TabItemContentRenderer Renderer)
                    {
                        string StartupArgument = Uri.EscapeDataString(JsonSerializer.Serialize(new List<string[]>
                        {
                            Renderer.Presenters.Select((Presenter) => Presenter.CurrentFolder?.Path)
                                               .Where((Path) => !string.IsNullOrWhiteSpace(Path))
                                               .ToArray()
                        }));

                        await Task.WhenAll(CleanUpAndRemoveTabItem(Tab), Launcher.LaunchUriAsync(new Uri($"rx-explorer-uwp:{StartupArgument}")).AsTask());
                    }
                }
                else if (ApplicationData.Current.LocalSettings.Values["TabItemDropTargetApplicationId"] is string TargetApplicationId)
                {
                    ApplicationData.Current.LocalSettings.Values.Remove("TabItemDropTargetApplicationId");

                    if (TargetApplicationId != AppInstanceIdContainer.CurrentId)
                    {
                        await CleanUpAndRemoveTabItem(Tab);
                    }
                }
            }
        }

        private void SecureAreaButton_Click(object sender, RoutedEventArgs e)
        {
            CurrentTabRenderer.RendererFrame.Navigate(typeof(SecureAreaContainer), null, new DrillInNavigationTransitionInfo());
        }

        private void TabViewControl_ContextRequested(UIElement sender, ContextRequestedEventArgs args)
        {
            args.Handled = true;

            if (args.TryGetPosition(sender, out Point Position))
            {
                if ((args.OriginalSource as FrameworkElement)?.FindParentOfType<TabViewItem>() is TabViewItem Item)
                {
                    TabViewControl.SelectedItem = Item;

                    TabCommandFlyout?.ShowAt(Item, new FlyoutShowOptions
                    {
                        Position = Position,
                        Placement = FlyoutPlacementMode.BottomEdgeAlignedLeft,
                        ShowMode = FlyoutShowMode.Standard
                    });
                }
            }
        }
    }
}
