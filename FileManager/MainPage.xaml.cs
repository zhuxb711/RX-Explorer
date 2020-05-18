using AnimationEffectProvider;
using FileManager.Class;
using FileManager.Dialog;
using Microsoft.Toolkit.Uwp.Notifications;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Background;
using Windows.ApplicationModel.Core;
using Windows.Foundation;
using Windows.Security.Cryptography;
using Windows.Security.Cryptography.Core;
using Windows.Services.Store;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.System;
using Windows.System.Profile;
using Windows.UI.Core;
using Windows.UI.Core.Preview;
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
        public static MainPage ThisPage { get; private set; }

        private Dictionary<Type, string> PageDictionary;

        public bool IsUSBActivate { get; set; } = false;

        public string ActivateUSBDevicePath { get; private set; }

        private EntranceAnimationEffect EntranceEffectProvider;

        public bool IsAnyTaskRunning { get; set; }

        public MainPage()
        {
            InitializeComponent();
            ThisPage = this;
            Window.Current.SetTitleBar(TitleBar);
            Loaded += MainPage_Loaded;
            Loaded += MainPage_Loaded1;
            Application.Current.EnteredBackground += Current_EnteredBackground;
            Application.Current.LeavingBackground += Current_LeavingBackground;
            SystemNavigationManagerPreview.GetForCurrentView().CloseRequested += MainPage_CloseRequested;
            SystemNavigationManager.GetForCurrentView().BackRequested += MainPage_BackRequested;

            try
            {
                ToastNotificationManager.History.Clear();
            }
            catch (Exception)
            {

            }
#if DEBUG
            AppName.Text += " (Debug 模式)";
#endif
        }

        private async void MainPage_Loaded1(object sender, RoutedEventArgs e)
        {
#if !DEBUG
            if(await FullTrustExcutorController.CheckQuicklookIsAvaliable().ConfigureAwait(false))
            {
                SettingControl.IsQuicklookAvailable = true;
            }
            else
            {
                SettingControl.IsQuicklookAvailable = false;
            }
#endif

            if (ApplicationData.Current.LocalSettings.Values["EnableQuicklook"] is bool Enable)
            {
                SettingControl.IsQuicklookEnable = Enable;
            }
            else
            {
                ApplicationData.Current.LocalSettings.Values["EnableQuicklook"] = true;
                SettingControl.IsQuicklookEnable = true;
            }
        }

        private void MainPage_BackRequested(object sender, BackRequestedEventArgs e)
        {
            TabViewContainer.GoBack();
            e.Handled = true;
        }

        private void Current_LeavingBackground(object sender, LeavingBackgroundEventArgs e)
        {
            ToastNotificationManager.History.Remove("EnterBackgroundTips");
        }

        private void Current_EnteredBackground(object sender, EnteredBackgroundEventArgs e)
        {
            if (IsAnyTaskRunning || GeneralTransformer.IsAnyTransformTaskRunning)
            {
                ToastNotificationManager.History.Remove("EnterBackgroundTips");

                ToastContent Content = new ToastContent()
                {
                    Scenario = ToastScenario.Alarm,
                    Launch = "EnterBackgroundTips",
                    Visual = new ToastVisual()
                    {
                        BindingGeneric = new ToastBindingGeneric()
                        {
                            Children =
                                {
                                    new AdaptiveText()
                                    {
                                        Text = Globalization.GetString("Toast_EnterBackground_Text_1")
                                    },

                                    new AdaptiveText()
                                    {
                                        Text = Globalization.GetString("Toast_EnterBackground_Text_2")
                                    },

                                    new AdaptiveText()
                                    {
                                        Text = Globalization.GetString("Toast_EnterBackground_Text_3")
                                    }
                                }
                        }
                    },
                };
                ToastNotificationManager.CreateToastNotifier().Show(new ToastNotification(Content.GetXml()) { Tag = "EnterBackgroundTips", Priority = ToastNotificationPriority.High });
            }
        }

        private async void MainPage_CloseRequested(object sender, SystemNavigationCloseRequestedPreviewEventArgs e)
        {
            Deferral Deferral = e.GetDeferral();

            if (IsAnyTaskRunning || GeneralTransformer.IsAnyTransformTaskRunning)
            {

                QueueContentDialog Dialog = new QueueContentDialog
                {
                    Title = Globalization.GetString("Common_Dialog_WarningTitle"),
                    Content = Globalization.GetString("QueueDialog_WaitUntilFinish_Content"),
                    PrimaryButtonText = Globalization.GetString("QueueDialog_WaitUntilFinish_PrimaryButton"),
                    CloseButtonText = Globalization.GetString("QueueDialog_WaitUntilFinish_CloseButton")
                };

                if ((await Dialog.ShowAsync().ConfigureAwait(true)) != ContentDialogResult.Primary)
                {
                    e.Handled = true;
                }
                else
                {
                    IsAnyTaskRunning = false;
                    GeneralTransformer.IsAnyTransformTaskRunning = false;
                    ToastNotificationManager.History.Clear();
                }
            }

            Deferral.Complete();
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

                if (Win10VersionChecker.Windows10_1903)
                {
                    EntranceEffectProvider = new EntranceAnimationEffect(this, Nav, Parameter.Item2);
                    EntranceEffectProvider.PrepareEntranceEffect();
                }
            }
            else if (e.Parameter is Rect SplashRect)
            {
                if (Win10VersionChecker.Windows10_1903)
                {
                    EntranceEffectProvider = new EntranceAnimationEffect(this, Nav, SplashRect);
                    EntranceEffectProvider.PrepareEntranceEffect();
                }
            }
        }

        private async void MainPage_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                if (ApplicationData.Current.LocalSettings.Values["IsDoubleClickEnable"] is bool IsDoubleClick)
                {
                    SettingControl.IsDoubleClickEnable = IsDoubleClick;
                }
                else
                {
                    ApplicationData.Current.LocalSettings.Values["IsDoubleClickEnable"] = true;
                }

                if (ApplicationData.Current.LocalSettings.Values["DetachTreeViewAndPresenter"] is bool IsDetach)
                {
                    SettingControl.IsDetachTreeViewAndPresenter = IsDetach;
                }
                else
                {
                    ApplicationData.Current.LocalSettings.Values["DetachTreeViewAndPresenter"] = false;
                }

                PageDictionary = new Dictionary<Type, string>()
                {
                    {typeof(TabViewContainer),Globalization.GetString("MainPage_PageDictionary_ThisPC_Label") },
                    {typeof(FileControl),Globalization.GetString("MainPage_PageDictionary_ThisPC_Label") },
                    {typeof(SecureArea),Globalization.GetString("MainPage_PageDictionary_SecureArea_Label") }
                };

                if (Win10VersionChecker.Windows10_1903)
                {
                    EntranceEffectProvider.StartEntranceEffect();
                }

                Nav.Navigate(typeof(TabViewContainer));

                var PictureUri = await SQLite.Current.GetBackgroundPictureAsync().ConfigureAwait(true);
                var FileList = await (await ApplicationData.Current.LocalFolder.CreateFolderAsync("CustomImageFolder", CreationCollisionOption.OpenIfExists)).GetFilesAsync();
                foreach (var ToDeletePicture in FileList.Where((File) => PictureUri.All((ImageUri) => ImageUri.ToString().Replace("ms-appdata:///local/CustomImageFolder/", string.Empty) != File.Name)))
                {
                    await ToDeletePicture.DeleteAsync(StorageDeleteOption.PermanentDelete);
                }

                await GetUserInfoAsync().ConfigureAwait(true);

                await ShowReleaseLogDialogAsync().ConfigureAwait(true);

