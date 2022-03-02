using Microsoft.Toolkit.Deferred;
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
using Windows.Storage.Search;
using Windows.Storage.Streams;
using Windows.System;
using Windows.UI.Core;
using Windows.UI.Xaml.Media.Imaging;
using Timer = System.Timers.Timer;

namespace RX_Explorer.Class
{
    public static class CommonAccessCollection
    {
        public static ObservableCollection<DriveDataBase> DriveList { get; } = new ObservableCollection<DriveDataBase>();
        public static ObservableCollection<LibraryStorageFolder> LibraryList { get; } = new ObservableCollection<LibraryStorageFolder>();
        public static ObservableCollection<QuickStartItem> QuickStartList { get; } = new ObservableCollection<QuickStartItem>();
        public static ObservableCollection<QuickStartItem> WebLinkList { get; } = new ObservableCollection<QuickStartItem>();

        private static readonly List<FileSystemStorageFolder> DriveCache = new List<FileSystemStorageFolder>();

        private static readonly DeviceWatcher PortalDriveWatcher = DeviceInformation.CreateWatcher(DeviceClass.PortableStorageDevice);

        private static readonly Timer NetworkDriveCheckTimer = new Timer(5000)
        {
            AutoReset = true,
            Enabled = true
        };

        public static event EventHandler<DriveChangedDeferredEventArgs> DriveChanged;

        public static event EventHandler<LibraryChangedDeferredEventArgs> LibraryChanged;

        public static event EventHandler<IEnumerable<string>> LibraryNotFound;

        private static readonly SemaphoreSlim DriveChangeLocker = new SemaphoreSlim(1, 1);

        private static readonly SemaphoreSlim LibraryChangeLocker = new SemaphoreSlim(1, 1);

        private static int IsDriveLoaded;
        private static int IsLibraryLoaded;
        private static int IsQuickStartLoaded;

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

                            BitmapImage Bitmap = new BitmapImage();

                            using (IRandomAccessStream Stream = await ImageFile.OpenAsync(FileAccessMode.Read))
                            {
                                await Bitmap.SetSourceAsync(Stream);
                            }

