using PropertyChanged;
using RX_Explorer.Interface;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Windows.ApplicationModel.Core;
using Windows.Devices.Enumeration;
using Windows.Devices.Portable;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.UI.Core;
using Windows.UI.Xaml.Media.Imaging;

namespace RX_Explorer.Class
{
    [AddINotifyPropertyChangedInterface]
    public abstract partial class DriveDataBase : IDriveData, IEquatable<DriveDataBase>
    {
        private static readonly Uri SystemDriveIconUri = new Uri("ms-appx:///Assets/SystemDrive.ico");
        private static readonly Uri SystemDriveUnLockedIconUri = new Uri("ms-appx:///Assets/SystemDriveUnLocked.ico");
        private static readonly Uri NormalDriveIconUri = new Uri("ms-appx:///Assets/NormalDrive.ico");
        private static readonly Uri NormalDriveLockedIconUri = new Uri("ms-appx:///Assets/NormalDriveLocked.ico");
        private static readonly Uri NormalDriveUnLockedIconUri = new Uri("ms-appx:///Assets/NormalDriveUnLocked.ico");
        private static readonly Uri NetworkDriveIconUri = new Uri("ms-appx:///Assets/NetworkDrive.ico");

        public BitmapImage Thumbnail { get; private set; }

        public FileSystemStorageFolder DriveFolder { get; }

        public DriveType DriveType { get; private set; }

        public virtual string Name
        {
            get
            {
                string Name = Regex.Replace((DriveFolder?.DisplayName) ?? string.Empty, $@"\({Regex.Escape(Path.TrimEnd('\\'))}\)$", string.Empty).Trim();

                if (string.IsNullOrEmpty(Name))
                {
                    return (DriveFolder?.Name) ?? string.Empty;
                }
                else
                {
                    return Name;
                }
            }
        }

        public virtual string DisplayName => (DriveFolder?.DisplayName) ?? string.Empty;

        public virtual string Path => (DriveFolder?.Path) ?? string.Empty;

        public virtual string FileSystem { get; }

        public virtual ulong TotalByte { get; }

        public virtual ulong FreeByte { get; }

        public ulong UsedByte => TotalByte - FreeByte;

        public string DeviceId { get; }

        public string DriveSpaceDescription => $"{FreeSpace} {Globalization.GetString("Disk_Capacity_Description")} {Capacity}";

        public double Percent
        {
            get
            {
                if (TotalByte > 0)
                {
                    return 1 - FreeByte / Convert.ToDouble(TotalByte);
                }

                return 0;
            }
        }

        public string Capacity
        {
            get
            {
                if (TotalByte > 0)
                {
                    return TotalByte.GetFileSizeDescription();
                }
                else
                {
                    return Globalization.GetString("UnknownText");
                }
            }
        }

        public string FreeSpace
        {
            get
            {
                if (FreeByte > 0)
                {
                    return FreeByte.GetFileSizeDescription();
                }
                else
                {
                    return Globalization.GetString("UnknownText");
                }
            }
        }

        public string UsedSpace
        {
            get
            {
                if (UsedByte > 0)
                {
                    return UsedByte.GetFileSizeDescription();
                }
                else
                {
                    return Globalization.GetString("UnknownText");
                }
            }
        }

