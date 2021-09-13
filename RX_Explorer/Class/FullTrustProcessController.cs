using RX_Explorer.Interface;
using ShareClassLibrary;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.ApplicationModel.AppService;
using Windows.Foundation.Collections;
using Windows.Storage;
using Windows.UI.Xaml;

namespace RX_Explorer.Class
{
    /// <summary>
    /// 用于启动具备完全权限的附加程序的控制器
    /// </summary>
    public sealed class FullTrustProcessController : IDisposable
    {
        public const ushort DynamicBackupProcessNum = 2;

        private readonly int CurrentProcessId;

        private bool IsConnected;

        private bool IsDisposed;

        public bool IsAnyActionExcutingInCurrentController { get; private set; }

        public static bool IsAnyActionExcutingInAllControllers
        {
            get
            {
                return AllControllerList.ToArray().Any((Controller) => Controller.IsAnyActionExcutingInCurrentController);
            }
        }

        public static int InUseControllersNum
        {
            get
            {
                return CurrentRunningControllerNum - AvailableControllers.Count;
            }
        }

        public static int AllControllersNum
        {
            get
            {
                return CurrentRunningControllerNum;
            }
        }

        public static int AvailableControllersNum
        {
            get
            {
                return AvailableControllers.Count;
            }
        }

        private readonly static Thread DispatcherThread = new Thread(DispatcherMethod)
        {
            IsBackground = true,
            Priority = ThreadPriority.Normal
        };

        private readonly NamedPipeReadController PipeProgressReadController;

        private readonly NamedPipeReadController PipeCommandReadController;

        private readonly NamedPipeWriteController PipeCommandWriteController;

        private readonly AppServiceConnection Connection;

        private readonly TaskCompletionSource<bool> IdentityTaskCompletionSource;

        private static readonly SynchronizedCollection<FullTrustProcessController> AllControllerList = new SynchronizedCollection<FullTrustProcessController>();

        private static readonly ConcurrentQueue<FullTrustProcessController> AvailableControllers = new ConcurrentQueue<FullTrustProcessController>();

        private static readonly ConcurrentQueue<TaskCompletionSource<ExclusiveUsage>> WaitingTaskQueue = new ConcurrentQueue<TaskCompletionSource<ExclusiveUsage>>();

        private static volatile int CurrentRunningControllerNum;

        private static volatile int LastRequestedControllerNum;

        private static event EventHandler<FullTrustProcessController> ExclusiveDisposed;

        public static event EventHandler<bool> CurrentBusyStatus;

        public static event EventHandler AppServiceConnectionLost;

        public static Task ResizeTask;

        private static readonly AutoResetEvent DispatcherSleepLocker = new AutoResetEvent(false);

        static FullTrustProcessController()
        {
            DispatcherThread.Start();
            ExclusiveDisposed += FullTrustProcessController_ExclusiveDisposed;
            Application.Current.Suspending += Current_Suspending;
            Application.Current.Resuming += Current_Resuming;
        }

        private FullTrustProcessController()
        {
            Interlocked.Increment(ref CurrentRunningControllerNum);

            using (Process CurrentProcess = Process.GetCurrentProcess())
            {
                CurrentProcessId = CurrentProcess.Id;
            }

            AllControllerList.Add(this);

            if (WindowsVersionChecker.IsNewerOrEqual(Version.Windows10_2004))
            {
                if (NamedPipeReadController.TryCreateNamedPipe(out NamedPipeReadController CommandReadController))
                {
                    PipeCommandReadController = CommandReadController;
                }

                if (NamedPipeReadController.TryCreateNamedPipe(out NamedPipeReadController ProgressReadController))
                {
                    PipeProgressReadController = ProgressReadController;
                }

                if (NamedPipeWriteController.TryCreateNamedPipe(out NamedPipeWriteController CommandWriteController))
                {
                    PipeCommandWriteController = CommandWriteController;
                }
            }

            Connection = new AppServiceConnection
            {
                AppServiceName = "CommunicateService",
                PackageFamilyName = Package.Current.Id.FamilyName
            };

            Connection.RequestReceived += Connection_RequestReceived;
            Connection.ServiceClosed += Connection_ServiceClosed;

            IdentityTaskCompletionSource = new TaskCompletionSource<bool>();
        }

        private static void Current_Resuming(object sender, object e)
        {
            LogTracer.Log("RX-Explorer is resuming, recover all instance");

            AllControllerList.Clear();
            AvailableControllers.Clear();

            RequestResizeController(LastRequestedControllerNum);
        }

        private static void Current_Suspending(object sender, SuspendingEventArgs e)
        {
            LogTracer.Log("RX-Explorer is suspending, dispose this instance");
            AllControllerList.ToList().ForEach((Control) => Control.Dispose());
        }

        private static void FullTrustProcessController_ExclusiveDisposed(object sender, FullTrustProcessController Controller)
        {
            AvailableControllers.Enqueue(Controller);
        }

        private static void DispatcherMethod()
        {
            while (true)
            {
                if (WaitingTaskQueue.IsEmpty)
                {
                    DispatcherSleepLocker.WaitOne();
                }

                while (WaitingTaskQueue.TryDequeue(out TaskCompletionSource<ExclusiveUsage> CompletionSource))
                {
                    while (true)
                    {
                        if (AvailableControllers.TryDequeue(out FullTrustProcessController Controller))
                        {
                            if (Controller.IsDisposed)
                            {
                                FullTrustProcessController NewController = null;

                                for (int i = 0; i < 3; i++)
                                {
                                    NewController = CreateAsync().Result;

                                    if (NewController == null)
                                    {
                                        LogTracer.Log($"Dispatcher fould a controller was disposed, but could not recreate a new controller. Retrying execute {nameof(CreateAsync)} in {i + 1} times");
                                    }
                                    else
                                    {
                                        break;
                                    }
                                }

                                if (NewController != null)
                                {
                                    CompletionSource.SetResult(new ExclusiveUsage(NewController, ExtendedExecutionController.TryCreateExtendedExecution().Result));
                                    break;
                                }
                            }
                            else
                            {
                                CompletionSource.SetResult(new ExclusiveUsage(Controller, ExtendedExecutionController.TryCreateExtendedExecution().Result));
                                break;
                            }
                        }
                        else
                        {
                            if (CurrentRunningControllerNum > 0)
                            {
                                if (!SpinWait.SpinUntil(() => !AvailableControllers.IsEmpty, 3000))
                                {
                                    CurrentBusyStatus?.Invoke(null, true);

                                    SpinWait.SpinUntil(() => !AvailableControllers.IsEmpty);

                                    CurrentBusyStatus?.Invoke(null, false);
                                }
                            }
                            else
                            {
                                ResizeController(1);
                            }
                        }
                    }
                }
            }
        }

        public static void RequestResizeController(int RequestedTarget)
        {
            if (ResizeTask == null || ResizeTask.IsCompleted)
            {
                ResizeTask = Task.Run(() => ResizeController(RequestedTarget));
            }
            else
            {
                ResizeTask = ResizeTask.ContinueWith((_) => ResizeController(RequestedTarget), TaskContinuationOptions.PreferFairness);
            }
        }

