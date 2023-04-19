using System;
using System.IO;
using System.Text.Json.Serialization;

namespace SharedLibrary
{
    public sealed class MTPFileData
    {
        public string DeviceName { get; }

        public string Path { get; }

        public ulong Size { get; }

        [JsonIgnore]
        public bool IsReadOnly => Attributes.HasFlag(FileAttributes.ReadOnly);

        [JsonIgnore]
        public bool IsSystemItem => Attributes.HasFlag(FileAttributes.System);

        [JsonIgnore]
        public bool IsHiddenItem => Attributes.HasFlag(FileAttributes.Hidden);

        public FileAttributes Attributes { get; }

        public DateTimeOffset CreationTime { get; }

        public DateTimeOffset ModifiedTime { get; }

        public MTPFileData(string DeviceName, string Path, ulong Size, FileAttributes Attributes, DateTimeOffset CreationTime, DateTimeOffset ModifiedTime)
        {
            this.DeviceName = DeviceName;
            this.Path = Path.TrimEnd('\\');
            this.Size = Size;
            this.Attributes = Attributes;
            this.CreationTime = CreationTime;
            this.ModifiedTime = ModifiedTime;
        }
    }
}
