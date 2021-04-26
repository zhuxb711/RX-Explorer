using System.Collections.Generic;
using Windows.Storage;
using Windows.UI.Xaml.Media.Imaging;

namespace RX_Explorer.Class
{
    public class WslDriveData : DriveDataBase
    {
        public WslDriveData(StorageFolder Device, BitmapImage Thumbnail, IDictionary<string, object> PropertiesRetrieve) : base(Device, Thumbnail, PropertiesRetrieve, System.IO.DriveType.Network)
        {

        }
    }
}