        private static void ResizeController(int RequestedTarget)
        {
            try
            {
                using (ExtendedExecutionController ExtExecution = ExtendedExecutionController.TryCreateExtendedExecution().Result)
                {
                    LastRequestedControllerNum = RequestedTarget;

                    RequestedTarget += DynamicBackupProcessNum;

                    while (CurrentRunningControllerNum > RequestedTarget && AvailableControllers.Count > DynamicBackupProcessNum)
                    {
                        if (AvailableControllers.TryDequeue(out FullTrustProcessController Controller))
                        {
                            Controller.Dispose();
                        }
                        else
                        {
                            if (!SpinWait.SpinUntil(() => !AvailableControllers.IsEmpty, 3000))
                            {
                                break;
                            }
                        }
                    }

                    while (CurrentRunningControllerNum < RequestedTarget)
                    {
                        FullTrustProcessController NewController = CreateAsync().Result;

                        if (NewController != null)
                        {
                            AvailableControllers.Enqueue(NewController);
                        }
                        else
                        {
                            throw new InvalidOperationException("Could not create a new controller");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, "An exception was threw when maintance FullTrustProcessController");
            }
        }


        public static Task<ExclusiveUsage> GetAvailableController()
        {
            TaskCompletionSource<ExclusiveUsage> CompletionSource = new TaskCompletionSource<ExclusiveUsage>();

            WaitingTaskQueue.Enqueue(CompletionSource);

            if (DispatcherThread.ThreadState.HasFlag(System.Threading.ThreadState.WaitSleepJoin))
            {
                DispatcherSleepLocker.Set();
            }

            return CompletionSource.Task;
        }

        private static async Task<FullTrustProcessController> CreateAsync()
        {
            try
            {
                FullTrustProcessController Controller = new FullTrustProcessController();
                await FullTrustProcessLauncher.LaunchFullTrustProcessForCurrentAppAsync();
                await Controller.ConnectRemoteAsync();
                return Controller;
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, "Could not create FullTrustProcess properly");
                return null;
            }
        }

        private async void Connection_RequestReceived(AppServiceConnection sender, AppServiceRequestReceivedEventArgs args)
        {
            AppServiceDeferral Deferral = args.GetDeferral();

            try
            {
                switch (Enum.Parse<CommandType>(Convert.ToString(args.Request.Message["CommandType"])))
                {
                    case CommandType.Identity:
                        {
                            await args.Request.SendResponseAsync(new ValueSet { { "Identity", "UWP" } });
                            IdentityTaskCompletionSource.TrySetResult(true);
                            break;
                        }
                    case CommandType.AppServiceCancelled:
                        {
                            LogTracer.Log($"AppService is cancelled. It might be due to System Policy or FullTrustProcess exit unexpectedly. Reason: {args.Request.Message["Reason"]}");

                            if (!((PipeCommandReadController?.IsConnected).GetValueOrDefault()
                                   && (PipeCommandWriteController?.IsConnected).GetValueOrDefault()
                                   && (PipeProgressReadController?.IsConnected).GetValueOrDefault()))
                            {
                                Dispose();
                                AppServiceConnectionLost?.Invoke(this, null);
                            }

                            break;
                        }
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex);
            }
            finally
            {
                Deferral.Complete();
            }
        }

        private async Task<bool> ConnectRemoteAsync()
        {
            try
            {
                if (IsDisposed)
                {
                    return false;
                }

                if (!IsConnected)
                {
                    AppServiceConnectionStatus Status = await Connection.OpenAsync();

                    if (Status == AppServiceConnectionStatus.Success)
                    {
                        if (await Task.WhenAny(IdentityTaskCompletionSource.Task, Task.Delay(3000)) != IdentityTaskCompletionSource.Task)
                        {
                            Dispose();
                            LogTracer.Log($"Identity task failed because AppSerive not response and time out. Dispose this instance");
                            IdentityTaskCompletionSource.TrySetResult(false);
                            return false;
                        }
                    }
                    else
                    {
                        Dispose();
                        LogTracer.Log($"Connect to AppService failed, Response status: \"{Enum.GetName(typeof(AppServiceResponseStatus), Status)}\". Dispose this instance");
                        return false;
                    }
                }

                for (int Count = 0; Count < 3; Count++)
                {
                    ValueSet Value = new ValueSet
                    {
                        { "CommandType", Enum.GetName(typeof(CommandType), CommandType.Test_Connection) },
                        { "ProcessId", CurrentProcessId },
                    };

                    if (PipeCommandWriteController != null)
                    {
                        Value.Add("PipeCommandWriteId", PipeCommandWriteController.PipeUniqueId);
                    }

                    if (PipeCommandReadController != null)
                    {
                        Value.Add("PipeCommandReadId", PipeCommandReadController.PipeUniqueId);
                    }

                    if (PipeProgressReadController != null)
                    {
                        Value.Add("PipeProgressReadId", PipeProgressReadController.PipeUniqueId);
                    }

                    AppServiceResponse Response = await Connection.SendMessageAsync(Value);

                    if (Response.Status == AppServiceResponseStatus.Success)
                    {
                        if (Response.Message.ContainsKey(Enum.GetName(typeof(CommandType), CommandType.Test_Connection)))
                        {
                            return IsConnected = true;
                        }
                        else
                        {
                            if (Response.Message.TryGetValue("Error", out object Error))
                            {
                                LogTracer.Log($"Connect to FullTrustProcess failed, reason: \"{Error}\". Retrying...in {Count + 1} times");
                            }

                            await Task.Delay(1000);
                        }
                    }
                }

                LogTracer.Log("Connect to FullTrustProcess failed after retrying 3 times. Dispose this instance");
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"An unexpected error was threw in {nameof(ConnectRemoteAsync)}");
            }

            Dispose();
            return false;
        }

        private void Connection_ServiceClosed(AppServiceConnection sender, AppServiceClosedEventArgs args)
        {
            LogTracer.Log("AppServiceConnection is closed unexpected");

            if (!((PipeCommandReadController?.IsConnected).GetValueOrDefault()
                   && (PipeCommandWriteController?.IsConnected).GetValueOrDefault()
                   && (PipeProgressReadController?.IsConnected).GetValueOrDefault()))
            {
                Dispose();
                AppServiceConnectionLost?.Invoke(this, null);
            }
        }

        private async Task<IDictionary<string, string>> SendCommandAsync(CommandType Type, params (string, string)[] Arguments)
        {
            IsAnyActionExcutingInCurrentController = true;

            try
            {
                if ((PipeCommandReadController?.IsConnected).GetValueOrDefault() && (PipeCommandWriteController?.IsConnected).GetValueOrDefault())
                {
                    Dictionary<string, string> Command = new Dictionary<string, string>
                    {
                        { "CommandType", Enum.GetName(typeof(CommandType), Type) }
                    };

                    foreach ((string, object) Argument in Arguments)
                    {
                        Command.Add(Argument.Item1, Convert.ToString(Argument.Item2));
                    }

                    TaskCompletionSource<IDictionary<string, string>> CompletionSource = new TaskCompletionSource<IDictionary<string, string>>();

                    void PipeReadController_OnDataReceived(object sender, NamedPipeDataReceivedArgs e)
                    {
                        if (e.ExtraException is Exception Ex)
                        {
                            CompletionSource.SetException(Ex);
                        }
                        else
                        {
                            try
                            {
                                CompletionSource.SetResult(JsonSerializer.Deserialize<IDictionary<string, string>>(e.Data));
                            }
                            catch (Exception ex)
                            {
                                CompletionSource.SetException(ex);
                            }
                        }
                    }

                    try
                    {
                        PipeCommandReadController.OnDataReceived += PipeReadController_OnDataReceived;

                        PipeCommandWriteController.SendData(JsonSerializer.Serialize(Command));

                        return await CompletionSource.Task;
                    }
                    finally
                    {
                        PipeCommandReadController.OnDataReceived -= PipeReadController_OnDataReceived;
                    }
                }
                else
                {
                    if (await ConnectRemoteAsync())
                    {
                        ValueSet Command = new ValueSet
                        {
                            { "CommandType", Enum.GetName(typeof(CommandType), Type) }
                        };

                        foreach ((string, string) Argument in Arguments)
                        {
                            Command.Add(Argument.Item1, Argument.Item2);
                        }

                        AppServiceResponse Response = await Connection.SendMessageAsync(Command);

                        if (Response.Status == AppServiceResponseStatus.Success)
                        {
                            Dictionary<string, string> Result = new Dictionary<string, string>(Response.Message.Count);

                            foreach (KeyValuePair<string, object> Pair in Response.Message)
                            {
                                Result.Add(Pair.Key, Convert.ToString(Pair.Value));
                            }

                            return Result;
                        }
                        else
                        {
                            throw new Exception($"AppServiceResponse return an invalid status. Status: {Enum.GetName(typeof(AppServiceResponseStatus), Response.Status)}");
                        }
                    }
                    else
                    {
                        throw new Exception("Failed to connect the AppService");
                    }
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"{nameof(SendCommandAsync)} throw an error");
                return null;
            }
            finally
            {
                IsAnyActionExcutingInCurrentController = false;
            }
        }

