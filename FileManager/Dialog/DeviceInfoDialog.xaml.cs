using FileManager.Class;
using System;
using System.IO;
using System.Linq;
using Windows.UI.Xaml;

namespace FileManager.Dialog
{
    public sealed partial class DeviceInfoDialog : QueueContentDialog
    {
        HardDeviceInfo Device;
        public DeviceInfoDialog(HardDeviceInfo Device)
        {
            if (Device == null)
            {
                throw new ArgumentNullException(nameof(Device), "Parameter could not be null");
            }

            InitializeComponent();
            this.Device = Device;
            DeviceName.Text = Device.Name;
            Thumbnail.Source = Device.Thumbnail;
            if (Globalization.Language == LanguageEnum.Chinese)
            {
                FreeByte.Text = Device.FreeByte.ToString("N0") + " 字节";
                TotalByte.Text = Device.TotalByte.ToString("N0") + " 字节";
                UsedByte.Text = (Device.TotalByte - Device.FreeByte).ToString("N0") + " 字节";
            }
            else
            {
                FreeByte.Text = Device.FreeByte.ToString("N0") + " bytes";
                TotalByte.Text = Device.TotalByte.ToString("N0") + " bytes";
                UsedByte.Text = (Device.TotalByte - Device.FreeByte).ToString("N0") + " bytes";
            }
            FreeSpace.Text = Device.FreeSpace;
            TotalSpace.Text = Device.Capacity;
            UsedSpace.Text = GetSizeDescription(Device.TotalByte - Device.FreeByte);
            Loaded += DeviceInfoDialog_Loaded;
        }

        private void DeviceInfoDialog_Loaded(object sender, RoutedEventArgs e)
        {
            DoubleAnimation.To = Device.Percent * 100;
            Animation.Begin();

            if (DriveInfo.GetDrives().FirstOrDefault((Drive) => Drive.RootDirectory.FullName == Device.Folder.Path) is DriveInfo Info)
            {
                switch (Info.DriveType)
                {
                    case DriveType.Fixed:
                        {
                            DeviceType.Text = Globalization.Language == LanguageEnum.Chinese ? "本地驱动器" : "Local drive";
                            break;
                        }
                    case DriveType.Network:
                        {
                            DeviceType.Text = Globalization.Language == LanguageEnum.Chinese ? "网络驱动器" : "Network drive";
                            break;
                        }
                    case DriveType.Removable:
                        {
                            DeviceType.Text = Globalization.Language == LanguageEnum.Chinese ? "可移动驱动器" : "Removable drive";
                            break;
                        }
                    case DriveType.Ram:
                        {
                            DeviceType.Text = Globalization.Language == LanguageEnum.Chinese ? "内存驱动器" : "Ram drive";
                            break;
                        }
                    default:
                        {
                            DeviceType.Text = Globalization.Language == LanguageEnum.Chinese ? "未知" : "Unknown";
                            break;
                        }
                }
            }
            else
            {
                DeviceType.Text = Globalization.Language == LanguageEnum.Chinese ? "未知" : "Unknown";
            }
        }

        private string GetSizeDescription(ulong Size)
        {
            return Size / 1024f < 1024 ? Math.Round(Size / 1024f, 2).ToString("0.00") + " KB" :
            (Size / 1048576f < 1024 ? Math.Round(Size / 1048576f, 2).ToString("0.00") + " MB" :
            (Size / 1073741824f < 1024 ? Math.Round(Size / 1073741824f, 2).ToString("0.00") + " GB" :
            Math.Round(Size / Convert.ToDouble(1099511627776), 2).ToString("0.00") + " TB"));
        }
    }
}
