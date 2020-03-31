using Bluetooth.Services.Obex;
using System;
using Windows.Storage;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;


namespace FileManager
{
    public sealed partial class BluetoothFileTransfer : QueueContentDialog
    {
        public StorageFile FileToSend { get; private set; }

        private ObexService ObexClient;

        private bool AbortFromHere = false;

        public BluetoothFileTransfer(StorageFile FileToSend)
        {
            InitializeComponent();
            this.FileToSend = FileToSend ?? throw new ArgumentNullException(nameof(FileToSend), "Parameter could not be null");
            ObexClient = ObexServiceProvider.GetObexNewInstance();
            TransferName.Text = Globalization.Language == LanguageEnum.Chinese ? $"传输文件名：{FileToSend.Name}" : $"File to transfer：{FileToSend.Name}";
            TransferDeviceName.Text = Globalization.Language == LanguageEnum.Chinese ? $"目标设备：{ObexServiceProvider.DeviceName}" : $"Target device：{ObexServiceProvider.DeviceName}";
            Loaded += BluetoothFileTransfer_Loaded;
            Closing += BluetoothFileTransfer_Closing;
        }

        private void BluetoothFileTransfer_Closing(ContentDialog sender, ContentDialogClosingEventArgs args)
        {
            ObexClient.DataTransferFailed -= ObexClient_DataTransferFailed;
            ObexClient.DataTransferProgressed -= ObexClient_DataTransferProgressed;
            ObexClient.DataTransferSucceeded -= ObexClient_DataTransferSucceeded;
            ObexClient.ConnectionFailed -= ObexClient_ConnectionFailed;
            ObexClient.Aborted -= ObexClient_Aborted;
            ObexClient.Disconnected -= ObexClient_Disconnected;
            ObexClient.DeviceConnected -= ObexClient_DeviceConnected;
        }

        private async void BluetoothFileTransfer_Loaded(object sender, RoutedEventArgs e)
        {
            ObexClient.DataTransferFailed += ObexClient_DataTransferFailed;
            ObexClient.DataTransferProgressed += ObexClient_DataTransferProgressed;
            ObexClient.DataTransferSucceeded += ObexClient_DataTransferSucceeded;
            ObexClient.ConnectionFailed += ObexClient_ConnectionFailed;
            ObexClient.Aborted += ObexClient_Aborted;
            ObexClient.Disconnected += ObexClient_Disconnected;
            ObexClient.DeviceConnected += ObexClient_DeviceConnected;

            await ObexClient.ConnectAsync().ConfigureAwait(false);

            await ObexClient.SendFileAsync(FileToSend).ConfigureAwait(false);
        }

