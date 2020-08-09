using RX_Explorer.Class;
using System;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.ApplicationModel.Core;
using Windows.Storage;
using Windows.System;
using Windows.UI;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace RX_Explorer
{
    /// <summary>
    /// 提供特定于应用程序的行为，以补充默认的应用程序类。
    /// </summary>
    sealed partial class App : Application
    {
        bool IsInBackgroundMode = false;

        /// <summary>
        /// 初始化单一实例应用程序对象。这是执行的创作代码的第一行，
        /// 已执行，逻辑上等同于 main() 或 WinMain()。
        /// </summary>
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
            await FullTrustExcutorController.Current.TryConnectToFullTrustExutor().ConfigureAwait(true);
        }

        protected async override void OnWindowCreated(WindowCreatedEventArgs args)
        {
            if (await FullTrustExcutorController.Current.CheckQuicklookIsAvaliableAsync().ConfigureAwait(true))
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
            FullTrustExcutorController.Current.Dispose();
        }

        private void CurrentDomain_UnhandledException(object sender, System.UnhandledExceptionEventArgs e)
        {
            if (!e.IsTerminating && e.ExceptionObject is Exception ex)
            {
                ExceptionTracer.RequestBlueScreen(ex);
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

            if (!FullTrustExcutorController.Current.IsNowHasAnyActionExcuting)
            {
                PipeLineController.Current.Dispose();
                FullTrustExcutorController.Current.Dispose();
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
            ExceptionTracer.RequestBlueScreen(e.Exception);
            e.Handled = true;
        }

        /// <summary>
        /// 在应用程序由最终用户正常启动时进行调用。
        /// 将在启动应用程序以打开特定文件等情况下使用。
        /// </summary>
        /// <param name="e">有关启动请求和过程的详细信息。</param>
        protected override void OnLaunched(LaunchActivatedEventArgs e)
        {
            CoreApplication.GetCurrentView().TitleBar.ExtendViewIntoTitleBar = true;
            var viewTitleBar = ApplicationView.GetForCurrentView().TitleBar;
            viewTitleBar.ButtonBackgroundColor = Colors.Transparent;
            viewTitleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
            viewTitleBar.ButtonForegroundColor = (Color)Resources["SystemBaseHighColor"];

            if (!(Window.Current.Content is Frame) && !(Window.Current.Content is ExtendedSplash))
            {
                if (e.PrelaunchActivated)
                {
                    _ = new ExtendedSplash(e.SplashScreen, true);
                }
                else
                {
                    if (!ApplicationData.Current.LocalSettings.Values.ContainsKey("EnablePreLaunch"))
                    {
                        CoreApplication.EnablePrelaunch(true);
                        ApplicationData.Current.LocalSettings.Values["EnablePreLaunch"] = true;
                    }

                    ExtendedSplash extendedSplash = new ExtendedSplash(e.SplashScreen);
                    Window.Current.Content = extendedSplash;
                }
            }

            Window.Current.Activate();
        }

        protected override void OnActivated(IActivatedEventArgs args)
        {
            CoreApplication.GetCurrentView().TitleBar.ExtendViewIntoTitleBar = true;
            var viewTitleBar = ApplicationView.GetForCurrentView().TitleBar;
            viewTitleBar.ButtonBackgroundColor = Colors.Transparent;
            viewTitleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
            viewTitleBar.ButtonForegroundColor = (Color)Resources["SystemBaseHighColor"];

            if (!(Window.Current.Content is Frame))
            {
                if (args.Kind == ActivationKind.Protocol)
                {
                    ProtocolActivatedEventArgs ProtocalArgs = args as ProtocolActivatedEventArgs;

                    if (!string.IsNullOrWhiteSpace(ProtocalArgs.Uri.LocalPath))
                    {
                        ExtendedSplash extendedSplash = new ExtendedSplash(args.SplashScreen, false, $"PathActivate||{ProtocalArgs.Uri.LocalPath}");
                        Window.Current.Content = extendedSplash;
                    }
                    else
                    {
                        ExtendedSplash extendedSplash = new ExtendedSplash(args.SplashScreen);
                        Window.Current.Content = extendedSplash;
                    }
                }
                else
                {
                    ExtendedSplash extendedSplash = new ExtendedSplash(args.SplashScreen);
                    Window.Current.Content = extendedSplash;
                }
            }

            Window.Current.Activate();
        }

        protected override void OnFileActivated(FileActivatedEventArgs args)
        {
            try
            {
                if (args.Verb == "USBArrival")
                {
                    CoreApplication.GetCurrentView().TitleBar.ExtendViewIntoTitleBar = true;
                    var viewTitleBar = ApplicationView.GetForCurrentView().TitleBar;
                    viewTitleBar.ButtonBackgroundColor = Colors.Transparent;
                    viewTitleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
                    viewTitleBar.ButtonForegroundColor = (Color)Resources["SystemBaseHighColor"];

                    ExtendedSplash extendedSplash = new ExtendedSplash(args.SplashScreen, false, $"PathActivate||{args.Files[0].Path}");
                    Window.Current.Content = extendedSplash;

                    Window.Current.Activate();
                }
            }
            catch (Exception ex)
            {
                ExceptionTracer.RequestBlueScreen(ex);
            }
        }
    }
}
