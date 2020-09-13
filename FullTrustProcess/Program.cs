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
using Vanara.Windows.Shell;
using Windows.ApplicationModel.AppService;
using Windows.Foundation.Collections;
using Windows.Storage;
using FileAttributes = System.IO.FileAttributes;

namespace FullTrustProcess
{
    class Program
    {
        private static AppServiceConnection Connection;

        private static readonly Dictionary<string, NamedPipeServerStream> PipeServers = new Dictionary<string, NamedPipeServerStream>();

        private readonly static ManualResetEvent ExitLocker = new ManualResetEvent(false);

        private static readonly object Locker = new object();

        private static Process ExplorerProcess;

        private static Timer AliveCheckTimer;

        [STAThread]
        static async Task Main(string[] args)
        {
            try
            {
                if (args.Length > 2 && args[1] == "Elevation_Restart")
                {
                    ExplorerProcess = Process.GetProcessById(Convert.ToInt32(args[2]));
                }

                Connection = new AppServiceConnection
                {
                    AppServiceName = "CommunicateService",
                    PackageFamilyName = "36186RuoFan.USB_q3e6crc0w375t"
                };
                Connection.RequestReceived += Connection_RequestReceived;

                if (await Connection.OpenAsync() == AppServiceConnectionStatus.Success)
                {
                    AliveCheckTimer = new Timer(AliveCheck, null, 5000, 5000);
                }
                else
                {
                    ExitLocker.Set();
                }

                ExitLocker.WaitOne();
            }
            catch (Exception e)
            {
                Debug.WriteLine($"FullTrustProcess出现异常，错误信息{e.Message}");
            }
            finally
            {
                Connection?.Dispose();
                ExitLocker?.Dispose();
                AliveCheckTimer?.Dispose();

                ExplorerProcess.Dispose();

                PipeServers.Values.ToList().ForEach((Item) =>
                {
                    Item.Disconnect();
                    Item.Dispose();
                });

                PipeServers.Clear();

                Environment.Exit(0);
            }
        }

