using Bluetooth.Core.Services;
using RX_Explorer.Class;
using System;
using System.Collections.Generic;
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
            ContentDialogButtonClickDeferral Deferral = args.GetDeferral();

            try
            {
                Tips.Text = string.Empty;
                Tips.Visibility = Visibility.Collapsed;

                BluetoothDeivceData DeviceData = null;

                if (BluetoothControl.SelectedIndex >= 0 && BluetoothControl.SelectedIndex < BluetoothDeviceCollection.Count)
                {
                    DeviceData = BluetoothDeviceCollection[BluetoothControl.SelectedIndex];
                }

                if ((DeviceData?.DeviceInfo.Pairing.IsPaired).GetValueOrDefault())
                {
                    string FailureReason = null;
                    IReadOnlyList<BluetoothDevice> PairedDevice = null;

                    void BTService_SearchForPairedDevicesSucceeded(object sender, SearchForPairedDevicesSucceededEventArgs e)
                    {
                        FailureReason = string.Empty;
                        PairedDevice = e.PairedDevices;
                    }

                    void BTService_SearchForPairedDevicesFailed(object sender, SearchForPairedDevicesFailedEventArgs e)
                    {
                        FailureReason = Enum.GetName(typeof(SearchForDeviceFailureReasons), e.FailureReason);
                    }

                    BluetoothService BTService = BluetoothService.GetDefault();
                    BTService.SearchForPairedDevicesSucceeded += BTService_SearchForPairedDevicesSucceeded;
                    BTService.SearchForPairedDevicesFailed += BTService_SearchForPairedDevicesFailed;
                    await BTService.SearchForPairedDevicesAsync();

                    if (string.IsNullOrEmpty(FailureReason))
                    {
                        await PrepareSelectedBluetoothDeviceAsync(PairedDevice);
                    }
                    else
                    {
                        throw new Exception(FailureReason);
                    }
                }
                else
                {
                    throw new Exception(Globalization.GetString("BluetoothUI_Tips_Text_1"));
                }
            }
            catch (Exception ex)
            {
                args.Cancel = true;
                Tips.Text = ex.Message;
                Tips.Visibility = Visibility.Visible;

                LogTracer.Log(ex, "Could not connect to the bluetooth device");
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

        private async Task PrepareSelectedBluetoothDeviceAsync(IReadOnlyList<BluetoothDevice> PairedDevices)
        {
            BluetoothDeivceData DeviceData = null;

            if (BluetoothControl.SelectedIndex >= 0 && BluetoothControl.SelectedIndex < BluetoothDeviceCollection.Count)
            {
                DeviceData = BluetoothDeviceCollection[BluetoothControl.SelectedIndex];
            }

            if (DeviceData != null)
            {
                string CanonicalName = await ConnectToRfcommServiceAsync(DeviceData);

                if (!string.IsNullOrEmpty(CanonicalName))
                {
                    if (PairedDevices.FirstOrDefault((Device) => Device.DeviceHost.CanonicalName == CanonicalName) is BluetoothDevice Device)
                    {
                        ObexServiceProvider.SetObexInstance(Device, DeviceData.Name);
                        return;
                    }
                }
            }

            throw new Exception(Globalization.GetString("BluetoothUI_Tips_Text_2"));
        }

        private async void BluetoothWatcher_EnumerationCompleted(DeviceWatcher sender, object args)
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                Progress.Visibility = Visibility.Collapsed;
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

        private async Task<string> ConnectToRfcommServiceAsync(BluetoothDeivceData Data)
        {
            if (Data == null)
            {
                throw new ArgumentNullException(nameof(Data), "Parameter could not be null");
            }

            using (Windows.Devices.Bluetooth.BluetoothDevice Device = await Windows.Devices.Bluetooth.BluetoothDevice.FromIdAsync(Data.Id))
            {
                RfcommDeviceServicesResult Services = await Device.GetRfcommServicesForIdAsync(RfcommServiceId.ObexObjectPush);

                if (Services.Services.Any())
                {
                    return Services.Services.Select((Service) => Service.ConnectionHostName?.CanonicalName).Where((Name) => !string.IsNullOrEmpty(Name)).FirstOrDefault();
                }
                else
                {
                    throw new NotSupportedException(Globalization.GetString("BluetoothUI_Tips_Text_3"));
                }
            }
        }

        private async void PairOrCancelButton_Click(object sender, RoutedEventArgs e)
        {
            Button Btn = sender as Button;

            if (Btn.DataContext is BluetoothDeivceData Device)
            {
                if (Btn.Content.ToString() == Globalization.GetString("PairText"))
                {
                    await PairAsync(Device);
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
                        return;
                    }
                }

                throw new Exception();
            }
            catch (Exception ex)
            {
                Tips.Text = string.IsNullOrEmpty(ex.Message) ? Globalization.GetString("BluetoothUI_Tips_Text_4") : $"{Globalization.GetString("BluetoothUI_Tips_Text_4")}: {ex.Message}";
                Tips.Visibility = Visibility.Visible;

                LogTracer.Log(ex, $"Unable pair with bluetooth device: \"{Device.Name}\"");
            }
        }

        private async void CustomPairInfo_PairingRequested(DeviceInformationCustomPairing sender, DevicePairingRequestedEventArgs args)
        {
            Deferral PairDeferral = args.GetDeferral();

            try
            {
                TaskCompletionSource<bool> PairConfirmaion = new TaskCompletionSource<bool>();

                Task TimeoutTask = Task.Delay(60000);

                switch (args.PairingKind)
                {
                    case DevicePairingKinds.ConfirmPinMatch:
                        {
                            void PinConfirm_Click(object sender, RoutedEventArgs e)
                            {
                                PinConfirm.Click -= PinConfirm_Click;
                                Tips.Text = string.Empty;
                                Tips.Visibility = Visibility.Collapsed;
                                PinButtonArea.Visibility = Visibility.Collapsed;
                                PairConfirmaion.SetResult(true);
                            }

                            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                            {
                                Tips.Text = $"{Globalization.GetString("BluetoothUI_Tips_Text_5")}{Environment.NewLine}{args.Pin}";
                                Tips.Visibility = Visibility.Visible;
                                PinButtonArea.Visibility = Visibility.Visible;
                                PinConfirm.Click += PinConfirm_Click;
                            });

                            if (await Task.WhenAny(PairConfirmaion.Task, TimeoutTask) != TimeoutTask)
                            {
                                if (PairConfirmaion.Task.Result)
                                {
                                    args.Accept(args.Pin);
                                }
                            }

                            break;
                        }
                    case DevicePairingKinds.ConfirmOnly:
                        {
                            void PinRefuse_Click(object sender, RoutedEventArgs e)
                            {
                                PinRefuse.Click -= PinRefuse_Click;
                                Tips.Text = string.Empty;
                                Tips.Visibility = Visibility.Collapsed;
                                PinButtonArea.Visibility = Visibility.Collapsed;
                                PairConfirmaion.SetResult(false);
                            }

                            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                            {
                                Tips.Text = Globalization.GetString("BluetoothUI_Tips_Text_6");
                                Tips.Visibility = Visibility.Visible;
                                PinButtonArea.Visibility = Visibility.Visible;
                                PinRefuse.Click += PinRefuse_Click;
                            });

                            if (await Task.WhenAny(PairConfirmaion.Task, TimeoutTask) != TimeoutTask)
                            {
                                if (PairConfirmaion.Task.Result)
                                {
                                    args.Accept();
                                }
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
    }

}
