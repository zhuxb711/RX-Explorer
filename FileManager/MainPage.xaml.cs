using AnimationEffectProvider;
using SQLConnectionPoolProvider;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Background;
using Windows.ApplicationModel.Core;
using Windows.Devices.Enumeration;
using Windows.Foundation;
using Windows.Services.Store;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.System;
using Windows.UI.Shell;
using Windows.UI.StartScreen;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Navigation;

namespace FileManager
{
    public sealed partial class MainPage : Page
    {
        public static MainPage ThisPage { get; private set; }

        private Dictionary<Type, string> PageDictionary;

        public bool IsUSBActivate { get; set; } = false;

        public string ActivateUSBDevicePath { get; private set; }

        private EntranceAnimationEffect EntranceEffectProvider;

        private DeviceWatcher PortalDeviceWatcher;

        public string LastPageName { get; private set; }

        public MainPage()
        {
            InitializeComponent();
            ThisPage = this;
            Window.Current.SetTitleBar(TitleBar);
            Loaded += MainPage_Loaded;
            Application.Current.Resuming += Current_Resuming;
            Application.Current.Suspending += Current_Suspending;
        }

        private void Current_Suspending(object sender, SuspendingEventArgs e)
        {
            PortalDeviceWatcher?.Stop();
        }

        private void Current_Resuming(object sender, object e)
        {
            PortalDeviceWatcher.Start();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            if (e.Parameter is Tuple<string, Rect> Parameter)
            {
                string[] Paras = Parameter.Item1.Split("||");
                if (Paras[0] == "USBActivate")
                {
                    IsUSBActivate = true;
                    ActivateUSBDevicePath = Paras[1];
                }
                EntranceEffectProvider = new EntranceAnimationEffect(this, Nav, Parameter.Item2);
                EntranceEffectProvider.PrepareEntranceEffect();
            }
            else if (e.Parameter is Rect SplashRect)
            {
                EntranceEffectProvider = new EntranceAnimationEffect(this, Nav, SplashRect);
                EntranceEffectProvider.PrepareEntranceEffect();
            }
        }

        private async void MainPage_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                PortalDeviceWatcher = DeviceInformation.CreateWatcher(DeviceClass.PortableStorageDevice);
                PortalDeviceWatcher.Added += PortalDeviceWatcher_Added;
                PortalDeviceWatcher.Removed += PortalDeviceWatcher_Removed;
                PortalDeviceWatcher.Start();

                if (!(ApplicationData.Current.LocalSettings.Values["UIDisplayMode"] is string Mode))
                {
                    ApplicationData.Current.LocalSettings.Values["UIDisplayMode"] = Globalization.Language == LanguageEnum.Chinese
                                ? "推荐"
                                : "Recommand";
                }

                if (ApplicationData.Current.LocalSettings.Values["IsDoubleClickEnable"] is bool IsDoubleClick)
                {
                    SettingPage.IsDoubleClickEnable = IsDoubleClick;
                }
                else
                {
                    ApplicationData.Current.LocalSettings.Values["IsDoubleClickEnable"] = true;
                }

                if (Globalization.Language == LanguageEnum.Chinese)
                {
                    PageDictionary = new Dictionary<Type, string>()
                {
                    {typeof(WebTab), "浏览器"},
                    {typeof(ThisPC),"这台电脑" },
                    {typeof(FileControl),"这台电脑" },
                    {typeof(AboutMe),"这台电脑" },
                    {typeof(SecureArea),"安全域" }
                };
                }
                else
                {
                    PageDictionary = new Dictionary<Type, string>()
                {
                    {typeof(WebTab), "Browser"},
                    {typeof(ThisPC),"ThisPC" },
                    {typeof(FileControl),"ThisPC" },
                    {typeof(AboutMe),"ThisPC" },
                    {typeof(SecureArea),"Security Area" }
                };
                }

                Nav.Navigate(typeof(ThisPC));

                EntranceEffectProvider.AnimationCompleted += async(s, t) =>
                {
                    (await MySQL.Current.GetConnectionFromPoolAsync()).Dispose();
                };

                EntranceEffectProvider.StartEntranceEffect();

