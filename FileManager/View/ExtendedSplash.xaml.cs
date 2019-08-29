using Microsoft.Toolkit.Uwp.Notifications;
using System;
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

// https://go.microsoft.com/fwlink/?LinkId=234238 上介绍了“空白页”项模板

namespace FileManager
{
    public sealed partial class ExtendedSplash : Page
    {
        internal Rect SplashImageRect;
        private SplashScreen Splash;
        public ExtendedSplash(SplashScreen Screen)
        {
            InitializeComponent();

            Window.Current.SizeChanged += Current_SizeChanged;
            Splash = Screen;

            if (Screen != null)
            {
                Screen.Dismissed += Screen_Dismissed;

                SplashImageRect = Screen.ImageLocation;

                SetControlPosition();
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
            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.High, () =>
            {
                var rootFrame = new Frame();
                Window.Current.Content = rootFrame;
                rootFrame.Navigate(typeof(MainPage));
            });
        }

        private async void Screen_Dismissed(SplashScreen sender, object args)
        {
            if (!(ApplicationData.Current.LocalSettings.Values["IsInitialQuickStart"] is bool) || !(ApplicationData.Current.LocalSettings.Values["QuickStartInitialFinished"] is bool))
            {
                var SQL = SQLite.GetInstance();
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
                    Display.Text = "请开启此应用的文件系统访问权限以正常工作\r然后重新启动该应用";
                    ButtonPane.Visibility = Visibility.Visible;
                });
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
                _ = await StorageFolder.GetFolderFromPathAsync("C:\\");
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
            await Launcher.LaunchUriAsync(new Uri("ms-settings:appsfeatures-app"));
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

            Application.Current.Exit();
        }
    }
}
