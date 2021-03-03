using System;
using System.Threading.Tasks;
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

        public RecycleStorageItem(string ActualPath, string OriginPath, StorageItemTypes StorageType, DateTimeOffset CreateTime)
        {
            this.OriginPath = OriginPath;
            this.StorageType = StorageType;

            ModifiedTimeRaw = CreateTime.ToLocalTime();
            InternalPathString = ActualPath;
        }
    }
}
