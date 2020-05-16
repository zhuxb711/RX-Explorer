using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Windows.ApplicationModel.AppService;
using Windows.Foundation.Collections;

namespace FullTrustProcess
{
    class Program
    {
        static async Task Main(string[] args)
        {
            HashSet<string> SpecialStringMap = new HashSet<string>(2)
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32\\WindowsPowerShell\\v1.0\\powershell.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32\\cmd.exe")
            };

            try
            {
                using (AppServiceConnection Connection = new AppServiceConnection
                {
                    AppServiceName = "CommunicateService",
                    PackageFamilyName = "36186RuoFan.USB_q3e6crc0w375t"
                })
                {
                    if (await Connection.OpenAsync() == AppServiceConnectionStatus.Success)
                    {
                        ValueSet Value = new ValueSet
                        {
                            { "ExcuteType", string.Empty }
                        };

                        AppServiceResponse Response = await Connection.SendMessageAsync(Value);

                        if (Response.Status == AppServiceResponseStatus.Success && !Response.Message.ContainsKey("Error"))
                        {
                            switch (Response.Message["ExcuteType"])
                            {
                                case "Excute_Quicklook":
                                    {
                                        string ExcutePath = Convert.ToString(Response.Message["ExcutePath"]);
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
                                        await Connection.SendMessageAsync(Result);
                                        break;
                                    }
                                case "Excute_Get_Associate":
                                    {
                                        string Path = Convert.ToString(Response.Message["AssociatePath"]);
                                        string Associate = ExtensionAssociate.GetAssociate(Path);
                                       
                                        ValueSet Result = new ValueSet
                                        {
                                            {"Get_Associate_Result",string.IsNullOrEmpty(Associate)?"<Empty>":Associate }
                                        };
                                        await Connection.SendMessageAsync(Result);
                                        break;
                                    }
                                case "Excute_RunExe":
                                    {
                                        string ExcutePath = Convert.ToString(Response.Message["ExcutePath"]);
                                        string ExcuteParameter = Convert.ToString(Response.Message["ExcuteParameter"]);
                                        string ExcuteAuthority = Convert.ToString(Response.Message["ExcuteAuthority"]);

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
                            }
                        }
                    }
                }
            }
            catch
            {

            }
        }
    }
}
