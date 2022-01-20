using System;
using System.IO;

namespace FullTrustProcess
{
    public sealed class RemoteClipboardDataPackage : IDisposable
    {
        public RemoteClipboardStorageType ItemType { get; }

        public MemoryStream ContentStream { get; }

        public string Name { get; }

        public RemoteClipboardDataPackage(string Name, RemoteClipboardStorageType ItemType, MemoryStream ContentStream)
        {
            this.Name = Name;
            this.ItemType = ItemType;
            this.ContentStream = ContentStream;
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
            ContentStream?.Dispose();
        }

        ~RemoteClipboardDataPackage()
        {
            Dispose();
        }
    }

    public enum RemoteClipboardStorageType
    {
        File = 0,
        Folder = 1
    }
}