        /*
                 * | System.Volume.      | Control Panel                    | manage-bde conversion     | manage-bde     | Get-BitlockerVolume          | Get-BitlockerVolume |
                 * | BitLockerProtection |                                  |                           | protection     | VolumeStatus                 | ProtectionStatus    |
                 * | ------------------- | -------------------------------- | ------------------------- | -------------- | ---------------------------- | ------------------- |
                 * |                   1 | BitLocker on                     | Used Space Only Encrypted | Protection On  | FullyEncrypted               | On                  |
                 * |                   1 | BitLocker on                     | Fully Encrypted           | Protection On  | FullyEncrypted               | On                  |
                 * |                   1 | BitLocker on                     | Fully Encrypted           | Protection On  | FullyEncryptedWipeInProgress | On                  |
                 * |                   2 | BitLocker off                    | Fully Decrypted           | Protection Off | FullyDecrypted               | Off                 |
                 * |                   3 | BitLocker Encrypting             | Encryption In Progress    | Protection Off | EncryptionInProgress         | Off                 |
                 * |                   3 | BitLocker Encryption Paused      | Encryption Paused         | Protection Off | EncryptionSuspended          | Off                 |
                 * |                   4 | BitLocker Decrypting             | Decryption in progress    | Protection Off | DecyptionInProgress          | Off                 |
                 * |                   4 | BitLocker Decryption Paused      | Decryption Paused         | Protection Off | DecryptionSuspended          | Off                 |
                 * |                   5 | BitLocker suspended              | Used Space Only Encrypted | Protection Off | FullyEncrypted               | Off                 |
                 * |                   5 | BitLocker suspended              | Fully Encrypted           | Protection Off | FullyEncrypted               | Off                 |
                 * |                   6 | BitLocker on (Locked)            | Unknown                   | Unknown        | $null                        | Unknown             |
                 * |                   7 |                                  |                           |                |                              |                     |
                 * |                   8 | BitLocker waiting for activation | Used Space Only Encrypted | Protection Off | FullyEncrypted               | Off                 |
                 * 
                 * We could use Powershell command: Get-BitLockerVolume -MountPoint C: | Select -ExpandProperty LockStatus -------------->Locked / Unlocked
                 * But powershell might speed too much time to load. So we would not use it
        */
        public int BitlockerStatusCode { get; }

        public static async Task<DriveDataBase> CreateAsync(DriveType DriveType, DeviceInformation DeviceInfo)
        {
            try
            {
                StorageFolder DriveFolder = await Task.Run(() => StorageDevice.FromId(DeviceInfo.Id));

                if (string.IsNullOrEmpty(DriveFolder.Path))
                {
                    if (await FileSystemStorageItemBase.OpenAsync(DeviceInfo.Id) is FileSystemStorageFolder Folder)
                    {
                        return await CreateAsync(DriveType, Folder, DeviceInfo);
                    }
                }
                else if (System.IO.Path.IsPathRooted(DriveFolder.Path))
                {
                    return await CreateAsync(DriveType, new FileSystemStorageFolder(await DriveFolder.GetNativeFileDataAsync()), DeviceInfo);
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"Could not create {nameof(DriveDataBase)} from {nameof(DeviceInformation)}");
            }

            return null;
        }

        public static async Task<DriveDataBase> CreateAsync(DriveInfo Info)
        {
            if (await FileSystemStorageItemBase.OpenAsync(Info.Name) is FileSystemStorageFolder Folder)
            {
                return await CreateAsync(Info.DriveType, Folder);
            }

            return null;
        }

        public static async Task<DriveDataBase> CreateAsync(DriveDataBase Drive)
        {
            if (await FileSystemStorageItemBase.OpenAsync(Drive.Path) is FileSystemStorageFolder Folder)
            {
                return await CreateAsync(Drive.DriveType, Folder, string.IsNullOrEmpty(Drive.DeviceId) ? null : await DeviceInformation.CreateFromIdAsync(Drive.DeviceId));
            }

            return null;
        }

        public static async Task<DriveDataBase> CreateAsync(DriveType DriveType, string DeviceId)
        {
            if (string.IsNullOrEmpty(DeviceId))
            {
                throw new ArgumentNullException(nameof(DeviceId));
            }

            return await CreateAsync(DriveType, await DeviceInformation.CreateFromIdAsync(DeviceId));
        }

        public static async Task<DriveDataBase> CreateAsync(DriveType DriveType, FileSystemStorageFolder DriveFolder)
        {
            return await CreateAsync(DriveType, DriveFolder, null);
        }

