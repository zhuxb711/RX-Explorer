using AnimationEffectProvider;
using Microsoft.Toolkit.Uwp.Helpers;
using Microsoft.Toolkit.Uwp.Notifications;
using Microsoft.UI.Xaml.Controls;
using RX_Explorer.Class;
using RX_Explorer.Dialog;
using RX_Explorer.View;
using ShareClassLibrary;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Background;
using Windows.ApplicationModel.Core;
using Windows.ApplicationModel.DataTransfer;
using Windows.Devices.Enumeration;
using Windows.Foundation;
using Windows.Media.Audio;
using Windows.Services.Store;
using Windows.Storage;
using Windows.System;
using Windows.System.Power;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.Core.Preview;
using Windows.UI.Input;
using Windows.UI.Notifications;
using Windows.UI.Shell;
using Windows.UI.StartScreen;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;
using NavigationView = Microsoft.UI.Xaml.Controls.NavigationView;
using NavigationViewBackRequestedEventArgs = Microsoft.UI.Xaml.Controls.NavigationViewBackRequestedEventArgs;
using NavigationViewItem = Microsoft.UI.Xaml.Controls.NavigationViewItem;
using NavigationViewItemInvokedEventArgs = Microsoft.UI.Xaml.Controls.NavigationViewItemInvokedEventArgs;
using NavigationViewPaneDisplayMode = Microsoft.UI.Xaml.Controls.NavigationViewPaneDisplayMode;

namespace RX_Explorer
{
    public sealed partial class MainPage : Page
    {
        public static MainPage Current { get; private set; }

        public List<string[]> ActivatePathArray { get; }

        private readonly Dictionary<Type, string> PageDictionary;

        private readonly EntranceAnimationEffect EntranceEffectProvider;

        private readonly Task EntranceAnimationPreloadTask;

        private DeviceWatcher BluetoothAudioWatcher;

        public MainPage(Rect Parameter, List<string[]> ActivatePathArray = null)
        {
            InitializeComponent();

            Current = this;
            this.ActivatePathArray = ActivatePathArray;

            CoreApplicationViewTitleBar SystemBar = CoreApplication.GetCurrentView().TitleBar;
            TitleBar.Margin = new Thickness(SystemBar.SystemOverlayLeftInset, TitleBar.Margin.Top, SystemBar.SystemOverlayRightInset, TitleBar.Margin.Bottom);
            SystemBar.LayoutMetricsChanged += TitleBar_LayoutMetricsChanged;
            SystemBar.IsVisibleChanged += SystemBar_IsVisibleChanged;
            ApplicationView.GetForCurrentView().TitleBar.ButtonForegroundColor = AppThemeController.Current.Theme == ElementTheme.Dark ? Colors.White : Colors.Black;

            Window.Current.SetTitleBar(TitleBar);

            Loaded += MainPage_Loaded;
            Loaded += MainPage_Loaded1;
            Window.Current.Activated += MainPage_Activated;
            Application.Current.EnteredBackground += Current_EnteredBackground;
            Application.Current.LeavingBackground += Current_LeavingBackground;
            SystemNavigationManagerPreview.GetForCurrentView().CloseRequested += MainPage_CloseRequested;
            SystemNavigationManager.GetForCurrentView().BackRequested += MainPage_BackRequested;
            AppThemeController.Current.ThemeChanged += Current_ThemeChanged;
            FullTrustProcessController.CurrentBusyStatus += FullTrustProcessController_CurrentBusyStatus;

            MSStoreHelper.Current.PreLoadStoreData();
            InfoTipController.Current.SetInfoTipPanel(BannerPanel);

            BackgroundController.Current.SetAcrylicEffectPresenter(CompositorAcrylicBackground);

#if DEBUG
            AppName.Text += $" ({Globalization.GetString("Development_Version")})";
#endif

            NavView.RegisterPropertyChangedCallback(NavigationView.PaneDisplayModeProperty, new DependencyPropertyChangedCallback(OnPaneDisplayModeChanged));
            NavView.PaneDisplayMode = SettingPage.LayoutMode;

            if (WindowsVersionChecker.IsNewerOrEqual(Class.Version.Windows11))
            {
                BackdropMaterial.SetApplyToRootOrPageBackground(this, BackgroundController.Current.IsMicaEffectEnabled);
                BackgroundEffectArea.RegisterPropertyChangedCallback(VisibilityProperty, new DependencyPropertyChangedCallback(OnBackgroundEffectAreaVisibilityChanged));
            }

            PageDictionary = new Dictionary<Type, string>()
            {
                {typeof(TabViewContainer),Globalization.GetString("MainPage_PageDictionary_Home_Label") },
                {typeof(FileControl),Globalization.GetString("MainPage_PageDictionary_Home_Label") },
                {typeof(SecureAreaContainer),Globalization.GetString("MainPage_PageDictionary_SecureArea_Label") },
                {typeof(RecycleBin),Globalization.GetString("MainPage_PageDictionary_RecycleBin_Label") }
            };

            if (!AnimationController.Current.IsDisableStartupAnimation && (ActivatePathArray?.Count).GetValueOrDefault() == 0)
            {
                EntranceEffectProvider = new EntranceAnimationEffect(this, NavView, Parameter);
                EntranceAnimationPreloadTask = EntranceEffectProvider.PrepareEntranceEffect();
            }
        }

        private void SystemBar_IsVisibleChanged(CoreApplicationViewTitleBar sender, object args)
        {
            if (sender.IsVisible)
            {
                Window.Current.SetTitleBar(TitleBar);
                TitleBar.Visibility = Visibility.Visible;
            }
            else
            {
                Window.Current.SetTitleBar(null);
                TitleBar.Visibility = Visibility.Collapsed;
            }
        }

        private void OnBackgroundEffectAreaVisibilityChanged(DependencyObject sender, DependencyProperty dp)
        {
            BackdropMaterial.SetApplyToRootOrPageBackground(this, BackgroundController.Current.IsMicaEffectEnabled);
        }

