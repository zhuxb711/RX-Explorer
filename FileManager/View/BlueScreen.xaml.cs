using Microsoft.Toolkit.Uwp.Notifications;
using System;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Core;
using Windows.Storage;
using Windows.System;
using Windows.UI.Notifications;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace FileManager
{
    public sealed partial class BlueScreen : Page
    {
        public BlueScreen()
        {
            InitializeComponent();
            Window.Current.SetTitleBar(TitleBar);
            Loaded += BlueScreen_Loaded;
        }

        private async void BlueScreen_Loaded(object sender, RoutedEventArgs e)
        {
            await Task.Delay(5000);
            await SendEmailAsync(Message.Text);
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            if (e.Parameter is string ExceptionMessage)
            {
                Message.Text = ExceptionMessage;
            }
        }

        private async Task SendEmailAsync(string messageBody)
        {
            if (Windows.System.UserProfile.GlobalizationPreferences.Languages.FirstOrDefault().StartsWith("zh"))
            {
                messageBody = "版本: "
                            + string.Format("Version: {0}.{1}.{2}.{3}", Package.Current.Id.Version.Major, Package.Current.Id.Version.Minor, Package.Current.Id.Version.Build, Package.Current.Id.Version.Revision)
                            + messageBody
                            + "\r\r问题复现方法：\r\r1、\r\r2、\r\r3、\r";
            }
            else
            {
                messageBody = "Version: "
                            + string.Format("Version: {0}.{1}.{2}.{3}", Package.Current.Id.Version.Major, Package.Current.Id.Version.Minor, Package.Current.Id.Version.Build, Package.Current.Id.Version.Revision)
                            + messageBody
                            + "\r\rProblem recurrence method：\r\r1、\r\r2、\r\r3、\r";
            }

            messageBody = Uri.EscapeDataString(messageBody);
            string url = "mailto:zhuxb711@yeah.net?subject=BugReport&body=" + messageBody;
            await Launcher.LaunchUriAsync(new Uri(url));
        }

        private async void Report_Click(object sender, RoutedEventArgs e)
        {
            await SendEmailAsync(Message.Text);
        }

        private async void Reset_Click(object sender, RoutedEventArgs e)
        {
            SQLite.Current.Dispose();
            MySQL.Current.Dispose();
            try
            {
                await ApplicationData.Current.ClearAsync();
            }
            catch (Exception)
            {
                ApplicationData.Current.LocalSettings.Values.Clear();
                await ApplicationData.Current.LocalFolder.DeleteAllSubFilesAndFolders();
                await ApplicationData.Current.TemporaryFolder.DeleteAllSubFilesAndFolders();
                await ApplicationData.Current.LocalCacheFolder.DeleteAllSubFilesAndFolders();
            }
            _ = await CoreApplication.RequestRestartAsync(string.Empty);
        }

    }
}
