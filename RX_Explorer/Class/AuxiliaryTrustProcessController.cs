using ConcurrentPriorityQueue.Core;
using Microsoft.Win32.SafeHandles;
using SharedLibrary;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.Storage;
using Windows.Storage.Streams;
using FileAttributes = System.IO.FileAttributes;

namespace RX_Explorer.Class
{
    /// <summary>
    /// 用于启动具备完全权限的附加程序的控制器
    /// </summary>
    public sealed class AuxiliaryTrustProcessController : IDisposable
    {
        public bool IsAnyCommandExecutingInCurrentController => CurrentControllerExecutingCommandNum > 0;

        public bool IsConnected => (PipeCommandWriteController?.IsConnected).GetValueOrDefault()
                             && (PipeCommandReadController?.IsConnected).GetValueOrDefault()
                             && (PipeProgressReadController?.IsConnected).GetValueOrDefault()
                             && (PipeCancellationWriteController?.IsConnected).GetValueOrDefault();

        public static ushort DynamicBackupProcessNum => 2;

        public static bool IsAnyCommandExecutingInAllControllers => AllControllerCollection.ToArray().Any((Controller) => Controller.IsAnyCommandExecutingInCurrentController);

        public static int InUseControllersNum => AllControllerCollection.Count - AvailableControllerCollection.Count;

        public static int AllControllersNum => AllControllerCollection.Count;

        public static int AvailableControllersNum => AvailableControllerCollection.Count;

        private readonly static Thread DispatcherThread = new Thread(DispatcherCore)
        {
            IsBackground = true,
            Priority = ThreadPriority.Normal
        };

        private SafeProcessHandle AuxiliaryTrustProcessHandle;

        private RegisteredWaitHandle RegisteredAuxiliaryTrustProcessWaitHandle;

        private NamedPipeReadController PipeProgressReadController;

        private NamedPipeReadController PipeCommandReadController;

        private NamedPipeWriteController PipeCommandWriteController;

        private NamedPipeWriteController PipeCancellationWriteController;

        private NamedPipeAuxiliaryCommunicationBaseController PipeCommunicationBaseController;

        private readonly ConcurrentQueue<InternalCommandQueueItem> CommandQueue = new ConcurrentQueue<InternalCommandQueueItem>();

        private static readonly SynchronizedCollection<AuxiliaryTrustProcessController> AllControllerCollection = new SynchronizedCollection<AuxiliaryTrustProcessController>();

        private static readonly BlockingCollection<AuxiliaryTrustProcessController> AvailableControllerCollection = new BlockingCollection<AuxiliaryTrustProcessController>();

        private static readonly BlockingCollection<InternalExclusivePriorityQueueItem> ExclusivePriorityCollection = new ConcurrentPriorityQueue<InternalExclusivePriorityQueueItem, CustomPriority>().ToBlockingCollection();

        private static int ExpectedControllerNum;

        public static event EventHandler<bool> CurrentBusyStatus;

        private static readonly SemaphoreSlim ResizeTaskLocker = new SemaphoreSlim(1, 1);

        private int CurrentControllerExecutingCommandNum;

        private bool IsDisposed;

        private const int PipeConnectionTimeout = 10000;

        static AuxiliaryTrustProcessController()
        {
            DispatcherThread.Start();
        }

        private AuxiliaryTrustProcessController()
        {
            AllControllerCollection.Add(this);
        }

