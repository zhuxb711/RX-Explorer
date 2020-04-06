using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using TinyPinyin.Core;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Core;
using Windows.Services.Store;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.System;
using Windows.UI;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;


namespace FileManager
{
    public sealed partial class SettingControl : UserControl
    {
        private ObservableCollection<FeedBackItem> FeedBackCollection = new ObservableCollection<FeedBackItem>();

        private ObservableCollection<BackgroundPicture> PictureList = new ObservableCollection<BackgroundPicture>();

        private readonly string UserName = ApplicationData.Current.LocalSettings.Values["SystemUserName"].ToString();

        private readonly string UserID = ApplicationData.Current.LocalSettings.Values["SystemUserID"].ToString();

        public static bool IsDoubleClickEnable { get; set; } = true;

        private int EnterAndExitLock = 0;

        public bool IsOpened { get; private set; } = false;

        public SettingControl()
        {
            InitializeComponent();

            Version.Text = string.Format("Version: {0}.{1}.{2}.{3}", Package.Current.Id.Version.Major, Package.Current.Id.Version.Minor, Package.Current.Id.Version.Build, Package.Current.Id.Version.Revision);

            EmptyFeedBack.Text = Globalization.Language == LanguageEnum.Chinese ? "正在加载..." : "Loading...";

            Loading += SettingPage_Loading;
            Loaded += SettingPage_Loaded;
        }

        public async Task ExcuteWhenShown()
        {
            AutoBoot.Toggled -= AutoBoot_Toggled;

            switch ((await StartupTask.GetAsync("RXExplorer")).State)
            {
                case StartupTaskState.DisabledByPolicy:
                case StartupTaskState.DisabledByUser:
                case StartupTaskState.Disabled:
                    {
                        AutoBoot.IsOn = false;
                        break;
                    }
                default:
                    {
                        AutoBoot.IsOn = true;
                        break;
                    }
            }

            AutoBoot.Toggled += AutoBoot_Toggled;


            await Task.Delay(1000).ConfigureAwait(true);

            if (PictureMode.IsChecked.GetValueOrDefault() && PictureGirdView.SelectedItem != null)
            {
                PictureGirdView.ScrollIntoViewSmoothly(PictureGirdView.SelectedItem);
            }
        }

        public async Task Show()
        {
            if (!IsOpened && Interlocked.Exchange(ref EnterAndExitLock, 1) == 0)
            {
                IsOpened = true;

                Scroll.ChangeView(null, 0, null, true);

                Visibility = Visibility.Visible;

                ActivateAnimation(Gr, TimeSpan.FromMilliseconds(600), TimeSpan.FromMilliseconds(200), 200, false);
                ActivateAnimation(LeftPanel, TimeSpan.FromMilliseconds(800), TimeSpan.FromMilliseconds(300), 150, false);
                ActivateAnimation(RightPanel, TimeSpan.FromMilliseconds(800), TimeSpan.FromMilliseconds(300), 150, false);

                await ExcuteWhenShown().ConfigureAwait(false);
            }
        }

        public async Task Hide()
        {
            if (IsOpened)
            {
                IsOpened = false;

                ActivateAnimation(LeftPanel, TimeSpan.FromMilliseconds(600), TimeSpan.FromMilliseconds(200), 150, true);
                ActivateAnimation(RightPanel, TimeSpan.FromMilliseconds(600), TimeSpan.FromMilliseconds(200), 150, true);
                ActivateAnimation(Gr, TimeSpan.FromMilliseconds(800), TimeSpan.FromMilliseconds(300), 200, true);

                await Task.Delay(1400).ConfigureAwait(true);

                Visibility = Visibility.Collapsed;

                _ = Interlocked.Exchange(ref EnterAndExitLock, 0);
            }
        }

        private void ActivateAnimation(UIElement Element, TimeSpan Duration, TimeSpan DelayTime, float VerticalOffset, bool IsReverse)
        {
            Visual Visual = ElementCompositionPreview.GetElementVisual(Element);

            Vector3KeyFrameAnimation EntranceAnimation = Visual.Compositor.CreateVector3KeyFrameAnimation();
            ScalarKeyFrameAnimation FadeAnimation = Visual.Compositor.CreateScalarKeyFrameAnimation();

            EntranceAnimation.Target = nameof(Visual.Offset);
            EntranceAnimation.InsertKeyFrame(0, new Vector3(Visual.Offset.X, VerticalOffset, Visual.Offset.Z));
            EntranceAnimation.InsertKeyFrame(1, new Vector3(Visual.Offset.X, 0, Visual.Offset.Z), Visual.Compositor.CreateCubicBezierEasingFunction(new Vector2(.1f, .9f), new Vector2(.2f, 1)));
            EntranceAnimation.Duration = Duration;
            EntranceAnimation.DelayTime = DelayTime;
            EntranceAnimation.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;

            FadeAnimation.Target = nameof(Visual.Opacity);
            FadeAnimation.InsertKeyFrame(0, 0);
            FadeAnimation.InsertKeyFrame(1, 1);
            FadeAnimation.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;
            FadeAnimation.DelayTime = DelayTime;
            FadeAnimation.Duration = Duration;

            if (IsReverse)
            {
                EntranceAnimation.Direction = AnimationDirection.Reverse;
                FadeAnimation.Direction = AnimationDirection.Reverse;
            }
            else
            {
                EntranceAnimation.Direction = AnimationDirection.Normal;
                FadeAnimation.Direction = AnimationDirection.Normal;
            }

            CompositionAnimationGroup AnimationGroup = Visual.Compositor.CreateAnimationGroup();
            AnimationGroup.Add(EntranceAnimation);
            AnimationGroup.Add(FadeAnimation);

            Visual.StartAnimationGroup(AnimationGroup);
        }

