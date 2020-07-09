using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.AppService;
using Windows.Foundation.Collections;
using Windows.System;

namespace FullTrustProcess
{
    class Program
    {
        private static AppServiceConnection Connection;

        private static readonly HashSet<string> SpecialStringMap = new HashSet<string>()
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "WindowsPowerShell\\v1.0\\powershell.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.SystemX86), "cmd.exe"),
            "wt.exe"
        };

        private readonly static ManualResetEvent ExitLocker = new ManualResetEvent(false);

        [STAThread]
        static async Task Main(string[] args)
        {
            try
            {
                using (Mutex LaunchLocker = new Mutex(true, "RX_Explorer_FullTrustProcess", out bool IsNotExist))
                {
                    if(!IsNotExist)
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
            catch(Exception e)
            {
                Debug.WriteLine($"FullTrustProcess出现异常，错误信息{e.Message}");
            }
            finally
            {
                Connection?.Dispose();
                ExitLocker?.Dispose();

                Environment.Exit(0);
            }
        }

        private async static void Connection_RequestReceived(AppServiceConnection sender, AppServiceRequestReceivedEventArgs args)
        {
            var Deferral = args.GetDeferral();

            try
            {
                switch (args.Request.Message["ExcuteType"])
                {
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

                            List<KeyValuePair<string, string>> SourcePathList = JsonConvert.DeserializeObject<List<KeyValuePair<string, string>>>(SourcePathJson);

                            if (SourcePathList.All((Item) => Directory.Exists(Item.Key) || File.Exists(Item.Key)))
                            {
                                if (StorageItemController.Copy(SourcePathList, DestinationPath))
                                {
                                    Value.Add("Success", string.Empty);
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

                            await args.Request.SendResponseAsync(Value);
                            break;
                        }
                    case "Excute_Move":
                        {
                            ValueSet Value = new ValueSet();

                            string SourcePathJson = Convert.ToString(args.Request.Message["SourcePath"]);
                            string DestinationPath = Convert.ToString(args.Request.Message["DestinationPath"]);

                            List<KeyValuePair<string, string>> SourcePathList = JsonConvert.DeserializeObject<List<KeyValuePair<string, string>>>(SourcePathJson);

                            if (SourcePathList.All((Item) => Directory.Exists(Item.Key) || File.Exists(Item.Key)))
                            {
                                if (SourcePathList.Where((Path) => File.Exists(Path.Key)).Any((Item) => StorageItemController.CheckOccupied(Item.Key)))
                                {
                                    Value.Add("Error_Capture", "An error occurred while moving the folder");
                                }
                                else
                                {
                                    if (StorageItemController.Move(SourcePathList, DestinationPath))
                                    {
                                        Value.Add("Success", string.Empty);
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

                            await args.Request.SendResponseAsync(Value);
                            break;
                        }
                    case "Excute_Delete":
                        {
                            ValueSet Value = new ValueSet();

                            string ExcutePathJson = Convert.ToString(args.Request.Message["ExcutePath"]);
                            bool PermanentDelete = Convert.ToBoolean(args.Request.Message["PermanentDelete"]);

                            List<string> ExcutePathList = JsonConvert.DeserializeObject<List<string>>(ExcutePathJson);

                            try
                            {
                                if (ExcutePathList.All((Item) => Directory.Exists(Item) || File.Exists(Item)))
                                {
                                    if (ExcutePathList.Where((Path) => File.Exists(Path)).Any((Item) => StorageItemController.CheckOccupied(Item)))
                                    {
                                        Value.Add("Error_Capture", "An error occurred while moving the folder");
                                    }
                                    else
                                    {
                                        ExcutePathList.Where((Path) => File.Exists(Path)).ToList().ForEach((Item) => File.SetAttributes(Item, FileAttributes.Normal));
                                        ExcutePathList.Where((Path) => Directory.Exists(Path)).ToList().ForEach((Item) => _ = new DirectoryInfo(Item)
                                        {
                                            Attributes = FileAttributes.Normal & FileAttributes.Directory
                                        });

                                        if (StorageItemController.Delete(ExcutePathList, PermanentDelete))
                                        {
                                            Value.Add("Success", string.Empty);
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