        private async Task<IDictionary<string, string>> SendCommandAndReportProgressAsync(CommandType Type, ProgressChangedEventHandler ProgressHandler, params (string, string)[] Arguments)
        {
            IsAnyActionExcutingInCurrentController = true;

            try
            {
                void PipeProgressReadController_OnDataReceived(object sender, NamedPipeDataReceivedArgs e)
                {
                    if (e.ExtraException == null)
                    {
                        ProgressHandler?.Invoke(null, new ProgressChangedEventArgs(Convert.ToInt32(e.Data), null));
                    }
                }

                if ((PipeCommandReadController?.IsConnected).GetValueOrDefault() && (PipeCommandWriteController?.IsConnected).GetValueOrDefault())
                {
                    Dictionary<string, string> Command = new Dictionary<string, string>
                    {
                        { "CommandType", Enum.GetName(typeof(CommandType), Type) }
                    };

                    foreach ((string, string) Argument in Arguments)
                    {
                        Command.Add(Argument.Item1, Argument.Item2);
                    }

                    TaskCompletionSource<IDictionary<string, string>> CompletionSource = new TaskCompletionSource<IDictionary<string, string>>();

                    void PipeCommandReadController_OnDataReceived(object sender, NamedPipeDataReceivedArgs e)
                    {
                        if (e.ExtraException is Exception Ex)
                        {
                            CompletionSource.SetException(Ex);
                        }
                        else
                        {
                            try
                            {
                                CompletionSource.SetResult(JsonSerializer.Deserialize<IDictionary<string, string>>(e.Data));
                            }
                            catch (Exception ex)
                            {
                                CompletionSource.SetException(ex);
                            }
                        }
                    }

                    if ((PipeProgressReadController?.IsConnected).GetValueOrDefault())
                    {
                        PipeProgressReadController.OnDataReceived += PipeProgressReadController_OnDataReceived;
                    }

                    try
                    {
                        PipeCommandReadController.OnDataReceived += PipeCommandReadController_OnDataReceived;

                        PipeCommandWriteController.SendData(JsonSerializer.Serialize(Command));

                        return await CompletionSource.Task;
                    }
                    finally
                    {
                        PipeCommandReadController.OnDataReceived -= PipeCommandReadController_OnDataReceived;

                        if ((PipeProgressReadController?.IsConnected).GetValueOrDefault())
                        {
                            PipeProgressReadController.OnDataReceived -= PipeProgressReadController_OnDataReceived;
                        }
                    }
                }
                else
                {
                    if (await ConnectRemoteAsync())
                    {
                        ValueSet Command = new ValueSet
                        {
                            { "CommandType", Enum.GetName(typeof(CommandType), Type) }
                        };

                        foreach ((string, string) Argument in Arguments)
                        {
                            Command.Add(Argument.Item1, Argument.Item2);
                        }

                        if ((PipeProgressReadController?.IsConnected).GetValueOrDefault())
                        {
                            PipeProgressReadController.OnDataReceived += PipeProgressReadController_OnDataReceived;
                        }

                        AppServiceResponse Response = await Connection.SendMessageAsync(Command);

                        if ((PipeProgressReadController?.IsConnected).GetValueOrDefault())
                        {
                            PipeProgressReadController.OnDataReceived -= PipeProgressReadController_OnDataReceived;
                        }

                        if (Response.Status == AppServiceResponseStatus.Success)
                        {
                            Dictionary<string, string> Result = new Dictionary<string, string>(Response.Message.Count);

                            foreach (KeyValuePair<string, object> Pair in Response.Message)
                            {
                                Result.Add(Pair.Key, Convert.ToString(Pair.Value));
                            }

                            return Result;
                        }
                        else
                        {
                            throw new Exception($"AppServiceResponse return an invalid status. Status: {Enum.GetName(typeof(AppServiceResponseStatus), Response.Status)}");
                        }
                    }
                    else
                    {
                        throw new Exception("Failed to connect the AppService");
                    }
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"{nameof(SendCommandAndReportProgressAsync)} throw an error");
                return null;
            }
            finally
            {
                IsAnyActionExcutingInCurrentController = false;
            }
        }


        public async Task<string> GetMIMEContentTypeAsync(string Path)
        {
            if (await SendCommandAsync(CommandType.GetMIMEContentType, ("ExecutePath", Path)) is IDictionary<string, string> Response)
            {
                if (Response.TryGetValue("Success", out string MIME))
                {
                    return MIME;
                }
                else
                {
                    if (Response.TryGetValue("Error", out string ErrorMessage))
                    {
                        LogTracer.Log($"An unexpected error was threw in {nameof(GetMIMEContentTypeAsync)}, message: {ErrorMessage}");
                    }

                    return string.Empty;
                }
            }
            else
            {
                return string.Empty;
            }
        }

        public async Task<string> GetUrlTargetPathAsync(string Path)
        {
            if (await SendCommandAsync(CommandType.GetUrlTargetPath, ("ExecutePath", Path)) is IDictionary<string, string> Response)
            {
                if (Response.TryGetValue("Success", out string TargetPath))
                {
                    return TargetPath;
                }
                else
                {
                    if (Response.TryGetValue("Error", out string ErrorMessage))
                    {
                        LogTracer.Log($"An unexpected error was threw in {nameof(GetMIMEContentTypeAsync)}, message: {ErrorMessage}");
                    }

                    return string.Empty;
                }
            }
            else
            {
                return string.Empty;
            }
        }

        public async Task<string> GetTooltipTextAsync(string Path)
        {
            if (await SendCommandAsync(CommandType.GetTooltipText, ("Path", Path)) is IDictionary<string, string> Response)
            {
                if (Response.TryGetValue("Success", out string Tooltip))
                {
                    return Tooltip;
                }
                else
                {
                    if (Response.TryGetValue("Error", out string ErrorMessage))
                    {
                        LogTracer.Log($"An unexpected error was threw in {nameof(GetTooltipTextAsync)}, message: {ErrorMessage}");
                    }

                    return string.Empty;
                }
            }
            else
            {
                return string.Empty;
            }
        }

        public async Task<byte[]> GetThumbnailOverlayAsync(string Path)
        {
            if (await SendCommandAsync(CommandType.GetThumbnailOverlay, ("Path", Path)) is IDictionary<string, string> Response)
            {
                if (Response.TryGetValue("Success", out string ThumbnailOverlayStr))
                {
                    return JsonSerializer.Deserialize<byte[]>(Convert.ToString(ThumbnailOverlayStr));
                }
                else
                {
                    if (Response.TryGetValue("Error", out string ErrorMessage))
                    {
                        LogTracer.Log($"An unexpected error was threw in {nameof(GetThumbnailOverlayAsync)}, message: {ErrorMessage}");
                    }

                    return Array.Empty<byte>();
                }
            }
            else
            {
                return Array.Empty<byte>();
            }
        }

        public async Task<string> CreateNewAsync(CreateType Type, string Path)
        {
            if (await SendCommandAsync(CommandType.CreateNew, ("NewPath", Path), ("Type", Enum.GetName(typeof(CreateType), Type))) is IDictionary<string, string> Response)
            {
                if (Response.TryGetValue("Success", out string NewPath))
                {
                    return Convert.ToString(NewPath);
                }
                else
                {
                    if (Response.TryGetValue("Error_Failure", out string ErrorMessage2))
                    {
                        LogTracer.Log($"An unexpected error was threw in {nameof(CreateNewAsync)}, message: {ErrorMessage2}");
                    }
                    else if (Response.TryGetValue("Error_NoPermission", out string ErrorMessage4))
                    {
                        LogTracer.Log($"An unexpected error was threw in {nameof(CreateNewAsync)}, message: {ErrorMessage4}");
                    }
                    else if (Response.TryGetValue("Error", out string ErrorMessage))
                    {
                        LogTracer.Log($"An unexpected error was threw in {nameof(CreateNewAsync)}, message: {ErrorMessage}");
                    }

                    return string.Empty;
                }
            }
            else
            {
                return string.Empty;
            }
        }

        public async Task<bool> SetAsTopMostWindowAsync(string PackageFamilyName, uint? WithPID = null)
        {
            if (await SendCommandAsync(CommandType.SetAsTopMostWindow, ("PackageFamilyName", PackageFamilyName), ("WithPID", Convert.ToString(WithPID.GetValueOrDefault()))) is IDictionary<string, string> Response)
            {
                if (Response.TryGetValue("Success", out string ThumbnailOverlayStr))
                {
                    return true;
                }
                else
                {
                    if (Response.TryGetValue("Error", out string ErrorMessage))
                    {
                        LogTracer.Log($"An unexpected error was threw in {nameof(SetAsTopMostWindowAsync)}, message: {ErrorMessage}");
                    }

                    return false;
                }
            }
            else
            {
                return false;
            }
        }

