using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.ApplicationModel.Core;
using Windows.Foundation;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.UI;
using Windows.UI.Notifications;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Navigation;

namespace FileManager
{
    /// <summary>
    /// 提供特定于应用程序的行为，以补充默认的应用程序类。
    /// </summary>
    sealed partial class App : Application
    {
        /// <summary>
        /// 初始化单一实例应用程序对象。这是执行的创作代码的第一行，
        /// 已执行，逻辑上等同于 main() 或 WinMain()。
        /// </summary>
        public App()
        {
            InitializeComponent();

            try
            {
                ToastNotificationManager.History.Clear();
            }
            catch (COMException)
            {

            }

            Suspending += OnSuspending;
            UnhandledException += App_UnhandledException;
        }

        private void App_UnhandledException(object sender, Windows.UI.Xaml.UnhandledExceptionEventArgs e)
        {
            if (!(Window.Current.Content is Frame rootFrame))
            {
                rootFrame = new Frame();

                rootFrame.NavigationFailed += OnNavigationFailed;

                Window.Current.Content = rootFrame;
            }

            string Message =
                "\r\r以下是错误信息：\r\rException Code错误代码：" + e.Exception.HResult +
                "\r\rMessage错误消息：" + e.Exception.Message +
                "\r\rSource来源：" + (string.IsNullOrEmpty(e.Exception.Source) ? "Unknown" : e.Exception.Source) +
                "\r\rStackTrace堆栈追踪：" + (string.IsNullOrEmpty(e.Exception.StackTrace) ? "Unknown" : e.Exception.StackTrace);

            rootFrame.Navigate(typeof(BlueScreen), Message);

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

            if (!(Window.Current.Content is Frame))
            {
                ExtendedSplash extendedSplash = new ExtendedSplash(e.SplashScreen);
                Window.Current.Content = extendedSplash;
            }

            Window.Current.Activate();
        }

        protected override void OnActivated(IActivatedEventArgs args)
        {
            if (Window.Current.Content != null)
            {
                if (args is ToastNotificationActivatedEventArgs e && e.Argument == "Updating")
                {
                    return;
                }
            }

            CoreApplication.GetCurrentView().TitleBar.ExtendViewIntoTitleBar = true;
            var viewTitleBar = ApplicationView.GetForCurrentView().TitleBar;
            viewTitleBar.ButtonBackgroundColor = Colors.Transparent;
            viewTitleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
            viewTitleBar.ButtonForegroundColor = (Color)Resources["SystemBaseHighColor"];

            if (!(Window.Current.Content is Frame))
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
                CoreApplication.GetCurrentView().TitleBar.ExtendViewIntoTitleBar = true;
                var viewTitleBar = ApplicationView.GetForCurrentView().TitleBar;
                viewTitleBar.ButtonBackgroundColor = Colors.Transparent;
                viewTitleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
                viewTitleBar.ButtonForegroundColor = (Color)Resources["SystemBaseHighColor"];

                if (Window.Current.Content is Frame)
                {
                    if (MainPage.ThisPage.Nav.CurrentSourcePageType.Name == "FileControl")
                    {
                        MainPage.ThisPage.Nav.GoBack();
                        MainPage.ThisPage.Nav.Navigate(typeof(FileControl), ThisPC.ThisPage.HardDeviceList.Last().Folder, new DrillInNavigationTransitionInfo());
                    }
                    else
                    {
                        MainPage.ThisPage.Nav.Navigate(typeof(FileControl), ThisPC.ThisPage.HardDeviceList.Last().Folder, new DrillInNavigationTransitionInfo());
                    }
                }
                else
                {
                    try
                    {
                        _ = await StorageFolder.GetFolderFromPathAsync(Directory.GetLogicalDrives().FirstOrDefault());
                    }
                    catch (UnauthorizedAccessException)
                    {
                        ExtendedSplash extendedSplash = new ExtendedSplash(args.SplashScreen);
                        Window.Current.Content = extendedSplash;
                    }

                    Frame rootFrame = new Frame();
                    Window.Current.Content = rootFrame;
                    rootFrame.Navigate(typeof(MainPage), new Tuple<string, Rect>("USBActivate||" + args.Files.FirstOrDefault().Path, args.SplashScreen.ImageLocation));
                }

                Window.Current.Activate();
            }
        }

        /// <summary>
        /// 导航到特定页失败时调用
        /// </summary>
        ///<param name="sender">导航失败的框架</param>
        ///<param name="e">有关导航失败的详细信息</param>
        void OnNavigationFailed(object sender, NavigationFailedEventArgs e)
        {
            throw new Exception("Failed to load Page " + e.SourcePageType.FullName);
        }

        /// <summary>
        /// 在将要挂起应用程序执行时调用。  在不知道应用程序
        /// 无需知道应用程序会被终止还是会恢复，
        /// 并让内存内容保持不变。
        /// </summary>
        /// <param name="sender">挂起的请求的源。</param>
        /// <param name="e">有关挂起请求的详细信息。</param>
        private void OnSuspending(object sender, SuspendingEventArgs e)
        {
            var deferral = e.SuspendingOperation.GetDeferral();
            //TODO: 保存应用程序状态并停止任何后台活动
            SQLite.GetInstance().Dispose();
            deferral.Complete();
        }
    }
}
