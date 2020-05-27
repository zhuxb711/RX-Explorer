using RX_Explorer.Class;
using RX_Explorer.Dialog;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.Devices.Enumeration;
using Windows.Storage;
using Windows.Storage.FileProperties;
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

        public static Frame CurrentPageNav { get; private set; }

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
        }

        private void TabViewContainer_PointerPressed(CoreWindow sender, PointerEventArgs args)
        {
            bool BackButtonPressed = args.CurrentPoint.Properties.IsXButton1Pressed;
            bool ForwardButtonPressed = args.CurrentPoint.Properties.IsXButton2Pressed;

            if (CurrentPageNav.Content is FileControl Control)
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
            else if (CurrentPageNav.Content is ThisPC PC)
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
            if (CurrentPageNav.Content is FileControl Control)
            {
                if (Control.Nav.CanGoBack)
                {
                    Control.Nav.GoBack();
                }
                else if (CurrentPageNav.CanGoBack)
                {
                    CurrentPageNav.GoBack();
                }
            }
            else if (CurrentPageNav.CanGoBack)
            {
                CurrentPageNav.GoBack();
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
                                 while (CurrentPageNav.CanGoBack)
                                 {
                                     CurrentPageNav.GoBack();
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

                try
                {
                    if (ApplicationData.Current.LocalSettings.Values["UserDefineDownloadPath"] is string UserDefinePath)
                    {
                        try
                        {
                            StorageFolder DownloadFolder = await StorageFolder.GetFolderFromPathAsync(UserDefinePath);
                            LibraryFolderList.Add(new LibraryFolder(DownloadFolder, await DownloadFolder.GetThumbnailBitmapAsync().ConfigureAwait(true), LibrarySource.SystemBase));
                        }
                        catch
                        {
                            UserFolderDialog Dialog = new UserFolderDialog(Globalization.GetString("Download_Folder_Name"));
                            if ((await Dialog.ShowAsync().ConfigureAwait(true)) == ContentDialogResult.Primary)
                            {
                                LibraryFolderList.Add(new LibraryFolder(Dialog.MissingFolder, await Dialog.MissingFolder.GetThumbnailBitmapAsync().ConfigureAwait(true), LibrarySource.SystemBase));
                                ApplicationData.Current.LocalSettings.Values["UserDefineDownloadPath"] = Dialog.MissingFolder.Path;
                            }
                        }
                    }
                    else
                    {
                        string UserPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                        if (!string.IsNullOrEmpty(UserPath))
                        {
                            StorageFolder CurrentFolder = await StorageFolder.GetFolderFromPathAsync(UserPath);

                            if ((await CurrentFolder.TryGetItemAsync("Downloads")) is StorageFolder DownloadFolder)
                            {
                                LibraryFolderList.Add(new LibraryFolder(DownloadFolder, await DownloadFolder.GetThumbnailBitmapAsync().ConfigureAwait(true), LibrarySource.SystemBase));
                            }
                            else
                            {
                                UserFolderDialog Dialog = new UserFolderDialog(Globalization.GetString("Download_Folder_Name"));
                                if ((await Dialog.ShowAsync().ConfigureAwait(true)) == ContentDialogResult.Primary)
                                {
                                    LibraryFolderList.Add(new LibraryFolder(Dialog.MissingFolder, await Dialog.MissingFolder.GetThumbnailBitmapAsync().ConfigureAwait(true), LibrarySource.SystemBase));
                                    ApplicationData.Current.LocalSettings.Values["UserDefineDownloadPath"] = Dialog.MissingFolder.Path;
                                }
                            }
                        }
                    }

                    string DesktopPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                    if (!string.IsNullOrEmpty(DesktopPath))
                    {
                        StorageFolder DesktopFolder = await StorageFolder.GetFolderFromPathAsync(DesktopPath);
                        LibraryFolderList.Add(new LibraryFolder(DesktopFolder, await DesktopFolder.GetThumbnailBitmapAsync().ConfigureAwait(true), LibrarySource.SystemBase));
                    }

                    string VideoPath = Environment.GetFolderPath(Environment.SpecialFolder.MyVideos);
                    if (!string.IsNullOrEmpty(VideoPath))
                    {
                        StorageFolder VideoFolder = await StorageFolder.GetFolderFromPathAsync(VideoPath);
                        LibraryFolderList.Add(new LibraryFolder(VideoFolder, await VideoFolder.GetThumbnailBitmapAsync().ConfigureAwait(true), LibrarySource.SystemBase));
                    }

                    string PicturePath = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
                    if (!string.IsNullOrEmpty(PicturePath))
                    {
                        StorageFolder PictureFolder = await StorageFolder.GetFolderFromPathAsync(PicturePath);
                        LibraryFolderList.Add(new LibraryFolder(PictureFolder, await PictureFolder.GetThumbnailBitmapAsync().ConfigureAwait(true), LibrarySource.SystemBase));
                    }

                    string DocumentPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                    if (!string.IsNullOrEmpty(DocumentPath))
                    {
                        StorageFolder DocumentFolder = await StorageFolder.GetFolderFromPathAsync(DocumentPath);
                        LibraryFolderList.Add(new LibraryFolder(DocumentFolder, await DocumentFolder.GetThumbnailBitmapAsync().ConfigureAwait(true), LibrarySource.SystemBase));
                    }

                    string MusicPath = Environment.GetFolderPath(Environment.SpecialFolder.MyMusic);
                    if (!string.IsNullOrEmpty(MusicPath))
                    {
                        StorageFolder MusicFolder = await StorageFolder.GetFolderFromPathAsync(MusicPath);
                        LibraryFolderList.Add(new LibraryFolder(MusicFolder, await MusicFolder.GetThumbnailBitmapAsync().ConfigureAwait(true), LibrarySource.SystemBase));
                    }

                    string UserProfilePath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                    if (!string.IsNullOrEmpty(UserProfilePath))
                    {
                        StorageFolder OneDriveFolder = await StorageFolder.GetFolderFromPathAsync(Path.Combine(UserProfilePath, "OneDrive"));
                        LibraryFolderList.Add(new LibraryFolder(OneDriveFolder, await OneDriveFolder.GetThumbnailBitmapAsync().ConfigureAwait(true), LibrarySource.SystemBase));
                    }
                }
                catch (Exception)
                {
                    QueueContentDialog Dialog = new QueueContentDialog
                    {
                        Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                        Content = Globalization.GetString("QueueDialog_ImportLibraryFolderError_Content"),
                        CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                    };
                    _ = await Dialog.ShowAsync().ConfigureAwait(true);
                }

                Queue<string> ErrorList = new Queue<string>();
                foreach (var FolderPath in await SQLite.Current.GetFolderLibraryAsync().ConfigureAwait(true))
                {
                    try
                    {
                        StorageFolder PinFile = await StorageFolder.GetFolderFromPathAsync(FolderPath);
                        BitmapImage Thumbnail = await PinFile.GetThumbnailBitmapAsync().ConfigureAwait(true);
                        LibraryFolderList.Add(new LibraryFolder(PinFile, Thumbnail, LibrarySource.UserCustom));
                    }
                    catch (Exception)
                    {
                        ErrorList.Enqueue(FolderPath);
                        await SQLite.Current.DeleteFolderLibraryAsync(FolderPath).ConfigureAwait(true);
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
                    string Display = string.Empty;
                    while (ErrorList.Count > 0)
                    {
                        Display += "   " + ErrorList.Dequeue() + "\r";
                    }

                    QueueContentDialog dialog = new QueueContentDialog
                    {
                        Title = Globalization.GetString("Common_Dialog_WarningTitle"),
                        Content = Globalization.GetString("QueueDialog_PinFolderNotFound_Content") + Display,
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
                            CurrentPageNav.Navigate(typeof(FileControl), new Tuple<TabViewItem, StorageFolder, ThisPC>(TabViewControl.TabItems.FirstOrDefault() as TabViewItem, HardDevice.Folder, CurrentPageNav.Content as ThisPC), new DrillInNavigationTransitionInfo());
                        }
                        else
                        {
                            CurrentPageNav.Navigate(typeof(FileControl), new Tuple<TabViewItem, StorageFolder, ThisPC>(TabViewControl.TabItems.FirstOrDefault() as TabViewItem, HardDevice.Folder, CurrentPageNav.Content as ThisPC), new SuppressNavigationTransitionInfo());
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
                FFInstanceContainer.Remove(TFInstanceContainer[PC]);
                FSInstanceContainer.Remove(TFInstanceContainer[PC]);
                TFInstanceContainer.Remove(PC);
            }
            else if ((args.Tab.Content as Frame).Content is FileControl Control && TFInstanceContainer.ContainsValue(Control))
            {
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
                CurrentPageNav = Item.Content as Frame;
                CurrentPageNav.Navigated += Nav_Navigated;
                MainPage.ThisPage.NavView.IsBackEnabled = CurrentPageNav.CanGoBack;
            }
        }

        private void Nav_Navigated(object sender, Windows.UI.Xaml.Navigation.NavigationEventArgs e)
        {
            MainPage.ThisPage.NavView.IsBackEnabled = CurrentPageNav.CanGoBack;
        }
    }
}
