using Microsoft.Win32.SafeHandles;
using RX_Explorer.Interface;
using ShareClassLibrary;
using System;
using System.IO;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media.Imaging;

namespace RX_Explorer.Class
{
    public class FileSystemStorageFile : FileSystemStorageItemBase, ICoreStorageItem<StorageFile>
    {
        public override string SizeDescription => Size.GetSizeDescription();

        public override string DisplayType => (StorageItem?.DisplayType) ?? Type;

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

        public override BitmapImage Thumbnail => base.Thumbnail ?? new BitmapImage(AppThemeController.Current.Theme == ElementTheme.Dark ? Const_File_White_Image_Uri : Const_File_Black_Image_Uri);

        public StorageFile StorageItem { get; protected set; }

        public FileSystemStorageFile(StorageFile Item) : base(Item.Path, Item.GetSafeFileHandle(AccessMode.Read, OptimizeOption.None), false)
        {
            StorageItem = Item;
        }

        public FileSystemStorageFile(Win32_File_Data Data) : base(Data)
        {

        }

        public async virtual Task<FileStream> GetStreamFromFileAsync(AccessMode Mode, OptimizeOption Option)
        {
            if (Win32_Native_API.CreateStreamFromFile(Path, Mode, Option) is FileStream Stream)
            {
                return Stream;
            }
            else
            {
                FileAccess Access = Mode switch
                {
                    AccessMode.Read => FileAccess.Read,
                    AccessMode.ReadWrite or AccessMode.Exclusive => FileAccess.ReadWrite,
                    AccessMode.Write => FileAccess.Write,
                    _ => throw new NotSupportedException()
                };

                SafeFileHandle Handle = await GetNativeHandleAsync(Mode, Option);

                if (Handle.IsInvalid)
                {
                    LogTracer.Log($"Could not create a new file stream, Path: \"{Path}\"");
                    throw new UnauthorizedAccessException();
                }
                else
                {
                    return new FileStream(Handle, Access);
                }
            }
        }

        public async Task<ulong> GetSizeOnDiskAsync()
        {
            using (SafeFileHandle Handle = await GetNativeHandleAsync(AccessMode.Read, OptimizeOption.None))
            {
                if (!Handle.IsInvalid)
                {
                    return Win32_Native_API.GetSizeOnDisk(Path, Handle.DangerousGetHandle());
                }
            }

            return 0;
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

        protected override async Task LoadCoreAsync(bool ForceUpdate)
        {
            if (ForceUpdate)
            {
                try
                {
                    Win32_File_Data Data = Win32_Native_API.GetStorageItemRawData(Path);

                    if (Data.IsDataValid)
                    {
                        ModifiedTime = Data.ModifiedTime;
                        Size = Data.Size;
                    }
                    else if (await GetStorageItemAsync() is StorageFile File)
                    {
                        ModifiedTime = await File.GetModifiedTimeAsync();
                        Size = await File.GetSizeRawDataAsync();
                    }
                }
                catch (Exception ex)
                {
                    LogTracer.Log(ex, $"An unexpected exception was threw in {nameof(LoadCoreAsync)}");
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

        public static explicit operator StorageFile(FileSystemStorageFile File)
        {
            return File.StorageItem;
        }
    }
}
