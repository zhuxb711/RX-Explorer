using Microsoft.Toolkit.Uwp.Notifications;
using System;
using System.Linq;
using Windows.ApplicationModel;
using Windows.Services.Store;
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
                QueueContentDialog dialog = new QueueContentDialog
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
                QueueContentDialog dialog = new QueueContentDialog
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
                QueueContentDialog dialog = new QueueContentDialog
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
                QueueContentDialog dialog = new QueueContentDialog
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

                AcrylicBackgroundController.TintOpacity = 0.6;
                AcrylicBackgroundController.TintLuminosityOpacity = -1;
                AcrylicBackgroundController.AcrylicColor = Colors.LightSlateGray;
            }
            else
            {
                CustomUIArea.Visibility = Visibility.Visible;

                if (ApplicationData.Current.LocalSettings.Values["BackgroundTintLuminosity"] is string Luminosity)
                {
                    float Value = Convert.ToSingle(Luminosity);
                    TintLuminositySlider.Value = Value;
                    AcrylicBackgroundController.TintLuminosityOpacity = Value;
                }
                else
                {
                    TintLuminositySlider.Value = 0.8;
                    AcrylicBackgroundController.TintLuminosityOpacity = 0.8;
                }

                if (ApplicationData.Current.LocalSettings.Values["BackgroundTintOpacity"] is string Opacity)
                {
                    float Value = Convert.ToSingle(Opacity);
                    TintOpacitySlider.Value = Value;
                    AcrylicBackgroundController.TintOpacity = Value;
                }
                else
                {
                    TintOpacitySlider.Value = 0.6;
                    AcrylicBackgroundController.TintOpacity = 0.6;
                }

                if (ApplicationData.Current.LocalSettings.Values["AcrylicThemeColor"] is string AcrylicColor)
                {
                    AcrylicBackgroundController.AcrylicColor = AcrylicBackgroundController.GetColorFromHexString(AcrylicColor);
                }
            }
        }

        private void AcrylicColor_Click(object sender, RoutedEventArgs e)
        {
            ColorPickerTeachTip.IsOpen = true;
        }

        private void TintOpacityQuestion_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            OpacityTip.IsOpen = true;
        }

        private void TintLuminosityQuestion_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            LuminosityTip.IsOpen = true;
        }

        private void ColorPickerTeachTip_Closed(Microsoft.UI.Xaml.Controls.TeachingTip sender, Microsoft.UI.Xaml.Controls.TeachingTipClosedEventArgs args)
        {
            ApplicationData.Current.LocalSettings.Values["AcrylicThemeColor"] = AcrylicColorPicker.Color.ToString();
        }

        private async void Donation_Click(object sender, RoutedEventArgs e)
        {
            if (MainPage.ThisPage.CurrentLanguage == LanguageEnum.Chinese)
            {
                QueueContentDialog dialog = new QueueContentDialog
                {
                    Title = "支持",
                    Content = "开发者开发RX文件管理器花费了大量精力\r" +
                              "🎉您可以自愿为开发者贡献一点小零花钱🎉\r\r" +
                              "给开发者支持7个🍪吧\r\r" +
                              "若您不愿意，则可以点击\"跪安\"以取消\r" +
                              "若您愿意支持开发者，则可以点击\"准奏\"\r\r" +
                              "Tips: 无论支持与否，RX文件管理器都将继续运行，且无任何功能限制",
                    PrimaryButtonText = "准奏",
                    CloseButtonText = "跪安",
                    Background = Application.Current.Resources["DialogAcrylicBrush"] as Brush
                };
                if (await dialog.ShowAsync() == ContentDialogResult.Primary)
                {
                    StoreContext Store = StoreContext.GetDefault();
                    StoreProductQueryResult StoreProductResult = await Store.GetAssociatedStoreProductsAsync(new string[] { "Durable" });
                    if (StoreProductResult.ExtendedError == null)
                    {
                        StoreProduct Product = StoreProductResult.Products.Values.FirstOrDefault();
                        if (Product != null)
                        {
                            switch ((await Store.RequestPurchaseAsync(Product.StoreId)).Status)
                            {
                                case StorePurchaseStatus.Succeeded:
                                    {
                                        QueueContentDialog QueueContenDialog = new QueueContentDialog
                                        {
                                            Title = "感谢",
                                            Content = "感谢您的支持，我们将努力将RX做得越来越好q(≧▽≦q)\r\r" +
                                                       "RX文件管理器的诞生，是为了填补UWP文件管理器缺位的空白\r" +
                                                       "它并非是一个盈利项目，因此下载和使用都是免费的，并且不含有广告\r" +
                                                       "RX的目标是打造一个免费且功能全面文件管理器\r" +
                                                       "RX文件管理器是我利用业余时间开发的项目\r" +
                                                       "希望大家能够喜欢\r\r" +
                                                       "Ruofan,\r敬上",
                                            CloseButtonText = "朕知道了",
                                            Background = Application.Current.Resources["DialogAcrylicBrush"] as Brush
                                        };
                                        _ = await QueueContenDialog.ShowAsync();
                                        break;
                                    }
                                case StorePurchaseStatus.AlreadyPurchased:
                                    {
                                        QueueContentDialog QueueContenDialog = new QueueContentDialog
                                        {
                                            Title = "再次感谢",
                                            Content = "您已为RX支持过一次了，您的心意开发者已心领\r\r" +
                                                      "RX的初衷并非是赚钱，因此不可重复支持哦\r\r" +
                                                      "您可以向周围的人宣传一下RX，也是对RX的最好的支持哦（*＾-＾*）\r\r" +
                                                      "Ruofan,\r敬上",
                                            CloseButtonText = "朕知道了",
                                            Background = Application.Current.Resources["DialogAcrylicBrush"] as Brush
                                        };
                                        _ = await QueueContenDialog.ShowAsync();
                                        break;
                                    }
                                case StorePurchaseStatus.NotPurchased:
                                    {
                                        QueueContentDialog QueueContenDialog = new QueueContentDialog
                                        {
                                            Title = "感谢",
                                            Content = "无论支持与否，RX始终如一\r\r" +
                                                      "即使您最终决定放弃支持本项目，依然十分感谢您能够点进来看一看\r\r" +
                                                      "Ruofan,\r敬上",
                                            CloseButtonText = "朕知道了",
                                            Background = Application.Current.Resources["DialogAcrylicBrush"] as Brush
                                        };
                                        _ = await QueueContenDialog.ShowAsync();
                                        break;
                                    }
                                default:
                                    {
                                        QueueContentDialog QueueContenDialog = new QueueContentDialog
                                        {
                                            Title = "抱歉",
                                            Content = "由于Microsoft Store或网络原因，无法打开支持页面，请稍后再试",
                                            CloseButtonText = "朕知道了",
                                            Background = Application.Current.Resources["DialogAcrylicBrush"] as Brush
                                        };
                                        _ = await QueueContenDialog.ShowAsync();
                                        break;
                                    }
                            }
                        }
                    }
                    else
                    {
                        QueueContentDialog QueueContenDialog = new QueueContentDialog
                        {
                            Title = "抱歉",
                            Content = "由于Microsoft Store或网络原因，无法打开支持页面，请稍后再试",
                            CloseButtonText = "朕知道了",
                            Background = Application.Current.Resources["DialogAcrylicBrush"] as Brush
                        };
                        _ = await QueueContenDialog.ShowAsync();
                    }
                }
            }
            else
            {
                QueueContentDialog dialog = new QueueContentDialog
                {
                    Title = "Donation",
                    Content = "It takes a lot of effort for developers to develop RX file manager\r" +
                              "🎉You can volunteer to contribute a little pocket money to developers.🎉\r\r" +
                              "Please donate 0.99$ 🍪\r\r" +
                              "If you don't want to, you can click \"Later\" to cancel\r" +
                              "if you want to donate, you can click \"Donate\" to support developer\r\r" +
                              "Tips: Whether donated or not, the RX File Manager will continue to run without any functional limitations",
                    PrimaryButtonText = "Donate",
                    CloseButtonText = "Later",
                    Background = Application.Current.Resources["DialogAcrylicBrush"] as Brush
                };
                if (await dialog.ShowAsync() == ContentDialogResult.Primary)
                {
                    StoreContext Store = StoreContext.GetDefault();
                    StoreProductQueryResult StoreProductResult = await Store.GetAssociatedStoreProductsAsync(new string[] { "Durable" });
                    if (StoreProductResult.ExtendedError == null)
                    {
                        StoreProduct Product = StoreProductResult.Products.Values.FirstOrDefault();
                        if (Product != null)
                        {
                            switch ((await Store.RequestPurchaseAsync(Product.StoreId)).Status)
                            {
                                case StorePurchaseStatus.Succeeded:
                                    {
                                        QueueContentDialog QueueContenDialog = new QueueContentDialog
                                        {
                                            Title = "Appreciation",
                                            Content = "Thank you for your support, we will work hard to make RX better and better q(≧▽≦q)\r\r" +
                                                      "The RX file manager was born to fill the gaps in the UWP file manager\r" +
                                                      "This is not a profitable project, so downloading and using are free and do not include ads\r" +
                                                      "RX's goal is to create a free and full-featured file manager\r" +
                                                      "RX File Manager is a project I developed in my spare time\r" +
                                                      "I hope everyone likes\r\r" +
                                                      "Sincerely,\rRuofan",
                                            CloseButtonText = "朕知道了",
                                            Background = Application.Current.Resources["DialogAcrylicBrush"] as Brush
                                        };
                                        _ = await QueueContenDialog.ShowAsync();
                                        break;
                                    }
                                case StorePurchaseStatus.AlreadyPurchased:
                                    {
                                        QueueContentDialog QueueContenDialog = new QueueContentDialog
                                        {
                                            Title = "Thanks again",
                                            Content = "You have already supported RX once, thank you very much\r\r" +
                                                      "The original intention of RX is not to make money, so you can't repeat purchase it.\r\r" +
                                                      "You can advertise the RX to the people around you, and it is also the best support for RX（*＾-＾*）\r\r" +
                                                      "Sincerely,\rRuofan",
                                            CloseButtonText = "Got it",
                                            Background = Application.Current.Resources["DialogAcrylicBrush"] as Brush
                                        };
                                        _ = await QueueContenDialog.ShowAsync();
                                        break;
                                    }
                                case StorePurchaseStatus.NotPurchased:
                                    {
                                        QueueContentDialog QueueContenDialog = new QueueContentDialog
                                        {
                                            Title = "Appreciation",
                                            Content = "Whether supported or not, RX is always the same\r\r" +
                                                      "Even if you finally decide to give up supporting the project, thank you very much for being able to click to see it\r\r" +
                                                      "Sincerely,\rRuofan",
                                            CloseButtonText = "Got it",
                                            Background = Application.Current.Resources["DialogAcrylicBrush"] as Brush
                                        };
                                        _ = await QueueContenDialog.ShowAsync();
                                        break;
                                    }
                                default:
                                    {
                                        QueueContentDialog QueueContenDialog = new QueueContentDialog
                                        {
                                            Title = "Sorry",
                                            Content = "Unable to open support page due to Microsoft Store or network, please try again later",
                                            CloseButtonText = "Got it",
                                            Background = Application.Current.Resources["DialogAcrylicBrush"] as Brush
                                        };
                                        _ = await QueueContenDialog.ShowAsync();
                                        break;
                                    }
                            }
                        }
                    }
                    else
                    {
                        QueueContentDialog QueueContenDialog = new QueueContentDialog
                        {
                            Title = "Sorry",
                            Content = "Unable to open support page due to Microsoft Store or network, please try again later",
                            CloseButtonText = "Got it",
                            Background = Application.Current.Resources["DialogAcrylicBrush"] as Brush
                        };
                        _ = await QueueContenDialog.ShowAsync();
                    }
                }
            }
        }

        private async void UpdateLogLink_Click(object sender, RoutedEventArgs e)
        {
            WhatIsNew Dialog = new WhatIsNew();
            await Dialog.ShowAsync();
        }

        private async void SystemInfoButton_Click(object sender, RoutedEventArgs e)
        {
            if (Package.Current.Id.Architecture == ProcessorArchitecture.X64 || Package.Current.Id.Architecture == ProcessorArchitecture.X86)
            {
                SystemInfoDialog dialog = new SystemInfoDialog();
                await dialog.ShowAsync();
            }
            else
            {
                if (MainPage.ThisPage.CurrentLanguage == LanguageEnum.Chinese)
                {
                    QueueContentDialog dialog = new QueueContentDialog
                    {
                        Title = "抱歉",
                        Content = "系统信息窗口所依赖的部分组件仅支持在X86或X64处理器上实现\rARM处理器暂不支持，因此无法打开此窗口",
                        CloseButtonText = "知道了"
                    };
                    await dialog.ShowAsync();
                }
                else
                {
                    QueueContentDialog dialog = new QueueContentDialog
                    {
                        Title = "Sorry",
                        Content = "Some components that the system information dialog depends on only support X86 or X64 processors\rUnsupport ARM processor for now, so this dialog will not be opened",
                        CloseButtonText = "Got it"
                    };
                    await dialog.ShowAsync();
                }
            }

        }
    }
}
