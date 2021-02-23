using Bluetooth.Core.Services;
using RX_Explorer.Class;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Windows.Devices.Bluetooth.Rfcomm;
using Windows.Devices.Enumeration;
using Windows.Foundation;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Imaging;

namespace RX_Explorer.Dialog
{
    public sealed partial class BluetoothUI : QueueContentDialog
    {
        private readonly ObservableCollection<BluetoothDeivceData> BluetoothDeviceCollection = new ObservableCollection<BluetoothDeivceData>();
        private TaskCompletionSource<bool> PairConfirmaion;
        private DeviceWatcher BluetoothWatcher;

        public BluetoothUI()
        {
            InitializeComponent();
            Loaded += BluetoothUI_Loaded;
            Closing += BluetoothUI_Closing;
        }

        private void BluetoothUI_Closing(ContentDialog sender, ContentDialogClosingEventArgs args)
        {
            if (BluetoothWatcher != null)
            {
                BluetoothWatcher.Added -= BluetoothWatcher_Added;
                BluetoothWatcher.Updated -= BluetoothWatcher_Updated;
                BluetoothWatcher.Removed -= BluetoothWatcher_Removed;
                BluetoothWatcher.EnumerationCompleted -= BluetoothWatcher_EnumerationCompleted;
                BluetoothWatcher.Stop();
                BluetoothWatcher = null;
            }

            BluetoothDeviceCollection.Clear();
        }

        private void BluetoothUI_Loaded(object sender, RoutedEventArgs e)
        {
            CreateBluetoothWatcher();
        }

        private async void QueueContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            var Deferral = args.GetDeferral();

