using System.ComponentModel;
using Windows.Devices.Enumeration;
using Windows.UI.Xaml.Media.Imaging;

namespace RX_Explorer.Class
{
    /// <summary>
    /// 为蓝牙模块提供蓝牙设备信息保存功能
    /// </summary>
    public sealed class BluetoothDeivceData : INotifyPropertyChanged
    {
        /// <summary>
        /// 表示蓝牙设备信息
        /// </summary>
        public DeviceInformation DeviceInfo { get; set; }

        /// <summary>
        /// 获取蓝牙设备名称
        /// </summary>
        public string Name
        {
            get
            {
                return string.IsNullOrWhiteSpace(DeviceInfo.Name) ? Globalization.GetString("UnknownText") : DeviceInfo.Name;
            }
        }

        public BitmapImage Glyph { get;}

        /// <summary>
        /// 获取蓝牙标识字符串
        /// </summary>
        public string Id
        {
            get
            {
                return DeviceInfo.Id;
            }
        }

        /// <summary>
        /// 获取配对情况描述字符串
        /// </summary>
        public string IsPaired
        {
            get
            {
                if (DeviceInfo.Pairing.IsPaired)
                {
                    return Globalization.GetString("PairedText");
                }
                else
                {
                    return Globalization.GetString("ReadyToPairText");
                }
            }
        }

        /// <summary>
        /// Button显示属性
        /// </summary>
        public string ActionButtonText
        {
            get
            {
                if (DeviceInfo.Pairing.IsPaired)
                {
                    return Globalization.GetString("UnpairText");
                }
                else
                {
                    return Globalization.GetString("PairText");
                }
            }
        }

        /// <summary>
        /// 更新蓝牙设备信息
        /// </summary>
        /// <param name="DeviceInfoUpdate">蓝牙设备的更新属性</param>
        public void Update(DeviceInformationUpdate DeviceInfoUpdate)
        {
            DeviceInfo.Update(DeviceInfoUpdate);
            OnPropertyChanged(nameof(IsPaired));
            OnPropertyChanged(nameof(Name));
        }

        public void Update()
        {
            OnPropertyChanged(nameof(IsPaired));
            OnPropertyChanged(nameof(Name));
            OnPropertyChanged(nameof(ActionButtonText));
        }

        private void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// 创建BluetoothDeivceData的实例
        /// </summary>
        /// <param name="DeviceInfo">蓝牙设备</param>
        public BluetoothDeivceData(DeviceInformation DeviceInfo, BitmapImage Glyph)
        {
            this.DeviceInfo = DeviceInfo;
            this.Glyph = Glyph;
        }
    }
}
