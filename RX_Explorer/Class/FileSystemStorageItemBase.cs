using Microsoft.Win32.SafeHandles;
using RX_Explorer.Interface;
using ShareClassLibrary;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.Core;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.Storage.Streams;
using Windows.UI.Core;
using Windows.UI.Xaml.Media.Imaging;

namespace RX_Explorer.Class
{
    /// <summary>
    /// 提供对设备中的存储对象的描述
    /// </summary>
    public abstract class FileSystemStorageItemBase : IStorageItemPropertiesBase, INotifyPropertyChanged, IStorageItemOperation, IEquatable<FileSystemStorageItemBase>
    {
        public string Path { get; protected set; }

        public virtual string SizeDescription { get; }

        public virtual string Name => System.IO.Path.GetFileName(Path) ?? string.Empty;

        public virtual string DisplayName => Name;

        public virtual string Type => System.IO.Path.GetExtension(Path)?.ToUpper() ?? string.Empty;

        public virtual string DisplayType => Type;

        public ColorTag ColorTag
        {
            get
            {
                return SQLite.Current.GetColorTag(Path);
            }
            set
            {
                SQLite.Current.SetColorTag(Path, value);
                OnPropertyChanged();
            }
        }

        private bool ThubmnalModeChanged;
        private int IsLoaded;
        private RefSharedRegion<FullTrustProcessController.ExclusiveUsage> ControllerSharedRef;

        public double ThumbnailOpacity { get; protected set; } = 1d;

        public ulong Size { get; protected set; }

        public DateTimeOffset CreationTime { get; protected set; }

        public DateTimeOffset ModifiedTime { get; protected set; }

        public virtual string ModifiedTimeDescription
        {
            get
            {
                if (ModifiedTime != DateTimeOffset.MaxValue.ToLocalTime() && ModifiedTime != DateTimeOffset.MinValue.ToLocalTime())
                {
                    return ModifiedTime.ToString("G");
                }
                else
                {
                    return string.Empty;
                }
            }
        }

