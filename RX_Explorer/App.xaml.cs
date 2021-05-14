using Microsoft.Toolkit.Uwp.Helpers;
using Microsoft.Toolkit.Uwp.Notifications;
using RX_Explorer.Class;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.ApplicationModel.Core;
using Windows.System;
using Windows.System.Power;
using Windows.UI;
using Windows.UI.Notifications;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace RX_Explorer
{
    sealed partial class App : Application
    {
        bool IsInBackgroundMode;

        public App()
        {
            InitializeComponent();

            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            Suspending += App_Suspending;
            Resuming += App_Resuming;
            UnhandledException += App_UnhandledException;
            EnteredBackground += App_EnteredBackground;
            LeavingBackground += App_LeavingBackground;
            MemoryManager.AppMemoryUsageIncreased += MemoryManager_AppMemoryUsageIncreased;
            MemoryManager.AppMemoryUsageLimitChanging += MemoryManager_AppMemoryUsageLimitChanging;
            PowerManager.EnergySaverStatusChanged += PowerManager_EnergySaverStatusChanged;
            PowerManager.PowerSupplyStatusChanged += PowerManager_PowerSupplyStatusChanged;
        }

        private void PowerManager_PowerSupplyStatusChanged(object sender, object e)
        {
            SendActivateToast();
        }

        private void PowerManager_EnergySaverStatusChanged(object sender, object e)
        {
            SendActivateToast();
        }

        private void SendActivateToast()
        {
            if (IsInBackgroundMode
                && (FullTrustProcessController.IsAnyActionExcutingInAllControllers
                    || GeneralTransformer.IsAnyTransformTaskRunning
                    || QueueTaskController.IsAnyTaskRunningInController))
            {
                try
                {
                    ToastNotificationManager.History.Remove("EnterBackgroundTips");

                    if (PowerManager.EnergySaverStatus == EnergySaverStatus.On)
                    {
                        ToastContentBuilder Builder = new ToastContentBuilder()
                                                      .SetToastScenario(ToastScenario.Reminder)
                                                      .AddToastActivationInfo("EnterBackgroundTips", ToastActivationType.Foreground)
                                                      .AddText(Globalization.GetString("Toast_EnterBackground_Text_1"))
                                                      .AddText(Globalization.GetString("Toast_EnterBackground_Text_2"))
                                                      .AddText(Globalization.GetString("Toast_EnterBackground_Text_4"))
                                                      .AddButton(new ToastButton(Globalization.GetString("Toast_EnterBackground_ActionButton"), "EnterBackgroundTips"))
                                                      .AddButton(new ToastButtonDismiss(Globalization.GetString("Toast_EnterBackground_Dismiss")));

                        ToastNotificationManager.CreateToastNotifier().Show(new ToastNotification(Builder.GetToastContent().GetXml())
                        {
                            Tag = "EnterBackgroundTips",
                            Priority = ToastNotificationPriority.High
                        });
                    }
                }
                catch (Exception ex)
                {
                    LogTracer.Log(ex, "Toast notification could not be sent");
                }
            }
        }

        private void App_Resuming(object sender, object e)
        {
            AppInstanceIdContainer.RegisterId(AppInstanceIdContainer.CurrentId);
        }

        private void App_Suspending(object sender, SuspendingEventArgs e)
        {
            SQLite.Current.Dispose();
            AppInstanceIdContainer.UngisterId(AppInstanceIdContainer.CurrentId);
        }

        private void CurrentDomain_UnhandledException(object sender, System.UnhandledExceptionEventArgs e)
        {
            SQLite.Current.Dispose();

            if (!e.IsTerminating && e.ExceptionObject is Exception ex)
            {
                LogTracer.LeadToBlueScreen(ex);
            }
        }

        private void MemoryManager_AppMemoryUsageLimitChanging(object sender, AppMemoryUsageLimitChangingEventArgs e)
        {
            if (IsInBackgroundMode)
            {
                if (MemoryManager.AppMemoryUsage >= e.NewLimit)
                {
                    ReduceMemoryUsage();
                }
            }
        }

        private void MemoryManager_AppMemoryUsageIncreased(object sender, object e)
        {
            if (IsInBackgroundMode)
            {
                AppMemoryUsageLevel level = MemoryManager.AppMemoryUsageLevel;

                if (level == AppMemoryUsageLevel.OverLimit || level == AppMemoryUsageLevel.High)
                {
                    ReduceMemoryUsage();
                }
            }
        }

        private void ReduceMemoryUsage()
        {
            SQLite.Current.Dispose();
            GC.Collect();
        }

        private void App_LeavingBackground(object sender, LeavingBackgroundEventArgs e)
        {
            IsInBackgroundMode = false;
        }

        private void App_EnteredBackground(object sender, EnteredBackgroundEventArgs e)
        {
            IsInBackgroundMode = true;
        }

        private void App_UnhandledException(object sender, Windows.UI.Xaml.UnhandledExceptionEventArgs e)
        {
            SQLite.Current.Dispose();

            LogTracer.LeadToBlueScreen(e.Exception);
            e.Handled = true;
        }

        protected override async void OnLaunched(LaunchActivatedEventArgs e)
        {
            SystemInformation.Instance.TrackAppUse(e);

            ApplicationViewTitleBar TitleBar = ApplicationView.GetForCurrentView().TitleBar;
            TitleBar.ButtonBackgroundColor = Colors.Transparent;
            TitleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
            TitleBar.ButtonForegroundColor = AppThemeController.Current.Theme == ElementTheme.Dark ? Colors.White : "#1E1E1E".ToColor();

            CoreApplication.GetCurrentView().TitleBar.ExtendViewIntoTitleBar = true;

            if (Window.Current.Content is Frame frame)
            {
                if (frame.Content is MainPage Main && Main.Nav.Content is TabViewContainer TabContainer)
                {
                    if (!string.IsNullOrWhiteSpace(e.Arguments) && await FileSystemStorageItemBase.CheckExistAsync(e.Arguments))
                    {
                        await TabContainer.CreateNewTabAsync(e.Arguments);
                    }
                    else
                    {
                        await TabContainer.CreateNewTabAsync();
                    }
                }
            }
            else
            {
                if (!string.IsNullOrWhiteSpace(e.Arguments) && await FileSystemStorageItemBase.CheckExistAsync(e.Arguments))
                {
                    ExtendedSplash extendedSplash = new ExtendedSplash(e.SplashScreen, new List<string[]> { new string[] { e.Arguments } });
                    Window.Current.Content = extendedSplash;
                }
                else
                {
                    StartupMode Mode = StartupModeController.GetStartupMode();

                    switch (Mode)
                    {
                        case StartupMode.CreateNewTab:
                            {
                                ExtendedSplash extendedSplash = new ExtendedSplash(e.SplashScreen);
                                Window.Current.Content = extendedSplash;
                                break;
                            }
                        case StartupMode.LastOpenedTab:
                            {
                                List<string[]> LastOpenedPathArray = await StartupModeController.GetAllPathAsync(Mode).ToListAsync();

                                StartupModeController.Clear(StartupMode.LastOpenedTab);

                                if (LastOpenedPathArray.Count == 0)
                                {
                                    ExtendedSplash extendedSplash = new ExtendedSplash(e.SplashScreen);
                                    Window.Current.Content = extendedSplash;
                                }
                                else
                                {
                                    ExtendedSplash extendedSplash = new ExtendedSplash(e.SplashScreen, LastOpenedPathArray);
                                    Window.Current.Content = extendedSplash;
                                }

                                break;
                            }
                        case StartupMode.SpecificTab:
                            {
                                string[] SpecificPathArray = await StartupModeController.GetAllPathAsync(Mode).Select((Item) => Item.FirstOrDefault()).OfType<string>().ToArrayAsync();

                                if (SpecificPathArray.Length == 0)
                                {
                                    ExtendedSplash extendedSplash = new ExtendedSplash(e.SplashScreen);
                                    Window.Current.Content = extendedSplash;
                                }
                                else
                                {
                                    ExtendedSplash extendedSplash = new ExtendedSplash(e.SplashScreen, SpecificPathArray);
                                    Window.Current.Content = extendedSplash;
                                }

                                break;
                            }
                    }
                }
            }

            Window.Current.Activate();
        }

        protected override async void OnActivated(IActivatedEventArgs args)
        {
            ApplicationViewTitleBar TitleBar = ApplicationView.GetForCurrentView().TitleBar;
            TitleBar.ButtonBackgroundColor = Colors.Transparent;
            TitleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
            TitleBar.ButtonForegroundColor = AppThemeController.Current.Theme == ElementTheme.Dark ? Colors.White : "#1E1E1E".ToColor();

            CoreApplication.GetCurrentView().TitleBar.ExtendViewIntoTitleBar = true;

            switch (args)
            {
                case CommandLineActivatedEventArgs CmdArgs:
                    {
                        string[] Arguments = CmdArgs.Operation.Arguments.Split(" ", StringSplitOptions.RemoveEmptyEntries);

                        if (Window.Current.Content is Frame frame)
                        {
                            if (frame.Content is MainPage Main && Main.Nav.Content is TabViewContainer TabContainer)
                            {
                                if (Arguments.Length > 1)
                                {
                                    string Path = string.Join(" ", Arguments.Skip(1));

                                    if (string.IsNullOrWhiteSpace(Path) || Regex.IsMatch(Path, @"::\{[0-9A-F\-]+\}", RegexOptions.IgnoreCase))
                                    {
                                        await TabContainer.CreateNewTabAsync();
                                    }
                                    else
                                    {
                                        await TabContainer.CreateNewTabAsync(Path == "." ? CmdArgs.Operation.CurrentDirectoryPath : Path);
                                    }
                                }
                                else
                                {
                                    await TabContainer.CreateNewTabAsync();
                                }
                            }
                        }
                        else
                        {
                            string Path = string.Join(" ", Arguments.Skip(1));

                            if (Arguments.Length > 1)
                            {
                                if (string.IsNullOrWhiteSpace(Path) || Regex.IsMatch(Path, @"::\{[0-9A-F\-]+\}", RegexOptions.IgnoreCase))
                                {
                                    ExtendedSplash extendedSplash = new ExtendedSplash(CmdArgs.SplashScreen);
                                    Window.Current.Content = extendedSplash;
                                }
                                else
                                {
                                    ExtendedSplash extendedSplash = new ExtendedSplash(CmdArgs.SplashScreen, new List<string[]> { new string[] { Path == "." ? CmdArgs.Operation.CurrentDirectoryPath : Path } });
                                    Window.Current.Content = extendedSplash;
                                }
                            }
                            else
                            {
                                ExtendedSplash extendedSplash = new ExtendedSplash(CmdArgs.SplashScreen);
                                Window.Current.Content = extendedSplash;
                            }
                        }

                        break;
                    }

                case ProtocolActivatedEventArgs ProtocalArgs:
                    {
                        if (!string.IsNullOrWhiteSpace(ProtocalArgs.Uri.AbsolutePath))
                        {
                            ExtendedSplash extendedSplash = new ExtendedSplash(ProtocalArgs.SplashScreen, new List<string[]> { Uri.UnescapeDataString(ProtocalArgs.Uri.AbsolutePath).Split("||", StringSplitOptions.RemoveEmptyEntries) });
                            Window.Current.Content = extendedSplash;
                        }
                        else
                        {
                            ExtendedSplash extendedSplash = new ExtendedSplash(ProtocalArgs.SplashScreen);
                            Window.Current.Content = extendedSplash;
                        }

                        break;
                    }

                case not ToastNotificationActivatedEventArgs:
                    {
                        ExtendedSplash extendedSplash = new ExtendedSplash(args.SplashScreen);
                        Window.Current.Content = extendedSplash;
                        break;
                    }
            }

            Window.Current.Activate();
        }

        protected override async void OnFileActivated(FileActivatedEventArgs args)
        {
            if (args.Verb == "USBArrival")
            {
                ApplicationViewTitleBar TitleBar = ApplicationView.GetForCurrentView().TitleBar;
                TitleBar.ButtonBackgroundColor = Colors.Transparent;
                TitleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
                TitleBar.ButtonForegroundColor = AppThemeController.Current.Theme == ElementTheme.Dark ? Colors.White : "#1E1E1E".ToColor();

                CoreApplication.GetCurrentView().TitleBar.ExtendViewIntoTitleBar = true;

                if (Window.Current.Content is Frame mainPageFrame)
                {
                    if (mainPageFrame.Content is MainPage mainPage && mainPage.Nav.Content is TabViewContainer Container)
                    {
                        await Container.CreateNewTabAsync(args.Files.Select((File) => File.Path).ToArray());
                    }
                }
                else
                {
                    ExtendedSplash extendedSplash = new ExtendedSplash(args.SplashScreen, new List<string[]> { args.Files.Select((File) => File.Path).ToArray() });
                    Window.Current.Content = extendedSplash;
                }

                Window.Current.Activate();
            }
        }
    }
}
