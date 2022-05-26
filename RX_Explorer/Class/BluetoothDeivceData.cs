using Bluetooth.Core.Services;
using Bluetooth.Services.Obex;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.Core;
using Windows.Devices.Bluetooth.Rfcomm;
using Windows.Devices.Enumeration;
using Windows.Foundation;
using Windows.Storage;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media.Imaging;
using BluetoothDevice = Bluetooth.Core.Services.BluetoothDevice;

namespace RX_Explorer.Class
{
    public sealed class BluetoothDeivceData : INotifyPropertyChanged
    {
        private string panelMode = "TextMode";
        private double progressValue;
        private bool isProgressIndeterminate;
        private readonly StorageFile SharedFile;
        private TaskCompletionSource<bool> PairConfirmaion;
        public event PropertyChangedEventHandler PropertyChanged;

        public BitmapImage DeviceThumbnail { get; }

        public DeviceInformation DeviceInfo { get; }

        public string InfoText { get; private set; }

        public string Name => string.IsNullOrWhiteSpace(DeviceInfo.Name) ? Globalization.GetString("UnknownText") : DeviceInfo.Name;

        public string Id => DeviceInfo.Id;

        public string DevicePairingStatus
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

        public string PanelMode
        {
            get => panelMode;
            private set
            {
                panelMode = value;
                OnPropertyChanged();
            }
        }

        public string ActionButtonText
        {
            get
            {
                if (DeviceInfo.Pairing.IsPaired)
                {
                    return "发送文件";
                }
                else
                {
                    return Globalization.GetString("PairText");
                }
            }
        }

        public double ProgressValue
        {
            get => progressValue;
            private set
            {
                progressValue = value;
                OnPropertyChanged();
            }
        }

        public bool IsProgressIndeterminate
        {
            get => isProgressIndeterminate;
            private set
            {
                isProgressIndeterminate = value;
                OnPropertyChanged();
            }
        }

        public static async Task<BluetoothDeivceData> CreateAsync(DeviceInformation DeviceInfo, StorageFile SharedFile)
        {
            BitmapImage DeviceThumbnail = new BitmapImage();

            try
            {
                using (DeviceThumbnail ThubnailStream = await DeviceInfo.GetGlyphThumbnailAsync())
                {
                    await DeviceThumbnail.SetSourceAsync(ThubnailStream);
                }
            }
            catch (Exception)
            {
                //No need to handle this exception
            }

            return new BluetoothDeivceData(DeviceInfo, DeviceThumbnail, SharedFile);
        }

        public void Update(DeviceInformationUpdate DeviceInfoUpdate = null)
        {
            if (DeviceInfoUpdate != null)
            {
                DeviceInfo.Update(DeviceInfoUpdate);
            }

            OnPropertyChanged(nameof(Name));
            OnPropertyChanged(nameof(DevicePairingStatus));
            OnPropertyChanged(nameof(ActionButtonText));
        }

        public async Task SendFileAsync()
        {
            if (DeviceInfo.Pairing.IsPaired)
            {
                PanelMode = "TransferMode";
                IsProgressIndeterminate = true;

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
                    string CanonicalName = await ConnectToRfcommServiceAsync();

                    if (!string.IsNullOrEmpty(CanonicalName))
                    {
                        if (PairedDevice.FirstOrDefault((Device) => Device.DeviceHost.CanonicalName == CanonicalName) is BluetoothDevice Device)
                        {
                            ObexService Service = ObexService.GetDefaultForBluetoothDevice(Device);

                            Service.DataTransferFailed += async (s, e) =>
                            {
                                await HandleBluetoothEvent(BluetoothEventKind.TransferFailure);
                            };
                            Service.DataTransferProgressed += async (s, e) =>
                            {
                                await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                                {
                                    IsProgressIndeterminate = false;
                                    ProgressValue = Math.Min(100, Math.Max(0, Convert.ToInt32(Math.Ceiling(e.TransferInPercentage * 100))));
                                });
                            };
                            Service.DataTransferSucceeded += async (s, e) =>
                            {
                                await HandleBluetoothEvent(BluetoothEventKind.TransferSuccess);
                            };
                            Service.ConnectionFailed += async (s, e) =>
                            {
                                await HandleBluetoothEvent(BluetoothEventKind.ConnectionFailure);
                            };
                            Service.Aborted += async (s, e) =>
                            {
                                await HandleBluetoothEvent(BluetoothEventKind.Aborted);
                            };
                            Service.Disconnected += async (s, e) =>
                            {
                                await HandleBluetoothEvent(BluetoothEventKind.Disconnected);
                            };
                            Service.DeviceConnected += async (s, e) =>
                            {
                                await HandleBluetoothEvent(BluetoothEventKind.Connected);
                            };

                            await Service.ConnectAsync();
                            await Service.SendFileAsync(SharedFile);

                            return;
                        }
                    }

                    throw new Exception(Globalization.GetString("BluetoothUI_Tips_Text_2"));
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

        private async Task<string> ConnectToRfcommServiceAsync()
        {
            using (Windows.Devices.Bluetooth.BluetoothDevice Device = await Windows.Devices.Bluetooth.BluetoothDevice.FromIdAsync(Id))
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

        private async Task HandleBluetoothEvent(BluetoothEventKind Kind)
        {
            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                switch (Kind)
                {
                    case BluetoothEventKind.Aborted:
                        {
                            InfoText = $"{Globalization.GetString("Bluetooth_Transfer_Status_2")}: {Globalization.GetString("Bluetooth_Transfer_Description_2")}";
                            break;
                        }
                    case BluetoothEventKind.Connected:
                        {
                            InfoText = Globalization.GetString("Bluetooth_Transfer_Status_1");
                            break;
                        }
                    case BluetoothEventKind.Disconnected:
                        {
                            InfoText = $"{Globalization.GetString("Bluetooth_Transfer_Status_2")}: {Globalization.GetString("Bluetooth_Transfer_Description_1")}";
                            break;
                        }
                    case BluetoothEventKind.TransferSuccess:
                        {
                            InfoText = Globalization.GetString("Bluetooth_Transfer_Status_3");
                            break;
                        }
                    case BluetoothEventKind.TransferFailure:
                        {
                            InfoText = $"{Globalization.GetString("Bluetooth_Transfer_Status_2")}: {Globalization.GetString("Bluetooth_Transfer_Description_5")}";
                            break;
                        }
                    case BluetoothEventKind.ConnectionFailure:
                        {
                            InfoText = $"{Globalization.GetString("Bluetooth_Transfer_Status_2")}: {Globalization.GetString("Bluetooth_Transfer_Description_5")}";
                            break;
                        }
                }

                OnPropertyChanged(nameof(InfoText));
            });
        }

