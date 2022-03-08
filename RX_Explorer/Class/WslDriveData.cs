using System.Collections.Generic;
using System.Threading.Tasks;

namespace RX_Explorer.Class
{
    public class WslDriveData : DriveDataBase
    {
        public WslDriveData(FileSystemStorageFolder Drive, IReadOnlyDictionary<string, string> PropertiesRetrieve, string DriveId = null) : base(Drive, PropertiesRetrieve, System.IO.DriveType.Network, DriveId)
        {

        }

        protected override Task LoadCoreAsync()
        {
            return Task.CompletedTask;
        }
    }
}
