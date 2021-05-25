using Microsoft.Win32.SafeHandles;
using NetworkAccess;
using RX_Explorer.Interface;
using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel;
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
                LogTracer.Log(ex, "Could not create a new file stream");
                return null;
            }
        }

        public virtual async Task<IRandomAccessStream> GetRandomAccessStreamFromFileAsync(FileAccessMode Mode)
        {
            return await FileRandomAccessStream.OpenAsync(Path, Mode);
        }

        public virtual async Task<StorageStreamTransaction> GetTransactionStreamFromFileAsync()
        {
            return await FileRandomAccessStream.OpenTransactedWriteAsync(Path);
        }

        protected override async Task LoadMorePropertiesCoreAsync(bool ForceUpdate)
        {
            if (await GetStorageItemAsync() is StorageFile File)
            {
                Thumbnail = await File.GetThumbnailBitmapAsync(ThumbnailMode.ListView);

                if (ForceUpdate)
                {
                    ModifiedTimeRaw = await File.GetModifiedTimeAsync();
                    SizeRaw = await File.GetSizeRawDataAsync();
                }
            }
        }

        protected override Task LoadMorePropertiesCoreAsync(FullTrustProcessController Controller, bool ForceUpdate)
        {
            return LoadMorePropertiesCoreAsync(ForceUpdate);
        }

        protected override bool LoadMorePropertiesWithFullTrustProcess()
        {
            return false;
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
    }
}
