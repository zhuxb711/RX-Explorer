using RX_Explorer.Interface;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Threading.Tasks;
using Windows.ApplicationModel.Core;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;

namespace RX_Explorer.Class
{
    /// <summary>
    /// 提供驱动器的界面支持
    /// </summary>
    public class DriveDataBase : INotifyPropertyChanged, IDriveData
    {
        /// <summary>
        /// 驱动器缩略图
        /// </summary>
        public virtual BitmapImage Thumbnail { get; }

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
        /// 容量显示条对可用空间不足的情况转换颜色
        /// </summary>
        public SolidColorBrush ProgressBarForeground
        {
            get
            {
                if (Percent >= 0.9)
                {
                    return new SolidColorBrush(Colors.Red);
                }
                else
                {
                    return progressBarForeground;
                }
            }
            protected set
            {
                progressBarForeground = value;
            }
        }

        private SolidColorBrush progressBarForeground = new SolidColorBrush((Color)Application.Current.Resources["SystemAccentColor"]);

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

        private readonly UISettings UIS;

        public event PropertyChangedEventHandler PropertyChanged;

        public static async Task<DriveDataBase> CreateAsync(StorageFolder Drive, DriveType DriveType)
        {
            BasicProperties Properties = await Drive.GetBasicPropertiesAsync();

            BitmapImage Thumbnail = await Drive.GetThumbnailBitmapAsync(ThumbnailMode.SingleItem);
            IDictionary<string, object> PropertiesRetrieve = await Properties.RetrievePropertiesAsync(new string[] { "System.Capacity", "System.FreeSpace", "System.Volume.FileSystem", "System.Volume.BitLockerProtection" });

            if (Drive.Path.StartsWith(@"\\wsl", StringComparison.OrdinalIgnoreCase))
            {
                return new WslDriveData(Drive, Thumbnail, PropertiesRetrieve);
            }
            else
            {
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
                if (PropertiesRetrieve.TryGetValue("System.Volume.BitLockerProtection", out object BitlockerStateRaw) && BitlockerStateRaw is int BitlockerState)
                {
                    if (BitlockerState == 6 && !PropertiesRetrieve.ContainsKey("System.Capacity") && !PropertiesRetrieve.ContainsKey("System.FreeSpace"))
                    {
                        return new LockedDriveData(Drive, Thumbnail, PropertiesRetrieve, DriveType);
                    }
                    else
                    {
                        return new NormalDriveData(Drive, Thumbnail, PropertiesRetrieve, DriveType);
                    }
                }
                else
                {
                    return new NormalDriveData(Drive, Thumbnail, PropertiesRetrieve, DriveType);
                }
            }
        }

        private async void UIS_ColorValuesChanged(UISettings sender, object args)
        {
            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
            {
                ProgressBarForeground = new SolidColorBrush(sender.GetColorValue(UIColorType.Accent));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ProgressBarForeground)));
            });
        }

        /// <summary>
        /// 初始化DriveRelatedData对象
        /// </summary>
        /// <param name="DriveFolder">驱动器文件夹</param>
        /// <param name="Thumbnail">缩略图</param>
        /// <param name="PropertiesRetrieve">额外信息</param>
        protected DriveDataBase(StorageFolder DriveFolder, BitmapImage Thumbnail, IDictionary<string, object> PropertiesRetrieve, DriveType DriveType)
        {
            this.DriveFolder = DriveFolder ?? throw new FileNotFoundException();
            this.Thumbnail = Thumbnail ?? new BitmapImage(new Uri("ms-appx:///Assets/DeviceIcon.png"));
            this.DriveType = DriveType;

            UIS = new UISettings();
            UIS.ColorValuesChanged += UIS_ColorValuesChanged;

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
            }
        }
    }
}
