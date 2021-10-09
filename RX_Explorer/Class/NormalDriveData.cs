using System.Collections.Generic;
using System.IO;
using Windows.Storage;

namespace RX_Explorer.Class
{
    public class NormalDriveData : DriveDataBase
    {
        public NormalDriveData(StorageFolder Device, IDictionary<string, object> PropertiesRetrieve, DriveType DriveType, string DriveId = null) : base(Device, PropertiesRetrieve, DriveType, DriveId)
        {

        }
    }
}
