using Microsoft.Toolkit.Uwp.Helpers;
using Microsoft.Toolkit.Uwp.Notifications;
using RX_Explorer.Class;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.ApplicationModel.Core;
using Windows.System;
using Windows.System.Power;
using Windows.UI;
using Windows.UI.Core;
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

            Suspending += App_Suspending;
            UnhandledException += App_UnhandledException;
            EnteredBackground += App_EnteredBackground;
            LeavingBackground += App_LeavingBackground;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
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

        private void App_Suspending(object sender, SuspendingEventArgs e)
        {
            SuspendingDeferral Deferral = e.SuspendingOperation.GetDeferral();

            try
            {
                LogTracer.MakeSureLogIsFlushed(Math.Min((int)((e.SuspendingOperation.Deadline - DateTimeOffset.Now).TotalMilliseconds - 500), 3000));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"A exception was threw when suspending, message: {ex}");
            }
            finally
            {
                Deferral.Complete();
            }
        }

        private void CurrentDomain_UnhandledException(object sender, System.UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is Exception ex)
            {
                LogTracer.Log(ex, "UnhandleException");
                LogTracer.MakeSureLogIsFlushed(1000);
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
                if (MemoryManager.AppMemoryUsageLevel is AppMemoryUsageLevel.OverLimit or AppMemoryUsageLevel.High)
                {
                    ReduceMemoryUsage();
                }
            }
        }

        private void ReduceMemoryUsage()
        {
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

        private async void App_UnhandledException(object sender, Windows.UI.Xaml.UnhandledExceptionEventArgs e)
        {
            e.Handled = true;
            await LeadToBlueScreen(e.Exception);
        }

        protected override async void OnLaunched(LaunchActivatedEventArgs e)
        {
            await OnLaunchOrOnActivate(e);
        }

        protected override async void OnActivated(IActivatedEventArgs args)
        {
            await OnLaunchOrOnActivate(args);
        }

        private async Task OnLaunchOrOnActivate(IActivatedEventArgs args)
        {
            Globalization.Initialize();
            FontFamilyController.Initialize();

            SystemInformation.Instance.TrackAppUse(args);

            ApplicationViewTitleBar TitleBar = ApplicationView.GetForCurrentView().TitleBar;
            TitleBar.ButtonBackgroundColor = Colors.Transparent;
            TitleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
            TitleBar.ButtonForegroundColor = AppThemeController.Current.Theme == ElementTheme.Dark ? Colors.White : Colors.Black;

            CoreApplication.GetCurrentView().TitleBar.ExtendViewIntoTitleBar = true;

            switch (args)
            {
                case LaunchActivatedEventArgs LaunchArgs:
                    {
                        if (Window.Current.Content is Frame frame)
                        {
                            if (frame.Content is MainPage Main && Main.Nav.Content is TabViewContainer TabContainer)
                            {
                                if (!string.IsNullOrWhiteSpace(LaunchArgs.Arguments) && await FileSystemStorageItemBase.CheckExistAsync(LaunchArgs.Arguments))
                                {
                                    await TabContainer.CreateNewTabAsync(LaunchArgs.Arguments);
                                }
                                else
                                {
                                    await TabContainer.CreateNewTabAsync();
                                }
                            }
                        }
                        else
                        {
                            if (string.IsNullOrWhiteSpace(LaunchArgs.Arguments) || !await FileSystemStorageItemBase.CheckExistAsync(LaunchArgs.Arguments))
                            {
                                await LaunchWithStartupMode(LaunchArgs);
                            }
                            else
                            {
                                ExtendedSplash extendedSplash = new ExtendedSplash(LaunchArgs.SplashScreen, new List<string[]> { new string[] { LaunchArgs.Arguments } });
                                Window.Current.Content = extendedSplash;
                            }
                        }

                        break;
                    }
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
                                    await LaunchWithStartupMode(CmdArgs);
                                }
                                else
                                {
                                    ExtendedSplash extendedSplash = new ExtendedSplash(CmdArgs.SplashScreen, new List<string[]> { new string[] { Path == "." ? CmdArgs.Operation.CurrentDirectoryPath : Path } });
                                    Window.Current.Content = extendedSplash;
                                }
                            }
                            else
                            {
                                await LaunchWithStartupMode(CmdArgs);
                            }
                        }

                        break;
                    }
                case ProtocolActivatedEventArgs ProtocalArgs:
                    {
                        if (string.IsNullOrWhiteSpace(ProtocalArgs.Uri.AbsolutePath))
                        {
                            ExtendedSplash extendedSplash = new ExtendedSplash(ProtocalArgs.SplashScreen);
                            Window.Current.Content = extendedSplash;
                        }
                        else
                        {
                            string StartupArgument = Uri.UnescapeDataString(ProtocalArgs.Uri.AbsolutePath);
                            ExtendedSplash extendedSplash = new ExtendedSplash(ProtocalArgs.SplashScreen, JsonSerializer.Deserialize<List<string[]>>(StartupArgument));
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

        private async Task LaunchWithStartupMode(IActivatedEventArgs LaunchArgs)
        {
            switch (StartupModeController.Mode)
            {
                case StartupMode.CreateNewTab:
                    {
                        ExtendedSplash extendedSplash = new ExtendedSplash(LaunchArgs.SplashScreen);
                        Window.Current.Content = extendedSplash;
                        break;
                    }
                case StartupMode.LastOpenedTab:
                    {
                        List<string[]> LastOpenedPathArray = await StartupModeController.GetAllPathAsync().ToListAsync();

                        if (LastOpenedPathArray.Count > 0)
                        {
                            ExtendedSplash extendedSplash = new ExtendedSplash(LaunchArgs.SplashScreen, LastOpenedPathArray);
                            Window.Current.Content = extendedSplash;
                        }
                        else
                        {
                            ExtendedSplash extendedSplash = new ExtendedSplash(LaunchArgs.SplashScreen);
                            Window.Current.Content = extendedSplash;
                        }

                        break;
                    }
                case StartupMode.SpecificTab:
                    {
                        string[] SpecificPathArray = await StartupModeController.GetAllPathAsync().Select((Item) => Item.FirstOrDefault()).OfType<string>().ToArrayAsync();

                        if (SpecificPathArray.Length > 0)
                        {
                            ExtendedSplash extendedSplash = new ExtendedSplash(LaunchArgs.SplashScreen, SpecificPathArray);
                            Window.Current.Content = extendedSplash;
                        }
                        else
                        {
                            ExtendedSplash extendedSplash = new ExtendedSplash(LaunchArgs.SplashScreen);
                            Window.Current.Content = extendedSplash;
                        }

                        break;
                    }
            }
        }

        private static async Task LeadToBlueScreen(Exception Ex, [CallerMemberName] string MemberName = "", [CallerFilePath] string SourceFilePath = "", [CallerLineNumber] int SourceLineNumber = 0)
        {
            if (Ex == null)
            {
                throw new ArgumentNullException(nameof(Ex), "Exception could not be null");
            }

            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                string[] MessageSplit;

                try
                {
                    if (string.IsNullOrWhiteSpace(Ex.Message))
                    {
                        MessageSplit = Array.Empty<string>();
                    }
                    else
                    {
                        MessageSplit = Ex.Message.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries).Select((Line) => $"        {Line.Trim()}").ToArray();
                    }
                }
                catch
                {
                    MessageSplit = Array.Empty<string>();
                }

                string[] StackTraceSplit;

                try
                {
                    if (string.IsNullOrWhiteSpace(Ex.StackTrace))
                    {
                        StackTraceSplit = Array.Empty<string>();
                    }
                    else
                    {
                        StackTraceSplit = Ex.StackTrace.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries).Select((Line) => $"        {Line.Trim()}").ToArray();
                    }
                }
                catch
                {
                    StackTraceSplit = Array.Empty<string>();
                }

                StringBuilder Builder = new StringBuilder()
                                        .AppendLine($"Version: {string.Join('.', Package.Current.Id.Version.Major, Package.Current.Id.Version.Minor, Package.Current.Id.Version.Build, Package.Current.Id.Version.Revision)}")
                                        .AppendLine()
                                        .AppendLine("The following is the error message:")
                                        .AppendLine("------------------------------------")
                                        .AppendLine($"Exception: {Ex}")
                                        .AppendLine()
                                        .AppendLine("Message:")
                                        .AppendLine(MessageSplit.Length == 0 ? "        Unknown" : string.Join(Environment.NewLine, MessageSplit))
                                        .AppendLine()
                                        .AppendLine("StackTrace:")
                                        .AppendLine(StackTraceSplit.Length == 0 ? "        Unknown" : string.Join(Environment.NewLine, StackTraceSplit))
                                        .AppendLine()
                                        .AppendLine("Extra info: ")
                                        .AppendLine($"        CallerMemberName: {MemberName}")
                                        .AppendLine($"        CallerFilePath: {SourceFilePath}")
                                        .AppendLine($"        CallerLineNumber: {SourceLineNumber}")
                                        .AppendLine("------------------------------------")
                                        .AppendLine();

                if (Window.Current.Content is Frame rootFrame)
                {
                    rootFrame.Navigate(typeof(BlueScreen), Builder.ToString());
                }
                else
                {
                    Frame Frame = new Frame();

                    Window.Current.Content = Frame;

                    Frame.Navigate(typeof(BlueScreen), Builder.ToString());
                }
            });

            LogTracer.Log(Ex, "UnhandleException");
        }
    }
}
