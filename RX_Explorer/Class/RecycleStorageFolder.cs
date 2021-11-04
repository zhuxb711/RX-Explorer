using RX_Explorer.Interface;
using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Windows.Storage;

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
                    return string.Empty;
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

        public RecycleStorageFolder(StorageFolder Folder, string OriginPath, DateTimeOffset DeleteTime) : base(Folder)
        {
            this.OriginPath = OriginPath;
            ModifiedTime = DeleteTime.ToLocalTime();
        }

        public RecycleStorageFolder(Win32_File_Data Data, string OriginPath, DateTimeOffset DeleteTime) : base(Data)
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
