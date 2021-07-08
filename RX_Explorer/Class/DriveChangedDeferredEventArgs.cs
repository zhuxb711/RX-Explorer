using Microsoft.Toolkit.Deferred;
using System.Threading.Tasks;

namespace RX_Explorer.Class
{
    public sealed class DriveChangedDeferredEventArgs : DeferredEventArgs
    {
        public FileSystemStorageFolder StorageItem { get; }

        public static async Task<DriveChangedDeferredEventArgs> CreateAsync(DriveDataBase Data)
        {
            return new DriveChangedDeferredEventArgs(await FileSystemStorageItemBase.CreateFromStorageItemAsync(Data.DriveFolder));
        }

        private DriveChangedDeferredEventArgs(FileSystemStorageFolder StorageItem)
        {
            this.StorageItem = StorageItem;
        }
    }
}
