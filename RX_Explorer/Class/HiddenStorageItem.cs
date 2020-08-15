using System;
using System.Threading.Tasks;
using Windows.Storage;

namespace RX_Explorer.Class
{
    public sealed class HiddenStorageItem : FileSystemStorageItem
    {
        public override Task<IStorageItem> GetStorageItem()
        {
            return null;
        }

        public HiddenStorageItem(WIN_Native_API.WIN32_FIND_DATA Data, StorageItemTypes StorageType, string Path, DateTimeOffset ModifiedTime) : base(Data, StorageType, Path, ModifiedTime)
        {
            SetThumbnailOpacity(ThumbnailStatus.ReduceOpacity);
        }
    }
}
