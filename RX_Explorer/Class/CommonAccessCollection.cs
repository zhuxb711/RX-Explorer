using Microsoft.Toolkit.Deferred;
using Nito.AsyncEx;
using RX_Explorer.View;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using Windows.ApplicationModel.Core;
using Windows.Devices.Enumeration;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.System;
using Windows.UI.Core;
using Timer = System.Timers.Timer;

namespace RX_Explorer.Class
{
    public static class CommonAccessCollection
    {
        private static int IsDriveLoaded;
        private static int IsLibraryLoaded;
        private static int IsQuickStartLoaded;

        private static readonly List<FileSystemStorageFolder> DriveCache = new List<FileSystemStorageFolder>();
        private static readonly AsyncLock DriveChangeLocker = new AsyncLock();
        private static readonly AsyncLock LibraryChangeLocker = new AsyncLock();
        private static readonly DeviceWatcher PortalDriveWatcher = DeviceInformation.CreateWatcher(DeviceInformation.GetAqsFilterFromDeviceClass(DeviceClass.PortableStorageDevice),
                                                                                                   new string[] { "System.Capacity", "System.FreeSpace", "System.Volume.FileSystem", "System.Volume.BitLockerProtection" });
        private static readonly Timer NetworkDriveCheckTimer = new Timer(5000)
        {
            AutoReset = true,
            Enabled = true
        };

        public static ObservableCollection<DriveDataBase> DriveList { get; } = new ObservableCollection<DriveDataBase>();
        public static ObservableCollection<LibraryStorageFolder> LibraryList { get; } = new ObservableCollection<LibraryStorageFolder>();
        public static ObservableCollection<QuickStartItem> QuickStartList { get; } = new ObservableCollection<QuickStartItem>();
        public static ObservableCollection<QuickStartItem> WebLinkList { get; } = new ObservableCollection<QuickStartItem>();

        public static event EventHandler<DriveChangedDeferredEventArgs> DriveChanged;

        public static event EventHandler<LibraryChangedDeferredEventArgs> LibraryChanged;

        public static event EventHandler<IEnumerable<string>> LibraryNotFound;

