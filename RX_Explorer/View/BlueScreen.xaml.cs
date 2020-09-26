using System;
using System.Threading.Tasks;
using Windows.System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace RX_Explorer
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
            if (e?.Parameter is string ExceptionMessage)
            {
                Message.Text = ExceptionMessage;
            }
        }

        private async Task SendEmailAsync(string messageBody)
        {
            _ = await Launcher.LaunchUriAsync(new Uri("mailto:zrfcfgs@outlook.com?subject=BugReport&body=" + Uri.EscapeDataString(messageBody)));
        }

        private async void Report_Click(object sender, RoutedEventArgs e)
        {
            await SendEmailAsync(Message.Text).ConfigureAwait(false);
        }
    }
}
