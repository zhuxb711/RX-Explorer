using Microsoft.Toolkit.Uwp.Notifications;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel;
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
        internal Rect SplashImageRect;

        private readonly SplashScreen Splash;

        private AutoResetEvent ReleaseLock;

        private string USBActivateParameter = null;

        public ExtendedSplash(SplashScreen Screen, string USBActivateParameter = null)
        {
            InitializeComponent();

            ReleaseLock = new AutoResetEvent(false);

            Window.Current.SizeChanged += Current_SizeChanged;
            Splash = Screen;

            if (Screen != null)
            {
                Screen.Dismissed += Screen_Dismissed;

                SplashImageRect = Screen.ImageLocation;

                SetControlPosition();
            }

            if (!string.IsNullOrEmpty(USBActivateParameter))
            {
                this.USBActivateParameter = USBActivateParameter;
            }
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

        private async void DismissExtendedSplash()
        {
            try
            {
                ReleaseLock.Dispose();
                ReleaseLock = null;

                await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    var rootFrame = new Frame();
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

        private async void Screen_Dismissed(SplashScreen sender, object args)
        {
            try
            {
                string CurrentLanguageString = Windows.System.UserProfile.GlobalizationPreferences.Languages.FirstOrDefault();

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
                                _ = await dialog.ShowAsync();
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
                                _ = await dialog.ShowAsync();
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

                if (!(ApplicationData.Current.LocalSettings.Values["IsInitialQuickStart"] is bool) || !(ApplicationData.Current.LocalSettings.Values["QuickStartInitialFinished"] is bool))
                {
                    var SQL = SQLite.Current;
                    await SQL.ClearTableAsync("QuickStart");
                    ApplicationData.Current.LocalSettings.Values["IsInitialQuickStart"] = true;

                    var QuickFolder = await ApplicationData.Current.LocalFolder.CreateFolderAsync("QuickStartImage", CreationCollisionOption.ReplaceExisting);
                    foreach (var File in await (await Package.Current.InstalledLocation.GetFolderAsync("QuickStartImage")).GetFilesAsync())
                    {
                        _ = await File.CopyAsync(QuickFolder, File.Name, NameCollisionOption.ReplaceExisting);
                    }

                    var WebFolder = await ApplicationData.Current.LocalFolder.CreateFolderAsync("HotWebImage", CreationCollisionOption.ReplaceExisting);
                    foreach (var File in await (await Package.Current.InstalledLocation.GetFolderAsync("HotWebImage")).GetFilesAsync())
                    {
                        _ = await File.CopyAsync(WebFolder, File.Name, NameCollisionOption.ReplaceExisting);
                    }

                    if (Windows.System.UserProfile.GlobalizationPreferences.Languages.FirstOrDefault().StartsWith("zh"))
                    {
                        await SQL.SetQuickStartItemAsync("应用商店", "QuickStartImage\\MicrosoftStore.png", "ms-windows-store://home", QuickStartType.Application);
                        await SQL.SetQuickStartItemAsync("计算器", "QuickStartImage\\Calculator.png", "calculator:", QuickStartType.Application);
                        await SQL.SetQuickStartItemAsync("系统设置", "QuickStartImage\\Setting.png", "ms-settings:", QuickStartType.Application);
                        await SQL.SetQuickStartItemAsync("邮件", "QuickStartImage\\Email.png", "mailto:", QuickStartType.Application);
                        await SQL.SetQuickStartItemAsync("日历", "QuickStartImage\\Calendar.png", "outlookcal:", QuickStartType.Application);
                        await SQL.SetQuickStartItemAsync("必应地图", "QuickStartImage\\Map.png", "bingmaps:", QuickStartType.Application);
                        await SQL.SetQuickStartItemAsync("天气", "QuickStartImage\\Weather.png", "bingweather:", QuickStartType.Application);
                        await SQL.SetQuickStartItemAsync("必应", "HotWebImage\\Bing.png", "https://www.bing.com/", QuickStartType.WebSite);
                        await SQL.SetQuickStartItemAsync("百度", "HotWebImage\\Baidu.png", "https://www.baidu.com/", QuickStartType.WebSite);
                        await SQL.SetQuickStartItemAsync("微信", "HotWebImage\\Wechat.png", "https://wx.qq.com/", QuickStartType.WebSite);
                        await SQL.SetQuickStartItemAsync("IT之家", "HotWebImage\\iThome.jpg", "https://www.ithome.com/", QuickStartType.WebSite);
                        await SQL.SetQuickStartItemAsync("微博", "HotWebImage\\Weibo.png", "https://www.weibo.com/", QuickStartType.WebSite);
                    }
                    else
                    {
                        await SQL.SetQuickStartItemAsync("Microsoft Store", "QuickStartImage\\MicrosoftStore.png", "ms-windows-store://home", QuickStartType.Application);
                        await SQL.SetQuickStartItemAsync("Calculator", "QuickStartImage\\Calculator.png", "calculator:", QuickStartType.Application);
                        await SQL.SetQuickStartItemAsync("Setting", "QuickStartImage\\Setting.png", "ms-settings:", QuickStartType.Application);
                        await SQL.SetQuickStartItemAsync("Email", "QuickStartImage\\Email.png", "mailto:", QuickStartType.Application);
                        await SQL.SetQuickStartItemAsync("Calendar", "QuickStartImage\\Calendar.png", "outlookcal:", QuickStartType.Application);
                        await SQL.SetQuickStartItemAsync("Bing Map", "QuickStartImage\\Map.png", "bingmaps:", QuickStartType.Application);
                        await SQL.SetQuickStartItemAsync("Weather", "QuickStartImage\\Weather.png", "bingweather:", QuickStartType.Application);
                        await SQL.SetQuickStartItemAsync("Bing", "HotWebImage\\Bing.png", "https://www.bing.com/", QuickStartType.WebSite);
                        await SQL.SetQuickStartItemAsync("Facebook", "HotWebImage\\Facebook.png", "https://www.facebook.com/", QuickStartType.WebSite);
                        await SQL.SetQuickStartItemAsync("Instagram", "HotWebImage\\Instagram.png", "https://www.instagram.com/", QuickStartType.WebSite);
                        await SQL.SetQuickStartItemAsync("Twitter", "HotWebImage\\Twitter.png", "https://twitter.com", QuickStartType.WebSite);
                    }

                    ApplicationData.Current.LocalSettings.Values["QuickStartInitialFinished"] = true;
                }

                if (await CheckFileAccessAuthority())
                {
                    DismissExtendedSplash();
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

        private async Task<bool> CheckFileAccessAuthority()
        {
            try
            {
                _ = await StorageFolder.GetFolderFromPathAsync(Environment.GetLogicalDrives().FirstOrDefault());
                return true;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Exit();
        }

        private async void NavigationButton_Click(object sender, RoutedEventArgs e)
        {
            await Launcher.LaunchUriAsync(new Uri("ms-settings:privacy-broadfilesystemaccess"));
            if (Windows.System.UserProfile.GlobalizationPreferences.Languages.FirstOrDefault().StartsWith("zh"))
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
