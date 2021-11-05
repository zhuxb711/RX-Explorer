using System.Collections.Generic;
using System.IO;

namespace RX_Explorer.Class
{
    public class NormalDriveData : DriveDataBase
    {
        public NormalDriveData(FileSystemStorageFolder Drive, IDictionary<string, object> PropertiesRetrieve, DriveType DriveType, string DriveId = null) : base(Drive, PropertiesRetrieve, DriveType, DriveId)
        {

        }
    }
}
