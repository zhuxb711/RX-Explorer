using System;

namespace SharedLibrary
{
    public sealed class RecycleBinItemDataPackage
    {
        public string Path { get; }

        public string OriginPath { get; }

        public bool IsDirectory { get; }

        public ulong Size { get; }

        public DateTimeOffset DeleteTime { get; }

        public RecycleBinItemDataPackage(string Path, string OriginPath, bool IsDirectory, ulong Size, DateTimeOffset DeleteTime)
        {
            this.Path = Path;
            this.OriginPath = OriginPath;
            this.IsDirectory = IsDirectory;
            this.Size = Size;
            this.DeleteTime = DeleteTime;
        }
    }
}
