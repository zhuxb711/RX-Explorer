using RX_Explorer.Interface;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using Windows.ApplicationModel.Core;
using Windows.Storage;
using Windows.UI.Core;
using Windows.UI.Xaml.Media.Imaging;

namespace RX_Explorer.Class
{
    /// <summary>
    /// 提供对设备中的存储对象的描述
    /// </summary>
    public abstract class FileSystemStorageItemBase : IStorageItemPropertyBase, INotifyPropertyChanged, IStorageItemOperation, IEquatable<FileSystemStorageItemBase>
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

        protected static readonly Uri Const_Folder_Image_Uri = new Uri("ms-appx:///Assets/FolderIcon.png");

        protected static readonly Uri Const_File_White_Image_Uri = new Uri("ms-appx:///Assets/Page_Solid_White.png");

        protected static readonly Uri Const_File_Black_Image_Uri = new Uri("ms-appx:///Assets/Page_Solid_Black.png");

        public static async Task MoveAsync(string SourcePath, string DirectoryPath, ProgressChangedEventHandler ProgressHandler = null, bool IsUndoOperation = false)
        {
            using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableController())
            {
                await Exclusive.Controller.MoveAsync(SourcePath, DirectoryPath, ProgressHandler, IsUndoOperation).ConfigureAwait(false);
            }
        }

        public static async Task MoveAsync(IEnumerable<string> SourcePathList, string DirectoryPath, ProgressChangedEventHandler ProgressHandler = null, bool IsUndoOperation = false)
        {
            using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableController())
            {
                await Exclusive.Controller.MoveAsync(SourcePathList, DirectoryPath, ProgressHandler, IsUndoOperation).ConfigureAwait(false);
            }
        }

        public static async Task CopyAsync(string SourcePath, string DirectoryPath, ProgressChangedEventHandler ProgressHandler = null, bool IsUndoOperation = false)
        {
            using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableController())
            {
                await Exclusive.Controller.CopyAsync(SourcePath, DirectoryPath, ProgressHandler, IsUndoOperation).ConfigureAwait(false);
            }
        }

        public static async Task CopyAsync(IEnumerable<string> SourcePathList, string DirectoryPath, ProgressChangedEventHandler ProgressHandler = null, bool IsUndoOperation = false)
        {
            using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableController())
            {
                await Exclusive.Controller.CopyAsync(SourcePathList, DirectoryPath, ProgressHandler, IsUndoOperation).ConfigureAwait(false);
            }
        }

        public static async Task<string> RenameAsync(string Path, string DesireName)
        {
            using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableController())
            {
                return await Exclusive.Controller.RenameAsync(Path, DesireName).ConfigureAwait(false);
            }
        }

        public static async Task DeleteAsync(string SourcePath, bool PermanentDelete, ProgressChangedEventHandler ProgressHandler = null, bool IsUndoOperation = false)
        {
            using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableController())
            {
                await Exclusive.Controller.DeleteAsync(SourcePath, PermanentDelete, ProgressHandler, IsUndoOperation).ConfigureAwait(false);
            }
        }

        public static async Task DeleteAsync(IEnumerable<string> SourcePathList, bool PermanentDelete, ProgressChangedEventHandler ProgressHandler = null, bool IsUndoOperation = false)
        {
            using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableController())
            {
                await Exclusive.Controller.DeleteAsync(SourcePathList, PermanentDelete, ProgressHandler, IsUndoOperation).ConfigureAwait(false);
            }
        }

        public static async Task<bool> CheckExistAsync(string Path)
        {
            if (System.IO.Path.IsPathRooted(Path))
            {
                if (WIN_Native_API.CheckLocationAvailability(Path))
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

        public static async Task<FileSystemStorageItemBase> OpenAsync(string Path)
        {
            if (System.IO.Path.GetPathRoot(Path) != Path && WIN_Native_API.GetStorageItem(Path) is FileSystemStorageItemBase Item)
            {
                return Item;
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
                        return new FileSystemStorageFolder(Folder, await Folder.GetThumbnailBitmapAsync().ConfigureAwait(true), await Folder.GetModifiedTimeAsync().ConfigureAwait(true));
                    }
                    else
                    {
                        StorageFolder ParentFolder = await StorageFolder.GetFolderFromPathAsync(DirectoryPath);

                        switch (await ParentFolder.TryGetItemAsync(System.IO.Path.GetFileName(Path)))
                        {
                            case StorageFolder Folder:
                                {
                                    return new FileSystemStorageFolder(Folder, await Folder.GetThumbnailBitmapAsync().ConfigureAwait(true), await Folder.GetModifiedTimeAsync().ConfigureAwait(true));
                                }
                            case StorageFile File:
                                {
                                    return new FileSystemStorageFile(File, await File.GetThumbnailBitmapAsync().ConfigureAwait(true), await File.GetSizeRawDataAsync().ConfigureAwait(true), await File.GetModifiedTimeAsync().ConfigureAwait(true));
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
                            return await OpenAsync(NewPath).ConfigureAwait(true);
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
                                            return new FileSystemStorageFile(NewFile, await NewFile.GetThumbnailBitmapAsync().ConfigureAwait(true), await NewFile.GetSizeRawDataAsync().ConfigureAwait(true), await NewFile.GetModifiedTimeAsync().ConfigureAwait(true));
                                        }
                                    case CreateOption.OpenIfExist:
                                        {
                                            StorageFile NewFile = await Folder.CreateFileAsync(System.IO.Path.GetFileName(Path), CreationCollisionOption.OpenIfExists);
                                            return new FileSystemStorageFile(NewFile, await NewFile.GetThumbnailBitmapAsync().ConfigureAwait(true), await NewFile.GetSizeRawDataAsync().ConfigureAwait(true), await NewFile.GetModifiedTimeAsync().ConfigureAwait(true));
                                        }
                                    case CreateOption.ReplaceExisting:
                                        {
                                            StorageFile NewFile = await Folder.CreateFileAsync(System.IO.Path.GetFileName(Path), CreationCollisionOption.ReplaceExisting);
                                            return new FileSystemStorageFile(NewFile, await NewFile.GetThumbnailBitmapAsync().ConfigureAwait(true), await NewFile.GetSizeRawDataAsync().ConfigureAwait(true), await NewFile.GetModifiedTimeAsync().ConfigureAwait(true));
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
                            return await OpenAsync(NewPath).ConfigureAwait(true);
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
                                            return new FileSystemStorageFolder(NewFolder, await NewFolder.GetThumbnailBitmapAsync().ConfigureAwait(true), await NewFolder.GetModifiedTimeAsync().ConfigureAwait(true));
                                        }
                                    case CreateOption.OpenIfExist:
                                        {
                                            StorageFolder NewFolder = await Folder.CreateFolderAsync(System.IO.Path.GetFileName(Path), CreationCollisionOption.OpenIfExists);
                                            return new FileSystemStorageFolder(NewFolder, await NewFolder.GetThumbnailBitmapAsync().ConfigureAwait(true), await NewFolder.GetModifiedTimeAsync().ConfigureAwait(true));
                                        }
                                    case CreateOption.ReplaceExisting:
                                        {
                                            StorageFolder NewFolder = await Folder.CreateFolderAsync(System.IO.Path.GetFileName(Path), CreationCollisionOption.ReplaceExisting);
                                            return new FileSystemStorageFolder(NewFolder, await NewFolder.GetThumbnailBitmapAsync().ConfigureAwait(true), await NewFolder.GetModifiedTimeAsync().ConfigureAwait(true));
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

            SizeRaw = ((ulong)Data.nFileSizeHigh << 32) + Data.nFileSizeLow;

            WIN_Native_API.FileTimeToSystemTime(ref Data.ftLastWriteTime, out WIN_Native_API.SYSTEMTIME ModTime);
            ModifiedTimeRaw = new DateTime(ModTime.Year, ModTime.Month, ModTime.Day, ModTime.Hour, ModTime.Minute, ModTime.Second, ModTime.Milliseconds, DateTimeKind.Utc).ToLocalTime();

            WIN_Native_API.FileTimeToSystemTime(ref Data.ftCreationTime, out WIN_Native_API.SYSTEMTIME CreTime);
            CreationTimeRaw = new DateTime(CreTime.Year, CreTime.Month, CreTime.Day, CreTime.Hour, CreTime.Minute, CreTime.Second, CreTime.Milliseconds, DateTimeKind.Utc).ToLocalTime();
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
                case ThumbnailStatus.ReduceOpacity:
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

        public async Task LoadMorePropertyAsync()
        {
            if ((this is FileSystemStorageFile && SettingControl.ContentLoadMode == LoadMode.OnlyFile) || SettingControl.ContentLoadMode == LoadMode.FileAndFolder)
            {
                if (!CheckIfPropertyLoaded())
                {
                    await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, async () =>
                    {
                        try
                        {
                            await LoadMorePropertyCore().ConfigureAwait(true);

                            OnPropertyChanged(nameof(Size));
                            OnPropertyChanged(nameof(Name));
                            OnPropertyChanged(nameof(ModifiedTime));
                            OnPropertyChanged(nameof(Thumbnail));
                            OnPropertyChanged(nameof(DisplayType));
                        }
                        catch (Exception ex)
                        {
                            LogTracer.Log(ex, $"An exception was threw in {nameof(LoadMorePropertyAsync)}, StorageType: {GetType().FullName}, Path: {Path}");
                        }
                    });
                }
            }
        }

        protected abstract bool CheckIfPropertyLoaded();

        protected abstract Task LoadMorePropertyCore(bool ForceUpdate = false);

        public abstract Task<IStorageItem> GetStorageItemAsync();

        public async Task ReplaceAsync(string NewPath)
        {
            if (NewPath != Path)
            {
                Path = NewPath;
                await RefreshAsync().ConfigureAwait(false);
            }
        }

        public async Task RefreshAsync()
        {
            try
            {
                if (await CheckExistAsync(Path).ConfigureAwait(true))
                {
                    await LoadMorePropertyCore(true).ConfigureAwait(true);

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

        public virtual Task MoveAsync(string DirectoryPath, ProgressChangedEventHandler ProgressHandler = null, bool IsUndoOperation = false)
        {
            return MoveAsync(Path, DirectoryPath, ProgressHandler, IsUndoOperation);
        }

        public virtual Task MoveAsync(FileSystemStorageFolder Directory, ProgressChangedEventHandler ProgressHandler = null, bool IsUndoOperation = false)
        {
            return MoveAsync(Directory.Path, ProgressHandler, IsUndoOperation);
        }

        public virtual Task CopyAsync(string DirectoryPath, ProgressChangedEventHandler ProgressHandler = null, bool IsUndoOperation = false)
        {
            return CopyAsync(Path, DirectoryPath, ProgressHandler, IsUndoOperation);
        }

        public virtual Task CopyAsync(FileSystemStorageFolder Directory, ProgressChangedEventHandler ProgressHandler = null, bool IsUndoOperation = false)
        {
            return CopyAsync(Directory.Path, ProgressHandler, IsUndoOperation);
        }

        public async virtual Task<string> RenameAsync(string DesireName)
        {
            string NewName = await RenameAsync(Path, DesireName);

            await ReplaceAsync(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Path), NewName)).ConfigureAwait(false);

            return NewName;
        }

        public virtual Task DeleteAsync(bool PermanentDelete, ProgressChangedEventHandler ProgressHandler = null, bool IsUndoOperation = false)
        {
            return DeleteAsync(Path, PermanentDelete, ProgressHandler, IsUndoOperation);
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
    }
}
