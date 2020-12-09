using System;
using System.Threading.Tasks;
using Windows.Storage;

namespace RX_Explorer.Class
{
    public sealed class HiddenStorageItem : FileSystemStorageItemBase
    {
        public override Task<IStorageItem> GetStorageItem()
        {
            return Task.FromResult<IStorageItem>(null);
        }

        public override void SetThumbnailOpacity(ThumbnailStatus Status)
        {

        }

        public HiddenStorageItem(WIN_Native_API.WIN32_FIND_DATA Data, StorageItemTypes StorageType, string Path, DateTimeOffset CreationTime, DateTimeOffset ModifiedTime) : base(Data, StorageType, Path, CreationTime, ModifiedTime)
        {
            base.SetThumbnailOpacity(ThumbnailStatus.ReduceOpacity);
        }
    }
}
