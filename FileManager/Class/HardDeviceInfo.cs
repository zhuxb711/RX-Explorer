using System;
using System.Collections.Generic;
using System.IO;
using Windows.Storage;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;

namespace FileManager.Class
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

        /// <summary>
        /// 初始化HardDeviceInfo对象
        /// </summary>
        /// <param name="Device">驱动器文件夹</param>
        /// <param name="Thumbnail">缩略图</param>
        /// <param name="PropertiesRetrieve">额外信息</param>
        public HardDeviceInfo(StorageFolder Device, BitmapImage Thumbnail, IDictionary<string, object> PropertiesRetrieve)
        {
            Folder = Device ?? throw new FileNotFoundException();

            if (PropertiesRetrieve == null)
            {
                throw new ArgumentNullException(nameof(PropertiesRetrieve), "Parameter could not be null");
            }

            this.Thumbnail = Thumbnail;

            if (PropertiesRetrieve["System.Capacity"] is ulong TotalByte && PropertiesRetrieve["System.FreeSpace"] is ulong FreeByte)
            {
                this.TotalByte = (ulong)PropertiesRetrieve["System.Capacity"];
                this.FreeByte = (ulong)PropertiesRetrieve["System.FreeSpace"];
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
            return Size / 1024f < 1024 ? Math.Round(Size / 1024f, 2).ToString("0.00") + " KB" :
            (Size / 1048576f < 1024 ? Math.Round(Size / 1048576f, 2).ToString("0.00") + " MB" :
            (Size / 1073741824f < 1024 ? Math.Round(Size / 1073741824f, 2).ToString("0.00") + " GB" :
            Math.Round(Size / Convert.ToDouble(1099511627776), 2).ToString("0.00") + " TB"));
        }
    }
}
