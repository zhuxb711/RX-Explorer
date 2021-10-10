using Microsoft.Toolkit.Uwp.Notifications;
using Microsoft.Toolkit.Uwp.UI.Controls;
using Microsoft.UI.Xaml.Controls;
using RX_Explorer.Class;
using RX_Explorer.Dialog;
using RX_Explorer.SeparateWindow.PropertyWindow;
using ShareClassLibrary;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.ApplicationModel.DataTransfer.DragDrop;
using Windows.Foundation;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.System;
using Windows.UI.Core;
using Windows.UI.Input;
using Windows.UI.Notifications;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Media.Imaging;
using CommandBarFlyout = Microsoft.UI.Xaml.Controls.CommandBarFlyout;
using ProgressBar = Microsoft.UI.Xaml.Controls.ProgressBar;
using TabViewItem = Microsoft.UI.Xaml.Controls.TabViewItem;

namespace RX_Explorer
{
    public sealed partial class Home : Page
    {
        public event EventHandler<string> EnterActionRequested;
        private CancellationTokenSource DelaySelectionCancellation;
        private CancellationTokenSource DelayEnterCancellation;

        public Home()
        {
            InitializeComponent();
            Loaded += Home_Loaded;
        }

        private async void Home_Loaded(object sender, RoutedEventArgs e)
        {
            if (await MSStoreHelper.Current.CheckPurchaseStatusAsync())
            {
                OpenFolderInVerticalSplitView.Visibility = Visibility.Visible;
            }
        }

        private void CloseAllFlyout()
        {
            try
            {
                LibraryEmptyFlyout.Hide();
                DriveEmptyFlyout.Hide();
                DriveFlyout.Hide();
                LibraryFlyout.Hide();
                PortableDeviceFlyout.Hide();
                BitlockerDeviceFlyout.Hide();
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, "Could not close the flyout for unknown reason");
            }
        }

        private void DriveGrid_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                args.ItemContainer.AllowDrop = false;

