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
            InitializeComponent();

            this.Device = Device ?? throw new ArgumentNullException(nameof(Device), "Parameter could not be null");

            DeviceName.Text = Device.Name;
            Thumbnail.Source = Device.Thumbnail;

            string Unit = Globalization.GetString("Device_Capacity_Unit");
            FreeByte.Text = $"{Device.FreeByte:N0} {Unit}";
            TotalByte.Text = $"{Device.TotalByte:N0} {Unit}";
            UsedByte.Text = $"{Device.TotalByte - Device.FreeByte:N0} {Unit}";

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
                            DeviceType.Text = Globalization.GetString("Device_Type_1");
                            break;
                        }
                    case DriveType.Network:
                        {
                            DeviceType.Text = Globalization.GetString("Device_Type_2");
                            break;
                        }
                    case DriveType.Removable:
                        {
                            DeviceType.Text = Globalization.GetString("Device_Type_3");
                            break;
                        }
                    case DriveType.Ram:
                        {
                            DeviceType.Text = Globalization.GetString("Device_Type_4");
                            break;
                        }
                    default:
                        {
                            DeviceType.Text = Globalization.GetString("Device_Type_5");
                            break;
                        }
                }
            }
            else
            {
                DeviceType.Text = Globalization.GetString("Device_Type_5");
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
