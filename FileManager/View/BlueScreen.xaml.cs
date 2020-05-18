using FileManager.Class;
using System;
using System.Threading.Tasks;
using Windows.ApplicationModel.Core;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.System;
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

#if !DEBUG
            Loaded += BlueScreen_Loaded;
#endif
        }

        private async void BlueScreen_Loaded(object sender, RoutedEventArgs e)
        {
            await Task.Delay(5000).ConfigureAwait(true);
            await SendEmailAsync(Message.Text).ConfigureAwait(false);
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
            if (await ApplicationData.Current.TemporaryFolder.TryGetItemAsync("ErrorCaptureFile.png") is StorageFile ErrorScreenShot)
            {
                FileSavePicker Picker = new FileSavePicker
                {
                    SuggestedStartLocation = PickerLocationId.Desktop,
                    SuggestedFileName = $"{Globalization.GetString("Crash_Screenshot_Send_Text")}.png"
                };
                Picker.FileTypeChoices.Add("PNG", new string[] { ".png" });
                if (await Picker.PickSaveFileAsync() is StorageFile SaveFile)
                {
                    await ErrorScreenShot.CopyAndReplaceAsync(SaveFile);
                }
            }
            _ = await Launcher.LaunchUriAsync(new Uri("mailto:zrfcfgs@outlook.com?subject=BugReport&body=" + Uri.EscapeDataString(messageBody)));
        }

        private async void Report_Click(object sender, RoutedEventArgs e)
        {
            await SendEmailAsync(Message.Text).ConfigureAwait(false);
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
                try
                {
                    ApplicationData.Current.LocalSettings.Values.Clear();
                    await ApplicationData.Current.LocalFolder.DeleteAllSubFilesAndFolders().ConfigureAwait(false);
                    await ApplicationData.Current.TemporaryFolder.DeleteAllSubFilesAndFolders().ConfigureAwait(false);
                    await ApplicationData.Current.LocalCacheFolder.DeleteAllSubFilesAndFolders().ConfigureAwait(false);
                }
                catch
                {

                }
            }

            _ = await CoreApplication.RequestRestartAsync(string.Empty);
        }

    }
}
