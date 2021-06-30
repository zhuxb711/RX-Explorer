using Microsoft.Toolkit.Uwp.Notifications;
using Microsoft.Toolkit.Uwp.UI.Controls;
using RX_Explorer.Class;
using RX_Explorer.CustomControl;
using RX_Explorer.Dialog;
using RX_Explorer.SeparateWindow.PropertyWindow;
using ShareClassLibrary;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.System;
using Windows.UI;
using Windows.UI.Input;
using Windows.UI.Notifications;
using Windows.UI.WindowManagement;
using Windows.UI.WindowManagement.Preview;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Hosting;
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

        public Home()
        {
            InitializeComponent();

            LibraryExpander.IsExpanded = SettingControl.LibraryExpanderIsExpand;
            DeviceExpander.IsExpanded = SettingControl.DeviceExpanderIsExpand;

            Loaded += Home_Loaded;
        }

        private async void Home_Loaded(object sender, RoutedEventArgs e)
        {
            LibraryExpander.IsExpanded = SettingControl.LibraryExpanderIsExpand;
            DeviceExpander.IsExpanded = SettingControl.DeviceExpanderIsExpand;

            if (await MSStoreHelper.Current.CheckPurchaseStatusAsync())
            {
                OpenFolderInVerticalSplitView.Visibility = Visibility.Visible;
            }
        }

        private void CloseAllFlyout()
        {
            LibraryEmptyFlyout.Hide();
            DriveEmptyFlyout.Hide();
            DriveFlyout.Hide();
            LibraryFlyout.Hide();
            PortableDeviceFlyout.Hide();
            BitlockerDeviceFlyout.Hide();
        }

        private void DeviceGrid_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                args.ItemContainer.PointerEntered -= ItemContainer_PointerEntered;
            }
            else
            {
                args.ItemContainer.PointerEntered += ItemContainer_PointerEntered;

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
            }
        }

        private void LibraryGrid_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                args.ItemContainer.PointerEntered -= ItemContainer_PointerEntered;
            }
            else
            {
                args.ItemContainer.PointerEntered += ItemContainer_PointerEntered;
            }
        }

        private void ItemContainer_PointerEntered(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if (!SettingControl.IsDoubleClickEnable)
            {
                if ((e.OriginalSource as FrameworkElement)?.DataContext is object Item)
                {
                    if (Item is LibraryFolder)
                    {
                        LibraryGrid.SelectedItem = Item;
                        DriveGrid.SelectedIndex = -1;
                    }
                    else if (Item is DriveDataBase)
                    {
                        DriveGrid.SelectedItem = Item;
                        LibraryGrid.SelectedIndex = -1;
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

                            StorageFolder DriveFolder = await StorageFolder.GetFolderFromPathAsync(LockedDrive.Path);

                            DriveDataBase NewDrive = await DriveDataBase.CreateAsync(DriveFolder, LockedDrive.DriveType);

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
                        await OpenTargetFolder(Drive.DriveFolder).ConfigureAwait(false);
                        break;
                    }
            }
        }

        public async Task OpenTargetFolder(StorageFolder Folder)
        {
            if (Folder == null)
            {
                throw new ArgumentNullException(nameof(Folder), "Argument could not be null");
            }

            try
            {
                if (string.IsNullOrEmpty(Folder.Path))
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
                    EnterActionRequested?.Invoke(this, Folder.Path);
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, "An error was threw when entering device");
            }
        }

        private async void DeviceGrid_DoubleTapped(object sender, Windows.UI.Xaml.Input.DoubleTappedRoutedEventArgs e)
        {
            LibraryGrid.SelectedIndex = -1;

            if ((e.OriginalSource as FrameworkElement)?.DataContext is DriveDataBase Drive)
            {
                await OpenTargetDriveAsync(Drive);
            }
        }

        private async void LibraryGrid_DoubleTapped(object sender, Windows.UI.Xaml.Input.DoubleTappedRoutedEventArgs e)
        {
            DriveGrid.SelectedIndex = -1;

            if ((e.OriginalSource as FrameworkElement)?.DataContext is LibraryFolder Library)
            {
                await OpenTargetFolder(Library.Folder);
            }
        }

        private async void DeviceGrid_RightTapped(object sender, Windows.UI.Xaml.Input.RightTappedRoutedEventArgs e)
        {
            if (e.PointerDeviceType == Windows.Devices.Input.PointerDeviceType.Mouse)
            {
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
                        Position = e.GetPosition((FrameworkElement)sender),
                        Placement = FlyoutPlacementMode.RightEdgeAlignedTop
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

        private void DeviceGrid_PointerPressed(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
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
                if ((e.OriginalSource as FrameworkElement)?.DataContext is LibraryFolder Context)
                {
                    LibraryGrid.SelectedItem = Context;
                    await LibraryFlyout.ShowCommandBarFlyoutWithExtraContextMenuItems(LibraryGrid, e.GetPosition((FrameworkElement)sender), LibraryGrid.SelectedItems.Cast<LibraryFolder>().Select((Lib) => Lib.Path).ToArray());
                }
                else
                {
                    LibraryEmptyFlyout.ShowAt(LibraryGrid, new FlyoutShowOptions
                    {
                        Position = e.GetPosition((FrameworkElement)sender),
                        Placement = FlyoutPlacementMode.RightEdgeAlignedTop
                    });
                }
            }
        }

        private async void OpenLibrary_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();

            DriveGrid.SelectedIndex = -1;

            if (LibraryGrid.SelectedItem is LibraryFolder Library)
            {
                await OpenTargetFolder(Library.Folder);
            }
        }

        private async void RemovePin_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();

            if (LibraryGrid.SelectedItem is LibraryFolder Library)
            {
                CommonAccessCollection.LibraryFolderList.Remove(Library);
                SQLite.Current.DeleteLibrary(Library.Path);
                await JumpListController.Current.RemoveItem(JumpListGroup.Library, Library.Folder);
            }
        }

        private async void LibraryProperties_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();

            if (LibraryGrid.SelectedItem is LibraryFolder Library)
            {
                FileSystemStorageFolder Folder = await FileSystemStorageItemBase.CreateByStorageItemAsync(Library.Folder);

                if (Folder != null)
                {
                    await Folder.LoadAsync();

                    AppWindow NewWindow = await AppWindow.TryCreateAsync();
                    NewWindow.RequestSize(new Size(420, 600));
                    NewWindow.RequestMoveRelativeToCurrentViewContent(new Point(Window.Current.Bounds.Width / 2 - 200, Window.Current.Bounds.Height / 2 - 300));
                    NewWindow.PersistedStateId = "Properties";
                    NewWindow.Title = Globalization.GetString("Properties_Window_Title");
                    NewWindow.TitleBar.ExtendsContentIntoTitleBar = true;
                    NewWindow.TitleBar.ButtonForegroundColor = AppThemeController.Current.Theme == ElementTheme.Dark ? Colors.White : Colors.Black;
                    NewWindow.TitleBar.ButtonBackgroundColor = Colors.Transparent;
                    NewWindow.TitleBar.ButtonInactiveBackgroundColor = Colors.Transparent;

                    ElementCompositionPreview.SetAppWindowContent(NewWindow, new PropertyBase(NewWindow, Folder));
                    WindowManagementPreview.SetPreferredMinSize(NewWindow, new Size(420, 600));

                    await NewWindow.TryShowAsync();
                }
            }
        }

        private async void Refresh_Click(object sender, RoutedEventArgs e)
        {
            await CommonAccessCollection.LoadDriveAsync(true);
        }

        private async void DeviceGrid_ItemClick(object sender, ItemClickEventArgs e)
        {
            LibraryGrid.SelectedIndex = -1;

            if (!SettingControl.IsDoubleClickEnable && e.ClickedItem is DriveDataBase Drive)
            {
                await OpenTargetDriveAsync(Drive);
            }
        }

        private async void LibraryGrid_ItemClick(object sender, ItemClickEventArgs e)
        {
            DriveGrid.SelectedIndex = -1;

            if (!SettingControl.IsDoubleClickEnable && e.ClickedItem is LibraryFolder Library)
            {
                await OpenTargetFolder(Library.Folder).ConfigureAwait(false);
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
                if (DriveFolder.Path.Equals(Path.GetPathRoot(DriveFolder.Path), StringComparison.OrdinalIgnoreCase) && DriveInfo.GetDrives().Where((Drive) => Drive.DriveType == DriveType.Fixed || Drive.DriveType == DriveType.Removable || Drive.DriveType == DriveType.Network).Any((Item) => Item.RootDirectory.FullName == DriveFolder.Path))
                {
                    if (CommonAccessCollection.DriveList.All((Item) => !Item.Path.Equals(DriveFolder.Path, StringComparison.OrdinalIgnoreCase)))
                    {
                        CommonAccessCollection.DriveList.Add(await DriveDataBase.CreateAsync(DriveFolder, new DriveInfo(DriveFolder.Path).DriveType));
                    }
                    else
                    {
                        QueueContentDialog Dialog = new QueueContentDialog
                        {
                            Title = Globalization.GetString("Common_Dialog_TipTitle"),
                            Content = Globalization.GetString("QueueDialog_DeviceExist_Content"),
                            CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                        };
                        await Dialog.ShowAsync();
                    }
                }
                else
                {
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

        private async void Attribute_Click(object sender, RoutedEventArgs e)
        {
            DeviceInfoDialog Dialog = new DeviceInfoDialog(DriveGrid.SelectedItem as DriveDataBase);
            await Dialog.ShowAsync();
        }

        private async void LibraryGrid_Holding(object sender, Windows.UI.Xaml.Input.HoldingRoutedEventArgs e)
        {
            if (e.HoldingState == HoldingState.Started)
            {
                if ((e.OriginalSource as FrameworkElement)?.DataContext is LibraryFolder Context)
                {
                    LibraryGrid.SelectedItem = Context;
                    await LibraryFlyout.ShowCommandBarFlyoutWithExtraContextMenuItems(LibraryGrid, e.GetPosition((FrameworkElement)sender), LibraryGrid.SelectedItems.Cast<LibraryFolder>().Select((Lib) => Lib.Path).ToArray());
                }
                else
                {
                    LibraryFlyout.ShowAt(LibraryGrid, new FlyoutShowOptions
                    {
                        Position = e.GetPosition((FrameworkElement)sender),
                        Placement = FlyoutPlacementMode.RightEdgeAlignedTop
                    });
                }
            }
        }

        private async void DeviceGrid_Holding(object sender, Windows.UI.Xaml.Input.HoldingRoutedEventArgs e)
        {
            if (e.HoldingState == HoldingState.Started)
            {
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
                        Position = e.GetPosition((FrameworkElement)sender),
                        Placement = FlyoutPlacementMode.RightEdgeAlignedTop
                    });
                }
            }
        }

        private void LibraryExpander_Collapsed(object sender, EventArgs e)
        {
            SettingControl.LibraryExpanderIsExpand = false;
            LibraryGrid.SelectedIndex = -1;
        }

        private void DeviceExpander_Collapsed(object sender, EventArgs e)
        {
            SettingControl.DeviceExpanderIsExpand = false;
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
                    foreach ((TabViewItem Tab, BladeItem[] Blades) in TabViewContainer.ThisPage.TabCollection.Where((Tab) => Tab.Tag is FileControl)
                                                                                                             .Select((Tab) => (Tab, (Tab.Tag as FileControl).BladeViewer.Items.Cast<BladeItem>().ToArray())).ToArray())
                    {
                        if (Blades.Select((BItem) => (BItem.Content as FilePresenter)?.CurrentFolder?.Path)
                                  .All((BladePath) => Item.Path.Equals(Path.GetPathRoot(BladePath), StringComparison.OrdinalIgnoreCase)))
                        {
                            await TabViewContainer.ThisPage.CleanUpAndRemoveTabItem(Tab);
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

            foreach (LibraryFolder Item in CommonAccessCollection.LibraryFolderList)
            {
                SQLite.Current.SetLibraryPath(Item.Type, Item.Path);
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
                    CommonAccessCollection.LibraryFolderList.Add(await LibraryFolder.CreateAsync(Folder, LibraryType.UserCustom));
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
            SettingControl.LibraryExpanderIsExpand = true;
            await CommonAccessCollection.LoadLibraryFoldersAsync();
        }

        private async void DeviceExpander_Expanded(object sender, EventArgs e)
        {
            SettingControl.DeviceExpanderIsExpand = true;
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

                    List<FileSystemStorageFolder> TransformList = new List<FileSystemStorageFolder>();

                    foreach (LibraryFolder Lib in LibraryGrid.SelectedItems.Cast<LibraryFolder>())
                    {
                        FileSystemStorageFolder Folder = await FileSystemStorageItemBase.CreateByStorageItemAsync(Lib.Folder);

                        if (Folder != null)
                        {
                            TransformList.Add(Folder);
                        }
                    }

                    Clipboard.SetContent(await TransformList.GetAsDataPackageAsync(DataPackageOperation.Copy));
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

        private async void Cut_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();

            if (LibraryGrid.SelectedItems.Count > 0)
            {
                try
                {
                    Clipboard.Clear();

                    List<FileSystemStorageFolder> TransformList = new List<FileSystemStorageFolder>();

                    foreach (LibraryFolder Lib in LibraryGrid.SelectedItems.Cast<LibraryFolder>())
                    {
                        FileSystemStorageFolder Folder = await FileSystemStorageItemBase.CreateByStorageItemAsync(Lib.Folder);

                        if (Folder != null)
                        {
                            TransformList.Add(Folder);
                        }
                    }

                    Clipboard.SetContent(await TransformList.GetAsDataPackageAsync(DataPackageOperation.Move));
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

            if (LibraryGrid.SelectedItem is LibraryFolder Lib)
            {
                await TabViewContainer.ThisPage.CreateNewTabAsync(Lib.Path);
            }
        }

        private async void OpenFolderInNewWindow_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();

            if (LibraryGrid.SelectedItem is LibraryFolder Lib)
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

            if (LibraryGrid.SelectedItem is LibraryFolder Lib)
            {
                await this.FindParentOfType<FileControl>()?.CreateNewBladeAsync(Lib.Path);
            }
        }

        private void SendToFlyout_Opening(object sender, object e)
        {
            if (sender is MenuFlyout Flyout)
            {
                foreach (MenuFlyoutItemWithImage Item in Flyout.Items)
                {
                    Item.Click -= SendToItem_Click;
                }

                Flyout.Items.Clear();

                MenuFlyoutItemWithImage SendDocumentItem = new MenuFlyoutItemWithImage
                {
                    Name = "SendDocumentItem",
                    Text = Globalization.GetString("SendTo_Document"),
                    ImageIcon = new BitmapImage(new Uri("ms-appx:///Assets/DocumentIcon.ico")),
                    MinWidth = 150,
                    MaxWidth = 350
                };
                SendDocumentItem.Click += SendToItem_Click;

                Flyout.Items.Add(SendDocumentItem);

                MenuFlyoutItemWithImage SendLinkItem = new MenuFlyoutItemWithImage
                {
                    Name = "SendLinkItem",
                    Text = Globalization.GetString("SendTo_CreateDesktopShortcut"),
                    ImageIcon = new BitmapImage(new Uri("ms-appx:///Assets/DesktopIcon.ico")),
                    MinWidth = 150,
                    MaxWidth = 350
                };
                SendLinkItem.Click += SendToItem_Click;

                Flyout.Items.Add(SendLinkItem);

                foreach (DriveDataBase RemovableDrive in CommonAccessCollection.DriveList.Where((Drive) => (Drive.DriveType == DriveType.Removable || Drive.DriveType == DriveType.Network) && !string.IsNullOrEmpty(Drive.Path)))
                {
                    MenuFlyoutItemWithImage SendRemovableDriveItem = new MenuFlyoutItemWithImage
                    {
                        Name = "SendRemovableItem",
                        Text = $"{(string.IsNullOrEmpty(RemovableDrive.DisplayName) ? RemovableDrive.Path : RemovableDrive.DisplayName)}",
                        ImageIcon = RemovableDrive.Thumbnail,
                        MinWidth = 150,
                        MaxWidth = 350,
                        Tag = RemovableDrive.Path
                    };
                    SendRemovableDriveItem.Click += SendToItem_Click;

                    Flyout.Items.Add(SendRemovableDriveItem);
                }
            }
        }

        private async void SendToItem_Click(object sender, RoutedEventArgs e)
        {
            CloseAllFlyout();

            if (sender is MenuFlyoutItemWithImage Item)
            {
                if (LibraryGrid.SelectedItem is LibraryFolder SItem)
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
                                        if (!await Exclusive.Controller.CreateLinkAsync(Path.Combine(DesktopPath, $"{SItem.Name}.lnk"),
                                                                                        SItem.Path,
                                                                                        string.Empty,
                                                                                        WindowState.Normal,
                                                                                        0,
                                                                                        string.Empty))
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
                                                if (!await Exclusive.Controller.CreateLinkAsync(Path.Combine(DataPath.Desktop, $"{SItem.Name}.lnk"),
                                                                                                SItem.Path,
                                                                                                string.Empty,
                                                                                                WindowState.Normal,
                                                                                                0,
                                                                                                string.Empty))
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
    }
}
