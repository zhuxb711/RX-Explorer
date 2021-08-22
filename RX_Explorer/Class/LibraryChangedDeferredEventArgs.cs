
using Microsoft.Toolkit.Deferred;

namespace RX_Explorer.Class
{
    public sealed class LibraryChangedDeferredEventArgs : DeferredEventArgs
    {
        public LibraryStorageFolder StorageItem { get; }

        public LibraryChangedDeferredEventArgs(LibraryStorageFolder StorageItem)
        {
            this.StorageItem = StorageItem;
        }
    }
}
