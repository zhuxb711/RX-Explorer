using Microsoft.Win32.SafeHandles;
using RX_Explorer.Interface;
using ShareClassLibrary;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.Storage;

namespace RX_Explorer.Class
{
    /// <summary>
    /// 用于启动具备完全权限的附加程序的控制器
    /// </summary>
    public sealed class FullTrustProcessController : IDisposable
    {
        public static ushort DynamicBackupProcessNum => 2;

        public bool IsAnyActionExcutingInCurrentController { get; private set; }

        public static bool IsAnyActionExcutingInAllControllers => AllControllerList.ToArray().Any((Controller) => Controller.IsAnyActionExcutingInCurrentController);

        public static int InUseControllersNum => AllControllerList.Count - AvailableControllers.Count;

        public static int AllControllersNum => AllControllerList.Count;

        public static int AvailableControllersNum => AvailableControllers.Count;

        private readonly static Thread DispatcherThread = new Thread(DispatcherCore)
        {
            IsBackground = true,
            Priority = ThreadPriority.Normal
        };

        private NamedPipeReadController PipeProgressReadController;

        private NamedPipeReadController PipeCommandReadController;

        private NamedPipeWriteController PipeCommandWriteController;

        private NamedPipeWriteController PipeCancellationWriteController;

        private readonly ConcurrentQueue<Command> CommandQueue = new ConcurrentQueue<Command>();

        private readonly NamedPipeCommunicationBaseController PipeCommunicationBaseController;

        private static readonly SynchronizedCollection<FullTrustProcessController> AllControllerList = new SynchronizedCollection<FullTrustProcessController>();

        private static readonly ConcurrentQueue<FullTrustProcessController> AvailableControllers = new ConcurrentQueue<FullTrustProcessController>();

        private static readonly ConcurrentQueue<TaskCompletionSource<ExclusiveUsage>> WaitingTaskQueue = new ConcurrentQueue<TaskCompletionSource<ExclusiveUsage>>();

        private static int ExpectedControllerNum;

        public static event EventHandler<bool> CurrentBusyStatus;

        private static readonly AutoResetEvent DispatcherSleepLocker = new AutoResetEvent(false);

        private static readonly SemaphoreSlim ResizeTaskLocker = new SemaphoreSlim(1, 1);

        private readonly int CurrentProcessId;

        private bool IsDisposed;

        static FullTrustProcessController()
        {
            DispatcherThread.Start();
        }

        private FullTrustProcessController()
        {
            using (Process CurrentProcess = Process.GetCurrentProcess())
            {
                CurrentProcessId = CurrentProcess.Id;
            }

            AllControllerList.Add(this);

            if (NamedPipeControllerBase.TryCreateNamedPipe(out NamedPipeCommunicationBaseController CommunicationController))
            {
                PipeCommunicationBaseController = CommunicationController;
            }
        }

        private static void DispatcherCore()
        {
            while (true)
            {
                if (WaitingTaskQueue.IsEmpty)
                {
                    DispatcherSleepLocker.WaitOne();
                }

            NEXT:
                while (WaitingTaskQueue.TryDequeue(out TaskCompletionSource<ExclusiveUsage> CompletionSource))
                {
                    while (true)
                    {
                        if (AvailableControllers.TryDequeue(out FullTrustProcessController Controller))
                        {
                            if ((Controller?.IsDisposed).GetValueOrDefault(true) || Controller.SendCommandAsync(CommandType.Test).Result == null)
                            {
                                LogTracer.Log($"Dispatcher found a controller was disposed or disconnected, trying create a new one for dispatching");

                                if (!Controller.IsDisposed)
                                {
                                    Controller.Dispose();
                                }

                                for (int Retry = 1; Retry <= 3; Retry++)
                                {
                                    if (CreateAsync().Result is FullTrustProcessController NewController)
                                    {
                                        AvailableControllers.Enqueue(NewController);
                                        break;
                                    }
                                    else
                                    {
                                        LogTracer.Log($"Dispatcher found a controller was disposed, but could not recreate a new controller. Retrying execute {nameof(CreateAsync)} in {Retry} times");
                                    }
                                }
                            }
                            else
                            {
                                CompletionSource.SetResult(new ExclusiveUsage(Controller, ExtendedExecutionController.CreateExtendedExecutionAsync().Result));
                                break;
                            }
                        }
                        else
                        {
                            for (int WaitCount = 1; AvailableControllers.IsEmpty; WaitCount++)
                            {
                                if (SpinWait.SpinUntil(() => !AvailableControllers.IsEmpty, 2000))
                                {
                                    if (WaitCount > 5)
                                    {
                                        CurrentBusyStatus?.Invoke(null, false);
                                    }

                                    break;
                                }

                                switch (WaitCount)
                                {
                                    case 5:
                                        {
                                            CurrentBusyStatus?.Invoke(null, true);
                                            break;
                                        }
                                    case 30:
                                        {
                                            CurrentBusyStatus?.Invoke(null, false);
                                            CompletionSource.TrySetException(new TimeoutException("Dispather timeout"));
                                            goto NEXT;
                                        }
                                }
                            }
                        }
                    }
                }
            }
        }

