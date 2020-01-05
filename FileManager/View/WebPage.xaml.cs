using DownloaderProvider;
using Microsoft.Toolkit.Uwp.Notifications;
using Microsoft.Toolkit.Uwp.UI.Controls;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Devices.Bluetooth;
using Windows.Devices.Geolocation;
using Windows.Devices.Radios;
using Windows.Media.Capture;
using Windows.Storage;
using Windows.Storage.AccessCache;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;
using Windows.System;
using Windows.UI;
using Windows.UI.Notifications;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;

namespace FileManager
{
    public sealed partial class WebPage : Page, IDisposable
    {
        private bool CanCancelLoading;
        public bool IsPressedFavourite;
        public WebView WebBrowser = null;
        public TabViewItem ThisTab;
        private bool IsRefresh = false;
        private bool IsPermissionProcessing = false;
        private DispatcherTimer SuggestionTimer;
        private AutoResetEvent PermissionLocker;
        private bool UserDenyOnToast = false;

        public WebPage(Uri uri = null)
        {
            InitializeComponent();
            PermissionLocker = new AutoResetEvent(false);
            FavouriteList.ItemsSource = WebTab.ThisPage.FavouriteCollection;
            DownloadList.ItemsSource = WebDownloader.DownloadList;
            if (Globalization.Language == LanguageEnum.Chinese)
            {
                TabOpenMethod.Items.Add("空白页");
                TabOpenMethod.Items.Add("主页");
                TabOpenMethod.Items.Add("特定页");
                SearchEngine.Items.Add("百度");
                SearchEngine.Items.Add("必应");
                SearchEngine.Items.Add("谷歌");
            }
            else
            {
                TabOpenMethod.Items.Add("Blank Page");
                TabOpenMethod.Items.Add("Home Page");
                TabOpenMethod.Items.Add("Specific Page");
                SearchEngine.Items.Add("Google");
                SearchEngine.Items.Add("Bing");
                SearchEngine.Items.Add("Baidu");
            }

        //由于未知原因此处new WebView时，若选择多进程模型则可能会引发异常
        FLAG:
            try
            {
                WebBrowser = new WebView(WebViewExecutionMode.SeparateProcess);
            }
            catch (Exception)
            {
                goto FLAG;
            }

            try
            {
                InitHistoryList();
                InitializeWebView();
            }
            catch(Exception ex)
            {
                ExceptionTracer.RequestBlueScreen(ex);
            }

            Loaded += WebPage_Loaded;

            if (uri != null)
            {
                WebBrowser.Navigate(uri);
            }
        }

        /// <summary>
        /// 初始化历史记录列表
        /// </summary>
        private void InitHistoryList()
        {
            //根据WebTab提供的分类信息决定历史记录树应当展示多少分类
            if (Globalization.Language == LanguageEnum.Chinese)
            {
                if (WebTab.ThisPage.HistoryFlag.HasFlag(HistoryTreeCategoryFlag.Today))
                {
                    HistoryTree.RootNodes.Add(new TreeViewNode
                    {
                        Content = new WebSiteItem("今天", string.Empty),
                        HasUnrealizedChildren = true,
                        IsExpanded = true
                    });
                }

                if (WebTab.ThisPage.HistoryFlag.HasFlag(HistoryTreeCategoryFlag.Yesterday))
                {
                    HistoryTree.RootNodes.Add(new TreeViewNode
                    {
                        Content = new WebSiteItem("昨天", string.Empty),
                        HasUnrealizedChildren = true
                    });
                }

                if (WebTab.ThisPage.HistoryFlag.HasFlag(HistoryTreeCategoryFlag.Earlier))
                {
                    HistoryTree.RootNodes.Add(new TreeViewNode
                    {
                        Content = new WebSiteItem("更早", string.Empty),
                        HasUnrealizedChildren = true
                    });
                }

                //遍历HistoryCollection集合以向历史记录树中对应分类添加子对象
                foreach (var HistoryItem in WebTab.ThisPage.HistoryCollection)
                {
                    if (HistoryItem.Key == DateTime.Today.AddDays(-1))
                    {
                        var TreeNode = from Item in HistoryTree.RootNodes
                                       where (Item.Content as WebSiteItem).Subject == "昨天"
                                       select Item;
                        TreeNode.FirstOrDefault()?.Children.Add(new TreeViewNode
                        {
                            Content = HistoryItem.Value,
                            HasUnrealizedChildren = false,
                            IsExpanded = false
                        });

                    }
                    else if (HistoryItem.Key == DateTime.Today)
                    {
                        var TreeNode = from Item in HistoryTree.RootNodes
                                       where (Item.Content as WebSiteItem).Subject == "今天"
                                       select Item;
                        TreeNode.FirstOrDefault()?.Children.Add(new TreeViewNode
                        {
                            Content = HistoryItem.Value,
                            HasUnrealizedChildren = false,
                            IsExpanded = false
                        });
                    }
                    else
                    {
                        var TreeNode = from Item in HistoryTree.RootNodes
                                       where (Item.Content as WebSiteItem).Subject == "更早"
                                       select Item;
                        TreeNode.FirstOrDefault()?.Children.Add(new TreeViewNode
                        {
                            Content = HistoryItem.Value,
                            HasUnrealizedChildren = false,
                            IsExpanded = false
                        });
                    }
                }
            }
            else
            {
                if (WebTab.ThisPage.HistoryFlag.HasFlag(HistoryTreeCategoryFlag.Today))
                {
                    HistoryTree.RootNodes.Add(new TreeViewNode
                    {
                        Content = new WebSiteItem("Today", string.Empty),
                        HasUnrealizedChildren = true,
                        IsExpanded = true
                    });
                }

                if (WebTab.ThisPage.HistoryFlag.HasFlag(HistoryTreeCategoryFlag.Yesterday))
                {
                    HistoryTree.RootNodes.Add(new TreeViewNode
                    {
                        Content = new WebSiteItem("Yesterday", string.Empty),
                        HasUnrealizedChildren = true
                    });
                }

                if (WebTab.ThisPage.HistoryFlag.HasFlag(HistoryTreeCategoryFlag.Earlier))
                {
                    HistoryTree.RootNodes.Add(new TreeViewNode
                    {
                        Content = new WebSiteItem("Earlier", string.Empty),
                        HasUnrealizedChildren = true
                    });
                }

                //遍历HistoryCollection集合以向历史记录树中对应分类添加子对象
                foreach (var HistoryItem in WebTab.ThisPage.HistoryCollection)
                {
                    if (HistoryItem.Key == DateTime.Today.AddDays(-1))
                    {
                        var TreeNode = from Item in HistoryTree.RootNodes
                                       where (Item.Content as WebSiteItem).Subject == "Yesterday"
                                       select Item;
                        TreeNode.FirstOrDefault()?.Children.Add(new TreeViewNode
                        {
                            Content = HistoryItem.Value,
                            HasUnrealizedChildren = false,
                            IsExpanded = false
                        });

                    }
                    else if (HistoryItem.Key == DateTime.Today)
                    {
                        var TreeNode = from Item in HistoryTree.RootNodes
                                       where (Item.Content as WebSiteItem).Subject == "Today"
                                       select Item;
                        TreeNode.FirstOrDefault()?.Children.Add(new TreeViewNode
                        {
                            Content = HistoryItem.Value,
                            HasUnrealizedChildren = false,
                            IsExpanded = false
                        });
                    }
                    else
                    {
                        var TreeNode = from Item in HistoryTree.RootNodes
                                       where (Item.Content as WebSiteItem).Subject == "Earlier"
                                       select Item;
                        TreeNode.FirstOrDefault()?.Children.Add(new TreeViewNode
                        {
                            Content = HistoryItem.Value,
                            HasUnrealizedChildren = false,
                            IsExpanded = false
                        });
                    }
                }
            }
        }

