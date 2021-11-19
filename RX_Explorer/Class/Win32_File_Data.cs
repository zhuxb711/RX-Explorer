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
            Attributes = (FileAttributes)Data.dwFileAttributes;

            ModifiedTime = Data.ftLastWriteTime.ConvertToLocalDateTimeOffset();
            CreationTime = Data.ftCreationTime.ConvertToLocalDateTimeOffset();
        }

        public Win32_File_Data(string Path, ulong Size, FileAttributes Attributes, FILETIME LWTime, FILETIME CTime)
        {
            this.Path = Path;
            this.Size = Size;
            this.Attributes = Attributes;

            ModifiedTime = LWTime.ConvertToLocalDateTimeOffset();
            CreationTime = CTime.ConvertToLocalDateTimeOffset();
        }

        public Win32_File_Data(string Path)
        {
            IsDataValid = false;
            this.Path = Path;
        }
    }
}
