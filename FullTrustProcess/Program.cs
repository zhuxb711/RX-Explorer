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

            using (AppServiceConnection Connection = new AppServiceConnection
            {
                AppServiceName = "CommunicateService",
                PackageFamilyName = "36186RuoFan.USB_q3e6crc0w375t"
            })
            {
                string ExcutePath = string.Empty;
                string ExcuteParameter = string.Empty;
                string ExcuteAuthority = string.Empty;

                if (await Connection.OpenAsync() == AppServiceConnectionStatus.Success)
                {
                    ValueSet Value = new ValueSet
                    {
                        { "RX_GetExcuteInfo", string.Empty }
                    };

                    AppServiceResponse Response = await Connection.SendMessageAsync(Value);

                    if (Response.Status == AppServiceResponseStatus.Success && !Response.Message.ContainsKey("Error"))
                    {
                        ExcutePath = Response.Message["RX_ExcutePath"].ToString();
                        ExcuteParameter = Response.Message["RX_ExcuteParameter"].ToString();
                        ExcuteAuthority = Response.Message["RX_ExcuteAuthority"].ToString();
                    }
                }

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
            }
        }
    }
}