        private void SettingPage_Loading(FrameworkElement sender, object args)
        {
            if (Globalization.Language == LanguageEnum.Chinese)
            {
                UIMode.Items.Add("推荐");
                UIMode.Items.Add("纯色");
                UIMode.Items.Add("自定义");
            }
            else
            {
                UIMode.Items.Add("Recommand");
                UIMode.Items.Add("Solid Color");
                UIMode.Items.Add("Custom");
            }

            if (ApplicationData.Current.LocalSettings.Values["UIDisplayMode"] is string Mode)
            {
                UIMode.SelectedItem = UIMode.Items.Where((Item) => Item.ToString() == Mode).FirstOrDefault();
            }
            else
            {
                ApplicationData.Current.LocalSettings.Values["UIDisplayMode"] = Globalization.Language == LanguageEnum.Chinese
                                                                                ? "推荐"
                                                                                : "Recommand";
                UIMode.SelectedIndex = 0;
            }

            if (ApplicationData.Current.LocalSettings.Values["IsLeftAreaOpen"] is bool Enable)
            {
                OpenLeftArea.IsOn = Enable;
            }

            if (ApplicationData.Current.LocalSettings.Values["IsDoubleClickEnable"] is bool IsDoubleClick)
            {
                FolderOpenMethod.IsOn = IsDoubleClick;
            }

            if (ApplicationData.Current.LocalSettings.Values["EnablePreLaunch"] is bool PreLaunch)
            {
                EnablePreLaunch.IsOn = PreLaunch;
            }

            if (AppThemeController.Current.Theme == ElementTheme.Light)
            {
                CustomFontColor.IsOn = true;
            }
        }

