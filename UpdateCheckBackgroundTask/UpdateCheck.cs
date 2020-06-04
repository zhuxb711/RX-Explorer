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
                switch (LanguageIndex)
                {
                    case 0:
                        {

                            ToastContent Content = new ToastContent()
                            {
                                Scenario = ToastScenario.Default,
                                Launch = "ms-windows-store://pdp/?productid=9N88QBQKF2RS",
                                Visual = new ToastVisual()
                                {
                                    BindingGeneric = new ToastBindingGeneric()
                                    {
                                        Children =
                                        {
                                            new AdaptiveText()
                                            {
                                                Text = "针对RX文件管理器的更新已发布！"
                                            },

                                            new AdaptiveText()
                                            {
                                                Text = "包含最新的功能和改进"
                                            },

                                            new AdaptiveText()
                                            {
                                                Text = "点击以立即更新"
                                            }
                                        }
                                    }
                                },
                                ActivationType = ToastActivationType.Protocol
                            };
                            ToastNotificationManager.History.Clear();
                            ToastNotificationManager.CreateToastNotifier().Show(new ToastNotification(Content.GetXml()));
                            break;
                        }
                    case 1:
                        {

                            ToastContent Content = new ToastContent()
                            {
                                Scenario = ToastScenario.Default,
                                Launch = "ms-windows-store://pdp/?productid=9N88QBQKF2RS",
                                Visual = new ToastVisual()
                                {
                                    BindingGeneric = new ToastBindingGeneric()
                                    {
                                        Children =
                                        {
                                            new AdaptiveText()
                                            {
                                                Text = "An update for the RX Explorer is available!"
                                            },

                                            new AdaptiveText()
                                            {
                                                Text = "Includes the latest features and improvements"
                                            },

                                            new AdaptiveText()
                                            {
                                                Text = "Click to update now"
                                            }
                                        }
                                    }
                                },
                                ActivationType = ToastActivationType.Protocol
                            };
                            ToastNotificationManager.History.Clear();
                            ToastNotificationManager.CreateToastNotifier().Show(new ToastNotification(Content.GetXml()));
                            break;
                        }
                    case 2:
                        {

                            ToastContent Content = new ToastContent()
                            {
                                Scenario = ToastScenario.Default,
                                Launch = "ms-windows-store://pdp/?productid=9N88QBQKF2RS",
                                Visual = new ToastVisual()
                                {
                                    BindingGeneric = new ToastBindingGeneric()
                                    {
                                        Children =
                                        {
                                            new AdaptiveText()
                                            {
                                                Text = "Une mise à jour pour RX Explorer est disponible!"
                                            },

                                            new AdaptiveText()
                                            {
                                                Text = "Comprend les dernières fonctionnalités et améliorations"
                                            },

                                            new AdaptiveText()
                                            {
                                                Text = "Cliquez pour mettre à jour maintenant"
                                            }
                                        }
                                    }
                                },
                                ActivationType = ToastActivationType.Protocol
                            };
                            ToastNotificationManager.History.Clear();
                            ToastNotificationManager.CreateToastNotifier().Show(new ToastNotification(Content.GetXml()));
                            break;
                        }
                }
            }
        }
    }
}
