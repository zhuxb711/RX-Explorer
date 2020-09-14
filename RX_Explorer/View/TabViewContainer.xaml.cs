using HtmlAgilityPack;
using Microsoft.UI.Xaml.Controls;
using RX_Explorer.Class;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Core;
using Windows.ApplicationModel.DataTransfer;
using Windows.Devices.Enumeration;
using Windows.Devices.Portable;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.System;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Media.Imaging;
using SymbolIconSource = Microsoft.UI.Xaml.Controls.SymbolIconSource;
using TabView = Microsoft.UI.Xaml.Controls.TabView;
using TabViewTabCloseRequestedEventArgs = Microsoft.UI.Xaml.Controls.TabViewTabCloseRequestedEventArgs;

namespace RX_Explorer
{
    public sealed partial class TabViewContainer : Page, INotifyPropertyChanged
    {
        private int LockResource;

        public static Frame CurrentTabNavigation { get; private set; }

        private readonly DeviceWatcher PortalDeviceWatcher = DeviceInformation.CreateWatcher(DeviceClass.PortableStorageDevice);

        public static TabViewContainer ThisPage { get; private set; }

        public GridLength LeftSideLength
        {
            get
            {
                if (ApplicationData.Current.LocalSettings.Values["IsLeftAreaOpen"] is bool Enable)
                {
                    return Enable ? new GridLength(2.5, GridUnitType.Star) : new GridLength(0);
                }
                else
                {
                    ApplicationData.Current.LocalSettings.Values["IsLeftAreaOpen"] = true;

                    return new GridLength(2.5, GridUnitType.Star);
                }
            }
            set
            {
                if (value.Value == 0)
                {
                    ApplicationData.Current.LocalSettings.Values["IsLeftAreaOpen"] = false;
                }
                else
                {
                    ApplicationData.Current.LocalSettings.Values["IsLeftAreaOpen"] = true;
                }

                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LeftSideLength)));
            }
        }

        public GridLength TreeViewLength
        {
            get
            {
                if (ApplicationData.Current.LocalSettings.Values["DetachTreeViewAndPresenter"] is bool Enable)
                {
                    return Enable ? new GridLength(0) : new GridLength(2, GridUnitType.Star);
                }
                else
                {
                    ApplicationData.Current.LocalSettings.Values["DetachTreeViewAndPresenter"] = false;
                    return new GridLength(2, GridUnitType.Star);
                }
            }
            set
            {
                if (value.Value == 0)
                {
                    ApplicationData.Current.LocalSettings.Values["DetachTreeViewAndPresenter"] = true;
                    SettingControl.IsDetachTreeViewAndPresenter = true;
                }
                else
                {
                    ApplicationData.Current.LocalSettings.Values["DetachTreeViewAndPresenter"] = false;
                    SettingControl.IsDetachTreeViewAndPresenter = false;
                }

                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TreeViewLength)));
            }
        }

        public static bool LibraryExpanderIsExpand
        {
            get
            {
                if (ApplicationData.Current.LocalSettings.Values["LibraryExpanderIsExpand"] is bool IsExpand)
                {
                    return IsExpand;
                }
                else
                {
                    ApplicationData.Current.LocalSettings.Values["LibraryExpanderIsExpand"] = true;
                    return true;
                }
            }
            set
            {
                ApplicationData.Current.LocalSettings.Values["LibraryExpanderIsExpand"] = value;
            }
        }

        public static bool DeviceExpanderIsExpand
        {
            get
            {
                if (ApplicationData.Current.LocalSettings.Values["DeviceExpanderIsExpand"] is bool IsExpand)
                {
                    return IsExpand;
                }
                else
                {
                    ApplicationData.Current.LocalSettings.Values["DeviceExpanderIsExpand"] = true;
                    return true;
                }
            }
            set
            {
                ApplicationData.Current.LocalSettings.Values["DeviceExpanderIsExpand"] = value;
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public TabViewContainer()
        {
            InitializeComponent();
            ThisPage = this;
            Loaded += TabViewContainer_Loaded;
            PortalDeviceWatcher.Added += PortalDeviceWatcher_Added;
            PortalDeviceWatcher.Removed += PortalDeviceWatcher_Removed;
            Application.Current.Resuming += Current_Resuming;
            Application.Current.Suspending += Current_Suspending;
            CoreWindow.GetForCurrentThread().PointerPressed += TabViewContainer_PointerPressed;
            CoreWindow.GetForCurrentThread().KeyDown += TabViewContainer_KeyDown;
        }

        private async void TabViewContainer_KeyDown(CoreWindow sender, KeyEventArgs args)
        {
            if (!QueueContentDialog.IsRunningOrWaiting && CurrentTabNavigation.Content is ThisPC PC)
            {
                args.Handled = true;

                switch (args.VirtualKey)
                {
                    case VirtualKey.Space when SettingControl.IsQuicklookAvailable && SettingControl.IsQuicklookEnable:
                        {
                            if (PC.DeviceGrid.SelectedItem is HardDeviceInfo Device)
                            {
                                await FullTrustExcutorController.Current.ViewWithQuicklookAsync(Device.Folder.Path).ConfigureAwait(false);
                            }
                            else if (PC.LibraryGrid.SelectedItem is LibraryFolder Library)
                            {
                                await FullTrustExcutorController.Current.ViewWithQuicklookAsync(Library.Folder.Path).ConfigureAwait(false);
                            }
                            break;
                        }
                    case VirtualKey.F5:
                        {
                            PC.Refresh_Click(null, null);
                            break;
                        }
                    case VirtualKey.Enter:
                        {
                            if (PC.DeviceGrid.SelectedItem is HardDeviceInfo Device)
                            {
                                await FullTrustExcutorController.Current.ViewWithQuicklookAsync(Device.Folder.Path).ConfigureAwait(false);
                            }
                            else if (PC.LibraryGrid.SelectedItem is LibraryFolder Library)
                            {
                                await FullTrustExcutorController.Current.ViewWithQuicklookAsync(Library.Folder.Path).ConfigureAwait(false);
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

            if (CurrentTabNavigation?.Content is FileControl Control)
            {
                if (BackButtonPressed)
                {
                    args.Handled = true;
                    SettingControl.IsInputFromPrimaryButton = false;

                    if (!QueueContentDialog.IsRunningOrWaiting)
                    {
                        if (Control.Nav.CurrentSourcePageType.Name == nameof(FilePresenter))
                        {
                            if (Control.GoBackRecord.IsEnabled)
                            {
                                Control.GoBackRecord_Click(null, null);
                            }
                            else
                            {
                                MainPage.ThisPage.NavView_BackRequested(null, null);
                            }
                        }
                        else
                        {
                            if(Control.Nav.CanGoBack)
                            {
                                Control.Nav.GoBack();
                            }
                        }
                    }
                }
                else if (ForwardButtonPressed)
                {
                    args.Handled = true;
                    SettingControl.IsInputFromPrimaryButton = false;

                    if (Control.Nav.CurrentSourcePageType.Name == nameof(FilePresenter) && !QueueContentDialog.IsRunningOrWaiting && Control.GoForwardRecord.IsEnabled)
                    {
                        Control.GoForwardRecord_Click(null, null);
                    }
                }
                else
                {
                    SettingControl.IsInputFromPrimaryButton = true;
                }
            }
            else if (CurrentTabNavigation?.Content is ThisPC PC)
            {
                if (BackButtonPressed)
                {
                    args.Handled = true;
                    SettingControl.IsInputFromPrimaryButton = false;

                    MainPage.ThisPage.NavView_BackRequested(null, null);
                }
                else if (ForwardButtonPressed)
                {
                    args.Handled = true;
                    SettingControl.IsInputFromPrimaryButton = false;
                }
                else
                {
                    SettingControl.IsInputFromPrimaryButton = true;
                }
            }
        }

        public async Task CreateNewTabAndOpenTargetFolder(string Path)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(Path))
                {
                    if (CreateNewTab() is TabViewItem Item)
                    {
                        TabViewControl.TabItems.Add(Item);
                        TabViewControl.UpdateLayout();
                        TabViewControl.SelectedItem = Item;

                        if (TabViewControl.TabItems.Count > 1)
                        {
                            foreach (TabViewItem Tab in TabViewControl.TabItems)
                            {
                                Tab.IsClosable = true;
                            }
                        }
                    }
                }
                else
                {
                    if (WIN_Native_API.CheckIfHidden(Path))
                    {
                        QueueContentDialog Dialog = new QueueContentDialog
                        {
                            Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                            Content = Globalization.GetString("QueueDialog_ItemHidden_Content"),
                            CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                        };

                        _ = await Dialog.ShowAsync().ConfigureAwait(false);
                    }
                    else
                    {
                        if (CreateNewTab(await StorageFolder.GetFolderFromPathAsync(Path)) is TabViewItem Item)
                        {
                            TabViewControl.TabItems.Add(Item);
                            TabViewControl.UpdateLayout();
                            TabViewControl.SelectedItem = Item;

                            if (TabViewControl.TabItems.Count > 1)
                            {
                                foreach (TabViewItem Tab in TabViewControl.TabItems)
                                {
                                    Tab.IsClosable = true;
                                }
                            }
                        }
                    }
                }
            }
            catch
            {
                Debug.WriteLine("Error happened when try to create a new tab");
            }
        }

        private void Current_Suspending(object sender, SuspendingEventArgs e)
        {
            if (PortalDeviceWatcher != null && (PortalDeviceWatcher.Status == DeviceWatcherStatus.Started || PortalDeviceWatcher.Status == DeviceWatcherStatus.EnumerationCompleted))
            {
                PortalDeviceWatcher.Stop();
            }
        }

        private void Current_Resuming(object sender, object e)
        {
            switch (PortalDeviceWatcher.Status)
            {
                case DeviceWatcherStatus.Created:
                case DeviceWatcherStatus.Aborted:
                case DeviceWatcherStatus.Stopped:
                    {
                        PortalDeviceWatcher?.Start();
                        break;
                    }
            }
        }

        public static void GoBack()
        {
            if (CurrentTabNavigation.Content is FileControl Control)
            {
                if (Control.Nav.CanGoBack)
                {
                    Control.Nav.GoBack();
                }
                else if (CurrentTabNavigation.CanGoBack)
                {
                    CurrentTabNavigation.GoBack();
                }
            }
            else if (CurrentTabNavigation.CanGoBack)
            {
                CurrentTabNavigation.GoBack();
            }
        }

        private async void PortalDeviceWatcher_Removed(DeviceWatcher sender, DeviceInformationUpdate args)
        {
            try
            {
                List<string> AllBaseDevice = DriveInfo.GetDrives().Where((Drives) => Drives.DriveType == DriveType.Fixed || Drives.DriveType == DriveType.Network)
                                                                  .Select((Info) => Info.RootDirectory.FullName).ToList();

                List<StorageFolder> PortableDevice = new List<StorageFolder>();

                foreach (DeviceInformation Device in await DeviceInformation.FindAllAsync(StorageDevice.GetDeviceSelector()))
                {
                    try
                    {
                        PortableDevice.Add(StorageDevice.FromId(Device.Id));
                    }
                    catch
                    {
                        Debug.WriteLine($"Error happened when get storagefolder from {Device.Name}");
                    }
                }

                foreach (string PortDevice in AllBaseDevice.Where((Path) => PortableDevice.Any((Item) => Item.Path == Path)))
                {
                    AllBaseDevice.Remove(PortDevice);
                }

                List<HardDeviceInfo> OneStepDeviceList = CommonAccessCollection.HardDeviceList.Where((Item) => !AllBaseDevice.Contains(Item.Folder.Path)).ToList();
                List<HardDeviceInfo> TwoStepDeviceList = OneStepDeviceList.Where((RemoveItem) => PortableDevice.All((Item) => Item.Name != RemoveItem.Folder.Name)).ToList();

                foreach (HardDeviceInfo Device in TwoStepDeviceList)
                {
                    await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                    {
                        for (int j = 0; j < TabViewControl.TabItems.Count; j++)
                        {
                            if (((TabViewControl.TabItems[j] as TabViewItem)?.Content as Frame)?.Content is FileControl Control && Path.GetPathRoot(Control.CurrentFolder.Path) == Device.Folder.Path)
                            {
                                if (TabViewControl.TabItems.Count == 1)
                                {
                                    while (CurrentTabNavigation.CanGoBack)
                                    {
                                        CurrentTabNavigation.GoBack();
                                    }
                                }
                                else
                                {
                                    CommonAccessCollection.UnRegister(Control);

                                    Control.Dispose();

                                    TabViewControl.TabItems.RemoveAt(j);

                                    if (TabViewControl.TabItems.Count == 1)
                                    {
                                        (TabViewControl.TabItems.First() as TabViewItem).IsClosable = false;
                                    }
                                }
                            }
                        }

                        CommonAccessCollection.HardDeviceList.Remove(Device);
                    });
                }
            }
            catch
            {
                Debug.WriteLine($"Error happened when remove device from HardDeviceList");
            }
        }

        private async void PortalDeviceWatcher_Added(DeviceWatcher sender, DeviceInformation args)
        {
            try
            {
                StorageFolder DeviceFolder = StorageDevice.FromId(args.Id);

                if (CommonAccessCollection.HardDeviceList.All((Device) => (string.IsNullOrEmpty(Device.Folder.Path) || string.IsNullOrEmpty(DeviceFolder.Path)) ? Device.Folder.Name != DeviceFolder.Name : Device.Folder.Path != DeviceFolder.Path))
                {
                    BasicProperties Properties = await DeviceFolder.GetBasicPropertiesAsync();
                    IDictionary<string, object> PropertiesRetrieve = await Properties.RetrievePropertiesAsync(new string[] { "System.Capacity", "System.FreeSpace", "System.Volume.FileSystem" });

                    if (PropertiesRetrieve["System.Capacity"] is ulong && PropertiesRetrieve["System.FreeSpace"] is ulong)
                    {
                        await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
                        {
                            CommonAccessCollection.HardDeviceList.Add(new HardDeviceInfo(DeviceFolder, await DeviceFolder.GetThumbnailBitmapAsync().ConfigureAwait(true), PropertiesRetrieve, DriveType.Removable));
                        });
                    }
                    else
                    {
                        IReadOnlyList<IStorageItem> InnerItemList = await DeviceFolder.GetItemsAsync(0, 2);

                        if (InnerItemList.Count == 1 && InnerItemList[0] is StorageFolder InnerFolder)
                        {
                            BasicProperties InnerProperties = await InnerFolder.GetBasicPropertiesAsync();
                            IDictionary<string, object> InnerPropertiesRetrieve = await InnerProperties.RetrievePropertiesAsync(new string[] { "System.Capacity", "System.FreeSpace", "System.Volume.FileSystem" });

                            if (InnerPropertiesRetrieve["System.Capacity"] is ulong && InnerPropertiesRetrieve["System.FreeSpace"] is ulong)
                            {
                                await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
                                {
                                    CommonAccessCollection.HardDeviceList.Add(new HardDeviceInfo(DeviceFolder, await DeviceFolder.GetThumbnailBitmapAsync().ConfigureAwait(true), InnerPropertiesRetrieve, DriveType.Removable));
                                });
                            }
                            else
                            {
                                await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
                                {
                                    CommonAccessCollection.HardDeviceList.Add(new HardDeviceInfo(DeviceFolder, await DeviceFolder.GetThumbnailBitmapAsync().ConfigureAwait(true), PropertiesRetrieve, DriveType.Removable));
                                });
                            }
                        }
                        else
                        {
                            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
                            {
                                CommonAccessCollection.HardDeviceList.Add(new HardDeviceInfo(DeviceFolder, await DeviceFolder.GetThumbnailBitmapAsync().ConfigureAwait(true), PropertiesRetrieve, DriveType.Removable));
                            });
                        }
                    }
                }
            }
            catch
            {
                if (!ApplicationData.Current.LocalSettings.Values.ContainsKey("DisableDeviceFailTip"))
                {
                    await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
                    {
                        QueueContentDialog Dialog = new QueueContentDialog
                        {
                            Title = Globalization.GetString("Common_Dialog_TipTitle"),
                            Content = $"{Globalization.GetString("QueueDialog_AddDeviceFail_Content")} \"{args.Name}\"",
                            PrimaryButtonText = Globalization.GetString("Common_Dialog_DoNotTip"),
                            CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                        };

                        if (await Dialog.ShowAsync().ConfigureAwait(true) == ContentDialogResult.Primary)
                        {
                            ApplicationData.Current.LocalSettings.Values["DisableDeviceFailTip"] = true;
                        }
                    });
                }
            }
        }

        private async void TabViewContainer_Loaded(object sender, RoutedEventArgs e)
        {
            Loaded -= TabViewContainer_Loaded;

            if (MainPage.ThisPage.IsPathActivate)
            {
                try
                {
                    await CreateNewTabAndOpenTargetFolder(MainPage.ThisPage.ActivatePath).ConfigureAwait(true);
                }
                catch
                {
                    Debug.WriteLine("ActivatePath is not exist, activate action stop");
                }
                finally
                {
                    MainPage.ThisPage.IsPathActivate = false;
                }
            }
            else
            {
                if (CreateNewTab() is TabViewItem TabItem)
                {
                    TabViewControl.TabItems.Add(TabItem);
                }
            }

            try
            {
                foreach (KeyValuePair<QuickStartType, QuickStartItem> Item in await SQLite.Current.GetQuickStartItemAsync().ConfigureAwait(true))
                {
                    if (Item.Key == QuickStartType.Application)
                    {
                        CommonAccessCollection.QuickStartList.Add(Item.Value);
                    }
                    else
                    {
                        CommonAccessCollection.HotWebList.Add(Item.Value);
                    }
                }

                if (!ApplicationData.Current.LocalSettings.Values.ContainsKey("IsLibraryInitialized"))
                {
                    try
                    {
                        IReadOnlyList<User> UserList = await User.FindAllAsync();

                        UserDataPaths DataPath = UserList.Count > 1 ? UserDataPaths.GetForUser(UserList[0]) : UserDataPaths.GetDefault();

                        try
                        {
                            if (!string.IsNullOrEmpty(DataPath.Downloads))
                            {
                                await SQLite.Current.SetLibraryPathAsync(DataPath.Downloads, LibraryType.Downloads).ConfigureAwait(true);
                            }

                            if (!string.IsNullOrEmpty(DataPath.Desktop))
                            {
                                await SQLite.Current.SetLibraryPathAsync(DataPath.Desktop, LibraryType.Desktop).ConfigureAwait(true);
                            }

                            if (!string.IsNullOrEmpty(DataPath.Videos))
                            {
                                await SQLite.Current.SetLibraryPathAsync(DataPath.Videos, LibraryType.Videos).ConfigureAwait(true);
                            }

                            if (!string.IsNullOrEmpty(DataPath.Pictures))
                            {
                                await SQLite.Current.SetLibraryPathAsync(DataPath.Pictures, LibraryType.Pictures).ConfigureAwait(true);
                            }

                            if (!string.IsNullOrEmpty(DataPath.Documents))
                            {
                                await SQLite.Current.SetLibraryPathAsync(DataPath.Documents, LibraryType.Document).ConfigureAwait(true);
                            }

                            if (!string.IsNullOrEmpty(DataPath.Music))
                            {
                                await SQLite.Current.SetLibraryPathAsync(DataPath.Music, LibraryType.Music).ConfigureAwait(true);
                            }

                            if (!string.IsNullOrEmpty(DataPath.Profile))
                            {
                                await SQLite.Current.SetLibraryPathAsync(Path.Combine(DataPath.Profile, "OneDrive"), LibraryType.OneDrive).ConfigureAwait(true);
                            }
                        }
                        catch
                        {

                        }
                    }
                    catch
                    {
                        string DesktopPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                        if (!string.IsNullOrEmpty(DesktopPath))
                        {
                            await SQLite.Current.SetLibraryPathAsync(DesktopPath, LibraryType.Desktop).ConfigureAwait(true);
                        }

                        string VideoPath = Environment.GetFolderPath(Environment.SpecialFolder.MyVideos);
                        if (!string.IsNullOrEmpty(VideoPath))
                        {
                            await SQLite.Current.SetLibraryPathAsync(VideoPath, LibraryType.Videos).ConfigureAwait(true);
                        }

                        string PicturesPath = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
                        if (!string.IsNullOrEmpty(PicturesPath))
                        {
                            await SQLite.Current.SetLibraryPathAsync(PicturesPath, LibraryType.Pictures).ConfigureAwait(true);
                        }

                        string DocumentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                        if (!string.IsNullOrEmpty(DocumentsPath))
                        {
                            await SQLite.Current.SetLibraryPathAsync(DocumentsPath, LibraryType.Document).ConfigureAwait(true);
                        }

                        string MusicPath = Environment.GetFolderPath(Environment.SpecialFolder.MyMusic);
                        if (!string.IsNullOrEmpty(MusicPath))
                        {
                            await SQLite.Current.SetLibraryPathAsync(MusicPath, LibraryType.Music).ConfigureAwait(true);
                        }

                        string ProfilePath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                        if (!string.IsNullOrEmpty(ProfilePath))
                        {
                            await SQLite.Current.SetLibraryPathAsync(Path.Combine(ProfilePath, "OneDrive"), LibraryType.OneDrive).ConfigureAwait(true);
                        }
                    }
                    finally
                    {
                        ApplicationData.Current.LocalSettings.Values["IsLibraryInitialized"] = true;
                    }
                }
                else
                {
                    try
                    {
                        IReadOnlyList<User> UserList = await User.FindAllAsync();

                        UserDataPaths DataPath = UserList.Count > 1 ? UserDataPaths.GetForUser(UserList[0]) : UserDataPaths.GetDefault();

                        try
                        {
                            if (!string.IsNullOrEmpty(DataPath.Downloads))
                            {
                                await SQLite.Current.UpdateLibraryAsync(DataPath.Downloads, LibraryType.Downloads).ConfigureAwait(true);
                            }

                            if (!string.IsNullOrEmpty(DataPath.Desktop))
                            {
                                await SQLite.Current.UpdateLibraryAsync(DataPath.Desktop, LibraryType.Desktop).ConfigureAwait(true);
                            }

                            if (!string.IsNullOrEmpty(DataPath.Videos))
                            {
                                await SQLite.Current.UpdateLibraryAsync(DataPath.Videos, LibraryType.Videos).ConfigureAwait(true);
                            }

                            if (!string.IsNullOrEmpty(DataPath.Pictures))
                            {
                                await SQLite.Current.UpdateLibraryAsync(DataPath.Pictures, LibraryType.Pictures).ConfigureAwait(true);
                            }

                            if (!string.IsNullOrEmpty(DataPath.Documents))
                            {
                                await SQLite.Current.UpdateLibraryAsync(DataPath.Documents, LibraryType.Document).ConfigureAwait(true);
                            }

                            if (!string.IsNullOrEmpty(DataPath.Music))
                            {
                                await SQLite.Current.UpdateLibraryAsync(DataPath.Music, LibraryType.Music).ConfigureAwait(true);
                            }

                            if (!string.IsNullOrEmpty(DataPath.Profile))
                            {
                                await SQLite.Current.UpdateLibraryAsync(Path.Combine(DataPath.Profile, "OneDrive"), LibraryType.OneDrive).ConfigureAwait(true);
                            }
                        }
                        catch
                        {

                        }
                    }
                    catch
                    {
                        string DesktopPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                        if (!string.IsNullOrEmpty(DesktopPath))
                        {
                            await SQLite.Current.UpdateLibraryAsync(DesktopPath, LibraryType.Desktop).ConfigureAwait(true);
                        }

                        string VideoPath = Environment.GetFolderPath(Environment.SpecialFolder.MyVideos);
                        if (!string.IsNullOrEmpty(VideoPath))
                        {
                            await SQLite.Current.UpdateLibraryAsync(VideoPath, LibraryType.Videos).ConfigureAwait(true);
                        }

                        string PicturesPath = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
                        if (!string.IsNullOrEmpty(PicturesPath))
                        {
                            await SQLite.Current.UpdateLibraryAsync(PicturesPath, LibraryType.Pictures).ConfigureAwait(true);
                        }

                        string DocumentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                        if (!string.IsNullOrEmpty(DocumentsPath))
                        {
                            await SQLite.Current.UpdateLibraryAsync(DocumentsPath, LibraryType.Document).ConfigureAwait(true);
                        }

                        string MusicPath = Environment.GetFolderPath(Environment.SpecialFolder.MyMusic);
                        if (!string.IsNullOrEmpty(MusicPath))
                        {
                            await SQLite.Current.UpdateLibraryAsync(MusicPath, LibraryType.Music).ConfigureAwait(true);
                        }

                        string ProfilePath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                        if (!string.IsNullOrEmpty(ProfilePath))
                        {
                            await SQLite.Current.UpdateLibraryAsync(Path.Combine(ProfilePath, "OneDrive"), LibraryType.OneDrive).ConfigureAwait(true);
                        }
                    }
                }

                Queue<string> ErrorList = new Queue<string>();
                foreach (var FolderPath in await SQLite.Current.GetLibraryPathAsync().ConfigureAwait(true))
                {
                    try
                    {
                        StorageFolder PinFile = await StorageFolder.GetFolderFromPathAsync(FolderPath);
                        BitmapImage Thumbnail = await PinFile.GetThumbnailBitmapAsync().ConfigureAwait(true);
                        CommonAccessCollection.LibraryFolderList.Add(new LibraryFolder(PinFile, Thumbnail));
                    }
                    catch (Exception)
                    {
                        ErrorList.Enqueue(FolderPath);
                        await SQLite.Current.DeleteLibraryAsync(FolderPath).ConfigureAwait(true);
                    }
                }

                bool AccessError = false;
                foreach (DriveInfo Drive in DriveInfo.GetDrives().Where((Drives) => Drives.DriveType == DriveType.Fixed || Drives.DriveType == DriveType.Network || Drives.DriveType == DriveType.Removable)
                                                                 .Where((NewItem) => CommonAccessCollection.HardDeviceList.All((Item) => Item.Folder.Path != NewItem.RootDirectory.FullName)))
                {
                    try
                    {
                        StorageFolder Device = await StorageFolder.GetFolderFromPathAsync(Drive.RootDirectory.FullName);

                        BasicProperties Properties = await Device.GetBasicPropertiesAsync();
                        IDictionary<string, object> PropertiesRetrieve = await Properties.RetrievePropertiesAsync(new string[] { "System.Capacity", "System.FreeSpace", "System.Volume.FileSystem" });

                        CommonAccessCollection.HardDeviceList.Add(new HardDeviceInfo(Device, await Device.GetThumbnailBitmapAsync().ConfigureAwait(true), PropertiesRetrieve, Drive.DriveType));
                    }
                    catch
                    {
                        AccessError = true;
                    }
                }

                if (AccessError && !ApplicationData.Current.LocalSettings.Values.ContainsKey("DisableAccessErrorTip"))
                {
                    QueueContentDialog dialog = new QueueContentDialog
                    {
                        Title = Globalization.GetString("Common_Dialog_WarningTitle"),
                        Content = Globalization.GetString("QueueDialog_DeviceHideForError_Content"),
                        PrimaryButtonText = Globalization.GetString("Common_Dialog_DoNotTip"),
                        CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                    };

                    if (await dialog.ShowAsync().ConfigureAwait(true) == ContentDialogResult.Primary)
                    {
                        ApplicationData.Current.LocalSettings.Values["DisableAccessErrorTip"] = true;
                    }
                }

                switch (PortalDeviceWatcher.Status)
                {
                    case DeviceWatcherStatus.Created:
                    case DeviceWatcherStatus.Aborted:
                    case DeviceWatcherStatus.Stopped:
                        {
                            PortalDeviceWatcher?.Start();
                            break;
                        }
                }

                if (ErrorList.Count > 0)
                {
                    StringBuilder Builder = new StringBuilder();

                    while (ErrorList.TryDequeue(out string ErrorMessage))
                    {
                        Builder.AppendLine($"   {ErrorMessage}");
                    }

                    QueueContentDialog dialog = new QueueContentDialog
                    {
                        Title = Globalization.GetString("Common_Dialog_WarningTitle"),
                        Content = Globalization.GetString("QueueDialog_PinFolderNotFound_Content") + Builder.ToString(),
                        CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                    };
                    _ = await dialog.ShowAsync().ConfigureAwait(true);
                }
            }
            catch (Exception ex)
            {
                ExceptionTracer.RequestBlueScreen(ex);
            }
        }

        private void TabViewControl_TabCloseRequested(TabView sender, TabViewTabCloseRequestedEventArgs args)
        {
            if ((args.Tab.Content as Frame).Content is ThisPC PC)
            {
                CommonAccessCollection.GetFileControlInstance(PC)?.Dispose();
                CommonAccessCollection.UnRegister(PC);
            }
            else if ((args.Tab.Content as Frame).Content is FileControl Control)
            {
                Control.Dispose();
                CommonAccessCollection.UnRegister(Control);
            }

            args.Tab.DragOver -= Item_DragOver;
            args.Tab.Drop -= Item_Drop;
            sender.TabItems.Remove(args.Tab);

            if (TabViewControl.TabItems.Count == 0)
            {
                CoreApplication.Exit();
            }
        }

        private void TabViewControl_AddTabButtonClick(TabView sender, object args)
        {
            if (CreateNewTab() is TabViewItem Item)
            {
                sender.TabItems.Add(Item);
                sender.UpdateLayout();
                sender.SelectedItem = Item;

                if (sender.TabItems.Count > 1)
                {
                    foreach (TabViewItem Tab in TabViewControl.TabItems)
                    {
                        Tab.IsClosable = true;
                    }
                }
            }
        }

        private TabViewItem CreateNewTab(StorageFolder StorageFolderForNewTab = null)
        {
            if (Interlocked.Exchange(ref LockResource, 1) == 0)
            {
                try
                {
                    Frame frame = new Frame();

                    TabViewItem Item = new TabViewItem
                    {
                        IconSource = new SymbolIconSource { Symbol = Symbol.Document },
                        AllowDrop = true
                    };
                    Item.DragOver += Item_DragOver;
                    Item.Drop += Item_Drop;

                    if (StorageFolderForNewTab != null)
                    {
                        frame.Navigate(typeof(ThisPC), new Tuple<TabViewItem, Frame>(Item, frame), new SuppressNavigationTransitionInfo());

                        if (AnimationController.Current.IsEnableAnimation)
                        {
                            frame.Navigate(typeof(FileControl), new Tuple<TabViewItem, StorageFolder, ThisPC>(Item, StorageFolderForNewTab, frame.Content as ThisPC), new DrillInNavigationTransitionInfo());
                        }
                        else
                        {
                            frame.Navigate(typeof(FileControl), new Tuple<TabViewItem, StorageFolder, ThisPC>(Item, StorageFolderForNewTab, frame.Content as ThisPC), new SuppressNavigationTransitionInfo());
                        }
                    }
                    else
                    {
                        frame.Navigate(typeof(ThisPC), new Tuple<TabViewItem, Frame>(Item, frame), new SuppressNavigationTransitionInfo());
                    }

                    Item.Content = frame;

                    return Item;
                }
                finally
                {
                    _ = Interlocked.Exchange(ref LockResource, 0);
                }
            }
            else
            {
                return null;
            }
        }

        private async void Item_Drop(object sender, DragEventArgs e)
        {
            var Deferral = e.GetDeferral();

            try
            {
                if (e.DataView.Contains(StandardDataFormats.Html))
                {
                    string Html = await e.DataView.GetHtmlFormatAsync();
                    string Fragment = HtmlFormatHelper.GetStaticFragment(Html);

                    HtmlDocument Document = new HtmlDocument();
                    Document.LoadHtml(Fragment);
                    HtmlNode HeadNode = Document.DocumentNode.SelectSingleNode("/head");

                    if (HeadNode?.InnerText == "RX-Explorer-TabItem")
                    {
                        HtmlNode BodyNode = Document.DocumentNode.SelectSingleNode("/p");
                        string[] Split = BodyNode.InnerText.Split("||", StringSplitOptions.RemoveEmptyEntries);

                        switch (Split[0])
                        {
                            case "ThisPC":
                                {
                                    await CreateNewTabAndOpenTargetFolder(string.Empty).ConfigureAwait(true);
                                    break;
                                }
                            case "FileControl":
                                {
                                    await CreateNewTabAndOpenTargetFolder(Split[1]).ConfigureAwait(true);
                                    break;
                                }
                        }
                    }
                }
            }
            catch
            {
                Debug.WriteLine("Error happened when try to drop a tab");
            }
            finally
            {
                e.Handled = true;
                Deferral.Complete();
            }
        }

        private async void Item_DragOver(object sender, DragEventArgs e)
        {
            var Deferral = e.GetDeferral();

            try
            {
                if (e.DataView.Contains(StandardDataFormats.Html))
                {
                    string Html = await e.DataView.GetHtmlFormatAsync();
                    string Fragment = HtmlFormatHelper.GetStaticFragment(Html);

                    HtmlDocument Document = new HtmlDocument();
                    Document.LoadHtml(Fragment);
                    HtmlNode HeadNode = Document.DocumentNode.SelectSingleNode("/head");

                    if (HeadNode?.InnerText == "RX-Explorer-TabItem")
                    {
                        e.AcceptedOperation = DataPackageOperation.Link;
                    }
                    else
                    {
                        if (e.OriginalSource is TabViewItem Item)
                        {
                            TabViewControl.SelectedItem = Item;
                        }
                    }
                }
                else if (e.DataView.Contains(StandardDataFormats.StorageItems))
                {
                    if (e.OriginalSource is TabViewItem Item)
                    {
                        TabViewControl.SelectedItem = Item;
                    }
                }
            }
            finally
            {
                e.Handled = true;
                Deferral.Complete();
            }
        }

        private void TabViewControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            foreach (var Nav in TabViewControl.TabItems.Select((Item) => (Item as TabViewItem).Content as Frame))
            {
                Nav.Navigated -= Nav_Navigated;
            }

            if (TabViewControl.SelectedItem is TabViewItem Item)
            {
                CurrentTabNavigation = Item.Content as Frame;
                CurrentTabNavigation.Navigated += Nav_Navigated;
                MainPage.ThisPage.NavView.IsBackEnabled = CurrentTabNavigation.CanGoBack;
            }
        }

        private void Nav_Navigated(object sender, Windows.UI.Xaml.Navigation.NavigationEventArgs e)
        {
            MainPage.ThisPage.NavView.IsBackEnabled = CurrentTabNavigation.CanGoBack;
        }

        private void TabViewControl_TabDragStarting(TabView sender, TabViewTabDragStartingEventArgs args)
        {
            StringBuilder Builder = new StringBuilder("<head>RX-Explorer-TabItem</head>");

            if ((args.Tab.Content as Frame)?.Content is ThisPC)
            {
                Builder.Append("<p>ThisPC||</p>");
            }
            else if ((args.Tab.Content as Frame)?.Content is FileControl Control)
            {
                Builder.Append($"<p>FileControl||{Control.CurrentFolder.Path}</p>");
            }
            else
            {
                args.Cancel = true;
            }

            args.Data.SetHtmlFormat(HtmlFormatHelper.CreateHtmlFormat(Builder.ToString()));
        }

        private void TabViewControl_TabDragCompleted(TabView sender, TabViewTabDragCompletedEventArgs args)
        {
            if (args.DropResult == DataPackageOperation.Link)
            {
                if ((args.Tab.Content as Frame).Content is ThisPC PC)
                {
                    CommonAccessCollection.GetFileControlInstance(PC)?.Dispose();
                    CommonAccessCollection.UnRegister(PC);
                }
                else if ((args.Tab.Content as Frame).Content is FileControl Control)
                {
                    Control.Dispose();
                    CommonAccessCollection.UnRegister(Control);
                }

                args.Tab.DragOver -= Item_DragOver;
                args.Tab.Drop -= Item_Drop;

                sender.TabItems.Remove(args.Tab);

                if (TabViewControl.TabItems.Count >= 1)
                {
                    foreach (TabViewItem Tab in TabViewControl.TabItems)
                    {
                        Tab.IsClosable = true;
                    }
                }
                else
                {
                    Application.Current.Exit();
                }
            }
        }
    }
}
