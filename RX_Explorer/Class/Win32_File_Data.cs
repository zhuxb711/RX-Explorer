using System;
using System.IO;
using System.Runtime.InteropServices.ComTypes;

namespace RX_Explorer.Class
{
    public sealed class Win32_File_Data
    {
        public string Path { get; }

        public ulong Size { get; }

        public bool IsReadOnly => Attributes.HasFlag(FileAttributes.ReadOnly);

        public bool IsSystemItem => Attributes.HasFlag(FileAttributes.System);

        public bool IsDataValid { get; } = true;

        public FileAttributes Attributes { get; }

        public DateTimeOffset CreationTime { get; }

        public DateTimeOffset ModifiedTime { get; }

        public DateTimeOffset LastAccessTime { get; }

        public Win32_File_Data(string Path, Win32_Native_API.WIN32_FIND_DATA Data)
        {
            this.Path = Path;
            Size = ((ulong)Data.nFileSizeHigh << 32) + Data.nFileSizeLow;
            Attributes = Data.dwFileAttributes;

            if (Win32_Native_API.FileTimeToSystemTime(ref Data.ftLastWriteTime, out Win32_Native_API.SYSTEMTIME ModTime))
            {
                ModifiedTime = new DateTime(ModTime.Year, ModTime.Month, ModTime.Day, ModTime.Hour, ModTime.Minute, ModTime.Second, ModTime.Milliseconds, DateTimeKind.Utc).ToLocalTime();
            }

            if (Win32_Native_API.FileTimeToSystemTime(ref Data.ftCreationTime, out Win32_Native_API.SYSTEMTIME CreTime))
            {
                CreationTime = new DateTime(CreTime.Year, CreTime.Month, CreTime.Day, CreTime.Hour, CreTime.Minute, CreTime.Second, CreTime.Milliseconds, DateTimeKind.Utc).ToLocalTime();
            }
        }

        public Win32_File_Data(string Path, ulong Size, FileAttributes Attributes, FILETIME LWTime, FILETIME CTime)
        {
            this.Path = Path;
            this.Size = Size;
            this.Attributes = Attributes;

            if (Win32_Native_API.FileTimeToSystemTime(ref LWTime, out Win32_Native_API.SYSTEMTIME ModTime))
            {
                ModifiedTime = new DateTime(ModTime.Year, ModTime.Month, ModTime.Day, ModTime.Hour, ModTime.Minute, ModTime.Second, ModTime.Milliseconds, DateTimeKind.Utc).ToLocalTime();
            }

            if (Win32_Native_API.FileTimeToSystemTime(ref CTime, out Win32_Native_API.SYSTEMTIME CreTime))
            {
                CreationTime = new DateTime(CreTime.Year, CreTime.Month, CreTime.Day, CreTime.Hour, CreTime.Minute, CreTime.Second, CreTime.Milliseconds, DateTimeKind.Utc).ToLocalTime();
            }
        }

        public Win32_File_Data(string Path)
        {
            IsDataValid = false;
            this.Path = Path;
        }
    }
}
