using FluentFTP;
using System;
using System.Text.RegularExpressions;

namespace RX_Explorer.Class
{
    public sealed class FtpFileData
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

        public FtpFileData(FtpPathAnalysis PathAnalysis, FtpListItem Item)
        {
            Path = PathAnalysis.Path;
            Size = Convert.ToUInt64(Item.Size);
            Permission = Item.OwnerPermissions;
            RelatedPath = PathAnalysis.RelatedPath;
            ModifiedTime = new DateTimeOffset(Item.Modified, TimeZoneInfo.Utc.BaseUtcOffset);
            CreationTime = new DateTimeOffset(Item.Modified, TimeZoneInfo.Utc.BaseUtcOffset);
        }

        public FtpFileData(FtpPathAnalysis PathAnalysis)
        {
            Path = PathAnalysis.Path;
            Permission = FtpPermission.Read | FtpPermission.Write;
            RelatedPath = PathAnalysis.RelatedPath;
            ModifiedTime = DateTimeOffset.MinValue;
            CreationTime = DateTimeOffset.MinValue;
        }
    }
}
