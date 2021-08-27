using ShareClassLibrary;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.AppService;
using Windows.ApplicationModel.Background;
using Windows.Foundation.Collections;

namespace CommunicateService
{
    public sealed class Service : IBackgroundTask
    {
        private BackgroundTaskDeferral Deferral;
        private static readonly ConcurrentDictionary<AppServiceConnection, AppServiceConnection> FullTrustConnections = new ConcurrentDictionary<AppServiceConnection, AppServiceConnection>();
        private static readonly ConcurrentDictionary<AppServiceConnection, AppServiceConnection> UWPConnections = new ConcurrentDictionary<AppServiceConnection, AppServiceConnection>();
        private static readonly Queue<AppServiceConnection> ClientWaitingQueue = new Queue<AppServiceConnection>();
        private static readonly Queue<AppServiceConnection> ServerWaitingrQueue = new Queue<AppServiceConnection>();
        private static readonly object Locker = new object();

        public async void Run(IBackgroundTaskInstance taskInstance)
        {
            Deferral = taskInstance.GetDeferral();

            taskInstance.Canceled += TaskInstance_Canceled;

            if (taskInstance.TriggerDetails is AppServiceTriggerDetails Trigger)
            {
                AppServiceConnection IncomeConnection = Trigger.AppServiceConnection;

                IncomeConnection.RequestReceived += Connection_RequestReceived;

                AppServiceResponse Response = await IncomeConnection.SendMessageAsync(new ValueSet { { "CommandType", Enum.GetName(typeof(CommandType), CommandType.Identity) } });

                if (Response.Status == AppServiceResponseStatus.Success)
                {
                    if (Response.Message.TryGetValue("Identity", out object Identity))
                    {
                        lock (Locker)
                        {
                            switch (Convert.ToString(Identity))
                            {
                                case "FullTrustProcess":
                                    {
                                        if (ClientWaitingQueue.TryDequeue(out AppServiceConnection ClientConnection))
                                        {
                                            UWPConnections.AddOrUpdate(ClientConnection, IncomeConnection, (_, _) => IncomeConnection);
                                            FullTrustConnections.AddOrUpdate(IncomeConnection, ClientConnection, (_, _) => ClientConnection);
                                        }
                                        else
                                        {
                                            ServerWaitingrQueue.Enqueue(IncomeConnection);
                                        }

                                        break;
                                    }
                                case "UWP":
                                    {
                                        if (ServerWaitingrQueue.TryDequeue(out AppServiceConnection ServerConnection))
                                        {
                                            UWPConnections.AddOrUpdate(IncomeConnection, ServerConnection, (_, _) => ServerConnection);
                                            FullTrustConnections.AddOrUpdate(ServerConnection, IncomeConnection, (_, _) => IncomeConnection);
                                        }
                                        else
                                        {
                                            ClientWaitingQueue.Enqueue(IncomeConnection);
                                        }

                                        break;
                                    }
                            }
                        }
                    }
                }
            }
        }

        private async void Connection_RequestReceived(AppServiceConnection sender, AppServiceRequestReceivedEventArgs args)
        {
            AppServiceDeferral Deferral = args.GetDeferral();

            try
            {
                SpinWait Spin = new SpinWait();
                DateTimeOffset StartTime = DateTimeOffset.Now;

                AppServiceConnection ServerConnection = null;

                while (!UWPConnections.TryGetValue(sender, out ServerConnection) && (DateTimeOffset.Now - StartTime).TotalMilliseconds < 5000)
                {
                    if (Spin.NextSpinWillYield)
                    {
                        await Task.Delay(500);
                    }
                    else
                    {
                        Spin.SpinOnce();
                    }
                }

                if (ServerConnection != null)
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
                    await args.Request.SendResponseAsync(new ValueSet { { "Error", "Failed to wait a server connection within the specified time" } });
                }
            }
            catch (Exception ex)
            {
                await args.Request.SendResponseAsync(new ValueSet { { "Error", $"Some exceptions were threw while transmitting the message, exception: {ex}, message: {ex.Message}" } });
            }
            finally
            {
                Deferral.Complete();
            }
        }

        private void TaskInstance_Canceled(IBackgroundTaskInstance sender, BackgroundTaskCancellationReason reason)
        {
            try
            {
                if (sender.TriggerDetails is AppServiceTriggerDetails Details)
                {
                    if (Details.AppServiceConnection is AppServiceConnection DisConnection)
                    {
                        try
                        {
                            DisConnection.RequestReceived -= Connection_RequestReceived;

                            lock (Locker)
                            {
                                ValueSet Value = new ValueSet
                                {
                                    { "CommandType", Enum.GetName(typeof(CommandType), CommandType.AppServiceCancelled) },
                                    { "Reason", Enum.GetName(typeof(BackgroundTaskCancellationReason), reason) }
                                };

                                if (FullTrustConnections.TryRemove(DisConnection, out AppServiceConnection UWPConnection))
                                {
                                    Task.WaitAny(UWPConnection.SendMessageAsync(Value).AsTask(), Task.Delay(2000));
                                }
                                else if (UWPConnections.TryRemove(DisConnection, out AppServiceConnection FullTrustConnection))
                                {
                                    Task.WaitAny(FullTrustConnection.SendMessageAsync(Value).AsTask(), Task.Delay(2000));
                                }
                            }
                        }
                        finally
                        {
                            DisConnection.Dispose();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error was threw in CommunicateService: {ex.Message}");
            }
            finally
            {
                Deferral.Complete();
            }
        }
    }
}
