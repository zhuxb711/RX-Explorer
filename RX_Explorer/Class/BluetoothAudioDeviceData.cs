using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Windows.Devices.Enumeration;
using Windows.Media.Audio;
using Windows.UI.Xaml.Media.Imaging;

namespace RX_Explorer.Class
{
    public sealed class BluetoothAudioDeviceData : INotifyPropertyChanged, IDisposable
    {
        private readonly DeviceInformation DeviceInfo;

        private AudioPlaybackConnection AudioConnection;

        private static event EventHandler<bool> ConnectionStatusChanged;

        public BitmapImage Glyph { get; }

        public string Name
        {
            get
            {
                return string.IsNullOrWhiteSpace(DeviceInfo.Name) ? Globalization.GetString("UnknownText") : DeviceInfo.Name;
            }
        }

        public string Status { get; private set; } = Globalization.GetString("BluetoothAudio_Status_1");

        public string Id
        {
            get
            {
                return DeviceInfo.Id;
            }
        }

        public string ActionButtonText { get; private set; } = Globalization.GetString("BluetoothAudio_Button_Text_1");

        public bool ActionButtonEnabled { get; private set; } = true;

        public bool IsConnected { get; private set; }

        public async Task ConnectAsync()
        {
            try
            {
                if (AudioConnection != null)
                {
                    AudioConnection.Dispose();
                    AudioConnection = null;
                }

                ActionButtonText = Globalization.GetString("BluetoothAudio_Button_Text_1");
                ActionButtonEnabled = false;
                Status = Globalization.GetString("BluetoothAudio_Status_2");

                OnPropertyChanged(nameof(ActionButtonEnabled));
                OnPropertyChanged(nameof(ActionButtonText));
                OnPropertyChanged(nameof(Status));

                ConnectionStatusChanged?.Invoke(this, true);

                AudioConnection = AudioPlaybackConnection.TryCreateFromId(Id);

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
                                ActionButtonEnabled = true;

                                OnPropertyChanged(nameof(ActionButtonEnabled));
                                OnPropertyChanged(nameof(ActionButtonText));
                                OnPropertyChanged(nameof(Status));

                                break;
                            }
                        case AudioPlaybackConnectionOpenResultStatus.RequestTimedOut:
                            {
                                IsConnected = false;

                                ActionButtonText = Globalization.GetString("BluetoothAudio_Button_Text_1");
                                Status = Globalization.GetString("BluetoothAudio_Status_4");
                                ActionButtonEnabled = true;

                                OnPropertyChanged(nameof(ActionButtonEnabled));
                                OnPropertyChanged(nameof(ActionButtonText));
                                OnPropertyChanged(nameof(Status));

                                ConnectionStatusChanged?.Invoke(this, false);

                                LogTracer.Log("Connect to AudioPlayback failed for time out");

                                break;
                            }
                        case AudioPlaybackConnectionOpenResultStatus.DeniedBySystem:
                            {
                                IsConnected = false;

                                ActionButtonText = Globalization.GetString("BluetoothAudio_Button_Text_1");
                                Status = Globalization.GetString("BluetoothAudio_Status_5");
                                ActionButtonEnabled = true;

                                OnPropertyChanged(nameof(ActionButtonEnabled));
                                OnPropertyChanged(nameof(ActionButtonText));
                                OnPropertyChanged(nameof(Status));

                                ConnectionStatusChanged?.Invoke(this, false);

                                LogTracer.Log("Connect to AudioPlayback failed for being denied by system");

                                break;
                            }
                        case AudioPlaybackConnectionOpenResultStatus.UnknownFailure:
                            {
                                IsConnected = false;

                                ActionButtonText = Globalization.GetString("BluetoothAudio_Button_Text_1");
                                Status = Globalization.GetString("BluetoothAudio_Status_6");
                                ActionButtonEnabled = true;

                                OnPropertyChanged(nameof(ActionButtonEnabled));
                                OnPropertyChanged(nameof(ActionButtonText));
                                OnPropertyChanged(nameof(Status));

                                ConnectionStatusChanged?.Invoke(this, false);

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
                    IsConnected = false;

                    ActionButtonText = Globalization.GetString("BluetoothAudio_Button_Text_1");
                    Status = Globalization.GetString("BluetoothAudio_Status_7");
                    ActionButtonEnabled = true;

                    OnPropertyChanged(nameof(ActionButtonEnabled));
                    OnPropertyChanged(nameof(ActionButtonText));
                    OnPropertyChanged(nameof(Status));

                    ConnectionStatusChanged?.Invoke(this, false);
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"Unable to create a new {nameof(AudioPlaybackConnection)}");

                IsConnected = false;

                ActionButtonText = Globalization.GetString("BluetoothAudio_Button_Text_1");
                Status = Globalization.GetString("BluetoothAudio_Status_7");
                ActionButtonEnabled = true;

                OnPropertyChanged(nameof(ActionButtonEnabled));
                OnPropertyChanged(nameof(ActionButtonText));
                OnPropertyChanged(nameof(Status));

                ConnectionStatusChanged?.Invoke(this, false);
            }
        }

        public void Disconnect()
        {
            if (AudioConnection != null)
            {
                AudioConnection.Dispose();
                AudioConnection = null;
            }

            IsConnected = false;

            ActionButtonText = Globalization.GetString("BluetoothAudio_Button_Text_1");
            ActionButtonEnabled = true;
            Status = Globalization.GetString("BluetoothAudio_Status_1");

            OnPropertyChanged(nameof(ActionButtonEnabled));
            OnPropertyChanged(nameof(ActionButtonText));
            OnPropertyChanged(nameof(Status));

            ConnectionStatusChanged?.Invoke(this, false);
        }

        public void Update(DeviceInformationUpdate DeviceInfoUpdate)
        {
            DeviceInfo.Update(DeviceInfoUpdate);
            OnPropertyChanged(nameof(Name));
        }

        private void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public BluetoothAudioDeviceData(DeviceInformation DeviceInfo, BitmapImage Glyph)
        {
            this.DeviceInfo = DeviceInfo;
            this.Glyph = Glyph;

            ConnectionStatusChanged += StatusChanged;
        }

        private void StatusChanged(object sender, bool IsConnected)
        {
            if (sender != this)
            {
                if (IsConnected)
                {
                    ActionButtonEnabled = false;
                    OnPropertyChanged(nameof(ActionButtonEnabled));
                }
                else
                {
                    ActionButtonEnabled = true;
                    OnPropertyChanged(nameof(ActionButtonEnabled));
                }
            }
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);

            IsConnected = false;

            if (AudioConnection != null)
            {
                AudioConnection.Dispose();
                AudioConnection = null;
            }

            ConnectionStatusChanged -= StatusChanged;
        }

        ~BluetoothAudioDeviceData()
        {
            Dispose();
        }
    }
}