        public static async Task SetExpectedControllerNumAsync(int ExpectedNum)
        {
            await ResizeTaskLocker.WaitAsync();

            try
            {
                ExpectedControllerNum = ExpectedNum;

                if (ExpectedNum > AllControllersNum - DynamicBackupProcessNum)
                {
                    int AddCount = ExpectedNum - AllControllersNum + DynamicBackupProcessNum;

                    List<Task> ParallelList = new List<Task>(AddCount);

                    for (int Counter = 0; Counter < AddCount; Counter++)
                    {
                        ParallelList.Add(CreateAsync().ContinueWith((PreviousTask) =>
                        {
                            if (PreviousTask.Result is FullTrustProcessController NewController)
                            {
                                AvailableControllers.Enqueue(NewController);
                            }
                        }));
                    }

                    await Task.WhenAll(ParallelList);
                }
                else if (ExpectedNum < AllControllersNum - DynamicBackupProcessNum)
                {
                    int RemoveCount = AllControllersNum - DynamicBackupProcessNum - ExpectedNum;

                    for (int Counter = 0; Counter < RemoveCount; Counter++)
                    {
                        if (AvailableControllers.TryDequeue(out FullTrustProcessController RemoveController))
                        {
                            RemoveController.Dispose();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"An exception was threw in {nameof(SetExpectedControllerNumAsync)}");
            }
            finally
            {
                ResizeTaskLocker.Release();
            }
        }


        public static Task<ExclusiveUsage> GetAvailableControllerAsync()
        {
            TaskCompletionSource<ExclusiveUsage> CompletionSource = new TaskCompletionSource<ExclusiveUsage>();

            WaitingTaskQueue.Enqueue(CompletionSource);

            DispatcherSleepLocker.Set();

            return CompletionSource.Task;
        }

        private static async Task<FullTrustProcessController> CreateAsync()
        {
            FullTrustProcessController Controller = new FullTrustProcessController();

            try
            {
                await FullTrustProcessLauncher.LaunchFullTrustProcessForCurrentAppAsync();
                await Controller.ConnectRemoteAsync();
                return Controller;
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, "Could not create FullTrustProcess properly");
                Controller?.Dispose();
                return null;
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

                if ((PipeCommandWriteController?.IsConnected).GetValueOrDefault()
                     && (PipeCommandReadController?.IsConnected).GetValueOrDefault()
                     && (PipeProgressReadController?.IsConnected).GetValueOrDefault()
                     && (PipeCancellationWriteController?.IsConnected).GetValueOrDefault())
                {
                    return true;
                }

                if (await Task.Run(() => SpinWait.SpinUntil(() => (PipeCommunicationBaseController?.IsConnected).GetValueOrDefault(), 10000)))
                {
                    for (int RetryCount = 1; RetryCount <= 3; RetryCount++)
                    {
                        PipeCommandWriteController?.Dispose();
                        PipeCommandReadController?.Dispose();
                        PipeProgressReadController?.Dispose();
                        PipeCancellationWriteController?.Dispose();

                        if (PipeCommandReadController != null)
                        {
                            PipeCommandReadController.OnDataReceived -= PipeCommandReadController_OnDataReceived;
                        }

                        if (PipeProgressReadController != null)
                        {
                            PipeProgressReadController.OnDataReceived -= PipeProgressReadController_OnDataReceived;
                        }

                        Dictionary<string, string> Command = new Dictionary<string, string>
                        {
                            { "ProcessId", Convert.ToString(CurrentProcessId) }
                        };

                        if (NamedPipeControllerBase.TryCreateNamedPipe(out PipeCommandReadController))
                        {
                            Command.Add("PipeCommandReadId", PipeCommandReadController.PipeId);
                        }

                        if (NamedPipeControllerBase.TryCreateNamedPipe(out PipeProgressReadController))
                        {
                            Command.Add("PipeProgressReadId", PipeProgressReadController.PipeId);
                        }

                        if (NamedPipeControllerBase.TryCreateNamedPipe(out PipeCommandWriteController))
                        {
                            Command.Add("PipeCommandWriteId", PipeCommandWriteController.PipeId);
                        }

                        if (NamedPipeControllerBase.TryCreateNamedPipe(out PipeCancellationWriteController))
                        {
                            Command.Add("PipeCancellationWriteId", PipeCancellationWriteController.PipeId);
                        }

                        PipeCommunicationBaseController.SendData(JsonSerializer.Serialize(Command));

                        if (await Task.Run(() => SpinWait.SpinUntil(() => (PipeCommandWriteController?.IsConnected).GetValueOrDefault()
                                                                           && (PipeCommandReadController?.IsConnected).GetValueOrDefault()
                                                                           && (PipeProgressReadController?.IsConnected).GetValueOrDefault()
                                                                           && (PipeCancellationWriteController?.IsConnected).GetValueOrDefault(), 3000)))
                        {
                            PipeCommandReadController.OnDataReceived += PipeCommandReadController_OnDataReceived;
                            PipeProgressReadController.OnDataReceived += PipeProgressReadController_OnDataReceived;
                            return true;
                        }
                        else
                        {
                            LogTracer.Log($"Try connect to FullTrustProcess in {RetryCount} times");
                        }
                    }

                    LogTracer.Log("Retry 3 times and still could not connect to FullTrustProcess, disposing this instance");
                }
                else
                {
                    LogTracer.Log("CommunicationBaseController is not connected, disposing this instance");
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"An unexpected exception was threw in {nameof(ConnectRemoteAsync)}");
            }

            Dispose();

            return false;
        }

        private void PipeProgressReadController_OnDataReceived(object sender, NamedPipeDataReceivedArgs e)
        {
            if (CommandQueue.TryPeek(out Command CommandObject))
            {
                if (e.ExtraException == null)
                {
                    CommandObject.ProgressHandler?.Invoke(null, new ProgressChangedEventArgs(Convert.ToInt32(e.Data), null));
                }
            }
        }

        private void PipeCommandReadController_OnDataReceived(object sender, NamedPipeDataReceivedArgs e)
        {
            if (CommandQueue.TryDequeue(out Command CommandObject))
            {
                bool ResponseSet;

                if (e.ExtraException is Exception Ex)
                {
                    ResponseSet = CommandObject.ResultSetter.TrySetException(Ex);
                }
                else
                {
                    try
                    {
                        ResponseSet = CommandObject.ResultSetter.TrySetResult(JsonSerializer.Deserialize<IDictionary<string, string>>(e.Data));
                    }
                    catch (Exception ex)
                    {
                        ResponseSet = CommandObject.ResultSetter.TrySetException(ex);
                    }
                }

                if (!ResponseSet && !CommandObject.ResultSetter.TrySetCanceled())
                {
                    LogTracer.Log("FullTrustProcessController could not set the response properly");
                }
            }
        }

        private async Task<IDictionary<string, string>> SendCommandAsync(CommandType Type, params (string, string)[] Arguments)
        {
            IsAnyActionExcutingInCurrentController = true;

            try
            {
                if (await ConnectRemoteAsync())
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

                    CommandQueue.Enqueue(new Command(CompletionSource));
                    PipeCommandWriteController.SendData(JsonSerializer.Serialize(Command));

                    return await CompletionSource.Task;
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"{nameof(SendCommandAsync)} throw an error");
            }
            finally
            {
                IsAnyActionExcutingInCurrentController = false;
            }

            return null;
        }

        private async Task<IDictionary<string, string>> SendCommandAndReportProgressAsync(CommandType Type, ProgressChangedEventHandler ProgressHandler, params (string, string)[] Arguments)
        {
            IsAnyActionExcutingInCurrentController = true;

            try
            {
                if (await ConnectRemoteAsync())
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

                    CommandQueue.Enqueue(new Command(CompletionSource, ProgressHandler));
                    PipeCommandWriteController.SendData(JsonSerializer.Serialize(Command));

                    return await CompletionSource.Task;
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"{nameof(SendCommandAndReportProgressAsync)} throw an error");
            }
            finally
            {
                IsAnyActionExcutingInCurrentController = false;
            }

            return null;
        }

        private bool TryCancelCurrentOperation()
        {
            try
            {
                if ((PipeCancellationWriteController?.IsConnected).GetValueOrDefault())
                {
                    PipeCancellationWriteController.SendData("Cancel");
                    return true;
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"An exception was threw in {nameof(TryCancelCurrentOperation)}");
            }

            return false;
        }

        public async Task<IEnumerable<T>> OrderByNaturalStringSortAlgorithmAsync<T>(IEnumerable<T> InputList, Func<T, string> StringSelector, SortDirection Direction)
        {
            Dictionary<string, T> MapDictionary = InputList.ToDictionary((Item) => Guid.NewGuid().ToString("N"));

            if (await SendCommandAsync(CommandType.OrderByNaturalStringSortAlgorithm, ("InputList", JsonSerializer.Serialize(MapDictionary.Select((Item) => new StringNaturalAlgorithmData(Item.Key, StringSelector(Item.Value) ?? string.Empty))))) is IDictionary<string, string> Response)
            {
                if (Response.TryGetValue("Success", out string RawText))
                {
                    IEnumerable<StringNaturalAlgorithmData> SortedList = JsonSerializer.Deserialize<IEnumerable<StringNaturalAlgorithmData>>(RawText);

                    if (Direction == SortDirection.Ascending)
                    {
                        return SortedList.Select((Item) => MapDictionary[Item.UniqueId]);
                    }
                    else
                    {
                        return SortedList.Select((Item) => MapDictionary[Item.UniqueId]).Reverse();
                    }
                }
                else if (Response.TryGetValue("Error", out string ErrorMessage))
                {
                    LogTracer.Log($"An unexpected error was threw in {nameof(OrderByNaturalStringSortAlgorithmAsync)}, message: {ErrorMessage}");
                }
            }

            return InputList;
        }

