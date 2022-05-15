using FluentFTP;
using System;

namespace RX_Explorer.Class
{
    public sealed class FTPFileData
    {
        public string Path { get; }

        public string RelatedPath { get; }

        public ulong Size { get; }

        public bool IsReadOnly => !Permission.HasFlag(FtpPermission.Write);

        public bool IsSystemItem => false;

        public bool IsHiddenItem => false;

        public FtpPermission Permission { get; }

        public DateTimeOffset CreationTime { get; }

        public DateTimeOffset ModifiedTime { get; }

        public FTPFileData(string Path, string RelatedPath, ulong Size, FtpPermission Permission, DateTimeOffset ModifiedTime, DateTimeOffset CreationTime)
        {
            this.Path = Path;
            this.Size = Size;
            this.Permission = Permission; 
            this.RelatedPath = RelatedPath;
            this.ModifiedTime = ModifiedTime;
            this.CreationTime = CreationTime;
        }
    }
}
