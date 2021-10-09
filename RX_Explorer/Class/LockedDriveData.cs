using ShareClassLibrary;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.UI.Xaml.Media.Imaging;

namespace RX_Explorer.Class
{
    public class LockedDriveData : NormalDriveData
    {
        public async Task<bool> UnlockAsync(string Password)
        {
            using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableController())
            {
                return await Exclusive.Controller.RunAsync("powershell.exe", string.Empty, WindowState.Normal, true, true, true, "-Command", $"$BitlockerSecureString = ConvertTo-SecureString '{Password}' -AsPlainText -Force;", $"Unlock-BitLocker -MountPoint '{DriveFolder.Path}' -Password $BitlockerSecureString");
            }
        }

        public LockedDriveData(StorageFolder Device, IDictionary<string, object> PropertiesRetrieve, DriveType DriveType, string DriveId = null) : base(Device, PropertiesRetrieve, DriveType, DriveId)
        {

        }
    }
}
