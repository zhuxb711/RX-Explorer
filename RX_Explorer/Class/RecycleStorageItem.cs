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

        public RecycleStorageItem(string ActualPath, string OriginPath, DateTimeOffset CreateTime)
        {
            this.OriginPath = OriginPath;
            ModifiedTimeRaw = CreateTime.ToLocalTime();

            if (string.IsNullOrEmpty(System.IO.Path.GetExtension(OriginPath)))
            {
                StorageType = StorageItemTypes.Folder;
            }
            else
            {
                StorageType = StorageItemTypes.File;
                SizeRaw = WIN_Native_API.CalculateSize(ActualPath);
            }

            InternalPathString = ActualPath;
        }
    }
}
