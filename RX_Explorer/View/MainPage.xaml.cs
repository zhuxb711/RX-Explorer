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
using Windows.Devices.Enumeration;
using Windows.Foundation;
using Windows.Media.Audio;
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
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;
using NavigationView = Microsoft.UI.Xaml.Controls.NavigationView;
using NavigationViewBackRequestedEventArgs = Microsoft.UI.Xaml.Controls.NavigationViewBackRequestedEventArgs;
using NavigationViewItem = Microsoft.UI.Xaml.Controls.NavigationViewItem;
using NavigationViewItemInvokedEventArgs = Microsoft.UI.Xaml.Controls.NavigationViewItemInvokedEventArgs;

namespace RX_Explorer
{
    public sealed partial class MainPage : Page
    {
        public static MainPage ThisPage { get; private set; }

        private Dictionary<Type, string> PageDictionary;

        public List<string[]> ActivatePathArray { get; private set; }

        private EntranceAnimationEffect EntranceEffectProvider;

        private DeviceWatcher BluetoothAudioWatcher;

        public MainPage(Rect Parameter, List<string[]> ActivatePathArray = null)
        {
            InitializeComponent();
            ThisPage = this;
            Window.Current.SetTitleBar(TitleBar);
            Application.Current.FocusVisualKind = FocusVisualKind.Reveal;
            Loaded += MainPage_Loaded;
            Loaded += MainPage_Loaded1;
            Window.Current.Activated += MainPage_Activated;
            Application.Current.EnteredBackground += Current_EnteredBackground;
            Application.Current.LeavingBackground += Current_LeavingBackground;
            SystemNavigationManagerPreview.GetForCurrentView().CloseRequested += MainPage_CloseRequested;
            SystemNavigationManager.GetForCurrentView().BackRequested += MainPage_BackRequested;

            BackgroundController.Current.SetAcrylicEffectPresenter(CompositorAcrylicBackground);

            if (Package.Current.IsDevelopmentMode)
            {
                AppName.Text += " (Development Mode)";
            }

            this.ActivatePathArray = ActivatePathArray;

            if (!AnimationController.Current.IsDisableStartupAnimation && (ActivatePathArray?.Count).GetValueOrDefault() == 0)
            {
                EntranceEffectProvider = new EntranceAnimationEffect(this, Nav, Parameter);
                EntranceEffectProvider.PrepareEntranceEffect();
            }
        }

