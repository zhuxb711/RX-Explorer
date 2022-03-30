using System.Threading.Tasks;

namespace RX_Explorer.Class
{
    public class WslDriveData : DriveDataBase
    {
        protected override Task LoadCoreAsync()
        {
            return Task.CompletedTask;
        }

        public WslDriveData(FileSystemStorageFolder Drive, string DriveId = null) : base(Drive, System.IO.DriveType.Network, DriveId)
        {

        }
    }
}
