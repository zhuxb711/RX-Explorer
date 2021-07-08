
using Microsoft.Toolkit.Deferred;
using System.Threading.Tasks;

namespace RX_Explorer.Class
{
    public sealed class LibraryChangedDeferredEventArgs : DeferredEventArgs
    {
        public FileSystemStorageFolder StorageItem { get; }

        public static async Task<LibraryChangedDeferredEventArgs> CreateAsync(LibraryFolder Lib)
        {
            return new LibraryChangedDeferredEventArgs(await FileSystemStorageItemBase.CreateFromStorageItemAsync(Lib.LibFolder));
        }

        private LibraryChangedDeferredEventArgs(FileSystemStorageFolder StorageItem)
        {
            this.StorageItem = StorageItem;
        }
    }
}
