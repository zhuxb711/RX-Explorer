
using Microsoft.Toolkit.Deferred;

namespace RX_Explorer.Class
{
    public sealed class LibraryChangedDeferredEventArgs : DeferredEventArgs
    {
        public LibraryStorageFolder StorageItem { get; }

        public CommonChangeType Type { get; }

        public LibraryChangedDeferredEventArgs(CommonChangeType Type, LibraryStorageFolder StorageItem)
        {
            this.Type = Type;
            this.StorageItem = StorageItem;
        }
    }
}