        private async void SettingPage_Loaded(object sender, RoutedEventArgs e)
        {
            FeedBackCollection.CollectionChanged += (s, t) =>
            {
                if (FeedBackCollection.Count == 0)
                {
                    EmptyFeedBack.Text = Globalization.Language == LanguageEnum.Chinese ? "无任何反馈或建议" : "No feedback or suggestions";
                    EmptyFeedBack.Visibility = Visibility.Visible;
                    FeedBackList.Visibility = Visibility.Collapsed;
                }
                else
                {
                    EmptyFeedBack.Visibility = Visibility.Collapsed;
                    FeedBackList.Visibility = Visibility.Visible;
                }
            };

            try
            {
                await foreach (FeedBackItem FeedBackItem in MySQL.Current.GetAllFeedBackAsync())
                {
                    if (FeedBackItem.Title.StartsWith("@"))
                    {
                        if (Globalization.Language == LanguageEnum.Chinese)
                        {
                            FeedBackItem.UpdateTitleAndSuggestion(FeedBackItem.Title, await FeedBackItem.Suggestion.Translate().ConfigureAwait(true));
                        }
                        else
                        {
                            FeedBackItem.UpdateTitleAndSuggestion(FeedBackItem.Title.All((Char) => !PinyinHelper.IsChinese(Char)) ? FeedBackItem.Title : PinyinHelper.GetPinyin(FeedBackItem.Title), await FeedBackItem.Suggestion.Translate().ConfigureAwait(true));
                        }
                    }
                    else
                    {
                        FeedBackItem.UpdateTitleAndSuggestion(await FeedBackItem.Title.Translate().ConfigureAwait(true), await FeedBackItem.Suggestion.Translate().ConfigureAwait(true));
                    }

                    FeedBackCollection.Add(FeedBackItem);
                }
            }
            catch { }
            finally
            {
                if (FeedBackCollection.Count == 0)
                {
                    EmptyFeedBack.Text = Globalization.Language == LanguageEnum.Chinese ? "无任何反馈或建议" : "No feedback or suggestions";
                }
                else
                {
                    FeedBackList.UpdateLayout();

                    await Task.Delay(500).ConfigureAwait(true);

                    FeedBackList.ScrollIntoViewSmoothly(FeedBackCollection.Last());
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

        private async void Like_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            _ = await Launcher.LaunchUriAsync(new Uri("ms-windows-store://review/?productid=9N88QBQKF2RS"));
        }

        private async void FlyoutContinue_Click(object sender, RoutedEventArgs e)
        {
            ConfirmFly.Hide();
            await SQLite.Current.ClearSearchHistoryRecord().ConfigureAwait(true);

            if (Globalization.Language == LanguageEnum.Chinese)
            {
                QueueContentDialog dialog = new QueueContentDialog
                {
                    Title = "提示",
                    Content = "搜索历史记录清理完成",
                    CloseButtonText = "确定"
                };
                _ = await dialog.ShowAsync().ConfigureAwait(true);
            }
            else
            {
                QueueContentDialog dialog = new QueueContentDialog
                {
                    Title = "Tips",
                    Content = "Search history cleanup completed",
                    CloseButtonText = "Confirm"
                };
                _ = await dialog.ShowAsync().ConfigureAwait(true);
            }
        }

        private void FlyoutCancel_Click(object sender, RoutedEventArgs e)
        {
            ConfirmFly.Hide();
        }

        private async void ClearUp_Click(object sender, RoutedEventArgs e)
        {
            ResetDialog Dialog = new ResetDialog();
            if ((await Dialog.ShowAsync().ConfigureAwait(true)) == ContentDialogResult.Primary)
            {
                if (Dialog.IsClearSecureFolder)
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
                        await ApplicationData.Current.LocalFolder.DeleteAllSubFilesAndFolders().ConfigureAwait(true);
                        await ApplicationData.Current.TemporaryFolder.DeleteAllSubFilesAndFolders().ConfigureAwait(true);
                        await ApplicationData.Current.LocalCacheFolder.DeleteAllSubFilesAndFolders().ConfigureAwait(true);
                    }

                    Window.Current.Activate();
                    switch (await CoreApplication.RequestRestartAsync(string.Empty))
                    {
                        case AppRestartFailureReason.InvalidUser:
                        case AppRestartFailureReason.NotInForeground:
                        case AppRestartFailureReason.Other:
                            {
                                if (Globalization.Language == LanguageEnum.Chinese)
                                {
                                    QueueContentDialog Dialog1 = new QueueContentDialog
                                    {
                                        Title = "错误",
                                        Content = "自动重新启动过程中出现问题，请手动重启RX文件管理器",
                                        CloseButtonText = "确定"
                                    };
                                    _ = await Dialog1.ShowAsync().ConfigureAwait(true);
                                }
                                else
                                {
                                    QueueContentDialog Dialog1 = new QueueContentDialog
                                    {
                                        Title = "Error",
                                        Content = "There was a problem during the automatic restart, please restart the RX Explorer manually",
                                        CloseButtonText = "Got it"
                                    };
                                    _ = await Dialog1.ShowAsync().ConfigureAwait(true);
                                }
                                break;
                            }
                    }
                }
                else
                {
                    LoadingText.Text = Globalization.Language == LanguageEnum.Chinese ? "正在导出..." : "Exporting";
                    LoadingControl.IsLoading = true;
                    MainPage.ThisPage.IsAnyTaskRunning = true;

                    StorageFolder SecureFolder = await ApplicationData.Current.LocalCacheFolder.CreateFolderAsync("SecureFolder", CreationCollisionOption.OpenIfExists);
                    string FileEncryptionAesKey = KeyGenerator.GetMD5FromKey(CredentialProtector.GetPasswordFromProtector("SecureAreaPrimaryPassword"), 16);

                    foreach (var Item in await SecureFolder.GetFilesAsync())
                    {
                        try
                        {
                            _ = await Item.DecryptAsync(Dialog.ExportFolder, FileEncryptionAesKey).ConfigureAwait(true);

                            await Item.DeleteAsync(StorageDeleteOption.PermanentDelete);
                        }
                        catch (Exception ex)
                        {
                            await Item.MoveAsync(Dialog.ExportFolder, Item.Name + (Globalization.Language == LanguageEnum.Chinese ? "-解密错误备份" : "-Decrypt Error Backup"), NameCollisionOption.GenerateUniqueName);
                            if (ex is PasswordErrorException)
                            {
                                if (Globalization.Language == LanguageEnum.Chinese)
                                {
                                    QueueContentDialog Dialog1 = new QueueContentDialog
                                    {
                                        Title = "错误",
                                        Content = "由于解密密码错误，解密失败，导出任务已经终止\r\r这可能是由于待解密文件数据不匹配造成的",
                                        CloseButtonText = "确定"
                                    };
                                    _ = await Dialog1.ShowAsync().ConfigureAwait(true);
                                }
                                else
                                {
                                    QueueContentDialog Dialog1 = new QueueContentDialog
                                    {
                                        Title = "Error",
                                        Content = "The decryption failed due to the wrong decryption password, the export task has been terminated \r \rThis may be caused by a mismatch in the data of the files to be decrypted",
                                        CloseButtonText = "Got it"
                                    };
                                    _ = await Dialog1.ShowAsync().ConfigureAwait(true);
                                }
                            }
                            else if (ex is FileDamagedException)
                            {
                                if (Globalization.Language == LanguageEnum.Chinese)
                                {
                                    QueueContentDialog Dialog1 = new QueueContentDialog
                                    {
                                        Title = "错误",
                                        Content = "由于待解密文件的内部结构损坏，解密失败，导出任务已经终止\r\r这可能是由于文件数据已损坏或被修改造成的",
                                        CloseButtonText = "确定"
                                    };
                                    _ = await Dialog1.ShowAsync().ConfigureAwait(true);
                                }
                                else
                                {
                                    QueueContentDialog Dialog1 = new QueueContentDialog
                                    {
                                        Title = "Error",
                                        Content = "Because the internal structure of the file to be decrypted is damaged and the decryption fails, the export task has been terminated \r \rThis may be caused by the file data being damaged or modified",
                                        CloseButtonText = "Got it"
                                    };
                                    _ = await Dialog1.ShowAsync().ConfigureAwait(true);
                                }
                            }
                        }
                    }

                    SQLite.Current.Dispose();
                    MySQL.Current.Dispose();
                    try
                    {
                        ApplicationData.Current.LocalSettings.Values.Clear();
                        await ApplicationData.Current.ClearAsync(ApplicationDataLocality.Local);
                        await ApplicationData.Current.ClearAsync(ApplicationDataLocality.Temporary);
                        await ApplicationData.Current.ClearAsync(ApplicationDataLocality.Roaming);
                    }
                    catch (Exception)
                    {
                        await ApplicationData.Current.LocalFolder.DeleteAllSubFilesAndFolders().ConfigureAwait(true);
                        await ApplicationData.Current.TemporaryFolder.DeleteAllSubFilesAndFolders().ConfigureAwait(true);
                        await ApplicationData.Current.RoamingFolder.DeleteAllSubFilesAndFolders().ConfigureAwait(true);
                    }

                    await Task.Delay(1000).ConfigureAwait(true);

                    LoadingControl.IsLoading = false;
                    MainPage.ThisPage.IsAnyTaskRunning = false;

                    await Task.Delay(1000).ConfigureAwait(true);

                    Window.Current.Activate();
                    switch (await CoreApplication.RequestRestartAsync(string.Empty))
                    {
                        case AppRestartFailureReason.InvalidUser:
                        case AppRestartFailureReason.NotInForeground:
                        case AppRestartFailureReason.Other:
                            {
                                if (Globalization.Language == LanguageEnum.Chinese)
                                {
                                    QueueContentDialog Dialog1 = new QueueContentDialog
                                    {
                                        Title = "错误",
                                        Content = "自动重新启动过程中出现问题，请手动重启RX文件管理器",
                                        CloseButtonText = "确定"
                                    };
                                    _ = await Dialog1.ShowAsync().ConfigureAwait(true);
                                }
                                else
                                {
                                    QueueContentDialog Dialog1 = new QueueContentDialog
                                    {
                                        Title = "Error",
                                        Content = "There was a problem during the automatic restart, please restart the RX Explorer manually",
                                        CloseButtonText = "Got it"
                                    };
                                    _ = await Dialog1.ShowAsync().ConfigureAwait(true);
                                }
                                break;
                            }
                    }
                }
            }
        }