        private static async Task<DriveDataBase> CreateAsync(DriveType DriveType, FileSystemStorageFolder DriveFolder, DeviceInformation DeviceInfo)
        {
            try
            {
                if (DriveFolder.Path.StartsWith(@"\\wsl", StringComparison.OrdinalIgnoreCase))
                {
                    return new WslDriveData(DriveFolder, DeviceInfo?.Id);
                }
                else if (DriveFolder.Path.TrimEnd('\\').Equals(DeviceInfo?.Id, StringComparison.OrdinalIgnoreCase))
                {
                    return new MTPDriveData(DriveFolder, DeviceInfo?.Id);
                }
                else
                {
                    string[] Properties = new string[] { "System.Capacity", "System.FreeSpace", "System.Volume.FileSystem", "System.Volume.BitLockerProtection" };

                    Dictionary<string, string> DeviceProperties = new Dictionary<string, string>(Properties.Length);

                    if (DeviceInfo != null)
                    {
                        foreach (string Property in Properties)
                        {
                            if (DeviceInfo.Properties.TryGetValue(Property, out object Value) && !string.IsNullOrEmpty(Convert.ToString(Value)))
                            {
                                DeviceProperties.Add(Property, Convert.ToString(Value));
                            }
                        }
                    }

                    DeviceProperties.AddRange(await DriveFolder.GetPropertiesAsync(Properties.Except(DeviceProperties.Keys)));

                    if (DeviceProperties.TryGetValue("System.Volume.BitLockerProtection", out string BitlockerStateRaw) && !string.IsNullOrEmpty(BitlockerStateRaw))
                    {
                        if (Convert.ToInt32(BitlockerStateRaw) == 6)
                        {
                            return new LockedDriveData(DriveFolder, DeviceProperties, DriveType, DeviceInfo?.Id);
                        }
                    }

                    return new NormalDriveData(DriveFolder, DeviceProperties, DriveType, DeviceInfo?.Id);
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"Could not create the {nameof(DriveDataBase)} from {nameof(FileSystemStorageFolder)} and {nameof(DeviceInformation)}");
            }

            return null;
        }

        public async Task LoadAsync()
        {
            try
            {
                await CoreApplication.MainView.CoreWindow.Dispatcher.RunAndWaitAsyncTask(CoreDispatcherPriority.Low, async () =>
                {
                    await Execution.ExecuteOnceAsync(this, () => Task.WhenAll(LoadCoreAsync(), GetThumbnailAsync()));
                });
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"Could not load the DriveDataBase on path: {Path}");
            }
        }

        protected abstract Task LoadCoreAsync();

        public async Task<BitmapImage> GetThumbnailAsync()
        {
            return Thumbnail ??= await GetThumbnailCoreAsync();
        }

        protected virtual async Task<BitmapImage> GetThumbnailCoreAsync()
        {
            BitmapImage Thumbnail = await DriveFolder.GetThumbnailAsync(ThumbnailMode.SingleItem);

            if (!string.IsNullOrEmpty(Thumbnail?.UriSource?.AbsoluteUri))
            {
                switch (BitlockerStatusCode)
                {
                    case -1:
                        {
                            if (System.IO.Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.Windows)).Equals(Path, StringComparison.OrdinalIgnoreCase))
                            {
                                Thumbnail = new BitmapImage(SystemDriveIconUri);
                            }
                            else if (DriveType == DriveType.Network)
                            {
                                Thumbnail = new BitmapImage(NetworkDriveIconUri);
                            }
                            else
                            {
                                Thumbnail = new BitmapImage(NormalDriveIconUri);
                            }

                            break;
                        }
                    case 6:
                        {
                            Thumbnail = new BitmapImage(NormalDriveLockedIconUri);
                            break;
                        }
                    case 3:
                    case 2:
                        {
                            if (System.IO.Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.Windows)).Equals(Path, StringComparison.OrdinalIgnoreCase))
                            {
                                Thumbnail = new BitmapImage(SystemDriveIconUri);
                            }
                            else
                            {
                                Thumbnail = new BitmapImage(NormalDriveIconUri);
                            }

