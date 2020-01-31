using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel.AppService;
using Windows.ApplicationModel.Background;
using Windows.Foundation.Collections;
using Windows.Storage;

namespace CommunicateService
{
    public sealed class Service : IBackgroundTask
    {
        private BackgroundTaskDeferral Deferral;
        private AppServiceConnection Connection;

        public void Run(IBackgroundTaskInstance taskInstance)
        {
            Deferral = taskInstance.GetDeferral();

            taskInstance.Canceled += TaskInstance_Canceled;

            Connection = (taskInstance.TriggerDetails as AppServiceTriggerDetails).AppServiceConnection;
            Connection.RequestReceived += Connection_RequestReceived;
        }

        private async void Connection_RequestReceived(AppServiceConnection sender, AppServiceRequestReceivedEventArgs args)
        {
            var Deferral = args.GetDeferral();

            try
            {
                ValueSet Value = new ValueSet();
                if (args.Request.Message.ContainsKey("RX_GetExcuteInfo"))
                {
                    Value.Add("RX_ExcutePath", ApplicationData.Current.LocalSettings.Values["ExcutePath"]);
                    Value.Add("RX_ExcuteParameter", ApplicationData.Current.LocalSettings.Values["ExcuteParameter"]);
                    Value.Add("RX_ExcuteAuthority", ApplicationData.Current.LocalSettings.Values["ExcuteAuthority"]);
                }
                else
                {
                    Value.Add("Error", "This app service is designed only for RX Explorer");
                }

                await args.Request.SendResponseAsync(Value);
            }
            finally
            {
                Deferral.Complete();
            }
        }

        private void TaskInstance_Canceled(IBackgroundTaskInstance sender, BackgroundTaskCancellationReason reason)
        {
            Connection = null;
            Deferral.Complete();
        }
    }
}