        private void UIMode_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ApplicationData.Current.LocalSettings.Values["UIDisplayMode"] = UIMode.SelectedItem.ToString();

            switch (UIMode.SelectedIndex)
            {
                case 0:
                    {
                        CustomUIArea.Visibility = Visibility.Collapsed;
                        SolidColorArea.Visibility = Visibility.Collapsed;

                        AcrylicMode.IsChecked = null;
                        PictureMode.IsChecked = null;
                        SolidColor_White.IsChecked = null;
                        SolidColor_Black.IsChecked = null;
                        CustomFontColor.IsEnabled = true;

                        BackgroundController.Current.SwitchTo(BackgroundBrushType.Acrylic);
                        BackgroundController.Current.TintOpacity = 0.6;
                        BackgroundController.Current.TintLuminosityOpacity = -1;
                        BackgroundController.Current.AcrylicColor = Colors.LightSlateGray;
                        break;
                    }
                case 1:
                    {
                        CustomUIArea.Visibility = Visibility.Collapsed;
                        SolidColorArea.Visibility = Visibility.Visible;

                        AcrylicMode.IsChecked = null;
                        PictureMode.IsChecked = null;
                        CustomFontColor.IsEnabled = false;

                        if (ApplicationData.Current.LocalSettings.Values["SolidColorType"] is string ColorType)
                        {
                            if (ColorType == Colors.White.ToString())
                            {
                                SolidColor_White.IsChecked = true;
                            }
                            else
                            {
                                SolidColor_Black.IsChecked = true;
                            }
                        }
                        else
                        {
                            SolidColor_White.IsChecked = true;
                        }

                        break;
                    }
                default:
                    {
                        CustomUIArea.Visibility = Visibility.Visible;
                        SolidColorArea.Visibility = Visibility.Collapsed;
                        SolidColor_White.IsChecked = null;
                        SolidColor_Black.IsChecked = null;
                        CustomFontColor.IsEnabled = true;

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
                                BackgroundController.Current.AcrylicColor = BackgroundController.GetColorFromHexString(AcrylicColor);
                            }
                        }

