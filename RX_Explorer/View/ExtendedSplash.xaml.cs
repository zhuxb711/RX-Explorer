using Microsoft.Toolkit.Uwp.Notifications;
using RX_Explorer.Class;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel.Activation;
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
        private SplashScreen Splash;

        private List<string[]> Parameter;

        public ExtendedSplash(SplashScreen Screen, List<string[]> Parameter)
        {
            this.Parameter = Parameter;
            Initialize(Screen);
        }

        public ExtendedSplash(SplashScreen Screen, string[] Parameter)
        {
            this.Parameter = Parameter.Select((Item) => new string[] { Item }).ToList();
            Initialize(Screen);
        }

        public ExtendedSplash(SplashScreen Screen)
        {
            Initialize(Screen);
        }

        private void Initialize(SplashScreen Screen)
        {
            InitializeComponent();

            Window.Current.SetTitleBar(TitleBar);

#if DEBUG
            AppName.Text += $" ({Globalization.GetString("Development_Version")})";
#endif

            Splash = Screen ?? throw new ArgumentNullException(nameof(Screen), "Parameter could not be null");

            Splash.Dismissed += Screen_Dismissed;

            Loaded += ExtendedSplash_Loaded;
            Unloaded += ExtendedSplash_Unloaded;
        }

        private void ExtendedSplash_Unloaded(object sender, RoutedEventArgs e)
        {
            Window.Current.SizeChanged -= Current_SizeChanged;
        }

        private void ExtendedSplash_Loaded(object sender, RoutedEventArgs e)
        {
            SetControlsPosition();
            Window.Current.SizeChanged += Current_SizeChanged;
        }

        private void SetControlsPosition()
        {
            double HorizonLocation = Splash.ImageLocation.X + (Splash.ImageLocation.Width * 0.5);
            double VerticalLocation = Splash.ImageLocation.Y + (Splash.ImageLocation.Height * 0.7);

            PermissionArea.SetValue(Canvas.LeftProperty, HorizonLocation - (PermissionArea.ActualWidth * 0.5));
            PermissionArea.SetValue(Canvas.TopProperty, VerticalLocation + 15);

            LoadingBingArea.SetValue(Canvas.LeftProperty, HorizonLocation - (LoadingBingArea.ActualWidth * 0.5));
            LoadingBingArea.SetValue(Canvas.TopProperty, VerticalLocation + 15);

            extendedSplashImage.SetValue(Canvas.LeftProperty, Splash.ImageLocation.X);
            extendedSplashImage.SetValue(Canvas.TopProperty, Splash.ImageLocation.Y);

            extendedSplashImage.Height = Splash.ImageLocation.Height;
            extendedSplashImage.Width = Splash.ImageLocation.Width;
        }

        private async Task DismissExtendedSplashAsync()
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
            {
                try
                {
                    if (BackgroundController.Current.CurrentType == BackgroundBrushType.BingPicture)
                    {
                        LoadingBingArea.Visibility = Visibility.Visible;
                        LoadingBingArea.UpdateLayout();
                        SetControlsPosition();
                    }

                    await BackgroundController.Current.InitializeAsync();

                    Frame RootFrame = new Frame();

                    if ((Parameter?.Count).GetValueOrDefault() == 0)
                    {
                        RootFrame.Content = new MainPage(Splash.ImageLocation);
                    }
                    else
                    {
                        RootFrame.Content = new MainPage(Splash.ImageLocation, Parameter);
                    }

                    Window.Current.Content = RootFrame;
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
                bool IsFileAccessible = await CheckAccessAuthorityAsync();

                if (IsFileAccessible)
                {
                    await DismissExtendedSplashAsync();
                }
                else
                {
                    await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                    {
                        Display.Text = Globalization.GetString("ExtendedSplash_Access_Tips");
                        PermissionArea.Visibility = Visibility.Visible;
                        LoadingBingArea.Visibility = Visibility.Collapsed;
                        PermissionArea.UpdateLayout();
                        LoadingBingArea.UpdateLayout();
                        SetControlsPosition();
                    });
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, "An error was threw when dismissing extendedsplash ");
            }
        }

        private async Task<bool> CheckAccessAuthorityAsync()
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
            try
            {
                await Launcher.LaunchUriAsync(new Uri("ms-settings:privacy-broadfilesystemaccess"));

                ToastContentBuilder Builder = new ToastContentBuilder()
                                          .SetToastScenario(ToastScenario.Reminder)
                                          .AddText(Globalization.GetString("Toast_BroadFileSystemAccess_Text_1"))
                                          .AddText(Globalization.GetString("Toast_BroadFileSystemAccess_Text_2"))
                                          .AddButton(Globalization.GetString("Toast_BroadFileSystemAccess_ActionButton_1"), ToastActivationType.Foreground, "Restart")
                                          .AddButton(new ToastButtonDismiss(Globalization.GetString("Toast_BroadFileSystemAccess_ActionButton_2")));

                ToastNotificationManager.CreateToastNotifier().Show(new ToastNotification(Builder.GetToastContent().GetXml()));
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, "Toast notification could not be sent");
            }
            finally
            {
                await ApplicationView.GetForCurrentView().TryConsolidateAsync();
            }
        }

        private void Current_SizeChanged(object sender, WindowSizeChangedEventArgs e)
        {
            if (Splash != null)
            {
                SetControlsPosition();
            }
        }
    }
}