        public async Task<bool> RemoveTopMostWindowAsync(string PackageFamilyName, uint? WithPID = null)
        {
            if (await SendCommandAsync(CommandType.RemoveTopMostWindow, ("PackageFamilyName", PackageFamilyName), ("WithPID", Convert.ToString(WithPID.GetValueOrDefault()))) is IDictionary<string, string> Response)
            {
                if (Response.TryGetValue("Success", out string ThumbnailOverlayStr))
                {
                    return true;
                }
                else
                {
                    if (Response.TryGetValue("Error", out string ErrorMessage))
                    {
                        LogTracer.Log($"An unexpected error was threw in {nameof(RemoveTopMostWindowAsync)}, message: {ErrorMessage}");
                    }

                    return false;
                }
            }
            else
            {
                return false;
            }
        }

        public async Task<Dictionary<string, string>> GetDocumentProperties(string Path)
        {
            if (await SendCommandAsync(CommandType.GetDocumentProperties, ("ExecutePath", Path)) is IDictionary<string, string> Response)
            {
                if (Response.TryGetValue("Success", out string Properties))
                {
                    return JsonSerializer.Deserialize<Dictionary<string, string>>(Convert.ToString(Properties));
                }
                else
                {
                    if (Response.TryGetValue("Error", out string ErrorMessage))
                    {
                        LogTracer.Log($"An unexpected error was threw in {nameof(GetDocumentProperties)}, message: {ErrorMessage}");
                    }

                    return new Dictionary<string, string>(0);
                }
            }
            else
            {
                return new Dictionary<string, string>(0);
            }
        }

        public async Task SetFileAttribute(string Path, params KeyValuePair<ModifyAttributeAction, System.IO.FileAttributes>[] Attribute)
        {
            if (await SendCommandAsync(CommandType.SetFileAttribute, ("ExecutePath", Path), ("Attributes", JsonSerializer.Serialize(Attribute))) is IDictionary<string, string> Response)
            {
                if (Response.TryGetValue("Error", out string ErrorMessage))
                {
                    LogTracer.Log($"An unexpected error was threw in {nameof(SetFileAttribute)}, message: {ErrorMessage}");
                }
            }
        }

        public async Task<bool> CheckIfEverythingIsAvailableAsync()
        {
            if (await SendCommandAsync(CommandType.CheckIfEverythingAvailable) is IDictionary<string, string> Response)
            {
                if (Response.ContainsKey("Success"))
                {
                    return true;
                }
                else
                {
                    if (Response.TryGetValue("Error", out string ErrorMessage))
                    {
                        LogTracer.Log($"An unexpected error was threw in {nameof(CheckIfEverythingIsAvailableAsync)}, message: {ErrorMessage}");
                    }

                    return false;
                }
            }
            else
            {
                return false;
            }
        }

        public async Task<IReadOnlyList<FileSystemStorageItemBase>> SearchByEverythingAsync(string BaseLocation, string SearchWord, bool SearchAsRegex, bool IgnoreCase)
        {
            if (await SendCommandAsync(CommandType.SearchByEverything, ("BaseLocation", BaseLocation), ("SearchWord", SearchWord), ("SearchAsRegex", Convert.ToString(SearchAsRegex)), ("IgnoreCase", Convert.ToString(IgnoreCase))) is IDictionary<string, string> Response)
            {
                if (Response.TryGetValue("Success", out string Result))
                {
                    string[] SearchResult = JsonSerializer.Deserialize<string[]>(Result);

                    if (SearchResult.Length == 0)
                    {
                        return new List<FileSystemStorageItemBase>(0);
                    }
                    else
                    {
                        return await FileSystemStorageItemBase.OpenInBatchAsync(SearchResult);
                    }
                }
                else
                {
                    if (Response.TryGetValue("Error", out string ErrorMessage))
                    {
                        LogTracer.Log($"An unexpected error was threw in {nameof(SearchByEverythingAsync)}, message: {ErrorMessage}");
                    }

                    return new List<FileSystemStorageItemBase>(0);
                }
            }
            else
            {
                return new List<FileSystemStorageItemBase>(0);
            }
        }

        public async Task<bool> LaunchUWPFromAUMIDAsync(string AppUserModelId, params string[] PathArray)
        {
            if (await SendCommandAsync(CommandType.LaunchUWP, ("AppUserModelId", AppUserModelId), ("LaunchPathArray", JsonSerializer.Serialize(PathArray))) is IDictionary<string, string> Response)
            {
                if (Response.TryGetValue("Success", out string Result))
                {
                    return true;
                }
                else
                {
                    if (Response.TryGetValue("Error", out string ErrorMessage))
                    {
                        LogTracer.Log($"An unexpected error was threw in {nameof(LaunchUWPFromAUMIDAsync)}, message: {ErrorMessage}");
                    }

                    return false;
                }
            }
            else
            {
                return false;
            }
        }

        public async Task<bool> LaunchUWPFromPfnAsync(string PackageFamilyName, params string[] PathArray)
        {
            if (await SendCommandAsync(CommandType.LaunchUWP, ("PackageFamilyName", PackageFamilyName), ("LaunchPathArray", JsonSerializer.Serialize(PathArray))) is IDictionary<string, string> Response)
            {
                if (Response.TryGetValue("Success", out string Result))
                {
                    return true;
                }
                else
                {
                    if (Response.TryGetValue("Error", out string ErrorMessage))
                    {
                        LogTracer.Log($"An unexpected error was threw in {nameof(LaunchUWPFromPfnAsync)}, message: {ErrorMessage}");
                    }

                    return false;
                }
            }
            else
            {
                return false;
            }
        }

        public async Task<bool> CheckIfPackageFamilyNameExist(string PackageFamilyName)
        {
            if (await SendCommandAsync(CommandType.CheckPackageFamilyNameExist, ("PackageFamilyName", PackageFamilyName)) is IDictionary<string, string> Response)
            {
                if (Response.TryGetValue("Success", out string Result))
                {
                    return Convert.ToBoolean(Result);
                }
                else
                {
                    if (Response.TryGetValue("Error", out string ErrorMessage))
                    {
                        LogTracer.Log($"An unexpected error was threw in {nameof(CheckIfPackageFamilyNameExist)}, message: {ErrorMessage}");
                    }

                    return false;
                }
            }
            else
            {
                return false;
            }
        }

        public async Task<InstalledApplication> GetInstalledApplicationAsync(string PackageFamilyName)
        {
            if (await SendCommandAsync(CommandType.GetInstalledApplication, ("PackageFamilyName", PackageFamilyName)) is IDictionary<string, string> Response)
            {
                if (Response.TryGetValue("Success", out string Result))
                {
                    InstalledApplicationPackage Pack = JsonSerializer.Deserialize<InstalledApplicationPackage>(Result);

                    return await InstalledApplication.CreateAsync(Pack);
                }
                else
                {
                    if (Response.TryGetValue("Error", out string ErrorMessage))
                    {
                        LogTracer.Log($"An unexpected error was threw in {nameof(GetInstalledApplicationAsync)}, message: {ErrorMessage}");
                    }

                    return null;
                }
            }
            else
            {
                return null;
            }
        }


        public async Task<IReadOnlyList<InstalledApplication>> GetAllInstalledApplicationAsync()
        {
            if (await SendCommandAsync(CommandType.GetAllInstalledApplication) is IDictionary<string, string> Response)
            {
                if (Response.TryGetValue("Success", out string Result))
                {
                    List<InstalledApplication> PackageList = new List<InstalledApplication>();

                    foreach (InstalledApplicationPackage Pack in JsonSerializer.Deserialize<IEnumerable<InstalledApplicationPackage>>(Result))
                    {
                        PackageList.Add(await InstalledApplication.CreateAsync(Pack));
                    }

                    return PackageList;
                }
                else
                {
                    if (Response.TryGetValue("Error", out string ErrorMessage))
                    {
                        LogTracer.Log($"An unexpected error was threw in {nameof(GetAllInstalledApplicationAsync)}, message: {ErrorMessage}");
                    }

                    return Array.Empty<InstalledApplication>();
                }
            }
            else
            {
                return Array.Empty<InstalledApplication>();
            }
        }


