using Microsoft.UI.Xaml.Controls;
using RX_Explorer.Class;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.Devices.Enumeration;
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
        private int LockResource = 0;

        public static Frame CurrentTabNavigation { get; private set; }

        public ObservableCollection<HardDeviceInfo> HardDeviceList { get; private set; } = new ObservableCollection<HardDeviceInfo>();
        public ObservableCollection<LibraryFolder> LibraryFolderList { get; private set; } = new ObservableCollection<LibraryFolder>();
        public ObservableCollection<QuickStartItem> QuickStartList { get; private set; } = new ObservableCollection<QuickStartItem>();
        public ObservableCollection<QuickStartItem> HotWebList { get; private set; } = new ObservableCollection<QuickStartItem>();

        private DeviceWatcher PortalDeviceWatcher = DeviceInformation.CreateWatcher(DeviceClass.PortableStorageDevice);

        public Dictionary<FileControl, FilePresenter> FFInstanceContainer { get; private set; } = new Dictionary<FileControl, FilePresenter>();

        public Dictionary<FileControl, SearchPage> FSInstanceContainer { get; private set; } = new Dictionary<FileControl, SearchPage>();

        public Dictionary<ThisPC, FileControl> TFInstanceContainer { get; private set; } = new Dictionary<ThisPC, FileControl>();

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
            CoreWindow.GetForCurrentThread().KeyUp += TabViewContainer_KeyUp;
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
                                await FullTrustExcutorController.ViewWithQuicklook(Device.Folder.Path).ConfigureAwait(false);
                            }
                            else if (PC.LibraryGrid.SelectedItem is LibraryFolder Library)
                            {
                                await FullTrustExcutorController.ViewWithQuicklook(Library.Folder.Path).ConfigureAwait(false);
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
                                await FullTrustExcutorController.ViewWithQuicklook(Device.Folder.Path).ConfigureAwait(false);
                            }
                            else if (PC.LibraryGrid.SelectedItem is LibraryFolder Library)
                            {
                                await FullTrustExcutorController.ViewWithQuicklook(Library.Folder.Path).ConfigureAwait(false);
                            }
                            break;
                        }
                }
            }
        }

        private void TabViewContainer_KeyUp(CoreWindow sender, KeyEventArgs args)
        {
            if (args.VirtualKey == VirtualKey.Space)
            {
                args.Handled = true;
            }
        }

        private void TabViewContainer_PointerPressed(CoreWindow sender, PointerEventArgs args)
        {
            bool BackButtonPressed = args.CurrentPoint.Properties.IsXButton1Pressed;
            bool ForwardButtonPressed = args.CurrentPoint.Properties.IsXButton2Pressed;

            if (CurrentTabNavigation.Content is FileControl Control)
            {
                if (BackButtonPressed)
                {
                    args.Handled = true;
                    SettingControl.IsInputFromPrimaryButton = false;

                    if (!QueueContentDialog.IsRunningOrWaiting && Control.Nav.CurrentSourcePageType.Name == nameof(FilePresenter))
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
            else if (CurrentTabNavigation.Content is ThisPC PC)
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
                        if (PortalDeviceWatcher != null)
                        {
                            PortalDeviceWatcher.Start();
                        }
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
            IEnumerable<string> CurrentDrives = DriveInfo.GetDrives().Where((Drives) => Drives.DriveType == DriveType.Fixed || Drives.DriveType == DriveType.Removable || Drives.DriveType == DriveType.Ram || Drives.DriveType == DriveType.Network)
                                                                     .Select((Info) => Info.RootDirectory.FullName);

            IEnumerable<HardDeviceInfo> RemovedDriveList = HardDeviceList.Where((RemoveItem) => CurrentDrives.All((Item) => Item != RemoveItem.Folder.Path));

            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
             {
                 for (int i = 0; i < RemovedDriveList.Count(); i++)
                 {
                     for (int j = 0; j < TabViewControl.TabItems.Count; j++)
                     {
                         if (((TabViewControl.TabItems[j] as TabViewItem)?.Content as Frame)?.Content is FileControl Control && Path.GetPathRoot(Control.CurrentFolder.Path) == RemovedDriveList.ElementAt(i).Folder.Path)
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
                                 if (TFInstanceContainer.ContainsValue(Control))
                                 {
                                     FFInstanceContainer.Remove(Control);
                                     FSInstanceContainer.Remove(Control);
                                     TFInstanceContainer.Remove(TFInstanceContainer.First((Item) => Item.Value == Control).Key);
                                 }

                                 TabViewControl.TabItems.RemoveAt(j);

                                 if (TabViewControl.TabItems.Count == 1)
                                 {
                                     (TabViewControl.TabItems.First() as TabViewItem).IsClosable = false;
                                 }
                             }
                         }
                     }

                     HardDeviceList.Remove(RemovedDriveList.ElementAt(i));
                 }
             });
        }

        private async void PortalDeviceWatcher_Added(DeviceWatcher sender, DeviceInformation args)
        {
            IEnumerable<string> NewDriveAddedList = DriveInfo.GetDrives().Where((Drives) => Drives.DriveType == DriveType.Fixed || Drives.DriveType == DriveType.Removable || Drives.DriveType == DriveType.Ram || Drives.DriveType == DriveType.Network)
                                                                         .Select((Item) => Item.RootDirectory.FullName)
                                                                         .Where((NewItem) => HardDeviceList.All((Item) => Item.Folder.Path != NewItem));
            try
            {
                foreach (string DriveRootPath in NewDriveAddedList)
                {
                    StorageFolder Device = await StorageFolder.GetFolderFromPathAsync(DriveRootPath);
                    BasicProperties Properties = await Device.GetBasicPropertiesAsync();
                    IDictionary<string, object> PropertiesRetrieve = await Properties.RetrievePropertiesAsync(new string[] { "System.Capacity", "System.FreeSpace" });

                    await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
                    {
                        HardDeviceList.Add(new HardDeviceInfo(Device, await Device.GetThumbnailBitmapAsync().ConfigureAwait(true), PropertiesRetrieve));
                    });
                }
            }
            catch (UnauthorizedAccessException)
            {
                await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
                {
                    QueueContentDialog Dialog = new QueueContentDialog
                    {
                        Title = Globalization.GetString("Common_Dialog_TipTitle"),
                        Content = $"{Globalization.GetString("QueueDialog_AddDeviceFail_Content")} \"{args.Name}\"",
                        CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                    };
                    _ = await Dialog.ShowAsync().ConfigureAwait(true);
                });
            }
        }

        private async void TabViewContainer_Loaded(object sender, RoutedEventArgs e)
        {
            Loaded -= TabViewContainer_Loaded;

            if (CreateNewTab() is TabViewItem TabItem)
            {
                TabViewControl.TabItems.Add(TabItem);
            }

            try
            {
                foreach (KeyValuePair<QuickStartType, QuickStartItem> Item in await SQLite.Current.GetQuickStartItemAsync().ConfigureAwait(true))
                {
                    if (Item.Key == QuickStartType.Application)
                    {
                        QuickStartList.Add(Item.Value);
                    }
                    else
                    {
                        HotWebList.Add(Item.Value);
                    }
                }

                QuickStartList.Add(new QuickStartItem(new BitmapImage(new Uri("ms-appx:///Assets/Add.png")) { DecodePixelHeight = 100, DecodePixelWidth = 100 }, null, default, null));
                HotWebList.Add(new QuickStartItem(new BitmapImage(new Uri("ms-appx:///Assets/Add.png")) { DecodePixelHeight = 100, DecodePixelWidth = 100 }, null, default, null));

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
                        LibraryFolderList.Add(new LibraryFolder(PinFile, Thumbnail));
                    }
                    catch (Exception)
                    {
                        ErrorList.Enqueue(FolderPath);
                        await SQLite.Current.DeleteLibraryAsync(FolderPath).ConfigureAwait(true);
                    }
                }

                Dictionary<string, bool> VisibilityMap = await SQLite.Current.GetDeviceVisibilityMapAsync().ConfigureAwait(true);

                bool AccessError = false;
                foreach (string DriveRootPath in DriveInfo.GetDrives().Where((Drives) => Drives.DriveType == DriveType.Fixed || Drives.DriveType == DriveType.Removable || Drives.DriveType == DriveType.Ram || Drives.DriveType == DriveType.Network)
                                                                      .Select((Item) => Item.RootDirectory.FullName)
                                                                      .Where((NewItem) => HardDeviceList.All((Item) => Item.Folder.Path != NewItem)))
                {
                    if (!VisibilityMap.ContainsKey(DriveRootPath) || VisibilityMap[DriveRootPath])
                    {
                        try
                        {
                            StorageFolder Device = await StorageFolder.GetFolderFromPathAsync(DriveRootPath);

                            BasicProperties Properties = await Device.GetBasicPropertiesAsync();
                            IDictionary<string, object> PropertiesRetrieve = await Properties.RetrievePropertiesAsync(new string[] { "System.Capacity", "System.FreeSpace" });
                            HardDeviceList.Add(new HardDeviceInfo(Device, await Device.GetThumbnailBitmapAsync().ConfigureAwait(true), PropertiesRetrieve));
                        }
                        catch (Exception)
                        {
                            AccessError = true;
                        }
                    }
                }

                if (AccessError)
                {
                    QueueContentDialog dialog = new QueueContentDialog
                    {
                        Title = Globalization.GetString("Common_Dialog_WarningTitle"),
                        Content = Globalization.GetString("QueueDialog_DeviceHideForError_Content"),
                        CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                    };
                    _ = await dialog.ShowAsync().ConfigureAwait(true);
                }

                foreach (string AdditionalDrivePath in VisibilityMap.Where((Item) => Item.Value && HardDeviceList.All((Device) => Item.Key != Device.Folder.Path)).Select((Result) => Result.Key))
                {
                    try
                    {
                        StorageFolder Device = await StorageFolder.GetFolderFromPathAsync(AdditionalDrivePath);

                        BasicProperties Properties = await Device.GetBasicPropertiesAsync();
                        IDictionary<string, object> PropertiesRetrieve = await Properties.RetrievePropertiesAsync(new string[] { "System.Capacity", "System.FreeSpace" });

                        HardDeviceList.Add(new HardDeviceInfo(Device, await Device.GetThumbnailBitmapAsync().ConfigureAwait(true), PropertiesRetrieve));
                    }
                    catch (Exception)
                    {
                        await SQLite.Current.SetDeviceVisibilityAsync(AdditionalDrivePath, false).ConfigureAwait(true);
                    }
                }

                switch (PortalDeviceWatcher.Status)
                {
                    case DeviceWatcherStatus.Created:
                    case DeviceWatcherStatus.Aborted:
                    case DeviceWatcherStatus.Stopped:
                        {
                            PortalDeviceWatcher.Start();
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

                if (MainPage.ThisPage.IsUSBActivate && !string.IsNullOrWhiteSpace(MainPage.ThisPage.ActivateUSBDevicePath))
                {
                    MainPage.ThisPage.IsUSBActivate = false;
                    if (HardDeviceList.FirstOrDefault((Device) => Device.Folder.Path == MainPage.ThisPage.ActivateUSBDevicePath) is HardDeviceInfo HardDevice)
                    {
                        await Task.Delay(1000).ConfigureAwait(true);

                        if (AnimationController.Current.IsEnableAnimation)
                        {
                            CurrentTabNavigation.Navigate(typeof(FileControl), new Tuple<TabViewItem, StorageFolder, ThisPC>(TabViewControl.TabItems.FirstOrDefault() as TabViewItem, HardDevice.Folder, CurrentTabNavigation.Content as ThisPC), new DrillInNavigationTransitionInfo());
                        }
                        else
                        {
                            CurrentTabNavigation.Navigate(typeof(FileControl), new Tuple<TabViewItem, StorageFolder, ThisPC>(TabViewControl.TabItems.FirstOrDefault() as TabViewItem, HardDevice.Folder, CurrentTabNavigation.Content as ThisPC), new SuppressNavigationTransitionInfo());
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ExceptionTracer.RequestBlueScreen(ex);
            }
        }

        private void TabViewControl_TabCloseRequested(TabView sender, TabViewTabCloseRequestedEventArgs args)
        {
            if ((args.Tab.Content as Frame).Content is ThisPC PC && TFInstanceContainer.ContainsKey(PC))
            {
                TFInstanceContainer[PC].Dispose();
                FFInstanceContainer.Remove(TFInstanceContainer[PC]);
                FSInstanceContainer.Remove(TFInstanceContainer[PC]);
                TFInstanceContainer.Remove(PC);
            }
            else if ((args.Tab.Content as Frame).Content is FileControl Control && TFInstanceContainer.ContainsValue(Control))
            {
                Control.Dispose();
                FFInstanceContainer.Remove(Control);
                FSInstanceContainer.Remove(Control);
                TFInstanceContainer.Remove(TFInstanceContainer.First((Item) => Item.Value == Control).Key);
            }

            args.Tab.DragEnter -= Item_DragEnter;
            sender.TabItems.Remove(args.Tab);

            if (TabViewControl.TabItems.Count > 1)
            {
                foreach (TabViewItem Tab in TabViewControl.TabItems)
                {
                    Tab.IsClosable = true;
                }
            }
            else
            {
                (TabViewControl.TabItems.First() as TabViewItem).IsClosable = false;
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
                        Content = frame,
                        AllowDrop = true,
                        IsClosable = false
                    };
                    Item.DragEnter += Item_DragEnter;

                    frame.Navigate(typeof(ThisPC), new Tuple<TabViewItem, StorageFolder>(Item, StorageFolderForNewTab));

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

        private void Item_DragEnter(object sender, DragEventArgs e)
        {
            if (e.OriginalSource is TabViewItem Item)
            {
                TabViewControl.SelectedItem = Item;
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
    }
}
