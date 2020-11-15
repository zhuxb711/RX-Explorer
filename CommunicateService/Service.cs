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
                    switch (Convert.ToString(Identity))
                    {
                        case "FullTrustProcess":
                            {
                                lock (Locker)
                                {
                                    if (Response.Message.TryGetValue("PreviousExplorerId", out object PreviousExplorerId))
                                    {
                                        if (ServiceAndClientConnections.FirstOrDefault((Pair) => Pair.ClientProcessId == Convert.ToString(PreviousExplorerId)) is ServerAndClientPair ConnectionPair)
                                        {
                                            if(ConnectionPair.Server != null)
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
                                            ServiceAndClientConnections.Add(new ServerAndClientPair(IncomeConnection, null, string.Empty));
                                        }
                                    }
                                }

                                break;
                            }
                        case "UWP":
                            {
                                lock (Locker)
                                {
                                    if (Response.Message.TryGetValue("ProcessId", out object ProcessId))
                                    {
                                        if (ServiceAndClientConnections.FirstOrDefault((Pair) => Pair.Client == null) is ServerAndClientPair ConnectionPair)
                                        {
                                            ConnectionPair.Client = IncomeConnection;
                                            ConnectionPair.ClientProcessId = Convert.ToString(ProcessId);
                                        }
                                        else
                                        {
                                            ServiceAndClientConnections.Add(new ServerAndClientPair(null, IncomeConnection, Convert.ToString(ProcessId)));
                                        }
                                    }
                                    else
                                    {
                                        throw new InvalidDataException("Must contains ProcessId in response");
                                    }
                                }

                                break;
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
                        ServerConnection = ServiceAndClientConnections.Where((Pair) => Pair.Client == sender).Select((Pair) => Pair.Server).FirstOrDefault();

                        if (ServerConnection != null)
                        {
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
                try
                {
                    if ((sender.TriggerDetails as AppServiceTriggerDetails)?.AppServiceConnection is AppServiceConnection DisConnection)
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
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error thrown in CommuniteService: {ex.Message}");
                }
                finally
                {
                    Deferral.Complete();
                }
            }
        }
    }
}