                            if (Enum.Parse<QuickStartType>(Type) == QuickStartType.Application)
                            {
                                QuickStartList.Add(new QuickStartItem(QuickStartType.Application, Bitmap, Protocal, IconPath, Name));
                            }
                            else
                            {
                                WebLinkList.Add(new QuickStartItem(QuickStartType.WebSite, Bitmap, Protocal, IconPath, Name));
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

                        SQLite.Current.UpdateLibraryFolder(new List<LibraryFolderRecord>(7)
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
                        SQLite.Current.UpdateLibraryFolder(new List<LibraryFolderRecord>(6)
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

                    foreach (LibraryFolderRecord Record in SQLite.Current.GetLibraryFolderRecord().OrderBy((Item) => Item.Type))
                    {
                        Task LoadTask = LibraryStorageFolder.CreateAsync(Record.Type, Record.Path).ContinueWith((PreviousTask) =>
                        {
                            if (PreviousTask.Result == null)
                            {
                                ErrorList.Add(Record.Path);
                            }
                            else if (!LibraryList.Contains(PreviousTask.Result))
                            {
                                LibraryList.Add(PreviousTask.Result);
                            }
                        }, TaskScheduler.FromCurrentSynchronizationContext());

                        if (await Task.WhenAny(LoadTask, Task.Delay(2000)) != LoadTask)
                        {
                            LongRunningTaskList.Add(LoadTask);
                        }
                    }

                    await Task.WhenAll(LongRunningTaskList);
                    await JumpListController.Current.AddItemAsync(JumpListGroup.Library, LibraryList.Where((Library) => Library.LibType == LibraryType.UserCustom)
                                                                                                    .Select((Library) => Library.Path)
                                                                                                    .ToArray());

                    if (!ErrorList.IsEmpty)
                    {
                        foreach (string ErrorPath in ErrorList)
                        {
                            SQLite.Current.DeleteLibraryFolder(ErrorPath);
                        }

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
                    await CoreApplication.MainView.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                    {
                        DriveList.Clear();
                    });

                    List<Task> LongRunningTaskList = new List<Task>();

                    foreach (DriveInfo Drive in DriveInfo.GetDrives().Where((Drives) => Drives.DriveType is DriveType.Fixed or DriveType.Network or DriveType.CDRom))
                    {
                        Task LoadTask = DriveDataBase.CreateAsync(Drive).ContinueWith((PreviousTask) =>
                        {
                            if (PreviousTask.Exception is Exception Ex)
                            {
                                LogTracer.Log(Ex, $"Ignore the drive \"{Drive.Name}\" because we could not get details from this drive");
                            }
                            else if (!DriveList.Contains(PreviousTask.Result))
                            {
                                DriveList.Add(PreviousTask.Result);
                            }
                        }, TaskScheduler.FromCurrentSynchronizationContext());

                        if (await Task.WhenAny(LoadTask, Task.Delay(2000)) != LoadTask)
                        {
                            LongRunningTaskList.Add(LoadTask);
                        }
                    }

                    foreach (DeviceInformation Drive in await DeviceInformation.FindAllAsync(DeviceInformation.GetAqsFilterFromDeviceClass(DeviceClass.PortableStorageDevice)))
                    {
                        Task LoadTask = DriveDataBase.CreateAsync(DriveType.Removable, Drive.Id).ContinueWith((PreviousTask) =>
                        {
                            if (PreviousTask.Exception is Exception Ex)
                            {
                                LogTracer.Log(Ex, $"Ignore the drive \"{Drive.Name}\" because we could not get details from this drive");
                            }
                            else if (PreviousTask.Result != null && !DriveList.Contains(PreviousTask.Result))
                            {
                                DriveList.Add(PreviousTask.Result);
                            }
                        }, TaskScheduler.FromCurrentSynchronizationContext());

                        if (await Task.WhenAny(LoadTask, Task.Delay(2000)) != LoadTask)
                        {
                            LongRunningTaskList.Add(LoadTask);
                        }
                    }

                    foreach (StorageFolder WslFolder in await GetWslDriveAsync())
                    {
                        Task LoadTask = DriveDataBase.CreateAsync(DriveType.Network, WslFolder).ContinueWith((PreviousTask) =>
                        {
                            if (PreviousTask.Exception is Exception Ex)
                            {
                                LogTracer.Log(Ex, $"Ignore the drive \"{WslFolder.Path}\" because we could not get details from this drive");
                            }
                            else if (!DriveList.Contains(PreviousTask.Result))
                            {
                                DriveList.Add(PreviousTask.Result);
                            }
                        }, TaskScheduler.FromCurrentSynchronizationContext());

                        if (await Task.WhenAny(LoadTask, Task.Delay(2000)) != LoadTask)
                        {
                            LongRunningTaskList.Add(LoadTask);
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
                await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                {
                    if (DriveList.FirstOrDefault((Drive) => Drive.DriveId == args.Id) is DriveDataBase RemovedDrive)
                    {
                        DriveList.Remove(RemovedDrive);
                    }
                });
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"An exception was threw im {nameof(PortalDriveWatcher_Removed)}");
            }
        }

        private static async void PortalDriveWatcher_Added(DeviceWatcher sender, DeviceInformation args)
        {
            try
            {
                DriveDataBase NewDrive = await DriveDataBase.CreateAsync(DriveType.Removable, args.Id);

                if (NewDrive != null)
                {
                    await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                    {
                        if (!DriveList.Contains(NewDrive))
                        {
                            DriveList.Add(NewDrive);
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"An exception was threw in {nameof(PortalDriveWatcher_Added)}");
            }
        }

        private async static void DriveList_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            await DriveChangeLocker.WaitAsync();

            try
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
            finally
            {
                DriveChangeLocker.Release();
            }
        }

        private async static Task<IReadOnlyList<StorageFolder>> GetWslDriveAsync()
        {
            try
            {
                StorageFolder WslBaseFolder = await StorageFolder.GetFolderFromPathAsync(@"\\wsl$");

                StorageFolderQueryResult Query = WslBaseFolder.CreateFolderQueryWithOptions(new QueryOptions
                {
                    FolderDepth = FolderDepth.Shallow,
                    IndexerOption = IndexerOption.DoNotUseIndexer
                });

                return await Query.GetFoldersAsync();
            }
            catch
            {
                return new List<StorageFolder>(0);
            }
        }

        private async static void NetworkDriveCheckTimer_Tick(object sender, ElapsedEventArgs e)
        {
            NetworkDriveCheckTimer.Enabled = false;

            DriveInfo[] NewNetworkDrive = DriveInfo.GetDrives().Where((Drives) => Drives.DriveType == DriveType.Network).ToArray();
            DriveDataBase[] ExistNetworkDrive = DriveList.OfType<NormalDriveData>().Where((ExistDrive) => ExistDrive.DriveType == DriveType.Network).ToArray();

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
                    DriveDataBase NetworkDrive = await DriveDataBase.CreateAsync(Drive);

                    await CoreApplication.MainView.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                    {
                        if (!DriveList.Contains(NetworkDrive))
                        {
                            DriveList.Add(NetworkDrive);
                        }
                    });
                }
                catch (Exception ex)
                {
                    LogTracer.Log(ex, $"Ignore the drive \"{Drive.Name}\" because we could not get details from this drive");
                }
            }

            NetworkDriveCheckTimer.Enabled = true;
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
            await LibraryChangeLocker.WaitAsync();

            try
            {
                if (LibraryChanged != null)
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
            finally
            {
                LibraryChangeLocker.Release();
            }
        }
    }
}