        public async Task MTPReplaceWithNewFileAsync(string Path, string NewFilePath)
        {
            if (await SendCommandAsync(CommandType.MTPReplaceWithNewFile, ("Path", Path), ("NewFilePath", NewFilePath)) is IDictionary<string, string> Response)
            {
                if (Response.TryGetValue("Error", out string ErrorMessage))
                {
                    LogTracer.Log($"An unexpected error was threw in {nameof(MTPReplaceWithNewFileAsync)}, message: {ErrorMessage}");
                }
            }
        }

        public async Task<string> MTPDownloadAndGetPathAsync(string Path)
        {
            if (await SendCommandAsync(CommandType.MTPDownloadAndGetPath, ("Path", Path)) is IDictionary<string, string> Response)
            {
                if (Response.TryGetValue("Success", out string RawText))
                {
                    return RawText;
                }
                else if (Response.TryGetValue("Error", out string ErrorMessage))
                {
                    LogTracer.Log($"An unexpected error was threw in {nameof(MTPDownloadAndGetPathAsync)}, message: {ErrorMessage}");
                }
            }

            return null;
        }

        public async Task<SafeFileHandle> MTPDownloadAndGetHandleAsync(string Path, AccessMode Access, OptimizeOption Option)
        {
            if (await SendCommandAsync(CommandType.MTPDownloadAndGetHandle, ("Path", Path), ("AccessMode", Enum.GetName(typeof(AccessMode), Access)), ("OptimizeOption", Enum.GetName(typeof(OptimizeOption), Option))) is IDictionary<string, string> Response)
            {
                if (Response.TryGetValue("Success", out string HandleString))
                {
                    return new SafeFileHandle(new IntPtr(Convert.ToInt64(HandleString)), true);
                }
                else if (Response.TryGetValue("Error", out string ErrorMessage))
                {
                    LogTracer.Log($"An unexpected error was threw in {nameof(MTPDownloadAndGetHandleAsync)}, message: {ErrorMessage}");
                }
            }

            return null;
        }

        public async Task<MTPFileData> MTPCreateSubItemAsync(string Path, string Name, StorageItemTypes ItemTypes, CreateOption Option)
        {
            if (await SendCommandAsync(CommandType.MTPCreateSubItem,
                                       ("Path", Path),
                                       ("Name", Name),
                                       ("Type", ItemTypes switch
                                       {
                                           StorageItemTypes.File => Enum.GetName(typeof(CreateType), CreateType.File),
                                           StorageItemTypes.Folder => Enum.GetName(typeof(CreateType), CreateType.Folder),
                                           _ => throw new NotSupportedException()
                                       }),
                                       ("Option", Option switch
                                       {
                                           CreateOption.ReplaceExisting => Enum.GetName(typeof(CollisionOptions), CollisionOptions.OverrideOnCollision),
                                           CreateOption.OpenIfExist => Enum.GetName(typeof(CollisionOptions), CollisionOptions.None),
                                           CreateOption.GenerateUniqueName => Enum.GetName(typeof(CollisionOptions), CollisionOptions.RenameOnCollision),
                                           _ => throw new NotSupportedException()
                                       })) is IDictionary<string, string> Response)
            {
                if (Response.TryGetValue("Success", out string RawText))
                {
                    return JsonSerializer.Deserialize<MTPFileData>(RawText);
                }
                else if (Response.TryGetValue("Error", out string ErrorMessage))
                {
                    LogTracer.Log($"An unexpected error was threw in {nameof(MTPCreateSubItemAsync)}, message: {ErrorMessage}");
                }
            }

            return null;
        }

        public async Task<MTPDriveVolumnData> GetMTPDriveVolumnDataAsync(string DeviceId)
        {
            if (await SendCommandAsync(CommandType.MTPGetDriveVolumnData, ("DeviceId", DeviceId)) is IDictionary<string, string> Response)
            {
                if (Response.TryGetValue("Success", out string RawText))
                {
                    return JsonSerializer.Deserialize<MTPDriveVolumnData>(RawText);
                }
                else if (Response.TryGetValue("Error", out string ErrorMessage))
                {
                    LogTracer.Log($"An unexpected error was threw in {nameof(GetMTPDriveVolumnDataAsync)}, message: {ErrorMessage}");
                }
            }

            return new MTPDriveVolumnData();
        }

        public async Task<ulong> GetMTPFolderSizeAsync(string Path, CancellationToken CancelToken = default)
        {
            using (CancelToken.Register(() =>
            {
                if (!TryCancelCurrentOperation())
                {
                    LogTracer.Log($"Could not cancel the operation in {nameof(GetMTPFolderSizeAsync)}");
                }
            }))
            {
                if (await SendCommandAsync(CommandType.MTPGetFolderSize, ("Path", Path)) is IDictionary<string, string> Response)
                {
                    if (Response.TryGetValue("Success", out string RawText))
                    {
                        return Convert.ToUInt64(RawText);
                    }
                    else if (Response.TryGetValue("Error", out string ErrorMessage))
                    {
                        LogTracer.Log($"An unexpected error was threw in {nameof(GetMTPFolderSizeAsync)}, message: {ErrorMessage}");
                    }
                }

                return 0;
            }
        }

        public async Task<bool> MTPCheckContainersAnyItemsAsync(string Path, bool IncludeHiddenItems, bool IncludeSystemItems, BasicFilters Filter)
        {
            string ConvertFilterToText(BasicFilters Filters)
            {
                if (Filters.HasFlag(BasicFilters.File) && Filters.HasFlag(BasicFilters.Folder))
                {
                    return "All";
                }
                else if (Filters.HasFlag(BasicFilters.File))
                {
                    return "File";
                }
                else if (Filters.HasFlag(BasicFilters.Folder))
                {
                    return "Folder";
                }

                return string.Empty;
            }

            if (await SendCommandAsync(CommandType.MTPCheckContainsAnyItems, ("Path", Path), ("IncludeHiddenItems", Convert.ToString(IncludeHiddenItems)), ("IncludeSystemItems", Convert.ToString(IncludeSystemItems)), ("Filter", ConvertFilterToText(Filter))) is IDictionary<string, string> Response)
            {
                if (Response.TryGetValue("Success", out string RawText))
                {
                    return Convert.ToBoolean(RawText);
                }
                else if (Response.TryGetValue("Error", out string ErrorMessage))
                {
                    LogTracer.Log($"An unexpected error was threw in {nameof(MTPCheckContainersAnyItemsAsync)}, message: {ErrorMessage}");
                }
            }

            return false;
        }

        public async Task<bool> MTPCheckExistsAsync(string Path)
        {
            if (await SendCommandAsync(CommandType.MTPCheckExists, ("Path", Path)) is IDictionary<string, string> Response)
            {
                if (Response.TryGetValue("Success", out string RawText))
                {
                    return Convert.ToBoolean(RawText);
                }
                else if (Response.TryGetValue("Error", out string ErrorMessage))
                {
                    LogTracer.Log($"An unexpected error was threw in {nameof(MTPCheckExistsAsync)}, message: {ErrorMessage}");
                }
            }

            return false;
        }

        public async Task<MTPFileData> GetMTPItemDataAsync(string Path)
        {
            if (await SendCommandAsync(CommandType.MTPGetItem, ("Path", Path)) is IDictionary<string, string> Response)
            {
                if (Response.TryGetValue("Success", out string RawText))
                {
                    return JsonSerializer.Deserialize<MTPFileData>(RawText);
                }
                else if (Response.TryGetValue("Error", out string ErrorMessage))
                {
                    LogTracer.Log($"An unexpected error was threw in {nameof(GetMTPItemDataAsync)}, message: {ErrorMessage}");
                }
            }

            return null;
        }