        public static async Task LoadQuickStartItemsAsync()
        {
            try
            {
                if (Interlocked.CompareExchange(ref IsQuickStartLoaded, 1, 0) == 0)
                {
                    await CoreApplication.MainView.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                    {
                        WebLinkList.Clear();
                        QuickStartList.Clear();
                    });

                    foreach ((string Name, string IconPath, string Protocal, string Type) in SQLite.Current.GetQuickStartItem())
                    {
                        StorageFile ImageFile = null;

                        try
                        {
                            ImageFile = IconPath.StartsWith("ms-appx") ? await StorageFile.GetFileFromApplicationUriAsync(new Uri(IconPath))
                                                                       : await StorageFile.GetFileFromPathAsync(Path.Combine(ApplicationData.Current.LocalFolder.Path, IconPath));

                            using (IRandomAccessStream Stream = await ImageFile.OpenAsync(FileAccessMode.Read))
                            {
                                if (Enum.Parse<QuickStartType>(Type) == QuickStartType.Application)
                                {
                                    QuickStartList.Add(new QuickStartItem(QuickStartType.Application, await Helper.CreateBitmapImageAsync(Stream), Protocal, IconPath, Name));
                                }
                                else
                                {
                                    WebLinkList.Add(new QuickStartItem(QuickStartType.WebSite, await Helper.CreateBitmapImageAsync(Stream), Protocal, IconPath, Name));
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            LogTracer.Log(ex, $"Could not load QuickStart item, Name: {Name}");

                            SQLite.Current.DeleteQuickStartItem(Name, Protocal, IconPath, Type);

                            if (ImageFile != null)
                            {
                                await ImageFile.DeleteAsync(StorageDeleteOption.PermanentDelete);
                            }
                        }
                    }

                    QuickStartList.Add(new QuickStartItem());
                    WebLinkList.Add(new QuickStartItem());
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, "QuickStart could not be loaded as expected");
            }
        }

        public static async Task LoadLibraryFoldersAsync(bool IsRefresh = false)
        {
            try
            {
                if (Interlocked.CompareExchange(ref IsLibraryLoaded, 1, 0) == 0 || IsRefresh)
                {
                    await CoreApplication.MainView.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                    {
                        LibraryList.Clear();
                    });

                    try
                    {
                        IReadOnlyList<User> UserList = await User.FindAllAsync();

                        UserDataPaths DataPath = UserList.Where((User) => User.AuthenticationStatus == UserAuthenticationStatus.LocallyAuthenticated && User.Type == UserType.LocalUser)
                                                         .FirstOrDefault() is User CurrentUser
                                                 ? UserDataPaths.GetForUser(CurrentUser)
                                                 : UserDataPaths.GetDefault();

                        SQLite.Current.UpdateLibraryFolderRecord(new List<LibraryFolderRecord>(7)
                        {
                            new LibraryFolderRecord(LibraryType.Downloads, string.IsNullOrWhiteSpace(DataPath.Downloads) ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads") : DataPath.Downloads),
                            new LibraryFolderRecord(LibraryType.Desktop, string.IsNullOrWhiteSpace(DataPath.Desktop) ? Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory) : DataPath.Desktop),
                            new LibraryFolderRecord(LibraryType.Videos, string.IsNullOrWhiteSpace(DataPath.Videos) ? Environment.GetFolderPath(Environment.SpecialFolder.MyVideos) : DataPath.Videos),
                            new LibraryFolderRecord(LibraryType.Pictures, string.IsNullOrWhiteSpace(DataPath.Pictures) ? Environment.GetFolderPath(Environment.SpecialFolder.MyPictures) : DataPath.Pictures),
                            new LibraryFolderRecord(LibraryType.Document, string.IsNullOrWhiteSpace(DataPath.Documents) ? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) : DataPath.Documents),
                            new LibraryFolderRecord(LibraryType.Music, string.IsNullOrWhiteSpace(DataPath.Music) ? Environment.GetFolderPath(Environment.SpecialFolder.MyMusic) : DataPath.Music),
                            new LibraryFolderRecord(LibraryType.OneDrive, Environment.GetEnvironmentVariable("OneDrive"))
                        });
                    }
                    catch (Exception)
                    {
                        SQLite.Current.UpdateLibraryFolderRecord(new List<LibraryFolderRecord>(6)
                        {
                            new LibraryFolderRecord(LibraryType.Desktop, Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory)),
                            new LibraryFolderRecord(LibraryType.Videos, Environment.GetFolderPath(Environment.SpecialFolder.MyVideos)),
                            new LibraryFolderRecord(LibraryType.Pictures, Environment.GetFolderPath(Environment.SpecialFolder.MyPictures)),
                            new LibraryFolderRecord(LibraryType.Document, Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)),
                            new LibraryFolderRecord(LibraryType.Music, Environment.GetFolderPath(Environment.SpecialFolder.MyMusic)),
                            new LibraryFolderRecord(LibraryType.OneDrive, Environment.GetEnvironmentVariable("OneDrive"))
                        });
                    }

                    ConcurrentBag<string> ErrorList = new ConcurrentBag<string>();

                    List<Task> LongRunningTaskList = new List<Task>();

                    foreach (LibraryFolderRecord Record in SQLite.Current.GetLibraryFolderRecord()
                                                                         .OrderBy((Record) => Record.Type)
                                                                         .Where((Record) => !string.IsNullOrEmpty(Record.Path)))
                    {
                        Task LoadTask = LibraryStorageFolder.CreateAsync(Record.Type, Record.Path).ContinueWith((PreviousTask) =>
                        {
                            if (PreviousTask.Result is LibraryStorageFolder Library)
                            {
                                if (!LibraryList.Contains(Library))
                                {
                                    LibraryList.Add(Library);
                                }
                            }
                            else
                            {
                                ErrorList.Add(Record.Path);
                            }
                        }, default, TaskContinuationOptions.PreferFairness, TaskScheduler.FromCurrentSynchronizationContext());

                        if (await Task.WhenAny(LoadTask, Task.Delay(2000)) != LoadTask)
                        {
                            LongRunningTaskList.Add(LoadTask);
                        }
                    }

                    await Task.WhenAll(LongRunningTaskList);
                    await JumpListController.AddItemAsync(JumpListGroup.Library, LibraryList.Where((Library) => Library.LibType == LibraryType.UserCustom)
                                                                                                    .Select((Library) => Library.Path)
                                                                                                    .ToArray());

