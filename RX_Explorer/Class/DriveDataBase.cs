using RX_Explorer.Interface;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
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
    public abstract class DriveDataBase : INotifyPropertyChanged, IDriveData, IEquatable<DriveDataBase>
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
        public FileSystemStorageFolder DriveFolder { get; }

        public virtual string Name => (DriveFolder?.Name) ?? string.Empty;
        /// <summary>
        /// 驱动器名称
        /// </summary>
        public virtual string DisplayName => (DriveFolder?.DisplayName) ?? string.Empty;

        public virtual string Path => (DriveFolder?.Path) ?? string.Empty;

        public virtual string FileSystem { get; } = Globalization.GetString("UnknownText");

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
                    return TotalByte.GetSizeDescription();
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
                    return FreeByte.GetSizeDescription();
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
                    return UsedByte.GetSizeDescription();
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
        public virtual ulong TotalByte { get; }

        /// <summary>
        /// 空闲字节数
        /// </summary>
        public virtual ulong FreeByte { get; }

        public ulong UsedByte => TotalByte - FreeByte;

        /// <summary>
        /// 存储空间描述
        /// </summary>
        public string DriveSpaceDescription => $"{FreeSpace} {Globalization.GetString("Disk_Capacity_Description")} {Capacity}";

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

        private int IsContentLoaded;

        public static async Task<DriveDataBase> CreateAsync(DriveType DriveType, string DriveId)
        {
            StorageFolder DriveFolder = await Task.Run(() => StorageDevice.FromId(DriveId));

            if (string.IsNullOrEmpty(DriveFolder.Path))
            {
                if (await FileSystemStorageItemBase.OpenAsync(DriveId) is FileSystemStorageFolder Folder)
                {
                    return await CreateAsync(DriveType, Folder, DriveId);
                }
            }
            else if (System.IO.Path.IsPathRooted(DriveFolder.Path))
            {
                return await CreateAsync(DriveType, new FileSystemStorageFolder(DriveFolder), DriveId);
            }

            return null;
        }

        public static async Task<DriveDataBase> CreateAsync(DriveInfo Info)
        {
            if (await FileSystemStorageItemBase.OpenAsync(Info.Name) is FileSystemStorageFolder Folder)
            {
                return await CreateAsync(Info.DriveType, Folder);
            }
            else
            {
                throw new DirectoryNotFoundException();
            }
        }

        public static async Task<DriveDataBase> CreateAsync(DriveType DriveType, StorageFolder DriveFolder)
        {
            if (string.IsNullOrEmpty(DriveFolder.Path))
            {
                throw new ArgumentNullException(nameof(DriveFolder.Path), "Path is invalid and please use DriveId to create it instead");
            }

            return await CreateAsync(DriveType, new FileSystemStorageFolder(DriveFolder));
        }

        private static async Task<DriveDataBase> CreateAsync(DriveType DriveType, FileSystemStorageFolder DriveFolder, string DriveId = null)
        {
            if (DriveFolder.Path.StartsWith(@"\\wsl", StringComparison.OrdinalIgnoreCase))
            {
                return new WslDriveData(DriveFolder, DriveId);
            }
            else if (DriveFolder.Path.TrimEnd('\\').Equals(DriveId, StringComparison.OrdinalIgnoreCase))
            {
                return new MTPDriveData(DriveFolder, DriveId);
            }
            else
            {
                IReadOnlyDictionary<string, string> PropertiesRetrieve = await DriveFolder.GetPropertiesAsync(new string[] { "System.Capacity", "System.FreeSpace", "System.Volume.FileSystem", "System.Volume.BitLockerProtection" });

                if (PropertiesRetrieve.TryGetValue("System.Volume.BitLockerProtection", out string BitlockerStateRaw) && !string.IsNullOrEmpty(BitlockerStateRaw))
                {
                    if (Convert.ToInt32(BitlockerStateRaw) == 6)
                    {
                        return new LockedDriveData(DriveFolder, PropertiesRetrieve, DriveType, DriveId);
                    }
                }

                return new NormalDriveData(DriveFolder, PropertiesRetrieve, DriveType, DriveId);
            }
        }

        public async Task LoadAsync()
        {
            if (Interlocked.CompareExchange(ref IsContentLoaded, 1, 0) == 0)
            {
                try
                {
                    if (CoreApplication.MainView.CoreWindow.Dispatcher.HasThreadAccess)
                    {
                        await Task.WhenAll(LoadCoreAsync(), GetThumbnailAsync());
                    }
                    else
                    {
                        await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, async () =>
                        {
                            await Task.WhenAll(LoadCoreAsync(), GetThumbnailAsync());
                        });
                    }
                }
                catch (Exception ex)
                {
                    LogTracer.Log(ex, $"Could not load the DriveDataBase on path: {Path}");
                }
                finally
                {
                    OnPropertyChanged(nameof(Thumbnail));
                    OnPropertyChanged(nameof(DisplayName));
                    OnPropertyChanged(nameof(Percent));
                    OnPropertyChanged(nameof(DriveSpaceDescription));
                }
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

            if (Thumbnail == null)
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

        protected void OnPropertyChanged([CallerMemberName] string PropertyName = null)
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

        protected DriveDataBase(FileSystemStorageFolder DriveFolder, DriveType DriveType, string DriveId = null)
        {
            this.DriveFolder = DriveFolder ?? throw new ArgumentNullException(nameof(DriveFolder), "Argument could not be null");
            this.DriveType = DriveType;
            this.DriveId = DriveId;
        }

        protected DriveDataBase(FileSystemStorageFolder DriveFolder, IReadOnlyDictionary<string, string> PropertiesRetrieve, DriveType DriveType, string DriveId = null) : this(DriveFolder, DriveType, DriveId)
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

                if (PropertiesRetrieve.TryGetValue("System.Volume.BitLockerProtection", out string BitlockerStateCodeRaw) && !string.IsNullOrEmpty(BitlockerStateCodeRaw))
                {
                    BitlockerStatusCode = Convert.ToInt32(BitlockerStateCodeRaw);
                }
            }
        }
    }
}
