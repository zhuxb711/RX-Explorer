using System;
using System.Runtime.InteropServices.ComTypes;
using Windows.Storage;
using FileAttributes = System.IO.FileAttributes;

namespace RX_Explorer.Class
{
    public sealed class NativeFileData
    {
        public string Path { get; }

        public ulong Size { get; }

        public bool IsReadOnly => Attributes.HasFlag(FileAttributes.ReadOnly);

        public bool IsSystemItem => Attributes.HasFlag(FileAttributes.System);

        public bool IsHiddenItem => Attributes.HasFlag(FileAttributes.Hidden);

        public bool IsDataValid { get; } = true;

        public FileAttributes Attributes { get; }

        public DateTimeOffset CreationTime { get; }

        public DateTimeOffset ModifiedTime { get; }

        public DateTimeOffset LastAccessTime { get; }

        public IStorageItem StorageItem { get; private set; }

        public void SetStorageItemOnAvailable(IStorageItem Item)
        {
            StorageItem = Item;
        }

        public NativeFileData(string Path, NativeWin32API.WIN32_FIND_DATA Data)
        {
            this.Path = Path;
            Size = ((ulong)Data.nFileSizeHigh << 32) + Data.nFileSizeLow;
            Attributes = Data.dwFileAttributes;

            if (NativeWin32API.FileTimeToSystemTime(ref Data.ftLastWriteTime, out NativeWin32API.SYSTEMTIME ModTime))
            {
                ModifiedTime = new DateTime(ModTime.Year, ModTime.Month, ModTime.Day, ModTime.Hour, ModTime.Minute, ModTime.Second, ModTime.Milliseconds, DateTimeKind.Utc).ToLocalTime();
            }

            if (NativeWin32API.FileTimeToSystemTime(ref Data.ftCreationTime, out NativeWin32API.SYSTEMTIME CreTime))
            {
                CreationTime = new DateTime(CreTime.Year, CreTime.Month, CreTime.Day, CreTime.Hour, CreTime.Minute, CreTime.Second, CreTime.Milliseconds, DateTimeKind.Utc).ToLocalTime();
            }

            if (NativeWin32API.FileTimeToSystemTime(ref Data.ftLastAccessTime, out NativeWin32API.SYSTEMTIME AccTime))
            {
                LastAccessTime = new DateTime(AccTime.Year, AccTime.Month, AccTime.Day, AccTime.Hour, AccTime.Minute, AccTime.Second, AccTime.Milliseconds, DateTimeKind.Utc).ToLocalTime();
            }
        }

        public NativeFileData(string Path, ulong Size, FileAttributes Attributes, FILETIME LWTime, FILETIME CTime, FILETIME LAime)
        {
            this.Path = Path;
            this.Size = Size;
            this.Attributes = Attributes;

            if (NativeWin32API.FileTimeToSystemTime(ref LWTime, out NativeWin32API.SYSTEMTIME ModTime))
            {
                ModifiedTime = new DateTime(ModTime.Year, ModTime.Month, ModTime.Day, ModTime.Hour, ModTime.Minute, ModTime.Second, ModTime.Milliseconds, DateTimeKind.Utc).ToLocalTime();
            }

            if (NativeWin32API.FileTimeToSystemTime(ref CTime, out NativeWin32API.SYSTEMTIME CreTime))
            {
                CreationTime = new DateTime(CreTime.Year, CreTime.Month, CreTime.Day, CreTime.Hour, CreTime.Minute, CreTime.Second, CreTime.Milliseconds, DateTimeKind.Utc).ToLocalTime();
            }

            if (NativeWin32API.FileTimeToSystemTime(ref LAime, out NativeWin32API.SYSTEMTIME AccTime))
            {
                LastAccessTime = new DateTime(AccTime.Year, AccTime.Month, AccTime.Day, AccTime.Hour, AccTime.Minute, AccTime.Second, AccTime.Milliseconds, DateTimeKind.Utc).ToLocalTime();
            }
        }

        public NativeFileData(string Path)
        {
            IsDataValid = false;
            this.Path = Path;
        }
    }
}
