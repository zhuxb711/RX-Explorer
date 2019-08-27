using Microsoft.Toolkit.Uwp.Notifications;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Core;
using Windows.Services.Store;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.Storage.Search;
using Windows.UI.Notifications;
using Windows.UI.Shell;
using Windows.UI.StartScreen;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Navigation;

namespace FileManager
{
    public sealed partial class MainPage : Page
    {
        private StoreContext Context;
        private IReadOnlyList<StorePackageUpdate> Updates;
        private List<string> SearchHistoryRecord;
        public static MainPage ThisPage { get; private set; }
        public bool IsNowSearching { get; set; }

        private Dictionary<Type, string> PageDictionary;

        public bool IsUSBActivate { get; set; } = false;

        public string ActivateUSBDevicePath { get; private set; }

        public MainPage()
        {
            InitializeComponent();
            ThisPage = this;
            Window.Current.SetTitleBar(TitleBar);
            Loaded += MainPage_Loaded;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            if (e.Parameter is string Parameter)
            {
                string[] Paras = Parameter.Split("||");
                if (Paras[0] == "USBActivate")
                {
                    IsUSBActivate = true;
                    ActivateUSBDevicePath = Paras[1];
                }
            }
        }

        private async void MainPage_Loaded(object sender, RoutedEventArgs e)
        {
            if (ApplicationData.Current.LocalSettings.Values["SetSearchResultMaxNum"] == null)
            {
                ApplicationData.Current.LocalSettings.Values["SetSearchResultMaxNum"] = Convert.ToString(100);
            }

            PageDictionary = new Dictionary<Type, string>()
            {
                {typeof(WebTab), "浏览器"},
                {typeof(ThisPC),"这台电脑" },
                {typeof(FileControl),"这台电脑" },
                {typeof(AboutMe),"这台电脑" }
            };

            SQLite SQL = SQLite.GetInstance();
            SearchHistoryRecord = await SQL.GetSearchHistoryAsync();

            if (!(ApplicationData.Current.LocalSettings.Values["IsInitQuickStart"] is bool))
            {
                await SQLite.GetInstance().ClearTableAsync("QuickStart");
                ApplicationData.Current.LocalSettings.Values["IsInitQuickStart"] = true;
                await SQL.SetQuickStartItemAsync("应用商店", Path.Combine(Package.Current.InstalledLocation.Path, "QuickStartImage\\MicrosoftStore.png"), "ms-windows-store://home", QuickStartType.Application);
                await SQL.SetQuickStartItemAsync("计算器", Path.Combine(Package.Current.InstalledLocation.Path, "QuickStartImage\\Calculator.png"), "calculator:", QuickStartType.Application);
                await SQL.SetQuickStartItemAsync("系统设置", Path.Combine(Package.Current.InstalledLocation.Path, "QuickStartImage\\Setting.png"), "ms-settings:", QuickStartType.Application);
                await SQL.SetQuickStartItemAsync("邮件", Path.Combine(Package.Current.InstalledLocation.Path, "QuickStartImage\\Email.png"), "mailto:", QuickStartType.Application);
                await SQL.SetQuickStartItemAsync("日历", Path.Combine(Package.Current.InstalledLocation.Path, "QuickStartImage\\Calendar.png"), "outlookcal:", QuickStartType.Application);
                await SQL.SetQuickStartItemAsync("必应地图", Path.Combine(Package.Current.InstalledLocation.Path, "QuickStartImage\\Map.png"), "bingmaps:", QuickStartType.Application);
                await SQL.SetQuickStartItemAsync("天气", Path.Combine(Package.Current.InstalledLocation.Path, "QuickStartImage\\Weather.png"), "bingweather:", QuickStartType.Application);
                await SQL.SetQuickStartItemAsync("必应", Path.Combine(Package.Current.InstalledLocation.Path, "HotWebImage\\Bing.png"), "https://www.bing.com/", QuickStartType.WebSite);
                await SQL.SetQuickStartItemAsync("百度", Path.Combine(Package.Current.InstalledLocation.Path, "HotWebImage\\Baidu.png"), "https://www.baidu.com/", QuickStartType.WebSite);
                await SQL.SetQuickStartItemAsync("微信", Path.Combine(Package.Current.InstalledLocation.Path, "HotWebImage\\Wechat.png"), "https://wx.qq.com/", QuickStartType.WebSite);
                await SQL.SetQuickStartItemAsync("IT之家", Path.Combine(Package.Current.InstalledLocation.Path, "HotWebImage\\iThome.jpg"), "https://www.ithome.com/", QuickStartType.WebSite);
                await SQL.SetQuickStartItemAsync("微博", Path.Combine(Package.Current.InstalledLocation.Path, "HotWebImage\\Weibo.png"), "https://www.weibo.com/", QuickStartType.WebSite);
            }

            Nav.Navigate(typeof(ThisPC));

            await CheckAndInstallUpdate();
        }

