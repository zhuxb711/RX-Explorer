using RX_Explorer.Class;

namespace RX_Explorer.Interface
{
    public interface IOneDriveStorageItem : IStorageItemPropertiesBase
    {
        public OneDriveSyncStatus SyncStatus { get; }
    }
}
