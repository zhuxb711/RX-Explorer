using Microsoft.Toolkit.Deferred;
using Microsoft.Win32;
using ShareClassLibrary;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using UtfUnknown;
using Vanara.PInvoke;
using Vanara.Windows.Shell;
using Windows.ApplicationModel;
using IDataObject = System.Windows.Forms.IDataObject;
using Size = System.Drawing.Size;
using Timer = System.Timers.Timer;

namespace FullTrustProcess
{
    class Program
    {
        private static ManualResetEvent ExitLocker;

        private static Timer AliveCheckTimer;

        private static DateTimeOffset StartTime;

        private static Process ExplorerProcess;

        private static NamedPipeWriteController PipeCommandWriteController;

        private static NamedPipeReadController PipeCommandReadController;

        private static NamedPipeWriteController PipeProgressWriterController;

        private static NamedPipeReadController PipeCancellationReadController;

        private static NamedPipeReadController PipeCommunicationBaseController;

        private static CancellationTokenSource CurrentTaskCancellation;

        [STAThread]
        static void Main(string[] args)
        {
            try
            {
                StartTime = DateTimeOffset.Now;
                ExitLocker = new ManualResetEvent(false);

                Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

                AliveCheckTimer = new Timer(5000)
                {
                    AutoReset = true,
                    Enabled = true
                };
                AliveCheckTimer.Elapsed += AliveCheckTimer_Elapsed;

                AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

                if (args.FirstOrDefault() == "/ExecuteAdminOperation")
                {
                    string Input = args.LastOrDefault();

                    if (!string.IsNullOrEmpty(Input))
                    {
                        using (NamedPipeClientStream PipeClient = new NamedPipeClientStream(".", Input, PipeDirection.InOut, PipeOptions.Asynchronous | PipeOptions.WriteThrough, TokenImpersonationLevel.Anonymous))
                        {
                            PipeClient.Connect(2000);

                            using (StreamReader Reader = new StreamReader(PipeClient, new UTF8Encoding(false), true, leaveOpen: true))
                            using (StreamWriter Writer = new StreamWriter(PipeClient, new UTF8Encoding(false), leaveOpen: true))
                            {
                                IDictionary<string, string> Value = new Dictionary<string, string>();

                                try
                                {
                                    string RawTypeData = Reader.ReadLine();
                                    string CancelSignalData = Reader.ReadLine();
                                    string CommandData = Reader.ReadLine();

                                    if (EventWaitHandle.TryOpenExisting(CancelSignalData, out EventWaitHandle EventHandle))
                                    {
                                        using (CancellationTokenSource Cancellation = new CancellationTokenSource())
                                        {
                                            RegisteredWaitHandle RegistedHandle = ThreadPool.RegisterWaitForSingleObject(EventHandle, (state, timeout) =>
                                            {
                                                if (state is CancellationTokenSource Cancellation)
                                                {
                                                    Cancellation.Cancel();
                                                }
                                            }, Cancellation, -1, true);

                                            try
                                            {
                                                switch (JsonSerializer.Deserialize(CommandData, Type.GetType(RawTypeData)))
                                                {
                                                    case ElevationSetDriveCompressStatusData DriveCompressStatusData:
                                                        {
                                                            static bool SetCompressionCore(string Path, Kernel32.COMPRESSION_FORMAT CompressStatus)
                                                            {
                                                                if (Directory.Exists(Path))
                                                                {
                                                                    using (Kernel32.SafeHFILE Handle = Kernel32.CreateFile(Path, Kernel32.FileAccess.GENERIC_READ | Kernel32.FileAccess.GENERIC_WRITE, FileShare.ReadWrite, null, FileMode.Open, FileFlagsAndAttributes.FILE_FLAG_BACKUP_SEMANTICS))
                                                                    {
                                                                        if (Handle.IsInvalid)
                                                                        {
                                                                            return false;
                                                                        }
                                                                        else
                                                                        {
                                                                            return Kernel32.DeviceIoControl(Handle, Kernel32.IOControlCode.FSCTL_SET_COMPRESSION, CompressStatus);
                                                                        }
                                                                    }
                                                                }
                                                                else if (File.Exists(Path))
                                                                {
                                                                    using (Kernel32.SafeHFILE Handle = Kernel32.CreateFile(Path, Kernel32.FileAccess.GENERIC_READ | Kernel32.FileAccess.GENERIC_WRITE, FileShare.ReadWrite, null, FileMode.Open, FileFlagsAndAttributes.FILE_ATTRIBUTE_NORMAL))
                                                                    {
                                                                        if (Handle.IsInvalid)
                                                                        {
                                                                            return false;
                                                                        }
                                                                        else
                                                                        {
                                                                            return Kernel32.DeviceIoControl(Handle, Kernel32.IOControlCode.FSCTL_SET_COMPRESSION, CompressStatus);
                                                                        }
                                                                    }
                                                                }
                                                                else
                                                                {
                                                                    return false;
                                                                }
                                                            }

                                                            Kernel32.COMPRESSION_FORMAT CompressionFormat = DriveCompressStatusData.IsSetCompressionStatus ? Kernel32.COMPRESSION_FORMAT.COMPRESSION_FORMAT_LZNT1 : Kernel32.COMPRESSION_FORMAT.COMPRESSION_FORMAT_NONE;

                                                            if (SetCompressionCore(DriveCompressStatusData.Path, CompressionFormat))
                                                            {
                                                                if (DriveCompressStatusData.ApplyToSubItems)
                                                                {
                                                                    foreach (string Entry in Directory.EnumerateFileSystemEntries(DriveCompressStatusData.Path))
                                                                    {
                                                                        if (Cancellation.IsCancellationRequested)
                                                                        {
                                                                            break;
                                                                        }

                                                                        SetCompressionCore(DriveCompressStatusData.Path, CompressionFormat);
                                                                    }
                                                                }

                                                                Value.Add("Success", string.Empty);
                                                            }
                                                            else
                                                            {
                                                                Value.Add("Error_ApplyToRootFailure", "Could not apply compression status to root path");
                                                            }

                                                            break;
                                                        }
                                                    case ElevationSetDriveIndexStatusData DriveIndexStatusData:
                                                        {
                                                            try
                                                            {
                                                                File.SetAttributes(DriveIndexStatusData.Path, DriveIndexStatusData.AllowIndex ? File.GetAttributes(DriveIndexStatusData.Path) & ~FileAttributes.NotContentIndexed : File.GetAttributes(DriveIndexStatusData.Path) | FileAttributes.NotContentIndexed);

                                                                if (DriveIndexStatusData.ApplyToSubItems)
                                                                {
                                                                    foreach (string Entry in Directory.EnumerateFileSystemEntries(DriveIndexStatusData.Path))
                                                                    {
                                                                        if (Cancellation.IsCancellationRequested)
                                                                        {
                                                                            break;
                                                                        }

                                                                        try
                                                                        {
                                                                            File.SetAttributes(DriveIndexStatusData.Path, DriveIndexStatusData.AllowIndex ? File.GetAttributes(DriveIndexStatusData.Path) & ~FileAttributes.NotContentIndexed : File.GetAttributes(DriveIndexStatusData.Path) | FileAttributes.NotContentIndexed);
                                                                        }
                                                                        catch (Exception)
                                                                        {
                                                                            //No need to handle this exception
                                                                        }
                                                                    }
                                                                }

                                                                Value.Add("Success", string.Empty);
                                                            }
                                                            catch (Exception)
                                                            {
                                                                Value.Add("Error", "Could not set file attribute to the root path");
                                                            }

                                                            break;
                                                        }
                                                    case ElevationSetDriveLabelData DriveLabelData:
                                                        {
                                                            short LengthLimit;

                                                            if (Kernel32.GetVolumeInformation(DriveLabelData.Path, out _, out _, out _, out _, out string FileSystemName))
                                                            {
                                                                LengthLimit = FileSystemName switch
                                                                {
                                                                    "NTFS" or "HPFS" or "CDFS" or "UDF" or "NWFS" => 32,
                                                                    "FAT32" or "exFAT" or "FAT" => 11,
                                                                    _ => short.MaxValue
                                                                };
                                                            }
                                                            else
                                                            {
                                                                LengthLimit = 32;
                                                            }

                                                            if (DriveLabelData.DriveLabelName.Length > LengthLimit)
                                                            {
                                                                Value.Add("Error", $"Drive label name is longer than limitation, drive filesystem: {FileSystemName}");
                                                            }
                                                            else
                                                            {
                                                                if (Kernel32.SetVolumeLabel(DriveLabelData.Path, DriveLabelData.DriveLabelName))
                                                                {
                                                                    Value.Add("Success", string.Empty);
                                                                }
                                                                else
                                                                {
                                                                    Value.Add("Error", $"Set drive label failed, message: {new Win32Exception(Marshal.GetLastWin32Error()).Message}");
                                                                }
                                                            }

                                                            break;
                                                        }
                                                    case ElevationCreateNewData NewData:
                                                        {
                                                            if (StorageItemController.CheckPermission(Path.GetDirectoryName(NewData.Path) ?? NewData.Path, NewData.Type == CreateType.File ? FileSystemRights.CreateFiles : FileSystemRights.CreateDirectories))
                                                            {
                                                                if (StorageItemController.Create(NewData.Type, NewData.Path))
                                                                {
                                                                    Value.Add("Success", NewData.Path);
                                                                }
                                                                else if (Directory.Exists(NewData.Path) || File.Exists(NewData.Path))
                                                                {
                                                                    Value.Add("Success", NewData.Path);
                                                                }
                                                                else
                                                                {
                                                                    Value.Add("Error_Failure", "Error happened when create new");
                                                                }
                                                            }
                                                            else
                                                            {
                                                                Value.Add("Error_NoPermission", "Do not have enough permission");
                                                            }

                                                            break;
                                                        }
                                                    case ElevationCopyData CopyData:
                                                        {
                                                            if (CopyData.SourcePath.All((Item) => Directory.Exists(Item) || File.Exists(Item)))
                                                            {
                                                                if (StorageItemController.CheckPermission(CopyData.DestinationPath, FileSystemRights.Modify))
                                                                {
                                                                    List<string> OperationRecordList = new List<string>();

                                                                    if (StorageItemController.Copy(CopyData.SourcePath, CopyData.DestinationPath, CopyData.Option, (s, e) =>
                                                                    {
                                                                        if (Cancellation.IsCancellationRequested)
                                                                        {
                                                                            throw new COMException(null, HRESULT.E_ABORT);
                                                                        }
                                                                    }, PostCopyEvent: (se, arg) =>
                                                                    {
                                                                        if (arg.Result == HRESULT.S_OK)
                                                                        {
                                                                            if (arg.DestItem == null || string.IsNullOrEmpty(arg.Name))
                                                                            {
                                                                                OperationRecordList.Add($"{arg.SourceItem.FileSystemPath}||Copy||{Path.Combine(arg.DestFolder.FileSystemPath, arg.SourceItem.Name)}");
                                                                            }
                                                                            else
                                                                            {
                                                                                OperationRecordList.Add($"{arg.SourceItem.FileSystemPath}||Copy||{Path.Combine(arg.DestFolder.FileSystemPath, arg.Name)}");
                                                                            }
                                                                        }
                                                                    }))
                                                                    {
                                                                        Value.Add("Success", JsonSerializer.Serialize(OperationRecordList));
                                                                    }
                                                                    else if (CopyData.SourcePath.Select((Item) => Path.Combine(CopyData.DestinationPath, Path.GetFileName(Item)))
                                                                                                .All((Path) => Directory.Exists(Path) || File.Exists(Path)))
                                                                    {
                                                                        Value.Add("Success", JsonSerializer.Serialize(OperationRecordList));
                                                                    }
                                                                    else
                                                                    {
                                                                        Value.Add("Error_Failure", "Error happened when copying files");
                                                                    }
                                                                }
                                                                else
                                                                {
                                                                    Value.Add("Error_Capture", "Do not have enough permission");
                                                                }
                                                            }
                                                            else
                                                            {
                                                                Value.Add("Error_NotFound", "Could not found the file");
                                                            }

                                                            break;
                                                        }
                                                    case ElevationMoveData MoveData:
                                                        {
                                                            if (MoveData.SourcePath.Keys.All((Item) => Directory.Exists(Item) || File.Exists(Item)))
                                                            {
                                                                if (MoveData.SourcePath.Keys.Any((Item) => StorageItemController.CheckCaptured(Item)))
                                                                {
                                                                    Value.Add("Error_Capture", "An error occurred while renaming the files");
                                                                }
                                                                else
                                                                {
                                                                    if (StorageItemController.CheckPermission(MoveData.DestinationPath, FileSystemRights.Modify)
                                                                        && MoveData.SourcePath.Keys.All((Path) => StorageItemController.CheckPermission(System.IO.Path.GetDirectoryName(Path) ?? Path, FileSystemRights.Modify)))
                                                                    {
                                                                        List<string> OperationRecordList = new List<string>();

                                                                        if (StorageItemController.Move(MoveData.SourcePath, MoveData.DestinationPath, MoveData.Option, (s, e) =>
                                                                        {
                                                                            if (Cancellation.IsCancellationRequested)
                                                                            {
                                                                                throw new COMException(null, HRESULT.E_ABORT);
                                                                            }
                                                                        }, PostMoveEvent: (se, arg) =>
                                                                        {
                                                                            if (arg.Result == HRESULT.COPYENGINE_S_DONT_PROCESS_CHILDREN)
                                                                            {
                                                                                if (arg.DestItem == null || string.IsNullOrEmpty(arg.Name))
                                                                                {
                                                                                    OperationRecordList.Add($"{arg.SourceItem.FileSystemPath}||Move||{Path.Combine(arg.DestFolder.FileSystemPath, arg.SourceItem.Name)}");
                                                                                }
                                                                                else
                                                                                {
                                                                                    OperationRecordList.Add($"{arg.SourceItem.FileSystemPath}||Move||{Path.Combine(arg.DestFolder.FileSystemPath, arg.Name)}");
                                                                                }
                                                                            }
                                                                        }))
                                                                        {
                                                                            if (MoveData.SourcePath.Keys.All((Item) => !Directory.Exists(Item) && !File.Exists(Item)))
                                                                            {
                                                                                Value.Add("Success", JsonSerializer.Serialize(OperationRecordList));
                                                                            }
                                                                            else
                                                                            {
                                                                                Value.Add("Error_Capture", "An error occurred while renaming the files");
                                                                            }
                                                                        }
                                                                        else if (MoveData.SourcePath.Keys.All((Item) => !Directory.Exists(Item) && !File.Exists(Item))
                                                                                 && MoveData.SourcePath.Select((Item) => Path.Combine(MoveData.DestinationPath, string.IsNullOrEmpty(Item.Value) ? Path.GetFileName(Item.Key) : Item.Value))
                                                                                                       .All((Path) => Directory.Exists(Path) || File.Exists(Path)))
                                                                        {
                                                                            Value.Add("Success", JsonSerializer.Serialize(OperationRecordList));
                                                                        }
                                                                        else
                                                                        {
                                                                            Value.Add("Error_Failure", "Error happened when moving files");
                                                                        }
                                                                    }
                                                                    else
                                                                    {
                                                                        Value.Add("Error_Capture", "Do not have enough permission");
                                                                    }
                                                                }
                                                            }
                                                            else
                                                            {
                                                                Value.Add("Error_NotFound", "Could not found the file");
                                                            }

                                                            break;
                                                        }
                                                    case ElevationDeleteData DeleteData:
                                                        {
                                                            if (DeleteData.DeletePath.All((Item) => Directory.Exists(Item) || File.Exists(Item)))
                                                            {
                                                                if (DeleteData.DeletePath.Any((Item) => StorageItemController.CheckCaptured(Item)))
                                                                {
                                                                    Value.Add("Error_Capture", "An error occurred while renaming the files");
                                                                }
                                                                else
                                                                {
                                                                    if (DeleteData.DeletePath.All((Path) => StorageItemController.CheckPermission(System.IO.Path.GetDirectoryName(Path) ?? Path, FileSystemRights.Modify)))
                                                                    {
                                                                        List<string> OperationRecordList = new List<string>();

                                                                        if (StorageItemController.Delete(DeleteData.DeletePath, DeleteData.PermanentDelete, (s, e) =>
                                                                        {
                                                                            if (Cancellation.IsCancellationRequested)
                                                                            {
                                                                                throw new COMException(null, HRESULT.E_ABORT);
                                                                            }
                                                                        }, PostDeleteEvent: (se, arg) =>
                                                                        {
                                                                            if (!DeleteData.PermanentDelete)
                                                                            {
                                                                                OperationRecordList.Add($"{arg.SourceItem.FileSystemPath}||Delete");
                                                                            }
                                                                        }))
                                                                        {
                                                                            if (DeleteData.DeletePath.All((Item) => !Directory.Exists(Item) && !File.Exists(Item)))
                                                                            {
                                                                                Value.Add("Success", JsonSerializer.Serialize(OperationRecordList));
                                                                            }
                                                                            else
                                                                            {
                                                                                Value.Add("Error_Capture", "An error occurred while renaming the files");
                                                                            }
                                                                        }
                                                                        else if (DeleteData.DeletePath.All((Item) => !Directory.Exists(Item) && !File.Exists(Item)))
                                                                        {
                                                                            Value.Add("Success", JsonSerializer.Serialize(OperationRecordList));
                                                                        }
                                                                        else
                                                                        {
                                                                            Value.Add("Error_Failure", "The specified file could not be deleted");
                                                                        }
                                                                    }
                                                                    else
                                                                    {
                                                                        Value.Add("Error_Capture", "Do not have enough permission");
                                                                    }
                                                                }
                                                            }
                                                            else
                                                            {
                                                                Value.Add("Error_NotFound", "Could not found the file");
                                                            }

                                                            break;
                                                        }
                                                    case ElevationRenameData RenameData:
                                                        {
                                                            if (File.Exists(RenameData.Path) || Directory.Exists(RenameData.Path))
                                                            {
                                                                if (StorageItemController.CheckCaptured(RenameData.Path))
                                                                {
                                                                    Value.Add("Error_Capture", "An error occurred while renaming the files");
                                                                }
                                                                else
                                                                {
                                                                    if (StorageItemController.CheckPermission(Path.GetDirectoryName(RenameData.Path) ?? RenameData.Path, FileSystemRights.Modify))
                                                                    {
                                                                        string NewName = string.Empty;

                                                                        if (StorageItemController.Rename(RenameData.Path, RenameData.DesireName, (s, e) =>
                                                                        {
                                                                            NewName = e.Name;
                                                                        }))
                                                                        {
                                                                            Value.Add("Success", NewName);
                                                                        }
                                                                        else
                                                                        {
                                                                            Value.Add("Error_Failure", "Error happened when renaming files");
                                                                        }
                                                                    }
                                                                    else
                                                                    {
                                                                        Value.Add("Error_NoPermission", "Do not have enough permission");
                                                                    }
                                                                }
                                                            }
                                                            else
                                                            {
                                                                Value.Add("Error_NotFound", "Could not found the file");
                                                            }

                                                            break;
                                                        }
                                                }
                                            }
                                            finally
                                            {
                                                RegistedHandle.Unregister(EventHandle);
                                            }
                                        }
                                    }
                                    else
                                    {
                                        Value.Add("Error_CancelSignalNotSet", "Failed to get the CancelSignal");
                                    }
                                }
                                finally
                                {
                                    Writer.WriteLine(JsonSerializer.Serialize(Value));
                                    Writer.Flush();
                                }
                            }
                        }
                    }
                    else
                    {
                        throw new InvalidDataException("Startup parameter is not correct");
                    }
                }
                else
                {
                    PipeCommunicationBaseController = new NamedPipeReadController("Explorer_NamedPipe_CommunicationBase");
                    PipeCommunicationBaseController.OnDataReceived += PipeCommunicationBaseController_OnDataReceived;

                    if (SpinWait.SpinUntil(() => PipeCommunicationBaseController.IsConnected, 5000))
                    {
                        AliveCheckTimer.Start();
                    }
                    else
                    {
                        LogTracer.Log($"Could not connect to the explorer. CommunicationBaseController connect timeout. Exiting...");
                        ExitLocker.Set();
                    }

                    ExitLocker.WaitOne();
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, "An unexpected exception was threw in starting FullTrustProcess");
            }
            finally
            {
                ExitLocker?.Dispose();
                AliveCheckTimer?.Dispose();

                PipeCommandWriteController?.Dispose();
                PipeCommandReadController?.Dispose();
                PipeProgressWriterController?.Dispose();
                PipeCancellationReadController?.Dispose();
                PipeCommunicationBaseController?.Dispose();

                LogTracer.MakeSureLogIsFlushed(2000);
            }
        }

