using RX_Explorer.Interface;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Windows.ApplicationModel.Core;
using Windows.Storage;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using ColorHelper = Microsoft.Toolkit.Uwp.Helpers.ColorHelper;

namespace RX_Explorer.Class
{
    /// <summary>
    /// 提供对设备中的存储对象的描述
    /// </summary>
    public abstract class FileSystemStorageItemBase : IStorageItemPropertiesBase, INotifyPropertyChanged, IStorageItemOperation, IEquatable<FileSystemStorageItemBase>
    {
        public string Path { get; protected set; }

        public virtual string Size { get; }

        public virtual string Name
        {
            get
            {
                return System.IO.Path.GetFileName(Path);
            }
        }

        public virtual string DisplayName
        {
            get
            {
                return Name;
            }
        }

        public virtual string Type
        {
            get
            {
                return System.IO.Path.GetExtension(Path).ToUpper();
            }
        }

        public virtual string DisplayType
        {
            get
            {
                return Type;
            }
        }

        public SolidColorBrush BackgroundColor
        {
            get
            {
                return backgroundColor ??= new SolidColorBrush(Colors.Transparent);
            }
            private set
            {
                backgroundColor = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(BackgroundColor)));
            }
        }

        private SolidColorBrush backgroundColor;

        public void SetColorAsSpecific(Color Color)
        {
            BackgroundColor = new SolidColorBrush(Color);
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(BackgroundColor)));
        }

        public void SetColorAsNormal()
        {
            BackgroundColor = new SolidColorBrush(Colors.Transparent);
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(BackgroundColor)));
        }

        public double ThumbnailOpacity { get; protected set; } = 1d;

        public ulong SizeRaw { get; protected set; }

        public DateTimeOffset CreationTimeRaw { get; protected set; }

        public DateTimeOffset ModifiedTimeRaw { get; protected set; }

        public virtual string ModifiedTime
        {
            get
            {
                if (ModifiedTimeRaw == DateTimeOffset.MaxValue.ToLocalTime() || ModifiedTimeRaw == DateTimeOffset.MinValue.ToLocalTime())
                {
                    return Globalization.GetString("UnknownText");
                }
                else
                {
                    return ModifiedTimeRaw.ToString("G");
                }
            }
        }

        public virtual string CreationTime
        {
            get
            {
                if (CreationTimeRaw == DateTimeOffset.MaxValue.ToLocalTime())
                {
                    return Globalization.GetString("UnknownText");
                }
                else
                {
                    return CreationTimeRaw.ToString("G");
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public virtual BitmapImage Thumbnail { get; protected set; }

        public virtual BitmapImage ThumbnailOverlay { get; protected set; }

        public virtual bool IsReadOnly { get; protected set; }

        public virtual bool IsSystemItem { get; protected set; }

        public SyncStatus SyncStatus { get; protected set; } = SyncStatus.Unknown;

        protected IStorageItem StorageItem { get; set; }

        protected static readonly Uri Const_Folder_Image_Uri = new Uri("ms-appx:///Assets/FolderIcon.png");

        protected static readonly Uri Const_File_White_Image_Uri = new Uri("ms-appx:///Assets/Page_Solid_White.png");

        protected static readonly Uri Const_File_Black_Image_Uri = new Uri("ms-appx:///Assets/Page_Solid_Black.png");

        public static async Task<bool> CheckExistAsync(string Path)
        {
            if (System.IO.Path.IsPathRooted(Path))
            {
                if (WIN_Native_API.CheckLocationAvailability(System.IO.Path.GetDirectoryName(Path)))
                {
                    return WIN_Native_API.CheckExist(Path);
                }
                else
                {
                    try
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
                    catch (Exception ex)
                    {
                        LogTracer.Log(ex, "CheckExist threw an exception");
                        return false;
                    }
                }
            }
            else
            {
                return false;
            }
        }

        public static async Task<FileSystemStorageItemBase> CreateFromStorageItemAsync<T>(T Item) where T : IStorageItem
        {
            switch (Item)
            {
                case StorageFile File:
                    {
                        foreach (ConstructorInfo Info in typeof(FileSystemStorageFile).GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance))
                        {
                            ParameterInfo[] Parameters = Info.GetParameters();

                            if (Parameters[0].ParameterType == typeof(StorageFile))
                            {
                                return (FileSystemStorageFile)Info.Invoke(new object[] { File, await File.GetModifiedTimeAsync(), await File.GetSizeRawDataAsync() });
                            }
                        }

                        return null;
                    }
                case StorageFolder Folder:
                    {
                        foreach (ConstructorInfo Info in typeof(FileSystemStorageFolder).GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance))
                        {
                            ParameterInfo[] Parameters = Info.GetParameters();

                            if (Parameters[0].ParameterType == typeof(StorageFolder))
                            {
                                return (FileSystemStorageFolder)Info.Invoke(new object[] { Folder, await Folder.GetModifiedTimeAsync() });
                            }
                        }

                        return null;
                    }
                default:
                    {
                        return null;
                    }
            }
        }

        public static async Task<FileSystemStorageItemBase> OpenAsync(string Path)
        {
            if (WIN_Native_API.CheckLocationAvailability(System.IO.Path.GetDirectoryName(Path)))
            {
                return WIN_Native_API.GetStorageItem(Path);
            }
            else
            {
                LogTracer.Log($"Native API could not found the path: \"{Path}\", fall back to UWP storage API");

                try
                {
                    string DirectoryPath = System.IO.Path.GetDirectoryName(Path);

                    if (string.IsNullOrEmpty(DirectoryPath))
                    {
                        StorageFolder Folder = await StorageFolder.GetFolderFromPathAsync(Path);
                        return await CreateFromStorageItemAsync(Folder);
                    }
                    else
                    {
                        StorageFolder ParentFolder = await StorageFolder.GetFolderFromPathAsync(DirectoryPath);

                        switch (await ParentFolder.TryGetItemAsync(System.IO.Path.GetFileName(Path)))
                        {
                            case StorageFolder Folder:
                                {
                                    return await CreateFromStorageItemAsync(Folder);
                                }
                            case StorageFile File:
                                {
                                    return await CreateFromStorageItemAsync(File);
                                }
                            default:
                                {
                                    LogTracer.Log($"UWP storage API could not found the path: \"{Path}\"");
                                    return null;
                                }
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogTracer.Log(ex, $"UWP storage API could not found the path: \"{Path}\"");
                    return null;
                }
            }
        }

        public static async Task<FileSystemStorageItemBase> CreateAsync(string Path, StorageItemTypes ItemTypes, CreateOption Option)
        {
            switch (ItemTypes)
            {
                case StorageItemTypes.File:
                    {
                        if (WIN_Native_API.CreateFileFromPath(Path, Option, out string NewPath))
                        {
                            return await OpenAsync(NewPath);
                        }
                        else
                        {
                            LogTracer.Log($"Native API could not create file: \"{Path}\", fall back to UWP storage API");

                            try
                            {
                                StorageFolder Folder = await StorageFolder.GetFolderFromPathAsync(System.IO.Path.GetDirectoryName(Path));

                                switch (Option)
                                {
                                    case CreateOption.GenerateUniqueName:
                                        {
                                            StorageFile NewFile = await Folder.CreateFileAsync(System.IO.Path.GetFileName(Path), CreationCollisionOption.GenerateUniqueName);
                                            return await CreateFromStorageItemAsync(NewFile);
                                        }
                                    case CreateOption.OpenIfExist:
                                        {
                                            StorageFile NewFile = await Folder.CreateFileAsync(System.IO.Path.GetFileName(Path), CreationCollisionOption.OpenIfExists);
                                            return await CreateFromStorageItemAsync(NewFile);
                                        }
                                    case CreateOption.ReplaceExisting:
                                        {
                                            StorageFile NewFile = await Folder.CreateFileAsync(System.IO.Path.GetFileName(Path), CreationCollisionOption.ReplaceExisting);
                                            return await CreateFromStorageItemAsync(NewFile);
                                        }
                                    default:
                                        {
                                            return null;
                                        }
                                }
                            }
                            catch
                            {
                                LogTracer.Log($"UWP storage API could not create file: \"{Path}\"");
                                return null;
                            }
                        }
                    }
                case StorageItemTypes.Folder:
                    {
                        if (WIN_Native_API.CreateDirectoryFromPath(Path, Option, out string NewPath))
                        {
                            return await OpenAsync(NewPath);
                        }
                        else
                        {
                            LogTracer.Log($"Native API could not create file: \"{Path}\", fall back to UWP storage API");

                            try
                            {
                                StorageFolder Folder = await StorageFolder.GetFolderFromPathAsync(System.IO.Path.GetDirectoryName(Path));

                                switch (Option)
                                {
                                    case CreateOption.GenerateUniqueName:
                                        {
                                            StorageFolder NewFolder = await Folder.CreateFolderAsync(System.IO.Path.GetFileName(Path), CreationCollisionOption.GenerateUniqueName);
                                            return await CreateFromStorageItemAsync(NewFolder);
                                        }
                                    case CreateOption.OpenIfExist:
                                        {
                                            StorageFolder NewFolder = await Folder.CreateFolderAsync(System.IO.Path.GetFileName(Path), CreationCollisionOption.OpenIfExists);
                                            return await CreateFromStorageItemAsync(NewFolder);
                                        }
                                    case CreateOption.ReplaceExisting:
                                        {
                                            StorageFolder NewFolder = await Folder.CreateFolderAsync(System.IO.Path.GetFileName(Path), CreationCollisionOption.ReplaceExisting);
                                            return await CreateFromStorageItemAsync(NewFolder);
                                        }
                                    default:
                                        {
                                            return null;
                                        }
                                }
                            }
                            catch
                            {
                                LogTracer.Log($"UWP storage API could not create folder: \"{Path}\"");
                                return null;
                            }
                        }
                    }
                default:
                    {
                        return null;
                    }
            }
        }

        protected FileSystemStorageItemBase(string Path)
        {
            this.Path = Path;
        }

        protected FileSystemStorageItemBase(string Path, WIN_Native_API.WIN32_FIND_DATA Data)
        {
            this.Path = Path;

            if (Data != default)
            {
                IsReadOnly = ((System.IO.FileAttributes)Data.dwFileAttributes).HasFlag(System.IO.FileAttributes.ReadOnly);
                IsSystemItem = IsReadOnly = ((System.IO.FileAttributes)Data.dwFileAttributes).HasFlag(System.IO.FileAttributes.System);

                SizeRaw = ((ulong)Data.nFileSizeHigh << 32) + Data.nFileSizeLow;

                WIN_Native_API.FileTimeToSystemTime(ref Data.ftLastWriteTime, out WIN_Native_API.SYSTEMTIME ModTime);
                ModifiedTimeRaw = new DateTime(ModTime.Year, ModTime.Month, ModTime.Day, ModTime.Hour, ModTime.Minute, ModTime.Second, ModTime.Milliseconds, DateTimeKind.Utc).ToLocalTime();

                WIN_Native_API.FileTimeToSystemTime(ref Data.ftCreationTime, out WIN_Native_API.SYSTEMTIME CreTime);
                CreationTimeRaw = new DateTime(CreTime.Year, CreTime.Month, CreTime.Day, CreTime.Hour, CreTime.Minute, CreTime.Second, CreTime.Milliseconds, DateTimeKind.Utc).ToLocalTime();
            }
        }

        protected void OnPropertyChanged(string Name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(Name));
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

        public async Task LoadMorePropertiesAsync()
        {
            if (!CheckIfPropertiesLoaded())
            {
                async void LocalLoadFunction()
                {
                    try
                    {
                        if (LoadMorePropertiesWithFullTrustProcess())
                        {
                            using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableController())
                            {
                                if ((this is FileSystemStorageFile && SettingControl.ContentLoadMode == LoadMode.OnlyFile) || SettingControl.ContentLoadMode == LoadMode.FileAndFolder)
                                {
                                    await LoadMorePropertiesCoreAsync(Exclusive.Controller, false);
                                }

                                if (CheckIfNeedLoadThumbnailOverlay())
                                {
                                    await LoadThumbnailOverlayAsync(Exclusive.Controller);
                                }
                            }
                        }
                        else
                        {
                            if ((this is FileSystemStorageFile && SettingControl.ContentLoadMode == LoadMode.OnlyFile) || SettingControl.ContentLoadMode == LoadMode.FileAndFolder)
                            {
                                await LoadMorePropertiesCoreAsync(false);
                            }

                            if (CheckIfNeedLoadThumbnailOverlay())
                            {
                                using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableController())
                                {
                                    await LoadThumbnailOverlayAsync(Exclusive.Controller);
                                }
                            }
                        }

                        OnPropertyChanged(nameof(Name));
                        OnPropertyChanged(nameof(Size));
                        OnPropertyChanged(nameof(DisplayType));
                        OnPropertyChanged(nameof(ModifiedTime));
                        OnPropertyChanged(nameof(Thumbnail));
                        OnPropertyChanged(nameof(ThumbnailOverlay));
                        
                        LoadForegroundConfiguration();
                        
                        await LoadSyncStatusAsync();
                    }
                    catch (Exception ex)
                    {
                        LogTracer.Log(ex, $"An exception was threw in {nameof(LoadMorePropertiesAsync)}, StorageType: {GetType().FullName}, Path: {Path}");
                    }
                };

                if (CoreApplication.MainView.CoreWindow.Dispatcher.HasThreadAccess)
                {
                    LocalLoadFunction();
                }
                else
                {
                    await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, LocalLoadFunction);
                }
            }
        }

        private async Task LoadSyncStatusAsync()
        {
            if (SpecialPath.IsPathIncluded(Path, SpecialPath.SpecialPathEnum.OneDrive))
            {
                switch (await GetStorageItemAsync())
                {
                    case StorageFile File:
                        {
                            IDictionary<string, object> Properties = await File.Properties.RetrievePropertiesAsync(new string[] { "System.FilePlaceholderStatus", "System.FileOfflineAvailabilityStatus" });

                            if (!Properties.TryGetValue("System.FilePlaceholderStatus", out object StatusIndex))
                            {
                                if (!Properties.TryGetValue("System.FileOfflineAvailabilityStatus", out StatusIndex))
                                {
                                    SyncStatus = SyncStatus.Unknown;
                                    break;
                                }
                            }

                            switch (Convert.ToUInt32(StatusIndex))
                            {
                                case 0:
                                case 1:
                                case 8:
                                    {
                                        SyncStatus = SyncStatus.AvailableOnline;
                                        break;
                                    }
                                case 2:
                                case 3:
                                case 14:
                                case 15:
                                    {
                                        SyncStatus = SyncStatus.AvailableOffline;
                                        break;
                                    }
                                case 9:
                                    {
                                        SyncStatus = SyncStatus.Sync;
                                        break;
                                    }
                                case 4:
                                    {
                                        SyncStatus = SyncStatus.Excluded;
                                        break;
                                    }
                                default:
                                    {
                                        SyncStatus = SyncStatus.Unknown;
                                        break;
                                    }
                            }

                            break;
                        }
                    case StorageFolder Folder:
                        {
                            IDictionary<string, object> Properties = await Folder.Properties.RetrievePropertiesAsync(new string[] { "System.FilePlaceholderStatus", "System.FileOfflineAvailabilityStatus" });


                            if (!Properties.TryGetValue("System.FileOfflineAvailabilityStatus", out object StatusIndex))
                            {
                                if (!Properties.TryGetValue("System.FilePlaceholderStatus", out StatusIndex))
                                {
                                    SyncStatus = SyncStatus.Unknown;
                                    break;
                                }
                            }

                            switch (Convert.ToUInt32(StatusIndex))
                            {
                                case 0:
                                case 1:
                                case 8:
                                    {
                                        SyncStatus = SyncStatus.AvailableOnline;
                                        break;
                                    }
                                case 2:
                                case 3:
                                case 14:
                                case 15:
                                    {
                                        SyncStatus = SyncStatus.AvailableOffline;
                                        break;
                                    }
                                case 9:
                                    {
                                        SyncStatus = SyncStatus.Sync;
                                        break;
                                    }
                                case 4:
                                    {
                                        SyncStatus = SyncStatus.Excluded;
                                        break;
                                    }
                                default:
                                    {
                                        SyncStatus = SyncStatus.Unknown;
                                        break;
                                    }
                            }

                            break;
                        }
                    default:
                        {
                            SyncStatus = SyncStatus.Unknown;
                            break;
                        }
                }
            }
            else
            {
                SyncStatus = SyncStatus.Unknown;
            }

            OnPropertyChanged(nameof(SyncStatus));
        }

        private async Task LoadThumbnailOverlayAsync(FullTrustProcessController Controller)
        {
            byte[] ThumbnailOverlayByteArray = await Controller.GetThumbnailOverlayAsync(Path);

            if (ThumbnailOverlayByteArray.Length > 0)
            {
                using (MemoryStream Ms = new MemoryStream(ThumbnailOverlayByteArray))
                {
                    ThumbnailOverlay = new BitmapImage();
                    await ThumbnailOverlay.SetSourceAsync(Ms.AsRandomAccessStream());
                }
            }
        }

        private void LoadForegroundConfiguration()
        {
            string ColorString = SQLite.Current.GetFileColor(Path);

            if (!string.IsNullOrEmpty(ColorString))
            {
                SetColorAsSpecific(ColorHelper.ToColor(ColorString));
            }
        }

        protected abstract bool CheckIfNeedLoadThumbnailOverlay();

        protected abstract bool CheckIfPropertiesLoaded();

        //Use this overload if subclass has no need for FullTrustProcessController.
        //Make sure override LoadMorePropertiesWithFullTrustProcess() and reture false.
        protected abstract Task LoadMorePropertiesCoreAsync(bool ForceUpdate);

        //Use this overload to share common FullTrustProcessController. FileSystemStorageItemBase will create a common FullTrustProcessController for you.
        //Subclass who want to use FullTrustProcessController should override this method.
        //Make sure override LoadMorePropertiesWithFullTrustProcess() and reture true.
        protected abstract Task LoadMorePropertiesCoreAsync(FullTrustProcessController Controller, bool ForceUpdate);

        protected abstract bool LoadMorePropertiesWithFullTrustProcess();

        public abstract Task<IStorageItem> GetStorageItemAsync();

        public async Task RefreshAsync()
        {
            try
            {
                if (await CheckExistAsync(Path))
                {
                    if (LoadMorePropertiesWithFullTrustProcess())
                    {
                        using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableController())
                        {
                            await LoadMorePropertiesCoreAsync(Exclusive.Controller, true);
                        }
                    }
                    else
                    {
                        await LoadMorePropertiesCoreAsync(true);
                    }

                    OnPropertyChanged(nameof(Size));
                    OnPropertyChanged(nameof(Name));
                    OnPropertyChanged(nameof(ModifiedTime));
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

        public virtual async Task MoveAsync(string DirectoryPath, ProgressChangedEventHandler ProgressHandler = null)
        {
            using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableController())
            {
                await Exclusive.Controller.MoveAsync(Path, DirectoryPath, false, ProgressHandler);
            }
        }

        public virtual Task MoveAsync(FileSystemStorageFolder Directory, ProgressChangedEventHandler ProgressHandler = null)
        {
            return MoveAsync(Directory.Path, ProgressHandler);
        }

        public virtual async Task CopyAsync(string DirectoryPath, ProgressChangedEventHandler ProgressHandler = null)
        {
            using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableController())
            {
                await Exclusive.Controller.CopyAsync(Path, DirectoryPath, false, ProgressHandler);
            }
        }

        public virtual Task CopyAsync(FileSystemStorageFolder Directory, ProgressChangedEventHandler ProgressHandler = null)
        {
            return CopyAsync(Directory.Path, ProgressHandler);
        }

        public async virtual Task<string> RenameAsync(string DesireName)
        {
            using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableController())
            {
                string NewName = await Exclusive.Controller.RenameAsync(Path, DesireName);
                Path = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Path), NewName);
                return NewName;
            }
        }

        public virtual async Task DeleteAsync(bool PermanentDelete, ProgressChangedEventHandler ProgressHandler = null)
        {
            using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableController())
            {
                await Exclusive.Controller.DeleteAsync(Path, PermanentDelete, ProgressHandler);
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

        public sealed class SpecialPath
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
