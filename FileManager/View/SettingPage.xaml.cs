using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Core;
using Windows.Security.Cryptography;
using Windows.Security.Cryptography.Core;
using Windows.Services.Store;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;
using Windows.System;
using Windows.System.Profile;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;

namespace FileManager
{
    public sealed partial class SettingPage : Page
    {
        private ObservableCollection<FeedBackItem> FeedBackCollection;
        private string UserFullName = string.Empty;
        public string UserID = string.Empty;
        public static SettingPage ThisPage { get; private set; }
        public static bool IsDoubleClickEnable { get; set; } = true;

        private ObservableCollection<BackgroundPicture> PictureList = new ObservableCollection<BackgroundPicture>();

        public SettingPage()
        {
            InitializeComponent();
            ThisPage = this;
            Version.Text = string.Format("Version: {0}.{1}.{2}.{3}", Package.Current.Id.Version.Major, Package.Current.Id.Version.Minor, Package.Current.Id.Version.Build, Package.Current.Id.Version.Revision);
            PictureGirdView.ItemsSource = PictureList;

            Loading += SettingPage_Loading;
            Loaded += SettingPage_Loaded1;
            Loaded += SettingPage_Loaded;
        }

        private void SettingPage_Loaded1(object sender, RoutedEventArgs e)
        {
            if (PictureMode.IsChecked.GetValueOrDefault() == true && PictureGirdView.SelectedItem != null)
            {
                PictureGirdView.ScrollIntoViewSmoothly(PictureGirdView.SelectedItem);
            }
        }

        private async void SettingPage_Loading(FrameworkElement sender, object args)
        {
            Loading -= SettingPage_Loading;

            foreach (Uri ImageUri in await SQLite.Current.GetBackgroundPictureAsync())
            {
                BitmapImage Image = new BitmapImage
                {
                    DecodePixelHeight = 90,
                    DecodePixelWidth = 160
                };
                PictureList.Add(new BackgroundPicture(Image, ImageUri));
                Image.UriSource = ImageUri;
            }

            if (Globalization.Language == LanguageEnum.Chinese)
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

            if (ApplicationData.Current.LocalSettings.Values["IsLeftAreaOpen"] is bool Enable)
            {
                OpenLeftArea.IsOn = Enable;
            }

            if (ApplicationData.Current.LocalSettings.Values["IsDoubleClickEnable"] is bool IsDoubleClick)
            {
                FolderOpenMethod.IsOn = IsDoubleClick;
            }
        }

