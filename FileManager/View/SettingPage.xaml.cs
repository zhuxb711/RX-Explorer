using Microsoft.Toolkit.Uwp.Notifications;
using System;
using System.Linq;
using Windows.ApplicationModel;
using Windows.Storage;
using Windows.System;
using Windows.UI;
using Windows.UI.Notifications;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;


namespace FileManager
{
    public sealed partial class SettingPage : Page
    {
        public SettingPage()
        {
            InitializeComponent();
            Version.Text = string.Format("Version: {0}.{1}.{2}.{3}", Package.Current.Id.Version.Major, Package.Current.Id.Version.Minor, Package.Current.Id.Version.Build, Package.Current.Id.Version.Revision);
            AcrylicBackgroundController.SetTintOpacityAndLuminositySlider(TintOpacitySlider, TintLuminositySlider);

            for (int i = 1; i <= 10; i++)
            {
                SearchNum.Items.Add(MainPage.ThisPage.CurrentLanguage == LanguageEnum.Chinese
                    ? "前" + (i * 10) + "项结果"
                    : "Top" + (i * 10) + "Results");
            }

            if (MainPage.ThisPage.CurrentLanguage == LanguageEnum.Chinese)
            {
                UIMode.Items.Add("推荐");
                UIMode.Items.Add("自定义");
            }
            else
            {
                UIMode.Items.Add("Recommand");
                UIMode.Items.Add("Custom");
            }

            if (ApplicationData.Current.LocalSettings.Values["UIDisplayMode"] is string Mode)
            {
                UIMode.SelectedItem = UIMode.Items.Where((Item) => Item.ToString() == Mode).FirstOrDefault();
            }

            if (ApplicationData.Current.LocalSettings.Values["SetSearchResultMaxNum"] is string MaxNum)
            {
                SearchNum.SelectedIndex = SearchNum.Items.IndexOf(SearchNum.Items.Where((Item) => Item.ToString().Contains(MaxNum)).FirstOrDefault());
            }

            if (ApplicationData.Current.LocalSettings.Values["EnableMultiInstanceSupport"] is bool Enable)
            {
                MultiInstace.IsChecked = Enable;
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
            await Launcher.LaunchUriAsync(new Uri("ms-windows-store://review/?productid=9N88QBQKF2RS"));
        }

        private void SearchNum_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ApplicationData.Current.LocalSettings.Values["SetSearchResultMaxNum"] = ((SearchNum.SelectedIndex + 1) * 10).ToString();
        }

        private async void FlyoutContinue_Click(object sender, RoutedEventArgs e)
        {
            ConfirmFly.Hide();
            await SQLite.GetInstance().ClearSearchHistoryRecord();

            if (MainPage.ThisPage.CurrentLanguage == LanguageEnum.Chinese)
            {
                ContentDialog dialog = new ContentDialog
                {
                    Title = "提示",
                    Content = "搜索历史记录清理完成",
                    CloseButtonText = "确定",
                    Background = Application.Current.Resources["DialogAcrylicBrush"] as Brush
                };
                _ = await dialog.ShowAsync();
            }
            else
            {
                ContentDialog dialog = new ContentDialog
                {
                    Title = "Tips",
                    Content = "Search history cleanup completed",
                    CloseButtonText = "Confirm",
                    Background = Application.Current.Resources["DialogAcrylicBrush"] as Brush
                };
                _ = await dialog.ShowAsync();
            }
        }

        private void FlyoutCancel_Click(object sender, RoutedEventArgs e)
        {
            ConfirmFly.Hide();
        }