            try
            {
                if (BluetoothControl.SelectedIndex == -1 || !BluetoothDeviceCollection[BluetoothControl.SelectedIndex].DeviceInfo.Pairing.IsPaired)
                {
                    Tips.Text = Globalization.GetString("BluetoothUI_Tips_Text_1");
                    Tips.Visibility = Visibility.Visible;
                    args.Cancel = true;
                }
                else
                {
                    //首先连接到RFComm服务，获取到设备的规范名称
                    string CanonicalName = await ConnectToRfcommServiceAsync(BluetoothDeviceCollection[BluetoothControl.SelectedIndex]).ConfigureAwait(true);

                    BluetoothService BTService = BluetoothService.GetDefault();
                    BTService.SearchForPairedDevicesSucceeded += BTService_SearchForPairedDevicesSucceeded;

                    void BTService_SearchForPairedDevicesSucceeded(object sender, SearchForPairedDevicesSucceededEventArgs e)
                    {
                        BTService.SearchForPairedDevicesSucceeded -= BTService_SearchForPairedDevicesSucceeded;

                        if (e.PairedDevices.FirstOrDefault((Device) => Device.DeviceHost.CanonicalName == CanonicalName) is BluetoothDevice BTDevice)
                        {
                            ObexServiceProvider.SetObexInstance(BTDevice, BluetoothDeviceCollection[BluetoothControl.SelectedIndex].Name);

                            if (ObexServiceProvider.GetObexInstance() == null)
                            {
                                throw new Exception(Globalization.GetString("BluetoothUI_Tips_Text_2"));
                            }
                        }
                        else
                        {
                            throw new Exception(Globalization.GetString("BluetoothUI_Tips_Text_2"));
                        }
                    }

                    //能到这里说明该设备已经配对，启动搜索，完成后PairedBluetoothDeviceCollection被填充
                    await BTService.SearchForPairedDevicesAsync().ConfigureAwait(true);
                }
            }
            catch (Exception e)
            {
                args.Cancel = true;

                await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    Tips.Text = e.Message;
                    Tips.Visibility = Visibility.Visible;
                });
            }
            finally
            {
                Deferral.Complete();
            }
        }

        /// <summary>
        /// 创建蓝牙的检测器，检测器将定期检测蓝牙设备
        /// </summary>
        public void CreateBluetoothWatcher()
        {
            if (BluetoothWatcher != null)
            {
                BluetoothWatcher.Added -= BluetoothWatcher_Added;
                BluetoothWatcher.Updated -= BluetoothWatcher_Updated;
                BluetoothWatcher.Removed -= BluetoothWatcher_Removed;
                BluetoothWatcher.EnumerationCompleted -= BluetoothWatcher_EnumerationCompleted;

                if (BluetoothWatcher.Status == DeviceWatcherStatus.Started || BluetoothWatcher.Status == DeviceWatcherStatus.EnumerationCompleted)
                {
                    BluetoothWatcher.Stop();
                }
            }

            Progress.IsActive = true;
            StatusText.Text = Globalization.GetString("BluetoothUI_Status_Text_1");

            //根据指定的筛选条件创建检测器
            BluetoothWatcher = DeviceInformation.CreateWatcher("System.Devices.Aep.ProtocolId:=\"{e0cbf06c-cd8b-4647-bb8a-263b43f0f974}\"",
                                                               new string[] { "System.Devices.Aep.DeviceAddress", "System.Devices.Aep.IsConnected", "System.Devices.Aep.Bluetooth.Le.IsConnectable" },
                                                               DeviceInformationKind.AssociationEndpoint);

            BluetoothWatcher.Added += BluetoothWatcher_Added;
            BluetoothWatcher.Updated += BluetoothWatcher_Updated;
            BluetoothWatcher.Removed += BluetoothWatcher_Removed;
            BluetoothWatcher.EnumerationCompleted += BluetoothWatcher_EnumerationCompleted;

            BluetoothWatcher.Start();
        }

        private async void BluetoothWatcher_EnumerationCompleted(DeviceWatcher sender, object args)
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
            {
                Progress.IsActive = false;
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
                    Device.Update(args);
                }
            });
        }

        private async void BluetoothWatcher_Added(DeviceWatcher sender, DeviceInformation args)
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
            {
                using (DeviceThumbnail Thumbnail = await args.GetGlyphThumbnailAsync())
                {
                    BitmapImage Image = new BitmapImage();
                    BluetoothDeviceCollection.Add(new BluetoothDeivceData(args, Image));
                    await Image.SetSourceAsync(Thumbnail);
                }
            });
        }

        /// <summary>
        /// 连接到指定的蓝牙设备的RFComm服务
        /// </summary>
        /// <param name="BL">要连接到的设备</param>
        /// <returns>主机对象的规范名称</returns>
        public async Task<string> ConnectToRfcommServiceAsync(BluetoothDeivceData BL)
        {
            if (BL == null)
            {
                throw new ArgumentNullException(nameof(BL), "Parameter could not be null");
            }

            try
            {
                using (Windows.Devices.Bluetooth.BluetoothDevice Device = await Windows.Devices.Bluetooth.BluetoothDevice.FromIdAsync(BL.Id))
                {
                    RfcommDeviceServicesResult Services = await Device.GetRfcommServicesForIdAsync(RfcommServiceId.ObexObjectPush);

                    if (Services.Services.Any())
                    {
                        return Services.Services.Select((Service) => Service.ConnectionHostName?.CanonicalName).Where((Name) => !string.IsNullOrEmpty(Name)).FirstOrDefault();
                    }
                    else
                    {
                        throw new Exception(Globalization.GetString("BluetoothUI_Tips_Text_3"));
                    }
                }
            }
            catch
            {
                throw new Exception(Globalization.GetString("BluetoothUI_Tips_Text_2"));
            }
        }

        private async void PairOrCancelButton_Click(object sender, RoutedEventArgs e)
        {
            Button Btn = sender as Button;

            if (Btn.DataContext is BluetoothDeivceData Device)
            {
                if (Btn.Content.ToString() == Globalization.GetString("PairText"))
                {
                    await PairAsync(Device).ConfigureAwait(false);
                }
                else
                {
                    DeviceUnpairingResult UnPairResult = await Device.DeviceInfo.Pairing.UnpairAsync();

                    if (UnPairResult.Status == DeviceUnpairingResultStatus.Unpaired || UnPairResult.Status == DeviceUnpairingResultStatus.AlreadyUnpaired)
                    {
                        BluetoothDeviceCollection.Remove(Device);
                    }
                }
            }
        }

        /// <summary>
        /// 异步启动蓝牙的配对过程
        /// </summary>
        /// <param name="DeviceInfo"></param>
        /// <returns></returns>
        private async Task PairAsync(BluetoothDeivceData Device)
        {
            try
            {
                if (Device.DeviceInfo.Pairing.CanPair)
                {
                    DeviceInformationCustomPairing CustomPairing = Device.DeviceInfo.Pairing.Custom;

                    CustomPairing.PairingRequested += CustomPairInfo_PairingRequested;

                    DevicePairingResult PairResult = await CustomPairing.PairAsync(DevicePairingKinds.ConfirmOnly | DevicePairingKinds.ConfirmPinMatch, DevicePairingProtectionLevel.EncryptionAndAuthentication);

                    CustomPairing.PairingRequested -= CustomPairInfo_PairingRequested;

                    if (PairResult.Status == DevicePairingResultStatus.Paired)
                    {
                        Device.Update();
                    }
                    else
                    {
                        await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                        {
                            Tips.Text = Globalization.GetString("BluetoothUI_Tips_Text_4");
                            Tips.Visibility = Visibility.Visible;
                        });
                    }
                }
                else
                {
                    LogTracer.Log($"Unable pair with Bluetooth device: \"{Device.Name}\", reason: CanPair property return false");
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"Unable pair with Bluetooth device: \"{Device.Name}\"");
            }
        }

        private async void CustomPairInfo_PairingRequested(DeviceInformationCustomPairing sender, DevicePairingRequestedEventArgs args)
        {
            Deferral PairDeferral = args.GetDeferral();

            try
            {
                PairConfirmaion = new TaskCompletionSource<bool>();

                switch (args.PairingKind)
                {
                    case DevicePairingKinds.ConfirmPinMatch:
                        {
                            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                            {
                                Tips.Text = $"{Globalization.GetString("BluetoothUI_Tips_Text_5")}{Environment.NewLine}{args.Pin}";
                                Tips.Visibility = Visibility.Visible;
                                PinConfirm.Visibility = Visibility.Visible;
                                PinRefuse.Visibility = Visibility.Visible;
                            });

                            if (await PairConfirmaion.Task)
                            {
                                args.Accept(args.Pin);
                            }

                            break;
                        }
                    case DevicePairingKinds.ConfirmOnly:
                        {
                            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                            {
                                Tips.Text = Globalization.GetString("BluetoothUI_Tips_Text_6");
                                Tips.Visibility = Visibility.Visible;
                                PinConfirm.Visibility = Visibility.Visible;
                                PinRefuse.Visibility = Visibility.Visible;
                            });

                            if (await PairConfirmaion.Task)
                            {
                                args.Accept();
                            }

                            break;
                        }
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"An exception was threw in {nameof(CustomPairInfo_PairingRequested)}, pair with bluetooth failed");
            }
            finally
            {
                PairDeferral.Complete();
            }
        }

        private void PinConfirm_Click(object sender, RoutedEventArgs e)
        {
            Tips.Text = string.Empty;
            Tips.Visibility = Visibility.Collapsed;
            PinConfirm.Visibility = Visibility.Collapsed;
            PinRefuse.Visibility = Visibility.Collapsed;

            PairConfirmaion?.SetResult(true);
        }

        private void PinRefuse_Click(object sender, RoutedEventArgs e)
        {
            Tips.Text = string.Empty;
            Tips.Visibility = Visibility.Collapsed;
            PinConfirm.Visibility = Visibility.Collapsed;
            PinRefuse.Visibility = Visibility.Collapsed;

            PairConfirmaion?.SetResult(false);
        }
    }

}
