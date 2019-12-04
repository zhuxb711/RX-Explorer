using AnimationEffectProvider;
using Microsoft.Toolkit.Uwp.Notifications;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Core;
using Windows.Devices.Enumeration;
using Windows.Foundation;
using Windows.Services.Store;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.System;
using Windows.UI.Notifications;
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
        private StoreContext Context;

        private IReadOnlyList<StorePackageUpdate> Updates;

        public static MainPage ThisPage { get; private set; }

        private Dictionary<Type, string> PageDictionary;

        public bool IsUSBActivate { get; set; } = false;

        public string ActivateUSBDevicePath { get; private set; }

        public LanguageEnum CurrentLanguage { get; private set; }

        private EntranceAnimationEffect EntranceEffectProvider;

        private DeviceWatcher PortalDeviceWatcher;

        public MainPage()
        {
            InitializeComponent();
            ThisPage = this;
            Window.Current.SetTitleBar(TitleBar);
            Loaded += MainPage_Loaded;
            CurrentLanguage = Windows.System.UserProfile.GlobalizationPreferences.Languages.FirstOrDefault().StartsWith("zh")
                ? LanguageEnum.Chinese
                : LanguageEnum.English;
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
            PortalDeviceWatcher = DeviceInformation.CreateWatcher(DeviceClass.PortableStorageDevice);
            PortalDeviceWatcher.Added += PortalDeviceWatcher_Added;
            PortalDeviceWatcher.Removed += PortalDeviceWatcher_Removed;
            PortalDeviceWatcher.Start();

            if (!(ApplicationData.Current.LocalSettings.Values["UIDisplayMode"] is string Mode))
            {
                ApplicationData.Current.LocalSettings.Values["UIDisplayMode"] = CurrentLanguage == LanguageEnum.Chinese
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

            if (CurrentLanguage == LanguageEnum.Chinese)
            {
                PageDictionary = new Dictionary<Type, string>()
                {
                    {typeof(WebTab), "浏览器"},
                    {typeof(ThisPC),"这台电脑" },
                    {typeof(FileControl),"这台电脑" },
                    {typeof(AboutMe),"这台电脑" }
                };
            }
            else
            {
                PageDictionary = new Dictionary<Type, string>()
                {
                    {typeof(WebTab), "Browser"},
                    {typeof(ThisPC),"ThisPC" },
                    {typeof(FileControl),"ThisPC" },
                    {typeof(AboutMe),"ThisPC" }
                };
            }

            Nav.Navigate(typeof(ThisPC));

            EntranceEffectProvider.StartEntranceEffect();

            var PictureUri = await SQLite.Current.GetBackgroundPictureAsync();
            var FileList = await (await ApplicationData.Current.LocalFolder.CreateFolderAsync("CustomImageFolder", CreationCollisionOption.OpenIfExists)).GetFilesAsync();
            foreach (var ToDeletePicture in FileList.Where((File) => PictureUri.All((Image) => Image.Replace("ms-appdata:///local/CustomImageFolder/", string.Empty) != File.Name)))
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

#if !DEBUG
            await CheckAndInstallUpdate();
#endif
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
            if (Nav.CurrentSourcePageType == typeof(ThisPC) || Nav.CurrentSourcePageType == typeof(WebTab))
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
                await Task.Delay(60000);
                RequestRateApplication();
            };

            PinTip.Subtitle = CurrentLanguage == LanguageEnum.Chinese
                ? "将RX文件管理器固定在和开始屏幕任务栏，启动更快更方便哦！\r\r★固定至开始菜单\r\r★固定至任务栏"
                : "Pin the RX FileManager to StartScreen and TaskBar ！\r\r★Pin to StartScreen\r\r★Pin to TaskBar";
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
                await Launcher.LaunchUriAsync(new Uri("ms-windows-store://review/?productid=9N88QBQKF2RS"));
            };
        }

        private async Task CheckAndInstallUpdate()
        {
            try
            {
                Context = StoreContext.GetDefault();
                Updates = await Context.GetAppAndOptionalStorePackageUpdatesAsync();

                if (Updates.Count > 0)
                {
                    UpdateTip.Subtitle = CurrentLanguage == LanguageEnum.Chinese
                        ? "最新版RX文件管理器已推出！\r最新版包含针对以往问题的修复补丁\r是否立即下载？"
                        : "The latest version of the RX FileManager has been released! \rIncluding fixes for past issues\rWhether to download it now ?";

                    UpdateTip.ActionButtonClick += async (s, e) =>
                    {
                        s.IsOpen = false;
                        SendUpdatableToastWithProgress();

                        if (Context.CanSilentlyDownloadStorePackageUpdates)
                        {
                            IProgress<StorePackageUpdateStatus> DownloadProgress = new Progress<StorePackageUpdateStatus>((Status) =>
                            {
                                if (Status.PackageDownloadProgress > 1)
                                {
                                    return;
                                }

                                string Tag = "RX-Updating";
                                var data = new NotificationData
                                {
                                    SequenceNumber = 0
                                };
                                data.Values["ProgressValue"] = Status.PackageDownloadProgress.ToString("0.##");
                                data.Values["ProgressString"] = Convert.ToUInt16(Math.Ceiling(Status.PackageDownloadProgress * 100)).ToString() + "%";

                                ToastNotificationManager.CreateToastNotifier().Update(data, Tag);
                            });

                            StorePackageUpdateResult DownloadResult = await Context.TrySilentDownloadStorePackageUpdatesAsync(Updates).AsTask(DownloadProgress);

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
                                if (Status.PackageDownloadProgress > 1)
                                {
                                    return;
                                }

                                string Tag = "RX-Updating";
                                var data = new NotificationData
                                {
                                    SequenceNumber = 0
                                };
                                data.Values["ProgressValue"] = Status.PackageDownloadProgress.ToString("0.##");
                                data.Values["ProgressString"] = Convert.ToUInt16(Math.Ceiling(Status.PackageDownloadProgress * 100)).ToString() + "%";

                                ToastNotificationManager.CreateToastNotifier().Update(data, Tag);
                            });

                            StorePackageUpdateResult DownloadResult = await Context.RequestDownloadStorePackageUpdatesAsync(Updates).AsTask(DownloadProgress);

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
                else
                {
                    await Task.Delay(5000);
                    await PinApplicationToTaskBar();
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
                                Text = CurrentLanguage == LanguageEnum.Chinese
                                        ? "更新失败"
                                        : "Update Failed"
                            },

                            new AdaptiveText()
                            {
                                Text = CurrentLanguage == LanguageEnum.Chinese
                                        ? "RX文件管理器无法更新至最新版"
                                        : "RX FileManager cannot be updated to the latest version"
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
                                Text = CurrentLanguage == LanguageEnum.Chinese
                                        ? "正在下载应用更新..."
                                        : "Downloading Updates..."
                            },

                            new AdaptiveProgressBar()
                            {
                                Title = CurrentLanguage == LanguageEnum.Chinese
                                        ? "正在更新..."
                                        : "Updating...",
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
            Toast.Data.Values["ProgressStatus"] = CurrentLanguage == LanguageEnum.Chinese
                                                    ? "正在下载..."
                                                    : "Downloading...";
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
                    case "ThisPC":
                        Nav.Navigate(typeof(ThisPC), null, new DrillInNavigationTransitionInfo()); break;
                    case "浏览器":
                    case "Browser":
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
