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
        private string infoText;
        private double progressValue;
        private BluetoothPanelMode panelMode;
        private readonly StorageFile SharedFile;
        private TaskCompletionSource<bool> PairConfirmaion;
        private TaskCompletionSource<bool> OperationAbort;
        public event PropertyChangedEventHandler PropertyChanged;

        public BitmapImage DeviceThumbnail { get; }

        public DeviceInformation DeviceInfo { get; }

        public string InfoText
        {
            get => infoText;
            private set
            {
                infoText = value;
                OnPropertyChanged();
            }
        }

        public string Name => string.IsNullOrWhiteSpace(DeviceInfo.Name) ? Globalization.GetString("UnknownText") : DeviceInfo.Name;

        public string Id => DeviceInfo.Id;

        public bool IsPaired => DeviceInfo.Pairing.IsPaired;

        public string DevicePairingStatus
        {
            get
            {
                if (IsPaired)
                {
                    return Globalization.GetString("PairedText");
                }
                else
                {
                    return Globalization.GetString("ReadyToPairText");
                }
            }
        }

        public BluetoothPanelMode PanelMode
        {
            get => panelMode;
            private set
            {
                panelMode = value;
                InfoText = string.Empty;
                OnPropertyChanged();
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

        public static async Task<BluetoothDeivceData> CreateAsync(DeviceInformation DeviceInfo, StorageFile SharedFile)
        {
            try
            {
                using (DeviceThumbnail ThubnailStream = await DeviceInfo.GetGlyphThumbnailAsync())
                {
                    return new BluetoothDeivceData(DeviceInfo, SharedFile, await Helper.CreateBitmapImageAsync(ThubnailStream));
                }
            }
            catch (Exception)
            {
                return new BluetoothDeivceData(DeviceInfo, SharedFile);
            }
        }

        public void UpdateBasicInformation(DeviceInformationUpdate DeviceInfoUpdate = null)
        {
            if (DeviceInfoUpdate != null)
            {
                DeviceInfo.Update(DeviceInfoUpdate);
            }

            OnPropertyChanged(nameof(Name));
            OnPropertyChanged(nameof(IsPaired));
            OnPropertyChanged(nameof(DevicePairingStatus));
        }

        public void PinConfirmClick(object sender, RoutedEventArgs args)
        {
            if (Interlocked.Exchange(ref PairConfirmaion, null) is TaskCompletionSource<bool> Completion)
            {
                Completion.SetResult(true);
            }
        }

        public void PinRefuseClick(object sender, RoutedEventArgs args)
        {
            if (Interlocked.Exchange(ref PairConfirmaion, null) is TaskCompletionSource<bool> Completion)
            {
                Completion.SetResult(false);
            }
        }

        public void AbortClick(object sender, RoutedEventArgs args)
        {
            if (Interlocked.Exchange(ref OperationAbort, null) is TaskCompletionSource<bool> Completion)
            {
                Completion.SetResult(true);
            }
        }

        public async Task PairAsync()
        {
            try
            {
                if (DeviceInfo.Pairing.CanPair)
                {
                    DeviceInfo.Pairing.Custom.PairingRequested += CustomPairInfo_PairingRequested;

                    DevicePairingResult PairResult = await DeviceInfo.Pairing.Custom.PairAsync(DevicePairingKinds.ConfirmOnly | DevicePairingKinds.ConfirmPinMatch, DevicePairingProtectionLevel.EncryptionAndAuthentication);

                    DeviceInfo.Pairing.Custom.PairingRequested -= CustomPairInfo_PairingRequested;

                    if (PairResult.Status == DevicePairingResultStatus.Paired)
                    {
                        UpdateBasicInformation();
                    }
                    else
                    {
                        throw new Exception(Enum.GetName(typeof(DevicePairingResultStatus), PairResult.Status));
                    }
                }
                else
                {
                    throw new Exception();
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"Unable pair with bluetooth device: \"{Name}\"");

                PanelMode = BluetoothPanelMode.TextMode;

                if (string.IsNullOrEmpty(ex.Message))
                {
                    InfoText = Globalization.GetString("BluetoothUI_Tips_Text_4");
                }
                else
                {
                    InfoText = $"{Globalization.GetString("BluetoothUI_Tips_Text_4")}: {string.Join(" ", ex.Message.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries))}";
                }
            }
        }

        public async Task UnPairAsync()
        {
            DeviceUnpairingResult UnPairResult = await DeviceInfo.Pairing.UnpairAsync();

            if (UnPairResult.Status == DeviceUnpairingResultStatus.Unpaired || UnPairResult.Status == DeviceUnpairingResultStatus.AlreadyUnpaired)
            {
                PanelMode = BluetoothPanelMode.None;
                UpdateBasicInformation();
            }
            else
            {
                PanelMode = BluetoothPanelMode.TextMode;
                InfoText = Globalization.GetString("BluetoothUI_Tips_Text_7");
            }
        }

        public async Task<bool> SendFileAsync()
        {
            try
            {
                if (IsPaired)
                {
                    PanelMode = BluetoothPanelMode.TransferMode;
                    InfoText = Globalization.GetString("BluetoothUI_Tips_Text_8");

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
                        if (await GetCurrentBluetoothDeviceAsync(PairedDevice) is BluetoothDevice TargetDevice)
                        {
                            TaskCompletionSource<bool> WaitCompleteSource = new TaskCompletionSource<bool>();
                            TaskCompletionSource<bool> AbortOperationSource = new TaskCompletionSource<bool>();

                            Interlocked.Exchange(ref OperationAbort, AbortOperationSource);

                            ObexService Service = ObexService.GetDefaultForBluetoothDevice(TargetDevice);

                            Service.DataTransferFailed += async (s, e) =>
                            {
                                WaitCompleteSource.SetResult(false);
                                await HandleBluetoothEvent(BluetoothEventKind.TransferFailure);
                            };
                            Service.DataTransferProgressed += async (s, e) =>
                            {
                                await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                                {
                                    ProgressValue = Math.Min(100, Math.Max(0, Convert.ToInt32(Math.Ceiling(e.TransferInPercentage * 100))));
                                });
                            };
                            Service.DataTransferSucceeded += async (s, e) =>
                            {
                                WaitCompleteSource.SetResult(true);
                                await HandleBluetoothEvent(BluetoothEventKind.TransferSuccess);
                            };
                            Service.ConnectionFailed += async (s, e) =>
                            {
                                WaitCompleteSource.SetResult(false);
                                await HandleBluetoothEvent(BluetoothEventKind.ConnectionFailure);
                            };
                            Service.Aborted += async (s, e) =>
                            {
                                WaitCompleteSource.SetException(new OperationCanceledException());
                                await HandleBluetoothEvent(BluetoothEventKind.Aborted);
                            };
                            Service.DeviceConnected += async (s, e) =>
                            {
                                await HandleBluetoothEvent(BluetoothEventKind.Connected);
                                await Service.SendFileAsync(SharedFile);
                            };

                            await Service.ConnectAsync();

                            if (await Task.WhenAny(WaitCompleteSource.Task, AbortOperationSource.Task) == AbortOperationSource.Task)
                            {
                                await Service.AbortAsync();
                                return false;
                            }

                            return await WaitCompleteSource.Task;
                        }
                        else
                        {
                            throw new Exception(Globalization.GetString("BluetoothUI_Tips_Text_2"));
                        }
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
                LogTracer.Log(ex, $"Unable send the file to bluetooth device: \"{Name}\"");

                PanelMode = BluetoothPanelMode.TextMode;

                if (!string.IsNullOrEmpty(ex.Message))
                {
                    InfoText = string.Join(" ", ex.Message.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries));
                }
            }

            return false;
        }

        private async Task<BluetoothDevice> GetCurrentBluetoothDeviceAsync(IReadOnlyList<BluetoothDevice> AvailableDevice)
        {
            using (Windows.Devices.Bluetooth.BluetoothDevice Device = await Windows.Devices.Bluetooth.BluetoothDevice.FromIdAsync(Id))
            {
                RfcommDeviceServicesResult PushServices = await Device.GetRfcommServicesForIdAsync(RfcommServiceId.ObexObjectPush);

                if (PushServices.Services.Any())
                {
                    string CanonicalName = PushServices.Services.Select((Service) => Service.ConnectionHostName?.CanonicalName).Where((Name) => !string.IsNullOrEmpty(Name)).FirstOrDefault();

                    if (AvailableDevice.FirstOrDefault((Device) => Device.DeviceHost.CanonicalName == CanonicalName) is BluetoothDevice TargetDevice)
                    {
                        return TargetDevice;
                    }
                }
                else
                {
                    throw new NotSupportedException(Globalization.GetString("BluetoothUI_Tips_Text_3"));
                }
            }

            return null;
        }

        private async Task HandleBluetoothEvent(BluetoothEventKind Kind)
        {
            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                switch (Kind)
                {
                    case BluetoothEventKind.Aborted:
                    case BluetoothEventKind.TransferFailure:
                        {
                            PanelMode = BluetoothPanelMode.TextMode;
                            InfoText = Globalization.GetString("Bluetooth_Transfer_Status_2");
                            break;
                        }
                    case BluetoothEventKind.Connected:
                        {
                            ProgressValue = 0;
                            InfoText = Globalization.GetString("Bluetooth_Transfer_Status_1");
                            break;
                        }
                    case BluetoothEventKind.TransferSuccess:
                        {
                            ProgressValue = 100;
                            PanelMode = BluetoothPanelMode.TextMode;
                            InfoText = Globalization.GetString("Bluetooth_Transfer_Status_3");
                            break;
                        }
                    case BluetoothEventKind.ConnectionFailure:
                        {
                            PanelMode = BluetoothPanelMode.TextMode;
                            InfoText = Globalization.GetString("Bluetooth_Transfer_Status_4");
                            break;
                        }
                }
            });
        }

        private void OnPropertyChanged([CallerMemberName] string PropertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(PropertyName));
        }

        private async void CustomPairInfo_PairingRequested(DeviceInformationCustomPairing sender, DevicePairingRequestedEventArgs args)
        {
            Deferral PairDeferral = args.GetDeferral();

            try
            {
                TaskCompletionSource<bool> Confirmaion = new TaskCompletionSource<bool>();

                Interlocked.Exchange(ref PairConfirmaion, Confirmaion);

                switch (args.PairingKind)
                {
                    case DevicePairingKinds.ConfirmPinMatch:
                        {
                            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                            {
                                PanelMode = BluetoothPanelMode.PairMode;
                                InfoText = $"{Globalization.GetString("BluetoothUI_Tips_Text_5")}{Environment.NewLine}{args.Pin}";
                            });

                            if (await Task.WhenAny(Confirmaion.Task, Task.Delay(60000)) == Confirmaion.Task)
                            {
                                if (Confirmaion.Task.Result)
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
                                PanelMode = BluetoothPanelMode.PairMode;
                                InfoText = Globalization.GetString("BluetoothUI_Tips_Text_6");
                            });

                            if (await Task.WhenAny(Confirmaion.Task, Task.Delay(60000)) == Confirmaion.Task)
                            {
                                if (Confirmaion.Task.Result)
                                {
                                    args.Accept();
                                }
                            }

                            break;
                        }
                    default:
                        {
                            throw new NotSupportedException($"The pair mode of bluetooth is not supported: {args.PairingKind}");
                        }
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"An exception was threw in {nameof(CustomPairInfo_PairingRequested)}, pair with bluetooth failed");
            }
            finally
            {
                await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    PanelMode = BluetoothPanelMode.None;
                });

                PairDeferral.Complete();
            }
        }

        private BluetoothDeivceData(DeviceInformation DeviceInfo, StorageFile SharedFile, BitmapImage DeviceThumbnail)
        {
            this.DeviceInfo = DeviceInfo;
            this.DeviceThumbnail = DeviceThumbnail;
            this.SharedFile = SharedFile;
        }

        private BluetoothDeivceData(DeviceInformation DeviceInfo, StorageFile SharedFile)
        {
            this.DeviceInfo = DeviceInfo;
            this.SharedFile = SharedFile;
        }
    }
}
