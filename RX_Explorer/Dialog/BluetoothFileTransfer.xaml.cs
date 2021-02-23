using Bluetooth.Services.Obex;
using RX_Explorer.Class;
using System;
using Windows.Storage;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;


namespace RX_Explorer.Dialog
{
    public sealed partial class BluetoothFileTransfer : QueueContentDialog
    {
        public StorageFile FileToSend { get; private set; }

        private ObexService ObexClient;

        private bool AbortFromHere;

        public BluetoothFileTransfer(StorageFile FileToSend)
        {
            InitializeComponent();
            this.FileToSend = FileToSend ?? throw new ArgumentNullException(nameof(FileToSend), "Parameter could not be null");

            ObexClient = ObexServiceProvider.GetObexInstance();

            TransferName.Text = $"{Globalization.GetString("Bluetooth_Transfer_FileName")}: {FileToSend.Name}";
            TransferDeviceName.Text = $"{Globalization.GetString("Bluetooth_Transfer_DeviceName")}: {ObexServiceProvider.DeviceName}";

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
                Title = Globalization.GetString("Bluetooth_Transfer_Status_1");
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
                Title = Globalization.GetString("Bluetooth_Transfer_Status_2");
                ProgressText.Text = Globalization.GetString("Bluetooth_Transfer_Description_1");
                CloseButtonText = Globalization.GetString("Bluetooth_Transfer_ExitButton");
                SecondaryButtonText = Globalization.GetString("Bluetooth_Transfer_RetryButton");
            });
        }

        private async void ObexClient_Aborted(object sender, EventArgs e)
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                Title = Globalization.GetString("Bluetooth_Transfer_Status_2");
                ProgressText.Text = Globalization.GetString("Bluetooth_Transfer_Description_2");
                CloseButtonText = Globalization.GetString("Bluetooth_Transfer_ExitButton");
                SecondaryButtonText = Globalization.GetString("Bluetooth_Transfer_RetryButton");
            });
        }

        private async void ObexClient_ConnectionFailed(object sender, ConnectionFailedEventArgs e)
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                Title = Globalization.GetString("Bluetooth_Transfer_Status_2");
                ProgressText.Text = $"{Globalization.GetString("Bluetooth_Transfer_Description_2")} {e.ExceptionObject.Message}";
                CloseButtonText = Globalization.GetString("Bluetooth_Transfer_ExitButton");
                SecondaryButtonText = Globalization.GetString("Bluetooth_Transfer_RetryButton");
            });
        }

        private async void ObexClient_DataTransferSucceeded(object sender, EventArgs e)
        {
            AbortFromHere = true;
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                Title = Globalization.GetString("Bluetooth_Transfer_Status_3");
                ProgressControl.Value = 100;
                ProgressText.Text = $"100%{Environment.NewLine}{Globalization.GetString("Bluetooth_Transfer_Description_4")}";
                SecondaryButtonText = Globalization.GetString("Common_Dialog_CloseButton");
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
                Title = Globalization.GetString("Bluetooth_Transfer_Status_2");
                ProgressText.Text = $"{Globalization.GetString("Bluetooth_Transfer_Description_5")} {e.ExceptionObject.Message}";
                CloseButtonText = Globalization.GetString("Bluetooth_Transfer_ExitButton");
                SecondaryButtonText = Globalization.GetString("Bluetooth_Transfer_RetryButton");
            });
        }

        private async void QueueContentDialog_SecondaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            var Deferral = args.GetDeferral();

            try
            {
                if (SecondaryButtonText == Globalization.GetString("Bluetooth_Transfer_RetryButton"))
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

                    ObexClient = ObexServiceProvider.GetObexInstance();

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
                        SecondaryButtonText = Globalization.GetString("BluetoothTranfer.SecondaryButtonText");
                        await ObexClient.ConnectAsync().ConfigureAwait(true);
                        await ObexClient.SendFileAsync(FileToSend).ConfigureAwait(true);
                    }
                    catch (Exception)
                    {
                        ProgressText.Text = Globalization.GetString("Bluetooth_Transfer_Description_6");
                    }
                }
                else
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
            }
            finally
            {
                Deferral.Complete();
            }
        }
    }
}
