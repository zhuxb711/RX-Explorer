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
using Windows.UI.Xaml.Controls;

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

        private const string ExecuteType_Get_Associate = "Execute_Get_Associate";

        private const string ExecuteType_Get_RecycleBinItems = "Execute_Get_RecycleBinItems";

        private const string ExeuteType_RequestCreateNewPipe = "Execute_RequestCreateNewPipe";

        private const string ExecuteType_InterceptWinE = "Execute_Intercept_Win_E";

        private const string ExecuteType_RestoreWinE = "Execute_Restore_Win_E";

        private const string ExecuteType_GetLnkData = "Execute_GetLnkData";

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

        private const string ExecuteType_PasteRemoteFile = "Paste_Remote_File";

        private const string ExecuteType_Test_Connection = "Execute_Test_Connection";

        private const string ExecuteType_GetContextMenuItems = "Execute_GetContextMenuItems";

        private const string ExecuteType_InvokeContextMenuItem = "Execute_InvokeContextMenuItem";

        private const string ExecuteType_CheckIfEverythingAvailable = "Execute_CheckIfEverythingAvailable";

        private const string ExecuteType_SearchByEverything = "Execute_SearchByEverything";

        private const string ExecuteType_GetHiddenItemInfo = "Execute_GetHiddenItemInfo";

        private const string ExecuteType_GetMIMEContentType = "Execute_GetMIMEContentType";

        private const string ExecuteType_GetAllInstalledApplication = "Execute_GetAllInstalledApplication";

        private const string ExecuteType_CheckPackageFamilyNameExist = "Execute_CheckPackageFamilyNameExist";

        private const string ExecuteType_GetInstalledApplication = "Execute_GetInstalledApplication";

        private const string ExecuteType_LaunchUWPLnkFile = "Execute_LaunchUWPLnkFile";

        private readonly static Thread DispatcherThread = new Thread(DispatcherMethod)
        {
            IsBackground = true,
            Priority = ThreadPriority.Normal
        };

        private static readonly AutoResetEvent DispatcherSleepLocker = new AutoResetEvent(false);

        private const ushort DynamicBackupProcessNum = 1;

        private readonly int CurrentProcessId;

        private bool IsConnected;

        private AppServiceConnection Connection;

        private bool IsDisposed;

        public bool IsAnyActionExcutingInCurrentController { get; private set; }

        public static bool IsAnyActionExcutingInAllController
        {
            get
            {
                return AvailableControllerQueue.Count < CurrentRunningControllerNum;
            }
        }

        private PipeLineController PipeController;

        private static readonly ConcurrentQueue<FullTrustProcessController> AvailableControllerQueue = new ConcurrentQueue<FullTrustProcessController>();

        private static readonly ConcurrentQueue<TaskCompletionSource<ExclusiveUsage>> WaitingTaskQueue = new ConcurrentQueue<TaskCompletionSource<ExclusiveUsage>>();

        private static volatile int CurrentRunningControllerNum;

        private static event EventHandler<FullTrustProcessController> ExclusiveDisposed;

        public static event EventHandler<bool> CurrentBusyStatus;

        static FullTrustProcessController()
        {
            DispatcherThread.Start();
            ExclusiveDisposed += FullTrustProcessController_ExclusiveDisposed;
        }

        private FullTrustProcessController()
        {
            Interlocked.Increment(ref CurrentRunningControllerNum);

            PipeController = new PipeLineController(this);

            Application.Current.Suspending += Current_Suspending;

            using (Process CurrentProcess = Process.GetCurrentProcess())
            {
                CurrentProcessId = CurrentProcess.Id;
            }
        }

        private void Current_Suspending(object sender, SuspendingEventArgs e)
        {
            LogTracer.Log("RX-Explorer enter suspend state, dispose this instance");
            Dispose();
        }

        private static async void FullTrustProcessController_ExclusiveDisposed(object sender, FullTrustProcessController e)
        {
            if (e.IsDisposed)
            {
                FullTrustProcessController Controller = await CreateAsync().ConfigureAwait(true);
                AvailableControllerQueue.Enqueue(Controller);
            }
            else
            {
                AvailableControllerQueue.Enqueue(e);
            }
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
                        if (AvailableControllerQueue.TryDequeue(out FullTrustProcessController Controller))
                        {
                            if (Controller.IsDisposed)
                            {
                                CompletionSource.SetResult(new ExclusiveUsage(CreateAsync().GetAwaiter().GetResult()));
                            }
                            else
                            {
                                CompletionSource.SetResult(new ExclusiveUsage(Controller));
                            }

                            break;
                        }
                        else
                        {
                            if (CurrentRunningControllerNum > 0)
                            {
                                if (!SpinWait.SpinUntil(() => !AvailableControllerQueue.IsEmpty, 5000))
                                {
                                    CurrentBusyStatus?.Invoke(null, true);

                                    SpinWait.SpinUntil(() => !AvailableControllerQueue.IsEmpty);

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
            _ = Task.Run(() =>
            {
                ResizeController(RequestedTarget);
            });
        }

        private static void ResizeController(int RequestedTarget)
        {
            try
            {
                RequestedTarget += DynamicBackupProcessNum;

                while (CurrentRunningControllerNum > RequestedTarget)
                {
                    if (AvailableControllerQueue.TryDequeue(out FullTrustProcessController Controller))
                    {
                        Controller.Dispose();
                    }
                    else
                    {
                        if (!SpinWait.SpinUntil(() => !AvailableControllerQueue.IsEmpty, 2000))
                        {
                            break;
                        }
                    }
                }

                while (CurrentRunningControllerNum < RequestedTarget)
                {
                    AvailableControllerQueue.Enqueue(CreateAsync().GetAwaiter().GetResult());
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
            FullTrustProcessController Controller = new FullTrustProcessController();
            await FullTrustProcessLauncher.LaunchFullTrustProcessForCurrentAppAsync();
            await Controller.ConnectRemoteAsync().ConfigureAwait(false);
            return Controller;
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
                if (Connection == null || !IsConnected)
                {
                    if (Connection != null)
                    {
                        Connection.RequestReceived -= Connection_RequestReceived;
                        Connection.Dispose();
                        Connection = null;
                    }

                    Connection = new AppServiceConnection
                    {
                        AppServiceName = "CommunicateService",
                        PackageFamilyName = Package.Current.Id.FamilyName
                    };

                    Connection.RequestReceived += Connection_RequestReceived;
                    Connection.ServiceClosed += Connection_ServiceClosed;

                    if ((await Connection.OpenAsync()) == AppServiceConnectionStatus.Success)
                    {
                        //Do not remove this delay, leave some time for "Identity" call from AppService
                        await Task.Delay(500).ConfigureAwait(false);
                    }
                    else
                    {
                        return IsConnected = false;
                    }
                }

                for (int Count = 0; Count < 3; Count++)
                {
                    AppServiceResponse Response = await Connection.SendMessageAsync(new ValueSet { { "ExecuteType", ExecuteType_Test_Connection }, { "ProcessId", CurrentProcessId } });

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
                                LogTracer.Log($"Connect to FullTrustProcess failed, reason: \"{Error}\". Retrying...in {Count} times");
                            }

                            await Task.Delay(500).ConfigureAwait(false);
                        }
                    }
                    else
                    {
                        return IsConnected = false;
                    }
                }

                LogTracer.Log("Connect to FullTrustProcess failed after retrying 3 times. Dispose this instance");

                Dispose();

                return IsConnected = false;
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"An unexpected error was threw in {nameof(ConnectRemoteAsync)}");
                return IsConnected = false;
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

                if (await ConnectRemoteAsync().ConfigureAwait(true))
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

        public async Task<bool> CheckIfEverythingIsAvailableAsync()
        {
            try
            {
                IsAnyActionExcutingInCurrentController = true;

                if (await ConnectRemoteAsync().ConfigureAwait(true))
                {
                    ValueSet Value = new ValueSet
                    {
                        {"ExecuteType", ExecuteType_CheckIfEverythingAvailable}
                    };

                    AppServiceResponse Response = await Connection.SendMessageAsync(Value);

                    if (Response.Status == AppServiceResponseStatus.Success)
                    {
                        if (Response.Message.TryGetValue("Success", out object IsAvailable))
                        {
                            return Convert.ToBoolean(IsAvailable);
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

        public async Task<List<FileSystemStorageItemBase>> SearchByEverythingAsync(string BaseLocation, string SearchWord, bool SearchAsRegex = false, bool IgnoreCase = true, uint MaxCount = 500)
        {
            try
            {
                IsAnyActionExcutingInCurrentController = true;

                if (await ConnectRemoteAsync().ConfigureAwait(true))
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

        public async Task<bool> LaunchUWPLnkAsync(string PackageFamilyName)
        {
            try
            {
                IsAnyActionExcutingInCurrentController = true;

                if (await ConnectRemoteAsync().ConfigureAwait(true))
                {
                    ValueSet Value = new ValueSet
                    {
                        {"ExecuteType", ExecuteType_LaunchUWPLnkFile},
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
                                LogTracer.Log($"An unexpected error was threw in {nameof(LaunchUWPLnkAsync)}, message: {ErrorMessage}");
                            }

                            return false;
                        }
                    }
                    else
                    {
                        LogTracer.Log($"AppServiceResponse in {nameof(LaunchUWPLnkAsync)} return an invalid status. Status: {Enum.GetName(typeof(AppServiceResponseStatus), Response.Status)}");
                        return false;
                    }
                }
                else
                {
                    LogTracer.Log($"{nameof(LaunchUWPLnkAsync)}: Failed to connect AppService ");
                    return false;
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"{ nameof(LaunchUWPLnkAsync)} throw an error");
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

                if (await ConnectRemoteAsync().ConfigureAwait(true))
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

                if (await ConnectRemoteAsync().ConfigureAwait(true))
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

                            return await InstalledApplication.CreateAsync(Pack).ConfigureAwait(true);
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


        public async Task<InstalledApplication[]> GetAllInstalledApplicationAsync()
        {
            try
            {
                IsAnyActionExcutingInCurrentController = true;

                if (await ConnectRemoteAsync().ConfigureAwait(true))
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
                                PackageList.Add(await InstalledApplication.CreateAsync(Pack).ConfigureAwait(true));
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
                    if (await ConnectRemoteAsync().ConfigureAwait(true))
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

        public async Task<List<ContextMenuItem>> GetContextMenuItemsAsync(string Path, bool IncludeExtensionItem = false)
        {
            try
            {
                IsAnyActionExcutingInCurrentController = true;

                if (Path.Any())
                {
                    if (await ConnectRemoteAsync().ConfigureAwait(true))
                    {
                        ValueSet Value = new ValueSet
                        {
                            {"ExecuteType", ExecuteType_GetContextMenuItems},
                            {"ExecutePath", Path},
                            {"IncludeExtensionItem", IncludeExtensionItem }
                        };

                        AppServiceResponse Response = await Connection.SendMessageAsync(Value);

                        if (Response.Status == AppServiceResponseStatus.Success)
                        {
                            if (Response.Message.TryGetValue("Success", out object Result))
                            {
                                return JsonSerializer.Deserialize<ContextMenuPackage[]>(Convert.ToString(Result)).Select((Item) => new ContextMenuItem(Item, Path)).ToList();
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

        public async Task InvokeContextMenuItemAsync(ContextMenuItem Item)
        {
            if (Item == null)
            {
                throw new ArgumentNullException(nameof(Item), "Argument could not be null");
            }

            try
            {
                IsAnyActionExcutingInCurrentController = true;

                if (await ConnectRemoteAsync().ConfigureAwait(true))
                {
                    ValueSet Value = new ValueSet
                    {
                        {"ExecuteType", ExecuteType_InvokeContextMenuItem},
                        {"ExecutePath", Item.BelongTo },
                        {"InvokeId", Item.Id},
                        {"InvokeVerb", Item.Verb }
                    };

                    AppServiceResponse Response = await Connection.SendMessageAsync(Value);

                    if (Response.Status == AppServiceResponseStatus.Success)
                    {
                        if (Response.Message.TryGetValue("Error", out object ErrorMessage))
                        {
                            LogTracer.Log($"An unexpected error was threw in {nameof(GetContextMenuItemsAsync)}, message: {ErrorMessage}");
                        }
                    }
                    else
                    {
                        LogTracer.Log($"AppServiceResponse in {nameof(GetContextMenuItemsAsync)} return an invalid status. Status: {Enum.GetName(typeof(AppServiceResponseStatus), Response.Status)}");
                    }
                }
                else
                {
                    LogTracer.Log($"{nameof(GetContextMenuItemsAsync)}: Failed to connect AppService ");
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"{ nameof(GetContextMenuItemsAsync)} throw an error");
            }
            finally
            {
                IsAnyActionExcutingInCurrentController = false;
            }
        }

        public async Task<bool> CreateLinkAsync(string LinkPath, string LinkTarget, string LinkDesc, params string[] LinkArgument)
        {
            try
            {
                IsAnyActionExcutingInCurrentController = true;

                if (await ConnectRemoteAsync().ConfigureAwait(false))
                {
                    ValueSet Value = new ValueSet
                    {
                        {"ExecuteType", ExecuteType_CreateLink},
                        {"DataPackage", JsonSerializer.Serialize(new LinkDataPackage(LinkPath, LinkTarget, LinkDesc, false, null, LinkArgument)) }
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

        public async Task<string> RenameAsync(string Path, string DesireName)
        {
            try
            {
                IsAnyActionExcutingInCurrentController = true;

                if (await ConnectRemoteAsync().ConfigureAwait(false))
                {
                    ValueSet Value = new ValueSet
                    {
                        {"ExecuteType", ExecuteType_Rename},
                        {"ExecutePath",Path },
                        {"DesireName",DesireName}
                    };

                    AppServiceResponse Response = await Connection.SendMessageAsync(Value);

                    if (Response.Status == AppServiceResponseStatus.Success)
                    {
                        if(Response.Message.TryGetValue("Success", out object NewName))
                        {
                            return Convert.ToString(NewName);
                        }
                        else if (Response.Message.TryGetValue("Error_Occupied", out object ErrorMessage1))
                        {
                            LogTracer.Log($"An unexpected error was threw in {nameof(RenameAsync)}, message: {ErrorMessage1}");

                            throw new FileLoadException();
                        }
                        else if (Response.Message.TryGetValue("Error_Failure", out object ErrorMessage2))
                        {
                            LogTracer.Log($"An unexpected error was threw in {nameof(RenameAsync)}, message: {ErrorMessage2}");

                            throw new InvalidOperationException();
                        }
                        else if (Response.Message.TryGetValue("Error", out object ErrorMessage3))
                        {
                            LogTracer.Log($"An unexpected error was threw in {nameof(RenameAsync)}, message: {ErrorMessage3}");

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

        public async Task RequestCreateNewPipeLineAsync(Guid CurrentProcessID)
        {
            try
            {
                IsAnyActionExcutingInCurrentController = true;

                if (await ConnectRemoteAsync().ConfigureAwait(false))
                {
                    ValueSet Value = new ValueSet
                    {
                        {"ExecuteType", ExeuteType_RequestCreateNewPipe},
                        {"Guid",CurrentProcessID.ToString() },
                    };

                    await Connection.SendMessageAsync(Value);
                }
                else
                {
                    LogTracer.Log($"{nameof(RequestCreateNewPipeLineAsync)}: Failed to connect AppService");
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"{nameof(RequestCreateNewPipeLineAsync)} throw an error");
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
        public async Task RunAsync(string Path, bool RunAsAdmin = false, bool CreateNoWindow = false, bool ShouldWaitForExit = false, params string[] Parameters)
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
                        {"ExecuteShouldWaitForExit", ShouldWaitForExit}
                    };

                    AppServiceResponse Response = await Connection.SendMessageAsync(Value);

                    if (Response.Status == AppServiceResponseStatus.Success)
                    {
                        if (Response.Message.TryGetValue("Error_Failure", out object ErrorMessage1))
                        {
                            LogTracer.Log($"An unexpected error was threw in {nameof(RunAsync)}, message: {ErrorMessage1}");
                            throw new InvalidOperationException();
                        }
                        else if (Response.Message.TryGetValue("Error", out object ErrorMessage2))
                        {
                            LogTracer.Log($"An unexpected error was threw in {nameof(RunAsync)}, message: {ErrorMessage2}");
                            throw new InvalidOperationException();
                        }
                    }
                    else
                    {
                        LogTracer.Log($"AppServiceResponse in {nameof(RunAsync)} return an invalid status. Status: {Enum.GetName(typeof(AppServiceResponseStatus), Response.Status)}");
                    }
                }
                else
                {
                    LogTracer.Log($"{nameof(RunAsync)}: Failed to connect AppService");
                }
            }
            catch (InvalidOperationException)
            {
                throw;
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"{nameof(RunAsync)} throw an error");
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

        public async Task<List<AssociationPackage>> GetAssociateFromPathAsync(string Path)
        {
            try
            {
                IsAnyActionExcutingInCurrentController = true;

                if (await ConnectRemoteAsync().ConfigureAwait(false))
                {
                    ValueSet Value = new ValueSet
                    {
                        {"ExecuteType", ExecuteType_Get_Associate},
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
                                LogTracer.Log($"An unexpected error was threw in {nameof(GetAssociateFromPathAsync)}, message: {ErrorMessage}");
                            }

                            return new List<AssociationPackage>(0);
                        }
                    }
                    else
                    {
                        LogTracer.Log($"AppServiceResponse in {nameof(GetAssociateFromPathAsync)} return an invalid status. Status: {Enum.GetName(typeof(AppServiceResponseStatus), Response.Status)}");
                        return new List<AssociationPackage>(0);
                    }
                }
                else
                {
                    LogTracer.Log($"{nameof(GetAssociateFromPathAsync)}: Failed to connect AppService");
                    return new List<AssociationPackage>(0);
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"{nameof(GetAssociateFromPathAsync)} throw an error");
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

        public async Task<List<IRecycleStorageItem>> GetRecycleBinItemsAsync()
        {
            try
            {
                IsAnyActionExcutingInCurrentController = true;

                if (await ConnectRemoteAsync().ConfigureAwait(true))
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
                                if (Enum.Parse<StorageItemTypes>(PropertyDic["StorageType"]) == StorageItemTypes.Folder)
                                {
                                    RecycleItems.Add(new RecycleStorageFolder(PropertyDic["ActualPath"], PropertyDic["OriginPath"], DateTimeOffset.FromFileTime(Convert.ToInt64(PropertyDic["DeleteTime"]))));
                                }
                                else
                                {
                                    RecycleItems.Add(new RecycleStorageFile(PropertyDic["ActualPath"], PropertyDic["OriginPath"], DateTimeOffset.FromFileTime(Convert.ToInt64(PropertyDic["DeleteTime"]))));
                                }
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

        public async Task<bool> TryUnlockFileOccupy(string Path)
        {
            try
            {
                IsAnyActionExcutingInCurrentController = true;

                if (await ConnectRemoteAsync().ConfigureAwait(false))
                {
                    ValueSet Value = new ValueSet
                    {
                        {"ExecuteType", ExecuteType_UnlockOccupy},
                        {"ExecutePath", Path }
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

        public async Task DeleteAsync(IEnumerable<string> Source, bool PermanentDelete, ProgressChangedEventHandler ProgressHandler = null, bool IsUndoOperation = false)
        {
            try
            {
                IsAnyActionExcutingInCurrentController = true;

                if (await ConnectRemoteAsync().ConfigureAwait(false))
                {
                    Task ProgressTask;

                    if (await PipeController.CreateNewNamedPipeAsync().ConfigureAwait(true))
                    {
                        ProgressTask = PipeController.ListenPipeMessageAsync(ProgressHandler);
                    }
                    else
                    {
                        ProgressTask = Task.CompletedTask;
                    }

                    ValueSet Value = new ValueSet
                    {
                        {"ExecuteType", ExecuteType_Delete},
                        {"ExecutePath", JsonSerializer.Serialize(Source)},
                        {"PermanentDelete", PermanentDelete},
                        {"Guid", PipeController.GUID.ToString() },
                        {"Undo", IsUndoOperation }
                    };

                    Task<AppServiceResponse> MessageTask = Connection.SendMessageAsync(Value).AsTask();

                    await Task.WhenAll(MessageTask, ProgressTask).ConfigureAwait(true);

                    if (MessageTask.Result.Status == AppServiceResponseStatus.Success)
                    {
                        if (MessageTask.Result.Message.ContainsKey("Success"))
                        {
                            if (MessageTask.Result.Message.TryGetValue("OperationRecord", out object value))
                            {
                                OperationRecorder.Current.Push(JsonSerializer.Deserialize<List<string>>(Convert.ToString(value)));
                            }
                        }
                        else if (MessageTask.Result.Message.TryGetValue("Error_NotFound", out object ErrorMessage1))
                        {
                            LogTracer.Log($"An unexpected error was threw in {nameof(DeleteAsync)}, message: {ErrorMessage1}");
                            throw new FileNotFoundException();
                        }
                        else if (MessageTask.Result.Message.TryGetValue("Error_Failure", out object ErrorMessage2))
                        {
                            LogTracer.Log($"An unexpected error was threw in {nameof(DeleteAsync)}, message: {ErrorMessage2}");
                            throw new InvalidOperationException("Fail to delete item");
                        }
                        else if (MessageTask.Result.Message.TryGetValue("Error_Capture", out object ErrorMessage3))
                        {
                            LogTracer.Log($"An unexpected error was threw in {nameof(DeleteAsync)}, message: {ErrorMessage3}");
                            throw new FileCaputureException();
                        }
                        else if (MessageTask.Result.Message.TryGetValue("Error", out object ErrorMessage4))
                        {
                            LogTracer.Log($"An unexpected error was threw in {nameof(DeleteAsync)}, message: {ErrorMessage4}");
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
                        LogTracer.Log($"AppServiceResponse in {nameof(DeleteAsync)} return an invalid status. Status: {Enum.GetName(typeof(AppServiceResponseStatus), MessageTask.Result.Status)}");
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

        public Task DeleteAsync(string Source, bool PermanentDelete, ProgressChangedEventHandler ProgressHandler = null, bool IsUndoOperation = false)
        {
            if (Source == null)
            {
                throw new ArgumentNullException(nameof(Source), "Parameter could not be null");
            }

            return DeleteAsync(new string[1] { Source }, PermanentDelete, ProgressHandler, IsUndoOperation);
        }

        public async Task MoveAsync(IEnumerable<string> Source, string DestinationPath, ProgressChangedEventHandler ProgressHandler = null, bool IsUndoOperation = false)
        {
            if (Source == null)
            {
                throw new ArgumentNullException(nameof(Source), "Parameter could not be null");
            }

            try
            {
                IsAnyActionExcutingInCurrentController = true;

                if (await ConnectRemoteAsync().ConfigureAwait(true))
                {
                    List<KeyValuePair<string, string>> MessageList = new List<KeyValuePair<string, string>>();

                    foreach (string SourcePath in Source)
                    {
                        if (await FileSystemStorageItemBase.OpenAsync(SourcePath).ConfigureAwait(true) is FileSystemStorageItemBase Item)
                        {
                            switch (Item)
                            {
                                case FileSystemStorageFile:
                                    {
                                        MessageList.Add(new KeyValuePair<string, string>(SourcePath, string.Empty));

                                        break;
                                    }
                                case FileSystemStorageFolder:
                                    {
                                        string TargetPath = Path.Combine(DestinationPath, Path.GetFileName(SourcePath));

                                        if (await FileSystemStorageItemBase.CheckExistAsync(TargetPath).ConfigureAwait(true))
                                        {
                                            QueueContentDialog Dialog = new QueueContentDialog
                                            {
                                                Title = Globalization.GetString("Common_Dialog_WarningTitle"),
                                                Content = $"{Globalization.GetString("QueueDialog_FolderRepeat_Content")} {Path.GetFileName(SourcePath)}",
                                                PrimaryButtonText = Globalization.GetString("QueueDialog_FolderRepeat_PrimaryButton"),
                                                CloseButtonText = Globalization.GetString("QueueDialog_FolderRepeat_CloseButton")
                                            };

                                            if (await Dialog.ShowAsync().ConfigureAwait(false) != ContentDialogResult.Primary)
                                            {
                                                if (await FileSystemStorageItemBase.CreateAsync(TargetPath, StorageItemTypes.Folder, CreateOption.GenerateUniqueName).ConfigureAwait(true) is FileSystemStorageItemBase NewFolder)
                                                {
                                                    MessageList.Add(new KeyValuePair<string, string>(SourcePath, Path.GetFileName(NewFolder.Path)));
                                                }
                                                else
                                                {
                                                    throw new Exception($"Could not create a folder on \"{TargetPath}\"");
                                                }
                                            }
                                            else
                                            {
                                                MessageList.Add(new KeyValuePair<string, string>(SourcePath, string.Empty));
                                            }
                                        }
                                        else
                                        {
                                            MessageList.Add(new KeyValuePair<string, string>(SourcePath, string.Empty));
                                        }

                                        break;
                                    }
                                default:
                                    {
                                        throw new FileNotFoundException();
                                    }
                            }
                        }
                        else
                        {
                            throw new FileNotFoundException();
                        }
                    }

                    Task ProgressTask;

                    if (await PipeController.CreateNewNamedPipeAsync().ConfigureAwait(true))
                    {
                        ProgressTask = PipeController.ListenPipeMessageAsync(ProgressHandler);
                    }
                    else
                    {
                        ProgressTask = Task.CompletedTask;
                    }

                    ValueSet Value = new ValueSet
                    {
                        {"ExecuteType", ExecuteType_Move},
                        {"SourcePath", JsonSerializer.Serialize(MessageList)},
                        {"DestinationPath", DestinationPath},
                        {"Guid", PipeController.GUID.ToString() },
                        {"Undo", IsUndoOperation }
                    };

                    Task<AppServiceResponse> MessageTask = Connection.SendMessageAsync(Value).AsTask();

                    await Task.WhenAll(MessageTask, ProgressTask).ConfigureAwait(true);

                    if (MessageTask.Result.Status == AppServiceResponseStatus.Success)
                    {
                        if (MessageTask.Result.Message.ContainsKey("Success"))
                        {
                            if (MessageTask.Result.Message.TryGetValue("OperationRecord", out object value))
                            {
                                OperationRecorder.Current.Push(JsonSerializer.Deserialize<List<string>>(Convert.ToString(value)));
                            }
                        }
                        else if (MessageTask.Result.Message.TryGetValue("Error_NotFound", out object ErrorMessage1))
                        {
                            LogTracer.Log($"An unexpected error was threw in {nameof(MoveAsync)}, message: {ErrorMessage1}");
                            throw new FileNotFoundException();
                        }
                        else if (MessageTask.Result.Message.TryGetValue("Error_Failure", out object ErrorMessage2))
                        {
                            LogTracer.Log($"An unexpected error was threw in {nameof(MoveAsync)}, message: {ErrorMessage2}");
                            throw new InvalidOperationException();
                        }
                        else if (MessageTask.Result.Message.TryGetValue("Error_Capture", out object ErrorMessage3))
                        {
                            LogTracer.Log($"An unexpected error was threw in {nameof(MoveAsync)}, message: {ErrorMessage3}");
                            throw new FileCaputureException();
                        }
                        else if (MessageTask.Result.Message.TryGetValue("Error", out object ErrorMessage4))
                        {
                            LogTracer.Log($"An unexpected error was threw in {nameof(MoveAsync)}, message: {ErrorMessage4}");
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
                        LogTracer.Log($"AppServiceResponse in {nameof(MoveAsync)} return an invalid status. Status: {Enum.GetName(typeof(AppServiceResponseStatus), MessageTask.Result.Status)}");
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

        public Task MoveAsync(string SourcePath, string Destination, ProgressChangedEventHandler ProgressHandler = null, bool IsUndoOperation = false)
        {
            if (string.IsNullOrEmpty(SourcePath))
            {
                throw new ArgumentNullException(nameof(SourcePath), "Parameter could not be null");
            }

            if (string.IsNullOrEmpty(Destination))
            {
                throw new ArgumentNullException(nameof(Destination), "Parameter could not be null");
            }

            return MoveAsync(new string[1] { SourcePath }, Destination, ProgressHandler, IsUndoOperation);
        }

        public async Task<bool> PasteRemoteFile(string DestinationPath)
        {
            try
            {
                IsAnyActionExcutingInCurrentController = true;

                if (await ConnectRemoteAsync().ConfigureAwait(true))
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

        public async Task CopyAsync(IEnumerable<string> Source, string DestinationPath, ProgressChangedEventHandler ProgressHandler = null, bool IsUndoOperation = false)
        {
            if (Source == null)
            {
                throw new ArgumentNullException(nameof(Source), "Parameter could not be null");
            }

            try
            {
                IsAnyActionExcutingInCurrentController = true;

                if (await ConnectRemoteAsync().ConfigureAwait(true))
                {
                    List<KeyValuePair<string, string>> MessageList = new List<KeyValuePair<string, string>>();

                    foreach (string SourcePath in Source)
                    {
                        if (await FileSystemStorageItemBase.OpenAsync(SourcePath).ConfigureAwait(true) is FileSystemStorageItemBase Item)
                        {
                            switch (Item)
                            {
                                case FileSystemStorageFile:
                                    {
                                        MessageList.Add(new KeyValuePair<string, string>(SourcePath, string.Empty));

                                        break;
                                    }
                                case FileSystemStorageFolder:
                                    {
                                        if (Path.GetDirectoryName(SourcePath) != DestinationPath)
                                        {
                                            string TargetPath = Path.Combine(DestinationPath, Path.GetFileName(SourcePath));

                                            if (await FileSystemStorageItemBase.CheckExistAsync(TargetPath).ConfigureAwait(true))
                                            {
                                                QueueContentDialog Dialog = new QueueContentDialog
                                                {
                                                    Title = Globalization.GetString("Common_Dialog_WarningTitle"),
                                                    Content = $"{Globalization.GetString("QueueDialog_FolderRepeat_Content")} {Path.GetFileName(SourcePath)}",
                                                    PrimaryButtonText = Globalization.GetString("QueueDialog_FolderRepeat_PrimaryButton"),
                                                    CloseButtonText = Globalization.GetString("QueueDialog_FolderRepeat_CloseButton")
                                                };

                                                if (await Dialog.ShowAsync().ConfigureAwait(false) != ContentDialogResult.Primary)
                                                {
                                                    if (await FileSystemStorageItemBase.CreateAsync(TargetPath, StorageItemTypes.Folder, CreateOption.GenerateUniqueName).ConfigureAwait(true) is FileSystemStorageItemBase NewFolder)
                                                    {
                                                        MessageList.Add(new KeyValuePair<string, string>(SourcePath, Path.GetFileName(NewFolder.Path)));
                                                    }
                                                    else
                                                    {
                                                        throw new Exception($"Could not create a folder on \"{TargetPath}\"");
                                                    }
                                                }
                                                else
                                                {
                                                    MessageList.Add(new KeyValuePair<string, string>(SourcePath, string.Empty));
                                                }
                                            }
                                            else
                                            {
                                                MessageList.Add(new KeyValuePair<string, string>(SourcePath, string.Empty));
                                            }
                                        }
                                        else
                                        {
                                            MessageList.Add(new KeyValuePair<string, string>(SourcePath, string.Empty));
                                        }

                                        break;
                                    }
                                default:
                                    {
                                        throw new FileNotFoundException();
                                    }
                            }
                        }
                        else
                        {
                            throw new FileNotFoundException();
                        }
                    }

                    Task ProgressTask;

                    if (await PipeController.CreateNewNamedPipeAsync().ConfigureAwait(true))
                    {
                        ProgressTask = PipeController.ListenPipeMessageAsync(ProgressHandler);
                    }
                    else
                    {
                        ProgressTask = Task.CompletedTask;
                    }

                    ValueSet Value = new ValueSet
                    {
                        {"ExecuteType", ExecuteType_Copy},
                        {"SourcePath", JsonSerializer.Serialize(MessageList)},
                        {"DestinationPath", DestinationPath},
                        {"Guid", PipeController.GUID.ToString() },
                        {"Undo", IsUndoOperation }
                    };

                    Task<AppServiceResponse> MessageTask = Connection.SendMessageAsync(Value).AsTask();

                    await Task.WhenAll(MessageTask, ProgressTask).ConfigureAwait(true);

                    if (MessageTask.Result.Status == AppServiceResponseStatus.Success)
                    {
                        if (MessageTask.Result.Message.ContainsKey("Success"))
                        {
                            if (MessageTask.Result.Message.TryGetValue("OperationRecord", out object value))
                            {
                                OperationRecorder.Current.Push(JsonSerializer.Deserialize<List<string>>(Convert.ToString(value)));
                            }
                        }
                        else if (MessageTask.Result.Message.TryGetValue("Error_NotFound", out object ErrorMessage1))
                        {
                            LogTracer.Log($"An unexpected error was threw in {nameof(CopyAsync)}, message: {ErrorMessage1}");
                            throw new FileNotFoundException();
                        }
                        else if (MessageTask.Result.Message.TryGetValue("Error_Failure", out object ErrorMessage2))
                        {
                            LogTracer.Log($"An unexpected error was threw in {nameof(CopyAsync)}, message: {ErrorMessage2}");
                            throw new InvalidOperationException();
                        }
                        else if (MessageTask.Result.Message.TryGetValue("Error", out object ErrorMessage3))
                        {
                            LogTracer.Log($"An unexpected error was threw in {nameof(CopyAsync)}, message: {ErrorMessage3}");
                            throw new InvalidOperationException();
                        }
                        else
                        {
                            LogTracer.Log($"An unexpected error was threw in {nameof(CopyAsync)}");
                            throw new Exception("Unknown reason");
                        }
                    }
                    else
                    {
                        LogTracer.Log($"AppServiceResponse in {nameof(CopyAsync)} return an invalid status. Status: {Enum.GetName(typeof(AppServiceResponseStatus), MessageTask.Result.Status)}");
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

        public Task CopyAsync(string SourcePath, string Destination, ProgressChangedEventHandler ProgressHandler = null, bool IsUndoOperation = false)
        {
            if (string.IsNullOrEmpty(SourcePath))
            {
                throw new ArgumentNullException(nameof(SourcePath), "Parameter could not be null");
            }

            if (string.IsNullOrEmpty(Destination))
            {
                throw new ArgumentNullException(nameof(Destination), "Parameter could not be null");
            }

            return CopyAsync(new string[1] { SourcePath }, Destination, ProgressHandler, IsUndoOperation);
        }

        public async Task<bool> RestoreItemInRecycleBinAsync(string OriginPath)
        {
            if (string.IsNullOrWhiteSpace(OriginPath))
            {
                throw new ArgumentNullException(nameof(OriginPath), "Parameter could not be null or empty");
            }

            try
            {
                IsAnyActionExcutingInCurrentController = true;

                if (await ConnectRemoteAsync().ConfigureAwait(false))
                {
                    ValueSet Value = new ValueSet
                    {
                        {"ExecuteType", ExecuteType_Restore_RecycleItem},
                        {"ExecutePath", OriginPath}
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
                IsDisposed = true;
                IsConnected = false;

                GC.SuppressFinalize(this);

                Interlocked.Decrement(ref CurrentRunningControllerNum);

                Application.Current.Suspending -= Current_Suspending;

                if (Connection != null)
                {
                    Connection.RequestReceived -= Connection_RequestReceived;
                    Connection.ServiceClosed -= Connection_ServiceClosed;

                    Connection.Dispose();
                    Connection = null;
                }

                if (PipeController != null)
                {
                    PipeController.Dispose();
                    PipeController = null;
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

            public ExclusiveUsage(FullTrustProcessController Controller)
            {
                this.Controller = Controller;
            }

            public void Dispose()
            {
                ExclusiveDisposed?.Invoke(this, Controller);
                Controller = null;
            }
        }
    }
}
