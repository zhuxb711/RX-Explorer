using System;

namespace RX_Explorer.Class
{
    public sealed class Win32_File_Data
    {
        public string Path { get; }

        public ulong Size { get; }

        public bool IsReadOnly { get; }

        public bool IsSystemItem { get; }

        public DateTimeOffset CreationTime { get; }

        public DateTimeOffset ModifiedTime { get; }

        public DateTimeOffset LastAccessTime { get; }

        public static Win32_File_Data Empty { get; } = new Win32_File_Data();

        public Win32_File_Data(string Path, Win32_Native_API.WIN32_FIND_DATA Data)
        {
            this.Path = Path;

            IsReadOnly = ((System.IO.FileAttributes)Data.dwFileAttributes).HasFlag(System.IO.FileAttributes.ReadOnly);
            IsSystemItem = IsReadOnly = ((System.IO.FileAttributes)Data.dwFileAttributes).HasFlag(System.IO.FileAttributes.System);

            Size = ((ulong)Data.nFileSizeHigh << 32) + Data.nFileSizeLow;

            Win32_Native_API.FileTimeToSystemTime(ref Data.ftLastWriteTime, out Win32_Native_API.SYSTEMTIME ModTime);
            ModifiedTime = new DateTime(ModTime.Year, ModTime.Month, ModTime.Day, ModTime.Hour, ModTime.Minute, ModTime.Second, ModTime.Milliseconds, DateTimeKind.Utc).ToLocalTime();

            Win32_Native_API.FileTimeToSystemTime(ref Data.ftCreationTime, out Win32_Native_API.SYSTEMTIME CreTime);
            CreationTime = new DateTime(CreTime.Year, CreTime.Month, CreTime.Day, CreTime.Hour, CreTime.Minute, CreTime.Second, CreTime.Milliseconds, DateTimeKind.Utc).ToLocalTime();
        }

        private Win32_File_Data()
        {

        }
    }
}
