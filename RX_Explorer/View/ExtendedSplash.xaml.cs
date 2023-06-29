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

        private readonly IReadOnlyList<string[]> Parameter;

        public ExtendedSplash(SplashScreen Screen, IReadOnlyList<string[]> Parameter) : this(Screen)
        {
            this.Parameter = Parameter;
        }

        public ExtendedSplash(SplashScreen Screen, string[] Parameter) : this(Screen)
        {
            this.Parameter = Parameter.Select((Item) => new string[] { Item }).ToArray();
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
                    Canvas.SetTop(PermissionArea, VerticalLocation + 15);
                    Canvas.SetLeft(PermissionArea, HorizonLocation - (PermissionArea.ActualWidth * 0.5));
                }

                if (LoadingArea.Visibility == Visibility.Visible)
                {
                    Canvas.SetTop(LoadingArea, VerticalLocation + 15);
                    Canvas.SetLeft(LoadingArea, HorizonLocation - (LoadingArea.ActualWidth * 0.5));
                }

                Logo.Height = Splash.ImageLocation.Height;
                Logo.Width = Splash.ImageLocation.Width;

                Canvas.SetTop(Logo, Splash.ImageLocation.Y);
                Canvas.SetLeft(Logo, Splash.ImageLocation.X);
            }
        }

        private async Task DismissExtendedSplashAsync()
        {
#if !DEBUG
            try
            {
                Microsoft.AppCenter.AppCenter.Start("<RX-Explorer-AppCenter-Secret-Value>", typeof(Microsoft.AppCenter.Crashes.Crashes));

                if (await Microsoft.AppCenter.AppCenter.IsEnabledAsync())
                {
                    LogTracer.Log("AppCenter is initialized successfully and was enabled");
                }
                else
                {
                    LogTracer.Log("AppCenter is initialized successfully and was disabled");
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, "Could not start the app center component");
            }
#endif

            try
            {
                await Dispatcher.RunAndWaitAsyncTask(CoreDispatcherPriority.Normal, async () =>
                {
                    Task TrustProcessInitializeTask = Task.WhenAll(AuxiliaryTrustProcessController.InitializeAsync(), MonitorTrustProcessController.InitializeAsync());

                    if (await Task.WhenAny(TrustProcessInitializeTask, Task.Delay(3000)) != TrustProcessInitializeTask)
                    {
                        LoadingText.Text = Globalization.GetString("ExtendedSplashLoadingFullTrustText");
                        LoadingArea.Visibility = Visibility.Visible;
                        LoadingArea.UpdateLayout();
                        SetControlsPosition();

                        await TrustProcessInitializeTask;

                        LoadingArea.Visibility = Visibility.Collapsed;
                    }

                    Task BackgroundInitializeTask = BackgroundController.Current.InitializeAsync();

                    if (BackgroundController.Current.CurrentType == BackgroundBrushType.BingPicture)
                    {
                        if (await Task.WhenAny(BackgroundInitializeTask, Task.Delay(2000)) != BackgroundInitializeTask)
                        {
                            LoadingText.Text = Globalization.GetString("ExtendedSplashLoadingBingText");
                            LoadingArea.Visibility = Visibility.Visible;
                            LoadingArea.UpdateLayout();
                            SetControlsPosition();

                            await BackgroundInitializeTask;

                            LoadingArea.Visibility = Visibility.Collapsed;
                        }
                    }
                    else
                    {
                        await BackgroundInitializeTask;
                    }

                    Window.Current.Content = new Frame
                    {
                        Content = (Parameter?.Any()).GetValueOrDefault() ? new MainPage(Splash.ImageLocation, Parameter) : new MainPage(Splash.ImageLocation)
                    };
                });
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"An exception was threw in {nameof(DismissExtendedSplashAsync)}");
            }
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
                LogTracer.Log(ex, "An exception was threw when dismissing extendedsplash ");
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
            if (!await ApplicationView.GetForCurrentView().TryConsolidateAsync())
            {
                Application.Current.Exit();
            }
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
                if (!await ApplicationView.GetForCurrentView().TryConsolidateAsync())
                {
                    Application.Current.Exit();
                }
            }
        }

        private void Current_SizeChanged(object sender, WindowSizeChangedEventArgs e)
        {
            SetControlsPosition();
        }
    }
}
