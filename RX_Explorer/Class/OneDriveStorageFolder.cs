using RX_Explorer.Interface;
using System;
using System.Linq;
using Windows.Storage;

namespace RX_Explorer.Class
{
    class OneDriveStorageFolder : FileSystemStorageFolder, IOneDriveStorageItem
    {
        public OneDriveSyncStatus SyncStatus => throw new NotImplementedException();

        public OneDriveStorageFolder(StorageFolder Item, DateTimeOffset ModifiedTime) : base(Item, ModifiedTime)
        {

        }

        public OneDriveStorageFolder(string Path, WIN_Native_API.WIN32_FIND_DATA Data) : base(Path, Data)
        {

        }

        protected override bool CheckIfNeedLoadThumbnailOverlay()
        {
            return true;
        }
    }
}
