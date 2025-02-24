using Newtonsoft.Json;
using SharedLibrary;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.Storage;

namespace RX_Explorer.Class
{
    public static class MonitorTrustProcessController
    {
        private static NamedPipeReadController PipeCommandReadController;
        private static NamedPipeWriteController PipeCommandWriteController;
        private static NamedPipeMonitorCommunicationBaseController PipeCommunicationBaseController;
        private readonly static ConcurrentQueue<InternalCommandQueueItem> CommandQueue = new ConcurrentQueue<InternalCommandQueueItem>();

        public static bool IsConnected => (PipeCommandWriteController?.IsConnected).GetValueOrDefault() && (PipeCommandReadController?.IsConnected).GetValueOrDefault();

        public static async Task InitializeAsync()
        {
            try
            {
                if (WindowsVersionChecker.IsNewerOrEqual(WindowsVersion.Windows11))
                {
                    await FullTrustProcessLauncher.LaunchFullTrustProcessForCurrentAppWithArgumentsAsync("--MonitorTrustProcess");
                }
                else
                {
                    await FullTrustProcessLauncher.LaunchFullTrustProcessForCurrentAppAsync("MonitorTrustProcess");
                }

                if (!await ConnectRemoteAsync())
                {
                    throw new Exception();
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, "Could not create or connect to monitor process as expected");
            }
        }

        public static async Task<bool> RegisterRestartRequestAsync(string RecoveryData)
        {
            if (await SendCommandAsync(MonitorCommandType.RegisterRestartRequest, ("RecoveryData", RecoveryData)) is IDictionary<string, string> Response)
            {
                if (Response.ContainsKey("Success"))
                {
                    return true;
                }
                else if (Response.TryGetValue("Error", out string ErrorMessage))
                {
                    LogTracer.Log($"An unexpected error was threw in {nameof(RegisterRestartRequestAsync)}, message: {ErrorMessage}");
                }
            }

            return false;
        }

        public static async Task<bool> SetRecoveryDataAsync(string RecoveryData)
        {
            if (await SendCommandAsync(MonitorCommandType.SetRecoveryData, ("RecoveryData", RecoveryData)) is IDictionary<string, string> Response)
            {
                if (Response.ContainsKey("Success"))
                {
                    return true;
                }
                else if (Response.TryGetValue("Error", out string ErrorMessage))
                {
                    LogTracer.Log($"An unexpected error was threw in {nameof(SetRecoveryDataAsync)}, message: {ErrorMessage}");
                }
            }

            return false;
        }

        public static async Task<bool> StartMonitorAsync()
        {
            if (await SendCommandAsync(MonitorCommandType.StartMonitor) is IDictionary<string, string> Response)
            {
                if (Response.ContainsKey("Success"))
                {
                    return true;
                }
                else if (Response.TryGetValue("Error", out string ErrorMessage))
                {
                    LogTracer.Log($"An unexpected error was threw in {nameof(StartMonitorAsync)}, message: {ErrorMessage}");
                }
            }

            return false;
        }

        public static async Task<bool> StopMonitorAsync()
        {
            if (await SendCommandAsync(MonitorCommandType.StopMonitor) is IDictionary<string, string> Response)
            {
                if (Response.ContainsKey("Success"))
                {
                    return true;
                }
                else if (Response.TryGetValue("Error", out string ErrorMessage))
                {
                    LogTracer.Log($"An unexpected error was threw in {nameof(StopMonitorAsync)}, message: {ErrorMessage}");
                }
            }

            return false;
        }

        public static async Task<bool> EnableFeatureAsync(MonitorFeature Feature)
        {
            if (await SendCommandAsync(MonitorCommandType.EnableFeature, ("Feature", Enum.GetName(typeof(MonitorFeature), Feature))) is IDictionary<string, string> Response)
            {
                if (Response.ContainsKey("Success"))
                {
                    return true;
                }
                else if (Response.TryGetValue("Error", out string ErrorMessage))
                {
                    LogTracer.Log($"An unexpected error was threw in {nameof(EnableFeatureAsync)}, message: {ErrorMessage}");
                }
            }

            return false;
        }

        public static async Task<bool> DisableFeatureAsync(MonitorFeature Feature)
        {
            if (await SendCommandAsync(MonitorCommandType.DisableFeature, ("Feature", Enum.GetName(typeof(MonitorFeature), Feature))) is IDictionary<string, string> Response)
            {
                if (Response.ContainsKey("Success"))
                {
                    return true;
                }
                else if (Response.TryGetValue("Error", out string ErrorMessage))
                {
                    LogTracer.Log($"An unexpected error was threw in {nameof(DisableFeatureAsync)}, message: {ErrorMessage}");
                }
            }

            return false;
        }