                            break;
                        }
                    default:
                        {
                            if (System.IO.Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.Windows)).Equals(Path, StringComparison.OrdinalIgnoreCase))
                            {
                                Thumbnail = new BitmapImage(SystemDriveUnLockedIconUri);
                            }
                            else
                            {
                                Thumbnail = new BitmapImage(NormalDriveUnLockedIconUri);
                            }

                            break;
                        }
                }
            }

            return Thumbnail;
        }

        public bool Equals(DriveDataBase other)
        {
            if (other == null)
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }
            else
            {
                if (!string.IsNullOrEmpty(DeviceId) && !string.IsNullOrEmpty(other.DeviceId))
                {
                    return DeviceId.Equals(other.DeviceId, StringComparison.OrdinalIgnoreCase);
                }
                else
                {
                    return Path.Equals(other.Path, StringComparison.OrdinalIgnoreCase);
                }
            }
        }

        public override bool Equals(object obj)
        {
            return obj is DriveDataBase Item && Equals(Item);
        }

        public override int GetHashCode()
        {
            return Path.GetHashCode();
        }

        public override string ToString()
        {
            return $"Path: {Path}, DriveId: {DeviceId ?? "<None>"}";
        }

        public static bool operator ==(DriveDataBase left, DriveDataBase right)
        {
            if (left is null)
            {
                return right is null;
            }
            else
            {
                if (right is null)
                {
                    return false;
                }
                else
                {
                    if (!string.IsNullOrEmpty(left.DeviceId) && !string.IsNullOrEmpty(right.DeviceId))
                    {
                        return left.DeviceId.Equals(right.DeviceId, StringComparison.OrdinalIgnoreCase);
                    }
                    else
                    {
                        return left.Path.Equals(right.Path, StringComparison.OrdinalIgnoreCase);
                    }
                }
            }
        }

        public static bool operator !=(DriveDataBase left, DriveDataBase right)
        {
            if (left is null)
            {
                return right is not null;
            }
            else
            {
                if (right is null)
                {
                    return true;
                }
                else
                {
                    if (!string.IsNullOrEmpty(left.DeviceId) && !string.IsNullOrEmpty(right.DeviceId))
                    {
                        return !left.DeviceId.Equals(right.DeviceId, StringComparison.OrdinalIgnoreCase);
                    }
                    else
                    {
                        return !left.Path.Equals(right.Path, StringComparison.OrdinalIgnoreCase);
                    }
                }
            }
        }

        protected DriveDataBase(FileSystemStorageFolder DriveFolder, DriveType DriveType, string DeviceId = null)
        {
            this.DriveFolder = DriveFolder ?? throw new ArgumentNullException(nameof(DriveFolder), "Argument could not be null");
            this.DriveType = DriveType;
            this.DeviceId = DeviceId;
        }

        protected DriveDataBase(FileSystemStorageFolder DriveFolder, IReadOnlyDictionary<string, string> PropertiesRetrieve, DriveType DriveType, string DeviceId = null) : this(DriveFolder, DriveType, DeviceId)
        {
            if (PropertiesRetrieve != null)
            {
                if (PropertiesRetrieve.TryGetValue("System.Capacity", out string TotalByteRaw) && !string.IsNullOrEmpty(TotalByteRaw))
                {
                    TotalByte = Convert.ToUInt64(TotalByteRaw);
                }

                if (PropertiesRetrieve.TryGetValue("System.FreeSpace", out string FreeByteRaw) && !string.IsNullOrEmpty(FreeByteRaw))
                {
                    FreeByte = Convert.ToUInt64(FreeByteRaw);
                }

                if (PropertiesRetrieve.TryGetValue("System.Volume.FileSystem", out string FileSystemRaw) && !string.IsNullOrEmpty(FileSystemRaw))
                {
                    FileSystem = FileSystemRaw;
                }
                else
                {
                    FileSystem = Globalization.GetString("UnknownText");
                }

                if (PropertiesRetrieve.TryGetValue("System.Volume.BitLockerProtection", out string BitlockerStateCodeRaw) && !string.IsNullOrEmpty(BitlockerStateCodeRaw))
                {
                    BitlockerStatusCode = Convert.ToInt32(BitlockerStateCodeRaw);
                }
                else
                {
                    BitlockerStatusCode = -1;
                }
            }
        }
    }
}
