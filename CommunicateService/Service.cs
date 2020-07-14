using System;
using System.Collections.Generic;
using System.Threading;
using Windows.ApplicationModel.AppService;
using Windows.ApplicationModel.Background;
using Windows.Foundation.Collections;

namespace CommunicateService
{
    public sealed class Service : IBackgroundTask
    {
        private BackgroundTaskDeferral Deferral;
        private static readonly Dictionary<AppServiceConnection, string> ClientConnections = new Dictionary<AppServiceConnection, string>();
        private static AppServiceConnection ServerConnection;
        private static readonly object Locker = new object();

        public async void Run(IBackgroundTaskInstance taskInstance)
        {
            Deferral = taskInstance.GetDeferral();

            taskInstance.Canceled += TaskInstance_Canceled;

            AppServiceConnection IncomeConnection = (taskInstance.TriggerDetails as AppServiceTriggerDetails).AppServiceConnection;

            IncomeConnection.RequestReceived += Connection_RequestReceived;

            AppServiceResponse Response = await IncomeConnection.SendMessageAsync(new ValueSet { { "ExcuteType", "Identity" } });

            if (Response.Status == AppServiceResponseStatus.Success && Response.Message.ContainsKey("Identity"))
            {
                switch (Response.Message["Identity"])
                {
                    case "FullTrustProcess":
                        {
                            lock (Locker)
                            {
                                if (ServerConnection != null)
                                {
                                    ServerConnection.Dispose();
                                    ServerConnection = null;
                                }

                                ServerConnection = IncomeConnection;
                            }

                            break;
                        }
                    case "UWP":
                        {
                            lock (Locker)
                            {
                                string Guid = Convert.ToString(Response.Message["Guid"]);
                                ClientConnections.Add(IncomeConnection, Guid);
                            }

                            break;
                        }
                }
            }
        }

        private async void Connection_RequestReceived(AppServiceConnection sender, AppServiceRequestReceivedEventArgs args)
        {
            AppServiceDeferral Deferral = args.GetDeferral();

            try
            {
                if (SpinWait.SpinUntil(() => ServerConnection != null, 5000))
                {
                    AppServiceResponse ServerRespose = await ServerConnection.SendMessageAsync(args.Request.Message);

                    if (ServerRespose.Status == AppServiceResponseStatus.Success)
                    {
                        await args.Request.SendResponseAsync(ServerRespose.Message);
                    }
                    else
                    {
                        await args.Request.SendResponseAsync(new ValueSet { { "Error", "Can't not send message to server" } });
                    }
                }
                else
                {
                    ValueSet Value = new ValueSet
                    {
                        { "Error", "Failed to wait a server connection within the specified time" }
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

                DisConnection.RequestReceived -= Connection_RequestReceived;

                if (ClientConnections.ContainsKey(DisConnection))
                {
                    ServerConnection.SendMessageAsync(new ValueSet { { "ExcuteType", "Excute_RequestClosePipe" }, { "Guid", ClientConnections[DisConnection] } }).AsTask().ConfigureAwait(false).GetAwaiter().GetResult();

                    ClientConnections.Remove(DisConnection);

                    DisConnection.Dispose();

                    if (ClientConnections.Count == 0)
                    {
                        ServerConnection.SendMessageAsync(new ValueSet { { "ExcuteType", "Excute_Exit" } }).AsTask().ConfigureAwait(false).GetAwaiter().GetResult();
                    }
                }
                else
                {
                    if (ReferenceEquals(DisConnection, ServerConnection))
                    {
                        ServerConnection.Dispose();
                        ServerConnection = null;
                    }
                    else
                    {
                        DisConnection.Dispose();
                    }
                }
            }

            Deferral.Complete();
        }
    }
}
