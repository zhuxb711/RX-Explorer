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
            ExitExistInstance();

            try
            {
                Connection = new AppServiceConnection
                {
                    AppServiceName = "CommunicateService",
                    PackageFamilyName = "36186RuoFan.USB_q3e6crc0w375t"
                };

                if (await Connection.OpenAsync() == AppServiceConnectionStatus.Success)
                {
                    Connection.RequestReceived += Connection_RequestReceived;
                    ExitLocker.WaitOne();
                }
            }
            catch
            {

            }
            finally
            {
                Connection.Dispose();
                ExitLocker.Dispose();

                Environment.Exit(0);
            }
        }

        public static void ExitExistInstance()
        {
            Process Current = Process.GetCurrentProcess();

            Process[] AllProcess = Process.GetProcessesByName(Current.ProcessName);

            foreach (Process ExistProcess in AllProcess.Where(Process => Process.Id != Current.Id && Assembly.GetExecutingAssembly().Location.Replace("/", @"\") == Current.MainModule.FileName))
            {
                ExistProcess.Kill();
            }
        }

        private async static void Connection_RequestReceived(AppServiceConnection sender, AppServiceRequestReceivedEventArgs args)
        {
            var Deferral = args.GetDeferral();

            try
            {
                switch (args.Request.Message["ExcuteType"])
                {
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
                            string Path = Convert.ToString(args.Request.Message["AssociatePath"]);
                            string Associate = ExtensionAssociate.GetAssociate(Path);

                            ValueSet Result = new ValueSet
                            {
                                {"Associate_Result",string.IsNullOrEmpty(Associate)?"<Empty>":Associate }
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
                            Debugger.Launch();

                            string Path = Convert.ToString(args.Request.Message["ExcutePath"]);
                            
                            ValueSet Result = new ValueSet
                            {
                                {"Restore_Result", RecycleBinController.Restore(Path) }
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

                            string SourcePath = Convert.ToString(args.Request.Message["SourcePath"]);
                            string DestinationPath = Convert.ToString(args.Request.Message["DestinationPath"]);

                            if (Directory.Exists(SourcePath))
                            {
                                if (StorageItemController.Copy(SourcePath, DestinationPath, args.Request.Message.ContainsKey("NewName") ? Convert.ToString(args.Request.Message["NewName"]) : null))
                                {
                                    Value.Add("Success", string.Empty);
                                }
                                else
                                {
                                    Value.Add("Error_Failure", "An error occurred while copying the folder");
                                }
                            }
                            else if (File.Exists(SourcePath))
                            {
                                if (StorageItemController.Copy(SourcePath, DestinationPath))
                                {
                                    Value.Add("Success", string.Empty);
                                }
                                else
                                {
                                    Value.Add("Error_Failure", "An error occurred while copying the file");
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

                            string SourcePath = Convert.ToString(args.Request.Message["SourcePath"]);
                            string DestinationPath = Convert.ToString(args.Request.Message["DestinationPath"]);

                            if (Directory.Exists(SourcePath))
                            {
                                if (StorageItemController.Move(SourcePath, DestinationPath, args.Request.Message.ContainsKey("NewName") ? Convert.ToString(args.Request.Message["NewName"]) : null))
                                {
                                    Value.Add("Success", string.Empty);
                                }
                                else
                                {
                                    Value.Add("Error_Failure", "An error occurred while moving the folder");
                                }
                            }
                            else if (File.Exists(SourcePath))
                            {
                                if (StorageItemController.CheckOccupied(SourcePath))
                                {
                                    Value.Add("Error_Capture", "An error occurred while moving the folder");
                                }
                                else
                                {
                                    if (StorageItemController.Move(SourcePath, DestinationPath))
                                    {
                                        Value.Add("Success", string.Empty);
                                    }
                                    else
                                    {
                                        Value.Add("Error_Failure", "An error occurred while moving the file");
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

                            string ExcutePath = Convert.ToString(args.Request.Message["ExcutePath"]);
                            bool PermanentDelete = Convert.ToBoolean(args.Request.Message["PermanentDelete"]);

                            try
                            {
                                if (File.Exists(ExcutePath))
                                {
                                    if (StorageItemController.CheckOccupied(ExcutePath))
                                    {
                                        Value.Add("Error_Capture", "The specified file is captured");
                                    }
                                    else
                                    {
                                        File.SetAttributes(ExcutePath, FileAttributes.Normal);

                                        if (StorageItemController.Delete(ExcutePath, PermanentDelete))
                                        {
                                            Value.Add("Success", string.Empty);
                                        }
                                        else
                                        {
                                            Value.Add("Error_Failure", "The specified file could not be deleted");
                                        }
                                    }
                                }
                                else if (Directory.Exists(ExcutePath))
                                {
                                    DirectoryInfo Info = new DirectoryInfo(ExcutePath)
                                    {
                                        Attributes = FileAttributes.Normal & FileAttributes.Directory
                                    };

                                    if (StorageItemController.Delete(ExcutePath, PermanentDelete))
                                    {
                                        Value.Add("Success", string.Empty);
                                    }
                                    else
                                    {
                                        Value.Add("Error_Failure", "The specified folder could not be deleted");
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
