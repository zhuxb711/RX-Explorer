using ShareClassLibrary;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.UI.Xaml.Media.Imaging;

namespace RX_Explorer.Class
{
    public class MTPDriveData : NormalDriveData
    {
        public override string Name => string.IsNullOrEmpty(SizeData?.Name) ? base.Name : SizeData.Name;

        public override string DisplayName => Name;

        public override string Path => DriveId;

        public override ulong TotalByte => (SizeData?.TotalByte).GetValueOrDefault();

        public override ulong FreeByte => (SizeData?.FreeByte).GetValueOrDefault();

        public override string FileSystem => string.IsNullOrEmpty(SizeData?.FileSystem) ? base.FileSystem : SizeData.FileSystem;

        private MTPDriveVolumnData SizeData;

        public MTPDriveData(FileSystemStorageFolder Drive, IReadOnlyDictionary<string, string> PropertiesRetrieve, string DriveId = null) : base(Drive, PropertiesRetrieve, DriveType.Removable, DriveId)
        {

        }

        protected override async Task<BitmapImage> GetThumbnailCoreAsync()
        {
            if (await DriveFolder.GetStorageItemAsync() is IStorageItem Item)
            {
                return await Item.GetThumbnailBitmapAsync(ThumbnailMode.SingleItem);
            }

            return null;
        }

        protected override async Task LoadCoreAsync()
        {
            using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableControllerAsync())
            {
                SizeData = await Exclusive.Controller.GetMTPDriveSizeAsync(Path);
            }
        }
    }
}