        public virtual string CreationTimeDescription
        {
            get
            {
                if (CreationTime == DateTimeOffset.MaxValue.ToLocalTime())
                {
                    return Globalization.GetString("UnknownText");
                }
                else
                {
                    return CreationTime.ToString("G");
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public virtual BitmapImage Thumbnail { get; private set; }

        public virtual BitmapImage ThumbnailOverlay { get; protected set; }

        public virtual bool IsReadOnly { get; protected set; }

        public virtual bool IsSystemItem { get; protected set; }

        protected virtual bool ShouldGenerateThumbnail => (this is FileSystemStorageFile && SettingPage.ContentLoadMode == LoadMode.OnlyFile) || SettingPage.ContentLoadMode == LoadMode.All;

        protected ThumbnailMode ThumbnailMode { get; set; } = ThumbnailMode.ListView;

        public SyncStatus SyncStatus { get; protected set; } = SyncStatus.Unknown;

        protected IStorageItem StorageItem { get; set; }

        protected static readonly Uri Const_Folder_Image_Uri = WindowsVersionChecker.IsNewerOrEqual(Version.Windows11)
                                                                 ? new Uri("ms-appx:///Assets/FolderIcon_Win11.png")
                                                                 : new Uri("ms-appx:///Assets/FolderIcon_Win10.png");

        protected static readonly Uri Const_File_White_Image_Uri = new Uri("ms-appx:///Assets/Page_Solid_White.png");

        protected static readonly Uri Const_File_Black_Image_Uri = new Uri("ms-appx:///Assets/Page_Solid_Black.png");

        public static async Task<bool> CheckExistAsync(string Path)
        {
            if (!string.IsNullOrEmpty(Path) && System.IO.Path.IsPathRooted(Path))
            {
                try
                {
                    try
                    {
                        return await Task.Run(() => Win32_Native_API.CheckExist(Path));
                    }
                    catch (LocationNotAvailableException)
                    {
                        string DirectoryPath = System.IO.Path.GetDirectoryName(Path);

                        if (string.IsNullOrEmpty(DirectoryPath))
                        {
                            await StorageFolder.GetFolderFromPathAsync(Path);
                            return true;
                        }
                        else
                        {
                            StorageFolder Folder = await StorageFolder.GetFolderFromPathAsync(DirectoryPath);

                            if (await Folder.TryGetItemAsync(System.IO.Path.GetFileName(Path)) != null)
                            {
                                return true;
                            }
                            else
                            {
                                return false;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogTracer.Log(ex, "CheckExist threw an exception");
                    return false;
                }
            }
            else
            {
                return false;
            }
        }

        public static Task<IReadOnlyList<FileSystemStorageItemBase>> OpenInBatchAsync(IEnumerable<string> PathArray)
        {
            return Task.Factory.StartNew<IReadOnlyList<FileSystemStorageItemBase>>(() =>
            {
                ConcurrentBag<FileSystemStorageItemBase> Result = new ConcurrentBag<FileSystemStorageItemBase>();
                ConcurrentBag<(string, Exception)> RetryBag = new ConcurrentBag<(string, Exception)>();

                Parallel.ForEach(PathArray, (Path) =>
                {
                    try
                    {
                        if (Win32_Native_API.GetStorageItem(Path) is FileSystemStorageItemBase Item)
                        {
                            Result.Add(Item);
                        }
                    }
                    catch (LocationNotAvailableException)
                    {
                        try
                        {
                            string DirectoryPath = System.IO.Path.GetDirectoryName(Path);

                            if (string.IsNullOrEmpty(DirectoryPath))
                            {
                                StorageFolder Folder = StorageFolder.GetFolderFromPathAsync(Path).AsTask().Result;
                                Result.Add(new FileSystemStorageFolder(Folder));
                            }
                            else
                            {
                                StorageFolder ParentFolder = StorageFolder.GetFolderFromPathAsync(DirectoryPath).AsTask().Result;

                                switch (ParentFolder.TryGetItemAsync(System.IO.Path.GetFileName(Path)).AsTask().Result)
                                {
                                    case StorageFolder Folder:
                                        {
                                            Result.Add(new FileSystemStorageFolder(Folder));
                                            break;
                                        }
                                    case StorageFile File:
                                        {
                                            Result.Add(new FileSystemStorageFile(File));
                                            break;
                                        }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            if (ex is not FileNotFoundException or DirectoryNotFoundException)
                            {
                                RetryBag.Add((Path, ex));
                            }
                        }
                    }
                });

                using (FullTrustProcessController.ExclusiveUsage Exclusive = FullTrustProcessController.GetAvailableControllerAsync().Result)
                {
                    foreach ((string Path, Exception ex) in RetryBag)
                    {
                        using (SafeFileHandle Handle = Exclusive.Controller.GetFileHandleAsync(Path, AccessMode.ReadWrite).Result)
                        {
                            if (Handle.IsInvalid)
                            {
                                LogTracer.Log(ex, $"{nameof(OpenInBatchAsync)} failed and could not get the storage item, path:\"{Path}\"");
                            }
                            else
                            {
                                LogTracer.Log($"Try get storage item from {nameof(Win32_Native_API.GetStorageItemFromHandle)}");

                                if (Win32_Native_API.GetStorageItemFromHandle(Path, Handle.DangerousGetHandle()) is FileSystemStorageItemBase Item)
                                {
                                    Result.Add(Item);
                                }
                                else
                                {
                                    LogTracer.Log(ex, $"{nameof(OpenInBatchAsync)} failed and could not get the storage item, path:\"{Path}\"");
                                }
                            }
                        }
                    }
                }

                return Result.ToList();
            }, TaskCreationOptions.LongRunning);
        }

        public static async Task<FileSystemStorageItemBase> OpenAsync(string Path)
        {
            try
            {
                try
                {
                    return await Task.Run(() => Win32_Native_API.GetStorageItem(Path));
                }
                catch (LocationNotAvailableException)
                {
                    try
                    {
                        string DirectoryPath = System.IO.Path.GetDirectoryName(Path);

                        if (string.IsNullOrEmpty(DirectoryPath))
                        {
                            StorageFolder Folder = await StorageFolder.GetFolderFromPathAsync(Path);
                            return new FileSystemStorageFolder(Folder);
                        }
                        else
                        {
                            StorageFolder ParentFolder = await StorageFolder.GetFolderFromPathAsync(DirectoryPath);

                            switch (await ParentFolder.TryGetItemAsync(System.IO.Path.GetFileName(Path)))
                            {
                                case StorageFolder Folder:
                                    {
                                        return new FileSystemStorageFolder(Folder);
                                    }
                                case StorageFile File:
                                    {
                                        return new FileSystemStorageFile(File);
                                    }
                                default:
                                    {
                                        LogTracer.Log($"UWP storage API could not found the path: \"{Path}\"");
                                        return null;
                                    }
                            }
                        }
                    }
                    catch (Exception ex) when (ex is not FileNotFoundException or DirectoryNotFoundException)
                    {
                        using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableControllerAsync())
                        using (SafeFileHandle Handle = await Exclusive.Controller.GetFileHandleAsync(Path, AccessMode.ReadWrite))
                        {
                            if (Handle.IsInvalid)
                            {
                                throw;
                            }
                            else
                            {
                                LogTracer.Log($"Try get storageitem from {nameof(Win32_Native_API.GetStorageItemFromHandle)}");
                                return Win32_Native_API.GetStorageItemFromHandle(Path, Handle.DangerousGetHandle());
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"{nameof(OpenAsync)} failed and could not get the storage item, path:\"{Path}\"");
                return null;
            }
        }

        public static async Task<FileSystemStorageItemBase> CreateNewAsync(string Path, StorageItemTypes ItemTypes, CreateOption Option)
        {
            switch (ItemTypes)
            {
                case StorageItemTypes.File:
                    {
                        try
                        {
                            if (Win32_Native_API.CreateFileFromPath(Path, Option, out string NewPath))
                            {
                                return await OpenAsync(NewPath);
                            }
                            else
                            {
                                StorageFolder Folder = await StorageFolder.GetFolderFromPathAsync(System.IO.Path.GetDirectoryName(Path));

                                switch (Option)
                                {
                                    case CreateOption.GenerateUniqueName:
                                        {
                                            StorageFile NewFile = await Folder.CreateFileAsync(System.IO.Path.GetFileName(Path), CreationCollisionOption.GenerateUniqueName);
                                            return new FileSystemStorageFile(NewFile);
                                        }
                                    case CreateOption.OpenIfExist:
                                        {
                                            StorageFile NewFile = await Folder.CreateFileAsync(System.IO.Path.GetFileName(Path), CreationCollisionOption.OpenIfExists);
                                            return new FileSystemStorageFile(NewFile);
                                        }
                                    case CreateOption.ReplaceExisting:
                                        {
                                            StorageFile NewFile = await Folder.CreateFileAsync(System.IO.Path.GetFileName(Path), CreationCollisionOption.ReplaceExisting);
                                            return new FileSystemStorageFile(NewFile);
                                        }
                                    default:
                                        {
                                            return null;
                                        }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            LogTracer.Log(ex, $"{nameof(CreateNewAsync)} failed and could not create the storage item, path:\"{Path}\"");

                            using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableControllerAsync())
                            {
                                string NewItemPath = await Exclusive.Controller.CreateNewAsync(CreateType.File, Path);

                                if (string.IsNullOrEmpty(NewItemPath))
                                {
                                    LogTracer.Log("Elevated FullTrustProcess could not create a new file");
                                    return null;
                                }
                                else
                                {
                                    return await OpenAsync(NewItemPath);
                                }
                            }
                        }
                    }
                case StorageItemTypes.Folder:
                    {
                        try
                        {
                            if (Win32_Native_API.CreateDirectoryFromPath(Path, Option, out string NewPath))
                            {
                                return await OpenAsync(NewPath);
                            }
                            else
                            {
                                StorageFolder Folder = await StorageFolder.GetFolderFromPathAsync(System.IO.Path.GetDirectoryName(Path));

                                switch (Option)
                                {
                                    case CreateOption.GenerateUniqueName:
                                        {
                                            StorageFolder NewFolder = await Folder.CreateFolderAsync(System.IO.Path.GetFileName(Path), CreationCollisionOption.GenerateUniqueName);
                                            return new FileSystemStorageFolder(NewFolder);
                                        }
                                    case CreateOption.OpenIfExist:
                                        {
                                            StorageFolder NewFolder = await Folder.CreateFolderAsync(System.IO.Path.GetFileName(Path), CreationCollisionOption.OpenIfExists);
                                            return new FileSystemStorageFolder(NewFolder);
                                        }
                                    case CreateOption.ReplaceExisting:
                                        {
                                            StorageFolder NewFolder = await Folder.CreateFolderAsync(System.IO.Path.GetFileName(Path), CreationCollisionOption.ReplaceExisting);
                                            return new FileSystemStorageFolder(NewFolder);
                                        }
                                    default:
                                        {
                                            return null;
                                        }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            LogTracer.Log(ex, $"{nameof(CreateNewAsync)} failed and could not create the storage item, path:\"{Path}\"");

                            using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableControllerAsync())
                            {
                                string NewItemPath = await Exclusive.Controller.CreateNewAsync(CreateType.Folder, Path);

                                if (string.IsNullOrEmpty(NewItemPath))
                                {
                                    LogTracer.Log("Elevated FullTrustProcess could not create new");
                                    return null;
                                }
                                else
                                {
                                    return await OpenAsync(NewItemPath);
                                }
                            }
                        }
                    }
                default:
                    {
                        return null;
                    }
            }
        }

        protected FileSystemStorageItemBase(string Path, SafeFileHandle Handle, bool LeaveOpen) : this(Win32_Native_API.GetStorageItemRawDataFromHandle(Path, Handle.DangerousGetHandle()))
        {
            if (!LeaveOpen)
            {
                Handle.Dispose();
            }
        }

        protected FileSystemStorageItemBase(Win32_File_Data Data)
        {
            Path = Data.Path;

            if (Data.IsDataValid)
            {
                IsReadOnly = Data.IsReadOnly;
                IsSystemItem = Data.IsSystemItem;
                Size = Data.Size;
                ModifiedTime = Data.ModifiedTime;
                CreationTime = Data.CreationTime;
            }
        }

        protected void OnPropertyChanged([CallerMemberName] string PropertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(PropertyName));
        }

        public virtual void SetThumbnailOpacity(ThumbnailStatus Status)
        {
            switch (Status)
            {
                case ThumbnailStatus.Normal:
                    {
                        if (ThumbnailOpacity != 1d)
                        {
                            ThumbnailOpacity = 1d;
                        }

                        break;
                    }
                case ThumbnailStatus.ReducedOpacity:
                    {
                        if (ThumbnailOpacity != 0.5)
                        {
                            ThumbnailOpacity = 0.5;
                        }

                        break;
                    }
            }

            OnPropertyChanged(nameof(ThumbnailOpacity));
        }


        public void SetThumbnailMode(ThumbnailMode Mode)
        {
            if (Mode != ThumbnailMode)
            {
                ThumbnailMode = Mode;
                ThubmnalModeChanged = true;
            }
        }

        public async Task LoadAsync()
        {
            try
            {
                if (Interlocked.Exchange(ref IsLoaded, 1) > 0)
                {
                    if (ThubmnalModeChanged)
                    {
                        ThubmnalModeChanged = false;

                        if (ShouldGenerateThumbnail)
                        {
                            Thumbnail = await GetThumbnailAsync(ThumbnailMode);
                        }
                    }
                }
                else
                {
                    await StartProcessRefShareRegionAsync();

                    try
                    {
                        async Task LocalLoadFunction()
                        {
                            if (ShouldGenerateThumbnail)
                            {
                                await LoadCoreAsync(false);

                                if (await GetThumbnailAsync(ThumbnailMode) is BitmapImage Thumbnail)
                                {
                                    this.Thumbnail = Thumbnail;
                                }
                            }

                            ThumbnailOverlay = await GetThumbnailOverlayAsync();

                            if (SpecialPath.IsPathIncluded(Path, SpecialPath.SpecialPathEnum.OneDrive))
                            {
                                await GetSyncStatusAsync();
                            }
                        };

                        if (CoreApplication.MainView.CoreWindow.Dispatcher.HasThreadAccess)
                        {
                            await LocalLoadFunction();
                        }
                        else
                        {
                            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, async () => await LocalLoadFunction());
                        }
                    }
                    finally
                    {
                        await EndProcessRefShareRegionAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"An exception was threw in {nameof(LoadAsync)}, StorageType: {GetType().FullName}, Path: {Path}");
            }
            finally
            {
                OnPropertyChanged(nameof(Name));
                OnPropertyChanged(nameof(SizeDescription));
                OnPropertyChanged(nameof(DisplayType));
                OnPropertyChanged(nameof(ModifiedTimeDescription));
                OnPropertyChanged(nameof(Thumbnail));
                OnPropertyChanged(nameof(ThumbnailOverlay));
            }
        }

        public async Task StartProcessRefShareRegionAsync()
        {
            RefSharedRegion<FullTrustProcessController.ExclusiveUsage> Ref = new RefSharedRegion<FullTrustProcessController.ExclusiveUsage>(await FullTrustProcessController.GetAvailableControllerAsync());

            if (Interlocked.Exchange(ref ControllerSharedRef, Ref) is RefSharedRegion<FullTrustProcessController.ExclusiveUsage> PreviousRef)
            {
                PreviousRef.Dispose();
            }
        }

        protected RefSharedRegion<FullTrustProcessController.ExclusiveUsage> GetProcessRefShareRegion()
        {
            return ControllerSharedRef?.CreateNew();
        }

        public Task EndProcessRefShareRegionAsync()
        {
            if (Interlocked.Exchange(ref ControllerSharedRef, null) is RefSharedRegion<FullTrustProcessController.ExclusiveUsage> PreviousRef)
            {
                PreviousRef.Dispose();
            }

            return Task.CompletedTask;
        }

        private async Task GetSyncStatusAsync()
        {
            IReadOnlyDictionary<string, string> Properties = await GetPropertiesAsync(new string[] { "System.FilePlaceholderStatus", "System.FileOfflineAvailabilityStatus" });

            if (string.IsNullOrEmpty(Properties["System.FilePlaceholderStatus"]) && string.IsNullOrEmpty(Properties["System.FileOfflineAvailabilityStatus"]))
            {
                SyncStatus = SyncStatus.Unknown;
            }
            else
            {
                int StatusIndex = string.IsNullOrEmpty(Properties["System.FilePlaceholderStatus"]) ? Convert.ToInt32(Properties["System.FileOfflineAvailabilityStatus"])
                                                                                                   : Convert.ToInt32(Properties["System.FilePlaceholderStatus"]);
                SyncStatus = StatusIndex switch
                {
                    0 or 1 or 8 => SyncStatus.AvailableOnline,
                    2 or 3 or 14 or 15 => SyncStatus.AvailableOffline,
                    9 => SyncStatus.Sync,
                    4 => SyncStatus.Excluded,
                    _ => SyncStatus.Unknown
                };
            }

            OnPropertyChanged(nameof(SyncStatus));
        }

        public async Task<SafeFileHandle> GetNativeHandleAsync(AccessMode Mode)
        {
            if (await GetStorageItemAsync() is IStorageItem Item)
            {
                return Item.GetSafeFileHandle(Mode);
            }
            else
            {
                using (RefSharedRegion<FullTrustProcessController.ExclusiveUsage> ControllerRef = GetProcessRefShareRegion())
                {
                    if (ControllerRef != null)
                    {
                        return await ControllerRef.Value.Controller.GetFileHandleAsync(Path, Mode);
                    }
                    else
                    {
                        using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableControllerAsync())
                        {
                            return await Exclusive.Controller.GetFileHandleAsync(Path, Mode);
                        }
                    }
                }
            }
        }

        protected virtual async Task<BitmapImage> GetThumbnailOverlayAsync()
        {
            async Task<BitmapImage> GetThumbnailOverlayCoreAsync(FullTrustProcessController.ExclusiveUsage Exclusive)
            {
                byte[] ThumbnailOverlayByteArray = await Exclusive.Controller.GetThumbnailOverlayAsync(Path);

                if (ThumbnailOverlayByteArray.Length > 0)
                {
                    using (MemoryStream Ms = new MemoryStream(ThumbnailOverlayByteArray))
                    {
                        BitmapImage Overlay = new BitmapImage();
                        await Overlay.SetSourceAsync(Ms.AsRandomAccessStream());
                        return Overlay;
                    }
                }
                else
                {
                    return null;
                }
            }

            using (RefSharedRegion<FullTrustProcessController.ExclusiveUsage> ControllerRef = GetProcessRefShareRegion())
            {
                if (ControllerRef != null)
                {
                    return await GetThumbnailOverlayCoreAsync(ControllerRef.Value);
                }
                else
                {
                    using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableControllerAsync())
                    {
                        return await GetThumbnailOverlayCoreAsync(Exclusive);
                    }
                }
            }
        }

        protected abstract Task LoadCoreAsync(bool ForceUpdate);

        public abstract Task<IStorageItem> GetStorageItemAsync();

        public virtual async Task<BitmapImage> GetThumbnailAsync(ThumbnailMode Mode)
        {
            async Task<BitmapImage> GetThumbnailTask()
            {
                async Task<BitmapImage> GetThumbnailCoreAsync(FullTrustProcessController.ExclusiveUsage Exclusive)
                {
                    byte[] ThumbnailData = await Exclusive.Controller.GetThumbnailAsync(Path);

                    if (ThumbnailData.Length > 0)
                    {
                        using (MemoryStream IconStream = new MemoryStream(ThumbnailData))
                        {
                            BitmapImage Image = new BitmapImage();
                            await Image.SetSourceAsync(IconStream.AsRandomAccessStream());
                            return Image;
                        }
                    }
                    else
                    {
                        return null;
                    }
                }

                using (RefSharedRegion<FullTrustProcessController.ExclusiveUsage> ControllerRef = GetProcessRefShareRegion())
                {
                    if (ControllerRef != null)
                    {
                        return await GetThumbnailCoreAsync(ControllerRef.Value);
                    }
                    else
                    {
                        using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableControllerAsync())
                        {
                            return await GetThumbnailCoreAsync(Exclusive);
                        }
                    }
                }
            }

            if (await GetStorageItemAsync() is IStorageItem Item)
            {
                BitmapImage LocalThumbnail = await Item.GetThumbnailBitmapAsync(Mode);

                if (LocalThumbnail == null)
                {
                    return await GetThumbnailTask();
                }
                else
                {
                    return LocalThumbnail;
                }
            }
            else
            {
                return await GetThumbnailTask();
            }
        }

        public virtual async Task<IRandomAccessStream> GetThumbnailRawStreamAsync(ThumbnailMode Mode)
        {
            if (await GetStorageItemAsync() is IStorageItem Item)
            {
                return await Item.GetThumbnailRawStreamAsync(Mode);
            }
            else
            {
                async Task<IRandomAccessStream> GetThumbnailRawStreamCoreAsync(FullTrustProcessController.ExclusiveUsage Exclusive)
                {
                    byte[] ThumbnailData = await Exclusive.Controller.GetThumbnailAsync(Path);

                    if (ThumbnailData.Length > 0)
                    {
                        using (MemoryStream IconStream = new MemoryStream(ThumbnailData))
                        {
                            return IconStream.AsRandomAccessStream();
                        }
                    }
                    else
                    {
                        return null;
                    }
                }

                using (RefSharedRegion<FullTrustProcessController.ExclusiveUsage> ControllerRef = GetProcessRefShareRegion())
                {
                    if (ControllerRef != null)
                    {
                        return await GetThumbnailRawStreamCoreAsync(ControllerRef.Value);
                    }
                    else
                    {
                        using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableControllerAsync())
                        {
                            return await GetThumbnailRawStreamCoreAsync(Exclusive);
                        }
                    }
                }
            }
        }

        public async Task<IReadOnlyDictionary<string, string>> GetPropertiesAsync(IEnumerable<string> Properties)
        {
            async Task<IReadOnlyDictionary<string, string>> GetPropertiesTask(IEnumerable<string> Properties)
            {
                using (RefSharedRegion<FullTrustProcessController.ExclusiveUsage> ControllerRef = GetProcessRefShareRegion())
                {
                    if (ControllerRef != null)
                    {
                        return await ControllerRef.Value.Controller.GetPropertiesAsync(Path, Properties);
                    }
                    else
                    {
                        using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableControllerAsync())
                        {
                            return await Exclusive.Controller.GetPropertiesAsync(Path, Properties);
                        }
                    }
                }
            }

            IEnumerable<string> DistinctProperties = Properties.Distinct();

            if (await GetStorageItemAsync() is IStorageItem Item)
            {
                try
                {
                    Dictionary<string, string> Result = new Dictionary<string, string>();

                    BasicProperties Basic = await Item.GetBasicPropertiesAsync();
                    IDictionary<string, object> UwpResult = await Basic.RetrievePropertiesAsync(DistinctProperties);

                    List<string> MissingKeys = new List<string>(DistinctProperties.Except(UwpResult.Keys));

                    foreach (KeyValuePair<string, object> Pair in UwpResult)
                    {
                        string Value = Pair.Value switch
                        {
                            IEnumerable<string> Array => string.Join(", ", Array),
                            _ => Convert.ToString(Pair.Value)
                        };

                        if (string.IsNullOrEmpty(Value))
                        {
                            MissingKeys.Add(Pair.Key);
                        }
                        else
                        {
                            Result.Add(Pair.Key, Value);
                        }
                    }

                    if (MissingKeys.Count > 0)
                    {
                        Result.AddRange(await GetPropertiesTask(DistinctProperties));
                    }

                    return Result;
                }
                catch
                {
                    return await GetPropertiesTask(DistinctProperties);
                }
            }
            else
            {
                return await GetPropertiesTask(DistinctProperties);
            }
        }

        public async Task RefreshAsync()
        {
            try
            {
                if (await CheckExistAsync(Path))
                {
                    await LoadCoreAsync(true);

                    OnPropertyChanged(nameof(SizeDescription));
                    OnPropertyChanged(nameof(Name));
                    OnPropertyChanged(nameof(ModifiedTimeDescription));
                    OnPropertyChanged(nameof(Thumbnail));
                    OnPropertyChanged(nameof(DisplayType));
                }
                else
                {
                    LogTracer.Log($"File/Folder not found or access deny when executing FileSystemStorageItemBase.Update, path: {Path}");
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"An exception was threw when executing FileSystemStorageItemBase.Update, path: {Path}");
            }
        }

        public virtual async Task MoveAsync(string DirectoryPath, CollisionOptions Option = CollisionOptions.None, ProgressChangedEventHandler ProgressHandler = null)
        {
            using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableControllerAsync())
            {
                await Exclusive.Controller.MoveAsync(Path, DirectoryPath, Option, true, ProgressHandler: ProgressHandler);
            }
        }

        public virtual Task MoveAsync(FileSystemStorageFolder Directory, CollisionOptions Option = CollisionOptions.None, ProgressChangedEventHandler ProgressHandler = null)
        {
            return MoveAsync(Directory.Path, Option, ProgressHandler);
        }

        public virtual async Task CopyAsync(string DirectoryPath, CollisionOptions Option = CollisionOptions.None, ProgressChangedEventHandler ProgressHandler = null)
        {
            using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableControllerAsync())
            {
                await Exclusive.Controller.CopyAsync(Path, DirectoryPath, Option, true, ProgressHandler: ProgressHandler);
            }
        }

        public virtual Task CopyAsync(FileSystemStorageFolder Directory, CollisionOptions Option = CollisionOptions.None, ProgressChangedEventHandler ProgressHandler = null)
        {
            return CopyAsync(Directory.Path, Option, ProgressHandler);
        }

        public async virtual Task<string> RenameAsync(string DesireName)
        {
            using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableControllerAsync())
            {
                string NewName = await Exclusive.Controller.RenameAsync(Path, DesireName);
                Path = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Path), NewName);
                return NewName;
            }
        }

        public virtual async Task DeleteAsync(bool PermanentDelete, ProgressChangedEventHandler ProgressHandler = null)
        {
            using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableControllerAsync())
            {
                await Exclusive.Controller.DeleteAsync(Path, PermanentDelete, true, ProgressHandler: ProgressHandler);
            }
        }