        public async Task<HiddenDataPackage> GetHiddenItemDataAsync(string Path)
        {
            if (await SendCommandAsync(CommandType.GetHiddenItemData, ("ExecutePath", Path)) is IDictionary<string, string> Response)
            {
                if (Response.TryGetValue("Success", out string Result))
                {
                    return JsonSerializer.Deserialize<HiddenDataPackage>(Result);
                }
                else
                {
                    if (Response.TryGetValue("Error", out string ErrorMessage))
                    {
                        LogTracer.Log($"An unexpected error was threw in {nameof(GetHiddenItemDataAsync)}, message: {ErrorMessage}");
                    }

                    return null;
                }
            }
            else
            {
                return null;
            }
        }

        public async Task<byte[]> GetThumbnailAsync(string Path)
        {
            if (await SendCommandAsync(CommandType.GetThumbnail, ("ExecutePath", Path)) is IDictionary<string, string> Response)
            {
                if (Response.TryGetValue("Success", out string Result))
                {
                    return JsonSerializer.Deserialize<byte[]>(Result);
                }
                else
                {
                    if (Response.TryGetValue("Error", out string ErrorMessage))
                    {
                        LogTracer.Log($"An unexpected error was threw in {nameof(GetHiddenItemDataAsync)}, message: {ErrorMessage}");
                    }

                    return Array.Empty<byte>();
                }
            }
            else
            {
                return Array.Empty<byte>();
            }
        }

        public async Task<IReadOnlyList<ContextMenuItem>> GetContextMenuItemsAsync(string[] PathArray, bool IncludeExtensionItem = false)
        {
            if (PathArray.All((Path) => !string.IsNullOrWhiteSpace(Path)))
            {
                if (await SendCommandAsync(CommandType.GetContextMenuItems, ("ExecutePath", JsonSerializer.Serialize(PathArray)), ("IncludeExtensionItem", Convert.ToString(IncludeExtensionItem))) is IDictionary<string, string> Response)
                {
                    if (Response.TryGetValue("Success", out string Result))
                    {
                        return JsonSerializer.Deserialize<ContextMenuPackage[]>(Result).Select((Item) => new ContextMenuItem(Item)).ToList();
                    }
                    else
                    {
                        if (Response.TryGetValue("Error", out string ErrorMessage))
                        {
                            LogTracer.Log($"An unexpected error was threw in {nameof(GetContextMenuItemsAsync)}, message: {ErrorMessage}");
                        }

                        return new List<ContextMenuItem>(0);
                    }
                }
                else
                {
                    return new List<ContextMenuItem>(0);
                }
            }
            else
            {
                return new List<ContextMenuItem>(0);
            }
        }

        public async Task<bool> InvokeContextMenuItemAsync(ContextMenuPackage Package)
        {
            if (Package?.Clone() is ContextMenuPackage ClonePackage)
            {
                ClonePackage.IconData = Array.Empty<byte>();

                if (await SendCommandAsync(CommandType.InvokeContextMenuItem, ("DataPackage", JsonSerializer.Serialize(ClonePackage))) is IDictionary<string, string> Response)
                {
                    if (Response.TryGetValue("Error", out string ErrorMessage))
                    {
                        LogTracer.Log($"An unexpected error was threw in {nameof(GetContextMenuItemsAsync)}, message: {ErrorMessage}");
                        return false;
                    }
                    else
                    {
                        return true;
                    }
                }
                else
                {
                    return false;
                }
            }
            else
            {
                return false;
            }
        }

        public async Task<bool> CreateLinkAsync(LinkDataPackage Package)
        {
            if (await SendCommandAsync(CommandType.CreateLink, ("DataPackage", JsonSerializer.Serialize(Package))) is IDictionary<string, string> Response)
            {
                if (Response.ContainsKey("Success"))
                {
                    return true;
                }
                else
                {
                    if (Response.TryGetValue("Error", out string ErrorMessage))
                    {
                        LogTracer.Log($"An unexpected error was threw in {nameof(CreateLinkAsync)}, message: {ErrorMessage}");
                    }

                    return false;
                }
            }
            else
            {
                return false;
            }
        }

        public async Task UpdateLinkAsync(LinkDataPackage Package)
        {
            if (await SendCommandAsync(CommandType.UpdateLink, ("DataPackage", JsonSerializer.Serialize(Package))) is IDictionary<string, string> Response)
            {
                if (Response.TryGetValue("Error", out string ErrorMessage))
                {
                    LogTracer.Log($"An unexpected error was threw in {nameof(UpdateLinkAsync)}, message: {ErrorMessage}");
                }
            }
        }

        public async Task UpdateUrlAsync(UrlDataPackage Package)
        {
            if (await SendCommandAsync(CommandType.UpdateUrl, ("DataPackage", JsonSerializer.Serialize(Package))) is IDictionary<string, string> Response)
            {
                if (Response.TryGetValue("Error", out string ErrorMessage))
                {
                    LogTracer.Log($"An unexpected error was threw in {nameof(UpdateUrlAsync)}, message: {ErrorMessage}");
                }
            }
        }

        public async Task<List<string>> GetVariableSuggestionAsync(string PartialVariable)
        {
            if (await SendCommandAsync(CommandType.GetVariablePathSuggestion, ("PartialVariable", PartialVariable)) is not IDictionary<string, string> Response)
                return null;


            if (Response.TryGetValue("Success", out string Result))
            {
                return JsonSerializer.Deserialize<List<string>>(Result);
            }
            else if (Response.TryGetValue("Error", out var ErrorMessage))
            {
                LogTracer.Log($"An unexpected error was threw in {nameof(GetVariableSuggestionAsync)}, message: {ErrorMessage}");
            }
            return null;

        }
        public async Task<string> GetVariablePathAsync(string Variable)
        {
            if (await SendCommandAsync(CommandType.GetVariablePath, ("Variable", Variable)) is IDictionary<string, string> Response)
            {
                if (Response.TryGetValue("Success", out string Result))
                {
                    return Convert.ToString(Result);
                }
                else
                {
                    if (Response.TryGetValue("Error", out string ErrorMessage))
                    {
                        LogTracer.Log($"An unexpected error was threw in {nameof(GetVariablePathAsync)}, message: {ErrorMessage}");
                    }

                    return string.Empty;
                }
            }
            else
            {
                return string.Empty;
            }
        }

        public async Task<string> RenameAsync(string Path, string DesireName, bool SkipOperationRecord = false)
        {
            if (await SendCommandAsync(CommandType.Rename, ("ExecutePath", Path), ("DesireName", DesireName)) is IDictionary<string, string> Response)
            {
                if (Response.TryGetValue("Success", out string NewName))
                {
                    string NewNameString = Convert.ToString(NewName);

                    if (!SkipOperationRecord)
                    {
                        OperationRecorder.Current.Push($"{Path}||Rename||{System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Path), NewNameString)}");
                    }

                    return NewNameString;
                }
                else if (Response.TryGetValue("Error_Capture", out string ErrorMessage1))
                {
                    LogTracer.Log($"An unexpected error was threw in {nameof(RenameAsync)}, message: {ErrorMessage1}");
                    throw new FileCaputureException();
                }
                else if (Response.TryGetValue("Error_Failure", out string ErrorMessage2))
                {
                    LogTracer.Log($"An unexpected error was threw in {nameof(RenameAsync)}, message: {ErrorMessage2}");
                    throw new InvalidOperationException();
                }
                else if (Response.TryGetValue("Error_NoPermission", out string ErrorMessage3))
                {
                    LogTracer.Log($"An unexpected error was threw in {nameof(RenameAsync)}, message: {ErrorMessage3}");
                    throw new InvalidOperationException();
                }
                else if (Response.TryGetValue("Error", out string ErrorMessage4))
                {
                    LogTracer.Log($"An unexpected error was threw in {nameof(RenameAsync)}, message: {ErrorMessage4}");
                    throw new InvalidOperationException();
                }
                else
                {
                    throw new Exception();
                }
            }
            else
            {
                throw new NoResponseException();
            }
        }

