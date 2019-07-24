using Microsoft.Toolkit.Uwp.Notifications;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.Services.Store;
using Windows.Storage;
using Windows.System;
using Windows.UI.Notifications;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Animation;

namespace USBManager
{
    public sealed partial class MainPage : Page
    {
        private StoreContext Context;
        private IReadOnlyList<StorePackageUpdate> Updates;

        public MainPage()
        {
            InitializeComponent();
            Window.Current.SetTitleBar(TitleBar);
            Loaded += MainPage_Loaded;
        }

        private async void MainPage_Loaded(object sender, RoutedEventArgs e)
        {
            Nav.Navigate(typeof(USBControl), null, new DrillInNavigationTransitionInfo());

            USBControl.ThisPage.Nav.Navigated += Nav_Navigated;

            await CheckAndInstallUpdate();

            await Task.Delay(30000);
            RequestRateApplication();
        }

        private void Nav_Navigated(object sender, Windows.UI.Xaml.Navigation.NavigationEventArgs e)
        {
            BackButton.IsEnabled = USBControl.ThisPage.Nav.CanGoBack;
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (USBControl.ThisPage.Nav.CanGoBack)
            {
                USBControl.ThisPage.Nav.GoBack();
            }
        }

        private bool WhetherUserRateAppInPast()
        {
            return ApplicationData.Current.LocalSettings.Values["IsRated"] is bool;
        }

        private void RequestRateApplication()
        {
            if (WhetherUserRateAppInPast())
            {
                return;
            }
            else
            {
                ApplicationData.Current.LocalSettings.Values["IsRated"] = true;
            }

            RateTip.IsOpen = true;
            RateTip.ActionButtonClick += async(s, e) =>
            {
                var Result = await Context.RequestRateAndReviewAppAsync();
                switch (Result.Status)
                {
                    case StoreRateAndReviewStatus.Succeeded:
                        ShowRateSucceedNotification();
                        break;
                    case StoreRateAndReviewStatus.CanceledByUser:
                        break;
                    default:
                        ShowRateErrorNotification();
                        break;
                }
            };
        }

        private void ShowRateSucceedNotification()
        {
            var Content = new ToastContent()
            {
                Scenario = ToastScenario.Default,
                Launch = "Updating",
                Visual = new ToastVisual()
                {
                    BindingGeneric = new ToastBindingGeneric()
                    {
                        Children =
                        {
                            new AdaptiveText()
                            {
                                Text = "评价成功"
                            },

                            new AdaptiveText()
                            {
                                Text = "感谢您对此App的评价，帮助我们做得更好。"
                            }
                        }
                    }
                },
            };
            ToastNotificationManager.CreateToastNotifier().Show(new ToastNotification(Content.GetXml()));
        }

        private void ShowRateErrorNotification()
        {
            var Content = new ToastContent()
            {
                Scenario = ToastScenario.Default,
                Launch = "Updating",
                Visual = new ToastVisual()
                {
                    BindingGeneric = new ToastBindingGeneric()
                    {
                        Children =
                        {
                            new AdaptiveText()
                            {
                                Text = "评价失败"
                            },

                            new AdaptiveText()
                            {
                                Text = "因网络或其他原因而无法进行评价"
                            }
                        }
                    }
                },
            };
            ToastNotificationManager.CreateToastNotifier().Show(new ToastNotification(Content.GetXml()));
        }


        private async Task CheckAndInstallUpdate()
        {
            try
            {
                Context = StoreContext.GetDefault();
                Updates = await Context.GetAppAndOptionalStorePackageUpdatesAsync();

                if (Updates.Count > 0)
                {
                    UpdateTip.Subtitle = "最新版USB文件管理器已推出！\r最新版包含针对以往问题的修复补丁\r是否立即下载？";

                    UpdateTip.ActionButtonClick += async (s, e) =>
                    {
                        s.IsOpen = false;
                        SendUpdatableToastWithProgress();

                        Progress<StorePackageUpdateStatus> UpdateProgress = new Progress<StorePackageUpdateStatus>((Status) =>
                        {
                            string Tag = "USB-Updating";

                            var data = new NotificationData
                            {
                                SequenceNumber = 0
                            };
                            data.Values["ProgressValue"] = (Status.PackageDownloadProgress * 1.25).ToString("0.##");
                            data.Values["ProgressString"] = Math.Ceiling(Status.PackageDownloadProgress * 125).ToString() + "%";

                            ToastNotificationManager.CreateToastNotifier().Update(data, Tag);
                        });

                        if (Context.CanSilentlyDownloadStorePackageUpdates)
                        {
                            StorePackageUpdateResult DownloadResult = await Context.TrySilentDownloadAndInstallStorePackageUpdatesAsync(Updates).AsTask(UpdateProgress);

                            if (DownloadResult.OverallState != StorePackageUpdateState.Completed)
                            {
                                ShowErrorNotification();
                            }
                        }
                        else
                        {
                            StorePackageUpdateResult DownloadResult = await Context.RequestDownloadAndInstallStorePackageUpdatesAsync(Updates).AsTask(UpdateProgress);

                            if (DownloadResult.OverallState != StorePackageUpdateState.Completed)
                            {
                                ShowErrorNotification();
                            }
                        }
                    };

                    UpdateTip.IsOpen = true;
                }
            }
            catch (Exception)
            {
                ShowErrorNotification();
            }
        }

        private void ShowErrorNotification()
        {
            var Content = new ToastContent()
            {
                Scenario = ToastScenario.Default,
                Launch = "Updating",
                Visual = new ToastVisual()
                {
                    BindingGeneric = new ToastBindingGeneric()
                    {
                        Children =
                        {
                            new AdaptiveText()
                            {
                                Text = "更新失败"
                            },

                            new AdaptiveText()
                            {
                                Text = "USB文件管理器无法更新至最新版"
                            }
                        }
                    }
                },
            };
            ToastNotificationManager.History.Clear();
            ToastNotificationManager.CreateToastNotifier().Show(new ToastNotification(Content.GetXml()));
        }

        private void SendUpdatableToastWithProgress()
        {
            string Tag = "USB-Updating";

            var content = new ToastContent()
            {
                Launch = "Updating",
                Scenario = ToastScenario.Reminder,
                Visual = new ToastVisual()
                {
                    BindingGeneric = new ToastBindingGeneric()
                    {
                        Children =
                        {
                            new AdaptiveText()
                            {
                                Text = "正在下载应用更新..."
                            },

                            new AdaptiveProgressBar()
                            {
                                Title = "正在更新",
                                Value = new BindableProgressBarValue("ProgressValue"),
                                Status = new BindableString("ProgressStatus"),
                                ValueStringOverride = new BindableString("ProgressString")
                            }
                        }
                    }
                }
            };

            var Toast = new ToastNotification(content.GetXml())
            {
                Tag = Tag,
                Data = new NotificationData()
            };
            Toast.Data.Values["ProgressValue"] = "0";
            Toast.Data.Values["ProgressStatus"] = "正在下载...";
            Toast.Data.Values["ProgressString"] = "0%";
            Toast.Data.SequenceNumber = 0;

            ToastNotificationManager.History.Clear();
            ToastNotificationManager.CreateToastNotifier().Show(Toast);
        }

    }
}