        public override string ToString()
        {
            return Name;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(this, obj))
            {
                return true;
            }
            else
            {
                if (obj is FileSystemStorageItemBase Item)
                {
                    return Item.Path.Equals(Path, StringComparison.OrdinalIgnoreCase);
                }
                else
                {
                    return false;
                }
            }
        }

        public override int GetHashCode()
        {
            return Path.GetHashCode();
        }

        public bool Equals(FileSystemStorageItemBase other)
        {
            if (ReferenceEquals(this, other))
            {
                return true;
            }
            else
            {
                if (other == null)
                {
                    return false;
                }
                else
                {
                    return other.Path.Equals(Path, StringComparison.OrdinalIgnoreCase);
                }
            }
        }

        public static bool operator ==(FileSystemStorageItemBase left, FileSystemStorageItemBase right)
        {
            if (left is null)
            {
                return right is null;
            }
            else
            {
                if (right is null)
                {
                    return false;
                }
                else
                {
                    return left.Path.Equals(right.Path, StringComparison.OrdinalIgnoreCase);
                }
            }
        }

        public static bool operator !=(FileSystemStorageItemBase left, FileSystemStorageItemBase right)
        {
            if (left is null)
            {
                return right is object;
            }
            else
            {
                if (right is null)
                {
                    return true;
                }
                else
                {
                    return !left.Path.Equals(right.Path, StringComparison.OrdinalIgnoreCase);
                }
            }
        }

        public static explicit operator StorageFile(FileSystemStorageItemBase File)
        {
            return File.StorageItem as StorageFile;
        }

        public static explicit operator StorageFolder(FileSystemStorageItemBase File)
        {
            return File.StorageItem as StorageFolder;
        }

        public static class SpecialPath
        {
            public static IReadOnlyList<string> OneDrivePathCollection { get; } = new List<string>
            {
                Environment.GetEnvironmentVariable("OneDriveConsumer"),
                Environment.GetEnvironmentVariable("OneDriveCommercial"),
                Environment.GetEnvironmentVariable("OneDrive")
            };

            public enum SpecialPathEnum
            {
                OneDrive
            }

            public static bool IsPathIncluded(string Path, SpecialPathEnum Enum)
            {
                switch (Enum)
                {
                    case SpecialPathEnum.OneDrive:
                        {
                            return OneDrivePathCollection.Where((Path) => !string.IsNullOrEmpty(Path)).Any((OneDrivePath) => Path.StartsWith(OneDrivePath, StringComparison.OrdinalIgnoreCase) && !Path.Equals(OneDrivePath, StringComparison.OrdinalIgnoreCase));
                        }
                    default:
                        {
                            return false;
                        }
                }
            }
        }
    }
}
