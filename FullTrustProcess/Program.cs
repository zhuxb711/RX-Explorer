using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Windows.ApplicationModel.AppService;
using Windows.Foundation.Collections;

namespace FullTrustProcess
{
    class Program
    {
        static async Task Main(string[] args)
        {
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
                    Process.Start(ExcutePath);
                }
            }
        }
    }
}
