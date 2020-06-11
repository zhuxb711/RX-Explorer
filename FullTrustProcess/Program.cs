using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.ApplicationModel.AppService;
using Windows.Foundation.Collections;

namespace FullTrustProcess
{
    class Program
    {
        private static readonly AppServiceConnection Connection = new AppServiceConnection
        {
            AppServiceName = "CommunicateService",
            PackageFamilyName = Package.Current.Id.FamilyName
        };

        private static readonly HashSet<string> SpecialStringMap = new HashSet<string>(2)
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32\\WindowsPowerShell\\v1.0\\powershell.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32\\cmd.exe")
        };

        private readonly static ManualResetEvent ExitLocker = new ManualResetEvent(false);

        static async Task Main(string[] args)
        {
            if (await Connection.OpenAsync() == AppServiceConnectionStatus.Success)
            {
                Connection.RequestReceived += Connection_RequestReceived;
                ExitLocker.WaitOne();
            }

            Connection.Dispose();
            ExitLocker.Dispose();

            Environment.Exit(0);
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
                            ValueSet Result = new ValueSet
                            {
                                {"RecycleBinItems_Json_Result" , RecycleBinController.GenerateRecycleItemsByJson()}
                            };
                            await args.Request.SendResponseAsync(Result);
                            break;
                        }
                    case "Excute_Empty_RecycleBin":
                        {
                            RecycleBinController.EmptyRecycleBin();
                            break;
                        }
                    case "Excute_Copy":
                        {
                            ValueSet Value = new ValueSet();

                            string SourcePath = Convert.ToString(args.Request.Message["SourcePath"]);
                            string DestinationPath = Convert.ToString(args.Request.Message["DestinationPath"]);

                            if (Directory.Exists(SourcePath))
                            {
                                if (StorageItemOperator.CopyFolder(SourcePath, DestinationPath))
                                {
                                    Value.Add("Success", string.Empty);
                                }
                                else
                                {
                                    Value.Add("Error_Failure", "An error occurred while copying the folder");
                                }
                            }
                            else if(File.Exists(SourcePath))
                            {
                                if (StorageItemOperator.CopyFile(SourcePath, DestinationPath))
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
                                Value.Add("Error_NoExist", "SourcePath is not a file or directory");
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
                                if (StorageItemOperator.MoveFolder(SourcePath, DestinationPath))
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
                                if (StorageItemOperator.MoveFile(SourcePath, DestinationPath))
                                {
                                    Value.Add("Success", string.Empty);
                                }
                                else
                                {
                                    Value.Add("Error_Failure", "An error occurred while moving the file");
                                }
                            }
                            else
                            {
                                Value.Add("Error_NoExist", "SourcePath is not a file or directory");
                            }

                            await args.Request.SendResponseAsync(Value);

                            break;
                        }
                    case "Excute_Delete":
                        {
                            ValueSet Value = new ValueSet();

                            string ExcutePath = Convert.ToString(args.Request.Message["ExcutePath"]);

                            try
                            {
                                if (File.Exists(ExcutePath))
                                {
                                    File.SetAttributes(ExcutePath, FileAttributes.Normal);

                                    if (StorageItemOperator.TryUnoccupied(ExcutePath))
                                    {
                                        File.Delete(ExcutePath);

                                        Value.Add("Success", string.Empty);
                                    }
                                    else
                                    {
                                        Value.Add("Error_Failure", "The specified file or folder could not be deleted");
                                    }
                                }
                                else if (Directory.Exists(ExcutePath))
                                {
                                    DirectoryInfo Info = new DirectoryInfo(ExcutePath)
                                    {
                                        Attributes = FileAttributes.Normal & FileAttributes.Directory
                                    };

                                    Info.Delete(true);

                                    Value.Add("Success", string.Empty);
                                }
                                else
                                {
                                    Value.Add("Error_NoExist", "ExcutePath is not a file or directory");
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
                                        ProcessStartInfo Info = new ProcessStartInfo(ExcutePath) { Verb = "runAs" };
                                        Process.Start(Info).Dispose();
                                    }
                                    else
                                    {
                                        Process.Start(ExcutePath).Dispose();
                                    }
                                }
                                else
                                {
                                    if (SpecialStringMap.Contains(ExcutePath))
                                    {
                                        if (ExcuteAuthority == "Administrator")
                                        {
                                            ProcessStartInfo Info = new ProcessStartInfo(ExcutePath, ExcuteParameter) { Verb = "runAs" };
                                            Process.Start(Info).Dispose();
                                        }
                                        else
                                        {
                                            Process.Start(ExcutePath, ExcuteParameter).Dispose();
                                        }
                                    }
                                    else
                                    {
                                        if (ExcuteAuthority == "Administrator")
                                        {
                                            ProcessStartInfo Info = new ProcessStartInfo(ExcutePath, $"\"{ExcuteParameter}\"") { Verb = "runAs" };
                                            Process.Start(Info).Dispose();
                                        }
                                        else
                                        {
                                            Process.Start(ExcutePath, $"\"{ExcuteParameter}\"").Dispose();
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
