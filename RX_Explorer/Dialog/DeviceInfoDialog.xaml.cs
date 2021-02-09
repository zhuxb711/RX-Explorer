using RX_Explorer.Class;
using System;
using System.IO;
using Windows.UI.Xaml;

namespace RX_Explorer.Dialog
{
    public sealed partial class DeviceInfoDialog : QueueContentDialog
    {
        private HardDeviceInfo Device;

        public DeviceInfoDialog(HardDeviceInfo Device)
        {
            InitializeComponent();

            this.Device = Device ?? throw new ArgumentNullException(nameof(Device), "Parameter could not be null");

            DeviceName.Text = Device.Name;
            Thumbnail.Source = Device.Thumbnail;

            FreeByte.Text = $"{Device.FreeByte:N0} {Globalization.GetString("Device_Capacity_Unit")}";
            TotalByte.Text = $"{Device.TotalByte:N0} {Globalization.GetString("Device_Capacity_Unit")}";
            UsedByte.Text = $"{Device.TotalByte - Device.FreeByte:N0} {Globalization.GetString("Device_Capacity_Unit")}";

            FreeSpace.Text = Device.FreeSpace;
            TotalSpace.Text = Device.Capacity;
            UsedSpace.Text = (Device.TotalByte - Device.FreeByte).ToFileSizeDescription();

            switch (Device.DriveType)
            {
                case DriveType.Fixed:
                    {
                        DeviceType.Text = Globalization.GetString("Device_Type_1");
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
                case DriveType.Network:
                    {
                        DeviceType.Text = Globalization.GetString("Device_Type_5");
                        break;
                    }
                default:
                    {
                        DeviceType.Text = Globalization.GetString("UnknownText");
                        break;
                    }
            }

            FileSystem.Text = Device.FileSystem;

            Loaded += DeviceInfoDialog_Loaded;
        }

        private void DeviceInfoDialog_Loaded(object sender, RoutedEventArgs e)
        {
            DoubleAnimation.To = Device.Percent * 100;
            Animation.Begin();
        }
    }
}
