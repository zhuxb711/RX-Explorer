using Microsoft.Toolkit.Uwp.UI.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace FileManager
{
    public sealed partial class WebTab : Page
    {
        public static WebTab ThisPage { get; set; }
        public ObservableCollection<TabViewItem> TabCollection = new ObservableCollection<TabViewItem>();
        public ObservableCollection<WebSiteItem> FavouriteCollection;
        public Dictionary<string, WebSiteItem> FavouriteDictionary;
        public ObservableCollection<KeyValuePair<DateTime, WebSiteItem>> HistoryCollection;
        public HistoryTreeCategoryFlag HistoryFlag;
        private WebPage CurrentWebPage;
        private bool IsClosing = false;

        public WebTab()
        {
            InitializeComponent();
            ThisPage = this;
            OnFirstLoad();
        }

        private async void OnFirstLoad()
        {
            await SQLite.Current.GetDownloadHistoryAsync();

            var FavList = await SQLite.Current.GetWebFavouriteListAsync();

            if (FavList.Count > 0)
            {
                FavouriteCollection = new ObservableCollection<WebSiteItem>(FavList);
                FavouriteDictionary = new Dictionary<string, WebSiteItem>();

                foreach (var Item in FavList)
                {
                    FavouriteDictionary.Add(Item.WebSite, Item);
                }
            }
            else
            {
                FavouriteCollection = new ObservableCollection<WebSiteItem>();
                FavouriteDictionary = new Dictionary<string, WebSiteItem>();
            }

            var HistoryList = await SQLite.Current.GetWebHistoryListAsync();

            if (HistoryList.Count > 0)
            {
                HistoryCollection = new ObservableCollection<KeyValuePair<DateTime, WebSiteItem>>(HistoryList);
                bool ExistToday = false, ExistYesterday = false, ExistEarlier = false;
                foreach (var HistoryItem in HistoryCollection)
                {
                    if (HistoryItem.Key == DateTime.Today.AddDays(-1))
                    {
                        ExistYesterday = true;
                    }
                    else if (HistoryItem.Key == DateTime.Today)
                    {
                        ExistToday = true;
                    }
                    else
                    {
                        ExistEarlier = true;
                    }
                }

                if (ExistYesterday && ExistToday && ExistEarlier)
                {
                    HistoryFlag = HistoryTreeCategoryFlag.Today | HistoryTreeCategoryFlag.Yesterday | HistoryTreeCategoryFlag.Earlier;
                }
                else if (!ExistYesterday && ExistToday && ExistEarlier)
                {
                    HistoryFlag = HistoryTreeCategoryFlag.Today | HistoryTreeCategoryFlag.Earlier;
                }
                else if (ExistYesterday && !ExistToday && ExistEarlier)
                {
                    HistoryFlag = HistoryTreeCategoryFlag.Yesterday | HistoryTreeCategoryFlag.Earlier;
                }
                else if (ExistYesterday && ExistToday && !ExistEarlier)
                {
                    HistoryFlag = HistoryTreeCategoryFlag.Today | HistoryTreeCategoryFlag.Yesterday;
                }
                else if (!ExistYesterday && !ExistToday && ExistEarlier)
                {
                    HistoryFlag = HistoryTreeCategoryFlag.Earlier;
                }
                else if (!ExistYesterday && ExistToday && !ExistEarlier)
                {
                    HistoryFlag = HistoryTreeCategoryFlag.Today;
                }
                else if (ExistYesterday && !ExistToday && !ExistEarlier)
                {
                    HistoryFlag = HistoryTreeCategoryFlag.Yesterday;
                }

            }
            else
            {
                HistoryCollection = new ObservableCollection<KeyValuePair<DateTime, WebSiteItem>>();
                HistoryFlag = HistoryTreeCategoryFlag.None;
            }

            TabControl.ItemsSource = TabCollection;

            FavouriteCollection.CollectionChanged += (s, e) =>
            {
                CurrentWebPage.FavEmptyTips.Visibility = FavouriteCollection.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            };

            HistoryCollection.CollectionChanged += (s, e) =>
            {
                CurrentWebPage.HistoryEmptyTips.Visibility = HistoryCollection.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

                lock (SyncRootProvider.SyncRoot)
                {
                    if (e.Action == NotifyCollectionChangedAction.Add)
                    {
                        var TreeNode = from Item in CurrentWebPage.HistoryTree.RootNodes
                                       let Subject = Globalization.Language == LanguageEnum.Chinese ? "今天" : "Today"
                                       where (Item.Content as WebSiteItem).Subject == Subject
                                       select Item;
                        if (TreeNode.Count() == 0)
                        {
                            CurrentWebPage.HistoryTree.RootNodes.Insert(0, new TreeViewNode
                            {
                                Content = new WebSiteItem(Globalization.Language == LanguageEnum.Chinese ? "今天" : "Today", string.Empty),
                                HasUnrealizedChildren = true,
                                IsExpanded = true
                            });
                            HistoryFlag = HistoryTreeCategoryFlag.Today;
                            foreach (KeyValuePair<DateTime, WebSiteItem> New in e.NewItems)
                            {
                                CurrentWebPage.HistoryTree.RootNodes[0].Children.Insert(0, new TreeViewNode
                                {
                                    Content = New.Value,
                                    HasUnrealizedChildren = false,
                                    IsExpanded = false
                                });

                                SQLite.Current.SetWebHistoryList(New);
                            }

                        }
                        else
                        {
                            foreach (KeyValuePair<DateTime, WebSiteItem> New in e.NewItems)
                            {
                                TreeNode.First().Children.Insert(0, new TreeViewNode
                                {
                                    Content = New.Value,
                                    HasUnrealizedChildren = false,
                                    IsExpanded = false
                                });
                                SQLite.Current.SetWebHistoryList(New);
                            }
                        }
                    }
                }
            };

            switch (ApplicationData.Current.LocalSettings.Values["WebTabOpenMethod"]?.ToString() ?? (Globalization.Language == LanguageEnum.Chinese ? "空白页" : "Blank Page"))
            {
                case "空白页":
                case "Blank Page":
                    CreateNewTab(new Uri("about:blank"));
                    break;
                case "主页":
                    {
                        string TabMain = ApplicationData.Current.LocalSettings.Values["WebTabMainPage"].ToString();
                        if (Uri.TryCreate(TabMain, UriKind.Absolute, out Uri uri))
                        {
                            CreateNewTab(uri);
                        }
                        else
                        {
                            QueueContentDialog dialog = new QueueContentDialog
                            {
                                Content = "导航失败，请检查网址或网络连接",
                                Title = "提示",
                                CloseButtonText = "确定"
                            };
                            _ = dialog.ShowAsync();
                            CreateNewTab(new Uri("about:blank"));
                        }
                        break;
                    }
                case "Home Page":
                    {
                        string TabMain = ApplicationData.Current.LocalSettings.Values["WebTabMainPage"].ToString();
                        if (Uri.TryCreate(TabMain, UriKind.Absolute, out Uri uri))
                        {
                            CreateNewTab(uri);
                        }
                        else
                        {
                            QueueContentDialog dialog = new QueueContentDialog
                            {
                                Content = "Navigation failed, please check the URL or network connection",
                                Title = "Tips",
                                CloseButtonText = "Confirm"
                            };
                            _ = dialog.ShowAsync();
                            CreateNewTab(new Uri("about:blank"));
                        }
                        break;
                    }
                case "特定页":
                    {
                        string SpecifiedPage = ApplicationData.Current.LocalSettings.Values["WebTabSpecifiedPage"].ToString();
                        if (Uri.TryCreate(SpecifiedPage, UriKind.Absolute, out Uri uri1))
                        {
                            CreateNewTab(uri1);
                        }
                        else
                        {
                            QueueContentDialog dialog = new QueueContentDialog
                            {
                                Content = "导航失败，请检查网址或网络连接",
                                Title = "提示",
                                CloseButtonText = "确定"
                            };
                            _ = dialog.ShowAsync();
                            CreateNewTab(new Uri("about:blank"));
                        }
                        break;
                    }
                case "Specific Page":
                    {
                        string SpecifiedPage = ApplicationData.Current.LocalSettings.Values["WebTabSpecifiedPage"].ToString();
                        if (Uri.TryCreate(SpecifiedPage, UriKind.Absolute, out Uri uri1))
                        {
                            CreateNewTab(uri1);
                        }
                        else
                        {
                            QueueContentDialog dialog = new QueueContentDialog
                            {
                                Content = "Navigation failed, please check the URL or network connection",
                                Title = "Tips",
                                CloseButtonText = "Confirm"
                            };
                            _ = dialog.ShowAsync();
                            CreateNewTab(new Uri("about:blank"));
                        }
                        break;
                    }
            }
        }

        /// <summary>
        /// 创建新的WebTab标签页
        /// </summary>
        /// <param name="uri">导航网址</param>
        private void CreateNewTab(Uri uri = null)
        {
            lock (SyncRootProvider.SyncRoot)
            {
                WebPage Web = new WebPage(uri);
                TabViewItem CurrentItem = new TabViewItem
                {
                    Header = Globalization.Language == LanguageEnum.Chinese ? "空白页" : "Blank Page",
                    Icon = new SymbolIcon(Symbol.Document),
                    Content = Web
                };
                Web.ThisTab = CurrentItem;
                TabCollection.Add(CurrentItem);
                TabControl.SelectedItem = CurrentItem;
            }
        }


        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            if (e.Parameter is Uri uri)
            {
                CreateNewTab(uri);
            }
        }

        private void AddTabButtonUpper_Click(object sender, RoutedEventArgs e)
        {
            switch (ApplicationData.Current.LocalSettings.Values["WebTabOpenMethod"].ToString())
            {
                case "空白页":
                case "Blank Page":
                    CreateNewTab(new Uri("about:blank"));
                    break;
                case "主页":
                    {
                        string TabMain = ApplicationData.Current.LocalSettings.Values["WebTabMainPage"].ToString();
                        if (Uri.TryCreate(TabMain, UriKind.Absolute, out Uri uri))
                        {
                            CreateNewTab(uri);
                        }
                        else
                        {
                            QueueContentDialog dialog = new QueueContentDialog
                            {
                                Content = "导航失败，请检查网址或网络连接",
                                Title = "提示",
                                CloseButtonText = "确定"
                            };
                            _ = dialog.ShowAsync();
                            CreateNewTab(new Uri("about:blank"));
                        }
                        break;
                    }
                case "Home Page":
                    {
                        string TabMain = ApplicationData.Current.LocalSettings.Values["WebTabMainPage"].ToString();
                        if (Uri.TryCreate(TabMain, UriKind.Absolute, out Uri uri))
                        {
                            CreateNewTab(uri);
                        }
                        else
                        {
                            QueueContentDialog dialog = new QueueContentDialog
                            {
                                Content = "Navigation failed, please check the URL or network connection",
                                Title = "Tips",
                                CloseButtonText = "Confirm"
                            };
                            _ = dialog.ShowAsync();
                            CreateNewTab(new Uri("about:blank"));
                        }
                        break;
                    }
                case "特定页":
                    {
                        string SpecifiedPage = ApplicationData.Current.LocalSettings.Values["WebTabSpecifiedPage"].ToString();
                        if (Uri.TryCreate(SpecifiedPage, UriKind.Absolute, out Uri uri1))
                        {
                            CreateNewTab(uri1);
                        }
                        else
                        {
                            QueueContentDialog dialog = new QueueContentDialog
                            {
                                Content = "导航失败，请检查网址或网络连接",
                                Title = "提示",
                                CloseButtonText = "确定"
                            };
                            _ = dialog.ShowAsync();
                            CreateNewTab(new Uri("about:blank"));
                        }
                        break;
                    }
                case "Specific Page":
                    {
                        string SpecifiedPage = ApplicationData.Current.LocalSettings.Values["WebTabSpecifiedPage"].ToString();
                        if (Uri.TryCreate(SpecifiedPage, UriKind.Absolute, out Uri uri1))
                        {
                            CreateNewTab(uri1);
                        }
                        else
                        {
                            QueueContentDialog dialog = new QueueContentDialog
                            {
                                Content = "Navigation failed, please check the URL or network connection",
                                Title = "Tips",
                                CloseButtonText = "Confirm"
                            };
                            _ = dialog.ShowAsync();
                            CreateNewTab(new Uri("about:blank"));
                        }
                        break;
                    }
            }

        }

        private async void TabControl_TabClosing(object sender, TabClosingEventArgs e)
        {
            lock (SyncRootProvider.SyncRoot)
            {
                if (IsClosing)
                {
                    e.Cancel = true;
                    return;
                }
                IsClosing = true;
            }

            if (TabCollection.Count == 1)
            {
                e.Cancel = true;
                CreateNewTab(new Uri("about:blank"));
                TabControl.SelectedIndex = 1;
                await Task.Delay(900);
                TabCollection.Remove(e.Tab);
            }

            (e.Tab.Content as WebPage)?.Dispose();
            e.Tab.Content = null;

            IsClosing = false;
        }

        private void TabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (TabControl.SelectedIndex == -1)
            {
                return;
            }

            CurrentWebPage = (TabControl.SelectedItem as TabViewItem).Content as WebPage;

            if (CurrentWebPage.PivotControl.SelectedIndex != 0)
            {
                CurrentWebPage.PivotControl.SelectedIndex = 0;
            }

            if (FavouriteDictionary.ContainsKey(CurrentWebPage.AutoSuggest.Text))
            {
                CurrentWebPage.Favourite.Symbol = Symbol.SolidStar;
                CurrentWebPage.Favourite.Foreground = new SolidColorBrush(Colors.Gold);
                CurrentWebPage.IsPressedFavourite = true;
            }
            else
            {
                CurrentWebPage.Favourite.Symbol = Symbol.OutlineStar;
                CurrentWebPage.Favourite.Foreground = new SolidColorBrush(Colors.White);
                CurrentWebPage.IsPressedFavourite = false;
            }
        }
    }
}
