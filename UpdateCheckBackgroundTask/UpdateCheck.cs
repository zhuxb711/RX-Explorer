using Microsoft.Toolkit.Uwp.Notifications;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Background;
using Windows.Services.Store;
using Windows.Storage;
using Windows.UI.Notifications;

namespace UpdateCheckBackgroundTask
{
    public sealed class UpdateCheck : IBackgroundTask
    {
        public async void Run(IBackgroundTaskInstance taskInstance)
        {
            var Deferral = taskInstance.GetDeferral();

            try
            {
                using (CancellationTokenSource Cancellation = new CancellationTokenSource())
                {
                    taskInstance.Canceled += (s, e) =>
                    {
                        Cancellation.Cancel();
                    };

                    if (await CheckUpdateAsync(Cancellation.Token))
                    {
                        ShowUpdateNotification();
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // No need to handle this exception
            }
            catch (Exception)
            {
#if DEBUG
                if (Debugger.IsAttached)
                {
                    Debugger.Break();
                }
                else
                {
                    Debugger.Launch();
                }
#endif
            }
            finally
            {
                Deferral.Complete();
            }
        }

        private async Task<bool> CheckUpdateAsync(CancellationToken CancelToken = default)
        {
            if (!Package.Current.IsDevelopmentMode && Package.Current.SignatureKind == PackageSignatureKind.Store)
            {
                StoreContext Context = StoreContext.GetDefault();
                IReadOnlyList<StorePackageUpdate> Updates = await Context.GetAppAndOptionalStorePackageUpdatesAsync().AsTask().AsCancellable(CancelToken);
                return Updates.Count > 0;
            }

            return false;
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
