using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Vanara.PInvoke;
using Windows.ApplicationModel.AppService;
using Windows.Foundation.Collections;

namespace FullTrustProcess
{
    class Program
    {
        private static AppServiceConnection Connection;

        private static readonly Dictionary<string, NamedPipeServerStream> PipeServers = new Dictionary<string, NamedPipeServerStream>();

        private static readonly HashSet<string> SpecialStringMap = new HashSet<string>()
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "WindowsPowerShell\\v1.0\\powershell.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.SystemX86), "cmd.exe"),
            "wt.exe"
        };

        private readonly static ManualResetEvent ExitLocker = new ManualResetEvent(false);

        private static readonly object Locker = new object();

        [STAThread]
        static async Task Main(string[] args)
        {
            try
            {
                using (Mutex LaunchLocker = new Mutex(true, "RX_Explorer_FullTrustProcess", out bool IsNotExist))
                {
                    if (!IsNotExist)
                    {
                        return;
                    }

                    Connection = new AppServiceConnection
                    {
                        AppServiceName = "CommunicateService",
                        PackageFamilyName = "36186RuoFan.USB_q3e6crc0w375t"
                    };
                    Connection.RequestReceived += Connection_RequestReceived;

                    if (await Connection.OpenAsync() != AppServiceConnectionStatus.Success)
                    {
                        ExitLocker.Set();
                    }

                    ExitLocker.WaitOne();
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine($"FullTrustProcess出现异常，错误信息{e.Message}");
            }
            finally
            {
                Connection?.Dispose();
                ExitLocker?.Dispose();

                PipeServers.Values.ToList().ForEach((Item) =>
                {
                    Item.Disconnect();
                    Item.Dispose();
                });

                PipeServers.Clear();

                Environment.Exit(0);
            }
        }

        private static void InitializeNewNamedPipe(string ID)
        {
            NamedPipeServerStream NewPipeServer = new NamedPipeServerStream($@"Explorer_And_FullTrustProcess_NamedPipe-{ID}", PipeDirection.InOut, NamedPipeServerStream.MaxAllowedServerInstances, PipeTransmissionMode.Byte, PipeOptions.None, 2048, 2048, null, HandleInheritability.None, PipeAccessRights.ChangePermissions);
            PipeSecurity Security = NewPipeServer.GetAccessControl();
            PipeAccessRule ClientRule = new PipeAccessRule(new SecurityIdentifier("S-1-15-2-1"), PipeAccessRights.ReadWrite | PipeAccessRights.CreateNewInstance, AccessControlType.Allow);
            PipeAccessRule OwnerRule = new PipeAccessRule(WindowsIdentity.GetCurrent().Owner, PipeAccessRights.FullControl, AccessControlType.Allow);
            Security.AddAccessRule(ClientRule);
            Security.AddAccessRule(OwnerRule);
            NewPipeServer.SetAccessControl(Security);

            PipeServers.Add(ID, NewPipeServer);
        }

        private async static void Connection_RequestReceived(AppServiceConnection sender, AppServiceRequestReceivedEventArgs args)
        {
            AppServiceDeferral Deferral = args.GetDeferral();

            try
            {
                switch (args.Request.Message["ExcuteType"])
                {
                    case "Excute_RequestClosePipe":
                        {
                            string Guid = Convert.ToString(args.Request.Message["Guid"]);

                            if (PipeServers.ContainsKey(Guid))
                            {
                                PipeServers[Guid].Disconnect();
                                PipeServers[Guid].Dispose();
                                PipeServers.Remove(Guid);
                            }
                            break;
                        }
                    case "Excute_RequestCreateNewPipe":
                        {
                            string Guid = Convert.ToString(args.Request.Message["Guid"]);

                            if (!PipeServers.ContainsKey(Guid))
                            {
                                InitializeNewNamedPipe(Guid);
                            }

                            break;
                        }
                    case "Identity":
                        {
                            await args.Request.SendResponseAsync(new ValueSet { { "Identity", "FullTrustProcess" } });
                            break;
                        }
                    case "Excute_Quicklook":
                        {
                            string ExcutePath = Convert.ToString(args.Request.Message["ExcutePath"]);
                            if (!string.IsNullOrEmpty(ExcutePath))
                            {
                                await QuicklookConnector.SendMessageToQuicklook(ExcutePath);
                            }

                            break;
                        }
                    case "Excute_Check_QuicklookIsAvaliable":
                        {
                            bool IsSuccess = await QuicklookConnector.CheckQuicklookIsAvaliable();
                            ValueSet Result = new ValueSet
                            {
                                {"Check_QuicklookIsAvaliable_Result",IsSuccess }
                            };

                            await args.Request.SendResponseAsync(Result);
                            break;
                        }
                    case "Excute_Get_Associate":
                        {
                            string Path = Convert.ToString(args.Request.Message["ExcutePath"]);
                            string Associate = ExtensionAssociate.GetAssociate(Path);

                            ValueSet Result = new ValueSet
                            {
                                {"Associate_Result", Associate }
                            };

                            await args.Request.SendResponseAsync(Result);
                            break;
                        }
                    case "Excute_Get_RecycleBinItems":
                        {
                            ValueSet Result = new ValueSet();

                            string RecycleItemResult = RecycleBinController.GenerateRecycleItemsByJson();
                            if (string.IsNullOrEmpty(RecycleItemResult))
                            {
                                Result.Add("Error", "Unknown reason");
                            }
                            else
                            {
                                Result.Add("RecycleBinItems_Json_Result", RecycleItemResult);
                            }

                            await args.Request.SendResponseAsync(Result);
                            break;
                        }
                    case "Excute_Empty_RecycleBin":
                        {
                            ValueSet Result = new ValueSet();

                            try
                            {
                                Result.Add("RecycleBinItems_Clear_Result", RecycleBinController.EmptyRecycleBin());
                            }
                            catch (Exception e)
                            {
                                Result.Add("Error", e.Message);
                            }

                            await args.Request.SendResponseAsync(Result);
                            break;
                        }
                    case "Excute_Restore_RecycleItem":
                        {
                            string Path = Convert.ToString(args.Request.Message["ExcutePath"]);

                            ValueSet Result = new ValueSet
                            {
                                {"Restore_Result", RecycleBinController.Restore(Path) }
                            };

                            await args.Request.SendResponseAsync(Result);
                            break;
                        }
                    case "Excute_Delete_RecycleItem":
                        {
                            string Path = Convert.ToString(args.Request.Message["ExcutePath"]);

                            ValueSet Result = new ValueSet
                            {
                                {"Delete_Result", RecycleBinController.Delete(Path) }
                            };

                            await args.Request.SendResponseAsync(Result);
                            break;
                        }
                    case "Excute_EjectUSB":
                        {
                            ValueSet Value = new ValueSet();

                            string Path = Convert.ToString(args.Request.Message["ExcutePath"]);

                            if (string.IsNullOrEmpty(Path))
                            {
                                Value.Add("EjectResult", false);
                            }
                            else
                            {
                                Value.Add("EjectResult", USBController.EjectDevice(Path));
                            }

                            await args.Request.SendResponseAsync(Value);
                            break;
                        }
                    case "Excute_Unlock_Occupy":
                        {
                            ValueSet Value = new ValueSet();

                            string Path = Convert.ToString(args.Request.Message["ExcutePath"]);

                            if (File.Exists(Path))
                            {
                                if (StorageItemController.CheckOccupied(Path))
                                {
                                    if (StorageItemController.TryUnoccupied(Path))
                                    {
                                        Value.Add("Success", string.Empty);
                                    }
                                    else
                                    {
                                        Value.Add("Error_Failure", "Unoccupied failed");
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

                            await args.Request.SendResponseAsync(Value);
                            break;
                        }
                    case "Excute_Copy":
                        {
                            ValueSet Value = new ValueSet();

                            string SourcePathJson = Convert.ToString(args.Request.Message["SourcePath"]);
                            string DestinationPath = Convert.ToString(args.Request.Message["DestinationPath"]);
                            string Guid = Convert.ToString(args.Request.Message["Guid"]);
                            bool IsUndo = Convert.ToBoolean(args.Request.Message["Undo"]);

                            List<KeyValuePair<string, string>> SourcePathList = JsonConvert.DeserializeObject<List<KeyValuePair<string, string>>>(SourcePathJson);
                            List<string> OperationRecordList = new List<string>();

                            if (SourcePathList.All((Item) => Directory.Exists(Item.Key) || File.Exists(Item.Key)))
                            {
                                if (StorageItemController.Copy(SourcePathList, DestinationPath, (s, e) =>
                                {
                                    lock (Locker)
                                    {
                                        try
                                        {
                                            NamedPipeServerStream Server = PipeServers[Guid];

                                            if (!Server.IsConnected)
                                            {
                                                Server.WaitForConnection();
                                            }

                                            using (StreamWriter Writer = new StreamWriter(Server, new UTF8Encoding(false), 1024, true))
                                            {
                                                Writer.WriteLine(e.ProgressPercentage);
                                            }
                                        }
                                        catch
                                        {
                                            Debug.WriteLine("无法传输进度数据");
                                        }
                                    }
                                },
                                (se, arg) =>
                                {
                                    if (arg.Result == HRESULT.S_OK && !IsUndo)
                                    {
                                        if (arg.DestItem == null || string.IsNullOrEmpty(arg.Name))
                                        {
                                            OperationRecordList.Add($"{arg.SourceItem.FileSystemPath}||Copy||{(Directory.Exists(arg.SourceItem.FileSystemPath) ? "Folder" : "File")}||{Path.Combine(arg.DestFolder.FileSystemPath, arg.SourceItem.Name)}");
                                        }
                                        else
                                        {
                                            OperationRecordList.Add($"{arg.SourceItem.FileSystemPath}||Copy||{(Directory.Exists(arg.SourceItem.FileSystemPath) ? "Folder" : "File")}||{Path.Combine(arg.DestFolder.FileSystemPath, arg.Name)}");
                                        }
                                    }
                                }))
                                {
                                    Value.Add("Success", string.Empty);

                                    if (OperationRecordList.Count > 0)
                                    {
                                        Value.Add("OperationRecord", JsonConvert.SerializeObject(OperationRecordList));
                                    }
                                }
                                else
                                {
                                    Value.Add("Error_Failure", "An error occurred while copying the folder");
                                }
                            }
                            else
                            {
                                Value.Add("Error_NotFound", "SourcePath is not a file or directory");
                            }

                            if (!Value.ContainsKey("Success"))
                            {
                                lock (Locker)
                                {
                                    try
                                    {
                                        NamedPipeServerStream Server = PipeServers[Guid];

                                        if (!Server.IsConnected)
                                        {
                                            Server.WaitForConnection();
                                        }

                                        using (StreamWriter Writer = new StreamWriter(Server, new UTF8Encoding(false), 1024, true))
                                        {
                                            Writer.WriteLine("Error_Stop_Signal");
                                        }
                                    }
                                    catch
                                    {
                                        Debug.WriteLine("无法传输进度数据");
                                    }
                                }
                            }

                            await args.Request.SendResponseAsync(Value);
                            break;
                        }
                    case "Excute_Move":
                        {
                            ValueSet Value = new ValueSet();

                            string SourcePathJson = Convert.ToString(args.Request.Message["SourcePath"]);
                            string DestinationPath = Convert.ToString(args.Request.Message["DestinationPath"]);
                            string Guid = Convert.ToString(args.Request.Message["Guid"]);
                            bool IsUndo = Convert.ToBoolean(args.Request.Message["Undo"]);

                            List<KeyValuePair<string, string>> SourcePathList = JsonConvert.DeserializeObject<List<KeyValuePair<string, string>>>(SourcePathJson);
                            List<string> OperationRecordList = new List<string>();

                            if (SourcePathList.All((Item) => Directory.Exists(Item.Key) || File.Exists(Item.Key)))
                            {
                                if (SourcePathList.Where((Path) => File.Exists(Path.Key)).Any((Item) => StorageItemController.CheckOccupied(Item.Key)))
                                {
                                    Value.Add("Error_Capture", "An error occurred while moving the folder");
                                }
                                else
                                {
                                    if (StorageItemController.Move(SourcePathList, DestinationPath, (s, e) =>
                                    {
                                        lock (Locker)
                                        {
                                            try
                                            {
                                                NamedPipeServerStream Server = PipeServers[Guid];

                                                if (!Server.IsConnected)
                                                {
                                                    Server.WaitForConnection();
                                                }

                                                using (StreamWriter Writer = new StreamWriter(Server, new UTF8Encoding(false), 1024, true))
                                                {
                                                    Writer.WriteLine(e.ProgressPercentage);
                                                }
                                            }
                                            catch
                                            {
                                                Debug.WriteLine("无法传输进度数据");
                                            }
                                        }
                                    },
                                    (se, arg) =>
                                    {
                                        if (arg.Result == HRESULT.COPYENGINE_S_DONT_PROCESS_CHILDREN && !IsUndo)
                                        {
                                            if (arg.DestItem == null || string.IsNullOrEmpty(arg.Name))
                                            {
                                                OperationRecordList.Add($"{arg.SourceItem.FileSystemPath}||Move||{(Directory.Exists(arg.SourceItem.FileSystemPath) ? "Folder" : "File")}||{Path.Combine(arg.DestFolder.FileSystemPath, arg.SourceItem.Name)}");
                                            }
                                            else
                                            {
                                                OperationRecordList.Add($"{arg.SourceItem.FileSystemPath}||Move||{(Directory.Exists(arg.SourceItem.FileSystemPath) ? "Folder" : "File")}||{Path.Combine(arg.DestFolder.FileSystemPath, arg.Name)}");
                                            }
                                        }
                                    }))
                                    {
                                        Value.Add("Success", string.Empty);
                                        if (OperationRecordList.Count > 0)
                                        {
                                            Value.Add("OperationRecord", JsonConvert.SerializeObject(OperationRecordList));
                                        }
                                    }
                                    else
                                    {
                                        Value.Add("Error_Failure", "An error occurred while moving the folder");
                                    }
                                }
                            }
                            else
                            {
                                Value.Add("Error_NotFound", "SourcePath is not a file or directory");
                            }

                            if (!Value.ContainsKey("Success"))
                            {
                                lock (Locker)
                                {
                                    try
                                    {
                                        NamedPipeServerStream Server = PipeServers[Guid];

                                        if (!Server.IsConnected)
                                        {
                                            Server.WaitForConnection();
                                        }

                                        using (StreamWriter Writer = new StreamWriter(Server, new UTF8Encoding(false), 1024, true))
                                        {
                                            Writer.WriteLine("Error_Stop_Signal");
                                        }
                                    }
                                    catch
                                    {
                                        Debug.WriteLine("无法传输进度数据");
                                    }
                                }
                            }

                            await args.Request.SendResponseAsync(Value);
                            break;
                        }
                    case "Excute_Delete":
                        {
                            ValueSet Value = new ValueSet();

                            string ExcutePathJson = Convert.ToString(args.Request.Message["ExcutePath"]);
                            string Guid = Convert.ToString(args.Request.Message["Guid"]);
                            bool PermanentDelete = Convert.ToBoolean(args.Request.Message["PermanentDelete"]);
                            bool IsUndo = Convert.ToBoolean(args.Request.Message["Undo"]);

                            List<string> ExcutePathList = JsonConvert.DeserializeObject<List<string>>(ExcutePathJson);
                            List<string> OperationRecordList = new List<string>();

                            try
                            {
                                if (ExcutePathList.All((Item) => Directory.Exists(Item) || File.Exists(Item)))
                                {
                                    if (ExcutePathList.Where((Path) => File.Exists(Path)).Any((Item) => StorageItemController.CheckOccupied(Item)))
                                    {
                                        Value.Add("Error_Capture", "An error occurred while deleting the folder");
                                    }
                                    else
                                    {
                                        ExcutePathList.Where((Path) => File.Exists(Path)).ToList().ForEach((Item) => File.SetAttributes(Item, FileAttributes.Normal));
                                        ExcutePathList.Where((Path) => Directory.Exists(Path)).ToList().ForEach((Item) => _ = new DirectoryInfo(Item)
                                        {
                                            Attributes = FileAttributes.Normal & FileAttributes.Directory
                                        });

                                        if (StorageItemController.Delete(ExcutePathList, PermanentDelete, (s, e) =>
                                        {
                                            lock (Locker)
                                            {
                                                try
                                                {
                                                    NamedPipeServerStream Server = PipeServers[Guid];

                                                    if (!Server.IsConnected)
                                                    {
                                                        Server.WaitForConnection();
                                                    }

                                                    using (StreamWriter Writer = new StreamWriter(Server, new UTF8Encoding(false), 1024, true))
                                                    {
                                                        Writer.WriteLine(e.ProgressPercentage);
                                                    }
                                                }
                                                catch
                                                {
                                                    Debug.WriteLine("无法传输进度数据");
                                                }
                                            }
                                        },
                                        (se, arg) =>
                                        {
                                            if (!PermanentDelete && !IsUndo)
                                            {
                                                OperationRecordList.Add($"{arg.SourceItem.FileSystemPath}||Delete");
                                            }
                                        }))
                                        {
                                            Value.Add("Success", string.Empty);
                                            if (OperationRecordList.Count > 0)
                                            {
                                                Value.Add("OperationRecord", JsonConvert.SerializeObject(OperationRecordList));
                                            }
                                        }
                                        else
                                        {
                                            Value.Add("Error_Failure", "The specified file could not be deleted");
                                        }
                                    }
                                }
                                else
                                {
                                    Value.Add("Error_NotFound", "ExcutePath is not a file or directory");
                                }
                            }
                            catch
                            {
                                Value.Add("Error_Failure", "The specified file or folder could not be deleted");
                            }

                            if (!Value.ContainsKey("Success"))
                            {
                                lock (Locker)
                                {
                                    try
                                    {
                                        NamedPipeServerStream Server = PipeServers[Guid];

                                        if (!Server.IsConnected)
                                        {
                                            Server.WaitForConnection();
                                        }

                                        using (StreamWriter Writer = new StreamWriter(Server, new UTF8Encoding(false), 1024, true))
                                        {
                                            Writer.WriteLine("Error_Stop_Signal");
                                        }
                                    }
                                    catch
                                    {
                                        Debug.WriteLine("无法传输进度终止数据");
                                    }
                                }
                            }

                            await args.Request.SendResponseAsync(Value);
                            break;
                        }
                    case "Excute_RunExe":
                        {
                            string ExcutePath = Convert.ToString(args.Request.Message["ExcutePath"]);
                            string ExcuteParameter = Convert.ToString(args.Request.Message["ExcuteParameter"]);
                            string ExcuteAuthority = Convert.ToString(args.Request.Message["ExcuteAuthority"]);

                            if (!string.IsNullOrEmpty(ExcutePath))
                            {
                                if (string.IsNullOrEmpty(ExcuteParameter))
                                {
                                    if (ExcuteAuthority == "Administrator")
                                    {
                                        using (Process Process = new Process())
                                        {
                                            Process.StartInfo.Verb = "runAs";
                                            Process.Start();
                                        }
                                    }
                                    else
                                    {
                                        using (Process Process = new Process())
                                        {
                                            Process.StartInfo.FileName = ExcutePath;
                                            Process.Start();
                                        }
                                    }
                                }
                                else
                                {
                                    if (SpecialStringMap.Contains(ExcutePath))
                                    {
                                        if (ExcuteAuthority == "Administrator")
                                        {
                                            using (Process Process = new Process())
                                            {
                                                Process.StartInfo.FileName = ExcutePath;
                                                Process.StartInfo.Arguments = ExcuteParameter;
                                                Process.StartInfo.Verb = "runAs";
                                                Process.Start();
                                            }
                                        }
                                        else
                                        {
                                            using (Process Process = new Process())
                                            {
                                                Process.StartInfo.FileName = ExcutePath;
                                                Process.StartInfo.Arguments = ExcuteParameter;
                                                Process.Start();
                                            }
                                        }
                                    }
                                    else
                                    {
                                        if (ExcuteAuthority == "Administrator")
                                        {
                                            using (Process Process = new Process())
                                            {
                                                Process.StartInfo.FileName = ExcutePath;
                                                Process.StartInfo.Arguments = $"\"{ExcuteParameter}\"";
                                                Process.StartInfo.Verb = "runAs";
                                                Process.Start();
                                            }
                                        }
                                        else
                                        {
                                            using (Process Process = new Process())
                                            {
                                                Process.StartInfo.FileName = ExcutePath;
                                                Process.StartInfo.Arguments = $"\"{ExcuteParameter}\"";
                                                Process.Start();
                                            }
                                        }
                                    }
                                }
                            }

                            break;
                        }
                    case "Excute_Test_Connection":
                        {
                            await args.Request.SendResponseAsync(new ValueSet { { "Excute_Test_Connection", string.Empty } });

                            break;
                        }
                    case "Excute_Exit":
                        {
                            ExitLocker.Set();

                            break;
                        }
                }
            }
            catch
            {
                ValueSet Value = new ValueSet
                {
                    {"Error","An exception occurred while processing the instruction" }
                };

                await args.Request.SendResponseAsync(Value);
            }
            finally
            {
                Deferral.Complete();
            }
        }
    }
}
