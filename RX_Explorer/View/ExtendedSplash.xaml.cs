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

namespace RX_Explorer.View
{
    public sealed partial class ExtendedSplash : Page
    {
        private readonly SplashScreen Splash;

        private readonly List<string[]> Parameter;

        public ExtendedSplash(SplashScreen Screen, List<string[]> Parameter) : this(Screen)
        {
            this.Parameter = Parameter;
        }

        public ExtendedSplash(SplashScreen Screen, string[] Parameter) : this(Screen)
        {
            this.Parameter = Parameter.Select((Item) => new string[] { Item }).ToList();
        }

        public ExtendedSplash(SplashScreen Screen)
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
            if (Splash != null)
            {
                double HorizonLocation = Splash.ImageLocation.X + (Splash.ImageLocation.Width * 0.5);
                double VerticalLocation = Splash.ImageLocation.Y + (Splash.ImageLocation.Height * 0.7);

                if (PermissionArea.Visibility == Visibility.Visible)
                {
                    PermissionArea.SetValue(Canvas.LeftProperty, HorizonLocation - (PermissionArea.ActualWidth * 0.5));
                    PermissionArea.SetValue(Canvas.TopProperty, VerticalLocation + 15);
                }

                if (LoadingArea.Visibility == Visibility.Visible)
                {
                    LoadingArea.SetValue(Canvas.LeftProperty, HorizonLocation - (LoadingArea.ActualWidth * 0.5));
                    LoadingArea.SetValue(Canvas.TopProperty, VerticalLocation + 15);
                }

                Logo.SetValue(Canvas.LeftProperty, Splash.ImageLocation.X);
                Logo.SetValue(Canvas.TopProperty, Splash.ImageLocation.Y);

                Logo.Height = Splash.ImageLocation.Height;
                Logo.Width = Splash.ImageLocation.Width;
            }
        }

        private async Task DismissExtendedSplashAsync()
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
            {
                try
                {
                    Task BackgroundInitializeTask = BackgroundController.Current.InitializeAsync();
                    Task FullTrustProcessInitializeTask = FullTrustProcessController.InitializeAsync();

                    if (await Task.WhenAny(FullTrustProcessInitializeTask, Task.Delay(2000)) != FullTrustProcessInitializeTask)
                    {
                        LoadingText.Text = Globalization.GetString("ExtendedSplashLoadingFullTrustText");
                        LoadingArea.Visibility = Visibility.Visible;
                        LoadingArea.UpdateLayout();
                        SetControlsPosition();

                        await FullTrustProcessInitializeTask;

                        LoadingArea.Visibility = Visibility.Collapsed;
                    }

                    if (await Task.WhenAny(BackgroundInitializeTask, Task.Delay(1000)) != BackgroundInitializeTask)
                    {
                        if (BackgroundController.Current.CurrentType == BackgroundBrushType.BingPicture)
                        {
                            LoadingText.Text = Globalization.GetString("ExtendedSplashLoadingBingText");
                            LoadingArea.Visibility = Visibility.Visible;
                            LoadingArea.UpdateLayout();
                            SetControlsPosition();
                        }

                        await BackgroundInitializeTask;

                        LoadingArea.Visibility = Visibility.Collapsed;
                    }

                    Window.Current.Content = new Frame
                    {
                        Content = (Parameter?.Any()).GetValueOrDefault() ? new MainPage(Splash.ImageLocation, Parameter) : new MainPage(Splash.ImageLocation)
                    };
                }
                catch (Exception ex)
                {
                    LogTracer.Log(ex, $"An error was threw in {nameof(DismissExtendedSplashAsync)}");
                }
            });
        }

        private async void Screen_Dismissed(SplashScreen sender, object args)
        {
            try
            {
                if (await CheckAccessAuthorityAsync())
                {
                    await DismissExtendedSplashAsync();
                }
                else
                {
                    await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                    {
                        LoadingArea.Visibility = Visibility.Collapsed;
                        PermissionArea.Visibility = Visibility.Visible;
                        PermissionArea.UpdateLayout();
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
                await StorageFolder.GetFolderFromPathAsync(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }

            return true;
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
            SetControlsPosition();
        }
    }
}