                    if (!ErrorList.IsEmpty)
                    {
                        LibraryNotFound?.Invoke(null, ErrorList);
                    }
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, "Library could not be loaded as expected");
            }
        }

        public static async Task LoadDriveAsync(bool IsRefresh = false)
        {
            try
            {
                if (Interlocked.CompareExchange(ref IsDriveLoaded, 1, 0) == 0 || IsRefresh)
                {
                    DriveList.Clear();

                    List<Task> LongRunningTaskList = new List<Task>();

                    foreach (DriveInfo Drive in DriveInfo.GetDrives().Where((Drives) => Drives.DriveType is DriveType.Fixed or DriveType.Network or DriveType.CDRom or DriveType.Ram))
                    {
                        Task LoadTask = DriveDataBase.CreateAsync(Drive).ContinueWith((PreviousTask) =>
                        {
                            if (PreviousTask.Exception is Exception Ex)
                            {
                                LogTracer.Log(Ex, $"Ignore the drive \"{Drive.Name}\" because we could not get details from this drive");
                            }
                            else
                            {
                                if (PreviousTask.Result is DriveDataBase DriveData)
                                {
                                    if (!DriveList.Contains(DriveData))
                                    {
                                        DriveList.Add(DriveData);
                                    }
                                }
                                else
                                {
                                    LogTracer.Log($"Ignore the drive \"{Drive.Name}\" because we could not get details from this drive");
                                }
                            }
                        }, default, TaskContinuationOptions.PreferFairness, TaskScheduler.FromCurrentSynchronizationContext());

                        if (await Task.WhenAny(LoadTask, Task.Delay(2000)) != LoadTask)
                        {
                            LongRunningTaskList.Add(LoadTask);
                        }
                    }

                    foreach (DeviceInformation Device in await DeviceInformation.FindAllAsync(DeviceInformation.GetAqsFilterFromDeviceClass(DeviceClass.PortableStorageDevice), new string[] { "System.Capacity", "System.FreeSpace", "System.Volume.FileSystem", "System.Volume.BitLockerProtection" }))
                    {
                        if (Device.IsEnabled)
                        {
                            Task LoadTask = DriveDataBase.CreateAsync(DriveType.Removable, Device).ContinueWith((PreviousTask) =>
                            {
                                if (PreviousTask.Exception is Exception Ex)
                                {
                                    LogTracer.Log(Ex, $"Ignore the drive \"{Device.Name}\" because we could not get details from this drive");
                                }
                                else
                                {
                                    if (PreviousTask.Result is DriveDataBase DriveData)
                                    {
                                        if (!DriveList.Contains(DriveData))
                                        {
                                            DriveList.Add(DriveData);
                                        }
                                    }
                                    else
                                    {
                                        LogTracer.Log($"Ignore the drive \"{Device.Name}\" because we could not get details from this drive");
                                    }
                                }
                            }, default, TaskContinuationOptions.PreferFairness, TaskScheduler.FromCurrentSynchronizationContext());

                            if (await Task.WhenAny(LoadTask, Task.Delay(2000)) != LoadTask)
                            {
                                LongRunningTaskList.Add(LoadTask);
                            }
                        }
                    }

                    foreach (StorageFolder ExternalDeviceFolder in await KnownFolders.RemovableDevices.GetFoldersAsync())
                    {
                        Task LoadTask = DriveDataBase.CreateAsync(DriveType.Removable, new FileSystemStorageFolder(await ExternalDeviceFolder.GetNativeFileDataAsync())).ContinueWith((PreviousTask) =>
                        {
                            if (PreviousTask.Exception is Exception Ex)
                            {
                                LogTracer.Log(Ex, $"Ignore the drive \"{ExternalDeviceFolder.Name}\" because we could not get details from this drive");
                            }
                            else
                            {
                                if (PreviousTask.Result is DriveDataBase DriveData)
                                {
                                    if (!DriveList.Contains(DriveData))
                                    {
                                        DriveList.Add(DriveData);
                                    }
                                }
                                else
                                {
                                    LogTracer.Log($"Ignore the drive \"{ExternalDeviceFolder.Name}\" because we could not get details from this drive");
                                }
                            }
                        }, default, TaskContinuationOptions.PreferFairness, TaskScheduler.FromCurrentSynchronizationContext());

                        if (await Task.WhenAny(LoadTask, Task.Delay(2000)) != LoadTask)
                        {
                            LongRunningTaskList.Add(LoadTask);
                        }
                    }

                    if (SettingPage.IsLoadWSLFolderOnStartupEnabled)
                    {
                        foreach (StorageFolder WslFolder in await GetAvailableWslDriveAsync())
                        {
                            Task LoadTask = DriveDataBase.CreateAsync(DriveType.Network, new FileSystemStorageFolder(await WslFolder.GetNativeFileDataAsync())).ContinueWith((PreviousTask) =>
                            {
                                if (PreviousTask.Exception is Exception Ex)
                                {
                                    LogTracer.Log(Ex, $"Ignore the drive \"{WslFolder.Path}\" because we could not get details from this drive");
                                }
                                else
                                {
                                    if (PreviousTask.Result is DriveDataBase DriveData)
                                    {
                                        if (!DriveList.Contains(DriveData))
                                        {
                                            DriveList.Add(DriveData);
                                        }
                                    }
                                    else
                                    {
                                        LogTracer.Log($"Ignore the drive \"{WslFolder.Path}\" because we could not get details from this drive");
                                    }
                                }
                            }, default, TaskContinuationOptions.PreferFairness, TaskScheduler.FromCurrentSynchronizationContext());

                            if (await Task.WhenAny(LoadTask, Task.Delay(1000)) != LoadTask)
                            {
                                LongRunningTaskList.Add(LoadTask);
                            }
                        }
                    }

                    await Task.WhenAll(LongRunningTaskList);

                    if (!IsRefresh)
                    {
                        PortalDriveWatcher.Start();
                        NetworkDriveCheckTimer.Start();
                    }
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, "Drive could not be loaded as expected");
            }
        }

        public static IReadOnlyList<FileSystemStorageFolder> GetMissedDriveBeforeSubscribeEvents()
        {
            return DriveCache.AsReadOnly();
        }

        private static async void PortalDriveWatcher_Removed(DeviceWatcher sender, DeviceInformationUpdate args)
        {
            try
            {
                if (DriveList.FirstOrDefault((Drive) => Drive.DeviceId == args.Id) is DriveDataBase RemovedDrive)
                {
                    await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                    {
                        DriveList.Remove(RemovedDrive);
                    });
                }
                else
                {
                    throw new Exception($"Device Id: {args.Id}, Device Kind: {Enum.GetName(typeof(DeviceInformationKind), args.Kind)}");
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, "Could not remove the drive because drive is not found in the drive list");
            }
        }

        private static async void PortalDriveWatcher_Added(DeviceWatcher sender, DeviceInformation args)
        {
            if (args.IsEnabled)
            {
                try
                {
                    if (DriveList.All((Drive) => Drive.DeviceId != args.Id))
                    {
                        if (await DriveDataBase.CreateAsync(DriveType.Removable, args) is DriveDataBase DriveData)
                        {
                            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                            {
                                if (!DriveList.Contains(DriveData))
                                {
                                    DriveList.Add(DriveData);
                                }
                            });
                        }
                        else
                        {
                            throw new Exception($"Device Id: {args.Id}, Device Name: {args.Name}, Device Kind: {Enum.GetName(typeof(DeviceInformationKind), args.Kind)}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogTracer.Log(ex, $"Ignore the drive because we could not create {nameof(DriveDataBase)} from this drive");
                }
            }
        }

        private async static void DriveList_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            using (await DriveChangeLocker.LockAsync())
            {
                switch (e.Action)
                {
                    case NotifyCollectionChangedAction.Add:
                        {
                            if (DriveChanged != null)
                            {
                                foreach (DriveDataBase Drive in e.NewItems)
                                {
                                    await DriveChanged.InvokeAsync(null, new DriveChangedDeferredEventArgs(CommonChangeType.Added, Drive.DriveFolder));
                                }
                            }
                            else
                            {
                                DriveCache.AddRange(e.NewItems.OfType<DriveDataBase>().Select((Drive) => Drive.DriveFolder));
                            }

                            break;
                        }
                    case NotifyCollectionChangedAction.Remove:
                        {
                            if (DriveChanged != null)
                            {
                                foreach (DriveDataBase Drive in e.OldItems)
                                {
                                    await DriveChanged.InvokeAsync(null, new DriveChangedDeferredEventArgs(CommonChangeType.Removed, Drive.DriveFolder));
                                }
                            }
                            else
                            {
                                foreach (DriveDataBase Drive in e.OldItems)
                                {
                                    DriveCache.Remove(Drive.DriveFolder);
                                }
                            }

                            break;
                        }
                }

            }
        }

        private async static Task<IReadOnlyList<StorageFolder>> GetAvailableWslDriveAsync()
        {
            List<StorageFolder> AvailableWslFolderList = new List<StorageFolder>();

            try
            {
                using (AuxiliaryTrustProcessController.Exclusive Exclusive = await AuxiliaryTrustProcessController.GetControllerExclusiveAsync(Priority: PriorityLevel.Low))
                {
                    foreach (string WslPath in await Exclusive.Controller.GetAvailableWslDrivePathListAsync())
                    {
                        AvailableWslFolderList.Add(await StorageFolder.GetFolderFromPathAsync(WslPath));
                    }
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, "Could not get the storage folder for WSL");
            }

            return AvailableWslFolderList;
        }

        private async static void NetworkDriveCheckTimer_Tick(object sender, ElapsedEventArgs e)
        {
            NetworkDriveCheckTimer.Enabled = false;

            try
            {
                DriveInfo[] NewNetworkDrive = DriveInfo.GetDrives().Where((Drives) => Drives.DriveType == DriveType.Network).ToArray();
                DriveDataBase[] ExistNetworkDrive = DriveList.Where((ExistDrive) => ExistDrive is not WslDriveData && ExistDrive.DriveType == DriveType.Network).ToArray();

                IEnumerable<DriveInfo> AddList = NewNetworkDrive.Where((NewDrive) => ExistNetworkDrive.All((ExistDrive) => !ExistDrive.Path.Equals(NewDrive.Name, StringComparison.OrdinalIgnoreCase)));
                IEnumerable<DriveDataBase> RemoveList = ExistNetworkDrive.Where((ExistDrive) => NewNetworkDrive.All((NewDrive) => !ExistDrive.Path.Equals(NewDrive.Name, StringComparison.OrdinalIgnoreCase)));

                await CoreApplication.MainView.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                {
                    foreach (DriveDataBase ExistDrive in RemoveList)
                    {
                        DriveList.Remove(ExistDrive);
                    }
                });

                foreach (DriveInfo Drive in AddList)
                {
                    try
                    {
                        if (await DriveDataBase.CreateAsync(Drive) is DriveDataBase DriveData)
                        {
                            await CoreApplication.MainView.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                            {
                                if (!DriveList.Contains(DriveData))
                                {
                                    DriveList.Add(DriveData);
                                }
                            });
                        }
                        else
                        {
                            throw new InvalidOperationException();
                        }
                    }
                    catch (Exception ex)
                    {
                        LogTracer.Log(ex, $"Ignore the drive \"{Drive.Name}\" because we could not get details from this drive");
                    }
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, "Could not add or remove the network drive");
            }
            finally
            {
                NetworkDriveCheckTimer.Enabled = true;
            }
        }

        static CommonAccessCollection()
        {
            PortalDriveWatcher.Added += PortalDriveWatcher_Added;
            PortalDriveWatcher.Removed += PortalDriveWatcher_Removed;

            NetworkDriveCheckTimer.Elapsed += NetworkDriveCheckTimer_Tick;

            DriveList.CollectionChanged += DriveList_CollectionChanged;
            LibraryList.CollectionChanged += LibraryFolderList_CollectionChanged;
        }

        private static async void LibraryFolderList_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (LibraryChanged != null)
            {
                using (await LibraryChangeLocker.LockAsync())
                {
                    switch (e.Action)
                    {
                        case NotifyCollectionChangedAction.Add:
                            {
                                foreach (LibraryStorageFolder Lib in e.NewItems)
                                {
                                    await LibraryChanged.InvokeAsync(null, new LibraryChangedDeferredEventArgs(CommonChangeType.Added, Lib));
                                }

                                break;
                            }
                        case NotifyCollectionChangedAction.Remove:
                            {
                                foreach (LibraryStorageFolder Lib in e.OldItems)
                                {
                                    await LibraryChanged.InvokeAsync(null, new LibraryChangedDeferredEventArgs(CommonChangeType.Removed, Lib));
                                }

                                break;
                            }
                    }
                }
            }
        }
    }
}
