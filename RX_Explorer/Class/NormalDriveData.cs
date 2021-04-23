using System.Collections.Generic;
using System.IO;
using Windows.Storage;
using Windows.UI.Xaml.Media.Imaging;

namespace RX_Explorer.Class
{
    public class NormalDriveData : DriveDataBase
    {
        public NormalDriveData(StorageFolder Device, BitmapImage Thumbnail, IDictionary<string, object> PropertiesRetrieve, DriveType DriveType) : base(Device, Thumbnail, PropertiesRetrieve, DriveType)
        {

        }
    }
}