        public async Task<LinkDataPackage> GetLinkDataAsync(string Path)
        {
            if (await SendCommandAsync(CommandType.GetLinkData, ("ExecutePath", Path)) is IDictionary<string, string> Response)
            {
                if (Response.TryGetValue("Success", out string Result))
                {
                    return JsonSerializer.Deserialize<LinkDataPackage>(Result);
                }
                else
                {
                    if (Response.TryGetValue("Error", out string ErrorMessage))
                    {
                        LogTracer.Log($"An unexpected error was threw in {nameof(GetLinkDataAsync)}, message: {ErrorMessage}");
                    }

                    return null;
                }
            }
            else
            {
                return null;
            }
        }

        public async Task<UrlDataPackage> GetUrlDataAsync(string Path)
        {
            if (await SendCommandAsync(CommandType.GetUrlData, ("ExecutePath", Path)) is IDictionary<string, string> Response)
            {
                if (Response.TryGetValue("Success", out string Result))
                {
                    return JsonSerializer.Deserialize<UrlDataPackage>(Result);
                }
                else
                {
                    if (Response.TryGetValue("Error", out string ErrorMessage))
                    {
                        LogTracer.Log($"An unexpected error was threw in {nameof(GetUrlDataAsync)}, message: {ErrorMessage}");
                    }

                    return null;
                }
            }
            else
            {
                return null;
            }
        }

        public async Task<bool> InterceptWindowsPlusEAsync()
        {
            if (await SendCommandAsync(CommandType.InterceptWinE) is IDictionary<string, string> Response)
            {
                if (Response.ContainsKey("Success"))
                {
                    return true;
                }
                else
                {
                    if (Response.TryGetValue("Error", out string ErrorMessage))
                    {
                        LogTracer.Log($"An unexpected error was threw in {nameof(InterceptWindowsPlusEAsync)}, message: {ErrorMessage}");
                    }

                    return false;
                }
            }
            else
            {
                return false;
            }
        }

