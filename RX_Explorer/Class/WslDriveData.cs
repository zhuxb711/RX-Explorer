using System.Collections.Generic;

namespace RX_Explorer.Class
{
    public class WslDriveData : DriveDataBase
    {
        public WslDriveData(FileSystemStorageFolder Drive, IDictionary<string, object> PropertiesRetrieve, string DriveId = null) : base(Drive, PropertiesRetrieve, System.IO.DriveType.Network, DriveId)
        {

        }
    }
}
