using System;
using Windows.System;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;

namespace FileManager
{
    public sealed partial class AboutMe : Page
    {
        public AboutMe()
        {
            InitializeComponent();
        }

        private async void GithubImage_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            await Launcher.LaunchUriAsync(new Uri("https://github.com/zhuxb711"));
        }

        private async void EmailImage_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            await Launcher.LaunchUriAsync(new Uri("mailto:zhuxb711@yeah.net"));
        }
    }
}
