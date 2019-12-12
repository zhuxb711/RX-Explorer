using Bluetooth.Services.Obex;
using System;
using System.IO;
using Windows.Storage;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;


namespace FileManager
{
    public sealed partial class BluetoothFileTransfer : QueueContentDialog
    {
        public Stream StreamToSend { private get; set; }
        public StorageFile FileToSend { private get; set; }
        public bool UseStorageFileRatherThanStream { private get; set; } = false;
        public string FileName { private get; set; }
        private StorageFile ToDeleteFile;
        private ObexService ObexClient = ObexServiceProvider.GetObexNewInstance();
        private bool AbortFromHere = false;
        public BluetoothFileTransfer()
        {
            InitializeComponent();
            Loaded += BluetoothFileTransfer_Loaded;
            Closing += BluetoothFileTransfer_Closing;
        }

        private async void BluetoothFileTransfer_Closing(ContentDialog sender, ContentDialogClosingEventArgs args)
        {
            if (!UseStorageFileRatherThanStream)
            {
                await ToDeleteFile.DeleteAsync(StorageDeleteOption.PermanentDelete);
                ToDeleteFile = null;
            }

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

            await ObexClient.ConnectAsync();

            if (UseStorageFileRatherThanStream)
            {
                await ObexClient.SendFileAsync(FileToSend);
            }
            else
            {
                StorageFile file = await ApplicationData.Current.TemporaryFolder.CreateFileAsync(FileName, CreationCollisionOption.ReplaceExisting);
                using (Stream stream = await file.OpenStreamForWriteAsync())
                {
                    using (StreamToSend)
                    {
                        await StreamToSend.CopyToAsync(stream);
                    }
                }
                ToDeleteFile = file;

                await ObexClient.SendFileAsync(file);
            }
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

            if (Globalization.Language == LanguageEnum.Chinese)
            {
                if (SecondaryButtonText == "中止")
                {
                    args.Cancel = true;
                    AbortFromHere = true;

                    try
                    {
                        await ObexClient.AbortAsync();
                    }
                    catch (Exception) { }
                }
                else if (SecondaryButtonText == "重试")
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
                        CloseButtonText = "";
                        SecondaryButtonText = "中止";
                        await ObexClient.ConnectAsync();
                        await ObexClient.SendFileAsync(ToDeleteFile);
                    }
                    catch (Exception)
                    {
                        ProgressText.Text = "尝试重新连接失败";
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
                        await ObexClient.AbortAsync();
                    }
                    catch (Exception) { }
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
                        CloseButtonText = "";
                        SecondaryButtonText = "Abort";
                        await ObexClient.ConnectAsync();
                        await ObexClient.SendFileAsync(ToDeleteFile);
                    }
                    catch (Exception)
                    {
                        ProgressText.Text = "Trying to reconnect failed";
                    }
                }
            }
            Deferral.Complete();
        }
    }
}
