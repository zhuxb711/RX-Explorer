using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
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
        private static readonly List<ServerAndClientPair> ServiceAndClientConnections = new List<ServerAndClientPair>();
        private static readonly object Locker = new object();

        public async void Run(IBackgroundTaskInstance taskInstance)
        {
            Deferral = taskInstance.GetDeferral();

            taskInstance.Canceled += TaskInstance_Canceled;

            AppServiceConnection IncomeConnection = (taskInstance.TriggerDetails as AppServiceTriggerDetails).AppServiceConnection;

            IncomeConnection.RequestReceived += Connection_RequestReceived;

            AppServiceResponse Response = await IncomeConnection.SendMessageAsync(new ValueSet { { "ExecuteType", "Identity" } });

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
                                    if (Response.Message.TryGetValue("PreviousExplorerId", out object PreviousExplorerId) && Response.Message.TryGetValue("PreviousConnectionId", out object PreviousConnectionId))
                                    {
                                        if (ServiceAndClientConnections.FirstOrDefault((Pair) => Pair.ClientProcessId == Convert.ToString(PreviousExplorerId) && Pair.ClientConnectionId == Convert.ToString(PreviousConnectionId)) is ServerAndClientPair ConnectionPair)
                                        {
                                            if (ConnectionPair.Server != null)
                                            {
                                                ConnectionPair.Server.Dispose();
                                            }

                                            ConnectionPair.Server = IncomeConnection;
                                        }
                                        else
                                        {
                                            throw new InvalidDataException("Could not find PreviousExplorerId in ServiceAndClientConnections");
                                        }
                                    }
                                    else
                                    {
                                        if (ServiceAndClientConnections.FirstOrDefault((Pair) => Pair.Server == null) is ServerAndClientPair ConnectionPair)
                                        {
                                            ConnectionPair.Server = IncomeConnection;
                                        }
                                        else
                                        {
                                            ServiceAndClientConnections.Add(new ServerAndClientPair
                                            {
                                                Server = IncomeConnection
                                            });
                                        }
                                    }

                                    break;
                                }
                            case "UWP":
                                {
                                    if (Response.Message.TryGetValue("ProcessId", out object ProcessId) && Response.Message.TryGetValue("ConnectionId", out object ConnectionId))
                                    {
                                        if (ServiceAndClientConnections.FirstOrDefault((Pair) => Pair.Client == null) is ServerAndClientPair ConnectionPair)
                                        {
                                            ConnectionPair.Client = IncomeConnection;
                                            ConnectionPair.ClientProcessId = Convert.ToString(ProcessId);
                                            ConnectionPair.ClientConnectionId = Convert.ToString(ConnectionId);
                                        }
                                        else
                                        {
                                            ServiceAndClientConnections.Add(new ServerAndClientPair
                                            {
                                                Client = IncomeConnection,
                                                ClientProcessId = Convert.ToString(ProcessId),
                                                ClientConnectionId = Convert.ToString(ConnectionId)
                                            });
                                        }
                                    }
                                    else
                                    {
                                        throw new InvalidDataException("Must contains ProcessId in response");
                                    }

                                    break;
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
                AppServiceConnection ServerConnection = null;

                using (CancellationTokenSource Cancel = new CancellationTokenSource(5000))
                {
                    SpinWait Spin = new SpinWait();

                    while (!Cancel.IsCancellationRequested)
                    {
                        if (ServiceAndClientConnections.FirstOrDefault((Pair) => Pair.Client == sender)?.Server is AppServiceConnection Server)
                        {
                            ServerConnection = Server;
                            break;
                        }

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
                    { "Error", "Some exceptions were threw while transmitting the message" }
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
            try
            {
                if ((sender.TriggerDetails as AppServiceTriggerDetails)?.AppServiceConnection is AppServiceConnection DisConnection)
                {
                    lock (Locker)
                    {
                        try
                        {
                            DisConnection.RequestReceived -= Connection_RequestReceived;

                            if (ServiceAndClientConnections.FirstOrDefault((Pair) => Pair.Server == DisConnection || Pair.Client == DisConnection) is ServerAndClientPair ConnectionPair)
                            {
                                if (ConnectionPair.Server == DisConnection)
                                {
                                    ConnectionPair.Server = null;
                                }
                                else
                                {
                                    ServiceAndClientConnections.Remove(ConnectionPair);

                                    ConnectionPair.Client = null;

                                    ConnectionPair.Server?.SendMessageAsync(new ValueSet { { "ExecuteType", "Execute_Exit" } }).AsTask().ConfigureAwait(false).GetAwaiter().GetResult();
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
