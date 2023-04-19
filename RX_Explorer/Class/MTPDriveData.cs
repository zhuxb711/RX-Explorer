using PropertyChanged;
using SharedLibrary;
using System;
using System.IO;
using System.Threading.Tasks;
using Windows.Devices.Portable;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.UI.Xaml.Media.Imaging;

namespace RX_Explorer.Class
{
    public class MTPDriveData : NormalDriveData
    {
        [DependsOn(nameof(RawData))]
        public override string Name => string.IsNullOrEmpty(RawData?.Name) ? base.Name : RawData.Name;

        public override string DisplayName => Name;

        public override string Path => DeviceId;

        [DependsOn(nameof(RawData))]
        public override ulong TotalByte => (RawData?.TotalByte).GetValueOrDefault();

        [DependsOn(nameof(RawData))]
        public override ulong FreeByte => (RawData?.FreeByte).GetValueOrDefault();

        [DependsOn(nameof(RawData))]
        public override string FileSystem => string.IsNullOrEmpty(RawData?.FileSystem) ? base.FileSystem : RawData.FileSystem;

        protected MTPDriveVolumnData RawData { get; private set; }

        public MTPDriveData(FileSystemStorageFolder Drive, string DriveId = null) : base(Drive, DriveType.Removable, DriveId)
        {

        }

        protected override async Task<BitmapImage> GetThumbnailCoreAsync()
        {
            try
            {
                StorageFolder Item = await Task.Run(() => StorageDevice.FromId(DriveFolder.Path));

                if (Item != null)
                {
                    return await Item.GetThumbnailBitmapAsync(ThumbnailMode.SingleItem);
                }
            }
            catch (Exception)
            {
                //No need to handle this exception
            }

            return null;
        }

        protected override async Task LoadCoreAsync()
        {
            using (AuxiliaryTrustProcessController.Exclusive Exclusive = await AuxiliaryTrustProcessController.GetControllerExclusiveAsync())
            {
                RawData = await Exclusive.Controller.GetMTPDriveVolumnDataAsync(Path);
            }
        }
    }
}
