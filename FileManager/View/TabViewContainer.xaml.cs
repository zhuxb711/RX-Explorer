using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.Devices.Enumeration;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Media.Imaging;
using BitmapIconSource = Microsoft.UI.Xaml.Controls.BitmapIconSource;
using SymbolIconSource = Microsoft.UI.Xaml.Controls.SymbolIconSource;
using TabView = Microsoft.UI.Xaml.Controls.TabView;
using TabViewTabCloseRequestedEventArgs = Microsoft.UI.Xaml.Controls.TabViewTabCloseRequestedEventArgs;

namespace FileManager
{
    public sealed partial class TabViewContainer : Page
    {
        private int LockResource = 0;

        public static Frame CurrentPageNav { get; private set; }

        public ObservableCollection<HardDeviceInfo> HardDeviceList { get; private set; } = new ObservableCollection<HardDeviceInfo>();
        public ObservableCollection<LibraryFolder> LibraryFolderList { get; private set; } = new ObservableCollection<LibraryFolder>();
        public ObservableCollection<QuickStartItem> QuickStartList { get; private set; } = new ObservableCollection<QuickStartItem>();
        public ObservableCollection<QuickStartItem> HotWebList { get; private set; } = new ObservableCollection<QuickStartItem>();

        private DeviceWatcher PortalDeviceWatcher = DeviceInformation.CreateWatcher(DeviceClass.PortableStorageDevice);

        public Dictionary<FileControl, FilePresenter> InstanceContainer { get; private set; } = new Dictionary<FileControl, FilePresenter>();

        public static TabViewContainer ThisPage { get; private set; }

        public TabViewContainer()
        {
            InitializeComponent();
            ThisPage = this;
            Loaded += TabViewContainer_Loaded;
            PortalDeviceWatcher.Added += PortalDeviceWatcher_Added;
            PortalDeviceWatcher.Removed += PortalDeviceWatcher_Removed;
            Application.Current.Resuming += Current_Resuming;
            Application.Current.Suspending += Current_Suspending;
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
            if (PortalDeviceWatcher != null && PortalDeviceWatcher.Status == DeviceWatcherStatus.Stopped)
            {
                PortalDeviceWatcher.Start();
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
            var CurrentDrives = DriveInfo.GetDrives().TakeWhile((Drives) => Drives.DriveType == DriveType.Fixed || Drives.DriveType == DriveType.Removable || Drives.DriveType == DriveType.Ram || Drives.DriveType == DriveType.Network)
                                                     .GroupBy((Item) => Item.Name)
                                                     .Select((Group) => Group.FirstOrDefault().Name);
            var RemovedDriveList = HardDeviceList.SkipWhile((RemoveItem) => CurrentDrives.Any((Item) => Item == RemoveItem.Folder.Path));

            await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                for (int i = 0; i < RemovedDriveList.Count(); i++)
                {
                    HardDeviceList.Remove(RemovedDriveList.ElementAt(i));
                }
            });
        }

