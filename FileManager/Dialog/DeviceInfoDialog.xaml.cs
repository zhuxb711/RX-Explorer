using System;
using System.Linq;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace FileManager
{
    public sealed partial class DeviceInfoDialog : ContentDialog
    {
        HardDeviceInfo Device;
        public DeviceInfoDialog(HardDeviceInfo Device)
        {
            InitializeComponent();
            this.Device = Device;
            DeviceName.Text = Device.Name;
            Thumbnail.Source = Device.Thumbnail;
            if (MainPage.ThisPage.CurrentLanguage == LanguageEnum.Chinese)
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

        private async void DeviceInfoDialog_Loaded(object sender, RoutedEventArgs e)
        {
            DoubleAnimation.To = Device.Percent * 100;
            Animation.Begin();

            if (MainPage.ThisPage.CurrentLanguage == LanguageEnum.Chinese)
            {
                DeviceType.Text = (await KnownFolders.RemovableDevices.GetFoldersAsync()).Where((Folder) => Folder.FolderRelativeId == Device.Folder.FolderRelativeId).FirstOrDefault() != null
                ? "可移动磁盘"
                : "本地磁盘";
            }
            else
            {
                DeviceType.Text = (await KnownFolders.RemovableDevices.GetFoldersAsync()).Where((Folder) => Folder.FolderRelativeId == Device.Folder.FolderRelativeId).FirstOrDefault() != null
                ? "Removable Disk"
                : "Local Disk";
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
