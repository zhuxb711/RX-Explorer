using System.Collections.Generic;

namespace RX_Explorer.Class
{
    public sealed class RefreshRequestedEventArgs
    {
        public IEnumerable<FileSystemStorageItemBase> FilterCollection { get; }

        public RefreshRequestedEventArgs(IEnumerable<FileSystemStorageItemBase> FilterCollection)
        {
            this.FilterCollection = FilterCollection;
        }
    }
}
