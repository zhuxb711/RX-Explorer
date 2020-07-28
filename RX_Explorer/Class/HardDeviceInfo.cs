using System;
using System.Collections.Generic;
using System.IO;
using Windows.Storage;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;

namespace RX_Explorer.Class
{
    /// <summary>
    /// 提供驱动器的界面支持
    /// </summary>
    public sealed class HardDeviceInfo
    {
        /// <summary>
        /// 驱动器缩略图
        /// </summary>
        public BitmapImage Thumbnail { get; private set; }

        /// <summary>
        /// 驱动器对象
        /// </summary>
        public StorageFolder Folder { get; private set; }

        /// <summary>
        /// 驱动器名称
        /// </summary>
        public string Name
        {
            get
            {
                return Folder.DisplayName;
            }
        }

        /// <summary>
        /// 容量百分比
        /// </summary>
        public double Percent { get; private set; }

        /// <summary>
        /// 总容量的描述
        /// </summary>
        public string Capacity { get; private set; }

        /// <summary>
        /// 总字节数
        /// </summary>
        public ulong TotalByte { get; private set; }

        /// <summary>
        /// 空闲字节数
        /// </summary>
        public ulong FreeByte { get; private set; }

        /// <summary>
        /// 可用空间的描述
        /// </summary>
        public string FreeSpace { get; private set; }

        /// <summary>
        /// 容量显示条对可用空间不足的情况转换颜色
        /// </summary>
        public SolidColorBrush ProgressBarForeground
        {
            get
            {
                if (Percent >= 0.85)
                {
                    return new SolidColorBrush(Colors.Red);
                }
                else
                {
                    return new SolidColorBrush((Color)Application.Current.Resources["SystemAccentColor"]);
                }
            }
        }

        /// <summary>
        /// 存储空间描述
        /// </summary>
        public string StorageSpaceDescription
        {
            get
            {
                return $"{FreeSpace} {Globalization.GetString("Disk_Capacity_Description")} {Capacity}";
            }
        }

        public DriveType DriveType { get; private set; }

        /// <summary>
        /// 初始化HardDeviceInfo对象
        /// </summary>
        /// <param name="Device">驱动器文件夹</param>
        /// <param name="Thumbnail">缩略图</param>
        /// <param name="PropertiesRetrieve">额外信息</param>
        public HardDeviceInfo(StorageFolder Device, BitmapImage Thumbnail, IDictionary<string, object> PropertiesRetrieve, DriveType DriveType)
        {
            Folder = Device ?? throw new FileNotFoundException();

            this.Thumbnail = Thumbnail ?? new BitmapImage(new Uri("ms-appx:///Assets/DeviceIcon.png"));
            this.DriveType = DriveType;

            if (PropertiesRetrieve != null && PropertiesRetrieve["System.Capacity"] is ulong TotalByte && PropertiesRetrieve["System.FreeSpace"] is ulong FreeByte)
            {
                this.TotalByte = TotalByte;
                this.FreeByte = FreeByte;
                Capacity = GetSizeDescription(TotalByte);
                FreeSpace = GetSizeDescription(FreeByte);
                Percent = 1 - FreeByte / Convert.ToDouble(TotalByte);
            }
            else
            {
                Capacity = "Unknown";
                FreeSpace = "Unknown";
                Percent = 0;
            }
        }

        /// <summary>
        /// 根据Size计算大小描述
        /// </summary>
        /// <param name="Size">大小</param>
        /// <returns></returns>
        private string GetSizeDescription(ulong Size)
        {
            return Size / 1024d < 1024 ? Math.Round(Size / 1024d, 2).ToString("0.00") + " KB" :
            (Size / 1048576d < 1024 ? Math.Round(Size / 1048576d, 2).ToString("0.00") + " MB" :
            (Size / 1073741824d < 1024 ? Math.Round(Size / 1073741824d, 2).ToString("0.00") + " GB" :
            Math.Round(Size / 1099511627776d, 2).ToString("0.00") + " TB"));
        }
    }
}
