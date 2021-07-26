using Microsoft.Toolkit.Uwp.Notifications;
using System;
using System.Linq;
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
        private IBackgroundTaskInstance Instance;

        private CancellationTokenSource Cancellation;

        public async void Run(IBackgroundTaskInstance taskInstance)
        {
            var Deferral = taskInstance.GetDeferral();

            Instance = taskInstance;
            Instance.Canceled += Instance_Canceled;

            if (await CheckUpdateAsync())
            {
                ShowUpdateNotification();
            }

            Deferral.Complete();
        }

        private void Instance_Canceled(IBackgroundTaskInstance sender, BackgroundTaskCancellationReason reason)
        {
            Cancellation?.Cancel();
        }

        private async Task<bool> CheckUpdateAsync()
        {
            try
            {
                Cancellation = new CancellationTokenSource();

                if (StoreContext.GetDefault() is StoreContext Context)
                {
                    return (await Context.GetAppAndOptionalStorePackageUpdatesAsync().AsTask(Cancellation.Token)).Any();
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

                ToastContentBuilder Builder = new ToastContentBuilder()
                                              .SetToastScenario(ToastScenario.Default)
                                              .AddToastActivationInfo("ms-windows-store://pdp/?productid=9N88QBQKF2RS", ToastActivationType.Protocol);
                switch (LanguageIndex)
                {
                    case 0:
                        {
                            Builder.AddText("针对RX文件管理器的更新已发布!", AdaptiveTextStyle.Title)
                                   .AddText("包含最新的功能和改进", AdaptiveTextStyle.Subtitle)
                                   .AddText("点击以立即更新", AdaptiveTextStyle.Subtitle);

                            break;
                        }
                    case 1:
                        {
                            Builder.AddText("An update for the RX-Explorer is available!", AdaptiveTextStyle.Title)
                                   .AddText("Includes the latest features and improvements", AdaptiveTextStyle.Subtitle)
                                   .AddText("Click to update now", AdaptiveTextStyle.Subtitle);

                            break;
                        }
                    case 2:
                        {
                            Builder.AddText("Une mise à jour pour RX Explorer est disponible!", AdaptiveTextStyle.Title)
                                   .AddText("Comprend les dernières fonctionnalités et améliorations", AdaptiveTextStyle.Subtitle)
                                   .AddText("Cliquez pour mettre à jour maintenant", AdaptiveTextStyle.Subtitle);

                            break;
                        }
                    case 3:
                        {

                            Builder.AddText("RX檔案管家的更新已發布!", AdaptiveTextStyle.Title)
                                   .AddText("包括最新功能和改進", AdaptiveTextStyle.Subtitle)
                                   .AddText("點擊立即更新", AdaptiveTextStyle.Subtitle);

                            break;
                        }
                    case 4:
                        {
                            Builder.AddText("¡Se ha lanzado una actualización para el administrador de archivos RX!", AdaptiveTextStyle.Title)
                                   .AddText("Contiene las últimas funciones y mejoras.", AdaptiveTextStyle.Subtitle)
                                   .AddText("Haga clic para actualizar ahora", AdaptiveTextStyle.Subtitle);

                            break;
                        }
                    case 5:
                        {
                            Builder.AddText("Ein Update für den RX-Dateimanager wurde veröffentlicht!", AdaptiveTextStyle.Title)
                                   .AddText("Enthält die neuesten Funktionen und Verbesserungen", AdaptiveTextStyle.Subtitle)
                                   .AddText("Klicken Sie hier, um jetzt zu aktualisieren", AdaptiveTextStyle.Subtitle);

                            break;
                        }
                }

                ToastNotificationManager.CreateToastNotifier().Show(new ToastNotification(Builder.GetToastContent().GetXml()));
            }
        }
    }
}
