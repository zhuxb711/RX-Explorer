using Microsoft.Toolkit.Uwp.Notifications;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.Services.Store;
using Windows.Storage;
using Windows.UI.Notifications;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace USBManager
{
    public sealed partial class MainPage : Page
    {
        private StoreContext Context;
        private IReadOnlyList<StorePackageUpdate> Updates;
        private Dictionary<Type, string> PageDictionary;
        public static MainPage ThisPage { get; private set; }
        public MainPage()
        {
            InitializeComponent();
            ThisPage = this;
            Window.Current.SetTitleBar(TitleBar);
            Loaded += MainPage_Loaded;
        }

        private async void MainPage_Loaded(object sender, RoutedEventArgs e)
        {
            PageDictionary = new Dictionary<Type, string>
            {
                { typeof(USBControl),"USB管理" },
                { typeof(AboutMe),NavView.SettingsItem.ToString() }
            };

            if(ApplicationData.Current.LocalSettings.Values["EnableTrace"] == null)
            {
                ApplicationData.Current.LocalSettings.Values["EnableTrace"] = true;
            }

            if(ApplicationData.Current.LocalSettings.Values["EnableDirectDelete"]==null)
            {
                ApplicationData.Current.LocalSettings.Values["EnableDirectDelete"] = true;
            }

            Nav.Navigate(typeof(USBControl));

            await CheckAndInstallUpdate();

            await Task.Delay(30000);
            RequestRateApplication();
        }

        private void Nav_Navigated(object sender, Windows.UI.Xaml.Navigation.NavigationEventArgs e)
        {
            BackButton.IsEnabled = Nav.CanGoBack;

            if (Nav.SourcePageType == typeof(SettingPage))
            {
                NavView.SelectedItem = NavView.SettingsItem as NavigationViewItem;
            }
            else
            {
                foreach (var MenuItem in from NavigationViewItemBase MenuItem in NavView.MenuItems
                                         where MenuItem is NavigationViewItem && MenuItem.Content.ToString() == PageDictionary[Nav.SourcePageType]
                                         select MenuItem)
                {
                    MenuItem.IsSelected = true;
                    break;
                }
            }
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            switch (Nav.CurrentSourcePageType.Name)
            {
                case "USBControl":
                    if (USBControl.ThisPage.Nav.CanGoBack)
                    {
                        USBControl.ThisPage.Nav.GoBack();
                    }
                    else if (Nav.CanGoBack)
                    {
                        Nav.GoBack();
                    }
                    break;
                default:
                    if (Nav.CanGoBack)
                    {
                        Nav.GoBack();
                    }
                    break;
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
            RateTip.ActionButtonClick += async (s, e) =>
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

        private void NavView_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
        {
            if (args.IsSettingsInvoked)
            {
                Nav.Navigate(typeof(SettingPage));
            }
            else
            {
                switch (args.InvokedItem.ToString())
                {
                    case "USB管理": Nav.Navigate(typeof(USBControl)); break;
                }
            }
        }

        private void Nav_Navigating(object sender, Windows.UI.Xaml.Navigation.NavigatingCancelEventArgs e)
        {
            if (Nav.CurrentSourcePageType == e.SourcePageType)
            {
                e.Cancel = true;
            }
        }
    }
}
