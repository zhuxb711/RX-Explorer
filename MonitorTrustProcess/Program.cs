using Microsoft.Toolkit.Deferred;
using SharedLibrary;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Vanara.PInvoke;
using Windows.ApplicationModel;
using Timer = System.Timers.Timer;

namespace MonitorTrustProcess
{
    class Program
    {
        private static Process ExplorerProcess;

        private static ManualResetEvent ExitLocker;

        private static NamedPipeMonitorCommunicationBaseController PipeCommunicationBaseController;

        private static NamedPipeWriteController PipeCommandWriteController;

        private static NamedPipeReadController PipeCommandReadController;

        private static Timer RespondingTimer;

        private static bool IsCrashOrHangMonitorEnabled;

        private static bool IsProcessMonitorIsBeingDebugged;

        private static string RecoveryData;

        [STAThread]
        static void Main(string[] args)
        {
            try
            {
                ExitLocker = new ManualResetEvent(false);

                PipeCommunicationBaseController = new NamedPipeMonitorCommunicationBaseController();
                PipeCommunicationBaseController.OnDataReceived += PipeCommunicationBaseController_OnDataReceived;

                RespondingTimer = new Timer(15000)
                {
                    AutoReset = true,
                    Enabled = true
                };
                RespondingTimer.Elapsed += RespondingTimer_Elapsed;

                if (PipeCommunicationBaseController.WaitForConnectionAsync(10000).Result)
                {
                    ExitLocker.WaitOne();
                }
                else
                {
                    LogTracer.Log($"Could not connect to the explorer. PipeCommunicationBaseController connect timeout. Exiting...");
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, "An unexpected exception was threw in starting MonitorTrustProcess");
            }
            finally
            {
                ExitLocker?.Dispose();
                RespondingTimer?.Dispose();
                PipeCommandWriteController?.Dispose();
                PipeCommandReadController?.Dispose();
                PipeCommunicationBaseController?.Dispose();

                LogTracer.MakeSureLogIsFlushed(2000);

                STAThreadController.Current.Dispose();
            }
        }

