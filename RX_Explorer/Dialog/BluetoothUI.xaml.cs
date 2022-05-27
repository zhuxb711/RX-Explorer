using RX_Explorer.Class;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Rfcomm;
using Windows.Devices.Enumeration;
using Windows.Storage;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace RX_Explorer.Dialog
{
    public sealed partial class BluetoothUI : QueueContentDialog
    {
        private readonly StorageFile ShareFile;
        private readonly DeviceWatcher BluetoothWatcher;
        private readonly ObservableCollection<BluetoothDeivceData> BluetoothDeviceCollection;

        public BluetoothUI(StorageFile ShareFile)
        {
            InitializeComponent();

            this.ShareFile = ShareFile;

            BluetoothDeviceCollection = new ObservableCollection<BluetoothDeivceData>();
            BluetoothWatcher = DeviceInformation.CreateWatcher("System.Devices.Aep.ProtocolId:=\"{e0cbf06c-cd8b-4647-bb8a-263b43f0f974}\"", 
                                                               new string[] { "System.Devices.Aep.DeviceAddress", "System.Devices.Aep.IsConnected" },
                                                               DeviceInformationKind.AssociationEndpoint);

            BluetoothWatcher.Added += BluetoothWatcher_Added;
            BluetoothWatcher.Updated += BluetoothWatcher_Updated;
            BluetoothWatcher.Removed += BluetoothWatcher_Removed;
            BluetoothWatcher.EnumerationCompleted += BluetoothWatcher_EnumerationCompleted;

            BluetoothWatcher.Start();

            Closing += BluetoothUI_Closing;
        }

        private void BluetoothUI_Closing(ContentDialog sender, ContentDialogClosingEventArgs args)
        {
            BluetoothWatcher.Added -= BluetoothWatcher_Added;
            BluetoothWatcher.Updated -= BluetoothWatcher_Updated;
            BluetoothWatcher.Removed -= BluetoothWatcher_Removed;
            BluetoothWatcher.EnumerationCompleted -= BluetoothWatcher_EnumerationCompleted;
            BluetoothWatcher.Stop();

            BluetoothDeviceCollection.Clear();
        }

        private async void BluetoothWatcher_EnumerationCompleted(DeviceWatcher sender, object args)
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                SearchProgress.Visibility = Visibility.Collapsed;
                StatusText.Text = Globalization.GetString("BluetoothUI_Status_Text_2");
            });
        }

        private async void BluetoothWatcher_Removed(DeviceWatcher sender, DeviceInformationUpdate args)
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                if (BluetoothDeviceCollection.FirstOrDefault((Device) => Device.Id == args.Id) is BluetoothDeivceData Device)
                {
                    BluetoothDeviceCollection.Remove(Device);
                }
            });
        }

        private async void BluetoothWatcher_Updated(DeviceWatcher sender, DeviceInformationUpdate args)
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
            {
                if (BluetoothDeviceCollection.FirstOrDefault((Device) => Device.Id == args.Id) is BluetoothDeivceData Device)
                {
                    Device.UpdateBasicInformation(args);
                }
            });
        }

        private async void BluetoothWatcher_Added(DeviceWatcher sender, DeviceInformation args)
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
            {
                BluetoothDeviceCollection.Add(await BluetoothDeivceData.CreateAsync(args, ShareFile));
            });
        }

        private async void PairButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button Btn && Btn.DataContext is BluetoothDeivceData Device)
            {
                await Device.PairAsync();
            }
        }

        private async void UnpairButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button Btn && Btn.DataContext is BluetoothDeivceData Device)
            {
                await Device.UnPairAsync();
            }
        }

        private async void SendFileButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button Btn && Btn.DataContext is BluetoothDeivceData Device)
            {
                await Device.SendFileAsync();
            }
        }
    }

}