        private async void WebPage_Loaded(object sender, RoutedEventArgs e)
        {
            //Loaded在每次切换至当前标签页时都会得到执行，因此在此处可以借机同步不同标签页之间的数据
            //包括其他标签页向收藏列表新增的条目，或其他标签页通过访问网页而向历史记录添加的新条目

            //确定历史记录或收藏列表是否为空，若空则显示“无内容”提示标签
            FavEmptyTips.Visibility = WebTab.ThisPage.FavouriteCollection.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            HistoryEmptyTips.Visibility = WebTab.ThisPage.HistoryCollection.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            DownloadEmptyTips.Visibility = WebDownloader.DownloadList.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

            //其他标签页已执行清空历史记录时，当前标签页也必须删除历史记录树内的所有节点
            if (WebTab.ThisPage.HistoryCollection.Count == 0)
            {
                HistoryTree.RootNodes.Clear();
            }
            else
            {
                var TreeNodes = from Item in HistoryTree.RootNodes
                                let Subject = Globalization.Language == LanguageEnum.Chinese ? "今天" : "Today"
                                where (Item.Content as WebSiteItem).Subject == Subject
                                select Item;
                var TodayNode = TreeNodes.FirstOrDefault();
                if (TodayNode != null)
                {
                    TodayNode.Children.Clear();

                    foreach (var HistoryItem in WebTab.ThisPage.HistoryCollection)
                    {
                        TodayNode.Children.Add(new TreeViewNode
                        {
                            Content = HistoryItem.Value,
                            HasUnrealizedChildren = false,
                            IsExpanded = false
                        });
                    }
                }
            }

            //以下为检索各存储设置以同步各标签页之间对设置界面选项的更改
            if (ApplicationData.Current.LocalSettings.Values["WebTabOpenMethod"] is string Method)
            {
                foreach (var Item in from string Item in TabOpenMethod.Items
                                     where Method == Item
                                     select Item)
                {
                    TabOpenMethod.SelectedItem = Item;
                }
            }
            else
            {
                TabOpenMethod.SelectedIndex = 0;
            }

            if (ApplicationData.Current.LocalSettings.Values["WebTabMainPage"] is string Page)
            {
                MainUrl.Text = Page;
            }
            else
            {
                if (Globalization.Language == LanguageEnum.Chinese)
                {
                    ApplicationData.Current.LocalSettings.Values["WebTabMainPage"] = "https://www.baidu.com";
                    MainUrl.Text = "https://www.baidu.com";
                }
                else
                {
                    ApplicationData.Current.LocalSettings.Values["WebTabMainPage"] = "https://www.bing.com";
                    MainUrl.Text = "https://www.bing.com";
                }
            }

            if (ApplicationData.Current.LocalSettings.Values["WebTabSpecifiedPage"] is string Specified)
            {
                SpecificUrl.Text = Specified;
            }
            else
            {
                ApplicationData.Current.LocalSettings.Values["WebTabSpecifiedPage"] = "about:blank";
                SpecificUrl.Text = "about:blank";
            }

            if (ApplicationData.Current.LocalSettings.Values["WebShowMainButton"] is bool IsShow)
            {
                ShowMainButton.IsOn = IsShow;
            }
            else
            {
                ApplicationData.Current.LocalSettings.Values["WebShowMainButton"] = true;
                ShowMainButton.IsOn = true;
            }

            if (ApplicationData.Current.LocalSettings.Values["WebEnableJS"] is bool IsEnableJS)
            {
                AllowJS.IsOn = IsEnableJS;
            }
            else
            {
                ApplicationData.Current.LocalSettings.Values["WebEnableJS"] = true;
                AllowJS.IsOn = true;
            }

            if (ApplicationData.Current.LocalSettings.Values["WebEnableDB"] is bool IsEnableDB)
            {
                AllowIndexedDB.IsOn = IsEnableDB;
            }
            else
            {
                ApplicationData.Current.LocalSettings.Values["WebEnableDB"] = true;
                AllowIndexedDB.IsOn = true;
            }

            if (!StorageApplicationPermissions.FutureAccessList.ContainsItem("DownloadPath"))
            {
                StorageFolder Folder = ThisPC.ThisPage.LibraryFolderList.Where((Library) => Library.Name == "Downloads").FirstOrDefault().Folder;
                StorageApplicationPermissions.FutureAccessList.AddOrReplace("DownloadPath", Folder);
                DownloadPath.Text = Folder.Path;
            }
            else
            {
                StorageFolder Folder = await StorageApplicationPermissions.FutureAccessList.GetItemAsync("DownloadPath") as StorageFolder;
                DownloadPath.Text = Folder.Path;
            }

            if (ApplicationData.Current.LocalSettings.Values["WebSearchEngine"] is string Engine)
            {
                SearchEngine.SelectedItem = SearchEngine.Items.Where((Item) => Item.ToString() == Engine).FirstOrDefault();
            }
            else
            {
                if (Globalization.Language == LanguageEnum.Chinese)
                {
                    ApplicationData.Current.LocalSettings.Values["WebSearchEngine"] = "百度";
                    SearchEngine.SelectedIndex = 0;
                }
                else
                {
                    ApplicationData.Current.LocalSettings.Values["WebSearchEngine"] = "Google";
                    SearchEngine.SelectedIndex = 0;
                }
            }

            //切换不同标签页时，应当同步InPrivate模式的设置
            //同时因为改变InPrivate设置将导致Toggled事件触发，因此先解除，改变后再绑定
            InPrivate.Toggled -= InPrivate_Toggled;
            if (ApplicationData.Current.LocalSettings.Values["WebActivateInPrivate"] is bool EnableInPrivate)
            {
                InPrivate.IsOn = EnableInPrivate;
                if (EnableInPrivate)
                {
                    Favourite.Visibility = Visibility.Collapsed;
                }
                else
                {
                    Favourite.Visibility = Visibility.Visible;
                }
                InPrivate.Toggled += InPrivate_Toggled;
            }
            else
            {
                ApplicationData.Current.LocalSettings.Values["WebActivateInPrivate"] = false;
                InPrivate.IsOn = false;
                InPrivate.Toggled += InPrivate_Toggled;
            }

            WebDownloader.DownloadList.CollectionChanged += DownloadList_CollectionChanged;
        }

        private void DownloadList_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            DownloadEmptyTips.Visibility = WebDownloader.DownloadList.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private async void InPrivate_Toggled(object sender, RoutedEventArgs e)
        {
            ApplicationData.Current.LocalSettings.Values["WebActivateInPrivate"] = InPrivate.IsOn;
            if (InPrivate.IsOn)
            {
                Favourite.Visibility = Visibility.Collapsed;

                if (Resources.TryGetValue("InAppNotificationWithButtonsTemplate", out object NotificationTemplate) && NotificationTemplate is DataTemplate template)
                {
                    InPrivateNotification.Show(template, 10000);
                }
            }
            else
            {
                InPrivateNotification.Dismiss();
                Favourite.Visibility = Visibility.Visible;
                await WebView.ClearTemporaryWebDataAsync();
            }
        }

        /// <summary>
        /// 初始化WebView控件并部署至XAML界面
        /// </summary>
        private void InitializeWebView()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            SuggestionTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(600)
            };
            SuggestionTimer.Tick += SuggestionTimer_Tick;

