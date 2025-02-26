﻿using AuxiliaryTrustProcess.Class;
using AuxiliaryTrustProcess.Interface;
using CommandLine;
using MediaDevices;
using SharedLibrary;
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
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using UsbEject;
using UtfUnknown;
using Vanara.PInvoke;
using Vanara.Windows.Shell;
using Timer = System.Timers.Timer;
using UACUtil = UACHelper.UACHelper;

namespace AuxiliaryTrustProcess
{
    internal class Program
    {
        private static ManualResetEvent ExitLocker;

        private static Timer AliveCheckTimer;

        private static DateTimeOffset StartTime;

        private static Process ExplorerProcess;

        private static NamedPipeWriteController PipeCommandWriteController;

        private static NamedPipeReadController PipeCommandReadController;

        private static NamedPipeWriteController PipeProgressWriterController;

        private static NamedPipeReadController PipeCancellationReadController;

        private static NamedPipeAuxiliaryCommunicationBaseController PipeCommunicationBaseController;

        private static CancellationTokenSource CurrentTaskCancellation;

        private static MTPDeviceManager MTPManager;

        private const string ExplorerPackageFamilyName = "36186RuoFan.USB_q3e6crc0w375t";

        [STAThread]
        static void Main(string[] args)
        {
            Parser ArgumentParser = new Parser((With) =>
            {
                With.AutoHelp = true;
                With.CaseInsensitiveEnumValues = true;
                With.IgnoreUnknownArguments = true;
                With.CaseSensitive = true;
            });

            ArgumentParser.ParseArguments<CommandLineOptions>(args.SkipWhile((Value) => Value.EndsWith(Path.GetFileName(Environment.ProcessPath), StringComparison.OrdinalIgnoreCase))).WithParsed((Options) =>
            {
                Ole32.OleInitialize();

                try
                {
                    StartTime = DateTimeOffset.Now;
                    ExitLocker = new ManualResetEvent(false);
                    CurrentTaskCancellation = new CancellationTokenSource();
                    MTPManager = new MTPDeviceManager();

                    Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
                    AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

                    if (Options.ExecuteElevatedOperation)
                    {
                        ArgumentException.ThrowIfNullOrEmpty(Options.CommunicatePipeName);
                        ArgumentException.ThrowIfNullOrEmpty(Options.ProgressPipeName);
                        ArgumentException.ThrowIfNullOrEmpty(Options.CancelSignalName);

                        if (!UACUtil.IsElevated)
                        {
                            throw new InvalidOperationException("Currently not running as elevated");
                        }

                        using (NamedPipeClientStream CommunicatePipeStream = new NamedPipeClientStream(".", Options.CommunicatePipeName, PipeDirection.InOut, PipeOptions.Asynchronous | PipeOptions.WriteThrough, TokenImpersonationLevel.Anonymous))
                        using (NamedPipeClientStream ProgressPipeStream = new NamedPipeClientStream(".", Options.ProgressPipeName, PipeDirection.InOut, PipeOptions.Asynchronous | PipeOptions.WriteThrough, TokenImpersonationLevel.Anonymous))
                        {
                            CommunicatePipeStream.Connect(2000);
                            ProgressPipeStream.Connect(2000);

                            using (StreamReader CommunicatePipeReader = new StreamReader(CommunicatePipeStream, Encoding.Unicode, true, leaveOpen: true))
                            using (StreamWriter CommunicatePipeWriter = new StreamWriter(CommunicatePipeStream, Encoding.Unicode, leaveOpen: true))
                            using (StreamWriter ProgressPipeWriter = new StreamWriter(ProgressPipeStream, Encoding.Unicode, leaveOpen: true))
                            using (CancellationTokenSource Cancellation = new CancellationTokenSource())
                            {
                                IDictionary<string, string> Value = new Dictionary<string, string>();

                                try
                                {
                                    string CommandData = CommunicatePipeReader.ReadLine();

                                    if (EventWaitHandle.TryOpenExisting(Options.CancelSignalName, out EventWaitHandle EventHandle))
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
                                            switch (JsonSerializer.Deserialize(CommandData, JsonSourceGenerationContext.Default.IElevationData))
                                            {
                                                case ElevationRemoteCopyData RemoteData:
                                                    {
                                                        try
                                                        {
                                                            RemoteClipboardRelatedData RelatedData = RemoteDataObject.GetRemoteClipboardRelatedInformation();

                                                            if (RelatedData.ItemsCount > 0)
                                                            {
                                                                ulong CurrentPosition = 0;

                                                                foreach (RemoteClipboardData Package in RemoteDataObject.GetRemoteClipboardData(Cancellation.Token))
                                                                {
                                                                    string TargetPath = Path.Combine(RemoteData.BaseFolderPath, Package.Name);

                                                                    switch (Package)
                                                                    {
                                                                        case RemoteClipboardFileData FileData:
                                                                            {
                                                                                if (!Directory.Exists(RemoteData.BaseFolderPath))
                                                                                {
                                                                                    Directory.CreateDirectory(RemoteData.BaseFolderPath);
                                                                                }

                                                                                string UniqueName = Helper.GenerateUniquePathOnLocal(TargetPath, CreateType.File);

                                                                                using (FileStream Stream = File.Open(UniqueName, FileMode.CreateNew, FileAccess.Write))
                                                                                {
                                                                                    FileData.ContentStream.CopyTo(Stream, Convert.ToInt64(FileData.Size), Cancellation.Token, (s, e) =>
                                                                                    {
                                                                                        ProgressPipeWriter.WriteLine(Convert.ToString(Math.Ceiling((CurrentPosition + Convert.ToUInt64(e.ProgressPercentage / 100d * FileData.Size)) * 100d / RelatedData.TotalSize)));
                                                                                        ProgressPipeWriter.Flush();
                                                                                    });
                                                                                }

                                                                                CurrentPosition += FileData.Size;

                                                                                break;
                                                                            }
                                                                        case RemoteClipboardFolderData:
                                                                            {
                                                                                if (!Directory.Exists(TargetPath))
                                                                                {
                                                                                    Directory.CreateDirectory(TargetPath);
                                                                                }

                                                                                break;
                                                                            }
                                                                        default:
                                                                            {
                                                                                throw new NotSupportedException();
                                                                            }
                                                                    }
                                                                }

                                                                Value.Add("Success", string.Empty);
                                                            }
                                                            else
                                                            {
                                                                Value.Add("Error", "No remote data object is available");
                                                            }
                                                        }
                                                        catch (OperationCanceledException)
                                                        {
                                                            //No need to handle this exception
                                                        }

                                                        break;
                                                    }
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

                                                                    SetCompressionCore(Entry, CompressionFormat);
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
                                                                        File.SetAttributes(Entry, DriveIndexStatusData.AllowIndex ? File.GetAttributes(DriveIndexStatusData.Path) & ~FileAttributes.NotContentIndexed : File.GetAttributes(DriveIndexStatusData.Path) | FileAttributes.NotContentIndexed);
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
                                                                Value.Add("Error_Failure", new Win32Exception(Marshal.GetLastWin32Error()).Message);
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
                                                        if (CopyData.SourcePathMapping.Keys.All((Path) => Directory.Exists(Path) || File.Exists(Path)))
                                                        {
                                                            if (StorageItemController.CheckPermission(CopyData.DestinationPath, FileSystemRights.Modify))
                                                            {
                                                                List<string> OperationRecordList = new List<string>();

                                                                if (StorageItemController.Copy(CopyData.SourcePathMapping, CopyData.DestinationPath, CopyData.Option, (s, e) =>
                                                                {
                                                                    ProgressPipeWriter.WriteLine(e.ProgressPercentage);
                                                                    ProgressPipeWriter.Flush();

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
                                                                    Value.Add("Success", JsonSerializer.Serialize(OperationRecordList, JsonSourceGenerationContext.Default.IEnumerableString));
                                                                }
                                                                else if (CopyData.SourcePathMapping.Select((Item) => Path.Combine(CopyData.DestinationPath, string.IsNullOrEmpty(Item.Value) ? Path.GetFileName(Item.Key) : Item.Value))
                                                                                                   .All((Path) => Directory.Exists(Path) || File.Exists(Path)))
                                                                {
                                                                    Value.Add("Success", JsonSerializer.Serialize(OperationRecordList, JsonSourceGenerationContext.Default.IEnumerableString));
                                                                }
                                                                else
                                                                {
                                                                    Value.Add("Error_Failure", new Win32Exception(Marshal.GetLastWin32Error()).Message);
                                                                }
                                                            }
                                                            else
                                                            {
                                                                Value.Add("Error_NoPermission", "Do not have enough permission");
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
                                                        if (MoveData.SourcePathMapping.Keys.All((Item) => Directory.Exists(Item) || File.Exists(Item)))
                                                        {
                                                            if (MoveData.SourcePathMapping.Keys.Any((Item) => StorageItemController.CheckCaptured(Item)))
                                                            {
                                                                Value.Add("Error_Capture", "One of these files was captured and could not be renamed");
                                                            }
                                                            else
                                                            {
                                                                if (StorageItemController.CheckPermission(MoveData.DestinationPath, FileSystemRights.Modify)
                                                                    && MoveData.SourcePathMapping.Keys.All((Path) => StorageItemController.CheckPermission(System.IO.Path.GetDirectoryName(Path) ?? Path, FileSystemRights.Modify)))
                                                                {
                                                                    List<string> OperationRecordList = new List<string>();

                                                                    if (StorageItemController.Move(MoveData.SourcePathMapping, MoveData.DestinationPath, MoveData.Option, (s, e) =>
                                                                    {
                                                                        ProgressPipeWriter.WriteLine(e.ProgressPercentage);
                                                                        ProgressPipeWriter.Flush();

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
                                                                        if (MoveData.SourcePathMapping.Keys.All((Path) => !Directory.Exists(Path) && !File.Exists(Path)))
                                                                        {
                                                                            Value.Add("Success", JsonSerializer.Serialize(OperationRecordList, JsonSourceGenerationContext.Default.IEnumerableString));
                                                                        }
                                                                        else
                                                                        {
                                                                            Value.Add("Error_Capture", "One of these files was captured and could not be renamed");
                                                                        }
                                                                    }
                                                                    else if (MoveData.SourcePathMapping.Keys.All((Path) => !Directory.Exists(Path) && !File.Exists(Path))
                                                                             && MoveData.SourcePathMapping.Select((Item) => Path.Combine(MoveData.DestinationPath, string.IsNullOrEmpty(Item.Value) ? Path.GetFileName(Item.Key) : Item.Value))
                                                                                                          .All((Path) => Directory.Exists(Path) || File.Exists(Path)))
                                                                    {
                                                                        Value.Add("Success", JsonSerializer.Serialize(OperationRecordList, JsonSourceGenerationContext.Default.IEnumerableString));
                                                                    }
                                                                    else
                                                                    {
                                                                        Value.Add("Error_Failure", new Win32Exception(Marshal.GetLastWin32Error()).Message);
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
                                                case ElevationDeleteData DeleteData:
                                                    {
                                                        if (DeleteData.DeletePath.All((Item) => Directory.Exists(Item) || File.Exists(Item)))
                                                        {
                                                            if (DeleteData.DeletePath.Any((Item) => StorageItemController.CheckCaptured(Item)))
                                                            {
                                                                Value.Add("Error_Capture", "One of these files was captured and could not be renamed");
                                                            }
                                                            else
                                                            {
                                                                if (DeleteData.DeletePath.All((Path) => StorageItemController.CheckPermission(System.IO.Path.GetDirectoryName(Path) ?? Path, FileSystemRights.Modify)))
                                                                {
                                                                    int ProcessedCounter = 0;
                                                                    List<string> OperationRecordList = new List<string>();

                                                                    if (StorageItemController.Delete(DeleteData.DeletePath, DeleteData.PermanentDelete, (s, e) =>
                                                                    {
                                                                        ProgressPipeWriter.WriteLine(e.ProgressPercentage);
                                                                        ProgressPipeWriter.Flush();

                                                                        if (Cancellation.IsCancellationRequested)
                                                                        {
                                                                            throw new COMException(null, HRESULT.E_ABORT);
                                                                        }
                                                                    }, PostDeleteEvent: (se, arg) =>
                                                                    {
                                                                        ProgressPipeWriter.WriteLine((int)(++ProcessedCounter / DeleteData.DeletePath.Count * 100f));

                                                                        if (!DeleteData.PermanentDelete)
                                                                        {
                                                                            OperationRecordList.Add($"{arg.SourceItem.FileSystemPath}||Delete");
                                                                        }
                                                                    }))
                                                                    {
                                                                        if (DeleteData.DeletePath.All((Item) => !Directory.Exists(Item) && !File.Exists(Item)))
                                                                        {
                                                                            Value.Add("Success", JsonSerializer.Serialize(OperationRecordList, JsonSourceGenerationContext.Default.IEnumerableString));
                                                                        }
                                                                        else
                                                                        {
                                                                            Value.Add("Error_Capture", "One of these files was captured and could not be renamed");
                                                                        }
                                                                    }
                                                                    else if (DeleteData.DeletePath.All((Item) => !Directory.Exists(Item) && !File.Exists(Item)))
                                                                    {
                                                                        Value.Add("Success", JsonSerializer.Serialize(OperationRecordList, JsonSourceGenerationContext.Default.IEnumerableString));
                                                                    }
                                                                    else
                                                                    {
                                                                        Value.Add("Error_Failure", new Win32Exception(Marshal.GetLastWin32Error()).Message);
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
                                                case ElevationRenameData RenameData:
                                                    {
                                                        if (File.Exists(RenameData.Path) || Directory.Exists(RenameData.Path))
                                                        {
                                                            if (StorageItemController.CheckCaptured(RenameData.Path))
                                                            {
                                                                Value.Add("Error_Capture", "One of these files was captured and could not be renamed");
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
                                                                        Value.Add("Error_Failure", new Win32Exception(Marshal.GetLastWin32Error()).Message);
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
                                            RegistedHandle.Unregister(null);
                                        }
                                    }
                                    else
                                    {
                                        Value.Add("Error_CancelSignalNotSet", "Failed to get the CancelSignal");
                                    }
                                }
                                finally
                                {
                                    if (!Cancellation.IsCancellationRequested)
                                    {
                                        CommunicatePipeWriter.WriteLine(JsonSerializer.Serialize(Value, JsonSourceGenerationContext.Default.IDictionaryStringString));
                                        CommunicatePipeWriter.Flush();
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        AliveCheckTimer = new Timer(TimeSpan.FromSeconds(15))
                        {
                            AutoReset = true,
                            Enabled = true
                        };
                        AliveCheckTimer.Elapsed += AliveCheckTimer_Elapsed;

                        PipeCommunicationBaseController = new NamedPipeAuxiliaryCommunicationBaseController(ExplorerPackageFamilyName);
                        PipeCommunicationBaseController.OnDataReceived += PipeCommunicationBaseController_OnDataReceived;

                        if (PipeCommunicationBaseController.WaitForConnectionAsync(TimeSpan.FromSeconds(15)).Result)
                        {
                            AliveCheckTimer.Start();

                            //Warm up the context menu first to make sure we could get the correct menus
                            ContextMenu.Current.GetContextMenuItems(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory));

                            ExitLocker.WaitOne();
                        }
                        else
                        {
                            LogTracer.Log($"Could not connect to the explorer. PipeCommunicationBaseController connect timeout. Exiting...");
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogTracer.Log(ex, "An unexpected exception was threw in starting AuxiliaryTrustProcess");
                }
                finally
                {
                    ExitLocker?.Dispose();
                    AliveCheckTimer?.Dispose();
                    MTPManager?.Dispose();

                    PipeCommandWriteController?.Dispose();
                    PipeCommandReadController?.Dispose();
                    PipeProgressWriterController?.Dispose();
                    PipeCancellationReadController?.Dispose();
                    PipeCommunicationBaseController?.Dispose();

                    LogTracer.MakeSureLogIsFlushed(2000);

                    Ole32.OleUninitialize();
                }
            })
            .WithNotParsed((ErrorList) =>
            {
                LogTracer.Log($"Unable to parse the arguments, reason: {string.Join(';', ErrorList.Select((Error) => Enum.GetName(Error.Tag)))}");
            });
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
                try
                {
                    IDictionary<string, string> Package = JsonSerializer.Deserialize(e.Data, JsonSourceGenerationContext.Default.IDictionaryStringString);

                    if (Package.TryGetValue("LogRecordFolderPath", out string LogRecordPath))
                    {
                        LogTracer.SetLogRecordFolderPath(LogRecordPath);
                    }

                    if (Package.TryGetValue("ProcessId", out string ProcessId))
                    {
                        if ((ExplorerProcess?.Id).GetValueOrDefault() != Convert.ToInt32(ProcessId))
                        {
                            ExplorerProcess = Process.GetProcessById(Convert.ToInt32(ProcessId));
                            ExplorerProcess.EnableRaisingEvents = true;
                            ExplorerProcess.Exited += ExplorerProcess_Exited;
                        }
                    }

                    if (PipeCommandReadController != null)
                    {
                        PipeCommandReadController.OnDataReceived -= PipeReadController_OnDataReceived;
                    }

                    if (PipeCancellationReadController != null)
                    {
                        PipeCancellationReadController.OnDataReceived -= PipeReadController_OnDataReceived;
                    }

                    if (Package.TryGetValue("PipeCommandWriteId", out string PipeCommandWriteId))
                    {
                        PipeCommandReadController?.Dispose();
                        PipeCommandReadController = new NamedPipeReadController(ExplorerPackageFamilyName, PipeCommandWriteId);
                        PipeCommandReadController.OnDataReceived += PipeReadController_OnDataReceived;
                    }

                    if (Package.TryGetValue("PipeCommandReadId", out string PipeCommandReadId))
                    {
                        PipeCommandWriteController?.Dispose();
                        PipeCommandWriteController = new NamedPipeWriteController(ExplorerPackageFamilyName, PipeCommandReadId);
                    }

                    if (Package.TryGetValue("PipeProgressReadId", out string PipeProgressReadId))
                    {
                        PipeProgressWriterController?.Dispose();
                        PipeProgressWriterController = new NamedPipeWriteController(ExplorerPackageFamilyName, PipeProgressReadId);
                    }

                    if (Package.TryGetValue("PipeCancellationWriteId", out string PipeCancellationReadId))
                    {
                        PipeCancellationReadController?.Dispose();
                        PipeCancellationReadController = new NamedPipeReadController(ExplorerPackageFamilyName, PipeCancellationReadId);
                        PipeCancellationReadController.OnDataReceived += PipeCancellationController_OnDataReceived;
                    }
                }
                catch (Exception ex)
                {
                    LogTracer.Log(ex, $"An exception was threw in get data in {nameof(PipeCommunicationBaseController_OnDataReceived)}");
                }
            }
        }

        private static void ExplorerProcess_Exited(object sender, EventArgs e)
        {
            try
            {
                ExitLocker.Set();
            }
            catch (Exception)
            {
                //No need to handle this exception
            }
        }

        private static void PipeCancellationController_OnDataReceived(object sender, NamedPipeDataReceivedArgs e)
        {
            if (e.Data == "Cancel")
            {
                if (Interlocked.Exchange(ref CurrentTaskCancellation, new CancellationTokenSource()) is CancellationTokenSource Cancellation)
                {
                    Cancellation.Cancel();
                    Cancellation.Dispose();
                }
            }
        }

        private static void PipeReadController_OnDataReceived(object sender, NamedPipeDataReceivedArgs e)
        {
            try
            {
                if (e.ExtraException is Exception Ex)
                {
                    LogTracer.Log(Ex, "Could not receive pipe data");
                }
                else
                {
                    IDictionary<string, string> Request = JsonSerializer.Deserialize(e.Data, JsonSourceGenerationContext.Default.IDictionaryStringString);
                    IDictionary<string, string> Response = HandleCommand(Request);
                    PipeCommandWriteController?.SendData(JsonSerializer.Serialize(Response, JsonSourceGenerationContext.Default.IDictionaryStringString));
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, "An exception was threw in responding pipe message");
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
                MTPManager?.Dispose();

                PipeCommandWriteController?.Dispose();
                PipeCommandReadController?.Dispose();
                PipeProgressWriterController?.Dispose();
                PipeCancellationReadController?.Dispose();
                PipeCommunicationBaseController?.Dispose();
            }
        }

        private static IDictionary<string, string> HandleCommand(IDictionary<string, string> CommandValue)
        {
#if DEBUG
            if (Thread.CurrentThread.GetApartmentState() != ApartmentState.STA)
            {
                if (Debugger.IsAttached)
                {
                    Debugger.Break();
                }
                else
                {
                    Debugger.Launch();
                }

                throw new Exception("Not allowed to execute this function in modes rather than STA");
            }
#endif

            IDictionary<string, string> Value = new Dictionary<string, string>();

            using (CancellationTokenSource Cancellation = CancellationTokenSource.CreateLinkedTokenSource(CurrentTaskCancellation.Token))
            {
                try
                {
                    switch (Enum.Parse<AuxiliaryTrustProcessCommandType>(CommandValue["CommandType"]))
                    {
                        case AuxiliaryTrustProcessCommandType.RetrieveAADTokenFromBackend:
                            {
                                RetrieveAADTokenContentResponseDto ResponseContentDto = BackendUtil.SendAndGetResponseAsync<RetrieveAADTokenContentResponseDto>(new Uri("https://xz-home.brasilia.me:61550/api/v1/validation/retrieveAADToken"), HttpMethod.Get, CancelToken: Cancellation.Token).Result;

                                if (ResponseContentDto.ExpiresOn > DateTimeOffset.Now)
                                {
                                    Value.Add("Success", ResponseContentDto.AADToken);
                                }
                                else
                                {
                                    throw new Exception("AAD token has already expired");
                                }

                                break;
                            }
                        case AuxiliaryTrustProcessCommandType.RedeemCodeFromBackend:
                            {
                                Value.Add("Success", JsonSerializer.Serialize(BackendUtil.SendAndGetResponseAsync<RedeemCodeContentResponseDto>(new Uri($"https://xz-home.brasilia.me:61550/api/v1/validation/redeemCode?customerCollectionId={Uri.EscapeDataString(CommandValue["CustomerCollectionId"])}"), HttpMethod.Get, CancelToken: Cancellation.Token).Result, JsonSourceGenerationContext.Default.RedeemCodeContentResponseDto));
                                break;
                            }
                        case AuxiliaryTrustProcessCommandType.GetAvailableNetworkPort:
                            {
                                HashSet<int> TriedPorts = new HashSet<int>();
                                Random Rand = new Random(Guid.NewGuid().GetHashCode());

                                for (int Retry = 0; Retry < 100; Retry++)
                                {
                                    int RandomPort = Rand.Next(60000, 65535);

                                    if (TriedPorts.Add(RandomPort))
                                    {
                                        IPGlobalProperties IPProperties = IPGlobalProperties.GetIPGlobalProperties();

                                        if (!IPProperties.GetActiveTcpConnections()
                                                         .Select((Connection) => Connection.LocalEndPoint.Port)
                                                         .Concat(IPProperties.GetActiveTcpListeners().Select((EndPoint) => EndPoint.Port))
                                                         .Contains(RandomPort))
                                        {
                                            Value.Add("Success", Convert.ToString(RandomPort));
                                            break;
                                        }
                                    }
                                }

                                if (!Value.ContainsKey("Success"))
                                {
                                    Value.Add("Error", "Could not get the available port after max retry times");
                                }

                                break;
                            }
                        case AuxiliaryTrustProcessCommandType.SetWallpaperImage:
                            {
                                string Path = CommandValue["Path"];

                                if (File.Exists(Path))
                                {
                                    using (Image TempImage = Image.FromFile(Path))
                                    {
                                        string TempBmpPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"{Guid.NewGuid():N}.bmp");

                                        try
                                        {
                                            using (Stream TempBmpStream = File.Create(TempBmpPath))
                                            {
                                                TempImage.Save(TempBmpStream, ImageFormat.Bmp);
                                            }

                                            IntPtr PathPointer = Marshal.StringToHGlobalUni(TempBmpPath);

                                            try
                                            {
                                                Value.Add("Success", Convert.ToString(User32.SystemParametersInfo(User32.SPI.SPI_SETDESKWALLPAPER, 0, PathPointer, User32.SPIF.SPIF_SENDCHANGE | User32.SPIF.SPIF_UPDATEINIFILE)));
                                            }
                                            finally
                                            {
                                                Marshal.FreeHGlobal(PathPointer);
                                            }
                                        }
                                        finally
                                        {
                                            if (File.Exists(TempBmpPath))
                                            {
                                                File.Delete(TempBmpPath);
                                            }
                                        }
                                    }
                                }
                                else
                                {
                                    Value.Add("Error", "File is not found");
                                }

                                break;
                            }
                        case AuxiliaryTrustProcessCommandType.GetProcessHandle:
                            {
                                using (Process CurrentProcess = Process.GetCurrentProcess())
                                {
                                    if (Kernel32.DuplicateHandle(Kernel32.GetCurrentProcess(), CurrentProcess.Handle, ExplorerProcess.Handle, out IntPtr TargetHandle, default, default, Kernel32.DUPLICATE_HANDLE_OPTIONS.DUPLICATE_SAME_ACCESS))
                                    {
                                        Value.Add("Success", Convert.ToString(TargetHandle.ToInt64()));
                                    }
                                    else
                                    {
                                        Value.Add("Error", "Could not duplicate the handle");
                                    }
                                }

                                break;
                            }
                        case AuxiliaryTrustProcessCommandType.GetFileAttribute:
                            {
                                string Path = CommandValue["Path"];

                                if (File.Exists(Path))
                                {
                                    Value.Add("Success", Enum.GetName(File.GetAttributes(Path)));
                                }
                                else if (Directory.Exists(Path))
                                {
                                    Value.Add("Success", Enum.GetName(new DirectoryInfo(Path).Attributes));
                                }
                                else
                                {
                                    Value.Add("Error", "Could not found the items according to the path");
                                }

                                break;
                            }
                        case AuxiliaryTrustProcessCommandType.GetRecyclePathFromOriginPath:
                            {
                                string OriginPath = CommandValue["OriginPath"];

                                using (ShellItem Item = RecycleBinController.GetItemFromOriginPath(OriginPath))
                                {
                                    Value.Add("Success", Item.FileSystemPath);
                                }

                                break;
                            }
                        case AuxiliaryTrustProcessCommandType.CreateTemporaryFileHandle:
                            {
                                string TempFilePath = CommandValue["TempFilePath"];
                                IOPreference Preference = Enum.Parse<IOPreference>(CommandValue["Preference"]);
                                FileFlagsAndAttributes FileAttribute = FileFlagsAndAttributes.FILE_FLAG_DELETE_ON_CLOSE | FileFlagsAndAttributes.FILE_FLAG_OVERLAPPED;

                                if (Preference == IOPreference.PreferUseMoreMemory)
                                {
                                    Kernel32.MEMORYSTATUSEX Status = Kernel32.MEMORYSTATUSEX.Default;

                                    if (Kernel32.GlobalMemoryStatusEx(ref Status))
                                    {
                                        if (Status.dwMemoryLoad <= 90 && Status.ullAvailPhys >= 1073741824)
                                        {
                                            FileAttribute |= FileFlagsAndAttributes.FILE_ATTRIBUTE_TEMPORARY;
                                        }
                                    }
                                }

                                using (Kernel32.SafeHFILE Handle = Kernel32.CreateFile(string.IsNullOrEmpty(TempFilePath) ? Path.Combine(Path.GetTempPath(), $"{Path.GetRandomFileName()}.tmp") : TempFilePath,
                                                                                       Kernel32.FileAccess.GENERIC_READ | Kernel32.FileAccess.GENERIC_WRITE,
                                                                                       FileShare.Read | FileShare.Write | FileShare.Delete,
                                                                                       null,
                                                                                       FileMode.CreateNew,
                                                                                       FileAttribute))
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

                                break;
                            }
                        case AuxiliaryTrustProcessCommandType.GetAvailableWslDrivePathList:
                            {
                                using (Process PowershellProcess = Process.Start(new ProcessStartInfo
                                {
                                    CreateNoWindow = true,
                                    UseShellExecute = false,
                                    RedirectStandardOutput = true,
                                    FileName = "powershell.exe",
                                    Arguments = $"-Command \"wsl --list --quiet\""
                                }))
                                {
                                    Value.Add("Success", JsonSerializer.Serialize(PowershellProcess.StandardOutput.ReadToEnd().Replace("\0", string.Empty).Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries).Select((Name) => $@"\\wsl$\{Name}").Where(Directory.Exists), JsonSourceGenerationContext.Default.IEnumerableString));

                                    if (!PowershellProcess.WaitForExit(2000))
                                    {
                                        PowershellProcess.Kill();
                                    }
                                }

                                break;
                            }
                        case AuxiliaryTrustProcessCommandType.GetSizeOnDisk:
                            {
                                string Path = CommandValue["Path"];

                                if (Kernel32.GetDiskFreeSpace(System.IO.Path.GetPathRoot(Path.TrimEnd('\\')), out uint SectorsPerCluster, out uint BytesPerSector, out _, out _))
                                {
                                    ulong ClusterSize = Convert.ToUInt64(SectorsPerCluster) * Convert.ToUInt64(BytesPerSector);

                                    if (ClusterSize > 0)
                                    {
                                        ulong CompressedSize = Helper.GetAllocationSize(Path);

                                        if (CompressedSize % ClusterSize > 0)
                                        {
                                            Value.Add("Success", Convert.ToString(CompressedSize + ClusterSize - CompressedSize % ClusterSize));
                                        }
                                        else
                                        {
                                            Value.Add("Success", Convert.ToString(CompressedSize));
                                        }
                                    }
                                    else
                                    {
                                        Value.Add("Error", "ClusterSize is equal to zero");
                                    }
                                }
                                else
                                {
                                    Value.Add("Error", new Win32Exception(Marshal.GetLastWin32Error()).Message);
                                }

                                break;
                            }
                        case AuxiliaryTrustProcessCommandType.OrderByNaturalStringSortAlgorithm:
                            {
                                Value.Add("Success", JsonSerializer.Serialize(JsonSerializer.Deserialize(CommandValue["InputList"], JsonSourceGenerationContext.Default.IEnumerableStringNaturalAlgorithmData).OrderBy((Item) => Item.Value, Comparer<string>.Create(ShlwApi.StrCmpLogicalW)), JsonSourceGenerationContext.Default.IEnumerableStringNaturalAlgorithmData));
                                break;
                            }
                        case AuxiliaryTrustProcessCommandType.MTPReplaceWithNewFile:
                            {
                                string Path = CommandValue["Path"];
                                string NewFilePath = CommandValue["NewFilePath"];

                                MTPPathAnalysis PathAnalysis = new MTPPathAnalysis(Path);

                                if (MTPManager.GetDeviceFromId(PathAnalysis.DeviceId) is MediaDevice Device)
                                {
                                    if (Device.FileExists(PathAnalysis.RelativePath))
                                    {
                                        Device.DeleteFile(PathAnalysis.RelativePath);
                                        Device.UploadFile(NewFilePath, PathAnalysis.RelativePath);
                                        Value.Add("Success", string.Empty);
                                    }
                                    else
                                    {
                                        Value.Add("Error", "MTP file is not found");
                                    }
                                }
                                else
                                {
                                    Value.Add("Error", "MTP device is not found");
                                }

                                break;
                            }
                        case AuxiliaryTrustProcessCommandType.MTPDownloadAndGetHandle:
                            {
                                string Path = CommandValue["Path"];
                                AccessMode Mode = Enum.Parse<AccessMode>(CommandValue["AccessMode"]);
                                OptimizeOption Option = Enum.Parse<OptimizeOption>(CommandValue["OptimizeOption"]);

                                MTPPathAnalysis PathAnalysis = new MTPPathAnalysis(Path);

                                if (MTPManager.GetDeviceFromId(PathAnalysis.DeviceId) is MediaDevice Device)
                                {
                                    if (Device.FileExists(PathAnalysis.RelativePath))
                                    {
                                        string TempFilePath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"{Guid.NewGuid().ToString("N")}{System.IO.Path.GetExtension(PathAnalysis.RelativePath)}");

                                        Device.DownloadFile(PathAnalysis.RelativePath, TempFilePath);

                                        Kernel32.FileAccess Access = Mode switch
                                        {
                                            AccessMode.Read => Kernel32.FileAccess.FILE_GENERIC_READ,
                                            AccessMode.ReadWrite or AccessMode.Exclusive => Kernel32.FileAccess.FILE_GENERIC_READ | Kernel32.FileAccess.FILE_GENERIC_WRITE,
                                            AccessMode.Write => Kernel32.FileAccess.FILE_GENERIC_WRITE,
                                            _ => throw new NotSupportedException()
                                        };

                                        FileShare Share = Mode switch
                                        {
                                            AccessMode.Read => FileShare.Read,
                                            AccessMode.ReadWrite or AccessMode.Write => FileShare.ReadWrite,
                                            AccessMode.Exclusive => FileShare.None,
                                            _ => throw new NotSupportedException()
                                        };

                                        FileFlagsAndAttributes Flags = FileFlagsAndAttributes.FILE_FLAG_OVERLAPPED | FileFlagsAndAttributes.FILE_FLAG_DELETE_ON_CLOSE | Option switch
                                        {
                                            OptimizeOption.None => FileFlagsAndAttributes.FILE_ATTRIBUTE_NORMAL,
                                            OptimizeOption.Sequential => FileFlagsAndAttributes.FILE_FLAG_SEQUENTIAL_SCAN,
                                            OptimizeOption.RandomAccess => FileFlagsAndAttributes.FILE_FLAG_RANDOM_ACCESS,
                                            _ => throw new NotSupportedException()
                                        };

                                        using (Kernel32.SafeHFILE Handle = Kernel32.CreateFile(TempFilePath, Access, Share, null, FileMode.OpenOrCreate, Flags))
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
                                    else
                                    {
                                        Value.Add("Error", "MTP file is not found");
                                    }
                                }
                                else
                                {
                                    Value.Add("Error", "MTP device is not found");
                                }

                                break;
                            }
                        case AuxiliaryTrustProcessCommandType.MTPCreateSubItem:
                            {
                                string Path = CommandValue["Path"];
                                string Name = CommandValue["Name"];
                                CreateType Type = Enum.Parse<CreateType>(CommandValue["Type"]);
                                CollisionOptions Option = Enum.Parse<CollisionOptions>(CommandValue["Option"]);

                                MTPPathAnalysis PathAnalysis = new MTPPathAnalysis(Path);

                                if (MTPManager.GetDeviceFromId(PathAnalysis.DeviceId) is MediaDevice Device)
                                {
                                    static FileAttributes ConvertAttribute(MediaFileAttributes Attributes)
                                    {
                                        FileAttributes Return = 0;

                                        if (Attributes.HasFlag(MediaFileAttributes.Hidden))
                                        {
                                            Return |= FileAttributes.Hidden;
                                        }
                                        else if (Attributes.HasFlag(MediaFileAttributes.System))
                                        {
                                            Return |= FileAttributes.System;
                                        }
                                        else if (Attributes.HasFlag(MediaFileAttributes.Object) || Attributes.HasFlag(MediaFileAttributes.Directory))
                                        {
                                            Return |= FileAttributes.Directory;
                                        }

                                        if (Return == 0)
                                        {
                                            Return |= FileAttributes.Normal;
                                        }

                                        return Return;
                                    }

                                    if (Device.DirectoryExists(PathAnalysis.RelativePath))
                                    {
                                        switch (Type)
                                        {
                                            case CreateType.File:
                                                {
                                                    switch (Option)
                                                    {
                                                        case CollisionOptions.None:
                                                            {
                                                                string TargetPath = $"{PathAnalysis.RelativePath}\\{Name}";

                                                                if (Device.FileExists(TargetPath))
                                                                {
                                                                    throw new Exception($"{TargetPath} is already exists");
                                                                }

                                                                break;
                                                            }
                                                        case CollisionOptions.Skip:
                                                            {
                                                                string TargetPath = $"{PathAnalysis.RelativePath}\\{Name}";

                                                                if (!Device.FileExists(TargetPath))
                                                                {
                                                                    Device.UploadFile(new MemoryStream(), TargetPath);
                                                                }

                                                                MediaFileInfo File = Device.GetFileInfo(TargetPath);
                                                                Value.Add("Success", JsonSerializer.Serialize(new MTPFileData(string.IsNullOrEmpty(Device.FriendlyName) ? Device.Description : Device.FriendlyName, Device.DeviceId + File.FullName, File.Length, ConvertAttribute(File.Attributes), File.CreationTime.GetValueOrDefault().ToLocalTime(), File.LastWriteTime.GetValueOrDefault().ToLocalTime()), JsonSourceGenerationContext.Default.MTPFileData));

                                                                break;
                                                            }
                                                        case CollisionOptions.RenameOnCollision:
                                                            {
                                                                string TargetPath = $"{PathAnalysis.RelativePath}\\{Name}";

                                                                if (Device.FileExists(TargetPath) || Device.DirectoryExists(TargetPath))
                                                                {
                                                                    string UniquePath = TargetPath;
                                                                    string NameWithoutExt = System.IO.Path.GetFileNameWithoutExtension(UniquePath);
                                                                    string Extension = System.IO.Path.GetExtension(UniquePath);

                                                                    for (ushort Count = 1; Device.DirectoryExists(UniquePath) || Device.FileExists(UniquePath); Count++)
                                                                    {
                                                                        if (Regex.IsMatch(NameWithoutExt, @".*\(\d+\)"))
                                                                        {
                                                                            UniquePath = $"{System.IO.Path.Combine(PathAnalysis.RelativePath, NameWithoutExt.Substring(0, NameWithoutExt.LastIndexOf("(", StringComparison.InvariantCultureIgnoreCase)))}({Count}){Extension}";
                                                                        }
                                                                        else
                                                                        {
                                                                            UniquePath = $"{System.IO.Path.Combine(PathAnalysis.RelativePath, NameWithoutExt)} ({Count}){Extension}";
                                                                        }
                                                                    }

                                                                    TargetPath = UniquePath;
                                                                }

                                                                Device.UploadFile(new MemoryStream(), TargetPath);
                                                                MediaFileInfo File = Device.GetFileInfo(TargetPath);
                                                                Value.Add("Success", JsonSerializer.Serialize(new MTPFileData(string.IsNullOrEmpty(Device.FriendlyName) ? Device.Description : Device.FriendlyName, Device.DeviceId + File.FullName, File.Length, ConvertAttribute(File.Attributes), File.CreationTime.GetValueOrDefault().ToLocalTime(), File.LastWriteTime.GetValueOrDefault().ToLocalTime()), JsonSourceGenerationContext.Default.MTPFileData));

                                                                break;
                                                            }
                                                        case CollisionOptions.OverrideOnCollision:
                                                            {
                                                                string TargetPath = $"{PathAnalysis.RelativePath}\\{Name}";

                                                                if (Device.FileExists(TargetPath))
                                                                {
                                                                    Device.DeleteFile(TargetPath);
                                                                }

                                                                Device.UploadFile(new MemoryStream(), TargetPath);
                                                                MediaFileInfo File = Device.GetFileInfo(TargetPath);
                                                                Value.Add("Success", JsonSerializer.Serialize(new MTPFileData(string.IsNullOrEmpty(Device.FriendlyName) ? Device.Description : Device.FriendlyName, Device.DeviceId + File.FullName, File.Length, ConvertAttribute(File.Attributes), File.CreationTime.GetValueOrDefault().ToLocalTime(), File.LastWriteTime.GetValueOrDefault().ToLocalTime()), JsonSourceGenerationContext.Default.MTPFileData));

                                                                break;
                                                            }
                                                    }

                                                    break;
                                                }
                                            case CreateType.Folder:
                                                {
                                                    switch (Option)
                                                    {
                                                        case CollisionOptions.Skip:
                                                            {
                                                                string TargetPath = $"{PathAnalysis.RelativePath}\\{Name}";

                                                                if (!Device.DirectoryExists(TargetPath))
                                                                {
                                                                    Device.CreateDirectory(TargetPath);
                                                                }

                                                                MediaDirectoryInfo Directory = Device.GetDirectoryInfo(TargetPath);
                                                                Value.Add("Success", JsonSerializer.Serialize(new MTPFileData(string.IsNullOrEmpty(Device.FriendlyName) ? Device.Description : Device.FriendlyName, Device.DeviceId + Directory.FullName, 0, ConvertAttribute(Directory.Attributes), Directory.CreationTime.GetValueOrDefault().ToLocalTime(), Directory.LastWriteTime.GetValueOrDefault().ToLocalTime()), JsonSourceGenerationContext.Default.MTPFileData));

                                                                break;
                                                            }
                                                        case CollisionOptions.RenameOnCollision:
                                                            {
                                                                string TargetPath = $"{PathAnalysis.RelativePath}\\{Name}";

                                                                if (Device.FileExists(TargetPath) || Device.DirectoryExists(TargetPath))
                                                                {
                                                                    string UniquePath = TargetPath;

                                                                    for (ushort Count = 1; Device.DirectoryExists(UniquePath) || Device.FileExists(UniquePath); Count++)
                                                                    {
                                                                        if (Regex.IsMatch(Name, @".*\(\d+\)"))
                                                                        {
                                                                            UniquePath = $"{PathAnalysis.RelativePath}{Name.Substring(0, Name.LastIndexOf("(", StringComparison.InvariantCultureIgnoreCase))}({Count})";
                                                                        }
                                                                        else
                                                                        {
                                                                            UniquePath = $"{PathAnalysis.RelativePath}{Name} ({Count})";
                                                                        }
                                                                    }

                                                                    TargetPath = UniquePath;
                                                                }

                                                                Device.CreateDirectory(TargetPath);
                                                                MediaDirectoryInfo Directory = Device.GetDirectoryInfo(TargetPath);
                                                                Value.Add("Success", JsonSerializer.Serialize(new MTPFileData(string.IsNullOrEmpty(Device.FriendlyName) ? Device.Description : Device.FriendlyName, Device.DeviceId + Directory.FullName, 0, ConvertAttribute(Directory.Attributes), Directory.CreationTime.GetValueOrDefault().ToLocalTime(), Directory.LastWriteTime.GetValueOrDefault().ToLocalTime()), JsonSourceGenerationContext.Default.MTPFileData));

                                                                break;
                                                            }
                                                        case CollisionOptions.OverrideOnCollision:
                                                            {
                                                                string TargetPath = $"{PathAnalysis.RelativePath}\\{Name}";

                                                                if (Device.DirectoryExists(TargetPath))
                                                                {
                                                                    Device.DeleteDirectory(TargetPath, true);
                                                                }

                                                                Device.CreateDirectory(TargetPath);
                                                                MediaDirectoryInfo Directory = Device.GetDirectoryInfo(TargetPath);
                                                                Value.Add("Success", JsonSerializer.Serialize(new MTPFileData(string.IsNullOrEmpty(Device.FriendlyName) ? Device.Description : Device.FriendlyName, Device.DeviceId + Directory.FullName, 0, ConvertAttribute(Directory.Attributes), Directory.CreationTime.GetValueOrDefault().ToLocalTime(), Directory.LastWriteTime.GetValueOrDefault().ToLocalTime()), JsonSourceGenerationContext.Default.MTPFileData));

                                                                break;
                                                            }
                                                    }

                                                    break;
                                                }
                                        }
                                    }
                                    else
                                    {
                                        Value.Add("Error", "MTP folder is not found");
                                    }
                                }
                                else
                                {
                                    Value.Add("Error", "MTP device is not found");
                                }

                                break;
                            }
                        case AuxiliaryTrustProcessCommandType.MTPGetDriveVolumnData:
                            {
                                string DeviceId = CommandValue["DeviceId"];

                                if (MTPManager.GetDeviceFromId(DeviceId) is MediaDevice Device)
                                {
                                    if (Device.GetDrives()?.FirstOrDefault() is MediaDriveInfo DriveInfo)
                                    {
                                        Value.Add("Success", JsonSerializer.Serialize(new MTPDriveVolumnData
                                        (
                                            string.IsNullOrEmpty(Device.FriendlyName) ? Device.Description : Device.FriendlyName,
                                            DriveInfo.DriveFormat,
                                            Convert.ToUInt64(DriveInfo.TotalSize),
                                            Convert.ToUInt64(DriveInfo.AvailableFreeSpace)
                                        ), JsonSourceGenerationContext.Default.MTPDriveVolumnData));
                                    }
                                    else
                                    {
                                        Value.Add("Error", "No available data for MTPDriveData");
                                    }
                                }
                                else
                                {
                                    Value.Add("Error", "MTP device is not found");
                                }

                                break;
                            }
                        case AuxiliaryTrustProcessCommandType.MTPCheckExists:
                            {
                                string Path = CommandValue["Path"];

                                MTPPathAnalysis PathAnalysis = new MTPPathAnalysis(Path);

                                if (MTPManager.GetDeviceFromId(PathAnalysis.DeviceId) is MediaDevice Device)
                                {
                                    Value.Add("Success", Convert.ToString(Device.DirectoryExists(PathAnalysis.RelativePath) || Device.FileExists(PathAnalysis.RelativePath)));
                                }
                                else
                                {
                                    Value.Add("Error", "MTP device is not found");
                                }

                                break;
                            }
                        case AuxiliaryTrustProcessCommandType.MTPGetItem:
                            {
                                string Path = CommandValue["Path"];

                                MTPPathAnalysis PathAnalysis = new MTPPathAnalysis(Path);

                                static FileAttributes ConvertAttribute(MediaFileAttributes Attributes)
                                {
                                    FileAttributes Return = 0;

                                    if (Attributes.HasFlag(MediaFileAttributes.Hidden))
                                    {
                                        Return |= FileAttributes.Hidden;
                                    }
                                    else if (Attributes.HasFlag(MediaFileAttributes.System))
                                    {
                                        Return |= FileAttributes.System;
                                    }
                                    else if (Attributes.HasFlag(MediaFileAttributes.Object) || Attributes.HasFlag(MediaFileAttributes.Directory))
                                    {
                                        Return |= FileAttributes.Directory;
                                    }

                                    if (Return == 0)
                                    {
                                        Return |= FileAttributes.Normal;
                                    }

                                    return Return;
                                }

                                if (MTPManager.GetDeviceFromId(PathAnalysis.DeviceId) is MediaDevice Device)
                                {
                                    MediaFileSystemInfo Item = null;

                                    if (Device.DirectoryExists(PathAnalysis.RelativePath))
                                    {
                                        Item = Device.GetDirectoryInfo(PathAnalysis.RelativePath);
                                    }
                                    else if (Device.FileExists(PathAnalysis.RelativePath))
                                    {
                                        Item = Device.GetFileInfo(PathAnalysis.RelativePath);
                                    }

                                    if (Item != null)
                                    {
                                        Value.Add("Success", JsonSerializer.Serialize(new MTPFileData(string.IsNullOrEmpty(Device.FriendlyName) ? Device.Description : Device.FriendlyName, Device.DeviceId + Item.FullName, Item.Length, ConvertAttribute(Item.Attributes), Item.CreationTime.GetValueOrDefault().ToLocalTime(), Item.LastWriteTime.GetValueOrDefault().ToLocalTime()), JsonSourceGenerationContext.Default.MTPFileData));
                                    }
                                    else
                                    {
                                        Value.Add("Error", "MTP folder or file is not found");
                                    }
                                }
                                else
                                {
                                    Value.Add("Error", "MTP device is not found");
                                }

                                break;
                            }
                        case AuxiliaryTrustProcessCommandType.MTPGetChildItems:
                            {
                                string Path = CommandValue["Path"];
                                string Type = CommandValue["Type"];
                                bool IncludeHiddenItems = Convert.ToBoolean(CommandValue["IncludeHiddenItems"]);
                                bool IncludeSystemItems = Convert.ToBoolean(CommandValue["IncludeSystemItems"]);
                                bool IncludeAllSubItems = Convert.ToBoolean(CommandValue["IncludeAllSubItems"]);

                                MTPPathAnalysis PathAnalysis = new MTPPathAnalysis(Path);

                                static FileAttributes ConvertAttribute(MediaFileAttributes Attributes)
                                {
                                    FileAttributes Return = 0;

                                    if (Attributes.HasFlag(MediaFileAttributes.Hidden))
                                    {
                                        Return |= FileAttributes.Hidden;
                                    }
                                    else if (Attributes.HasFlag(MediaFileAttributes.System))
                                    {
                                        Return |= FileAttributes.System;
                                    }
                                    else if (Attributes.HasFlag(MediaFileAttributes.Object) || Attributes.HasFlag(MediaFileAttributes.Directory))
                                    {
                                        Return |= FileAttributes.Directory;
                                    }

                                    if (Return == 0)
                                    {
                                        Return |= FileAttributes.Normal;
                                    }

                                    return Return;
                                }

                                if (MTPManager.GetDeviceFromId(PathAnalysis.DeviceId) is MediaDevice Device)
                                {
                                    if (Device.DirectoryExists(PathAnalysis.RelativePath))
                                    {
                                        IEnumerable<MediaFileSystemInfo> BasicItems = Type switch
                                        {
                                            "All" => Device.GetDirectoryInfo(PathAnalysis.RelativePath).EnumerateFileSystemInfos("*", IncludeAllSubItems ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly),
                                            "File" => Device.GetDirectoryInfo(PathAnalysis.RelativePath).EnumerateFiles("*", IncludeAllSubItems ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly),
                                            "Folder" => Device.GetDirectoryInfo(PathAnalysis.RelativePath).EnumerateDirectories("*", IncludeAllSubItems ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly),
                                            _ => throw new NotSupportedException()
                                        };

                                        IEnumerable<MediaFileSystemInfo> MTPSubItems = BasicItems.Where((Item) => IncludeSystemItems || !Item.Attributes.HasFlag(MediaFileAttributes.System))
                                                                                                 .Where((Item) => IncludeHiddenItems || !Item.Attributes.HasFlag(MediaFileAttributes.Hidden));

                                        List<MTPFileData> Result = new List<MTPFileData>();

                                        foreach (MediaFileSystemInfo Item in MTPSubItems)
                                        {
                                            Result.Add(new MTPFileData(string.IsNullOrEmpty(Device.FriendlyName) ? Device.Description : Device.FriendlyName, Device.DeviceId + Item.FullName, Item.Length, ConvertAttribute(Item.Attributes), new DateTimeOffset(Item.CreationTime.GetValueOrDefault().ToLocalTime()), new DateTimeOffset(Item.LastWriteTime.GetValueOrDefault().ToLocalTime())));

                                            if (Cancellation.Token.IsCancellationRequested)
                                            {
                                                break;
                                            }
                                        }

                                        Value.Add("Success", JsonSerializer.Serialize(Result, JsonSourceGenerationContext.Default.IEnumerableMTPFileData));
                                    }
                                    else
                                    {
                                        Value.Add("Error", "MTP folder is not found");
                                    }
                                }
                                else
                                {
                                    Value.Add("Error", "MTP device is not found");
                                }

                                break;
                            }
                        case AuxiliaryTrustProcessCommandType.ConvertToLongPath:
                            {
                                Value.Add("Success", Helper.ConvertShortPathToLongPath(CommandValue["Path"]));

                                break;
                            }
                        case AuxiliaryTrustProcessCommandType.GetFriendlyTypeName:
                            {
                                string Extension = CommandValue["Extension"];
                                string FriendlyName = ExtensionAssociation.GetFriendlyTypeNameFromExtension(Extension);

                                if (string.IsNullOrEmpty(FriendlyName))
                                {
                                    Value.Add("Success", Extension);
                                }
                                else
                                {
                                    Value.Add("Success", FriendlyName);
                                }

                                break;
                            }
                        case AuxiliaryTrustProcessCommandType.GetPermissions:
                            {
                                string Path = CommandValue["Path"];

                                if (Directory.Exists(Path) || File.Exists(Path))
                                {
                                    Value.Add("Success", JsonSerializer.Serialize(StorageItemController.GetAllAccountPermissions(Path), JsonSourceGenerationContext.Default.IEnumerablePermissionDataPackage));
                                }
                                else
                                {
                                    Value.Add("Error", "File or directory is not found");
                                }

                                break;
                            }
                        case AuxiliaryTrustProcessCommandType.SetDriveLabel:
                            {
                                string Path = CommandValue["Path"];
                                string DriveLabelName = CommandValue["DriveLabelName"];

                                if (System.IO.Path.GetPathRoot(Path).Equals(Path, StringComparison.OrdinalIgnoreCase))
                                {
                                    try
                                    {
                                        IDictionary<string, string> ResultMap = CreateNewProcessAsElevatedAndWaitForResult(new ElevationSetDriveLabelData(Path, DriveLabelName), CancelToken: Cancellation.Token);

                                        foreach (KeyValuePair<string, string> Result in ResultMap)
                                        {
                                            Value.Add(Result);
                                        }
                                    }
                                    catch (OperationCanceledException)
                                    {
                                        Value.Add("Error_Cancelled", "Operation is cancelled");
                                    }
                                }
                                else
                                {
                                    Value.Add("Error", "Path is not a drive root path");
                                }

                                break;
                            }
                        case AuxiliaryTrustProcessCommandType.SetDriveIndexStatus:
                            {
                                string Path = CommandValue["Path"];
                                bool ApplyToSubItems = Convert.ToBoolean(CommandValue["ApplyToSubItems"]);
                                bool AllowIndex = Convert.ToBoolean(CommandValue["AllowIndex"]);

                                if (System.IO.Path.GetPathRoot(Path).Equals(Path, StringComparison.OrdinalIgnoreCase))
                                {
                                    try
                                    {
                                        IDictionary<string, string> ResultMap = CreateNewProcessAsElevatedAndWaitForResult(new ElevationSetDriveIndexStatusData(Path, AllowIndex, ApplyToSubItems), CancelToken: Cancellation.Token);

                                        foreach (KeyValuePair<string, string> Result in ResultMap)
                                        {
                                            Value.Add(Result);
                                        }
                                    }
                                    catch (OperationCanceledException)
                                    {
                                        Value.Add("Error_Cancelled", "Operation is cancelled");
                                    }
                                }
                                else
                                {
                                    Value.Add("Error", "Path is not a drive root path");
                                }

                                break;
                            }
                        case AuxiliaryTrustProcessCommandType.GetDriveIndexStatus:
                            {
                                string Path = CommandValue["Path"];

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
                        case AuxiliaryTrustProcessCommandType.SetDriveCompressionStatus:
                            {
                                string Path = CommandValue["Path"];
                                bool ApplyToSubItems = Convert.ToBoolean(CommandValue["ApplyToSubItems"]);
                                bool IsSetCompressionStatus = Convert.ToBoolean(CommandValue["IsSetCompressionStatus"]);

                                if (System.IO.Path.GetPathRoot(Path).Equals(Path, StringComparison.OrdinalIgnoreCase))
                                {
                                    try
                                    {
                                        IDictionary<string, string> ResultMap = CreateNewProcessAsElevatedAndWaitForResult(new ElevationSetDriveCompressStatusData(Path, IsSetCompressionStatus, ApplyToSubItems), CancelToken: Cancellation.Token);

                                        foreach (KeyValuePair<string, string> Result in ResultMap)
                                        {
                                            Value.Add(Result);
                                        }
                                    }
                                    catch (OperationCanceledException)
                                    {
                                        Value.Add("Error_Cancelled", "Operation is cancelled");
                                    }
                                }
                                else
                                {
                                    Value.Add("Error", "Path is not a drive root path");
                                }

                                break;
                            }
                        case AuxiliaryTrustProcessCommandType.GetDriveCompressionStatus:
                            {
                                string Path = CommandValue["Path"];

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
                        case AuxiliaryTrustProcessCommandType.DetectEncoding:
                            {
                                string Path = CommandValue["Path"];

                                DetectionResult Detection = CharsetDetector.DetectFromFile(Path);
                                DetectionDetail Details = Detection.Detected;

                                if ((Details?.Confidence).GetValueOrDefault() >= 0.7f)
                                {
                                    Value.Add("Success", Convert.ToString(Details.Encoding.CodePage));
                                }
                                else
                                {
                                    Value.Add("Error", "Detect encoding failed");
                                }

                                break;
                            }
                        case AuxiliaryTrustProcessCommandType.GetAllEncodings:
                            {
                                Value.Add("Success", JsonSerializer.Serialize(Encoding.GetEncodings().Select((Encoding) => Encoding.CodePage), JsonSourceGenerationContext.Default.IEnumerableInt32));
                                break;
                            }
                        case AuxiliaryTrustProcessCommandType.Test:
                            {
                                Value.Add("Success", string.Empty);
                                break;
                            }
                        case AuxiliaryTrustProcessCommandType.GetProperties:
                            {
                                string Path = CommandValue["Path"];

                                IReadOnlyList<string> Properties = JsonSerializer.Deserialize(CommandValue["Properties"], JsonSourceGenerationContext.Default.IEnumerableString).ToArray();

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
                                                LogTracer.Log(ex, $"Could not get the property from path: \"{Path}\", value: \"{Property}\"");
                                                Result.Add(Property, string.Empty);
                                            }
                                        }
                                    }

                                    Value.Add("Success", JsonSerializer.Serialize(Result, JsonSourceGenerationContext.Default.IDictionaryStringString));
                                }
                                else
                                {
                                    Value.Add("Error", "Path is not found");
                                }

                                break;
                            }
                        case AuxiliaryTrustProcessCommandType.SetTaskBarProgress:
                            {
                                ulong ProgressValue = Math.Min(100, Math.Max(0, Convert.ToUInt64(CommandValue["ProgressValue"])));

                                if (Helper.GetWindowInformationFromUwpApplication(Convert.ToUInt32((ExplorerProcess?.Id).GetValueOrDefault())) is WindowInformation Info)
                                {
                                    if (Info.IsValidInfomation)
                                    {
                                        switch (ProgressValue)
                                        {
                                            case 0:
                                                {
                                                    TaskbarList.SetProgressState(Info.ApplicationFrameWindowHandle, TaskbarButtonProgressState.Indeterminate);
                                                    break;
                                                }
                                            case 100:
                                                {
                                                    TaskbarList.SetProgressState(Info.ApplicationFrameWindowHandle, TaskbarButtonProgressState.None);
                                                    break;
                                                }
                                            default:
                                                {
                                                    TaskbarList.SetProgressState(Info.ApplicationFrameWindowHandle, TaskbarButtonProgressState.Normal);
                                                    TaskbarList.SetProgressValue(Info.ApplicationFrameWindowHandle, ProgressValue, 100);
                                                    break;
                                                }
                                        }
                                    }
                                }

                                Value.Add("Success", string.Empty);

                                break;
                            }
                        case AuxiliaryTrustProcessCommandType.MapToUncPath:
                            {
                                Value.Add("Success", Helper.MapUncPathToDrivePath(Convert.ToString(CommandValue["UncPath"])));

                                break;
                            }
                        case AuxiliaryTrustProcessCommandType.GetDirectoryMonitorHandle:
                            {
                                if ((ExplorerProcess?.Handle.CheckIfValidPtr()).GetValueOrDefault())
                                {
                                    string ExecutePath = CommandValue["ExecutePath"];

                                    using (Kernel32.SafeHFILE Handle = Kernel32.CreateFile(ExecutePath, Kernel32.FileAccess.FILE_LIST_DIRECTORY, FileShare.Read | FileShare.Write | FileShare.Delete, null, FileMode.Open, FileFlagsAndAttributes.FILE_FLAG_BACKUP_SEMANTICS))
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

                                break;
                            }
                        case AuxiliaryTrustProcessCommandType.GetNativeHandle:
                            {
                                if ((ExplorerProcess?.Handle.CheckIfValidPtr()).GetValueOrDefault())
                                {
                                    string ExecutePath = CommandValue["ExecutePath"];

                                    if (File.Exists(ExecutePath) || Directory.Exists(ExecutePath))
                                    {
                                        AccessMode Mode = Enum.Parse<AccessMode>(CommandValue["AccessMode"]);
                                        OptimizeOption Option = Enum.Parse<OptimizeOption>(CommandValue["OptimizeOption"]);

                                        Kernel32.FileAccess Access = Mode switch
                                        {
                                            AccessMode.Read => Kernel32.FileAccess.FILE_GENERIC_READ,
                                            AccessMode.ReadWrite or AccessMode.Exclusive => Kernel32.FileAccess.FILE_GENERIC_READ | Kernel32.FileAccess.FILE_GENERIC_WRITE,
                                            AccessMode.Write => Kernel32.FileAccess.FILE_GENERIC_WRITE,
                                            _ => throw new NotSupportedException()
                                        };

                                        FileShare Share = Mode switch
                                        {
                                            AccessMode.Read => FileShare.Read,
                                            AccessMode.ReadWrite or AccessMode.Write => FileShare.ReadWrite,
                                            AccessMode.Exclusive => FileShare.None,
                                            _ => throw new NotSupportedException()
                                        };

                                        FileFlagsAndAttributes Flags;

                                        if (Directory.Exists(ExecutePath))
                                        {
                                            Flags = FileFlagsAndAttributes.FILE_FLAG_BACKUP_SEMANTICS;
                                        }
                                        else
                                        {
                                            Flags = FileFlagsAndAttributes.FILE_FLAG_OVERLAPPED;

                                            // About SEQUENTIAL_SCAN & RANDOM_ACCESS flags
                                            // These two flags takes no effect if we only write data into the file (Only takes effect on ReadFile related API)
                                            // https://devblogs.microsoft.com/oldnewthing/20120120-00/?p=8493
                                            if (Mode != AccessMode.Write && Option != OptimizeOption.None)
                                            {
                                                Flags |= Option switch
                                                {
                                                    OptimizeOption.Sequential => FileFlagsAndAttributes.FILE_FLAG_SEQUENTIAL_SCAN,
                                                    OptimizeOption.RandomAccess => FileFlagsAndAttributes.FILE_FLAG_RANDOM_ACCESS,
                                                    _ => throw new NotSupportedException()
                                                };
                                            }
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
                        case AuxiliaryTrustProcessCommandType.GetThumbnail:
                            {
                                string ExecutePath = CommandValue["ExecutePath"];

                                if (File.Exists(ExecutePath) || Directory.Exists(ExecutePath))
                                {
                                    using (ShellItem Item = new ShellItem(ExecutePath))
                                    using (Gdi32.SafeHBITMAP HBitmap = Item.GetImage(new SIZE(150, 150), ShellItemGetImageOptions.BiggerSizeOk))
                                    using (Bitmap OriginBitmap = Image.FromHbitmap(HBitmap.DangerousGetHandle()))
                                    using (MemoryStream Stream = new MemoryStream())
                                    {
                                        using (Bitmap ConvertedBitmap = OriginBitmap.ConvertToBitmapWithAlphaChannel())
                                        {
                                            ConvertedBitmap?.Save(Stream, ImageFormat.Png);
                                        }

                                        Value.Add("Success", JsonSerializer.Serialize(Stream.ToArray(), JsonSourceGenerationContext.Default.IEnumerableByte));
                                    }
                                }
                                else
                                {
                                    Shell32.SHFILEINFO Info = new Shell32.SHFILEINFO();

                                    IntPtr Result = Shell32.SHGetFileInfo(ExecutePath, FileAttributes.Normal, ref Info, Marshal.SizeOf<Shell32.SHFILEINFO>(), Shell32.SHGFI.SHGFI_USEFILEATTRIBUTES | Shell32.SHGFI.SHGFI_ICON | Shell32.SHGFI.SHGFI_LARGEICON);

                                    if (Result.CheckIfValidPtr() && !Info.hIcon.IsNull)
                                    {
                                        try
                                        {
                                            using (Gdi32.SafeHBITMAP HBitmap = Info.hIcon.ToHBITMAP())
                                            using (Bitmap OriginBitmap = Bitmap.FromHbitmap(HBitmap.DangerousGetHandle()))
                                            using (MemoryStream Stream = new MemoryStream())
                                            {
                                                using (Bitmap ConvertedBitmap = OriginBitmap.ConvertToBitmapWithAlphaChannel())
                                                {
                                                    ConvertedBitmap?.Save(Stream, ImageFormat.Png);
                                                }

                                                Value.Add("Success", JsonSerializer.Serialize(Stream.ToArray(), JsonSourceGenerationContext.Default.IEnumerableByte));
                                            }
                                        }
                                        finally
                                        {
                                            User32.DestroyIcon(Info.hIcon);
                                        }
                                    }
                                    else
                                    {
                                        Value.Add("Error", "Could not get the thumbnail");
                                    }
                                }

                                break;
                            }
                        case AuxiliaryTrustProcessCommandType.LaunchUWP:
                            {
                                if (CommandValue.TryGetValue("PackageFamilyName", out string PackageFamilyName))
                                {
                                    if (string.IsNullOrEmpty(PackageFamilyName))
                                    {
                                        Value.Add("Error", "PackageFamilyName could not empty");
                                    }
                                    else
                                    {
                                        if (Helper.LaunchApplicationFromPackageFamilyName(PackageFamilyName, JsonSerializer.Deserialize(CommandValue["LaunchPathArray"], JsonSourceGenerationContext.Default.IEnumerableString).ToArray()))
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
                                        if (Helper.LaunchApplicationFromAppUserModelId(AppUserModelId, JsonSerializer.Deserialize(CommandValue["LaunchPathArray"], JsonSourceGenerationContext.Default.IEnumerableString).ToArray()))
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
                        case AuxiliaryTrustProcessCommandType.GetMIMEContentType:
                            {
                                string ExecutePath = CommandValue["ExecutePath"];

                                Value.Add("Success", Helper.GetMIMEFromPath(ExecutePath));

                                break;
                            }
                        case AuxiliaryTrustProcessCommandType.GetTooltipText:
                            {
                                string Path = CommandValue["Path"];

                                if (File.Exists(Path) || Directory.Exists(Path))
                                {
                                    try
                                    {
                                        using (ShellItem Item = new ShellItem(Path))
                                        {
                                            Value.Add("Success", Task.Run(() => Item.GetToolTip(ShellItemToolTipOptions.AllowDelay)).AsCancellable(Cancellation.Token).Result);
                                        }
                                    }
                                    catch (OperationCanceledException)
                                    {
                                        Value.Add("Success", string.Empty);
                                    }
                                    catch (Exception ex)
                                    {
                                        Value.Add("Error", $"Could not get the tooltip, message: {ex.Message}");
                                    }
                                }
                                else
                                {
                                    Value.Add("Error", "Path is not exists");
                                }

                                break;
                            }
                        case AuxiliaryTrustProcessCommandType.CheckIfEverythingAvailable:
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
                        case AuxiliaryTrustProcessCommandType.SearchByEverything:
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
                                        Value.Add("Success", JsonSerializer.Serialize(SearchResult, JsonSourceGenerationContext.Default.IEnumerableString));
                                    }
                                    else
                                    {
                                        EverythingConnector.StateCode Code = EverythingConnector.GetLastErrorCode();

                                        if (Code == EverythingConnector.StateCode.OK)
                                        {
                                            Value.Add("Success", JsonSerializer.Serialize(SearchResult, JsonSourceGenerationContext.Default.IEnumerableString));
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
                        case AuxiliaryTrustProcessCommandType.GetContextMenuItems:
                            {
                                string[] ExecutePath = JsonSerializer.Deserialize(CommandValue["ExecutePath"], JsonSourceGenerationContext.Default.IEnumerableString).ToArray();

                                if (ExecutePath.Length > 0)
                                {
                                    Value.Add("Success", JsonSerializer.Serialize(ContextMenu.Current.GetContextMenuItems(ExecutePath, Convert.ToBoolean(CommandValue["IncludeExtensionItem"])), JsonSourceGenerationContext.Default.IEnumerableContextMenuPackage));
                                }
                                else
                                {
                                    Value.Add("Error", "Argument could not be empty");
                                }

                                break;
                            }
                        case AuxiliaryTrustProcessCommandType.InvokeContextMenuItem:
                            {
                                ContextMenuPackage Package = JsonSerializer.Deserialize(CommandValue["DataPackage"], JsonSourceGenerationContext.Default.ContextMenuPackage);

                                if (ContextMenu.Current.InvokeVerb(Package))
                                {
                                    Value.Add("Success", string.Empty);
                                }
                                else
                                {
                                    Value.Add("Error", $"Execute Id: \"{Package.Id}\", Verb: \"{Package.Verb}\" failed");
                                }

                                break;
                            }
                        case AuxiliaryTrustProcessCommandType.CreateLink:
                            {
                                LinkFileData Package = JsonSerializer.Deserialize(CommandValue["DataPackage"], JsonSourceGenerationContext.Default.LinkFileData);

                                string Arguments = null;

                                if ((Package.Arguments?.Any()).GetValueOrDefault())
                                {
                                    Arguments = string.Join(" ", Package.Arguments.Select((Para) => Para.Contains(' ') ? $"\"{Para.Trim('\"')}\"" : Para));
                                }

                                string UniquePath = Helper.GenerateUniquePathOnLocal(Package.LinkPath, CreateType.File);

                                using (ShellLink Link = ShellLink.Create(UniquePath, Package.LinkTargetPath, Package.Comment, Package.WorkDirectory, Arguments))
                                {
                                    Link.ShowState = Package.WindowState switch
                                    {
                                        WindowState.Normal => ShowWindowCommand.SW_SHOWNORMAL,
                                        WindowState.Minimized => ShowWindowCommand.SW_SHOWMINIMIZED,
                                        WindowState.Maximized => ShowWindowCommand.SW_SHOWMAXIMIZED,
                                        _ => throw new NotSupportedException()
                                    };
                                    Link.RunAsAdministrator = Package.NeedRunAsAdmin;

                                    if (Package.HotKey > 0)
                                    {
                                        Link.HotKey = ((Package.HotKey >= 112 && Package.HotKey <= 135) || (Package.HotKey >= 96 && Package.HotKey <= 105)) ? Package.HotKey : Macros.MAKEWORD(Package.HotKey, (byte)(User32.HOTKEYF.HOTKEYF_CONTROL | User32.HOTKEYF.HOTKEYF_ALT));
                                    }
                                }

                                if (File.Exists(UniquePath))
                                {
                                    Value.Add("Success", UniquePath);
                                }
                                else
                                {
                                    Value.Add("Error", "Could not create the lnk file as expected");
                                }

                                break;
                            }
                        case AuxiliaryTrustProcessCommandType.GetVariablePath:
                            {
                                string Variable = CommandValue["Variable"];

                                string EnvPath = Environment.GetEnvironmentVariable(Variable);

                                if (string.IsNullOrEmpty(EnvPath))
                                {
                                    Value.Add("Error", "Could not found EnvironmentVariable");
                                }
                                else
                                {
                                    Value.Add("Success", Path.GetFullPath(EnvPath));
                                }

                                break;
                            }
                        case AuxiliaryTrustProcessCommandType.GetVariablePathList:
                            {
                                string PartialVariable = CommandValue["PartialVariable"];


                                IEnumerable<KeyValuePair<string, string>> AllEnvironmentVariables = Environment.GetEnvironmentVariables().Cast<DictionaryEntry>()
                                                                                                                                        .Select((Pair) => new KeyValuePair<string, string>(Convert.ToString(Pair.Key), Path.GetFullPath(Convert.ToString(Pair.Value))))
                                                                                                                                        .Where((Pair) => Directory.Exists(Pair.Value));

                                if (string.IsNullOrEmpty(PartialVariable))
                                {
                                    Value.Add("Success", JsonSerializer.Serialize(AllEnvironmentVariables.Select((Pair) => new VariableDataPackage(Pair.Value, $"%{Pair.Key}%")), JsonSourceGenerationContext.Default.IEnumerableVariableDataPackage));
                                }
                                else if (PartialVariable.IndexOf('%') == 0 && PartialVariable.LastIndexOf('%') == 0)
                                {
                                    Value.Add("Success", JsonSerializer.Serialize(AllEnvironmentVariables.Where((Pair) => Pair.Key.StartsWith(PartialVariable[1..], StringComparison.OrdinalIgnoreCase))
                                                                                                         .Select((Pair) => new VariableDataPackage(Pair.Value, $"%{Pair.Key}%")), JsonSourceGenerationContext.Default.IEnumerableVariableDataPackage));
                                }
                                else
                                {
                                    Value.Add("Error", "Unexpected Partial Environmental String");
                                }

                                break;
                            }
                        case AuxiliaryTrustProcessCommandType.CreateNew:
                            {
                                CreateType Type = Enum.Parse<CreateType>(CommandValue["Type"]);

                                string CreateNewPath = CommandValue["NewPath"];
                                string UniquePath = Helper.GenerateUniquePathOnLocal(CreateNewPath, Type);

                                if (StorageItemController.CheckPermission(Path.GetDirectoryName(UniquePath) ?? UniquePath, Type == CreateType.File ? FileSystemRights.CreateFiles : FileSystemRights.CreateDirectories))
                                {
                                    if (StorageItemController.Create(Type, UniquePath))
                                    {
                                        Value.Add("Success", string.Empty);
                                    }
                                    else if (Marshal.GetLastWin32Error() == 5)
                                    {
                                        IDictionary<string, string> ResultMap = CreateNewProcessAsElevatedAndWaitForResult(new ElevationCreateNewData(Type, UniquePath), CancelToken: Cancellation.Token);

                                        foreach (KeyValuePair<string, string> Result in ResultMap)
                                        {
                                            Value.Add(Result);
                                        }
                                    }
                                    else
                                    {
                                        Value.Add("Error_Failure", new Win32Exception(Marshal.GetLastWin32Error()).Message);
                                    }
                                }
                                else
                                {
                                    IDictionary<string, string> ResultMap = CreateNewProcessAsElevatedAndWaitForResult(new ElevationCreateNewData(Type, UniquePath), CancelToken: Cancellation.Token);

                                    foreach (KeyValuePair<string, string> Result in ResultMap)
                                    {
                                        Value.Add(Result);
                                    }
                                }

                                break;
                            }
                        case AuxiliaryTrustProcessCommandType.Rename:
                            {
                                string ExecutePath = CommandValue["ExecutePath"];
                                string DesireName = CommandValue["DesireName"];

                                if (ExecutePath.StartsWith(@"\\?\"))
                                {
                                    MTPPathAnalysis PathAnalysis = new MTPPathAnalysis(ExecutePath);

                                    if (MTPManager.GetDeviceFromId(PathAnalysis.DeviceId) is MediaDevice Device)
                                    {
                                        if (Device.FileExists(PathAnalysis.RelativePath) || Device.DirectoryExists(PathAnalysis.RelativePath))
                                        {
                                            string UniqueNewName = Path.GetFileName(Helper.MTPGenerateUniquePath(Device, Path.Combine(Path.GetDirectoryName(PathAnalysis.RelativePath), DesireName), Device.DirectoryExists(PathAnalysis.RelativePath) ? CreateType.Folder : CreateType.File));
                                            Device.Rename(PathAnalysis.RelativePath, UniqueNewName);
                                            Value.Add("Success", UniqueNewName);
                                        }
                                        else
                                        {
                                            Value.Add("Error", "MTP file is not found");
                                        }
                                    }
                                    else
                                    {
                                        Value.Add("Error", "MTP device is not found");
                                    }
                                }
                                else if (File.Exists(ExecutePath) || Directory.Exists(ExecutePath))
                                {
                                    if (StorageItemController.CheckCaptured(ExecutePath))
                                    {
                                        Value.Add("Error_Capture", "One of these files was captured and could not be renamed");
                                    }
                                    else
                                    {
                                        try
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
                                                    IDictionary<string, string> ResultMap = CreateNewProcessAsElevatedAndWaitForResult(new ElevationRenameData(ExecutePath, DesireName), CancelToken: Cancellation.Token);

                                                    foreach (KeyValuePair<string, string> Result in ResultMap)
                                                    {
                                                        Value.Add(Result);
                                                    }
                                                }
                                                else
                                                {
                                                    Value.Add("Error_Failure", new Win32Exception(Marshal.GetLastWin32Error()).Message);
                                                }
                                            }
                                            else
                                            {
                                                IDictionary<string, string> ResultMap = CreateNewProcessAsElevatedAndWaitForResult(new ElevationRenameData(ExecutePath, DesireName), CancelToken: Cancellation.Token);

                                                foreach (KeyValuePair<string, string> Result in ResultMap)
                                                {
                                                    Value.Add(Result);
                                                }
                                            }
                                        }
                                        catch (OperationCanceledException)
                                        {
                                            Value.Add("Error_Cancelled", "Operation is cancelled");
                                        }
                                    }
                                }
                                else
                                {
                                    Value.Add("Error_NotFound", "Path is not found");
                                }

                                break;
                            }
                        case AuxiliaryTrustProcessCommandType.GetSpecificInstalledUwpApplication:
                            {
                                string PackageFamilyName = CommandValue["PackageFamilyName"];

                                if (!string.IsNullOrEmpty(PackageFamilyName))
                                {
                                    if (Helper.GetSpecificInstalledUwpApplication(PackageFamilyName) is InstalledApplicationPackage Package)
                                    {
                                        Value.Add("Success", JsonSerializer.Serialize(Package, JsonSourceGenerationContext.Default.InstalledApplicationPackage));
                                    }
                                    else
                                    {
                                        Value.Add("Error", "Could not found the package with family name");
                                    }
                                }
                                else
                                {
                                    Value.Add("Error", "Could not found the package with family name");
                                }

                                break;
                            }
                        case AuxiliaryTrustProcessCommandType.GetAllInstalledUwpApplication:
                            {
                                Value.Add("Success", JsonSerializer.Serialize(Helper.GetAllInstalledUwpApplication(), JsonSourceGenerationContext.Default.IEnumerableInstalledApplicationPackage));

                                break;
                            }
                        case AuxiliaryTrustProcessCommandType.CheckPackageFamilyNameExist:
                            {
                                string PackageFamilyName = CommandValue["PackageFamilyName"];

                                if (!string.IsNullOrEmpty(PackageFamilyName))
                                {
                                    Value.Add("Success", Convert.ToString(Helper.CheckIfPackageFamilyNameExist(PackageFamilyName)));
                                }
                                else
                                {
                                    Value.Add("Error", "Could not found the package with family name");
                                }

                                break;
                            }
                        case AuxiliaryTrustProcessCommandType.UpdateUrl:
                            {
                                UrlFileData Package = JsonSerializer.Deserialize(CommandValue["DataPackage"], JsonSourceGenerationContext.Default.UrlFileData);

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
                        case AuxiliaryTrustProcessCommandType.UpdateLink:
                            {
                                LinkFileData Package = JsonSerializer.Deserialize<LinkFileData>(CommandValue["DataPackage"], JsonSourceGenerationContext.Default.LinkFileData);

                                if (File.Exists(Package.LinkPath))
                                {
                                    if (Path.IsPathRooted(Package.LinkTargetPath))
                                    {
                                        string Arguments = null;

                                        if ((Package.Arguments?.Any()).GetValueOrDefault())
                                        {
                                            Arguments = string.Join(" ", Package.Arguments.Select((Para) => Para.Contains(' ') ? $"\"{Para.Trim('\"')}\"" : Para));
                                        }

                                        using (ShellLink Link = new ShellLink(Package.LinkPath))
                                        {
                                            Link.TargetPath = Package.LinkTargetPath;
                                            Link.WorkingDirectory = Package.WorkDirectory;
                                            Link.ShowState = Package.WindowState switch
                                            {
                                                WindowState.Normal => ShowWindowCommand.SW_SHOWNORMAL,
                                                WindowState.Minimized => ShowWindowCommand.SW_SHOWMINIMIZED,
                                                WindowState.Maximized => ShowWindowCommand.SW_SHOWMAXIMIZED,
                                                _ => throw new NotSupportedException()
                                            };
                                            Link.RunAsAdministrator = Package.NeedRunAsAdmin;
                                            Link.Description = Package.Comment;
                                            Link.Arguments = Arguments;

                                            if (Package.HotKey > 0)
                                            {
                                                Link.HotKey = ((Package.HotKey >= 112 && Package.HotKey <= 135) || (Package.HotKey >= 96 && Package.HotKey <= 105)) ? Package.HotKey : Macros.MAKEWORD(Package.HotKey, (byte)(User32.HOTKEYF.HOTKEYF_CONTROL | User32.HOTKEYF.HOTKEYF_ALT));
                                            }
                                            else
                                            {
                                                Link.HotKey = 0;
                                            }
                                        }
                                    }
                                    else if (Helper.CheckIfPackageFamilyNameExist(Package.LinkTargetPath))
                                    {
                                        using (ShellLink Link = new ShellLink(Package.LinkPath))
                                        {
                                            Link.ShowState = Package.WindowState switch
                                            {
                                                WindowState.Normal => ShowWindowCommand.SW_SHOWNORMAL,
                                                WindowState.Minimized => ShowWindowCommand.SW_SHOWMINIMIZED,
                                                WindowState.Maximized => ShowWindowCommand.SW_SHOWMAXIMIZED,
                                                _ => throw new NotSupportedException()
                                            };
                                            Link.Description = Package.Comment;

                                            if (Package.HotKey > 0)
                                            {
                                                Link.HotKey = ((Package.HotKey >= 112 && Package.HotKey <= 135) || (Package.HotKey >= 96 && Package.HotKey <= 105)) ? Package.HotKey : Macros.MAKEWORD(Package.HotKey, (byte)(User32.HOTKEYF.HOTKEYF_CONTROL | User32.HOTKEYF.HOTKEYF_ALT));
                                            }
                                            else
                                            {
                                                Link.HotKey = 0;
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
                        case AuxiliaryTrustProcessCommandType.SetFileAttribute:
                            {
                                string ExecutePath = CommandValue["ExecutePath"];

                                IDictionary<ModifyAttributeAction, FileAttributes> AttributeMapping = JsonSerializer.Deserialize(CommandValue["Attributes"], JsonSourceGenerationContext.Default.IDictionaryModifyAttributeActionFileAttributes);

                                if (File.Exists(ExecutePath))
                                {
                                    FileInfo File = new FileInfo(ExecutePath);

                                    foreach (KeyValuePair<ModifyAttributeAction, FileAttributes> AttributePair in AttributeMapping)
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

                                    foreach (KeyValuePair<ModifyAttributeAction, FileAttributes> AttributePair in AttributeMapping)
                                    {
                                        if (AttributePair.Key == ModifyAttributeAction.Add)
                                        {
                                            if (AttributePair.Value == FileAttributes.ReadOnly)
                                            {
                                                foreach (FileInfo SubFile in Dir.EnumerateFiles("*", SearchOption.AllDirectories))
                                                {
                                                    SubFile.Attributes |= AttributePair.Value;
                                                }
                                            }
                                            else
                                            {
                                                Dir.Attributes |= AttributePair.Value;
                                            }
                                        }
                                        else
                                        {
                                            if (AttributePair.Value == FileAttributes.ReadOnly)
                                            {
                                                foreach (FileInfo SubFile in Dir.EnumerateFiles("*", SearchOption.AllDirectories))
                                                {
                                                    SubFile.Attributes &= ~AttributePair.Value;
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
                        case AuxiliaryTrustProcessCommandType.GetUrlData:
                            {
                                string ExecutePath = CommandValue["ExecutePath"];

                                if (File.Exists(ExecutePath))
                                {
                                    byte[] IconData = Array.Empty<byte>();

                                    try
                                    {
                                        string DefaultProgramPath = ExtensionAssociation.GetDefaultProgramPathFromExtension(".html");

                                        using (ShellItem DefaultProgramItem = new ShellItem(DefaultProgramPath))
                                        using (Gdi32.SafeHBITMAP HBitmap = DefaultProgramItem.GetImage(new SIZE(150, 150), ShellItemGetImageOptions.BiggerSizeOk | ShellItemGetImageOptions.ResizeToFit | ShellItemGetImageOptions.IconOnly))
                                        using (MemoryStream IconStream = new MemoryStream())
                                        using (Bitmap OriginBitmap = Image.FromHbitmap(HBitmap.DangerousGetHandle()))
                                        {
                                            using (Bitmap ConvertedBitmap = OriginBitmap.ConvertToBitmapWithAlphaChannel())
                                            {
                                                ConvertedBitmap?.Save(IconStream, ImageFormat.Png);
                                            }

                                            IconData = IconStream.ToArray();
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        LogTracer.Log(ex, $"Could not get the icon of \"{ExecutePath}\"");
                                    }

                                    using (ShellItem Item = new ShellItem(ExecutePath))
                                    {
                                        Value.Add("Success", JsonSerializer.Serialize(new UrlFileData(ExecutePath, Item.Properties.GetPropertyString(Ole32.PROPERTYKEY.System.Link.TargetUrl), IconData), JsonSourceGenerationContext.Default.UrlFileData));
                                    }
                                }
                                else
                                {
                                    Value.Add("Error", "File not found");
                                }

                                break;
                            }
                        case AuxiliaryTrustProcessCommandType.GetLinkData:
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

                                            LinkFileData Package = new LinkFileData
                                            {
                                                LinkPath = ExecutePath,
                                                LinkTargetPath = ActualPath
                                            };

                                            try
                                            {
                                                using (ShellItem Item = new ShellItem(ActualPath))
                                                using (Gdi32.SafeHBITMAP HBitmap = Item.GetImage(new SIZE(150, 150), ShellItemGetImageOptions.BiggerSizeOk | ShellItemGetImageOptions.ResizeToFit))
                                                using (MemoryStream IconStream = new MemoryStream())
                                                using (Bitmap OriginBitmap = Image.FromHbitmap(HBitmap.DangerousGetHandle()))
                                                {
                                                    using (Bitmap ConvertedBitmap = OriginBitmap.ConvertToBitmapWithAlphaChannel())
                                                    {
                                                        ConvertedBitmap?.Save(IconStream, ImageFormat.Png);
                                                    }

                                                    Package.IconData = IconStream.ToArray();
                                                }
                                            }
                                            catch (Exception ex)
                                            {
                                                LogTracer.Log(ex, "Could not get the icon");
                                                Package.IconData = Array.Empty<byte>();
                                            }

                                            Value.Add("Success", JsonSerializer.Serialize(Package, JsonSourceGenerationContext.Default.LinkFileData));
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
                                            LinkFileData Package = new LinkFileData
                                            {
                                                LinkPath = ExecutePath,
                                                WorkDirectory = Link.WorkingDirectory,
                                                WindowState = Link.ShowState switch
                                                {
                                                    ShowWindowCommand.SW_MINIMIZE or ShowWindowCommand.SW_SHOWMINIMIZED => WindowState.Minimized,
                                                    ShowWindowCommand.SW_MAXIMIZE or ShowWindowCommand.SW_SHOWMAXIMIZED => WindowState.Maximized,
                                                    _ => WindowState.Normal
                                                },
                                                HotKey = Macros.LOBYTE(Link.HotKey),
                                                NeedRunAsAdmin = Link.RunAsAdministrator,
                                                Comment = Link.Description,
                                                Arguments = Regex.Matches(Link.Arguments, "[^ \"]+|\"[^\"]*\"").Cast<Match>().Select((Mat) => Mat.Value)
                                            };

                                            if (string.IsNullOrEmpty(Link.TargetPath))
                                            {
                                                string PackageFamilyName = Helper.GetPackageFamilyNameFromShellLink(ExecutePath);

                                                if (string.IsNullOrEmpty(PackageFamilyName))
                                                {
                                                    throw new Exception($"Link file \"{ExecutePath}\" without TargetPath but also not reference to a uwp application");
                                                }
                                                else
                                                {
                                                    Package.LinkTargetPath = PackageFamilyName;

                                                    if (Helper.GetSpecificInstalledUwpApplication(PackageFamilyName) is InstalledApplicationPackage Data)
                                                    {
                                                        Package.IconData = Data.Logo;
                                                    }
                                                    else
                                                    {
                                                        Package.IconData = Array.Empty<byte>();
                                                    }
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
                                                    using (Gdi32.SafeHBITMAP HBitmap = Link.GetImage(new SIZE(150, 150), ShellItemGetImageOptions.BiggerSizeOk | ShellItemGetImageOptions.ResizeToFit))
                                                    using (MemoryStream IconStream = new MemoryStream())
                                                    using (Bitmap OriginBitmap = Image.FromHbitmap(HBitmap.DangerousGetHandle()))
                                                    {
                                                        using (Bitmap ConvertedBitmap = OriginBitmap.ConvertToBitmapWithAlphaChannel())
                                                        {
                                                            ConvertedBitmap?.Save(IconStream, ImageFormat.Png);
                                                        }

                                                        Package.IconData = IconStream.ToArray();
                                                    }
                                                }
                                                catch (Exception)
                                                {
                                                    Package.IconData = Array.Empty<byte>();
                                                }
                                            }

                                            Value.Add("Success", JsonSerializer.Serialize(Package, JsonSourceGenerationContext.Default.LinkFileData));
                                        }
                                    }
                                }
                                else
                                {
                                    Value.Add("Error", "File is not exist");
                                }

                                break;
                            }
                        case AuxiliaryTrustProcessCommandType.InterceptFolder:
                            {
                                string SystemLaunchHelperTargetBaseFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RX-Explorer_Launch_Helper");
                                string SystemLaunchHelperOriginBaseFolder = Path.Combine(Helper.GetInstalledPathFromPackageFullName(Helper.GetPackageFullNameFromPackageFamilyName(ExplorerPackageFamilyName)), "SystemLaunchHelper");
                                string VersionLockPath = Path.Combine(SystemLaunchHelperTargetBaseFolder, "Version.lock");
                                string CurrentVersion = Helper.GetInstalledUwpApplicationVersion(Helper.GetPackageFullNameFromPackageFamilyName(ExplorerPackageFamilyName));

                                bool NeedsUpdateSystemLaunchHelper = false;

                                if (File.Exists(VersionLockPath))
                                {
                                    using (StreamReader Reader = File.OpenText(VersionLockPath))
                                    {
                                        if (Reader.ReadLine() != CurrentVersion)
                                        {
                                            NeedsUpdateSystemLaunchHelper = true;
                                        }
                                    }
                                }
                                else
                                {
                                    NeedsUpdateSystemLaunchHelper = true;
                                }

                                if (NeedsUpdateSystemLaunchHelper)
                                {
                                    Helper.CopyFileOrFolderTo(SystemLaunchHelperOriginBaseFolder, SystemLaunchHelperTargetBaseFolder);

                                    if (!string.IsNullOrEmpty(CurrentVersion))
                                    {
                                        using (StreamWriter Writer = File.CreateText(VersionLockPath))
                                        {
                                            Writer.WriteLine(CurrentVersion);
                                            Writer.Flush();
                                        }
                                    }
                                }

                                using (Process HelperProcess = Process.Start(new ProcessStartInfo
                                {
                                    FileName = Path.Combine(SystemLaunchHelperTargetBaseFolder, "SystemLaunchHelper.exe"),
                                    UseShellExecute = false,
                                    Arguments = "--Command InterceptFolder",
                                }))
                                {
                                    HelperProcess.WaitForExit();

                                    switch ((SystemLaunchHelperExitCodeEnum)HelperProcess.ExitCode)
                                    {
                                        case SystemLaunchHelperExitCodeEnum.Success:
                                            {
                                                Value.Add("Success", string.Empty);
                                                break;
                                            }
                                        case SystemLaunchHelperExitCodeEnum.FailedOnRegistryCheck:
                                            {
                                                Value.Add("Error", "Registry checking failed in SystemLaunchHelper");
                                                break;
                                            }
                                        case SystemLaunchHelperExitCodeEnum.FailedOnParseArguments:
                                            {
                                                Value.Add("Error", "Could not parse the launch arguements");
                                                break;
                                            }
                                        case SystemLaunchHelperExitCodeEnum.FailedOnLaunchExplorer:
                                            {
                                                Value.Add("Error", "Could not launch the explorer process");
                                                break;
                                            }
                                        default:
                                            {
                                                Value.Add("Error", "Unknown exception was threw in SystemLaunchHelper");
                                                break;
                                            }
                                    }
                                }

                                break;
                            }
                        case AuxiliaryTrustProcessCommandType.InterceptWinE:
                            {
                                string SystemLaunchHelperTargetBaseFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RX-Explorer_Launch_Helper");
                                string SystemLaunchHelperOriginBaseFolder = Path.Combine(Helper.GetInstalledPathFromPackageFullName(Helper.GetPackageFullNameFromPackageFamilyName(ExplorerPackageFamilyName)), "SystemLaunchHelper");
                                string VersionLockPath = Path.Combine(SystemLaunchHelperTargetBaseFolder, "Version.lock");
                                string CurrentVersion = Helper.GetInstalledUwpApplicationVersion(Helper.GetPackageFullNameFromPackageFamilyName(ExplorerPackageFamilyName));

                                bool NeedsUpdateSystemLaunchHelper = false;

                                if (File.Exists(VersionLockPath))
                                {
                                    using (StreamReader Reader = File.OpenText(VersionLockPath))
                                    {
                                        if (Reader.ReadLine() != CurrentVersion)
                                        {
                                            NeedsUpdateSystemLaunchHelper = true;
                                        }
                                    }
                                }
                                else
                                {
                                    NeedsUpdateSystemLaunchHelper = true;
                                }

                                if (NeedsUpdateSystemLaunchHelper)
                                {
                                    Helper.CopyFileOrFolderTo(SystemLaunchHelperOriginBaseFolder, SystemLaunchHelperTargetBaseFolder);

                                    if (!string.IsNullOrEmpty(CurrentVersion))
                                    {
                                        using (StreamWriter Writer = File.CreateText(VersionLockPath))
                                        {
                                            Writer.WriteLine(CurrentVersion);
                                            Writer.Flush();
                                        }
                                    }
                                }

                                using (Process HelperProcess = Process.Start(new ProcessStartInfo
                                {
                                    FileName = Path.Combine(SystemLaunchHelperTargetBaseFolder, "SystemLaunchHelper.exe"),
                                    UseShellExecute = false,
                                    Arguments = "--Command InterceptWinE",
                                }))
                                {
                                    HelperProcess.WaitForExit();

                                    switch ((SystemLaunchHelperExitCodeEnum)HelperProcess.ExitCode)
                                    {
                                        case SystemLaunchHelperExitCodeEnum.Success:
                                            {
                                                Value.Add("Success", string.Empty);
                                                break;
                                            }
                                        case SystemLaunchHelperExitCodeEnum.FailedOnRegistryCheck:
                                            {
                                                Value.Add("Error", "Registry checking failed in SystemLaunchHelper");
                                                break;
                                            }
                                        case SystemLaunchHelperExitCodeEnum.FailedOnParseArguments:
                                            {
                                                Value.Add("Error", "Could not parse the launch arguements");
                                                break;
                                            }
                                        case SystemLaunchHelperExitCodeEnum.FailedOnLaunchExplorer:
                                            {
                                                Value.Add("Error", "Could not launch the explorer process");
                                                break;
                                            }
                                        default:
                                            {
                                                Value.Add("Error", "Unknown exception was threw in SystemLaunchHelper");
                                                break;
                                            }
                                    }
                                }

                                break;
                            }
                        case AuxiliaryTrustProcessCommandType.RestoreFolderInterception:
                            {
                                string SystemLaunchHelperTargetBaseFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RX-Explorer_Launch_Helper");
                                string SystemLaunchHelperTargetExecuteable = Path.Combine(SystemLaunchHelperTargetBaseFolder, "SystemLaunchHelper.exe");

                                if (File.Exists(SystemLaunchHelperTargetExecuteable))
                                {
                                    using (Process HelperProcess = Process.Start(new ProcessStartInfo
                                    {
                                        UseShellExecute = false,
                                        Arguments = "--Command RestoreFolder",
                                        FileName = SystemLaunchHelperTargetExecuteable
                                    }))
                                    {
                                        HelperProcess.WaitForExit();

                                        switch ((SystemLaunchHelperExitCodeEnum)HelperProcess.ExitCode)
                                        {
                                            case SystemLaunchHelperExitCodeEnum.Success:
                                                {
                                                    Value.Add("Success", string.Empty);
                                                    break;
                                                }
                                            case SystemLaunchHelperExitCodeEnum.FailedOnRegistryCheck:
                                                {
                                                    Value.Add("Error", "Registry checking failed in SystemLaunchHelper");
                                                    break;
                                                }
                                            case SystemLaunchHelperExitCodeEnum.FailedOnParseArguments:
                                                {
                                                    Value.Add("Error", "Could not parse the launch arguements");
                                                    break;
                                                }
                                            case SystemLaunchHelperExitCodeEnum.FailedOnLaunchExplorer:
                                                {
                                                    Value.Add("Error", "Could not launch the explorer process");
                                                    break;
                                                }
                                            default:
                                                {
                                                    Value.Add("Error", "Unknown exception was threw in SystemLaunchHelper");
                                                    break;
                                                }
                                        }
                                    }
                                }
                                else
                                {
                                    if (Directory.Exists(SystemLaunchHelperTargetBaseFolder))
                                    {
                                        Directory.Delete(SystemLaunchHelperTargetBaseFolder, true);
                                    }

                                    string SystemLaunchHelperOriginExecuteable = Path.Combine(Helper.GetInstalledPathFromPackageFullName(Helper.GetPackageFullNameFromPackageFamilyName(ExplorerPackageFamilyName)), "SystemLaunchHelper", "SystemLaunchHelper.exe");

                                    if (File.Exists(SystemLaunchHelperOriginExecuteable))
                                    {
                                        using (Process HelperProcess = Process.Start(new ProcessStartInfo
                                        {
                                            UseShellExecute = false,
                                            Arguments = "--SuppressSelfDeletion --Command RestoreFolder",
                                            FileName = SystemLaunchHelperOriginExecuteable
                                        }))
                                        {
                                            HelperProcess.WaitForExit();

                                            switch ((SystemLaunchHelperExitCodeEnum)HelperProcess.ExitCode)
                                            {
                                                case SystemLaunchHelperExitCodeEnum.Success:
                                                    {
                                                        Value.Add("Success", string.Empty);
                                                        break;
                                                    }
                                                case SystemLaunchHelperExitCodeEnum.FailedOnRegistryCheck:
                                                    {
                                                        Value.Add("Error", "Registry checking failed in SystemLaunchHelper");
                                                        break;
                                                    }
                                                case SystemLaunchHelperExitCodeEnum.FailedOnParseArguments:
                                                    {
                                                        Value.Add("Error", "Could not parse the launch arguements");
                                                        break;
                                                    }
                                                case SystemLaunchHelperExitCodeEnum.FailedOnLaunchExplorer:
                                                    {
                                                        Value.Add("Error", "Could not launch the explorer process");
                                                        break;
                                                    }
                                                default:
                                                    {
                                                        Value.Add("Error", "Unknown exception was threw in SystemLaunchHelper");
                                                        break;
                                                    }
                                            }
                                        }
                                    }
                                }

                                break;
                            }
                        case AuxiliaryTrustProcessCommandType.RestoreWinEInterception:
                            {
                                string SystemLaunchHelperTargetBaseFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RX-Explorer_Launch_Helper");
                                string SystemLaunchHelperTargetExecuteable = Path.Combine(SystemLaunchHelperTargetBaseFolder, "SystemLaunchHelper.exe");

                                if (File.Exists(SystemLaunchHelperTargetExecuteable))
                                {
                                    using (Process HelperProcess = Process.Start(new ProcessStartInfo
                                    {
                                        UseShellExecute = false,
                                        Arguments = "--Command RestoreWinE",
                                        FileName = SystemLaunchHelperTargetExecuteable
                                    }))
                                    {
                                        HelperProcess.WaitForExit();

                                        switch ((SystemLaunchHelperExitCodeEnum)HelperProcess.ExitCode)
                                        {
                                            case SystemLaunchHelperExitCodeEnum.Success:
                                                {
                                                    Value.Add("Success", string.Empty);
                                                    break;
                                                }
                                            case SystemLaunchHelperExitCodeEnum.FailedOnRegistryCheck:
                                                {
                                                    Value.Add("Error", "Registry checking failed in SystemLaunchHelper");
                                                    break;
                                                }
                                            case SystemLaunchHelperExitCodeEnum.FailedOnParseArguments:
                                                {
                                                    Value.Add("Error", "Could not parse the launch arguements");
                                                    break;
                                                }
                                            case SystemLaunchHelperExitCodeEnum.FailedOnLaunchExplorer:
                                                {
                                                    Value.Add("Error", "Could not launch the explorer process");
                                                    break;
                                                }
                                            default:
                                                {
                                                    Value.Add("Error", "Unknown exception was threw in SystemLaunchHelper");
                                                    break;
                                                }
                                        }
                                    }
                                }
                                else
                                {
                                    if (Directory.Exists(SystemLaunchHelperTargetBaseFolder))
                                    {
                                        Directory.Delete(SystemLaunchHelperTargetBaseFolder, true);
                                    }

                                    string SystemLaunchHelperOriginExecuteable = Path.Combine(Helper.GetInstalledPathFromPackageFullName(Helper.GetPackageFullNameFromPackageFamilyName(ExplorerPackageFamilyName)), "SystemLaunchHelper", "SystemLaunchHelper.exe");

                                    if (File.Exists(SystemLaunchHelperOriginExecuteable))
                                    {
                                        using (Process HelperProcess = Process.Start(new ProcessStartInfo
                                        {
                                            UseShellExecute = false,
                                            Arguments = "--SuppressSelfDeletion --Command RestoreWinE",
                                            FileName = SystemLaunchHelperOriginExecuteable
                                        }))
                                        {
                                            HelperProcess.WaitForExit();

                                            switch ((SystemLaunchHelperExitCodeEnum)HelperProcess.ExitCode)
                                            {
                                                case SystemLaunchHelperExitCodeEnum.Success:
                                                    {
                                                        Value.Add("Success", string.Empty);
                                                        break;
                                                    }
                                                case SystemLaunchHelperExitCodeEnum.FailedOnRegistryCheck:
                                                    {
                                                        Value.Add("Error", "Registry checking failed in SystemLaunchHelper");
                                                        break;
                                                    }
                                                case SystemLaunchHelperExitCodeEnum.FailedOnParseArguments:
                                                    {
                                                        Value.Add("Error", "Could not parse the launch arguements");
                                                        break;
                                                    }
                                                case SystemLaunchHelperExitCodeEnum.FailedOnLaunchExplorer:
                                                    {
                                                        Value.Add("Error", "Could not launch the explorer process");
                                                        break;
                                                    }
                                                default:
                                                    {
                                                        Value.Add("Error", "Unknown exception was threw in SystemLaunchHelper");
                                                        break;
                                                    }
                                            }
                                        }
                                    }
                                }

                                break;
                            }
                        case AuxiliaryTrustProcessCommandType.ToggleQuicklookWindow:
                            {
                                string ExecutePath = CommandValue["ExecutePath"];

                                if (string.IsNullOrEmpty(ExecutePath))
                                {
                                    Value.Add("Error", "Path could not be empty");
                                }
                                else if (QuicklookServiceProvider.Current.ToggleServiceWindow(ExecutePath))
                                {
                                    Value.Add("Success", string.Empty);
                                }
                                else
                                {
                                    Value.Add("Error", "Could not send the command to quick look service");
                                }

                                break;
                            }
                        case AuxiliaryTrustProcessCommandType.SwitchQuicklookWindow:
                            {
                                string ExecutePath = CommandValue["ExecutePath"];

                                if (string.IsNullOrEmpty(ExecutePath))
                                {
                                    Value.Add("Error", "Path could not be empty");
                                }
                                else if (QuicklookServiceProvider.Current.SwitchServiceWindow(ExecutePath))
                                {
                                    Value.Add("Success", string.Empty);
                                }
                                else
                                {
                                    Value.Add("Error", "Could not send the command to quick look service");
                                }

                                break;
                            }
                        case AuxiliaryTrustProcessCommandType.CheckQuicklookAvailable:
                            {
                                Value.Add("Success", Convert.ToString(QuicklookServiceProvider.Current.CheckServiceAvailable()));

                                break;
                            }
                        case AuxiliaryTrustProcessCommandType.CheckQuicklookWindowVisible:
                            {
                                Value.Add("Success", Convert.ToString(QuicklookServiceProvider.Current.CheckWindowVisible()));

                                break;
                            }
                        case AuxiliaryTrustProcessCommandType.CloseQuicklookWindow:
                            {
                                if (QuicklookServiceProvider.Current.CloseServiceWindow())
                                {
                                    Value.Add("Success", string.Empty);
                                }
                                else
                                {
                                    Value.Add("Error", "Could not send the command to quick look service");
                                }

                                break;
                            }
                        case AuxiliaryTrustProcessCommandType.ToggleSeerWindow:
                            {
                                string ExecutePath = CommandValue["ExecutePath"];

                                if (string.IsNullOrEmpty(ExecutePath))
                                {
                                    Value.Add("Error", "Path could not be empty");
                                }
                                else if (SeerServiceProvider.Current.ToggleServiceWindow(ExecutePath))
                                {
                                    Value.Add("Success", string.Empty);
                                }
                                else
                                {
                                    Value.Add("Error", "Could not send the command to seer service");
                                }

                                break;
                            }
                        case AuxiliaryTrustProcessCommandType.SwitchSeerWindow:
                            {
                                string ExecutePath = CommandValue["ExecutePath"];

                                if (string.IsNullOrEmpty(ExecutePath))
                                {
                                    Value.Add("Error", "Path could not be empty");
                                }
                                else if (SeerServiceProvider.Current.SwitchServiceWindow(ExecutePath))
                                {
                                    Value.Add("Success", string.Empty);
                                }
                                else
                                {
                                    Value.Add("Error", "Could not send the command to seer service");
                                }

                                break;
                            }
                        case AuxiliaryTrustProcessCommandType.CheckSeerAvailable:
                            {
                                Value.Add("Success", Convert.ToString(SeerServiceProvider.Current.CheckServiceAvailable()));

                                break;
                            }
                        case AuxiliaryTrustProcessCommandType.CheckSeerWindowVisible:
                            {
                                Value.Add("Success", Convert.ToString(SeerServiceProvider.Current.CheckWindowVisible()));

                                break;
                            }
                        case AuxiliaryTrustProcessCommandType.CloseSeerWindow:
                            {
                                if (SeerServiceProvider.Current.CloseServiceWindow())
                                {
                                    Value.Add("Success", string.Empty);
                                }
                                else
                                {
                                    Value.Add("Error", "Could not send the command to seer service");
                                }

                                break;
                            }
                        case AuxiliaryTrustProcessCommandType.GetAssociation:
                            {
                                string Extension = CommandValue["Extension"];

                                if (!string.IsNullOrEmpty(Extension))
                                {
                                    Value.Add("Success", JsonSerializer.Serialize(ExtensionAssociation.GetAssociationFromExtension(Extension), JsonSourceGenerationContext.Default.IEnumerableAssociationPackage));
                                }
                                else
                                {
                                    Value.Add("Error", "Argument could not be empty");
                                }

                                break;
                            }
                        case AuxiliaryTrustProcessCommandType.Default_Association:
                            {
                                string Path = CommandValue["ExecutePath"];

                                Value.Add("Success", ExtensionAssociation.GetDefaultProgramPathRelated(Path));

                                break;
                            }
                        case AuxiliaryTrustProcessCommandType.GetRecycleBinItems:
                            {
                                Value.Add("RecycleBinItems_Json_Result", JsonSerializer.Serialize(RecycleBinController.GetRecycleItems(), JsonSourceGenerationContext.Default.IEnumerableRecycleBinItemDataPackage));

                                break;
                            }
                        case AuxiliaryTrustProcessCommandType.EmptyRecycleBin:
                            {
                                Value.Add("RecycleBinItems_Clear_Result", Convert.ToString(RecycleBinController.EmptyRecycleBin()));

                                break;
                            }
                        case AuxiliaryTrustProcessCommandType.RestoreRecycleItem:
                            {
                                Value.Add("Restore_Result", Convert.ToString(RecycleBinController.Restore(JsonSerializer.Deserialize(CommandValue["ExecutePath"], JsonSourceGenerationContext.Default.IEnumerableString))));

                                break;
                            }
                        case AuxiliaryTrustProcessCommandType.DeleteRecycleItem:
                            {
                                string Path = CommandValue["ExecutePath"];

                                Value.Add("Delete_Result", Convert.ToString(RecycleBinController.Delete(Path)));

                                break;
                            }
                        case AuxiliaryTrustProcessCommandType.EjectUSB:
                            {
                                string Path = CommandValue["ExecutePath"];

                                if (string.IsNullOrEmpty(Path))
                                {
                                    Value.Add("EjectResult", Convert.ToString(false));
                                }
                                else
                                {
                                    try
                                    {
                                        using (VolumeDeviceClass Volumes = new VolumeDeviceClass())
                                        {
                                            if (Volumes.Where((Vol) => Vol.IsUsb).FirstOrDefault((Vol) => Vol.LogicalDrive.Equals(Path.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase)) is Device UsbDevice)
                                            {
                                                UsbDevice.Eject(false);
                                                Value.Add("EjectResult", Convert.ToString(true));
                                            }
                                            else
                                            {
                                                throw new FileNotFoundException();
                                            }
                                        }
                                    }
                                    catch (Exception)
                                    {
                                        Value.Add("EjectResult", Convert.ToString(false));
                                    }
                                }

                                break;
                            }
                        case AuxiliaryTrustProcessCommandType.UnlockOccupy:
                            {
                                string Path = CommandValue["ExecutePath"];

                                if (File.Exists(Path))
                                {
                                    if (StorageItemController.CheckCaptured(Path))
                                    {
                                        bool ForceClose = Convert.ToBoolean(CommandValue["ForceClose"]);

                                        IReadOnlyList<Process> LockingProcesses = Helper.GetLockingProcessesList(Path);

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
                                            Value.Add("Error_Failure", $"Unlock failed because {ex.Message}");
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
                        case AuxiliaryTrustProcessCommandType.Copy:
                            {
                                string SourcePathJson = CommandValue["SourcePath"];
                                string DestinationPath = CommandValue["DestinationPath"];

                                CollisionOptions Option = Enum.Parse<CollisionOptions>(CommandValue["CollisionOptions"]);
                                IReadOnlyDictionary<string, string> SourcePathMapping = new Dictionary<string, string>(JsonSerializer.Deserialize(SourcePathJson, JsonSourceGenerationContext.Default.IDictionaryStringString));

                                try
                                {
                                    if (SourcePathMapping.Keys.All((Source) => Source.StartsWith(@"\\?\")) && DestinationPath.StartsWith(@"\\?\"))
                                    {
                                        MTPPathAnalysis SourcePathAnalysis = new MTPPathAnalysis(SourcePathMapping.Keys.First());
                                        MTPPathAnalysis DestinationPathAnalysis = new MTPPathAnalysis(DestinationPath);

                                        if (MTPManager.GetDeviceFromId(SourcePathAnalysis.DeviceId) is MediaDevice SourceDevice
                                            && MTPManager.GetDeviceFromId(DestinationPathAnalysis.DeviceId) is MediaDevice DestinationDevice)
                                        {
                                            IReadOnlyDictionary<string, string> SourceRelativePathMapping = new Dictionary<string, string>(SourcePathMapping.Select((Pair) => new KeyValuePair<string, string>(new MTPPathAnalysis(Pair.Key).RelativePath, Pair.Value)));

                                            if (SourceRelativePathMapping.Keys.All((SourceRelativePath) => SourceDevice.FileExists(SourceRelativePath) || SourceDevice.DirectoryExists(SourceRelativePath)))
                                            {
                                                double CurrentPosition = 0;
                                                double EachTaskStep = 100d / SourceRelativePathMapping.Count;

                                                foreach (KeyValuePair<string, string> SourceRelativePair in SourceRelativePathMapping)
                                                {
                                                    Cancellation.Token.ThrowIfCancellationRequested();

                                                    if (SourceDevice.FileExists(SourceRelativePair.Key))
                                                    {
                                                        string TargetPath = Path.Combine(DestinationPathAnalysis.RelativePath, string.IsNullOrEmpty(SourceRelativePair.Value) ? Path.GetFileName(SourceRelativePair.Key) : SourceRelativePair.Value);

                                                        switch (Option)
                                                        {
                                                            case CollisionOptions.None:
                                                                {
                                                                    if (DestinationDevice.FileExists(TargetPath))
                                                                    {
                                                                        throw new Exception($"{TargetPath} is already exists");
                                                                    }

                                                                    break;
                                                                }
                                                            case CollisionOptions.RenameOnCollision:
                                                                {
                                                                    if (DestinationDevice.FileExists(TargetPath))
                                                                    {
                                                                        TargetPath = Helper.MTPGenerateUniquePath(DestinationDevice, TargetPath, CreateType.File);
                                                                    }

                                                                    break;
                                                                }
                                                            case CollisionOptions.OverrideOnCollision:
                                                                {
                                                                    if (DestinationDevice.FileExists(TargetPath))
                                                                    {
                                                                        DestinationDevice.DeleteFile(TargetPath);
                                                                    }

                                                                    break;
                                                                }
                                                            case CollisionOptions.Skip:
                                                                {
                                                                    if (DestinationDevice.FileExists(TargetPath))
                                                                    {
                                                                        continue;
                                                                    }

                                                                    break;
                                                                }
                                                        }

                                                        using (FileStream TempStream = File.Create(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")), 4096, FileOptions.DeleteOnClose))
                                                        {
                                                            SourceDevice.DownloadFile(SourceRelativePair.Key, TempStream, Cancellation.Token, (s, e) =>
                                                            {
                                                                PipeProgressWriterController?.SendData(Convert.ToString(Math.Ceiling(CurrentPosition + (e.ProgressPercentage / 200 * EachTaskStep))));
                                                            });

                                                            TempStream.Seek(0, SeekOrigin.Begin);

                                                            DestinationDevice.UploadFile(TempStream, TargetPath, Cancellation.Token, (s, e) =>
                                                            {
                                                                PipeProgressWriterController?.SendData(Convert.ToString(Math.Ceiling(CurrentPosition + (EachTaskStep / 2) + (e.ProgressPercentage / 200 * EachTaskStep))));
                                                            });
                                                        }
                                                    }
                                                    else if (SourceDevice.DirectoryExists(SourceRelativePair.Key))
                                                    {
                                                        string TargetPath = Path.Combine(DestinationPathAnalysis.RelativePath, string.IsNullOrEmpty(SourceRelativePair.Value) ? Path.GetFileName(SourceRelativePair.Key) : SourceRelativePair.Value);

                                                        switch (Option)
                                                        {
                                                            case CollisionOptions.None:
                                                                {
                                                                    if (DestinationDevice.DirectoryExists(TargetPath))
                                                                    {
                                                                        throw new Exception($"{TargetPath} is already exists");
                                                                    }

                                                                    break;
                                                                }
                                                            case CollisionOptions.RenameOnCollision:
                                                                {
                                                                    if (DestinationDevice.DirectoryExists(TargetPath))
                                                                    {
                                                                        TargetPath = Helper.MTPGenerateUniquePath(DestinationDevice, TargetPath, CreateType.Folder);
                                                                    }

                                                                    break;
                                                                }
                                                            case CollisionOptions.OverrideOnCollision:
                                                                {
                                                                    if (DestinationDevice.DirectoryExists(TargetPath))
                                                                    {
                                                                        DestinationDevice.DeleteDirectory(TargetPath, true);
                                                                    }

                                                                    break;
                                                                }
                                                            case CollisionOptions.Skip:
                                                                {
                                                                    if (DestinationDevice.DirectoryExists(TargetPath))
                                                                    {
                                                                        continue;
                                                                    }

                                                                    break;
                                                                }
                                                        }

                                                        string TempDirectoryPath = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"))).FullName;

                                                        try
                                                        {
                                                            SourceDevice.DownloadFolder(SourceRelativePair.Key, TempDirectoryPath, Cancellation.Token, (s, e) =>
                                                            {
                                                                PipeProgressWriterController?.SendData(Convert.ToString(Math.Ceiling(CurrentPosition + (e.ProgressPercentage / 200 * EachTaskStep))));
                                                            });

                                                            DestinationDevice.UploadFolder(TempDirectoryPath, TargetPath, Cancellation.Token, (s, e) =>
                                                            {
                                                                PipeProgressWriterController?.SendData(Convert.ToString(Math.Ceiling(CurrentPosition + (EachTaskStep / 2) + (e.ProgressPercentage / 200 * EachTaskStep))));
                                                            });
                                                        }
                                                        finally
                                                        {
                                                            Directory.Delete(TempDirectoryPath, true);
                                                        }
                                                    }

                                                    CurrentPosition += EachTaskStep;
                                                }

                                                Value.Add("Success", JsonSerializer.Serialize(Array.Empty<string>(), JsonSourceGenerationContext.Default.IEnumerableString));
                                            }
                                            else
                                            {
                                                Value.Add("Error_NotFound", $"One of path in \"{nameof(SourcePathMapping)}\" is not a file or directory");
                                            }
                                        }
                                        else
                                        {
                                            Value.Add("Error_NotFound", "MTP device is not found");
                                        }
                                    }
                                    else if (DestinationPath.StartsWith(@"\\?\"))
                                    {
                                        MTPPathAnalysis DestinationPathAnalysis = new MTPPathAnalysis(DestinationPath);

                                        if (MTPManager.GetDeviceFromId(DestinationPathAnalysis.DeviceId) is MediaDevice DestinationDevice)
                                        {
                                            if (SourcePathMapping.Keys.All((Path) => Directory.Exists(Path) || File.Exists(Path)))
                                            {
                                                double CurrentPosition = 0;
                                                double EachTaskStep = 100d / SourcePathMapping.Count;

                                                foreach (KeyValuePair<string, string> SourceRelativePair in SourcePathMapping)
                                                {
                                                    Cancellation.Token.ThrowIfCancellationRequested();

                                                    if (File.Exists(SourceRelativePair.Key))
                                                    {
                                                        string TargetPath = Path.Combine(DestinationPathAnalysis.RelativePath, string.IsNullOrEmpty(SourceRelativePair.Value) ? Path.GetFileName(SourceRelativePair.Key) : SourceRelativePair.Value);

                                                        switch (Option)
                                                        {
                                                            case CollisionOptions.None:
                                                                {
                                                                    if (DestinationDevice.FileExists(TargetPath))
                                                                    {
                                                                        throw new Exception($"{TargetPath} is already exists");
                                                                    }

                                                                    break;
                                                                }
                                                            case CollisionOptions.RenameOnCollision:
                                                                {
                                                                    if (DestinationDevice.FileExists(TargetPath))
                                                                    {
                                                                        TargetPath = Helper.MTPGenerateUniquePath(DestinationDevice, TargetPath, CreateType.File);
                                                                    }

                                                                    break;
                                                                }
                                                            case CollisionOptions.OverrideOnCollision:
                                                                {
                                                                    if (DestinationDevice.FileExists(TargetPath))
                                                                    {
                                                                        DestinationDevice.DeleteFile(TargetPath);
                                                                    }

                                                                    break;
                                                                }
                                                            case CollisionOptions.Skip:
                                                                {
                                                                    if (DestinationDevice.FileExists(TargetPath))
                                                                    {
                                                                        continue;
                                                                    }

                                                                    break;
                                                                }
                                                        }

                                                        DestinationDevice.UploadFile(SourceRelativePair.Key, TargetPath, Cancellation.Token, (s, e) =>
                                                        {
                                                            PipeProgressWriterController?.SendData(Convert.ToString(Math.Ceiling(CurrentPosition + (e.ProgressPercentage / 100 * EachTaskStep))));
                                                        });
                                                    }
                                                    else if (Directory.Exists(SourceRelativePair.Key))
                                                    {
                                                        string TargetPath = Path.Combine(DestinationPathAnalysis.RelativePath, string.IsNullOrEmpty(SourceRelativePair.Value) ? Path.GetFileName(SourceRelativePair.Key) : SourceRelativePair.Value);

                                                        switch (Option)
                                                        {
                                                            case CollisionOptions.None:
                                                                {
                                                                    if (DestinationDevice.DirectoryExists(TargetPath))
                                                                    {
                                                                        throw new Exception($"{TargetPath} is already exists");
                                                                    }

                                                                    break;
                                                                }
                                                            case CollisionOptions.RenameOnCollision:
                                                                {
                                                                    if (DestinationDevice.DirectoryExists(TargetPath))
                                                                    {
                                                                        TargetPath = Helper.MTPGenerateUniquePath(DestinationDevice, TargetPath, CreateType.Folder);
                                                                    }

                                                                    break;
                                                                }
                                                            case CollisionOptions.OverrideOnCollision:
                                                                {
                                                                    if (DestinationDevice.DirectoryExists(TargetPath))
                                                                    {
                                                                        DestinationDevice.DeleteDirectory(TargetPath, true);
                                                                    }

                                                                    break;
                                                                }
                                                            case CollisionOptions.Skip:
                                                                {
                                                                    if (DestinationDevice.DirectoryExists(TargetPath))
                                                                    {
                                                                        continue;
                                                                    }

                                                                    break;
                                                                }
                                                        }

                                                        DestinationDevice.UploadFolder(SourceRelativePair.Key, TargetPath, Cancellation.Token, (s, e) =>
                                                        {
                                                            PipeProgressWriterController?.SendData(Convert.ToString(Math.Ceiling(CurrentPosition + (e.ProgressPercentage / 100 * EachTaskStep))));
                                                        });
                                                    }

                                                    CurrentPosition += EachTaskStep;
                                                }

                                                Value.Add("Success", JsonSerializer.Serialize(Array.Empty<string>(), JsonSourceGenerationContext.Default.IEnumerableString));
                                            }
                                            else
                                            {
                                                Value.Add("Error_NotFound", $"One of path in \"{nameof(SourcePathMapping)}\" is not a file or directory");
                                            }
                                        }
                                        else
                                        {
                                            Value.Add("Error_NotFound", "MTP device is not found");
                                        }
                                    }
                                    else if (SourcePathMapping.Keys.All((Source) => Source.StartsWith(@"\\?\")))
                                    {
                                        MTPPathAnalysis SourcePathAnalysis = new MTPPathAnalysis(SourcePathMapping.Keys.First());

                                        if (MTPManager.GetDeviceFromId(SourcePathAnalysis.DeviceId) is MediaDevice SourceDevice)
                                        {
                                            IReadOnlyDictionary<string, string> SourceRelativePathMapping = new Dictionary<string, string>(SourcePathMapping.Select((Pair) => new KeyValuePair<string, string>(new MTPPathAnalysis(Pair.Key).RelativePath, Pair.Value)));

                                            if (SourceRelativePathMapping.Keys.All((SourceRelativePath) => SourceDevice.FileExists(SourceRelativePath) || SourceDevice.DirectoryExists(SourceRelativePath)))
                                            {
                                                double CurrentPosition = 0;
                                                double EachTaskStep = 100d / SourceRelativePathMapping.Count;

                                                foreach (KeyValuePair<string, string> SourceRelativePair in SourceRelativePathMapping)
                                                {
                                                    Cancellation.Token.ThrowIfCancellationRequested();

                                                    if (SourceDevice.FileExists(SourceRelativePair.Key))
                                                    {
                                                        string TargetPath = Path.Combine(DestinationPath, string.IsNullOrEmpty(SourceRelativePair.Value) ? Path.GetFileName(SourceRelativePair.Key) : SourceRelativePair.Value);

                                                        switch (Option)
                                                        {
                                                            case CollisionOptions.None:
                                                                {
                                                                    if (File.Exists(TargetPath))
                                                                    {
                                                                        throw new Exception($"{TargetPath} is already exists");
                                                                    }

                                                                    break;
                                                                }
                                                            case CollisionOptions.RenameOnCollision:
                                                                {
                                                                    if (File.Exists(TargetPath))
                                                                    {
                                                                        TargetPath = Helper.GenerateUniquePathOnLocal(TargetPath, CreateType.File);
                                                                    }

                                                                    break;
                                                                }
                                                            case CollisionOptions.OverrideOnCollision:
                                                                {
                                                                    if (File.Exists(TargetPath))
                                                                    {
                                                                        File.Delete(TargetPath);
                                                                    }

                                                                    break;
                                                                }
                                                            case CollisionOptions.Skip:
                                                                {
                                                                    if (File.Exists(TargetPath))
                                                                    {
                                                                        continue;
                                                                    }

                                                                    break;
                                                                }
                                                        }

                                                        SourceDevice.DownloadFile(SourceRelativePair.Key, TargetPath, Cancellation.Token, (s, e) =>
                                                        {
                                                            PipeProgressWriterController?.SendData(Convert.ToString(Math.Ceiling(CurrentPosition + (e.ProgressPercentage / 100 * EachTaskStep))));
                                                        });
                                                    }
                                                    else if (SourceDevice.DirectoryExists(SourceRelativePair.Key))
                                                    {
                                                        string TargetPath = Path.Combine(DestinationPath, string.IsNullOrEmpty(SourceRelativePair.Value) ? Path.GetFileName(SourceRelativePair.Key) : SourceRelativePair.Value);

                                                        switch (Option)
                                                        {
                                                            case CollisionOptions.None:
                                                                {
                                                                    if (Directory.Exists(TargetPath))
                                                                    {
                                                                        throw new Exception($"{TargetPath} is already exists");
                                                                    }

                                                                    break;
                                                                }
                                                            case CollisionOptions.RenameOnCollision:
                                                                {
                                                                    if (Directory.Exists(TargetPath))
                                                                    {
                                                                        TargetPath = Helper.GenerateUniquePathOnLocal(TargetPath, CreateType.Folder);
                                                                    }

                                                                    break;
                                                                }
                                                            case CollisionOptions.OverrideOnCollision:
                                                                {
                                                                    if (Directory.Exists(TargetPath))
                                                                    {
                                                                        Directory.Delete(TargetPath, true);
                                                                    }

                                                                    break;
                                                                }
                                                            case CollisionOptions.Skip:
                                                                {
                                                                    if (Directory.Exists(TargetPath))
                                                                    {
                                                                        continue;
                                                                    }

                                                                    break;
                                                                }
                                                        }

                                                        SourceDevice.DownloadFolder(SourceRelativePair.Key, TargetPath, Cancellation.Token, (s, e) =>
                                                        {
                                                            PipeProgressWriterController?.SendData(Convert.ToString(Math.Ceiling(CurrentPosition + (e.ProgressPercentage / 100 * EachTaskStep))));
                                                        });
                                                    }

                                                    CurrentPosition += EachTaskStep;
                                                }

                                                Value.Add("Success", JsonSerializer.Serialize(Array.Empty<string>(), JsonSourceGenerationContext.Default.IEnumerableString));
                                            }
                                            else
                                            {
                                                Value.Add("Error_NotFound", $"One of path in \"{nameof(SourcePathMapping)}\" is not a file or directory");
                                            }
                                        }
                                        else
                                        {
                                            Value.Add("Error_NotFound", "MTP device is not found");
                                        }
                                    }
                                    else if (SourcePathMapping.Keys.All((Path) => Directory.Exists(Path) || File.Exists(Path)))
                                    {
                                        List<string> OperationRecordList = new List<string>();

                                        if (StorageItemController.CheckPermission(DestinationPath, FileSystemRights.Modify))
                                        {
                                            try
                                            {
                                                if (StorageItemController.Copy(SourcePathMapping, DestinationPath, Option, (s, e) =>
                                                {
                                                    if (Cancellation.Token.IsCancellationRequested)
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
                                                        Value.Add("Success", JsonSerializer.Serialize(OperationRecordList, JsonSourceGenerationContext.Default.IEnumerableString));
                                                    }
                                                }
                                                else if (Marshal.GetLastWin32Error() == 5)
                                                {
                                                    IDictionary<string, string> ResultMap = CreateNewProcessAsElevatedAndWaitForResult(new ElevationCopyData(SourcePathMapping, DestinationPath, Option), (s, e) =>
                                                    {
                                                        PipeProgressWriterController?.SendData(Convert.ToString(e.ProgressPercentage));
                                                    }, Cancellation.Token);

                                                    foreach (KeyValuePair<string, string> Result in ResultMap)
                                                    {
                                                        Value.Add(Result);
                                                    }
                                                }
                                                else if (!Value.ContainsKey("Error_UserCancel"))
                                                {
                                                    Value.Add("Error_Failure", new Win32Exception(Marshal.GetLastWin32Error()).Message);
                                                }
                                            }
                                            catch (COMException ex) when (ex.ErrorCode == HRESULT.E_ABORT)
                                            {
                                                throw new OperationCanceledException();
                                            }
                                        }
                                        else
                                        {
                                            IDictionary<string, string> ResultMap = CreateNewProcessAsElevatedAndWaitForResult(new ElevationCopyData(SourcePathMapping, DestinationPath, Option), (s, e) =>
                                            {
                                                PipeProgressWriterController?.SendData(Convert.ToString(e.ProgressPercentage));
                                            }, Cancellation.Token);

                                            foreach (KeyValuePair<string, string> Result in ResultMap)
                                            {
                                                Value.Add(Result);
                                            }
                                        }
                                    }
                                    else
                                    {
                                        Value.Add("Error_NotFound", $"One of path in \"{nameof(SourcePathMapping)}\" is not a file or directory");
                                    }
                                }
                                catch (OperationCanceledException)
                                {
                                    Value.Add("Error_Cancelled", "Operation is cancelled");
                                }

                                break;
                            }
                        case AuxiliaryTrustProcessCommandType.Move:
                            {
                                string SourcePathJson = CommandValue["SourcePath"];
                                string DestinationPath = CommandValue["DestinationPath"];

                                CollisionOptions Option = Enum.Parse<CollisionOptions>(CommandValue["CollisionOptions"]);
                                IReadOnlyDictionary<string, string> SourcePathList = new Dictionary<string, string>(JsonSerializer.Deserialize(SourcePathJson, JsonSourceGenerationContext.Default.IDictionaryStringString));

                                try
                                {
                                    if (SourcePathList.Keys.All((Source) => Source.StartsWith(@"\\?\")) && DestinationPath.StartsWith(@"\\?\"))
                                    {
                                        MTPPathAnalysis SourcePathAnalysis = new MTPPathAnalysis(SourcePathList.Keys.First());
                                        MTPPathAnalysis DestinationPathAnalysis = new MTPPathAnalysis(DestinationPath);

                                        if (MTPManager.GetDeviceFromId(SourcePathAnalysis.DeviceId) is MediaDevice SourceDevice
                                            && MTPManager.GetDeviceFromId(DestinationPathAnalysis.DeviceId) is MediaDevice DestinationDevice)
                                        {
                                            IReadOnlyList<string> SourceRelativePathArray = SourcePathList.Keys.Select((Source) => new MTPPathAnalysis(Source).RelativePath).ToArray();

                                            if (SourceRelativePathArray.All((SourceRelativePath) => SourceDevice.FileExists(SourceRelativePath) || SourceDevice.DirectoryExists(SourceRelativePath)))
                                            {
                                                double CurrentPosition = 0;
                                                double EachTaskStep = 100d / SourceRelativePathArray.Count;

                                                foreach (string SourceRelativePath in SourceRelativePathArray)
                                                {
                                                    Cancellation.Token.ThrowIfCancellationRequested();

                                                    if (SourceDevice.FileExists(SourceRelativePath))
                                                    {
                                                        string TargetPath = Path.Combine(DestinationPathAnalysis.RelativePath, Path.GetFileName(SourceRelativePath));

                                                        switch (Option)
                                                        {
                                                            case CollisionOptions.None:
                                                                {
                                                                    if (DestinationDevice.FileExists(TargetPath))
                                                                    {
                                                                        throw new Exception($"{TargetPath} is already exists");
                                                                    }

                                                                    break;
                                                                }
                                                            case CollisionOptions.RenameOnCollision:
                                                                {
                                                                    TargetPath = Helper.MTPGenerateUniquePath(DestinationDevice, TargetPath, CreateType.File);
                                                                    break;
                                                                }
                                                            case CollisionOptions.OverrideOnCollision:
                                                                {
                                                                    DestinationDevice.DeleteFile(TargetPath);
                                                                    break;
                                                                }
                                                            case CollisionOptions.Skip:
                                                                {
                                                                    if (DestinationDevice.FileExists(TargetPath))
                                                                    {
                                                                        continue;
                                                                    }

                                                                    break;
                                                                }
                                                        }

                                                        using (FileStream TempStream = File.Create(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")), 4096, FileOptions.DeleteOnClose))
                                                        {
                                                            SourceDevice.DownloadFile(SourceRelativePath, TempStream, Cancellation.Token, (s, e) =>
                                                            {
                                                                PipeProgressWriterController?.SendData(Convert.ToString(Math.Ceiling(CurrentPosition + (e.ProgressPercentage / 200 * EachTaskStep))));
                                                            });

                                                            TempStream.Seek(0, SeekOrigin.Begin);

                                                            DestinationDevice.UploadFile(TempStream, TargetPath, Cancellation.Token, (s, e) =>
                                                            {
                                                                PipeProgressWriterController?.SendData(Convert.ToString(Math.Ceiling(CurrentPosition + (EachTaskStep / 2) + (e.ProgressPercentage / 200 * EachTaskStep))));
                                                            });

                                                            SourceDevice.DeleteFile(SourceRelativePath);
                                                        }
                                                    }
                                                    else if (SourceDevice.DirectoryExists(SourceRelativePath))
                                                    {
                                                        string TargetPath = Path.Combine(DestinationPathAnalysis.RelativePath, Path.GetFileName(SourceRelativePath));

                                                        switch (Option)
                                                        {
                                                            case CollisionOptions.None:
                                                                {
                                                                    if (DestinationDevice.DirectoryExists(TargetPath))
                                                                    {
                                                                        throw new Exception($"{TargetPath} is already exists");
                                                                    }

                                                                    break;
                                                                }
                                                            case CollisionOptions.RenameOnCollision:
                                                                {
                                                                    TargetPath = Helper.MTPGenerateUniquePath(DestinationDevice, TargetPath, CreateType.Folder);
                                                                    break;
                                                                }
                                                            case CollisionOptions.OverrideOnCollision:
                                                                {
                                                                    DestinationDevice.DeleteDirectory(TargetPath, true);
                                                                    break;
                                                                }
                                                            case CollisionOptions.Skip:
                                                                {
                                                                    if (DestinationDevice.DirectoryExists(TargetPath))
                                                                    {
                                                                        continue;
                                                                    }

                                                                    break;
                                                                }
                                                        }

                                                        string TempDirectoryPath = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"))).FullName;

                                                        try
                                                        {
                                                            SourceDevice.DownloadFolder(SourceRelativePath, TempDirectoryPath, Cancellation.Token, (s, e) =>
                                                            {
                                                                PipeProgressWriterController?.SendData(Convert.ToString(Math.Ceiling(CurrentPosition + (e.ProgressPercentage / 200 * EachTaskStep))));
                                                            });

                                                            DestinationDevice.UploadFolder(TempDirectoryPath, TargetPath, Cancellation.Token, (s, e) =>
                                                            {
                                                                PipeProgressWriterController?.SendData(Convert.ToString(Math.Ceiling(CurrentPosition + (EachTaskStep / 2) + (e.ProgressPercentage / 200 * EachTaskStep))));
                                                            });

                                                            SourceDevice.DeleteDirectory(SourceRelativePath, true);
                                                        }
                                                        finally
                                                        {
                                                            Directory.Delete(TempDirectoryPath, true);
                                                        }
                                                    }

                                                    CurrentPosition += EachTaskStep;
                                                }

                                                Value.Add("Success", JsonSerializer.Serialize(Array.Empty<string>(), JsonSourceGenerationContext.Default.IEnumerableString));
                                            }
                                            else
                                            {
                                                Value.Add("Error_NotFound", $"One of path in \"{nameof(SourcePathList)}\" is not a file or directory");
                                            }
                                        }
                                        else
                                        {
                                            Value.Add("Error_NotFound", "MTP device is not found");
                                        }
                                    }
                                    else if (DestinationPath.StartsWith(@"\\?\"))
                                    {
                                        MTPPathAnalysis DestinationPathAnalysis = new MTPPathAnalysis(DestinationPath);

                                        if (MTPManager.GetDeviceFromId(DestinationPathAnalysis.DeviceId) is MediaDevice DestinationDevice)
                                        {
                                            if (SourcePathList.Keys.All((Item) => Directory.Exists(Item) || File.Exists(Item)))
                                            {
                                                double CurrentPosition = 0;
                                                double EachTaskStep = 100d / SourcePathList.Keys.Count();

                                                foreach (string SourcePath in SourcePathList.Keys)
                                                {
                                                    Cancellation.Token.ThrowIfCancellationRequested();

                                                    if (File.Exists(SourcePath))
                                                    {
                                                        string TargetPath = Path.Combine(DestinationPathAnalysis.RelativePath, Path.GetFileName(SourcePath));

                                                        switch (Option)
                                                        {
                                                            case CollisionOptions.None:
                                                                {
                                                                    if (DestinationDevice.FileExists(TargetPath))
                                                                    {
                                                                        throw new Exception($"{TargetPath} is already exists");
                                                                    }

                                                                    break;
                                                                }
                                                            case CollisionOptions.RenameOnCollision:
                                                                {
                                                                    TargetPath = Helper.MTPGenerateUniquePath(DestinationDevice, TargetPath, CreateType.File);
                                                                    break;
                                                                }
                                                            case CollisionOptions.OverrideOnCollision:
                                                                {
                                                                    DestinationDevice.DeleteFile(TargetPath);
                                                                    break;
                                                                }
                                                            case CollisionOptions.Skip:
                                                                {
                                                                    if (DestinationDevice.FileExists(TargetPath))
                                                                    {
                                                                        continue;
                                                                    }

                                                                    break;
                                                                }
                                                        }

                                                        DestinationDevice.UploadFile(SourcePath, TargetPath, Cancellation.Token, (s, e) =>
                                                        {
                                                            PipeProgressWriterController?.SendData(Convert.ToString(Math.Ceiling(CurrentPosition + (e.ProgressPercentage / 100 * EachTaskStep))));
                                                        });

                                                        File.Delete(SourcePath);
                                                    }
                                                    else if (Directory.Exists(SourcePath))
                                                    {
                                                        string TargetPath = Path.Combine(DestinationPathAnalysis.RelativePath, Path.GetFileName(SourcePath));

                                                        switch (Option)
                                                        {
                                                            case CollisionOptions.None:
                                                                {
                                                                    if (DestinationDevice.DirectoryExists(TargetPath))
                                                                    {
                                                                        throw new Exception($"{TargetPath} is already exists");
                                                                    }

                                                                    break;
                                                                }
                                                            case CollisionOptions.RenameOnCollision:
                                                                {
                                                                    TargetPath = Helper.MTPGenerateUniquePath(DestinationDevice, TargetPath, CreateType.Folder);
                                                                    break;
                                                                }
                                                            case CollisionOptions.OverrideOnCollision:
                                                                {
                                                                    DestinationDevice.DeleteDirectory(TargetPath, true);
                                                                    break;
                                                                }
                                                            case CollisionOptions.Skip:
                                                                {
                                                                    if (DestinationDevice.DirectoryExists(TargetPath))
                                                                    {
                                                                        continue;
                                                                    }

                                                                    break;
                                                                }
                                                        }

                                                        DestinationDevice.UploadFolder(SourcePath, TargetPath, Cancellation.Token, (s, e) =>
                                                        {
                                                            PipeProgressWriterController?.SendData(Convert.ToString(Math.Ceiling(CurrentPosition + (e.ProgressPercentage / 100 * EachTaskStep))));
                                                        });

                                                        Directory.Delete(SourcePath, true);
                                                    }

                                                    CurrentPosition += EachTaskStep;
                                                }

                                                Value.Add("Success", JsonSerializer.Serialize(Array.Empty<string>(), JsonSourceGenerationContext.Default.IEnumerableString));
                                            }
                                            else
                                            {
                                                Value.Add("Error_NotFound", $"One of path in \"{nameof(SourcePathList)}\" is not a file or directory");
                                            }
                                        }
                                        else
                                        {
                                            Value.Add("Error_NotFound", "MTP device is not found");
                                        }
                                    }
                                    else if (SourcePathList.Keys.All((Source) => Source.StartsWith(@"\\?\")))
                                    {
                                        MTPPathAnalysis SourcePathAnalysis = new MTPPathAnalysis(SourcePathList.Keys.First());

                                        if (MTPManager.GetDeviceFromId(SourcePathAnalysis.DeviceId) is MediaDevice SourceDevice)
                                        {
                                            IReadOnlyList<string> SourceRelativePathArray = SourcePathList.Keys.Select((Source) => new MTPPathAnalysis(Source).RelativePath).ToArray();

                                            if (SourceRelativePathArray.All((SourceRelativePath) => SourceDevice.FileExists(SourceRelativePath) || SourceDevice.DirectoryExists(SourceRelativePath)))
                                            {
                                                double CurrentPosition = 0;
                                                double EachTaskStep = 100d / SourceRelativePathArray.Count;

                                                foreach (string SourceRelativePath in SourceRelativePathArray)
                                                {
                                                    Cancellation.Token.ThrowIfCancellationRequested();

                                                    if (SourceDevice.FileExists(SourceRelativePath))
                                                    {
                                                        string TargetPath = Path.Combine(DestinationPath, Path.GetFileName(SourceRelativePath));

                                                        switch (Option)
                                                        {
                                                            case CollisionOptions.None:
                                                                {
                                                                    if (File.Exists(TargetPath))
                                                                    {
                                                                        throw new Exception($"{TargetPath} is already exists");
                                                                    }

                                                                    break;
                                                                }
                                                            case CollisionOptions.RenameOnCollision:
                                                                {
                                                                    if (File.Exists(TargetPath))
                                                                    {
                                                                        TargetPath = Helper.GenerateUniquePathOnLocal(TargetPath, CreateType.File);
                                                                    }

                                                                    break;
                                                                }
                                                            case CollisionOptions.OverrideOnCollision:
                                                                {
                                                                    if (File.Exists(TargetPath))
                                                                    {
                                                                        File.Delete(TargetPath);
                                                                    }

                                                                    break;
                                                                }
                                                            case CollisionOptions.Skip:
                                                                {
                                                                    if (File.Exists(TargetPath))
                                                                    {
                                                                        continue;
                                                                    }

                                                                    break;
                                                                }
                                                        }

                                                        SourceDevice.DownloadFile(SourceRelativePath, TargetPath, Cancellation.Token, (s, e) =>
                                                        {
                                                            PipeProgressWriterController?.SendData(Convert.ToString(Math.Ceiling(CurrentPosition + (e.ProgressPercentage / 100 * EachTaskStep))));
                                                        });

                                                        SourceDevice.DeleteFile(SourceRelativePath);
                                                    }
                                                    else if (SourceDevice.DirectoryExists(SourceRelativePath))
                                                    {
                                                        string TargetPath = Path.Combine(DestinationPath, Path.GetFileName(SourceRelativePath));

                                                        switch (Option)
                                                        {
                                                            case CollisionOptions.None:
                                                                {
                                                                    if (Directory.Exists(TargetPath))
                                                                    {
                                                                        throw new Exception($"{TargetPath} is already exists");
                                                                    }

                                                                    break;
                                                                }
                                                            case CollisionOptions.RenameOnCollision:
                                                                {
                                                                    if (Directory.Exists(TargetPath))
                                                                    {
                                                                        TargetPath = Helper.GenerateUniquePathOnLocal(TargetPath, CreateType.Folder);
                                                                    }

                                                                    break;
                                                                }
                                                            case CollisionOptions.OverrideOnCollision:
                                                                {
                                                                    if (Directory.Exists(TargetPath))
                                                                    {
                                                                        Directory.Delete(SourceRelativePath, true);
                                                                    }

                                                                    break;
                                                                }
                                                            case CollisionOptions.Skip:
                                                                {
                                                                    if (Directory.Exists(TargetPath))
                                                                    {
                                                                        continue;
                                                                    }

                                                                    break;
                                                                }
                                                        }

                                                        SourceDevice.DownloadFolder(SourceRelativePath, TargetPath, Cancellation.Token, (s, e) =>
                                                        {
                                                            PipeProgressWriterController?.SendData(Convert.ToString(Math.Ceiling(CurrentPosition + (e.ProgressPercentage / 100 * EachTaskStep))));
                                                        });

                                                        SourceDevice.DeleteDirectory(SourceRelativePath, true);
                                                    }

                                                    CurrentPosition += EachTaskStep;
                                                }

                                                Value.Add("Success", JsonSerializer.Serialize(Array.Empty<string>(), JsonSourceGenerationContext.Default.IEnumerableString));
                                            }
                                            else
                                            {
                                                Value.Add("Error_NotFound", $"One of path in \"{nameof(SourcePathList)}\" is not a file or directory");
                                            }
                                        }
                                        else
                                        {
                                            Value.Add("Error_NotFound", "MTP device is not found");
                                        }
                                    }
                                    else if (SourcePathList.Keys.All((Item) => Directory.Exists(Item) || File.Exists(Item)))
                                    {
                                        List<string> OperationRecordList = new List<string>(SourcePathList.Count);

                                        if (SourcePathList.Keys.Any(StorageItemController.CheckCaptured))
                                        {
                                            Value.Add("Error_Capture", "One of these files was captured and could not be moved");
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
                                                        if (Cancellation.Token.IsCancellationRequested)
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
                                                                Value.Add("Success", JsonSerializer.Serialize(OperationRecordList, JsonSourceGenerationContext.Default.IEnumerableString));
                                                            }
                                                            else
                                                            {
                                                                Value.Add("Error_Capture", "One of these files was captured and could not be moved");
                                                            }
                                                        }
                                                    }
                                                    else if (Marshal.GetLastWin32Error() == 5)
                                                    {
                                                        IDictionary<string, string> ResultMap = CreateNewProcessAsElevatedAndWaitForResult(new ElevationMoveData(SourcePathList, DestinationPath, Option), (s, e) =>
                                                        {
                                                            PipeProgressWriterController?.SendData(Convert.ToString(e.ProgressPercentage));
                                                        }, Cancellation.Token);

                                                        foreach (KeyValuePair<string, string> Result in ResultMap)
                                                        {
                                                            Value.Add(Result);
                                                        }
                                                    }
                                                    else if (!Value.ContainsKey("Error_UserCancel"))
                                                    {
                                                        Value.Add("Error_Failure", new Win32Exception(Marshal.GetLastWin32Error()).Message);
                                                    }
                                                }
                                                catch (COMException ex) when (ex.ErrorCode == HRESULT.E_ABORT)
                                                {
                                                    throw new OperationCanceledException();
                                                }
                                            }
                                            else
                                            {
                                                IDictionary<string, string> ResultMap = CreateNewProcessAsElevatedAndWaitForResult(new ElevationMoveData(SourcePathList, DestinationPath, Option), (s, e) =>
                                                {
                                                    PipeProgressWriterController?.SendData(Convert.ToString(e.ProgressPercentage));
                                                }, Cancellation.Token);

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
                                }
                                catch (OperationCanceledException)
                                {
                                    Value.Add("Error_Cancelled", "Operation is cancelled");
                                }

                                break;
                            }
                        case AuxiliaryTrustProcessCommandType.Delete:
                            {
                                string ExecutePathJson = CommandValue["ExecutePath"];

                                bool PermanentDelete = Convert.ToBoolean(CommandValue["PermanentDelete"]);

                                IReadOnlyList<string> ExecutePathList = JsonSerializer.Deserialize(ExecutePathJson, JsonSourceGenerationContext.Default.IEnumerableString).ToArray();

                                try
                                {
                                    if (ExecutePathList.All((Source) => Source.StartsWith(@"\\?\")))
                                    {
                                        MTPPathAnalysis SourcePathAnalysis = new MTPPathAnalysis(ExecutePathList[0]);

                                        if (MTPManager.GetDeviceFromId(SourcePathAnalysis.DeviceId) is MediaDevice MTPDevice)
                                        {
                                            IReadOnlyList<string> RelativePathArray = ExecutePathList.Select((Source) => new MTPPathAnalysis(Source).RelativePath).ToArray();

                                            if (RelativePathArray.All((RelativePath) => MTPDevice.FileExists(RelativePath) || MTPDevice.DirectoryExists(RelativePath)))
                                            {
                                                double CurrentPosition = 0;
                                                double EachTaskStep = 100d / RelativePathArray.Count;

                                                foreach (string Path in RelativePathArray)
                                                {
                                                    Cancellation.Token.ThrowIfCancellationRequested();

                                                    if (MTPDevice.FileExists(Path))
                                                    {
                                                        MTPDevice.DeleteFile(Path);
                                                    }
                                                    else if (MTPDevice.DirectoryExists(Path))
                                                    {
                                                        MTPDevice.DeleteDirectory(Path, true);
                                                    }

                                                    PipeProgressWriterController.SendData(Convert.ToString(CurrentPosition += EachTaskStep));
                                                }

                                                Value.Add("Success", JsonSerializer.Serialize(Array.Empty<string>(), JsonSourceGenerationContext.Default.IEnumerableString));
                                            }
                                            else
                                            {
                                                Value.Add("Error_NotFound", $"One of path in \"{nameof(ExecutePathList)}\" is not a file or directory");
                                            }
                                        }
                                        else
                                        {
                                            Value.Add("Error_NotFound", "MTP device is not found");
                                        }
                                    }
                                    else if (ExecutePathList.All((Item) => Directory.Exists(Item) || File.Exists(Item)))
                                    {
                                        if (ExecutePathList.Any(StorageItemController.CheckCaptured))
                                        {
                                            Value.Add("Error_Capture", "An error occurred while deleting the files");
                                        }
                                        else if (ExecutePathList.All((Path) => StorageItemController.CheckPermission(System.IO.Path.GetDirectoryName(Path) ?? Path, FileSystemRights.Modify)))
                                        {
                                            try
                                            {
                                                int ProcessedCounter = 0;
                                                List<string> OperationRecordList = new List<string>(ExecutePathList.Count);

                                                if (StorageItemController.Delete(ExecutePathList, PermanentDelete, (s, e) =>
                                                {
                                                    if (Cancellation.Token.IsCancellationRequested)
                                                    {
                                                        throw new COMException(null, HRESULT.E_ABORT);
                                                    }

                                                    PipeProgressWriterController?.SendData(Convert.ToString(e.ProgressPercentage));
                                                },
                                                PostDeleteEvent: (se, arg) =>
                                                {
                                                    PipeProgressWriterController?.SendData(Convert.ToString((int)(++ProcessedCounter / ExecutePathList.Count * 100f)));

                                                    if (!PermanentDelete)
                                                    {
                                                        OperationRecordList.Add($"{arg.SourceItem.FileSystemPath}||Delete");
                                                    }
                                                }))
                                                {
                                                    if (ExecutePathList.All((Item) => !Directory.Exists(Item) && !File.Exists(Item)))
                                                    {
                                                        Value.Add("Success", JsonSerializer.Serialize(OperationRecordList, JsonSourceGenerationContext.Default.IEnumerableString));
                                                    }
                                                    else
                                                    {
                                                        Value.Add("Error_Capture", "An error occurred while deleting the folder");
                                                    }
                                                }
                                                else if (Marshal.GetLastWin32Error() == 5)
                                                {
                                                    IDictionary<string, string> ResultMap = CreateNewProcessAsElevatedAndWaitForResult(new ElevationDeleteData(ExecutePathList, PermanentDelete), (s, e) =>
                                                    {
                                                        PipeProgressWriterController?.SendData(Convert.ToString(e.ProgressPercentage));
                                                    }, Cancellation.Token);

                                                    foreach (KeyValuePair<string, string> Result in ResultMap)
                                                    {
                                                        Value.Add(Result);
                                                    }
                                                }
                                                else
                                                {
                                                    Value.Add("Error_Failure", new Win32Exception(Marshal.GetLastWin32Error()).Message);
                                                }
                                            }
                                            catch (COMException ex) when (ex.ErrorCode == HRESULT.E_ABORT)
                                            {
                                                throw new OperationCanceledException();
                                            }
                                        }
                                        else
                                        {
                                            IDictionary<string, string> ResultMap = CreateNewProcessAsElevatedAndWaitForResult(new ElevationDeleteData(ExecutePathList, PermanentDelete), (s, e) =>
                                            {
                                                PipeProgressWriterController?.SendData(Convert.ToString(e.ProgressPercentage));
                                            }, Cancellation.Token);

                                            foreach (KeyValuePair<string, string> Result in ResultMap)
                                            {
                                                Value.Add(Result);
                                            }
                                        }
                                    }
                                    else
                                    {
                                        Value.Add("Error_NotFound", $"One of path in \"{nameof(ExecutePathList)}\" is not a file or directory");
                                    }
                                }
                                catch (OperationCanceledException)
                                {
                                    Value.Add("Error_Cancelled", "Operation is cancelled");
                                }

                                break;
                            }
                        case AuxiliaryTrustProcessCommandType.RunExecutable:
                            {
                                string ExecutePath = CommandValue["ExecutePath"];
                                string ExecuteParameter = CommandValue["ExecuteParameter"];
                                string ExecuteAuthority = CommandValue["ExecuteAuthority"];
                                string ExecuteWindowStyle = CommandValue["ExecuteWindowStyle"];
                                string ExecuteWorkDirectory = CommandValue["ExecuteWorkDirectory"];

                                bool ShouldWaitForExit = Convert.ToBoolean(CommandValue["ExecuteShouldWaitForExit"]);
                                bool ExecuteCreateNoWindow = Convert.ToBoolean(CommandValue["ExecuteCreateNoWindow"]);

                                if (!string.IsNullOrEmpty(ExecutePath))
                                {
                                    if (StorageItemController.CheckPermission(ExecutePath, FileSystemRights.ReadAndExecute))
                                    {
                                        try
                                        {
                                            IReadOnlyList<HWND> WindowsBeforeStartup = Helper.GetCurrentWindowsHandles().ToArray();

                                            Shell32.SHELLEXECUTEINFO ExecuteInfo = new Shell32.SHELLEXECUTEINFO
                                            {
                                                hwnd = HWND.NULL,
                                                lpVerb = Regex.IsMatch(Path.GetExtension(ExecutePath), @"\.(exe|bat|msi|msc|cmd)$", RegexOptions.IgnoreCase) && ExecuteAuthority == "Administrator" ? "runas" : null,
                                                cbSize = Marshal.SizeOf<Shell32.SHELLEXECUTEINFO>(),
                                                lpFile = ExecutePath,
                                                lpParameters = string.IsNullOrWhiteSpace(ExecuteParameter) ? null : ExecuteParameter,
                                                lpDirectory = string.IsNullOrWhiteSpace(ExecuteWorkDirectory) ? null : ExecuteWorkDirectory,
                                                fMask = Shell32.ShellExecuteMaskFlags.SEE_MASK_FLAG_NO_UI
                                                        | Shell32.ShellExecuteMaskFlags.SEE_MASK_UNICODE
                                                        | Shell32.ShellExecuteMaskFlags.SEE_MASK_DOENVSUBST
                                                        | Shell32.ShellExecuteMaskFlags.SEE_MASK_NOASYNC
                                                        | Shell32.ShellExecuteMaskFlags.SEE_MASK_NOCLOSEPROCESS,
                                                nShellExecuteShow = ExecuteCreateNoWindow
                                                                        ? ShowWindowCommand.SW_HIDE
                                                                        : Enum.Parse<ProcessWindowStyle>(ExecuteWindowStyle) switch
                                                                        {
                                                                            ProcessWindowStyle.Hidden => ShowWindowCommand.SW_HIDE,
                                                                            ProcessWindowStyle.Minimized => ShowWindowCommand.SW_SHOWMINIMIZED,
                                                                            ProcessWindowStyle.Maximized => ShowWindowCommand.SW_SHOWMAXIMIZED,
                                                                            _ => ShowWindowCommand.SW_NORMAL
                                                                        },
                                            };

                                            if (Shell32.ShellExecuteEx(ref ExecuteInfo))
                                            {
                                                if (!ExecuteInfo.hProcess.IsNull)
                                                {
                                                    try
                                                    {
                                                        NtDll.NtQueryResult<NtDll.PROCESS_BASIC_INFORMATION> Information = NtDll.NtQueryInformationProcess<NtDll.PROCESS_BASIC_INFORMATION>(ExecuteInfo.hProcess, NtDll.PROCESSINFOCLASS.ProcessBasicInformation);

                                                        if (Information.HasValue)
                                                        {
                                                            using (Process OpenedProcess = Process.GetProcessById(Information.Value.UniqueProcessId.ToInt32()))
                                                            {
                                                                if (Helper.GetWindowInformationFromUwpApplication(Convert.ToUInt32((ExplorerProcess?.Id).GetValueOrDefault())) is WindowInformation Info)
                                                                {
                                                                    if (Info.IsValidInfomation)
                                                                    {
                                                                        if (!Helper.IsTopMostWindow(Info.ApplicationFrameWindowHandle))
                                                                        {
                                                                            SetWindowsZPosition(OpenedProcess, WindowsBeforeStartup);
                                                                        }
                                                                    }
                                                                }

                                                                if (ShouldWaitForExit)
                                                                {
                                                                    OpenedProcess.WaitForExit();
                                                                }
                                                            }
                                                        }
                                                    }
                                                    catch (Exception)
                                                    {
                                                        // No need to handle exception in here
                                                    }
                                                    finally
                                                    {
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
                                                    ShowWindowCommand = ExecuteCreateNoWindow ? ShowWindowCommand.SW_HIDE : Enum.Parse<ProcessWindowStyle>(ExecuteWindowStyle) switch
                                                    {
                                                        ProcessWindowStyle.Hidden => ShowWindowCommand.SW_HIDE,
                                                        ProcessWindowStyle.Minimized => ShowWindowCommand.SW_SHOWMINIMIZED,
                                                        ProcessWindowStyle.Maximized => ShowWindowCommand.SW_SHOWMAXIMIZED,
                                                        _ => ShowWindowCommand.SW_NORMAL
                                                    },
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
                                                        using (Process OpenedProcess = Process.GetProcessById(Convert.ToInt32(PInfo.dwProcessId)))
                                                        {
                                                            if (Helper.GetWindowInformationFromUwpApplication(Convert.ToUInt32((ExplorerProcess?.Id).GetValueOrDefault())) is WindowInformation Info)
                                                            {
                                                                if (Info.IsValidInfomation)
                                                                {
                                                                    if (!Helper.IsTopMostWindow(Info.ApplicationFrameWindowHandle))
                                                                    {
                                                                        SetWindowsZPosition(OpenedProcess, WindowsBeforeStartup);
                                                                    }
                                                                }
                                                            }

                                                            if (ShouldWaitForExit)
                                                            {
                                                                OpenedProcess.WaitForExit();
                                                            }
                                                        }
                                                    }
                                                    catch (Exception)
                                                    {
                                                        // No need to handle exception in here
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
                        case AuxiliaryTrustProcessCommandType.GetRemoteClipboardRelatedData:
                            {
                                RemoteClipboardRelatedData RelatedData = RemoteDataObject.GetRemoteClipboardRelatedInformation();

                                if ((RelatedData?.ItemsCount).GetValueOrDefault() > 0)
                                {
                                    Value.Add("Success", JsonSerializer.Serialize(RelatedData, JsonSourceGenerationContext.Default.RemoteClipboardRelatedData));
                                }
                                else
                                {
                                    Value.Add("Error", "No remote data object is available");
                                }

                                break;
                            }
                        case AuxiliaryTrustProcessCommandType.PasteRemoteFile:
                            {
                                string BaseFolderPath = CommandValue["Path"];

                                try
                                {
                                    if (StorageItemController.CheckPermission(BaseFolderPath, FileSystemRights.CreateFiles | FileSystemRights.CreateDirectories))
                                    {
                                        RemoteClipboardRelatedData RelatedData = RemoteDataObject.GetRemoteClipboardRelatedInformation();

                                        if ((RelatedData?.ItemsCount).GetValueOrDefault() > 0)
                                        {
                                            ulong CurrentPosition = 0;

                                            foreach (RemoteClipboardData Package in RemoteDataObject.GetRemoteClipboardData(Cancellation.Token))
                                            {
                                                string TargetPath = Path.Combine(BaseFolderPath, Package.Name);

                                                switch (Package)
                                                {
                                                    case RemoteClipboardFileData FileData:
                                                        {
                                                            if (!Directory.Exists(BaseFolderPath))
                                                            {
                                                                Directory.CreateDirectory(BaseFolderPath);
                                                            }

                                                            string UniqueName = Helper.GenerateUniquePathOnLocal(TargetPath, CreateType.File);

                                                            using (FileStream Stream = File.Open(UniqueName, FileMode.CreateNew, FileAccess.Write))
                                                            {
                                                                FileData.ContentStream.CopyTo(Stream, Convert.ToInt64(FileData.Size), Cancellation.Token, (s, e) =>
                                                                {
                                                                    PipeProgressWriterController?.SendData(Convert.ToString(Math.Ceiling((CurrentPosition + Convert.ToUInt64(e.ProgressPercentage / 100d * FileData.Size)) * 100d / RelatedData.TotalSize)));
                                                                });
                                                            }

                                                            CurrentPosition += FileData.Size;

                                                            break;
                                                        }
                                                    case RemoteClipboardFolderData:
                                                        {
                                                            if (!Directory.Exists(TargetPath))
                                                            {
                                                                Directory.CreateDirectory(TargetPath);
                                                            }

                                                            break;
                                                        }
                                                    default:
                                                        {
                                                            throw new NotSupportedException();
                                                        }
                                                }
                                            }

                                            Value.Add("Success", string.Empty);
                                        }
                                        else
                                        {
                                            Value.Add("Error", "No remote data object is available");
                                        }
                                    }
                                    else
                                    {
                                        IDictionary<string, string> ResultMap = CreateNewProcessAsElevatedAndWaitForResult(new ElevationRemoteCopyData(BaseFolderPath), (s, e) =>
                                        {
                                            PipeProgressWriterController?.SendData(Convert.ToString(e.ProgressPercentage));
                                        }, Cancellation.Token);

                                        foreach (KeyValuePair<string, string> Result in ResultMap)
                                        {
                                            Value.Add(Result);
                                        }
                                    }
                                }
                                catch (OperationCanceledException)
                                {
                                    Value.Add("Error_Cancelled", "Operation is cancelled");
                                }

                                break;
                            }
                        case AuxiliaryTrustProcessCommandType.GetThumbnailOverlay:
                            {
                                Value.Add("Success", JsonSerializer.Serialize(Helper.GetThumbnailOverlay(CommandValue["Path"]), JsonSourceGenerationContext.Default.IEnumerableByte));

                                break;
                            }
                        case AuxiliaryTrustProcessCommandType.SetAsTopMostWindow:
                            {
                                uint ProcessId = Convert.ToUInt32(CommandValue["ProcessId"]);

                                if (Helper.GetWindowInformationFromUwpApplication(ProcessId) is WindowInformation Info)
                                {
                                    if (Info.IsValidInfomation)
                                    {
                                        User32.SetWindowPos(Info.ApplicationFrameWindowHandle, new IntPtr(-1), 0, 0, 0, 0, User32.SetWindowPosFlags.SWP_NOMOVE | User32.SetWindowPosFlags.SWP_NOSIZE);
                                    }

                                    Value.Add("Success", string.Empty);
                                }
                                else
                                {
                                    Value.Add("Error", "Could not found the window handle");
                                }

                                break;
                            }
                        case AuxiliaryTrustProcessCommandType.RemoveTopMostWindow:
                            {
                                uint ProcessId = Convert.ToUInt32(CommandValue["ProcessId"]);

                                if (Helper.GetWindowInformationFromUwpApplication(ProcessId) is WindowInformation Info)
                                {
                                    if (Info.IsValidInfomation)
                                    {
                                        User32.SetWindowPos(Info.ApplicationFrameWindowHandle, new IntPtr(-2), 0, 0, 0, 0, User32.SetWindowPosFlags.SWP_NOMOVE | User32.SetWindowPosFlags.SWP_NOSIZE);
                                    }

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
                    Value.Add("Error", $"An exception was threw during command processing, exception message: {ex.Message}");
                    LogTracer.Log(ex, $"An exception was threw during command processing, exception message: {ex.Message}");
                }
            }

            return Value;
        }

        private static void SetWindowsZPosition(Process OtherProcess, IEnumerable<HWND> WindowsBeforeStartup)
        {
            static void SetWindowsPosFallback(IEnumerable<HWND> WindowsBeforeStartup)
            {
                foreach (HWND Handle in Helper.GetCurrentWindowsHandles().Except(WindowsBeforeStartup).Reverse())
                {
                    User32.SetWindowPos(Handle, User32.SpecialWindowHandles.HWND_TOPMOST, 0, 0, 0, 0, User32.SetWindowPosFlags.SWP_NOMOVE | User32.SetWindowPosFlags.SWP_NOSIZE);
                    User32.SetWindowPos(Handle, User32.SpecialWindowHandles.HWND_NOTOPMOST, 0, 0, 0, 0, User32.SetWindowPosFlags.SWP_NOMOVE | User32.SetWindowPosFlags.SWP_NOSIZE);
                }
            }

            if (!OtherProcess.HasExited)
            {
                try
                {
                    if (OtherProcess.WaitForInputIdle(5000))
                    {
                        IntPtr MainWindowHandle = IntPtr.Zero;

                        for (int i = 0; i < 10 && !OtherProcess.HasExited; i++)
                        {
                            if (OtherProcess.MainWindowHandle.CheckIfValidPtr())
                            {
                                MainWindowHandle = OtherProcess.MainWindowHandle;
                                break;
                            }

                            Thread.Sleep(500);
                        }

                        if (MainWindowHandle.CheckIfValidPtr())
                        {
                            HWND ForegroundWindow = User32.GetForegroundWindow();

                            if (MainWindowHandle == ForegroundWindow)
                            {
                                return;
                            }

                            uint CurrentThreadId = Kernel32.GetCurrentThreadId();
                            uint ForegroundThreadId = User32.GetWindowThreadProcessId(ForegroundWindow, out _);

                            if (User32.AttachThreadInput(ForegroundThreadId, CurrentThreadId, true))
                            {
                                bool IsSuccess = true;

                                try
                                {
                                    if (User32.IsIconic(MainWindowHandle))
                                    {
                                        IsSuccess &= User32.ShowWindow(MainWindowHandle, ShowWindowCommand.SW_RESTORE);
                                    }

                                    if (User32.SystemParametersInfo(User32.SPI.SPI_GETFOREGROUNDLOCKTIMEOUT, out uint LockTimeout))
                                    {
                                        try
                                        {
                                            IsSuccess &= User32.SystemParametersInfo<uint>(User32.SPI.SPI_SETFOREGROUNDLOCKTIMEOUT, 0, false, false);

                                            if (User32.AllowSetForegroundWindow((uint)Environment.ProcessId))
                                            {
                                                IsSuccess &= User32.SetForegroundWindow(MainWindowHandle);
                                            }
                                        }
                                        finally
                                        {
                                            User32.SystemParametersInfo(User32.SPI.SPI_SETFOREGROUNDLOCKTIMEOUT, LockTimeout, false, false);
                                        }
                                    }

                                    if (Helper.GetWindowInformationFromUwpApplication(Convert.ToUInt32((ExplorerProcess?.Id).GetValueOrDefault())) is WindowInformation UwpInfo)
                                    {
                                        if (UwpInfo.IsValidInfomation)
                                        {
                                            IsSuccess &= User32.SetWindowPos(UwpInfo.ApplicationFrameWindowHandle, MainWindowHandle, 0, 0, 0, 0, User32.SetWindowPosFlags.SWP_NOMOVE | User32.SetWindowPosFlags.SWP_NOSIZE | User32.SetWindowPosFlags.SWP_NOACTIVATE);
                                        }
                                    }
                                }
                                finally
                                {
                                    User32.AttachThreadInput(ForegroundThreadId, CurrentThreadId, false);
                                }

                                if (IsSuccess)
                                {
                                    return;
                                }
                            }
                        }
                    }

                    SetWindowsPosFallback(WindowsBeforeStartup);
                }
                catch (Exception ex)
                {
                    LogTracer.Log(ex, "Could not set the windows position");
                }
            }
        }

        private static IDictionary<string, string> CreateNewProcessAsElevatedAndWaitForResult<T>(T Data, ProgressChangedEventHandler Progress = null, CancellationToken CancelToken = default) where T : IElevationData
        {
            string UniqueName = Guid.NewGuid().ToString("N");
            string PipeName = $"FullTrustProcess_ElevatedPipe_{UniqueName}";
            string ProgressPipeName = $"FullTrustProcess_ElevatedProgressPipe_{UniqueName}";
            string CancelSignalName = $"FullTrustProcess_ElevatedCancellation_{UniqueName}";

            using (EventWaitHandle CancelEvent = new EventWaitHandle(false, EventResetMode.ManualReset, CancelSignalName))
            using (NamedPipeServerStream CommunicatePipeStream = new NamedPipeServerStream(PipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Message, PipeOptions.Asynchronous | PipeOptions.WriteThrough))
            using (NamedPipeServerStream ProgressPipeStream = new NamedPipeServerStream(ProgressPipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Message, PipeOptions.Asynchronous | PipeOptions.WriteThrough))
            using (StreamWriter CommunicatePipeWriter = new StreamWriter(CommunicatePipeStream, Encoding.Unicode, leaveOpen: true))
            using (StreamReader CommunicatePipeReader = new StreamReader(CommunicatePipeStream, Encoding.Unicode, true, leaveOpen: true))
            using (StreamReader ProgressPipeReader = new StreamReader(ProgressPipeStream, Encoding.Unicode, true, leaveOpen: true))
            using (CancellationTokenSource ProgressCancellation = new CancellationTokenSource())
            using (Process ElevatedProcess = UACUtil.StartElevated(new ProcessStartInfo
            {
                UseShellExecute = true,
                FileName = Environment.ProcessPath,
                Arguments = $"--ExecuteElevatedOperation --CommunicatePipeName {PipeName} --ProgressPipeName {ProgressPipeName} --CancelSignalName {CancelSignalName}"
            }))
            {
                Task GetProgressResultTask = Task.CompletedTask;
                Task<string> GetRawResultTask = Task.FromResult(string.Empty);

                try
                {
                    using (CancellationTokenSource WaitPipeConnectionCancellation = new CancellationTokenSource(TimeSpan.FromSeconds(15)))
                    using (CancellationTokenSource CombineCancellation = CancellationTokenSource.CreateLinkedTokenSource(CancelToken, WaitPipeConnectionCancellation.Token))
                    {
                        Task.WaitAll(new Task[] { CommunicatePipeStream.WaitForConnectionAsync(CombineCancellation.Token), ProgressPipeStream.WaitForConnectionAsync(CombineCancellation.Token) }, CombineCancellation.Token);
                    }

                    CommunicatePipeWriter.WriteLine(JsonSerializer.Serialize(Data, JsonSourceGenerationContext.Default.IElevationData));
                    CommunicatePipeWriter.Flush();

                    GetRawResultTask = CommunicatePipeReader.ReadLineAsync(CancelToken).AsTask();
                    GetProgressResultTask = Task.Factory.StartNew((Parameter) =>
                    {
                        try
                        {
                            if (Parameter is CancellationToken Token)
                            {
                                while (!Token.IsCancellationRequested)
                                {
                                    string ProgressText = ProgressPipeReader.ReadLine();

                                    if (int.TryParse(ProgressText, out int ProgressValue))
                                    {
                                        if (ProgressValue >= 0)
                                        {
                                            Progress?.Invoke(null, new ProgressChangedEventArgs(ProgressValue, null));

                                            if (ProgressValue < 100)
                                            {
                                                continue;
                                            }
                                        }
                                    }

                                    break;
                                }
                            }
                        }
                        catch (Exception)
                        {
                            //No need to handle this exception
                        }
                    }, ProgressCancellation.Token, TaskCreationOptions.LongRunning);

                    ElevatedProcess.WaitForExitAsync(CancelToken).Wait(CancelToken);
                }
                catch (AggregateException ex) when (ex.Flatten().InnerExceptions.OfType<OperationCanceledException>().Any())
                {
                    CancelEvent.Set();

                    if (!ElevatedProcess.WaitForExit(TimeSpan.FromSeconds(15)))
                    {
                        LogTracer.Log("Elevated process is not exit in 10s and we will not wait for it any more");
                    }

                    throw ex.InnerException;
                }
                catch (OperationCanceledException)
                {
                    CancelEvent.Set();

                    if (!ElevatedProcess.WaitForExit(TimeSpan.FromSeconds(15)))
                    {
                        LogTracer.Log("Elevated process is not exit in 10s and we will not wait for it any more");
                    }

                    throw;
                }
                finally
                {
                    ProgressCancellation.Cancel();
                }

                string RawResultText = GetRawResultTask.Result;

                if (string.IsNullOrEmpty(RawResultText))
                {
                    return new Dictionary<string, string>
                    {
                        { "Success", string.Empty }
                    };
                }
                else
                {
                    return JsonSerializer.Deserialize(RawResultText, JsonSourceGenerationContext.Default.IDictionaryStringString);
                }
            }
        }
    }
}
