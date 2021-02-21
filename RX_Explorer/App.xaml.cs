using RX_Explorer.Class;
using System;
using System.Collections.Generic;
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

        private void App_Resuming(object sender, object e)
        {
            AppInstanceIdContainer.RegisterId(AppInstanceIdContainer.CurrentId);
        }

        protected override async void OnWindowCreated(WindowCreatedEventArgs args)
        {
            MSStoreHelper.Current.PreLoadAppLicense();

            using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableController())
            {
                if (await Exclusive.Controller.CheckIfQuicklookIsAvaliableAsync().ConfigureAwait(true))
                {
                    SettingControl.IsQuicklookAvailable = true;
                }
                else
                {
                    SettingControl.IsQuicklookAvailable = false;
                }
            }
        }

        private void App_Suspending(object sender, SuspendingEventArgs e)
        {
            AppInstanceIdContainer.UngisterId(AppInstanceIdContainer.CurrentId);
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
            Microsoft.Toolkit.Uwp.Helpers.SystemInformation.TrackAppUse(e);

            ApplicationViewTitleBar TitleBar = ApplicationView.GetForCurrentView().TitleBar;
            TitleBar.ButtonBackgroundColor = Colors.Transparent;
            TitleBar.ButtonInactiveBackgroundColor = Colors.Transparent;

            CoreApplication.GetCurrentView().TitleBar.ExtendViewIntoTitleBar = true;

            if (Window.Current.Content is Frame frame)
            {
                if (frame.Content is MainPage Main && Main.Nav.Content is TabViewContainer TabContainer)
                {
                    if (!string.IsNullOrWhiteSpace(e.Arguments) && await FileSystemStorageItemBase.CheckExist(e.Arguments).ConfigureAwait(true))
                    {
                        await TabContainer.CreateNewTabAsync(null, new string[] { e.Arguments }).ConfigureAwait(true);
                    }
                    else
                    {
                        await TabContainer.CreateNewTabAsync(null, Array.Empty<string>()).ConfigureAwait(true);
                    }
                }
            }
            else
            {
                if (!string.IsNullOrWhiteSpace(e.Arguments) && await FileSystemStorageItemBase.CheckExist(e.Arguments).ConfigureAwait(true))
                {
                    ExtendedSplash extendedSplash = new ExtendedSplash(e.SplashScreen, new List<string[]> { new string[] { e.Arguments } });
                    Window.Current.Content = extendedSplash;
                }
                else
                {
                    LaunchWithTabMode Mode = LaunchModeController.GetLaunchMode();

                    switch (Mode)
                    {
                        case LaunchWithTabMode.CreateNewTab:
                            {
                                ExtendedSplash extendedSplash = new ExtendedSplash(e.SplashScreen);
                                Window.Current.Content = extendedSplash;
                                break;
                            }
                        case LaunchWithTabMode.LastOpenedTab:
                            {
                                List<string[]> LastOpenedPathArray = await LaunchModeController.GetAllPathAsync(Mode).ToListAsync();

                                LaunchModeController.Clear(LaunchWithTabMode.LastOpenedTab);

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
                        case LaunchWithTabMode.SpecificTab:
                            {
                                string[] SpecificPathArray = await LaunchModeController.GetAllPathAsync(Mode).Select((Item) => Item.FirstOrDefault()).OfType<string>().ToArrayAsync();

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

                            if (string.IsNullOrWhiteSpace(Path) || Regex.IsMatch(Path, @"::\{[0-9A-F\-]+\}", RegexOptions.IgnoreCase))
                            {
                                await TabContainer.CreateNewTabAsync(null, Array.Empty<string>()).ConfigureAwait(true);
                            }
                            else
                            {
                                await TabContainer.CreateNewTabAsync(null, Path == "." ? CmdArgs.Operation.CurrentDirectoryPath : Path).ConfigureAwait(true);
                            }
                        }
                        else
                        {
                            await TabContainer.CreateNewTabAsync(null, Array.Empty<string>()).ConfigureAwait(true);
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
            }
            else if (args is ProtocolActivatedEventArgs ProtocalArgs)
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
            }
            else
            {
                ExtendedSplash extendedSplash = new ExtendedSplash(args.SplashScreen);
                Window.Current.Content = extendedSplash;
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

                CoreApplication.GetCurrentView().TitleBar.ExtendViewIntoTitleBar = true;

                if (Window.Current.Content is Frame mainPageFrame)
                {
                    if (mainPageFrame.Content is MainPage mainPage && mainPage.Nav.Content is TabViewContainer Container)
                    {
                        await Container.CreateNewTabAsync(null, args.Files.Select((File) => File.Path).ToArray()).ConfigureAwait(true);
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