        private void OnPaneDisplayModeChanged(DependencyObject sender, DependencyProperty dp)
        {
            if (sender is NavigationView View)
            {
                if (View.PaneDisplayMode == NavigationViewPaneDisplayMode.LeftCompact)
                {
                    if (View.IsPaneOpen)
                    {
                        AppName.Translation = new System.Numerics.Vector3(0, 0, 0);
                    }
                    else
                    {
                        AppName.Translation = new System.Numerics.Vector3(42, 0, 0);
                    }
                }
                else
                {
                    AppName.Translation = new System.Numerics.Vector3(0, 0, 0);
                }
            }
        }

        private void TitleBar_LayoutMetricsChanged(CoreApplicationViewTitleBar sender, object args)
        {
            TitleBar.Height = sender.Height;
            TitleBar.Margin = new Thickness(sender.SystemOverlayLeftInset, TitleBar.Margin.Top, sender.SystemOverlayRightInset, TitleBar.Margin.Bottom);
        }

        private async void FullTrustProcessController_CurrentBusyStatus(object sender, bool IsBusy)
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                if (IsBusy)
                {
                    InfoTipController.Current.Show(InfoTipType.FullTrustBusy);
                }
                else
                {
                    InfoTipController.Current.Hide(InfoTipType.FullTrustBusy);
                }
            });
        }

        private async void Current_ThemeChanged(object sender, ElementTheme Theme)
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                ApplicationView.GetForCurrentView().TitleBar.ButtonForegroundColor = Theme == ElementTheme.Dark ? Colors.White : Colors.Black;
            });
        }

        private async void Current_DataChanged(ApplicationData sender, object args)
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.Low, async () =>
            {
                if (ApplicationData.Current.LocalSettings.Values["ShouldShowRecycleBinItem"] is bool ShowRecycleBin)
                {
                    RecycleBinItem.Visibility = ShowRecycleBin ? Visibility.Visible : Visibility.Collapsed;
                }

                if (ApplicationData.Current.LocalSettings.Values["ShouldShowQuickStartItem"] is bool ShowQuickStart)
                {
                    QuickStartItem.Visibility = ShowQuickStart ? Visibility.Visible : Visibility.Collapsed;
                }

                if (ApplicationData.Current.LocalSettings.Values["ShouldShowSecureAreaItem"] is bool ShowSecureArea)
                {
                    SecureAreaItem.Visibility = ShowSecureArea ? Visibility.Visible : Visibility.Collapsed;
                }

                if (ApplicationData.Current.LocalSettings.Values["ShouldShowBluetoothAudioItem"] is bool ShowBluetoothAudio)
                {
                    BluetoothAudioItem.Visibility = ShowBluetoothAudio ? Visibility.Visible : Visibility.Collapsed;
                }

                if (FindName(nameof(Settings)) is SettingPage Dialog)
                {
                    await Settings.InitializeAsync();
                }
            });
        }

        private void MainPage_Activated(object sender, WindowActivatedEventArgs e)
        {
            if (e.WindowActivationState != CoreWindowActivationState.Deactivated)
            {
                AppInstanceIdContainer.SetCurrentIdAsLastActivateId();
            }
        }

        private async void MainPage_Loaded1(object sender, RoutedEventArgs e)
        {
            if (SystemInformation.Instance.IsFirstRun)
            {
                switch (await Launcher.QueryUriSupportAsync(new Uri("ms-windows-store:"), LaunchQuerySupportType.Uri, "Microsoft.WindowsTerminal_8wekyb3d8bbwe"))
                {
                    case LaunchQuerySupportStatus.Available:
                    case LaunchQuerySupportStatus.NotSupported:
                        {
                            SQLite.Current.SetTerminalProfile(new TerminalProfile("Windows Terminal", "wt.exe", "/d [CurrentLocation]", true));
                            break;
                        }
                }
            }

            if (SettingPage.WindowAlwaysOnTop)
            {
                try
                {
                    using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableControllerAsync())
                    {
                        using Process CurrentProcess = Process.GetCurrentProcess();

                        if (!await Exclusive.Controller.SetAsTopMostWindowAsync(Package.Current.Id.FamilyName, Convert.ToUInt32(CurrentProcess.Id)))
                        {
                            QueueContentDialog Dialog = new QueueContentDialog
                            {
                                Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                Content = Globalization.GetString("QueueDialog_SetTopMostFailed_Content"),
                                CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                            };

                            await Dialog.ShowAsync();
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogTracer.Log(ex, "An exception was threw when setting this application as the TopMost window");
                }
            }
        }

        private void MainPage_BackRequested(object sender, BackRequestedEventArgs e)
        {
            NavView_BackRequested(null, null);

            e.Handled = true;
        }

        private void Current_LeavingBackground(object sender, LeavingBackgroundEventArgs e)
        {
            try
            {
                ToastNotificationManager.History.Remove("EnterBackgroundTips");
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, "Toast notification could not be removed");
            }
        }

        private void Current_EnteredBackground(object sender, EnteredBackgroundEventArgs e)
        {
            if (FullTrustProcessController.IsAnyActionExcutingInAllControllers || GeneralTransformer.IsAnyTransformTaskRunning || QueueTaskController.IsAnyTaskRunningInController)
            {
                try
                {
                    ToastNotificationManager.History.Remove("EnterBackgroundTips");

                    if (PowerManager.PowerSupplyStatus == PowerSupplyStatus.NotPresent || PowerManager.EnergySaverStatus == EnergySaverStatus.On)
                    {
                        ToastContentBuilder Builder = new ToastContentBuilder()
                                                      .SetToastScenario(ToastScenario.Reminder)
                                                      .AddToastActivationInfo("EnterBackgroundTips", ToastActivationType.Foreground)
                                                      .AddText(Globalization.GetString("Toast_EnterBackground_Text_1"))
                                                      .AddText(Globalization.GetString("Toast_EnterBackground_Text_2"))
                                                      .AddText(Globalization.GetString("Toast_EnterBackground_Text_4"))
                                                      .AddButton(new ToastButton(Globalization.GetString("Toast_EnterBackground_ActionButton"), "EnterBackgroundTips"))
                                                      .AddButton(new ToastButtonDismiss(Globalization.GetString("Toast_EnterBackground_Dismiss")));

                        ToastNotificationManager.CreateToastNotifier().Show(new ToastNotification(Builder.GetToastContent().GetXml())
                        {
                            Tag = "EnterBackgroundTips",
                            Priority = ToastNotificationPriority.High
                        });
                    }
                }
                catch (Exception ex)
                {
                    LogTracer.Log(ex, "Toast notification could not be sent");
                }
            }
        }

        private async void MainPage_CloseRequested(object sender, SystemNavigationCloseRequestedPreviewEventArgs e)
        {
            Deferral Deferral = e.GetDeferral();

            try
            {
                if (GeneralTransformer.IsAnyTransformTaskRunning
                    || FullTrustProcessController.IsAnyActionExcutingInAllControllers
                    || QueueTaskController.IsAnyTaskRunningInController)
                {
                    QueueContentDialog Dialog = new QueueContentDialog
                    {
                        Title = Globalization.GetString("Common_Dialog_WarningTitle"),
                        Content = Globalization.GetString("QueueDialog_WaitUntilFinish_Content"),
                        PrimaryButtonText = Globalization.GetString("QueueDialog_WaitUntilFinish_PrimaryButton"),
                        CloseButtonText = Globalization.GetString("QueueDialog_WaitUntilFinish_CloseButton")
                    };

                    if ((await Dialog.ShowAsync()) == ContentDialogResult.Primary)
                    {
                        ToastNotificationManager.History.Clear();
                        Application.Current.EnteredBackground -= Current_EnteredBackground;
                        Application.Current.LeavingBackground -= Current_LeavingBackground;
                    }
                    else
                    {
                        e.Handled = true;
                        return;
                    }
                }

                bool ShouldPopKeepClipboardTip = false;

                try
                {
                    DataPackageView Package = Clipboard.GetContent();

                    if (Package.Properties.PackageFamilyName == Windows.ApplicationModel.Package.Current.Id.FamilyName)
                    {
                        ShouldPopKeepClipboardTip = await Package.CheckIfContainsAvailableDataAsync();
                    }
                }
                catch
                {
                    ShouldPopKeepClipboardTip = false;
                }

                if (ShouldPopKeepClipboardTip)
                {
                    if (ApplicationData.Current.LocalSettings.Values["ClipboardFlushAlways"] is bool IsFlush)
                    {
                        if (IsFlush)
                        {
                            Clipboard.Flush();
                        }
                    }
                    else
                    {
                        StackPanel Panel = new StackPanel
                        {
                            HorizontalAlignment = HorizontalAlignment.Stretch
                        };

                        TextBlock Text = new TextBlock
                        {
                            Text = Globalization.GetString("QueueDialog_ClipboardFlushTip_Content"),
                            TextWrapping = TextWrapping.WrapWholeWords
                        };

                        CheckBox Box = new CheckBox
                        {
                            Content = Globalization.GetString("QueueDialog_ClipboardFlushRemember_Content"),
                            Margin = new Thickness(0, 10, 0, 0)
                        };

                        Panel.Children.Add(Text);
                        Panel.Children.Add(Box);

                        QueueContentDialog Dialog = new QueueContentDialog
                        {
                            Title = Globalization.GetString("Common_Dialog_TipTitle"),
                            Content = Panel,
                            PrimaryButtonText = Globalization.GetString("Common_Dialog_ContinueButton"),
                            CloseButtonText = Globalization.GetString("Common_Dialog_CancelButton")
                        };

                        ContentDialogResult Result = await Dialog.ShowAsync();

                        if (Result == ContentDialogResult.Primary)
                        {
                            Clipboard.Flush();
                        }

                        if (Box.IsChecked.GetValueOrDefault())
                        {
                            if (Result == ContentDialogResult.Primary)
                            {
                                ApplicationData.Current.LocalSettings.Values["ClipboardFlushAlways"] = true;
                            }
                            else
                            {
                                ApplicationData.Current.LocalSettings.Values["ClipboardFlushAlways"] = false;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, "An error was threw in close delay");
            }
            finally
            {
                Deferral.Complete();
            }
        }

        private async void MainPage_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                if (EntranceAnimationPreloadTask != null)
                {
                    await EntranceAnimationPreloadTask;
                }

                if (NavView.SettingsItem is NavigationViewItem SettingItem)
                {
                    SettingItem.SelectsOnInvoked = false;
                }

                Nav.Navigate(typeof(TabViewContainer), null, new SuppressNavigationTransitionInfo());

                if (AnimationController.Current.IsDisableStartupAnimation)
                {
                    NavView.Opacity = 1;
                }
                else
                {
                    await Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                    {
                        OpacityAnimation.Begin();

                        if ((ActivatePathArray?.Count).GetValueOrDefault() == 0)
                        {
                            EntranceEffectProvider.StartEntranceEffect();
                        }
                    });
                }

                ApplicationData.Current.DataChanged += Current_DataChanged;

                if (SystemInformation.Instance.IsAppUpdated || SystemInformation.Instance.IsFirstRun)
                {
                    await new WhatIsNew().ShowAsync();
                }

                await Task.WhenAll(RegisterBackgroundTaskAsync(), CheckUpdateIfExistAsync());

                bool IsPurchased = await MSStoreHelper.Current.CheckPurchaseStatusAsync();

                if (!IsPurchased)
                {
                    AppName.Text += $" ({Globalization.GetString("Trial_Version")})";
                }

                switch (SystemInformation.Instance.LaunchCount)
                {
                    case 15 when !IsPurchased:
                    case 25 when !IsPurchased:
                    case 35 when !IsPurchased:
                        {
                            PurchaseApplication();
                            break;
                        }
                    case 5:
                        {
                            await PinApplicationToTaskBarAsync();
                            break;
                        }
                    case 10:
                        {
                            RequestRateApplication();
                            break;
                        }
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, "MainPage initialize failed");
            }
        }

        private async Task CheckUpdateIfExistAsync()
        {
            if (await MSStoreHelper.Current.CheckHasUpdateAsync())
            {
                if (await MSStoreHelper.Current.CheckIfUpdateIsMandatoryAsync())
                {
                    InfoTipController.Current.Show(InfoTipType.MandatoryUpdateAvailable);
                }
                else
                {
                    InfoTipController.Current.Hide(InfoTipType.UpdateAvailable);
                }
            }
        }

        private async Task RegisterBackgroundTaskAsync()
        {
            try
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

                            BackgroundTaskBuilder Builder = new BackgroundTaskBuilder
                            {
                                Name = "UpdateTask",
                                IsNetworkRequested = true,
                                TaskEntryPoint = "UpdateCheckBackgroundTask.UpdateCheck"
                            };
                            Builder.SetTrigger(new SystemTrigger(SystemTriggerType.SessionConnected, false));
                            Builder.AddCondition(new SystemCondition(SystemConditionType.InternetAvailable));
                            Builder.AddCondition(new SystemCondition(SystemConditionType.UserPresent));
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

                                switch (await Dialog.ShowAsync())
                                {
                                    case ContentDialogResult.Primary:
                                        {
                                            await Launcher.LaunchUriAsync(new Uri("ms-settings:privacy-backgroundapps"));
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
            catch (Exception e)
            {
                LogTracer.Log(e, $"An error was threw in {nameof(RegisterBackgroundTaskAsync)}");
            }
        }

        private void Nav_Navigated(object sender, NavigationEventArgs e)
        {
            if (NavView.MenuItems.Select((Item) => Item as NavigationViewItem).FirstOrDefault((Item) => Item.Content.ToString() == PageDictionary[e.SourcePageType]) is NavigationViewItem Item)
            {
                Item.IsSelected = true;
            }

            if (PageDictionary[e.SourcePageType] == Globalization.GetString("MainPage_PageDictionary_Home_Label"))
            {
                NavView.IsBackEnabled = (TabViewContainer.CurrentNavigationControl?.CanGoBack).GetValueOrDefault();
            }
            else if (PageDictionary[e.SourcePageType] == Globalization.GetString("MainPage_PageDictionary_SecureArea_Label"))
            {
                NavView.IsBackEnabled = (SecureAreaContainer.Current.Nav?.CanGoBack).GetValueOrDefault();
            }
            else
            {
                NavView.IsBackEnabled = false;
            }
        }

        private async Task PinApplicationToTaskBarAsync()
        {
            TaskbarManager BarManager = TaskbarManager.GetDefault();
            StartScreenManager ScreenManager = StartScreenManager.GetDefault();

            bool PinStartScreen = false, PinTaskBar = false;

            if ((await Package.Current.GetAppListEntriesAsync()).FirstOrDefault() is AppListEntry Entry)
            {
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
                        await BarManager.RequestPinCurrentAppAsync();
                        await ScreenManager.RequestAddAppListEntryAsync(Entry);
                    };
                }
                else if (PinStartScreen && !PinTaskBar)
                {
                    PinTip.ActionButtonClick += async (s, e) =>
                    {
                        s.IsOpen = false;
                        await ScreenManager.RequestAddAppListEntryAsync(Entry);
                    };
                }
                else if (!PinStartScreen && PinTaskBar)
                {
                    PinTip.ActionButtonClick += async (s, e) =>
                    {
                        s.IsOpen = false;
                        await BarManager.RequestPinCurrentAppAsync();
                    };
                }
                else
                {
                    PinTip.ActionButtonClick += (s, e) =>
                    {
                        s.IsOpen = false;
                    };
                }

                PinTip.Subtitle = Globalization.GetString("TeachingTip_PinToMenu_Subtitle");
                PinTip.IsOpen = true;
            }
        }

        private void RequestRateApplication()
        {
            RateTip.ActionButtonClick += async (s, e) =>
            {
                s.IsOpen = false;
                await SystemInformation.LaunchStoreForReviewAsync();
            };

            RateTip.CloseButtonClick += (s, e) =>
            {
                s.IsOpen = false;
            };

            RateTip.IsOpen = true;
        }

        private async Task PopDiscountPurchaseApplicationAsync()
        {
            if (!ApplicationData.Current.LocalSettings.Values.ContainsKey("DoNotShowDiscountTip_20210621_20210705"))
            {
                DateTimeOffset DiscountStartTime = DateTimeOffset.ParseExact("2021-06-21 00-00-00", "yyyy-MM-dd HH-mm-ss", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal);
                DateTimeOffset DiscountEndTime = DateTimeOffset.ParseExact("2021-07-05 00-00-00", "yyyy-MM-dd HH-mm-ss", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal);
                DateTimeOffset UTCTime = DateTimeOffset.UtcNow;

                if (UTCTime > DiscountStartTime && UTCTime < DiscountEndTime)
                {
                    if (!await MSStoreHelper.Current.CheckPurchaseStatusAsync())
                    {
                        DiscountTip.ActionButtonClick += async (s, e) =>
                        {
                            s.IsOpen = false;

                            switch (await MSStoreHelper.Current.PurchaseAsync())
                            {
                                case StorePurchaseStatus.Succeeded:
                                    {
                                        QueueContentDialog QueueContenDialog = new QueueContentDialog
                                        {
                                            Title = Globalization.GetString("Common_Dialog_TipTitle"),
                                            Content = Globalization.GetString("QueueDialog_Store_PurchaseSuccess_Content"),
                                            CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                                        };

                                        await QueueContenDialog.ShowAsync();
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

                                        await QueueContenDialog.ShowAsync();
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

                                        await QueueContenDialog.ShowAsync();
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

                                        await QueueContenDialog.ShowAsync();
                                        break;
                                    }
                            }
                        };

                        DiscountTip.CloseButtonClick += (s, e) =>
                        {
                            ApplicationData.Current.LocalSettings.Values["DoNotShowDiscountTip_20210621_20210705"] = true;
                        };

                        DiscountTip.Subtitle = Globalization.GetString("TeachingTip_DiscountTip_Subtitle");
                        DiscountTip.IsOpen = true;
                    }
                }
            }
        }

        private void PurchaseApplication()
        {
            PurchaseTip.ActionButtonClick += async (s, e) =>
            {
                s.IsOpen = false;

                switch (await MSStoreHelper.Current.PurchaseAsync())
                {
                    case StorePurchaseStatus.Succeeded:
                        {
                            QueueContentDialog QueueContenDialog = new QueueContentDialog
                            {
                                Title = Globalization.GetString("Common_Dialog_TipTitle"),
                                Content = Globalization.GetString("QueueDialog_Store_PurchaseSuccess_Content"),
                                CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                            };

                            await QueueContenDialog.ShowAsync();
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

                            await QueueContenDialog.ShowAsync();
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

                            await QueueContenDialog.ShowAsync();
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

                            await QueueContenDialog.ShowAsync();
                            break;
                        }
                }
            };

            PurchaseTip.Subtitle = Globalization.GetString("TeachingTip_PurchaseTip_Subtitle");
            PurchaseTip.IsOpen = true;
        }

        private async void NavView_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
        {
            try
            {
                if (args.IsSettingsInvoked)
                {
                    if (!SettingPage.IsOpened && FindName(nameof(Settings)) is SettingPage Dialog)
                    {
                        await Dialog.ShowAsync();
                    }
                }
                else
                {
                    string InvokeString = Convert.ToString(args.InvokedItem);

                    if (InvokeString == Globalization.GetString("MainPage_PageDictionary_Home_Label"))
                    {
                        Nav.Navigate(typeof(TabViewContainer), null, new DrillInNavigationTransitionInfo());
                    }
                    else if (InvokeString == Globalization.GetString("MainPage_PageDictionary_SecureArea_Label"))
                    {
                        Nav.Navigate(typeof(SecureAreaContainer), null, new DrillInNavigationTransitionInfo());
                    }
                    else if (InvokeString == Globalization.GetString("MainPage_PageDictionary_RecycleBin_Label"))
                    {
                        Nav.Navigate(typeof(RecycleBin), null, new DrillInNavigationTransitionInfo());
                    }
                    else if (InvokeString == Globalization.GetString("MainPage_QuickStart_Label"))
                    {
                        if (!QuickStartTip.IsOpen)
                        {
                            if (sender.PaneDisplayMode == NavigationViewPaneDisplayMode.LeftCompact)
                            {
                                QuickStartTip.Target = QuickStartIcon;
                                QuickStartTip.PreferredPlacement = TeachingTipPlacementMode.RightTop;
                                QuickStartPanelRoot.MaxHeight = Math.Min(440, Math.Max(Window.Current.Bounds.Height - QuickStartItem.TransformToVisual(Window.Current.Content).TransformPoint(new Point(0, 0)).Y - 100, 0));
                            }
                            else
                            {
                                QuickStartTip.Target = QuickStartItem;
                                QuickStartTip.PreferredPlacement = TeachingTipPlacementMode.Bottom;
                                QuickStartPanelRoot.MaxHeight = Math.Min(440, Math.Max(Window.Current.Bounds.Height - QuickStartItem.ActualHeight - QuickStartItem.TransformToVisual(Window.Current.Content).TransformPoint(new Point(0, 0)).Y - 100, 0));
                            }

                            QuickStartTip.IsOpen = true;
                        }
                    }
                    else
                    {
                        if (args.InvokedItem is RelativePanel)
                        {
                            if (!BluetoothAudioQuestionTip.IsOpen)
                            {
                                if (sender.PaneDisplayMode == NavigationViewPaneDisplayMode.LeftCompact)
                                {
                                    BluetoothAudioSelectionTip.Target = BluetoothAudioIcon;
                                    BluetoothAudioSelectionTip.PreferredPlacement = TeachingTipPlacementMode.RightTop;
                                }
                                else
                                {
                                    BluetoothAudioSelectionTip.Target = BluetoothAudioItem;
                                    BluetoothAudioSelectionTip.PreferredPlacement = TeachingTipPlacementMode.Bottom;
                                }

                                BluetoothAudioSelectionTip.IsOpen = true;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, "An error was threw when navigating between NavigationView item");
            }
        }

        private void Nav_Navigating(object sender, NavigatingCancelEventArgs e)
        {
            if (Nav.CurrentSourcePageType == e.SourcePageType)
            {
                e.Cancel = true;
            }
        }

        public void NavView_BackRequested(NavigationView sender, NavigationViewBackRequestedEventArgs args)
        {
            try
            {
                if (Nav.CurrentSourcePageType == typeof(TabViewContainer))
                {
                    if ((TabViewContainer.CurrentNavigationControl?.CanGoBack).GetValueOrDefault())
                    {
                        TabViewContainer.CurrentNavigationControl.GoBack();
                    }
                }
                else if (Nav.CurrentSourcePageType == typeof(SecureAreaContainer))
                {
                    if ((SecureAreaContainer.Current.Nav?.CanGoBack).GetValueOrDefault())
                    {
                        SecureAreaContainer.Current.Nav.GoBack();
                    }
                }

                if (NavView.MenuItems.Select((Item) => Item as NavigationViewItem).FirstOrDefault((Item) => Item.Content.ToString() == PageDictionary[Nav.CurrentSourcePageType]) is NavigationViewItem Item)
                {
                    Item.IsSelected = true;
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, "An error was threw when navigate back");
            }
        }

        private void QuickStart_DragItemsCompleted(ListViewBase sender, DragItemsCompletedEventArgs args)
        {
            QuickStartItem AppAddItem = CommonAccessCollection.QuickStartList.First((Item) => Item.Type == QuickStartType.AddButton);

            int AppAddItemIndex = CommonAccessCollection.QuickStartList.IndexOf(AppAddItem);
            int AppLastIndex = CommonAccessCollection.QuickStartList.Count - 1;

            if (AppAddItemIndex != AppLastIndex)
            {
                CommonAccessCollection.QuickStartList.Move(AppAddItemIndex, AppLastIndex);
            }

            QuickStartItem WebAddItem = CommonAccessCollection.WebLinkList.First((Item) => Item.Type == QuickStartType.AddButton);

            int WebAddItemIndex = CommonAccessCollection.WebLinkList.IndexOf(WebAddItem);
            int WebLastIndex = CommonAccessCollection.WebLinkList.Count - 1;

            if (WebAddItemIndex != WebLastIndex)
            {
                CommonAccessCollection.WebLinkList.Move(WebAddItemIndex, WebLastIndex);
            }

            if ((sender as GridView).Name == nameof(QuickStartGridView))
            {
                SQLite.Current.DeleteQuickStartItem(QuickStartType.Application);

                foreach (QuickStartItem Item in CommonAccessCollection.QuickStartList.Where((Item) => Item.Type != QuickStartType.AddButton))
                {
                    SQLite.Current.SetQuickStartItem(Item.DisplayName, Item.IconPath, Item.Protocol, QuickStartType.Application);
                }
            }
            else
            {
                SQLite.Current.DeleteQuickStartItem(QuickStartType.WebSite);

                foreach (QuickStartItem Item in CommonAccessCollection.WebLinkList.Where((Item) => Item.Type != QuickStartType.AddButton))
                {
                    SQLite.Current.SetQuickStartItem(Item.DisplayName, Item.IconPath, Item.Protocol, QuickStartType.WebSite);
                }
            }
        }

        private void QuickStart_DragItemsStarting(object sender, DragItemsStartingEventArgs e)
        {
            if (e.Items.Cast<QuickStartItem>().Any((Item) => Item.Type == QuickStartType.AddButton))
            {
                e.Cancel = true;
            }
        }

        private void BluetoothAudioSelectionTip_Closed(TeachingTip sender, TeachingTipClosedEventArgs args)
        {
            if (BluetoothAudioWatcher != null)
            {
                BluetoothAudioWatcher.Added -= Watcher_Added;
                BluetoothAudioWatcher.Removed -= Watcher_Removed;
                BluetoothAudioWatcher.Updated -= Watcher_Updated;
                BluetoothAudioWatcher.EnumerationCompleted -= Watcher_EnumerationCompleted;

                if (BluetoothAudioWatcher.Status == DeviceWatcherStatus.Started || BluetoothAudioWatcher.Status == DeviceWatcherStatus.EnumerationCompleted)
                {
                    BluetoothAudioWatcher.Stop();
                }
            }

            if (BluetoothAudioDeivceList.Items.OfType<BluetoothAudioDeviceData>().All((Device) => !Device.IsConnected))
            {
                foreach (BluetoothAudioDeviceData Device in BluetoothAudioDeivceList.Items)
                {
                    Device.Dispose();
                }

                BluetoothAudioDeivceList.Items.Clear();
            }
        }

        private void BluetoothAudioArea_Loaded(object sender, RoutedEventArgs e)
        {
            if (WindowsVersionChecker.IsNewerOrEqual(Class.Version.Windows10_2004))
            {
                BluetoothAudioArea.Visibility = Visibility.Visible;
                VerisonIncorrectTip.Visibility = Visibility.Collapsed;

                StatusText.Text = Globalization.GetString("BluetoothUI_Status_Text_1");
                BluetoothSearchProgress.IsActive = true;

                BluetoothAudioWatcher = DeviceInformation.CreateWatcher(AudioPlaybackConnection.GetDeviceSelector());

                BluetoothAudioWatcher.Added += Watcher_Added;
                BluetoothAudioWatcher.Removed += Watcher_Removed;
                BluetoothAudioWatcher.Updated += Watcher_Updated;
                BluetoothAudioWatcher.EnumerationCompleted += Watcher_EnumerationCompleted;

                BluetoothAudioWatcher.Start();
            }
            else
            {
                BluetoothAudioArea.Visibility = Visibility.Collapsed;
                VerisonIncorrectTip.Visibility = Visibility.Visible;
            }
        }

        private async void Watcher_EnumerationCompleted(DeviceWatcher sender, object args)
        {
            await Task.Delay(1000);

            await Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
            {
                BluetoothSearchProgress.IsActive = false;
                StatusText.Text = Globalization.GetString("BluetoothUI_Status_Text_2");
            });
        }

        private async void Watcher_Updated(DeviceWatcher sender, DeviceInformationUpdate args)
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
            {
                if (BluetoothAudioDeivceList.Items.OfType<BluetoothAudioDeviceData>().FirstOrDefault((Device) => Device.Id == args.Id) is BluetoothAudioDeviceData Device)
                {
                    Device.Update(args);
                }
            });
        }

        private async void Watcher_Removed(DeviceWatcher sender, DeviceInformationUpdate args)
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                if (BluetoothAudioDeivceList.Items.OfType<BluetoothAudioDeviceData>().FirstOrDefault((Device) => Device.Id == args.Id) is BluetoothAudioDeviceData Device)
                {
                    BluetoothAudioDeivceList.Items.Remove(Device);
                    Device.Dispose();
                }
            });
        }

        private async void Watcher_Added(DeviceWatcher sender, DeviceInformation args)
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
            {
                if (BluetoothAudioDeivceList.Items.OfType<BluetoothAudioDeviceData>().All((Device) => Device.Id != args.Id))
                {
                    using (DeviceThumbnail ThumbnailStream = await args.GetGlyphThumbnailAsync())
                    {

                        BitmapImage Thumbnail = new BitmapImage();
                        BluetoothAudioDeivceList.Items.Add(new BluetoothAudioDeviceData(args, Thumbnail));
                        await Thumbnail.SetSourceAsync(ThumbnailStream);
                    }
                }
            });
        }

        private async void BluetoothAudioConnectButton_Click(object sender, RoutedEventArgs e)
        {
            if (((Button)sender).DataContext is BluetoothAudioDeviceData Device)
            {
                if (Device.IsConnected)
                {
                    Device.Disconnect();
                }
                else
                {
                    await Device.ConnectAsync().ConfigureAwait(false);
                }
            }
        }

        private void BluetoothAudioQuestion_PointerPressed(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            BluetoothAudioQuestionTip.IsOpen = true;
        }


        private async void QuickStart_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is QuickStartItem Item)
            {
                try
                {
                    QuickStartTip.IsOpen = false;

                    if ((sender as GridView).Name == nameof(QuickStartGridView))
                    {
                        if (Item.Type == QuickStartType.AddButton)
                        {
                            await new QuickStartModifiedDialog(QuickStartType.Application).ShowAsync();
                        }
                        else
                        {
                            if (Uri.TryCreate(Item.Protocol, UriKind.Absolute, out Uri Ur))
                            {
                                if (Ur.IsFile)
                                {
                                    if (await FileSystemStorageItemBase.CheckExistAsync(Item.Protocol))
                                    {
                                        try
                                        {
                                            using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableControllerAsync())
                                            {
                                                if (Path.GetExtension(Item.Protocol).ToLower() == ".msc")
                                                {
                                                    if (!await Exclusive.Controller.RunAsync("powershell.exe", string.Empty, WindowState.Normal, false, true, false, "-Command", Item.Protocol))
                                                    {
                                                        QueueContentDialog Dialog = new QueueContentDialog
                                                        {
                                                            Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                                            Content = Globalization.GetString("QueueDialog_LaunchFailed_Content"),
                                                            CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                                                        };

                                                        await Dialog.ShowAsync();
                                                    }
                                                }
                                                else
                                                {
                                                    if (!await Exclusive.Controller.RunAsync(Item.Protocol, Path.GetDirectoryName(Item.Protocol)))
                                                    {
                                                        QueueContentDialog Dialog = new QueueContentDialog
                                                        {
                                                            Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                                            Content = Globalization.GetString("QueueDialog_LaunchFailed_Content"),
                                                            CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                                                        };

                                                        await Dialog.ShowAsync();
                                                    }
                                                }
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            LogTracer.Log(ex, "Could not execute program in quick start");
                                        }
                                    }
                                    else
                                    {
                                        QueueContentDialog Dialog = new QueueContentDialog
                                        {
                                            Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                            Content = Globalization.GetString("QueueDialog_ApplicationNotFound_Content"),
                                            CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                                        };

                                        await Dialog.ShowAsync();
                                    }
                                }
                                else
                                {
                                    await Launcher.LaunchUriAsync(Ur);
                                }
                            }
                            else
                            {
                                using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableControllerAsync())
                                {
                                    if (!await Exclusive.Controller.LaunchUWPFromPfnAsync(Item.Protocol))
                                    {
                                        QueueContentDialog Dialog = new QueueContentDialog
                                        {
                                            Title = Globalization.GetString("Common_Dialog_ErrorTitle"),
                                            Content = Globalization.GetString("QueueDialog_LaunchFailed_Content"),
                                            CloseButtonText = Globalization.GetString("Common_Dialog_CloseButton")
                                        };

                                        await Dialog.ShowAsync();
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        if (Item.Type == QuickStartType.AddButton)
                        {
                            await new QuickStartModifiedDialog(QuickStartType.WebSite).ShowAsync();
                        }
                        else
                        {
                            await Launcher.LaunchUriAsync(new Uri(Item.Protocol));
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogTracer.Log(ex, $"An exception was threw in {nameof(QuickStart_ItemClick)}");
                }
            }
        }

        private void QuickStartItemDelete_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as AppBarButton)?.Name == nameof(AppDelete))
            {
                if (QuickStartGridView.Tag is QuickStartItem Item)
                {
                    CommonAccessCollection.QuickStartList.Remove(Item);
                    SQLite.Current.DeleteQuickStartItem(Item);
                }
            }
            else
            {
                if (WebGridView.Tag is QuickStartItem Item)
                {
                    CommonAccessCollection.WebLinkList.Remove(Item);
                    SQLite.Current.DeleteQuickStartItem(Item);
                }
            }
        }

        private async void QuickStartItemEdit_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as AppBarButton)?.Name == nameof(AppEdit))
            {
                if (QuickStartGridView.Tag is QuickStartItem Item)
                {
                    await new QuickStartModifiedDialog(Item).ShowAsync();
                }
            }
            else
            {
                if (WebGridView.Tag is QuickStartItem Item)
                {
                    await new QuickStartModifiedDialog(Item).ShowAsync();
                }
            }
        }

        private async void AddQuickStartItem_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as AppBarButton)?.Name == nameof(AddQuickStartApp))
            {
                await new QuickStartModifiedDialog(QuickStartType.Application).ShowAsync();
            }
            else
            {
                await new QuickStartModifiedDialog(QuickStartType.WebSite).ShowAsync();
            }
        }


        private void QuickStart_RightTapped(object sender, Windows.UI.Xaml.Input.RightTappedRoutedEventArgs e)
        {
            if (e.PointerDeviceType == Windows.Devices.Input.PointerDeviceType.Mouse)
            {
                if ((sender as GridView).Name == nameof(QuickStartGridView))
                {
                    if ((e.OriginalSource as FrameworkElement)?.DataContext is QuickStartItem Item)
                    {
                        if (Item.Type == QuickStartType.AddButton)
                        {
                            QuickStartGridView.Tag = null;
                            QuickStartGridView.ContextFlyout = null;
                        }
                        else
                        {
                            QuickStartGridView.Tag = Item;
                            QuickStartGridView.ContextFlyout = AppFlyout;
                        }
                    }
                    else
                    {
                        QuickStartGridView.Tag = null;
                        QuickStartGridView.ContextFlyout = AppEmptyFlyout;
                    }
                }
                else
                {
                    if ((e.OriginalSource as FrameworkElement)?.DataContext is QuickStartItem Item)
                    {
                        if (Item.Type == QuickStartType.AddButton)
                        {
                            WebGridView.Tag = null;
                            WebGridView.ContextFlyout = null;
                        }
                        else
                        {
                            WebGridView.Tag = Item;
                            WebGridView.ContextFlyout = WebFlyout;
                        }
                    }
                    else
                    {
                        WebGridView.Tag = null;
                        WebGridView.ContextFlyout = WebEmptyFlyout;
                    }
                }
            }
        }

        private void QuickStart_Holding(object sender, Windows.UI.Xaml.Input.HoldingRoutedEventArgs e)
        {
            if (e.HoldingState == HoldingState.Started)
            {
                if ((sender as GridView).Name == nameof(QuickStartGridView))
                {
                    if ((e.OriginalSource as FrameworkElement)?.DataContext is QuickStartItem Item)
                    {
                        QuickStartGridView.Tag = Item;
                        QuickStartGridView.ContextFlyout = AppFlyout;
                    }
                    else
                    {
                        QuickStartGridView.Tag = null;
                        QuickStartGridView.ContextFlyout = AppEmptyFlyout;
                    }
                }
                else
                {
                    if ((e.OriginalSource as FrameworkElement)?.DataContext is QuickStartItem Item)
                    {
                        WebGridView.Tag = Item;
                        WebGridView.ContextFlyout = WebFlyout;
                    }
                    else
                    {
                        WebGridView.Tag = null;
                        WebGridView.ContextFlyout = WebEmptyFlyout;
                    }
                }
            }
        }

        private void QuickStart_PreviewKeyDown(object sender, Windows.UI.Xaml.Input.KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Space)
            {
                e.Handled = true;
            }
        }

        private void NavView_PaneClosing(NavigationView sender, Microsoft.UI.Xaml.Controls.NavigationViewPaneClosingEventArgs args)
        {
            if (sender.PaneDisplayMode == NavigationViewPaneDisplayMode.LeftCompact)
            {
                AppName.Translation = new System.Numerics.Vector3(42, 0, 0);
            }
        }

        private void NavView_PaneOpening(NavigationView sender, object args)
        {
            if (sender.PaneDisplayMode == NavigationViewPaneDisplayMode.LeftCompact)
            {
                AppName.Translation = new System.Numerics.Vector3(0, 0, 0);
            }
        }

        private void NavView_Loaded(object sender, RoutedEventArgs e)
        {
            if (ApplicationData.Current.LocalSettings.Values["ShouldShowRecycleBinItem"] is bool ShowRecycleBin)
            {
                RecycleBinItem.Visibility = ShowRecycleBin ? Visibility.Visible : Visibility.Collapsed;
            }
            else
            {
                RecycleBinItem.Visibility = Visibility.Visible;
            }

            if (ApplicationData.Current.LocalSettings.Values["ShouldShowQuickStartItem"] is bool ShowQuickStart)
            {
                QuickStartItem.Visibility = ShowQuickStart ? Visibility.Visible : Visibility.Collapsed;
            }
            else
            {
                QuickStartItem.Visibility = Visibility.Visible;
            }

            if (ApplicationData.Current.LocalSettings.Values["ShouldShowSecureAreaItem"] is bool ShowSecureArea)
            {
                SecureAreaItem.Visibility = ShowSecureArea ? Visibility.Visible : Visibility.Collapsed;
            }
            else
            {
                SecureAreaItem.Visibility = Visibility.Visible;
            }

            if (ApplicationData.Current.LocalSettings.Values["ShouldShowBluetoothAudioItem"] is bool ShowBluetoothAudio)
            {
                BluetoothAudioItem.Visibility = ShowBluetoothAudio ? Visibility.Visible : Visibility.Collapsed;
            }
            else
            {
                BluetoothAudioItem.Visibility = Visibility.Visible;
            }
        }

        private void NavView_RightTapped(object sender, Windows.UI.Xaml.Input.RightTappedRoutedEventArgs e)
        {
            switch (e.OriginalSource)
            {
                case Grid Gr when Gr.Name == "LayoutRoot" || Gr.Name == "PaneRoot":
                case DependencyObject Obj when Obj.FindParentOfType<NavigationViewItem>() != null:
                    {
                        NavigationViewFlyout.ShowAt((FrameworkElement)sender, new FlyoutShowOptions
                        {
                            Position = e.GetPosition(NavView),
                            Placement = FlyoutPlacementMode.RightEdgeAlignedTop,
                            ShowMode = FlyoutShowMode.Standard
                        });

                        break;
                    }
            }
        }

        private async void EditNavItem_Click(object sender, RoutedEventArgs e)
        {
            EditNavigationViewItemDialog Dialog = new EditNavigationViewItemDialog();

            if (await Dialog.ShowAsync() == ContentDialogResult.Primary)
            {
                if (ApplicationData.Current.LocalSettings.Values["ShouldShowRecycleBinItem"] is bool ShowRecycleBin)
                {
                    RecycleBinItem.Visibility = ShowRecycleBin ? Visibility.Visible : Visibility.Collapsed;
                }

                if (ApplicationData.Current.LocalSettings.Values["ShouldShowQuickStartItem"] is bool ShowQuickStart)
                {
                    QuickStartItem.Visibility = ShowQuickStart ? Visibility.Visible : Visibility.Collapsed;
                }

                if (ApplicationData.Current.LocalSettings.Values["ShouldShowSecureAreaItem"] is bool ShowSecureArea)
                {
                    SecureAreaItem.Visibility = ShowSecureArea ? Visibility.Visible : Visibility.Collapsed;
                }

                if (ApplicationData.Current.LocalSettings.Values["ShouldShowBluetoothAudioItem"] is bool ShowBluetoothAudio)
                {
                    BluetoothAudioItem.Visibility = ShowBluetoothAudio ? Visibility.Visible : Visibility.Collapsed;
                }

                ApplicationData.Current.SignalDataChanged();
            }
        }

        private void NavView_Holding(object sender, Windows.UI.Xaml.Input.HoldingRoutedEventArgs e)
        {
            if (e.HoldingState == HoldingState.Started)
            {
                switch (e.OriginalSource)
                {
                    case Grid Gr when Gr.Name == "LayoutRoot" || Gr.Name == "PaneRoot":
                    case DependencyObject Obj when Obj.FindParentOfType<NavigationViewItem>() != null:
                        {
                            NavigationViewFlyout.ShowAt((FrameworkElement)sender, new FlyoutShowOptions
                            {
                                Position = e.GetPosition(NavView),
                                Placement = FlyoutPlacementMode.RightEdgeAlignedTop,
                                ShowMode = FlyoutShowMode.Standard
                            });

                            break;
                        }
                }
            }
        }
    }
}
