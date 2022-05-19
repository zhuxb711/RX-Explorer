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

        public FTPFileData(string FTPPath, FtpListItem Item)
        {
            if (Regex.IsMatch(FTPPath, @"^ftp(s)?:\\.+", RegexOptions.IgnoreCase))
            {
                if (Regex.IsMatch(FTPPath, @"^ftp(s)?:\\\\.+", RegexOptions.IgnoreCase))
                {
                    if (FTPPath.StartsWith("ftp:", StringComparison.OrdinalIgnoreCase))
                    {
                        FTPPath = FTPPath.Remove(5, 1);
                    }
                    else if (Path.StartsWith("ftps:", StringComparison.OrdinalIgnoreCase))
                    {
                        FTPPath = FTPPath.Remove(6, 1);
                    }
                }

                Path = FTPPath.Replace("/", @"\");
                Size = Convert.ToUInt64(Item.Size);
                Permission = Item.OwnerPermissions;
                RelatedPath = Item.FullName.Replace("/", @"\");
                ModifiedTime = new DateTimeOffset(Item.Modified, TimeZoneInfo.Utc.BaseUtcOffset);
                CreationTime = new DateTimeOffset(Item.Modified, TimeZoneInfo.Utc.BaseUtcOffset);
            }
            else
            {
                throw new NotSupportedException(Path);
            }
        }

        public FTPFileData(string FTPPath)
        {
            if (Regex.IsMatch(FTPPath, @"^ftp(s)?:\\.+", RegexOptions.IgnoreCase))
            {
                if (Regex.IsMatch(FTPPath, @"^ftp(s)?:\\\\.+", RegexOptions.IgnoreCase))
                {
                    if (FTPPath.StartsWith("ftp:", StringComparison.OrdinalIgnoreCase))
                    {
                        FTPPath = FTPPath.Remove(5, 1);
                    }
                    else if (Path.StartsWith("ftps:", StringComparison.OrdinalIgnoreCase))
                    {
                        FTPPath = FTPPath.Remove(6, 1);
                    }
                }

                Path = FTPPath.Replace("/", @"\");
                Size = 0;
                Permission = FtpPermission.Read | FtpPermission.Write;
                RelatedPath = @"\";
                ModifiedTime = DateTimeOffset.MinValue;
                CreationTime = DateTimeOffset.MinValue;
            }
            else
            {
                throw new NotSupportedException(Path);
            }
        }
    }
}