                var PictureUri = await SQLite.Current.GetBackgroundPictureAsync();
                var FileList = await (await ApplicationData.Current.LocalFolder.CreateFolderAsync("CustomImageFolder", CreationCollisionOption.OpenIfExists)).GetFilesAsync();
                foreach (var ToDeletePicture in FileList.Where((File) => PictureUri.All((ImageUri) => ImageUri.ToString().Replace("ms-appdata:///local/CustomImageFolder/", string.Empty) != File.Name)))
                {
                    await ToDeletePicture.DeleteAsync(StorageDeleteOption.PermanentDelete);
                }

                if (ApplicationData.Current.LocalSettings.Values["LastRunVersion"] is string Version)
                {
                    var VersionSplit = Version.Split(".").Select((Item) => ushort.Parse(Item));
                    if (VersionSplit.ElementAt(0) < Package.Current.Id.Version.Major || VersionSplit.ElementAt(1) < Package.Current.Id.Version.Minor || VersionSplit.ElementAt(2) < Package.Current.Id.Version.Build || VersionSplit.ElementAt(3) < Package.Current.Id.Version.Revision)
                    {
                        WhatIsNew Dialog = new WhatIsNew();
                        await Task.Delay(2000);
                        _ = await Dialog.ShowAsync();

                        ApplicationData.Current.LocalSettings.Values["LastRunVersion"] = string.Format("{0}.{1}.{2}.{3}", Package.Current.Id.Version.Major, Package.Current.Id.Version.Minor, Package.Current.Id.Version.Build, Package.Current.Id.Version.Revision);
                    }
                }
                else
                {
                    ApplicationData.Current.LocalSettings.Values["LastRunVersion"] = string.Format("{0}.{1}.{2}.{3}", Package.Current.Id.Version.Major, Package.Current.Id.Version.Minor, Package.Current.Id.Version.Build, Package.Current.Id.Version.Revision);
                    WhatIsNew Dialog = new WhatIsNew();
                    await Task.Delay(2000);
                    _ = await Dialog.ShowAsync();
                }

                await RegisterBackgroundTask();

                await DonateDeveloper();

                await Task.Delay(10000);

