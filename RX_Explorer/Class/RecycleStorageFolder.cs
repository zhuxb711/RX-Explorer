using RX_Explorer.Interface;
using System;
using System.ComponentModel;
using System.Threading.Tasks;

namespace RX_Explorer.Class
{
    public class RecycleStorageFolder : FileSystemStorageFolder, IRecycleStorageItem
    {
        public string OriginPath { get; private set; }

        public override string Name
        {
            get
            {
                return System.IO.Path.GetFileName(OriginPath);
            }
        }

        public override string DisplayName
        {
            get
            {
                return Name;
            }
        }

        public override string ModifiedTimeDescription
        {
            get
            {
                if (ModifiedTime == DateTimeOffset.FromFileTime(0))
                {
                    return Globalization.GetString("UnknownText");
                }
                else
                {
                    return ModifiedTime.ToString("G");
                }
            }
        }

        public override Task DeleteAsync(bool PermanentDelete, ProgressChangedEventHandler ProgressHandler = null)
        {
            return DeleteAsync();
        }

        public RecycleStorageFolder(string Path, string OriginPath, DateTimeOffset DeleteTime) : base(Win32_Native_API.GetStorageItemRawData(Path))
        {
            this.OriginPath = OriginPath;
            ModifiedTime = DeleteTime.ToLocalTime();
        }

        public async Task<bool> DeleteAsync()
        {
            using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableController())
            {
                return await Exclusive.Controller.DeleteItemInRecycleBinAsync(Path);
            }
        }

        public async Task<bool> RestoreAsync()
        {
            using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableController())
            {
                return await Exclusive.Controller.RestoreItemInRecycleBinAsync(OriginPath);
            }
        }
    }
}