        private static void DispatcherCore()
        {
            while (true)
            {
            NEXT:
                InternalExclusivePriorityQueueItem Item = ExclusivePriorityCollection.Take();

                while (true)
                {
                    int WaitCount = 0;

                REWAIT:
                    try
                    {
                        using (CancellationTokenSource Cancellation = new CancellationTokenSource(10000))
                        using (CancellationTokenSource CombineCancellation = CancellationTokenSource.CreateLinkedTokenSource(Cancellation.Token, Item.CancelToken))
                        {
                            AuxiliaryTrustProcessController Controller = AvailableControllerCollection.Take(CombineCancellation.Token);

                            Task<IDictionary<string, string>> TestCommandTask = Controller.SendCommandAsync(AuxiliaryTrustProcessCommandType.Test);

                            if (Task.WaitAny(Task.Delay(1000), TestCommandTask) > 0
                                && (TestCommandTask.Result?.ContainsKey("Success")).GetValueOrDefault())
                            {
                                if (WaitCount >= 3)
                                {
                                    CurrentBusyStatus?.Invoke(null, false);
                                }

                                if (Item.CancelToken.IsCancellationRequested)
                                {
                                    Item.TaskSource.TrySetCanceled();
                                }
                                else
                                {
                                    Item.TaskSource.TrySetResult(Exclusive.CreateAsync(Controller).Result);
                                }

                                break;
                            }
                            else
                            {
                                TestCommandTask.ContinueWith((PreviousTask, Input) =>
                                {
                                    if (Input is AuxiliaryTrustProcessController PreviousController)
                                    {
                                        if ((PreviousTask.Result?.ContainsKey("Success")).GetValueOrDefault())
                                        {
                                            AvailableControllerCollection.Add(PreviousController);
                                        }
                                        else
                                        {
                                            if (!PreviousController.IsDisposed)
                                            {
                                                PreviousController.Dispose();
                                            }

                                            LogTracer.Log($"Dispatcher found a controller was disposed or disconnected, trying create a new one for dispatching");

                                            for (int Retry = 1; Retry <= 3; Retry++)
                                            {
                                                if (CreateAsync().Result is AuxiliaryTrustProcessController NewController)
                                                {
                                                    AvailableControllerCollection.Add(NewController);
                                                    break;
                                                }

                                                LogTracer.Log($"Could not recreate a new controller. Retrying execute {nameof(CreateAsync)} in {Retry} times");
                                            }
                                        }
                                    }
                                }, Controller);
                            }
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        if (Item.CancelToken.IsCancellationRequested)
                        {
                            Item.TaskSource.TrySetCanceled();
                            goto NEXT;
                        }
                        else
                        {
                            switch (++WaitCount)
                            {
                                case 3:
                                    {
                                        CurrentBusyStatus?.Invoke(null, true);
                                        goto REWAIT;
                                    }
                                case < 6:
                                    {
                                        goto REWAIT;
                                    }
                                case 6:
                                    {
                                        CurrentBusyStatus?.Invoke(null, false);
                                        Item.TaskSource.TrySetException(new TimeoutException($"{nameof(AuxiliaryTrustProcessController)} task dispatch timeout"));
                                        goto NEXT;
                                    }
                            }
                        }
                    }
                }
            }
        }

        public static Task InitializeAsync()
        {
            return SetExpectedControllerNumAsync(1);
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
                            if (PreviousTask.Result is AuxiliaryTrustProcessController NewController)
                            {
                                AvailableControllerCollection.Add(NewController);
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
                        if (AvailableControllerCollection.TryTake(out AuxiliaryTrustProcessController RemoveController))
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

        public static LazyExclusive GetLazyControllerExclusive(PriorityLevel Priority = PriorityLevel.Normal)
        {
            return new LazyExclusive(Priority);
        }

        public static Task<Exclusive> GetControllerExclusiveAsync(CancellationToken CancelToken = default, PriorityLevel Priority = PriorityLevel.Normal)
        {
            InternalExclusivePriorityQueueItem ExclusiveQueueItem = new InternalExclusivePriorityQueueItem(CancelToken, Priority);

            ExclusivePriorityCollection.Add(ExclusiveQueueItem);

            return ExclusiveQueueItem.TaskSource.Task;
        }

        private static async Task<AuxiliaryTrustProcessController> CreateAsync()
        {
            AuxiliaryTrustProcessController Controller = new AuxiliaryTrustProcessController();

            try
            {
                await FullTrustProcessLauncher.LaunchFullTrustProcessForCurrentAppAsync("AuxiliaryTrustProcess");

                if (await Controller.ConnectRemoteAsync())
                {
                    if (await Controller.SendCommandAsync(AuxiliaryTrustProcessCommandType.GetProcessHandle) is IDictionary<string, string> Response)
                    {
                        if (Response.TryGetValue("Success", out string RawText))
                        {
                            Controller.SetAuxiliaryTrustProcessHandle(new IntPtr(Convert.ToInt64(RawText)));
                        }
                    }

                    return Controller;
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, "Could not create or connect to AuxiliaryTrustProcess as expected");
            }

            Controller.Dispose();

            return null;
        }

        public void SetAuxiliaryTrustProcessHandle(IntPtr ProcessHandle)
        {
            AuxiliaryTrustProcessHandle = new SafeProcessHandle(ProcessHandle, true);

            if (!AuxiliaryTrustProcessHandle.IsInvalid)
            {
                RegisteredAuxiliaryTrustProcessWaitHandle = ThreadPool.RegisterWaitForSingleObject(new ProcessWaitHandle(AuxiliaryTrustProcessHandle.DangerousGetHandle(), false), OnAuxiliaryTrustProcessExited, null, -1, true);
            }
        }

        private void OnAuxiliaryTrustProcessExited(object state, bool timedOut)
        {
            RegisteredAuxiliaryTrustProcessWaitHandle.Unregister(null);

            if (!IsDisposed)
            {
                Dispose();
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

                if (IsConnected)
                {
                    return true;
                }

                PipeCommunicationBaseController?.Dispose();
                PipeCommunicationBaseController = new NamedPipeAuxiliaryCommunicationBaseController();

                if (await PipeCommunicationBaseController.WaitForConnectionAsync(PipeConnectionTimeout))
                {
                    for (int RetryCount = 1; RetryCount <= 3; RetryCount++)
                    {
                        if (PipeCommandReadController != null)
                        {
                            PipeCommandReadController.OnDataReceived -= PipeCommandReadController_OnDataReceived;
                        }

                        if (PipeProgressReadController != null)
                        {
                            PipeProgressReadController.OnDataReceived -= PipeProgressReadController_OnDataReceived;
                        }

                        PipeCommandWriteController?.Dispose();
                        PipeCommandReadController?.Dispose();
                        PipeProgressReadController?.Dispose();
                        PipeCancellationWriteController?.Dispose();

                        PipeCommandReadController = new NamedPipeReadController();
                        PipeProgressReadController = new NamedPipeReadController();
                        PipeCommandWriteController = new NamedPipeWriteController();
                        PipeCancellationWriteController = new NamedPipeWriteController();

                        Dictionary<string, string> Command = new Dictionary<string, string>
                        {
                            { "ProcessId", Convert.ToString(Process.GetCurrentProcess().Id) },
                            { "PipeCommandReadId", PipeCommandReadController.PipeId },
                            { "PipeCommandWriteId", PipeCommandWriteController.PipeId },
                            { "PipeProgressReadId", PipeProgressReadController.PipeId },
                            { "PipeCancellationWriteId", PipeCancellationWriteController.PipeId },
                            { "LogRecordFolderPath", ApplicationData.Current.TemporaryFolder.Path }
                        };

                        PipeCommunicationBaseController.SendData(JsonSerializer.Serialize(Command));

                        if ((await Task.WhenAll(PipeCommandWriteController.WaitForConnectionAsync(PipeConnectionTimeout),
                                                PipeCommandReadController.WaitForConnectionAsync(PipeConnectionTimeout),
                                                PipeProgressReadController.WaitForConnectionAsync(PipeConnectionTimeout),
                                                PipeCancellationWriteController.WaitForConnectionAsync(PipeConnectionTimeout)))
                                       .All((Connected) => Connected))
                        {
                            PipeCommandReadController.OnDataReceived += PipeCommandReadController_OnDataReceived;
                            PipeProgressReadController.OnDataReceived += PipeProgressReadController_OnDataReceived;
                            return true;
                        }
                        else
                        {
                            LogTracer.Log($"Try connect to AuxiliaryTrustProcess in {RetryCount} times");
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

        private void PipeProgressReadController_OnDataReceived(object sender, NamedPipeDataReceivedArgs e)
        {
            if (CommandQueue.TryPeek(out InternalCommandQueueItem CommandObject))
            {
                if (e.ExtraException == null)
                {
                    if (int.TryParse(e.Data, out int IntResult))
                    {
                        CommandObject.ProgressHandler?.Invoke(null, new ProgressChangedEventArgs(Math.Min(100, Math.Max(0, IntResult)), null));
                    }
                    else if (double.TryParse(e.Data, out double DoubleResult))
                    {
                        CommandObject.ProgressHandler?.Invoke(null, new ProgressChangedEventArgs(Math.Min(100, Math.Max(0, Convert.ToInt32(Math.Ceiling(DoubleResult)))), null));
                    }
                    else
                    {
                        throw new InvalidDataException();
                    }
                }
            }
        }

        private void PipeCommandReadController_OnDataReceived(object sender, NamedPipeDataReceivedArgs e)
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
                        ResponseSet = CommandObject.TaskSource.TrySetResult(JsonSerializer.Deserialize<IDictionary<string, string>>(e.Data));
                    }
                    catch (Exception ex)
                    {
                        ResponseSet = CommandObject.TaskSource.TrySetException(ex);
                    }
                }

                if (!ResponseSet && !CommandObject.TaskSource.TrySetCanceled())
                {
                    LogTracer.Log($"{nameof(AuxiliaryTrustProcessController)} could not set the response properly");
                }
            }
        }

        private async Task<IDictionary<string, string>> SendCommandAsync(AuxiliaryTrustProcessCommandType Type, params (string, string)[] Arguments)
        {
            Interlocked.Increment(ref CurrentControllerExecutingCommandNum);

            try
            {
                if (IsConnected)
                {
                    Dictionary<string, string> Command = new Dictionary<string, string>
                    {
                        { "CommandType", Enum.GetName(typeof(AuxiliaryTrustProcessCommandType), Type) }
                    };

                    foreach ((string, object) Argument in Arguments)
                    {
                        Command.Add(Argument.Item1, Convert.ToString(Argument.Item2));
                    }

                    InternalCommandQueueItem CommandItem = new InternalCommandQueueItem();
                    CommandQueue.Enqueue(CommandItem);
                    PipeCommandWriteController.SendData(JsonSerializer.Serialize(Command));

                    return await CommandItem.TaskSource.Task;
                }
                else
                {
                    throw new Exception("Connection between AuxiliaryTrustProcess was lost");
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"{nameof(SendCommandAsync)} throw An exception");
                return null;
            }
            finally
            {
                Interlocked.Decrement(ref CurrentControllerExecutingCommandNum);
            }
        }

        private async Task<IDictionary<string, string>> SendCommandAndReportProgressAsync(AuxiliaryTrustProcessCommandType Type, ProgressChangedEventHandler ProgressHandler, params (string, string)[] Arguments)
        {
            Interlocked.Increment(ref CurrentControllerExecutingCommandNum);

            try
            {
                if (IsConnected)
                {
                    Dictionary<string, string> Command = new Dictionary<string, string>
                    {
                        { "CommandType", Enum.GetName(typeof(AuxiliaryTrustProcessCommandType), Type) }
                    };

                    foreach ((string, string) Argument in Arguments)
                    {
                        Command.Add(Argument.Item1, Argument.Item2);
                    }

                    InternalCommandQueueItem CommandItem = new InternalCommandQueueItem(ProgressHandler);
                    CommandQueue.Enqueue(CommandItem);
                    PipeCommandWriteController.SendData(JsonSerializer.Serialize(Command));

                    return await CommandItem.TaskSource.Task;
                }
                else
                {
                    throw new Exception("Connection between AuxiliaryTrustProcess was lost");
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"{nameof(SendCommandAndReportProgressAsync)} throw An exception");
                return null;
            }
            finally
            {
                Interlocked.Decrement(ref CurrentControllerExecutingCommandNum);
            }
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

        public async Task<bool> SetWallpaperImageAsync(string Path)
        {
            if (await SendCommandAsync(AuxiliaryTrustProcessCommandType.SetWallpaperImage, ("Path", Path)) is IDictionary<string, string> Response)
            {
                if (Response.TryGetValue("Success", out string RawText))
                {
                    return Convert.ToBoolean(RawText);
                }
                else if (Response.TryGetValue("Error", out string ErrorMessage))
                {
                    LogTracer.Log($"An unexpected error was threw in {nameof(SetWallpaperImageAsync)}, message: {ErrorMessage}");
                }
            }

            return false;
        }

        public async Task<string> GetRecyclePathFromOriginPathAsync(string OriginPath)
        {
            if (await SendCommandAsync(AuxiliaryTrustProcessCommandType.GetRecyclePathFromOriginPath, ("OriginPath", OriginPath)) is IDictionary<string, string> Response)
            {
                if (Response.TryGetValue("Success", out string RawText))
                {
                    return RawText;
                }
                else if (Response.TryGetValue("Error", out string ErrorMessage))
                {
                    LogTracer.Log($"An unexpected error was threw in {nameof(GetRecyclePathFromOriginPathAsync)}, message: {ErrorMessage}");
                }
            }

            return string.Empty;
        }

        public async Task<SafeFileHandle> CreateTemporaryFileHandleAsync(string TempFilePath = null)
        {
            if (await SendCommandAsync(AuxiliaryTrustProcessCommandType.CreateTemporaryFileHandle, ("TempFilePath", TempFilePath ?? string.Empty)) is IDictionary<string, string> Response)
            {
                if (Response.TryGetValue("Success", out string HandleString))
                {
                    return new SafeFileHandle(new IntPtr(Convert.ToInt64(HandleString)), true);
                }
                else if (Response.TryGetValue("Error", out string ErrorMessage))
                {
                    LogTracer.Log($"An unexpected error was threw in {nameof(CreateTemporaryFileHandleAsync)}, message: {ErrorMessage}");
                }
            }

            return new SafeFileHandle(IntPtr.Zero, true);
        }

        public async Task<RemoteClipboardRelatedData> GetRemoteClipboardRelatedDataAsync()
        {
            if (await SendCommandAsync(AuxiliaryTrustProcessCommandType.GetRemoteClipboardRelatedData) is IDictionary<string, string> Response)
            {
                if (Response.TryGetValue("Success", out string RawText))
                {
                    return JsonSerializer.Deserialize<RemoteClipboardRelatedData>(RawText);
                }
                else if (Response.TryGetValue("Error", out string ErrorMessage))
                {
                    LogTracer.Log($"An unexpected error was threw in {nameof(GetRemoteClipboardRelatedDataAsync)}, message: {ErrorMessage}");
                }
            }

            return null;
        }

        public async Task<IReadOnlyList<string>> GetAvailableWslDrivePathListAsync()
        {
            if (await SendCommandAsync(AuxiliaryTrustProcessCommandType.GetAvailableWslDrivePathList) is IDictionary<string, string> Response)
            {
                if (Response.TryGetValue("Success", out string RawText))
                {
                    return JsonSerializer.Deserialize<IReadOnlyList<string>>(RawText);
                }
                else if (Response.TryGetValue("Error", out string ErrorMessage))
                {
                    LogTracer.Log($"An unexpected error was threw in {nameof(GetAvailableWslDrivePathListAsync)}, message: {ErrorMessage}");
                }
            }

            return new List<string>(0);
        }

        public async Task<ulong> GetSizeOnDiskAsync(string Path)
        {
            if (await SendCommandAsync(AuxiliaryTrustProcessCommandType.GetSizeOnDisk, ("Path", Path)) is IDictionary<string, string> Response)
            {
                if (Response.TryGetValue("Success", out string RawText))
                {
                    return Convert.ToUInt64(RawText);
                }
                else if (Response.TryGetValue("Error", out string ErrorMessage))
                {
                    LogTracer.Log($"An unexpected error was threw in {nameof(GetSizeOnDiskAsync)}, message: {ErrorMessage}");
                }
            }

            return 0;
        }

        public async Task<IEnumerable<T>> OrderByNaturalStringSortAlgorithmAsync<T>(IEnumerable<T> InputList, Func<T, string> StringSelector, SortDirection Direction)
        {
            Dictionary<string, T> MapDictionary = InputList.ToDictionary((Item) => Guid.NewGuid().ToString("N"));

            if (await SendCommandAsync(AuxiliaryTrustProcessCommandType.OrderByNaturalStringSortAlgorithm, ("InputList", JsonSerializer.Serialize(MapDictionary.Select((Item) => new StringNaturalAlgorithmData(Item.Key, StringSelector(Item.Value) ?? string.Empty))))) is IDictionary<string, string> Response)
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
            if (await SendCommandAsync(AuxiliaryTrustProcessCommandType.MTPReplaceWithNewFile, ("Path", Path), ("NewFilePath", NewFilePath)) is IDictionary<string, string> Response)
            {
                if (Response.TryGetValue("Error", out string ErrorMessage))
                {
                    LogTracer.Log($"An unexpected error was threw in {nameof(MTPReplaceWithNewFileAsync)}, message: {ErrorMessage}");
                }
            }
        }

        public async Task<SafeFileHandle> MTPDownloadAndGetHandleAsync(string Path, AccessMode Access, OptimizeOption Option)
        {
            if (await SendCommandAsync(AuxiliaryTrustProcessCommandType.MTPDownloadAndGetHandle, ("Path", Path), ("AccessMode", Enum.GetName(typeof(AccessMode), Access)), ("OptimizeOption", Enum.GetName(typeof(OptimizeOption), Option))) is IDictionary<string, string> Response)
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

        public async Task<MTPFileData> MTPCreateSubItemAsync(string Path, string Name, CreateType ItemTypes, CreateOption Option)
        {
            if (await SendCommandAsync(AuxiliaryTrustProcessCommandType.MTPCreateSubItem,
                                       ("Path", Path),
                                       ("Name", Name),
                                       ("Type", Enum.GetName(typeof(CreateType), ItemTypes)),
                                       ("Option", Option switch
                                       {
                                           CreateOption.ReplaceExisting => Enum.GetName(typeof(CollisionOptions), CollisionOptions.OverrideOnCollision),
                                           CreateOption.OpenIfExist => Enum.GetName(typeof(CollisionOptions), CollisionOptions.Skip),
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
            if (await SendCommandAsync(AuxiliaryTrustProcessCommandType.MTPGetDriveVolumnData, ("DeviceId", DeviceId)) is IDictionary<string, string> Response)
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

            return null;
        }

        public async Task<bool> MTPCheckExistsAsync(string Path)
        {
            if (await SendCommandAsync(AuxiliaryTrustProcessCommandType.MTPCheckExists, ("Path", Path)) is IDictionary<string, string> Response)
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
            if (await SendCommandAsync(AuxiliaryTrustProcessCommandType.MTPGetItem, ("Path", Path)) is IDictionary<string, string> Response)
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
                if (await SendCommandAsync(AuxiliaryTrustProcessCommandType.MTPGetChildItems,
                                           ("Path", Path),
                                           ("IncludeHiddenItems", Convert.ToString(IncludeHiddenItems)),
                                           ("IncludeSystemItems", Convert.ToString(IncludeSystemItems)),
                                           ("IncludeAllSubItems", Convert.ToString(IncludeAllSubItems)),
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
            if (await SendCommandAsync(AuxiliaryTrustProcessCommandType.ConvertToLongPath, ("Path", Path)) is IDictionary<string, string> Response)
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
                if (await SendCommandAsync(AuxiliaryTrustProcessCommandType.GetFriendlyTypeName, ("Extension", Extension)) is IDictionary<string, string> Response)
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
            if (await SendCommandAsync(AuxiliaryTrustProcessCommandType.GetPermissions, ("Path", Path)) is IDictionary<string, string> Response)
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

        public async Task<bool> SetDriveLabelAsync(string DrivePath, string DriveLabelName, CancellationToken CancelToken = default)
        {
            using (CancelToken.Register(() =>
            {
                if (!TryCancelCurrentOperation())
                {
                    LogTracer.Log($"Could not cancel the operation in {nameof(SetDriveLabelAsync)}");
                }
            }))
            {
                if (await SendCommandAsync(AuxiliaryTrustProcessCommandType.SetDriveLabel, ("Path", DrivePath), ("DriveLabelName", DriveLabelName)) is IDictionary<string, string> Response)
                {
                    if (Response.ContainsKey("Success"))
                    {
                        return true;
                    }
                    else if (Response.TryGetValue("Error_Cancelled", out string ErrorMessage1))
                    {
                        throw new OperationCanceledException(ErrorMessage1);
                    }
                    else if (Response.TryGetValue("Error", out string ErrorMessage2))
                    {
                        LogTracer.Log($"An unexpected error was threw in {nameof(SetDriveLabelAsync)}, message: {ErrorMessage2}");
                    }
                }
            }

            return false;
        }

        public async Task<bool> GetDriveIndexStatusAsync(string DrivePath)
        {
            if (await SendCommandAsync(AuxiliaryTrustProcessCommandType.GetDriveIndexStatus, ("Path", DrivePath)) is IDictionary<string, string> Response)
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
                if (await SendCommandAsync(AuxiliaryTrustProcessCommandType.SetDriveIndexStatus, ("Path", DrivePath), ("AllowIndex", Convert.ToString(AllowIndex)), ("ApplyToSubItems", Convert.ToString(ApplyToSubItems))) is IDictionary<string, string> Response)
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
            if (await SendCommandAsync(AuxiliaryTrustProcessCommandType.GetDriveCompressionStatus, ("Path", DrivePath)) is IDictionary<string, string> Response)
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
                if (await SendCommandAsync(AuxiliaryTrustProcessCommandType.SetDriveCompressionStatus, ("Path", DrivePath), ("IsSetCompressionStatus", Convert.ToString(IsSetCompressionStatus)), ("ApplyToSubItems", Convert.ToString(ApplyToSubItems))) is IDictionary<string, string> Response)
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
            if (await SendCommandAsync(AuxiliaryTrustProcessCommandType.GetAllEncodings) is IDictionary<string, string> Response)
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
            if (await SendCommandAsync(AuxiliaryTrustProcessCommandType.DetectEncoding, ("Path", Path)) is IDictionary<string, string> Response)
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
            if (await SendCommandAsync(AuxiliaryTrustProcessCommandType.GetProperties, ("Path", Path), ("Properties", JsonSerializer.Serialize(Properties))) is IDictionary<string, string> Response)
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
            if (await SendCommandAsync(AuxiliaryTrustProcessCommandType.SetTaskBarProgress, ("ProgressValue", Convert.ToString(ProgressValue))) is IDictionary<string, string> Response)
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
            if (await SendCommandAsync(AuxiliaryTrustProcessCommandType.MapToUNCPath, ("PathList", JsonSerializer.Serialize(PathList))) is IDictionary<string, string> Response)
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
            if (await SendCommandAsync(AuxiliaryTrustProcessCommandType.GetNativeHandle, ("ExecutePath", Path), ("AccessMode", Enum.GetName(typeof(AccessMode), Access)), ("OptimizeOption", Enum.GetName(typeof(OptimizeOption), Option))) is IDictionary<string, string> Response)
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
            if (await SendCommandAsync(AuxiliaryTrustProcessCommandType.GetDirectoryMonitorHandle, ("ExecutePath", Path)) is IDictionary<string, string> Response)
            {
                if (Response.TryGetValue("Success", out string HandleString))
                {
                    return new SafeFileHandle(new IntPtr(Convert.ToInt64(HandleString)), true);
                }
                else
                {
                    if (Response.TryGetValue("Error", out string ErrorMessage))
                    {
                        LogTracer.Log($"An unexpected error was threw in {nameof(GetDirectoryMonitorHandleAsync)}, message: {ErrorMessage}");
                    }
                }
            }

            return new SafeFileHandle(IntPtr.Zero, true);
        }

        public async Task<string> GetMIMEContentTypeAsync(string Path)
        {
            if (await SendCommandAsync(AuxiliaryTrustProcessCommandType.GetMIMEContentType, ("ExecutePath", Path)) is IDictionary<string, string> Response)
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
            if (await SendCommandAsync(AuxiliaryTrustProcessCommandType.GetUrlTargetPath, ("ExecutePath", Path)) is IDictionary<string, string> Response)
            {
                if (Response.TryGetValue("Success", out string TargetPath))
                {
                    return TargetPath;
                }
                else
                {
                    if (Response.TryGetValue("Error", out string ErrorMessage))
                    {
                        LogTracer.Log($"An unexpected error was threw in {nameof(GetUrlTargetPathAsync)}, message: {ErrorMessage}");
                    }
                }
            }

            return string.Empty;
        }

        public async Task<string> GetTooltipTextAsync(string Path, CancellationToken CancelToken = default)
        {
            using (CancelToken.Register(() =>
            {
                if (!TryCancelCurrentOperation())
                {
                    LogTracer.Log($"Could not cancel the operation in {nameof(GetTooltipTextAsync)}");
                }
            }))
            {
                if (await SendCommandAsync(AuxiliaryTrustProcessCommandType.GetTooltipText, ("Path", Path)) is IDictionary<string, string> Response)
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
            }

            return string.Empty;
        }

        public async Task<byte[]> GetThumbnailOverlayAsync(string Path)
        {
            if (await SendCommandAsync(AuxiliaryTrustProcessCommandType.GetThumbnailOverlay, ("Path", Path)) is IDictionary<string, string> Response)
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
            if (await SendCommandAsync(AuxiliaryTrustProcessCommandType.CreateNew, ("NewPath", Path), ("Type", Enum.GetName(typeof(CreateType), Type))) is IDictionary<string, string> Response)
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
            if (await SendCommandAsync(AuxiliaryTrustProcessCommandType.SetAsTopMostWindow, ("PackageFamilyName", PackageFamilyName), ("ProcessId", Convert.ToString(WithPID.GetValueOrDefault()))) is IDictionary<string, string> Response)
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
            if (await SendCommandAsync(AuxiliaryTrustProcessCommandType.RemoveTopMostWindow, ("PackageFamilyName", PackageFamilyName), ("ProcessId", Convert.ToString(WithPID.GetValueOrDefault()))) is IDictionary<string, string> Response)
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

        public async Task<FileAttributes> GetFileAttributeAsync(string Path)
        {
            if (await SendCommandAsync(AuxiliaryTrustProcessCommandType.GetFileAttribute, ("Path", Path)) is IDictionary<string, string> Response)
            {
                if (Response.TryGetValue("Success", out string RawText))
                {
                    return Enum.Parse<FileAttributes>(RawText);
                }
                else if (Response.TryGetValue("Error", out string ErrorMessage))
                {
                    LogTracer.Log($"An unexpected error was threw in {nameof(GetFileAttributeAsync)}, message: {ErrorMessage}");
                }
            }

            return FileAttributes.Normal;
        }

        public async Task SetFileAttributeAsync(string Path, params KeyValuePair<ModifyAttributeAction, System.IO.FileAttributes>[] Attribute)
        {
            if (await SendCommandAsync(AuxiliaryTrustProcessCommandType.SetFileAttribute, ("ExecutePath", Path), ("Attributes", JsonSerializer.Serialize(Attribute))) is IDictionary<string, string> Response)
            {
                if (Response.TryGetValue("Error", out string ErrorMessage))
                {
                    throw new Exception(ErrorMessage);
                }
            }
        }

        public async Task<bool> CheckIfEverythingIsAvailableAsync()
        {
            if (await SendCommandAsync(AuxiliaryTrustProcessCommandType.CheckIfEverythingAvailable) is IDictionary<string, string> Response)
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

        public async Task<IReadOnlyList<string>> SearchByEverythingAsync(string BaseLocation, string SearchWord, bool SearchAsRegex, bool IgnoreCase)
        {
            if (await SendCommandAsync(AuxiliaryTrustProcessCommandType.SearchByEverything, ("BaseLocation", BaseLocation), ("SearchWord", SearchWord), ("SearchAsRegex", Convert.ToString(SearchAsRegex)), ("IgnoreCase", Convert.ToString(IgnoreCase))) is IDictionary<string, string> Response)
            {
                if (Response.TryGetValue("Success", out string Result))
                {
                    return JsonSerializer.Deserialize<IReadOnlyList<string>>(Result);
                }
                else
                {
                    if (Response.TryGetValue("Error", out string ErrorMessage))
                    {
                        LogTracer.Log($"An unexpected error was threw in {nameof(SearchByEverythingAsync)}, message: {ErrorMessage}");
                    }
                }
            }

            return new List<string>(0);
        }

        public async Task<bool> LaunchUWPFromAUMIDAsync(string AppUserModelId, params string[] PathArray)
        {
            if (await SendCommandAsync(AuxiliaryTrustProcessCommandType.LaunchUWP, ("AppUserModelId", AppUserModelId), ("LaunchPathArray", JsonSerializer.Serialize(PathArray))) is IDictionary<string, string> Response)
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
            if (await SendCommandAsync(AuxiliaryTrustProcessCommandType.LaunchUWP, ("PackageFamilyName", PackageFamilyName), ("LaunchPathArray", JsonSerializer.Serialize(PathArray))) is IDictionary<string, string> Response)
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

        public async Task<bool> CheckIfPackageFamilyNameExistAsync(string PackageFamilyName)
        {
            if (await SendCommandAsync(AuxiliaryTrustProcessCommandType.CheckPackageFamilyNameExist, ("PackageFamilyName", PackageFamilyName)) is IDictionary<string, string> Response)
            {
                if (Response.TryGetValue("Success", out string Result))
                {
                    return Convert.ToBoolean(Result);
                }
                else
                {
                    if (Response.TryGetValue("Error", out string ErrorMessage))
                    {
                        LogTracer.Log($"An unexpected error was threw in {nameof(CheckIfPackageFamilyNameExistAsync)}, message: {ErrorMessage}");
                    }
                }
            }

            return false;
        }

        public async Task<InstalledApplication> GetSpecificInstalledUwpApplicationAsync(string PackageFamilyName)
        {
            if (await SendCommandAsync(AuxiliaryTrustProcessCommandType.GetSpecificInstalledUwpApplication, ("PackageFamilyName", PackageFamilyName)) is IDictionary<string, string> Response)
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
                        LogTracer.Log($"An unexpected error was threw in {nameof(GetSpecificInstalledUwpApplicationAsync)}, message: {ErrorMessage}");
                    }
                }
            }

            return null;
        }


        public async Task<IReadOnlyList<InstalledApplication>> GetAllInstalledUwpApplicationAsync()
        {
            if (await SendCommandAsync(AuxiliaryTrustProcessCommandType.GetAllInstalledUwpApplication) is IDictionary<string, string> Response)
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
                        LogTracer.Log($"An unexpected error was threw in {nameof(GetAllInstalledUwpApplicationAsync)}, message: {ErrorMessage}");
                    }
                }
            }

            return Array.Empty<InstalledApplication>();
        }

        public async Task<IRandomAccessStream> GetThumbnailAsync(string Path)
        {
            if (await SendCommandAsync(AuxiliaryTrustProcessCommandType.GetThumbnail, ("ExecutePath", Path)) is IDictionary<string, string> Response)
            {
                if (Response.TryGetValue("Success", out string Result))
                {
                    byte[] Data = JsonSerializer.Deserialize<byte[]>(Result);

                    if (Data.Length > 0)
                    {
                        return await Helper.CreateRandomAccessStreamAsync(Data);
                    }
                }
                else
                {
                    if (Response.TryGetValue("Error", out string ErrorMessage))
                    {
                        LogTracer.Log($"An unexpected error was threw in {nameof(GetThumbnailAsync)}, message: {ErrorMessage}");
                    }
                }
            }

            throw new NotSupportedException("Could not get the thumbnail stream");
        }

        public async Task<IReadOnlyList<ContextMenuItem>> GetContextMenuItemsAsync(string[] PathArray, bool IncludeExtensionItem = false)
        {
            if (PathArray.All((Path) => !string.IsNullOrWhiteSpace(Path)))
            {
                if (await SendCommandAsync(AuxiliaryTrustProcessCommandType.GetContextMenuItems, ("ExecutePath", JsonSerializer.Serialize(PathArray)), ("IncludeExtensionItem", Convert.ToString(IncludeExtensionItem))) is IDictionary<string, string> Response)
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

                if (await SendCommandAsync(AuxiliaryTrustProcessCommandType.InvokeContextMenuItem, ("DataPackage", JsonSerializer.Serialize(ClonePackage))) is IDictionary<string, string> Response)
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
            if (await SendCommandAsync(AuxiliaryTrustProcessCommandType.CreateLink, ("DataPackage", JsonSerializer.Serialize(Package))) is IDictionary<string, string> Response)
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
            if (await SendCommandAsync(AuxiliaryTrustProcessCommandType.UpdateLink, ("DataPackage", JsonSerializer.Serialize(Package))) is IDictionary<string, string> Response)
            {
                if (Response.TryGetValue("Error", out string ErrorMessage))
                {
                    throw new Exception(ErrorMessage);
                }
            }
        }

        public async Task UpdateUrlAsync(UrlFileData Package)
        {
            if (await SendCommandAsync(AuxiliaryTrustProcessCommandType.UpdateUrl, ("DataPackage", JsonSerializer.Serialize(Package))) is IDictionary<string, string> Response)
            {
                if (Response.TryGetValue("Error", out string ErrorMessage))
                {
                    throw new Exception(ErrorMessage);
                }
            }
        }

        public async Task<IReadOnlyList<VariableDataPackage>> GetVariablePathListAsync(string PartialVariable = null)
        {
            if (await SendCommandAsync(AuxiliaryTrustProcessCommandType.GetVariablePathList, ("PartialVariable", PartialVariable)) is IDictionary<string, string> Response)
            {
                if (Response.TryGetValue("Success", out string Result))
                {
                    return JsonSerializer.Deserialize<IReadOnlyList<VariableDataPackage>>(Result);
                }
                else if (Response.TryGetValue("Error", out var ErrorMessage))
                {
                    LogTracer.Log($"An unexpected error was threw in {nameof(GetVariablePathListAsync)}, message: {ErrorMessage}");
                }
            }

            return new List<VariableDataPackage>(0);
        }
        public async Task<string> GetVariablePathAsync(string Variable)
        {
            if (await SendCommandAsync(AuxiliaryTrustProcessCommandType.GetVariablePath, ("Variable", Variable)) is IDictionary<string, string> Response)
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

        public async Task<string> RenameAsync(string Path, string DesireName, bool SkipOperationRecord = false, CancellationToken CancelToken = default)
        {
            using (CancelToken.Register(() =>
            {
                if (!TryCancelCurrentOperation())
                {
                    LogTracer.Log($"Could not cancel the operation in {nameof(RenameAsync)}");
                }
            }))
            {
                if (await SendCommandAsync(AuxiliaryTrustProcessCommandType.Rename, ("ExecutePath", Path), ("DesireName", DesireName)) is IDictionary<string, string> Response)
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
                    else if (Response.TryGetValue("Error_NoPermission", out string ErrorMessage2))
                    {
                        LogTracer.Log($"An unexpected error was threw in {nameof(RenameAsync)}, message: {ErrorMessage2}");
                        throw new InvalidOperationException();
                    }
                    else if (Response.TryGetValue("Error_NotFound", out string ErrorMessage3))
                    {
                        LogTracer.Log($"An unexpected error was threw in {nameof(RenameAsync)}, message: {ErrorMessage3}");
                        throw new FileNotFoundException();
                    }
                    else if (Response.TryGetValue("Error_Failure", out string ErrorMessage4))
                    {
                        throw new Exception(ErrorMessage4);
                    }
                    else if (Response.TryGetValue("Error", out string ErrorMessage5))
                    {
                        throw new Exception(ErrorMessage5);
                    }
                    else
                    {
                        throw new Exception("Unknown response");
                    }
                }
                else
                {
                    throw new NoResponseException();
                }
            }
        }

        public async Task<LinkFileData> GetLinkDataAsync(string Path)
        {
            if (await SendCommandAsync(AuxiliaryTrustProcessCommandType.GetLinkData, ("ExecutePath", Path)) is IDictionary<string, string> Response)
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
            if (await SendCommandAsync(AuxiliaryTrustProcessCommandType.GetUrlData, ("ExecutePath", Path)) is IDictionary<string, string> Response)
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
            if (await SendCommandAsync(AuxiliaryTrustProcessCommandType.InterceptWinE) is IDictionary<string, string> Response)
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
            if (await SendCommandAsync(AuxiliaryTrustProcessCommandType.InterceptFolder) is IDictionary<string, string> Response)
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
            if (await SendCommandAsync(AuxiliaryTrustProcessCommandType.RestoreFolderInterception) is IDictionary<string, string> Response)
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
            if (await SendCommandAsync(AuxiliaryTrustProcessCommandType.RestoreWinEInterception) is IDictionary<string, string> Response)
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
            if (await SendCommandAsync(AuxiliaryTrustProcessCommandType.RunExecutable,
                                       ("ExecutePath", Path),
                                       ("ExecuteParameter", string.Join(' ', Parameters.Select((Para) => Regex.IsMatch(Para, "^[^\"].*\\s+.*[^\"]$") ? $"\"{Para}\"" : Para))),
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
            if (await SendCommandAsync(AuxiliaryTrustProcessCommandType.ToggleQuicklook, ("ExecutePath", Path)) is IDictionary<string, string> Response)
            {
                if (Response.TryGetValue("Error", out string ErrorMessage))
                {
                    LogTracer.Log($"An unexpected error was threw in {nameof(ToggleQuicklookAsync)}, message: {ErrorMessage}");
                }
            }
        }

        public async Task SwitchQuicklookAsync(string Path)
        {
            if (await SendCommandAsync(AuxiliaryTrustProcessCommandType.SwitchQuicklook, ("ExecutePath", Path)) is IDictionary<string, string> Response)
            {
                if (Response.TryGetValue("Error", out string ErrorMessage))
                {
                    LogTracer.Log($"An unexpected error was threw in {nameof(SwitchQuicklookAsync)}, message: {ErrorMessage}");
                }
            }
        }

        public async Task<bool> CheckIfQuicklookIsAvaliableAsync()
        {
            if (await SendCommandAsync(AuxiliaryTrustProcessCommandType.Check_Quicklook) is IDictionary<string, string> Response)
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
            if (await SendCommandAsync(AuxiliaryTrustProcessCommandType.Default_Association, ("ExecutePath", Path)) is IDictionary<string, string> Response)
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

        public async Task<IReadOnlyList<AssociationPackage>> GetAssociationFromExtensionAsync(string Extension)
        {
            if (await SendCommandAsync(AuxiliaryTrustProcessCommandType.GetAssociation, ("Extension", Extension)) is IDictionary<string, string> Response)
            {
                if (Response.TryGetValue("Associate_Result", out string Result))
                {
                    return JsonSerializer.Deserialize<List<AssociationPackage>>(Result);
                }
                else
                {
                    if (Response.TryGetValue("Error", out string ErrorMessage))
                    {
                        LogTracer.Log($"An unexpected error was threw in {nameof(GetAssociationFromExtensionAsync)}, message: {ErrorMessage}");
                    }
                }
            }

            return new List<AssociationPackage>(0);
        }

        public async Task<bool> EmptyRecycleBinAsync()
        {
            if (await SendCommandAsync(AuxiliaryTrustProcessCommandType.EmptyRecycleBin) is IDictionary<string, string> Response)
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

        public async Task<IReadOnlyList<FileSystemStorageItemBase>> GetRecycleBinItemsAsync()
        {
            if (await SendCommandAsync(AuxiliaryTrustProcessCommandType.GetRecycleBinItems) is IDictionary<string, string> Response)
            {
                if (Response.TryGetValue("RecycleBinItems_Json_Result", out string Result))
                {
                    IReadOnlyList<Dictionary<string, string>> JsonList = JsonSerializer.Deserialize<IReadOnlyList<Dictionary<string, string>>>(Result);

                    List<FileSystemStorageItemBase> ItemResult = new List<FileSystemStorageItemBase>(JsonList.Count);

                    foreach (Dictionary<string, string> PropertyDic in JsonList)
                    {
                        try
                        {
                            NativeFileData Data = NativeWin32API.GetStorageItemRawData(PropertyDic["ActualPath"]);

                            if (Data.IsDataValid)
                            {
                                ItemResult.Add(PropertyDic["StorageType"] == "Folder"
                                                        ? new RecycleStorageFolder(Data, PropertyDic["OriginPath"], DateTimeOffset.FromFileTime(Convert.ToInt64(PropertyDic["DeleteTime"])))
                                                        : new RecycleStorageFile(Data, PropertyDic["OriginPath"], DateTimeOffset.FromFileTime(Convert.ToInt64(PropertyDic["DeleteTime"]))));

                            }
                            else
                            {
                                switch (PropertyDic["StorageType"])
                                {
                                    case "Folder":
                                        {
                                            StorageFolder Folder = await StorageFolder.GetFolderFromPathAsync(PropertyDic["ActualPath"]);
                                            ItemResult.Add(new RecycleStorageFolder(await Folder.GetNativeFileDataAsync(), PropertyDic["OriginPath"], DateTimeOffset.FromFileTime(Convert.ToInt64(PropertyDic["DeleteTime"]))));
                                            break;
                                        }
                                    case "File":
                                        {
                                            StorageFile File = await StorageFile.GetFileFromPathAsync(PropertyDic["ActualPath"]);
                                            ItemResult.Add(new RecycleStorageFile(await File.GetNativeFileDataAsync(), PropertyDic["OriginPath"], DateTimeOffset.FromFileTime(Convert.ToInt64(PropertyDic["DeleteTime"]))));
                                            break;
                                        }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            LogTracer.Log(ex, $"Could not load the recycle item, path: {PropertyDic["ActualPath"]}");
                        }
                    }

                    return ItemResult;
                }
                else
                {
                    if (Response.TryGetValue("Error", out string ErrorMessage))
                    {
                        LogTracer.Log($"An unexpected error was threw in {nameof(GetRecycleBinItemsAsync)}, message: {ErrorMessage}");
                    }
                }
            }

            return new List<FileSystemStorageItemBase>(0);
        }

        public async Task<bool> TryUnlockFileOccupy(string Path, bool ForceClose = false)
        {
            if (await SendCommandAsync(AuxiliaryTrustProcessCommandType.UnlockOccupy, ("ExecutePath", Path), ("ForceClose", Convert.ToString(ForceClose))) is IDictionary<string, string> Response)
            {
                if (Response.ContainsKey("Success"))
                {
                    return true;
                }
                if (Response.TryGetValue("Error_Failure", out string ErrorMessage1))
                {
                    LogTracer.Log($"An unexpected error was threw in {nameof(TryUnlockFileOccupy)}, message: {ErrorMessage1}");
                }
                else if (Response.TryGetValue("Error_NotOccupy", out string ErrorMessage2))
                {
                    LogTracer.Log($"An unexpected error was threw in {nameof(TryUnlockFileOccupy)}, message: {ErrorMessage2}");
                    throw new UnlockFileFailedException();
                }
                else if (Response.TryGetValue("Error_NotFoundOrNotFile", out string ErrorMessage3))
                {
                    LogTracer.Log($"An unexpected error was threw in {nameof(TryUnlockFileOccupy)}, message: {ErrorMessage3}");
                    throw new FileNotFoundException();
                }
                else if (Response.TryGetValue("Error", out string ErrorMessage4))
                {
                    LogTracer.Log($"An unexpected error was threw in {nameof(TryUnlockFileOccupy)}, message: {ErrorMessage4}");
                }
                else
                {
                    LogTracer.Log($"An unexpected error was threw in {nameof(TryUnlockFileOccupy)}");
                }

                return false;
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
                if (await SendCommandAndReportProgressAsync(AuxiliaryTrustProcessCommandType.Delete,
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
                    else if (Response.TryGetValue("Error_Capture", out string ErrorMessage2))
                    {
                        LogTracer.Log($"An unexpected error was threw in {nameof(DeleteAsync)}, message: {ErrorMessage2}");
                        throw new FileCaputureException();
                    }
                    else if (Response.TryGetValue("Error_NoPermission", out string ErrorMessage3))
                    {
                        LogTracer.Log($"An unexpected error was threw in {nameof(DeleteAsync)}, message: {ErrorMessage3}");
                        throw new InvalidOperationException("Fail to delete item");
                    }
                    else if (Response.ContainsKey("Error_Cancelled"))
                    {
                        LogTracer.Log($"Operation was cancelled successfully in {nameof(DeleteAsync)}");
                        throw new OperationCanceledException("Operation was cancelled successfully");
                    }
                    else if (Response.TryGetValue("Error_Failure", out string ErrorMessage4))
                    {
                        throw new Exception(ErrorMessage4);
                    }
                    else if (Response.TryGetValue("Error", out string ErrorMessage5))
                    {
                        throw new Exception(ErrorMessage5);
                    }
                    else
                    {
                        throw new Exception("Unknown response");
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
            if (string.IsNullOrEmpty(Source))
            {
                throw new ArgumentNullException(nameof(Source), "Parameter could not be empty or null");
            }

            return DeleteAsync(new string[1] { Source }, PermanentDelete, SkipOperationRecord, CancelToken, ProgressHandler);
        }

        public async Task MoveAsync(Dictionary<string, string> Source,
                                    string DestinationPath,
                                    CollisionOptions Option = CollisionOptions.Skip,
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
                if (await SendCommandAndReportProgressAsync(AuxiliaryTrustProcessCommandType.Move,
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
                    else if (Response.TryGetValue("Error_Capture", out string ErrorMessage2))
                    {
                        LogTracer.Log($"An unexpected error was threw in {nameof(MoveAsync)}, message: {ErrorMessage2}");
                        throw new FileCaputureException();
                    }
                    else if (Response.TryGetValue("Error_NoPermission", out string ErrorMessage3))
                    {
                        LogTracer.Log($"An unexpected error was threw in {nameof(MoveAsync)}, message: {ErrorMessage3}");
                        throw new InvalidOperationException();
                    }
                    else if (Response.TryGetValue("Error_UserCancel", out string ErrorMessage4))
                    {
                        LogTracer.Log($"An unexpected error was threw in {nameof(MoveAsync)}, message: {ErrorMessage4}");
                        throw new OperationCanceledException("Operation was cancelled");
                    }
                    else if (Response.ContainsKey("Error_Cancelled"))
                    {
                        LogTracer.Log($"Operation was cancelled successfully in {nameof(MoveAsync)}");
                        throw new OperationCanceledException("Operation was cancelled");
                    }
                    else if (Response.TryGetValue("Error", out string ErrorMessage5))
                    {
                        throw new Exception(ErrorMessage5);
                    }
                    else if (Response.TryGetValue("Error_Failure", out string ErrorMessage6))
                    {
                        throw new Exception(ErrorMessage6);
                    }
                    else
                    {
                        throw new Exception("Unknown response");
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
                              CollisionOptions Option = CollisionOptions.Skip,
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
                              CollisionOptions Option = CollisionOptions.Skip,
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
                                    CollisionOptions Option = CollisionOptions.Skip,
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
                if (await SendCommandAndReportProgressAsync(AuxiliaryTrustProcessCommandType.Copy,
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
                    else if (Response.ContainsKey("Error_Cancelled"))
                    {
                        LogTracer.Log($"Operation was cancelled successfully in {nameof(CopyAsync)}");
                        throw new OperationCanceledException("Operation was cancelled");
                    }
                    else if (Response.TryGetValue("Error_Failure", out string ErrorMessage2))
                    {
                        throw new Exception(ErrorMessage2);
                    }
                    else if (Response.TryGetValue("Error", out string ErrorMessage5))
                    {
                        throw new Exception(ErrorMessage5);
                    }
                    else
                    {
                        throw new Exception("Unknown response");
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
                              CollisionOptions Option = CollisionOptions.Skip,
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

            if (await SendCommandAsync(AuxiliaryTrustProcessCommandType.RestoreRecycleItem, ("ExecutePath", JsonSerializer.Serialize(OriginPathList))) is IDictionary<string, string> Response)
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

        public async Task PasteRemoteFileAsync(string DestinationPath, CancellationToken CancelToken = default, ProgressChangedEventHandler ProgressHandler = null)
        {
            using (CancelToken.Register(() =>
            {
                if (!TryCancelCurrentOperation())
                {
                    LogTracer.Log($"Could not cancel the operation in {nameof(PasteRemoteFileAsync)}");
                }
            }))
            {
                if (await SendCommandAndReportProgressAsync(AuxiliaryTrustProcessCommandType.PasteRemoteFile, ProgressHandler, ("Path", DestinationPath)) is IDictionary<string, string> Response)
                {
                    if (Response.ContainsKey("Error_Cancelled"))
                    {
                        throw new OperationCanceledException();
                    }
                    else if (Response.TryGetValue("Error", out string ErrorMessage))
                    {
                        throw new Exception(ErrorMessage);
                    }
                }
            }
        }

        public async Task<bool> DeleteItemInRecycleBinAsync(string Path)
        {
            if (string.IsNullOrWhiteSpace(Path))
            {
                throw new ArgumentNullException(nameof(Path), "Parameter could not be null or empty");
            }

            if (await SendCommandAsync(AuxiliaryTrustProcessCommandType.DeleteRecycleItem, ("ExecutePath", Path)) is IDictionary<string, string> Response)
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
                if (await SendCommandAsync(AuxiliaryTrustProcessCommandType.EjectUSB, ("ExecutePath", Path)) is IDictionary<string, string> Response)
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
                    AuxiliaryTrustProcessHandle?.Dispose();
                    RegisteredAuxiliaryTrustProcessWaitHandle?.Unregister(null);
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
                    AllControllerCollection.Remove(this);
                }
            }
        }

        ~AuxiliaryTrustProcessController()
        {
            Dispose();
        }

        private sealed class InternalCommandQueueItem
        {
            public ProgressChangedEventHandler ProgressHandler { get; }

            public TaskCompletionSource<IDictionary<string, string>> TaskSource { get; }

            public InternalCommandQueueItem(ProgressChangedEventHandler ProgressHandler = null) : this()
            {
                this.ProgressHandler = ProgressHandler;
            }

            private InternalCommandQueueItem()
            {
                TaskSource = new TaskCompletionSource<IDictionary<string, string>>();
            }
        }

        private sealed class InternalExclusivePriorityQueueItem : IHavePriority<CustomPriority>
        {
            public CancellationToken CancelToken { get; }

            public CustomPriority Priority { get; set; }

            public TaskCompletionSource<Exclusive> TaskSource { get; } = new TaskCompletionSource<Exclusive>();

            public InternalExclusivePriorityQueueItem(CancellationToken CancelToken, PriorityLevel Priority)
            {
                this.CancelToken = CancelToken;
                this.Priority = new CustomPriority(Priority);
            }
        }

        private class CustomPriority : IEquatable<CustomPriority>, IComparable<CustomPriority>
        {
            public PriorityLevel Priority { get; }

            public override int GetHashCode() => Priority.GetHashCode();

            public bool Equals(CustomPriority other)
            {
                return Priority.Equals(other.Priority);
            }

            public int CompareTo(CustomPriority other)
            {
                return Priority.CompareTo(other.Priority);
            }

            public CustomPriority(PriorityLevel Priority)
            {
                this.Priority = Priority;
            }
        }

        public sealed class LazyExclusive : IDisposable
        {
            private Exclusive Exclusive;
            private readonly PriorityLevel Priority;
            private readonly SemaphoreSlim Locker = new SemaphoreSlim(1, 1);

            public async Task<AuxiliaryTrustProcessController> GetRealControllerAsync()
            {
                await Locker.WaitAsync();

                try
                {
                    return (Exclusive ??= await GetControllerExclusiveAsync(Priority: Priority)).Controller;
                }
                finally
                {
                    Locker.Release();
                }
            }

            public LazyExclusive(PriorityLevel Priority = PriorityLevel.Normal)
            {
                this.Priority = Priority;
            }

            public void Dispose()
            {
                Locker.Dispose();
                Exclusive?.Dispose();
                GC.SuppressFinalize(this);
            }

            ~LazyExclusive()
            {
                Dispose();
            }
        }

        public sealed class Exclusive : IDisposable
        {
            public AuxiliaryTrustProcessController Controller { get; }

            private readonly ExtendedExecutionController ExtExecution;

            private bool IsDisposed;

            public static async Task<Exclusive> CreateAsync(AuxiliaryTrustProcessController Controller)
            {
                return new Exclusive(Controller, await ExtendedExecutionController.CreateExtendedExecutionAsync());
            }

            private Exclusive(AuxiliaryTrustProcessController Controller, ExtendedExecutionController ExtExecution)
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
                    AvailableControllerCollection.Add(Controller);
                }
            }

            ~Exclusive()
            {
                Dispose();
            }
        }
    }
}