        private async void ObexClient_DeviceConnected(object sender, EventArgs e)
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                Title = Globalization.Language == LanguageEnum.Chinese
                ? "正在传输中"
                : "Transferring";
            });
        }

        private async void ObexClient_Disconnected(object sender, EventArgs e)
        {
            if (AbortFromHere)
            {
                AbortFromHere = false;
                return;
            }

            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                if (Globalization.Language == LanguageEnum.Chinese)
                {
                    Title = "传输终止";
                    ProgressText.Text = "目标设备终止了文件传输";
                    CloseButtonText = "退出";
                    SecondaryButtonText = "重试";
                }
                else
                {
                    Title = "Transmission terminated";
                    ProgressText.Text = "Target device terminated file transfer";
                    CloseButtonText = "Exit";
                    SecondaryButtonText = "Retry";
                }
            });
        }

        private async void ObexClient_Aborted(object sender, EventArgs e)
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                if (Globalization.Language == LanguageEnum.Chinese)
                {
                    Title = "传输终止";
                    ProgressText.Text = "文件传输终止";
                    CloseButtonText = "退出";
                    SecondaryButtonText = "重试";
                }
                else
                {
                    Title = "Transmission terminated";
                    ProgressText.Text = "File transfer terminated";
                    CloseButtonText = "Exit";
                    SecondaryButtonText = "Retry";
                }
            });
        }

        private async void ObexClient_ConnectionFailed(object sender, ConnectionFailedEventArgs e)
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                if (Globalization.Language == LanguageEnum.Chinese)
                {
                    Title = "传输终止";
                    ProgressText.Text = "连接失败: " + e.ExceptionObject.Message;
                    CloseButtonText = "退出";
                    SecondaryButtonText = "重试";
                }
                else
                {
                    Title = "Transmission terminated";
                    ProgressText.Text = "Connection failed: " + e.ExceptionObject.Message;
                    CloseButtonText = "Exit";
                    SecondaryButtonText = "Retry";
                }
            });
        }

        private async void ObexClient_DataTransferSucceeded(object sender, EventArgs e)
        {
            AbortFromHere = true;
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                if (Globalization.Language == LanguageEnum.Chinese)
                {
                    Title = "传输完成";
                    ProgressControl.Value = 100;
                    ProgressText.Text = "100%" + " \r文件传输完成";
                    SecondaryButtonText = "完成";
                }
                else
                {
                    Title = "Transfer completed";
                    ProgressControl.Value = 100;
                    ProgressText.Text = "100%" + " \rFile transfer completed";
                    SecondaryButtonText = "Complete";
                }
            });
        }

        private async void ObexClient_DataTransferProgressed(object sender, DataTransferProgressedEventArgs e)
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                if (ProgressControl.IsIndeterminate)
                {
                    ProgressControl.IsIndeterminate = false;
                }

                ProgressControl.Value = e.TransferInPercentage * 100;
                ProgressText.Text = ((int)(e.TransferInPercentage * 100)) + "%";
            });
        }

        private async void ObexClient_DataTransferFailed(object sender, DataTransferFailedEventArgs e)
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                if (Globalization.Language == LanguageEnum.Chinese)
                {
                    Title = "传输终止";
                    ProgressText.Text = "文件传输意外终止:" + e.ExceptionObject.Message;
                    CloseButtonText = "退出";
                    SecondaryButtonText = "重试";
                }
                else
                {
                    Title = "Transmission terminated";
                    ProgressText.Text = "File transfer terminated unexpectedly:" + e.ExceptionObject.Message;
                    CloseButtonText = "Exit";
                    SecondaryButtonText = "Retry";
                }
            });
        }

        private async void QueueContentDialog_SecondaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            var Deferral = args.GetDeferral();

            try
            {
                if (Globalization.Language == LanguageEnum.Chinese)
                {
                    if (SecondaryButtonText == "终止")
                    {
                        args.Cancel = true;
                        AbortFromHere = true;

                        try
                        {
                            await ObexClient.AbortAsync().ConfigureAwait(true);
                        }
                        catch (Exception)
                        {

                        }
                    }
                    else if (SecondaryButtonText == "重试")
                    {
                        args.Cancel = true;
                        ProgressControl.IsIndeterminate = true;
                        ProgressText.Text = "0%";

                        ObexClient.DataTransferFailed -= ObexClient_DataTransferFailed;
                        ObexClient.DataTransferProgressed -= ObexClient_DataTransferProgressed;
                        ObexClient.DataTransferSucceeded -= ObexClient_DataTransferSucceeded;
                        ObexClient.ConnectionFailed -= ObexClient_ConnectionFailed;
                        ObexClient.Aborted -= ObexClient_Aborted;
                        ObexClient.Disconnected -= ObexClient_Disconnected;
                        ObexClient.DeviceConnected -= ObexClient_DeviceConnected;

                        ObexClient = ObexServiceProvider.GetObexNewInstance();

                        ObexClient.DataTransferFailed += ObexClient_DataTransferFailed;
                        ObexClient.DataTransferProgressed += ObexClient_DataTransferProgressed;
                        ObexClient.DataTransferSucceeded += ObexClient_DataTransferSucceeded;
                        ObexClient.ConnectionFailed += ObexClient_ConnectionFailed;
                        ObexClient.Aborted += ObexClient_Aborted;
                        ObexClient.Disconnected += ObexClient_Disconnected;
                        ObexClient.DeviceConnected += ObexClient_DeviceConnected;

                        try
                        {
                            ProgressControl.Value = 0;
                            CloseButtonText = string.Empty;
                            SecondaryButtonText = "终止";
                            await ObexClient.ConnectAsync().ConfigureAwait(true);
                            await ObexClient.SendFileAsync(FileToSend).ConfigureAwait(true);
                        }
                        catch (Exception)
                        {
                            ProgressText.Text = "无法重新连接目标设备";
                        }
                    }
                }
                else
                {
                    if (SecondaryButtonText == "Abort")
                    {
                        args.Cancel = true;
                        AbortFromHere = true;

                        try
                        {
                            await ObexClient.AbortAsync().ConfigureAwait(true);
                        }
                        catch (Exception)
                        {

                        }
                    }
                    else if (SecondaryButtonText == "Retry")
                    {
                        args.Cancel = true;
                        ProgressText.Text = "0%";

                        ObexClient.DataTransferFailed -= ObexClient_DataTransferFailed;
                        ObexClient.DataTransferProgressed -= ObexClient_DataTransferProgressed;
                        ObexClient.DataTransferSucceeded -= ObexClient_DataTransferSucceeded;
                        ObexClient.ConnectionFailed -= ObexClient_ConnectionFailed;
                        ObexClient.Aborted -= ObexClient_Aborted;
                        ObexClient.Disconnected -= ObexClient_Disconnected;
                        ObexClient.DeviceConnected -= ObexClient_DeviceConnected;

                        ObexClient = ObexServiceProvider.GetObexNewInstance();

                        ObexClient.DataTransferFailed += ObexClient_DataTransferFailed;
                        ObexClient.DataTransferProgressed += ObexClient_DataTransferProgressed;
                        ObexClient.DataTransferSucceeded += ObexClient_DataTransferSucceeded;
                        ObexClient.ConnectionFailed += ObexClient_ConnectionFailed;
                        ObexClient.Aborted += ObexClient_Aborted;
                        ObexClient.Disconnected += ObexClient_Disconnected;
                        ObexClient.DeviceConnected += ObexClient_DeviceConnected;

                        try
                        {
                            ProgressControl.Value = 0;
                            CloseButtonText = string.Empty;
                            SecondaryButtonText = "Abort";
                            await ObexClient.ConnectAsync().ConfigureAwait(true);
                            await ObexClient.SendFileAsync(FileToSend).ConfigureAwait(true);
                        }
                        catch (Exception)
                        {
                            ProgressText.Text = "Unable to reconnect the target device";
                        }
                    }
                }
            }
            finally
            {
                Deferral.Complete();
            }
        }
    }
}