        public void PinConfirm(object sender, RoutedEventArgs args)
        {
            if (Interlocked.Exchange(ref PairConfirmaion, null) is TaskCompletionSource<bool> Completion)
            {
                Completion.SetResult(true);
            }
        }

        public void PinRefuse(object sender, RoutedEventArgs args)
        {
            if (Interlocked.Exchange(ref PairConfirmaion, null) is TaskCompletionSource<bool> Completion)
            {
                Completion.SetResult(false);
            }
        }

        public async Task PairAsync()
        {
            try
            {
                if (DeviceInfo.Pairing.CanPair)
                {
                    async void CustomPairInfo_PairingRequested(DeviceInformationCustomPairing sender, DevicePairingRequestedEventArgs args)
                    {
                        Deferral PairDeferral = args.GetDeferral();

                        try
                        {
                            PairConfirmaion = new TaskCompletionSource<bool>();

                            switch (args.PairingKind)
                            {
                                case DevicePairingKinds.ConfirmPinMatch:
                                    {
                                        await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                                        {
                                            PanelMode = "PairMode";
                                            InfoText = $"{Globalization.GetString("BluetoothUI_Tips_Text_5")}{Environment.NewLine}{args.Pin}";
                                        });

                                        if (await Task.WhenAny(PairConfirmaion.Task, Task.Delay(60000)) == PairConfirmaion.Task)
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
                                        await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                                        {
                                            PanelMode = "PairMode";
                                            InfoText = Globalization.GetString("BluetoothUI_Tips_Text_6");
                                        });

                                        if (await Task.WhenAny(PairConfirmaion.Task, Task.Delay(60000)) == PairConfirmaion.Task)
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

                    DeviceInfo.Pairing.Custom.PairingRequested += CustomPairInfo_PairingRequested;

                    DevicePairingResult PairResult = await DeviceInfo.Pairing.Custom.PairAsync(DevicePairingKinds.ConfirmOnly | DevicePairingKinds.ConfirmPinMatch, DevicePairingProtectionLevel.EncryptionAndAuthentication);

                    DeviceInfo.Pairing.Custom.PairingRequested -= CustomPairInfo_PairingRequested;

                    if (PairResult.Status == DevicePairingResultStatus.Paired)
                    {
                        Update();
                        return;
                    }
                }

                throw new Exception();
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"Unable pair with bluetooth device: \"{Name}\"");

                if (string.IsNullOrEmpty(ex.Message))
                {
                    InfoText = Globalization.GetString("BluetoothUI_Tips_Text_4");
                }
                else
                {
                    InfoText = $"{Globalization.GetString("BluetoothUI_Tips_Text_4")}: {ex.Message}";
                }
            }
        }

        private void OnPropertyChanged([CallerMemberName] string PropertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(PropertyName));
        }

        private BluetoothDeivceData(DeviceInformation DeviceInfo, BitmapImage DeviceThumbnail, StorageFile SharedFile)
        {
            this.DeviceInfo = DeviceInfo;
            this.DeviceThumbnail = DeviceThumbnail;
            this.SharedFile = SharedFile;
        }
    }
}
