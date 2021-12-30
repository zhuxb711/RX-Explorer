using Microsoft.Win32.SafeHandles;
using Windows.Storage;

namespace RX_Explorer.Class
{
    public abstract class FileSystemStorageItemBase<T> : FileSystemStorageItemBase where T : IStorageItem
    {
        protected T StorageItem { get; set; }

        protected FileSystemStorageItemBase(Win32_File_Data Data) : base(Data)
        {
        }

        protected FileSystemStorageItemBase(string Path, SafeFileHandle Handle, bool LeaveOpen) : base(Path, Handle, LeaveOpen)
        {
        }

        public static explicit operator T(FileSystemStorageItemBase<T> File)
        {
            return File.StorageItem;
        }
    }
}
