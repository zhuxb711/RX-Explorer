using Microsoft.Toolkit.Uwp.Notifications;
using System;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.Storage;
using Windows.System;
using Windows.UI.Notifications;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace FileManager
{
    public sealed partial class BlueScreen : Page
    {
        public BlueScreen()
        {
            InitializeComponent();
            Window.Current.SetTitleBar(TitleBar);
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            if (e.Parameter is string ExceptionMessage)
            {
                Message.Text = ExceptionMessage;
            }
        }

        private async Task SendEmailAsync(string messageBody)
        {
            messageBody="版本: " + string.Format("Version: {0}.{1}.{2}.{3}", Package.Current.Id.Version.Major, Package.Current.Id.Version.Minor, Package.Current.Id.Version.Build, Package.Current.Id.Version.Revision) +
                        messageBody +
                        "\r\r问题复现方法：(可选)\r1、\r\r2、\r\r3、\r";

            messageBody = Uri.EscapeDataString(messageBody);
            string url = "mailto:zhuxb711@yeah.net?subject=RX_BugReport&body=" + messageBody;
            await Launcher.LaunchUriAsync(new Uri(url));
        }

        private async void Report_Click(object sender, RoutedEventArgs e)
        {
            await SendEmailAsync(Message.Text);
        }

        private async void Reset_Click(object sender, RoutedEventArgs e)
        {
            SQLite.GetInstance().Dispose();
            await ApplicationData.Current.ClearAsync();
            ToastNotificationManager.CreateToastNotifier().Show(new ToastNotification(GenerateRestartToast().GetXml()));
            Application.Current.Exit();
        }

        public static ToastContent GenerateRestartToast()
        {
            if (MainPage.ThisPage.CurrentLanguage == LanguageEnum.Chinese)
            {
                return new ToastContent()
                {
                    Launch = "Restart",
                    Scenario = ToastScenario.Alarm,

                    Visual = new ToastVisual()
                    {
                        BindingGeneric = new ToastBindingGeneric()
                        {
                            Children =
                            {
                                new AdaptiveText()
                                {
                                    Text = "需要重新启动RX文件管理器"
                                },

                                new AdaptiveText()
                                {
                                    Text = "重置已完成"
                                },

                                new AdaptiveText()
                                {
                                    Text = "请点击以立即重新启动RX"
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
            }
            else
            {
                return new ToastContent()
                {
                    Launch = "Restart",
                    Scenario = ToastScenario.Alarm,

                    Visual = new ToastVisual()
                    {
                        BindingGeneric = new ToastBindingGeneric()
                        {
                            Children =
                            {
                                new AdaptiveText()
                                {
                                    Text = "Need to restart RX FileManager"
                                },

                                new AdaptiveText()
                                {
                                    Text = "Reset completed"
                                },

                                new AdaptiveText()
                                {
                                    Text = "Please click to restart RX now"
                                }
                            }
                        }
                    },

                    Actions = new ToastActionsCustom
                    {
                        Buttons =
                        {
                            new ToastButton("Restart","Restart")
                            {
                                ActivationType =ToastActivationType.Foreground
                            },
                            new ToastButtonDismiss("Later")
                        }
                    }
                };

            }
        }
    }
}