        private async void ClearUp_Click(object sender, RoutedEventArgs e)
        {
            if (MainPage.ThisPage.CurrentLanguage == LanguageEnum.Chinese)
            {
                ContentDialog dialog = new ContentDialog
                {
                    Title = "警告",
                    Content = " 此操作将完全初始化RX文件管理器，包括：\r\r     • 清除全部数据存储\r\r     • 还原所有应用设置\r\r     • RX文件管理器将自动关闭\r\r 您需要按提示重新启动",
                    CloseButtonText = "取消",
                    PrimaryButtonText = "确认",
                    Background = Application.Current.Resources["DialogAcrylicBrush"] as Brush
                };
                if (await dialog.ShowAsync() == ContentDialogResult.Primary)
                {
                    SQLite.GetInstance().Dispose();
                    await ApplicationData.Current.ClearAsync();
                    ToastNotificationManager.CreateToastNotifier().Show(new ToastNotification(GenerateRestartToast().GetXml()));
                    Application.Current.Exit();
                }
            }
            else
            {
                ContentDialog dialog = new ContentDialog
                {
                    Title = "Warning",
                    Content = " This will fully initialize the RX FileManager，Including：\r\r     • Clear all data\r\r     • Restore all app settings\r\r     • RX FileManager will automatically close\r\r You need to restart as prompted",
                    CloseButtonText = "Cancel",
                    PrimaryButtonText = "Confirm",
                    Background = Application.Current.Resources["DialogAcrylicBrush"] as Brush
                };
                if (await dialog.ShowAsync() == ContentDialogResult.Primary)
                {
                    SQLite.GetInstance().Dispose();
                    await ApplicationData.Current.ClearAsync();
                    ToastNotificationManager.CreateToastNotifier().Show(new ToastNotification(GenerateRestartToast().GetXml()));
                    Application.Current.Exit();
                }
            }
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

        private void MultiInstace_Checked(object sender, RoutedEventArgs e)
        {
            ApplicationData.Current.LocalSettings.Values["EnableMultiInstanceSupport"] = true;
        }

        private void MultiInstace_Unchecked(object sender, RoutedEventArgs e)
        {
            ApplicationData.Current.LocalSettings.Values["EnableMultiInstanceSupport"] = false;
        }

        private void UIMode_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ApplicationData.Current.LocalSettings.Values["UIDisplayMode"] = UIMode.SelectedItem.ToString();
            if (UIMode.SelectedIndex == 0)
            {
                CustomUIArea.Visibility = Visibility.Collapsed;

                AcrylicBackgroundController.DirectAccessToAcrylicBrush().TintOpacity = 0.6;
                AcrylicBackgroundController.TintLuminosityOpacity = null;
                AcrylicBackgroundController.AcrylicColor = Colors.LightSlateGray;
            }
            else
            {
                CustomUIArea.Visibility = Visibility.Visible;

                if (ApplicationData.Current.LocalSettings.Values["BackgroundTintLuminosity"] is string Luminosity)
                {
                    AcrylicBackgroundController.TintLuminosityOpacity = Convert.ToDouble(Luminosity);
                }
                else
                {
                    ApplicationData.Current.LocalSettings.Values["BackgroundTintLuminosity"] = "0";
                    AcrylicBackgroundController.TintLuminosityOpacity = 0.4;
                }

                if (ApplicationData.Current.LocalSettings.Values["BackgroundTintOpacity"] is string Opacity)
                {
                    AcrylicBackgroundController.TintOpacity = Convert.ToDouble(Opacity);
                }
                else
                {
                    ApplicationData.Current.LocalSettings.Values["BackgroundTintOpacity"] = "0.4";
                    AcrylicBackgroundController.TintOpacity = 0.6;
                }

                if (ApplicationData.Current.LocalSettings.Values["AcrylicThemeColor"] is string AcrylicColor)
                {
                    AcrylicColorPicker.Color = AcrylicBackgroundController.GetColorFromHexString(AcrylicColor);
                    AcrylicBackgroundController.AcrylicColor = AcrylicColorPicker.Color;
                }
            }
        }

        private void TintOpacitySlider_ValueChanged(object sender, Windows.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            ApplicationData.Current.LocalSettings.Values["BackgroundTintOpacity"] = e.NewValue.ToString();
            AcrylicBackgroundController.TintOpacity = e.NewValue;
        }

        private void TintLuminositySlider_ValueChanged(object sender, Windows.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            ApplicationData.Current.LocalSettings.Values["BackgroundTintLuminosity"] = e.NewValue.ToString();
            AcrylicBackgroundController.TintLuminosityOpacity = e.NewValue;
        }

        private void AcrylicColor_Click(object sender, RoutedEventArgs e)
        {
            ColorPickerTeachTip.IsOpen = true;
        }

        private void ColorPickerTeachTip_Closed(Microsoft.UI.Xaml.Controls.TeachingTip sender, Microsoft.UI.Xaml.Controls.TeachingTipClosedEventArgs args)
        {
            ApplicationData.Current.LocalSettings.Values["AcrylicThemeColor"] = AcrylicBackgroundController.AcrylicColor.ToString();
        }

        private void TintOpacityQuestion_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            OpacityTip.IsOpen = true;
        }

        private void TintLuminosityQuestion_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            LuminosityTip.IsOpen = true;
        }
    }
}
