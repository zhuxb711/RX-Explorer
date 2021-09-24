using Microsoft.Toolkit.Deferred;

namespace RX_Explorer.Class
{
    public sealed class DriveChangedDeferredEventArgs : DeferredEventArgs
    {
        public FileSystemStorageFolder StorageItem { get; }

        public CommonChangeType Type { get; }

        public DriveChangedDeferredEventArgs(CommonChangeType Type, FileSystemStorageFolder StorageItem)
        {
            this.Type = Type;
            this.StorageItem = StorageItem;
        }
    }
}
