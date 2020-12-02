using RX_Explorer.Class;
using System;
using System.Linq;
using System.Text.RegularExpressions;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.ApplicationModel.Core;
using Windows.System;
using Windows.UI;
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
        }

        private async void App_Resuming(object sender, object e)
        {
            await FullTrustProcessController.Current.ConnectToFullTrustProcessorAsync().ConfigureAwait(true);
            AppInstanceIdContainer.RegisterCurrentId(AppInstanceIdContainer.CurrentId);
        }

        protected override async void OnWindowCreated(WindowCreatedEventArgs args)
        {
            await LogTracer.Initialize().ConfigureAwait(false);

            if (await FullTrustProcessController.Current.CheckIfQuicklookIsAvaliableAsync().ConfigureAwait(true))
            {
                SettingControl.IsQuicklookAvailable = true;
            }
            else
            {
                SettingControl.IsQuicklookAvailable = false;
            }
        }

        private void App_Suspending(object sender, SuspendingEventArgs e)
        {
            PipeLineController.Current.Dispose();
            FullTrustProcessController.Current.Dispose();
            AppInstanceIdContainer.UngisterCurrentId();
        }

        private void CurrentDomain_UnhandledException(object sender, System.UnhandledExceptionEventArgs e)
        {
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
            MySQL.Current.Dispose();

            if (!FullTrustProcessController.Current.IsNowHasAnyActionExcuting)
            {
                PipeLineController.Current.Dispose();
                FullTrustProcessController.Current.Dispose();
            }

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
            LogTracer.LeadToBlueScreen(e.Exception);
            e.Handled = true;
        }

        protected override async void OnLaunched(LaunchActivatedEventArgs e)
        {
            ApplicationViewTitleBar TitleBar = ApplicationView.GetForCurrentView().TitleBar;
            TitleBar.ButtonBackgroundColor = Colors.Transparent;
            TitleBar.ButtonInactiveBackgroundColor = Colors.Transparent;

            CoreApplication.GetCurrentView().TitleBar.ExtendViewIntoTitleBar = true;

            if (Window.Current.Content is Frame frame)
            {
                if (frame.Content is MainPage Main && Main.Nav.Content is TabViewContainer TabContainer)
                {
                    if (!string.IsNullOrWhiteSpace(e.Arguments) && WIN_Native_API.CheckExist(e.Arguments))
                    {
                        await TabContainer.CreateNewTabAndOpenTargetFolder(e.Arguments).ConfigureAwait(true);
                    }
                    else
                    {
                        await TabContainer.CreateNewTabAndOpenTargetFolder(string.Empty).ConfigureAwait(true);
                    }
                }
            }
            else
            {
                if (!string.IsNullOrWhiteSpace(e.Arguments) && WIN_Native_API.CheckExist(e.Arguments))
                {
                    ExtendedSplash extendedSplash = new ExtendedSplash(e.SplashScreen, $"PathActivate||{e.Arguments}");
                    Window.Current.Content = extendedSplash;
                }
                else 
                {
                    ExtendedSplash extendedSplash = new ExtendedSplash(e.SplashScreen);
                    Window.Current.Content = extendedSplash;
                }
            }

            Window.Current.Activate();
        }

        protected override async void OnActivated(IActivatedEventArgs args)
        {
            ApplicationViewTitleBar TitleBar = ApplicationView.GetForCurrentView().TitleBar;
            TitleBar.ButtonBackgroundColor = Colors.Transparent;
            TitleBar.ButtonInactiveBackgroundColor = Colors.Transparent;

            CoreApplication.GetCurrentView().TitleBar.ExtendViewIntoTitleBar = true;

            if (args is CommandLineActivatedEventArgs CmdArgs)
            {
                string[] Arguments = CmdArgs.Operation.Arguments.Split(" ", StringSplitOptions.RemoveEmptyEntries);

                if (Window.Current.Content is Frame frame)
                {
                    if (frame.Content is MainPage Main && Main.Nav.Content is TabViewContainer TabContainer)
                    {
                        if (Arguments.Length > 1)
                        {
                            string Path = string.Join(" ", Arguments.Skip(1));

                            if (Regex.IsMatch(Path, @"::\{[0-9A-F\-]+\}", RegexOptions.IgnoreCase))
                            {
                                await TabContainer.CreateNewTabAndOpenTargetFolder(string.Empty).ConfigureAwait(true);
                            }
                            else
                            {
                                await TabContainer.CreateNewTabAndOpenTargetFolder(Path).ConfigureAwait(true);
                            }
                        }
                        else
                        {
                            await TabContainer.CreateNewTabAndOpenTargetFolder(string.Empty).ConfigureAwait(true);
                        }
                    }
                }
                else
                {
                    string Path = string.Join(" ", Arguments.Skip(1));

                    if (Arguments.Length > 1 && !Regex.IsMatch(Path, @"::\{[0-9A-F\-]+\}", RegexOptions.IgnoreCase))
                    {
                        ExtendedSplash extendedSplash = new ExtendedSplash(CmdArgs.SplashScreen, $"PathActivate||{Path}");
                        Window.Current.Content = extendedSplash;
                    }
                    else
                    {
                        ExtendedSplash extendedSplash = new ExtendedSplash(CmdArgs.SplashScreen);
                        Window.Current.Content = extendedSplash;
                    }
                }
            }
            else if (args is ProtocolActivatedEventArgs ProtocalArgs)
            {
                if (!string.IsNullOrWhiteSpace(ProtocalArgs.Uri.LocalPath))
                {
                    ExtendedSplash extendedSplash = new ExtendedSplash(ProtocalArgs.SplashScreen, $"PathActivate||{ProtocalArgs.Uri.LocalPath}");
                    Window.Current.Content = extendedSplash;
                }
                else
                {
                    ExtendedSplash extendedSplash = new ExtendedSplash(ProtocalArgs.SplashScreen);
                    Window.Current.Content = extendedSplash;
                }
            }
            else if(args is not ToastNotificationActivatedEventArgs)
            {
                ExtendedSplash extendedSplash = new ExtendedSplash(args.SplashScreen);
                Window.Current.Content = extendedSplash;
            }

            Window.Current.Activate();
        }

        protected override void OnFileActivated(FileActivatedEventArgs args)
        {
            if (args.Verb == "USBArrival")
            {
                ApplicationViewTitleBar TitleBar = ApplicationView.GetForCurrentView().TitleBar;
                TitleBar.ButtonBackgroundColor = Colors.Transparent;
                TitleBar.ButtonInactiveBackgroundColor = Colors.Transparent;

                CoreApplication.GetCurrentView().TitleBar.ExtendViewIntoTitleBar = true;

                if (Window.Current.Content is Frame mainPageFrame)
                {
                    if (mainPageFrame.Content is MainPage mainPage && mainPage.Nav.Content is TabViewContainer tabViewContainer)
                    {
                        _ = tabViewContainer.CreateNewTabAndOpenTargetFolder(args.Files[0].Path);
                    }
                }
                else
                {
                    ExtendedSplash extendedSplash = new ExtendedSplash(args.SplashScreen, $"PathActivate||{args.Files[0].Path}");
                    Window.Current.Content = extendedSplash;
                }

                Window.Current.Activate();
            }
        }
    }
}
