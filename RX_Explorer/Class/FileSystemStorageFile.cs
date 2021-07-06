using Microsoft.Win32.SafeHandles;
using System;
using System.IO;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.Storage.Streams;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media.Imaging;

namespace RX_Explorer.Class
{
    public class FileSystemStorageFile : FileSystemStorageItemBase
    {
        public override string DisplayName
        {
            get
            {
                return ((StorageItem as StorageFile)?.DisplayName) ?? Name;
            }
        }

        public override string Size
        {
            get
            {
                return SizeRaw.GetFileSizeDescription();
            }
        }

        public override string DisplayType
        {
            get
            {
                return ((StorageItem as StorageFile)?.DisplayType) ?? Type;
            }
        }

        public override bool IsReadOnly
        {
            get
            {
                if (StorageItem == null)
                {
                    return base.IsReadOnly;
                }
                else
                {
                    return StorageItem.Attributes.HasFlag(Windows.Storage.FileAttributes.ReadOnly);
                }
            }
        }

        public override bool IsSystemItem
        {
            get
            {
                if (StorageItem == null)
                {
                    return base.IsSystemItem;
                }
                else
                {
                    return false;
                }
            }
        }

        private BitmapImage InnerThumbnail;

        public override BitmapImage Thumbnail
        {
            get
            {
                return InnerThumbnail ?? new BitmapImage(AppThemeController.Current.Theme == ElementTheme.Dark ? Const_File_White_Image_Uri : Const_File_Black_Image_Uri);
            }
            protected set
            {
                if (value != null && value != InnerThumbnail)
                {
                    InnerThumbnail = value;
                }
            }
        }

        protected override bool IsFullTrustProcessNeeded
        {
            get
            {
                return false;
            }
        }
        protected FileSystemStorageFile(StorageFile Item, DateTimeOffset ModifiedTime, ulong Size) : base(Item.Path)
        {
            CreationTimeRaw = Item.DateCreated;
            ModifiedTimeRaw = ModifiedTime;
            SizeRaw = Size;
        }

        public FileSystemStorageFile(string Path, WIN_Native_API.WIN32_FIND_DATA Data) : base(Path, Data)
        {

        }

        public async virtual Task<FileStream> GetFileStreamFromFileAsync(AccessMode Mode)
        {
            try
            {
                if (WIN_Native_API.CreateFileStreamFromExistingPath(Path, Mode) is FileStream Stream)
                {
                    return Stream;
                }
                else
                {
                    if (await GetStorageItemAsync() is StorageFile File)
                    {
                        SafeFileHandle Handle = File.GetSafeFileHandle();

                        return new FileStream(Handle, FileAccess.ReadWrite);
                    }
                    else
                    {
                        return null;
                    }
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"Could not create a new file stream, Path: \"{Path}\"");
                return null;
            }
        }

        public virtual async Task<IRandomAccessStream> GetRandomAccessStreamFromFileAsync(FileAccessMode Mode)
        {
            if (StorageItem is StorageFile File)
            {
                return await File.OpenAsync(Mode);
            }
            else
            {
                return await FileRandomAccessStream.OpenAsync(Path, Mode);
            }
        }

        public virtual async Task<StorageStreamTransaction> GetTransactionStreamFromFileAsync()
        {
            if (StorageItem is StorageFile File)
            {
                return await File.OpenTransactedWriteAsync();
            }
            else
            {
                return await FileRandomAccessStream.OpenTransactedWriteAsync(Path);
            }
        }

        protected override async Task LoadPropertiesAsync(bool ForceUpdate)
        {
            if (ForceUpdate)
            {
                if (await GetStorageItemAsync() is StorageFile File)
                {
                    ModifiedTimeRaw = await File.GetModifiedTimeAsync();
                    SizeRaw = await File.GetSizeRawDataAsync();
                }
            }
        }

        protected override Task LoadPropertiesAsync(bool ForceUpdate, FullTrustProcessController Controller)
        {
            return LoadPropertiesAsync(ForceUpdate);
        }

        protected override bool CheckIfPropertiesLoaded()
        {
            return StorageItem != null && InnerThumbnail != null;
        }

        public async override Task<IStorageItem> GetStorageItemAsync()
        {
            try
            {
                return StorageItem ??= await StorageFile.GetFileFromPathAsync(Path);
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"Could not get StorageFile, Path: {Path}");
                return null;
            }
        }

        protected override bool CheckIfNeedLoadThumbnailOverlay()
        {
            return SpecialPath.IsPathIncluded(Path, SpecialPath.SpecialPathEnum.OneDrive);
        }

        protected override async Task LoadThumbnailAsync(ThumbnailMode Mode)
        {
            if (await GetStorageItemAsync() is StorageFile File)
            {
                Thumbnail = await File.GetThumbnailBitmapAsync(Mode);
            }
        }
    }
}
