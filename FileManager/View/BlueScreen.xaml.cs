using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;
using Windows.System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// https://go.microsoft.com/fwlink/?LinkId=234238 上介绍了“空白页”项模板

namespace FileManager
{
    /// <summary>
    /// 可用于自身或导航至 Frame 内部的空白页。
    /// </summary>
    public sealed partial class BlueScreen : Page
    {
        public BlueScreen()
        {
            InitializeComponent();
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
            messageBody = Uri.EscapeDataString(messageBody);
            string url = "mailto:zhuxb711@yeah.net?subject=RX_BugReport&body=" + messageBody;
            await Launcher.LaunchUriAsync(new Uri(url));
        }

        private async void Report_Click(object sender, RoutedEventArgs e)
        {
            await SendEmailAsync(Message.Text);
        }
    }
}