            Gr.Children.Add(WebBrowser);
            WebBrowser.SetValue(Grid.RowProperty, 1);
            WebBrowser.SetValue(Canvas.ZIndexProperty, 0);
            WebBrowser.HorizontalAlignment = HorizontalAlignment.Stretch;
            WebBrowser.VerticalAlignment = VerticalAlignment.Stretch;
            WebBrowser.NewWindowRequested += WebBrowser_NewWindowRequested;
            WebBrowser.ContentLoading += WebBrowser_ContentLoading;
            WebBrowser.NavigationCompleted += WebBrowser_NavigationCompleted;
            WebBrowser.NavigationStarting += WebBrowser_NavigationStarting;
            WebBrowser.UnsafeContentWarningDisplaying += WebBrowser_UnsafeContentWarningDisplaying;
            WebBrowser.ContainsFullScreenElementChanged += WebBrowser_ContainsFullScreenElementChanged;
            WebBrowser.PermissionRequested += WebBrowser_PermissionRequested;
            WebBrowser.SeparateProcessLost += WebBrowser_SeparateProcessLost;
            WebBrowser.NavigationFailed += WebBrowser_NavigationFailed;
            WebBrowser.UnviewableContentIdentified += WebBrowser_UnviewableContentIdentified;
            WebBrowser.LongRunningScriptDetected += WebBrowser_LongRunningScriptDetected;
        }

        private void WebBrowser_LongRunningScriptDetected(WebView sender, WebViewLongRunningScriptDetectedEventArgs args)
        {
            if (args.ExecutionTime > TimeSpan.FromSeconds(15))
            {
                args.StopPageScriptExecution = true;
            }
        }

        private async void SuggestionTimer_Tick(object sender, object e)
        {
            SuggestionTimer.Stop();
            switch (SearchEngine.SelectedItem.ToString())
            {
                case "百度":
                case "Baidu":
                    {
                        if (!string.IsNullOrEmpty(AutoSuggest.Text))
                        {
                            if (JsonConvert.DeserializeObject<BaiduSearchSuggestionResult>(await GetBaiduJsonFromWeb(AutoSuggest.Text)) is BaiduSearchSuggestionResult BaiduSearchResult)
                            {
                                AutoSuggest.ItemsSource = BaiduSearchResult.s;
                            }
                        }
                        break;
                    }
                case "谷歌":
                case "Google":
                    {
                        if (!string.IsNullOrEmpty(AutoSuggest.Text))
                        {
                            AutoSuggest.ItemsSource = await GetGoogleSearchResponse(AutoSuggest.Text);
                        }
                        break;
                    }
                case "必应":
                case "Bing":
                    {
                        if (!string.IsNullOrEmpty(AutoSuggest.Text))
                        {
                            if (JsonConvert.DeserializeObject<BingSearchSuggestionResult>(await GetBingJsonFromWeb(AutoSuggest.Text)) is BingSearchSuggestionResult BingSearchResult)
                            {
                                AutoSuggest.ItemsSource = BingSearchResult.AS.Results?.FirstOrDefault()?.Suggests?.Select((Item) => Item.Txt);
                            }
                        }
                        break;
                    }
            }
        }

        private void WebBrowser_UnviewableContentIdentified(WebView sender, WebViewUnviewableContentIdentifiedEventArgs args)
        {
            string URL = args.Referrer.ToString();
            string FileName = URL.Substring(URL.LastIndexOf("/") + 1);

            DownloadNotification.Show(GenerateDownloadNotificationTemplate(FileName, args.Referrer));
        }

        private Grid GenerateDownloadNotificationTemplate(string FileName, Uri Refer)
        {
            Grid GridControl = new Grid();

            GridControl.ColumnDefinitions.Add(new ColumnDefinition
            {
                Width = new GridLength(1, GridUnitType.Star)
            });
            GridControl.ColumnDefinitions.Add(new ColumnDefinition
            {
                Width = GridLength.Auto
            });

            if (Globalization.Language == LanguageEnum.Chinese)
            {
                TextBlock textBlock = new TextBlock
                {
                    Text = "是否保存文件 " + FileName + " 至本地计算机?\r发布者：" + (string.IsNullOrWhiteSpace(Refer.Host) ? "Unknown" : Refer.Host),
                    VerticalAlignment = VerticalAlignment.Center,
                    FontSize = 16
                };
                GridControl.Children.Add(textBlock);
            }
            else
            {
                TextBlock textBlock = new TextBlock
                {
                    Text = "Whether to save the file " + FileName + " to the local computer?\rPublisher：" + (string.IsNullOrWhiteSpace(Refer.Host) ? "Unknown" : Refer.Host),
                    VerticalAlignment = VerticalAlignment.Center,
                    FontSize = 16
                };
                GridControl.Children.Add(textBlock);
            }

            // Buttons part
            StackPanel stackPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(20, 0, 0, 0),
            };

            Button SaveConfirmButton = new Button
            {
                Content = Globalization.Language == LanguageEnum.Chinese ? "保存" : "Save",
                Width = 120,
                Height = 30,
            };
            SaveConfirmButton.Click += async (s, e) =>
            {
                DownloadNotification.Dismiss();
                DownloadControl.IsPaneOpen = true;

                DownloadOperator Operation = await WebDownloader.CreateNewDownloadTask(Refer, FileName);
                Operation.DownloadSucceed += Operation_DownloadSucceed;
                Operation.DownloadErrorDetected += Operation_DownloadErrorDetected;
                Operation.DownloadTaskCancel += Operation_DownloadTaskCancel;

                Operation.StartDownload();

                await SQLite.Current.SetDownloadHistoryAsync(Operation);
            };
            stackPanel.Children.Add(SaveConfirmButton);

            Button CancelButton = new Button
            {
                Content = Globalization.Language == LanguageEnum.Chinese ? "取消" : "Cancel",
                Width = 120,
                Height = 30,
                Margin = new Thickness(10, 0, 0, 0)
            };
            CancelButton.Click += (s, e) =>
            {
                DownloadNotification.Dismiss();
            };
            stackPanel.Children.Add(CancelButton);

            Grid.SetColumn(stackPanel, 1);
            GridControl.Children.Add(stackPanel);

            return GridControl;
        }

        private async void Operation_DownloadTaskCancel(object sender, DownloadOperator e)
        {
            await SQLite.Current.UpdateDownloadHistoryAsync(e);

            ToastNotificationManager.CreateToastNotifier().Show(e.GenerateToastNotification(ToastNotificationCategory.TaskCancel));

            e.DownloadSucceed -= Operation_DownloadSucceed;
            e.DownloadErrorDetected -= Operation_DownloadErrorDetected;
            e.DownloadTaskCancel -= Operation_DownloadTaskCancel;
        }

        private async void Operation_DownloadErrorDetected(object sender, DownloadOperator e)
        {
            await SQLite.Current.UpdateDownloadHistoryAsync(e);

            ListViewItem Item = DownloadList.ContainerFromItem(e) as ListViewItem;
            Item.ContentTemplate = DownloadErrorTemplate;

            ToastNotificationManager.CreateToastNotifier().Show(e.GenerateToastNotification(ToastNotificationCategory.Error));

            e.DownloadSucceed -= Operation_DownloadSucceed;
            e.DownloadErrorDetected -= Operation_DownloadErrorDetected;
            e.DownloadTaskCancel -= Operation_DownloadTaskCancel;
        }

        private async void Operation_DownloadSucceed(object sender, DownloadOperator e)
        {
            await SQLite.Current.UpdateDownloadHistoryAsync(e);

            ListViewItem Item = DownloadList.ContainerFromItem(e) as ListViewItem;
            Item.ContentTemplate = DownloadCompleteTemplate;

            ToastNotificationManager.CreateToastNotifier().Show(e.GenerateToastNotification(ToastNotificationCategory.Succeed));

            e.DownloadSucceed -= Operation_DownloadSucceed;
            e.DownloadErrorDetected -= Operation_DownloadErrorDetected;
            e.DownloadTaskCancel -= Operation_DownloadTaskCancel;
        }

        private async void WebBrowser_NavigationFailed(object sender, WebViewNavigationFailedEventArgs e)
        {
            StorageFile HtmlFile = await StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///WebErrorStaticPage/index.html"));
            string HtmlContext = await FileIO.ReadTextAsync(HtmlFile);

            if (Globalization.Language == LanguageEnum.Chinese)
            {
                HtmlContext = HtmlContext.Replace("@PrimaryTip", "抱歉，您访问的页面出错了")
                                         .Replace("@SecondTip", "可能是该网页已删除或不存在或网络故障")
                                         .Replace("@ThirdTip", "您可以尝试以下方案")
                                         .Replace("@HomeButtonText", "返回主页")
                                         .Replace("@Title", "错误：导航失败")
                                         .Replace("@DiagnoseButtonText", "网络诊断");

                string HomeString = ApplicationData.Current.LocalSettings.Values["WebTabMainPage"].ToString();
                if (Uri.TryCreate(HomeString, UriKind.Absolute, out Uri uri))
                {
                    WebBrowser.NavigateToString(HtmlContext.Replace("@HomePageLink", HomeString));
                }
                else
                {
                    WebBrowser.NavigateToString(HtmlContext.Replace("@HomePageLink", "about:blank"));
                }

            }
            else
            {
                HtmlContext = HtmlContext.Replace("@PrimaryTip", "Sorry, the page you visited is not found")
                                         .Replace("@SecondTip", "It may be that the page has been deleted or does not exist or the network is down")
                                         .Replace("@ThirdTip", "You can try the following options")
                                         .Replace("@HomeButtonText", "Go to home page")
                                         .Replace("@Title", "Error: Page not found")
                                         .Replace("@DiagnoseButtonText", "Network diagnosis");

                string HomeString = ApplicationData.Current.LocalSettings.Values["WebTabMainPage"].ToString();
                if (Uri.TryCreate(HomeString, UriKind.Absolute, out Uri uri))
                {
                    WebBrowser.NavigateToString(HtmlContext.Replace("@HomePageLink", HomeString));
                }
                else
                {
                    WebBrowser.NavigateToString(HtmlContext.Replace("@HomePageLink", "about:blank"));
                }
            }
        }

        private async void WebBrowser_SeparateProcessLost(WebView sender, WebViewSeparateProcessLostEventArgs args)
        {
            if (Globalization.Language == LanguageEnum.Chinese)
            {
                QueueContentDialog dialog = new QueueContentDialog
                {
                    Content = "浏览器进程意外终止\r将自动重启并返回主页",
                    Title = "提示",
                    CloseButtonText = "确定"
                };
                _ = await dialog.ShowAsync();
            }
            else
            {
                QueueContentDialog dialog = new QueueContentDialog
                {
                    Content = "Browser process terminated unexpectedly\rWe will automatically restart and return to the home page",
                    Title = "Tips",
                    CloseButtonText = "Confirm"
                };
                _ = await dialog.ShowAsync();
            }

            WebBrowser = new WebView(WebViewExecutionMode.SeparateProcess);
            InitializeWebView();
            WebBrowser.Navigate(new Uri(ApplicationData.Current.LocalSettings.Values["WebTabMainPage"].ToString()));
        }

        private void WebBrowser_ContentLoading(WebView sender, WebViewContentLoadingEventArgs args)
        {
            if (args.Uri == null)
            {
                return;
            }

            ThisTab.Header = string.IsNullOrEmpty(WebBrowser.DocumentTitle) ? (Globalization.Language == LanguageEnum.Chinese ? "正在加载..." : "Loading...") : WebBrowser.DocumentTitle;

            if (AutoSuggest.Text != args.Uri.ToString())
            {
                AutoSuggest.Text = args.Uri.ToString();
            }

            Back.IsEnabled = WebBrowser.CanGoBack;
            Forward.IsEnabled = WebBrowser.CanGoForward;

            //根据AutoSuggest.Text的内容决定是否改变收藏星星的状态
            if (WebTab.ThisPage.FavouriteDictionary.ContainsKey(AutoSuggest.Text))
            {
                Favourite.Symbol = Symbol.SolidStar;
                Favourite.Foreground = new SolidColorBrush(Colors.Gold);
                IsPressedFavourite = true;
            }
            else
            {
                Favourite.Symbol = Symbol.OutlineStar;
                Favourite.Foreground = new SolidColorBrush(Colors.White);
                IsPressedFavourite = false;
            }

            if (InPrivate.IsOn)
            {
                return;
            }

            //多个标签页可能同时执行至此处，因此引用全局锁对象来确保线程同步
            lock (SyncRootProvider.SyncRoot)
            {
                if (AutoSuggest.Text != "about:blank" && !string.IsNullOrEmpty(WebBrowser.DocumentTitle))
                {
                    var HistoryItems = WebTab.ThisPage.HistoryCollection.Where((Item) => Item.Key == DateTime.Today && Item.Value.WebSite == args.Uri.ToString()).ToList();

                    foreach (var HistoryItem in HistoryItems)
                    {
                        if (!HistoryItem.Key.Equals(default))
                        {
                            TreeViewNode TodayNode = null;
                            if (Globalization.Language == LanguageEnum.Chinese)
                            {
                                TodayNode = HistoryTree.RootNodes.Where((Node) => (Node.Content as WebSiteItem).Subject == "今天").FirstOrDefault();
                            }
                            else
                            {
                                TodayNode = HistoryTree.RootNodes.Where((Node) => (Node.Content as WebSiteItem).Subject == "Today").FirstOrDefault();
                            }

                            var RepeatNodes = TodayNode.Children.Where((Item) => (Item.Content as WebSiteItem).WebSite == HistoryItem.Value.WebSite).ToList();
                            foreach (var RepeatNode in RepeatNodes)
                            {
                                TodayNode.Children.Remove(RepeatNode);
                                WebTab.ThisPage.HistoryCollection.Remove(HistoryItem);
                                SQLite.Current.DeleteWebHistory(HistoryItem);
                            }
                        }
                    }

                    WebTab.ThisPage.HistoryCollection.Insert(0, new KeyValuePair<DateTime, WebSiteItem>(DateTime.Today, new WebSiteItem(WebBrowser.DocumentTitle, args.Uri.ToString())));
                }
            }
        }

        private void WebBrowser_NewWindowRequested(WebView sender, WebViewNewWindowRequestedEventArgs args)
        {
            WebPage Web = new WebPage(args.Uri);

            //TabViewItem的Header必须设置否则将导致异常发生
            TabViewItem NewItem = new TabViewItem
            {
                Header = Globalization.Language == LanguageEnum.Chinese ? "空白页" : "Blank Page",
                Icon = new SymbolIcon(Symbol.Document),
                Content = Web
            };
            Web.ThisTab = NewItem;

            WebTab.ThisPage.TabCollection.Add(NewItem);
            WebTab.ThisPage.TabControl.SelectedItem = NewItem;

            //设置此标志以阻止打开外部浏览器
            args.Handled = true;
        }

        /// <summary>
        /// 从baidu搜索建议获取建议的Json字符串
        /// </summary>
        /// <param name="Context">搜索的内容</param>
        /// <returns>Json</returns>
        private async Task<string> GetBaiduJsonFromWeb(string Context)
        {
            string url = "http://suggestion.baidu.com/su?wd=" + Context + "&cb=window.baidu.sug";
            try
            {
                HttpWebRequest Request = WebRequest.CreateHttp(new Uri(url));
                Stream ResponseStream = (await Request.GetResponseAsync()).GetResponseStream();
                using (StreamReader Reader = new StreamReader(ResponseStream, Encoding.GetEncoding("GBK")))
                {
                    string Result = await Reader.ReadToEndAsync();
                    Result = Result.Remove(0, 17);
                    Result = Result.Remove(Result.Length - 2, 2);
                    return Result;
                }
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }

        private async Task<string> GetBingJsonFromWeb(string Context)
        {
            string url = "http://api.bing.com/qsonhs.aspx?type=cb&q=" + Context + "&cb=window.bing.sug";
            try
            {
                HttpWebRequest Request = WebRequest.CreateHttp(new Uri(url));
                Stream ResponseStream = (await Request.GetResponseAsync()).GetResponseStream();
                using (StreamReader Reader = new StreamReader(ResponseStream, Encoding.UTF8))
                {
                    string Result = await Reader.ReadToEndAsync();
                    int firstindex = Result.IndexOf("{");
                    int lastindex = Result.LastIndexOf("}");
                    Result = Result.Substring(firstindex, lastindex - firstindex + 1);
                    return Result;
                }
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }

        private async Task<List<string>> GetGoogleSearchResponse(string Context)
        {
            string url = "http://suggestqueries.google.com/complete/search?client=youtube&q=" + Context + "&jsonp=window.google.ac.h";
            try
            {
                HttpWebRequest Request = WebRequest.CreateHttp(new Uri(url));
                Stream ResponseStream = (await Request.GetResponseAsync()).GetResponseStream();
                using (StreamReader Reader = new StreamReader(ResponseStream, Encoding.UTF8))
                {
                    string str = await Reader.ReadToEndAsync();
                    return str.Remove(str.LastIndexOf("{") - 2)
                              .Substring(str.IndexOf(",") + 2)
                              .Split(",")
                              .Where((Item) => Item.StartsWith("["))
                              .Select((item) => item.Replace("\"", string.Empty).Substring(1))
                              .ToList();
                }
            }
            catch (Exception)
            {
                return null;
            }
        }

        private void AutoSuggestBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            lock (SyncRootProvider.SyncRoot)
            {
                if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput && !string.IsNullOrEmpty(sender.Text))
                {
                    SuggestionTimer.Stop();
                    SuggestionTimer.Start();
                }
            }
        }

        private void AutoSuggestBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
        {
            SuggestionTimer.Stop();

            if (args.ChosenSuggestion != null)
            {
                switch (SearchEngine.SelectedItem.ToString())
                {
                    case "百度":
                    case "Baidu":
                        {
                            WebBrowser.Navigate(new Uri("https://www.baidu.com/s?wd=" + args.ChosenSuggestion.ToString()));
                            break;
                        }
                    case "谷歌":
                    case "Google":
                        {
                            WebBrowser.Navigate(new Uri("https://www.google.com/search?q=" + args.ChosenSuggestion.ToString()));
                            break;
                        }
                    case "必应":
                    case "Bing":
                        {
                            WebBrowser.Navigate(new Uri("https://www.bing.com/search?q=" + args.ChosenSuggestion.ToString()));
                            break;
                        }
                }
            }
            else
            {
                string EnterUrl = args.QueryText;

                if ((EnterUrl.Contains("/") && IPAddress.TryParse(string.Concat(EnterUrl.Take(EnterUrl.IndexOf("/"))), out _)) || (!EnterUrl.Contains("/") && IPAddress.TryParse(EnterUrl, out _)))
                {
                    EnterUrl = "http://" + EnterUrl;
                }
                else if (EnterUrl.StartsWith("http://"))
                {
                    string Sub = EnterUrl.Substring(7);
                    if ((!Sub.Contains("/") || !IPAddress.TryParse(string.Concat(Sub.Take(Sub.IndexOf("/"))), out _)) && (Sub.Contains("/") || !IPAddress.TryParse(Sub, out _)) && !Sub.StartsWith("www."))
                    {
                        EnterUrl = EnterUrl.Insert(7, "www.");
                    }
                }
                else if (EnterUrl.StartsWith("https://"))
                {
                    string Sub = EnterUrl.Substring(8);
                    if ((!Sub.Contains("/") || !IPAddress.TryParse(string.Concat(Sub.Take(Sub.IndexOf("/"))), out _)) && (Sub.Contains("/") || !IPAddress.TryParse(Sub, out _)) && !Sub.StartsWith("www."))
                    {
                        EnterUrl = EnterUrl.Insert(8, "www.");
                    }
                }

                if (Uri.TryCreate(EnterUrl, UriKind.Absolute, out Uri NormalUrl))
                {
                    WebBrowser.Navigate(NormalUrl);
                }
                else if (EnterUrl.Contains(".") && Uri.CheckHostName(EnterUrl) == UriHostNameType.Dns)
                {
                    if (EnterUrl.StartsWith("www."))
                    {
                        WebBrowser.Navigate(new Uri("http://" + EnterUrl));
                    }
                    else
                    {
                        WebBrowser.Navigate(new Uri("http://www." + EnterUrl));
                    }
                }
                else
                {
                    switch (SearchEngine.SelectedItem.ToString())
                    {
                        case "百度":
                        case "Baidu":
                            {
                                WebBrowser.Navigate(new Uri("https://www.baidu.com/s?wd=" + args.QueryText));
                                break;
                            }
                        case "谷歌":
                        case "Google":
                            {
                                WebBrowser.Navigate(new Uri("https://www.google.com/search?q=" + args.QueryText));
                                break;
                            }
                        case "必应":
                        case "Bing":
                            {
                                WebBrowser.Navigate(new Uri("https://www.bing.com/search?q=" + args.QueryText));
                                break;
                            }
                    }
                }
            }
        }

        private void Back_Click(object sender, RoutedEventArgs e)
        {
            WebBrowser.GoBack();
            if (WebTab.ThisPage.FavouriteDictionary.ContainsKey(AutoSuggest.Text))
            {
                Favourite.Symbol = Symbol.SolidStar;
                Favourite.Foreground = new SolidColorBrush(Colors.Gold);
                IsPressedFavourite = true;
            }
            else
            {
                Favourite.Symbol = Symbol.OutlineStar;
                Favourite.Foreground = new SolidColorBrush(Colors.White);
                IsPressedFavourite = false;
            }
        }

        private void Forward_Click(object sender, RoutedEventArgs e)
        {
            WebBrowser.GoForward();
            if (WebTab.ThisPage.FavouriteDictionary.ContainsKey(AutoSuggest.Text))
            {
                Favourite.Symbol = Symbol.SolidStar;
                Favourite.Foreground = new SolidColorBrush(Colors.Gold);
                IsPressedFavourite = true;
            }
            else
            {
                Favourite.Symbol = Symbol.OutlineStar;
                Favourite.Foreground = new SolidColorBrush(Colors.White);
                IsPressedFavourite = false;
            }
        }

        private async void Home_Click(object sender, RoutedEventArgs e)
        {
            string HomeString = ApplicationData.Current.LocalSettings.Values["WebTabMainPage"].ToString();

            if (Uri.TryCreate(HomeString, UriKind.Absolute, out Uri uri))
            {
                WebBrowser.Navigate(uri);
            }
            else
            {
                StorageFile HtmlFile = await StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///WebErrorStaticPage/index.html"));
                string HtmlContext = await FileIO.ReadTextAsync(HtmlFile);

                if (Globalization.Language == LanguageEnum.Chinese)
                {
                    HtmlContext = HtmlContext.Replace("@PrimaryTip", "抱歉，您访问的页面出错了")
                                             .Replace("@SecondTip", "可能是该网页已删除或不存在或网络故障")
                                             .Replace("@ThirdTip", "您可以尝试以下方案")
                                             .Replace("@HomeButtonText", "返回主页")
                                             .Replace("@Title", "错误：导航失败")
                                             .Replace("@DiagnoseButtonText", "网络诊断");

                    WebBrowser.NavigateToString(HtmlContext.Replace("@HomePageLink", "about:blank"));

                }
                else
                {
                    HtmlContext = HtmlContext.Replace("@PrimaryTip", "Sorry, the page you visited is not found")
                                             .Replace("@SecondTip", "It may be that the page has been deleted or does not exist or the network is down")
                                             .Replace("@ThirdTip", "You can try the following options")
                                             .Replace("@HomeButtonText", "Go to home page")
                                             .Replace("@Title", "Error: Page not found")
                                             .Replace("@DiagnoseButtonText", "Network diagnosis");

                    WebBrowser.NavigateToString(HtmlContext.Replace("@HomePageLink", "about:blank"));
                }

            }
        }

        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            IsRefresh = true;
            if (CanCancelLoading)
            {
                WebBrowser.Stop();
                RefreshState.Symbol = Symbol.Refresh;
                ProGrid.Width = new GridLength(8);
                Progress.IsActive = false;
                CanCancelLoading = false;
            }
            else
            {
                WebBrowser.Refresh();
            }
        }

        private void WebBrowser_NavigationCompleted(WebView sender, WebViewNavigationCompletedEventArgs args)
        {
            if (ThisTab.Header.ToString() == "正在加载..." || ThisTab.Header.ToString() == "Loading...")
            {
                ThisTab.Header = string.IsNullOrEmpty(WebBrowser.DocumentTitle) ? (Globalization.Language == LanguageEnum.Chinese ? "空白页" : "Blank Page") : WebBrowser.DocumentTitle;
            }

            if (InPrivate.IsOn)
            {
                IsRefresh = false;
                goto FLAG;
            }

            //处理仅刷新的情况。点击刷新时将不会触发ContentLoading事件，因此需要单独处理
            if (IsRefresh)
            {
                lock (SyncRootProvider.SyncRoot)
                {
                    if (AutoSuggest.Text != "about:blank" && !string.IsNullOrEmpty(WebBrowser.DocumentTitle))
                    {
                        var HistoryItems = WebTab.ThisPage.HistoryCollection.Where((Item) => Item.Key == DateTime.Today && Item.Value.WebSite == args.Uri.ToString()).ToList();

                        foreach (var HistoryItem in HistoryItems)
                        {
                            if (!HistoryItem.Key.Equals(default))
                            {
                                TreeViewNode TodayNode = null;
                                if (Globalization.Language == LanguageEnum.Chinese)
                                {
                                    TodayNode = HistoryTree.RootNodes.Where((Node) => (Node.Content as WebSiteItem).Subject == "今天").FirstOrDefault();
                                }
                                else
                                {
                                    TodayNode = HistoryTree.RootNodes.Where((Node) => (Node.Content as WebSiteItem).Subject == "Today").FirstOrDefault();
                                }

                                var RepeatNodes = TodayNode.Children.Where((Item) => (Item.Content as WebSiteItem).WebSite == HistoryItem.Value.WebSite).ToList();
                                foreach (var RepeatNode in RepeatNodes)
                                {
                                    TodayNode.Children.Remove(RepeatNode);
                                    WebTab.ThisPage.HistoryCollection.Remove(HistoryItem);
                                    SQLite.Current.DeleteWebHistory(HistoryItem);
                                }
                            }
                        }

                        WebTab.ThisPage.HistoryCollection.Insert(0, new KeyValuePair<DateTime, WebSiteItem>(DateTime.Today, new WebSiteItem(WebBrowser.DocumentTitle, args.Uri.ToString())));
                    }
                }
                IsRefresh = false;
            }


        FLAG:
            RefreshState.Symbol = Symbol.Refresh;
            ProGrid.Width = new GridLength(8);
            Progress.IsActive = false;
            CanCancelLoading = false;
        }

        private void WebBrowser_NavigationStarting(WebView sender, WebViewNavigationStartingEventArgs args)
        {
            ProGrid.Width = new GridLength(40);
            Progress.IsActive = true;
            CanCancelLoading = true;
            RefreshState.Symbol = Symbol.Cancel;
            if (args.Uri != null && AutoSuggest.Text != args.Uri.ToString())
            {
                AutoSuggest.Text = args.Uri.ToString();
            }
        }

        private async void WebBrowser_UnsafeContentWarningDisplaying(WebView sender, object args)
        {
            if (Globalization.Language == LanguageEnum.Chinese)
            {
                QueueContentDialog dialog = new QueueContentDialog
                {
                    Content = "SmartScreen将该页面标记为不安全",
                    Title = "警告",
                    CloseButtonText = "继续访问",
                    PrimaryButtonText = "返回主页"
                };
                if (await dialog.ShowAsync() == ContentDialogResult.Primary)
                {
                    WebBrowser.Navigate(new Uri(ApplicationData.Current.LocalSettings.Values["WebTabMainPage"].ToString()));
                }
            }
            else
            {
                QueueContentDialog dialog = new QueueContentDialog
                {
                    Content = "SmartScreen marks the page as unsafe",
                    Title = "Warning",
                    CloseButtonText = "Ignore anyway",
                    PrimaryButtonText = "Back to homepage"
                };
                if (await dialog.ShowAsync() == ContentDialogResult.Primary)
                {
                    WebBrowser.Navigate(new Uri(ApplicationData.Current.LocalSettings.Values["WebTabMainPage"].ToString()));
                }
            }
        }

        private void WebBrowser_ContainsFullScreenElementChanged(WebView sender, object args)
        {
            var applicationView = ApplicationView.GetForCurrentView();

            if (sender.ContainsFullScreenElement)
            {
                applicationView.TryEnterFullScreenMode();
            }
            else if (applicationView.IsFullScreenMode)
            {
                applicationView.ExitFullScreenMode();
            }
        }

        private void ShowPermissionToast()
        {
            if (Globalization.Language == LanguageEnum.Chinese)
            {
                ToastContent Content = new ToastContent()
                {
                    Scenario = ToastScenario.Reminder,

                    Visual = new ToastVisual()
                    {
                        BindingGeneric = new ToastBindingGeneric()
                        {
                            Children =
                            {
                                new AdaptiveText()
                                {
                                    Text = "请授予RX权限..."
                                },

                                new AdaptiveText()
                                {
                                    Text = "点击相应的权限"
                                },

                                new AdaptiveText()
                                {
                                    Text = "随后点击下方的\"我已授权\""
                                }
                            }
                        }
                    },

                    Actions = new ToastActionsCustom
                    {
                        Buttons =
                        {
                            new ToastButton("我已授权","Permission")
                            {
                                ActivationType =ToastActivationType.Foreground
                            },
                            new ToastButtonDismiss("拒绝授权")
                        }
                    }
                };

                var Notification = new ToastNotification(Content.GetXml());
                Notification.Activated += Notification_Activated;
                ToastNotificationManager.CreateToastNotifier().Show(Notification);
            }
            else
            {
                ToastContent Content = new ToastContent()
                {
                    Scenario = ToastScenario.Reminder,

                    Visual = new ToastVisual()
                    {
                        BindingGeneric = new ToastBindingGeneric()
                        {
                            Children =
                            {
                                new AdaptiveText()
                                {
                                    Text = "Please grant RX permission..."
                                },

                                new AdaptiveText()
                                {
                                    Text = "Click the appropriate permission"
                                },

                                new AdaptiveText()
                                {
                                    Text = "Then click \"I have authorized\" below"
                                }
                            }
                        }
                    },

                    Actions = new ToastActionsCustom
                    {
                        Buttons =
                        {
                            new ToastButton("I have authorized","Permission")
                            {
                                ActivationType =ToastActivationType.Foreground
                            },
                            new ToastButtonDismiss("Refuse authorization")
                        }
                    }
                };

                var Notification = new ToastNotification(Content.GetXml());
                Notification.Activated += Notification_Activated;
                ToastNotificationManager.CreateToastNotifier().Show(Notification);
            }
        }

        private void Notification_Activated(ToastNotification sender, object args)
        {
            sender.Activated -= Notification_Activated;
            if (args is ToastActivatedEventArgs e && e.Arguments != "Permission")
            {
                UserDenyOnToast = true;
            }
            else
            {
                UserDenyOnToast = false;
            }
            PermissionLocker.Set();
        }

        private async void WebBrowser_PermissionRequested(WebView sender, WebViewPermissionRequestedEventArgs args)
        {
            args.PermissionRequest.Defer();

            if (IsPermissionProcessing)
            {
                return;
            }

            IsPermissionProcessing = true;

            foreach (var Permission in WebBrowser.DeferredPermissionRequests)
            {
                switch (Permission.PermissionType)
                {
                    case WebViewPermissionType.Geolocation:
                        {
                            QueueContentDialog dialog;
                            if (Globalization.Language == LanguageEnum.Chinese)
                            {
                                dialog = new QueueContentDialog
                                {
                                    Content = "此网站正在请求您的精确GPS定位",
                                    Title = "权限",
                                    SecondaryButtonText = "拒绝",
                                    PrimaryButtonText = "允许"
                                };
                            }
                            else
                            {
                                dialog = new QueueContentDialog
                                {
                                    Content = "This site is requesting your precise GPS location",
                                    Title = "Permission",
                                    SecondaryButtonText = "Deny",
                                    PrimaryButtonText = "Allow"
                                };
                            }
                            switch (await dialog.ShowAsync())
                            {
                                case ContentDialogResult.Primary:
                                    {
                                    Location:
                                        if (await Geolocator.RequestAccessAsync() == GeolocationAccessStatus.Allowed)
                                        {
                                            Permission.Allow();
                                        }
                                        else
                                        {
                                            if (UserDenyOnToast)
                                            {
                                                Permission.Deny();
                                                UserDenyOnToast = false;
                                                break;
                                            }

                                            QueueContentDialog LocationTips;
                                            if (Globalization.Language == LanguageEnum.Chinese)
                                            {
                                                LocationTips = new QueueContentDialog
                                                {
                                                    Title = "警告",
                                                    Content = "如果您拒绝授予RX文件管理器定位权限，则此网站亦无法获得您的精确位置",
                                                    PrimaryButtonText = "授予权限",
                                                    SecondaryButtonText = "仍然拒绝"
                                                };
                                            }
                                            else
                                            {
                                                LocationTips = new QueueContentDialog
                                                {
                                                    Title = "Warning",
                                                    Content = "If you refuse to grant RX File Manager targeting, this site will not be able to get your exact location",
                                                    PrimaryButtonText = "Permission",
                                                    SecondaryButtonText = "Deny"
                                                };
                                            }
                                            if (await LocationTips.ShowAsync() == ContentDialogResult.Primary)
                                            {
                                                await Launcher.LaunchUriAsync(new Uri("ms-settings:appsfeatures-app"));
                                                ShowPermissionToast();
                                                await Task.Run(() =>
                                                {
                                                    PermissionLocker.WaitOne();
                                                });
                                                goto Location;
                                            }
                                            else
                                            {
                                                Permission.Deny();
                                            }
                                        }
                                        break;
                                    }
                                case ContentDialogResult.Secondary:
                                    {
                                        Permission.Deny();
                                        break;
                                    }
                            }
                            break;
                        }

                    case WebViewPermissionType.WebNotifications:
                        {
                            QueueContentDialog dialog;
                            if (Globalization.Language == LanguageEnum.Chinese)
                            {
                                dialog = new QueueContentDialog
                                {
                                    Content = "此网站正在请求Web通知权限",
                                    Title = "权限",
                                    SecondaryButtonText = "拒绝",
                                    PrimaryButtonText = "允许"
                                };
                            }
                            else
                            {
                                dialog = new QueueContentDialog
                                {
                                    Content = "This site is requesting web notification permission",
                                    Title = "Permission",
                                    SecondaryButtonText = "Deny",
                                    PrimaryButtonText = "Allow"
                                };
                            }

                            switch (await dialog.ShowAsync())
                            {
                                case ContentDialogResult.Primary:
                                    Permission.Allow();
                                    break;
                                case ContentDialogResult.Secondary:
                                    Permission.Deny();
                                    break;
                            }
                            break;
                        }
                    case WebViewPermissionType.Media:
                        {
                            QueueContentDialog dialog;
                            if (Globalization.Language == LanguageEnum.Chinese)
                            {
                                dialog = new QueueContentDialog
                                {
                                    Content = "此网站正在请求音视频权限",
                                    Title = "权限",
                                    SecondaryButtonText = "拒绝",
                                    PrimaryButtonText = "允许"
                                };
                            }
                            else
                            {
                                dialog = new QueueContentDialog
                                {
                                    Content = "This site is requesting media playback permission",
                                    Title = "Permission",
                                    SecondaryButtonText = "Deny",
                                    PrimaryButtonText = "Allow"
                                };
                            }
                            switch (await dialog.ShowAsync())
                            {
                                case ContentDialogResult.Primary:
                                    {
                                    Location:
                                        MediaCapture Capture = null;
                                        try
                                        {
                                            if (UserDenyOnToast)
                                            {
                                                Permission.Deny();
                                                UserDenyOnToast = false;
                                                break;
                                            }

                                            var Setting = new MediaCaptureInitializationSettings
                                            {
                                                AlwaysPlaySystemShutterSound = false,
                                                AudioProcessing = Windows.Media.AudioProcessing.Default,
                                                MediaCategory = MediaCategory.Media,
                                                SharingMode = MediaCaptureSharingMode.SharedReadOnly,
                                                MemoryPreference = MediaCaptureMemoryPreference.Auto,
                                                StreamingCaptureMode = StreamingCaptureMode.AudioAndVideo
                                            };

                                            Capture = new MediaCapture();
                                            await Capture.InitializeAsync(Setting);

                                            Permission.Allow();
                                        }
                                        catch (UnauthorizedAccessException)
                                        {
                                            QueueContentDialog LocationTips;
                                            if (Globalization.Language == LanguageEnum.Chinese)
                                            {
                                                LocationTips = new QueueContentDialog
                                                {
                                                    Title = "警告",
                                                    Content = "如果您拒绝授予RX文件管理器音视频权限，则此网站亦无法获得您的音视频流",
                                                    PrimaryButtonText = "授予权限",
                                                    SecondaryButtonText = "仍然拒绝"
                                                };
                                            }
                                            else
                                            {
                                                LocationTips = new QueueContentDialog
                                                {
                                                    Title = "Warning",
                                                    Content = "If you refuse to grant RX File Manager audio and video permissions, the site will not be able to get your audio and video streams",
                                                    PrimaryButtonText = "Permission",
                                                    SecondaryButtonText = "Deny"
                                                };
                                            }

                                            if (await LocationTips.ShowAsync() == ContentDialogResult.Primary)
                                            {
                                                await Launcher.LaunchUriAsync(new Uri("ms-settings:appsfeatures-app"));
                                                ShowPermissionToast();
                                                await Task.Run(() =>
                                                {
                                                    PermissionLocker.WaitOne();
                                                });
                                                goto Location;
                                            }
                                            else
                                            {
                                                Permission.Deny();
                                            }
                                        }
                                        finally
                                        {
                                            await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                                            {
                                                Capture.Dispose();
                                            });
                                        }
                                        break;
                                    }
                                case ContentDialogResult.Secondary:
                                    {
                                        Permission.Deny();
                                        break;
                                    }
                            }
                            break;
                        }
                    case WebViewPermissionType.Screen:
                        {
                            QueueContentDialog dialog;
                            if (Globalization.Language == LanguageEnum.Chinese)
                            {
                                dialog = new QueueContentDialog
                                {
                                    Content = "此网站正在请求屏幕录制权限",
                                    Title = "权限",
                                    CloseButtonText = "拒绝",
                                    PrimaryButtonText = "允许"
                                };
                            }
                            else
                            {
                                dialog = new QueueContentDialog
                                {
                                    Content = "This site is requesting screen recording permission",
                                    Title = "Permission",
                                    CloseButtonText = "Deny",
                                    PrimaryButtonText = "Allow"
                                };
                            }
                            switch (await dialog.ShowAsync())
                            {
                                case ContentDialogResult.Primary:
                                    {
                                        Permission.Allow();
                                        break;
                                    }
                                case ContentDialogResult.Secondary:
                                    {
                                        Permission.Deny();
                                        break;
                                    }
                            }
                            break;
                        }
                    case WebViewPermissionType.UnlimitedIndexedDBQuota:
                        {
                            QueueContentDialog dialog;
                            if (Globalization.Language == LanguageEnum.Chinese)
                            {
                                dialog = new QueueContentDialog
                                {
                                    Content = "此网站正在请求无限制数据存储",
                                    Title = "权限",
                                    SecondaryButtonText = "拒绝",
                                    PrimaryButtonText = "允许"
                                };
                            }
                            else
                            {
                                dialog = new QueueContentDialog
                                {
                                    Content = "This site is requesting unlimited data storage",
                                    Title = "Permission",
                                    SecondaryButtonText = "Deny",
                                    PrimaryButtonText = "Allow"
                                };
                            }
                            switch (await dialog.ShowAsync())
                            {
                                case ContentDialogResult.Primary:
                                    {
                                        if (!WebBrowser.Settings.IsIndexedDBEnabled)
                                        {
                                            WebBrowser.Settings.IsIndexedDBEnabled = true;
                                        }
                                        Permission.Allow();
                                        break;
                                    }
                                case ContentDialogResult.Secondary:
                                    {
                                        Permission.Deny();
                                        break;
                                    }
                            }
                            break;
                        }
                    case WebViewPermissionType.PointerLock:
                        {
                            QueueContentDialog dialog;
                            if (Globalization.Language == LanguageEnum.Chinese)
                            {
                                dialog = new QueueContentDialog
                                {
                                    Content = "此网站正在请求鼠标指针锁定",
                                    Title = "权限",
                                    SecondaryButtonText = "拒绝",
                                    PrimaryButtonText = "允许"
                                };
                            }
                            else
                            {
                                dialog = new QueueContentDialog
                                {
                                    Content = "This site is requesting a mouse pointer lock",
                                    Title = "Permission",
                                    SecondaryButtonText = "Deny",
                                    PrimaryButtonText = "Allow"
                                };
                            }
                            switch (await dialog.ShowAsync())
                            {
                                case ContentDialogResult.Primary:
                                    {
                                        Permission.Allow();
                                        break;
                                    }
                                case ContentDialogResult.Secondary:
                                    {
                                        Permission.Deny();
                                        break;
                                    }
                            }
                            break;
                        }
                    case WebViewPermissionType.ImmersiveView:
                        {
                            QueueContentDialog dialog;
                            if (Globalization.Language == LanguageEnum.Chinese)
                            {
                                dialog = new QueueContentDialog
                                {
                                    Content = "此网站正在请求沉浸式视图模式(VR)",
                                    Title = "权限",
                                    SecondaryButtonText = "拒绝",
                                    PrimaryButtonText = "允许"
                                };
                            }
                            else
                            {
                                dialog = new QueueContentDialog
                                {
                                    Content = "This site is requesting immersive view mode (VR)",
                                    Title = "Permission",
                                    SecondaryButtonText = "Deny",
                                    PrimaryButtonText = "Allow"
                                };
                            }
                            switch (await dialog.ShowAsync())
                            {
                                case ContentDialogResult.Primary:
                                    {
                                        Permission.Allow();
                                        break;
                                    }
                                case ContentDialogResult.Secondary:
                                    {
                                        Permission.Deny();
                                        break;
                                    }
                            }
                            break;
                        }
                }
            }

            IsPermissionProcessing = false;
        }

        private async void ScreenShot_Click(object sender, RoutedEventArgs e)
        {
            if ((await (await BluetoothAdapter.GetDefaultAsync()).GetRadioAsync()).State != RadioState.On)
            {
                if (Globalization.Language == LanguageEnum.Chinese)
                {
                    QueueContentDialog dialog = new QueueContentDialog
                    {
                        Content = "蓝牙功能尚未开启，是否前往设置开启？",
                        Title = "提示",
                        PrimaryButtonText = "确定",
                        CloseButtonText = "取消"
                    };
                    if ((await dialog.ShowAsync()) == ContentDialogResult.Primary)
                    {
                        await Launcher.LaunchUriAsync(new Uri("ms-settings:bluetooth"));
                    }
                }
                else
                {
                    QueueContentDialog dialog = new QueueContentDialog
                    {
                        Content = "Bluetooth is not turned on, go to setting to enable？",
                        Title = "Tips",
                        PrimaryButtonText = "Confirm",
                        CloseButtonText = "Cancel"
                    };
                    if ((await dialog.ShowAsync()) == ContentDialogResult.Primary)
                    {
                        await Launcher.LaunchUriAsync(new Uri("ms-settings:bluetooth"));
                    }
                }
                return;
            }
            IRandomAccessStream stream = new InMemoryRandomAccessStream();
            await WebBrowser.CapturePreviewToStreamAsync(stream);

            BluetoothUI Bluetooth = new BluetoothUI();

            var result = await Bluetooth.ShowAsync();
            if (result == ContentDialogResult.Secondary)
            {
                Bluetooth = null;
                stream.Dispose();
                return;
            }
            else if (result == ContentDialogResult.Primary)
            {
                await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, async () =>
                {
                    BluetoothFileTransfer FileTransfer = new BluetoothFileTransfer
                    {
                        StreamToSend = stream.AsStream(),
                        FileName = string.IsNullOrEmpty(WebBrowser.DocumentTitle) ? (Globalization.Language == LanguageEnum.Chinese ? "屏幕截图.jpg" : "Screenshot.jpg") : (WebBrowser.DocumentTitle + ".jpg"),
                        UseStorageFileRatherThanStream = false
                    };
                    await FileTransfer.ShowAsync();
                });
            }
        }

        private async void ClearCache_Click(object sender, RoutedEventArgs e)
        {
            TipsFly.Hide();

            await WebView.ClearTemporaryWebDataAsync();
            await SQLite.Current.ClearTableAsync("WebHistory");
            WebTab.ThisPage.HistoryCollection.Clear();
            HistoryTree.RootNodes.Clear();

            if (Globalization.Language == LanguageEnum.Chinese)
            {
                QueueContentDialog dialog = new QueueContentDialog
                {
                    Content = "所有缓存和历史记录数据均已清空",
                    Title = "提示",
                    CloseButtonText = "确定",
                    Background = Application.Current.Resources["DialogAcrylicBrush"] as Brush
                };
                _ = await dialog.ShowAsync();
            }
            else
            {
                QueueContentDialog dialog = new QueueContentDialog
                {
                    Content = "All cache and history data has being cleared",
                    Title = "Tips",
                    CloseButtonText = "Confirm"
                };
                _ = await dialog.ShowAsync();
            }
        }

        private async void About_Click(object sender, RoutedEventArgs e)
        {
            if (Globalization.Language == LanguageEnum.Chinese)
            {
                QueueContentDialog dialog = new QueueContentDialog
                {
                    Content = "RX管理器内置浏览器\r\r具备SmartScreen保护和完整权限控制\r\r基于Microsoft Edge内核的轻型浏览器",
                    Title = "关于",
                    CloseButtonText = "确定"
                };
                _ = await dialog.ShowAsync();
            }
            else
            {
                QueueContentDialog dialog = new QueueContentDialog
                {
                    Content = "RX Manager built-in browser\r\rSmartScreen protection and full access control\r\rLightweight browser based on Microsoft Edge kernel",
                    Title = "About",
                    CloseButtonText = "Confirm"
                };
                _ = await dialog.ShowAsync();
            }
        }

        public async void Dispose()
        {
            if (WebBrowser != null)
            {
                await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                {
                    WebBrowser.NewWindowRequested -= WebBrowser_NewWindowRequested;
                    WebBrowser.ContentLoading -= WebBrowser_ContentLoading;
                    WebBrowser.NavigationCompleted -= WebBrowser_NavigationCompleted;
                    WebBrowser.NavigationStarting -= WebBrowser_NavigationStarting;
                    WebBrowser.UnsafeContentWarningDisplaying -= WebBrowser_UnsafeContentWarningDisplaying;
                    WebBrowser.ContainsFullScreenElementChanged -= WebBrowser_ContainsFullScreenElementChanged;
                    WebBrowser.PermissionRequested -= WebBrowser_PermissionRequested;
                    WebBrowser.SeparateProcessLost -= WebBrowser_SeparateProcessLost;
                    WebBrowser.NavigationFailed -= WebBrowser_NavigationFailed;
                    WebBrowser.LongRunningScriptDetected -= WebBrowser_LongRunningScriptDetected;
                    WebBrowser = null;
                });
            }
            PermissionLocker.Dispose();
            PermissionLocker = null;
            ThisTab = null;
            InPrivate.Toggled -= InPrivate_Toggled;
            WebDownloader.DownloadList.CollectionChanged -= DownloadList_CollectionChanged;
        }

        private void FavoutiteListButton_Click(object sender, RoutedEventArgs e)
        {
            SplitControl.IsPaneOpen = !SplitControl.IsPaneOpen;
        }

        private void Favourite_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            if (IsPressedFavourite)
            {
                return;
            }
            Favourite.Foreground = new SolidColorBrush(Colors.Gold);
        }

        private void Favourite_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            if (IsPressedFavourite)
            {
                return;
            }
            Favourite.Foreground = new SolidColorBrush(Colors.White);
        }

        private async void Favourite_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (ThisTab.Header.ToString() == "空白页" || ThisTab.Header.ToString() == "Blank Page")
            {
                return;
            }

            if (Favourite.Symbol == Symbol.SolidStar)
            {
                IsPressedFavourite = false;
                Favourite.Symbol = Symbol.OutlineStar;
                Favourite.Foreground = new SolidColorBrush(Colors.White);

                if (WebTab.ThisPage.FavouriteDictionary.ContainsKey(AutoSuggest.Text))
                {
                    var FavItem = WebTab.ThisPage.FavouriteDictionary[AutoSuggest.Text];
                    WebTab.ThisPage.FavouriteCollection.Remove(FavItem);
                    WebTab.ThisPage.FavouriteDictionary.Remove(FavItem.WebSite);

                    await SQLite.Current.DeleteWebFavouriteListAsync(FavItem);
                }
            }
            else
            {
                FavName.Text = WebBrowser.DocumentTitle;
                FavName.SelectAll();
                FlyoutBase.ShowAttachedFlyout((FrameworkElement)sender);
            }
        }

        private void Setting_Click(object sender, RoutedEventArgs e)
        {
            SettingControl.IsPaneOpen = !SettingControl.IsPaneOpen;
        }

        private void TabOpenMethod_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ApplicationData.Current.LocalSettings.Values["WebTabOpenMethod"] = TabOpenMethod.SelectedItem.ToString();
            SpecificUrl.Visibility = (TabOpenMethod.SelectedItem.ToString() == "特定页" || TabOpenMethod.SelectedItem.ToString() == "Specific Page") ? Visibility.Visible : Visibility.Collapsed;
        }

        private void ShowMainButton_Toggled(object sender, RoutedEventArgs e)
        {
            ApplicationData.Current.LocalSettings.Values["WebShowMainButton"] = ShowMainButton.IsOn;
            if (ShowMainButton.IsOn)
            {
                Home.Visibility = Visibility.Visible;
                HomeGrid.Width = new GridLength(50);
            }
            else
            {
                Home.Visibility = Visibility.Collapsed;
                HomeGrid.Width = new GridLength(0);
            }
        }

        private void AllowJS_Toggled(object sender, RoutedEventArgs e)
        {
            if (WebBrowser == null)
            {
                return;
            }
            ApplicationData.Current.LocalSettings.Values["WebEnableJS"] = AllowJS.IsOn;
            WebBrowser.Settings.IsJavaScriptEnabled = AllowJS.IsOn;
        }

        private void AllowIndexedDB_Toggled(object sender, RoutedEventArgs e)
        {
            if (WebBrowser == null)
            {
                return;
            }
            ApplicationData.Current.LocalSettings.Values["WebEnableDB"] = AllowIndexedDB.IsOn;
            WebBrowser.Settings.IsIndexedDBEnabled = AllowIndexedDB.IsOn;
        }

        private void SettingControl_PaneClosed(SplitView sender, object args)
        {
            //设置面板关闭时保存所有设置内容

            if (string.IsNullOrWhiteSpace(MainUrl.Text))
            {
                ApplicationData.Current.LocalSettings.Values["WebTabMainPage"] = "about:blank";
            }
            else
            {
                if (MainUrl.Text.StartsWith("http"))
                {
                    ApplicationData.Current.LocalSettings.Values["WebTabMainPage"] = MainUrl.Text;
                }
                else
                {
                    ApplicationData.Current.LocalSettings.Values["WebTabMainPage"] = "http://" + MainUrl.Text;
                }
            }

            if (string.IsNullOrWhiteSpace(SpecificUrl.Text))
            {
                ApplicationData.Current.LocalSettings.Values["WebTabSpecifiedPage"] = "about:blank";
            }
            else
            {
                if (SpecificUrl.Text == "about:blank")
                {
                    ApplicationData.Current.LocalSettings.Values["WebTabSpecifiedPage"] = SpecificUrl.Text;
                    return;
                }
                if (SpecificUrl.Text.StartsWith("http"))
                {
                    ApplicationData.Current.LocalSettings.Values["WebTabSpecifiedPage"] = SpecificUrl.Text;
                }
                else
                {
                    ApplicationData.Current.LocalSettings.Values["WebTabSpecifiedPage"] = "http://" + SpecificUrl.Text;
                }
            }

        }

        private async void SaveConfirm_Click(object sender, RoutedEventArgs e)
        {
            Fly.Hide();

            IsPressedFavourite = true;
            Favourite.Symbol = Symbol.SolidStar;
            Favourite.Foreground = new SolidColorBrush(Colors.Gold);

            if (!WebTab.ThisPage.FavouriteDictionary.ContainsKey(AutoSuggest.Text))
            {
                var FavItem = new WebSiteItem(FavName.Text, AutoSuggest.Text);
                WebTab.ThisPage.FavouriteCollection.Add(FavItem);
                WebTab.ThisPage.FavouriteDictionary.Add(AutoSuggest.Text, FavItem);

                await SQLite.Current.SetWebFavouriteListAsync(FavItem);
            }

        }

        private void FavName_TextChanged(object sender, TextChangedEventArgs e)
        {
            SaveConfirm.IsEnabled = !string.IsNullOrWhiteSpace(FavName.Text);
        }

        private void SaveCancel_Click(object sender, RoutedEventArgs e)
        {
            Fly.Hide();
        }

        private void FavouriteList_RightTapped(object sender, RightTappedRoutedEventArgs e)
        {
            var Context = (e.OriginalSource as FrameworkElement)?.DataContext as WebSiteItem;
            FavouriteList.SelectedItem = Context;
        }

        private void FavouriteList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Delete.IsEnabled = FavouriteList.SelectedIndex != -1;
        }

        private async void Delete_Click(object sender, RoutedEventArgs e)
        {
            var FavItem = FavouriteList.SelectedItem as WebSiteItem;
            if (AutoSuggest.Text == FavItem.WebSite)
            {
                Favourite.Symbol = Symbol.OutlineStar;
                Favourite.Foreground = new SolidColorBrush(Colors.White);
                IsPressedFavourite = false;
            }

            WebTab.ThisPage.FavouriteCollection.Remove(FavItem);
            WebTab.ThisPage.FavouriteDictionary.Remove(FavItem.WebSite);

            await SQLite.Current.DeleteWebFavouriteListAsync(FavItem);
        }

        private void FavouriteList_ItemClick(object sender, ItemClickEventArgs e)
        {
            WebBrowser.Navigate(new Uri((e.ClickedItem as WebSiteItem).WebSite));
        }

        private void HistoryTree_ItemInvoked(TreeView sender, TreeViewItemInvokedEventArgs args)
        {
            var WebItem = (args.InvokedItem as TreeViewNode).Content as WebSiteItem;
            WebBrowser.Navigate(new Uri(WebItem.WebSite));
        }

        private void ConfirmButton_Click(object sender, RoutedEventArgs e)
        {
            InPrivateNotification.Dismiss();
        }

        private void ExitButton_Click(object sender, RoutedEventArgs e)
        {
            InPrivate.IsOn = false;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            TipsFly.Hide();
        }

        private void SettingControl_PaneOpening(SplitView sender, object args)
        {
            Scroll.ChangeView(null, 0, null);
        }

        private void TextBlock_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            FlyoutBase.ShowAttachedFlyout((FrameworkElement)sender);
        }

        private async void ClearData_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button).Name == "ClearFav")
            {
                ClearFavFly.Hide();
                await SQLite.Current.ClearTableAsync("WebFavourite");

                foreach (var Web in from Tab in WebTab.ThisPage.TabCollection
                                    let Web = Tab.Content as WebPage
                                    where WebTab.ThisPage.FavouriteDictionary.ContainsKey(Web.AutoSuggest.Text)
                                    select Web)
                {
                    Web.Favourite.Symbol = Symbol.OutlineStar;
                    Web.Favourite.Foreground = new SolidColorBrush(Colors.White);
                    Web.IsPressedFavourite = false;
                }

                WebTab.ThisPage.FavouriteCollection.Clear();
                WebTab.ThisPage.FavouriteDictionary.Clear();
            }
            else
            {
                ClearHistoryFly.Hide();
                await SQLite.Current.ClearTableAsync("WebHistory");
                WebTab.ThisPage.HistoryCollection.Clear();
                HistoryTree.RootNodes.Clear();
            }
        }

        private void CancelClear_Click(object sender, RoutedEventArgs e)
        {
            ClearFavFly.Hide();
            ClearHistoryFly.Hide();
        }

        private void DownloadListButton_Click(object sender, RoutedEventArgs e)
        {
            DownloadControl.IsPaneOpen = !DownloadControl.IsPaneOpen;
        }

        private void PauseDownloadButton_Click(object sender, RoutedEventArgs e)
        {
            Button btn = (Button)sender;
            DownloadList.SelectedItem = btn.DataContext;

            if (btn.Content.ToString() == "暂停" || btn.Content.ToString() == "Pause")
            {
                ListViewItem Item = DownloadList.ContainerFromItem(DownloadList.SelectedItem) as ListViewItem;
                Item.ContentTemplate = DownloadPauseTemplate;

                WebDownloader.DownloadList[DownloadList.SelectedIndex].PauseDownload();
            }
            else
            {
                ListViewItem Item = DownloadList.ContainerFromItem(DownloadList.SelectedItem) as ListViewItem;
                Item.ContentTemplate = DownloadingTemplate;

                WebDownloader.DownloadList[DownloadList.SelectedIndex].ResumeDownload();
            }
        }

        private void StopDownloadButton_Click(object sender, RoutedEventArgs e)
        {
            DownloadList.SelectedItem = ((Button)sender).DataContext;

            ListViewItem Item = DownloadList.ContainerFromItem(DownloadList.SelectedItem) as ListViewItem;
            Item.ContentTemplate = DownloadCancelTemplate;

            var Operation = WebDownloader.DownloadList[DownloadList.SelectedIndex];
            Operation.StopDownload();
        }

        private async void SetDownloadPathButton_Click(object sender, RoutedEventArgs e)
        {
            FolderPicker SavePicker = new FolderPicker
            {
                SuggestedStartLocation = PickerLocationId.Downloads,
                ViewMode = PickerViewMode.List
            };
            SavePicker.FileTypeFilter.Add("*");
            StorageFolder SaveFolder = await SavePicker.PickSingleFolderAsync();

            if (SaveFolder != null)
            {
                DownloadPath.Text = SaveFolder.Path;
                StorageApplicationPermissions.FutureAccessList.AddOrReplace("DownloadPath", SaveFolder);
            }
        }

        private void CloseDownloadItemButton_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            ((SymbolIcon)sender).Foreground = new SolidColorBrush(Colors.OrangeRed);
        }

        private void CloseDownloadItemButton_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            ((SymbolIcon)sender).Foreground = new SolidColorBrush(Colors.White);
        }

        private async void CloseDownloadItemButton_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            DownloadList.SelectedItem = ((SymbolIcon)sender).DataContext;

            var Operation = WebDownloader.DownloadList[DownloadList.SelectedIndex];
            if (Operation.State == DownloadState.Downloading || Operation.State == DownloadState.Paused)
            {
                Operation.StopDownload();
            }

            WebDownloader.DownloadList.RemoveAt(DownloadList.SelectedIndex);

            await SQLite.Current.DeleteDownloadHistoryAsync(Operation);
        }

        private void SearchEngine_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ApplicationData.Current.LocalSettings.Values["WebSearchEngine"] = SearchEngine.SelectedItem.ToString();
        }

        private async void DownloadList_ItemClick(object sender, ItemClickEventArgs e)
        {
            if ((e.ClickedItem as DownloadOperator).State == DownloadState.AlreadyFinished)
            {
                StorageFolder Folder = await StorageApplicationPermissions.FutureAccessList.GetItemAsync("DownloadPath") as StorageFolder;
                _ = await Launcher.LaunchFolderAsync(Folder);
            }
        }

        private void DownloadList_RightTapped(object sender, RightTappedRoutedEventArgs e)
        {
            if ((e.OriginalSource as FrameworkElement)?.DataContext is DownloadOperator Context)
            {
                DownloadList.SelectedIndex = WebDownloader.DownloadList.IndexOf(Context);
            }
        }

        private void CopyDownloadLink_Click(object sender, RoutedEventArgs e)
        {
            DataPackage Package = new DataPackage();
            Package.SetText((DownloadList.SelectedItem as DownloadOperator).Address.AbsoluteUri);
            Clipboard.SetContent(Package);
        }

        private async void OpenDownloadLocation_Click(object sender, RoutedEventArgs e)
        {
            StorageFolder Folder = await StorageApplicationPermissions.FutureAccessList.GetItemAsync("DownloadPath") as StorageFolder;
            _ = await Launcher.LaunchFolderAsync(Folder);
        }
    }
}
