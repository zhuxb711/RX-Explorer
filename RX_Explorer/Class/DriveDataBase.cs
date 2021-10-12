using RX_Explorer.Interface;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Windows.ApplicationModel.Core;
using Windows.Devices.Portable;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.UI.Core;
using Windows.UI.Xaml.Media.Imaging;

namespace RX_Explorer.Class
{
    /// <summary>
    /// 提供驱动器的界面支持
    /// </summary>
    public class DriveDataBase : INotifyPropertyChanged, IDriveData, IEquatable<DriveDataBase>
    {
        private static readonly Uri SystemDriveIconUri = new Uri("ms-appx:///Assets/SystemDrive.ico");
        private static readonly Uri SystemDriveUnLockedIconUri = new Uri("ms-appx:///Assets/SystemDriveUnLocked.ico");
        private static readonly Uri NormalDriveIconUri = new Uri("ms-appx:///Assets/NormalDrive.ico");
        private static readonly Uri NormalDriveLockedIconUri = new Uri("ms-appx:///Assets/NormalDriveLocked.ico");
        private static readonly Uri NormalDriveUnLockedIconUri = new Uri("ms-appx:///Assets/NormalDriveUnLocked.ico");
        private static readonly Uri NetworkDriveIconUri = new Uri("ms-appx:///Assets/NetworkDrive.ico");

        /// <summary>
        /// 驱动器缩略图
        /// </summary>
        public BitmapImage Thumbnail { get; private set; }

        /// <summary>
        /// 驱动器对象
        /// </summary>
        public StorageFolder DriveFolder { get; }

        public string Name
        {
            get
            {
                return (DriveFolder?.Name) ?? string.Empty;
            }
        }
        /// <summary>
        /// 驱动器名称
        /// </summary>
        public virtual string DisplayName
        {
            get
            {
                return (DriveFolder?.DisplayName) ?? string.Empty;
            }
        }

        public string Path
        {
            get
            {
                return (DriveFolder?.Path) ?? string.Empty;
            }
        }

        public string FileSystem { get; } = Globalization.GetString("UnknownText");

        /// <summary>
        /// 容量百分比
        /// </summary>
        public double Percent
        {
            get
            {
                if (TotalByte != 0)
                {
                    return 1 - FreeByte / Convert.ToDouble(TotalByte);
                }
                else
                {
                    return 0;
                }
            }
        }

        /// <summary>
        /// 总容量的描述
        /// </summary>
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

        /// <summary>
        /// 可用空间的描述
        /// </summary>
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

        /// <summary>
        /// 总字节数
        /// </summary>
        public ulong TotalByte { get; }

        /// <summary>
        /// 空闲字节数
        /// </summary>
        public ulong FreeByte { get; }

        /// <summary>
        /// 存储空间描述
        /// </summary>
        public string DriveSpaceDescription
        {
            get
            {
                return $"{FreeSpace} {Globalization.GetString("Disk_Capacity_Description")} {Capacity}";
            }
        }

        public DriveType DriveType { get; private set; }

        public string DriveId { get; }

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
        public int BitlockerStatusCode { get; } = -1;

        public event PropertyChangedEventHandler PropertyChanged;

        public static async Task<DriveDataBase> CreateAsync(DriveType DriveType, string DriveId)
        {
            return await CreateAsync(DriveType, await Task.Run(() => StorageDevice.FromId(DriveId)), DriveId);
        }

        public static async Task<DriveDataBase> CreateAsync(DriveInfo Info)
        {
            return await CreateAsync(Info.DriveType, await StorageFolder.GetFolderFromPathAsync(Info.Name));
        }

        public static Task<DriveDataBase> CreateAsync(DriveType DriveType, StorageFolder DriveFolder)
        {
            return CreateAsync(DriveType, DriveFolder, null);
        }

        private static async Task<DriveDataBase> CreateAsync(DriveType DriveType, StorageFolder DriveFolder, string DriveId = null)
        {
            BasicProperties Properties = await DriveFolder.GetBasicPropertiesAsync();

            IDictionary<string, object> PropertiesRetrieve = await Properties.RetrievePropertiesAsync(new string[] { "System.Capacity", "System.FreeSpace", "System.Volume.FileSystem", "System.Volume.BitLockerProtection" });

            if (DriveFolder.Path.StartsWith(@"\\wsl", StringComparison.OrdinalIgnoreCase))
            {
                return new WslDriveData(DriveFolder, PropertiesRetrieve, DriveId);
            }
            else
            {
                if (PropertiesRetrieve.TryGetValue("System.Volume.BitLockerProtection", out object BitlockerStateRaw) && BitlockerStateRaw is int BitlockerState)
                {
                    switch (BitlockerState)
                    {
                        case 6 when !PropertiesRetrieve.ContainsKey("System.Capacity") && !PropertiesRetrieve.ContainsKey("System.FreeSpace"):
                            {
                                return new LockedDriveData(DriveFolder, PropertiesRetrieve, DriveType, DriveId);
                            }
                        default:
                            {
                                return new NormalDriveData(DriveFolder, PropertiesRetrieve, DriveType, DriveId);
                            }
                    }
                }
                else
                {
                    return new NormalDriveData(DriveFolder, PropertiesRetrieve, DriveType, DriveId);
                }
            }
        }

        public async Task LoadAsync()
        {
            async void LocalLoadFunction()
            {
                Thumbnail = await GetThumbnailAsync();

                OnPropertyChanged(nameof(Thumbnail));
            }

            if (CoreApplication.MainView.CoreWindow.Dispatcher.HasThreadAccess)
            {
                LocalLoadFunction();
            }
            else
            {
                await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, LocalLoadFunction);
            }
        }

