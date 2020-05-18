using Bluetooth.Core.Services;
using FileManager.Class;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.Devices.Bluetooth.Rfcomm;
using Windows.Devices.Enumeration;
using Windows.Foundation;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Imaging;

namespace FileManager.Dialog
{
    public sealed partial class BluetoothUI : QueueContentDialog
    {
        ObservableCollection<BluetoothList> BluetoothDeviceCollection;
        List<BluetoothDevice> PairedBluetoothDeviceCollection;
        AutoResetEvent PinLock = null;
        DeviceWatcher BluetoothWatcher = null;
        private int LastSelectIndex = -1;
        private bool IsPinConfirm = false;
        private bool IsAdding = false;
        private Queue<DeviceInformation> AddQueue;

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

            PinLock.Dispose();
            PinLock = null;
            BluetoothDeviceCollection.Clear();
            BluetoothDeviceCollection = null;
            PairedBluetoothDeviceCollection.Clear();
            PairedBluetoothDeviceCollection = null;
            AddQueue.Clear();
            AddQueue = null;
            IsAdding = false;
        }

        private void BluetoothUI_Loaded(object sender, RoutedEventArgs e)
        {
            AddQueue = new Queue<DeviceInformation>();
            PairedBluetoothDeviceCollection = new List<BluetoothDevice>();
            BluetoothDeviceCollection = new ObservableCollection<BluetoothList>();
            BluetoothControl.ItemsSource = BluetoothDeviceCollection;
            PinLock = new AutoResetEvent(false);
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
                    BTService.SearchForPairedDevicesSucceeded += (s, e) =>
                    {
                        PairedBluetoothDeviceCollection = e.PairedDevices;
                    };

                    //能到这里说明该设备已经配对，启动搜索，完成后PairedBluetoothDeviceCollection被填充
                    await BTService.SearchForPairedDevicesAsync().ConfigureAwait(true);

                    if (PairedBluetoothDeviceCollection.FirstOrDefault((Device) => Device.DeviceHost.CanonicalName == CanonicalName) is BluetoothDevice BTDevice)
                    {
                        ObexServiceProvider.SetObexInstance(BTDevice, BluetoothDeviceCollection[BluetoothControl.SelectedIndex].Name);
                    }

                    if (ObexServiceProvider.GetObexNewInstance() == null)
                    {
                        throw new Exception(Globalization.GetString("BluetoothUI_Tips_Text_2"));
                    }
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
                BluetoothWatcher.Stop();
                BluetoothWatcher = null;
                Progress.IsActive = true;
                StatusText.Text = Globalization.GetString("BluetoothUI_Status_Text");
            }

            //根据指定的筛选条件创建检测器
            BluetoothWatcher = DeviceInformation.CreateWatcher("System.Devices.Aep.ProtocolId:=\"{e0cbf06c-cd8b-4647-bb8a-263b43f0f974}\"", new string[] { "System.Devices.Aep.DeviceAddress", "System.Devices.Aep.IsConnected", "System.Devices.Aep.Bluetooth.Le.IsConnectable" }, DeviceInformationKind.AssociationEndpoint);

            BluetoothWatcher.Added += BluetoothWatcher_Added;
            BluetoothWatcher.Updated += BluetoothWatcher_Updated;
            BluetoothWatcher.Removed += BluetoothWatcher_Removed;
            BluetoothWatcher.EnumerationCompleted += BluetoothWatcher_EnumerationCompleted;

