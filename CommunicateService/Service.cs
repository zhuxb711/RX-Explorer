using System;
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

            ValueSet Value = new ValueSet();

            try
            {
                if (args.Request.Message.ContainsKey("ExcuteType"))
                {
                    switch (Convert.ToString(ApplicationData.Current.LocalSettings.Values["ExcuteType"]))
                    {
                        case "Excute_Quicklook":
                            {
                                Value.Add("ExcuteType", "Excute_Quicklook");
                                Value.Add("RX_ExcutePath", ApplicationData.Current.LocalSettings.Values["ExcutePath"]);
                                break;
                            }
                        case "Excute_RunExe":
                            {
                                Value.Add("ExcuteType", "Excute_RunExe");
                                Value.Add("RX_ExcutePath", ApplicationData.Current.LocalSettings.Values["ExcutePath"]);
                                Value.Add("RX_ExcuteParameter", ApplicationData.Current.LocalSettings.Values["ExcuteParameter"]);
                                Value.Add("RX_ExcuteAuthority", ApplicationData.Current.LocalSettings.Values["ExcuteAuthority"]);
                                break;
                            }
                        case "Excute_Check_QuicklookIsAvaliable":
                            {
                                Value.Add("ExcuteType", "Excute_Check_QuicklookIsAvaliable");
                                break;
                            }
                    }
                }
                else if (args.Request.Message.ContainsKey("Check_QuicklookIsAvaliable_Result"))
                {
                    ApplicationData.Current.LocalSettings.Values["Check_QuicklookIsAvaliable_Result"] = Convert.ToString(args.Request.Message["Check_QuicklookIsAvaliable_Result"]);
                }
                else
                {
                    Value.Add("Error", "This app service is designed only for RX Explorer");
                }

                await args.Request.SendResponseAsync(Value);
            }
            catch
            {
                Value.Add("Error", "Unknown Error happened");
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
