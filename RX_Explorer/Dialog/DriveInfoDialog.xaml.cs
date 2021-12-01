using RX_Explorer.Class;
using System;
using System.IO;
using Windows.UI.Xaml;

namespace RX_Explorer.Dialog
{
    public sealed partial class DriveInfoDialog : QueueContentDialog
    {
        private readonly DriveDataBase Drive;

        public DriveInfoDialog(DriveDataBase Drive)
        {
            InitializeComponent();

            this.Drive = Drive ?? throw new ArgumentNullException(nameof(Drive), "Parameter could not be null");

            DeviceName.Text = Drive.DisplayName;
            Thumbnail.Source = Drive.Thumbnail;

            FreeByte.Text = $"{Drive.FreeByte:N0} {Globalization.GetString("Device_Capacity_Unit")}";
            TotalByte.Text = $"{Drive.TotalByte:N0} {Globalization.GetString("Device_Capacity_Unit")}";
            UsedByte.Text = $"{Drive.TotalByte - Drive.FreeByte:N0} {Globalization.GetString("Device_Capacity_Unit")}";

            FreeSpace.Text = Drive.FreeSpace;
            TotalSpace.Text = Drive.Capacity;
            UsedSpace.Text = (Drive.TotalByte - Drive.FreeByte).GetSizeDescription();

            FileSystem.Text = Drive.FileSystem;

            DeviceType.Text = Drive.DriveType switch
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
            DoubleAnimation.To = Drive.Percent * 100;
            Animation.Begin();
        }
    }
}
