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

                if (await Connection.OpenAsync() == AppServiceConnectionStatus.Success)
                {
                    ValueSet Value = new ValueSet
                    {
                        { "RX_ExcutePath", string.Empty }
                    };

                    AppServiceResponse Response = await Connection.SendMessageAsync(Value);

                    if (Response.Status == AppServiceResponseStatus.Success && !Response.Message.ContainsKey("Error") && Response.Message.ContainsKey("RX_ExcutePath"))
                    {
                        ExcutePath = Response.Message["RX_ExcutePath"].ToString();
                    }
                }

                if (!string.IsNullOrEmpty(ExcutePath))
                {
                    if (ExcutePath.Contains("|"))
                    {
                        string[] ParaGroup = ExcutePath.Split('|');

                        if (SpecialStringMap.Contains(ParaGroup[0]))
                        {
                            Process.Start(ParaGroup[0], ParaGroup[1]).Dispose();
                        }
                        else
                        {
                            Process.Start(ParaGroup[0], $"\"{ ParaGroup[1]}\"").Dispose();
                        }
                    }
                    else
                    {
                        Process.Start(ExcutePath);
                    }
                }
            }
        }
    }
}
