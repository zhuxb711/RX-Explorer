using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel.Core;
using Windows.Devices.Enumeration;
using Windows.Devices.Portable;
using Windows.Storage;
using Windows.System;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media.Imaging;

namespace RX_Explorer.Class
{
    public static class CommonAccessCollection
    {
        public static ObservableCollection<DriveRelatedData> DriveList { get; } = new ObservableCollection<DriveRelatedData>();
        public static ObservableCollection<LibraryFolder> LibraryFolderList { get; } = new ObservableCollection<LibraryFolder>();
        public static ObservableCollection<QuickStartItem> QuickStartList { get; } = new ObservableCollection<QuickStartItem>();
        public static ObservableCollection<QuickStartItem> HotWebList { get; } = new ObservableCollection<QuickStartItem>();

        private static readonly DeviceWatcher PortalDeviceWatcher = DeviceInformation.CreateWatcher(DeviceClass.PortableStorageDevice);

        private static readonly DispatcherTimer NetworkDriveCheckTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(5)
        };

        public static event EventHandler<DriveRelatedData> DeviceAdded;

        public static event EventHandler<DriveRelatedData> DeviceRemoved;

        public static event EventHandler<Queue<string>> LibraryNotFound;

        public static async Task LoadQuickStartItemsAsync()
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
        }

        public static async Task LoadLibraryFoldersAsync()
        {
            if (!ApplicationData.Current.LocalSettings.Values.ContainsKey("IsLibraryInitialized"))
            {
                try
                {
                    IReadOnlyList<User> UserList = await User.FindAllAsync();

                    UserDataPaths DataPath = UserList.FirstOrDefault((User) => User.AuthenticationStatus == UserAuthenticationStatus.LocallyAuthenticated && User.Type == UserType.LocalUser) is User CurrentUser
                                             ? UserDataPaths.GetForUser(CurrentUser)
                                             : UserDataPaths.GetDefault();
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

                        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("OneDrive")))
                        {
                            await SQLite.Current.SetLibraryPathAsync(Environment.GetEnvironmentVariable("OneDrive"), LibraryType.OneDrive).ConfigureAwait(true);
                        }
                    }
                    catch (Exception ex)
                    {
                        LogTracer.Log(ex, "An error was threw when getting library folder (In initialize)");
                    }
                }
                catch (Exception ex)
                {
                    LogTracer.Log(ex, "An error was threw when try to get 'UserDataPath' (In initialize)");

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

                    string OneDrivePath = Environment.GetEnvironmentVariable("OneDrive");
                    if (!string.IsNullOrEmpty(OneDrivePath))
                    {
                        await SQLite.Current.SetLibraryPathAsync(OneDrivePath, LibraryType.OneDrive).ConfigureAwait(true);
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

                    UserDataPaths DataPath = UserList.FirstOrDefault((User) => User.AuthenticationStatus == UserAuthenticationStatus.LocallyAuthenticated && User.Type == UserType.LocalUser) is User CurrentUser
                                             ? UserDataPaths.GetForUser(CurrentUser)
                                             : UserDataPaths.GetDefault();
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

                        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("OneDrive")))
                        {
                            await SQLite.Current.UpdateLibraryAsync(Environment.GetEnvironmentVariable("OneDrive"), LibraryType.OneDrive).ConfigureAwait(true);
                        }
                    }
                    catch (Exception ex)
                    {
                        LogTracer.Log(ex, "An error was threw when getting library folder (Not in initialize)");
                    }
                }
                catch (Exception ex)
                {
                    LogTracer.Log(ex, "An error was threw when try to get 'UserDataPath' (Not in initialize)");

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

                    string OneDrivePath = Environment.GetEnvironmentVariable("OneDrive");
                    if (!string.IsNullOrEmpty(OneDrivePath))
                    {
                        await SQLite.Current.UpdateLibraryAsync(OneDrivePath, LibraryType.OneDrive).ConfigureAwait(true);
                    }
                }
            }

            Queue<string> ErrorList = new Queue<string>();

            foreach ((string, LibraryType) Library in await SQLite.Current.GetLibraryPathAsync().ConfigureAwait(true))
            {
                try
                {
                    StorageFolder PinFolder = await StorageFolder.GetFolderFromPathAsync(Library.Item1);
                    BitmapImage Thumbnail = await PinFolder.GetThumbnailBitmapAsync().ConfigureAwait(true);
                    LibraryFolderList.Add(new LibraryFolder(PinFolder, Thumbnail, Library.Item2));
                }
                catch (Exception)
                {
                    ErrorList.Enqueue(Library.Item1);
                    await SQLite.Current.DeleteLibraryAsync(Library.Item1).ConfigureAwait(true);
                }
            }

            await JumpListController.Current.AddItemAsync(JumpListGroup.Library, LibraryFolderList.Where((Library) => Library.Type == LibraryType.UserCustom).Select((Library) => Library.Folder.Path).ToArray()).ConfigureAwait(true);

            if (ErrorList.Count > 0)
            {
                LibraryNotFound?.Invoke(null, ErrorList);
            }
        }

        public static async Task LoadDeviceAsync()
        {
            foreach (DriveInfo Drive in DriveInfo.GetDrives().Where((Drives) => Drives.DriveType == DriveType.Fixed || Drives.DriveType == DriveType.Removable || Drives.DriveType == DriveType.Network)
                                                             .Where((NewItem) => DriveList.All((Item) => Item.Folder.Path != NewItem.RootDirectory.FullName)))
            {
                try
                {
                    StorageFolder DriveFolder = await StorageFolder.GetFolderFromPathAsync(Drive.RootDirectory.FullName);

                    DriveList.Add(await DriveRelatedData.CreateAsync(DriveFolder, Drive.DriveType).ConfigureAwait(true));
                }
                catch (Exception ex)
                {
                    LogTracer.Log(ex, $"Hide the device \"{Drive.RootDirectory.FullName}\" for error");
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

            NetworkDriveCheckTimer.Start();
        }

        private static async void PortalDeviceWatcher_Removed(DeviceWatcher sender, DeviceInformationUpdate args)
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
                    catch (Exception ex)
                    {
                        LogTracer.Log(ex, $"Error happened when get storagefolder from {Device.Name}");
                    }
                }

                foreach (string PortDevice in AllBaseDevice.Where((Path) => PortableDevice.Any((Item) => Item.Path.Equals(Path, StringComparison.OrdinalIgnoreCase))))
                {
                    AllBaseDevice.Remove(PortDevice);
                }

                List<DriveRelatedData> OneStepDeviceList = DriveList.Where((Item) => !AllBaseDevice.Contains(Item.Folder.Path)).ToList();
                List<DriveRelatedData> TwoStepDeviceList = OneStepDeviceList.Where((RemoveItem) => PortableDevice.All((Item) => Item.Name != RemoveItem.Folder.Name)).ToList();

                await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    foreach (DriveRelatedData Device in TwoStepDeviceList)
                    {
                        DriveList.Remove(Device);
                    }
                });
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"Error happened when remove device from HardDeviceList");
            }
        }

        private static async void PortalDeviceWatcher_Added(DeviceWatcher sender, DeviceInformation args)
        {
            try
            {
                StorageFolder DeviceFolder = StorageDevice.FromId(args.Id);

                if (DriveList.All((Device) => (string.IsNullOrEmpty(Device.Folder.Path) || string.IsNullOrEmpty(DeviceFolder.Path)) ? Device.Folder.Name != DeviceFolder.Name : Device.Folder.Path != DeviceFolder.Path))
                {
                    await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
                    {
                        DriveList.Add(await DriveRelatedData.CreateAsync(DeviceFolder, DriveType.Removable).ConfigureAwait(true));
                    });
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"Error happened when add device to HardDeviceList");
            }
        }

        private async static void HardDeviceList_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
            {
                switch (e.Action)
                {
                    case System.Collections.Specialized.NotifyCollectionChangedAction.Add:
                        {
                            foreach (DriveRelatedData Device in e.NewItems)
                            {
                                DeviceAdded?.Invoke(null, Device);
                            }

                            break;
                        }
                    case System.Collections.Specialized.NotifyCollectionChangedAction.Remove:
                        {
                            foreach (DriveRelatedData Device in e.OldItems)
                            {
                                DeviceRemoved?.Invoke(null, Device);
                            }

                            break;
                        }
                }
            });
        }

        private async static void NetworkDriveCheckTimer_Tick(object sender, object e)
        {
            NetworkDriveCheckTimer.Stop();

            DriveInfo[] NewNetworkDrive = DriveInfo.GetDrives().Where((Drives) => Drives.DriveType == DriveType.Network).ToArray();
            DriveRelatedData[] ExistNetworkDrive = DriveList.Where((ExistDrive) => ExistDrive.DriveType == DriveType.Network).ToArray();

            IEnumerable<DriveInfo> AddList = NewNetworkDrive.Where((NewDrive) => ExistNetworkDrive.All((ExistDrive) => ExistDrive.Folder.Path != NewDrive.RootDirectory.FullName));
            IEnumerable<DriveRelatedData> RemoveList = ExistNetworkDrive.Where((ExistDrive) => NewNetworkDrive.All((NewDrive) => ExistDrive.Folder.Path != NewDrive.RootDirectory.FullName));

            foreach (DriveRelatedData ExistDrive in RemoveList)
            {
                DriveList.Remove(ExistDrive);
            }

            foreach (DriveInfo Drive in AddList)
            {
                try
                {
                    StorageFolder Device = await StorageFolder.GetFolderFromPathAsync(Drive.RootDirectory.FullName);

                    DriveList.Add(await DriveRelatedData.CreateAsync(Device, Drive.DriveType).ConfigureAwait(true));
                }
                catch (Exception ex)
                {
                    LogTracer.Log(ex, $"Hide the device \"{Drive.RootDirectory.FullName}\" for error");
                }
            }

            NetworkDriveCheckTimer.Start();
        }


        static CommonAccessCollection()
        {
            PortalDeviceWatcher.Added += PortalDeviceWatcher_Added;
            PortalDeviceWatcher.Removed += PortalDeviceWatcher_Removed;
            DriveList.CollectionChanged += HardDeviceList_CollectionChanged;
            NetworkDriveCheckTimer.Tick += NetworkDriveCheckTimer_Tick;
        }
    }
}
