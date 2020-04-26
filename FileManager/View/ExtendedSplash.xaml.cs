using Microsoft.Toolkit.Uwp.Notifications;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.Activation;
using Windows.ApplicationModel.Core;
using Windows.Foundation;
using Windows.Storage;
using Windows.System;
using Windows.UI.Core;
using Windows.UI.Notifications;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace FileManager
{
    public sealed partial class ExtendedSplash : Page
    {
        private Rect SplashImageRect;

        private readonly SplashScreen Splash;

        private AutoResetEvent ReleaseLock = new AutoResetEvent(false);

        private string USBActivateParameter = null;

        public ExtendedSplash(SplashScreen Screen, bool IsPreLaunch = false, string USBActivateParameter = null)
        {
            InitializeComponent();
            Window.Current.SetTitleBar(TitleBar);

#if DEBUG
            AppName.Text += " (Debug 模式)";
#endif

            Splash = Screen ?? throw new ArgumentNullException(nameof(Screen), "Parameter could not be null");
            SplashImageRect = Screen.ImageLocation;

            SetControlPosition();

            if (IsPreLaunch)
            {
                PreLaunchInitialize();
            }
            else
            {
                Splash.Dismissed += Screen_Dismissed;
            }

            Loaded += ExtendedSplash_Loaded;
            Unloaded += ExtendedSplash_Unloaded;

            if (!string.IsNullOrEmpty(USBActivateParameter))
            {
                this.USBActivateParameter = USBActivateParameter;
            }
        }

        private void ExtendedSplash_Unloaded(object sender, RoutedEventArgs e)
        {
            Window.Current.SizeChanged -= Current_SizeChanged;
        }

        private void ExtendedSplash_Loaded(object sender, RoutedEventArgs e)
        {
            Window.Current.SizeChanged += Current_SizeChanged;
        }

        private void SetControlPosition()
        {
            double HorizonLocation = SplashImageRect.X + (SplashImageRect.Width * 0.5);
            double VerticalLocation = SplashImageRect.Y + (SplashImageRect.Height * 0.7);

            Display.SetValue(Canvas.LeftProperty, HorizonLocation - (Display.Width * 0.5));
            Display.SetValue(Canvas.TopProperty, VerticalLocation + 20);

            ButtonPane.SetValue(Canvas.LeftProperty, HorizonLocation - (ButtonPane.Width * 0.5));
            ButtonPane.SetValue(Canvas.TopProperty, VerticalLocation + Display.Height + 30);

            extendedSplashImage.SetValue(Canvas.LeftProperty, SplashImageRect.X);
            extendedSplashImage.SetValue(Canvas.TopProperty, SplashImageRect.Y);
            extendedSplashImage.Height = SplashImageRect.Height;
            extendedSplashImage.Width = SplashImageRect.Width;
        }

        private async Task DismissExtendedSplashAsync()
        {
            try
            {
                ReleaseLock.Dispose();
                ReleaseLock = null;

                await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    Frame rootFrame = new Frame();
                    Window.Current.Content = rootFrame;

                    if (string.IsNullOrEmpty(USBActivateParameter))
                    {
                        rootFrame.Navigate(typeof(MainPage), SplashImageRect);
                    }
                    else
                    {
                        rootFrame.Navigate(typeof(MainPage), new Tuple<string, Rect>(USBActivateParameter, SplashImageRect));
                    }
                });
            }
            catch (Exception ex)
            {
                ExceptionTracer.RequestBlueScreen(ex);
            }
        }

        private void DismissExtendedSplash()
        {
            try
            {
                ReleaseLock.Dispose();
                ReleaseLock = null;

                Frame rootFrame = new Frame();
                Window.Current.Content = rootFrame;
                rootFrame.Navigate(typeof(MainPage), SplashImageRect);
            }
            catch (Exception ex)
            {
                ExceptionTracer.RequestBlueScreen(ex);
            }
        }

        private void PreLaunchInitialize()
        {
            bool IsFileAccessible = CheckFileAccessAuthority().Result;

            if (IsFileAccessible)
            {
                DismissExtendedSplash();
            }
            else
            {
                Window.Current.Content = this;

                Display.Text = Globalization.Language == LanguageEnum.Chinese
                                ? "请开启此应用的文件系统访问权限以正常工作\r然后重新启动该应用"
                                : "Please enable file system access for this app to work properly\rThen restart the app";
                ButtonPane.Visibility = Visibility.Visible;
            }
        }

        private async void Screen_Dismissed(SplashScreen sender, object args)
        {
            try
            {
                string CurrentLanguageString = Windows.System.UserProfile.GlobalizationPreferences.Languages[0];

                if (ApplicationData.Current.LocalSettings.Values["LastStartupLanguage"] is string LastLanguageString)
                {
                    if (CurrentLanguageString != LastLanguageString)
                    {
                        ApplicationData.Current.LocalSettings.Values.Clear();
                        ApplicationData.Current.LocalSettings.Values["LastStartupLanguage"] = CurrentLanguageString;

                        if (CurrentLanguageString.StartsWith("zh"))
                        {
                            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
                            {
                                QueueContentDialog dialog = new QueueContentDialog
                                {
                                    Title = "提示",
                                    Content = "    自上次启动以来，系统语言设置发生了更改\r\r    语言更改:  " + LastLanguageString + " ⋙⋙⋙⋙ " + CurrentLanguageString + "\r\r    为了保证程序正常运行，RX已将所有已保存设置还原为默认值",
                                    CloseButtonText = "确定"
                                };
                                _ = await dialog.ShowAsync().ConfigureAwait(true);
                                ReleaseLock.Set();
                            });
                        }
                        else
                        {
                            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
                            {
                                QueueContentDialog dialog = new QueueContentDialog
                                {
                                    Title = "Tips",
                                    Content = "    The system language setting has changed since the last boot\r\r    Language changes:  " + LastLanguageString + " ⋙⋙⋙⋙ " + CurrentLanguageString + "\r\r    To ensure the program is running properly\r    RX has restored all saved settings to their default values",
                                    CloseButtonText = "Got it"
                                };
                                _ = await dialog.ShowAsync().ConfigureAwait(true);
                                ReleaseLock.Set();
                            });
                        }
                    }
                    else
                    {
                        ReleaseLock.Set();
                    }
                }
                else
                {
                    ApplicationData.Current.LocalSettings.Values["LastStartupLanguage"] = CurrentLanguageString;
                    ReleaseLock.Set();
                }

                ReleaseLock.WaitOne();

                if (!ApplicationData.Current.LocalSettings.Values.ContainsKey("QuickStartInitialFinished"))
                {
                    await SQLite.Current.ClearTableAsync("QuickStart").ConfigureAwait(false);

                    if (Globalization.Language == LanguageEnum.Chinese)
                    {
                        await SQLite.Current.SetQuickStartItemAsync("应用商店", "ms-appx:///QuickStartImage/MicrosoftStore.png", "ms-windows-store://home", QuickStartType.Application).ConfigureAwait(false);
                        await SQLite.Current.SetQuickStartItemAsync("计算器", "ms-appx:///QuickStartImage/Calculator.png", "calculator:", QuickStartType.Application).ConfigureAwait(false);
                        await SQLite.Current.SetQuickStartItemAsync("系统设置", "ms-appx:///QuickStartImage/Setting.png", "ms-settings:", QuickStartType.Application).ConfigureAwait(false);
                        await SQLite.Current.SetQuickStartItemAsync("邮件", "ms-appx:///QuickStartImage/Email.png", "mailto:", QuickStartType.Application).ConfigureAwait(false);
                        await SQLite.Current.SetQuickStartItemAsync("日历", "ms-appx:///QuickStartImage/Calendar.png", "outlookcal:", QuickStartType.Application).ConfigureAwait(false);
                        await SQLite.Current.SetQuickStartItemAsync("必应地图", "ms-appx:///QuickStartImage/Map.png", "bingmaps:", QuickStartType.Application).ConfigureAwait(false);
                        await SQLite.Current.SetQuickStartItemAsync("天气", "ms-appx:///QuickStartImage/Weather.png", "bingweather:", QuickStartType.Application).ConfigureAwait(false);
                        await SQLite.Current.SetQuickStartItemAsync("必应", "ms-appx:///HotWebImage/Bing.png", "https://www.bing.com/", QuickStartType.WebSite).ConfigureAwait(false);
                        await SQLite.Current.SetQuickStartItemAsync("百度", "ms-appx:///HotWebImage/Baidu.png", "https://www.baidu.com/", QuickStartType.WebSite).ConfigureAwait(false);
                        await SQLite.Current.SetQuickStartItemAsync("微信", "ms-appx:///HotWebImage/Wechat.png", "https://wx.qq.com/", QuickStartType.WebSite).ConfigureAwait(false);
                        await SQLite.Current.SetQuickStartItemAsync("IT之家", "ms-appx:///HotWebImage/iThome.jpg", "https://www.ithome.com/", QuickStartType.WebSite).ConfigureAwait(false);
                        await SQLite.Current.SetQuickStartItemAsync("微博", "ms-appx:///HotWebImage/Weibo.png", "https://www.weibo.com/", QuickStartType.WebSite).ConfigureAwait(false);
                    }
                    else
                    {
                        await SQLite.Current.SetQuickStartItemAsync("Microsoft Store", "ms-appx:///QuickStartImage/MicrosoftStore.png", "ms-windows-store://home", QuickStartType.Application).ConfigureAwait(false);
                        await SQLite.Current.SetQuickStartItemAsync("Calculator", "ms-appx:///QuickStartImage/Calculator.png", "calculator:", QuickStartType.Application).ConfigureAwait(false);
                        await SQLite.Current.SetQuickStartItemAsync("Setting", "ms-appx:///QuickStartImage/Setting.png", "ms-settings:", QuickStartType.Application).ConfigureAwait(false);
                        await SQLite.Current.SetQuickStartItemAsync("Email", "ms-appx:///QuickStartImage/Email.png", "mailto:", QuickStartType.Application).ConfigureAwait(false);
                        await SQLite.Current.SetQuickStartItemAsync("Calendar", "ms-appx:///QuickStartImage/Calendar.png", "outlookcal:", QuickStartType.Application).ConfigureAwait(false);
                        await SQLite.Current.SetQuickStartItemAsync("Bing Map", "ms-appx:///QuickStartImage/Map.png", "bingmaps:", QuickStartType.Application).ConfigureAwait(false);
                        await SQLite.Current.SetQuickStartItemAsync("Weather", "ms-appx:///QuickStartImage/Weather.png", "bingweather:", QuickStartType.Application).ConfigureAwait(false);
                        await SQLite.Current.SetQuickStartItemAsync("Bing", "ms-appx:///HotWebImage/Bing.png", "https://www.bing.com/", QuickStartType.WebSite).ConfigureAwait(false);
                        await SQLite.Current.SetQuickStartItemAsync("Facebook", "ms-appx:///HotWebImage/Facebook.png", "https://www.facebook.com/", QuickStartType.WebSite).ConfigureAwait(false);
                        await SQLite.Current.SetQuickStartItemAsync("Instagram", "ms-appx:///HotWebImage/Instagram.png", "https://www.instagram.com/", QuickStartType.WebSite).ConfigureAwait(false);
                        await SQLite.Current.SetQuickStartItemAsync("Twitter", "ms-appx:///HotWebImage/Twitter.png", "https://twitter.com", QuickStartType.WebSite).ConfigureAwait(false);
                    }

                    ApplicationData.Current.LocalSettings.Values["QuickStartInitialFinished"] = true;
                }

                bool IsFileAccessible = await CheckFileAccessAuthority().ConfigureAwait(false);

                if (IsFileAccessible)
                {
                    await DismissExtendedSplashAsync().ConfigureAwait(false);
                }
                else
                {
                    await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                    {

                        Display.Text = Globalization.Language == LanguageEnum.Chinese
                                        ? "请开启此应用的文件系统访问权限以正常工作\r然后重新启动该应用"
                                        : "Please enable file system access for this app to work properly\rThen restart the app";
                        ButtonPane.Visibility = Visibility.Visible;
                    });
                }
            }
            catch (Exception ex)
            {
                ExceptionTracer.RequestBlueScreen(ex);
            }
        }

        private void Current_SizeChanged(object sender, WindowSizeChangedEventArgs e)
        {
            if (Splash != null)
            {
                SplashImageRect = Splash.ImageLocation;
                SetControlPosition();
            }
        }

        private Task<bool> CheckFileAccessAuthority()
        {
            return Task.Run(() =>
            {
                try
                {
                    _ = StorageFolder.GetFolderFromPathAsync(Environment.GetLogicalDrives().FirstOrDefault()).AsTask().Result;
                    return true;
                }
                catch
                {
                    return false;
                }
            });
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Exit();
        }

        private async void NavigationButton_Click(object sender, RoutedEventArgs e)
        {
            await Launcher.LaunchUriAsync(new Uri("ms-settings:privacy-broadfilesystemaccess"));
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
                                    Text = "正在等待用户完成操作..."
                                },

                                new AdaptiveText()
                                {
                                    Text = "请开启文件系统权限"
                                },

                                new AdaptiveText()
                                {
                                    Text = "随后点击下方的立即启动"
                                }
                            }
                        }
                    },

                    Actions = new ToastActionsCustom
                    {
                        Buttons =
                        {
                            new ToastButton("立即启动","Restart")
                            {
                                ActivationType =ToastActivationType.Foreground
                            },
                            new ToastButtonDismiss("稍后")
                        }
                    }
                };
                ToastNotificationManager.CreateToastNotifier().Show(new ToastNotification(Content.GetXml()));
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
                                    Text = "Waiting for user to complete operation..."
                                },

                                new AdaptiveText()
                                {
                                    Text = "Please turn on file system permissions"
                                },

                                new AdaptiveText()
                                {
                                    Text = "Then click on the launch below to start immediately"
                                }
                            }
                        }
                    },

                    Actions = new ToastActionsCustom
                    {
                        Buttons =
                        {
                            new ToastButton("Launch","Restart")
                            {
                                ActivationType =ToastActivationType.Foreground
                            },
                            new ToastButtonDismiss("Later")
                        }
                    }
                };
                ToastNotificationManager.CreateToastNotifier().Show(new ToastNotification(Content.GetXml()));
            }

            Application.Current.Exit();
        }
    }
}