        private static void AliveCheckTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            try
            {
                if ((ExplorerProcess?.HasExited).GetValueOrDefault())
                {
                    ExitLocker.Set();
                }
                else if (e.SignalTime - StartTime >= TimeSpan.FromSeconds(10))
                {
                    if (!((PipeCommandWriteController?.IsConnected).GetValueOrDefault()
                          && (PipeCommandReadController?.IsConnected).GetValueOrDefault()
                          && (PipeProgressWriterController?.IsConnected).GetValueOrDefault()
                          && (PipeCancellationReadController?.IsConnected).GetValueOrDefault()
                          && (PipeCommunicationBaseController?.IsConnected).GetValueOrDefault()))
                    {
                        ExitLocker.Set();
                    }
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"{nameof(AliveCheckTimer_Elapsed)} threw an exception, message: {ex.Message}");
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
                        }
                    }

                    if (Package.TryGetValue("PipeCommandWriteId", out string PipeCommandWriteId))
                    {
                        if (PipeCommandReadController != null)
                        {
                            PipeCommandReadController.Dispose();
                            PipeCommandReadController.OnDataReceived -= PipeReadController_OnDataReceived;
                        }

                        PipeCommandReadController = new NamedPipeReadController(PipeCommandWriteId);
                        PipeCommandReadController.OnDataReceived += PipeReadController_OnDataReceived;
                    }

                    if (Package.TryGetValue("PipeCommandReadId", out string PipeCommandReadId))
                    {
                        if (PipeCommandWriteController != null)
                        {
                            PipeCommandWriteController.Dispose();
                        }

                        PipeCommandWriteController = new NamedPipeWriteController(PipeCommandReadId);
                    }

                    if (Package.TryGetValue("PipeProgressReadId", out string PipeProgressReadId))
                    {
                        if (PipeProgressWriterController != null)
                        {
                            PipeProgressWriterController.Dispose();
                        }

                        PipeProgressWriterController = new NamedPipeWriteController(PipeProgressReadId);
                    }