        public async Task<IReadOnlyList<MTPFileData>> GetMTPChildItemsDataAsync(string Path,
                                                                                  bool IncludeHiddenItems,
                                                                                  bool IncludeSystemItems,
                                                                                  bool IncludeAllSubItems,
                                                                                  uint MaxNumLimit,
                                                                                  BasicFilters Filters,
                                                                                  CancellationToken CancelToken = default)
        {
            string ConvertFilterToText(BasicFilters Filters)
            {
                if (Filters.HasFlag(BasicFilters.File) && Filters.HasFlag(BasicFilters.Folder))
                {
                    return "All";
                }
                else if (Filters.HasFlag(BasicFilters.File))
                {
                    return "File";
                }
                else if (Filters.HasFlag(BasicFilters.Folder))
                {
                    return "Folder";
                }

                return string.Empty;
            }

            using (CancelToken.Register(() =>
            {
                if (!TryCancelCurrentOperation())
                {
                    LogTracer.Log($"Could not cancel the operation in {nameof(GetMTPChildItemsDataAsync)}");
                }
            }))
            {
                if (await SendCommandAsync(CommandType.MTPGetChildItems,
                                           ("Path", Path),
                                           ("IncludeHiddenItems", Convert.ToString(IncludeHiddenItems)),
                                           ("IncludeSystemItems", Convert.ToString(IncludeSystemItems)),
                                           ("IncludeAllSubItems", Convert.ToString(IncludeAllSubItems)),
                                           ("MaxNumLimit", Convert.ToString(MaxNumLimit)),
                                           ("Type", ConvertFilterToText(Filters))) is IDictionary<string, string> Response)
                {
                    if (Response.TryGetValue("Success", out string RawText))
                    {
                        return JsonSerializer.Deserialize<IReadOnlyList<MTPFileData>>(RawText);
                    }
                    else if (Response.TryGetValue("Error", out string ErrorMessage))
                    {
                        LogTracer.Log($"An unexpected error was threw in {nameof(GetMTPChildItemsDataAsync)}, message: {ErrorMessage}");
                    }
                }

                return new List<MTPFileData>();
            }
        }

        public async Task<string> ConvertShortPathToLongPathAsync(string Path)
        {
            if (await SendCommandAsync(CommandType.ConvertToLongPath, ("Path", Path)) is IDictionary<string, string> Response)
            {
                if (Response.TryGetValue("Success", out string LongPath))
                {
                    return LongPath;
                }
                else if (Response.TryGetValue("Error", out string ErrorMessage))
                {
                    LogTracer.Log($"An unexpected error was threw in {nameof(ConvertShortPathToLongPathAsync)}, message: {ErrorMessage}");
                }
            }

            return Path;
        }

        public async Task<string> GetFriendlyTypeNameAsync(string Extension)
        {
            if (!string.IsNullOrWhiteSpace(Extension))
            {
                if (await SendCommandAsync(CommandType.GetFriendlyTypeName, ("Extension", Extension)) is IDictionary<string, string> Response)
                {
                    if (Response.TryGetValue("Success", out string TypeText))
                    {
                        return TypeText;
                    }
                    else if (Response.TryGetValue("Error", out string ErrorMessage))
                    {
                        LogTracer.Log($"An unexpected error was threw in {nameof(GetFriendlyTypeNameAsync)}, message: {ErrorMessage}");
                    }
                }
            }

            return Extension;
        }

        public async Task<IReadOnlyList<PermissionDataPackage>> GetPermissionsAsync(string Path)
        {
            if (await SendCommandAsync(CommandType.GetPermissions, ("Path", Path)) is IDictionary<string, string> Response)
            {
                if (Response.TryGetValue("Success", out string PermissionText))
                {
                    return JsonSerializer.Deserialize<IReadOnlyList<PermissionDataPackage>>(PermissionText);
                }
                else if (Response.TryGetValue("Error", out string ErrorMessage))
                {
                    LogTracer.Log($"An unexpected error was threw in {nameof(GetPermissionsAsync)}, message: {ErrorMessage}");
                }
            }

            return new List<PermissionDataPackage>(0);
        }

        public async Task<bool> SetDriveLabelAsync(string DrivePath, string DriveLabelName)
        {
            if (await SendCommandAsync(CommandType.SetDriveLabel, ("Path", DrivePath), ("DriveLabelName", DriveLabelName)) is IDictionary<string, string> Response)
            {
                if (Response.ContainsKey("Success"))
                {
                    return true;
                }
                else if (Response.TryGetValue("Error", out string ErrorMessage))
                {
                    LogTracer.Log($"An unexpected error was threw in {nameof(SetDriveLabelAsync)}, message: {ErrorMessage}");
                }
            }

            return false;
        }

        public async Task<bool> GetDriveIndexStatusAsync(string DrivePath)
        {
            if (await SendCommandAsync(CommandType.GetDriveIndexStatus, ("Path", DrivePath)) is IDictionary<string, string> Response)
            {
                if (Response.TryGetValue("Success", out string StatusString))
                {
                    return Convert.ToBoolean(StatusString);
                }
                else
                {
                    if (Response.TryGetValue("Error", out string ErrorMessage))
                    {
                        LogTracer.Log($"An unexpected error was threw in {nameof(GetDriveIndexStatusAsync)}, message: {ErrorMessage}");
                    }
                }
            }

            return false;
        }

        public async Task SetDriveIndexStatusAsync(string DrivePath, bool AllowIndex, bool ApplyToSubItems, CancellationToken CancelToken = default)
        {
            using (CancelToken.Register(() =>
            {
                if (!TryCancelCurrentOperation())
                {
                    LogTracer.Log($"Could not cancel the operation in {nameof(SetDriveIndexStatusAsync)}");
                }
            }))
            {
                if (await SendCommandAsync(CommandType.SetDriveIndexStatus, ("Path", DrivePath), ("AllowIndex", Convert.ToString(AllowIndex)), ("ApplyToSubItems", Convert.ToString(ApplyToSubItems))) is IDictionary<string, string> Response)
                {
                    if (Response.TryGetValue("Error", out string ErrorMessage))
                    {
                        LogTracer.Log($"An unexpected error was threw in {nameof(SetDriveIndexStatusAsync)}, message: {ErrorMessage}");
                    }
                }
            }
        }

        public async Task<bool> GetDriveCompressionStatusAsync(string DrivePath)
        {
            if (await SendCommandAsync(CommandType.GetDriveCompressionStatus, ("Path", DrivePath)) is IDictionary<string, string> Response)
            {
                if (Response.TryGetValue("Success", out string StatusString))
                {
                    return Convert.ToBoolean(StatusString);
                }
                else
                {
                    if (Response.TryGetValue("Error", out string ErrorMessage))
                    {
                        LogTracer.Log($"An unexpected error was threw in {nameof(GetDriveCompressionStatusAsync)}, message: {ErrorMessage}");
                    }
                }
            }

            return false;
        }