        private async static void Connection_RequestReceived(AppServiceConnection sender, AppServiceRequestReceivedEventArgs args)
        {
            AppServiceDeferral Deferral = args.GetDeferral();

            try
            {
                switch (args.Request.Message["ExcuteType"])
                {
                    case "Excute_ElevateAsAdmin":
                        {
                            Connection?.Dispose();
                            Connection = null;

                            using (Process AdminProcess = new Process())
                            {
                                AdminProcess.StartInfo.Verb = "runas";
                                AdminProcess.StartInfo.UseShellExecute = true;
                                AdminProcess.StartInfo.Arguments = $"Elevation_Restart {ExplorerProcess?.Id}";
                                AdminProcess.StartInfo.FileName = Process.GetCurrentProcess().MainModule.FileName;
                                AdminProcess.Start();
                            }

                            ExitLocker.Set();
                            break;
                        }
                    case "Excute_CreateLink":
                        {
                            string LinkPath = Convert.ToString(args.Request.Message["LinkPath"]);
                            string LinkTarget = Convert.ToString(args.Request.Message["LinkTarget"]);
                            string LinkDesc = Convert.ToString(args.Request.Message["LinkDesc"]);
                            string LinkArgument = Convert.ToString(args.Request.Message["LinkArgument"]);

                            ValueSet Value = new ValueSet();

                            try
                            {
                                ShellLink.Create(LinkPath, LinkTarget, description: LinkDesc, arguments: LinkArgument).Dispose();
                                Value.Add("Success", string.Empty);
                            }
                            catch (Exception e)
                            {
                                Value.Add("Error", e.Message);
                            }

                            await args.Request.SendResponseAsync(Value);

                            break;
                        }
                    case "Excute_GetVariable_Path":
                        {
                            string Variable = Convert.ToString(args.Request.Message["Variable"]);

                            ValueSet Value = new ValueSet
                            {
                                {"Success", Environment.GetEnvironmentVariable(Variable)}
                            };

                            await args.Request.SendResponseAsync(Value);

                            break;
                        }
                    case "Excute_Rename":
                        {
                            string ExcutePath = Convert.ToString(args.Request.Message["ExcutePath"]);
                            string DesireName = Convert.ToString(args.Request.Message["DesireName"]);

                            ValueSet Value = new ValueSet();

                            if (File.Exists(ExcutePath))
                            {
                                if (StorageItemController.CheckOccupied(ExcutePath))
                                {
                                    Value.Add("Error_Occupied", "FileLoadException");
                                }
                                else
                                {
                                    if (StorageItemController.CheckWritePermission(Path.GetDirectoryName(ExcutePath)))
                                    {
                                        if (StorageItemController.Rename(ExcutePath, DesireName))
                                        {
                                            Value.Add("Success", string.Empty);
                                        }
                                        else
                                        {
                                            Value.Add("Error", "Error happened when rename");
                                        }
                                    }
                                    else
                                    {
                                        Value.Add("Error_Failure", "Error happened when rename");
                                    }
                                }
                            }
                            else
                            {
                                Value.Add("Error_NotFound", "FileNotFoundException");
                            }

                            await args.Request.SendResponseAsync(Value);

                            break;
                        }
                    case "Excute_GetHyperlinkInfo":
                        {
                            string ExcutePath = Convert.ToString(args.Request.Message["ExcutePath"]);

                            ValueSet Value = new ValueSet();

                            if (File.Exists(ExcutePath))
                            {
                                try
                                {
                                    using (ShellLink Link = new ShellLink(ExcutePath))
                                    {
                                        Value.Add("Success", string.Empty);
                                        Value.Add("TargetPath", Link.TargetPath);
                                        Value.Add("Argument", Link.Arguments);
                                        Value.Add("RunAs", Link.RunAsAdministrator);
                                        Value.Add("IsFile", File.Exists(Link.TargetPath));
                                    }
                                }
                                catch (Exception e)
                                {
                                    Value.Add("Error", e.Message);
                                }
                            }
                            else
                            {
                                Value.Add("Error", "File is not exist");
                            }

                            await args.Request.SendResponseAsync(Value);

                            break;
                        }
                    case "Excute_Intercept_Win_E":
                        {
                            ValueSet Value = new ValueSet();

                            try
                            {
                                string[] EnvironmentVariables = Environment.GetEnvironmentVariable("Path", EnvironmentVariableTarget.User).Split(new string[] { ";" }, StringSplitOptions.RemoveEmptyEntries);

                                if (EnvironmentVariables.Where((Var) => Var.Contains("WindowsApps")).Select((Var) => Path.Combine(Var, "RX-Explorer.exe")).FirstOrDefault((Path) => File.Exists(Path)) is string AliasLocation)
                                {
                                    StorageFile InterceptFile = await StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///Assets/Intercept_WIN_E.reg"));
                                    StorageFile TempFile = await ApplicationData.Current.TemporaryFolder.CreateFileAsync("Intercept_WIN_E_Temp.reg", CreationCollisionOption.ReplaceExisting);

                                    using (Stream FileStream = await InterceptFile.OpenStreamForReadAsync().ConfigureAwait(true))
                                    using (StreamReader Reader = new StreamReader(FileStream))
                                    {
                                        string Content = await Reader.ReadToEndAsync().ConfigureAwait(true);

                                        using (Stream TempStream = await TempFile.OpenStreamForWriteAsync())
                                        using (StreamWriter Writer = new StreamWriter(TempStream))
                                        {
                                            await Writer.WriteAsync(Content.Replace("<FillActualAliasPathInHere>", AliasLocation.Replace(@"\", @"\\")));
                                        }
                                    }

                                    using (Process Process = Process.Start(TempFile.Path))
                                    {
                                        User32.SetWindowPos(Process.MainWindowHandle, new IntPtr(-1), 0, 0, 0, 0, User32.SetWindowPosFlags.SWP_NOSIZE | User32.SetWindowPosFlags.SWP_NOMOVE | User32.SetWindowPosFlags.SWP_SHOWWINDOW);
                                        Process.WaitForExit();
                                    }

                                    Value.Add("Success", string.Empty);
                                }
                                else
                                {
                                    Value.Add("Error", "Alias file is not exists");
                                }
                            }
                            catch (Exception e)
                            {
                                Value.Add("Error", e.Message);
                            }

                            await args.Request.SendResponseAsync(Value);

                            break;
                        }
                    case "Excute_Restore_Win_E":
                        {
                            ValueSet Value = new ValueSet();

                            try
                            {
                                StorageFile RestoreFile = await StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///Assets/Restore_WIN_E.reg"));

                                using (Process Process = Process.Start(RestoreFile.Path))
                                {
                                    User32.SetWindowPos(Process.MainWindowHandle, new IntPtr(-1), 0, 0, 0, 0, User32.SetWindowPosFlags.SWP_NOSIZE | User32.SetWindowPosFlags.SWP_NOMOVE | User32.SetWindowPosFlags.SWP_SHOWWINDOW);
                                    Process.WaitForExit();
                                }

                                Value.Add("Success", string.Empty);
                            }
                            catch (Exception e)
                            {
                                Value.Add("Error", e.Message);
                            }

                            await args.Request.SendResponseAsync(Value);

                            break;
                        }
                    case "Excute_RemoveHiddenAttribute":
                        {
                            ValueSet Value = new ValueSet();

                            string ExcutePath = Convert.ToString(args.Request.Message["ExcutePath"]);

                            try
                            {
                                if (File.Exists(ExcutePath))
                                {
                                    File.SetAttributes(ExcutePath, File.GetAttributes(ExcutePath) & ~FileAttributes.Hidden);
                                }
                                else if (Directory.Exists(ExcutePath))
                                {
                                    DirectoryInfo Info = new DirectoryInfo(ExcutePath);
                                    Info.Attributes &= ~FileAttributes.Hidden;
                                }

                                Value.Add("Success", string.Empty);
                            }
                            catch (Exception e)
                            {
                                Value.Add("Error_RemoveAttributeFailure", e.Message);
                            }

                            await args.Request.SendResponseAsync(Value);

                            break;
                        }
                    case "Excute_RequestCreateNewPipe":
                        {
                            string Guid = Convert.ToString(args.Request.Message["Guid"]);

                            if (!PipeServers.ContainsKey(Guid))
                            {
                                NamedPipeServerStream NewPipeServer = new NamedPipeServerStream($@"Explorer_And_FullTrustProcess_NamedPipe-{Guid}", PipeDirection.InOut, NamedPipeServerStream.MaxAllowedServerInstances, PipeTransmissionMode.Byte, PipeOptions.Asynchronous, 2048, 2048, null, HandleInheritability.None, PipeAccessRights.ChangePermissions);

                                PipeSecurity Security = NewPipeServer.GetAccessControl();
                                PipeAccessRule ClientRule = new PipeAccessRule(new SecurityIdentifier("S-1-15-2-1"), PipeAccessRights.ReadWrite | PipeAccessRights.CreateNewInstance, AccessControlType.Allow);
                                PipeAccessRule OwnerRule = new PipeAccessRule(WindowsIdentity.GetCurrent().Owner, PipeAccessRights.FullControl, AccessControlType.Allow);
                                Security.AddAccessRule(ClientRule);
                                Security.AddAccessRule(OwnerRule);
                                NewPipeServer.SetAccessControl(Security);

                                PipeServers.Add(Guid, NewPipeServer);

                                _ = NewPipeServer.WaitForConnectionAsync(new CancellationTokenSource(3000).Token).ContinueWith((task) =>
                                {
                                    if (PipeServers.TryGetValue(Guid, out NamedPipeServerStream Pipe))
                                    {
                                        Pipe.Dispose();
                                        PipeServers.Remove(Guid);
                                    }
                                }, TaskContinuationOptions.OnlyOnCanceled);
                            }

                            break;
                        }
                    case "Identity":
                        {
                            ValueSet Value = new ValueSet
                            {
                                { "Identity", "FullTrustProcess" }
                            };

                            if (ExplorerProcess != null)
                            {
                                Value.Add("PreviousExplorerId", ExplorerProcess.Id);
                            }

                            await args.Request.SendResponseAsync(Value);
                            break;
                        }
                    case "Excute_Quicklook":
                        {
                            string ExcutePath = Convert.ToString(args.Request.Message["ExcutePath"]);

                            if (!string.IsNullOrEmpty(ExcutePath))
                            {
                                QuicklookConnector.SendMessageToQuicklook(ExcutePath);
                            }

                            break;
                        }
                    case "Excute_Check_QuicklookIsAvaliable":
                        {
                            bool IsSuccess = QuicklookConnector.CheckQuicklookIsAvaliable();

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
                                if (StorageItemController.CheckWritePermission(DestinationPath))
                                {
                                    if (StorageItemController.Copy(SourcePathList, DestinationPath, (s, e) =>
                                    {
                                        lock (Locker)
                                        {
                                            try
                                            {
                                                if (PipeServers.TryGetValue(Guid, out NamedPipeServerStream Pipeline))
                                                {
                                                    using (StreamWriter Writer = new StreamWriter(Pipeline, new UTF8Encoding(false), 1024, true))
                                                    {
                                                        Writer.WriteLine(e.ProgressPercentage);
                                                    }
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
                                        Value.Add("Error", "An error occurred while copying the folder");
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
                                try
                                {
                                    if (PipeServers.TryGetValue(Guid, out NamedPipeServerStream Pipeline))
                                    {
                                        using (StreamWriter Writer = new StreamWriter(Pipeline, new UTF8Encoding(false), 1024, true))
                                        {
                                            Writer.WriteLine("Error_Stop_Signal");
                                        }
                                    }
                                }
                                catch
                                {
                                    Debug.WriteLine("无法传输进度数据");
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
                                    if (StorageItemController.CheckWritePermission(DestinationPath))
                                    {
                                        if (StorageItemController.Move(SourcePathList, DestinationPath, (s, e) =>
                                        {
                                            lock (Locker)
                                            {
                                                try
                                                {
                                                    if (PipeServers.TryGetValue(Guid, out NamedPipeServerStream Pipeline))
                                                    {
                                                        using (StreamWriter Writer = new StreamWriter(Pipeline, new UTF8Encoding(false), 1024, true))
                                                        {
                                                            Writer.WriteLine(e.ProgressPercentage);
                                                        }
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
                                            Value.Add("Error", "An error occurred while moving the folder");
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
                                try
                                {
                                    if (PipeServers.TryGetValue(Guid, out NamedPipeServerStream Pipeline))
                                    {
                                        using (StreamWriter Writer = new StreamWriter(Pipeline, new UTF8Encoding(false), 1024, true))
                                        {
                                            Writer.WriteLine("Error_Stop_Signal");
                                        }
                                    }
                                }
                                catch
                                {
                                    Debug.WriteLine("无法传输进度数据");
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
                                        ExcutePathList.Where((Path) => File.Exists(Path)).ToList().ForEach((Item) =>
                                        {
                                            File.SetAttributes(Item, FileAttributes.Normal);
                                        });

                                        ExcutePathList.Where((Path) => Directory.Exists(Path)).ToList().ForEach((Item) =>
                                        {
                                            DirectoryInfo Info = new DirectoryInfo(Item);
                                            Info.Attributes &= ~FileAttributes.ReadOnly;
                                        });

                                        if (ExcutePathList.Where((Item) => Directory.Exists(Item)).All((Item) => StorageItemController.CheckWritePermission(Item)) && ExcutePathList.Where((Item) => File.Exists(Item)).All((Item) => StorageItemController.CheckWritePermission(Path.GetDirectoryName(Item))))
                                        {
                                            if (StorageItemController.Delete(ExcutePathList, PermanentDelete, (s, e) =>
                                            {
                                                lock (Locker)
                                                {
                                                    try
                                                    {
                                                        if (PipeServers.TryGetValue(Guid, out NamedPipeServerStream Pipeline))
                                                        {
                                                            using (StreamWriter Writer = new StreamWriter(Pipeline, new UTF8Encoding(false), 1024, true))
                                                            {
                                                                Writer.WriteLine(e.ProgressPercentage);
                                                            }
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
                                                Value.Add("Error", "The specified file could not be deleted");
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
                                try
                                {
                                    if (PipeServers.TryGetValue(Guid, out NamedPipeServerStream Pipeline))
                                    {
                                        using (StreamWriter Writer = new StreamWriter(Pipeline, new UTF8Encoding(false), 1024, true))
                                        {
                                            Writer.WriteLine("Error_Stop_Signal");
                                        }
                                    }
                                }
                                catch
                                {
                                    Debug.WriteLine("无法传输进度终止数据");
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
                                    using (Process Process = new Process())
                                    {
                                        Process.StartInfo.FileName = ExcutePath;

                                        if (ExcuteAuthority == "Administrator")
                                        {
                                            Process.StartInfo.Verb = "runAs";
                                        }

                                        Process.Start();

                                        User32.SetWindowPos(Process.MainWindowHandle, new IntPtr(-1), 0, 0, 0, 0, User32.SetWindowPosFlags.SWP_NOSIZE | User32.SetWindowPosFlags.SWP_NOMOVE | User32.SetWindowPosFlags.SWP_SHOWWINDOW);
                                    }
                                }
                                else
                                {
                                    using (Process Process = new Process())
                                    {
                                        Process.StartInfo.FileName = ExcutePath;
                                        Process.StartInfo.Arguments = ExcuteParameter;

                                        if (ExcuteAuthority == "Administrator")
                                        {
                                            Process.StartInfo.Verb = "runAs";
                                        }

                                        Process.Start();

                                        User32.SetWindowPos(Process.MainWindowHandle, new IntPtr(-1), 0, 0, 0, 0, User32.SetWindowPosFlags.SWP_NOSIZE | User32.SetWindowPosFlags.SWP_NOMOVE | User32.SetWindowPosFlags.SWP_SHOWWINDOW);
                                    }
                                }
                            }

                            break;
                        }
                    case "Excute_Test_Connection":
                        {
                            try
                            {
                                if (args.Request.Message.TryGetValue("ProcessId", out object Obj) && Obj is int Id && ExplorerProcess?.Id != Id)
                                {
                                    ExplorerProcess?.Dispose();
                                    ExplorerProcess = Process.GetProcessById(Id);
                                }
                            }
                            catch
                            {
                                Debug.WriteLine("GetProcess from id and register Exit event failed");
                            }

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

        private static void AliveCheck(object state)
        {
            foreach (var Pair in PipeServers.Where(Pair => !Pair.Value.IsConnected).ToList())
            {
                Pair.Value.Disconnect();
                Pair.Value.Dispose();

                PipeServers.Remove(Pair.Key);
            }

            if (ExplorerProcess != null && ExplorerProcess.HasExited)
            {
                ExitLocker.Set();
            }
        }
    }
}
