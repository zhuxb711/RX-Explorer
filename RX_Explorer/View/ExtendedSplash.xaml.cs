using Microsoft.Toolkit.Uwp.Notifications;
using RX_Explorer.Class;
using System;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.ApplicationModel.Core;
using Windows.Foundation;
using Windows.Storage;
using Windows.System;
using Windows.UI.Core;
using Windows.UI.Notifications;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace RX_Explorer
{
    public sealed partial class ExtendedSplash : Page
    {
        private readonly SplashScreen Splash;

        private string[] Parameter;

        public ExtendedSplash(SplashScreen Screen, string[] Parameter = null)
        {
            InitializeComponent();

            Window.Current.SetTitleBar(TitleBar);

            if (Package.Current.IsDevelopmentMode)
            {
                AppName.Text += " (Development Mode)";
            }

            Splash = Screen ?? throw new ArgumentNullException(nameof(Screen), "Parameter could not be null");

            Splash.Dismissed += Screen_Dismissed;

            Loaded += ExtendedSplash_Loaded;
            Unloaded += ExtendedSplash_Unloaded;

            if (Parameter != null)
            {
                this.Parameter = Parameter;
            }
        }

        private void ExtendedSplash_Unloaded(object sender, RoutedEventArgs e)
        {
            Window.Current.SizeChanged -= Current_SizeChanged;
        }

        private void ExtendedSplash_Loaded(object sender, RoutedEventArgs e)
        {
            SetControlPosition();

            Window.Current.SizeChanged += Current_SizeChanged;
        }

        private void SetControlPosition()
        {
            double HorizonLocation = Splash.ImageLocation.X + (Splash.ImageLocation.Width * 0.5);
            double VerticalLocation = Splash.ImageLocation.Y + (Splash.ImageLocation.Height * 0.7);

            PermissionArea.SetValue(Canvas.LeftProperty, HorizonLocation - (PermissionArea.Width * 0.5));
            PermissionArea.SetValue(Canvas.TopProperty, VerticalLocation + 15);

            LoadingBingArea.SetValue(Canvas.LeftProperty, HorizonLocation - (LoadingBingArea.Width * 0.5));
            LoadingBingArea.SetValue(Canvas.TopProperty, VerticalLocation + 15);

            extendedSplashImage.SetValue(Canvas.LeftProperty, Splash.ImageLocation.X);
            extendedSplashImage.SetValue(Canvas.TopProperty, Splash.ImageLocation.Y);

            extendedSplashImage.Height = Splash.ImageLocation.Height;
            extendedSplashImage.Width = Splash.ImageLocation.Width;
        }

        private async Task DismissExtendedSplashAsync()
        {
            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
            {
                try
                {
                    if (BackgroundController.Current.CurrentType == BackgroundBrushType.BingPicture && await BingPictureDownloader.CheckIfNeedToUpdate().ConfigureAwait(true))
                    {
                        LoadingBingArea.Visibility = Visibility.Visible;
                    }

                    await BackgroundController.Current.Initialize().ConfigureAwait(true);

                    Frame RootFrame = new Frame();
                    Window.Current.Content = RootFrame;

                    if (Parameter == null)
                    {
                        MainPage Main = new MainPage(new Tuple<Rect, string[]>(Splash.ImageLocation, Array.Empty<string>()));
                        RootFrame.Content = Main;
                    }
                    else
                    {
                        MainPage Main = new MainPage(new Tuple<Rect, string[]>(Splash.ImageLocation, Parameter));
                        RootFrame.Content = Main;
                    }
                }
                catch (Exception ex)
                {
                    LogTracer.Log(ex, "An error was threw when dismissing extendedsplash ");
                }
            });
        }

        private async void Screen_Dismissed(SplashScreen sender, object args)
        {
            try
            {
                if (!ApplicationData.Current.LocalSettings.Values.ContainsKey("QuickStartInitialFinished1"))
                {
                    await SQLite.Current.ClearTableAsync("QuickStart").ConfigureAwait(false);

                    await SQLite.Current.SetQuickStartItemAsync(Globalization.GetString("ExtendedSplash_QuickStartItem_Name_1"), "ms-appx:///QuickStartImage/MicrosoftStore.png", "ms-windows-store://home", QuickStartType.Application).ConfigureAwait(false);
                    await SQLite.Current.SetQuickStartItemAsync(Globalization.GetString("ExtendedSplash_QuickStartItem_Name_2"), "ms-appx:///QuickStartImage/Calculator.png", "calculator:", QuickStartType.Application).ConfigureAwait(false);
                    await SQLite.Current.SetQuickStartItemAsync(Globalization.GetString("ExtendedSplash_QuickStartItem_Name_3"), "ms-appx:///QuickStartImage/Setting.png", "ms-settings:", QuickStartType.Application).ConfigureAwait(false);
                    await SQLite.Current.SetQuickStartItemAsync(Globalization.GetString("ExtendedSplash_QuickStartItem_Name_4"), "ms-appx:///QuickStartImage/Email.png", "mailto:", QuickStartType.Application).ConfigureAwait(false);
                    await SQLite.Current.SetQuickStartItemAsync(Globalization.GetString("ExtendedSplash_QuickStartItem_Name_5"), "ms-appx:///QuickStartImage/Calendar.png", "outlookcal:", QuickStartType.Application).ConfigureAwait(false);
                    await SQLite.Current.SetQuickStartItemAsync(Globalization.GetString("ExtendedSplash_QuickStartItem_Name_6"), "ms-appx:///QuickStartImage/Photos.png", "ms-photos:", QuickStartType.Application).ConfigureAwait(false);
                    await SQLite.Current.SetQuickStartItemAsync(Globalization.GetString("ExtendedSplash_QuickStartItem_Name_7"), "ms-appx:///QuickStartImage/Weather.png", "msnweather:", QuickStartType.Application).ConfigureAwait(false);
                    await SQLite.Current.SetQuickStartItemAsync(Globalization.GetString("ExtendedSplash_QuickStartItem_Name_9"), "ms-appx:///HotWebImage/Facebook.png", "https://www.facebook.com/", QuickStartType.WebSite).ConfigureAwait(false);
                    await SQLite.Current.SetQuickStartItemAsync(Globalization.GetString("ExtendedSplash_QuickStartItem_Name_10"), "ms-appx:///HotWebImage/Instagram.png", "https://www.instagram.com/", QuickStartType.WebSite).ConfigureAwait(false);
                    await SQLite.Current.SetQuickStartItemAsync(Globalization.GetString("ExtendedSplash_QuickStartItem_Name_11"), "ms-appx:///HotWebImage/Twitter.png", "https://twitter.com", QuickStartType.WebSite).ConfigureAwait(false);

                    ApplicationData.Current.LocalSettings.Values["QuickStartInitialFinished1"] = true;
                }

                await SQLite.Current.UpdateQuickStartItemAsync("ms-appx:///QuickStartImage/MicrosoftStore.png", Globalization.GetString("ExtendedSplash_QuickStartItem_Name_1"), QuickStartType.Application).ConfigureAwait(false);
                await SQLite.Current.UpdateQuickStartItemAsync("ms-appx:///QuickStartImage/Calculator.png", Globalization.GetString("ExtendedSplash_QuickStartItem_Name_2"), QuickStartType.Application).ConfigureAwait(false);
                await SQLite.Current.UpdateQuickStartItemAsync("ms-appx:///QuickStartImage/Setting.png", Globalization.GetString("ExtendedSplash_QuickStartItem_Name_3"), QuickStartType.Application).ConfigureAwait(false);
                await SQLite.Current.UpdateQuickStartItemAsync("ms-appx:///QuickStartImage/Email.png", Globalization.GetString("ExtendedSplash_QuickStartItem_Name_4"), QuickStartType.Application).ConfigureAwait(false);
                await SQLite.Current.UpdateQuickStartItemAsync("ms-appx:///QuickStartImage/Calendar.png", Globalization.GetString("ExtendedSplash_QuickStartItem_Name_5"), QuickStartType.Application).ConfigureAwait(false);
                await SQLite.Current.UpdateQuickStartItemAsync("ms-appx:///QuickStartImage/Photos.png", Globalization.GetString("ExtendedSplash_QuickStartItem_Name_6"), QuickStartType.Application).ConfigureAwait(false);
                await SQLite.Current.UpdateQuickStartItemAsync("ms-appx:///QuickStartImage/Weather.png", Globalization.GetString("ExtendedSplash_QuickStartItem_Name_7"), QuickStartType.Application).ConfigureAwait(false);
                await SQLite.Current.UpdateQuickStartItemAsync("ms-appx:///HotWebImage/Facebook.png", Globalization.GetString("ExtendedSplash_QuickStartItem_Name_9"), QuickStartType.WebSite).ConfigureAwait(false);
                await SQLite.Current.UpdateQuickStartItemAsync("ms-appx:///HotWebImage/Instagram.png", Globalization.GetString("ExtendedSplash_QuickStartItem_Name_10"), QuickStartType.WebSite).ConfigureAwait(false);
                await SQLite.Current.UpdateQuickStartItemAsync("ms-appx:///HotWebImage/Twitter.png", Globalization.GetString("ExtendedSplash_QuickStartItem_Name_11"), QuickStartType.WebSite).ConfigureAwait(false);

                bool IsFileAccessible = await CheckAccessAuthority().ConfigureAwait(false);

                if (IsFileAccessible)
                {
                    await DismissExtendedSplashAsync().ConfigureAwait(false);
                }
                else
                {
                    await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                    {
                        Display.Text = Globalization.GetString("ExtendedSplash_Access_Tips");
                        PermissionArea.Visibility = Visibility.Visible;
                        LoadingBingArea.Visibility = Visibility.Collapsed;
                    });
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, "An error was threw when dismissing extendedsplash ");
            }
        }

        private async Task<bool> CheckAccessAuthority()
        {
            try
            {
                await StorageFolder.GetFolderFromPathAsync(Environment.GetLogicalDrives().FirstOrDefault());
                return true;
            }
            catch
            {
                return false;
            }
        }

        private async void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            await ApplicationView.GetForCurrentView().TryConsolidateAsync();
        }

        private async void NavigationButton_Click(object sender, RoutedEventArgs e)
        {
            await Launcher.LaunchUriAsync(new Uri("ms-settings:privacy-broadfilesystemaccess"));

            ToastContentBuilder Builder = new ToastContentBuilder()
                                          .SetToastScenario(ToastScenario.Reminder)
                                          .AddText(Globalization.GetString("Toast_BroadFileSystemAccess_Text_1"))
                                          .AddText(Globalization.GetString("Toast_BroadFileSystemAccess_Text_2"))
                                          .AddButton(Globalization.GetString("Toast_BroadFileSystemAccess_ActionButton_1"), ToastActivationType.Foreground, "Restart")
                                          .AddButton(new ToastButtonDismiss(Globalization.GetString("Toast_BroadFileSystemAccess_ActionButton_2")));

            ToastNotificationManager.CreateToastNotifier().Show(new ToastNotification(Builder.GetToastContent().GetXml()));

            await ApplicationView.GetForCurrentView().TryConsolidateAsync();
        }

        private void Current_SizeChanged(object sender, WindowSizeChangedEventArgs e)
        {
            if (Splash != null)
            {
                SetControlPosition();
            }
        }
    }
}