        public async Task<bool> RestoreWindowsPlusEAsync()
        {
            if (await SendCommandAsync(CommandType.RestoreWinE) is IDictionary<string, string> Response)
            {
                if (Response.ContainsKey("Success"))
                {
                    return true;
                }
                else
                {
                    if (Response.TryGetValue("Error", out string ErrorMessage))
                    {
                        LogTracer.Log($"An unexpected error was threw in {nameof(RestoreWindowsPlusEAsync)}, message: {ErrorMessage}");
                    }

                    return false;
                }
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// 启动指定路径的程序，并传递指定的参数
        /// </summary>
        /// <param name="Path">程序路径</param>
        /// <param name="Parameters">传递的参数</param>
        /// <returns></returns>
        public async Task<bool> RunAsync(string Path, string WorkDirectory = null, WindowState WindowStyle = WindowState.Normal, bool RunAsAdmin = false, bool CreateNoWindow = false, bool ShouldWaitForExit = false, params string[] Parameters)
        {
            if (await SendCommandAsync(CommandType.RunExecutable,
                                       ("ExecutePath", Path),
                                       ("ExecuteParameter", string.Join(' ', Parameters.Select((Para) => (Para.Contains(" ") && !Para.StartsWith("\"") && !Para.EndsWith("\"")) ? $"\"{Para}\"" : Para))),
                                       ("ExecuteAuthority", RunAsAdmin ? "Administrator" : "Normal"),
                                       ("ExecuteCreateNoWindow", Convert.ToString(CreateNoWindow)),
                                       ("ExecuteShouldWaitForExit", Convert.ToString(ShouldWaitForExit)),
                                       ("ExecuteWorkDirectory", WorkDirectory ?? string.Empty),
                                       ("ExecuteWindowStyle", Enum.GetName(typeof(WindowState), WindowStyle))) is IDictionary<string, string> Response)
            {
                if (Response.TryGetValue("Error", out string ErrorMessage2))
                {
                    LogTracer.Log($"An unexpected error was threw in {nameof(RunAsync)}, message: {ErrorMessage2}");
                    return false;
                }
                else
                {
                    return true;
                }
            }
            else
            {
                return false;
            }
        }

        public async Task ViewWithQuicklookAsync(string Path)
        {
            await SendCommandAsync(CommandType.Quicklook, ("ExecutePath", Path));
        }

        public async Task<bool> CheckIfQuicklookIsAvaliableAsync()
        {
            if (await SendCommandAsync(CommandType.Check_Quicklook) is IDictionary<string, string> Response)
            {
                if (Response.TryGetValue("Check_QuicklookIsAvaliable_Result", out string Result))
                {
                    return Convert.ToBoolean(Result);
                }
                else
                {
                    if (Response.TryGetValue("Error", out string ErrorMessage))
                    {
                        LogTracer.Log($"An unexpected error was threw in {nameof(CheckIfQuicklookIsAvaliableAsync)}, message: {ErrorMessage}");
                    }

                    return false;
                }
            }
            else
            {
                return false;
            }
        }

        public async Task<string> GetDefaultAssociationFromPathAsync(string Path)
        {
            if (await SendCommandAsync(CommandType.Default_Association, ("ExecutePath", Path)) is IDictionary<string, string> Response)
            {
                if (Response.TryGetValue("Success", out string Result))
                {
                    return Convert.ToString(Result);
                }
                else
                {
                    if (Response.TryGetValue("Error", out string ErrorMessage))
                    {
                        LogTracer.Log($"An unexpected error was threw in {nameof(GetDefaultAssociationFromPathAsync)}, message: {ErrorMessage}");
                    }

                    return string.Empty;
                }
            }
            else
            {
                return string.Empty;
            }
        }

        public async Task<IReadOnlyList<AssociationPackage>> GetAssociationFromPathAsync(string Path)
        {
            if (await SendCommandAsync(CommandType.Get_Association, ("ExecutePath", Path)) is IDictionary<string, string> Response)
            {
                if (Response.TryGetValue("Associate_Result", out string Result))
                {
                    return JsonSerializer.Deserialize<List<AssociationPackage>>(Result);
                }
                else
                {
                    if (Response.TryGetValue("Error", out string ErrorMessage))
                    {
                        LogTracer.Log($"An unexpected error was threw in {nameof(GetAssociationFromPathAsync)}, message: {ErrorMessage}");
                    }

                    return new List<AssociationPackage>(0);
                }
            }
            else
            {
                return new List<AssociationPackage>(0);
            }
        }

        public async Task<bool> EmptyRecycleBinAsync()
        {
            if (await SendCommandAsync(CommandType.EmptyRecycleBin) is IDictionary<string, string> Response)
            {
                if (Response.TryGetValue("RecycleBinItems_Clear_Result", out string Result))
                {
                    return Convert.ToBoolean(Result);
                }
                else
                {
                    if (Response.TryGetValue("Error", out string ErrorMessage))
                    {
                        LogTracer.Log($"An unexpected error was threw in {nameof(EmptyRecycleBinAsync)}, message: {ErrorMessage}");
                    }

                    return false;
                }
            }
            else
            {
                return false;
            }
        }

        public async Task<IReadOnlyList<IRecycleStorageItem>> GetRecycleBinItemsAsync()
        {
            if (await SendCommandAsync(CommandType.Get_RecycleBinItems) is IDictionary<string, string> Response)
            {
                if (Response.TryGetValue("RecycleBinItems_Json_Result", out string Result))
                {
                    List<Dictionary<string, string>> JsonList = JsonSerializer.Deserialize<List<Dictionary<string, string>>>(Result);
                    List<IRecycleStorageItem> RecycleItems = new List<IRecycleStorageItem>(JsonList.Count);

                    foreach (Dictionary<string, string> PropertyDic in JsonList)
                    {
                        IRecycleStorageItem Item = Enum.Parse<StorageItemTypes>(PropertyDic["StorageType"]) == StorageItemTypes.Folder
                                                    ? new RecycleStorageFolder(PropertyDic["ActualPath"], PropertyDic["OriginPath"], DateTimeOffset.FromFileTime(Convert.ToInt64(PropertyDic["DeleteTime"])))
                                                    : new RecycleStorageFile(PropertyDic["ActualPath"], PropertyDic["OriginPath"], DateTimeOffset.FromFileTime(Convert.ToInt64(PropertyDic["DeleteTime"])));

                        RecycleItems.Add(Item);
                    }

                    return RecycleItems;
                }
                else
                {
                    if (Response.TryGetValue("Error", out string ErrorMessage))
                    {
                        LogTracer.Log($"An unexpected error was threw in {nameof(GetRecycleBinItemsAsync)}, message: {ErrorMessage}");
                    }

                    return new List<IRecycleStorageItem>(0);
                }
            }
            else
            {
                return new List<IRecycleStorageItem>(0);
            }
        }

        public async Task<bool> TryUnlockFileOccupy(string Path, bool ForceClose = false)
        {
            if (await SendCommandAsync(CommandType.UnlockOccupy, ("ExecutePath", Path), ("ForceClose", Convert.ToString(ForceClose))) is IDictionary<string, string> Response)
            {
                if (Response.ContainsKey("Success"))
                {
                    return true;
                }
                if (Response.TryGetValue("Error_Failure", out string ErrorMessage1))
                {
                    LogTracer.Log($"An unexpected error was threw in {nameof(TryUnlockFileOccupy)}, message: {ErrorMessage1}");
                    return false;
                }
                else if (Response.TryGetValue("Error_NotOccupy", out string ErrorMessage2))
                {
                    LogTracer.Log($"An unexpected error was threw in {nameof(TryUnlockFileOccupy)}, message: {ErrorMessage2}");
                    throw new UnlockException();
                }
                else if (Response.TryGetValue("Error_NotFoundOrNotFile", out string ErrorMessage3))
                {
                    LogTracer.Log($"An unexpected error was threw in {nameof(TryUnlockFileOccupy)}, message: {ErrorMessage3}");
                    throw new FileNotFoundException();
                }
                else if (Response.TryGetValue("Error", out string ErrorMessage4))
                {
                    LogTracer.Log($"An unexpected error was threw in {nameof(TryUnlockFileOccupy)}, message: {ErrorMessage4}");
                    return false;
                }
                else
                {
                    LogTracer.Log($"An unexpected error was threw in {nameof(TryUnlockFileOccupy)}");
                    return false;
                }
            }
            else
            {
                throw new NoResponseException();
            }
        }

        public async Task DeleteAsync(IEnumerable<string> Source, bool PermanentDelete, ProgressChangedEventHandler ProgressHandler = null)
        {
            if (await SendCommandAndReportProgressAsync(CommandType.Delete,
                                                        ProgressHandler,
                                                        ("ExecutePath", JsonSerializer.Serialize(Source)),
                                                        ("PermanentDelete", Convert.ToString(PermanentDelete))) is IDictionary<string, string> Response)
            {
                if (Response.TryGetValue("Success", out string Record))
                {
                    if (!PermanentDelete)
                    {
                        OperationRecorder.Current.Push(JsonSerializer.Deserialize<string[]>(Convert.ToString(Record)));
                    }
                }
                else if (Response.TryGetValue("Error_NotFound", out string ErrorMessage1))
                {
                    LogTracer.Log($"An unexpected error was threw in {nameof(DeleteAsync)}, message: {ErrorMessage1}");
                    throw new FileNotFoundException();
                }
                else if (Response.TryGetValue("Error_Failure", out string ErrorMessage2))
                {
                    LogTracer.Log($"An unexpected error was threw in {nameof(DeleteAsync)}, message: {ErrorMessage2}");
                    throw new InvalidOperationException("Fail to delete item");
                }
                else if (Response.TryGetValue("Error_Capture", out string ErrorMessage3))
                {
                    LogTracer.Log($"An unexpected error was threw in {nameof(DeleteAsync)}, message: {ErrorMessage3}");
                    throw new FileCaputureException();
                }
                else if (Response.TryGetValue("Error_NoPermission", out string ErrorMessage4))
                {
                    LogTracer.Log($"An unexpected error was threw in {nameof(DeleteAsync)}, message: {ErrorMessage4}");
                    throw new InvalidOperationException("Fail to delete item");
                }
                else if (Response.TryGetValue("Error", out string ErrorMessage5))
                {
                    LogTracer.Log($"An unexpected error was threw in {nameof(DeleteAsync)}, message: {ErrorMessage5}");
                    throw new Exception();
                }
                else
                {
                    LogTracer.Log($"An unexpected error was threw in {nameof(DeleteAsync)}");
                    throw new Exception("Unknown reason");
                }
            }
            else
            {
                throw new NoResponseException();
            }
        }

        public Task DeleteAsync(string Source, bool PermanentDelete, ProgressChangedEventHandler ProgressHandler = null)
        {
            if (Source == null)
            {
                throw new ArgumentNullException(nameof(Source), "Parameter could not be null");
            }

            return DeleteAsync(new string[1] { Source }, PermanentDelete, ProgressHandler);
        }

        public async Task MoveAsync(Dictionary<string, string> Source, string DestinationPath, CollisionOptions Option = CollisionOptions.None, bool SkipOperationRecord = false, ProgressChangedEventHandler ProgressHandler = null)
        {
            if (Source == null)
            {
                throw new ArgumentNullException(nameof(Source), "Parameter could not be null");
            }

            Dictionary<string, string> MessageList = new Dictionary<string, string>();

            foreach (KeyValuePair<string, string> SourcePair in Source)
            {
                if (await FileSystemStorageItemBase.CheckExistAsync(SourcePair.Key))
                {
                    MessageList.Add(SourcePair.Key, SourcePair.Value);
                }
                else
                {
                    throw new FileNotFoundException();
                }
            }

            if (await SendCommandAndReportProgressAsync(CommandType.Move,
                                                        ProgressHandler,
                                                        ("SourcePath", JsonSerializer.Serialize(MessageList)),
                                                        ("DestinationPath", DestinationPath),
                                                        ("CollisionOptions", Enum.GetName(typeof(CollisionOptions), Option))) is IDictionary<string, string> Response)
            {
                if (Response.TryGetValue("Success", out string Record))
                {
                    if (!SkipOperationRecord)
                    {
                        OperationRecorder.Current.Push(JsonSerializer.Deserialize<string[]>(Convert.ToString(Record)));
                    }
                }
                else if (Response.TryGetValue("Error_NotFound", out string ErrorMessage1))
                {
                    LogTracer.Log($"An unexpected error was threw in {nameof(MoveAsync)}, message: {ErrorMessage1}");
                    throw new FileNotFoundException();
                }
                else if (Response.TryGetValue("Error_Failure", out string ErrorMessage2))
                {
                    LogTracer.Log($"An unexpected error was threw in {nameof(MoveAsync)}, message: {ErrorMessage2}");
                    throw new InvalidOperationException();
                }
                else if (Response.TryGetValue("Error_Capture", out string ErrorMessage3))
                {
                    LogTracer.Log($"An unexpected error was threw in {nameof(MoveAsync)}, message: {ErrorMessage3}");
                    throw new FileCaputureException();
                }
                else if (Response.TryGetValue("Error_NoPermission", out string ErrorMessage4))
                {
                    LogTracer.Log($"An unexpected error was threw in {nameof(MoveAsync)}, message: {ErrorMessage4}");
                    throw new InvalidOperationException();
                }
                else if (Response.TryGetValue("Error_UserCancel", out string ErrorMessage5))
                {
                    LogTracer.Log($"An unexpected error was threw in {nameof(MoveAsync)}, message: {ErrorMessage5}");
                    throw new TaskCanceledException();
                }
                else if (Response.TryGetValue("Error", out string ErrorMessage6))
                {
                    LogTracer.Log($"An unexpected error was threw in {nameof(MoveAsync)}, message: {ErrorMessage6}");
                    throw new Exception();
                }
                else
                {
                    LogTracer.Log($"An unexpected error was threw in {nameof(MoveAsync)}");
                    throw new Exception();
                }
            }
            else
            {
                throw new NoResponseException();
            }
        }

        public Task MoveAsync(IEnumerable<string> Source, string DestinationPath, CollisionOptions Option = CollisionOptions.None, bool SkipOperationRecord = false, ProgressChangedEventHandler ProgressHandler = null)
        {
            Dictionary<string, string> Dic = new Dictionary<string, string>();

            foreach (string Path in Source)
            {
                Dic.Add(Path, null);
            }

            return MoveAsync(Dic, DestinationPath, Option, SkipOperationRecord, ProgressHandler);
        }

        public Task MoveAsync(string SourcePath, string Destination, CollisionOptions Option = CollisionOptions.None, bool SkipOperationRecord = false, ProgressChangedEventHandler ProgressHandler = null)
        {
            if (string.IsNullOrEmpty(SourcePath))
            {
                throw new ArgumentNullException(nameof(SourcePath), "Parameter could not be null");
            }

            if (string.IsNullOrEmpty(Destination))
            {
                throw new ArgumentNullException(nameof(Destination), "Parameter could not be null");
            }

            return MoveAsync(new string[] { SourcePath }, Destination, Option, SkipOperationRecord, ProgressHandler);
        }

        public async Task<bool> PasteRemoteFile(string DestinationPath)
        {
            if (await SendCommandAsync(CommandType.PasteRemoteFile, ("Path", DestinationPath)) is IDictionary<string, string> Response)
            {
                if (Response.ContainsKey("Success"))
                {
                    return true;
                }
                else
                {
                    if (Response.TryGetValue("Error", out string ErrorMessage))
                    {
                        LogTracer.Log($"An unexpected error was threw in {nameof(PasteRemoteFile)}, message: {ErrorMessage}");
                    }

                    return false;
                }
            }
            else
            {
                return false;
            }
        }

        public async Task CopyAsync(IEnumerable<string> Source, string DestinationPath, CollisionOptions Option = CollisionOptions.None, bool SkipOperationRecord = false, ProgressChangedEventHandler ProgressHandler = null)
        {
            if (Source == null)
            {
                throw new ArgumentNullException(nameof(Source), "Parameter could not be null");
            }

            List<string> ItemList = new List<string>();

            foreach (string SourcePath in Source)
            {
                if (await FileSystemStorageItemBase.CheckExistAsync(SourcePath))
                {
                    ItemList.Add(SourcePath);
                }
                else
                {
                    throw new FileNotFoundException();
                }
            }

            if (await SendCommandAndReportProgressAsync(CommandType.Copy,
                                                        ProgressHandler,
                                                        ("SourcePath", JsonSerializer.Serialize(ItemList)),
                                                        ("DestinationPath", DestinationPath),
                                                        ("CollisionOptions", Enum.GetName(typeof(CollisionOptions), Option))) is IDictionary<string, string> Response)
            {
                if (Response.TryGetValue("Success", out string Record))
                {
                    if (!SkipOperationRecord)
                    {
                        OperationRecorder.Current.Push(JsonSerializer.Deserialize<string[]>(Convert.ToString(Record)));
                    }
                }
                else if (Response.TryGetValue("Error_NotFound", out string ErrorMessage1))
                {
                    LogTracer.Log($"An unexpected error was threw in {nameof(CopyAsync)}, message: {ErrorMessage1}");
                    throw new FileNotFoundException();
                }
                else if (Response.TryGetValue("Error_Failure", out string ErrorMessage2))
                {
                    LogTracer.Log($"An unexpected error was threw in {nameof(CopyAsync)}, message: {ErrorMessage2}");
                    throw new InvalidOperationException();
                }
                else if (Response.TryGetValue("Error_NoPermission", out string ErrorMessage3))
                {
                    LogTracer.Log($"An unexpected error was threw in {nameof(CopyAsync)}, message: {ErrorMessage3}");
                    throw new InvalidOperationException();
                }
                else if (Response.TryGetValue("Error_UserCancel", out string ErrorMessage4))
                {
                    LogTracer.Log($"An unexpected error was threw in {nameof(CopyAsync)}, message: {ErrorMessage4}");
                    throw new TaskCanceledException();
                }
                else if (Response.TryGetValue("Error", out string ErrorMessage5))
                {
                    LogTracer.Log($"An unexpected error was threw in {nameof(CopyAsync)}, message: {ErrorMessage5}");
                    throw new Exception();
                }
                else
                {
                    LogTracer.Log($"An unexpected error was threw in {nameof(CopyAsync)}");
                    throw new Exception("Unknown reason");
                }
            }
            else
            {
                throw new NoResponseException();
            }
        }

        public Task CopyAsync(string SourcePath, string Destination, CollisionOptions Option = CollisionOptions.None, bool SkipOperationRecord = false, ProgressChangedEventHandler ProgressHandler = null)
        {
            if (string.IsNullOrEmpty(SourcePath))
            {
                throw new ArgumentNullException(nameof(SourcePath), "Parameter could not be null");
            }

            if (string.IsNullOrEmpty(Destination))
            {
                throw new ArgumentNullException(nameof(Destination), "Parameter could not be null");
            }

            return CopyAsync(new string[1] { SourcePath }, Destination, Option, SkipOperationRecord, ProgressHandler);
        }

        public async Task<bool> RestoreItemInRecycleBinAsync(params string[] OriginPathList)
        {
            if (OriginPathList.Any((Item) => string.IsNullOrWhiteSpace(Item)))
            {
                throw new ArgumentNullException(nameof(OriginPathList), "Parameter could not be null or empty");
            }

            if (await SendCommandAsync(CommandType.Restore_RecycleItem, ("ExecutePath", JsonSerializer.Serialize(OriginPathList))) is IDictionary<string, string> Response)
            {
                if (Response.TryGetValue("Restore_Result", out string Result))
                {
                    return Convert.ToBoolean(Result);
                }
                else
                {
                    if (Response.TryGetValue("Error", out string ErrorMessage))
                    {
                        LogTracer.Log($"An unexpected error was threw in {nameof(RestoreItemInRecycleBinAsync)}, message: {ErrorMessage}");
                    }

                    return false;
                }
            }
            else
            {
                return false;
            }
        }

        public async Task<bool> DeleteItemInRecycleBinAsync(string Path)
        {
            if (string.IsNullOrWhiteSpace(Path))
            {
                throw new ArgumentNullException(nameof(Path), "Parameter could not be null or empty");
            }

            if (await SendCommandAsync(CommandType.Delete_RecycleItem, ("ExecutePath", Path)) is IDictionary<string, string> Response)
            {
                if (Response.TryGetValue("Delete_Result", out string Result))
                {
                    return Convert.ToBoolean(Result);
                }
                else
                {
                    if (Response.TryGetValue("Error", out string ErrorMessage))
                    {
                        LogTracer.Log($"An unexpected error was threw in {nameof(DeleteItemInRecycleBinAsync)}, message: {ErrorMessage}");
                    }

                    return false;
                }
            }
            else
            {
                return false;
            }
        }

        public async Task<bool> EjectPortableDevice(string Path)
        {
            if (string.IsNullOrWhiteSpace(Path))
            {
                throw new ArgumentNullException(nameof(Path), "Parameter could not be null or empty");
            }

            if (await SendCommandAsync(CommandType.EjectUSB, ("ExecutePath", Path)) is IDictionary<string, string> Response)
            {
                if (Response.TryGetValue("EjectResult", out string Result))
                {
                    return Convert.ToBoolean(Result);
                }
                else
                {
                    if (Response.TryGetValue("Error", out string ErrorMessage))
                    {
                        LogTracer.Log($"An unexpected error was threw in {nameof(EjectPortableDevice)}, message: {ErrorMessage}");
                    }

                    return false;
                }
            }
            else
            {
                return false;
            }
        }

        public void Dispose()
        {
            if (!IsDisposed)
            {
                try
                {
                    IsDisposed = true;
                    IsConnected = false;

                    if (Connection != null)
                    {
                        Connection.RequestReceived -= Connection_RequestReceived;
                        Connection.ServiceClosed -= Connection_ServiceClosed;
                        Connection.Dispose();
                    }

                    PipeCommandReadController?.Dispose();
                    PipeCommandWriteController?.Dispose();
                    PipeProgressReadController?.Dispose();
                }
                catch (Exception ex)
                {
                    LogTracer.Log(ex);
                }
                finally
                {
                    AllControllerList.Remove(this);
                    GC.SuppressFinalize(this);
                    Interlocked.Decrement(ref CurrentRunningControllerNum);
                }
            }
        }

        ~FullTrustProcessController()
        {
            Dispose();
        }

        public sealed class ExclusiveUsage : IDisposable
        {
            public FullTrustProcessController Controller { get; private set; }

            private ExtendedExecutionController ExtExecution;

            public ExclusiveUsage(FullTrustProcessController Controller, ExtendedExecutionController ExtExecution)
            {
                this.Controller = Controller;
                this.ExtExecution = ExtExecution;
            }

            public void Dispose()
            {
                GC.SuppressFinalize(this);

                ExclusiveDisposed?.Invoke(this, Controller);
                Controller = null;

                ExtExecution?.Dispose();
                ExtExecution = null;
            }

            ~ExclusiveUsage()
            {
                Dispose();
            }
        }
    }
}
