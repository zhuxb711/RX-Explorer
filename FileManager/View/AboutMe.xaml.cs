using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
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