                        break;
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
                              "若您不愿意，则可以点击\"跪安\"以取消\r" +
                              "若您愿意支持开发者，则可以点击\"准奏\"\r\r" +
                              "Tips: 支持的小伙伴可以解锁独有文件保险柜功能：“安全域”",
                    PrimaryButtonText = "准奏",
                    CloseButtonText = "跪安"
                };
                if (await dialog.ShowAsync().ConfigureAwait(true) == ContentDialogResult.Primary)
                {
                    StoreContext Store = StoreContext.GetDefault();
                    StoreProductQueryResult PurchasedProductResult = await Store.GetUserCollectionAsync(new string[] { "Durable" });
                    if (PurchasedProductResult.ExtendedError == null)
                    {
                        if (PurchasedProductResult.Products.Count > 0)
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
                            _ = await QueueContenDialog.ShowAsync().ConfigureAwait(true);
                        }
                        else
                        {
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
                                                _ = await QueueContenDialog.ShowAsync().ConfigureAwait(true);
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
                                                _ = await QueueContenDialog.ShowAsync().ConfigureAwait(true);
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
                                                _ = await QueueContenDialog.ShowAsync().ConfigureAwait(true);
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
                                _ = await QueueContenDialog.ShowAsync().ConfigureAwait(true);
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
                        _ = await QueueContenDialog.ShowAsync().ConfigureAwait(true);
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
                              "If you don't want to, you can click \"Later\" to cancel\r" +
                              "if you want to donate, you can click \"Donate\" to support developer\r\r" +
                              "Tips: Donator can unlock the unique file safe feature: \"Security Area\"",
                    PrimaryButtonText = "Donate",
                    CloseButtonText = "Later"
                };
                if (await dialog.ShowAsync().ConfigureAwait(true) == ContentDialogResult.Primary)
                {
                    StoreContext Store = StoreContext.GetDefault();
                    StoreProductQueryResult PurchasedProductResult = await Store.GetUserCollectionAsync(new string[] { "Durable" });
                    if (PurchasedProductResult.ExtendedError == null)
                    {
                        if (PurchasedProductResult.Products.Count > 0)
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
                            _ = await QueueContenDialog.ShowAsync().ConfigureAwait(true);
                        }
                        else
                        {
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
                                                    CloseButtonText = "Got it"
                                                };
                                                _ = await QueueContenDialog.ShowAsync().ConfigureAwait(true);
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
                                                _ = await QueueContenDialog.ShowAsync().ConfigureAwait(true);
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
                                                _ = await QueueContenDialog.ShowAsync().ConfigureAwait(true);
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
                                _ = await QueueContenDialog.ShowAsync().ConfigureAwait(true);
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
                        _ = await QueueContenDialog.ShowAsync().ConfigureAwait(true);
                    }
                }
            }
        }

        private async void UpdateLogLink_Click(object sender, RoutedEventArgs e)
        {
            WhatIsNew Dialog = new WhatIsNew();
            _ = await Dialog.ShowAsync().ConfigureAwait(true);
        }

        private async void SystemInfoButton_Click(object sender, RoutedEventArgs e)
        {
            if (Package.Current.Id.Architecture == ProcessorArchitecture.X64 || Package.Current.Id.Architecture == ProcessorArchitecture.X86)
            {
                SystemInfoDialog dialog = new SystemInfoDialog();
                _ = await dialog.ShowAsync().ConfigureAwait(true);
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
                    _ = await dialog.ShowAsync().ConfigureAwait(true);
                }
                else
                {
                    QueueContentDialog dialog = new QueueContentDialog
                    {
                        Title = "Sorry",
                        Content = "Some components that the system information dialog depends on only support X86 or X64 processors\rUnsupport ARM processor for now, so this dialog will not be opened",
                        CloseButtonText = "Got it"
                    };
                    _ = await dialog.ShowAsync().ConfigureAwait(true);
                }
            }

        }

        private async void AddFeedBack_Click(object sender, RoutedEventArgs e)
        {
            FeedBackDialog Dialog = new FeedBackDialog();
            if ((await Dialog.ShowAsync().ConfigureAwait(true)) == ContentDialogResult.Primary)
            {
                if (FeedBackCollection.Count != 0)
                {
                    if (FeedBackCollection.FirstOrDefault((It) => It.UserName == UserName && It.Suggestion == Dialog.FeedBack && It.Title == Dialog.TitleName) == null)
                    {
                        FeedBackItem Item = new FeedBackItem(UserName, Dialog.TitleName, Dialog.FeedBack, "0", "0", UserID, Guid.NewGuid().ToString("D"));
                        if (await MySQL.Current.SetFeedBackAsync(Item).ConfigureAwait(true))
                        {
                            FeedBackCollection.Add(Item);
                            await Task.Delay(1000).ConfigureAwait(true);
                            FeedBackList.ScrollIntoViewSmoothly(FeedBackCollection.Last());
                        }
                        else
                        {
                            if (Globalization.Language == LanguageEnum.Chinese)
                            {
                                QueueContentDialog dialog = new QueueContentDialog
                                {
                                    Title = "错误",
                                    Content = "因网络原因无法进行此项操作",
                                    CloseButtonText = "确定"
                                };
                                _ = await dialog.ShowAsync().ConfigureAwait(true);
                            }
                            else
                            {
                                QueueContentDialog dialog = new QueueContentDialog
                                {
                                    Title = "Error",
                                    Content = "This operation cannot be performed due to network reasons",
                                    CloseButtonText = "Got it"
                                };
                                _ = await dialog.ShowAsync().ConfigureAwait(true);
                            }
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
                        _ = await TipsDialog.ShowAsync().ConfigureAwait(true);
                    }
                }
                else
                {
                    FeedBackItem Item = new FeedBackItem(UserName, Dialog.TitleName, Dialog.FeedBack, "0", "0", UserID, Guid.NewGuid().ToString("D"));
                    if (!await MySQL.Current.SetFeedBackAsync(Item).ConfigureAwait(true))
                    {
                        if (Globalization.Language == LanguageEnum.Chinese)
                        {
                            QueueContentDialog dialog = new QueueContentDialog
                            {
                                Title = "错误",
                                Content = "因网络原因无法进行此项操作",
                                CloseButtonText = "确定"
                            };
                            _ = await dialog.ShowAsync().ConfigureAwait(true);
                        }
                        else
                        {
                            QueueContentDialog dialog = new QueueContentDialog
                            {
                                Title = "Error",
                                Content = "This operation cannot be performed due to network reasons",
                                CloseButtonText = "Got it"
                            };
                            _ = await dialog.ShowAsync().ConfigureAwait(true);
                        }
                    }
                    else
                    {
                        FeedBackCollection.Add(Item);
                        await Task.Delay(1000).ConfigureAwait(true);
                        FeedBackList.ScrollIntoViewSmoothly(FeedBackCollection.Last());
                    }
                }
            }
        }

        private void FeedBackList_RightTapped(object sender, RightTappedRoutedEventArgs e)
        {
            if ((e.OriginalSource as FrameworkElement)?.DataContext is FeedBackItem Item)
            {
                FeedBackList.SelectedItem = Item;
                FeedBackList.ContextFlyout = UserID == "zrfcfgs@outlook.com" ? FeedBackDevelopFlyout : (Item.UserID == UserID ? FeedBackFlyout : null);
            }
        }

        private async void FeedBackEdit_Click(object sender, RoutedEventArgs e)
        {
            if (FeedBackList.SelectedItem is FeedBackItem SelectItem)
            {
                FeedBackDialog Dialog = new FeedBackDialog(SelectItem.Title, SelectItem.Suggestion);
                if ((await Dialog.ShowAsync().ConfigureAwait(true)) == ContentDialogResult.Primary)
                {
                    if (!await MySQL.Current.UpdateFeedBackAsync(Dialog.TitleName, Dialog.FeedBack, SelectItem.GUID).ConfigureAwait(true))
                    {
                        if (Globalization.Language == LanguageEnum.Chinese)
                        {
                            QueueContentDialog dialog = new QueueContentDialog
                            {
                                Title = "错误",
                                Content = "因网络原因无法进行此项操作",
                                CloseButtonText = "确定"
                            };
                            _ = await dialog.ShowAsync().ConfigureAwait(true);
                        }
                        else
                        {
                            QueueContentDialog dialog = new QueueContentDialog
                            {
                                Title = "Error",
                                Content = "This operation cannot be performed due to network reasons",
                                CloseButtonText = "Got it"
                            };
                            _ = await dialog.ShowAsync().ConfigureAwait(true);
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
                if (!await MySQL.Current.DeleteFeedBackAsync(SelectItem).ConfigureAwait(true))
                {
                    if (Globalization.Language == LanguageEnum.Chinese)
                    {
                        QueueContentDialog dialog = new QueueContentDialog
                        {
                            Title = "错误",
                            Content = "因网络原因无法进行此项操作",
                            CloseButtonText = "确定"
                        };
                        _ = await dialog.ShowAsync().ConfigureAwait(true);
                    }
                    else
                    {
                        QueueContentDialog dialog = new QueueContentDialog
                        {
                            Title = "Error",
                            Content = "This operation cannot be performed due to network reasons",
                            CloseButtonText = "Got it"
                        };
                        _ = await dialog.ShowAsync().ConfigureAwait(true);
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
            MainPage.ThisPage.LeftSideLength = OpenLeftArea.IsOn ? new GridLength(300) : new GridLength(0);
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
                BackgroundController.Current.AcrylicColor = BackgroundController.GetColorFromHexString(AcrylicColor);
            }
        }

        private async void PictureMode_Checked(object sender, RoutedEventArgs e)
        {
            CustomAcrylicArea.Visibility = Visibility.Collapsed;
            CustomPictureArea.Visibility = Visibility.Visible;

            if (PictureList.Count == 0)
            {
                foreach (Uri ImageUri in await SQLite.Current.GetBackgroundPictureAsync().ConfigureAwait(true))
                {
                    BitmapImage Image = new BitmapImage
                    {
                        DecodePixelHeight = 90,
                        DecodePixelWidth = 160
                    };
                    PictureList.Add(new BackgroundPicture(Image, ImageUri));
                    Image.UriSource = ImageUri;
                }
            }

            ApplicationData.Current.LocalSettings.Values["CustomUISubMode"] = Enum.GetName(typeof(BackgroundBrushType), BackgroundBrushType.Picture);

            if (ApplicationData.Current.LocalSettings.Values["PictureBackgroundUri"] is string Uri)
            {
                BackgroundPicture PictureItem = PictureList.FirstOrDefault((Picture) => Picture.PictureUri.ToString() == Uri);

                PictureGirdView.SelectedItem = PictureItem;
                PictureGirdView.ScrollIntoViewSmoothly(PictureItem);
                BackgroundController.Current.SwitchTo(BackgroundBrushType.Picture, PictureItem.PictureUri);
            }
            else
            {
                PictureGirdView.SelectedIndex = 0;
                BackgroundController.Current.SwitchTo(BackgroundBrushType.Picture, PictureList.FirstOrDefault().PictureUri);
            }
        }

        private void PictureGirdView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (PictureGirdView.SelectedItem is BackgroundPicture Picture)
            {
                BackgroundController.Current.SwitchTo(BackgroundBrushType.Picture, Picture.PictureUri);
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

                await SQLite.Current.SetBackgroundPictureAsync(Picture.PictureUri).ConfigureAwait(false);
            }
        }

        private async void DeletePictureButton_Click(object sender, RoutedEventArgs e)
        {
            if (PictureGirdView.SelectedItem is BackgroundPicture Picture)
            {
                await SQLite.Current.DeleteBackgroundPictureAsync(Picture.PictureUri).ConfigureAwait(true);
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

        private void CustomFontColor_Toggled(object sender, RoutedEventArgs e)
        {
            if (CustomFontColor.IsOn)
            {
                AppThemeController.Current.ChangeThemeTo(ElementTheme.Light);
            }
            else
            {
                AppThemeController.Current.ChangeThemeTo(ElementTheme.Dark);
            }
        }

        private async void AutoBoot_Toggled(object sender, RoutedEventArgs e)
        {
            StartupTask BootTask = await StartupTask.GetAsync("RXExplorer");

            if (AutoBoot.IsOn)
            {
                switch (await BootTask.RequestEnableAsync())
                {
                    case StartupTaskState.Disabled:
                    case StartupTaskState.DisabledByPolicy:
                    case StartupTaskState.DisabledByUser:
                        {
                            AutoBoot.Toggled -= AutoBoot_Toggled;
                            AutoBoot.IsOn = false;
                            AutoBoot.Toggled += AutoBoot_Toggled;

                            if (Globalization.Language == LanguageEnum.Chinese)
                            {
                                QueueContentDialog Dialog = new QueueContentDialog
                                {
                                    Title = "提示",
                                    Content = "由于自动启动被系统禁用，RX无法自动开启此功能\r您可以前往[系统设置]页面管理",
                                    PrimaryButtonText = "立即开启",
                                    CloseButtonText = "暂不开启"
                                };
                                if ((await Dialog.ShowAsync().ConfigureAwait(true)) == ContentDialogResult.Primary)
                                {
                                    await Launcher.LaunchUriAsync(new Uri("ms-settings:appsfeatures-app"));
                                }
                            }
                            else
                            {
                                QueueContentDialog Dialog = new QueueContentDialog
                                {
                                    Title = "Tips",
                                    Content = "RX cannot be turned on automatically because startup is disabled by the system\rYou can go to the [System Settings] page to manage",
                                    PrimaryButtonText = "Now",
                                    CloseButtonText = "Later"
                                };
                                if ((await Dialog.ShowAsync().ConfigureAwait(true)) == ContentDialogResult.Primary)
                                {
                                    await Launcher.LaunchUriAsync(new Uri("ms-settings:appsfeatures-app"));
                                }
                            }
                            break;
                        }
                    default:
                        {
                            break;
                        }
                }
            }
            else
            {
                BootTask.Disable();
            }
        }

        private void EnablePreLaunch_Toggled(object sender, RoutedEventArgs e)
        {
            ApplicationData.Current.LocalSettings.Values["EnablePreLaunch"] = EnablePreLaunch.IsOn;

            CoreApplication.EnablePrelaunch(EnablePreLaunch.IsOn);
        }

        private void PreLaunchQuestion_Tapped(object sender, TappedRoutedEventArgs e)
        {
            PreLaunchTip.IsOpen = true;
        }

        private void SolidColor_White_Checked(object sender, RoutedEventArgs e)
        {
            BackgroundController.Current.SwitchTo(BackgroundBrushType.SolidColor, Color: Colors.White);
            CustomFontColor.IsOn = true;
        }

        private void SolidColor_Black_Checked(object sender, RoutedEventArgs e)
        {
            BackgroundController.Current.SwitchTo(BackgroundBrushType.SolidColor, Color: Colors.Black);
            CustomFontColor.IsOn = false;
        }

        private async void FeedBackNotice_Click(object sender, RoutedEventArgs e)
        {
            if (FeedBackList.SelectedItem is FeedBackItem SelectItem)
            {
                FeedBackDialog Dialog = new FeedBackDialog($"@{SelectItem.UserName}");
                if ((await Dialog.ShowAsync().ConfigureAwait(true)) == ContentDialogResult.Primary)
                {
                    if (FeedBackCollection.FirstOrDefault((It) => It.UserName == UserName && It.Suggestion == Dialog.FeedBack && It.Title == Dialog.TitleName) == null)
                    {
                        FeedBackItem Item = new FeedBackItem(UserName, Dialog.TitleName, Dialog.FeedBack, "0", "0", UserID, Guid.NewGuid().ToString("D"));
                        if (await MySQL.Current.SetFeedBackAsync(Item).ConfigureAwait(true))
                        {
                            FeedBackCollection.Add(Item);
                            await Task.Delay(1000).ConfigureAwait(true);
                            FeedBackList.ScrollIntoViewSmoothly(FeedBackCollection.Last());

                            if (Regex.IsMatch(SelectItem.UserID, "^\\s*([A-Za-z0-9_-]+(\\.\\w+)*@(\\w+\\.)+\\w{2,5})\\s*$"))
                            {
                                string Message = $"您的反馈原文：\r------------------------------------\r{SelectItem.Title}{Environment.NewLine}{SelectItem.Suggestion}\r------------------------------------\r\r开发者回复内容：\r------------------------------------\r{Item.Title}{Environment.NewLine}{Item.Suggestion}\r------------------------------------{Environment.NewLine}";
                                _ = await Launcher.LaunchUriAsync(new Uri($"mailto:{SelectItem.UserID}?subject=开发者已回复您的反馈&body={Uri.EscapeDataString(Message)}"), new LauncherOptions { TreatAsUntrusted = false, DisplayApplicationPicker = false });
                            }
                        }
                        else
                        {
                            if (Globalization.Language == LanguageEnum.Chinese)
                            {
                                QueueContentDialog dialog = new QueueContentDialog
                                {
                                    Title = "错误",
                                    Content = "因网络原因无法进行此项操作",
                                    CloseButtonText = "确定"
                                };
                                _ = await dialog.ShowAsync().ConfigureAwait(true);
                            }
                            else
                            {
                                QueueContentDialog dialog = new QueueContentDialog
                                {
                                    Title = "Error",
                                    Content = "This operation cannot be performed due to network reasons",
                                    CloseButtonText = "Got it"
                                };
                                _ = await dialog.ShowAsync().ConfigureAwait(true);
                            }
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
                        _ = await TipsDialog.ShowAsync().ConfigureAwait(true);
                    }
                }
            }
        }
    }
}