        private static async void RespondingTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            if (IsCrashOrHangMonitorEnabled)
            {
                RespondingTimer.Enabled = false;

                try
                {
                    ExplorerProcess?.Refresh();

                    if (!(ExplorerProcess?.HasExited).GetValueOrDefault(true))
                    {
                        if (Helper.GetUWPWindowInformation(Package.Current.Id.FamilyName, Convert.ToUInt32((ExplorerProcess?.Id).GetValueOrDefault())) is WindowInformation UwpInfo)
                        {
                            if (UwpInfo.IsValidInfomation)
                            {
                                IntPtr Result = IntPtr.Zero;

                                if (!UwpInfo.CoreWindowHandle.IsNull)
                                {
                                    if (User32.SendMessageTimeout(UwpInfo.CoreWindowHandle, 0x0000, fuFlags: User32.SMTO.SMTO_ABORTIFHUNG, uTimeout: 10000, lpdwResult: ref Result) == IntPtr.Zero)
                                    {
                                        await CloseAndRestartApplicationAsync(RestartReason.Hang);
                                    }
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogTracer.Log(ex, "Could not finish the responding check");
                }
                finally
                {
                    RespondingTimer.Enabled = true;
                }
            }
        }

        private static void PipeCommunicationBaseController_OnDataReceived(object sender, NamedPipeDataReceivedArgs e)
        {
            if (e.ExtraException is Exception Ex)
            {
                LogTracer.Log(Ex, "Could not receive pipe data");
            }
            else
            {
                EventDeferral Deferral = e.GetDeferral();

                try
                {
                    IDictionary<string, string> Package = JsonSerializer.Deserialize<IDictionary<string, string>>(e.Data);

                    if (Package.TryGetValue("ProcessId", out string ProcessId))
                    {
                        if ((ExplorerProcess?.Id).GetValueOrDefault() != Convert.ToInt32(ProcessId))
                        {
                            ExplorerProcess = Process.GetProcessById(Convert.ToInt32(ProcessId));
                            ExplorerProcess.EnableRaisingEvents = true;
                            ExplorerProcess.Exited += ExplorerProcess_Exited;

                            IsProcessMonitorIsBeingDebugged = Helper.CheckIfProcessIsBeingDebugged(ExplorerProcess.Handle);
                        }
                    }

                    if (PipeCommandReadController != null)
                    {
                        PipeCommandReadController.OnDataReceived -= PipeCommandReadController_OnDataReceived;
                    }

                    if (Package.TryGetValue("PipeCommandWriteId", out string PipeCommandWriteId))
                    {
                        PipeCommandReadController?.Dispose();
                        PipeCommandReadController = new NamedPipeReadController(PipeCommandWriteId);
                        PipeCommandReadController.OnDataReceived += PipeCommandReadController_OnDataReceived;
                    }

                    if (Package.TryGetValue("PipeCommandReadId", out string PipeCommandReadId))
                    {
                        PipeCommandWriteController?.Dispose();
                        PipeCommandWriteController = new NamedPipeWriteController(PipeCommandReadId);
                    }
                }
                catch (Exception ex)
                {
                    LogTracer.Log(ex, $"An exception was threw in get data in {nameof(PipeCommunicationBaseController_OnDataReceived)}");
                }
                finally
                {
                    Deferral.Complete();
                }
            }
        }

        private static void PipeCommandReadController_OnDataReceived(object sender, NamedPipeDataReceivedArgs e)
        {
            EventDeferral Deferral = e.GetDeferral();

            try
            {
                if (e.ExtraException is Exception Ex)
                {
                    LogTracer.Log(Ex, "Could not receive pipe data");
                }
                else
                {
                    IDictionary<string, string> Request = JsonSerializer.Deserialize<IDictionary<string, string>>(e.Data);
                    IDictionary<string, string> Response = HandleCommand(Request);
                    PipeCommandWriteController?.SendData(JsonSerializer.Serialize(Response));
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, "An exception was threw in responding pipe message");
            }
            finally
            {
                Deferral.Complete();
            }
        }

        private static IDictionary<string, string> HandleCommand(IDictionary<string, string> CommandValue)
        {
            IDictionary<string, string> Value = new Dictionary<string, string>();

            try
            {
                switch (Enum.Parse<MonitorCommandType>(CommandValue["CommandType"]))
                {
                    case MonitorCommandType.SetRecoveryData:
                        {
                            RecoveryData = CommandValue["Data"];
                            break;
                        }
                    case MonitorCommandType.StartMonitor:
                        {
                            IsCrashOrHangMonitorEnabled = true;
                            break;
                        }
                    case MonitorCommandType.StopMonitor:
                        {
                            IsCrashOrHangMonitorEnabled = false;
                            break;
                        }
                }
            }
            catch (Exception ex)
            {
                Value.Clear();
                Value.Add("Error", ex.Message);
            }

            return Value;
        }

        private static async void ExplorerProcess_Exited(object sender, EventArgs e)
        {
            if (IsCrashOrHangMonitorEnabled && !IsProcessMonitorIsBeingDebugged)
            {
                await CloseAndRestartApplicationAsync(RestartReason.Crash);
            }
            else
            {
                ExitLocker.Set();
            }
        }

        private static async Task CloseAndRestartApplicationAsync(RestartReason Reason)
        {
            try
            {
                ExplorerProcess.EnableRaisingEvents = false;
                ExplorerProcess.Exited -= ExplorerProcess_Exited;

                ExplorerProcess?.Kill();

                if (string.IsNullOrEmpty(RecoveryData))
                {
                    await Helper.LaunchApplicationFromPackageFamilyNameAsync(Package.Current.Id.FamilyName);
                }
                else
                {
                    await Helper.LaunchApplicationFromPackageFamilyNameAsync(Package.Current.Id.FamilyName, $"/Recovery:{Reason switch { RestartReason.Crash => "Crash", RestartReason.Hang => "Hang", _ => throw new NotSupportedException() }}", Convert.ToBase64String(Encoding.UTF8.GetBytes(RecoveryData)));
                }

                ExitLocker.Set();
            }
            catch (Exception)
            {
                //No need to handle this exception
            }
        }
    }
}