        private async void PortalDeviceWatcher_Added(DeviceWatcher sender, DeviceInformation args)
        {
            var NewDriveAddedList = DriveInfo.GetDrives().Where((Drives) => Drives.DriveType == DriveType.Fixed || Drives.DriveType == DriveType.Removable || Drives.DriveType == DriveType.Ram || Drives.DriveType == DriveType.Network)
                                                         .Select((Item) => Item.RootDirectory.FullName)
                                                         .SkipWhile((NewItem) => HardDeviceList.Any((Item) => Item.Folder.Path == NewItem));
            try
            {
                foreach (string DriveRootPath in NewDriveAddedList)
                {
                    StorageFolder Device = await StorageFolder.GetFolderFromPathAsync(DriveRootPath);
                    BasicProperties Properties = await Device.GetBasicPropertiesAsync();
                    IDictionary<string, object> PropertiesRetrieve = await Properties.RetrievePropertiesAsync(new string[] { "System.Capacity", "System.FreeSpace" });

                    await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, async () =>
                    {
                        HardDeviceList.Add(new HardDeviceInfo(Device, await Device.GetThumbnailBitmapAsync().ConfigureAwait(true), PropertiesRetrieve));
                    });
                }
            }
            catch (UnauthorizedAccessException)
            {
                await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, async () =>
                {
                    if (Globalization.Language == LanguageEnum.Chinese)
                    {
                        QueueContentDialog Dialog = new QueueContentDialog
                        {
                            Title = "提示",
                            Content = $"由于缺少足够的访问权限，无法添加可移动设备：\"{args.Name}\"",
                            CloseButtonText = "确定"
                        };
                        _ = await Dialog.ShowAsync().ConfigureAwait(true);
                    }
                    else
                    {
                        QueueContentDialog Dialog = new QueueContentDialog
                        {
                            Title = "Tips",
                            Content = $"Cannot add removable device：\"{args.Name}\" due to lack of sufficient permissions",
                            CloseButtonText = "Got it"
                        };
                        _ = await Dialog.ShowAsync().ConfigureAwait(true);
                    }
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
                await foreach (var Item in SQLite.Current.GetQuickStartItemAsync())
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
                        catch (FileNotFoundException)
                        {
                            UserFolderDialog Dialog = new UserFolderDialog(Globalization.Language == LanguageEnum.Chinese ? "下载" : "Downloads");
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
                                UserFolderDialog Dialog = new UserFolderDialog(Globalization.Language == LanguageEnum.Chinese ? "下载" : "Downloads");
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
                }
                catch (Exception)
                {
                    if (Globalization.Language == LanguageEnum.Chinese)
                    {
                        QueueContentDialog Dialog = new QueueContentDialog
                        {
                            Title = "Opoos...",
                            Content = "由于某些无法预料的原因，无法导入库文件夹",
                            CloseButtonText = "确定"
                        };
                        _ = await Dialog.ShowAsync().ConfigureAwait(true);
                    }
                    else
                    {
                        QueueContentDialog Dialog = new QueueContentDialog
                        {
                            Title = "Opoos...",
                            Content = "Unable to import library folder for some unforeseen reasons",
                            CloseButtonText = "Got it"
                        };
                        _ = await Dialog.ShowAsync().ConfigureAwait(true);
                    }
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
                    catch (FileNotFoundException)
                    {
                        ErrorList.Enqueue(FolderPath);
                        await SQLite.Current.DeleteFolderLibraryAsync(FolderPath).ConfigureAwait(true);
                    }
                }

                Dictionary<string, bool> VisibilityMap = await SQLite.Current.GetDeviceVisibilityMapAsync().ConfigureAwait(true);

                foreach (string DriveRootPath in DriveInfo.GetDrives().Where((Drives) => Drives.DriveType == DriveType.Fixed || Drives.DriveType == DriveType.Removable || Drives.DriveType == DriveType.Ram || Drives.DriveType == DriveType.Network)
                                                                      .Select((Item) => Item.RootDirectory.FullName)
                                                                      .SkipWhile((NewItem) => HardDeviceList.Any((Item) => Item.Folder.Path == NewItem)))
                {
                    if (VisibilityMap.ContainsKey(DriveRootPath))
                    {
                        if (!VisibilityMap[DriveRootPath])
                        {
                            continue;
                        }
                    }

                    StorageFolder Device = await StorageFolder.GetFolderFromPathAsync(DriveRootPath);

                    BasicProperties Properties = await Device.GetBasicPropertiesAsync();
                    IDictionary<string, object> PropertiesRetrieve = await Properties.RetrievePropertiesAsync(new string[] { "System.Capacity", "System.FreeSpace" });
                    HardDeviceList.Add(new HardDeviceInfo(Device, await Device.GetThumbnailBitmapAsync().ConfigureAwait(true), PropertiesRetrieve));
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

                if (PortalDeviceWatcher.Status != DeviceWatcherStatus.Started && PortalDeviceWatcher.Status != DeviceWatcherStatus.EnumerationCompleted)
                {
                    PortalDeviceWatcher.Start();
                }

                if (ErrorList.Count > 0)
                {
                    string Display = string.Empty;
                    while (ErrorList.Count > 0)
                    {
                        Display += "   " + ErrorList.Dequeue() + "\r";
                    }

                    if (Globalization.Language == LanguageEnum.Chinese)
                    {
                        QueueContentDialog dialog = new QueueContentDialog
                        {
                            Title = "警告",
                            Content = "部分已固定的文件夹已无法找到，将自动移除\r\r"
                            + "包括：\r" + Display,
                            CloseButtonText = "知道了"
                        };
                        _ = await dialog.ShowAsync().ConfigureAwait(true);
                    }
                    else
                    {
                        QueueContentDialog dialog = new QueueContentDialog
                        {
                            Title = "Warning",
                            Content = "Some of the fixed folders are no longer found and will be automatically removed\r\r"
                            + "Including：\r" + Display,
                            CloseButtonText = "Got it"
                        };
                        _ = await dialog.ShowAsync().ConfigureAwait(true);
                    }
                }

                if (MainPage.ThisPage.IsUSBActivate && !string.IsNullOrWhiteSpace(MainPage.ThisPage.ActivateUSBDevicePath))
                {
                    MainPage.ThisPage.IsUSBActivate = false;
                    var HardDevice = HardDeviceList.Where((Device) => Device.Folder.Path == MainPage.ThisPage.ActivateUSBDevicePath).FirstOrDefault();
                    await Task.Delay(1000).ConfigureAwait(true);
                    CurrentPageNav.Navigate(typeof(FileControl), new Tuple<TabViewItem, StorageFolder>(TabViewControl.TabItems.FirstOrDefault() as TabViewItem, HardDevice.Folder), new DrillInNavigationTransitionInfo());
                }
            }
            catch (Exception ex)
            {
                ExceptionTracer.RequestBlueScreen(ex);
            }
        }

        private void TabViewControl_TabCloseRequested(TabView sender, TabViewTabCloseRequestedEventArgs args)
        {
            if (sender.TabItems.Count > 1)
            {
                sender.TabItems.Remove(args.Tab);
            }
        }

        private void TabViewControl_AddTabButtonClick(TabView sender, object args)
        {
            if (CreateNewTab() is TabViewItem Item)
            {
                sender.TabItems.Add(Item);
            }
        }

        private TabViewItem CreateNewTab()
        {
            if (Interlocked.Exchange(ref LockResource, 1) == 0)
            {
                try
                {
                    Frame frame = new Frame();

                    TabViewItem Item = new TabViewItem
                    {
                        IconSource = new SymbolIconSource { Symbol = Symbol.Document },
                        Content = frame
                    };

                    frame.Navigate(typeof(ThisPC), new Tuple<TabViewItem, Frame>(Item, frame));

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