        private async void SettingPage_Loaded(object sender, RoutedEventArgs e)
        {
            Loaded -= SettingPage_Loaded;

            if ((await User.FindAllAsync()).Where(p => p.AuthenticationStatus == UserAuthenticationStatus.LocallyAuthenticated && p.Type == UserType.LocalUser).FirstOrDefault() is User CurrentUser)
            {
                string FirstName = (await CurrentUser.GetPropertyAsync(KnownUserProperties.FirstName))?.ToString();
                string LastName = (await CurrentUser.GetPropertyAsync(KnownUserProperties.LastName))?.ToString();
                UserID = (await CurrentUser.GetPropertyAsync(KnownUserProperties.AccountName))?.ToString();
                if (string.IsNullOrEmpty(UserID))
                {
                    var Token = HardwareIdentification.GetPackageSpecificToken(null);
                    HashAlgorithmProvider md5 = HashAlgorithmProvider.OpenAlgorithm(HashAlgorithmNames.Md5);
                    IBuffer hashedData = md5.HashData(Token.Id);
                    UserID = CryptographicBuffer.EncodeToHexString(hashedData).ToUpper();
                }

                if (!(string.IsNullOrEmpty(FirstName) || string.IsNullOrEmpty(LastName)))
                {
                    if (Globalization.Language == LanguageEnum.Chinese)
                    {
                        UserFullName = $"{LastName} {FirstName}";
                    }
                    else
                    {
                        UserFullName = $"{FirstName} {LastName}";
                    }
                }
                else if (!string.IsNullOrEmpty(FirstName))
                {
                    UserFullName = FirstName;
                }
                else if (!string.IsNullOrEmpty(LastName))
                {
                    UserFullName = LastName;
                }
                else
                {
                    var Token = HardwareIdentification.GetPackageSpecificToken(null);
                    HashAlgorithmProvider md5 = HashAlgorithmProvider.OpenAlgorithm(HashAlgorithmNames.Md5);
                    IBuffer hashedData = md5.HashData(Token.Id);
                    UserFullName = CryptographicBuffer.EncodeToHexString(hashedData).ToUpper();
                }
            }
            else
            {
                var Token = HardwareIdentification.GetPackageSpecificToken(null);
                HashAlgorithmProvider md5 = HashAlgorithmProvider.OpenAlgorithm(HashAlgorithmNames.Md5);
                IBuffer hashedData = md5.HashData(Token.Id);
                UserFullName = CryptographicBuffer.EncodeToHexString(hashedData).ToUpper();
            }

            if (PictureMode.IsChecked.GetValueOrDefault() == true)
            {
                PictureGirdView.ScrollIntoViewSmoothly(PictureGirdView.SelectedItem);
            }

            FeedBackCollection = new ObservableCollection<FeedBackItem>();
            FeedBackCollection.CollectionChanged += (s, t) =>
            {
                if (FeedBackCollection.Count == 0)
                {
                    EmptyFeedBack.Visibility = Visibility.Visible;
                    FeedBackList.Visibility = Visibility.Collapsed;
                }
                else
                {
                    EmptyFeedBack.Visibility = Visibility.Collapsed;
                    FeedBackList.Visibility = Visibility.Visible;
                }
            };
            FeedBackList.ItemsSource = FeedBackCollection;

            foreach (var FeedBackItem in await MySQL.Current.GetAllFeedBackAsync())
            {
                FeedBackCollection.Add(FeedBackItem);

                await MySQL.Current.GetExtraFeedBackInfo(FeedBackItem);

                switch (FeedBackItem.UserVoteAction)
                {
                    case "+":
                        {
                            while (true)
                            {
                                if (FeedBackList.ContainerFromItem(FeedBackItem) is ListViewItem ListItem)
                                {
                                    ToggleButton Button = ListItem.FindChildOfName<ToggleButton>("FeedBackLike");
                                    if (!Button.IsChecked.GetValueOrDefault())
                                    {
                                        Button.Checked -= FeedBackLike_Checked;
                                        Button.IsChecked = true;
                                        Button.Checked += FeedBackLike_Checked;
                                    }
                                    break;
                                }
                                else
                                {
                                    await Task.Delay(200);
                                }
                            }
                            break;
                        }
                    case "-":
                        {
                            while (true)
                            {
                                if (FeedBackList.ContainerFromItem(FeedBackItem) is ListViewItem ListItem)
                                {
                                    ToggleButton Button = ListItem.FindChildOfName<ToggleButton>("FeedDislike");
                                    if (!Button.IsChecked.GetValueOrDefault())
                                    {
                                        Button.Checked -= FeedDislike_Checked;
                                        Button.IsChecked = true;
                                        Button.Checked += FeedDislike_Checked;
                                    }
                                    break;
                                }
                                else
                                {
                                    await Task.Delay(200);
                                }
                            }
                            break;
                        }
                }
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
            _ = await StoreContext.GetDefault().RequestRateAndReviewAppAsync();
        }

        private async void FlyoutContinue_Click(object sender, RoutedEventArgs e)
        {
            ConfirmFly.Hide();
            await SQLite.Current.ClearSearchHistoryRecord();

            if (Globalization.Language == LanguageEnum.Chinese)
            {
                QueueContentDialog dialog = new QueueContentDialog
                {
                    Title = "提示",
                    Content = "搜索历史记录清理完成",
                    CloseButtonText = "确定"
                };
                _ = await dialog.ShowAsync();
            }
            else
            {
                QueueContentDialog dialog = new QueueContentDialog
                {
                    Title = "Tips",
                    Content = "Search history cleanup completed",
                    CloseButtonText = "Confirm"
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
            if (Globalization.Language == LanguageEnum.Chinese)
            {
                QueueContentDialog dialog = new QueueContentDialog
                {
                    Title = "警告",
                    Content = " 此操作将完全初始化RX文件管理器，包括：\r\r     • 清除全部数据存储\r\r     • 还原所有应用设置\r\r     • RX文件管理器将自动关闭并重新启动",
                    CloseButtonText = "取消",
                    PrimaryButtonText = "确认"
                };
                if (await dialog.ShowAsync() == ContentDialogResult.Primary)
                {
                    SQLite.Current.Dispose();
                    MySQL.Current.Dispose();
                    try
                    {
                        await ApplicationData.Current.ClearAsync();
                    }
                    catch (Exception)
                    {
                        ApplicationData.Current.LocalSettings.Values.Clear();
                        await ApplicationData.Current.LocalFolder.DeleteAllSubFilesAndFolders();
                        await ApplicationData.Current.TemporaryFolder.DeleteAllSubFilesAndFolders();
                        await ApplicationData.Current.LocalCacheFolder.DeleteAllSubFilesAndFolders();
                    }
                    _ = await CoreApplication.RequestRestartAsync(string.Empty);
                }
            }
            else
            {
                QueueContentDialog dialog = new QueueContentDialog
                {
                    Title = "Warning",
                    Content = " This will fully initialize the RX FileManager，Including：\r\r     • Clear all data\r\r     • Restore all app settings\r\r     • RX FileManager will automatically restart",
                    CloseButtonText = "Cancel",
                    PrimaryButtonText = "Confirm"
                };
                if (await dialog.ShowAsync() == ContentDialogResult.Primary)
                {
                    SQLite.Current.Dispose();
                    MySQL.Current.Dispose();
                    try
                    {
                        await ApplicationData.Current.ClearAsync();
                    }
                    catch(Exception)
                    {
                        ApplicationData.Current.LocalSettings.Values.Clear();
                        await ApplicationData.Current.LocalFolder.DeleteAllSubFilesAndFolders();
                        await ApplicationData.Current.TemporaryFolder.DeleteAllSubFilesAndFolders();
                        await ApplicationData.Current.LocalCacheFolder.DeleteAllSubFilesAndFolders();
                    }
                    _ = await CoreApplication.RequestRestartAsync(string.Empty);
                }
            }
        }

        private void UIMode_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ApplicationData.Current.LocalSettings.Values["UIDisplayMode"] = UIMode.SelectedItem.ToString();

            if (UIMode.SelectedIndex == 0)
            {
                CustomUIArea.Visibility = Visibility.Collapsed;

                AcrylicMode.IsChecked = null;
                PictureMode.IsChecked = null;
                BackgroundController.Current.SwitchTo(BackgroundBrushType.Acrylic);
                BackgroundController.Current.TintOpacity = 0.6;
                BackgroundController.Current.TintLuminosityOpacity = -1;
                BackgroundController.Current.AcrylicColor = Colors.LightSlateGray;
            }
            else
            {
                CustomUIArea.Visibility = Visibility.Visible;

                if (ApplicationData.Current.LocalSettings.Values["CustomUISubMode"] is string Mode)
                {
                    if ((BackgroundBrushType)Enum.Parse(typeof(BackgroundBrushType), Mode) == BackgroundBrushType.Acrylic)
                    {
                        AcrylicMode.IsChecked = true;
                    }
                    else
                    {
                        PictureMode.IsChecked = true;
                    }
                }
                else
                {
                    ApplicationData.Current.LocalSettings.Values["CustomUISubMode"] = Enum.GetName(typeof(BackgroundBrushType), BackgroundBrushType.Acrylic);
                    AcrylicMode.IsChecked = true;

                    if (ApplicationData.Current.LocalSettings.Values["BackgroundTintLuminosity"] is string Luminosity)
                    {
                        float Value = Convert.ToSingle(Luminosity);
                        TintLuminositySlider.Value = Value;
                        BackgroundController.Current.TintLuminosityOpacity = Value;
                    }
                    else
                    {
                        TintLuminositySlider.Value = 0.8;
                        BackgroundController.Current.TintLuminosityOpacity = 0.8;
                    }

                    if (ApplicationData.Current.LocalSettings.Values["BackgroundTintOpacity"] is string Opacity)
                    {
                        float Value = Convert.ToSingle(Opacity);
                        TintOpacitySlider.Value = Value;
                        BackgroundController.Current.TintOpacity = Value;
                    }
                    else
                    {
                        TintOpacitySlider.Value = 0.6;
                        BackgroundController.Current.TintOpacity = 0.6;
                    }

                    if (ApplicationData.Current.LocalSettings.Values["AcrylicThemeColor"] is string AcrylicColor)
                    {
                        BackgroundController.Current.AcrylicColor = BackgroundController.Current.GetColorFromHexString(AcrylicColor);
                    }
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
            if (Globalization.Language == LanguageEnum.Chinese)
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
                    CloseButtonText = "跪安"
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
                                            CloseButtonText = "朕知道了"
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
                                            CloseButtonText = "朕知道了"
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
                                            CloseButtonText = "朕知道了"
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
                                            CloseButtonText = "朕知道了"
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
                            CloseButtonText = "朕知道了"
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
                    CloseButtonText = "Later"
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
                                            CloseButtonText = "朕知道了"
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
                                            CloseButtonText = "Got it"
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
                                            CloseButtonText = "Got it"
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
                                            CloseButtonText = "Got it"
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
                            CloseButtonText = "Got it"
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
                _ = await dialog.ShowAsync();
            }
            else
            {
                if (Globalization.Language == LanguageEnum.Chinese)
                {
                    QueueContentDialog dialog = new QueueContentDialog
                    {
                        Title = "抱歉",
                        Content = "系统信息窗口所依赖的部分组件仅支持在X86或X64处理器上实现\rARM处理器暂不支持，因此无法打开此窗口",
                        CloseButtonText = "知道了"
                    };
                    _ = await dialog.ShowAsync();
                }
                else
                {
                    QueueContentDialog dialog = new QueueContentDialog
                    {
                        Title = "Sorry",
                        Content = "Some components that the system information dialog depends on only support X86 or X64 processors\rUnsupport ARM processor for now, so this dialog will not be opened",
                        CloseButtonText = "Got it"
                    };
                    _ = await dialog.ShowAsync();
                }
            }

        }

        private async void AddFeedBack_Click(object sender, RoutedEventArgs e)
        {
            FeedBackDialog Dialog = new FeedBackDialog();
            if ((await Dialog.ShowAsync()) == ContentDialogResult.Primary)
            {
                if (FeedBackCollection.Count != 0)
                {
                    if (FeedBackCollection.FirstOrDefault((It) => It.UserName == UserFullName && It.Suggestion == Dialog.FeedBack && It.Title == Dialog.TitleName) == null)
                    {
                        FeedBackItem Item = new FeedBackItem(UserFullName, Dialog.TitleName, Dialog.FeedBack, "0", "0", UserID, Guid.NewGuid().ToString("D"));
                        if (!await MySQL.Current.SetFeedBackAsync(Item))
                        {
                            if (Globalization.Language == LanguageEnum.Chinese)
                            {
                                QueueContentDialog dialog = new QueueContentDialog
                                {
                                    Title = "错误",
                                    Content = "因网络原因无法进行此项操作",
                                    CloseButtonText = "确定"
                                };
                                _ = await dialog.ShowAsync();
                            }
                            else
                            {
                                QueueContentDialog dialog = new QueueContentDialog
                                {
                                    Title = "Error",
                                    Content = "This operation cannot be performed due to network reasons",
                                    CloseButtonText = "Got it"
                                };
                                _ = await dialog.ShowAsync();
                            }
                        }
                        else
                        {
                            FeedBackCollection.Add(Item);
                        }
                    }
                    else
                    {
                        QueueContentDialog TipsDialog = new QueueContentDialog
                        {
                            Title = "Tips",
                            Content = "The same feedback already exists, please do not submit it repeatedly",
                            CloseButtonText = "Got it"
                        };
                        _ = await TipsDialog.ShowAsync();
                    }
                }
                else
                {
                    FeedBackItem Item = new FeedBackItem(UserFullName, Dialog.TitleName, Dialog.FeedBack, "0", "0", UserID, Guid.NewGuid().ToString("D"));
                    if (!await MySQL.Current.SetFeedBackAsync(Item))
                    {
                        if (Globalization.Language == LanguageEnum.Chinese)
                        {
                            QueueContentDialog dialog = new QueueContentDialog
                            {
                                Title = "错误",
                                Content = "因网络原因无法进行此项操作",
                                CloseButtonText = "确定"
                            };
                            _ = await dialog.ShowAsync();
                        }
                        else
                        {
                            QueueContentDialog dialog = new QueueContentDialog
                            {
                                Title = "Error",
                                Content = "This operation cannot be performed due to network reasons",
                                CloseButtonText = "Got it"
                            };
                            _ = await dialog.ShowAsync();
                        }
                    }
                    else
                    {
                        FeedBackCollection.Add(Item);
                    }
                }
            }
        }

        private async void FeedDislike_Checked(object sender, RoutedEventArgs e)
        {
            if ((e.OriginalSource as FrameworkElement)?.DataContext is FeedBackItem Item)
            {
                if (FeedBackList.ContainerFromItem(Item) is ListViewItem ListItem)
                {
                    ToggleButton Button = ListItem.FindChildOfName<ToggleButton>("FeedBackLike");
                    if (Button.IsChecked.GetValueOrDefault())
                    {
                        Button.Unchecked -= FeedBackLike_Unchecked;
                        Button.IsChecked = false;
                        Button.Unchecked += FeedBackLike_Unchecked;
                        Item.UpdateSupportInfo(FeedBackUpdateType.Like, false);
                    }
                }

                Item.UpdateSupportInfo(FeedBackUpdateType.Dislike, true);
                if (!await MySQL.Current.UpdateFeedBackVoteAsync(Item))
                {
                    if (Globalization.Language == LanguageEnum.Chinese)
                    {
                        QueueContentDialog dialog = new QueueContentDialog
                        {
                            Title = "错误",
                            Content = "因网络原因无法进行此项操作",
                            CloseButtonText = "确定"
                        };
                        _ = await dialog.ShowAsync();
                    }
                    else
                    {
                        QueueContentDialog dialog = new QueueContentDialog
                        {
                            Title = "Error",
                            Content = "This operation cannot be performed due to network reasons",
                            CloseButtonText = "Got it"
                        };
                        _ = await dialog.ShowAsync();
                    }
                }
            }
        }

        private async void FeedDislike_Unchecked(object sender, RoutedEventArgs e)
        {
            if ((e.OriginalSource as FrameworkElement)?.DataContext is FeedBackItem Item)
            {
                Item.UpdateSupportInfo(FeedBackUpdateType.Dislike, false);
                if (!await MySQL.Current.UpdateFeedBackVoteAsync(Item))
                {
                    if (Globalization.Language == LanguageEnum.Chinese)
                    {
                        QueueContentDialog dialog = new QueueContentDialog
                        {
                            Title = "错误",
                            Content = "因网络原因无法进行此项操作",
                            CloseButtonText = "确定"
                        };
                        _ = await dialog.ShowAsync();
                    }
                    else
                    {
                        QueueContentDialog dialog = new QueueContentDialog
                        {
                            Title = "Error",
                            Content = "This operation cannot be performed due to network reasons",
                            CloseButtonText = "Got it"
                        };
                        _ = await dialog.ShowAsync();
                    }
                }
            }
        }

        private async void FeedBackLike_Checked(object sender, RoutedEventArgs e)
        {
            if ((e.OriginalSource as FrameworkElement)?.DataContext is FeedBackItem Item)
            {
                if (FeedBackList.ContainerFromItem(Item) is ListViewItem ListItem)
                {
                    ToggleButton Button = ListItem.FindChildOfName<ToggleButton>("FeedDislike");
                    if (Button.IsChecked.GetValueOrDefault())
                    {
                        Button.Unchecked -= FeedDislike_Unchecked;
                        Button.IsChecked = false;
                        Button.Unchecked += FeedDislike_Unchecked;
                        Item.UpdateSupportInfo(FeedBackUpdateType.Dislike, false);
                    }
                }

                Item.UpdateSupportInfo(FeedBackUpdateType.Like, true);
                if (!await MySQL.Current.UpdateFeedBackVoteAsync(Item))
                {
                    if (Globalization.Language == LanguageEnum.Chinese)
                    {
                        QueueContentDialog dialog = new QueueContentDialog
                        {
                            Title = "错误",
                            Content = "因网络原因无法进行此项操作",
                            CloseButtonText = "确定"
                        };
                        _ = await dialog.ShowAsync();
                    }
                    else
                    {
                        QueueContentDialog dialog = new QueueContentDialog
                        {
                            Title = "Error",
                            Content = "This operation cannot be performed due to network reasons",
                            CloseButtonText = "Got it"
                        };
                        _ = await dialog.ShowAsync();
                    }
                }
            }
        }

        private async void FeedBackLike_Unchecked(object sender, RoutedEventArgs e)
        {
            if ((e.OriginalSource as FrameworkElement)?.DataContext is FeedBackItem Item)
            {
                Item.UpdateSupportInfo(FeedBackUpdateType.Like, false);
                if (!await MySQL.Current.UpdateFeedBackVoteAsync(Item))
                {
                    if (Globalization.Language == LanguageEnum.Chinese)
                    {
                        QueueContentDialog dialog = new QueueContentDialog
                        {
                            Title = "错误",
                            Content = "因网络原因无法进行此项操作",
                            CloseButtonText = "确定"
                        };
                        _ = await dialog.ShowAsync();
                    }
                    else
                    {
                        QueueContentDialog dialog = new QueueContentDialog
                        {
                            Title = "Error",
                            Content = "This operation cannot be performed due to network reasons",
                            CloseButtonText = "Got it"
                        };
                        _ = await dialog.ShowAsync();
                    }
                }
            }
        }

        private void FeedBackList_RightTapped(object sender, RightTappedRoutedEventArgs e)
        {
            if ((e.OriginalSource as FrameworkElement)?.DataContext is FeedBackItem Item)
            {
                FeedBackList.SelectedItem = Item;
                FeedBackList.ContextFlyout = Item.UserID == UserID ? FeedBackFlyout : null;
            }
        }

        private async void FeedBackEdit_Click(object sender, RoutedEventArgs e)
        {
            if (FeedBackList.SelectedItem is FeedBackItem SelectItem)
            {
                FeedBackDialog Dialog = new FeedBackDialog(SelectItem.Title, SelectItem.Suggestion);
                if ((await Dialog.ShowAsync()) == ContentDialogResult.Primary)
                {
                    if (!await MySQL.Current.UpdateFeedBackTitleAndSuggestionAsync(Dialog.TitleName, Dialog.FeedBack, SelectItem.GUID))
                    {
                        if (Globalization.Language == LanguageEnum.Chinese)
                        {
                            QueueContentDialog dialog = new QueueContentDialog
                            {
                                Title = "错误",
                                Content = "因网络原因无法进行此项操作",
                                CloseButtonText = "确定"
                            };
                            _ = await dialog.ShowAsync();
                        }
                        else
                        {
                            QueueContentDialog dialog = new QueueContentDialog
                            {
                                Title = "Error",
                                Content = "This operation cannot be performed due to network reasons",
                                CloseButtonText = "Got it"
                            };
                            _ = await dialog.ShowAsync();
                        }
                    }
                    else
                    {
                        SelectItem.UpdateTitleAndSuggestion(Dialog.TitleName, Dialog.FeedBack);
                    }
                }
            }
        }

        private async void FeedBackDelete_Click(object sender, RoutedEventArgs e)
        {
            if (FeedBackList.SelectedItem is FeedBackItem SelectItem)
            {
                if (!await MySQL.Current.DeleteFeedBackAsync(SelectItem))
                {
                    if (Globalization.Language == LanguageEnum.Chinese)
                    {
                        QueueContentDialog dialog = new QueueContentDialog
                        {
                            Title = "错误",
                            Content = "因网络原因无法进行此项操作",
                            CloseButtonText = "确定"
                        };
                        _ = await dialog.ShowAsync();
                    }
                    else
                    {
                        QueueContentDialog dialog = new QueueContentDialog
                        {
                            Title = "Error",
                            Content = "This operation cannot be performed due to network reasons",
                            CloseButtonText = "Got it"
                        };
                        _ = await dialog.ShowAsync();
                    }
                }
                else
                {
                    FeedBackCollection.Remove(SelectItem);
                }
            }
        }

        private void FeedBackQuestion_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            FeedBackTip.IsOpen = true;
        }

        private void OpenLeftArea_Toggled(object sender, RoutedEventArgs e)
        {
            if (OpenLeftArea.IsOn)
            {
                ApplicationData.Current.LocalSettings.Values["IsLeftAreaOpen"] = true;
                ThisPC.ThisPage.Gr.ColumnDefinitions[0].Width = new GridLength(300);
            }
            else
            {
                ApplicationData.Current.LocalSettings.Values["IsLeftAreaOpen"] = false;
                ThisPC.ThisPage.Gr.ColumnDefinitions[0].Width = new GridLength(0);
            }
        }

        private void FolderOpenMethod_Toggled(object sender, RoutedEventArgs e)
        {
            if (FolderOpenMethod.IsOn)
            {
                ApplicationData.Current.LocalSettings.Values["IsDoubleClickEnable"] = true;
                IsDoubleClickEnable = true;
            }
            else
            {
                ApplicationData.Current.LocalSettings.Values["IsDoubleClickEnable"] = false;
                IsDoubleClickEnable = false;
            }
        }

        private void AcrylicMode_Checked(object sender, RoutedEventArgs e)
        {
            CustomAcrylicArea.Visibility = Visibility.Visible;
            CustomPictureArea.Visibility = Visibility.Collapsed;

            BackgroundController.Current.SwitchTo(BackgroundBrushType.Acrylic);
            ApplicationData.Current.LocalSettings.Values["CustomUISubMode"] = Enum.GetName(typeof(BackgroundBrushType), BackgroundBrushType.Acrylic);

            if (ApplicationData.Current.LocalSettings.Values["BackgroundTintLuminosity"] is string Luminosity)
            {
                float Value = Convert.ToSingle(Luminosity);
                TintLuminositySlider.Value = Value;
                BackgroundController.Current.TintLuminosityOpacity = Value;
            }
            else
            {
                TintLuminositySlider.Value = 0.8;
                BackgroundController.Current.TintLuminosityOpacity = 0.8;
            }

            if (ApplicationData.Current.LocalSettings.Values["BackgroundTintOpacity"] is string Opacity)
            {
                float Value = Convert.ToSingle(Opacity);
                TintOpacitySlider.Value = Value;
                BackgroundController.Current.TintOpacity = Value;
            }
            else
            {
                TintOpacitySlider.Value = 0.6;
                BackgroundController.Current.TintOpacity = 0.6;
            }

            if (ApplicationData.Current.LocalSettings.Values["AcrylicThemeColor"] is string AcrylicColor)
            {
                BackgroundController.Current.AcrylicColor = BackgroundController.Current.GetColorFromHexString(AcrylicColor);
            }
        }

        private void PictureMode_Checked(object sender, RoutedEventArgs e)
        {
            CustomAcrylicArea.Visibility = Visibility.Collapsed;
            CustomPictureArea.Visibility = Visibility.Visible;

            ApplicationData.Current.LocalSettings.Values["CustomUISubMode"] = Enum.GetName(typeof(BackgroundBrushType), BackgroundBrushType.Picture);

            if (ApplicationData.Current.LocalSettings.Values["PictureBackgroundUri"] is string Uri)
            {
                BackgroundPicture PictureItem = PictureList.FirstOrDefault((Picture) => Picture.PictureUri.ToString() == Uri);

                PictureGirdView.SelectedItem = PictureItem;
                PictureGirdView.ScrollIntoViewSmoothly(PictureItem);
                BackgroundController.Current.SwitchTo(BackgroundBrushType.Picture, PictureItem.PictureUri.ToString());
            }
            else
            {
                PictureGirdView.SelectedIndex = 0;
                BackgroundController.Current.SwitchTo(BackgroundBrushType.Picture, PictureList.FirstOrDefault().PictureUri.ToString());
            }
        }

        private void PictureGirdView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (PictureGirdView.SelectedItem is BackgroundPicture Picture)
            {
                BackgroundController.Current.SwitchTo(BackgroundBrushType.Picture, Picture.PictureUri.ToString());
            }
        }

        private async void AddImageToPictureButton_Click(object sender, RoutedEventArgs e)
        {
            FileOpenPicker Picker = new FileOpenPicker
            {
                SuggestedStartLocation = PickerLocationId.PicturesLibrary,
                ViewMode = PickerViewMode.Thumbnail
            };
            Picker.FileTypeFilter.Add(".png");
            Picker.FileTypeFilter.Add(".jpg");
            Picker.FileTypeFilter.Add(".jpeg");
            Picker.FileTypeFilter.Add(".bmp");

            if (await Picker.PickSingleFileAsync() is StorageFile File)
            {
                StorageFolder ImageFolder = await ApplicationData.Current.LocalFolder.GetFolderAsync("CustomImageFolder");

                StorageFile CopyedFile = await File.CopyAsync(ImageFolder, $"BackgroundPicture_{Guid.NewGuid().ToString("N")}{File.FileType}", NameCollisionOption.GenerateUniqueName);

                BitmapImage Bitmap = new BitmapImage
                {
                    DecodePixelWidth = 160,
                    DecodePixelHeight = 90
                };
                BackgroundPicture Picture = new BackgroundPicture(Bitmap, new Uri($"ms-appdata:///local/CustomImageFolder/{CopyedFile.Name}"));
                PictureList.Add(Picture);
                Bitmap.UriSource = Picture.PictureUri;

                PictureGirdView.ScrollIntoViewSmoothly(Picture);
                PictureGirdView.SelectedItem = Picture;

                await SQLite.Current.SetBackgroundPictureAsync(Picture.PictureUri.ToString());
            }
        }

        private async void DeletePictureButton_Click(object sender, RoutedEventArgs e)
        {
            if (PictureGirdView.SelectedItem is BackgroundPicture Picture)
            {
                await SQLite.Current.DeleteBackgroundPictureAsync(Picture.PictureUri.ToString());
                PictureList.Remove(Picture);
                PictureGirdView.SelectedIndex = PictureList.Count - 1;
            }
        }

        private void PictureGirdView_RightTapped(object sender, RightTappedRoutedEventArgs e)
        {
            if ((e.OriginalSource as FrameworkElement)?.DataContext is BackgroundPicture Picture)
            {
                PictureGirdView.ContextFlyout = PictureFlyout;

                DeletePictureButton.IsEnabled = !Picture.PictureUri.ToString().StartsWith("ms-appx://");

                PictureGirdView.SelectedItem = Picture;
            }
            else
            {
                PictureGirdView.ContextFlyout = null;
            }
        }
    }
}