        private async void Current_DataChanged(ApplicationData sender, object args)
        {
            ApplicationData.Current.DataChanged -= Current_DataChanged;

            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
            {
                if (FindName(nameof(SettingControl)) is SettingControl Control)
                {
                    await Control.Initialize().ConfigureAwait(true);
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
            if (ApplicationData.Current.LocalSettings.Values["BackgroundBlurValue"] is float BlurValue)
            {
                switch (BackgroundController.Current.CurrentType)
                {
                    case BackgroundBrushType.BingPicture:
                    case BackgroundBrushType.Picture:
                        {
                            BackgroundBlur.Amount = BlurValue / 5;
                            break;
                        }
                    default:
                        {
                            BackgroundBlur.Amount = 0;
                            break;
                        }
                }
            }
            else
            {
                ApplicationData.Current.LocalSettings.Values["BackgroundBlurValue"] = 0d;
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

            if (!ApplicationData.Current.LocalSettings.Values.ContainsKey("DefaultTerminal"))
            {
                ApplicationData.Current.LocalSettings.Values["DefaultTerminal"] = "Powershell";
                switch (await Launcher.QueryUriSupportAsync(new Uri("ms-windows-store:"), LaunchQuerySupportType.Uri, "Microsoft.WindowsTerminal_8wekyb3d8bbwe"))
                {
                    case LaunchQuerySupportStatus.Available:
                    case LaunchQuerySupportStatus.NotSupported:
                        {
                            await SQLite.Current.SetOrModifyTerminalProfile(new TerminalProfile("Windows Terminal", "wt.exe", "/d [CurrentLocation]", true)).ConfigureAwait(true);
                            break;
                        }
                }
            }

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

            if (!ApplicationData.Current.LocalSettings.Values.ContainsKey("AlwaysStartNew"))
            {
                ApplicationData.Current.LocalSettings.Values["AlwaysStartNew"] = true;
            }
        }

        private void MainPage_BackRequested(object sender, BackRequestedEventArgs e)
        {
            NavView_BackRequested(null, null);

            e.Handled = true;
        }

        private void Current_LeavingBackground(object sender, LeavingBackgroundEventArgs e)
        {
            ToastNotificationManager.History.Remove("EnterBackgroundTips");
        }

        private void Current_EnteredBackground(object sender, EnteredBackgroundEventArgs e)
        {
            if (FullTrustProcessController.IsAnyActionExcutingInAllController || GeneralTransformer.IsAnyTransformTaskRunning)
            {
                ToastNotificationManager.History.Remove("EnterBackgroundTips");

                ToastContentBuilder Builder = new ToastContentBuilder()
                                              .SetToastScenario(ToastScenario.Alarm)
                                              .AddToastActivationInfo("EnterBackgroundTips", ToastActivationType.Foreground)
                                              .AddText(Globalization.GetString("Toast_EnterBackground_Text_1"))
                                              .AddText(Globalization.GetString("Toast_EnterBackground_Text_2"))
                                              .AddText(Globalization.GetString("Toast_EnterBackground_Text_3"));

                ToastNotificationManager.CreateToastNotifier().Show(new ToastNotification(Builder.GetToastContent().GetXml())
                {
                    Tag = "EnterBackgroundTips",
                    Priority = ToastNotificationPriority.High
                });
            }
        }

        private async void MainPage_CloseRequested(object sender, SystemNavigationCloseRequestedPreviewEventArgs e)
        {
            Deferral Deferral = e.GetDeferral();

            try
            {
                if (GeneralTransformer.IsAnyTransformTaskRunning || FullTrustProcessController.IsAnyActionExcutingInAllController)
                {
                    QueueContentDialog Dialog = new QueueContentDialog
                    {
                        Title = Globalization.GetString("Common_Dialog_WarningTitle"),
                        Content = Globalization.GetString("QueueDialog_WaitUntilFinish_Content"),
                        PrimaryButtonText = Globalization.GetString("QueueDialog_WaitUntilFinish_PrimaryButton"),
                        CloseButtonText = Globalization.GetString("QueueDialog_WaitUntilFinish_CloseButton")
                    };

                    if ((await Dialog.ShowAsync().ConfigureAwait(true)) == ContentDialogResult.Primary)
                    {
                        ToastNotificationManager.History.Clear();
                    }
                    else
                    {
                        e.Handled = true;
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
                PageDictionary = new Dictionary<Type, string>()
                {
                    {typeof(TabViewContainer),Globalization.GetString("MainPage_PageDictionary_ThisPC_Label") },
                    {typeof(FileControl),Globalization.GetString("MainPage_PageDictionary_ThisPC_Label") },
                    {typeof(SecureArea),Globalization.GetString("MainPage_PageDictionary_SecureArea_Label") },
                    {typeof(RecycleBin),Globalization.GetString("MainPage_PageDictionary_RecycleBin_Label") }
                };

                Nav.Navigate(typeof(TabViewContainer), null, new SuppressNavigationTransitionInfo());

                if (!AnimationController.Current.IsDisableStartupAnimation && (ActivatePathArray?.Count).GetValueOrDefault() == 0)
                {
                    await Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                    {
                        EntranceEffectProvider.StartEntranceEffect();
                    });
                }

                ApplicationData.Current.DataChanged += Current_DataChanged;

                await GetUserInfoAsync().ConfigureAwait(true);

                await ShowReleaseLogDialogAsync().ConfigureAwait(true);

                await RegisterBackgroundTaskAsync().ConfigureAwait(true);

                switch (Microsoft.Toolkit.Uwp.Helpers.SystemInformation.LaunchCount)
                {
                    case 10:
                        {
                            await PinApplicationToTaskBarAsync().ConfigureAwait(true);
                            break;
                        }
                    case 20:
                        {
                            RequestRateApplication();
                            break;
                        }
                    case 30:
                        {
                            await PurchaseApplicationAsync().ConfigureAwait(true);
                            break;
                        }
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex);
            }
        }

        private async Task ShowReleaseLogDialogAsync()
        {
            if (Microsoft.Toolkit.Uwp.Helpers.SystemInformation.IsAppUpdated || Microsoft.Toolkit.Uwp.Helpers.SystemInformation.IsFirstRun)
            {
                WhatIsNew Dialog = new WhatIsNew();
                await Dialog.ShowAsync().ConfigureAwait(true);
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
                    HardwareToken Token = HardwareIdentification.GetPackageSpecificToken(null);
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
                HardwareToken Token = HardwareIdentification.GetPackageSpecificToken(null);
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

                PinTip.Subtitle = Globalization.GetString("TeachingTip_PinToMenu_Subtitle");
                PinTip.IsOpen = true;
            }
        }

        private void RequestRateApplication()
        {
            RateTip.ActionButtonClick += async (s, e) =>
            {
                s.IsOpen = false;
                await Microsoft.Toolkit.Uwp.Helpers.SystemInformation.LaunchStoreForReviewAsync().ConfigureAwait(true);
            };

            RateTip.CloseButtonClick += (s, e) =>
            {
                s.IsOpen = false;
            };

            RateTip.IsOpen = true;
        }

        private async Task PurchaseApplicationAsync()
        {
            if (!await MSStoreHelper.Current.CheckPurchaseStatusAsync().ConfigureAwait(true))
            {
                PurchaseTip.ActionButtonClick += async (s, e) =>
                {
                    s.IsOpen = false;

                    switch (await MSStoreHelper.Current.PurchaseAsync().ConfigureAwait(true))
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
                };

                PurchaseTip.Subtitle = Globalization.GetString("TeachingTip_PurchaseTip_Subtitle");
                PurchaseTip.IsOpen = true;
                ApplicationData.Current.LocalSettings.Values["IsDonated"] = false;
            }
        }

        private async void NavView_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
        {
            try
            {
                if (args.IsSettingsInvoked)
                {
                    NavView.IsBackEnabled = true;

                    if (FindName(nameof(SettingControl)) is SettingControl Control)
                    {
                        await Control.Show().ConfigureAwait(true);
                    }
                }
                else
                {
                    if (SettingControl != null)
                    {
                        await SettingControl.Hide().ConfigureAwait(true);
                    }

                    if (args.InvokedItem.ToString() == Globalization.GetString("MainPage_PageDictionary_ThisPC_Label"))
                    {
                        NavView.IsBackEnabled = (TabViewContainer.CurrentNavigationControl?.CanGoBack).GetValueOrDefault();

                        Nav.Navigate(typeof(TabViewContainer), null, new DrillInNavigationTransitionInfo());
                    }
                    else if (args.InvokedItem is StackPanel)
                    {
                        if (!BluetoothAudioQuestionTip.IsOpen)
                        {
                            BluetoothAudioSelectionTip.IsOpen = true;
                        }
                    }
                    else
                    {
                        NavView.IsBackEnabled = false;

                        if (args.InvokedItem.ToString() == Globalization.GetString("MainPage_PageDictionary_SecureArea_Label"))
                        {
                            Nav.Navigate(typeof(SecureArea), null, new DrillInNavigationTransitionInfo());
                        }
                        else if (args.InvokedItem.ToString() == Globalization.GetString("MainPage_PageDictionary_RecycleBin_Label"))
                        {
                            Nav.Navigate(typeof(RecycleBin), null, new DrillInNavigationTransitionInfo());
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

        public async void NavView_BackRequested(NavigationView sender, NavigationViewBackRequestedEventArgs args)
        {
            try
            {
                if ((SettingControl?.IsOpened).GetValueOrDefault() || (SettingControl?.IsAnimating).GetValueOrDefault())
                {
                    if (Nav.CurrentSourcePageType == typeof(TabViewContainer))
                    {
                        NavView.IsBackEnabled = (TabViewContainer.CurrentNavigationControl?.CanGoBack).GetValueOrDefault();
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
                    if (TabViewContainer.CurrentNavigationControl.CanGoBack)
                    {
                        TabViewContainer.CurrentNavigationControl.GoBack();
                    }
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, "An error was threw when navigate back");
            }
        }

        private void BluetoothAudioSelectionTip_Closed(Microsoft.UI.Xaml.Controls.TeachingTip sender, Microsoft.UI.Xaml.Controls.TeachingTipClosedEventArgs args)
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
            await Task.Delay(1000).ConfigureAwait(true);

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
    }
}
