using Microsoft.Toolkit.Uwp.Notifications;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.Background;
using Windows.Services.Store;
using Windows.System.UserProfile;
using Windows.UI.Notifications;

namespace UpdateCheckBackgroundTask
{
    public sealed class UpdateCheck : IBackgroundTask
    {
        IBackgroundTaskInstance Instance;

        private readonly bool IsChinese = GlobalizationPreferences.Languages.FirstOrDefault().StartsWith("zh", StringComparison.OrdinalIgnoreCase);

        private CancellationTokenSource Cancellation;

        public async void Run(IBackgroundTaskInstance taskInstance)
        {
            var Deferral = taskInstance.GetDeferral();

            Instance = taskInstance;
            Instance.Canceled += Instance_Canceled;

            if(await CheckAndInstallUpdate())
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
            var Content = new ToastContent()
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
                                Text = IsChinese
                                        ? "针对RX文件管理器的更新已发布！"
                                        : "An update for the RX Explorer is available!"
                            },

                            new AdaptiveText()
                            {
                                Text = IsChinese
                                        ?"包含最新的功能和改进"
                                        :"Includes the latest features and improvements"
                            },

                            new AdaptiveText()
                            {
                                Text = IsChinese ? "点击以立即更新" : "Click to update now"
                            }
                        }
                    }
                },
                ActivationType = ToastActivationType.Protocol
            };
            ToastNotificationManager.History.Clear();
            ToastNotificationManager.CreateToastNotifier().Show(new ToastNotification(Content.GetXml()));
        }
    }
}
