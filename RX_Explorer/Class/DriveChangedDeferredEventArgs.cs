using Microsoft.Toolkit.Deferred;
using System.Threading.Tasks;

namespace RX_Explorer.Class
{
    public sealed class DriveChangedDeferredEventArgs : DeferredEventArgs
    {
        public FileSystemStorageFolder StorageItem { get; }

        public DriveChangedDeferredEventArgs(DriveDataBase Data)
        {
            StorageItem = new FileSystemStorageFolder(Data.DriveFolder);
        }
    }
}