                await PinApplicationToTaskBar();

            }
            catch (Exception ex)
            {
                ExceptionTracer.RequestBlueScreen(ex);
            }
        }

        private async Task RegisterBackgroundTask()
        {
            switch (await BackgroundExecutionManager.RequestAccessAsync())
            {
                case BackgroundAccessStatus.AllowedSubjectToSystemPolicy:
                case BackgroundAccessStatus.AlwaysAllowed:
                    {
                        if (BackgroundTaskRegistration.AllTasks.Select((item) => item.Value).FirstOrDefault((task) => task.Name == "UpdateTask") is IBackgroundTaskRegistration Registration)
                        {
                            Registration.Unregister(true);
                        }

                        SystemTrigger Trigger = new SystemTrigger(SystemTriggerType.SessionConnected, false);
                        BackgroundTaskBuilder Builder = new BackgroundTaskBuilder
                        {
                            Name = "UpdateTask",
                            IsNetworkRequested = true,
                            TaskEntryPoint = "UpdateCheckBackgroundTask.UpdateCheck"
                        };
                        Builder.SetTrigger(Trigger);
                        Builder.AddCondition(new SystemCondition(SystemConditionType.InternetAvailable));
                        Builder.AddCondition(new SystemCondition(SystemConditionType.UserPresent));
                        Builder.AddCondition(new SystemCondition(SystemConditionType.FreeNetworkAvailable));
                        Builder.Register();

                        break;
                    }
                default:
                    {
                        if (!ApplicationData.Current.LocalSettings.Values.ContainsKey("DisableBackgroundTaskTips"))
                        {
                            if (Globalization.Language == LanguageEnum.Chinese)
                            {
                                QueueContentDialog Dialog = new QueueContentDialog
                                {
                                    Title = "提示",
                                    Content = "后台任务被禁用，RX将无法在更新发布时及时通知您\r\r请手动开启后台任务权限",
                                    PrimaryButtonText = "现在开启",
                                    SecondaryButtonText = "稍后提醒",
                                    CloseButtonText = "不再提醒"
                                };
                                switch (await Dialog.ShowAsync())
                                {
                                    case ContentDialogResult.Primary:
                                        {
                                            _ = await Launcher.LaunchUriAsync(new Uri("ms-settings:appsfeatures-app"));
                                            break;
                                        }
                                    case ContentDialogResult.Secondary:
                                        {
                                            break;
                                        }
                                    default:
                                        {
                                            ApplicationData.Current.LocalSettings.Values["DisableBackgroundTaskTips"] = true;
                                            break;
                                        }
                                }
                            }
                            else
                            {
                                QueueContentDialog Dialog = new QueueContentDialog
                                {
                                    Title = "Tips",
                                    Content = "Background tasks are disabled, RX will not be able to notify you in time when the update is released \r \rPlease manually enable background task permissions",
                                    PrimaryButtonText = "Authorize now",
                                    SecondaryButtonText = "Remind later",
                                    CloseButtonText = "Never remind"
                                };
                                switch (await Dialog.ShowAsync())
                                {
                                    case ContentDialogResult.Primary:
                                        {
                                            _ = await Launcher.LaunchUriAsync(new Uri("ms-settings:appsfeatures-app"));
                                            break;
                                        }
                                    case ContentDialogResult.Secondary:
                                        {
                                            break;
                                        }
                                    default:
                                        {
                                            ApplicationData.Current.LocalSettings.Values["DisableBackgroundTaskTips"] = true;
                                            break;
                                        }
                                }
                            }
                        }
                        break;
                    }
            }
        }

        private async void PortalDeviceWatcher_Removed(DeviceWatcher sender, DeviceInformationUpdate args)
        {
            if (ThisPC.ThisPage == null)
            {
                return;
            }

            var CurrentDrives = DriveInfo.GetDrives().TakeWhile((Drives) => Drives.DriveType == DriveType.Fixed || Drives.DriveType == DriveType.Removable || Drives.DriveType == DriveType.Ram || Drives.DriveType == DriveType.Network)
                                                     .GroupBy((Item) => Item.Name)
                                                     .Select((Group) => Group.FirstOrDefault().Name);
            var RemovedDriveList = ThisPC.ThisPage.HardDeviceList.SkipWhile((RemoveItem) => CurrentDrives.Any((Item) => Item == RemoveItem.Folder.Path));

            await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                for (int i = 0; i < RemovedDriveList.Count(); i++)
                {
                    ThisPC.ThisPage.HardDeviceList.Remove(RemovedDriveList.ElementAt(i));
                }
            });
        }

        private async void PortalDeviceWatcher_Added(DeviceWatcher sender, DeviceInformation args)
        {
            if (ThisPC.ThisPage == null)
            {
                return;
            }

            var NewDriveAddedList = DriveInfo.GetDrives().TakeWhile((Drives) => Drives.DriveType == DriveType.Fixed || Drives.DriveType == DriveType.Removable || Drives.DriveType == DriveType.Ram || Drives.DriveType == DriveType.Network)
                                                         .GroupBy((Item) => Item.Name)
                                                         .Select((Group) => Group.FirstOrDefault().Name)
                                                         .SkipWhile((NewItem) => ThisPC.ThisPage.HardDeviceList.Any((Item) => Item.Folder.Path == NewItem));
            foreach (string DriveRootPath in NewDriveAddedList)
            {
                StorageFolder Device = await StorageFolder.GetFolderFromPathAsync(DriveRootPath);
                BasicProperties Properties = await Device.GetBasicPropertiesAsync();
                IDictionary<string, object> PropertiesRetrieve = await Properties.RetrievePropertiesAsync(new string[] { "System.Capacity", "System.FreeSpace" });

                await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, async () =>
                {
                    ThisPC.ThisPage.HardDeviceList.Add(new HardDeviceInfo(Device, await Device.GetThumbnailBitmapAsync(), PropertiesRetrieve));
                });
            }
        }

        private void Nav_Navigated(object sender, NavigationEventArgs e)
        {
            if (Nav.CurrentSourcePageType == typeof(ThisPC) || Nav.CurrentSourcePageType == typeof(WebTab) || Nav.CurrentSourcePageType == typeof(SecureArea) || Nav.CurrentSourcePageType == typeof(SettingPage))
            {
                NavView.IsBackEnabled = false;
            }
            else
            {
                NavView.IsBackEnabled = true;
            }

            if (Nav.SourcePageType == typeof(SettingPage) || Nav.SourcePageType == typeof(AboutMe))
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
            if (ApplicationData.Current.LocalSettings.Values.ContainsKey("IsPinToTaskBar"))
            {
                if (ApplicationData.Current.LocalSettings.Values.ContainsKey("IsRated"))
                {
                    return;
                }
                else
                {
                    await RequestRateApplication();
                    return;
                }
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
                    s.IsOpen = false;
                    _ = await BarManager.RequestPinCurrentAppAsync();
                    _ = await ScreenManager.RequestAddAppListEntryAsync(Entry);
                };
            }
            else if (PinStartScreen && !PinTaskBar)
            {
                PinTip.ActionButtonClick += async (s, e) =>
                {
                    s.IsOpen = false;
                    _ = await ScreenManager.RequestAddAppListEntryAsync(Entry);
                };
            }
            else if (!PinStartScreen && PinTaskBar)
            {
                PinTip.ActionButtonClick += async (s, e) =>
                {
                    s.IsOpen = false;
                    _ = await BarManager.RequestPinCurrentAppAsync();
                };
            }
            else
            {
                PinTip.ActionButtonClick += (s, e) =>
                {
                    s.IsOpen = false;
                };
            }

            PinTip.Closed += async (s, e) =>
            {
                s.IsOpen = false;
                await RequestRateApplication();
            };

            PinTip.Subtitle = Globalization.Language == LanguageEnum.Chinese
                ? "将RX文件管理器固定在和开始屏幕任务栏，启动更快更方便哦！\r\r★固定至开始菜单\r\r★固定至任务栏"
                : "Pin the RX FileManager to StartScreen and TaskBar ！\r\r★Pin to StartScreen\r\r★Pin to TaskBar";
            PinTip.IsOpen = true;
        }

        private async Task RequestRateApplication()
        {
            if (ApplicationData.Current.LocalSettings.Values.ContainsKey("IsRated"))
            {
                return;
            }
            else
            {
                await Task.Delay(60000);
                ApplicationData.Current.LocalSettings.Values["IsRated"] = true;
            }

            RateTip.ActionButtonClick += async (s, e) =>
            {
                s.IsOpen = false;
                await Launcher.LaunchUriAsync(new Uri("ms-windows-store://review/?productid=9N88QBQKF2RS"));
            };
            RateTip.IsOpen = true;
        }

        private async Task DonateDeveloper()
        {
            if (ApplicationData.Current.LocalSettings.Values["IsDonated"] is bool Donated)
            {
                if (Donated)
                {
                    await Task.Delay(30000);
                    DonateTip.ActionButtonClick += async (s, e) =>
                    {
                        s.IsOpen = false;

                        if (Globalization.Language == LanguageEnum.Chinese)
                        {
                            StoreContext Store = StoreContext.GetDefault();
                            StoreProductQueryResult StoreProductResult = await Store.GetAssociatedStoreProductsAsync(new string[] { "Durable" });
                            if (StoreProductResult.ExtendedError == null)
                            {
                                StoreProduct Product = StoreProductResult.Products.Values.FirstOrDefault();
                                if (Product != null)
                                {
                                    switch ((await Store.RequestPurchaseAsync(Product.StoreId)).Status)
                                    {
                                        case StorePurchaseStatus.Succeeded:
                                            {
                                                QueueContentDialog QueueContenDialog = new QueueContentDialog
                                                {
                                                    Title = "感谢",
                                                    Content = "感谢您的支持，我们将努力将RX做得越来越好q(≧▽≦q)\r\r" +
                                                               "RX文件管理器的诞生，是为了填补UWP文件管理器缺位的空白\r" +
                                                               "它并非是一个盈利项目，因此下载和使用都是免费的，并且不含有广告\r" +
                                                               "RX的目标是打造一个免费且功能全面文件管理器\r" +
                                                               "RX文件管理器是我利用业余时间开发的项目\r" +
                                                               "希望大家能够喜欢\r\r" +
                                                               "Ruofan,\r敬上",
                                                    CloseButtonText = "朕知道了"
                                                };
                                                _ = await QueueContenDialog.ShowAsync();
                                                break;
                                            }
                                        case StorePurchaseStatus.AlreadyPurchased:
                                            {
                                                QueueContentDialog QueueContenDialog = new QueueContentDialog
                                                {
                                                    Title = "再次感谢",
                                                    Content = "您已为RX支持过一次了，您的心意开发者已心领\r\r" +
                                                              "RX的初衷并非是赚钱，因此不可重复支持哦\r\r" +
                                                              "您可以向周围的人宣传一下RX，也是对RX的最好的支持哦（*＾-＾*）\r\r" +
                                                              "Ruofan,\r敬上",
                                                    CloseButtonText = "朕知道了"
                                                };
                                                _ = await QueueContenDialog.ShowAsync();
                                                break;
                                            }
                                        case StorePurchaseStatus.NotPurchased:
                                            {
                                                QueueContentDialog QueueContenDialog = new QueueContentDialog
                                                {
                                                    Title = "感谢",
                                                    Content = "无论支持与否，RX始终如一\r\r" +
                                                              "即使您最终决定放弃支持本项目，依然十分感谢您能够点进来看一看\r\r" +
                                                              "Ruofan,\r敬上",
                                                    CloseButtonText = "朕知道了"
                                                };
                                                _ = await QueueContenDialog.ShowAsync();
                                                break;
                                            }
                                        default:
                                            {
                                                QueueContentDialog QueueContenDialog = new QueueContentDialog
                                                {
                                                    Title = "抱歉",
                                                    Content = "由于Microsoft Store或网络原因，无法打开支持页面，请稍后再试",
                                                    CloseButtonText = "朕知道了"
                                                };
                                                _ = await QueueContenDialog.ShowAsync();
                                                break;
                                            }
                                    }
                                }
                            }
                            else
                            {
                                QueueContentDialog QueueContenDialog = new QueueContentDialog
                                {
                                    Title = "抱歉",
                                    Content = "由于Microsoft Store或网络原因，无法打开支持页面，请稍后再试",
                                    CloseButtonText = "朕知道了"
                                };
                                _ = await QueueContenDialog.ShowAsync();
                            }
                        }
                        else
                        {
                            StoreContext Store = StoreContext.GetDefault();
                            StoreProductQueryResult StoreProductResult = await Store.GetAssociatedStoreProductsAsync(new string[] { "Durable" });
                            if (StoreProductResult.ExtendedError == null)
                            {
                                StoreProduct Product = StoreProductResult.Products.Values.FirstOrDefault();
                                if (Product != null)
                                {
                                    switch ((await Store.RequestPurchaseAsync(Product.StoreId)).Status)
                                    {
                                        case StorePurchaseStatus.Succeeded:
                                            {
                                                QueueContentDialog QueueContenDialog = new QueueContentDialog
                                                {
                                                    Title = "Appreciation",
                                                    Content = "Thank you for your support, we will work hard to make RX better and better q(≧▽≦q)\r\r" +
                                                              "The RX file manager was born to fill the gaps in the UWP file manager\r" +
                                                              "This is not a profitable project, so downloading and using are free and do not include ads\r" +
                                                              "RX's goal is to create a free and full-featured file manager\r" +
                                                              "RX File Manager is a project I developed in my spare time\r" +
                                                              "I hope everyone likes\r\r" +
                                                              "Sincerely,\rRuofan",
                                                    CloseButtonText = "Got it"
                                                };
                                                _ = await QueueContenDialog.ShowAsync();
                                                break;
                                            }
                                        case StorePurchaseStatus.AlreadyPurchased:
                                            {
                                                QueueContentDialog QueueContenDialog = new QueueContentDialog
                                                {
                                                    Title = "Thanks again",
                                                    Content = "You have already supported RX once, thank you very much\r\r" +
                                                              "The original intention of RX is not to make money, so you can't repeat purchase it.\r\r" +
                                                              "You can advertise the RX to the people around you, and it is also the best support for RX（*＾-＾*）\r\r" +
                                                              "Sincerely,\rRuofan",
                                                    CloseButtonText = "Got it"
                                                };
                                                _ = await QueueContenDialog.ShowAsync();
                                                break;
                                            }
                                        case StorePurchaseStatus.NotPurchased:
                                            {
                                                QueueContentDialog QueueContenDialog = new QueueContentDialog
                                                {
                                                    Title = "Appreciation",
                                                    Content = "Whether supported or not, RX is always the same\r\r" +
                                                              "Even if you finally decide to give up supporting the project, thank you very much for being able to click to see it\r\r" +
                                                              "Sincerely,\rRuofan",
                                                    CloseButtonText = "Got it"
                                                };
                                                _ = await QueueContenDialog.ShowAsync();
                                                break;
                                            }
                                        default:
                                            {
                                                QueueContentDialog QueueContenDialog = new QueueContentDialog
                                                {
                                                    Title = "Sorry",
                                                    Content = "Unable to open support page due to Microsoft Store or network, please try again later",
                                                    CloseButtonText = "Got it"
                                                };
                                                _ = await QueueContenDialog.ShowAsync();
                                                break;
                                            }
                                    }
                                }
                            }
                            else
                            {
                                QueueContentDialog QueueContenDialog = new QueueContentDialog
                                {
                                    Title = "Sorry",
                                    Content = "Unable to open support page due to Microsoft Store or network, please try again later",
                                    CloseButtonText = "Got it"
                                };
                                _ = await QueueContenDialog.ShowAsync();
                            }
                        }
                    };

                    if (Globalization.Language == LanguageEnum.Chinese)
                    {
                        DonateTip.Subtitle = "开发者开发RX文件管理器花费了大量精力\r" +
                                             "🎉您可以自愿为开发者贡献一点小零花钱🎉\r\r" +
                                             "若您不愿意，则可以点击\"跪安\"以取消\r" +
                                             "若您愿意支持开发者，则可以点击\"准奏\"\r\r" +
                                             "Tips: 无论支持与否，RX文件管理器都将继续运行，且无任何功能限制";
                    }
                    else
                    {
                        DonateTip.Subtitle = "It takes a lot of effort for developers to develop RX file manager\r" +
                                             "🎉You can volunteer to contribute a little pocket money to developers.🎉\r\r" +
                                             "Please donate 0.99$ 🍪\r\r" +
                                             "If you don't want to, you can click \"Later\" to cancel\r" +
                                             "if you want to donate, you can click \"Donate\" to support developer\r\r" +
                                             "Tips: Whether donated or not, the RX File Manager will continue to run without any functional limitations";
                    }

                    DonateTip.IsOpen = true;
                    ApplicationData.Current.LocalSettings.Values["IsDonated"] = false;
                }
            }
            else
            {
                ApplicationData.Current.LocalSettings.Values["IsDonated"] = true;
            }
        }

        private void NavView_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
        {
            try
            {
                LastPageName = Nav.CurrentSourcePageType == null ? nameof(ThisPC) : Nav.CurrentSourcePageType.Name;

                if (args.IsSettingsInvoked)
                {
                    Nav.Navigate(typeof(SettingPage), null, new SlideNavigationTransitionInfo() { Effect = SlideNavigationTransitionEffect.FromRight });
                }
                else
                {
                    switch (args.InvokedItem.ToString())
                    {
                        case "这台电脑":
                        case "ThisPC":
                            {
                                Nav.Navigate(typeof(ThisPC), null, new SlideNavigationTransitionInfo() { Effect = SlideNavigationTransitionEffect.FromLeft });
                                break;
                            }
                        case "浏览器":
                        case "Browser":
                            {
                                if (LastPageName == nameof(SecureArea) || LastPageName == nameof(SettingPage))
                                {
                                    Nav.Navigate(typeof(WebTab), null, new SlideNavigationTransitionInfo() { Effect = SlideNavigationTransitionEffect.FromLeft });
                                }
                                else
                                {
                                    Nav.Navigate(typeof(WebTab), null, new SlideNavigationTransitionInfo() { Effect = SlideNavigationTransitionEffect.FromRight });
                                }
                                break;
                            }
                        case "安全域":
                        case "Security Area":
                            {
                                if (LastPageName == nameof(SettingPage))
                                {
                                    Nav.Navigate(typeof(SecureArea), null, new SlideNavigationTransitionInfo() { Effect = SlideNavigationTransitionEffect.FromLeft });
                                }
                                else
                                {
                                    Nav.Navigate(typeof(SecureArea), null, new SlideNavigationTransitionInfo() { Effect = SlideNavigationTransitionEffect.FromRight });
                                }
                                break;
                            }
                    }
                }
            }
            catch (Exception ex)
            {
                ExceptionTracer.RequestBlueScreen(ex);
            }
        }

        private void Nav_Navigating(object sender, NavigatingCancelEventArgs e)
        {
            if (Nav.CurrentSourcePageType == e.SourcePageType)
            {
                e.Cancel = true;
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
    }
}
