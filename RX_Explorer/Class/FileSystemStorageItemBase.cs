using Microsoft.Win32.SafeHandles;
using RX_Explorer.Interface;
using RX_Explorer.View;
using ShareClassLibrary;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
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

        public virtual string Type => System.IO.Path.GetExtension(Path)?.ToUpper() ?? string.Empty;

        public abstract string DisplayName { get; }

        public abstract string DisplayType { get; }

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

        private int IsContentLoaded;
        private int IsThubmnalModeChanged;
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

        public static async Task<bool> CheckExistsAsync(string Path)
        {
            if (!string.IsNullOrEmpty(Path))
            {
                try
                {
                    if (Path.StartsWith(@"\\?\", StringComparison.OrdinalIgnoreCase))
                    {
                        using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableControllerAsync())
                        {
                            return await Exclusive.Controller.MTPCheckExists(Path);
                        }
                    }
                    else
                    {
                        try
                        {
                            return Win32_Native_API.CheckExists(Path);
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

                                if (await Folder.TryGetItemAsync(System.IO.Path.GetFileName(Path)) is IStorageItem)
                                {
                                    return true;
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogTracer.Log(ex, "CheckExist threw an exception");
                }
            }

            return false;
        }

        public static async Task<IReadOnlyList<FileSystemStorageItemBase>> OpenInBatchAsync(IEnumerable<string> PathArray)
        {
            ConcurrentBag<FileSystemStorageItemBase> Result = new ConcurrentBag<FileSystemStorageItemBase>();

            using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableControllerAsync())
            {
                await Task.Factory.StartNew(() =>
                {
                    Parallel.ForEach(PathArray, (Path) =>
                    {
                        try
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
                                        Result.Add(new FileSystemStorageFolder(StorageFolder.GetFolderFromPathAsync(Path).AsTask().Result));
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
                                catch (Exception ex) when (ex is not (FileNotFoundException or DirectoryNotFoundException))
                                {
                                    using (SafeFileHandle Handle = Exclusive.Controller.GetNativeHandleAsync(Path, AccessMode.ReadWrite, OptimizeOption.None).Result)
                                    {
                                        if (Handle.IsInvalid)
                                        {
                                            LogTracer.Log($"Could not get native handle and failed to get the storage item, path: \"{Path}\"");
                                        }
                                        else
                                        {
                                            FileSystemStorageItemBase Item = Win32_Native_API.GetStorageItemFromHandle(Path, Handle.DangerousGetHandle());

                                            if (Item != null)
                                            {
                                                Result.Add(Item);
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            LogTracer.Log(ex, $"{nameof(OpenInBatchAsync)} failed and could not get the storage item, path:\"{Path}\"");
                        }
                    });
                }, TaskCreationOptions.LongRunning);
            }

            return Result.ToList();
        }

        public static async Task<FileSystemStorageItemBase> OpenAsync(string Path)
        {
            if (!string.IsNullOrEmpty(Path))
            {
                try
                {
                    if (Path.StartsWith(@"\\?\", StringComparison.OrdinalIgnoreCase))
                    {
                        using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableControllerAsync())
                        {
                            if (await Exclusive.Controller.GetMTPItemDataAsync(Path) is MTP_File_Data Data)
                            {
                                return new MTPStorageFolder(Data);
                            }
                        }
                    }
                    else
                    {
                        try
                        {
                            return Win32_Native_API.GetStorageItem(Path);
                        }
                        catch (LocationNotAvailableException)
                        {
                            try
                            {
                                string DirectoryPath = System.IO.Path.GetDirectoryName(Path);

                                if (string.IsNullOrEmpty(DirectoryPath))
                                {
                                    return new FileSystemStorageFolder(await StorageFolder.GetFolderFromPathAsync(Path));
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
                                                break;
                                            }
                                    }
                                }
                            }
                            catch (Exception ex) when (ex is not (FileNotFoundException or DirectoryNotFoundException))
                            {
                                using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableControllerAsync())
                                using (SafeFileHandle Handle = await Exclusive.Controller.GetNativeHandleAsync(Path, AccessMode.ReadWrite, OptimizeOption.None))
                                {
                                    if (Handle.IsInvalid)
                                    {
                                        LogTracer.Log($"Could not get native handle and failed to get the storage item, path: \"{Path}\"");
                                    }
                                    else
                                    {
                                        return Win32_Native_API.GetStorageItemFromHandle(Path, Handle.DangerousGetHandle());
                                    }
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogTracer.Log(ex, $"{nameof(OpenAsync)} failed and could not get the storage item, path:\"{Path}\"");
                }
            }

            return null;
        }

        public static async Task<FileSystemStorageItemBase> CreateNewAsync(string Path, StorageItemTypes ItemTypes, CreateOption Option)
        {
            try
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
                                                return new FileSystemStorageFile(await Folder.CreateFileAsync(System.IO.Path.GetFileName(Path), CreationCollisionOption.GenerateUniqueName));
                                            }
                                        case CreateOption.OpenIfExist:
                                            {
                                                return new FileSystemStorageFile(await Folder.CreateFileAsync(System.IO.Path.GetFileName(Path), CreationCollisionOption.OpenIfExists));
                                            }
                                        case CreateOption.ReplaceExisting:
                                            {
                                                return new FileSystemStorageFile(await Folder.CreateFileAsync(System.IO.Path.GetFileName(Path), CreationCollisionOption.ReplaceExisting));
                                            }
                                        default:
                                            {
                                                break;
                                            }
                                    }
                                }
                            }
                            catch (Exception)
                            {
                                using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableControllerAsync())
                                {
                                    string NewItemPath = await Exclusive.Controller.CreateNewAsync(CreateType.File, Path);

                                    if (string.IsNullOrEmpty(NewItemPath))
                                    {
                                        LogTracer.Log($"Could not use full trust process to create the storage item, path: \"{Path}\"");
                                    }
                                    else
                                    {
                                        return await OpenAsync(NewItemPath);
                                    }
                                }
                            }

                            break;
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
                                                break;
                                            }
                                    }
                                }
                            }
                            catch (Exception)
                            {
                                using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableControllerAsync())
                                {
                                    string NewItemPath = await Exclusive.Controller.CreateNewAsync(CreateType.Folder, Path);

                                    if (string.IsNullOrEmpty(NewItemPath))
                                    {
                                        LogTracer.Log($"Could not use full trust process to create the storage item, path: \"{Path}\"");
                                    }
                                    else
                                    {
                                        return await OpenAsync(NewItemPath);
                                    }
                                }
                            }

                            break;
                        }
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"{nameof(CreateNewAsync)} failed and could not create the storage item, path:\"{Path}\"");
            }

            return null;
        }

        protected FileSystemStorageItemBase(string Path, SafeFileHandle Handle, bool LeaveOpen) : this(Win32_Native_API.GetStorageItemRawDataFromHandle(Path, Handle.DangerousGetHandle()))
        {
            if (!LeaveOpen)
            {
                Handle.Dispose();
            }
        }

        protected FileSystemStorageItemBase(Win32_File_Data Data) : this(Data.Path)
        {
            if (Data != null && Data.IsDataValid)
            {
                Size = Data.Size;
                IsReadOnly = Data.IsReadOnly;
                IsSystemItem = Data.IsSystemItem;
                ModifiedTime = Data.ModifiedTime;
                CreationTime = Data.CreationTime;
            }
        }

        protected FileSystemStorageItemBase(MTP_File_Data Data) : this(Data.Path)
        {
            if (Data != null)
            {
                Size = Data.Size;
                IsReadOnly = Data.IsReadOnly;
                IsSystemItem = Data.IsSystemItem;
                ModifiedTime = Data.ModifiedTime;
                CreationTime = Data.CreationTime;
            }
        }

        protected FileSystemStorageItemBase(string Path)
        {
            this.Path = Path;
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
                Interlocked.CompareExchange(ref IsThubmnalModeChanged, 1, 0);
            }
        }

        public async Task LoadAsync()
        {
            if (Interlocked.CompareExchange(ref IsContentLoaded, 1, 0) > 0)
            {
                if (Interlocked.CompareExchange(ref IsThubmnalModeChanged, 0, 1) > 0)
                {
                    if (ShouldGenerateThumbnail)
                    {
                        try
                        {
                            Thumbnail = await GetThumbnailCoreAsync(ThumbnailMode);
                        }
                        catch (Exception ex)
                        {
                            LogTracer.Log(ex, $"An exception was threw in {nameof(LoadAsync)}, StorageType: {GetType().FullName}, Path: {Path}");
                        }
                        finally
                        {
                            OnPropertyChanged(nameof(Thumbnail));
                        }
                    }
                }
            }
            else
            {
                async Task LocalLoadAsync()
                {
                    try
                    {
                        using (RefSharedRegion<FullTrustProcessController.ExclusiveUsage> SharedRegion = await FullTrustProcessController.GetProcessSharedRegionAsync())
                        using (DisposableNotification Disposable = SetProcessRefShareRegion(SharedRegion))
                        {
                            await Task.WhenAll(LoadCoreAsync(false), GetStorageItemAsync(), GetThumbnailOverlayAsync());

                            List<Task> ParallelLoadTasks = new List<Task>(2);

                            if (ShouldGenerateThumbnail)
                            {
                                ParallelLoadTasks.Add(GetThumbnailAsync(ThumbnailMode));
                            }

                            if (SpecialPath.IsPathIncluded(Path, SpecialPath.SpecialPathEnum.OneDrive)
                                || SpecialPath.IsPathIncluded(Path, SpecialPath.SpecialPathEnum.Dropbox))
                            {
                                ParallelLoadTasks.Add(GetSyncStatusAsync());
                            }

                            await Task.WhenAll(ParallelLoadTasks);
                        }
                    }
                    catch (Exception ex)
                    {
                        LogTracer.Log(ex, $"An exception was threw in {nameof(LoadAsync)}, StorageType: {GetType().FullName}, Path: {Path}");
                    }
                    finally
                    {
                        OnPropertyChanged(nameof(Name));
                        OnPropertyChanged(nameof(DisplayName));
                        OnPropertyChanged(nameof(SizeDescription));
                        OnPropertyChanged(nameof(DisplayType));
                        OnPropertyChanged(nameof(ModifiedTimeDescription));
                        OnPropertyChanged(nameof(Thumbnail));
                        OnPropertyChanged(nameof(ThumbnailOverlay));
                        OnPropertyChanged(nameof(SyncStatus));
                    }
                };

                if (CoreApplication.MainView.CoreWindow.Dispatcher.HasThreadAccess)
                {
                    await LocalLoadAsync();
                }
                else
                {
                    await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, async () => await LocalLoadAsync());
                }
            }
        }

        public DisposableNotification SetProcessRefShareRegion(RefSharedRegion<FullTrustProcessController.ExclusiveUsage> SharedRef)
        {
            if (Interlocked.Exchange(ref ControllerSharedRef, SharedRef) is RefSharedRegion<FullTrustProcessController.ExclusiveUsage> PreviousRef)
            {
                PreviousRef.Dispose();
            }

            return new DisposableNotification(() =>
            {
                if (Interlocked.Exchange(ref ControllerSharedRef, null) is RefSharedRegion<FullTrustProcessController.ExclusiveUsage> PreviousRef)
                {
                    PreviousRef.Dispose();
                }
            });
        }

        protected RefSharedRegion<FullTrustProcessController.ExclusiveUsage> GetProcessSharedRegion()
        {
            return ControllerSharedRef?.CreateNew();
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
        }

        public virtual async Task<SafeFileHandle> GetNativeHandleAsync(AccessMode Mode, OptimizeOption Option)
        {
            async Task<SafeFileHandle> GetNativeHandleCoreAsync()
            {
                using (RefSharedRegion<FullTrustProcessController.ExclusiveUsage> ControllerRef = GetProcessSharedRegion())
                {
                    if (ControllerRef != null)
                    {
                        return await ControllerRef.Value.Controller.GetNativeHandleAsync(Path, Mode, Option);
                    }
                    else
                    {
                        using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableControllerAsync())
                        {
                            return await Exclusive.Controller.GetNativeHandleAsync(Path, Mode, Option);
                        }
                    }
                }
            }

            if (await GetStorageItemAsync() is IStorageItem Item)
            {
                SafeFileHandle Handle = Item.GetSafeFileHandle(Mode, Option);

                if (Handle.IsInvalid)
                {
                    return await GetNativeHandleCoreAsync();
                }
                else
                {
                    return Handle;
                }

            }
            else
            {
                return await GetNativeHandleCoreAsync();
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

                return null;
            }

            using (RefSharedRegion<FullTrustProcessController.ExclusiveUsage> ControllerRef = GetProcessSharedRegion())
            {
                if (ControllerRef != null)
                {
                    return ThumbnailOverlay = await GetThumbnailOverlayCoreAsync(ControllerRef.Value);
                }
                else
                {
                    using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableControllerAsync())
                    {
                        return ThumbnailOverlay = await GetThumbnailOverlayCoreAsync(Exclusive);
                    }
                }
            }
        }

        protected abstract Task LoadCoreAsync(bool ForceUpdate);

        public abstract Task<IStorageItem> GetStorageItemAsync();

        public async Task<BitmapImage> GetThumbnailAsync(ThumbnailMode Mode)
        {
            if (Thumbnail == null || !string.IsNullOrEmpty(Thumbnail.UriSource?.AbsoluteUri))
            {
                return Thumbnail = await GetThumbnailCoreAsync(Mode);
            }
            else
            {
                return Thumbnail;
            }
        }

        protected virtual async Task<BitmapImage> GetThumbnailCoreAsync(ThumbnailMode Mode)
        {
            async Task<BitmapImage> InternalGetThumbnailAsync(FullTrustProcessController.ExclusiveUsage Exclusive)
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

                return null;
            }

            try
            {
                if (await GetStorageItemAsync() is IStorageItem Item)
                {
                    if (await Item.GetThumbnailBitmapAsync(Mode) is BitmapImage LocalThumbnail)
                    {
                        return LocalThumbnail;
                    }
                }

                using (RefSharedRegion<FullTrustProcessController.ExclusiveUsage> ControllerRef = GetProcessSharedRegion())
                {
                    if (ControllerRef != null)
                    {
                        return await InternalGetThumbnailAsync(ControllerRef.Value);
                    }
                    else
                    {
                        using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableControllerAsync())
                        {
                            return await InternalGetThumbnailAsync(Exclusive);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"Could not get thumbnail of path: \"{Path}\"");
            }

            return null;
        }


        public Task<IRandomAccessStream> GetThumbnailRawStreamAsync(ThumbnailMode Mode)
        {
            return GetThumbnailRawStreamCoreAsync(Mode);
        }

        protected virtual async Task<IRandomAccessStream> GetThumbnailRawStreamCoreAsync(ThumbnailMode Mode)
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

                using (RefSharedRegion<FullTrustProcessController.ExclusiveUsage> ControllerRef = GetProcessSharedRegion())
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
                using (RefSharedRegion<FullTrustProcessController.ExclusiveUsage> ControllerRef = GetProcessSharedRegion())
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
                        Result.AddRange(await GetPropertiesTask(MissingKeys));
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
                if (await CheckExistsAsync(Path))
                {
                    await LoadCoreAsync(true);

                    OnPropertyChanged(nameof(SizeDescription));
                    OnPropertyChanged(nameof(Name));
                    OnPropertyChanged(nameof(DisplayName));
                    OnPropertyChanged(nameof(ModifiedTimeDescription));
                    OnPropertyChanged(nameof(Thumbnail));
                    OnPropertyChanged(nameof(DisplayType));
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

        public static class SpecialPath
        {
            private static IReadOnlyList<string> OneDrivePathCollection { get; } = new List<string>(3)
            {
                Environment.GetEnvironmentVariable("OneDriveConsumer"),
                Environment.GetEnvironmentVariable("OneDriveCommercial"),
                Environment.GetEnvironmentVariable("OneDrive")
            };

            private static IReadOnlyList<string> DropboxPathCollection { get; set; } = new List<string>(0);

            public enum SpecialPathEnum
            {
                OneDrive,
                Dropbox
            }

            public static async Task InitializeAsync()
            {
                static async Task<IReadOnlyList<string>> LocalLoadJsonAsync(string JsonPath)
                {
                    List<string> DropboxPathResult = new List<string>(2);

                    try
                    {
                        if (await OpenAsync(JsonPath) is FileSystemStorageFile JsonFile)
                        {
                            using (FileStream Stream = await JsonFile.GetStreamFromFileAsync(AccessMode.Read, OptimizeOption.Sequential))
                            using (StreamReader Reader = new StreamReader(Stream, true))
                            {
                                var JsonObject = JsonSerializer.Deserialize<IDictionary<string, IDictionary<string, object>>>(Reader.ReadToEnd());

                                if (JsonObject.TryGetValue("personal", out IDictionary<string, object> PersonalSubDic))
                                {
                                    DropboxPathResult.Add(Convert.ToString(PersonalSubDic["path"]));
                                }

                                if (JsonObject.TryGetValue("business", out IDictionary<string, object> BusinessSubDic))
                                {
                                    DropboxPathResult.Add(Convert.ToString(BusinessSubDic["path"]));
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        LogTracer.Log(ex, "Could not get the configuration from Dropbox info.json");
                    }

                    return DropboxPathResult;
                }

                string JsonPath1 = await EnvironmentVariables.ReplaceVariableWithActualPathAsync(@"%APPDATA%\Dropbox\info.json");
                string JsonPath2 = await EnvironmentVariables.ReplaceVariableWithActualPathAsync(@"%LOCALAPPDATA%\Dropbox\info.json");

                if (await CheckExistsAsync(JsonPath1))
                {
                    DropboxPathCollection = await LocalLoadJsonAsync(JsonPath1);
                }
                else if (await CheckExistsAsync(JsonPath2))
                {
                    DropboxPathCollection = await LocalLoadJsonAsync(JsonPath2);
                }

                if (DropboxPathCollection.Count == 0)
                {
                    DropboxPathCollection = new List<string>(1)
                    {
                        System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),"Dropbox")
                    };
                }
            }

            public static bool IsPathIncluded(string Path, SpecialPathEnum Enum)
            {
                switch (Enum)
                {
                    case SpecialPathEnum.OneDrive:
                        {
                            return OneDrivePathCollection.Where((Path) => !string.IsNullOrEmpty(Path)).Any((OneDrivePath) => Path.StartsWith(OneDrivePath, StringComparison.OrdinalIgnoreCase) && !Path.Equals(OneDrivePath, StringComparison.OrdinalIgnoreCase));
                        }
                    case SpecialPathEnum.Dropbox:
                        {
                            return DropboxPathCollection.Where((Path) => !string.IsNullOrEmpty(Path)).Any((DropboxPath) => Path.StartsWith(DropboxPath, StringComparison.OrdinalIgnoreCase) && !Path.Equals(DropboxPath, StringComparison.OrdinalIgnoreCase));
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