        private static async Task<IDictionary<string, string>> SendCommandAsync(MonitorCommandType Type, params (string Key, string Value)[] Arguments)
        {
            try
            {
                if (IsConnected)
                {
                    InternalCommandQueueItem CommandItem = new InternalCommandQueueItem();
                    CommandQueue.Enqueue(CommandItem);
                    PipeCommandWriteController.SendData(JsonConvert.SerializeObject(new Dictionary<string, string>(Arguments.Select((Argument) => new KeyValuePair<string, string>(Argument.Key, Argument.Value)).Prepend(new KeyValuePair<string, string>("CommandType", Enum.GetName(typeof(MonitorCommandType), Type))))));
                    return await CommandItem.TaskSource.Task;
                }
                else
                {
                    throw new Exception("Connection between monitor process was lost");
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"{nameof(SendCommandAsync)} throw an exception");
            }

            return null;
        }

        private static async Task<bool> ConnectRemoteAsync()
        {
            try
            {
                if (IsConnected)
                {
                    return true;
                }

                PipeCommunicationBaseController?.Dispose();
                PipeCommunicationBaseController = new NamedPipeMonitorCommunicationBaseController();

                if (await PipeCommunicationBaseController.WaitForConnectionAsync(TimeSpan.FromSeconds(15)))
                {
                    for (int RetryCount = 1; RetryCount <= 3; RetryCount++)
                    {
                        if (PipeCommandReadController != null)
                        {
                            PipeCommandReadController.OnDataReceived -= PipeCommandReadController_OnDataReceived;
                        }

                        PipeCommandWriteController?.Dispose();
                        PipeCommandReadController?.Dispose();

                        PipeCommandReadController = new NamedPipeReadController();
                        PipeCommandWriteController = new NamedPipeWriteController();

                        Dictionary<string, string> Command = new Dictionary<string, string>
                        {
                            { "ProcessId", Convert.ToString(Process.GetCurrentProcess().Id) },
                            { "PipeCommandReadId", PipeCommandReadController.PipeId },
                            { "PipeCommandWriteId", PipeCommandWriteController.PipeId },
                            { "LogRecordFolderPath", ApplicationData.Current.TemporaryFolder.Path }
                        };

                        PipeCommunicationBaseController.SendData(JsonConvert.SerializeObject(Command));

                        if ((await Task.WhenAll(PipeCommandWriteController.WaitForConnectionAsync(TimeSpan.FromSeconds(15)),
                                                PipeCommandReadController.WaitForConnectionAsync(TimeSpan.FromSeconds(15))))
                                       .All((Connected) => Connected))
                        {
                            PipeCommandReadController.OnDataReceived += PipeCommandReadController_OnDataReceived;
                            return true;
                        }
                        else
                        {
                            LogTracer.Log($"Try connect to monitor process in {RetryCount} times");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"An unexpected exception was threw in {nameof(ConnectRemoteAsync)}");
            }

            return false;
        }

        private static void PipeCommandReadController_OnDataReceived(object sender, NamedPipeDataReceivedArgs e)
        {
            if (CommandQueue.TryDequeue(out InternalCommandQueueItem CommandObject))
            {
                bool ResponseSet;

                if (e.ExtraException is Exception Ex)
                {
                    ResponseSet = CommandObject.TaskSource.TrySetException(Ex);
                }
                else
                {
                    try
                    {
                        ResponseSet = CommandObject.TaskSource.TrySetResult(JsonConvert.DeserializeObject<IDictionary<string, string>>(e.Data));
                    }
                    catch (Exception ex)
                    {
                        ResponseSet = CommandObject.TaskSource.TrySetException(ex);
                    }
                }

                if (!ResponseSet && !CommandObject.TaskSource.TrySetCanceled())
                {
                    LogTracer.Log($"{nameof(MonitorTrustProcessController)} could not set the response properly");
                }
            }
        }

        private sealed class InternalCommandQueueItem
        {
            public TaskCompletionSource<IDictionary<string, string>> TaskSource { get; }

            public InternalCommandQueueItem()
            {
                TaskSource = new TaskCompletionSource<IDictionary<string, string>>();
            }
        }
    }
}
