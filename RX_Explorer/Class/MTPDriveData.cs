using ShareClassLibrary;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace RX_Explorer.Class
{
    public class MTPDriveData : NormalDriveData
    {
        public override string Path => DriveId;

        public override ulong TotalByte => (SizeData?.TotalByte).GetValueOrDefault();

        public override ulong FreeByte => (SizeData?.FreeByte).GetValueOrDefault();

        private MTPDriveSizeData SizeData;

        public MTPDriveData(FileSystemStorageFolder Drive, IReadOnlyDictionary<string, string> PropertiesRetrieve, string DriveId = null) : base(Drive, PropertiesRetrieve, DriveType.Removable, DriveId)
        {

        }

        protected override async Task LoadCoreAsync()
        {
            using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableControllerAsync())
            {
                SizeData = await Exclusive.Controller.GetMTPDriveSizeAsync(Path);
            }

            OnPropertyChanged(nameof(DriveSpaceDescription));
        }
    }
}
