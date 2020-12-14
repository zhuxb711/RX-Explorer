using Newtonsoft.Json;
using ShareClassLibrary;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;
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
                if (args.Contains("Elevation_Restart") && int.TryParse(args.LastOrDefault(), out int Id))
                {
                    ExplorerProcess = Process.GetProcessById(Id);
                }

                Connection = new AppServiceConnection
                {
                    AppServiceName = "CommunicateService",
                    PackageFamilyName = "36186RuoFan.USB_q3e6crc0w375t"
                };
                Connection.RequestReceived += Connection_RequestReceived;
                Connection.ServiceClosed += Connection_ServiceClosed;

                if (await Connection.OpenAsync() == AppServiceConnectionStatus.Success)
                {
                    AliveCheckTimer = new Timer(AliveCheck, null, 10000, 5000);
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

                ExplorerProcess?.Dispose();

                try
                {
                    PipeServers.Values.ToList().ForEach((Item) =>
                    {
                        Item.Dispose();
                    });
                }
                catch
                {
                    Debug.WriteLine("Error when dispose PipeLine");
                }

                PipeServers.Clear();

                Environment.Exit(0);
            }
        }

        private static void Connection_ServiceClosed(AppServiceConnection sender, AppServiceClosedEventArgs args)
        {
            ExitLocker.Set();
        }

        private async static void Connection_RequestReceived(AppServiceConnection sender, AppServiceRequestReceivedEventArgs args)
        {
            AppServiceDeferral Deferral = args.GetDeferral();

            try
            {
                switch (args.Request.Message["ExecuteType"])
                {
                    case "Execute_GetMIMEContentType":
                        {
                            string ExecutePath = Convert.ToString(args.Request.Message["ExecutePath"]);

                            ValueSet Value = new ValueSet
                            {
                                { "Success", MIMEHelper.GetMIMEFromPath(ExecutePath)}
                            };

                            await args.Request.SendResponseAsync(Value);

                            break;
                        }
                    case "Execute_GetHiddenItemInfo":
                        {
                            string ExecutePath = Convert.ToString(args.Request.Message["ExecutePath"]);

                            using (ShellItem Item = ShellItem.Open(ExecutePath))
                            using (Image Thumbnail = Item.GetImage(new Size(128, 128), ShellItemGetImageOptions.BiggerSizeOk))
                            using (Bitmap OriginBitmap = new Bitmap(Thumbnail))
                            using (MemoryStream Stream = new MemoryStream())
                            {
                                OriginBitmap.MakeTransparent();
                                OriginBitmap.Save(Stream, ImageFormat.Png);

                                ValueSet Value = new ValueSet
                                {
                                    {"Success", JsonConvert.SerializeObject(new HiddenItemPackage(Item.FileInfo.TypeName, Stream.ToArray()))}
                                };

                                await args.Request.SendResponseAsync(Value);
                            }

                            break;
                        }
                    case "Execute_CheckIfEverythingAvailable":
                        {
                            await args.Request.SendResponseAsync(new ValueSet
                            {
                                {"Success", EverythingConnector.Current.IsAvailable }
                            });

                            break;
                        }
                    case "Execute_SearchByEverything":
                        {
                            string BaseLocation = Convert.ToString(args.Request.Message["BaseLocation"]);
                            string SearchWord = Convert.ToString(args.Request.Message["SearchWord"]);
                            bool SearchAsRegex = Convert.ToBoolean(args.Request.Message["SearchAsRegex"]);
                            bool IgnoreCase = Convert.ToBoolean(args.Request.Message["IgnoreCase"]);
                            uint MaxCount = Convert.ToUInt32(args.Request.Message["MaxCount"]);

                            ValueSet Value = new ValueSet();

                            if (EverythingConnector.Current.IsAvailable)
                            {
                                IEnumerable<string> SearchResult = EverythingConnector.Current.Search(BaseLocation, SearchWord, SearchAsRegex, IgnoreCase, MaxCount);

                                if (SearchResult.Any())
                                {
                                    Value.Add("Success", JsonConvert.SerializeObject(SearchResult));
                                }
                                else
                                {
                                    EverythingConnector.StateCode Code = EverythingConnector.Current.GetLastErrorCode();

                                    if (Code == EverythingConnector.StateCode.OK)
                                    {
                                        Value.Add("Success", JsonConvert.SerializeObject(SearchResult));
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

                            await args.Request.SendResponseAsync(Value);

                            break;
                        }
                    case "Execute_GetContextMenuItems":
                        {
                            string ExecutePath = Convert.ToString(args.Request.Message["ExecutePath"]);
                            bool IncludeExtensionItem = Convert.ToBoolean(args.Request.Message["IncludeExtensionItem"]);

                            List<ContextMenuPackage> ContextMenuItems = ContextMenu.FetchContextMenuItems(ExecutePath, IncludeExtensionItem);

                            ValueSet Value = new ValueSet
                            {
                                {"Success", JsonConvert.SerializeObject(ContextMenuItems) }
                            };

                            await args.Request.SendResponseAsync(Value);

                            break;
                        }
                    case "Execute_InvokeContextMenuItem":
                        {
                            string Verb = Convert.ToString(args.Request.Message["InvokeVerb"]);
                            string Path = Convert.ToString(args.Request.Message["ExecutePath"]);

                            ValueSet Value = new ValueSet();

                            if (!string.IsNullOrWhiteSpace(Verb) && !string.IsNullOrWhiteSpace(Path))
                            {
                                if (ContextMenu.InvokeVerb(Path, Verb))
                                {
                                    Value.Add("Success", string.Empty);
                                }
                                else
                                {
                                    Value.Add("Error", $"Execute Verb: {Verb} failed");
                                }
                            }
                            else
                            {
                                Value.Add("Error", "Verb is empty or Paths is empty");
                            }

                            await args.Request.SendResponseAsync(Value);

                            break;
                        }
                    case "Execute_ElevateAsAdmin":
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
                    case "Execute_CreateLink":
                        {
                            HyperlinkPackage Package = JsonConvert.DeserializeObject<HyperlinkPackage>(Convert.ToString(args.Request.Message["DataPackage"]));

                            string Argument = string.Join(" ", Package.Argument.Select((Para) => (Para.Contains(" ") && !Para.StartsWith("\"") && !Para.EndsWith("\"")) ? $"\"{Para}\"" : Para).ToArray());

                            ShellLink.Create(Package.LinkPath, Package.LinkTargetPath, description: Package.Description, arguments: Argument).Dispose();

                            ValueSet Value = new ValueSet
                            {
                                { "Success", string.Empty }
                            };

                            await args.Request.SendResponseAsync(Value);

                            break;
                        }
                    case "Execute_GetVariable_Path":
                        {
                            ValueSet Value = new ValueSet();

                            string Variable = Convert.ToString(args.Request.Message["Variable"]);

                            string Env = Environment.GetEnvironmentVariable(Variable);

                            if (string.IsNullOrEmpty(Env))
                            {
                                Value.Add("Error", "Could not found EnvironmentVariable");
                            }
                            else
                            {
                                Value.Add("Success", Env);
                            }

                            await args.Request.SendResponseAsync(Value);

                            break;
                        }
                    case "Execute_Rename":
                        {
                            string ExecutePath = Convert.ToString(args.Request.Message["ExecutePath"]);
                            string DesireName = Convert.ToString(args.Request.Message["DesireName"]);

                            ValueSet Value = new ValueSet();

                            if (File.Exists(ExecutePath) || Directory.Exists(ExecutePath))
                            {
                                if (StorageItemController.CheckOccupied(ExecutePath))
                                {
                                    Value.Add("Error_Occupied", "FileLoadException");
                                }
                                else
                                {
                                    if (StorageItemController.CheckPermission(FileSystemRights.Modify, Path.GetDirectoryName(ExecutePath)))
                                    {
                                        if (StorageItemController.Rename(ExecutePath, DesireName))
                                        {
                                            Value.Add("Success", string.Empty);
                                        }
                                        else
                                        {
                                            Value.Add("Error_Failure", "Error happened when rename");
                                        }
                                    }
                                    else
                                    {
                                        Value.Add("Error_Failure", "No Modify Permission");
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
                    case "Execute_GetHyperlinkInfo":
                        {
                            string ExecutePath = Convert.ToString(args.Request.Message["ExecutePath"]);

                            ValueSet Value = new ValueSet();

                            if (File.Exists(ExecutePath))
                            {
                                using (ShellLink Link = new ShellLink(ExecutePath))
                                {
                                    if (string.IsNullOrEmpty(Link.TargetPath))
                                    {
                                        Value.Add("Error", "TargetPath is invalid");
                                    }
                                    else
                                    {
                                        MatchCollection Collection = Regex.Matches(Link.Arguments, "[^ \"]+|\"[^\"]*\"");

                                        List<string> Arguments = new List<string>(Collection.Count);

                                        foreach (Match Mat in Collection)
                                        {
                                            Arguments.Add(Mat.Value);
                                        }

                                        Value.Add("Success", JsonConvert.SerializeObject(new HyperlinkPackage(ExecutePath, Link.TargetPath, Arguments.ToArray(), Link.Description, Link.RunAsAdministrator)));
                                    }
                                }
                            }
                            else
                            {
                                Value.Add("Error", "File is not exist");
                            }

                            await args.Request.SendResponseAsync(Value);

                            break;
                        }
                    case "Execute_Intercept_Win_E":
                        {
                            ValueSet Value = new ValueSet();

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
                                    using (StreamWriter Writer = new StreamWriter(TempStream, Encoding.Unicode))
                                    {
                                        await Writer.WriteAsync(Content.Replace("<FillActualAliasPathInHere>", $"{AliasLocation.Replace(@"\", @"\\")} %1"));
                                    }
                                }

                                using (Process Process = Process.Start(TempFile.Path))
                                {
                                    SetWindowsZPosition(Process);
                                    Process.WaitForExit();
                                }

                                Value.Add("Success", string.Empty);
                            }
                            else
                            {
                                Value.Add("Error", "Alias file is not exists");
                            }

                            await args.Request.SendResponseAsync(Value);

                            break;
                        }
                    case "Execute_Restore_Win_E":
                        {
                            StorageFile RestoreFile = await StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///Assets/Restore_WIN_E.reg"));

                            using (Process Process = Process.Start(RestoreFile.Path))
                            {
                                SetWindowsZPosition(Process);
                                Process.WaitForExit();
                            }

                            ValueSet Value = new ValueSet
                            {
                                { "Success", string.Empty }
                            };

                            await args.Request.SendResponseAsync(Value);

                            break;
                        }
                    case "Execute_RequestCreateNewPipe":
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
                    case "Execute_Quicklook":
                        {
                            string ExecutePath = Convert.ToString(args.Request.Message["ExecutePath"]);

                            if (!string.IsNullOrEmpty(ExecutePath))
                            {
                                QuicklookConnector.SendMessage(ExecutePath);
                            }

                            break;
                        }
                    case "Execute_Check_QuicklookIsAvaliable":
                        {
                            bool IsSuccess = QuicklookConnector.CheckQuicklookIsAvaliable();

                            ValueSet Result = new ValueSet
                            {
                                {"Check_QuicklookIsAvaliable_Result", IsSuccess }
                            };

                            await args.Request.SendResponseAsync(Result);

                            break;
                        }
                    case "Execute_Get_Associate":
                        {
                            string Path = Convert.ToString(args.Request.Message["ExecutePath"]);

                            ValueSet Result = new ValueSet
                            {
                                {"Associate_Result", JsonConvert.SerializeObject(ExtensionAssociate.GetAllAssociation(Path)) }
                            };

                            await args.Request.SendResponseAsync(Result);

                            break;
                        }
                    case "Execute_Get_RecycleBinItems":
                        {
                            ValueSet Result = new ValueSet();

                            string RecycleItemResult = RecycleBinController.GenerateRecycleItemsByJson();

                            if (string.IsNullOrEmpty(RecycleItemResult))
                            {
                                Result.Add("Error", "Could not get recycle items");
                            }
                            else
                            {
                                Result.Add("RecycleBinItems_Json_Result", RecycleItemResult);
                            }

                            await args.Request.SendResponseAsync(Result);

                            break;
                        }
                    case "Execute_Empty_RecycleBin":
                        {
                            ValueSet Result = new ValueSet
                            {
                                { "RecycleBinItems_Clear_Result", RecycleBinController.EmptyRecycleBin() }
                            };

                            await args.Request.SendResponseAsync(Result);

                            break;
                        }
                    case "Execute_Restore_RecycleItem":
                        {
                            string Path = Convert.ToString(args.Request.Message["ExecutePath"]);

                            ValueSet Result = new ValueSet
                            {
                                {"Restore_Result", RecycleBinController.Restore(Path) }
                            };

                            await args.Request.SendResponseAsync(Result);
                            break;
                        }
                    case "Execute_Delete_RecycleItem":
                        {
                            string Path = Convert.ToString(args.Request.Message["ExecutePath"]);

                            ValueSet Result = new ValueSet
                            {
                                {"Delete_Result", RecycleBinController.Delete(Path) }
                            };

                            await args.Request.SendResponseAsync(Result);
                            break;
                        }
                    case "Execute_EjectUSB":
                        {
                            ValueSet Value = new ValueSet();

                            string Path = Convert.ToString(args.Request.Message["ExecutePath"]);

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
                    case "Execute_Unlock_Occupy":
                        {
                            ValueSet Value = new ValueSet();

                            string Path = Convert.ToString(args.Request.Message["ExecutePath"]);

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
                    case "Execute_Copy":
                        {
                            ValueSet Value = new ValueSet();

                            string SourcePathJson = Convert.ToString(args.Request.Message["SourcePath"]);
                            string DestinationPath = Convert.ToString(args.Request.Message["DestinationPath"]);
                            string Guid = Convert.ToString(args.Request.Message["Guid"]);
                            bool IsUndo = Convert.ToBoolean(args.Request.Message["Undo"]);

                            List<KeyValuePair<string, string>> SourcePathList = JsonConvert.DeserializeObject<List<KeyValuePair<string, string>>>(SourcePathJson);
                            List<string> OperationRecordList = new List<string>();

                            int Progress = 0;

                            if (SourcePathList.All((Item) => Directory.Exists(Item.Key) || File.Exists(Item.Key)))
                            {
                                if (StorageItemController.CheckPermission(FileSystemRights.Modify, DestinationPath))
                                {
                                    if (StorageItemController.Copy(SourcePathList, DestinationPath, (s, e) =>
                                    {
                                        lock (Locker)
                                        {
                                            try
                                            {
                                                Progress = e.ProgressPercentage;

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
                                                Debug.WriteLine("Could not send progress data");
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
                                    Value.Add("Error_Failure", "An error occurred while copying the folder");
                                }
                            }
                            else
                            {
                                Value.Add("Error_NotFound", "SourcePath is not a file or directory");
                            }

                            if (Progress < 100)
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
                                    Debug.WriteLine("Could not send stop signal");
                                }
                            }

                            await args.Request.SendResponseAsync(Value);

                            break;
                        }
                    case "Execute_Move":
                        {
                            ValueSet Value = new ValueSet();

                            string SourcePathJson = Convert.ToString(args.Request.Message["SourcePath"]);
                            string DestinationPath = Convert.ToString(args.Request.Message["DestinationPath"]);
                            string Guid = Convert.ToString(args.Request.Message["Guid"]);
                            bool IsUndo = Convert.ToBoolean(args.Request.Message["Undo"]);

                            List<KeyValuePair<string, string>> SourcePathList = JsonConvert.DeserializeObject<List<KeyValuePair<string, string>>>(SourcePathJson);
                            List<string> OperationRecordList = new List<string>();

                            int Progress = 0;

                            if (SourcePathList.All((Item) => Directory.Exists(Item.Key) || File.Exists(Item.Key)))
                            {
                                if (SourcePathList.Any((Item) => StorageItemController.CheckOccupied(Item.Key)))
                                {
                                    Value.Add("Error_Capture", "An error occurred while moving the folder");
                                }
                                else
                                {
                                    if (StorageItemController.CheckPermission(FileSystemRights.Modify, DestinationPath))
                                    {
                                        if (StorageItemController.Move(SourcePathList, DestinationPath, (s, e) =>
                                        {
                                            lock (Locker)
                                            {
                                                try
                                                {
                                                    Progress = e.ProgressPercentage;

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
                                                    Debug.WriteLine("Could not send progress data");
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

                            if (Progress < 100)
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
                                    Debug.WriteLine("Could not send progress data");
                                }
                            }

                            await args.Request.SendResponseAsync(Value);
                            break;
                        }
                    case "Execute_Delete":
                        {
                            ValueSet Value = new ValueSet();

                            string ExecutePathJson = Convert.ToString(args.Request.Message["ExecutePath"]);
                            string Guid = Convert.ToString(args.Request.Message["Guid"]);
                            bool PermanentDelete = Convert.ToBoolean(args.Request.Message["PermanentDelete"]);
                            bool IsUndo = Convert.ToBoolean(args.Request.Message["Undo"]);

                            List<string> ExecutePathList = JsonConvert.DeserializeObject<List<string>>(ExecutePathJson);
                            List<string> OperationRecordList = new List<string>();

                            int Progress = 0;

                            try
                            {
                                if (ExecutePathList.All((Item) => Directory.Exists(Item) || File.Exists(Item)))
                                {
                                    if (ExecutePathList.Any((Item) => StorageItemController.CheckOccupied(Item)))
                                    {
                                        Value.Add("Error_Capture", "An error occurred while deleting the folder");
                                    }
                                    else
                                    {
                                        if (ExecutePathList.All((Path) => (Directory.Exists(Path) || File.Exists(Path)) && StorageItemController.CheckPermission(FileSystemRights.Modify, System.IO.Path.GetDirectoryName(Path))))
                                        {
                                            if (StorageItemController.Delete(ExecutePathList, PermanentDelete, (s, e) =>
                                            {
                                                lock (Locker)
                                                {
                                                    try
                                                    {
                                                        Progress = e.ProgressPercentage;

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
                                                        Debug.WriteLine("Could not send progress data");
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
                                        else
                                        {
                                            Value.Add("Error_Failure", "The specified file could not be deleted");
                                        }
                                    }
                                }
                                else
                                {
                                    Value.Add("Error_NotFound", "ExecutePath is not a file or directory");
                                }
                            }
                            catch
                            {
                                Value.Add("Error_Failure", "The specified file or folder could not be deleted");
                            }

                            if (Progress < 100)
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
                                    Debug.WriteLine("Could not send stop signal");
                                }
                            }

                            await args.Request.SendResponseAsync(Value);

                            break;
                        }
                    case "Execute_RunExe":
                        {
                            string ExecutePath = Convert.ToString(args.Request.Message["ExecutePath"]);
                            string ExecuteParameter = Convert.ToString(args.Request.Message["ExecuteParameter"]);
                            string ExecuteAuthority = Convert.ToString(args.Request.Message["ExecuteAuthority"]);

                            bool ExecuteCreateNoWindow = Convert.ToBoolean(args.Request.Message["ExecuteCreateNoWindow"]);
                            bool ShouldWaitForExit = Convert.ToBoolean(args.Request.Message["ExecuteShouldWaitForExit"]);

                            ValueSet Value = new ValueSet();

                            if (!string.IsNullOrEmpty(ExecutePath))
                            {
                                if (StorageItemController.CheckPermission(FileSystemRights.ReadAndExecute, ExecutePath))
                                {
                                    if (string.IsNullOrEmpty(ExecuteParameter))
                                    {
                                        using (Process Process = new Process())
                                        {
                                            Process.StartInfo.FileName = ExecutePath;
                                            Process.StartInfo.UseShellExecute = true;
                                            Process.StartInfo.WorkingDirectory = Path.GetDirectoryName(ExecutePath);

                                            if (ExecuteCreateNoWindow)
                                            {
                                                Process.StartInfo.CreateNoWindow = true;
                                                Process.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
                                            }

                                            if (ExecuteAuthority == "Administrator")
                                            {
                                                Process.StartInfo.Verb = "runAs";
                                            }

                                            Process.Start();

                                            SetWindowsZPosition(Process);

                                            if (ShouldWaitForExit)
                                            {
                                                Process.WaitForExit();
                                            }
                                        }
                                    }
                                    else
                                    {
                                        using (Process Process = new Process())
                                        {
                                            Process.StartInfo.FileName = ExecutePath;
                                            Process.StartInfo.Arguments = ExecuteParameter;
                                            Process.StartInfo.UseShellExecute = true;
                                            Process.StartInfo.WorkingDirectory = Path.GetDirectoryName(ExecutePath);

                                            if (ExecuteCreateNoWindow)
                                            {
                                                Process.StartInfo.CreateNoWindow = true;
                                                Process.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
                                            }

                                            if (ExecuteAuthority == "Administrator")
                                            {
                                                Process.StartInfo.Verb = "runAs";
                                            }

                                            Process.Start();

                                            SetWindowsZPosition(Process);

                                            if (ShouldWaitForExit)
                                            {
                                                Process.WaitForExit();
                                            }
                                        }
                                    }

                                    Value.Add("Success", string.Empty);
                                }
                                else
                                {
                                    Value.Add("Error_Failure", "The specified file could not be executed");
                                }
                            }
                            else
                            {
                                Value.Add("Success", string.Empty);
                            }

                            await args.Request.SendResponseAsync(Value);

                            break;
                        }
                    case "Execute_Test_Connection":
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

                            await args.Request.SendResponseAsync(new ValueSet { { "Execute_Test_Connection", string.Empty } });

                            break;
                        }
                    case "Execute_Exit":
                        {
                            ExitLocker.Set();
                            break;
                        }
                }
            }
            catch (Exception ex)
            {
                ValueSet Value = new ValueSet
                {
                    {"Error", ex.Message}
                };

                await args.Request.SendResponseAsync(Value);
            }
            finally
            {
                Deferral.Complete();
            }
        }

        private static void SetWindowsZPosition(Process OtherProcess)
        {
            try
            {
                OtherProcess.Refresh();
                User32.SetWindowPos(OtherProcess.MainWindowHandle, new IntPtr(-1), 0, 0, 0, 0, User32.SetWindowPosFlags.SWP_NOSIZE | User32.SetWindowPosFlags.SWP_NOMOVE | User32.SetWindowPosFlags.SWP_SHOWWINDOW);
            }
            catch (Exception e)
            {
                Debug.WriteLine($"Error: SetWindowsZPosition threw an error, message: {e.Message}");
            }
        }

        private static void AliveCheck(object state)
        {
            if (ExplorerProcess == null || ExplorerProcess.HasExited)
            {
                ExitLocker.Set();
            }
        }
    }
}
