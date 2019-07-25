using Microsoft.Toolkit.Uwp.Notifications;
using System;
using Windows.ApplicationModel;
using Windows.Services.Store;
using Windows.Storage;
using Windows.UI;
using Windows.UI.Notifications;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;


namespace USBManager
{
    public sealed partial class SettingPage : Page
    {
        public SettingPage()
        {
            InitializeComponent();
            Loaded += SettingPage_Loaded;
            Version.Text = string.Format("Version: {0}.{1}.{2}.{3}", Package.Current.Id.Version.Major, Package.Current.Id.Version.Minor, Package.Current.Id.Version.Build, Package.Current.Id.Version.Revision);
        }

        private void SettingPage_Loaded(object sender, RoutedEventArgs e)
        {
            if (ApplicationData.Current.LocalSettings.Values["EnableTrace"] is bool EnableTrace)
            {
                TraceSwitch.IsOn = EnableTrace;
            }


            if (ApplicationData.Current.LocalSettings.Values["EnableDirectDelete"] is bool EnableDirectDelete)
            {
                DeleteSwitch.IsOn = !EnableDirectDelete;
            }
        }

        private void Like_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            LikeSymbol.Foreground = new SolidColorBrush(Colors.Yellow);
            LikeText.Foreground = new SolidColorBrush(Colors.Yellow);
        }

        private void Like_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            LikeSymbol.Foreground = new SolidColorBrush(Colors.White);
            LikeText.Foreground = new SolidColorBrush(Colors.White);
        }

        private void Link_Click(object sender, RoutedEventArgs e)
        {
            MainPage.ThisPage.Nav.Navigate(typeof(AboutMe), null, new SlideNavigationTransitionInfo() { Effect = SlideNavigationTransitionEffect.FromRight });
        }

        private async void Like_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            var Result = await StoreContext.GetDefault().RequestRateAndReviewAppAsync();
            switch (Result.Status)
            {
                case StoreRateAndReviewStatus.Succeeded:
                    ShowRateSucceedNotification();
                    break;
                case StoreRateAndReviewStatus.CanceledByUser:
                    break;
                default:
                    ShowRateErrorNotification();
                    break;
            }
        }

        private void ShowRateSucceedNotification()
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
                                Text = "评价成功"
                            },

                            new AdaptiveText()
                            {
                                Text = "感谢您对此App的评价，帮助我们做得更好。"
                            }
                        }
                    }
                },
            };
            ToastNotificationManager.CreateToastNotifier().Show(new ToastNotification(Content.GetXml()));
        }

        private void ShowRateErrorNotification()
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
                                Text = "评价失败"
                            },

                            new AdaptiveText()
                            {
                                Text = "因网络或其他原因而无法进行评价"
                            }
                        }
                    }
                },
            };
            ToastNotificationManager.CreateToastNotifier().Show(new ToastNotification(Content.GetXml()));
        }

        private void TraceSwitch_Toggled(object sender, RoutedEventArgs e)
        {
            if (TraceSwitch.IsOn)
            {
                ApplicationData.Current.LocalSettings.Values["EnableTrace"] = true;
            }
            else
            {
                ApplicationData.Current.LocalSettings.Values["EnableTrace"] = false;
            }
        }

        private void DeleteSwitch_Toggled(object sender, RoutedEventArgs e)
        {
            if (DeleteSwitch.IsOn)
            {
                ApplicationData.Current.LocalSettings.Values["EnableDirectDelete"] = false;
            }
            else
            {
                ApplicationData.Current.LocalSettings.Values["EnableDirectDelete"] = true;
            }
        }
    }
}
