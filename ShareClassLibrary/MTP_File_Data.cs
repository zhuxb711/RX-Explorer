using System;
using System.IO;
using System.Text.Json.Serialization;

namespace ShareClassLibrary
{
    public sealed class MTP_File_Data
    {
        public string Path { get; }

        public ulong Size { get; }

        [JsonIgnore]
        public bool IsReadOnly => Attributes.HasFlag(FileAttributes.ReadOnly);

        [JsonIgnore]
        public bool IsSystemItem => Attributes.HasFlag(FileAttributes.System);

        public FileAttributes Attributes { get; }

        public DateTimeOffset CreationTime { get; }

        public DateTimeOffset ModifiedTime { get; }

        public MTP_File_Data(string Path, ulong Size, FileAttributes Attributes, DateTimeOffset CreationTime, DateTimeOffset ModifiedTime)
        {
            this.Path = Path;
            this.Size = Size;
            this.Attributes = Attributes;
            this.CreationTime = CreationTime;
            this.ModifiedTime = ModifiedTime;
        }
    }
}
