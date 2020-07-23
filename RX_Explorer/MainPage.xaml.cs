using AnimationEffectProvider;
using Microsoft.Toolkit.Uwp.Notifications;
using RX_Explorer.Class;
using RX_Explorer.Dialog;
using RX_Explorer.View;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Background;
using Windows.ApplicationModel.Core;
using Windows.ApplicationModel.DataTransfer;
using Windows.Devices.Enumeration;
using Windows.Devices.Portable;
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

namespace RX_Explorer
{
    public sealed partial class MainPage : Page
    {
        public static MainPage ThisPage { get; private set; }

        private Dictionary<Type, string> PageDictionary;

        public bool IsPathActivate { get; set; } = false;

        public string ActivatePath { get; private set; }

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

            if (Package.Current.IsDevelopmentMode)
            {
                AppName.Text += " (Development Mode)";
            }
        }

        private async void MainPage_Loaded1(object sender, RoutedEventArgs e)
        {
            if (await FullTrustExcutorController.Current.CheckQuicklookIsAvaliableAsync().ConfigureAwait(false))
            {
                SettingControl.IsQuicklookAvailable = true;
            }
            else
            {
                SettingControl.IsQuicklookAvailable = false;
            }

            if (ApplicationData.Current.LocalSettings.Values["EnableQuicklook"] is bool Enable)
            {
                SettingControl.IsQuicklookEnable = Enable;
            }
            else
            {
                ApplicationData.Current.LocalSettings.Values["EnableQuicklook"] = true;
                SettingControl.IsQuicklookEnable = true;
            }

            if (ApplicationData.Current.LocalSettings.Values["DisplayHiddenItem"] is bool Display)
            {
                SettingControl.IsDisplayHiddenItem = Display;
            }
            else
            {
                ApplicationData.Current.LocalSettings.Values["DisplayHiddenItem"] = false;
                SettingControl.IsDisplayHiddenItem = false;
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

            if (IsAnyTaskRunning || GeneralTransformer.IsAnyTransformTaskRunning || FullTrustExcutorController.Current.IsNowHasAnyActionExcuting)
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

            try
            {
                if (!e.Handled && Clipboard.GetContent().Contains(StandardDataFormats.StorageItems))
                {
                    Clipboard.Flush();
                }
            }
            catch
            {

            }

            Deferral.Complete();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            if (e.Parameter is Tuple<string, Rect> Parameter)
            {
                string[] Paras = Parameter.Item1.Split("||");
                switch (Paras[0])
                {
                    case "PathActivate":
                        {
                            IsPathActivate = true;
                            ActivatePath = Paras[1];
                            break;
                        }
                }

                if (WindowsVersionChecker.IsNewerOrEqual(WindowsVersionChecker.Version.Windows10_1903) && AnimationController.Current.IsEnableAnimation && !IsPathActivate)
                {
                    EntranceEffectProvider = new EntranceAnimationEffect(this, Nav, Parameter.Item2);
                    EntranceEffectProvider.PrepareEntranceEffect();
                }
            }
            else if (e.Parameter is Rect SplashRect)
            {
                if (WindowsVersionChecker.IsNewerOrEqual(WindowsVersionChecker.Version.Windows10_1903) && AnimationController.Current.IsEnableAnimation && !IsPathActivate)
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
                    {typeof(SecureArea),Globalization.GetString("MainPage_PageDictionary_SecureArea_Label") },
                    {typeof(RecycleBin),Globalization.GetString("MainPage_PageDictionary_RecycleBin_Label") }
                };

                if (WindowsVersionChecker.IsNewerOrEqual(WindowsVersionChecker.Version.Windows10_1903) && AnimationController.Current.IsEnableAnimation && !IsPathActivate)
                {
                    EntranceEffectProvider.StartEntranceEffect();
                }

                Nav.Navigate(typeof(TabViewContainer), null, new DrillInNavigationTransitionInfo());

                var PictureUri = await SQLite.Current.GetBackgroundPictureAsync().ConfigureAwait(true);
                var FileList = await (await ApplicationData.Current.LocalFolder.CreateFolderAsync("CustomImageFolder", CreationCollisionOption.OpenIfExists)).GetFilesAsync();
                foreach (var ToDeletePicture in FileList.Where((File) => PictureUri.All((ImageUri) => ImageUri.ToString().Replace("ms-appdata:///local/CustomImageFolder/", string.Empty) != File.Name)))
                {
                    await ToDeletePicture.DeleteAsync(StorageDeleteOption.PermanentDelete);
                }

                await GetUserInfoAsync().ConfigureAwait(true);

                await ShowReleaseLogDialogAsync().ConfigureAwait(true);

                await RegisterBackgroundTaskAsync().ConfigureAwait(true);

                await PurchaseApplicationAsync().ConfigureAwait(true);

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

            RateTip.CloseButtonClick += (s, e) =>
            {
                s.IsOpen = false;
                ApplicationData.Current.RoamingSettings.Values["IsRated"] = true;
            };

            RateTip.IsOpen = true;
        }

        private static async Task<bool> CheckPurchaseStatusAsync()
        {
            try
            {
                StoreContext Store = StoreContext.GetDefault();
                StoreAppLicense License = await Store.GetAppLicenseAsync();

                if (License.AddOnLicenses.Any((Item) => Item.Value.InAppOfferToken == "Donation"))
                {
                    return true;
                }

                if (License.IsActive)
                {
                    if (License.IsTrial)
                    {
                        return false;
                    }
                    else
                    {
                        return true;
                    }
                }
                else
                {
                    return false;
                }
            }
            catch
            {
                return false;
            }
        }


        private async Task PurchaseApplicationAsync()
        {
            if (ApplicationData.Current.LocalSettings.Values["IsDonated"] is bool Donated)
            {
                if (Donated && !await CheckPurchaseStatusAsync().ConfigureAwait(true))
                {
                    await Task.Delay(30000).ConfigureAwait(true);

                    PurchaseTip.ActionButtonClick += async (s, e) =>
                    {
                        s.IsOpen = false;

                        StoreContext Store = StoreContext.GetDefault();
                        StoreProductResult ProductResult = await Store.GetStoreProductForCurrentAppAsync();

                        if (ProductResult.ExtendedError == null)
                        {
                            if (ProductResult.Product != null)
                            {
                                switch ((await ProductResult.Product.RequestPurchaseAsync()).Status)
                                {
                                    case StorePurchaseStatus.Succeeded:
                                        {
                                            QueueContentDialog QueueContenDialog = new QueueContentDialog
                                            {
                                                Title = Globalization.GetString("Common_Dialog_TipTitle"),
                                                Content = Globalization.GetString("QueueDialog_Store_PurchaseSuccess_Content"),
                                                CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                                            };
                                            _ = await QueueContenDialog.ShowAsync().ConfigureAwait(true);
                                            break;
                                        }
                                    case StorePurchaseStatus.AlreadyPurchased:
                                        {
                                            QueueContentDialog QueueContenDialog = new QueueContentDialog
                                            {
                                                Title = Globalization.GetString("Common_Dialog_TipTitle"),
                                                Content = Globalization.GetString("QueueDialog_Store_AlreadyPurchase_Content"),
                                                CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                                            };
                                            _ = await QueueContenDialog.ShowAsync().ConfigureAwait(true);
                                            break;
                                        }
                                    case StorePurchaseStatus.NotPurchased:
                                        {
                                            QueueContentDialog QueueContenDialog = new QueueContentDialog
                                            {
                                                Title = Globalization.GetString("Common_Dialog_TipTitle"),
                                                Content = Globalization.GetString("QueueDialog_Store_NotPurchase_Content"),
                                                CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                                            };
                                            _ = await QueueContenDialog.ShowAsync().ConfigureAwait(true);
                                            break;
                                        }
                                    default:
                                        {
                                            QueueContentDialog QueueContenDialog = new QueueContentDialog
                                            {
                                                Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                                Content = Globalization.GetString("QueueDialog_Store_NetworkError_Content"),
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
                                Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                Content = Globalization.GetString("QueueDialog_Store_NetworkError_Content"),
                                CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                            };
                            _ = await QueueContenDialog.ShowAsync().ConfigureAwait(true);
                        }
                    };

                    PurchaseTip.Subtitle = Globalization.GetString("TeachingTip_PurchaseTip_Subtitle");
                    PurchaseTip.IsOpen = true;
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

                    if (args.InvokedItem.ToString() == Globalization.GetString("MainPage_PageDictionary_ThisPC_Label"))
                    {
                        NavView.IsBackEnabled = (TabViewContainer.CurrentTabNavigation?.CanGoBack).GetValueOrDefault();
                        Nav.Navigate(typeof(TabViewContainer), null, new SlideNavigationTransitionInfo() { Effect = SlideNavigationTransitionEffect.FromLeft });
                    }
                    else if (args.InvokedItem.ToString() == Globalization.GetString("MainPage_PageDictionary_SecureArea_Label"))
                    {
                        NavView.IsBackEnabled = false;
                        Nav.Navigate(typeof(SecureArea), null, new SlideNavigationTransitionInfo() { Effect = SlideNavigationTransitionEffect.FromRight });
                    }
                    else if (args.InvokedItem.ToString() == Globalization.GetString("MainPage_PageDictionary_RecycleBin_Label"))
                    {
                        NavView.IsBackEnabled = false;
                        Nav.Navigate(typeof(RecycleBin), null, new SlideNavigationTransitionInfo() { Effect = Nav.CurrentSourcePageType == typeof(SecureArea) ? SlideNavigationTransitionEffect.FromLeft : SlideNavigationTransitionEffect.FromRight });
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
                    NavView.IsBackEnabled = (TabViewContainer.CurrentTabNavigation?.CanGoBack).GetValueOrDefault();
                }
                else
                {
                    NavView.IsBackEnabled = false;
                }

                if (NavView.MenuItems.Select((Item) => Item as NavigationViewItem).FirstOrDefault((Item) => Item.Content.ToString() == PageDictionary[Nav.CurrentSourcePageType]) is NavigationViewItem Item)
                {
                    Item.IsSelected = true;
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
