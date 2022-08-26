using Nito.AsyncEx;
using RX_Explorer.Dialog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel.Core;
using Windows.UI.Core;
using Windows.UI.Xaml.Controls;

namespace RX_Explorer.Class
{
    public static class FtpClientManager
    {
        private static readonly AsyncLock GetLocker = new AsyncLock();
        private static readonly AsyncLock CreateLocker = new AsyncLock();
        private static readonly List<FtpClientController> ControllerList = new List<FtpClientController>();

        public static async Task<FtpClientController> CreateClientControllerAsync(FtpPathAnalysis Analysis)
        {
            using (await CreateLocker.LockAsync())
            {
                try
                {
                    FtpClientController NewClient = await CoreApplication.MainView.CoreWindow.Dispatcher.RunAndWaitAsyncTask(CoreDispatcherPriority.Normal, async () =>
                    {
                        FTPCredentialDialog Dialog = new FTPCredentialDialog(Analysis);

                        if (await Dialog.ShowAsync() == ContentDialogResult.Primary)
                        {
                            return Dialog.FtpController;
                        }

                        return null;
                    });

                    if (NewClient != null)
                    {
                        ControllerList.Add(NewClient);
                    }

                    return NewClient;
                }
                catch (Exception ex)
                {
                    LogTracer.Log(ex, "Could not get the ftp client as expected");
                }

                return null;
            }
        }

        public static async Task<FtpClientController> GetClientControllerAsync(FtpPathAnalysis Analysis)
        {
            using (await GetLocker.LockAsync())
            {
                try
                {
                    if (ControllerList.FirstOrDefault((Controller) => Controller.ServerHost == Analysis.Host && Controller.ServerPort == Analysis.Port) is FtpClientController ExistController)
                    {
                        try
                        {
                            return await FtpClientController.MakeSureConnectionAndCloseOnceFailedAsync(ExistController);
                        }
                        catch (Exception)
                        {
                            ControllerList.Remove(ExistController);
                        }

                        return await CreateClientControllerAsync(Analysis);
                    }
                }
                catch (Exception ex)
                {
                    LogTracer.Log(ex, "Could not get the ftp client as expected");
                }

                return null;
            }
        }

        public static async Task CloseAllClientAsync()
        {
            if (ControllerList.Count > 0)
            {
                try
                {
                    await Task.WhenAll(ControllerList.Select((Controller) => Task.Run(() => Controller.Dispose())));
                }
                catch (Exception ex)
                {
                    LogTracer.Log(ex, "Could not disconnect normally from ftp server");
                }
            }
        }
    }
}