        private void Nav_Navigated(object sender, NavigationEventArgs e)
        {
            NavView.IsBackEnabled = Nav.CanGoBack;

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

        private async Task PinApplicationToTaskBar()
        {
            if (ApplicationData.Current.LocalSettings.Values["IsPinToTaskBar"] is bool)
            {
                return;
            }
            else
            {
                ApplicationData.Current.LocalSettings.Values["IsPinToTaskBar"] = true;
            }

            TaskbarManager BarManager = TaskbarManager.GetDefault();
            StartScreenManager ScreenManager = StartScreenManager.GetDefault();

            bool PinStartScreen = false, PinTaskBar = false;

            AppListEntry Entry = (await Package.Current.GetAppListEntriesAsync()).FirstOrDefault();
            if (ScreenManager.SupportsAppListEntry(Entry) && !await ScreenManager.ContainsAppListEntryAsync(Entry))
            {
                PinStartScreen = true;
            }
            if (BarManager.IsPinningAllowed && !await BarManager.IsCurrentAppPinnedAsync())
            {
                PinTaskBar = true;
            }

            if (PinStartScreen && PinTaskBar)
            {
                PinTip.ActionButtonClick += async (s, e) =>
                {
                    _ = await BarManager.RequestPinCurrentAppAsync();
                    _ = await ScreenManager.RequestAddAppListEntryAsync(Entry);
                };
            }
            else if (PinStartScreen && !PinTaskBar)
            {
                PinTip.ActionButtonClick += async (s, e) =>
                {
                    _ = await ScreenManager.RequestAddAppListEntryAsync(Entry);
                };
            }
            else if (!PinStartScreen && PinTaskBar)
            {
                PinTip.ActionButtonClick += async (s, e) =>
                {
                    _ = await BarManager.RequestPinCurrentAppAsync();
                };
            }

            PinTip.Closed += async (s, e) =>
            {
                await Task.Delay(20000);
                RequestRateApplication();
            };

            PinTip.Subtitle = "将RX文件管理器固定在和开始屏幕任务栏，启动更快更方便哦！\r\r★固定至开始菜单\r\r★固定至任务栏";
            PinTip.IsOpen = true;
        }

        private void RequestRateApplication()
        {
            if (ApplicationData.Current.LocalSettings.Values["IsRated"] is bool)
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
                    UpdateTip.Subtitle = "最新版RX文件管理器已推出！\r最新版包含针对以往问题的修复补丁\r是否立即下载？";

                    UpdateTip.ActionButtonClick += async (s, e) =>
                    {
                        s.IsOpen = false;
                        SendUpdatableToastWithProgress();

                        if (Context.CanSilentlyDownloadStorePackageUpdates)
                        {
                            IProgress<StorePackageUpdateStatus> DownloadProgress = new Progress<StorePackageUpdateStatus>((Status) =>
                            {
                                if (Status.PackageDownloadProgress > 0.8)
                                {
                                    return;
                                }

                                string Tag = "RX-Updating";
                                var data = new NotificationData
                                {
                                    SequenceNumber = 0
                                };
                                data.Values["ProgressValue"] = (Status.PackageDownloadProgress * 1.25).ToString("0.##");
                                data.Values["ProgressString"] = Math.Ceiling(Status.PackageDownloadProgress * 125).ToString() + "%";

                                ToastNotificationManager.CreateToastNotifier().Update(data, Tag);
                            });

                            StorePackageUpdateResult DownloadResult = await Context.TrySilentDownloadAndInstallStorePackageUpdatesAsync(Updates).AsTask(DownloadProgress);

                            if (DownloadResult.OverallState == StorePackageUpdateState.Completed)
                            {
                                _ = await Context.TrySilentDownloadAndInstallStorePackageUpdatesAsync(Updates).AsTask();
                            }
                            else
                            {
                                ShowErrorNotification();
                            }
                        }
                        else
                        {
                            IProgress<StorePackageUpdateStatus> DownloadProgress = new Progress<StorePackageUpdateStatus>((Status) =>
                            {
                                if (Status.PackageDownloadProgress > 0.8)
                                {
                                    return;
                                }

                                string Tag = "RX-Updating";
                                var data = new NotificationData
                                {
                                    SequenceNumber = 0
                                };
                                data.Values["ProgressValue"] = (Status.PackageDownloadProgress * 1.25).ToString("0.##");
                                data.Values["ProgressString"] = Math.Ceiling(Status.PackageDownloadProgress * 125).ToString() + "%";

                                ToastNotificationManager.CreateToastNotifier().Update(data, Tag);
                            });

                            StorePackageUpdateResult DownloadResult = await Context.RequestDownloadAndInstallStorePackageUpdatesAsync(Updates).AsTask(DownloadProgress);

                            if (DownloadResult.OverallState == StorePackageUpdateState.Completed)
                            {
                                _ = await Context.RequestDownloadAndInstallStorePackageUpdatesAsync(Updates).AsTask();
                            }
                            else
                            {
                                ShowErrorNotification();
                            }
                        }
                    };

                    UpdateTip.Closed += async (s, e) =>
                    {
                        await Task.Delay(5000);
                        await PinApplicationToTaskBar();
                    };

                    UpdateTip.IsOpen = true;
                }
            }
            catch (Exception)
            {
                ShowErrorNotification();
                await Task.Delay(5000);
                await PinApplicationToTaskBar();
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
                                Text = "RX文件管理器无法更新至最新版"
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
            string Tag = "RX-Updating";

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
                Nav.Navigate(typeof(SettingPage), null, new DrillInNavigationTransitionInfo());
            }
            else
            {
                switch (args.InvokedItem.ToString())
                {
                    case "这台电脑":
                        Nav.Navigate(typeof(ThisPC), null, new DrillInNavigationTransitionInfo()); break;
                    case "浏览器":
                        Nav.Navigate(typeof(WebTab), null, new DrillInNavigationTransitionInfo()); break;
                }
            }
        }

