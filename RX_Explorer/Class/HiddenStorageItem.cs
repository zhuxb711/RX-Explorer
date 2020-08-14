using System;
using Windows.Storage;

namespace RX_Explorer.Class
{
    public class HiddenStorageItem : FileSystemStorageItem
    {
        public new IStorageItem GetStorageItem()
        {
            return null;
        }

        public HiddenStorageItem(WIN_Native_API.WIN32_FIND_DATA Data, StorageItemTypes StorageType, string Path, DateTimeOffset ModifiedTime) : base(Data, StorageType, Path, ModifiedTime)
        {
            SetThumbnailOpacity(ThumbnailStatus.ReduceOpacity);
        }
    }
}