        public async Task SetDriveCompressionStatusAsync(string DrivePath, bool IsSetCompressionStatus, bool ApplyToSubItems, CancellationToken CancelToken = default)
        {
            using (CancelToken.Register(() =>
            {
                if (!TryCancelCurrentOperation())
                {
                    LogTracer.Log($"Could not cancel the operation in {nameof(SetDriveCompressionStatusAsync)}");
                }
            }))
            {
                if (await SendCommandAsync(CommandType.SetDriveCompressionStatus, ("Path", DrivePath), ("IsSetCompressionStatus", Convert.ToString(IsSetCompressionStatus)), ("ApplyToSubItems", Convert.ToString(ApplyToSubItems))) is IDictionary<string, string> Response)
                {
                    if (Response.TryGetValue("Error", out string ErrorMessage))
                    {
                        LogTracer.Log($"An unexpected error was threw in {nameof(SetDriveCompressionStatusAsync)}, message: {ErrorMessage}");
                    }
                }
            }
        }

        public async Task<IReadOnlyList<Encoding>> GetAllEncodingsAsync()
        {
            if (await SendCommandAsync(CommandType.GetAllEncodings) is IDictionary<string, string> Response)
            {
                if (Response.TryGetValue("Success", out string EncodingsString))
                {
                    return JsonSerializer.Deserialize<IEnumerable<int>>(EncodingsString).Select((CodePage) => Encoding.GetEncoding(CodePage))
                                                                                        .Where((Encoding) => !string.IsNullOrWhiteSpace(Encoding.EncodingName))
                                                                                        .OrderByFastStringSortAlgorithm((Encoding) => Encoding.EncodingName, SortDirection.Ascending)
                                                                                        .ToList();
                }
                else
                {
                    if (Response.TryGetValue("Error", out string ErrorMessage))
                    {
                        LogTracer.Log($"An unexpected error was threw in {nameof(GetAllEncodingsAsync)}, message: {ErrorMessage}");
                    }
                }
            }

            return new List<Encoding>(0);
        }

        public async Task<Encoding> DetectEncodingAsync(string Path)
        {
            if (await SendCommandAsync(CommandType.DetectEncoding, ("Path", Path)) is IDictionary<string, string> Response)
            {
                if (Response.TryGetValue("Success", out string EncodingString))
                {
                    return Encoding.GetEncoding(Convert.ToInt32(EncodingString));
                }
                else
                {
                    if (Response.TryGetValue("Error", out string ErrorMessage))
                    {
                        LogTracer.Log($"An unexpected error was threw in {nameof(DetectEncodingAsync)}, message: {ErrorMessage}");
                    }
                }
            }

            return null;
        }

        public async Task<IReadOnlyDictionary<string, string>> GetPropertiesAsync(string Path, IEnumerable<string> Properties)
        {
            if (await SendCommandAsync(CommandType.GetProperties, ("Path", Path), ("Properties", JsonSerializer.Serialize(Properties))) is IDictionary<string, string> Response)
            {
                if (Response.TryGetValue("Success", out string PropertiesString))
                {
                    return JsonSerializer.Deserialize<IReadOnlyDictionary<string, string>>(PropertiesString);
                }
                else
                {
                    if (Response.TryGetValue("Error", out string ErrorMessage))
                    {
                        LogTracer.Log($"An unexpected error was threw in {nameof(GetPropertiesAsync)}, message: {ErrorMessage}");
                    }
                }
            }

            return new Dictionary<string, string>(Properties.Select((Item) => new KeyValuePair<string, string>(Item, string.Empty)));
        }

        public async Task<bool> SetTaskBarProgressAsync(int ProgressValue)
        {
            if (await SendCommandAsync(CommandType.SetTaskBarProgress, ("ProgressValue", Convert.ToString(ProgressValue))) is IDictionary<string, string> Response)
            {
                if (Response.ContainsKey("Success"))
                {
                    return true;
                }
                else
                {
                    if (Response.TryGetValue("Error", out string ErrorMessage))
                    {
                        LogTracer.Log($"An unexpected error was threw in {nameof(SetTaskBarProgressAsync)}, message: {ErrorMessage}");
                    }
                }
            }

            return false;
        }

        public async Task<IReadOnlyDictionary<string, string>> MapToUNCPathAsync(IEnumerable<string> PathList)
        {
            if (await SendCommandAsync(CommandType.MapToUNCPath, ("PathList", JsonSerializer.Serialize(PathList))) is IDictionary<string, string> Response)
            {
                if (Response.TryGetValue("Success", out string MapString))
                {
                    return JsonSerializer.Deserialize<IReadOnlyDictionary<string, string>>(MapString);
                }
                else
                {
                    if (Response.TryGetValue("Error", out string ErrorMessage))
                    {
                        LogTracer.Log($"An unexpected error was threw in {nameof(MapToUNCPathAsync)}, message: {ErrorMessage}");
                    }
                }
            }

            return new Dictionary<string, string>(0);
        }

        public async Task<SafeFileHandle> GetNativeHandleAsync(string Path, AccessMode Access, OptimizeOption Option)
        {
            if (await SendCommandAsync(CommandType.GetNativeHandle, ("ExecutePath", Path), ("AccessMode", Enum.GetName(typeof(AccessMode), Access)), ("OptimizeOption", Enum.GetName(typeof(OptimizeOption), Option))) is IDictionary<string, string> Response)
            {
                if (Response.TryGetValue("Success", out string HandleString))
                {
                    return new SafeFileHandle(new IntPtr(Convert.ToInt64(HandleString)), true);
                }
                else
                {
                    if (Response.TryGetValue("Error", out string ErrorMessage))
                    {
                        LogTracer.Log($"An unexpected error was threw in {nameof(GetNativeHandleAsync)}, message: {ErrorMessage}");
                    }
                }
            }

            return new SafeFileHandle(IntPtr.Zero, true);
        }

