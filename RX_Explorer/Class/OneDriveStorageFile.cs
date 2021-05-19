using RX_Explorer.Interface;
using System;
using System.Linq;
using Windows.Storage;

namespace RX_Explorer.Class
{
    class OneDriveStorageFile : FileSystemStorageFile, IOneDriveStorageItem
    {
        public OneDriveSyncStatus SyncStatus => throw new NotImplementedException();

        public OneDriveStorageFile(StorageFile Item, DateTimeOffset ModifiedTime, ulong Size) : base(Item, ModifiedTime, Size)
        {

        }

        public OneDriveStorageFile(string Path, WIN_Native_API.WIN32_FIND_DATA Data) : base(Path, Data)
        {

        }

        protected override bool CheckIfNeedLoadThumbnailOverlay()
        {
            return true;
        }
    }
}
