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

        public override Task DeleteAsync(bool PermanentDelete, ProgressChangedEventHandler ProgressHandler = null, bool IsUndoOperation = false)
        {
            return DeleteAsync();
        }

        public RecycleStorageFolder(string ActualPath, string OriginPath, DateTimeOffset CreateTime) : base(ActualPath)
        {
            this.OriginPath = OriginPath;
            ModifiedTimeRaw = CreateTime.ToLocalTime();
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
                return await Exclusive.Controller.RestoreItemInRecycleBinAsync(Path).ConfigureAwait(true);
            }
        }
    }
}
