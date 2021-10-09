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
using Windows.ApplicationModel.Core;
using Windows.Devices.Enumeration;
using Windows.Storage;
using Windows.Storage.Search;
using Windows.Storage.Streams;
using Windows.System;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media.Imaging;

namespace RX_Explorer.Class
{
    public static class CommonAccessCollection
    {
        public static ObservableCollection<DriveDataBase> DriveList { get; } = new ObservableCollection<DriveDataBase>();
        public static ObservableCollection<LibraryStorageFolder> LibraryFolderList { get; } = new ObservableCollection<LibraryStorageFolder>();
        public static ObservableCollection<QuickStartItem> QuickStartList { get; } = new ObservableCollection<QuickStartItem>();
        public static ObservableCollection<QuickStartItem> WebLinkList { get; } = new ObservableCollection<QuickStartItem>();

        private static readonly List<StorageFolder> DriveCache = new List<StorageFolder>();

        private static readonly DeviceWatcher PortalDriveWatcher = DeviceInformation.CreateWatcher(DeviceClass.PortableStorageDevice);

        private static readonly DispatcherTimer NetworkDriveCheckTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(5)
        };

        public static event EventHandler<DriveChangedDeferredEventArgs> DriveChanged;

        public static event EventHandler<LibraryChangedDeferredEventArgs> LibraryChanged;

        public static event EventHandler<IEnumerable<string>> LibraryNotFound;

        private static readonly SemaphoreSlim DriveChangeLocker = new SemaphoreSlim(1, 1);

        private static readonly SemaphoreSlim LibraryChangeLocker = new SemaphoreSlim(1, 1);

        public static bool IsLibaryLoaded { get; private set; }

        public static bool IsDriveLoaded { get; private set; }

        public static bool IsQuickStartLoaded { get; private set; }

        private static readonly object DriveListModifyLocker = new object();

        private static int LoadDriveLockResource;
        private static int LoadLibraryLockResource;
        private static int LoadQuickStartLockResource;

