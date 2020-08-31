using System;
using Windows.Storage;

namespace RX_Explorer.Class
{
    public sealed class RecycleStorageItem : FileSystemStorageItemBase
    {
        public string OriginPath { get; private set; }

        public override string Name
        {
            get
            {
                return System.IO.Path.GetFileName(OriginPath);
            }
        }

        public RecycleStorageItem(FileSystemStorageItemBase Item, string OriginPath, DateTimeOffset CreateTime)
        {
            if (Item == null)
            {
                throw new ArgumentNullException(nameof(Item), "Argument could not be null");
            }

            this.OriginPath = OriginPath;
            ModifiedTimeRaw = CreateTime.ToLocalTime();
            StorageType = Item.StorageType;
            InternalPathString = Item.Path;
            Thumbnail = Item.Thumbnail;

            if (StorageType != StorageItemTypes.Folder)
            {
                SizeRaw = Item.SizeRaw;
            }
        }

        public RecycleStorageItem(WIN_Native_API.WIN32_FIND_DATA Data, StorageItemTypes StorageType, string Path, DateTimeOffset ModifiedTime) : base(Data, StorageType, Path, ModifiedTime)
        {

        }
    }
}