        private async Task<BitmapImage> GetThumbnailAsync()
        {
            switch (BitlockerStatusCode)
            {
                case -1:
                    {
                        BitmapImage Thumbnail = await DriveFolder.GetThumbnailBitmapAsync(ThumbnailMode.SingleItem);

                        if (Thumbnail == null)
                        {
                            if (System.IO.Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.Windows)).Equals(DriveFolder.Path, StringComparison.OrdinalIgnoreCase))
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
                        }

                        return Thumbnail;
                    }
                case 6:
                    {
                        return await DriveFolder.GetThumbnailBitmapAsync(ThumbnailMode.SingleItem) ?? new BitmapImage(NormalDriveLockedIconUri);
                    }
                case 3:
                case 2:
                    {
                        BitmapImage Thumbnail = await DriveFolder.GetThumbnailBitmapAsync(ThumbnailMode.SingleItem);

                        if (Thumbnail == null)
                        {
                            if (System.IO.Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.Windows)).Equals(DriveFolder.Path, StringComparison.OrdinalIgnoreCase))
                            {
                                Thumbnail = new BitmapImage(SystemDriveIconUri);
                            }
                            else
                            {
                                Thumbnail = new BitmapImage(NormalDriveIconUri);
                            }
                        }

                        return Thumbnail;
                    }
                default:
                    {
                        BitmapImage Thumbnail = await DriveFolder.GetThumbnailBitmapAsync(ThumbnailMode.SingleItem);

                        if (Thumbnail == null)
                        {
                            if (System.IO.Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.Windows)).Equals(DriveFolder.Path, StringComparison.OrdinalIgnoreCase))
                            {
                                Thumbnail = new BitmapImage(SystemDriveUnLockedIconUri);
                            }
                            else
                            {
                                Thumbnail = new BitmapImage(NormalDriveUnLockedIconUri);
                            }
                        }

                        return Thumbnail;
                    }
            }
        }

        private void OnPropertyChanged([CallerMemberName] string PropertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(PropertyName));
        }

        public bool Equals(DriveDataBase other)
        {
            if (ReferenceEquals(this, other))
            {
                return true;
            }
            else
            {
                if (other == null)
                {
                    return false;
                }
                else
                {
                    if (!string.IsNullOrEmpty(DriveId) && !string.IsNullOrEmpty(other.DriveId))
                    {
                        return DriveId.Equals(other.DriveId, StringComparison.OrdinalIgnoreCase);
                    }
                    else
                    {
                        return Path.Equals(other.Path, StringComparison.OrdinalIgnoreCase);
                    }
                }
            }
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(this, obj))
            {
                return true;
            }
            else
            {
                if (obj is DriveDataBase Item)
                {
                    if (!string.IsNullOrEmpty(DriveId) && !string.IsNullOrEmpty(Item.DriveId))
                    {
                        return DriveId.Equals(Item.DriveId, StringComparison.OrdinalIgnoreCase);
                    }
                    else
                    {
                        return Path.Equals(Item.Path, StringComparison.OrdinalIgnoreCase);
                    }
                }
                else
                {
                    return false;
                }
            }
        }

        public override int GetHashCode()
        {
            return Path.GetHashCode();
        }

        public override string ToString()
        {
            return $"Path: {Path}, DriveId: {DriveId ?? "<None>"}";
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
                    if (!string.IsNullOrEmpty(left.DriveId) && !string.IsNullOrEmpty(right.DriveId))
                    {
                        return left.DriveId.Equals(right.DriveId, StringComparison.OrdinalIgnoreCase);
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
                return right is object;
            }
            else
            {
                if (right is null)
                {
                    return true;
                }
                else
                {
                    if (!string.IsNullOrEmpty(left.DriveId) && !string.IsNullOrEmpty(right.DriveId))
                    {
                        return !left.DriveId.Equals(right.DriveId, StringComparison.OrdinalIgnoreCase);
                    }
                    else
                    {
                        return !left.Path.Equals(right.Path, StringComparison.OrdinalIgnoreCase);
                    }
                }
            }
        }

        /// <summary>
        /// 初始化DriveDataBase对象
        /// </summary>
        /// <param name="DriveFolder">驱动器文件夹</param>
        /// <param name="Thumbnail">缩略图</param>
        /// <param name="PropertiesRetrieve">额外信息</param>
        protected DriveDataBase(StorageFolder DriveFolder, IDictionary<string, object> PropertiesRetrieve, DriveType DriveType, string DriveId = null)
        {
            this.DriveFolder = DriveFolder ?? throw new FileNotFoundException();
            this.DriveType = DriveType;
            this.DriveId = DriveId;

            if (PropertiesRetrieve != null)
            {
                if (PropertiesRetrieve.TryGetValue("System.Capacity", out object TotalByteRaw) && TotalByteRaw is ulong TotalByte)
                {
                    this.TotalByte = TotalByte;
                }

                if (PropertiesRetrieve.TryGetValue("System.FreeSpace", out object FreeByteRaw) && FreeByteRaw is ulong FreeByte)
                {
                    this.FreeByte = FreeByte;
                }

                if (PropertiesRetrieve.TryGetValue("System.Volume.FileSystem", out object FileSystemRaw) && FileSystemRaw is string FileSystem)
                {
                    this.FileSystem = FileSystem;
                }

                if (PropertiesRetrieve.TryGetValue("System.Volume.BitLockerProtection", out object BitlockerStateCodeRaw) && BitlockerStateCodeRaw is int BitlockerStatusCode)
                {
                    this.BitlockerStatusCode = BitlockerStatusCode;
                }
            }
        }
    }
}
