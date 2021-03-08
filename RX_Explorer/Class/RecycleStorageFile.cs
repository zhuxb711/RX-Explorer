using RX_Explorer.Interface;
using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Windows.Storage;

namespace RX_Explorer.Class
{
    public sealed class RecycleStorageFile : FileSystemStorageFile, IRecycleStorageItem
    {
        public string OriginPath { get; private set; }

        public override string Name
        {
            get
            {
                return System.IO.Path.GetFileName(OriginPath);
            }
        }

        public override string Type
        {
            get
            {
                return StorageItem?.FileType ?? System.IO.Path.GetExtension(OriginPath).ToUpper();
            }
        }

        public override string ModifiedTime
        {
            get
            {
                if (ModifiedTimeRaw == DateTimeOffset.FromFileTime(0))
                {
                    return Globalization.GetString("UnknownText");
                }
                else
                {
                    return ModifiedTimeRaw.ToString("G");
                }
            }
        }

        public override Task DeleteAsync(bool PermanentDelete, ProgressChangedEventHandler ProgressHandler = null, bool IsUndoOperation = false)
        {
            return DeleteAsync();
        }

        public RecycleStorageFile(string ActualPath, string OriginPath, DateTimeOffset CreateTime) : base(ActualPath)
        {
            this.OriginPath = OriginPath;
            ModifiedTimeRaw = CreateTime.ToLocalTime();
        }

        protected override async Task LoadMorePropertyCore(bool ForceUpdate)
        {
            if ((StorageItem == null || ForceUpdate) && (await GetStorageItemAsync().ConfigureAwait(true) is StorageFile File))
            {
                StorageItem = File;
                SizeRaw = await File.GetSizeRawDataAsync().ConfigureAwait(true);
                Thumbnail = await File.GetThumbnailBitmapAsync().ConfigureAwait(true);
            }
        }

        public async Task<bool> DeleteAsync()
        {
            using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableController())
            {
                return await Exclusive.Controller.DeleteItemInRecycleBinAsync(Path).ConfigureAwait(true);
            }
        }

        public async Task<bool> RestoreAsync()
        {
            using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableController())
            {
                return await Exclusive.Controller.RestoreItemInRecycleBinAsync(OriginPath).ConfigureAwait(true);
            }
        }
    }
}
