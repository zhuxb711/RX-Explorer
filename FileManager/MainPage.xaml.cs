using AnimationEffectProvider;
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
using Windows.Security.Cryptography;
using Windows.Security.Cryptography.Core;
using Windows.Services.Store;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.Storage.Streams;
using Windows.System;
using Windows.System.Profile;
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

        public DeviceWatcher PortalDeviceWatcher;

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
                        {typeof(ThisPC),"这台电脑" },
                        {typeof(FileControl),"这台电脑" },
                        {typeof(SecureArea),"安全域" }
                    };
                }
                else
                {
                    PageDictionary = new Dictionary<Type, string>()
                    {
                        {typeof(ThisPC),"ThisPC" },
                        {typeof(FileControl),"ThisPC" },
                        {typeof(SecureArea),"Security Area" }
                    };
                }

                Nav.Navigate(typeof(ThisPC));

                EntranceEffectProvider.StartEntranceEffect();

                var PictureUri = await SQLite.Current.GetBackgroundPictureAsync();
                var FileList = await (await ApplicationData.Current.LocalFolder.CreateFolderAsync("CustomImageFolder", CreationCollisionOption.OpenIfExists)).GetFilesAsync();
                foreach (var ToDeletePicture in FileList.Where((File) => PictureUri.All((ImageUri) => ImageUri.ToString().Replace("ms-appdata:///local/CustomImageFolder/", string.Empty) != File.Name)))
                {
                    await ToDeletePicture.DeleteAsync(StorageDeleteOption.PermanentDelete);
                }

                PortalDeviceWatcher = DeviceInformation.CreateWatcher(DeviceClass.PortableStorageDevice);
                PortalDeviceWatcher.Added += PortalDeviceWatcher_Added;
                PortalDeviceWatcher.Removed += PortalDeviceWatcher_Removed;

                await GetUserInfoAsync();

                await ShowReleaseLogDialogAsync();

                await RegisterBackgroundTaskAsync();

                await DonateDeveloperAsync();

                await Task.Delay(10000);

                await PinApplicationToTaskBarAsync();

            }
            catch (Exception ex)
            {
                ExceptionTracer.RequestBlueScreen(ex);
            }
        }

        private async Task ShowReleaseLogDialogAsync()
        {
            if (Microsoft.Toolkit.Uwp.Helpers.SystemInformation.IsAppUpdated || Microsoft.Toolkit.Uwp.Helpers.SystemInformation.IsFirstRun)
            {
                WhatIsNew Dialog = new WhatIsNew();
                _ = await Dialog.ShowAsync();
            }
        }

        private async Task GetUserInfoAsync()
        {
            if ((await User.FindAllAsync()).Where(p => p.AuthenticationStatus == UserAuthenticationStatus.LocallyAuthenticated && p.Type == UserType.LocalUser).FirstOrDefault() is User CurrentUser)
            {
                string UserName = (await CurrentUser.GetPropertyAsync(KnownUserProperties.FirstName))?.ToString();
                string UserID = (await CurrentUser.GetPropertyAsync(KnownUserProperties.AccountName))?.ToString();
                if (string.IsNullOrEmpty(UserID))
                {
                    var Token = HardwareIdentification.GetPackageSpecificToken(null);
                    HashAlgorithmProvider md5 = HashAlgorithmProvider.OpenAlgorithm(HashAlgorithmNames.Md5);
                    IBuffer hashedData = md5.HashData(Token.Id);
                    UserID = CryptographicBuffer.EncodeToHexString(hashedData).ToUpper();
                }

                if (string.IsNullOrEmpty(UserName))
                {
                    UserName = UserID.Substring(0, 10);
                }

                ApplicationData.Current.LocalSettings.Values["SystemUserName"] = UserName;
                ApplicationData.Current.LocalSettings.Values["SystemUserID"] = UserID;
            }
            else
            {
                var Token = HardwareIdentification.GetPackageSpecificToken(null);
                HashAlgorithmProvider md5 = HashAlgorithmProvider.OpenAlgorithm(HashAlgorithmNames.Md5);
                IBuffer hashedData = md5.HashData(Token.Id);
                string UserID = CryptographicBuffer.EncodeToHexString(hashedData).ToUpper();
                string UserName = UserID.Substring(0, 10);

                ApplicationData.Current.LocalSettings.Values["SystemUserName"] = UserName;
                ApplicationData.Current.LocalSettings.Values["SystemUserID"] = UserID;
            }
        }

        private async Task RegisterBackgroundTaskAsync()
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
            var NewDriveAddedList = DriveInfo.GetDrives().Where((Drives) => Drives.DriveType == DriveType.Fixed || Drives.DriveType == DriveType.Removable || Drives.DriveType == DriveType.Ram || Drives.DriveType == DriveType.Network)
                                                         .Select((Item) => Item.RootDirectory.FullName)
                                                         .SkipWhile((NewItem) => ThisPC.ThisPage.HardDeviceList.Any((Item) => Item.Folder.Path == NewItem));
            try
            {
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
            catch (UnauthorizedAccessException)
            {
                await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, async () =>
                {
                    if (Globalization.Language == LanguageEnum.Chinese)
                    {
                        QueueContentDialog Dialog = new QueueContentDialog
                        {
                            Title = "提示",
                            Content = $"由于缺少足够的访问权限，无法添加可移动设备：\"{args.Name}\"",
                            CloseButtonText = "确定"
                        };
                        _ = await Dialog.ShowAsync();
                    }
                    else
                    {
                        QueueContentDialog Dialog = new QueueContentDialog
                        {
                            Title = "Tips",
                            Content = $"Cannot add removable device：\"{args.Name}\" due to lack of sufficient permissions",
                            CloseButtonText = "Got it"
                        };
                        _ = await Dialog.ShowAsync();
                    }
                });
            }
        }

        private void Nav_Navigated(object sender, NavigationEventArgs e)
        {
            if (Nav.CurrentSourcePageType == typeof(ThisPC) || Nav.CurrentSourcePageType == typeof(SecureArea) || Nav.CurrentSourcePageType == typeof(SettingPage))
            {
                NavView.IsBackEnabled = false;
            }
            else
            {
                NavView.IsBackEnabled = true;
            }

            if (Nav.SourcePageType == typeof(SettingPage))
            {
                NavView.SelectedItem = NavView.SettingsItem as NavigationViewItem;
            }
            else
            {
                if (NavView.MenuItems.Select((Item) => Item as NavigationViewItem).FirstOrDefault((Item) => Item.Content.ToString() == PageDictionary[Nav.SourcePageType]) is NavigationViewItem Item)
                {
                    Item.IsSelected = true;
                }
            }
        }

        private async Task PinApplicationToTaskBarAsync()
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

        private async Task DonateDeveloperAsync()
        {
            if (ApplicationData.Current.LocalSettings.Values["IsDonated"] is bool Donated)
            {
                if (Donated)
                {
                    StoreProductQueryResult PurchasedProductResult = await StoreContext.GetDefault().GetUserCollectionAsync(new string[] { "Durable" });
                    if (PurchasedProductResult.ExtendedError == null && PurchasedProductResult.Products.Count > 0)
                    {
                        return;
                    }

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
                                             "Tips: 支持的小伙伴可以解锁独有文件保险柜功能：“安全域”";
                    }
                    else
                    {
                        DonateTip.Subtitle = "It takes a lot of effort for developers to develop RX file manager\r" +
                                             "🎉You can volunteer to contribute a little pocket money to developers.🎉\r\r" +
                                             "Please donate 0.99$ 🍪\r\r" +
                                             "If you don't want to, you can click \"Later\" to cancel\r" +
                                             "if you want to donate, you can click \"Donate\" to support developer\r\r" +
                                             "Tips: Donator can unlock the unique file safe feature: \"Security Area\"";
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
