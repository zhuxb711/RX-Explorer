using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace RX_Explorer.Class
{
    public class LockedDriveData : DriveDataBase
    {
        public async Task<bool> UnlockAsync(string Password)
        {
            using (FullTrustProcessController.Exclusive Exclusive = await FullTrustProcessController.GetControllerExclusiveAsync())
            {
                return await Exclusive.Controller.RunAsync("powershell.exe",
                                                           RunAsAdmin: true,
                                                           CreateNoWindow: true,
                                                           ShouldWaitForExit: true,
                                                           Parameters: new string[] { "-Command", $"$BitlockerSecureString = ConvertTo-SecureString '{Password}' -AsPlainText -Force;Unlock-BitLocker -MountPoint '{DriveFolder.Path}' -Password $BitlockerSecureString;Start-Sleep -Seconds 1" });
            }
        }

        protected override Task LoadCoreAsync()
        {
            return Task.CompletedTask;
        }

        public LockedDriveData(FileSystemStorageFolder Drive, IReadOnlyDictionary<string, string> PropertiesRetrieve, DriveType DriveType, string DriveId = null) : base(Drive, PropertiesRetrieve, DriveType, DriveId)
        {

        }
    }
}
