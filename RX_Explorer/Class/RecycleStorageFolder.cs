using RX_Explorer.Interface;
using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;

namespace RX_Explorer.Class
{
    public class RecycleStorageFolder : FileSystemStorageFolder, IRecycleStorageItem
    {
        public string OriginPath { get; }

        public DateTimeOffset RecycleDate { get; }

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

        public override ulong Size { get; protected set; }

        public override async Task DeleteAsync(bool PermanentDelete, bool SkipOperationRecord = false, CancellationToken CancelToken = default, ProgressChangedEventHandler ProgressHandler = null)
        {
            if (!PermanentDelete)
            {
                throw new NotSupportedException("Recycled item do not support non-permanent deletion");
            }

            await DeleteAsync();
        }

        public async Task<bool> DeleteAsync()
        {
            using (AuxiliaryTrustProcessController.Exclusive Exclusive = await AuxiliaryTrustProcessController.GetControllerExclusiveAsync())
            {
                return await Exclusive.Controller.DeleteItemInRecycleBinAsync(Path);
            }
        }

        public async Task<bool> RestoreAsync()
        {
            using (AuxiliaryTrustProcessController.Exclusive Exclusive = await AuxiliaryTrustProcessController.GetControllerExclusiveAsync())
            {
                return await Exclusive.Controller.RestoreItemInRecycleBinAsync(OriginPath);
            }
        }

        public RecycleStorageFolder(NativeFileData Data, string OriginPath, ulong Size, DateTimeOffset RecycleDate) : base(Data)
        {
            this.OriginPath = OriginPath;
            this.RecycleDate = RecycleDate.ToLocalTime();
            this.Size = Size;
        }
    }
}
