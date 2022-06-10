using RX_Explorer.Interface;
using System;
using System.ComponentModel;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;

namespace RX_Explorer.Class
{
    public sealed class RecycleStorageFile : FileSystemStorageFile, IRecycleStorageItem
    {
        public string OriginPath { get; }

        public override string Name => System.IO.Path.GetFileName(OriginPath);

        public override string DisplayName => Name;

        public override string DisplayType => ((StorageItem as StorageFile)?.DisplayType) ?? (string.IsNullOrEmpty(InnerDisplayType) ? Type : InnerDisplayType);

        public override string Type => ((StorageItem as StorageFile)?.FileType) ?? System.IO.Path.GetExtension(OriginPath).ToUpper();

        private string InnerDisplayType;

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

        protected override async Task LoadCoreAsync(bool ForceUpdate)
        {
            if (Regex.IsMatch(Name, @"\.(lnk|url)$", RegexOptions.IgnoreCase))
            {
                if (GetBulkAccessSharedController(out var ControllerRef))
                {
                    using (ControllerRef)
                    {
                        InnerDisplayType = await ControllerRef.Value.Controller.GetFriendlyTypeNameAsync(Type);
                    }
                }
                else
                {
                    using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableControllerAsync())
                    {
                        InnerDisplayType = await Exclusive.Controller.GetFriendlyTypeNameAsync(Type);
                    }
                }
            }

            await base.LoadCoreAsync(ForceUpdate);
        }

        protected override Task<IStorageItem> GetStorageItemCoreAsync(bool ForceUpdate)
        {
            if (Regex.IsMatch(Name, @"\.(lnk|url)$", RegexOptions.IgnoreCase))
            {
                return Task.FromResult<IStorageItem>(null);
            }
            else
            {
                return base.GetStorageItemCoreAsync(ForceUpdate);
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

        public RecycleStorageFile(StorageFile File, string OriginPath, DateTimeOffset DeleteTime) : base(File)
        {
            this.OriginPath = OriginPath;
            ModifiedTime = DeleteTime.ToLocalTime();
        }

        public RecycleStorageFile(NativeFileData Data, string OriginPath, DateTimeOffset DeleteTime) : base(Data)
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