        public static async Task LoadQuickStartItemsAsync(bool IsRefresh = false)
        {
            if (Interlocked.Exchange(ref LoadQuickStartLockResource, 1) == 0)
            {
                try
                {
                    if (!IsQuickStartLoaded || IsRefresh)
                    {
                        IsQuickStartLoaded = true;

                        if (IsRefresh)
                        {
                            QuickStartList.Clear();
                            WebLinkList.Clear();
                        }

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
                    LogTracer.Log(ex);
                }
                finally
                {
                    Interlocked.Exchange(ref LoadQuickStartLockResource, 0);
                }
            }
        }

        public static async Task LoadLibraryFoldersAsync(bool IsRefresh = false)
        {
            if (Interlocked.Exchange(ref LoadLibraryLockResource, 1) == 0)
            {
                try
                {
                    if (!IsLibaryLoaded || IsRefresh)
                    {
                        IsLibaryLoaded = true;

                        try
                        {
                            IReadOnlyList<User> UserList = await User.FindAllAsync();

                            UserDataPaths DataPath = UserList.FirstOrDefault((User) => User.AuthenticationStatus == UserAuthenticationStatus.LocallyAuthenticated && User.Type == UserType.LocalUser) is User CurrentUser
                                                     ? UserDataPaths.GetForUser(CurrentUser)
                                                     : UserDataPaths.GetDefault();
                            try
                            {
                                List<(LibraryType, string)> Array = new List<(LibraryType, string)>();

                                if (!string.IsNullOrEmpty(DataPath.Downloads))
                                {
                                    Array.Add((LibraryType.Downloads, DataPath.Downloads));
                                }

                                if (!string.IsNullOrEmpty(DataPath.Desktop))
                                {
                                    Array.Add((LibraryType.Desktop, DataPath.Desktop));
                                }

                                if (!string.IsNullOrEmpty(DataPath.Videos))
                                {
                                    Array.Add((LibraryType.Videos, DataPath.Videos));
                                }

                                if (!string.IsNullOrEmpty(DataPath.Pictures))
                                {
                                    Array.Add((LibraryType.Pictures, DataPath.Pictures));
                                }

                                if (!string.IsNullOrEmpty(DataPath.Documents))
                                {
                                    Array.Add((LibraryType.Document, DataPath.Documents));
                                }

                                if (!string.IsNullOrEmpty(DataPath.Music))
                                {
                                    Array.Add((LibraryType.Music, DataPath.Music));
                                }

                                if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("OneDrive")))
                                {
                                    Array.Add((LibraryType.OneDrive, Environment.GetEnvironmentVariable("OneDrive")));
                                }

                                SQLite.Current.UpdateLibraryPath(Array);
                            }
                            catch (Exception ex)
                            {
                                LogTracer.Log(ex, "An error was threw when getting library folder (In initialize)");
                            }
                        }
                        catch (Exception ex)
                        {
                            LogTracer.Log(ex, "An error was threw when try to get 'UserDataPath' (In initialize)");

                            List<(LibraryType, string)> Array = new List<(LibraryType, string)>();

                            string DesktopPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                            if (!string.IsNullOrEmpty(DesktopPath))
                            {
                                Array.Add((LibraryType.Desktop, DesktopPath));
                            }

                            string VideoPath = Environment.GetFolderPath(Environment.SpecialFolder.MyVideos);
                            if (!string.IsNullOrEmpty(VideoPath))
                            {
                                Array.Add((LibraryType.Videos, VideoPath));
                            }

                            string PicturesPath = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
                            if (!string.IsNullOrEmpty(PicturesPath))
                            {
                                Array.Add((LibraryType.Pictures, PicturesPath));
                            }

                            string DocumentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                            if (!string.IsNullOrEmpty(DocumentsPath))
                            {
                                Array.Add((LibraryType.Document, DocumentsPath));
                            }

                            string MusicPath = Environment.GetFolderPath(Environment.SpecialFolder.MyMusic);
                            if (!string.IsNullOrEmpty(MusicPath))
                            {
                                Array.Add((LibraryType.Music, MusicPath));
                            }

                            string OneDrivePath = Environment.GetEnvironmentVariable("OneDrive");
                            if (!string.IsNullOrEmpty(OneDrivePath))
                            {
                                Array.Add((LibraryType.OneDrive, OneDrivePath));
                            }

                            SQLite.Current.UpdateLibraryPath(Array);
                        }

                        ConcurrentBag<string> ErrorList = new ConcurrentBag<string>();
                        List<Task<LibraryStorageFolder>> LoadTaskList = new List<Task<LibraryStorageFolder>>();

                        foreach ((string, LibraryType) Library in SQLite.Current.GetLibraryPath())
                        {
                            LoadTaskList.Add(LibraryStorageFolder.CreateAsync(Library.Item2, Library.Item1).ContinueWith((PreviousTask) =>
                            {
                                if (PreviousTask.Exception is Exception Ex)
                                {
                                    ErrorList.Add(Library.Item1);
                                    SQLite.Current.DeleteLibrary(Library.Item1);
                                    return null;
                                }
                                else
                                {
                                    return PreviousTask.Result;
                                }
                            }));
                        }

                        LibraryStorageFolder[] Result = await Task.WhenAll(LoadTaskList);

                        LibraryFolderList.Clear();
                        LibraryFolderList.AddRange(Result.OfType<LibraryStorageFolder>().Distinct());
                        
                        if (ErrorList.Count > 0)
                        {
                            LibraryNotFound?.Invoke(null, ErrorList);
                        }

                        await JumpListController.Current.AddItemAsync(JumpListGroup.Library, LibraryFolderList.Where((Library) => Library.LibType == LibraryType.UserCustom).Select((Library) => Library.Path).ToArray());
                    }
                }
                finally
                {
                    Interlocked.Exchange(ref LoadLibraryLockResource, 0);
                }
            }
        }

        public static async Task LoadDriveAsync(bool IsRefresh = false)
        {
            if (Interlocked.Exchange(ref LoadDriveLockResource, 1) == 0)
            {
                try
                {
                    if (!IsDriveLoaded || IsRefresh)
                    {
                        IsDriveLoaded = true;

                        List<Task<DriveDataBase>> LoadTaskList = new List<Task<DriveDataBase>>();

                        foreach (DriveInfo Drive in DriveInfo.GetDrives().Where((Drives) => Drives.DriveType is DriveType.Fixed or DriveType.Network or DriveType.CDRom))
                        {
                            LoadTaskList.Add(DriveDataBase.CreateAsync(Drive).ContinueWith((PreviousTask) =>
                            {
                                if (PreviousTask.Exception is Exception Ex)
                                {
                                    LogTracer.Log(Ex, $"Ignore the drive \"{Drive.Name}\" because we could not get details from this drive");
                                    return null;
                                }
                                else
                                {
                                    return PreviousTask.Result;
                                }
                            }));
                        }

                        foreach (DeviceInformation Drive in await DeviceInformation.FindAllAsync(DeviceInformation.GetAqsFilterFromDeviceClass(DeviceClass.PortableStorageDevice)))
                        {
                            LoadTaskList.Add(DriveDataBase.CreateAsync(DriveType.Removable, Drive.Id).ContinueWith((PreviousTask) =>
                            {
                                if (PreviousTask.Exception is Exception Ex)
                                {
                                    LogTracer.Log(Ex, $"Ignore the drive \"{Drive.Name}\" because we could not get details from this drive");
                                    return null;
                                }
                                else
                                {
                                    return PreviousTask.Result;
                                }
                            }));
                        }

                        foreach (StorageFolder WslFolder in await GetWslDriveAsync())
                        {
                            LoadTaskList.Add(DriveDataBase.CreateAsync(DriveType.Network, WslFolder).ContinueWith((PreviousTask) =>
                            {
                                if (PreviousTask.Exception is Exception Ex)
                                {
                                    LogTracer.Log(Ex, $"Ignore the drive \"{WslFolder.Path}\" because we could not get details from this drive");
                                    return null;
                                }
                                else
                                {
                                    return PreviousTask.Result;
                                }
                            }));
                        }

                        DriveDataBase[] Result = await Task.WhenAll(LoadTaskList);

                        lock (DriveListModifyLocker)
                        {
                            DriveList.Clear();
                            DriveList.AddRange(Result.OfType<DriveDataBase>().Distinct());
                        }

                        switch (PortalDriveWatcher.Status)
                        {
                            case DeviceWatcherStatus.Created:
                            case DeviceWatcherStatus.Aborted:
                            case DeviceWatcherStatus.Stopped:
                                {
                                    PortalDriveWatcher.Start();
                                    break;
                                }
                        }

                        if (!NetworkDriveCheckTimer.IsEnabled)
                        {
                            NetworkDriveCheckTimer.Start();
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogTracer.Log(ex, $"An exception was threw in {nameof(LoadDriveAsync)}");
                }
                finally
                {
                    Interlocked.Exchange(ref LoadDriveLockResource, 0);
                }
            }
        }

        public static IReadOnlyList<StorageFolder> GetMissedDriveBeforeSubscribeEvents()
        {
            return DriveCache.AsReadOnly();
        }

        private static async void PortalDriveWatcher_Removed(DeviceWatcher sender, DeviceInformationUpdate args)
        {
            try
            {
                await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                {
                    lock (DriveListModifyLocker)
                    {
                        if (DriveList.FirstOrDefault((Drive) => Drive.DriveId == args.Id) is DriveDataBase RemovedDrive)
                        {
                            DriveList.Remove(RemovedDrive);
                        }
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

                await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                {
                    lock (DriveListModifyLocker)
                    {
                        if (DriveList.All((Drive) => Drive != NewDrive))
                        {
                            DriveList.Add(NewDrive);
                        }
                    }
                });
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
                                    await DriveChanged.InvokeAsync(null, new DriveChangedDeferredEventArgs(CommonChangeType.Added, new FileSystemStorageFolder(Drive.DriveFolder)));
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
                                    await DriveChanged.InvokeAsync(null, new DriveChangedDeferredEventArgs(CommonChangeType.Removed, new FileSystemStorageFolder(Drive.DriveFolder)));
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

        private async static void NetworkDriveCheckTimer_Tick(object sender, object e)
        {
            NetworkDriveCheckTimer.Stop();

            DriveInfo[] NewNetworkDrive = DriveInfo.GetDrives().Where((Drives) => Drives.DriveType == DriveType.Network).ToArray();
            DriveDataBase[] ExistNetworkDrive = DriveList.OfType<NormalDriveData>().Where((ExistDrive) => ExistDrive.DriveType == DriveType.Network).ToArray();

            IEnumerable<DriveInfo> AddList = NewNetworkDrive.Where((NewDrive) => ExistNetworkDrive.All((ExistDrive) => !ExistDrive.Path.Equals(NewDrive.Name, StringComparison.OrdinalIgnoreCase)));
            IEnumerable<DriveDataBase> RemoveList = ExistNetworkDrive.Where((ExistDrive) => NewNetworkDrive.All((NewDrive) => !ExistDrive.Path.Equals(NewDrive.Name, StringComparison.OrdinalIgnoreCase)));

            List<Task<DriveDataBase>> LoadTaskList = new List<Task<DriveDataBase>>();

            foreach (DriveInfo Drive in AddList)
            {
                LoadTaskList.Add(DriveDataBase.CreateAsync(Drive).ContinueWith((PreviousTask) =>
                {
                    if (PreviousTask.Exception is Exception Ex)
                    {
                        LogTracer.Log(Ex, $"Ignore the drive \"{Drive.Name}\" because we could not get details from this drive");
                        return null;
                    }
                    else
                    {
                        return PreviousTask.Result;
                    }
                }));
            }

            DriveDataBase[] Result = await Task.WhenAll(LoadTaskList);

            lock (DriveListModifyLocker)
            {
                foreach (DriveDataBase ExistDrive in RemoveList)
                {
                    DriveList.Remove(ExistDrive);
                }

                DriveList.AddRange(Result.OfType<DriveDataBase>().Distinct());
            }

            NetworkDriveCheckTimer.Start();
        }

        static CommonAccessCollection()
        {
            PortalDriveWatcher.Added += PortalDriveWatcher_Added;
            PortalDriveWatcher.Removed += PortalDriveWatcher_Removed;
            NetworkDriveCheckTimer.Tick += NetworkDriveCheckTimer_Tick;

            DriveList.CollectionChanged += DriveList_CollectionChanged;
            LibraryFolderList.CollectionChanged += LibraryFolderList_CollectionChanged;
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
