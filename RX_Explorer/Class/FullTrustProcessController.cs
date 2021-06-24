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
        private const string ExecuteType_RunExe = "Execute_RunExe";

        private const string ExecuteType_Quicklook = "Execute_Quicklook";

        private const string ExecuteType_Check_Quicklook = "Execute_Check_QuicklookIsAvaliable";

        private const string ExecuteType_Get_Association = "Execute_Get_Association";

        private const string ExecuteType_Default_Association = "Execute_Default_Association";

        private const string ExecuteType_Get_RecycleBinItems = "Execute_Get_RecycleBinItems";

        private const string ExecuteType_InterceptWinE = "Execute_Intercept_Win_E";

        private const string ExecuteType_RestoreWinE = "Execute_Restore_Win_E";

        private const string ExecuteType_GetLnkData = "Execute_GetLnkData";

        private const string ExecuteType_GetUrlData = "Execute_GetUrlData";

        private const string ExecuteType_Rename = "Execute_Rename";

        private const string ExecuteType_EmptyRecycleBin = "Execute_Empty_RecycleBin";

        private const string ExecuteType_UnlockOccupy = "Execute_Unlock_Occupy";

        private const string ExecuteType_EjectUSB = "Execute_EjectUSB";

        private const string ExecuteType_Copy = "Execute_Copy";

        private const string ExecuteType_Move = "Execute_Move";

        private const string ExecuteType_Delete = "Execute_Delete";

        private const string ExecuteAuthority_Normal = "Normal";

        private const string ExecuteAuthority_Administrator = "Administrator";

        private const string ExecuteType_Restore_RecycleItem = "Execute_Restore_RecycleItem";

        private const string ExecuteType_Delete_RecycleItem = "Execute_Delete_RecycleItem";

        private const string ExecuteType_GetVariablePath = "Execute_GetVariable_Path";

        private const string ExecuteType_CreateLink = "Execute_CreateLink";

        private const string ExecuteType_UpdateLink = "Execute_UpdateLink";

        private const string ExecuteType_UpdateUrl = "Execute_UpdateUrl";

        private const string ExecuteType_PasteRemoteFile = "Paste_Remote_File";

        private const string ExecuteType_Test_Connection = "Execute_Test_Connection";

        private const string ExecuteType_GetContextMenuItems = "Execute_GetContextMenuItems";

        private const string ExecuteType_InvokeContextMenuItem = "Execute_InvokeContextMenuItem";

        private const string ExecuteType_CheckIfEverythingAvailable = "Execute_CheckIfEverythingAvailable";

        private const string ExecuteType_SearchByEverything = "Execute_SearchByEverything";

        private const string ExecuteType_GetHiddenItemInfo = "Execute_GetHiddenItemInfo";

        private const string ExecuteType_SetFileAttribute = "Execute_SetFileAttribute";

        private const string ExecuteType_GetMIMEContentType = "Execute_GetMIMEContentType";

        private const string ExecuteType_GetAllInstalledApplication = "Execute_GetAllInstalledApplication";

        private const string ExecuteType_CheckPackageFamilyNameExist = "Execute_CheckPackageFamilyNameExist";

        private const string ExecuteType_GetInstalledApplication = "Execute_GetInstalledApplication";

        private const string ExecuteType_GetDocumentProperties = "Execute_GetDocumentProperties";

        private const string ExecuteType_LaunchUWP = "Execute_LaunchUWP";

        private const string ExecuteType_GetThumbnailOverlay = "Execute_GetThumbnailOverlay";

        private const string ExecuteType_SetAsTopMostWindow = "Execute_SetAsTopMostWindow";

        private const string ExecuteType_RemoveTopMostWindow = "Execute_RemoveTopMostWindow";

        public const ushort DynamicBackupProcessNum = 2;

        private readonly int CurrentProcessId;

        private bool IsConnected;

        private bool IsDisposed;

        public bool IsAnyActionExcutingInCurrentController { get; private set; }

        public static bool IsAnyActionExcutingInAllControllers
        {
            get
            {
                return AllControllerList.Any((Controller) => Controller.IsAnyActionExcutingInCurrentController);
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

        private readonly NamedPipeController PipeController;

        private readonly AppServiceConnection Connection;

        private static readonly SynchronizedCollection<FullTrustProcessController> AllControllerList = new SynchronizedCollection<FullTrustProcessController>();

        private static readonly ConcurrentQueue<FullTrustProcessController> AvailableControllers = new ConcurrentQueue<FullTrustProcessController>();

        private static readonly ConcurrentQueue<TaskCompletionSource<ExclusiveUsage>> WaitingTaskQueue = new ConcurrentQueue<TaskCompletionSource<ExclusiveUsage>>();

        private static volatile int CurrentRunningControllerNum;

        private static volatile int LastRequestedControllerNum;

        private static event EventHandler<FullTrustProcessController> ExclusiveDisposed;

        public static event EventHandler<bool> CurrentBusyStatus;

        public static event EventHandler FullTrustProcessExitedUnexpected;

        public static readonly AutoResetEvent ResizeLocker = new AutoResetEvent(true);

        private static readonly AutoResetEvent DispatcherSleepLocker = new AutoResetEvent(false);

        static FullTrustProcessController()
        {
            DispatcherThread.Start();
            ExclusiveDisposed += FullTrustProcessController_ExclusiveDisposed;
            Application.Current.Resuming += Current_Resuming;
        }

        private FullTrustProcessController()
        {
            Interlocked.Increment(ref CurrentRunningControllerNum);

            Application.Current.Suspending += Current_Suspending;

            using (Process CurrentProcess = Process.GetCurrentProcess())
            {
                CurrentProcessId = CurrentProcess.Id;
            }

            AllControllerList.Add(this);

            if (WindowsVersionChecker.IsNewerOrEqual(Version.Windows10_2004))
            {
                PipeController = new NamedPipeController();
            }

            Connection = new AppServiceConnection
            {
                AppServiceName = "CommunicateService",
                PackageFamilyName = Package.Current.Id.FamilyName
            };

            Connection.RequestReceived += Connection_RequestReceived;
            Connection.ServiceClosed += Connection_ServiceClosed;
        }

        private static void Current_Resuming(object sender, object e)
        {
            LogTracer.Log("RX-Explorer is resuming, recover all instance");
            AvailableControllers.Clear();
            AllControllerList.Clear();
            RequestResizeController(LastRequestedControllerNum);
        }

        private void Current_Suspending(object sender, SuspendingEventArgs e)
        {
            LogTracer.Log("RX-Explorer is suspending, dispose this instance");
            Dispose();
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
            _ = Task.Run(() => ResizeController(RequestedTarget));
        }

        private static void ResizeController(int RequestedTarget)
        {
            ResizeLocker.WaitOne();

            try
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
            catch (Exception ex)
            {
                LogTracer.Log(ex, "An exception was threw when maintance FullTrustProcessController");
            }
            finally
            {
                ResizeLocker.Set();
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
                switch (args.Request.Message["ExecuteType"])
                {
                    case "Identity":
                        {
                            await args.Request.SendResponseAsync(new ValueSet { { "Identity", "UWP" } });
                            break;
                        }
                    case "FullTrustProcessExited":
                        {
                            LogTracer.Log("FullTrustProcess exited unexpected, dispose this instance");
                            Dispose();
                            FullTrustProcessExitedUnexpected?.Invoke(this, null);
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
                if (!IsConnected)
                {
                    AppServiceConnectionStatus Status = await Connection.OpenAsync();

                    if (Status == AppServiceConnectionStatus.Success)
                    {
                        //Do not remove this delay, leave some time for "Identity" call from AppService
                        await Task.Delay(1000);
                    }
                    else
                    {
                        Dispose();
                        LogTracer.Log($"Connect to AppService failed, reason: \"{Enum.GetName(typeof(AppServiceResponseStatus), Status)}\". Dispose this instance");
                        return false;
                    }
                }

                for (int Count = 0; Count < 3; Count++)
                {
                    ValueSet Value = new ValueSet
                    {
                        { "ExecuteType", ExecuteType_Test_Connection },
                        { "ProcessId", CurrentProcessId },
                    };

                    if (PipeController != null)
                    {
                        Value.Add("PipeId", PipeController.PipeUniqueId);
                    }

                    AppServiceResponse Response = await Connection.SendMessageAsync(Value);

                    if (Response.Status == AppServiceResponseStatus.Success)
                    {
                        if (Response.Message.ContainsKey(ExecuteType_Test_Connection))
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

                Dispose();
                LogTracer.Log("Connect to FullTrustProcess failed after retrying 3 times. Dispose this instance");
                return false;
            }
            catch (Exception ex)
            {
                Dispose();
                LogTracer.Log(ex, $"An unexpected error was threw in {nameof(ConnectRemoteAsync)}");
                return false;
            }
        }

        private void Connection_ServiceClosed(AppServiceConnection sender, AppServiceClosedEventArgs args)
        {
            LogTracer.Log("AppServiceConnection closed, dispose this instance");
            Dispose();
        }

        public async Task<string> GetMIMEContentType(string Path)
        {
            try
            {
                IsAnyActionExcutingInCurrentController = true;

                if (await ConnectRemoteAsync())
                {
                    ValueSet Value = new ValueSet
                    {
                        {"ExecuteType", ExecuteType_GetMIMEContentType},
                        {"ExecutePath", Path}
                    };

                    AppServiceResponse Response = await Connection.SendMessageAsync(Value);

                    if (Response.Status == AppServiceResponseStatus.Success)
                    {
                        if (Response.Message.TryGetValue("Success", out object MIME))
                        {
                            return Convert.ToString(MIME);
                        }
                        else
                        {
                            if (Response.Message.TryGetValue("Error", out object ErrorMessage))
                            {
                                LogTracer.Log($"An unexpected error was threw in {nameof(GetMIMEContentType)}, message: {ErrorMessage}");
                            }

                            return string.Empty;
                        }
                    }
                    else
                    {
                        LogTracer.Log($"AppServiceResponse in {nameof(GetMIMEContentType)} return an invalid status. Status: {Enum.GetName(typeof(AppServiceResponseStatus), Response.Status)}");
                        return string.Empty;
                    }
                }
                else
                {
                    LogTracer.Log($"{nameof(GetMIMEContentType)}: Failed to connect AppService ");
                    return string.Empty;
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"{ nameof(GetMIMEContentType)} throw an error");
                return string.Empty;
            }
            finally
            {
                IsAnyActionExcutingInCurrentController = false;
            }
        }

        public async Task<byte[]> GetThumbnailOverlayAsync(string Path)
        {
            try
            {
                IsAnyActionExcutingInCurrentController = true;

                if (await ConnectRemoteAsync())
                {
                    ValueSet Value = new ValueSet
                    {
                        {"ExecuteType", ExecuteType_GetThumbnailOverlay},
                        {"Path", Path}
                    };

                    AppServiceResponse Response = await Connection.SendMessageAsync(Value);

                    if (Response.Status == AppServiceResponseStatus.Success)
                    {
                        if (Response.Message.TryGetValue("Success", out object ThumbnailOverlayStr))
                        {
                            return JsonSerializer.Deserialize<byte[]>(Convert.ToString(ThumbnailOverlayStr));
                        }
                        else
                        {
                            if (Response.Message.TryGetValue("Error", out object ErrorMessage))
                            {
                                LogTracer.Log($"An unexpected error was threw in {nameof(GetThumbnailOverlayAsync)}, message: {ErrorMessage}");
                            }

                            return Array.Empty<byte>();
                        }
                    }
                    else
                    {
                        LogTracer.Log($"AppServiceResponse in {nameof(GetThumbnailOverlayAsync)} return an invalid status. Status: {Enum.GetName(typeof(AppServiceResponseStatus), Response.Status)}");
                        return Array.Empty<byte>();
                    }
                }
                else
                {
                    LogTracer.Log($"{nameof(GetThumbnailOverlayAsync)}: Failed to connect AppService ");
                    return Array.Empty<byte>();
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"{ nameof(GetThumbnailOverlayAsync)} throw an error");
                return Array.Empty<byte>();
            }
            finally
            {
                IsAnyActionExcutingInCurrentController = false;
            }
        }

        public async Task<bool> SetAsTopMostWindowAsync(string PackageFamilyName, uint? WithPID = null)
        {
            try
            {
                IsAnyActionExcutingInCurrentController = true;

                if (await ConnectRemoteAsync())
                {
                    ValueSet Value = new ValueSet
                    {
                        {"ExecuteType", ExecuteType_SetAsTopMostWindow},
                        {"PackageFamilyName", PackageFamilyName}
                    };

                    if (WithPID != null)
                    {
                        Value.Add("WithPID", WithPID);
                    }

                    AppServiceResponse Response = await Connection.SendMessageAsync(Value);

                    if (Response.Status == AppServiceResponseStatus.Success)
                    {
                        if (Response.Message.TryGetValue("Success", out object ThumbnailOverlayStr))
                        {
                            return true;
                        }
                        else
                        {
                            if (Response.Message.TryGetValue("Error", out object ErrorMessage))
                            {
                                LogTracer.Log($"An unexpected error was threw in {nameof(SetAsTopMostWindowAsync)}, message: {ErrorMessage}");
                            }

                            return false;
                        }
                    }
                    else
                    {
                        LogTracer.Log($"AppServiceResponse in {nameof(SetAsTopMostWindowAsync)} return an invalid status. Status: {Enum.GetName(typeof(AppServiceResponseStatus), Response.Status)}");
                        return false;
                    }
                }
                else
                {
                    LogTracer.Log($"{nameof(SetAsTopMostWindowAsync)}: Failed to connect AppService ");
                    return false;
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"{ nameof(SetAsTopMostWindowAsync)} throw an error");
                return false;
            }
            finally
            {
                IsAnyActionExcutingInCurrentController = false;
            }
        }

        public async Task<bool> RemoveTopMostWindowAsync(string PackageFamilyName, uint? WithPID = null)
        {
            try
            {
                IsAnyActionExcutingInCurrentController = true;

                if (await ConnectRemoteAsync())
                {
                    ValueSet Value = new ValueSet
                    {
                        {"ExecuteType", ExecuteType_RemoveTopMostWindow},
                        {"PackageFamilyName", PackageFamilyName}
                    };

                    if (WithPID != null)
                    {
                        Value.Add("WithPID", WithPID);
                    }

                    AppServiceResponse Response = await Connection.SendMessageAsync(Value);

                    if (Response.Status == AppServiceResponseStatus.Success)
                    {
                        if (Response.Message.TryGetValue("Success", out object ThumbnailOverlayStr))
                        {
                            return true;
                        }
                        else
                        {
                            if (Response.Message.TryGetValue("Error", out object ErrorMessage))
                            {
                                LogTracer.Log($"An unexpected error was threw in {nameof(RemoveTopMostWindowAsync)}, message: {ErrorMessage}");
                            }

                            return false;
                        }
                    }
                    else
                    {
                        LogTracer.Log($"AppServiceResponse in {nameof(RemoveTopMostWindowAsync)} return an invalid status. Status: {Enum.GetName(typeof(AppServiceResponseStatus), Response.Status)}");
                        return false;
                    }
                }
                else
                {
                    LogTracer.Log($"{nameof(RemoveTopMostWindowAsync)}: Failed to connect AppService ");
                    return false;
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"{ nameof(RemoveTopMostWindowAsync)} throw an error");
                return false;
            }
            finally
            {
                IsAnyActionExcutingInCurrentController = false;
            }
        }

        public async Task<Dictionary<string, string>> GetDocumentProperties(string Path)
        {
            try
            {
                IsAnyActionExcutingInCurrentController = true;

                if (await ConnectRemoteAsync())
                {
                    ValueSet Value = new ValueSet
                    {
                        {"ExecuteType", ExecuteType_GetDocumentProperties},
                        {"ExecutePath", Path}
                    };

                    AppServiceResponse Response = await Connection.SendMessageAsync(Value);

                    if (Response.Status == AppServiceResponseStatus.Success)
                    {
                        if (Response.Message.TryGetValue("Success", out object Properties))
                        {
                            return JsonSerializer.Deserialize<Dictionary<string, string>>(Convert.ToString(Properties));
                        }
                        else
                        {
                            if (Response.Message.TryGetValue("Error", out object ErrorMessage))
                            {
                                LogTracer.Log($"An unexpected error was threw in {nameof(GetDocumentProperties)}, message: {ErrorMessage}");
                            }

                            return new Dictionary<string, string>(0);
                        }
                    }
                    else
                    {
                        LogTracer.Log($"AppServiceResponse in {nameof(GetDocumentProperties)} return an invalid status. Status: {Enum.GetName(typeof(AppServiceResponseStatus), Response.Status)}");
                        return new Dictionary<string, string>(0);
                    }
                }
                else
                {
                    LogTracer.Log($"{nameof(GetDocumentProperties)}: Failed to connect AppService ");
                    return new Dictionary<string, string>(0);
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"{ nameof(GetDocumentProperties)} throw an error");
                return new Dictionary<string, string>(0);
            }
            finally
            {
                IsAnyActionExcutingInCurrentController = false;
            }
        }

        public async Task SetFileAttribute(string Path, params KeyValuePair<ModifyAttributeAction, System.IO.FileAttributes>[] Attribute)
        {
            try
            {
                IsAnyActionExcutingInCurrentController = true;

                if (await ConnectRemoteAsync())
                {
                    ValueSet Value = new ValueSet
                    {
                        {"ExecuteType", ExecuteType_SetFileAttribute},
                        {"ExecutePath", Path},
                        {"Attributes", JsonSerializer.Serialize(Attribute)}
                    };

                    AppServiceResponse Response = await Connection.SendMessageAsync(Value);

                    if (Response.Status == AppServiceResponseStatus.Success)
                    {
                        if (Response.Message.TryGetValue("Error", out object ErrorMessage))
                        {
                            LogTracer.Log($"An unexpected error was threw in {nameof(SetFileAttribute)}, message: {ErrorMessage}");
                        }
                    }
                    else
                    {
                        LogTracer.Log($"AppServiceResponse in {nameof(SetFileAttribute)} return an invalid status. Status: {Enum.GetName(typeof(AppServiceResponseStatus), Response.Status)}");
                    }
                }
                else
                {
                    LogTracer.Log($"{nameof(SetFileAttribute)}: Failed to connect AppService ");
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"{ nameof(SetFileAttribute)} throw an error");
            }
            finally
            {
                IsAnyActionExcutingInCurrentController = false;
            }
        }

        public async Task<bool> CheckIfEverythingIsAvailableAsync()
        {
            try
            {
                IsAnyActionExcutingInCurrentController = true;

                if (await ConnectRemoteAsync())
                {
                    ValueSet Value = new ValueSet
                    {
                        {"ExecuteType", ExecuteType_CheckIfEverythingAvailable}
                    };

                    AppServiceResponse Response = await Connection.SendMessageAsync(Value);

                    if (Response.Status == AppServiceResponseStatus.Success)
                    {
                        if (Response.Message.ContainsKey("Success"))
                        {
                            return true;
                        }
                        else
                        {
                            if (Response.Message.TryGetValue("Error", out object ErrorMessage))
                            {
                                LogTracer.Log($"An unexpected error was threw in {nameof(CheckIfEverythingIsAvailableAsync)}, message: {ErrorMessage}");
                            }

                            return false;
                        }
                    }
                    else
                    {
                        LogTracer.Log($"AppServiceResponse in {nameof(CheckIfEverythingIsAvailableAsync)} return an invalid status. Status: {Enum.GetName(typeof(AppServiceResponseStatus), Response.Status)}");
                        return false;
                    }
                }
                else
                {
                    LogTracer.Log($"{nameof(CheckIfEverythingIsAvailableAsync)}: Failed to connect AppService ");
                    return false;
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"{ nameof(CheckIfEverythingIsAvailableAsync)} throw an error");
                return false;
            }
            finally
            {
                IsAnyActionExcutingInCurrentController = false;
            }
        }

        public async Task<IReadOnlyList<FileSystemStorageItemBase>> SearchByEverythingAsync(string BaseLocation, string SearchWord, bool SearchAsRegex = false, bool IgnoreCase = true, uint MaxCount = 500)
        {
            try
            {
                IsAnyActionExcutingInCurrentController = true;

                if (await ConnectRemoteAsync())
                {
                    ValueSet Value = new ValueSet
                    {
                        {"ExecuteType", ExecuteType_SearchByEverything},
                        {"BaseLocation", BaseLocation },
                        {"SearchWord", SearchWord },
                        {"SearchAsRegex", SearchAsRegex },
                        {"IgnoreCase", IgnoreCase },
                        {"MaxCount", MaxCount }
                    };

                    AppServiceResponse Response = await Connection.SendMessageAsync(Value);

                    if (Response.Status == AppServiceResponseStatus.Success)
                    {
                        if (Response.Message.TryGetValue("Success", out object Result))
                        {
                            string[] SearchResult = JsonSerializer.Deserialize<string[]>(Convert.ToString(Result));

                            if (SearchResult.Length == 0)
                            {
                                return new List<FileSystemStorageItemBase>(0);
                            }
                            else
                            {
                                return WIN_Native_API.GetStorageItemInBatch(SearchResult);
                            }
                        }
                        else
                        {
                            if (Response.Message.TryGetValue("Error", out object ErrorMessage))
                            {
                                LogTracer.Log($"An unexpected error was threw in {nameof(SearchByEverythingAsync)}, message: {ErrorMessage}");
                            }

                            return new List<FileSystemStorageItemBase>(0);
                        }
                    }
                    else
                    {
                        LogTracer.Log($"AppServiceResponse in {nameof(SearchByEverythingAsync)} return an invalid status. Status: {Enum.GetName(typeof(AppServiceResponseStatus), Response.Status)}");
                        return new List<FileSystemStorageItemBase>(0);
                    }
                }
                else
                {
                    LogTracer.Log($"{nameof(SearchByEverythingAsync)}: Failed to connect AppService ");
                    return new List<FileSystemStorageItemBase>(0);
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"{ nameof(SearchByEverythingAsync)} throw an error");
                return new List<FileSystemStorageItemBase>(0);
            }
            finally
            {
                IsAnyActionExcutingInCurrentController = false;
            }
        }

        public async Task<bool> LaunchUWPFromAUMIDAsync(string AppUserModelId, params string[] PathArray)
        {
            try
            {
                IsAnyActionExcutingInCurrentController = true;

                if (await ConnectRemoteAsync())
                {
                    ValueSet Value = new ValueSet
                    {
                        {"ExecuteType", ExecuteType_LaunchUWP},
                        {"AppUserModelId", AppUserModelId },
                        {"LaunchPathArray", JsonSerializer.Serialize(PathArray)}
                    };

                    AppServiceResponse Response = await Connection.SendMessageAsync(Value);

                    if (Response.Status == AppServiceResponseStatus.Success)
                    {
                        if (Response.Message.TryGetValue("Success", out object Result))
                        {
                            return true;
                        }
                        else
                        {
                            if (Response.Message.TryGetValue("Error", out object ErrorMessage))
                            {
                                LogTracer.Log($"An unexpected error was threw in {nameof(LaunchUWPFromAUMIDAsync)}, message: {ErrorMessage}");
                            }

                            return false;
                        }
                    }
                    else
                    {
                        LogTracer.Log($"AppServiceResponse in {nameof(LaunchUWPFromAUMIDAsync)} return an invalid status. Status: {Enum.GetName(typeof(AppServiceResponseStatus), Response.Status)}");
                        return false;
                    }
                }
                else
                {
                    LogTracer.Log($"{nameof(LaunchUWPFromAUMIDAsync)}: Failed to connect AppService ");
                    return false;
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"{ nameof(LaunchUWPFromAUMIDAsync)} throw an error");
                return false;
            }
            finally
            {
                IsAnyActionExcutingInCurrentController = false;
            }
        }

        public async Task<bool> LaunchUWPFromPfnAsync(string PackageFamilyName, params string[] PathArray)
        {
            try
            {
                IsAnyActionExcutingInCurrentController = true;

                if (await ConnectRemoteAsync())
                {
                    ValueSet Value = new ValueSet
                    {
                        {"ExecuteType", ExecuteType_LaunchUWP},
                        {"PackageFamilyName", PackageFamilyName },
                        {"LaunchPathArray", JsonSerializer.Serialize(PathArray)}
                    };

                    AppServiceResponse Response = await Connection.SendMessageAsync(Value);

                    if (Response.Status == AppServiceResponseStatus.Success)
                    {
                        if (Response.Message.TryGetValue("Success", out object Result))
                        {
                            return true;
                        }
                        else
                        {
                            if (Response.Message.TryGetValue("Error", out object ErrorMessage))
                            {
                                LogTracer.Log($"An unexpected error was threw in {nameof(LaunchUWPFromPfnAsync)}, message: {ErrorMessage}");
                            }

                            return false;
                        }
                    }
                    else
                    {
                        LogTracer.Log($"AppServiceResponse in {nameof(LaunchUWPFromPfnAsync)} return an invalid status. Status: {Enum.GetName(typeof(AppServiceResponseStatus), Response.Status)}");
                        return false;
                    }
                }
                else
                {
                    LogTracer.Log($"{nameof(LaunchUWPFromPfnAsync)}: Failed to connect AppService ");
                    return false;
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"{ nameof(LaunchUWPFromPfnAsync)} throw an error");
                return false;
            }
            finally
            {
                IsAnyActionExcutingInCurrentController = false;
            }
        }

        public async Task<bool> CheckIfPackageFamilyNameExist(string PackageFamilyName)
        {
            try
            {
                IsAnyActionExcutingInCurrentController = true;

                if (await ConnectRemoteAsync())
                {
                    ValueSet Value = new ValueSet
                    {
                        {"ExecuteType", ExecuteType_CheckPackageFamilyNameExist},
                        {"PackageFamilyName", PackageFamilyName }
                    };

                    AppServiceResponse Response = await Connection.SendMessageAsync(Value);

                    if (Response.Status == AppServiceResponseStatus.Success)
                    {
                        if (Response.Message.TryGetValue("Success", out object Result))
                        {
                            return Convert.ToBoolean(Result);
                        }
                        else
                        {
                            if (Response.Message.TryGetValue("Error", out object ErrorMessage))
                            {
                                LogTracer.Log($"An unexpected error was threw in {nameof(CheckIfPackageFamilyNameExist)}, message: {ErrorMessage}");
                            }

                            return false;
                        }
                    }
                    else
                    {
                        LogTracer.Log($"AppServiceResponse in {nameof(CheckIfPackageFamilyNameExist)} return an invalid status. Status: {Enum.GetName(typeof(AppServiceResponseStatus), Response.Status)}");
                        return false;
                    }
                }
                else
                {
                    LogTracer.Log($"{nameof(CheckIfPackageFamilyNameExist)}: Failed to connect AppService ");
                    return false;
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"{ nameof(CheckIfPackageFamilyNameExist)} throw an error");
                return false;
            }
            finally
            {
                IsAnyActionExcutingInCurrentController = false;
            }
        }

        public async Task<InstalledApplication> GetInstalledApplicationAsync(string PackageFamilyName)
        {
            try
            {
                IsAnyActionExcutingInCurrentController = true;

                if (await ConnectRemoteAsync())
                {
                    ValueSet Value = new ValueSet
                    {
                        {"ExecuteType", ExecuteType_GetInstalledApplication},
                        {"PackageFamilyName", PackageFamilyName }
                    };

                    AppServiceResponse Response = await Connection.SendMessageAsync(Value);

                    if (Response.Status == AppServiceResponseStatus.Success)
                    {
                        if (Response.Message.TryGetValue("Success", out object Result))
                        {
                            InstalledApplicationPackage Pack = JsonSerializer.Deserialize<InstalledApplicationPackage>(Convert.ToString(Result));

                            return await InstalledApplication.CreateAsync(Pack);
                        }
                        else
                        {
                            if (Response.Message.TryGetValue("Error", out object ErrorMessage))
                            {
                                LogTracer.Log($"An unexpected error was threw in {nameof(GetInstalledApplicationAsync)}, message: {ErrorMessage}");
                            }

                            return null;
                        }
                    }
                    else
                    {
                        LogTracer.Log($"AppServiceResponse in {nameof(GetInstalledApplicationAsync)} return an invalid status. Status: {Enum.GetName(typeof(AppServiceResponseStatus), Response.Status)}");
                        return null;
                    }
                }
                else
                {
                    LogTracer.Log($"{nameof(GetInstalledApplicationAsync)}: Failed to connect AppService ");
                    return null;
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"{ nameof(GetInstalledApplicationAsync)} throw an error");
                return null;
            }
            finally
            {
                IsAnyActionExcutingInCurrentController = false;
            }
        }


        public async Task<IReadOnlyList<InstalledApplication>> GetAllInstalledApplicationAsync()
        {
            try
            {
                IsAnyActionExcutingInCurrentController = true;

                if (await ConnectRemoteAsync())
                {
                    ValueSet Value = new ValueSet
                    {
                        {"ExecuteType", ExecuteType_GetAllInstalledApplication}
                    };

                    AppServiceResponse Response = await Connection.SendMessageAsync(Value);

                    if (Response.Status == AppServiceResponseStatus.Success)
                    {
                        if (Response.Message.TryGetValue("Success", out object Result))
                        {
                            List<InstalledApplication> PackageList = new List<InstalledApplication>();

                            foreach (InstalledApplicationPackage Pack in JsonSerializer.Deserialize<InstalledApplicationPackage[]>(Convert.ToString(Result)))
                            {
                                PackageList.Add(await InstalledApplication.CreateAsync(Pack));
                            }

                            return PackageList.ToArray();
                        }
                        else
                        {
                            if (Response.Message.TryGetValue("Error", out object ErrorMessage))
                            {
                                LogTracer.Log($"An unexpected error was threw in {nameof(GetAllInstalledApplicationAsync)}, message: {ErrorMessage}");
                            }

                            return Array.Empty<InstalledApplication>();
                        }
                    }
                    else
                    {
                        LogTracer.Log($"AppServiceResponse in {nameof(GetAllInstalledApplicationAsync)} return an invalid status. Status: {Enum.GetName(typeof(AppServiceResponseStatus), Response.Status)}");
                        return Array.Empty<InstalledApplication>();
                    }
                }
                else
                {
                    LogTracer.Log($"{nameof(GetAllInstalledApplicationAsync)}: Failed to connect AppService ");
                    return Array.Empty<InstalledApplication>();
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"{ nameof(GetAllInstalledApplicationAsync)} throw an error");
                return Array.Empty<InstalledApplication>();
            }
            finally
            {
                IsAnyActionExcutingInCurrentController = false;
            }
        }


        public async Task<HiddenDataPackage> GetHiddenItemDataAsync(string Path)
        {
            try
            {
                IsAnyActionExcutingInCurrentController = true;

                if (Path.Any())
                {
                    if (await ConnectRemoteAsync())
                    {
                        ValueSet Value = new ValueSet
                        {
                            {"ExecuteType", ExecuteType_GetHiddenItemInfo},
                            {"ExecutePath", Path}
                        };

                        AppServiceResponse Response = await Connection.SendMessageAsync(Value);

                        if (Response.Status == AppServiceResponseStatus.Success)
                        {
                            if (Response.Message.TryGetValue("Success", out object Result))
                            {
                                return JsonSerializer.Deserialize<HiddenDataPackage>(Convert.ToString(Result));
                            }
                            else
                            {
                                if (Response.Message.TryGetValue("Error", out object ErrorMessage))
                                {
                                    LogTracer.Log($"An unexpected error was threw in {nameof(GetHiddenItemDataAsync)}, message: {ErrorMessage}");
                                }

                                return null;
                            }
                        }
                        else
                        {
                            LogTracer.Log($"AppServiceResponse in {nameof(GetHiddenItemDataAsync)} return an invalid status. Status: {Enum.GetName(typeof(AppServiceResponseStatus), Response.Status)}");
                            return null;
                        }
                    }
                    else
                    {
                        LogTracer.Log($"{nameof(GetHiddenItemDataAsync)}: Failed to connect AppService ");
                        return null;
                    }
                }
                else
                {
                    return null;
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"{ nameof(GetHiddenItemDataAsync)} throw an error");
                return null;
            }
            finally
            {
                IsAnyActionExcutingInCurrentController = false;
            }
        }

        public async Task<IReadOnlyList<ContextMenuItem>> GetContextMenuItemsAsync(string[] PathArray, bool IncludeExtensionItem = false)
        {
            try
            {
                IsAnyActionExcutingInCurrentController = true;

                if (PathArray.All((Path) => !string.IsNullOrWhiteSpace(Path)))
                {
                    if (await ConnectRemoteAsync())
                    {
                        ValueSet Value = new ValueSet
                        {
                            {"ExecuteType", ExecuteType_GetContextMenuItems},
                            {"ExecutePath", JsonSerializer.Serialize(PathArray)},
                            {"IncludeExtensionItem", IncludeExtensionItem }
                        };

                        AppServiceResponse Response = await Connection.SendMessageAsync(Value);

                        if (Response.Status == AppServiceResponseStatus.Success)
                        {
                            if (Response.Message.TryGetValue("Success", out object Result))
                            {
                                return JsonSerializer.Deserialize<ContextMenuPackage[]>(Convert.ToString(Result)).Select((Item) => new ContextMenuItem(Item)).ToList();
                            }
                            else
                            {
                                if (Response.Message.TryGetValue("Error", out object ErrorMessage))
                                {
                                    LogTracer.Log($"An unexpected error was threw in {nameof(GetContextMenuItemsAsync)}, message: {ErrorMessage}");
                                }

                                return new List<ContextMenuItem>(0);
                            }
                        }
                        else
                        {
                            LogTracer.Log($"AppServiceResponse in {nameof(GetContextMenuItemsAsync)} return an invalid status. Status: {Enum.GetName(typeof(AppServiceResponseStatus), Response.Status)}");
                            return new List<ContextMenuItem>(0);
                        }
                    }
                    else
                    {
                        LogTracer.Log($"{nameof(GetContextMenuItemsAsync)}: Failed to connect AppService ");
                        return new List<ContextMenuItem>(0);
                    }
                }
                else
                {
                    return new List<ContextMenuItem>(0);
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"{ nameof(GetContextMenuItemsAsync)} throw an error");
                return new List<ContextMenuItem>(0);
            }
            finally
            {
                IsAnyActionExcutingInCurrentController = false;
            }
        }

        public async Task<bool> InvokeContextMenuItemAsync(ContextMenuPackage Package)
        {
            if (Package == null)
            {
                throw new ArgumentNullException(nameof(Package), "Argument could not be null");
            }

            try
            {
                IsAnyActionExcutingInCurrentController = true;

                if (await ConnectRemoteAsync())
                {
                    ValueSet Value = new ValueSet
                    {
                        {"ExecuteType", ExecuteType_InvokeContextMenuItem},
                        {"RelatedPath", JsonSerializer.Serialize(Package.RelatedPath) },
                        {"Verb", Package.Verb },
                        {"Id", Package.Id },
                        {"IncludeExtensionItem", Package.IncludeExtensionItem }
                    };

                    AppServiceResponse Response = await Connection.SendMessageAsync(Value);

                    if (Response.Status == AppServiceResponseStatus.Success)
                    {
                        if (Response.Message.TryGetValue("Error", out object ErrorMessage))
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
                        LogTracer.Log($"AppServiceResponse in {nameof(GetContextMenuItemsAsync)} return an invalid status. Status: {Enum.GetName(typeof(AppServiceResponseStatus), Response.Status)}");
                        return false;
                    }
                }
                else
                {
                    LogTracer.Log($"{nameof(GetContextMenuItemsAsync)}: Failed to connect AppService ");
                    return false;
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"{ nameof(GetContextMenuItemsAsync)} throw an error");
                return false;
            }
            finally
            {
                IsAnyActionExcutingInCurrentController = false;
            }
        }

        public async Task<bool> CreateLinkAsync(string LinkPath, string LinkTarget, string WorkDirectory, WindowState WindowState, int HotKey, string Comment, params string[] LinkArgument)
        {
            try
            {
                IsAnyActionExcutingInCurrentController = true;

                if (await ConnectRemoteAsync().ConfigureAwait(false))
                {
                    ValueSet Value = new ValueSet
                    {
                        {"ExecuteType", ExecuteType_CreateLink},
                        {"DataPackage", JsonSerializer.Serialize(new LinkDataPackage(LinkPath, LinkTarget, WorkDirectory, WindowState, HotKey, Comment, false, null, LinkArgument)) }
                    };

                    AppServiceResponse Response = await Connection.SendMessageAsync(Value);

                    if (Response.Status == AppServiceResponseStatus.Success)
                    {
                        if (Response.Message.ContainsKey("Success"))
                        {
                            return true;
                        }
                        else
                        {
                            if (Response.Message.TryGetValue("Error", out object ErrorMessage))
                            {
                                LogTracer.Log($"An unexpected error was threw in {nameof(CreateLinkAsync)}, message: {ErrorMessage}");
                            }

                            return false;
                        }
                    }
                    else
                    {
                        LogTracer.Log($"AppServiceResponse in {nameof(CreateLinkAsync)} return an invalid status. Status: {Enum.GetName(typeof(AppServiceResponseStatus), Response.Status)}");
                        return false;
                    }
                }
                else
                {
                    LogTracer.Log($"{nameof(CreateLinkAsync)}: Failed to connect AppService ");
                    return false;
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"{ nameof(CreateLinkAsync)} throw an error");
                return false;
            }
            finally
            {
                IsAnyActionExcutingInCurrentController = false;
            }
        }

        public async Task UpdateLinkAsync(string LinkPath, string LinkTarget, string WorkDirectory, WindowState WindowState, int HotKey, string Comment, bool NeedRunAsAdmin, params string[] LinkArgument)
        {
            try
            {
                IsAnyActionExcutingInCurrentController = true;

                if (await ConnectRemoteAsync().ConfigureAwait(false))
                {
                    ValueSet Value = new ValueSet
                    {
                        {"ExecuteType", ExecuteType_UpdateLink},
                        {"DataPackage", JsonSerializer.Serialize(new LinkDataPackage(LinkPath, LinkTarget, WorkDirectory, WindowState, HotKey, Comment, NeedRunAsAdmin, null, LinkArgument)) }
                    };

                    AppServiceResponse Response = await Connection.SendMessageAsync(Value);

                    if (Response.Status == AppServiceResponseStatus.Success)
                    {
                        if (Response.Message.TryGetValue("Error", out object ErrorMessage))
                        {
                            LogTracer.Log($"An unexpected error was threw in {nameof(UpdateLinkAsync)}, message: {ErrorMessage}");
                        }
                    }
                    else
                    {
                        LogTracer.Log($"AppServiceResponse in {nameof(UpdateLinkAsync)} return an invalid status. Status: {Enum.GetName(typeof(AppServiceResponseStatus), Response.Status)}");
                    }
                }
                else
                {
                    LogTracer.Log($"{nameof(UpdateLinkAsync)}: Failed to connect AppService ");
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"{ nameof(UpdateLinkAsync)} throw an error");
            }
            finally
            {
                IsAnyActionExcutingInCurrentController = false;
            }
        }

        public async Task UpdateUrlAsync(string UrlPath, string UrlTargetPath)
        {
            try
            {
                IsAnyActionExcutingInCurrentController = true;

                if (await ConnectRemoteAsync().ConfigureAwait(false))
                {
                    ValueSet Value = new ValueSet
                    {
                        {"ExecuteType", ExecuteType_UpdateUrl},
                        {"DataPackage", JsonSerializer.Serialize(new UrlDataPackage(UrlPath, UrlTargetPath, Array.Empty<byte>())) }
                    };

                    AppServiceResponse Response = await Connection.SendMessageAsync(Value);

                    if (Response.Status == AppServiceResponseStatus.Success)
                    {
                        if (Response.Message.TryGetValue("Error", out object ErrorMessage))
                        {
                            LogTracer.Log($"An unexpected error was threw in {nameof(UpdateUrlAsync)}, message: {ErrorMessage}");
                        }
                    }
                    else
                    {
                        LogTracer.Log($"AppServiceResponse in {nameof(UpdateUrlAsync)} return an invalid status. Status: {Enum.GetName(typeof(AppServiceResponseStatus), Response.Status)}");
                    }
                }
                else
                {
                    LogTracer.Log($"{nameof(UpdateUrlAsync)}: Failed to connect AppService ");
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"{ nameof(UpdateUrlAsync)} throw an error");
            }
            finally
            {
                IsAnyActionExcutingInCurrentController = false;
            }
        }

        public async Task<string> GetVariablePathAsync(string Variable)
        {
            try
            {
                IsAnyActionExcutingInCurrentController = true;

                if (await ConnectRemoteAsync().ConfigureAwait(false))
                {
                    ValueSet Value = new ValueSet
                    {
                        {"ExecuteType", ExecuteType_GetVariablePath},
                        {"Variable", Variable }
                    };

                    AppServiceResponse Response = await Connection.SendMessageAsync(Value);

                    if (Response.Status == AppServiceResponseStatus.Success)
                    {
                        if (Response.Message.TryGetValue("Success", out object Result))
                        {
                            return Convert.ToString(Result);
                        }
                        else
                        {
                            if (Response.Message.TryGetValue("Error", out object ErrorMessage))
                            {
                                LogTracer.Log($"An unexpected error was threw in {nameof(GetVariablePathAsync)}, message: {ErrorMessage}");
                            }

                            return string.Empty;
                        }
                    }
                    else
                    {
                        LogTracer.Log($"AppServiceResponse in {nameof(GetVariablePathAsync)} return an invalid status. Status: {Enum.GetName(typeof(AppServiceResponseStatus), Response.Status)}");
                        return string.Empty;
                    }
                }
                else
                {
                    LogTracer.Log($"{nameof(GetVariablePathAsync)}: Failed to connect AppService ");
                    return string.Empty;
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"{ nameof(GetVariablePathAsync)} throw an error");
                return string.Empty;
            }
            finally
            {
                IsAnyActionExcutingInCurrentController = false;
            }
        }

        public async Task<string> RenameAsync(string Path, string DesireName, bool SkipOperationRecord = false)
        {
            try
            {
                IsAnyActionExcutingInCurrentController = true;

                if (await ConnectRemoteAsync().ConfigureAwait(false))
                {
                    ValueSet Value = new ValueSet
                    {
                        {"ExecuteType", ExecuteType_Rename},
                        {"ExecutePath", Path},
                        {"DesireName", DesireName}
                    };

                    AppServiceResponse Response = await Connection.SendMessageAsync(Value);

                    if (Response.Status == AppServiceResponseStatus.Success)
                    {
                        if (Response.Message.TryGetValue("Success", out object NewName))
                        {
                            string NewNameString = Convert.ToString(NewName);

                            if (!SkipOperationRecord)
                            {
                                OperationRecorder.Current.Push($"{Path}||Rename||{System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Path), NewNameString)}");
                            }

                            return NewNameString;
                        }
                        else if (Response.Message.TryGetValue("Error_Capture", out object ErrorMessage1))
                        {
                            LogTracer.Log($"An unexpected error was threw in {nameof(RenameAsync)}, message: {ErrorMessage1}");
                            throw new FileLoadException();
                        }
                        else if (Response.Message.TryGetValue("Error_Failure", out object ErrorMessage2))
                        {
                            LogTracer.Log($"An unexpected error was threw in {nameof(RenameAsync)}, message: {ErrorMessage2}");
                            throw new InvalidOperationException();
                        }
                        else if (Response.Message.TryGetValue("Error_NoPermission", out object ErrorMessage3))
                        {
                            LogTracer.Log($"An unexpected error was threw in {nameof(RenameAsync)}, message: {ErrorMessage3}");
                            throw new InvalidOperationException();
                        }
                        else if (Response.Message.TryGetValue("Error", out object ErrorMessage4))
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
                        LogTracer.Log($"AppServiceResponse in {nameof(RenameAsync)} return an invalid status. Status: {Enum.GetName(typeof(AppServiceResponseStatus), Response.Status)}");
                        throw new NoResponseException();
                    }
                }
                else
                {
                    LogTracer.Log($"{nameof(RenameAsync)}: Failed to connect AppService ");
                    throw new NoResponseException();
                }
            }
            finally
            {
                IsAnyActionExcutingInCurrentController = false;
            }
        }

        public async Task<LinkDataPackage> GetLnkDataAsync(string Path)
        {
            try
            {
                IsAnyActionExcutingInCurrentController = true;

                if (await ConnectRemoteAsync().ConfigureAwait(false))
                {
                    ValueSet Value = new ValueSet
                    {
                        {"ExecuteType", ExecuteType_GetLnkData},
                        {"ExecutePath", Path}
                    };

                    AppServiceResponse Response = await Connection.SendMessageAsync(Value);

                    if (Response.Status == AppServiceResponseStatus.Success)
                    {
                        if (Response.Message.TryGetValue("Success", out object Result))
                        {
                            return JsonSerializer.Deserialize<LinkDataPackage>(Convert.ToString(Result));
                        }
                        else
                        {
                            if (Response.Message.TryGetValue("Error", out object ErrorMessage))
                            {
                                LogTracer.Log($"An unexpected error was threw in {nameof(GetLnkDataAsync)}, message: {ErrorMessage}");
                            }

                            throw new InvalidOperationException();
                        }
                    }
                    else
                    {
                        LogTracer.Log($"AppServiceResponse in {nameof(GetLnkDataAsync)} return an invalid status. Status: {Enum.GetName(typeof(AppServiceResponseStatus), Response.Status)}");

                        throw new NoResponseException();
                    }
                }
                else
                {
                    LogTracer.Log($"{nameof(GetLnkDataAsync)}: Failed to connect AppService");
                    throw new NoResponseException();
                }
            }
            finally
            {
                IsAnyActionExcutingInCurrentController = false;
            }
        }

        public async Task<UrlDataPackage> GetUrlDataAsync(string Path)
        {
            try
            {
                IsAnyActionExcutingInCurrentController = true;

                if (await ConnectRemoteAsync().ConfigureAwait(false))
                {
                    ValueSet Value = new ValueSet
                    {
                        {"ExecuteType", ExecuteType_GetUrlData},
                        {"ExecutePath", Path}
                    };

                    AppServiceResponse Response = await Connection.SendMessageAsync(Value);

                    if (Response.Status == AppServiceResponseStatus.Success)
                    {
                        if (Response.Message.TryGetValue("Success", out object Result))
                        {
                            return JsonSerializer.Deserialize<UrlDataPackage>(Convert.ToString(Result));
                        }
                        else
                        {
                            if (Response.Message.TryGetValue("Error", out object ErrorMessage))
                            {
                                LogTracer.Log($"An unexpected error was threw in {nameof(GetUrlDataAsync)}, message: {ErrorMessage}");
                            }

                            throw new InvalidOperationException();
                        }
                    }
                    else
                    {
                        LogTracer.Log($"AppServiceResponse in {nameof(GetUrlDataAsync)} return an invalid status. Status: {Enum.GetName(typeof(AppServiceResponseStatus), Response.Status)}");

                        throw new NoResponseException();
                    }
                }
                else
                {
                    LogTracer.Log($"{nameof(GetUrlDataAsync)}: Failed to connect AppService");
                    throw new NoResponseException();
                }
            }
            finally
            {
                IsAnyActionExcutingInCurrentController = false;
            }
        }

        public async Task<bool> InterceptWindowsPlusEAsync()
        {
            try
            {
                IsAnyActionExcutingInCurrentController = true;

                if (await ConnectRemoteAsync().ConfigureAwait(false))
                {
                    ValueSet Value = new ValueSet
                    {
                        {"ExecuteType", ExecuteType_InterceptWinE}
                    };

                    AppServiceResponse Response = await Connection.SendMessageAsync(Value);

                    if (Response.Status == AppServiceResponseStatus.Success)
                    {
                        if (Response.Message.ContainsKey("Success"))
                        {
                            return true;
                        }
                        else
                        {
                            if (Response.Message.TryGetValue("Error", out object ErrorMessage))
                            {
                                LogTracer.Log($"An unexpected error was threw in {nameof(InterceptWindowsPlusEAsync)}, message: {ErrorMessage}");
                            }

                            return false;
                        }
                    }
                    else
                    {
                        LogTracer.Log($"AppServiceResponse in {nameof(InterceptWindowsPlusEAsync)} return an invalid status. Status: {Enum.GetName(typeof(AppServiceResponseStatus), Response.Status)}");
                        return false;
                    }
                }
                else
                {
                    LogTracer.Log($"{nameof(InterceptWindowsPlusEAsync)}: Failed to connect AppService");
                    return false;
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"{nameof(InterceptWindowsPlusEAsync)} throw an error");
                return false;
            }
            finally
            {
                IsAnyActionExcutingInCurrentController = false;
            }
        }

        public async Task<bool> RestoreWindowsPlusEAsync()
        {
            try
            {
                IsAnyActionExcutingInCurrentController = true;

                if (await ConnectRemoteAsync().ConfigureAwait(false))
                {
                    ValueSet Value = new ValueSet
                    {
                        {"ExecuteType", ExecuteType_RestoreWinE}
                    };

                    AppServiceResponse Response = await Connection.SendMessageAsync(Value);

                    if (Response.Status == AppServiceResponseStatus.Success)
                    {
                        if (Response.Message.ContainsKey("Success"))
                        {
                            return true;
                        }
                        else
                        {
                            if (Response.Message.TryGetValue("Error", out object ErrorMessage))
                            {
                                LogTracer.Log($"An unexpected error was threw in {nameof(RestoreWindowsPlusEAsync)}, message: {ErrorMessage}");
                            }

                            return false;
                        }
                    }
                    else
                    {
                        LogTracer.Log($"AppServiceResponse in {nameof(RestoreWindowsPlusEAsync)} return an invalid status. Status: {Enum.GetName(typeof(AppServiceResponseStatus), Response.Status)}");

                        return false;
                    }
                }
                else
                {
                    LogTracer.Log($"{nameof(RestoreWindowsPlusEAsync)}: Failed to connect AppService");
                    return false;
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"{nameof(RestoreWindowsPlusEAsync)} throw an error");
                return false;
            }
            finally
            {
                IsAnyActionExcutingInCurrentController = false;
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
            try
            {
                IsAnyActionExcutingInCurrentController = true;

                if (await ConnectRemoteAsync().ConfigureAwait(false))
                {
                    ValueSet Value = new ValueSet
                    {
                        {"ExecuteType", ExecuteType_RunExe},
                        {"ExecutePath",Path },
                        {"ExecuteParameter", string.Join(' ', Parameters.Select((Para) => (Para.Contains(" ") && !Para.StartsWith("\"") && !Para.EndsWith("\"")) ? $"\"{Para}\"" : Para))},
                        {"ExecuteAuthority", RunAsAdmin ? ExecuteAuthority_Administrator : ExecuteAuthority_Normal},
                        {"ExecuteCreateNoWindow", CreateNoWindow },
                        {"ExecuteShouldWaitForExit", ShouldWaitForExit },
                        {"ExecuteWorkDirectory", WorkDirectory??string.Empty },
                        {"ExecuteWindowStyle", Enum.GetName(typeof(WindowState), WindowStyle) }
                    };

                    AppServiceResponse Response = await Connection.SendMessageAsync(Value);

                    if (Response.Status == AppServiceResponseStatus.Success)
                    {
                        if (Response.Message.TryGetValue("Error", out object ErrorMessage2))
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
                        LogTracer.Log($"AppServiceResponse in {nameof(RunAsync)} return an invalid status. Status: {Enum.GetName(typeof(AppServiceResponseStatus), Response.Status)}");
                        return false;
                    }
                }
                else
                {
                    LogTracer.Log($"{nameof(RunAsync)}: Failed to connect AppService");
                    return false;
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"{nameof(RunAsync)} throw an error");
                return false;
            }
            finally
            {
                IsAnyActionExcutingInCurrentController = false;
            }
        }

        public async Task ViewWithQuicklookAsync(string Path)
        {
            try
            {
                IsAnyActionExcutingInCurrentController = true;

                if (await ConnectRemoteAsync().ConfigureAwait(false))
                {
                    ValueSet Value = new ValueSet
                    {
                        {"ExecuteType", ExecuteType_Quicklook},
                        {"ExecutePath",Path }
                    };

                    await Connection.SendMessageAsync(Value);
                }
                else
                {
                    LogTracer.Log($"{nameof(ViewWithQuicklookAsync)}: Failed to connect AppService");
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"{nameof(ViewWithQuicklookAsync)} throw an error");
            }
            finally
            {
                IsAnyActionExcutingInCurrentController = false;
            }
        }

        public async Task<bool> CheckIfQuicklookIsAvaliableAsync()
        {
            try
            {
                IsAnyActionExcutingInCurrentController = true;

                if (await ConnectRemoteAsync().ConfigureAwait(false))
                {
                    ValueSet Value = new ValueSet
                    {
                        {"ExecuteType", ExecuteType_Check_Quicklook}
                    };

                    AppServiceResponse Response = await Connection.SendMessageAsync(Value);

                    if (Response.Status == AppServiceResponseStatus.Success)
                    {
                        if (Response.Message.TryGetValue("Check_QuicklookIsAvaliable_Result", out object Result))
                        {
                            return Convert.ToBoolean(Result);
                        }
                        else
                        {
                            if (Response.Message.TryGetValue("Error", out object ErrorMessage))
                            {
                                LogTracer.Log($"An unexpected error was threw in {nameof(CheckIfQuicklookIsAvaliableAsync)}, message: {ErrorMessage}");
                            }

                            return false;
                        }
                    }
                    else
                    {
                        LogTracer.Log($"AppServiceResponse in {nameof(CheckIfQuicklookIsAvaliableAsync)} return an invalid status. Status: {Enum.GetName(typeof(AppServiceResponseStatus), Response.Status)}");
                        return false;
                    }
                }
                else
                {
                    LogTracer.Log($"{nameof(CheckIfQuicklookIsAvaliableAsync)}: Failed to connect AppService");
                    return false;
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"{nameof(CheckIfQuicklookIsAvaliableAsync)} throw an error");
                return false;
            }
            finally
            {
                IsAnyActionExcutingInCurrentController = false;
            }
        }

        public async Task<string> GetDefaultAssociationFromPathAsync(string Path)
        {
            try
            {
                IsAnyActionExcutingInCurrentController = true;

                if (await ConnectRemoteAsync().ConfigureAwait(false))
                {
                    ValueSet Value = new ValueSet
                    {
                        {"ExecuteType", ExecuteType_Default_Association},
                        {"ExecutePath", Path}
                    };

                    AppServiceResponse Response = await Connection.SendMessageAsync(Value);

                    if (Response.Status == AppServiceResponseStatus.Success)
                    {
                        if (Response.Message.TryGetValue("Success", out object Result))
                        {
                            return Convert.ToString(Result);
                        }
                        else
                        {
                            if (Response.Message.TryGetValue("Error", out object ErrorMessage))
                            {
                                LogTracer.Log($"An unexpected error was threw in {nameof(GetDefaultAssociationFromPathAsync)}, message: {ErrorMessage}");
                            }

                            return string.Empty;
                        }
                    }
                    else
                    {
                        LogTracer.Log($"AppServiceResponse in {nameof(GetDefaultAssociationFromPathAsync)} return an invalid status. Status: {Enum.GetName(typeof(AppServiceResponseStatus), Response.Status)}");
                        return string.Empty;
                    }
                }
                else
                {
                    LogTracer.Log($"{nameof(GetDefaultAssociationFromPathAsync)}: Failed to connect AppService");
                    return string.Empty;
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"{nameof(GetDefaultAssociationFromPathAsync)} throw an error");
                return string.Empty;
            }
            finally
            {
                IsAnyActionExcutingInCurrentController = false;
            }
        }

        public async Task<IReadOnlyList<AssociationPackage>> GetAssociationFromPathAsync(string Path)
        {
            try
            {
                IsAnyActionExcutingInCurrentController = true;

                if (await ConnectRemoteAsync().ConfigureAwait(false))
                {
                    ValueSet Value = new ValueSet
                    {
                        {"ExecuteType", ExecuteType_Get_Association},
                        {"ExecutePath", Path}
                    };

                    AppServiceResponse Response = await Connection.SendMessageAsync(Value);

                    if (Response.Status == AppServiceResponseStatus.Success)
                    {
                        if (Response.Message.TryGetValue("Associate_Result", out object Result))
                        {
                            return JsonSerializer.Deserialize<List<AssociationPackage>>(Convert.ToString(Result));
                        }
                        else
                        {
                            if (Response.Message.TryGetValue("Error", out object ErrorMessage))
                            {
                                LogTracer.Log($"An unexpected error was threw in {nameof(GetAssociationFromPathAsync)}, message: {ErrorMessage}");
                            }

                            return new List<AssociationPackage>(0);
                        }
                    }
                    else
                    {
                        LogTracer.Log($"AppServiceResponse in {nameof(GetAssociationFromPathAsync)} return an invalid status. Status: {Enum.GetName(typeof(AppServiceResponseStatus), Response.Status)}");
                        return new List<AssociationPackage>(0);
                    }
                }
                else
                {
                    LogTracer.Log($"{nameof(GetAssociationFromPathAsync)}: Failed to connect AppService");
                    return new List<AssociationPackage>(0);
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"{nameof(GetAssociationFromPathAsync)} throw an error");
                return new List<AssociationPackage>(0);
            }
            finally
            {
                IsAnyActionExcutingInCurrentController = false;
            }
        }

        public async Task<bool> EmptyRecycleBinAsync()
        {
            try
            {
                IsAnyActionExcutingInCurrentController = true;

                if (await ConnectRemoteAsync().ConfigureAwait(false))
                {
                    ValueSet Value = new ValueSet
                    {
                        {"ExecuteType", ExecuteType_EmptyRecycleBin}
                    };

                    AppServiceResponse Response = await Connection.SendMessageAsync(Value);

                    if (Response.Status == AppServiceResponseStatus.Success)
                    {
                        if (Response.Message.TryGetValue("RecycleBinItems_Clear_Result", out object Result))
                        {
                            return Convert.ToBoolean(Result);
                        }
                        else
                        {
                            if (Response.Message.TryGetValue("Error", out object ErrorMessage))
                            {
                                LogTracer.Log($"An unexpected error was threw in {nameof(EmptyRecycleBinAsync)}, message: {ErrorMessage}");
                            }

                            return false;
                        }
                    }
                    else
                    {
                        LogTracer.Log($"AppServiceResponse in {nameof(EmptyRecycleBinAsync)} return an invalid status. Status: {Enum.GetName(typeof(AppServiceResponseStatus), Response.Status)}");
                        return false;
                    }
                }
                else
                {
                    LogTracer.Log($"{nameof(EmptyRecycleBinAsync)}: Failed to connect AppService");
                    return false;
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"{nameof(EmptyRecycleBinAsync)} throw an error");
                return false;
            }
            finally
            {
                IsAnyActionExcutingInCurrentController = false;
            }
        }

        public async Task<IReadOnlyList<IRecycleStorageItem>> GetRecycleBinItemsAsync()
        {
            try
            {
                IsAnyActionExcutingInCurrentController = true;

                if (await ConnectRemoteAsync())
                {
                    ValueSet Value = new ValueSet
                    {
                        {"ExecuteType", ExecuteType_Get_RecycleBinItems}
                    };

                    AppServiceResponse Response = await Connection.SendMessageAsync(Value);

                    if (Response.Status == AppServiceResponseStatus.Success)
                    {
                        if (Response.Message.TryGetValue("RecycleBinItems_Json_Result", out object Result))
                        {
                            List<Dictionary<string, string>> JsonList = JsonSerializer.Deserialize<List<Dictionary<string, string>>>(Convert.ToString(Result));
                            List<IRecycleStorageItem> RecycleItems = new List<IRecycleStorageItem>(JsonList.Count);

                            foreach (Dictionary<string, string> PropertyDic in JsonList)
                            {
                                WIN_Native_API.WIN32_FIND_DATA Data = WIN_Native_API.GetStorageItemRawData(PropertyDic["ActualPath"]);

                                IRecycleStorageItem Item = Enum.Parse<StorageItemTypes>(PropertyDic["StorageType"]) == StorageItemTypes.Folder
                                                            ? new RecycleStorageFolder(PropertyDic["ActualPath"], Data)
                                                            : new RecycleStorageFile(PropertyDic["ActualPath"], Data);

                                Item.SetRelatedData(PropertyDic["OriginPath"], DateTimeOffset.FromFileTime(Convert.ToInt64(PropertyDic["DeleteTime"])));

                                RecycleItems.Add(Item);
                            }

                            return RecycleItems;
                        }
                        else
                        {
                            if (Response.Message.TryGetValue("Error", out object ErrorMessage))
                            {
                                LogTracer.Log($"An unexpected error was threw in {nameof(GetRecycleBinItemsAsync)}, message: {ErrorMessage}");
                            }

                            return new List<IRecycleStorageItem>(0);
                        }
                    }
                    else
                    {
                        LogTracer.Log($"AppServiceResponse in {nameof(GetRecycleBinItemsAsync)} return an invalid status. Status: {Enum.GetName(typeof(AppServiceResponseStatus), Response.Status)}");
                        return new List<IRecycleStorageItem>(0);
                    }
                }
                else
                {
                    LogTracer.Log($"{nameof(GetRecycleBinItemsAsync)}: Failed to connect AppService");
                    return new List<IRecycleStorageItem>(0);
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"{nameof(GetRecycleBinItemsAsync)} throw an error");
                return new List<IRecycleStorageItem>(0);
            }
            finally
            {
                IsAnyActionExcutingInCurrentController = false;
            }
        }

        public async Task<bool> TryUnlockFileOccupy(string Path, bool ForceClose = false)
        {
            try
            {
                IsAnyActionExcutingInCurrentController = true;

                if (await ConnectRemoteAsync().ConfigureAwait(false))
                {
                    ValueSet Value = new ValueSet
                    {
                        {"ExecuteType", ExecuteType_UnlockOccupy},
                        {"ExecutePath", Path },
                        {"ForceClose", ForceClose }
                    };

                    AppServiceResponse Response = await Connection.SendMessageAsync(Value);

                    if (Response.Status == AppServiceResponseStatus.Success)
                    {
                        if (Response.Message.ContainsKey("Success"))
                        {
                            return true;
                        }
                        if (Response.Message.TryGetValue("Error_Failure", out object ErrorMessage1))
                        {
                            LogTracer.Log($"An unexpected error was threw in {nameof(TryUnlockFileOccupy)}, message: {ErrorMessage1}");
                            return false;
                        }
                        else if (Response.Message.TryGetValue("Error_NotOccupy", out object ErrorMessage2))
                        {
                            LogTracer.Log($"An unexpected error was threw in {nameof(TryUnlockFileOccupy)}, message: {ErrorMessage2}");
                            throw new UnlockException();
                        }
                        else if (Response.Message.TryGetValue("Error_NotFoundOrNotFile", out object ErrorMessage3))
                        {
                            LogTracer.Log($"An unexpected error was threw in {nameof(TryUnlockFileOccupy)}, message: {ErrorMessage3}");
                            throw new FileNotFoundException();
                        }
                        else if (Response.Message.TryGetValue("Error", out object ErrorMessage4))
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
                        LogTracer.Log($"AppServiceResponse in {nameof(TryUnlockFileOccupy)} return an invalid status. Status: {Enum.GetName(typeof(AppServiceResponseStatus), Response.Status)}");
                        throw new NoResponseException();
                    }
                }
                else
                {
                    LogTracer.Log($"{nameof(TryUnlockFileOccupy)}: Failed to connect AppService");
                    throw new NoResponseException();
                }
            }
            finally
            {
                IsAnyActionExcutingInCurrentController = false;
            }
        }

        public async Task DeleteAsync(IEnumerable<string> Source, bool PermanentDelete, ProgressChangedEventHandler ProgressHandler = null)
        {
            try
            {
                IsAnyActionExcutingInCurrentController = true;

                if (await ConnectRemoteAsync().ConfigureAwait(false))
                {
                    void PipeController_OnDataReceived(object sender, string Data)
                    {
                        ProgressHandler?.Invoke(null, new ProgressChangedEventArgs(Convert.ToInt32(Data), null));
                    }

                    if (PipeController != null)
                    {
                        PipeController.OnDataReceived += PipeController_OnDataReceived;
                    }

                    ValueSet Value = new ValueSet
                    {
                        {"ExecuteType", ExecuteType_Delete},
                        {"ExecutePath", JsonSerializer.Serialize(Source)},
                        {"PermanentDelete", PermanentDelete}
                    };

                    AppServiceResponse Response = await Connection.SendMessageAsync(Value).AsTask();

                    if (PipeController != null)
                    {
                        PipeController.OnDataReceived -= PipeController_OnDataReceived;
                    }

                    if (Response.Status == AppServiceResponseStatus.Success)
                    {
                        if (Response.Message.TryGetValue("Success", out object Record))
                        {
                            if (!PermanentDelete)
                            {
                                OperationRecorder.Current.Push(JsonSerializer.Deserialize<string[]>(Convert.ToString(Record)));
                            }
                        }
                        else if (Response.Message.TryGetValue("Error_NotFound", out object ErrorMessage1))
                        {
                            LogTracer.Log($"An unexpected error was threw in {nameof(DeleteAsync)}, message: {ErrorMessage1}");
                            throw new FileNotFoundException();
                        }
                        else if (Response.Message.TryGetValue("Error_Failure", out object ErrorMessage2))
                        {
                            LogTracer.Log($"An unexpected error was threw in {nameof(DeleteAsync)}, message: {ErrorMessage2}");
                            throw new InvalidOperationException("Fail to delete item");
                        }
                        else if (Response.Message.TryGetValue("Error_Capture", out object ErrorMessage3))
                        {
                            LogTracer.Log($"An unexpected error was threw in {nameof(DeleteAsync)}, message: {ErrorMessage3}");
                            throw new FileCaputureException();
                        }
                        else if (Response.Message.TryGetValue("Error_NoPermission", out object ErrorMessage4))
                        {
                            LogTracer.Log($"An unexpected error was threw in {nameof(DeleteAsync)}, message: {ErrorMessage4}");
                            throw new InvalidOperationException("Fail to delete item");
                        }
                        else if (Response.Message.TryGetValue("Error", out object ErrorMessage5))
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
                        LogTracer.Log($"AppServiceResponse in {nameof(DeleteAsync)} return an invalid status. Status: {Enum.GetName(typeof(AppServiceResponseStatus), Response.Status)}");
                        throw new NoResponseException();
                    }
                }
                else
                {
                    LogTracer.Log($"{nameof(DeleteAsync)}: Failed to connect AppService");
                    throw new NoResponseException();
                }
            }
            finally
            {
                IsAnyActionExcutingInCurrentController = false;
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

        public async Task MoveAsync(IEnumerable<string> Source, string DestinationPath, CollisionOptions Option = CollisionOptions.None, bool SkipOperationRecord = false, ProgressChangedEventHandler ProgressHandler = null)
        {
            if (Source == null)
            {
                throw new ArgumentNullException(nameof(Source), "Parameter could not be null");
            }

            try
            {
                IsAnyActionExcutingInCurrentController = true;

                if (await ConnectRemoteAsync())
                {
                    List<string> MessageList = new List<string>();

                    foreach (string SourcePath in Source)
                    {
                        if (await FileSystemStorageItemBase.CheckExistAsync(SourcePath))
                        {
                            MessageList.Add(SourcePath);
                        }
                        else
                        {
                            throw new FileNotFoundException();
                        }
                    }

                    void PipeController_OnDataReceived(object sender, string Data)
                    {
                        ProgressHandler?.Invoke(null, new ProgressChangedEventArgs(Convert.ToInt32(Data), null));
                    }

                    if (PipeController != null)
                    {
                        PipeController.OnDataReceived += PipeController_OnDataReceived;
                    }

                    ValueSet Value = new ValueSet
                    {
                        {"ExecuteType", ExecuteType_Move},
                        {"SourcePath", JsonSerializer.Serialize(MessageList)},
                        {"DestinationPath", DestinationPath},
                        {"CollisionOptions", Enum.GetName(typeof(CollisionOptions), Option) }
                    };

                    AppServiceResponse Response = await Connection.SendMessageAsync(Value);

                    if (PipeController != null)
                    {
                        PipeController.OnDataReceived -= PipeController_OnDataReceived;
                    }

                    if (Response.Status == AppServiceResponseStatus.Success)
                    {
                        if (Response.Message.TryGetValue("Success", out object Record))
                        {
                            if (!SkipOperationRecord)
                            {
                                OperationRecorder.Current.Push(JsonSerializer.Deserialize<string[]>(Convert.ToString(Record)));
                            }
                        }
                        else if (Response.Message.TryGetValue("Error_NotFound", out object ErrorMessage1))
                        {
                            LogTracer.Log($"An unexpected error was threw in {nameof(MoveAsync)}, message: {ErrorMessage1}");
                            throw new FileNotFoundException();
                        }
                        else if (Response.Message.TryGetValue("Error_Failure", out object ErrorMessage2))
                        {
                            LogTracer.Log($"An unexpected error was threw in {nameof(MoveAsync)}, message: {ErrorMessage2}");
                            throw new InvalidOperationException();
                        }
                        else if (Response.Message.TryGetValue("Error_Capture", out object ErrorMessage3))
                        {
                            LogTracer.Log($"An unexpected error was threw in {nameof(MoveAsync)}, message: {ErrorMessage3}");
                            throw new FileCaputureException();
                        }
                        else if (Response.Message.TryGetValue("Error_NoPermission", out object ErrorMessage4))
                        {
                            LogTracer.Log($"An unexpected error was threw in {nameof(MoveAsync)}, message: {ErrorMessage4}");
                            throw new InvalidOperationException();
                        }
                        else if (Response.Message.TryGetValue("Error", out object ErrorMessage5))
                        {
                            LogTracer.Log($"An unexpected error was threw in {nameof(MoveAsync)}, message: {ErrorMessage5}");
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
                        LogTracer.Log($"AppServiceResponse in {nameof(MoveAsync)} return an invalid status. Status: {Enum.GetName(typeof(AppServiceResponseStatus), Response.Status)}");
                        throw new NoResponseException();
                    }
                }
                else
                {
                    LogTracer.Log($"{nameof(MoveAsync)}: Failed to connect AppService");
                    throw new NoResponseException();
                }
            }
            finally
            {
                IsAnyActionExcutingInCurrentController = false;
            }
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

            return MoveAsync(new string[1] { SourcePath }, Destination, Option, SkipOperationRecord, ProgressHandler);
        }

        public async Task<bool> PasteRemoteFile(string DestinationPath)
        {
            try
            {
                IsAnyActionExcutingInCurrentController = true;

                if (await ConnectRemoteAsync())
                {
                    ValueSet Value = new ValueSet
                    {
                        {"ExecuteType", ExecuteType_PasteRemoteFile},
                        {"Path", DestinationPath}
                    };

                    AppServiceResponse Response = await Connection.SendMessageAsync(Value);

                    if (Response.Status == AppServiceResponseStatus.Success)
                    {
                        if (Response.Message.ContainsKey("Success"))
                        {
                            return true;
                        }
                        else
                        {
                            if (Response.Message.TryGetValue("Error", out object ErrorMessage))
                            {
                                LogTracer.Log($"An unexpected error was threw in {nameof(PasteRemoteFile)}, message: {ErrorMessage}");
                            }

                            return false;
                        }
                    }
                    else
                    {
                        LogTracer.Log($"AppServiceResponse in {nameof(PasteRemoteFile)} return an invalid status. Status: {Enum.GetName(typeof(AppServiceResponseStatus), Response.Status)}");
                        return false;
                    }
                }
                else
                {
                    LogTracer.Log($"{nameof(PasteRemoteFile)}: Failed to connect AppService ");
                    return false;
                }
            }
            finally
            {
                IsAnyActionExcutingInCurrentController = false;
            }
        }

        public async Task CopyAsync(IEnumerable<string> Source, string DestinationPath, CollisionOptions Option = CollisionOptions.None, bool SkipOperationRecord = false, ProgressChangedEventHandler ProgressHandler = null)
        {
            if (Source == null)
            {
                throw new ArgumentNullException(nameof(Source), "Parameter could not be null");
            }

            try
            {
                IsAnyActionExcutingInCurrentController = true;

                if (await ConnectRemoteAsync())
                {
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

                    void PipeController_OnDataReceived(object sender, string Data)
                    {
                        ProgressHandler?.Invoke(null, new ProgressChangedEventArgs(Convert.ToInt32(Data), null));
                    }

                    if (PipeController != null)
                    {
                        PipeController.OnDataReceived += PipeController_OnDataReceived;
                    }

                    ValueSet Value = new ValueSet
                    {
                        {"ExecuteType", ExecuteType_Copy},
                        {"SourcePath", JsonSerializer.Serialize(ItemList)},
                        {"DestinationPath", DestinationPath},
                        {"CollisionOptions", Enum.GetName(typeof(CollisionOptions), Option)}
                    };

                    AppServiceResponse Response = await Connection.SendMessageAsync(Value);

                    if (PipeController != null)
                    {
                        PipeController.OnDataReceived -= PipeController_OnDataReceived;
                    }

                    if (Response.Status == AppServiceResponseStatus.Success)
                    {
                        if (Response.Message.TryGetValue("Success", out object Record))
                        {
                            if (!SkipOperationRecord)
                            {
                                OperationRecorder.Current.Push(JsonSerializer.Deserialize<string[]>(Convert.ToString(Record)));
                            }
                        }
                        else if (Response.Message.TryGetValue("Error_NotFound", out object ErrorMessage1))
                        {
                            LogTracer.Log($"An unexpected error was threw in {nameof(CopyAsync)}, message: {ErrorMessage1}");
                            throw new FileNotFoundException();
                        }
                        else if (Response.Message.TryGetValue("Error_Failure", out object ErrorMessage2))
                        {
                            LogTracer.Log($"An unexpected error was threw in {nameof(CopyAsync)}, message: {ErrorMessage2}");
                            throw new InvalidOperationException();
                        }
                        else if (Response.Message.TryGetValue("Error_NoPermission", out object ErrorMessage3))
                        {
                            LogTracer.Log($"An unexpected error was threw in {nameof(CopyAsync)}, message: {ErrorMessage3}");
                            throw new InvalidOperationException();
                        }
                        else if (Response.Message.TryGetValue("Error", out object ErrorMessage4))
                        {
                            LogTracer.Log($"An unexpected error was threw in {nameof(CopyAsync)}, message: {ErrorMessage4}");
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
                        LogTracer.Log($"AppServiceResponse in {nameof(CopyAsync)} return an invalid status. Status: {Enum.GetName(typeof(AppServiceResponseStatus), Response.Status)}");
                        throw new NoResponseException();
                    }
                }
                else
                {
                    LogTracer.Log($"{nameof(CopyAsync)}: Failed to connect AppService");
                    throw new NoResponseException();
                }
            }
            finally
            {
                IsAnyActionExcutingInCurrentController = false;
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

            try
            {
                IsAnyActionExcutingInCurrentController = true;

                if (await ConnectRemoteAsync().ConfigureAwait(false))
                {
                    ValueSet Value = new ValueSet
                    {
                        {"ExecuteType", ExecuteType_Restore_RecycleItem},
                        {"ExecutePath", JsonSerializer.Serialize(OriginPathList)}
                    };

                    AppServiceResponse Response = await Connection.SendMessageAsync(Value);

                    if (Response.Status == AppServiceResponseStatus.Success)
                    {
                        if (Response.Message.TryGetValue("Restore_Result", out object Result))
                        {
                            return Convert.ToBoolean(Result);
                        }
                        else
                        {
                            if (Response.Message.TryGetValue("Error", out object ErrorMessage))
                            {
                                LogTracer.Log($"An unexpected error was threw in {nameof(RestoreItemInRecycleBinAsync)}, message: {ErrorMessage}");
                            }

                            return false;
                        }
                    }
                    else
                    {
                        LogTracer.Log($"AppServiceResponse in {nameof(RestoreItemInRecycleBinAsync)} return an invalid status. Status: {Enum.GetName(typeof(AppServiceResponseStatus), Response.Status)}");
                        return false;
                    }
                }
                else
                {
                    LogTracer.Log($"{nameof(RestoreItemInRecycleBinAsync)}: Failed to connect AppService");
                    return false;
                }
            }
            catch
            {
                return false;
            }
            finally
            {
                IsAnyActionExcutingInCurrentController = false;
            }
        }

        public async Task<bool> DeleteItemInRecycleBinAsync(string Path)
        {
            if (string.IsNullOrWhiteSpace(Path))
            {
                throw new ArgumentNullException(nameof(Path), "Parameter could not be null or empty");
            }

            try
            {
                IsAnyActionExcutingInCurrentController = true;

                if (await ConnectRemoteAsync().ConfigureAwait(false))
                {
                    ValueSet Value = new ValueSet
                    {
                        {"ExecuteType", ExecuteType_Delete_RecycleItem},
                        {"ExecutePath", Path},
                    };

                    AppServiceResponse Response = await Connection.SendMessageAsync(Value);

                    if (Response.Status == AppServiceResponseStatus.Success)
                    {
                        if (Response.Message.TryGetValue("Delete_Result", out object Result))
                        {
                            return Convert.ToBoolean(Result);
                        }
                        else
                        {
                            if (Response.Message.TryGetValue("Error", out object ErrorMessage))
                            {
                                LogTracer.Log($"An unexpected error was threw in {nameof(DeleteItemInRecycleBinAsync)}, message: {ErrorMessage}");
                            }

                            return false;
                        }
                    }
                    else
                    {
                        LogTracer.Log($"AppServiceResponse in {nameof(DeleteItemInRecycleBinAsync)} return an invalid status. Status: {Enum.GetName(typeof(AppServiceResponseStatus), Response.Status)}");
                        return false;
                    }
                }
                else
                {
                    LogTracer.Log($"{nameof(DeleteItemInRecycleBinAsync)}: Failed to connect AppService");
                    return false;
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"{nameof(DeleteItemInRecycleBinAsync)} throw an error");
                return false;
            }
            finally
            {
                IsAnyActionExcutingInCurrentController = false;
            }
        }

        public async Task<bool> EjectPortableDevice(string Path)
        {
            if (string.IsNullOrWhiteSpace(Path))
            {
                throw new ArgumentNullException(nameof(Path), "Parameter could not be null or empty");
            }

            try
            {
                IsAnyActionExcutingInCurrentController = true;

                if (await ConnectRemoteAsync().ConfigureAwait(false))
                {
                    ValueSet Value = new ValueSet
                    {
                        {"ExecuteType", ExecuteType_EjectUSB},
                        {"ExecutePath", Path},
                    };

                    AppServiceResponse Response = await Connection.SendMessageAsync(Value);

                    if (Response.Status == AppServiceResponseStatus.Success)
                    {
                        if (Response.Message.TryGetValue("EjectResult", out object Result))
                        {
                            return Convert.ToBoolean(Result);
                        }
                        else
                        {
                            if (Response.Message.TryGetValue("Error", out object ErrorMessage))
                            {
                                LogTracer.Log($"An unexpected error was threw in {nameof(EjectPortableDevice)}, message: {ErrorMessage}");
                            }

                            return false;
                        }
                    }
                    else
                    {
                        LogTracer.Log($"AppServiceResponse in {nameof(EjectPortableDevice)} return an invalid status. Status: {Enum.GetName(typeof(AppServiceResponseStatus), Response.Status)}");
                        return false;
                    }
                }
                else
                {
                    LogTracer.Log($"{nameof(EjectPortableDevice)}: Failed to connect AppService");
                    return false;
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"{nameof(EjectPortableDevice)} throw an error");
                return false;
            }
            finally
            {
                IsAnyActionExcutingInCurrentController = false;
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

                    if (PipeController != null)
                    {
                        PipeController.Dispose();
                    }

                    Application.Current.Suspending -= Current_Suspending;
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