        public async Task<SafeFileHandle> GetDirectoryMonitorHandleAsync(string Path)
        {
            if (await SendCommandAsync(CommandType.GetDirectoryMonitorHandle, ("ExecutePath", Path)) is IDictionary<string, string> Response)
            {
                if (Response.TryGetValue("Success", out string HandleString))
                {
                    return new SafeFileHandle(new IntPtr(Convert.ToInt64(HandleString)), true);
                }
                else
                {
                    if (Response.TryGetValue("Error", out string ErrorMessage))
                    {
                        LogTracer.Log($"An unexpected error was threw in {nameof(GetNativeHandleAsync)}, message: {ErrorMessage}");
                    }
                }
            }

            return new SafeFileHandle(IntPtr.Zero, true);
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
                }
            }

            return string.Empty;
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
                }
            }

            return string.Empty;
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
                }
            }

            return string.Empty;
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
                }
            }

            return Array.Empty<byte>();
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
                }
            }

            return string.Empty;
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
                }
            }

            return false;
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
                }
            }

            return false;
        }

        public async Task SetFileAttributeAsync(string Path, params KeyValuePair<ModifyAttributeAction, System.IO.FileAttributes>[] Attribute)
        {
            if (await SendCommandAsync(CommandType.SetFileAttribute, ("ExecutePath", Path), ("Attributes", JsonSerializer.Serialize(Attribute))) is IDictionary<string, string> Response)
            {
                if (Response.TryGetValue("Error", out string ErrorMessage))
                {
                    LogTracer.Log($"An unexpected error was threw in {nameof(SetFileAttributeAsync)}, message: {ErrorMessage}");
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
                }
            }

            return false;
        }

        public async Task<IReadOnlyList<FileSystemStorageItemBase>> SearchByEverythingAsync(string BaseLocation, string SearchWord, bool SearchAsRegex, bool IgnoreCase)
        {
            if (await SendCommandAsync(CommandType.SearchByEverything, ("BaseLocation", BaseLocation), ("SearchWord", SearchWord), ("SearchAsRegex", Convert.ToString(SearchAsRegex)), ("IgnoreCase", Convert.ToString(IgnoreCase))) is IDictionary<string, string> Response)
            {
                if (Response.TryGetValue("Success", out string Result))
                {
                    string[] SearchResult = JsonSerializer.Deserialize<string[]>(Result);

                    if (SearchResult.Length > 0)
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
                }
            }

            return new List<FileSystemStorageItemBase>(0);
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
                }
            }

            return false;
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
                }
            }

            return false;
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
                }
            }

            return false;
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
                }
            }

            return null;
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
                }
            }

            return Array.Empty<InstalledApplication>();
        }


        public async Task<HiddenFileData> GetHiddenItemDataAsync(string Path)
        {
            if (await SendCommandAsync(CommandType.GetHiddenItemData, ("ExecutePath", Path)) is IDictionary<string, string> Response)
            {
                if (Response.TryGetValue("Success", out string Result))
                {
                    return JsonSerializer.Deserialize<HiddenFileData>(Result);
                }
                else
                {
                    if (Response.TryGetValue("Error", out string ErrorMessage))
                    {
                        LogTracer.Log($"An unexpected error was threw in {nameof(GetHiddenItemDataAsync)}, message: {ErrorMessage}");
                    }
                }
            }

            return null;
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
                        LogTracer.Log($"An unexpected error was threw in {nameof(GetThumbnailAsync)}, message: {ErrorMessage}");
                    }
                }
            }

            return Array.Empty<byte>();
        }

        public async Task<IReadOnlyList<ContextMenuItem>> GetContextMenuItemsAsync(string[] PathArray, bool IncludeExtensionItem = false)
        {
            if (PathArray.All((Path) => !string.IsNullOrWhiteSpace(Path)))
            {
                if (await SendCommandAsync(CommandType.GetContextMenuItems, ("ExecutePath", JsonSerializer.Serialize(PathArray)), ("IncludeExtensionItem", Convert.ToString(IncludeExtensionItem))) is IDictionary<string, string> Response)
                {
                    if (Response.TryGetValue("Success", out string Result))
                    {
                        return JsonSerializer.Deserialize<ContextMenuPackage[]>(Result).OrderByFastStringSortAlgorithm((Item) => Item.Name, SortDirection.Ascending).Select((Item) => new ContextMenuItem(Item)).ToList();
                    }
                    else
                    {
                        if (Response.TryGetValue("Error", out string ErrorMessage))
                        {
                            LogTracer.Log($"An unexpected error was threw in {nameof(GetContextMenuItemsAsync)}, message: {ErrorMessage}");
                        }
                    }
                }
            }

            return new List<ContextMenuItem>(0);
        }

        public async Task<bool> InvokeContextMenuItemAsync(ContextMenuPackage Package)
        {
            if (Package?.Clone() is ContextMenuPackage ClonePackage)
            {
                ClonePackage.IconData = Array.Empty<byte>();

                if (await SendCommandAsync(CommandType.InvokeContextMenuItem, ("DataPackage", JsonSerializer.Serialize(ClonePackage))) is IDictionary<string, string> Response)
                {
                    if (Response.ContainsKey("Success"))
                    {
                        return true;
                    }
                    else if (Response.TryGetValue("Error", out string ErrorMessage))
                    {
                        LogTracer.Log($"An unexpected error was threw in {nameof(GetContextMenuItemsAsync)}, message: {ErrorMessage}");
                    }
                }
            }

            return false;
        }

        public async Task<bool> CreateLinkAsync(LinkFileData Package)
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
                }
            }

            return false;
        }

        public async Task UpdateLinkAsync(LinkFileData Package)
        {
            if (await SendCommandAsync(CommandType.UpdateLink, ("DataPackage", JsonSerializer.Serialize(Package))) is IDictionary<string, string> Response)
            {
                if (Response.TryGetValue("Error", out string ErrorMessage))
                {
                    LogTracer.Log($"An unexpected error was threw in {nameof(UpdateLinkAsync)}, message: {ErrorMessage}");
                }
            }
        }

        public async Task UpdateUrlAsync(UrlFileData Package)
        {
            if (await SendCommandAsync(CommandType.UpdateUrl, ("DataPackage", JsonSerializer.Serialize(Package))) is IDictionary<string, string> Response)
            {
                if (Response.TryGetValue("Error", out string ErrorMessage))
                {
                    LogTracer.Log($"An unexpected error was threw in {nameof(UpdateUrlAsync)}, message: {ErrorMessage}");
                }
            }
        }

        public async Task<IReadOnlyList<VariableDataPackage>> GetVariableSuggestionAsync(string PartialVariable)
        {
            if (await SendCommandAsync(CommandType.GetVariablePathSuggestion, ("PartialVariable", PartialVariable)) is IDictionary<string, string> Response)
            {
                if (Response.TryGetValue("Success", out string Result))
                {
                    return JsonSerializer.Deserialize<List<VariableDataPackage>>(Result);
                }
                else if (Response.TryGetValue("Error", out var ErrorMessage))
                {
                    LogTracer.Log($"An unexpected error was threw in {nameof(GetVariableSuggestionAsync)}, message: {ErrorMessage}");
                }
            }

            return new List<VariableDataPackage>(0);
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
                }
            }

            return string.Empty;
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

        public async Task<LinkFileData> GetLinkDataAsync(string Path)
        {
            if (await SendCommandAsync(CommandType.GetLinkData, ("ExecutePath", Path)) is IDictionary<string, string> Response)
            {
                if (Response.TryGetValue("Success", out string Result))
                {
                    return JsonSerializer.Deserialize<LinkFileData>(Result);
                }
                else
                {
                    if (Response.TryGetValue("Error", out string ErrorMessage))
                    {
                        LogTracer.Log($"An unexpected error was threw in {nameof(GetLinkDataAsync)}, message: {ErrorMessage}");
                    }
                }
            }

            return null;
        }

        public async Task<UrlFileData> GetUrlDataAsync(string Path)
        {
            if (await SendCommandAsync(CommandType.GetUrlData, ("ExecutePath", Path)) is IDictionary<string, string> Response)
            {
                if (Response.TryGetValue("Success", out string Result))
                {
                    return JsonSerializer.Deserialize<UrlFileData>(Result);
                }
                else
                {
                    if (Response.TryGetValue("Error", out string ErrorMessage))
                    {
                        LogTracer.Log($"An unexpected error was threw in {nameof(GetUrlDataAsync)}, message: {ErrorMessage}");
                    }
                }
            }

            return null;
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
                }
            }

            return false;
        }

        public async Task<bool> InterceptDesktopFolderAsync()
        {
            if (await SendCommandAsync(CommandType.InterceptFolder) is IDictionary<string, string> Response)
            {
                if (Response.ContainsKey("Success"))
                {
                    return true;
                }
                else
                {
                    if (Response.TryGetValue("Error", out string ErrorMessage))
                    {
                        LogTracer.Log($"An unexpected error was threw in {nameof(InterceptDesktopFolderAsync)}, message: {ErrorMessage}");
                    }
                }
            }

            return false;
        }

        public async Task<bool> RestoreFolderInterceptionAsync()
        {
            if (await SendCommandAsync(CommandType.RestoreFolderInterception) is IDictionary<string, string> Response)
            {
                if (Response.ContainsKey("Success"))
                {
                    return true;
                }
                else
                {
                    if (Response.TryGetValue("Error", out string ErrorMessage))
                    {
                        LogTracer.Log($"An unexpected error was threw in {nameof(RestoreFolderInterceptionAsync)}, message: {ErrorMessage}");
                    }
                }
            }

            return false;
        }


        public async Task<bool> RestoreWindowsPlusEInterceptionAsync()
        {
            if (await SendCommandAsync(CommandType.RestoreWinEInterception) is IDictionary<string, string> Response)
            {
                if (Response.ContainsKey("Success"))
                {
                    return true;
                }
                else
                {
                    if (Response.TryGetValue("Error", out string ErrorMessage))
                    {
                        LogTracer.Log($"An unexpected error was threw in {nameof(RestoreWindowsPlusEInterceptionAsync)}, message: {ErrorMessage}");
                    }
                }
            }

            return false;
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
                if (Response.ContainsKey("Success"))
                {
                    return true;
                }
                else if (Response.TryGetValue("Error", out string ErrorMessage1))
                {
                    LogTracer.Log($"An unexpected error was threw in {nameof(RunAsync)}, message: {ErrorMessage1}");
                }
                else if (Response.TryGetValue("Error_NoPermission", out string ErrorMessage2))
                {
                    LogTracer.Log($"An unexpected error was threw in {nameof(RunAsync)}, message: {ErrorMessage2}");
                }
            }

            return false;
        }

        public async Task ToggleQuicklookAsync(string Path)
        {
            if (await SendCommandAsync(CommandType.ToggleQuicklook, ("ExecutePath", Path)) is IDictionary<string, string> Response)
            {
                if (Response.TryGetValue("Error", out string ErrorMessage))
                {
                    LogTracer.Log($"An unexpected error was threw in {nameof(ToggleQuicklookAsync)}, message: {ErrorMessage}");
                }
            }
        }

        public async Task SwitchQuicklookAsync(string Path)
        {
            if (await SendCommandAsync(CommandType.SwitchQuicklook, ("ExecutePath", Path)) is IDictionary<string, string> Response)
            {
                if (Response.TryGetValue("Error", out string ErrorMessage))
                {
                    LogTracer.Log($"An unexpected error was threw in {nameof(SwitchQuicklookAsync)}, message: {ErrorMessage}");
                }
            }
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
                }
            }

            return false;
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
                }
            }

            return string.Empty;
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
                }
            }

            return new List<AssociationPackage>(0);
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
                }
            }

            return false;
        }

        public async Task<IReadOnlyList<IRecycleStorageItem>> GetRecycleBinItemsAsync()
        {
            if (await SendCommandAsync(CommandType.Get_RecycleBinItems) is IDictionary<string, string> Response)
            {
                if (Response.TryGetValue("RecycleBinItems_Json_Result", out string Result))
                {
                    IReadOnlyList<Dictionary<string, string>> JsonList = JsonSerializer.Deserialize<IReadOnlyList<Dictionary<string, string>>>(Result);

                    ConcurrentBag<IRecycleStorageItem> ResultBag = new ConcurrentBag<IRecycleStorageItem>();

                    Parallel.ForEach(JsonList, (PropertyDic) =>
                    {
                        try
                        {
                            NativeFileData Data = NativeWin32API.GetStorageItemRawData(PropertyDic["ActualPath"]);

                            if (Data.IsDataValid)
                            {
                                ResultBag.Add(Enum.Parse<StorageItemTypes>(PropertyDic["StorageType"]) == StorageItemTypes.Folder
                                                        ? new RecycleStorageFolder(Data, PropertyDic["OriginPath"], DateTimeOffset.FromFileTime(Convert.ToInt64(PropertyDic["DeleteTime"])))
                                                        : new RecycleStorageFile(Data, PropertyDic["OriginPath"], DateTimeOffset.FromFileTime(Convert.ToInt64(PropertyDic["DeleteTime"]))));

                            }
                            else
                            {
                                ResultBag.Add(Enum.Parse<StorageItemTypes>(PropertyDic["StorageType"]) == StorageItemTypes.Folder
                                                        ? new RecycleStorageFolder(StorageFolder.GetFolderFromPathAsync(PropertyDic["ActualPath"]).AsTask().Result, PropertyDic["OriginPath"], DateTimeOffset.FromFileTime(Convert.ToInt64(PropertyDic["DeleteTime"])))
                                                        : new RecycleStorageFile(StorageFile.GetFileFromPathAsync(PropertyDic["ActualPath"]).AsTask().Result, PropertyDic["OriginPath"], DateTimeOffset.FromFileTime(Convert.ToInt64(PropertyDic["DeleteTime"]))));
                            }
                        }
                        catch (Exception ex)
                        {
                            LogTracer.Log(ex, $"Could not load the recycle item, path: {PropertyDic["ActualPath"]}");
                        }
                    });

                    return ResultBag.ToList();
                }
                else
                {
                    if (Response.TryGetValue("Error", out string ErrorMessage))
                    {
                        LogTracer.Log($"An unexpected error was threw in {nameof(GetRecycleBinItemsAsync)}, message: {ErrorMessage}");
                    }
                }
            }

            return new List<IRecycleStorageItem>(0);
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

        public async Task DeleteAsync(IEnumerable<string> Source,
                                      bool PermanentDelete,
                                      bool SkipOperationRecord = false,
                                      CancellationToken CancelToken = default,
                                      ProgressChangedEventHandler ProgressHandler = null)
        {
            using (CancelToken.Register(() =>
            {
                if (!TryCancelCurrentOperation())
                {
                    LogTracer.Log($"Could not cancel the operation in {nameof(DeleteAsync)}");
                }
            }))
            {
                if (await SendCommandAndReportProgressAsync(CommandType.Delete,
                                                            ProgressHandler,
                                                            ("ExecutePath", JsonSerializer.Serialize(Source)),
                                                            ("PermanentDelete", Convert.ToString(PermanentDelete))) is IDictionary<string, string> Response)
                {
                    if (Response.TryGetValue("Success", out string Record))
                    {
                        if (!PermanentDelete && !SkipOperationRecord)
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
                        throw new Exception("Unknown reason");
                    }
                    else if (Response.ContainsKey("Error_Cancelled"))
                    {
                        LogTracer.Log($"Operation was cancelled successfully in {nameof(DeleteAsync)}");
                        throw new OperationCanceledException("Operation was cancelled successfully");
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
        }

        public Task DeleteAsync(string Source,
                                bool PermanentDelete,
                                bool SkipOperationRecord = false,
                                CancellationToken CancelToken = default,
                                ProgressChangedEventHandler ProgressHandler = null)
        {
            if (Source == null)
            {
                throw new ArgumentNullException(nameof(Source), "Parameter could not be null");
            }

            return DeleteAsync(new string[1] { Source }, PermanentDelete, SkipOperationRecord, CancelToken, ProgressHandler);
        }

        public async Task MoveAsync(Dictionary<string, string> Source,
                                    string DestinationPath,
                                    CollisionOptions Option = CollisionOptions.None,
                                    bool SkipOperationRecord = false,
                                    CancellationToken CancelToken = default,
                                    ProgressChangedEventHandler ProgressHandler = null)
        {
            if (Source == null)
            {
                throw new ArgumentNullException(nameof(Source), "Parameter could not be null");
            }

            Dictionary<string, string> MessageList = new Dictionary<string, string>();

            foreach (KeyValuePair<string, string> SourcePair in Source)
            {
                if (await FileSystemStorageItemBase.CheckExistsAsync(SourcePair.Key))
                {
                    MessageList.Add(SourcePair.Key, SourcePair.Value);
                }
                else
                {
                    throw new FileNotFoundException();
                }
            }

            using (CancelToken.Register(() =>
            {
                if (!TryCancelCurrentOperation())
                {
                    LogTracer.Log($"Could not cancel the operation in {nameof(MoveAsync)}");
                }
            }))
            {
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
                        throw new OperationCanceledException("Operation was cancelled");
                    }
                    else if (Response.TryGetValue("Error", out string ErrorMessage6))
                    {
                        LogTracer.Log($"An unexpected error was threw in {nameof(MoveAsync)}, message: {ErrorMessage6}");
                        throw new Exception();
                    }
                    else if (Response.ContainsKey("Error_Cancelled"))
                    {
                        LogTracer.Log($"Operation was cancelled successfully in {nameof(DeleteAsync)}");
                        throw new OperationCanceledException("Operation was cancelled");
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
        }

        public Task MoveAsync(IEnumerable<string> Source,
                              string DestinationPath,
                              CollisionOptions Option = CollisionOptions.None,
                              bool SkipOperationRecord = false,
                              CancellationToken CancelToken = default,
                              ProgressChangedEventHandler ProgressHandler = null)
        {
            Dictionary<string, string> Dic = new Dictionary<string, string>();

            foreach (string Path in Source)
            {
                Dic.Add(Path, null);
            }

            return MoveAsync(Dic, DestinationPath, Option, SkipOperationRecord, CancelToken, ProgressHandler);
        }

        public Task MoveAsync(string SourcePath,
                              string Destination,
                              CollisionOptions Option = CollisionOptions.None,
                              bool SkipOperationRecord = false,
                              CancellationToken CancelToken = default,
                              ProgressChangedEventHandler ProgressHandler = null)
        {
            if (string.IsNullOrEmpty(SourcePath))
            {
                throw new ArgumentNullException(nameof(SourcePath), "Parameter could not be null");
            }

            if (string.IsNullOrEmpty(Destination))
            {
                throw new ArgumentNullException(nameof(Destination), "Parameter could not be null");
            }

            return MoveAsync(new string[] { SourcePath }, Destination, Option, SkipOperationRecord, CancelToken, ProgressHandler);
        }

        public async Task CopyAsync(IEnumerable<string> Source,
                                    string DestinationPath,
                                    CollisionOptions Option = CollisionOptions.None,
                                    bool SkipOperationRecord = false,
                                    CancellationToken CancelToken = default,
                                    ProgressChangedEventHandler ProgressHandler = null)
        {
            if (Source == null)
            {
                throw new ArgumentNullException(nameof(Source), "Parameter could not be null");
            }

            List<string> ItemList = new List<string>();

            foreach (string SourcePath in Source)
            {
                if (await FileSystemStorageItemBase.CheckExistsAsync(SourcePath))
                {
                    ItemList.Add(SourcePath);
                }
                else
                {
                    throw new FileNotFoundException();
                }
            }

            using (CancelToken.Register(() =>
            {
                if (!TryCancelCurrentOperation())
                {
                    LogTracer.Log($"Could not cancel the operation in {nameof(CopyAsync)}");
                }
            }))
            {
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
                        throw new OperationCanceledException("Operation was cancelled");
                    }
                    else if (Response.TryGetValue("Error", out string ErrorMessage5))
                    {
                        LogTracer.Log($"An unexpected error was threw in {nameof(CopyAsync)}, message: {ErrorMessage5}");
                        throw new Exception();
                    }
                    else if (Response.ContainsKey("Error_Cancelled"))
                    {
                        LogTracer.Log($"Operation was cancelled successfully in {nameof(DeleteAsync)}");
                        throw new OperationCanceledException("Operation was cancelled");
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
        }

        public Task CopyAsync(string SourcePath,
                              string Destination,
                              CollisionOptions Option = CollisionOptions.None,
                              bool SkipOperationRecord = false,
                              CancellationToken CancelToken = default,
                              ProgressChangedEventHandler ProgressHandler = null)
        {
            if (string.IsNullOrEmpty(SourcePath))
            {
                throw new ArgumentNullException(nameof(SourcePath), "Parameter could not be null");
            }

            if (string.IsNullOrEmpty(Destination))
            {
                throw new ArgumentNullException(nameof(Destination), "Parameter could not be null");
            }

            return CopyAsync(new string[1] { SourcePath }, Destination, Option, SkipOperationRecord, CancelToken, ProgressHandler);
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
                }
            }

            return false;
        }

        public async Task<bool> PasteRemoteFile(string DestinationPath, ProgressChangedEventHandler ProgressHandler = null)
        {
            if (await SendCommandAndReportProgressAsync(CommandType.PasteRemoteFile, ProgressHandler, ("Path", DestinationPath)) is IDictionary<string, string> Response)
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
                }
            }

            return false;
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
                }
            }

            return false;
        }

        public async Task<bool> EjectPortableDevice(string Path)
        {
            if (!string.IsNullOrWhiteSpace(Path))
            {
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
                    }
                }
            }

            return false;
        }

        public void Dispose()
        {
            if (!IsDisposed)
            {
                IsDisposed = true;

                GC.SuppressFinalize(this);

                try
                {
                    PipeCommandReadController?.Dispose();
                    PipeCommandWriteController?.Dispose();
                    PipeProgressReadController?.Dispose();
                    PipeCancellationWriteController?.Dispose();
                    PipeCommunicationBaseController?.Dispose();

                    CommandQueue.Clear();

                    if (PipeCommandReadController != null)
                    {
                        PipeCommandReadController.OnDataReceived -= PipeCommandReadController_OnDataReceived;
                    }

                    if (PipeProgressReadController != null)
                    {
                        PipeProgressReadController.OnDataReceived -= PipeProgressReadController_OnDataReceived;
                    }
                }
                catch (Exception ex)
                {
                    LogTracer.Log(ex);
                }
                finally
                {
                    AllControllerList.Remove(this);
                }
            }
        }

        ~FullTrustProcessController()
        {
            Dispose();
        }

        private sealed class Command
        {
            public ProgressChangedEventHandler ProgressHandler { get; }

            public TaskCompletionSource<IDictionary<string, string>> ResultSetter { get; }

            public Command(TaskCompletionSource<IDictionary<string, string>> ResultSetter)
            {
                this.ResultSetter = ResultSetter;
            }

            public Command(TaskCompletionSource<IDictionary<string, string>> ResultSetter, ProgressChangedEventHandler ProgressHandler) : this(ResultSetter)
            {
                this.ProgressHandler = ProgressHandler;
            }
        }

        public sealed class ExclusiveUsage : IDisposable
        {
            public FullTrustProcessController Controller { get; }

            private readonly ExtendedExecutionController ExtExecution;

            private static readonly object DisposeSyncRoot = new object();

            private bool IsDisposed;

            public ExclusiveUsage(FullTrustProcessController Controller, ExtendedExecutionController ExtExecution)
            {
                this.Controller = Controller;
                this.ExtExecution = ExtExecution;
            }

            public void Dispose()
            {
                if (!IsDisposed)
                {
                    IsDisposed = true;

                    GC.SuppressFinalize(this);

                    ExtExecution?.Dispose();

                    lock (DisposeSyncRoot)
                    {
                        if (ExpectedControllerNum < AllControllersNum - DynamicBackupProcessNum)
                        {
                            Controller.Dispose();
                        }
                        else
                        {
                            AvailableControllers.Enqueue(Controller);
                        }
                    }
                }
            }

            ~ExclusiveUsage()
            {
                Dispose();
            }
        }
    }
}
