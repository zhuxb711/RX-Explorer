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
        private Rect SplashImageRect;

        private readonly SplashScreen Splash;

        private string ActivateParameter;

        public ExtendedSplash(SplashScreen Screen, bool IsPreLaunch = false, string ActivateParameter = null)
        {
            InitializeComponent();
            Window.Current.SetTitleBar(TitleBar);

            if (Package.Current.IsDevelopmentMode)
            {
                AppName.Text += " (Development Mode)";
            }

            Splash = Screen ?? throw new ArgumentNullException(nameof(Screen), "Parameter could not be null");
            SplashImageRect = Screen.ImageLocation;

            SetControlPosition();

            if (IsPreLaunch)
            {
                PreLaunchInitialize();
            }
            else
            {
                Splash.Dismissed += Screen_Dismissed;
            }

            Loaded += ExtendedSplash_Loaded;
            Unloaded += ExtendedSplash_Unloaded;

            if (!string.IsNullOrEmpty(ActivateParameter))
            {
                this.ActivateParameter = ActivateParameter;
            }
        }

        private void ExtendedSplash_Unloaded(object sender, RoutedEventArgs e)
        {
            Window.Current.SizeChanged -= Current_SizeChanged;
        }

        private void ExtendedSplash_Loaded(object sender, RoutedEventArgs e)
        {
            Window.Current.SizeChanged += Current_SizeChanged;
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

        private async Task DismissExtendedSplashAsync()
        {
            try
            {
                await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    Frame rootFrame = new Frame();
                    Window.Current.Content = rootFrame;

                    if (string.IsNullOrEmpty(ActivateParameter))
                    {
                        rootFrame.Navigate(typeof(MainPage), SplashImageRect);
                    }
                    else
                    {
                        rootFrame.Navigate(typeof(MainPage), new Tuple<string, Rect>(ActivateParameter, SplashImageRect));
                    }
                });
            }
            catch (Exception ex)
            {
                LogTracer.RequestBlueScreen(ex);
            }
        }

        private void DismissExtendedSplash()
        {
            try
            {
                Frame rootFrame = new Frame();
                Window.Current.Content = rootFrame;
                rootFrame.Navigate(typeof(MainPage), SplashImageRect);
            }
            catch (Exception ex)
            {
                LogTracer.RequestBlueScreen(ex);
            }
        }

        private void PreLaunchInitialize()
        {
            bool IsFileAccessible = CheckFileAccessAuthority().Result;

            if (IsFileAccessible)
            {
                DismissExtendedSplash();
            }
            else
            {
                Window.Current.Content = this;

                Display.Text = Globalization.GetString("ExtendedSplash_Access_Tips");
                ButtonPane.Visibility = Visibility.Visible;
            }
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

                bool IsFileAccessible = await CheckFileAccessAuthority().ConfigureAwait(false);

                if (IsFileAccessible)
                {
                    await DismissExtendedSplashAsync().ConfigureAwait(false);
                }
                else
                {
                    await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                    {
                        Display.Text = Globalization.GetString("ExtendedSplash_Access_Tips");
                        ButtonPane.Visibility = Visibility.Visible;
                    });
                }
            }
            catch (Exception ex)
            {
                LogTracer.RequestBlueScreen(ex);
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

        private Task<bool> CheckFileAccessAuthority()
        {
            return Task.Run(() =>
            {
                try
                {
                    _ = StorageFolder.GetFolderFromPathAsync(Environment.GetLogicalDrives().FirstOrDefault()).AsTask().Result;
                    return true;
                }
                catch
                {
                    return false;
                }
            });
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
                                          .AddText(Globalization.GetString("Toast_BroadFileSystemAccess_Text_3"))
                                          .AddButton(Globalization.GetString("Toast_BroadFileSystemAccess_ActionButton_1"), ToastActivationType.Foreground, "Restart")
                                          .AddButton(new ToastButtonDismiss());

            ToastNotificationManager.CreateToastNotifier().Show(new ToastNotification(Builder.GetToastContent().GetXml()));

            await ApplicationView.GetForCurrentView().TryConsolidateAsync();
        }
    }
}