                args.ItemContainer.Drop -= ItemContainer_Drop;
                args.ItemContainer.DragOver -= ItemContainer_DragOver;
                args.ItemContainer.DragEnter -= ItemContainer_DragEnter;
                args.ItemContainer.DragLeave -= ItemContainer_DragLeave;
                args.ItemContainer.PointerEntered -= ItemContainer_PointerEntered;
                args.ItemContainer.PointerExited -= ItemContainer_PointerExited;
                args.ItemContainer.PointerCanceled -= ItemContainer_PointerCanceled;
            }
            else
            {
                args.ItemContainer.AllowDrop = true;

                args.ItemContainer.Drop += ItemContainer_Drop;
                args.ItemContainer.DragOver += ItemContainer_DragOver;
                args.ItemContainer.DragEnter += ItemContainer_DragEnter;
                args.ItemContainer.DragLeave += ItemContainer_DragLeave;
                args.ItemContainer.PointerEntered += ItemContainer_PointerEntered;
                args.ItemContainer.PointerExited += ItemContainer_PointerExited;
                args.ItemContainer.PointerCanceled += ItemContainer_PointerCanceled;

                if (AnimationController.Current.IsEnableAnimation)
                {
                    ProgressBar ProBar = args.ItemContainer.FindChildOfType<ProgressBar>();
                    Storyboard Story = new Storyboard();
                    DoubleAnimation Animation = new DoubleAnimation()
                    {
                        To = (args.Item as DriveDataBase).Percent,
                        From = 0,
                        EnableDependentAnimation = true,
                        EasingFunction = new CircleEase { EasingMode = EasingMode.EaseInOut },
                        Duration = new TimeSpan(0, 0, 0, 0, 800)
                    };
                    Storyboard.SetTarget(Animation, ProBar);
                    Storyboard.SetTargetProperty(Animation, "Value");
                    Story.Children.Add(Animation);
                    Story.Begin();
                }

                args.RegisterUpdateCallback(async (s, e) =>
                {
                    if (e.Item is DriveDataBase Drive)
                    {
                        await Drive.LoadAsync().ConfigureAwait(false);
                    }
                });
            }
        }

        private void ItemContainer_DragLeave(object sender, DragEventArgs e)
        {
            DelayEnterCancellation?.Cancel();
        }

        private void ItemContainer_DragEnter(object sender, DragEventArgs e)
        {
            if (sender is SelectorItem Selector)
            {
                DelayEnterCancellation?.Cancel();
                DelayEnterCancellation?.Dispose();
                DelayEnterCancellation = new CancellationTokenSource();

                Task.Delay(2000).ContinueWith((task, input) =>
                {
                    try
                    {
                        if (input is CancellationTokenSource Cancel && !Cancel.IsCancellationRequested)
                        {
                            switch (Selector.Content)
                            {
                                case DriveDataBase Drive when Drive is not LockedDriveData:
                                    {
                                        EnterActionRequested?.Invoke(this, Drive.Path);
                                        break;
                                    }
                                case LibraryStorageFolder Lib when !LibraryGrid.CanReorderItems:
                                    {
                                        EnterActionRequested?.Invoke(this, Lib.Path);
                                        break;
                                    }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        LogTracer.Log(ex, "An exception was thew in DelayEnterProcess");
                    }
                }, DelayEnterCancellation, TaskScheduler.FromCurrentSynchronizationContext());
            }
        }

        private void LibraryGrid_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                args.ItemContainer.AllowDrop = false;

                args.ItemContainer.Drop -= ItemContainer_Drop;
                args.ItemContainer.DragOver -= ItemContainer_DragOver;
                args.ItemContainer.DragEnter -= ItemContainer_DragEnter;
                args.ItemContainer.DragLeave -= ItemContainer_DragLeave;
                args.ItemContainer.PointerEntered -= ItemContainer_PointerEntered;
                args.ItemContainer.PointerExited -= ItemContainer_PointerExited;
                args.ItemContainer.PointerCanceled -= ItemContainer_PointerCanceled;
            }
            else
            {
                args.ItemContainer.AllowDrop = true;

                args.ItemContainer.Drop += ItemContainer_Drop;
                args.ItemContainer.DragOver += ItemContainer_DragOver;
                args.ItemContainer.DragEnter += ItemContainer_DragEnter;
                args.ItemContainer.DragLeave += ItemContainer_DragLeave;
                args.ItemContainer.PointerEntered += ItemContainer_PointerEntered;
                args.ItemContainer.PointerExited += ItemContainer_PointerExited;
                args.ItemContainer.PointerCanceled += ItemContainer_PointerCanceled;

                args.RegisterUpdateCallback(async (s, e) =>
                {
                    if (e.Item is LibraryStorageFolder Item)
                    {
                        await Item.LoadAsync().ConfigureAwait(false);
                    }
                });
            }
        }

        private async void ItemContainer_DragOver(object sender, DragEventArgs e)
        {
            DragOperationDeferral Deferral = e.GetDeferral();

            try
            {
                switch ((sender as SelectorItem)?.Content)
                {
                    case LibraryStorageFolder Folder:
                        {
                            if (await e.DataView.CheckIfContainsAvailableDataAsync())
                            {
                                if (e.Modifiers.HasFlag(DragDropModifiers.Control))
                                {
                                    e.AcceptedOperation = DataPackageOperation.Copy;
                                    e.DragUIOverride.Caption = $"{Globalization.GetString("Drag_Tip_CopyTo")} \"{Folder.Name}\"";
                                }
                                else
                                {
                                    e.AcceptedOperation = DataPackageOperation.Move;
                                    e.DragUIOverride.Caption = $"{Globalization.GetString("Drag_Tip_MoveTo")} \"{Folder.Name}\"";
                                }

                                e.DragUIOverride.IsContentVisible = true;
                                e.DragUIOverride.IsCaptionVisible = true;
                                e.DragUIOverride.IsGlyphVisible = true;
                            }
                            else
                            {
                                e.AcceptedOperation = DataPackageOperation.None;
                            }

                            break;
                        }
                    case DriveDataBase Drive when Drive is not LockedDriveData:
                        {
                            if (await e.DataView.CheckIfContainsAvailableDataAsync())
                            {
                                if (e.Modifiers.HasFlag(DragDropModifiers.Control))
                                {
                                    e.AcceptedOperation = DataPackageOperation.Copy;
                                    e.DragUIOverride.Caption = $"{Globalization.GetString("Drag_Tip_CopyTo")} \"{Drive.Name}\"";
                                }
                                else
                                {
                                    e.AcceptedOperation = DataPackageOperation.Move;
                                    e.DragUIOverride.Caption = $"{Globalization.GetString("Drag_Tip_MoveTo")} \"{Drive.Name}\"";
                                }

                                e.DragUIOverride.IsContentVisible = true;
                                e.DragUIOverride.IsCaptionVisible = true;
                                e.DragUIOverride.IsGlyphVisible = true;
                            }
                            else
                            {
                                e.AcceptedOperation = DataPackageOperation.None;
                            }

                            break;
                        }
                    default:
                        {
                            e.AcceptedOperation = DataPackageOperation.None;
                            break;
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

        private async void ItemContainer_Drop(object sender, DragEventArgs e)
        {
            DragOperationDeferral Deferral = e.GetDeferral();

            try
            {
                e.Handled = true;

                DelayEnterCancellation?.Cancel();

                IReadOnlyList<string> PathList = await e.DataView.GetAsPathListAsync();

                if (PathList.Count > 0)
                {
                    switch ((sender as SelectorItem).Content)
                    {
                        case LibraryStorageFolder Lib:
                            {
                                if (e.AcceptedOperation.HasFlag(DataPackageOperation.Move))
                                {
                                    QueueTaskController.EnqueueMoveOpeartion(PathList, Lib.Path);
                                }
                                else
                                {
                                    QueueTaskController.EnqueueCopyOpeartion(PathList, Lib.Path);
                                }

                                break;
                            }
                        case DriveDataBase Drive when Drive is not LockedDriveData:
                            {
                                if (e.AcceptedOperation.HasFlag(DataPackageOperation.Move))
                                {
                                    QueueTaskController.EnqueueMoveOpeartion(PathList, Drive.Path);
                                }
                                else
                                {
                                    QueueTaskController.EnqueueCopyOpeartion(PathList, Drive.Path);
                                }

                                break;
                            }
                    }
                }
            }
            catch (Exception ex) when (ex.HResult is unchecked((int)0x80040064) or unchecked((int)0x8004006A))
            {
                if ((sender as SelectorItem).Content is FileSystemStorageItemBase Item)
                {
                    QueueTaskController.EnqueueRemoteCopyOpeartion(Item.Path);
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, "Could not get the content of clipboard");

                QueueContentDialog dialog = new QueueContentDialog
                {
                    Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                    Content = Globalization.GetString("QueueDialog_FailToGetClipboardError_Content"),
                    CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                };

                await dialog.ShowAsync();
            }
            finally
            {
                Deferral.Complete();
            }
        }

        private void ItemContainer_PointerCanceled(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            DelaySelectionCancellation?.Cancel();
            DelayEnterCancellation?.Cancel();
        }

        private void ItemContainer_PointerExited(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            DelaySelectionCancellation?.Cancel();
        }

        private void ItemContainer_PointerEntered(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if ((e.OriginalSource as FrameworkElement)?.DataContext is object Item)
            {
                switch (Item)
                {
                    case LibraryStorageFolder when !SettingControl.IsDoubleClickEnabled
                                                   && LibraryGrid.SelectedItem != Item
                                                   && !e.KeyModifiers.HasFlag(VirtualKeyModifiers.Control)
                                                   && !e.KeyModifiers.HasFlag(VirtualKeyModifiers.Shift):
                        {
                            DelaySelectionCancellation?.Cancel();
                            DelaySelectionCancellation?.Dispose();
                            DelaySelectionCancellation = new CancellationTokenSource();

                            Task.Delay(700).ContinueWith((task, input) =>
                            {
                                if (input is CancellationTokenSource Cancel && !Cancel.IsCancellationRequested)
                                {
                                    LibraryGrid.SelectedItem = Item;
                                    DriveGrid.SelectedItem = null;
                                }
                            }, DelaySelectionCancellation, TaskScheduler.FromCurrentSynchronizationContext());

                            break;
                        }
                    case DriveDataBase when !SettingControl.IsDoubleClickEnabled
                                            && LibraryGrid.SelectedItem != Item
                                            && !e.KeyModifiers.HasFlag(VirtualKeyModifiers.Control)
                                            && !e.KeyModifiers.HasFlag(VirtualKeyModifiers.Shift):
                        {
                            DelaySelectionCancellation?.Cancel();
                            DelaySelectionCancellation?.Dispose();
                            DelaySelectionCancellation = new CancellationTokenSource();

                            Task.Delay(700).ContinueWith((task, input) =>
                            {
                                if (input is CancellationTokenSource Cancel && !Cancel.IsCancellationRequested)
                                {
                                    DriveGrid.SelectedItem = Item;
                                    LibraryGrid.SelectedItem = null;
                                }
                            }, DelaySelectionCancellation, TaskScheduler.FromCurrentSynchronizationContext());

                            break;
                        }
                }
            }
        }

        public async Task OpenTargetDriveAsync(DriveDataBase Drive)
        {
            switch (Drive)
            {
                case LockedDriveData LockedDrive:
                    {
                    Retry:
                        BitlockerPasswordDialog Dialog = new BitlockerPasswordDialog();

                        if (await Dialog.ShowAsync() == ContentDialogResult.Primary)
                        {
                            if (!await LockedDrive.UnlockAsync(Dialog.Password))
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

                            DriveDataBase NewDrive = await DriveDataBase.CreateAsync(LockedDrive.DriveType, await StorageFolder.GetFolderFromPathAsync(LockedDrive.Path));

                            if (NewDrive is LockedDriveData)
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
                            }
                            else
                            {
                                int Index = CommonAccessCollection.DriveList.IndexOf(LockedDrive);

                                if (Index >= 0)
                                {
                                    CommonAccessCollection.DriveList.Remove(LockedDrive);
                                    CommonAccessCollection.DriveList.Insert(Index, NewDrive);
                                }
                                else
                                {
                                    CommonAccessCollection.DriveList.Add(NewDrive);
                                }
                            }
                        }

                        break;
                    }
                case WslDriveData:
                case NormalDriveData:
                    {
                        await OpenTargetFolder(Drive.Path);
                        break;
                    }
            }
        }

        public async Task OpenTargetFolder(string Path)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(Path))
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
                    DelaySelectionCancellation?.Cancel();
                    EnterActionRequested?.Invoke(this, Path);
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, "An error was threw when entering device");
            }
        }

        private async void DriveGrid_DoubleTapped(object sender, Windows.UI.Xaml.Input.DoubleTappedRoutedEventArgs e)
        {
            LibraryGrid.SelectedIndex = -1;

            if ((e.OriginalSource as FrameworkElement)?.DataContext is DriveDataBase Drive)
            {
                CoreWindow CWindow = CoreWindow.GetForCurrentThread();

                if (CWindow.GetKeyState(VirtualKey.Menu).HasFlag(CoreVirtualKeyStates.Down))
                {
                    await new DriveInfoDialog(Drive).ShowAsync();
                }
                else
                {
                    await OpenTargetDriveAsync(Drive);
                }
            }
        }

        private async void LibraryGrid_DoubleTapped(object sender, Windows.UI.Xaml.Input.DoubleTappedRoutedEventArgs e)
        {
            DriveGrid.SelectedIndex = -1;

            if ((e.OriginalSource as FrameworkElement)?.DataContext is LibraryStorageFolder Library)
            {
                CoreWindow CWindow = CoreWindow.GetForCurrentThread();

                if (CWindow.GetKeyState(VirtualKey.Menu).HasFlag(CoreVirtualKeyStates.Down))
                {
                    if (Library.Path.Equals(Path.GetPathRoot(Library.Path), StringComparison.OrdinalIgnoreCase))
                    {
                        if (CommonAccessCollection.DriveList.FirstOrDefault((Drive) => Drive.Path.Equals(Library.Path, StringComparison.OrdinalIgnoreCase)) is DriveDataBase Drive)
                        {
                            await new DriveInfoDialog(Drive).ShowAsync();
                            return;
                        }
                        else if (Library.Path.StartsWith(@"\\"))
                        {
                            IReadOnlyList<DriveDataBase> NetworkDriveList = CommonAccessCollection.DriveList.Where((Drive) => Drive.DriveType == DriveType.Network).ToList();

                            if (NetworkDriveList.Count > 0)
                            {
                                string RemappedPath = await UncPath.MapUncToDrivePath(NetworkDriveList.Select((Drive) => Drive.Path), Library.Path);

                                if (NetworkDriveList.FirstOrDefault((Drive) => Drive.Path.Equals(RemappedPath, StringComparison.OrdinalIgnoreCase)) is DriveDataBase NetworkDrive)
                                {
                                    await new DriveInfoDialog(NetworkDrive).ShowAsync();
                                    return;
                                }
                            }
                        }
                    }

                    PropertiesWindowBase NewWindow = await PropertiesWindowBase.CreateAsync(Library);
                    await NewWindow.ShowAsync(new Point(Window.Current.Bounds.Width / 2 - 200, Window.Current.Bounds.Height / 2 - 300));
                }
                else
                {
                    await OpenTargetFolder(Library.Path);
                }
            }
        }

        private async void DriveGrid_RightTapped(object sender, Windows.UI.Xaml.Input.RightTappedRoutedEventArgs e)
        {
            if (e.PointerDeviceType == Windows.Devices.Input.PointerDeviceType.Mouse)
            {
                if (!SettingControl.IsDoubleClickEnabled)
                {
                    DelaySelectionCancellation?.Cancel();
                }

                if ((e.OriginalSource as FrameworkElement)?.DataContext is DriveDataBase Context)
                {
                    DriveGrid.SelectedItem = Context;

                    CommandBarFlyout Flyout;

                    if (Context is LockedDriveData)
                    {
                        Flyout = BitlockerDeviceFlyout;
                    }
                    else
                    {
                        Flyout = Context.DriveType == DriveType.Removable ? PortableDeviceFlyout : DriveFlyout;
                    }

                    await Flyout.ShowCommandBarFlyoutWithExtraContextMenuItems(DriveGrid, e.GetPosition((FrameworkElement)sender), DriveGrid.SelectedItems.Cast<DriveDataBase>().Select((Drive) => Drive.Path).ToArray());
                }
                else
                {
                    DriveGrid.SelectedIndex = -1;

                    DriveEmptyFlyout.ShowAt(DriveGrid, new FlyoutShowOptions
                    {
                        Position = e.GetPosition((FrameworkElement)sender)
                    });
                }
            }
        }

        private async void OpenDrive_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();

            LibraryGrid.SelectedIndex = -1;

            if (DriveGrid.SelectedItem is DriveDataBase Drive)
            {
                await OpenTargetDriveAsync(Drive);
            }
        }

        private void DriveGrid_PointerPressed(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if ((e.OriginalSource as FrameworkElement)?.DataContext == null)
            {
                DriveGrid.SelectedIndex = -1;
            }
            else
            {
                LibraryGrid.SelectedIndex = -1;
            }
        }

        private void LibraryGrid_PointerPressed(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if ((e.OriginalSource as FrameworkElement)?.DataContext == null)
            {
                LibraryGrid.SelectedIndex = -1;
            }
            else
            {
                DriveGrid.SelectedIndex = -1;
            }
        }

        private void Grid_PointerPressed(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            DriveGrid.SelectedIndex = -1;
            LibraryGrid.SelectedIndex = -1;
        }

        private async void LibraryGrid_RightTapped(object sender, Windows.UI.Xaml.Input.RightTappedRoutedEventArgs e)
        {
            if (e.PointerDeviceType == Windows.Devices.Input.PointerDeviceType.Mouse)
            {
                if (!SettingControl.IsDoubleClickEnabled)
                {
                    DelaySelectionCancellation?.Cancel();
                }

                if ((e.OriginalSource as FrameworkElement)?.DataContext is LibraryStorageFolder Context)
                {
                    LibraryGrid.SelectedItem = Context;
                    await LibraryFlyout.ShowCommandBarFlyoutWithExtraContextMenuItems(LibraryGrid, e.GetPosition((FrameworkElement)sender), LibraryGrid.SelectedItems.Cast<LibraryStorageFolder>().Select((Lib) => Lib.Path).ToArray());
                }
                else
                {
                    LibraryEmptyFlyout.ShowAt(LibraryGrid, new FlyoutShowOptions
                    {
                        Position = e.GetPosition((FrameworkElement)sender)
                    });
                }
            }
        }

        private async void OpenLibrary_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();

            DriveGrid.SelectedIndex = -1;

            if (LibraryGrid.SelectedItem is LibraryStorageFolder Library)
            {
                await OpenTargetFolder(Library.Path);
            }
        }

        private async void RemovePin_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();

            if (LibraryGrid.SelectedItem is LibraryStorageFolder Library)
            {
                CommonAccessCollection.LibraryFolderList.Remove(Library);
                SQLite.Current.DeleteLibrary(Library.Path);
                await JumpListController.Current.RemoveItemAsync(JumpListGroup.Library, Library.Path);
            }
        }

        private async void LibraryProperties_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();

            if (LibraryGrid.SelectedItem is LibraryStorageFolder Library)
            {
                if (Library.Path.Equals(Path.GetPathRoot(Library.Path), StringComparison.OrdinalIgnoreCase))
                {
                    if (CommonAccessCollection.DriveList.FirstOrDefault((Drive) => Drive.Path.Equals(Library.Path, StringComparison.OrdinalIgnoreCase)) is DriveDataBase Drive)
                    {
                        await new DriveInfoDialog(Drive).ShowAsync();
                        return;
                    }
                    else if (Library.Path.StartsWith(@"\\"))
                    {
                        IReadOnlyList<DriveDataBase> NetworkDriveList = CommonAccessCollection.DriveList.Where((Drive) => Drive.DriveType == DriveType.Network).ToList();

                        if (NetworkDriveList.Count > 0)
                        {
                            string RemappedPath = await UncPath.MapUncToDrivePath(NetworkDriveList.Select((Drive) => Drive.Path), Library.Path);

                            if (NetworkDriveList.FirstOrDefault((Drive) => Drive.Path.Equals(RemappedPath, StringComparison.OrdinalIgnoreCase)) is DriveDataBase NetworkDrive)
                            {
                                await new DriveInfoDialog(NetworkDrive).ShowAsync();
                                return;
                            }
                        }
                    }
                }

                PropertiesWindowBase NewWindow = await PropertiesWindowBase.CreateAsync(Library);
                await NewWindow.ShowAsync(new Point(Window.Current.Bounds.Width / 2 - 200, Window.Current.Bounds.Height / 2 - 300));
            }
        }

        private async void Refresh_Click(object sender, RoutedEventArgs e)
        {
            await CommonAccessCollection.LoadDriveAsync(true);
        }

        private async void DriveGrid_ItemClick(object sender, ItemClickEventArgs e)
        {
            LibraryGrid.SelectedIndex = -1;

            if (!SettingControl.IsDoubleClickEnabled && e.ClickedItem is DriveDataBase Drive)
            {
                await OpenTargetDriveAsync(Drive);
            }
        }

        private async void LibraryGrid_ItemClick(object sender, ItemClickEventArgs e)
        {
            DriveGrid.SelectedIndex = -1;

            if (!SettingControl.IsDoubleClickEnabled && e.ClickedItem is LibraryStorageFolder Library)
            {
                await OpenTargetFolder(Library.Path);
            }
        }

        private async void AddDrive_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();

            FolderPicker Picker = new FolderPicker
            {
                SuggestedStartLocation = PickerLocationId.ComputerFolder,
                ViewMode = PickerViewMode.Thumbnail
            };
            Picker.FileTypeFilter.Add("*");

            StorageFolder DriveFolder = await Picker.PickSingleFolderAsync();

            if (DriveFolder != null)
            {
                if (DriveInfo.GetDrives().Where((Drive) => Drive.DriveType is DriveType.Fixed or DriveType.Removable or DriveType.Network or DriveType.CDRom)
                                         .Any((Item) => Item.RootDirectory.FullName.Equals(DriveFolder.Path, StringComparison.OrdinalIgnoreCase)))
                {
                    if (CommonAccessCollection.DriveList.Any((Item) => Item.Path.Equals(DriveFolder.Path, StringComparison.OrdinalIgnoreCase)))
                    {
                        LogTracer.Log("Could not add the drive to DriveList because it already exist in DriveList");

                        QueueContentDialog Dialog = new QueueContentDialog
                        {
                            Title = Globalization.GetString("Common_Dialog_TipTitle"),
                            Content = Globalization.GetString("QueueDialog_DeviceExist_Content"),
                            CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                        };

                        await Dialog.ShowAsync();
                    }
                    else
                    {
                        CommonAccessCollection.DriveList.Add(await DriveDataBase.CreateAsync(new DriveInfo(DriveFolder.Path).DriveType, DriveFolder));
                    }
                }
                else
                {
                    LogTracer.Log("Could not add the drive to DriveList because it is not in system drive list");

                    QueueContentDialog Dialog = new QueueContentDialog
                    {
                        Title = Globalization.GetString("Common_Dialog_TipTitle"),
                        Content = Globalization.GetString("QueueDialog_DeviceSelectError_Content"),
                        CloseButtonText = Globalization.GetString("Common_Dialog_TipTitle")
                    };

                    await Dialog.ShowAsync();
                }
            }
        }

        private async void Properties_Click(object sender, RoutedEventArgs e)
        {
            if (DriveGrid.SelectedItem is DriveDataBase Drive)
            {
                await new DriveInfoDialog(Drive).ShowAsync();
            }
        }

        private async void LibraryGrid_Holding(object sender, Windows.UI.Xaml.Input.HoldingRoutedEventArgs e)
        {
            if (e.HoldingState == HoldingState.Started)
            {
                if (!SettingControl.IsDoubleClickEnabled)
                {
                    DelaySelectionCancellation?.Cancel();
                }

                if ((e.OriginalSource as FrameworkElement)?.DataContext is LibraryStorageFolder Context)
                {
                    LibraryGrid.SelectedItem = Context;
                    await LibraryFlyout.ShowCommandBarFlyoutWithExtraContextMenuItems(LibraryGrid, e.GetPosition((FrameworkElement)sender), LibraryGrid.SelectedItems.Cast<LibraryStorageFolder>().Select((Lib) => Lib.Path).ToArray());
                }
                else
                {
                    LibraryFlyout.ShowAt(LibraryGrid, new FlyoutShowOptions
                    {
                        Position = e.GetPosition((FrameworkElement)sender)
                    });
                }
            }
        }

        private async void DriveGrid_Holding(object sender, Windows.UI.Xaml.Input.HoldingRoutedEventArgs e)
        {
            if (e.HoldingState == HoldingState.Started)
            {
                if (!SettingControl.IsDoubleClickEnabled)
                {
                    DelaySelectionCancellation?.Cancel();
                }

                if ((e.OriginalSource as FrameworkElement)?.DataContext is DriveDataBase Context)
                {
                    DriveGrid.SelectedItem = Context;

                    CommandBarFlyout Flyout;

                    if (Context is LockedDriveData)
                    {
                        Flyout = BitlockerDeviceFlyout;
                    }
                    else
                    {
                        Flyout = Context.DriveType == DriveType.Removable ? PortableDeviceFlyout : DriveFlyout;
                    }

                    await Flyout.ShowCommandBarFlyoutWithExtraContextMenuItems(DriveGrid, e.GetPosition((FrameworkElement)sender), DriveGrid.SelectedItems.Cast<DriveDataBase>().Select((Drive) => Drive.Path).ToArray());
                }
                else
                {
                    DriveGrid.SelectedIndex = -1;

                    DriveEmptyFlyout.ShowAt(DriveGrid, new FlyoutShowOptions
                    {
                        Position = e.GetPosition((FrameworkElement)sender)
                    });
                }
            }
        }

        private void LibraryExpander_Collapsed(object sender, EventArgs e)
        {
            LibraryGrid.SelectedIndex = -1;
        }

        private void DeviceExpander_Collapsed(object sender, EventArgs e)
        {
            DriveGrid.SelectedIndex = -1;
        }

        private async void EjectButton_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();

            if (DriveGrid.SelectedItem is DriveDataBase Item)
            {
                if (string.IsNullOrEmpty(Item.Path))
                {
                    QueueContentDialog Dialog = new QueueContentDialog
                    {
                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                        Content = Globalization.GetString("QueueContentDialog_UnableToEject_Content"),
                        CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                    };

                    await Dialog.ShowAsync().ConfigureAwait(false);
                }
                else
                {
                    foreach ((TabViewItem Tab, BladeItem[] Blades) in TabViewContainer.Current.TabCollection.Where((Tab) => Tab.Tag is FileControl)
                                                                                                             .Select((Tab) => (Tab, (Tab.Tag as FileControl).BladeViewer.Items.Cast<BladeItem>().ToArray())).ToArray())
                    {
                        if (Blades.Select((BItem) => (BItem.Content as FilePresenter)?.CurrentFolder?.Path)
                                  .All((BladePath) => Item.Path.Equals(Path.GetPathRoot(BladePath), StringComparison.OrdinalIgnoreCase)))
                        {
                            await TabViewContainer.Current.CleanUpAndRemoveTabItem(Tab);
                        }
                        else
                        {
                            foreach (BladeItem BItem in Blades.Where((BItem) => Item.Path.Equals(Path.GetPathRoot((BItem.Content as FilePresenter)?.CurrentFolder?.Path))))
                            {
                                await (Tab.Tag as FileControl).CloseBladeAsync(BItem);
                            }
                        }
                    }

                    using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableController())
                    {
                        if (await Exclusive.Controller.EjectPortableDevice(Item.Path))
                        {
                            ShowEjectNotification();
                        }
                        else
                        {
                            QueueContentDialog Dialog = new QueueContentDialog
                            {
                                Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                Content = Globalization.GetString("QueueContentDialog_UnableToEject_Content"),
                                CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                            };

                            await Dialog.ShowAsync().ConfigureAwait(false);
                        }
                    }
                }
            }
        }

        private void ShowEjectNotification()
        {
            try
            {
                ToastNotificationManager.History.Remove("MergeVideoNotification");

                ToastContentBuilder Builder = new ToastContentBuilder()
                                              .SetToastScenario(ToastScenario.Default)
                                              .AddToastActivationInfo("Transcode", ToastActivationType.Foreground)
                                              .AddText(Globalization.GetString("Eject_Toast_Text_1"))
                                              .AddText(Globalization.GetString("Eject_Toast_Text_2"))
                                              .AddText(Globalization.GetString("Eject_Toast_Text_3"));

                ToastNotificationManager.CreateToastNotifier().Show(new ToastNotification(Builder.GetToastContent().GetXml()));
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, "Toast notification could not be sent");
            }
        }

        private void LibraryGrid_DragItemsCompleted(ListViewBase sender, DragItemsCompletedEventArgs args)
        {
            SQLite.Current.ClearTable("Library");

            foreach (LibraryStorageFolder Item in CommonAccessCollection.LibraryFolderList)
            {
                SQLite.Current.SetLibraryPath(Item.LibType, Item.Path);
            }
        }

        private async void AddLibraryButton_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();

            FolderPicker Picker = new FolderPicker
            {
                ViewMode = PickerViewMode.List,
                SuggestedStartLocation = PickerLocationId.ComputerFolder
            };
            Picker.FileTypeFilter.Add("*");

            if (await Picker.PickSingleFolderAsync() is StorageFolder Folder)
            {
                if (CommonAccessCollection.LibraryFolderList.Any((Library) => Library.Path.Equals(Folder.Path, StringComparison.OrdinalIgnoreCase)))
                {
                    QueueContentDialog dialog = new QueueContentDialog
                    {
                        Title = Globalization.GetString("Common_Dialog_TipTitle"),
                        Content = Globalization.GetString("QueueDialog_RepeatAddToHomePage_Content"),
                        CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                    };

                    await dialog.ShowAsync();
                }
                else
                {
                    CommonAccessCollection.LibraryFolderList.Add(await LibraryStorageFolder.CreateAsync(LibraryType.UserCustom, Folder.Path));
                    SQLite.Current.SetLibraryPath(LibraryType.UserCustom, Folder.Path);
                    await JumpListController.Current.AddItemAsync(JumpListGroup.Library, Folder.Path);
                }
            }
        }

        private async void UnlockBitlocker_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();

            if (DriveGrid.SelectedItem is DriveDataBase Drive)
            {
                await OpenTargetDriveAsync(Drive);
            }
        }

        private async void LibraryExpander_Expanded(object sender, EventArgs e)
        {
            await CommonAccessCollection.LoadLibraryFoldersAsync();
        }

        private async void DeviceExpander_Expanded(object sender, EventArgs e)
        {
            await CommonAccessCollection.LoadDriveAsync();
        }

        private async void Copy_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();

            if (LibraryGrid.SelectedItems.Count > 0)
            {
                try
                {
                    Clipboard.Clear();
                    Clipboard.SetContent(await LibraryGrid.SelectedItems.Cast<LibraryStorageFolder>().GetAsDataPackageAsync(DataPackageOperation.Copy));
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

        private async void OpenFolderInNewTab_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();

            if (LibraryGrid.SelectedItem is LibraryStorageFolder Lib)
            {
                await TabViewContainer.Current.CreateNewTabAsync(Lib.Path);
            }
        }

        private async void OpenFolderInNewWindow_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();

            if (LibraryGrid.SelectedItem is LibraryStorageFolder Lib)
            {
                string StartupArgument = Uri.EscapeDataString(JsonSerializer.Serialize(new List<string[]>
                {
                    new string[]{ Lib.Path }
                }));

                await Launcher.LaunchUriAsync(new Uri($"rx-explorer:{StartupArgument}"));
            }
        }

        private async void OpenFolderInVerticalSplitView_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();

            if (LibraryGrid.SelectedItem is LibraryStorageFolder Lib)
            {
                await this.FindParentOfType<FileControl>()?.CreateNewBladeAsync(Lib.Path);
            }
        }

        private void SendToFlyout_Opening(object sender, object e)
        {
            if (sender is MenuFlyout Flyout)
            {
                foreach (MenuFlyoutItem Item in Flyout.Items)
                {
                    Item.Click -= SendToItem_Click;
                }

                Flyout.Items.Clear();

                MenuFlyoutItem SendDocumentItem = new MenuFlyoutItem
                {
                    Name = "SendDocumentItem",
                    Text = Globalization.GetString("SendTo_Document"),
                    Icon = new ImageIcon
                    {
                        Source = new BitmapImage(new Uri("ms-appx:///Assets/DocumentIcon.ico"))
                    },
                    MinWidth = 150,
                    MaxWidth = 350
                };
                SendDocumentItem.Click += SendToItem_Click;

                Flyout.Items.Add(SendDocumentItem);

                MenuFlyoutItem SendLinkItem = new MenuFlyoutItem
                {
                    Name = "SendLinkItem",
                    Text = Globalization.GetString("SendTo_CreateDesktopShortcut"),
                    Icon = new ImageIcon
                    {
                        Source = new BitmapImage(new Uri("ms-appx:///Assets/DesktopIcon.ico"))
                    },
                    MinWidth = 150,
                    MaxWidth = 350
                };
                SendLinkItem.Click += SendToItem_Click;

                Flyout.Items.Add(SendLinkItem);

                foreach (DriveDataBase RemovableDrive in CommonAccessCollection.DriveList.Where((Drive) => (Drive.DriveType is DriveType.Removable or DriveType.Network) && !string.IsNullOrEmpty(Drive.Path)).ToArray())
                {
                    MenuFlyoutItem SendRemovableDriveItem = new MenuFlyoutItem
                    {
                        Name = "SendRemovableItem",
                        Text = $"{(string.IsNullOrEmpty(RemovableDrive.DisplayName) ? RemovableDrive.Path : RemovableDrive.DisplayName)}",
                        Icon = new ImageIcon
                        {
                            Source = RemovableDrive.Thumbnail
                        },
                        MinWidth = 150,
                        MaxWidth = 350,
                        Tag = RemovableDrive.Path
                    };
                    SendRemovableDriveItem.Click += SendToItem_Click;

                    Flyout.Items.Add(SendRemovableDriveItem);
                }
            }
        }

        private void CommandBarFlyout_Closing(FlyoutBase sender, FlyoutBaseClosingEventArgs args)
        {
            if (sender is CommandBarFlyout Flyout)
            {
                foreach (AppBarButton Btn in Flyout.SecondaryCommands.OfType<AppBarButton>())
                {
                    Btn.Flyout?.Hide();
                }
            }
        }

        private async void SendToItem_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();

            if (sender is FrameworkElement Item)
            {
                if (LibraryGrid.SelectedItem is LibraryStorageFolder SItem)
                {
                    switch (Item.Name)
                    {
                        case "SendLinkItem":
                            {
                                string DesktopPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);

                                if (await FileSystemStorageItemBase.CheckExistAsync(DesktopPath))
                                {
                                    using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableController())
                                    {
                                        if (!await Exclusive.Controller.CreateLinkAsync(new LinkDataPackage
                                        {
                                            LinkPath = Path.Combine(DesktopPath, $"{SItem.Name}.lnk"),
                                            LinkTargetPath = SItem.Path
                                        }))
                                        {
                                            QueueContentDialog Dialog = new QueueContentDialog
                                            {
                                                Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                                Content = Globalization.GetString("QueueDialog_UnauthorizedCreateNewFile_Content"),
                                                CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                                            };

                                            await Dialog.ShowAsync();
                                        }
                                    }
                                }
                                else
                                {
                                    try
                                    {
                                        IReadOnlyList<User> UserList = await User.FindAllAsync();

                                        UserDataPaths DataPath = UserList.FirstOrDefault((User) => User.AuthenticationStatus == UserAuthenticationStatus.LocallyAuthenticated && User.Type == UserType.LocalUser) is User CurrentUser
                                                                 ? UserDataPaths.GetForUser(CurrentUser)
                                                                 : UserDataPaths.GetDefault();

                                        if (await FileSystemStorageItemBase.CheckExistAsync(DataPath.Desktop))
                                        {
                                            using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableController())
                                            {
                                                if (!await Exclusive.Controller.CreateLinkAsync(new LinkDataPackage
                                                {
                                                    LinkPath = Path.Combine(DataPath.Desktop, $"{SItem.Name}.lnk"),
                                                    LinkTargetPath = SItem.Path
                                                }))
                                                {
                                                    QueueContentDialog Dialog = new QueueContentDialog
                                                    {
                                                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                                        Content = Globalization.GetString("QueueDialog_UnauthorizedCreateNewFile_Content"),
                                                        CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                                                    };

                                                    await Dialog.ShowAsync();
                                                }
                                            }
                                        }
                                        else
                                        {
                                            LogTracer.Log($"Could not execute \"Send to\" command because desktop path \"{DataPath.Desktop}\" is not exists");
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        LogTracer.Log(ex, "Could not get desktop path from UserDataPaths");
                                    }
                                }

                                break;
                            }
                        case "SendDocumentItem":
                            {
                                string DocumentPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

                                if (await FileSystemStorageItemBase.CheckExistAsync(DocumentPath))
                                {
                                    QueueTaskController.EnqueueCopyOpeartion(SItem.Path, DocumentPath);
                                }
                                else
                                {
                                    try
                                    {
                                        IReadOnlyList<User> UserList = await User.FindAllAsync();

                                        UserDataPaths DataPath = UserList.FirstOrDefault((User) => User.AuthenticationStatus == UserAuthenticationStatus.LocallyAuthenticated && User.Type == UserType.LocalUser) is User CurrentUser
                                                                 ? UserDataPaths.GetForUser(CurrentUser)
                                                                 : UserDataPaths.GetDefault();

                                        if (await FileSystemStorageItemBase.CheckExistAsync(DataPath.Documents))
                                        {
                                            QueueTaskController.EnqueueCopyOpeartion(SItem.Path, DataPath.Documents);
                                        }
                                        else
                                        {
                                            LogTracer.Log($"Could not execute \"Send to\" command because document path \"{DataPath.Documents}\" is not exists");
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        LogTracer.Log(ex, "Could not get document path from UserDataPaths");
                                    }
                                }

                                break;
                            }
                        case "SendRemovableItem":
                            {
                                if (Item.Tag is string RemovablePath)
                                {
                                    QueueTaskController.EnqueueCopyOpeartion(SItem.Path, RemovablePath);
                                }

                                break;
                            }
                    }
                }
            }
        }

        private void LibraryGrid_DragOver(object sender, DragEventArgs e)
        {
            e.Handled = true;
            LibraryGrid.CanReorderItems = LibraryGrid.IsDragSource();
        }
    }
}
