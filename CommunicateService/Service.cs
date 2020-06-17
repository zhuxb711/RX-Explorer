using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Windows.ApplicationModel.AppService;
using Windows.ApplicationModel.Background;
using Windows.Foundation.Collections;

namespace CommunicateService
{
    public sealed class Service : IBackgroundTask
    {
        private BackgroundTaskDeferral Deferral;
        private static readonly List<AppServiceConnection> Connections = new List<AppServiceConnection>();
        private static readonly object Locker = new object();

        public void Run(IBackgroundTaskInstance taskInstance)
        {
            Deferral = taskInstance.GetDeferral();

            taskInstance.Canceled += TaskInstance_Canceled;

            AppServiceConnection IncomeConnection = (taskInstance.TriggerDetails as AppServiceTriggerDetails).AppServiceConnection;

            Connections.Add(IncomeConnection);

            IncomeConnection.RequestReceived += Connection_RequestReceived;
        }

        private async void Connection_RequestReceived(AppServiceConnection sender, AppServiceRequestReceivedEventArgs args)
        {
            AppServiceDeferral Deferral = args.GetDeferral();

            try
            {
                if (SpinWait.SpinUntil(() => Connections.Count == 2, 8000))
                {
                    AppServiceConnection AnotherConnection = Connections.FirstOrDefault((Con) => Con != sender);

                    AppServiceResponse AnotherRespose = await AnotherConnection.SendMessageAsync(args.Request.Message);

                    if (AnotherRespose.Status == AppServiceResponseStatus.Success)
                    {
                        await args.Request.SendResponseAsync(AnotherRespose.Message);
                    }
                }
                else
                {
                    ValueSet Value = new ValueSet
                    {
                        { "Error", "Another device failed to make a peer-to-peer connection within the specified time" }
                    };

                    await args.Request.SendResponseAsync(Value);
                }
            }
            catch
            {
                ValueSet Value = new ValueSet
                {
                    { "Error", "Some exceptions were thrown while transmitting the message" }
                };

                await args.Request.SendResponseAsync(Value);
            }
            finally
            {
                Deferral.Complete();
            }
        }

        private void TaskInstance_Canceled(IBackgroundTaskInstance sender, BackgroundTaskCancellationReason reason)
        {
            lock (Locker)
            {
                AppServiceConnection DisConnection = (sender.TriggerDetails as AppServiceTriggerDetails).AppServiceConnection;

                if (Connections.Contains(DisConnection))
                {
                    Connections.Remove(DisConnection);
                    DisConnection.Dispose();
                }

                Deferral.Complete();
            }
        }
    }
}
