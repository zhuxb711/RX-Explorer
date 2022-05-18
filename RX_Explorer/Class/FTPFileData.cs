using FluentFTP;
using System;
using System.Text.RegularExpressions;

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
            if (Regex.IsMatch(Path, @"^ftp(s)?:\\.+", RegexOptions.IgnoreCase))
            {
                if (Regex.IsMatch(Path, @"^ftp(s)?:\\\\.+", RegexOptions.IgnoreCase))
                {
                    if (Path.StartsWith("ftp:", StringComparison.OrdinalIgnoreCase))
                    {
                        Path = Path.Remove(5, 1);
                    }
                    else if (Path.StartsWith("ftps:", StringComparison.OrdinalIgnoreCase))
                    {
                        Path = Path.Remove(6, 1);
                    }
                }

                this.Path = Path;
                this.Size = Size;
                this.Permission = Permission;
                this.RelatedPath = RelatedPath;
                this.ModifiedTime = ModifiedTime;
                this.CreationTime = CreationTime;
            }
            else
            {
                throw new NotSupportedException(Path);
            }
        }
    }
}