                    if (Package.TryGetValue("PipeCancellationWriteId", out string PipeCancellationReadId))
                    {
                        if (PipeCancellationReadController != null)
                        {
                            PipeCancellationReadController.Dispose();
                            PipeCancellationReadController.OnDataReceived -= PipeReadController_OnDataReceived;
                        }

                        PipeCancellationReadController = new NamedPipeReadController(PipeCancellationReadId);
                        PipeCancellationReadController.OnDataReceived += PipeCancellationController_OnDataReceived;
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

        private static void PipeCancellationController_OnDataReceived(object sender, NamedPipeDataReceivedArgs e)
        {
            if (e.Data == "Cancel")
            {
                CurrentTaskCancellation?.Cancel();
            }
        }

        private static async void PipeReadController_OnDataReceived(object sender, NamedPipeDataReceivedArgs e)
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
                    IDictionary<string, string> Response = await HandleCommand(Request);
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

        private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is Exception Ex)
            {
                LogTracer.Log(Ex, "UnhandledException");
                LogTracer.MakeSureLogIsFlushed(2000);

                ExitLocker?.Dispose();
                AliveCheckTimer?.Dispose();

                PipeCommandWriteController?.Dispose();
                PipeCommandReadController?.Dispose();
                PipeProgressWriterController?.Dispose();
                PipeCancellationReadController?.Dispose();
                PipeCommunicationBaseController?.Dispose();
            }
        }

        private async static Task<IDictionary<string, string>> HandleCommand(IDictionary<string, string> CommandValue)
        {
            IDictionary<string, string> Value = new Dictionary<string, string>();

            CurrentTaskCancellation?.Dispose();
            CurrentTaskCancellation = new CancellationTokenSource();

            try
            {
                switch (Enum.Parse(typeof(CommandType), CommandValue["CommandType"]))
                {
                    case CommandType.ConvertToLongPath:
                        {
                            string Path = Convert.ToString(CommandValue["Path"]);

                            if (Directory.Exists(Path) || File.Exists(Path))
                            {
                                Value.Add("Success", Helper.ConvertShortPathToLongPath(Path));
                            }
                            else
                            {
                                Value.Add("Error", "File or directory is not found");
                            }

                            break;
                        }
                    case CommandType.GetFriendlyTypeName:
                        {
                            string[] ExtensionArray = JsonSerializer.Deserialize<string[]>(Convert.ToString(CommandValue["ExtensionArray"]));

                            List<string> Result = new List<string>(ExtensionArray.Length);

                            foreach (string Extension in ExtensionArray)
                            {
                                string FriendlyName = ExtensionAssociation.GetFriendlyTypeNameFromExtension(Extension);

                                if (string.IsNullOrEmpty(FriendlyName))
                                {
                                    Result.Add(Extension);
                                }
                                else
                                {
                                    Result.Add(FriendlyName);
                                }
                            }

                            Value.Add("Success", JsonSerializer.Serialize(Result));

                            break;
                        }
                    case CommandType.GetPermissions:
                        {
                            string Path = Convert.ToString(CommandValue["Path"]);

                            if (Directory.Exists(Path) || File.Exists(Path))
                            {
                                Value.Add("Success", JsonSerializer.Serialize(StorageItemController.GetAllAccountPermissions(Path)));
                            }
                            else
                            {
                                Value.Add("Error", "File or directory is not found");
                            }

                            break;
                        }
                    case CommandType.SetDriveLabel:
                        {
                            string Path = Convert.ToString(CommandValue["Path"]);
                            string DriveLabelName = Convert.ToString(CommandValue["DriveLabelName"]);

                            if (System.IO.Path.GetPathRoot(Path).Equals(Path, StringComparison.OrdinalIgnoreCase))
                            {
                                IDictionary<string, string> ResultMap = await CreateNewProcessAsElevatedAndWaitForResultAsync(new ElevationSetDriveLabelData(Path, DriveLabelName), CurrentTaskCancellation.Token);

                                foreach (KeyValuePair<string, string> Result in ResultMap)
                                {
                                    Value.Add(Result);
                                }
                            }
                            else
                            {
                                Value.Add("Error", "Path is not a drive root path");
                            }

                            break;
                        }
                    case CommandType.SetDriveIndexStatus:
                        {
                            string Path = Convert.ToString(CommandValue["Path"]);
                            bool ApplyToSubItems = Convert.ToBoolean(CommandValue["ApplyToSubItems"]);
                            bool AllowIndex = Convert.ToBoolean(CommandValue["AllowIndex"]);

                            if (System.IO.Path.GetPathRoot(Path).Equals(Path, StringComparison.OrdinalIgnoreCase))
                            {
                                IDictionary<string, string> ResultMap = await CreateNewProcessAsElevatedAndWaitForResultAsync(new ElevationSetDriveIndexStatusData(Path, AllowIndex, ApplyToSubItems), CurrentTaskCancellation.Token);

                                foreach (KeyValuePair<string, string> Result in ResultMap)
                                {
                                    Value.Add(Result);
                                }
                            }
                            else
                            {
                                Value.Add("Error", "Path is not a drive root path");
                            }

                            break;
                        }
                    case CommandType.GetDriveIndexStatus:
                        {
                            string Path = Convert.ToString(CommandValue["Path"]);

                            if (System.IO.Path.GetPathRoot(Path).Equals(Path, StringComparison.OrdinalIgnoreCase))
                            {
                                Value.Add("Success", Convert.ToString(!File.GetAttributes(Path).HasFlag(FileAttributes.NotContentIndexed)));
                            }
                            else
                            {
                                Value.Add("Error", "Path is not a drive root path");
                            }

                            break;
                        }
                    case CommandType.SetDriveCompressionStatus:
                        {
                            string Path = Convert.ToString(CommandValue["Path"]);
                            bool ApplyToSubItems = Convert.ToBoolean(CommandValue["ApplyToSubItems"]);
                            bool IsSetCompressionStatus = Convert.ToBoolean(CommandValue["IsSetCompressionStatus"]);

                            if (System.IO.Path.GetPathRoot(Path).Equals(Path, StringComparison.OrdinalIgnoreCase))
                            {
                                IDictionary<string, string> ResultMap = await CreateNewProcessAsElevatedAndWaitForResultAsync(new ElevationSetDriveCompressStatusData(Path, IsSetCompressionStatus, ApplyToSubItems), CurrentTaskCancellation.Token);

                                foreach (KeyValuePair<string, string> Result in ResultMap)
                                {
                                    Value.Add(Result);
                                }
                            }
                            else
                            {
                                Value.Add("Error", "Path is not a drive root path");
                            }

                            break;
                        }
                    case CommandType.GetDriveCompressionStatus:
                        {
                            string Path = Convert.ToString(CommandValue["Path"]);

                            if (System.IO.Path.GetPathRoot(Path).Equals(Path, StringComparison.OrdinalIgnoreCase))
                            {
                                if (Directory.Exists(Path))
                                {
                                    using (Kernel32.SafeHFILE Handle = Kernel32.CreateFile(Path, Kernel32.FileAccess.GENERIC_READ, FileShare.ReadWrite, null, FileMode.Open, FileFlagsAndAttributes.FILE_FLAG_BACKUP_SEMANTICS))
                                    {
                                        if (Handle.IsInvalid)
                                        {
                                            Value.Add("Error", "Handle is invalid");
                                        }
                                        else
                                        {
                                            if (Kernel32.DeviceIoControl(Handle, Kernel32.IOControlCode.FSCTL_GET_COMPRESSION, out Kernel32.COMPRESSION_FORMAT CompressionFormat))
                                            {
                                                Value.Add("Success", Convert.ToString(CompressionFormat == Kernel32.COMPRESSION_FORMAT.COMPRESSION_FORMAT_LZNT1));
                                            }
                                        }
                                    }
                                }
                                else
                                {
                                    Value.Add("Error", "File or directory is not found");
                                }
                            }
                            else
                            {
                                Value.Add("Error", "Path is not a drive root path");
                            }

                            break;
                        }
                    case CommandType.DetectEncoding:
                        {
                            string Path = Convert.ToString(CommandValue["Path"]);

                            DetectionResult Detection = CharsetDetector.DetectFromFile(Path);
                            DetectionDetail Details = Detection.Detected;

                            if ((Details?.Confidence).GetValueOrDefault() >= 0.8f)
                            {
                                Value.Add("Success", Convert.ToString(Details.Encoding.CodePage));
                            }
                            else
                            {
                                Value.Add("Error", "Detect encoding failed");
                            }

                            break;
                        }
                    case CommandType.GetAllEncodings:
                        {
                            Value.Add("Success", JsonSerializer.Serialize(Encoding.GetEncodings().Select((Encoding) => Encoding.CodePage)));
                            break;
                        }
                    case CommandType.Test:
                        {
                            Value.Add("Success", string.Empty);
                            break;
                        }
                    case CommandType.GetProperties:
                        {
                            string Path = Convert.ToString(CommandValue["Path"]);

                            IReadOnlyList<string> Properties = JsonSerializer.Deserialize<IReadOnlyList<string>>(Convert.ToString(CommandValue["Properties"]));

                            if (File.Exists(Path) || Directory.Exists(Path))
                            {
                                Dictionary<string, string> Result = new Dictionary<string, string>(Properties.Count);

                                using (ShellItem Item = new ShellItem(Path))
                                {
                                    foreach (string Property in Properties)
                                    {
                                        try
                                        {
                                            object PropertyObj = Item.Properties[Property];

                                            string PropertyValue = PropertyObj switch
                                            {
                                                IEnumerable<string> Array => string.Join(", ", Array),
                                                FILETIME FileTime => Helper.ConvertToLocalDateTimeOffset(FileTime).ToString(),
                                                _ => Convert.ToString(PropertyObj)
                                            };

                                            Result.Add(Property, PropertyValue);
                                        }
                                        catch (Exception ex)
                                        {
                                            LogTracer.Log(ex, $"Could not get the property value: \"{Property}\"");
                                            Result.Add(Property, string.Empty);
                                        }
                                    }
                                }

                                Value.Add("Success", JsonSerializer.Serialize(Result));
                            }
                            else
                            {
                                Value.Add("Error", "File or directory is not found");
                            }

                            break;
                        }
                    case CommandType.SetTaskBarProgress:
                        {
                            ulong ProgressValue = Math.Min(100, Math.Max(0, Convert.ToUInt64(CommandValue["ProgressValue"])));

                            if (Helper.GetUWPWindowInformation(Package.Current.Id.FamilyName, Convert.ToUInt32((ExplorerProcess?.Id).GetValueOrDefault())) is WindowInformation Info && !Info.Handle.IsNull)
                            {
                                switch (ProgressValue)
                                {
                                    case 0:
                                        {
                                            TaskbarList.SetProgressState(Info.Handle, TaskbarButtonProgressState.Indeterminate);
                                            break;
                                        }
                                    case 100:
                                        {
                                            TaskbarList.SetProgressState(Info.Handle, TaskbarButtonProgressState.None);
                                            break;
                                        }
                                    default:
                                        {
                                            TaskbarList.SetProgressState(Info.Handle, TaskbarButtonProgressState.Normal);
                                            TaskbarList.SetProgressValue(Info.Handle, ProgressValue, 100);
                                            break;
                                        }
                                }
                            }

                            Value.Add("Success", string.Empty);

                            break;
                        }
                    case CommandType.MapToUNCPath:
                        {
                            IReadOnlyList<string> PathList = JsonSerializer.Deserialize<IReadOnlyList<string>>(CommandValue["PathList"]);
                            Dictionary<string, string> MapResult = new Dictionary<string, string>(PathList.Count);

                            foreach (string Path in PathList)
                            {
                                uint BufferSize = 128;

                                IntPtr BufferPtr = Marshal.AllocCoTaskMem((int)BufferSize);

                                try
                                {
                                    Win32Error Error = Mpr.WNetGetUniversalName(Path, Mpr.INFO_LEVEL.UNIVERSAL_NAME_INFO_LEVEL, BufferPtr, ref BufferSize);

                                    if (Error.Succeeded)
                                    {
                                        MapResult.Add(Path, Marshal.PtrToStructure<Mpr.UNIVERSAL_NAME_INFO>(BufferPtr).lpUniversalName.TrimEnd('\\'));
                                    }
                                    else if (Error == Win32Error.ERROR_MORE_DATA)
                                    {
                                        IntPtr NewBufferPtr = Marshal.AllocCoTaskMem((int)BufferSize);

                                        try
                                        {
                                            if (Mpr.WNetGetUniversalName(Path, Mpr.INFO_LEVEL.UNIVERSAL_NAME_INFO_LEVEL, NewBufferPtr, ref BufferSize).Succeeded)
                                            {
                                                MapResult.Add(Path, Marshal.PtrToStructure<Mpr.UNIVERSAL_NAME_INFO>(NewBufferPtr).lpUniversalName.TrimEnd('\\'));
                                            }
                                            else
                                            {
                                                MapResult.Add(Path, Path);
                                            }
                                        }
                                        finally
                                        {
                                            Marshal.FreeCoTaskMem(NewBufferPtr);
                                        }
                                    }
                                    else
                                    {
                                        MapResult.Add(Path, Path);
                                    }
                                }
                                finally
                                {
                                    Marshal.FreeCoTaskMem(BufferPtr);
                                }
                            }

                            Value.Add("Success", JsonSerializer.Serialize(MapResult));

                            break;
                        }
                    case CommandType.GetDirectoryMonitorHandle:
                        {
                            if ((ExplorerProcess?.Handle.CheckIfValidPtr()).GetValueOrDefault())
                            {
                                string ExecutePath = CommandValue["ExecutePath"];

                                using (Kernel32.SafeHFILE Handle = Kernel32.CreateFile(ExecutePath, Kernel32.FileAccess.FILE_LIST_DIRECTORY, FileShare.Read | FileShare.Write | FileShare.Delete, null, FileMode.Open, FileFlagsAndAttributes.FILE_FLAG_BACKUP_SEMANTICS))
                                {
                                    if (Handle.IsInvalid || Handle.IsNull)
                                    {
                                        Value.Add("Error", $"Could not access to the handle, reason: {new Win32Exception(Marshal.GetLastWin32Error()).Message}");
                                    }
                                    else
                                    {
                                        if (Kernel32.DuplicateHandle(Kernel32.GetCurrentProcess(), Handle.DangerousGetHandle(), ExplorerProcess.Handle, out IntPtr TargetHandle, default, default, Kernel32.DUPLICATE_HANDLE_OPTIONS.DUPLICATE_SAME_ACCESS))
                                        {
                                            Value.Add("Success", Convert.ToString(TargetHandle.ToInt64()));
                                        }
                                        else
                                        {
                                            Value.Add("Error", $"Could not duplicate the handle, reason: {new Win32Exception(Marshal.GetLastWin32Error()).Message}");
                                        }
                                    }
                                }
                            }

                            break;
                        }
                    case CommandType.GetNativeHandle:
                        {
                            if ((ExplorerProcess?.Handle.CheckIfValidPtr()).GetValueOrDefault())
                            {
                                string ExecutePath = CommandValue["ExecutePath"];

                                if (File.Exists(ExecutePath) || Directory.Exists(ExecutePath))
                                {
                                    AccessMode Mode = (AccessMode)Enum.Parse(typeof(AccessMode), CommandValue["AccessMode"]);
                                    OptimizeOption Option = (OptimizeOption)Enum.Parse(typeof(OptimizeOption), CommandValue["OptimizeOption"]);

                                    Kernel32.FileAccess Access = Mode switch
                                    {
                                        AccessMode.Read => Kernel32.FileAccess.FILE_GENERIC_READ,
                                        AccessMode.ReadWrite or AccessMode.Exclusive => Kernel32.FileAccess.FILE_GENERIC_READ | Kernel32.FileAccess.FILE_GENERIC_WRITE,
                                        AccessMode.Write => Kernel32.FileAccess.FILE_GENERIC_WRITE,
                                        _ => throw new NotSupportedException()
                                    };

                                    FileShare Share = Mode switch
                                    {
                                        AccessMode.Read => FileShare.ReadWrite,
                                        AccessMode.ReadWrite or AccessMode.Write => FileShare.Read,
                                        AccessMode.Exclusive => FileShare.None,
                                        _ => throw new NotSupportedException()
                                    };

                                    FileFlagsAndAttributes Flags = FileFlagsAndAttributes.FILE_ATTRIBUTE_NORMAL;

                                    if (Directory.Exists(ExecutePath))
                                    {
                                        Flags = FileFlagsAndAttributes.FILE_FLAG_BACKUP_SEMANTICS;
                                    }
                                    else
                                    {
                                        Flags = FileFlagsAndAttributes.FILE_FLAG_OVERLAPPED | Option switch
                                        {
                                            OptimizeOption.None => FileFlagsAndAttributes.FILE_ATTRIBUTE_NORMAL,
                                            OptimizeOption.Sequential => FileFlagsAndAttributes.FILE_FLAG_SEQUENTIAL_SCAN,
                                            OptimizeOption.RandomAccess => FileFlagsAndAttributes.FILE_FLAG_RANDOM_ACCESS,
                                            _ => throw new NotSupportedException()
                                        };
                                    }

                                    using (Kernel32.SafeHFILE Handle = Kernel32.CreateFile(ExecutePath, Access, Share, null, FileMode.Open, Flags))
                                    {
                                        if (Handle.IsInvalid)
                                        {
                                            Value.Add("Error", $"Could not access to the handle, reason: {new Win32Exception(Marshal.GetLastWin32Error()).Message}");
                                        }
                                        else
                                        {
                                            if (Kernel32.DuplicateHandle(Kernel32.GetCurrentProcess(), Handle.DangerousGetHandle(), ExplorerProcess.Handle, out IntPtr TargetHandle, default, default, Kernel32.DUPLICATE_HANDLE_OPTIONS.DUPLICATE_SAME_ACCESS))
                                            {
                                                Value.Add("Success", Convert.ToString(TargetHandle.ToInt64()));
                                            }
                                            else
                                            {
                                                Value.Add("Error", $"Could not duplicate the handle, reason: {new Win32Exception(Marshal.GetLastWin32Error()).Message}");
                                            }
                                        }
                                    }
                                }
                                else
                                {
                                    Value.Add("Error", "Path is not found");
                                }
                            }
                            else
                            {
                                Value.Add("Error", "Explorer process handle is not valid");
                            }

                            break;
                        }
                    case CommandType.GetUrlTargetPath:
                        {
                            string ExecutePath = CommandValue["ExecutePath"];

                            if (File.Exists(ExecutePath))
                            {
                                if (Path.GetExtension(ExecutePath).Equals(".url", StringComparison.OrdinalIgnoreCase))
                                {
                                    using (ShellItem Item = new ShellItem(ExecutePath))
                                    {
                                        Value.Add("Success", Item.Properties.GetPropertyString(Ole32.PROPERTYKEY.System.Link.TargetUrl));
                                    }
                                }
                                else
                                {
                                    string NewPath = Path.Combine(Path.GetDirectoryName(ExecutePath), $"{Path.GetFileNameWithoutExtension(ExecutePath)}.url");

                                    File.Move(ExecutePath, NewPath);

                                    using (ShellItem Item = new ShellItem(NewPath))
                                    {
                                        Value.Add("Success", Item.Properties.GetPropertyString(Ole32.PROPERTYKEY.System.Link.TargetUrl));
                                    }

                                    File.Move(NewPath, ExecutePath);
                                }
                            }
                            else
                            {
                                Value.Add("Error", "File not found");
                            }

                            break;
                        }
                    case CommandType.GetThumbnail:
                        {
                            string ExecutePath = CommandValue["ExecutePath"];

                            if (File.Exists(ExecutePath) || Directory.Exists(ExecutePath))
                            {
                                using (ShellItem Item = new ShellItem(ExecutePath))
                                using (Image Thumbnail = Item.GetImage(new Size(128, 128), ShellItemGetImageOptions.BiggerSizeOk))
                                using (Bitmap OriginBitmap = new Bitmap(Thumbnail))
                                using (MemoryStream Stream = new MemoryStream())
                                {
                                    OriginBitmap.MakeTransparent();
                                    OriginBitmap.Save(Stream, ImageFormat.Png);

                                    Value.Add("Success", JsonSerializer.Serialize(Stream.ToArray()));
                                }
                            }
                            else
                            {
                                Value.Add("Error", "File or directory not found");
                            }

                            break;
                        }
                    case CommandType.LaunchUWP:
                        {
                            string[] PathArray = JsonSerializer.Deserialize<string[]>(CommandValue["LaunchPathArray"]);

                            if (CommandValue.TryGetValue("PackageFamilyName", out string PackageFamilyName))
                            {
                                if (string.IsNullOrEmpty(PackageFamilyName))
                                {
                                    Value.Add("Error", "PackageFamilyName could not empty");
                                }
                                else
                                {
                                    if (await Helper.LaunchApplicationFromPackageFamilyNameAsync(PackageFamilyName, PathArray))
                                    {
                                        Value.Add("Success", string.Empty);
                                    }
                                    else
                                    {
                                        Value.Add("Error", "Could not launch the UWP");
                                    }
                                }
                            }
                            else if (CommandValue.TryGetValue("AppUserModelId", out string AppUserModelId))
                            {
                                if (string.IsNullOrEmpty(AppUserModelId))
                                {
                                    Value.Add("Error", "AppUserModelId could not empty");
                                }
                                else
                                {
                                    if (await Helper.LaunchApplicationFromAUMIDAsync(AppUserModelId, PathArray))
                                    {
                                        Value.Add("Success", string.Empty);
                                    }
                                    else
                                    {
                                        Value.Add("Error", "Could not launch the UWP");
                                    }
                                }
                            }

                            break;
                        }
                    case CommandType.GetMIMEContentType:
                        {
                            string ExecutePath = CommandValue["ExecutePath"];

                            Value.Add("Success", Helper.GetMIMEFromPath(ExecutePath));

                            break;
                        }
                    case CommandType.GetHiddenItemData:
                        {
                            string ExecutePath = CommandValue["ExecutePath"];

                            using (ShellItem Item = new ShellItem(ExecutePath))
                            {
                                HiddenDataPackage Package = new HiddenDataPackage
                                {
                                    DisplayType = Item.FileInfo.TypeName
                                };

                                try
                                {
                                    using (Image Thumbnail = Item.GetImage(new Size(128, 128), ShellItemGetImageOptions.BiggerSizeOk))
                                    using (Bitmap OriginBitmap = new Bitmap(Thumbnail))
                                    using (MemoryStream IconStream = new MemoryStream())
                                    {
                                        OriginBitmap.MakeTransparent();
                                        OriginBitmap.Save(IconStream, ImageFormat.Png);

                                        Package.IconData = IconStream.ToArray();
                                    }
                                }
                                catch (Exception ex)
                                {
                                    LogTracer.Log(ex, "Could not get the icon");
                                    Package.IconData = Array.Empty<byte>();
                                }

                                Value.Add("Success", JsonSerializer.Serialize(Package));
                            }

                            break;
                        }
                    case CommandType.GetTooltipText:
                        {
                            string Path = CommandValue["Path"];

                            if (File.Exists(Path) || Directory.Exists(Path))
                            {
                                try
                                {
                                    using (ShellItem Item = new ShellItem(Path))
                                    {
                                        Value.Add("Success", Item.GetToolTip(ShellItemToolTipOptions.AllowDelay));
                                    }
                                }
                                catch (Exception)
                                {
                                    Value.Add("Error", "Could not get the tooltip");
                                }
                            }
                            else
                            {
                                Value.Add("Error", "Path is not exists");
                            }

                            break;
                        }
                    case CommandType.CheckIfEverythingAvailable:
                        {
                            if (EverythingConnector.IsAvailable)
                            {
                                Value.Add("Success", string.Empty);
                            }
                            else
                            {
                                Value.Add("Error", $"Everything is not available, ErrorCode: {Enum.GetName(typeof(EverythingConnector.StateCode), EverythingConnector.GetLastErrorCode())}");
                            }

                            break;
                        }
                    case CommandType.SearchByEverything:
                        {
                            string BaseLocation = CommandValue["BaseLocation"];
                            string SearchWord = CommandValue["SearchWord"];
                            bool SearchAsRegex = Convert.ToBoolean(CommandValue["SearchAsRegex"]);
                            bool IgnoreCase = Convert.ToBoolean(CommandValue["IgnoreCase"]);

                            if (EverythingConnector.IsAvailable)
                            {
                                IEnumerable<string> SearchResult = EverythingConnector.Search(BaseLocation, SearchWord, SearchAsRegex, IgnoreCase);

                                if (SearchResult.Any())
                                {
                                    Value.Add("Success", JsonSerializer.Serialize(SearchResult));
                                }
                                else
                                {
                                    EverythingConnector.StateCode Code = EverythingConnector.GetLastErrorCode();

                                    if (Code == EverythingConnector.StateCode.OK)
                                    {
                                        Value.Add("Success", JsonSerializer.Serialize(SearchResult));
                                    }
                                    else
                                    {
                                        Value.Add("Error", $"Everything report an error, code: {Enum.GetName(typeof(EverythingConnector.StateCode), Code)}");
                                    }
                                }
                            }
                            else
                            {
                                Value.Add("Error", "Everything is not available");
                            }

                            break;
                        }
                    case CommandType.GetContextMenuItems:
                        {
                            string[] ExecutePath = JsonSerializer.Deserialize<string[]>(CommandValue["ExecutePath"]);

                            await Helper.ExecuteOnSTAThreadAsync(() =>
                            {
                                Value.Add("Success", JsonSerializer.Serialize(ContextMenu.Current.GetContextMenuItems(ExecutePath, Convert.ToBoolean(CommandValue["IncludeExtensionItem"]))));
                            });

                            break;
                        }
                    case CommandType.InvokeContextMenuItem:
                        {
                            ContextMenuPackage Package = JsonSerializer.Deserialize<ContextMenuPackage>(CommandValue["DataPackage"]);

                            await Helper.ExecuteOnSTAThreadAsync(() =>
                            {
                                if (ContextMenu.Current.InvokeVerb(Package))
                                {
                                    Value.Add("Success", string.Empty);
                                }
                                else
                                {
                                    Value.Add("Error", $"Execute Id: \"{Package.Id}\", Verb: \"{Package.Verb}\" failed");
                                }
                            });

                            break;
                        }
                    case CommandType.CreateLink:
                        {
                            LinkDataPackage Package = JsonSerializer.Deserialize<LinkDataPackage>(CommandValue["DataPackage"]);

                            string Arguments = null;

                            if ((Package.Arguments?.Length).GetValueOrDefault() > 0)
                            {
                                Arguments = string.Join(" ", Package.Arguments.Select((Para) => Para.Contains(" ") ? $"\"{Para.Trim('\"')}\"" : Para));
                            }

                            using (ShellLink Link = ShellLink.Create(StorageItemController.GenerateUniquePath(Package.LinkPath), Package.LinkTargetPath, Package.Comment, Package.WorkDirectory, Arguments))
                            {
                                Link.ShowState = (FormWindowState)Package.WindowState;
                                Link.RunAsAdministrator = Package.NeedRunAsAdmin;

                                if (Package.HotKey > 0)
                                {
                                    Link.HotKey = (((Package.HotKey >= 112 && Package.HotKey <= 135) || (Package.HotKey >= 96 && Package.HotKey <= 105)) || (Package.HotKey >= 96 && Package.HotKey <= 105)) ? (Keys)Package.HotKey : (Keys)Package.HotKey | Keys.Control | Keys.Alt;
                                }
                            }

                            Value.Add("Success", string.Empty);

                            break;
                        }
                    case CommandType.GetVariablePath:
                        {
                            string Variable = CommandValue["Variable"];

                            string Env = Environment.GetEnvironmentVariable(Variable);

                            if (string.IsNullOrEmpty(Env))
                            {
                                Value.Add("Error", "Could not found EnvironmentVariable");
                            }
                            else
                            {
                                Value.Add("Success", Env);
                            }

                            break;
                        }
                    case CommandType.GetVariablePathSuggestion:
                        {
                            string PartialVariable = CommandValue["PartialVariable"];

                            if (PartialVariable.IndexOf('%') == 0 && PartialVariable.LastIndexOf('%') == 0)
                            {
                                IEnumerable<VariableDataPackage> VariableList = Environment.GetEnvironmentVariables().Cast<DictionaryEntry>()
                                                                                                                     .Where((Pair) => Directory.Exists(Convert.ToString(Pair.Value)))
                                                                                                                     .Where((Pair) => Convert.ToString(Pair.Key).StartsWith(PartialVariable[1..], StringComparison.OrdinalIgnoreCase))
                                                                                                                     .Select((Pair) => new VariableDataPackage($"%{Pair.Key}%", Convert.ToString(Pair.Value)));
                                Value.Add("Success", JsonSerializer.Serialize(VariableList));
                            }
                            else
                            {
                                Value.Add("Error", "Unexpected Partial Environmental String");
                            }

                            break;
                        }
                    case CommandType.CreateNew:
                        {
                            string CreateNewPath = CommandValue["NewPath"];
                            string UniquePath = StorageItemController.GenerateUniquePath(CreateNewPath);

                            CreateType Type = (CreateType)Enum.Parse(typeof(CreateType), CommandValue["Type"]);

                            if (StorageItemController.CheckPermission(Path.GetDirectoryName(UniquePath) ?? UniquePath, Type == CreateType.File ? FileSystemRights.CreateFiles : FileSystemRights.CreateDirectories))
                            {
                                if (StorageItemController.Create(Type, UniquePath))
                                {
                                    Value.Add("Success", string.Empty);
                                }
                                else if (Marshal.GetLastWin32Error() == 5)
                                {
                                    IDictionary<string, string> ResultMap = await CreateNewProcessAsElevatedAndWaitForResultAsync(new ElevationCreateNewData(Type, UniquePath));

                                    foreach (KeyValuePair<string, string> Result in ResultMap)
                                    {
                                        Value.Add(Result);
                                    }
                                }
                                else
                                {
                                    Value.Add("Error_Failure", "Error happened when create a new file or directory");
                                }
                            }
                            else
                            {
                                IDictionary<string, string> ResultMap = await CreateNewProcessAsElevatedAndWaitForResultAsync(new ElevationCreateNewData(Type, UniquePath));

                                foreach (KeyValuePair<string, string> Result in ResultMap)
                                {
                                    Value.Add(Result);
                                }
                            }

                            break;
                        }
                    case CommandType.Rename:
                        {
                            string ExecutePath = CommandValue["ExecutePath"];
                            string DesireName = CommandValue["DesireName"];

                            if (File.Exists(ExecutePath) || Directory.Exists(ExecutePath))
                            {
                                if (StorageItemController.CheckCaptured(ExecutePath))
                                {
                                    Value.Add("Error_Capture", "An error occurred while renaming the files");
                                }
                                else
                                {
                                    if (StorageItemController.CheckPermission(Path.GetDirectoryName(ExecutePath) ?? ExecutePath, FileSystemRights.Modify))
                                    {
                                        string NewName = string.Empty;

                                        if (StorageItemController.Rename(ExecutePath, DesireName, (s, e) =>
                                        {
                                            NewName = e.Name;
                                        }))
                                        {
                                            Value.Add("Success", NewName);
                                        }
                                        else if (Marshal.GetLastWin32Error() == 5)
                                        {
                                            IDictionary<string, string> ResultMap = await CreateNewProcessAsElevatedAndWaitForResultAsync(new ElevationRenameData(ExecutePath, DesireName));

                                            foreach (KeyValuePair<string, string> Result in ResultMap)
                                            {
                                                Value.Add(Result);
                                            }
                                        }
                                        else
                                        {
                                            Value.Add("Error_Failure", "Error happened when rename");
                                        }
                                    }
                                    else
                                    {
                                        IDictionary<string, string> ResultMap = await CreateNewProcessAsElevatedAndWaitForResultAsync(new ElevationRenameData(ExecutePath, DesireName));

                                        foreach (KeyValuePair<string, string> Result in ResultMap)
                                        {
                                            Value.Add(Result);
                                        }
                                    }
                                }
                            }
                            else
                            {
                                Value.Add("Error_NotFound", "Path is not found");
                            }

                            break;
                        }
                    case CommandType.GetInstalledApplication:
                        {
                            string PFN = CommandValue["PackageFamilyName"];

                            InstalledApplicationPackage Pack = await Helper.GetInstalledApplicationAsync(PFN);

                            if (Pack != null)
                            {
                                Value.Add("Success", JsonSerializer.Serialize(Pack));
                            }
                            else
                            {
                                Value.Add("Error", "Could not found the package with PFN");
                            }

                            break;
                        }
                    case CommandType.GetAllInstalledApplication:
                        {
                            Value.Add("Success", JsonSerializer.Serialize(await Helper.GetInstalledApplicationAsync()));

                            break;
                        }
                    case CommandType.CheckPackageFamilyNameExist:
                        {
                            string PFN = CommandValue["PackageFamilyName"];

                            Value.Add("Success", Convert.ToString(Helper.CheckIfPackageFamilyNameExist(PFN)));

                            break;
                        }
                    case CommandType.UpdateUrl:
                        {
                            UrlDataPackage Package = JsonSerializer.Deserialize<UrlDataPackage>(CommandValue["DataPackage"]);

                            if (File.Exists(Package.UrlPath))
                            {
                                List<string> SplitList;

                                using (StreamReader Reader = new StreamReader(Package.UrlPath, true))
                                {
                                    SplitList = Reader.ReadToEnd().Split(new string[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries).ToList();

                                    string UrlLine = SplitList.FirstOrDefault((Line) => Line.StartsWith("URL=", StringComparison.OrdinalIgnoreCase));

                                    if (!string.IsNullOrEmpty(UrlLine))
                                    {
                                        SplitList.Remove(UrlLine);
                                        SplitList.Add($"URL={Package.UrlTargetPath}");
                                    }
                                }

                                using (StreamWriter Writer = new StreamWriter(Package.UrlPath, false))
                                {
                                    Writer.Write(string.Join(Environment.NewLine, SplitList));
                                }
                            }
                            else
                            {
                                Value.Add("Error", "File not found");
                            }

                            break;
                        }
                    case CommandType.UpdateLink:
                        {
                            LinkDataPackage Package = JsonSerializer.Deserialize<LinkDataPackage>(CommandValue["DataPackage"]);

                            if (File.Exists(Package.LinkPath))
                            {
                                if (Path.IsPathRooted(Package.LinkTargetPath))
                                {
                                    string Arguments = null;

                                    if ((Package.Arguments?.Length).GetValueOrDefault() > 0)
                                    {
                                        Arguments = string.Join(" ", Package.Arguments.Select((Para) => Para.Contains(" ") ? $"\"{Para.Trim('\"')}\"" : Para));
                                    }

                                    using (ShellLink Link = new ShellLink(Package.LinkPath))
                                    {
                                        Link.TargetPath = Package.LinkTargetPath;
                                        Link.WorkingDirectory = Package.WorkDirectory;
                                        Link.ShowState = (FormWindowState)Package.WindowState;
                                        Link.RunAsAdministrator = Package.NeedRunAsAdmin;
                                        Link.Description = Package.Comment;
                                        Link.Arguments = Arguments;

                                        if (Package.HotKey > 0)
                                        {
                                            Link.HotKey = ((Package.HotKey >= 112 && Package.HotKey <= 135) || (Package.HotKey >= 96 && Package.HotKey <= 105) || (Package.HotKey >= 96 && Package.HotKey <= 105)) ? (Keys)Package.HotKey : (Keys)Package.HotKey | Keys.Control | Keys.Alt;
                                        }
                                        else
                                        {
                                            Link.HotKey = Keys.None;
                                        }
                                    }
                                }
                                else if (Helper.CheckIfPackageFamilyNameExist(Package.LinkTargetPath))
                                {
                                    using (ShellLink Link = new ShellLink(Package.LinkPath))
                                    {
                                        Link.ShowState = (FormWindowState)Package.WindowState;
                                        Link.Description = Package.Comment;

                                        if (Package.HotKey > 0)
                                        {
                                            Link.HotKey = ((Package.HotKey >= 112 && Package.HotKey <= 135) || (Package.HotKey >= 96 && Package.HotKey <= 105)) ? (Keys)Package.HotKey : (Keys)Package.HotKey | Keys.Control | Keys.Alt;
                                        }
                                        else
                                        {
                                            Link.HotKey = Keys.None;
                                        }
                                    }
                                }

                                Value.Add("Success", string.Empty);
                            }
                            else
                            {
                                Value.Add("Error", "Path is not found");
                            }

                            break;
                        }
                    case CommandType.SetFileAttribute:
                        {
                            string ExecutePath = CommandValue["ExecutePath"];
                            KeyValuePair<ModifyAttributeAction, System.IO.FileAttributes>[] AttributeGourp = JsonSerializer.Deserialize<KeyValuePair<ModifyAttributeAction, System.IO.FileAttributes>[]>(CommandValue["Attributes"]);

                            if (File.Exists(ExecutePath))
                            {
                                FileInfo File = new FileInfo(ExecutePath);

                                foreach (KeyValuePair<ModifyAttributeAction, System.IO.FileAttributes> AttributePair in AttributeGourp)
                                {
                                    if (AttributePair.Key == ModifyAttributeAction.Add)
                                    {
                                        File.Attributes |= AttributePair.Value;
                                    }
                                    else
                                    {
                                        File.Attributes &= ~AttributePair.Value;
                                    }
                                }

                                Value.Add("Success", string.Empty);
                            }
                            else if (Directory.Exists(ExecutePath))
                            {
                                DirectoryInfo Dir = new DirectoryInfo(ExecutePath);

                                foreach (KeyValuePair<ModifyAttributeAction, System.IO.FileAttributes> AttributePair in AttributeGourp)
                                {
                                    if (AttributePair.Key == ModifyAttributeAction.Add)
                                    {
                                        if (AttributePair.Value == System.IO.FileAttributes.ReadOnly)
                                        {
                                            foreach (string SubPath in Directory.GetFiles(ExecutePath, "*", SearchOption.AllDirectories))
                                            {
                                                new FileInfo(SubPath).Attributes |= AttributePair.Value;
                                            }
                                        }
                                        else
                                        {
                                            Dir.Attributes |= AttributePair.Value;
                                        }
                                    }
                                    else
                                    {
                                        if (AttributePair.Value == System.IO.FileAttributes.ReadOnly)
                                        {
                                            foreach (string SubPath in Directory.GetFiles(ExecutePath, "*", SearchOption.AllDirectories))
                                            {
                                                new FileInfo(SubPath).Attributes &= ~AttributePair.Value;
                                            }
                                        }
                                        else
                                        {
                                            Dir.Attributes &= ~AttributePair.Value;
                                        }
                                    }
                                }

                                Value.Add("Success", string.Empty);
                            }
                            else
                            {
                                Value.Add("Error", "Path not found");
                            }

                            break;
                        }
                    case CommandType.GetUrlData:
                        {
                            string ExecutePath = CommandValue["ExecutePath"];

                            if (File.Exists(ExecutePath))
                            {
                                using (ShellItem Item = new ShellItem(ExecutePath))
                                {
                                    string UrlPath = Item.Properties.GetPropertyString(Ole32.PROPERTYKEY.System.Link.TargetUrl);

                                    UrlDataPackage Package = new UrlDataPackage
                                    {
                                        UrlPath = ExecutePath,
                                        UrlTargetPath = UrlPath
                                    };

                                    try
                                    {
                                        string DefaultProgramPath = ExtensionAssociation.GetDefaultProgramPathFromExtension(".html");

                                        using (ShellItem DefaultProgramItem = new ShellItem(DefaultProgramPath))
                                        using (Image IconImage = DefaultProgramItem.GetImage(new Size(150, 150), ShellItemGetImageOptions.BiggerSizeOk | ShellItemGetImageOptions.ResizeToFit | ShellItemGetImageOptions.IconOnly))
                                        using (MemoryStream IconStream = new MemoryStream())
                                        using (Bitmap TempBitmap = new Bitmap(IconImage))
                                        {
                                            TempBitmap.MakeTransparent();
                                            TempBitmap.Save(IconStream, ImageFormat.Png);

                                            Package.IconData = IconStream.ToArray();
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        LogTracer.Log(ex, $"Could not get the icon of \"{ExecutePath}\"");
                                        Package.IconData = Array.Empty<byte>();
                                    }

                                    Value.Add("Success", JsonSerializer.Serialize(Package));
                                }
                            }
                            else
                            {
                                Value.Add("Error", "File not found");
                            }

                            break;
                        }
                    case CommandType.GetLinkData:
                        {
                            string ExecutePath = CommandValue["ExecutePath"];

                            if (File.Exists(ExecutePath))
                            {
                                StringBuilder ProductCode = new StringBuilder(39);
                                StringBuilder ComponentCode = new StringBuilder(39);

                                if (Msi.MsiGetShortcutTarget(ExecutePath, ProductCode, szComponentCode: ComponentCode).Succeeded)
                                {
                                    uint Length = 0;

                                    StringBuilder ActualPathBuilder = new StringBuilder();

                                    Msi.INSTALLSTATE State = Msi.MsiGetComponentPath(ProductCode.ToString(), ComponentCode.ToString(), ActualPathBuilder, ref Length);

                                    if (State == Msi.INSTALLSTATE.INSTALLSTATE_LOCAL || State == Msi.INSTALLSTATE.INSTALLSTATE_SOURCE)
                                    {
                                        string ActualPath = ActualPathBuilder.ToString();

                                        foreach (Match Var in Regex.Matches(ActualPath, @"(?<=(%))[\s\S]+(?=(%))"))
                                        {
                                            ActualPath = ActualPath.Replace($"%{Var.Value}%", Environment.GetEnvironmentVariable(Var.Value));
                                        }

                                        LinkDataPackage Package = new LinkDataPackage
                                        {
                                            LinkPath = ExecutePath,
                                            LinkTargetPath = ActualPath
                                        };

                                        try
                                        {
                                            using (ShellItem Item = new ShellItem(ActualPath))
                                            using (Image IconImage = Item.GetImage(new Size(150, 150), ShellItemGetImageOptions.BiggerSizeOk | ShellItemGetImageOptions.ResizeToFit))
                                            using (MemoryStream IconStream = new MemoryStream())
                                            using (Bitmap TempBitmap = new Bitmap(IconImage))
                                            {
                                                TempBitmap.MakeTransparent();
                                                TempBitmap.Save(IconStream, ImageFormat.Png);

                                                Package.IconData = IconStream.ToArray();
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            LogTracer.Log(ex, "Could not get the icon");
                                            Package.IconData = Array.Empty<byte>();
                                        }

                                        Value.Add("Success", JsonSerializer.Serialize(Package));
                                    }
                                    else
                                    {
                                        Value.Add("Error", "Lnk file could not be analysis by MsiGetShortcutTarget");
                                    }
                                }
                                else
                                {
                                    using (ShellLink Link = new ShellLink(ExecutePath))
                                    {
                                        LinkDataPackage Package = new LinkDataPackage
                                        {
                                            LinkPath = ExecutePath,
                                            WorkDirectory = Link.WorkingDirectory,
                                            WindowState = (WindowState)Enum.Parse(typeof(WindowState), Enum.GetName(typeof(FormWindowState), Link.ShowState)),
                                            HotKey = (int)Link.HotKey,
                                            NeedRunAsAdmin = Link.RunAsAdministrator,
                                            Comment = Link.Description,
                                            Arguments = Regex.Matches(Link.Arguments, "[^ \"]+|\"[^\"]*\"").Cast<Match>().Select((Mat) => Mat.Value).ToArray()
                                        };

                                        if (string.IsNullOrEmpty(Link.TargetPath))
                                        {
                                            string PackageFamilyName = Helper.GetPackageFamilyNameFromUWPShellLink(ExecutePath);

                                            if (string.IsNullOrEmpty(PackageFamilyName))
                                            {
                                                Value.Add("Error", "TargetPath is invalid");
                                            }
                                            else
                                            {
                                                Package.LinkTargetPath = PackageFamilyName;
                                                Package.IconData = await Helper.GetIconDataFromPackageFamilyNameAsync(PackageFamilyName);
                                            }
                                        }
                                        else
                                        {
                                            string ActualPath = Link.TargetPath;

                                            foreach (Match Var in Regex.Matches(ActualPath, @"(?<=(%))[\s\S]+(?=(%))"))
                                            {
                                                ActualPath = ActualPath.Replace($"%{Var.Value}%", Environment.GetEnvironmentVariable(Var.Value));
                                            }

                                            Package.LinkTargetPath = ActualPath;

                                            try
                                            {
                                                using (Image IconImage = Link.GetImage(new Size(120, 120), ShellItemGetImageOptions.BiggerSizeOk | ShellItemGetImageOptions.ScaleUp))
                                                using (MemoryStream IconStream = new MemoryStream())
                                                using (Bitmap TempBitmap = new Bitmap(IconImage))
                                                {
                                                    TempBitmap.MakeTransparent();
                                                    TempBitmap.Save(IconStream, ImageFormat.Png);

                                                    Package.IconData = IconStream.ToArray();
                                                }
                                            }
                                            catch (Exception ex)
                                            {
                                                LogTracer.Log(ex, "Could not get the icon");
                                                Package.IconData = Array.Empty<byte>();
                                            }
                                        }

                                        Value.Add("Success", JsonSerializer.Serialize(Package));
                                    }
                                }
                            }
                            else
                            {
                                Value.Add("Error", "File is not exist");
                            }

                            break;
                        }
                    case CommandType.InterceptFolder:
                        {
                            string AliasLocation = null;

                            try
                            {
                                using (Process Pro = Process.Start(new ProcessStartInfo
                                {
                                    FileName = "powershell.exe",
                                    Arguments = "-Command \"Get-Command RX-Explorer | Format-List -Property Source\"",
                                    CreateNoWindow = true,
                                    RedirectStandardOutput = true,
                                    UseShellExecute = false
                                }))
                                {
                                    try
                                    {
                                        string OutputString = Pro.StandardOutput.ReadToEnd();

                                        if (!string.IsNullOrWhiteSpace(OutputString))
                                        {
                                            string Path = OutputString.Replace(Environment.NewLine, string.Empty).Split(new string[] { " " }, StringSplitOptions.RemoveEmptyEntries).LastOrDefault();

                                            if (File.Exists(Path))
                                            {
                                                AliasLocation = Path;
                                            }
                                        }
                                    }
                                    finally
                                    {
                                        if (!Pro.WaitForExit(1000))
                                        {
                                            Pro.Kill();
                                        }
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                LogTracer.Log(ex, "Could not get alias location by Powershell");
                            }

                            if (string.IsNullOrEmpty(AliasLocation))
                            {
                                string[] EnvironmentVariables = Environment.GetEnvironmentVariable("Path", EnvironmentVariableTarget.User)
                                                                           .Split(new string[] { ";" }, StringSplitOptions.RemoveEmptyEntries)
                                                                           .Concat(Environment.GetEnvironmentVariable("Path", EnvironmentVariableTarget.Machine)
                                                                                              .Split(new string[] { ";" }, StringSplitOptions.RemoveEmptyEntries))
                                                                           .Distinct()
                                                                           .ToArray();

                                if (EnvironmentVariables.Where((Var) => Var.Contains("WindowsApps")).Select((Var) => Path.Combine(Var, "RX-Explorer.exe")).FirstOrDefault((Path) => File.Exists(Path)) is string Location)
                                {
                                    AliasLocation = Location;
                                }
                                else
                                {
                                    string AppDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

                                    if (!string.IsNullOrEmpty(AppDataPath) && Directory.Exists(AppDataPath))
                                    {
                                        string WindowsAppsPath = Path.Combine(AppDataPath, "Microsoft", "WindowsApps");

                                        if (Directory.Exists(WindowsAppsPath))
                                        {
                                            string RXPath = Path.Combine(WindowsAppsPath, "RX-Explorer.exe");

                                            if (File.Exists(RXPath))
                                            {
                                                AliasLocation = RXPath;
                                            }
                                        }
                                    }
                                }
                            }

                            if (!string.IsNullOrEmpty(AliasLocation))
                            {
                                string TempFilePath = Path.Combine(Path.GetTempPath(), @$"{Guid.NewGuid()}.reg");

                                try
                                {
                                    using (FileStream TempFileStream = File.Open(TempFilePath, FileMode.OpenOrCreate, FileAccess.Write, FileShare.Read))
                                    using (FileStream RegStream = File.Open(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"RegFiles\Intercept_Folder.reg"), FileMode.Open, FileAccess.Read, FileShare.Read))
                                    using (StreamReader Reader = new StreamReader(RegStream))
                                    {
                                        string Content = Reader.ReadToEnd();

                                        using (StreamWriter Writer = new StreamWriter(TempFileStream, Encoding.Unicode))
                                        {
                                            Writer.Write(Content.Replace("<FillActualAliasPathInHere>", $"{AliasLocation.Replace(@"\", @"\\")} %1"));
                                        }
                                    }

                                    using (Process RegisterProcess = Process.Start(new ProcessStartInfo
                                    {
                                        FileName = "regedit.exe",
                                        Verb = "runas",
                                        CreateNoWindow = true,
                                        UseShellExecute = true,
                                        Arguments = $"/s \"{TempFilePath}\"",
                                    }))
                                    {
                                        RegisterProcess.WaitForExit();
                                    }
                                }
                                finally
                                {
                                    if (File.Exists(TempFilePath))
                                    {
                                        File.Delete(TempFilePath);
                                    }
                                }

                                bool IsRegistryCheckingSuccess = true;

                                try
                                {
                                    using (RegistryKey Key = Registry.ClassesRoot.OpenSubKey("Directory", false)?.OpenSubKey("shell", false)?.OpenSubKey("open", false)?.OpenSubKey("command", false))
                                    {
                                        if (Key != null)
                                        {
                                            if (!Convert.ToString(Key.GetValue(string.Empty)).Equals($"{AliasLocation} %1", StringComparison.OrdinalIgnoreCase))
                                            {
                                                IsRegistryCheckingSuccess = false;
                                            }
                                        }
                                    }

                                    using (RegistryKey Key = Registry.ClassesRoot.OpenSubKey("Drive", false)?.OpenSubKey("shell", false)?.OpenSubKey("open", false)?.OpenSubKey("command", false))
                                    {
                                        if (Key != null)
                                        {
                                            if (!Convert.ToString(Key.GetValue(string.Empty)).Equals($"{AliasLocation} %1", StringComparison.OrdinalIgnoreCase))
                                            {
                                                IsRegistryCheckingSuccess = false;
                                            }
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    LogTracer.Log(ex, "Registry checking failed");
                                }


                                if (IsRegistryCheckingSuccess)
                                {
                                    Value.Add("Success", string.Empty);
                                }
                                else
                                {
                                    Value.Add("Error", "Registry checking failed");
                                }
                            }
                            else
                            {
                                Value.Add("Error", "Alias file is not exists");
                            }

                            break;
                        }
                    case CommandType.InterceptWinE:
                        {
                            string AliasLocation = null;

                            try
                            {
                                using (Process Pro = Process.Start(new ProcessStartInfo
                                {
                                    FileName = "powershell.exe",
                                    Arguments = "-Command \"Get-Command RX-Explorer | Format-List -Property Source\"",
                                    CreateNoWindow = true,
                                    RedirectStandardOutput = true,
                                    UseShellExecute = false
                                }))
                                {
                                    try
                                    {
                                        string OutputString = Pro.StandardOutput.ReadToEnd();

                                        if (!string.IsNullOrWhiteSpace(OutputString))
                                        {
                                            string Path = OutputString.Replace(Environment.NewLine, string.Empty).Split(new string[] { " " }, StringSplitOptions.RemoveEmptyEntries).LastOrDefault();

                                            if (File.Exists(Path))
                                            {
                                                AliasLocation = Path;
                                            }
                                        }
                                    }
                                    finally
                                    {
                                        if (!Pro.WaitForExit(1000))
                                        {
                                            Pro.Kill();
                                        }
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                LogTracer.Log(ex, "Could not get alias location by Powershell");
                            }

                            if (string.IsNullOrEmpty(AliasLocation))
                            {
                                string[] EnvironmentVariables = Environment.GetEnvironmentVariable("Path", EnvironmentVariableTarget.User)
                                                                           .Split(new string[] { ";" }, StringSplitOptions.RemoveEmptyEntries)
                                                                           .Concat(Environment.GetEnvironmentVariable("Path", EnvironmentVariableTarget.Machine)
                                                                                              .Split(new string[] { ";" }, StringSplitOptions.RemoveEmptyEntries))
                                                                           .Distinct()
                                                                           .ToArray();

                                if (EnvironmentVariables.Where((Var) => Var.Contains("WindowsApps")).Select((Var) => Path.Combine(Var, "RX-Explorer.exe")).FirstOrDefault((Path) => File.Exists(Path)) is string Location)
                                {
                                    AliasLocation = Location;
                                }
                                else
                                {
                                    string AppDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

                                    if (!string.IsNullOrEmpty(AppDataPath) && Directory.Exists(AppDataPath))
                                    {
                                        string WindowsAppsPath = Path.Combine(AppDataPath, "Microsoft", "WindowsApps");

                                        if (Directory.Exists(WindowsAppsPath))
                                        {
                                            string RXPath = Path.Combine(WindowsAppsPath, "RX-Explorer.exe");

                                            if (File.Exists(RXPath))
                                            {
                                                AliasLocation = RXPath;
                                            }
                                        }
                                    }
                                }
                            }

                            if (!string.IsNullOrEmpty(AliasLocation))
                            {
                                string TempFilePath = Path.Combine(Path.GetTempPath(), @$"{Guid.NewGuid()}.reg");

                                try
                                {
                                    using (FileStream TempFileStream = File.Open(TempFilePath, FileMode.OpenOrCreate, FileAccess.Write, FileShare.Read))
                                    using (FileStream RegStream = File.Open(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"RegFiles\Intercept_WIN_E.reg"), FileMode.Open, FileAccess.Read, FileShare.Read))
                                    using (StreamReader Reader = new StreamReader(RegStream))
                                    {
                                        string Content = Reader.ReadToEnd();

                                        using (StreamWriter Writer = new StreamWriter(TempFileStream, Encoding.Unicode))
                                        {
                                            Writer.Write(Content.Replace("<FillActualAliasPathInHere>", $"{AliasLocation.Replace(@"\", @"\\")} %1"));
                                        }
                                    }

                                    using (Process RegisterProcess = Process.Start(new ProcessStartInfo
                                    {
                                        FileName = "regedit.exe",
                                        Verb = "runas",
                                        CreateNoWindow = true,
                                        UseShellExecute = true,
                                        Arguments = $"/s \"{TempFilePath}\"",
                                    }))
                                    {
                                        RegisterProcess.WaitForExit();
                                    }
                                }
                                finally
                                {
                                    if (File.Exists(TempFilePath))
                                    {
                                        File.Delete(TempFilePath);
                                    }
                                }

                                bool IsRegistryCheckingSuccess = true;

                                try
                                {
                                    using (RegistryKey Key = Registry.ClassesRoot.OpenSubKey("Folder", false)?.OpenSubKey("shell", false)?.OpenSubKey("opennewwindow", false)?.OpenSubKey("command", false))
                                    {
                                        if (Key != null)
                                        {
                                            if (!Convert.ToString(Key.GetValue(string.Empty)).Equals($"{AliasLocation} %1", StringComparison.OrdinalIgnoreCase) || Key.GetValue("DelegateExecute") != null)
                                            {
                                                IsRegistryCheckingSuccess = false;
                                            }
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    LogTracer.Log(ex, "Registry checking failed");
                                }

                                if (IsRegistryCheckingSuccess)
                                {
                                    Value.Add("Success", string.Empty);
                                }
                                else
                                {
                                    Value.Add("Error", "Registry checking failed");
                                }
                            }
                            else
                            {
                                Value.Add("Error", "Alias file is not exists");
                            }

                            break;
                        }
                    case CommandType.RestoreFolderInterception:
                        {
                            using (Process RegisterProcess = Process.Start(new ProcessStartInfo
                            {
                                FileName = "regedit.exe",
                                Verb = "runas",
                                CreateNoWindow = true,
                                UseShellExecute = true,
                                Arguments = $"/s \"{Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"RegFiles\Restore_Folder.reg")}\"",
                            }))
                            {
                                RegisterProcess.WaitForExit();
                            }

                            bool IsRegistryCheckingSuccess = true;

                            try
                            {
                                using (RegistryKey Key = Registry.ClassesRoot.OpenSubKey("Folder", false)?.OpenSubKey("Directory", false)?.OpenSubKey("open", false)?.OpenSubKey("command", false))
                                {
                                    if (Key != null)
                                    {
                                        if (Convert.ToString(Key.GetValue("DelegateExecute")) != "{11dbb47c-a525-400b-9e80-a54615a090c0}" || !string.IsNullOrEmpty(Convert.ToString(Key.GetValue(string.Empty))))
                                        {
                                            IsRegistryCheckingSuccess = false;
                                        }
                                    }
                                }

                                using (RegistryKey Key = Registry.ClassesRoot.OpenSubKey("Drive", false)?.OpenSubKey("shell", false)?.OpenSubKey("open", false)?.OpenSubKey("command", false))
                                {
                                    if (Key != null)
                                    {
                                        if (!string.IsNullOrEmpty(Convert.ToString(Key.GetValue(string.Empty))))
                                        {
                                            IsRegistryCheckingSuccess = false;
                                        }
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                LogTracer.Log(ex, "Registry checking failed");
                            }

                            if (IsRegistryCheckingSuccess)
                            {
                                Value.Add("Success", string.Empty);
                            }
                            else
                            {
                                Value.Add("Error", "Registry checking failed");
                            }

                            break;
                        }
                    case CommandType.RestoreWinEInterception:
                        {
                            using (Process RegisterProcess = Process.Start(new ProcessStartInfo
                            {
                                FileName = "regedit.exe",
                                Verb = "runas",
                                CreateNoWindow = true,
                                UseShellExecute = true,
                                Arguments = $"/s \"{Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"RegFiles\Restore_WIN_E.reg")}\"",
                            }))
                            {
                                RegisterProcess.WaitForExit();
                            }

                            bool IsRegistryCheckingSuccess = true;

                            try
                            {
                                using (RegistryKey Key = Registry.ClassesRoot.OpenSubKey("Folder", false)?.OpenSubKey("shell", false)?.OpenSubKey("opennewwindow", false)?.OpenSubKey("command", false))
                                {
                                    if (Key != null)
                                    {
                                        if (Convert.ToString(Key.GetValue("DelegateExecute")) != "{11dbb47c-a525-400b-9e80-a54615a090c0}" || !string.IsNullOrEmpty(Convert.ToString(Key.GetValue(string.Empty))))
                                        {
                                            IsRegistryCheckingSuccess = false;
                                        }
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                LogTracer.Log(ex, "Registry checking failed");
                            }

                            if (IsRegistryCheckingSuccess)
                            {
                                Value.Add("Success", string.Empty);
                            }
                            else
                            {
                                Value.Add("Error", "Registry checking failed");
                            }

                            break;
                        }
                    case CommandType.ToggleQuicklook:
                        {
                            string ExecutePath = CommandValue["ExecutePath"];

                            if (!string.IsNullOrEmpty(ExecutePath))
                            {
                                QuicklookConnector.ToggleQuicklook(ExecutePath);
                            }

                            break;
                        }
                    case CommandType.SwitchQuicklook:
                        {
                            string ExecutePath = CommandValue["ExecutePath"];

                            if (!string.IsNullOrEmpty(ExecutePath))
                            {
                                QuicklookConnector.SwitchQuicklook(ExecutePath);
                            }

                            break;
                        }
                    case CommandType.Check_Quicklook:
                        {
                            Value.Add("Check_QuicklookIsAvaliable_Result", Convert.ToString(QuicklookConnector.CheckQuicklookIsAvaliable()));

                            break;
                        }
                    case CommandType.Get_Association:
                        {
                            string Path = CommandValue["ExecutePath"];

                            Value.Add("Associate_Result", JsonSerializer.Serialize(ExtensionAssociation.GetAllAssociateProgramPath(Path)));

                            break;
                        }
                    case CommandType.Default_Association:
                        {
                            string Path = CommandValue["ExecutePath"];

                            Value.Add("Success", ExtensionAssociation.GetDefaultProgramPathRelated(Path));

                            break;
                        }
                    case CommandType.Get_RecycleBinItems:
                        {
                            string RecycleItemResult = JsonSerializer.Serialize(RecycleBinController.GetRecycleItems());

                            if (string.IsNullOrEmpty(RecycleItemResult))
                            {
                                Value.Add("Error", "Could not get recycle items");
                            }
                            else
                            {
                                Value.Add("RecycleBinItems_Json_Result", RecycleItemResult);
                            }

                            break;
                        }
                    case CommandType.EmptyRecycleBin:
                        {
                            Value.Add("RecycleBinItems_Clear_Result", Convert.ToString(RecycleBinController.EmptyRecycleBin()));

                            break;
                        }
                    case CommandType.Restore_RecycleItem:
                        {
                            string[] PathList = JsonSerializer.Deserialize<string[]>(CommandValue["ExecutePath"]);

                            Value.Add("Restore_Result", Convert.ToString(RecycleBinController.Restore(PathList)));

                            break;
                        }
                    case CommandType.Delete_RecycleItem:
                        {
                            string Path = CommandValue["ExecutePath"];

                            Value.Add("Delete_Result", Convert.ToString(RecycleBinController.Delete(Path)));

                            break;
                        }
                    case CommandType.EjectUSB:
                        {
                            string Path = CommandValue["ExecutePath"];

                            if (string.IsNullOrEmpty(Path))
                            {
                                Value.Add("EjectResult", Convert.ToString(false));
                            }
                            else
                            {
                                Value.Add("EjectResult", Convert.ToString(USBController.EjectDevice(Path)));
                            }

                            break;
                        }
                    case CommandType.UnlockOccupy:
                        {
                            string Path = CommandValue["ExecutePath"];
                            bool ForceClose = Convert.ToBoolean(CommandValue["ForceClose"]);

                            if (File.Exists(Path))
                            {
                                if (StorageItemController.CheckCaptured(Path))
                                {
                                    IReadOnlyList<Process> LockingProcesses = StorageItemController.GetLockingProcesses(Path);

                                    try
                                    {
                                        foreach (Process Pro in LockingProcesses)
                                        {
                                            if (ForceClose || !Pro.CloseMainWindow())
                                            {
                                                Pro.Kill();
                                            }
                                        }

                                        Value.Add("Success", string.Empty);
                                    }
                                    catch (Exception ex)
                                    {
                                        Value.Add("Error_Failure", $"Unoccupied failed, reason: {ex.Message}");
                                    }
                                    finally
                                    {
                                        foreach (Process Pro in LockingProcesses)
                                        {
                                            try
                                            {
                                                if (!ForceClose)
                                                {
                                                    Pro.WaitForExit();
                                                }

                                                Pro.Dispose();
                                            }
                                            catch (Exception ex)
                                            {
                                                LogTracer.Log(ex, "Process is no longer running");
                                            }
                                        }
                                    }
                                }
                                else
                                {
                                    Value.Add("Error_NotOccupy", "The file is not occupied");
                                }
                            }
                            else
                            {
                                Value.Add("Error_NotFoundOrNotFile", "Path is not a file");
                            }

                            break;
                        }
                    case CommandType.Copy:
                        {
                            string SourcePathJson = CommandValue["SourcePath"];
                            string DestinationPath = CommandValue["DestinationPath"];

                            CollisionOptions Option = (CollisionOptions)Enum.Parse(typeof(CollisionOptions), CommandValue["CollisionOptions"]);

                            List<string> SourcePathList = JsonSerializer.Deserialize<List<string>>(SourcePathJson);
                            List<string> OperationRecordList = new List<string>();

                            if (SourcePathList.All((Item) => Directory.Exists(Item) || File.Exists(Item)))
                            {
                                if (StorageItemController.CheckPermission(DestinationPath, FileSystemRights.Modify))
                                {
                                    try
                                    {
                                        if (StorageItemController.Copy(SourcePathList, DestinationPath, Option, (s, e) =>
                                        {
                                            if (CurrentTaskCancellation.IsCancellationRequested)
                                            {
                                                throw new COMException(null, HRESULT.E_ABORT);
                                            }

                                            PipeProgressWriterController?.SendData(Convert.ToString(e.ProgressPercentage));
                                        },
                                        PostCopyEvent: (se, arg) =>
                                        {
                                            if (arg.Result == HRESULT.S_OK)
                                            {
                                                if (arg.DestItem == null || string.IsNullOrEmpty(arg.Name))
                                                {
                                                    OperationRecordList.Add($"{arg.SourceItem.FileSystemPath}||Copy||{Path.Combine(arg.DestFolder.FileSystemPath, arg.SourceItem.Name)}");
                                                }
                                                else
                                                {
                                                    OperationRecordList.Add($"{arg.SourceItem.FileSystemPath}||Copy||{Path.Combine(arg.DestFolder.FileSystemPath, arg.Name)}");
                                                }
                                            }
                                            else if (arg.Result == HRESULT.COPYENGINE_S_USER_IGNORED || arg.Result == HRESULT.COPYENGINE_E_USER_CANCELLED)
                                            {
                                                Value.Add("Error_UserCancel", "User stop the operation");
                                            }
                                        }))
                                        {
                                            if (!Value.ContainsKey("Error_UserCancel"))
                                            {
                                                Value.Add("Success", JsonSerializer.Serialize(OperationRecordList));
                                            }
                                        }
                                        else if (Marshal.GetLastWin32Error() == 5)
                                        {
                                            IDictionary<string, string> ResultMap = await CreateNewProcessAsElevatedAndWaitForResultAsync(new ElevationCopyData(SourcePathList, DestinationPath, Option));

                                            foreach (KeyValuePair<string, string> Result in ResultMap)
                                            {
                                                Value.Add(Result);
                                            }
                                        }
                                        else if (!Value.ContainsKey("Error_UserCancel"))
                                        {
                                            Value.Add("Error_Failure", "An error occurred while copying the files");
                                        }
                                    }
                                    catch (Exception ex) when (ex is COMException or OperationCanceledException)
                                    {
                                        Value.Add("Error_Cancelled", "Operation is cancelled");
                                    }
                                }
                                else
                                {
                                    IDictionary<string, string> ResultMap = await CreateNewProcessAsElevatedAndWaitForResultAsync(new ElevationCopyData(SourcePathList, DestinationPath, Option));

                                    foreach (KeyValuePair<string, string> Result in ResultMap)
                                    {
                                        Value.Add(Result);
                                    }
                                }
                            }
                            else
                            {
                                Value.Add("Error_NotFound", $"One of path in \"{nameof(SourcePathList)}\" is not a file or directory");
                            }

                            break;
                        }
                    case CommandType.Move:
                        {
                            string SourcePathJson = CommandValue["SourcePath"];
                            string DestinationPath = CommandValue["DestinationPath"];

                            CollisionOptions Option = (CollisionOptions)Enum.Parse(typeof(CollisionOptions), CommandValue["CollisionOptions"]);

                            Dictionary<string, string> SourcePathList = JsonSerializer.Deserialize<Dictionary<string, string>>(SourcePathJson);
                            List<string> OperationRecordList = new List<string>();

                            if (SourcePathList.Keys.All((Item) => Directory.Exists(Item) || File.Exists(Item)))
                            {
                                if (SourcePathList.Keys.Any((Item) => StorageItemController.CheckCaptured(Item)))
                                {
                                    Value.Add("Error_Capture", "An error occurred while moving the folder");
                                }
                                else
                                {
                                    if (StorageItemController.CheckPermission(DestinationPath, FileSystemRights.Modify)
                                        && SourcePathList.Keys.All((Path) => StorageItemController.CheckPermission(System.IO.Path.GetDirectoryName(Path) ?? Path, FileSystemRights.Modify)))
                                    {
                                        try
                                        {
                                            if (StorageItemController.Move(SourcePathList, DestinationPath, Option, (s, e) =>
                                            {
                                                if (CurrentTaskCancellation.IsCancellationRequested)
                                                {
                                                    throw new COMException(null, HRESULT.E_ABORT);
                                                }

                                                PipeProgressWriterController?.SendData(Convert.ToString(e.ProgressPercentage));
                                            },
                                            PostMoveEvent: (se, arg) =>
                                            {
                                                if (arg.Result == HRESULT.COPYENGINE_S_DONT_PROCESS_CHILDREN)
                                                {
                                                    if (arg.DestItem == null || string.IsNullOrEmpty(arg.Name))
                                                    {
                                                        OperationRecordList.Add($"{arg.SourceItem.FileSystemPath}||Move||{Path.Combine(arg.DestFolder.FileSystemPath, arg.SourceItem.Name)}");
                                                    }
                                                    else
                                                    {
                                                        OperationRecordList.Add($"{arg.SourceItem.FileSystemPath}||Move||{Path.Combine(arg.DestFolder.FileSystemPath, arg.Name)}");
                                                    }
                                                }
                                                else if (arg.Result == HRESULT.COPYENGINE_S_USER_IGNORED || arg.Result == HRESULT.COPYENGINE_E_USER_CANCELLED)
                                                {
                                                    Value.Add("Error_UserCancel", "User stop the operation");
                                                }
                                            }))
                                            {
                                                if (!Value.ContainsKey("Error_UserCancel"))
                                                {
                                                    if (SourcePathList.Keys.All((Item) => !Directory.Exists(Item) && !File.Exists(Item)))
                                                    {
                                                        Value.Add("Success", JsonSerializer.Serialize(OperationRecordList));
                                                    }
                                                    else
                                                    {
                                                        Value.Add("Error_Capture", "An error occurred while moving the files");
                                                    }
                                                }
                                            }
                                            else if (Marshal.GetLastWin32Error() == 5)
                                            {
                                                IDictionary<string, string> ResultMap = await CreateNewProcessAsElevatedAndWaitForResultAsync(new ElevationMoveData(SourcePathList, DestinationPath, Option));

                                                foreach (KeyValuePair<string, string> Result in ResultMap)
                                                {
                                                    Value.Add(Result);
                                                }
                                            }
                                            else if (!Value.ContainsKey("Error_UserCancel"))
                                            {
                                                Value.Add("Error_Failure", "An error occurred while moving the files");
                                            }
                                        }
                                        catch (Exception ex) when (ex is COMException or OperationCanceledException)
                                        {
                                            Value.Add("Error_Cancelled", "The specified file could not be moved");
                                        }
                                    }
                                    else
                                    {
                                        IDictionary<string, string> ResultMap = await CreateNewProcessAsElevatedAndWaitForResultAsync(new ElevationMoveData(SourcePathList, DestinationPath, Option));

                                        foreach (KeyValuePair<string, string> Result in ResultMap)
                                        {
                                            Value.Add(Result);
                                        }
                                    }
                                }
                            }
                            else
                            {
                                Value.Add("Error_NotFound", $"One of path in \"{nameof(SourcePathList)}\" is not a file or directory");
                            }

                            break;
                        }
                    case CommandType.Delete:
                        {
                            string ExecutePathJson = CommandValue["ExecutePath"];

                            bool PermanentDelete = Convert.ToBoolean(CommandValue["PermanentDelete"]);

                            List<string> ExecutePathList = JsonSerializer.Deserialize<List<string>>(ExecutePathJson);
                            List<string> OperationRecordList = new List<string>();

                            if (ExecutePathList.All((Item) => Directory.Exists(Item) || File.Exists(Item)))
                            {
                                if (ExecutePathList.Any((Item) => StorageItemController.CheckCaptured(Item)))
                                {
                                    Value.Add("Error_Capture", "An error occurred while deleting the files");
                                }
                                else
                                {
                                    if (ExecutePathList.All((Path) => StorageItemController.CheckPermission(System.IO.Path.GetDirectoryName(Path) ?? Path, FileSystemRights.Modify)))
                                    {
                                        try
                                        {
                                            if (StorageItemController.Delete(ExecutePathList, PermanentDelete, (s, e) =>
                                            {
                                                if (CurrentTaskCancellation.IsCancellationRequested)
                                                {
                                                    throw new COMException(null, HRESULT.E_ABORT);
                                                }

                                                PipeProgressWriterController?.SendData(Convert.ToString(e.ProgressPercentage));
                                            },
                                            PostDeleteEvent: (se, arg) =>
                                            {
                                                if (!PermanentDelete)
                                                {
                                                    OperationRecordList.Add($"{arg.SourceItem.FileSystemPath}||Delete");
                                                }
                                            }))
                                            {
                                                if (ExecutePathList.All((Item) => !Directory.Exists(Item) && !File.Exists(Item)))
                                                {
                                                    Value.Add("Success", JsonSerializer.Serialize(OperationRecordList));
                                                }
                                                else
                                                {
                                                    Value.Add("Error_Capture", "An error occurred while deleting the folder");
                                                }
                                            }
                                            else if (Marshal.GetLastWin32Error() == 5)
                                            {
                                                IDictionary<string, string> ResultMap = await CreateNewProcessAsElevatedAndWaitForResultAsync(new ElevationDeleteData(ExecutePathList, PermanentDelete));

                                                foreach (KeyValuePair<string, string> Result in ResultMap)
                                                {
                                                    Value.Add(Result);
                                                }
                                            }
                                            else
                                            {
                                                Value.Add("Error_Failure", "The specified file could not be deleted");
                                            }
                                        }
                                        catch (Exception ex) when (ex is COMException or OperationCanceledException)
                                        {
                                            Value.Add("Error_Cancelled", "Operation is cancelled");
                                        }
                                    }
                                    else
                                    {
                                        IDictionary<string, string> ResultMap = await CreateNewProcessAsElevatedAndWaitForResultAsync(new ElevationDeleteData(ExecutePathList, PermanentDelete));

                                        foreach (KeyValuePair<string, string> Result in ResultMap)
                                        {
                                            Value.Add(Result);
                                        }
                                    }
                                }
                            }
                            else
                            {
                                Value.Add("Error_NotFound", $"One of path in \"{nameof(ExecutePathList)}\" is not a file or directory");
                            }

                            break;
                        }
                    case CommandType.RunExecutable:
                        {
                            string ExecutePath = CommandValue["ExecutePath"];
                            string ExecuteParameter = CommandValue["ExecuteParameter"];
                            string ExecuteAuthority = CommandValue["ExecuteAuthority"];
                            string ExecuteWindowStyle = CommandValue["ExecuteWindowStyle"];
                            string ExecuteWorkDirectory = CommandValue["ExecuteWorkDirectory"];

                            bool ExecuteCreateNoWindow = Convert.ToBoolean(CommandValue["ExecuteCreateNoWindow"]);
                            bool ShouldWaitForExit = Convert.ToBoolean(CommandValue["ExecuteShouldWaitForExit"]);

                            if (!string.IsNullOrEmpty(ExecutePath))
                            {
                                if (StorageItemController.CheckPermission(ExecutePath, FileSystemRights.ReadAndExecute))
                                {
                                    try
                                    {
                                        await Helper.ExecuteOnSTAThreadAsync(() =>
                                        {
                                            ShowWindowCommand WindowCommand;

                                            if (ExecuteCreateNoWindow)
                                            {
                                                WindowCommand = ShowWindowCommand.SW_HIDE;
                                            }
                                            else
                                            {
                                                switch ((ProcessWindowStyle)Enum.Parse(typeof(ProcessWindowStyle), ExecuteWindowStyle))
                                                {
                                                    case ProcessWindowStyle.Hidden:
                                                        {
                                                            WindowCommand = ShowWindowCommand.SW_HIDE;
                                                            break;
                                                        }
                                                    case ProcessWindowStyle.Minimized:
                                                        {
                                                            WindowCommand = ShowWindowCommand.SW_SHOWMINIMIZED;
                                                            break;
                                                        }
                                                    case ProcessWindowStyle.Maximized:
                                                        {
                                                            WindowCommand = ShowWindowCommand.SW_SHOWMAXIMIZED;
                                                            break;
                                                        }
                                                    default:
                                                        {
                                                            WindowCommand = ShowWindowCommand.SW_NORMAL;
                                                            break;
                                                        }
                                                }
                                            }

                                            bool CouldBeRunAsAdmin = Path.GetExtension(ExecutePath).ToLower() switch
                                            {
                                                ".exe" or ".bat" or ".msi" or ".msc" => true,
                                                _ => false
                                            };

                                            Shell32.SHELLEXECUTEINFO ExecuteInfo = new Shell32.SHELLEXECUTEINFO
                                            {
                                                hwnd = HWND.NULL,
                                                lpVerb = CouldBeRunAsAdmin && ExecuteAuthority == "Administrator" ? "runas" : null,
                                                cbSize = Marshal.SizeOf<Shell32.SHELLEXECUTEINFO>(),
                                                lpFile = ExecutePath,
                                                lpParameters = string.IsNullOrWhiteSpace(ExecuteParameter) ? null : ExecuteParameter,
                                                lpDirectory = string.IsNullOrWhiteSpace(ExecuteWorkDirectory) ? null : ExecuteWorkDirectory,
                                                fMask = Shell32.ShellExecuteMaskFlags.SEE_MASK_FLAG_NO_UI
                                                        | Shell32.ShellExecuteMaskFlags.SEE_MASK_UNICODE
                                                        | Shell32.ShellExecuteMaskFlags.SEE_MASK_DOENVSUBST
                                                        | Shell32.ShellExecuteMaskFlags.SEE_MASK_NOASYNC
                                                        | Shell32.ShellExecuteMaskFlags.SEE_MASK_NOCLOSEPROCESS,
                                                nShellExecuteShow = WindowCommand,
                                            };

                                            if (Shell32.ShellExecuteEx(ref ExecuteInfo))
                                            {
                                                if (!ExecuteInfo.hProcess.IsNull)
                                                {
                                                    IntPtr Buffer = Marshal.AllocCoTaskMem(Marshal.SizeOf<NtDll.PROCESS_BASIC_INFORMATION>());

                                                    try
                                                    {
                                                        NtDll.PROCESS_BASIC_INFORMATION Info = new NtDll.PROCESS_BASIC_INFORMATION();

                                                        Marshal.StructureToPtr(Info, Buffer, false);

                                                        if (NtDll.NtQueryInformationProcess(ExecuteInfo.hProcess, NtDll.PROCESSINFOCLASS.ProcessBasicInformation, Buffer, Convert.ToUInt32(Marshal.SizeOf<NtDll.PROCESS_BASIC_INFORMATION>()), out _).Succeeded)
                                                        {
                                                            NtDll.PROCESS_BASIC_INFORMATION ResultInfo = Marshal.PtrToStructure<NtDll.PROCESS_BASIC_INFORMATION>(Buffer);

                                                            IReadOnlyList<HWND> WindowsBeforeStartup = Helper.GetCurrentWindowsHandle();

                                                            using (Process OpenedProcess = Process.GetProcessById(ResultInfo.UniqueProcessId.ToInt32()))
                                                            {
                                                                SetWindowsZPosition(OpenedProcess, WindowsBeforeStartup);
                                                            }
                                                        }
                                                    }
                                                    catch (Exception ex)
                                                    {
                                                        LogTracer.Log(ex, "Could not set the windows Z position");
                                                    }
                                                    finally
                                                    {
                                                        Marshal.FreeCoTaskMem(Buffer);
                                                        Kernel32.CloseHandle(ExecuteInfo.hProcess);
                                                    }
                                                }
                                            }
                                            else if (Path.GetExtension(ExecutePath).Equals(".exe", StringComparison.OrdinalIgnoreCase))
                                            {
                                                Kernel32.CREATE_PROCESS CreationFlag = Kernel32.CREATE_PROCESS.NORMAL_PRIORITY_CLASS | Kernel32.CREATE_PROCESS.CREATE_UNICODE_ENVIRONMENT;

                                                if (ExecuteCreateNoWindow)
                                                {
                                                    CreationFlag |= Kernel32.CREATE_PROCESS.CREATE_NO_WINDOW;
                                                }

                                                Kernel32.STARTUPINFO SInfo = new Kernel32.STARTUPINFO
                                                {
                                                    cb = Convert.ToUInt32(Marshal.SizeOf<Kernel32.STARTUPINFO>()),
                                                    ShowWindowCommand = WindowCommand,
                                                    dwFlags = Kernel32.STARTF.STARTF_USESHOWWINDOW,
                                                };

                                                if (Kernel32.CreateProcess(lpCommandLine: new StringBuilder($"\"{ExecutePath}\" {(string.IsNullOrWhiteSpace(ExecuteParameter) ? string.Empty : ExecuteParameter)}"),
                                                                           bInheritHandles: false,
                                                                           dwCreationFlags: CreationFlag,
                                                                           lpCurrentDirectory: string.IsNullOrWhiteSpace(ExecuteWorkDirectory) ? null : ExecuteWorkDirectory,
                                                                           lpStartupInfo: SInfo,
                                                                           lpProcessInformation: out Kernel32.SafePROCESS_INFORMATION PInfo))

                                                {
                                                    try
                                                    {
                                                        IReadOnlyList<HWND> WindowsBeforeStartup = Helper.GetCurrentWindowsHandle();

                                                        using (Process OpenedProcess = Process.GetProcessById(Convert.ToInt32(PInfo.dwProcessId)))
                                                        {
                                                            SetWindowsZPosition(OpenedProcess, WindowsBeforeStartup);
                                                        }
                                                    }
                                                    catch (Exception ex)
                                                    {
                                                        LogTracer.Log(ex, "Could not set the windows Z position");
                                                    }
                                                    finally
                                                    {
                                                        if (!PInfo.hProcess.IsInvalid)
                                                        {
                                                            PInfo.hProcess.Dispose();
                                                        }

                                                        if (!PInfo.hThread.IsInvalid)
                                                        {
                                                            PInfo.hThread.Dispose();
                                                        }
                                                    }
                                                }
                                                else
                                                {
                                                    throw new Win32Exception(Marshal.GetLastWin32Error());
                                                }
                                            }
                                            else
                                            {
                                                throw new Win32Exception(Marshal.GetLastWin32Error());
                                            }
                                        });

                                        Value.Add("Success", string.Empty);
                                    }
                                    catch (Exception ex)
                                    {
                                        Value.Add("Error", $"Path: {ExecutePath}, Parameter: {(string.IsNullOrEmpty(ExecuteParameter) ? "<None>" : ExecuteParameter)}, Authority: {ExecuteAuthority}, ErrorMessage: {ex.Message}");
                                    }
                                }
                                else
                                {
                                    Value.Add("Error_NoPermission", "Do not have enough permission");
                                }
                            }
                            else
                            {
                                Value.Add("Error", "ExecutePath could not be null or empty");
                            }

                            break;
                        }
                    case CommandType.PasteRemoteFile:
                        {
                            string Path = CommandValue["Path"];

                            await Helper.ExecuteOnSTAThreadAsync(() =>
                            {
                                if (Clipboard.GetDataObject() is IDataObject RawData)
                                {
                                    RemoteDataObject Rdo = new RemoteDataObject(RawData);

                                    foreach (RemoteClipboardDataPackage Package in Rdo.GetRemoteData())
                                    {
                                        try
                                        {
                                            switch (Package.ItemType)
                                            {
                                                case RemoteClipboardStorageType.File:
                                                    {
                                                        string DirectoryPath = System.IO.Path.GetDirectoryName(Path);

                                                        if (!Directory.Exists(DirectoryPath))
                                                        {
                                                            Directory.CreateDirectory(DirectoryPath);
                                                        }

                                                        string UniqueName = StorageItemController.GenerateUniquePath(System.IO.Path.Combine(Path, Package.Name));

                                                        using (FileStream Stream = File.Open(UniqueName, FileMode.CreateNew, FileAccess.Write))
                                                        {
                                                            Package.ContentStream.CopyTo(Stream);
                                                        }

                                                        break;
                                                    }
                                                case RemoteClipboardStorageType.Folder:
                                                    {
                                                        string DirectoryPath = System.IO.Path.Combine(Path, Package.Name);

                                                        if (!Directory.Exists(DirectoryPath))
                                                        {
                                                            Directory.CreateDirectory(DirectoryPath);
                                                        }

                                                        break;
                                                    }
                                                default:
                                                    {
                                                        throw new NotSupportedException();
                                                    }
                                            }
                                        }
                                        finally
                                        {
                                            Package.Dispose();
                                        }
                                    }
                                }
                                else
                                {
                                    throw new Exception("Could not get the data from clipboard");
                                }
                            });

                            Value.Add("Success", string.Empty);

                            break;
                        }
                    case CommandType.GetThumbnailOverlay:
                        {
                            string Path = CommandValue["Path"];

                            Value.Add("Success", JsonSerializer.Serialize(StorageItemController.GetThumbnailOverlay(Path)));

                            break;
                        }
                    case CommandType.SetAsTopMostWindow:
                        {
                            string PackageFamilyName = CommandValue["PackageFamilyName"];
                            uint WithPID = Convert.ToUInt32(CommandValue["WithPID"]);

                            if (Helper.GetUWPWindowInformation(PackageFamilyName, WithPID) is WindowInformation Info && !Info.Handle.IsNull)
                            {
                                User32.SetWindowPos(Info.Handle, new IntPtr(-1), 0, 0, 0, 0, User32.SetWindowPosFlags.SWP_NOMOVE | User32.SetWindowPosFlags.SWP_NOSIZE);
                                Value.Add("Success", string.Empty);
                            }
                            else
                            {
                                Value.Add("Error", "Could not found the window handle");
                            }

                            break;
                        }
                    case CommandType.RemoveTopMostWindow:
                        {
                            string PackageFamilyName = CommandValue["PackageFamilyName"];
                            uint WithPID = Convert.ToUInt32(CommandValue["WithPID"]);

                            if (Helper.GetUWPWindowInformation(PackageFamilyName, WithPID) is WindowInformation Info && !Info.Handle.IsNull)
                            {
                                User32.SetWindowPos(Info.Handle, new IntPtr(-2), 0, 0, 0, 0, User32.SetWindowPosFlags.SWP_NOMOVE | User32.SetWindowPosFlags.SWP_NOSIZE);
                                Value.Add("Success", string.Empty);
                            }
                            else
                            {
                                Value.Add("Error", "Could not found the window handle");
                            }

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

        private static void SetWindowsZPosition(Process OtherProcess, IEnumerable<HWND> WindowsBeforeStartup)
        {
            void SetWindowsPosFallback(IEnumerable<HWND> WindowsBeforeStartup)
            {
                foreach (HWND Handle in Helper.GetCurrentWindowsHandle().Except(WindowsBeforeStartup))
                {
                    User32.SetWindowPos(Handle, User32.SpecialWindowHandles.HWND_TOPMOST, 0, 0, 0, 0, User32.SetWindowPosFlags.SWP_NOMOVE | User32.SetWindowPosFlags.SWP_NOSIZE);
                    User32.SetWindowPos(Handle, User32.SpecialWindowHandles.HWND_NOTOPMOST, 0, 0, 0, 0, User32.SetWindowPosFlags.SWP_NOMOVE | User32.SetWindowPosFlags.SWP_NOSIZE);
                }
            }

            if (OtherProcess.WaitForInputIdle(5000))
            {
                IntPtr MainWindowHandle = IntPtr.Zero;

                for (int i = 0; i < 10 && !OtherProcess.HasExited; i++)
                {
                    OtherProcess.Refresh();

                    if (OtherProcess.MainWindowHandle.CheckIfValidPtr())
                    {
                        MainWindowHandle = OtherProcess.MainWindowHandle;
                        break;
                    }
                    else
                    {
                        Thread.Sleep(500);
                    }
                }

                if (MainWindowHandle.CheckIfValidPtr())
                {
                    bool IsSuccess = true;

                    uint ExecuteThreadId = User32.GetWindowThreadProcessId(MainWindowHandle, out _);
                    uint ForegroundThreadId = User32.GetWindowThreadProcessId(User32.GetForegroundWindow(), out _);
                    uint CurrentThreadId = Kernel32.GetCurrentThreadId();

                    if (ForegroundThreadId != ExecuteThreadId)
                    {
                        User32.AttachThreadInput(ForegroundThreadId, CurrentThreadId, true);
                        User32.AttachThreadInput(ForegroundThreadId, ExecuteThreadId, true);
                    }

                    IsSuccess &= User32.ShowWindow(MainWindowHandle, ShowWindowCommand.SW_SHOWNORMAL);
                    IsSuccess &= User32.SetWindowPos(MainWindowHandle, User32.SpecialWindowHandles.HWND_TOPMOST, 0, 0, 0, 0, User32.SetWindowPosFlags.SWP_NOMOVE | User32.SetWindowPosFlags.SWP_NOSIZE);
                    IsSuccess &= User32.SetWindowPos(MainWindowHandle, User32.SpecialWindowHandles.HWND_NOTOPMOST, 0, 0, 0, 0, User32.SetWindowPosFlags.SWP_NOMOVE | User32.SetWindowPosFlags.SWP_NOSIZE);
                    IsSuccess &= User32.SetForegroundWindow(MainWindowHandle);

                    if (Helper.GetUWPWindowInformation(Package.Current.Id.FamilyName, (uint)(ExplorerProcess?.Id).GetValueOrDefault()) is WindowInformation UwpWindow && !UwpWindow.Handle.IsNull)
                    {
                        IsSuccess &= User32.SetWindowPos(UwpWindow.Handle, MainWindowHandle, 0, 0, 0, 0, User32.SetWindowPosFlags.SWP_NOMOVE | User32.SetWindowPosFlags.SWP_NOSIZE | User32.SetWindowPosFlags.SWP_NOACTIVATE);
                    }

                    if (ForegroundThreadId != ExecuteThreadId)
                    {
                        User32.AttachThreadInput(ForegroundThreadId, CurrentThreadId, false);
                        User32.AttachThreadInput(ForegroundThreadId, ExecuteThreadId, false);
                    }

                    if (!IsSuccess)
                    {
                        LogTracer.Log("Could not switch to window because noraml method failed, use fallback function");
                        SetWindowsPosFallback(WindowsBeforeStartup);
                    }
                }
                else
                {
                    LogTracer.Log("Could not switch to window because MainWindowHandle is invalid, use fallback function");
                    SetWindowsPosFallback(WindowsBeforeStartup);
                }
            }
            else
            {
                LogTracer.Log("Could not switch to window because WaitForInputIdle is timeout after 5000ms, use fallback function");
                SetWindowsPosFallback(WindowsBeforeStartup);
            }
        }

        private static async Task<IDictionary<string, string>> CreateNewProcessAsElevatedAndWaitForResultAsync<T>(T Data, CancellationToken CancelToken = default) where T : IElevationData
        {
            using (Process CurrentProcess = Process.GetCurrentProcess())
            {
                string PipeName = $"FullTrustProcess_ElevatedPipe_{Guid.NewGuid()}";
                string CancelSignalName = $"FullTrustProcess_ElevatedCancellation_{Guid.NewGuid()}";

                using (EventWaitHandle CancelEvent = new EventWaitHandle(false, EventResetMode.ManualReset, CancelSignalName))
                using (NamedPipeServerStream ServerStream = new NamedPipeServerStream(PipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Message, PipeOptions.Asynchronous | PipeOptions.WriteThrough))
                using (StreamWriter Writer = new StreamWriter(ServerStream, new UTF8Encoding(false), leaveOpen: true))
                using (StreamReader Reader = new StreamReader(ServerStream, new UTF8Encoding(false), true, leaveOpen: true))
                using (Process ElevatedProcess = Process.Start(new ProcessStartInfo
                {
                    FileName = CurrentProcess.MainModule.FileName,
                    Arguments = $"/ExecuteAdminOperation \"{PipeName}\"",
                    UseShellExecute = true,
                    Verb = "runas"
                }))
                {
                    Task<string> GetRawResultTask = Task.FromResult<string>(null);

                    try
                    {
                        await ServerStream.WaitForConnectionAsync(CancelToken);

                        Writer.WriteLine(Data.GetType().FullName);
                        Writer.WriteLine(CancelSignalName);
                        Writer.WriteLine(JsonSerializer.Serialize(Data));
                        Writer.Flush();

                        GetRawResultTask = Reader.ReadLineAsync();

                        await ElevatedProcess.WaitForExitAsync(CancelToken);
                    }
                    catch (TaskCanceledException)
                    {
                        CancelEvent.Set();

                        if (!ElevatedProcess.WaitForExit(10000))
                        {
                            LogTracer.Log("Elevated process is not exit in 10s and we will not wait for it any more");
                        }
                    }

                    string RawResultText = await GetRawResultTask;

                    if (string.IsNullOrEmpty(RawResultText))
                    {
                        return new Dictionary<string, string>
                        {
                            { "Success", string.Empty }
                        };
                    }
                    else
                    {
                        return JsonSerializer.Deserialize<IDictionary<string, string>>(RawResultText);
                    }
                }
            }
        }
    }
}
