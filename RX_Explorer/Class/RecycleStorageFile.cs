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

        public override string Name => System.IO.Path.GetFileName(OriginPath);

        public override string DisplayName => Name;

        public override string Type => ((StorageItem as StorageFile)?.FileType) ?? System.IO.Path.GetExtension(OriginPath).ToUpper();

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

        public RecycleStorageFile(StorageFile File, string OriginPath, DateTimeOffset DeleteTime) : base(File)
        {
            this.OriginPath = OriginPath;
            ModifiedTime = DeleteTime.ToLocalTime();
        }

        public RecycleStorageFile(Win32_File_Data Data, string OriginPath, DateTimeOffset DeleteTime) : base(Data)
        {
            this.OriginPath = OriginPath;
            ModifiedTime = DeleteTime.ToLocalTime();
        }

        public async Task<bool> DeleteAsync()
        {
            using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableControllerAsync())
            {
                return await Exclusive.Controller.DeleteItemInRecycleBinAsync(Path);
            }
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
