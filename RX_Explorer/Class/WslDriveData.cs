using System.Collections.Generic;
using Windows.Storage;
using Windows.UI.Xaml.Media.Imaging;

namespace RX_Explorer.Class
{
    public class WslDriveData : DriveDataBase
    {
        public WslDriveData(StorageFolder Device, IDictionary<string, object> PropertiesRetrieve, string DriveId = null) : base(Device, PropertiesRetrieve, System.IO.DriveType.Network, DriveId)
        {

        }
    }
}
