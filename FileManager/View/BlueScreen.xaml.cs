using Microsoft.Toolkit.Uwp.Notifications;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;
using Windows.System;
using Windows.UI.Notifications;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// https://go.microsoft.com/fwlink/?LinkId=234238 上介绍了“空白页”项模板

namespace FileManager
{
    /// <summary>
    /// 可用于自身或导航至 Frame 内部的空白页。
    /// </summary>
    public sealed partial class BlueScreen : Page
    {
        public BlueScreen()
        {
            InitializeComponent();
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
    }
