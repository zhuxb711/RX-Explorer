using Microsoft.Toolkit.Uwp.Notifications;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.Background;
using Windows.Services.Store;
using Windows.Storage;
using Windows.UI.Notifications;

namespace UpdateCheckBackgroundTask
{
    public sealed class UpdateCheck : IBackgroundTask
    {
        IBackgroundTaskInstance Instance;

        private CancellationTokenSource Cancellation;

        public async void Run(IBackgroundTaskInstance taskInstance)
        {
            var Deferral = taskInstance.GetDeferral();

            Instance = taskInstance;
            Instance.Canceled += Instance_Canceled;

            if (await CheckAndInstallUpdate())
            {
                ShowUpdateNotification();
            }

            Deferral.Complete();
        }

        private void Instance_Canceled(IBackgroundTaskInstance sender, BackgroundTaskCancellationReason reason)
        {
            Cancellation?.Cancel();
        }

        private async Task<bool> CheckAndInstallUpdate()
        {
            try
            {
                Cancellation = new CancellationTokenSource();

                if (StoreContext.GetDefault() is StoreContext Context)
                {
                    IReadOnlyList<StorePackageUpdate> Updates = await Context.GetAppAndOptionalStorePackageUpdatesAsync().AsTask(Cancellation.Token);

                    return Updates.Count > 0;
                }
                else
                {
                    return false;
                }
            }
            catch
            {
                return false;
            }
            finally
            {
                Cancellation.Dispose();
                Cancellation = null;
            }
        }

        private void ShowUpdateNotification()
        {
            if (ApplicationData.Current.LocalSettings.Values["LanguageOverride"] is int LanguageIndex)
            {
                ToastNotificationManager.History.Clear();

                switch (LanguageIndex)
                {
                    case 0:
                        {
                            ToastContentBuilder Builder = new ToastContentBuilder()
                                                          .SetToastScenario(ToastScenario.Default)
                                                          .AddToastActivationInfo("ms-windows-store://pdp/?productid=9N88QBQKF2RS", ToastActivationType.Protocol)
                                                          .AddText("针对RX文件管理器的更新已发布!", AdaptiveTextStyle.Title)
                                                          .AddText("包含最新的功能和改进", AdaptiveTextStyle.Subtitle)
                                                          .AddText("点击以立即更新", AdaptiveTextStyle.Subtitle);
                            
                            ToastNotificationManager.CreateToastNotifier().Show(new ToastNotification(Builder.GetToastContent().GetXml()));
                            break;
                        }
                    case 1:
                        {
                            ToastContentBuilder Builder = new ToastContentBuilder()
                                                          .SetToastScenario(ToastScenario.Default)
                                                          .AddToastActivationInfo("ms-windows-store://pdp/?productid=9N88QBQKF2RS", ToastActivationType.Protocol)
                                                          .AddText("An update for the RX-Explorer is available!", AdaptiveTextStyle.Title)
                                                          .AddText("Includes the latest features and improvements", AdaptiveTextStyle.Subtitle)
                                                          .AddText("Click to update now", AdaptiveTextStyle.Subtitle);

                            ToastNotificationManager.CreateToastNotifier().Show(new ToastNotification(Builder.GetToastContent().GetXml()));
                            break;
                        }
                    case 2:
                        {
                            ToastContentBuilder Builder = new ToastContentBuilder()
                                                          .SetToastScenario(ToastScenario.Default)
                                                          .AddToastActivationInfo("ms-windows-store://pdp/?productid=9N88QBQKF2RS", ToastActivationType.Protocol)
                                                          .AddText("Une mise à jour pour RX Explorer est disponible!", AdaptiveTextStyle.Title)
                                                          .AddText("Comprend les dernières fonctionnalités et améliorations", AdaptiveTextStyle.Subtitle)
                                                          .AddText("Cliquez pour mettre à jour maintenant", AdaptiveTextStyle.Subtitle);

                            ToastNotificationManager.CreateToastNotifier().Show(new ToastNotification(Builder.GetToastContent().GetXml()));
                            break;
                        }
                    case 3:
                        {
                            ToastContentBuilder Builder = new ToastContentBuilder()
                                                          .SetToastScenario(ToastScenario.Default)
                                                          .AddToastActivationInfo("ms-windows-store://pdp/?productid=9N88QBQKF2RS", ToastActivationType.Protocol)
                                                          .AddText("RX檔案管家的更新已發布!", AdaptiveTextStyle.Title)
                                                          .AddText("包括最新功能和改進", AdaptiveTextStyle.Subtitle)
                                                          .AddText("點擊立即更新", AdaptiveTextStyle.Subtitle);

                            ToastNotificationManager.CreateToastNotifier().Show(new ToastNotification(Builder.GetToastContent().GetXml()));
                            break;
                        }
                }
            }
        }
    }
}
