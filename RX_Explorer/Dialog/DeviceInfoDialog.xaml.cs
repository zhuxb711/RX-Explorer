using RX_Explorer.Class;
using System;
using System.IO;
using Windows.UI.Xaml;

namespace RX_Explorer.Dialog
{
    public sealed partial class DeviceInfoDialog : QueueContentDialog
    {
        private DriveDataBase Device;

        public DeviceInfoDialog(DriveDataBase Device)
        {
            InitializeComponent();

            this.Device = Device ?? throw new ArgumentNullException(nameof(Device), "Parameter could not be null");

            DeviceName.Text = Device.DisplayName;
            Thumbnail.Source = Device.Thumbnail;

            FreeByte.Text = $"{Device.FreeByte:N0} {Globalization.GetString("Device_Capacity_Unit")}";
            TotalByte.Text = $"{Device.TotalByte:N0} {Globalization.GetString("Device_Capacity_Unit")}";
            UsedByte.Text = $"{Device.TotalByte - Device.FreeByte:N0} {Globalization.GetString("Device_Capacity_Unit")}";

            FreeSpace.Text = Device.FreeSpace;
            TotalSpace.Text = Device.Capacity;
            UsedSpace.Text = (Device.TotalByte - Device.FreeByte).GetFileSizeDescription();

            FileSystem.Text = Device.FileSystem;

            DeviceType.Text = Device.DriveType switch
            {
                DriveType.Fixed => Globalization.GetString("Device_Type_1"),
                DriveType.CDRom => Globalization.GetString("Device_Type_2"),
                DriveType.Removable => Globalization.GetString("Device_Type_3"),
                DriveType.Ram => Globalization.GetString("Device_Type_4"),
                DriveType.Network => Globalization.GetString("Device_Type_5"),
                _ => Globalization.GetString("UnknownText")
            };

            Loaded += DeviceInfoDialog_Loaded;
        }

        private void DeviceInfoDialog_Loaded(object sender, RoutedEventArgs e)
        {
            DoubleAnimation.To = Device.Percent * 100;
            Animation.Begin();
        }
    }
}