#if !DEBUG
                await RegisterBackgroundTaskAsync().ConfigureAwait(true);
#endif

                await DonateDeveloperAsync().ConfigureAwait(true);

                await Task.Delay(10000).ConfigureAwait(true);

                await PinApplicationToTaskBarAsync().ConfigureAwait(true);

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
                _ = await Dialog.ShowAsync().ConfigureAwait(true);
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
                            QueueContentDialog Dialog = new QueueContentDialog
                            {
                                Title = Globalization.GetString("Common_Dialog_TipTitle"),
                                Content = Globalization.GetString("QueueDialog_BackgroundTaskDisable_Content"),
                                PrimaryButtonText = Globalization.GetString("QueueDialog_BackgroundTaskDisable_PrimaryButton"),
                                SecondaryButtonText = Globalization.GetString("QueueDialog_BackgroundTaskDisable_SecondaryButton"),
                                CloseButtonText = Globalization.GetString("QueueDialog_BackgroundTaskDisable_CloseButton")
                            };
                            switch (await Dialog.ShowAsync().ConfigureAwait(true))
                            {
                                case ContentDialogResult.Primary:
                                    {
                                        _ = await Launcher.LaunchUriAsync(new Uri("ms-settings:privacy-backgroundapps"));
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
                        break;
                    }
            }
        }

        private void Nav_Navigated(object sender, NavigationEventArgs e)
        {
            if (NavView.MenuItems.Select((Item) => Item as NavigationViewItem).FirstOrDefault((Item) => Item.Content.ToString() == PageDictionary[e.SourcePageType]) is NavigationViewItem Item)
            {
                Item.IsSelected = true;
            }
        }

        private async Task PinApplicationToTaskBarAsync()
        {
            if (ApplicationData.Current.LocalSettings.Values.ContainsKey("IsPinToTaskBar"))
            {
                if (!ApplicationData.Current.RoamingSettings.Values.ContainsKey("IsRated"))
                {
                    await RequestRateApplication().ConfigureAwait(false);
                }
            }
            else
            {
                ApplicationData.Current.LocalSettings.Values["IsPinToTaskBar"] = true;

                TaskbarManager BarManager = TaskbarManager.GetDefault();
                StartScreenManager ScreenManager = StartScreenManager.GetDefault();

                bool PinStartScreen = false, PinTaskBar = false;

                AppListEntry Entry = (await Package.Current.GetAppListEntriesAsync())[0];
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
                    await RequestRateApplication().ConfigureAwait(true);
                };

                PinTip.Subtitle = Globalization.GetString("TeachingTip_PinToMenu_Subtitle");
                PinTip.IsOpen = true;
            }
        }

        private async Task RequestRateApplication()
        {
            await Task.Delay(60000).ConfigureAwait(true);

            RateTip.ActionButtonClick += async (s, e) =>
            {
                s.IsOpen = false;
                await Launcher.LaunchUriAsync(new Uri("ms-windows-store://review/?productid=9N88QBQKF2RS"));
                ApplicationData.Current.RoamingSettings.Values["IsRated"] = true;
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

                    await Task.Delay(30000).ConfigureAwait(true);
                    DonateTip.ActionButtonClick += async (s, e) =>
                    {
                        s.IsOpen = false;

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
                                                Title = Globalization.GetString("QueueDialog_Donate_Success_Title"),
                                                Content = Globalization.GetString("QueueDialog_Donate_Success_Content"),
                                                CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                                            };
                                            _ = await QueueContenDialog.ShowAsync().ConfigureAwait(true);
                                            break;
                                        }
                                    case StorePurchaseStatus.AlreadyPurchased:
                                        {
                                            QueueContentDialog QueueContenDialog = new QueueContentDialog
                                            {
                                                Title = Globalization.GetString("QueueDialog_Donate_AlreadyPurchase_Title"),
                                                Content = Globalization.GetString("QueueDialog_Donate_AlreadyPurchase_Content"),
                                                CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                                            };
                                            _ = await QueueContenDialog.ShowAsync().ConfigureAwait(true);
                                            break;
                                        }
                                    case StorePurchaseStatus.NotPurchased:
                                        {
                                            QueueContentDialog QueueContenDialog = new QueueContentDialog
                                            {
                                                Title = Globalization.GetString("QueueDialog_Donate_NotPurchase_Title"),
                                                Content = Globalization.GetString("QueueDialog_Donate_NotPurchase_Content"),
                                                CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                                            };
                                            _ = await QueueContenDialog.ShowAsync().ConfigureAwait(true);
                                            break;
                                        }
                                    default:
                                        {
                                            QueueContentDialog QueueContenDialog = new QueueContentDialog
                                            {
                                                Title = Globalization.GetString("QueueDialog_Donate_NetworkError_Title"),
                                                Content = Globalization.GetString("QueueDialog_Donate_NetworkError_Content"),
                                                CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                                            };
                                            _ = await QueueContenDialog.ShowAsync().ConfigureAwait(true);
                                            break;
                                        }
                                }
                            }
                        }
                        else
                        {
                            QueueContentDialog QueueContenDialog = new QueueContentDialog
                            {
                                Title = Globalization.GetString("QueueDialog_Donate_NetworkError_Title"),
                                Content = Globalization.GetString("QueueDialog_Donate_NetworkError_Content"),
                                CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                            };
                            _ = await QueueContenDialog.ShowAsync().ConfigureAwait(true);
                        }
                    };

                    DonateTip.Subtitle = Globalization.GetString("TeachingTip_Donate_Subtitle");

                    DonateTip.IsOpen = true;
                    ApplicationData.Current.LocalSettings.Values["IsDonated"] = false;
                }
            }
            else
            {
                ApplicationData.Current.LocalSettings.Values["IsDonated"] = true;
            }
        }

        private async void NavView_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
        {
            try
            {
                if (args.IsSettingsInvoked)
                {
                    _ = FindName(nameof(SettingControl));

                    await SettingControl.Show().ConfigureAwait(true);

                    NavView.IsBackEnabled = true;
                }
                else
                {
                    if ((SettingControl?.IsOpened).GetValueOrDefault())
                    {
                        await SettingControl.Hide().ConfigureAwait(true);
                    }

                    switch (args.InvokedItem.ToString())
                    {
                        case "这台电脑":
                        case "ThisPC":
                            {
                                NavView.IsBackEnabled = (TabViewContainer.CurrentPageNav?.CanGoBack).GetValueOrDefault();
                                Nav.Navigate(typeof(TabViewContainer), null, new SlideNavigationTransitionInfo() { Effect = SlideNavigationTransitionEffect.FromLeft });
                                break;
                            }
                        case "安全域":
                        case "Security Area":
                            {
                                NavView.IsBackEnabled = false;
                                Nav.Navigate(typeof(SecureArea), null, new SlideNavigationTransitionInfo() { Effect = SlideNavigationTransitionEffect.FromRight });
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

        public async void NavView_BackRequested(NavigationView sender, NavigationViewBackRequestedEventArgs args)
        {
            if ((SettingControl?.IsOpened).GetValueOrDefault())
            {
                if (Nav.CurrentSourcePageType == typeof(TabViewContainer))
                {
                    NavView.IsBackEnabled = (TabViewContainer.CurrentPageNav?.CanGoBack).GetValueOrDefault();
                }
                else
                {
                    NavView.IsBackEnabled = false;
                }

                await SettingControl.Hide().ConfigureAwait(false);
            }
            else
            {
                TabViewContainer.GoBack();
            }
        }
    }
}