        private void Nav_Navigating(object sender, NavigatingCancelEventArgs e)
        {
            if (Nav.CurrentSourcePageType == e.SourcePageType)
            {
                e.Cancel = true;
            }
        }

        private async void GlobeSearch_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
        {
            if (string.IsNullOrWhiteSpace(args.QueryText))
            {
                return;
            }

            FlyoutBase.ShowAttachedFlyout(sender);

            if (!SearchHistoryRecord.Contains(args.QueryText))
            {
                SearchHistoryRecord.Add(args.QueryText);
                await SQLite.GetInstance().SetSearchHistoryAsync(args.QueryText);
            }
        }

        private void GlobeSearch_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            if (string.IsNullOrWhiteSpace(sender.Text))
            {
                if (IsNowSearching)
                {
                    FileControl.ThisPage.Nav.GoBack();
                }
                return;
            }
            if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
            {
                List<string> FilterResult = SearchHistoryRecord.FindAll((s) => s.Contains(sender.Text, StringComparison.OrdinalIgnoreCase));
                if (FilterResult.Count == 0)
                {
                    FilterResult.Add("无建议");
                }
                sender.ItemsSource = FilterResult;
            }
        }

        private void NavView_BackRequested(NavigationView sender, NavigationViewBackRequestedEventArgs args)
        {
            switch (Nav.CurrentSourcePageType.Name)
            {
                case "FileControl":
                    if (FileControl.ThisPage.Nav.CanGoBack)
                    {
                        FileControl.ThisPage.Nav.GoBack();
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

        private void SearchConfirm_Click(object sender, RoutedEventArgs e)
        {
            SearchFlyout.Hide();

            if (ApplicationData.Current.LocalSettings.Values["LaunchSearchTips"] == null)
            {
                ApplicationData.Current.LocalSettings.Values["LaunchSearchTips"] = true;
                SearchTip.IsOpen = true;
            }

            IsNowSearching = true;

            QueryOptions Options;
            if ((bool)ShallowRadio.IsChecked)
            {
                Options = new QueryOptions(CommonFileQuery.OrderByName, null)
                {
                    FolderDepth = FolderDepth.Shallow,
                    IndexerOption = IndexerOption.UseIndexerWhenAvailable,
                    ApplicationSearchFilter = "System.FileName:*" + GlobeSearch.Text + "*"
                };
            }
            else
            {
                Options = new QueryOptions(CommonFileQuery.OrderByName, null)
                {
                    FolderDepth = FolderDepth.Deep,
                    IndexerOption = IndexerOption.UseIndexerWhenAvailable,
                    ApplicationSearchFilter = "System.FileName:*" + GlobeSearch.Text + "*"
                };
            }

            Options.SetThumbnailPrefetch(ThumbnailMode.ListView, 60, ThumbnailOptions.ResizeThumbnail);

            if (FileControl.ThisPage.Nav.CurrentSourcePageType.Name != "SearchPage")
            {
                StorageItemQueryResult FileQuery = (FileControl.ThisPage.FolderTree.RootNodes.FirstOrDefault().Content as StorageFolder).CreateItemQueryWithOptions(Options);

                FileControl.ThisPage.Nav.Navigate(typeof(SearchPage), FileQuery, new DrillInNavigationTransitionInfo());
            }
            else
            {
                SearchPage.ThisPage.SetSearchTarget = Options;
            }
        }

        private void SearchCancel_Click(object sender, RoutedEventArgs e)
        {
            SearchFlyout.Hide();
        }
    }
}
