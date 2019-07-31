using Microsoft.Toolkit.Uwp.Notifications;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Services.Store;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.Storage.Search;
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
        private Dictionary<Type, string> PageDictionary;
        private List<string> SearchHistoryRecord;
        public static MainPage ThisPage { get; private set; }
        public bool IsNowSearching { get; set; }
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

            if (ApplicationData.Current.LocalSettings.Values["EnableTrace"] == null)
            {
                ApplicationData.Current.LocalSettings.Values["EnableTrace"] = true;
            }

            if (ApplicationData.Current.LocalSettings.Values["SetSearchResultMaxNum"] == null)
            {
                ApplicationData.Current.LocalSettings.Values["SetSearchResultMaxNum"] = Convert.ToString(100);
            }

            SearchHistoryRecord = await SQLite.GetInstance().GetSearchHistoryAsync();

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

                        if (Context.CanSilentlyDownloadStorePackageUpdates)
                        {
                            IProgress<StorePackageUpdateStatus> DownloadProgress = new Progress<StorePackageUpdateStatus>((Status) =>
                            {
                                if (Status.PackageDownloadProgress > 1.0)
                                {
                                    return;
                                }

                                string Tag = "USB-Updating";
                                var data = new NotificationData
                                {
                                    SequenceNumber = 0
                                };
                                data.Values["ProgressValue"] = Status.PackageDownloadProgress.ToString("0.##");
                                data.Values["ProgressString"] = Math.Ceiling(Status.PackageDownloadProgress).ToString() + "%";

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
                                if (Status.PackageDownloadProgress > 1.0)
                                {
                                    return;
                                }

                                string Tag = "USB-Updating";
                                var data = new NotificationData
                                {
                                    SequenceNumber = 0
                                };
                                data.Values["ProgressValue"] = Status.PackageDownloadProgress.ToString("0.##");
                                data.Values["ProgressString"] = Math.Ceiling(Status.PackageDownloadProgress).ToString() + "%";

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

        private async void GlobeSearch_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
        {
            if (string.IsNullOrWhiteSpace(args.QueryText))
            {
                return;
            }

            if (ApplicationData.Current.LocalSettings.Values["LaunchSearchTips"] == null)
            {
                ApplicationData.Current.LocalSettings.Values["LaunchSearchTips"] = true;
                SearchTip.IsOpen = true;
            }

            if (!SearchHistoryRecord.Contains(args.QueryText))
            {
                SearchHistoryRecord.Add(args.QueryText);
                await SQLite.GetInstance().SetSearchHistoryAsync(args.QueryText);
            }

            IsNowSearching = true;

            QueryOptions Options = new QueryOptions(CommonFileQuery.OrderByName, null)
            {
                FolderDepth = FolderDepth.Deep,
                IndexerOption = IndexerOption.UseIndexerWhenAvailable,
                ApplicationSearchFilter = "System.FileName:*" + sender.Text + "*"
            };

            Options.SetThumbnailPrefetch(ThumbnailMode.ListView, 30, ThumbnailOptions.ResizeThumbnail);

            if (GlobeSearch.PlaceholderText.Substring(2) == "USB管理")
            {
                if (USBControl.ThisPage.Nav.CurrentSourcePageType.Name != "SearchPage")
                {
                    StorageItemQueryResult FileQuery = KnownFolders.RemovableDevices.CreateItemQueryWithOptions(Options);

                    USBControl.ThisPage.Nav.Navigate(typeof(SearchPage), FileQuery, new DrillInNavigationTransitionInfo());
                }
                else
                {
                    SearchPage.ThisPage.SetSearchTarget = sender.Text;
                }
            }
        }

        private async void GlobeSearch_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            if (string.IsNullOrWhiteSpace(sender.Text))
            {
                if (IsNowSearching)
                {
                    if (USBControl.ThisPage.CurrentNode == null)
                    {
                        SearchPage.ThisPage.SearchResult.Clear();
                        SearchPage.ThisPage.HasItem.Visibility = Visibility.Visible;
                    }
                    else
                    {
                        await USBControl.ThisPage.DisplayItemsInFolder(USBControl.ThisPage.CurrentNode);
                    }
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

        private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            foreach (var CurrentSelectionName in from NavigationViewItemBase MenuItem in NavView.MenuItems
                                                 where MenuItem is NavigationViewItem && MenuItem.IsSelected
                                                 select MenuItem.Content.ToString())
            {
                GlobeSearch.PlaceholderText = "搜索" + CurrentSelectionName;
            }
        }
    }
}
