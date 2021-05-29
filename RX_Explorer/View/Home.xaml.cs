using Microsoft.Toolkit.Uwp.Notifications;
using Microsoft.Toolkit.Uwp.UI.Controls;
using RX_Explorer.Class;
using RX_Explorer.Dialog;
using RX_Explorer.SeparateWindow.PropertyWindow;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.UI;
using Windows.UI.Input;
using Windows.UI.Notifications;
using Windows.UI.WindowManagement;
using Windows.UI.WindowManagement.Preview;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Media.Animation;
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

        private void Home_Loaded(object sender, RoutedEventArgs e)
        {
            LibraryExpander.IsExpanded = SettingControl.LibraryExpanderIsExpand;
            DeviceExpander.IsExpanded = SettingControl.DeviceExpanderIsExpand;
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
                        DeviceGrid.SelectedIndex = -1;
                    }
                    else if (Item is DriveDataBase)
                    {
                        DeviceGrid.SelectedItem = Item;
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
            DeviceGrid.SelectedIndex = -1;

            if ((e.OriginalSource as FrameworkElement)?.DataContext is LibraryFolder Library)
            {
                await OpenTargetFolder(Library.Folder).ConfigureAwait(false);
            }
        }

        private void DeviceGrid_RightTapped(object sender, Windows.UI.Xaml.Input.RightTappedRoutedEventArgs e)
        {
            if (e.PointerDeviceType == Windows.Devices.Input.PointerDeviceType.Mouse)
            {
                if ((e.OriginalSource as FrameworkElement)?.DataContext is DriveDataBase Context)
                {
                    DeviceGrid.SelectedItem = Context;

                    if (Context is LockedDriveData)
                    {
                        DeviceGrid.ContextFlyout = BitlockerDeviceFlyout;
                    }
                    else
                    {
                        DeviceGrid.ContextFlyout = Context.DriveType == DriveType.Removable ? PortableDeviceFlyout : DeviceFlyout;
                    }
                }
                else
                {
                    DeviceGrid.SelectedIndex = -1;
                    DeviceGrid.ContextFlyout = EmptyFlyout;
                }
            }
        }

        private async void OpenDevice_Click(object sender, RoutedEventArgs e)
        {
            LibraryGrid.SelectedIndex = -1;

            if (DeviceGrid.SelectedItem is DriveDataBase Drive)
            {
                await OpenTargetDriveAsync(Drive);
            }
        }

        private void DeviceGrid_PointerPressed(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            if ((e.OriginalSource as FrameworkElement)?.DataContext == null)
            {
                DeviceGrid.SelectedIndex = -1;
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
                DeviceGrid.SelectedIndex = -1;
            }
        }

        private void Grid_PointerPressed(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            DeviceGrid.SelectedIndex = -1;
            LibraryGrid.SelectedIndex = -1;
        }

        private void LibraryGrid_RightTapped(object sender, Windows.UI.Xaml.Input.RightTappedRoutedEventArgs e)
        {
            if (e.PointerDeviceType == Windows.Devices.Input.PointerDeviceType.Mouse)
            {
                if ((e.OriginalSource as FrameworkElement)?.DataContext is LibraryFolder Context)
                {
                    LibraryGrid.SelectedItem = Context;
                    LibraryGrid.ContextFlyout = LibraryFlyout;
                }
                else
                {
                    LibraryGrid.ContextFlyout = LibraryEmptyFlyout;
                }
            }
        }

        private async void OpenLibrary_Click(object sender, RoutedEventArgs e)
        {
            DeviceGrid.SelectedIndex = -1;

            if (LibraryGrid.SelectedItem is LibraryFolder Library)
            {
                await OpenTargetFolder(Library.Folder).ConfigureAwait(false);
            }
        }

        private async void RemovePin_Click(object sender, RoutedEventArgs e)
        {
            if (LibraryGrid.SelectedItem is LibraryFolder Library)
            {
                CommonAccessCollection.LibraryFolderList.Remove(Library);
                SQLite.Current.DeleteLibrary(Library.Folder.Path);
                await JumpListController.Current.RemoveItem(JumpListGroup.Library, Library.Folder).ConfigureAwait(false);
            }
        }

        private async void LibraryProperties_Click(object sender, RoutedEventArgs e)
        {
            if (LibraryGrid.SelectedItem is LibraryFolder Library)
            {
                FileSystemStorageFolder Folder = await FileSystemStorageItemBase.CreatedByStorageItemAsync(Library.Folder);

                if (Folder != null)
                {
                    await Folder.LoadMorePropertiesAsync();

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
            DeviceGrid.SelectedIndex = -1;

            if (!SettingControl.IsDoubleClickEnable && e.ClickedItem is LibraryFolder Library)
            {
                await OpenTargetFolder(Library.Folder).ConfigureAwait(false);
            }
        }

        private async void AddDevice_Click(object sender, RoutedEventArgs e)
        {
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
            DeviceInfoDialog Dialog = new DeviceInfoDialog(DeviceGrid.SelectedItem as DriveDataBase);
            await Dialog.ShowAsync();
        }

        private void LibraryGrid_Holding(object sender, Windows.UI.Xaml.Input.HoldingRoutedEventArgs e)
        {
            if (e.HoldingState == HoldingState.Started)
            {
                if ((e.OriginalSource as FrameworkElement)?.DataContext is LibraryFolder Context)
                {
                    LibraryGrid.SelectedItem = Context;
                    LibraryGrid.ContextFlyout = LibraryFlyout;
                }
                else
                {
                    LibraryGrid.ContextFlyout = null;
                }
            }
        }

        private void DeviceGrid_Holding(object sender, Windows.UI.Xaml.Input.HoldingRoutedEventArgs e)
        {
            if (e.HoldingState == HoldingState.Started)
            {
                if ((e.OriginalSource as FrameworkElement)?.DataContext is DriveDataBase Context)
                {
                    DeviceGrid.SelectedItem = Context;
                    DeviceGrid.ContextFlyout = Context.DriveType == DriveType.Removable ? PortableDeviceFlyout : DeviceFlyout;
                }
                else
                {
                    DeviceGrid.SelectedIndex = -1;
                    DeviceGrid.ContextFlyout = EmptyFlyout;
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
            DeviceGrid.SelectedIndex = -1;
        }

        private async void EjectButton_Click(object sender, RoutedEventArgs e)
        {
            if (DeviceGrid.SelectedItem is DriveDataBase Item)
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
                SQLite.Current.SetLibraryPath(Item.Folder.Path, Item.Type);
            }
        }

        private async void AddLibraryButton_Click(object sender, RoutedEventArgs e)
        {
            FolderPicker Picker = new FolderPicker
            {
                ViewMode = PickerViewMode.List,
                SuggestedStartLocation = PickerLocationId.ComputerFolder
            };
            Picker.FileTypeFilter.Add("*");

            if (await Picker.PickSingleFolderAsync() is StorageFolder Folder)
            {
                if (CommonAccessCollection.LibraryFolderList.Any((Library) => Library.Folder.Path.Equals(Folder.Path, StringComparison.OrdinalIgnoreCase)))
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
                    SQLite.Current.SetLibraryPath(Folder.Path, LibraryType.UserCustom);
                    await JumpListController.Current.AddItemAsync(JumpListGroup.Library, Folder.Path).ConfigureAwait(false);
                }
            }
        }

        private async void UnlockBitlocker_Click(object sender, RoutedEventArgs e)
        {
            if (DeviceGrid.SelectedItem is DriveDataBase Drive)
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
    }
}