            BluetoothWatcher.Start();
        }

        private async void BluetoothWatcher_EnumerationCompleted(DeviceWatcher sender, object args)
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                Progress.IsActive = false;
                StatusText.Text = Globalization.GetString("BluetoothUI_Status_Text_2");
            });
        }

        private void BluetoothWatcher_Removed(DeviceWatcher sender, DeviceInformationUpdate args)
        {
            _ = Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
             {
                 lock (SyncRootProvider.SyncRoot)
                 {
                     try
                     {
                         if (BluetoothDeviceCollection != null)
                         {
                             for (int i = 0; i < BluetoothDeviceCollection.Count; i++)
                             {
                                 if (BluetoothDeviceCollection[i].Id == args.Id)
                                 {
                                     BluetoothDeviceCollection.RemoveAt(i);
                                     i--;
                                 }
                             }
                         }
                     }
                     catch (Exception) { }
                 }
             });
        }

        private void BluetoothWatcher_Updated(DeviceWatcher sender, DeviceInformationUpdate args)
        {
            _ = Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
             {
                 lock (SyncRootProvider.SyncRoot)
                 {
                     try
                     {
                         if (BluetoothDeviceCollection != null)
                         {
                             foreach (var Bluetooth in from BluetoothList Bluetooth in BluetoothDeviceCollection
                                                       where Bluetooth.Id == args.Id
                                                       select Bluetooth)
                             {
                                 Bluetooth.Update(args);
                             }
                         }
                     }
                     catch (Exception) { }
                 }
             });
        }

        private async void BluetoothWatcher_Added(DeviceWatcher sender, DeviceInformation args)
        {
            try
            {
                if (BluetoothDeviceCollection != null)
                {
                    lock (SyncRootProvider.SyncRoot)
                    {
                        AddQueue.Enqueue(args);

                        if (IsAdding)
                        {
                            return;
                        }
                        IsAdding = true;
                    }

                    while (AddQueue.Count != 0)
                    {
                        DeviceInformation Info = AddQueue.Dequeue();
                        using (var Thumbnail = await Info.GetGlyphThumbnailAsync())
                        {
                            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
                            {
                                BitmapImage Image = new BitmapImage
                                {
                                    DecodePixelHeight = 30,
                                    DecodePixelWidth = 30
                                };
                                await Image.SetSourceAsync(Thumbnail);
                                BluetoothDeviceCollection.Add(new BluetoothList(Info, Image));
                            });
                        }
                    }

                    IsAdding = false;
                }
            }
            catch (Exception)
            {
                IsAdding = false;
            }
        }

        /// <summary>
        /// 连接到指定的蓝牙设备的RFComm服务
        /// </summary>
        /// <param name="BL">要连接到的设备</param>
        /// <returns>主机对象的规范名称</returns>
        public async Task<string> ConnectToRfcommServiceAsync(BluetoothList BL)
        {
            if (BL == null)
            {
                throw new ArgumentNullException(nameof(BL), "Parameter could not be null");
            }

            try
            {
                var Device = await Windows.Devices.Bluetooth.BluetoothDevice.FromIdAsync(BL.Id);
                var Services = await Device.GetRfcommServicesForIdAsync(RfcommServiceId.ObexObjectPush);

                if (Services.Services.Count == 0)
                {
                    throw new Exception(Globalization.GetString("BluetoothUI_Tips_Text_3"));
                }

                RfcommDeviceService RfcService = Services.Services[0];
                return RfcService.ConnectionHostName.CanonicalName;
            }
            catch
            {
                throw new Exception(Globalization.GetString("BluetoothUI_Tips_Text_2"));
            }
        }

        private async void PairOrCancelButton_Click(object sender, RoutedEventArgs e)
        {
            Button btn = sender as Button;
            BluetoothControl.SelectedItem = btn.DataContext;
            LastSelectIndex = BluetoothControl.SelectedIndex;

            if (btn.Content.ToString() == Globalization.GetString("PairText"))
            {
                await PairAsync(BluetoothDeviceCollection[LastSelectIndex].DeviceInfo).ConfigureAwait(false);
            }
            else
            {
                var list = BluetoothDeviceCollection[BluetoothControl.SelectedIndex];
                var UnPairResult = await list.DeviceInfo.Pairing.UnpairAsync();
                if (UnPairResult.Status == DeviceUnpairingResultStatus.Unpaired || UnPairResult.Status == DeviceUnpairingResultStatus.AlreadyUnpaired)
                {
                    list.OnPropertyChanged("CancelOrPairButton");
                    list.OnPropertyChanged("Name");
                    list.OnPropertyChanged("IsPaired");
                }
            }
        }

        /// <summary>
        /// 异步启动蓝牙的配对过程
        /// </summary>
        /// <param name="DeviceInfo"></param>
        /// <returns></returns>
        private async Task PairAsync(DeviceInformation DeviceInfo)
        {
            DevicePairingKinds PairKinds = DevicePairingKinds.ConfirmOnly | DevicePairingKinds.ConfirmPinMatch;

            DeviceInformationCustomPairing CustomPairing = DeviceInfo.Pairing.Custom;

            CustomPairing.PairingRequested += CustomPairInfo_PairingRequested;

            DevicePairingResult PairResult = await CustomPairing.PairAsync(PairKinds, DevicePairingProtectionLevel.EncryptionAndAuthentication);

            CustomPairing.PairingRequested -= CustomPairInfo_PairingRequested;

            if (PairResult.Status == DevicePairingResultStatus.Paired)
            {
                BluetoothWatcher.Stop();
                BluetoothDeviceCollection.Clear();
                BluetoothWatcher.Start();
            }
            else
            {
                Tips.Text = Globalization.GetString("BluetoothUI_Tips_Text_4");
            }
        }

        private async void CustomPairInfo_PairingRequested(DeviceInformationCustomPairing sender, DevicePairingRequestedEventArgs args)
        {
            Deferral PairDeferral = args.GetDeferral();

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
                        break;
                    }
            }
            await Task.Run(() =>
            {
                PinLock.WaitOne();

                if (IsPinConfirm)
                {
                    args.Accept();
                }
            }).ConfigureAwait(false);

            PairDeferral.Complete();
        }

        private void PinConfirm_Click(object sender, RoutedEventArgs e)
        {
            IsPinConfirm = true;
            PinLock.Set();
            Tips.Text = string.Empty;
            Tips.Visibility = Visibility.Collapsed;
            PinConfirm.Visibility = Visibility.Collapsed;
            PinRefuse.Visibility = Visibility.Collapsed;
        }

        private void PinRefuse_Click(object sender, RoutedEventArgs e)
        {
            IsPinConfirm = false;
            PinLock.Set();
            Tips.Text = string.Empty;
            Tips.Visibility = Visibility.Collapsed;
            PinConfirm.Visibility = Visibility.Collapsed;
            PinRefuse.Visibility = Visibility.Collapsed;
        }
    }

}
