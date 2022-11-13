using PropertyChanged;
using System;
using System.Threading;
using System.Threading.Tasks;
using Windows.Devices.Enumeration;
using Windows.Media.Audio;
using Windows.UI.Xaml.Media.Imaging;

namespace RX_Explorer.Class
{
    [AddINotifyPropertyChangedInterface]
    public sealed partial class BluetoothAudioDeviceData : IDisposable
    {
        private AudioPlaybackConnection AudioConnection;
        private static event EventHandler<bool> ConnectionStatusChanged;

        private DeviceInformation DeviceInfo { get; }

        public BitmapImage Glyph { get; }

        public string Name => string.IsNullOrWhiteSpace(DeviceInfo.Name) ? Globalization.GetString("UnknownText") : DeviceInfo.Name;

        public string Status { get; private set; }

        public string Id => DeviceInfo.Id;

        public string ActionButtonText { get; private set; }

        public bool ActionButtonEnabled { get; private set; }

        [OnChangedMethod(nameof(OnIsConnectedChanged))]
        public bool IsConnected { get; private set; }

        public async Task ConnectAsync()
        {
            try
            {
                ActionButtonEnabled = false;
                ActionButtonText = Globalization.GetString("BluetoothAudio_Button_Text_1");
                Status = Globalization.GetString("BluetoothAudio_Status_2");

                if (Interlocked.Exchange(ref AudioConnection, AudioPlaybackConnection.TryCreateFromId(Id)) is AudioPlaybackConnection OldConnection)
                {
                    OldConnection.Dispose();
                }

                IsConnected = false;

                try
                {
                    if (AudioConnection != null)
                    {
                        await AudioConnection.StartAsync();

                        AudioPlaybackConnectionOpenResult Result = await AudioConnection.OpenAsync();

                        switch (Result.Status)
                        {
                            case AudioPlaybackConnectionOpenResultStatus.Success:
                                {
                                    IsConnected = true;
                                    ActionButtonText = Globalization.GetString("BluetoothAudio_Button_Text_2");
                                    Status = Globalization.GetString("BluetoothAudio_Status_3");

                                    break;
                                }
                            case AudioPlaybackConnectionOpenResultStatus.RequestTimedOut:
                                {
                                    Status = Globalization.GetString("BluetoothAudio_Status_4");
                                    LogTracer.Log("Connect to AudioPlayback failed for time out");

                                    break;
                                }
                            case AudioPlaybackConnectionOpenResultStatus.DeniedBySystem:
                                {
                                    Status = Globalization.GetString("BluetoothAudio_Status_5");
                                    LogTracer.Log("Connect to AudioPlayback failed for being denied by system");

                                    break;
                                }
                            case AudioPlaybackConnectionOpenResultStatus.UnknownFailure:
                                {
                                    Status = Globalization.GetString("BluetoothAudio_Status_6");

                                    if (Result.ExtendedError != null)
                                    {
                                        LogTracer.Log(Result.ExtendedError, "Connect to AudioPlayback failed for unknown reason");
                                    }
                                    else
                                    {
                                        LogTracer.Log("Connect to AudioPlayback failed for unknown reason");
                                    }

                                    break;
                                }
                        }
                    }
                    else
                    {
                        Status = Globalization.GetString("BluetoothAudio_Status_7");
                    }
                }
                finally
                {
                    ActionButtonEnabled = true;
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"Unable to create a new {nameof(AudioPlaybackConnection)}");

                IsConnected = false;

                ActionButtonText = Globalization.GetString("BluetoothAudio_Button_Text_1");
                Status = Globalization.GetString("BluetoothAudio_Status_7");
                ActionButtonEnabled = true;
            }
        }

        public void Update(DeviceInformationUpdate Update)
        {
            DeviceInfo.Update(Update);
            OnPropertyChanged(nameof(Name));
            OnPropertyChanged(nameof(Id));
        }

        public void Disconnect()
        {
            if (Interlocked.Exchange(ref AudioConnection, null) is AudioPlaybackConnection OldConnection)
            {
                OldConnection.Dispose();
            }

            IsConnected = false;
            ActionButtonText = Globalization.GetString("BluetoothAudio_Button_Text_1");
            ActionButtonEnabled = true;
            Status = Globalization.GetString("BluetoothAudio_Status_1");
        }

        public BluetoothAudioDeviceData(DeviceInformation DeviceInfo) : this(null, DeviceInfo)
        {

        }

        public BluetoothAudioDeviceData(BitmapImage Glyph, DeviceInformation DeviceInfo)
        {
            this.Glyph = Glyph;
            this.DeviceInfo = DeviceInfo;

            Status = Globalization.GetString("BluetoothAudio_Status_1");
            ActionButtonText = Globalization.GetString("BluetoothAudio_Button_Text_1");
            ActionButtonEnabled = true;

            ConnectionStatusChanged += StatusChanged;
        }

        private void OnIsConnectedChanged()
        {
            ConnectionStatusChanged?.Invoke(this, IsConnected);
        }

        private void StatusChanged(object sender, bool IsConnected)
        {
            if (sender != this)
            {
                ActionButtonEnabled = !IsConnected;
            }
        }

        public void Dispose()
        {
            if (Execution.CheckAlreadyExecuted(this))
            {
                throw new ObjectDisposedException(nameof(BluetoothAudioDeviceData));
            }

            GC.SuppressFinalize(this);

            Execution.ExecuteOnce(this, () =>
            {
                IsConnected = false;

                AudioConnection?.Dispose();
                AudioConnection = null;

                ConnectionStatusChanged -= StatusChanged;
            });
        }

        ~BluetoothAudioDeviceData()
        {
            Dispose();
        }
    }
}
