using RX_Explorer.Interface;
using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;

namespace RX_Explorer.Class
{
    public class RecycleStorageFolder : FileSystemStorageFolder, IRecycleStorageItem
    {
        public string OriginPath { get; }

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

        public override async Task DeleteAsync(bool PermanentDelete, CancellationToken CancelToken = default, ProgressChangedEventHandler ProgressHandler = null)
        {
            using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableControllerAsync())
            {
                if (!await Exclusive.Controller.DeleteItemInRecycleBinAsync(Path))
                {
                    throw new Exception();
                }
            }
        }

        public RecycleStorageFolder(NativeFileData Data, string OriginPath, DateTimeOffset DeleteTime) : base(Data)
        {
            this.OriginPath = OriginPath;
            ModifiedTime = DeleteTime.ToLocalTime();
        }

        public async Task<bool> RestoreAsync()
        {
            using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableControllerAsync())
            {
                return await Exclusive.Controller.RestoreItemInRecycleBinAsync(OriginPath);
            }
        }
    }
}
