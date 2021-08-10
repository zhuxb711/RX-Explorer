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

        public override BitmapImage Thumbnail
        {
            get
            {
                return base.Thumbnail ?? new BitmapImage(AppThemeController.Current.Theme == ElementTheme.Dark ? Const_File_White_Image_Uri : Const_File_Black_Image_Uri);
            }
        }

        public FileSystemStorageFile(StorageFile Item, DateTimeOffset ModifiedTime, ulong Size) : base(Item.Path)
        {
            StorageItem = Item;
            CreationTimeRaw = Item.DateCreated;
            ModifiedTimeRaw = ModifiedTime;
            SizeRaw = Size;
        }

        public FileSystemStorageFile(Win32_File_Data Data) : base(Data)
        {

        }

        public async virtual Task<FileStream> GetStreamFromFileAsync(AccessMode Mode)
        {
            try
            {
                if (Win32_Native_API.CreateFileStreamFromExistingPath(Path, Mode) is FileStream Stream)
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

        protected override async Task LoadCoreAsync(FullTrustProcessController Controller, bool ForceUpdate)
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
    }
}
