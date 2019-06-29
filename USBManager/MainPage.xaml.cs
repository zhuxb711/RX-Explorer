using Windows.System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media.Animation;

namespace USBManager
{
    public sealed partial class MainPage : Page
    {
        public MainPage()
        {
            InitializeComponent();
            Window.Current.SetTitleBar(TitleBar);
            Loaded += MainPage_Loaded;
        }

        private void MainPage_Loaded(object sender, RoutedEventArgs e)
        {
            KeyboardAccelerator GoBack = new KeyboardAccelerator
            {
                Key = VirtualKey.GoBack
            };
            GoBack.Invoked += BackInvoked;
            KeyboardAccelerator AltLeft = new KeyboardAccelerator
            {
                Key = VirtualKey.Left
            };
            AltLeft.Invoked += BackInvoked;
            KeyboardAccelerators.Add(GoBack);
            KeyboardAccelerators.Add(AltLeft);
            AltLeft.Modifiers = VirtualKeyModifiers.Menu;

            Nav.Navigate(typeof(USBControl), null, new DrillInNavigationTransitionInfo());

            USBControl.ThisPage.Nav.Navigated += Nav_Navigated;
        }

        private void Nav_Navigated(object sender, Windows.UI.Xaml.Navigation.NavigationEventArgs e)
        {
            BackButton.IsEnabled = USBControl.ThisPage.Nav.CanGoBack;
        }

        private void BackInvoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            BackRequested();
            args.Handled = true;
        }

        private void BackRequested()
        {
            if (USBControl.ThisPage.Nav.CanGoBack)
            {
                USBControl.ThisPage.Nav.GoBack();
            }
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            BackRequested();
        }
    }
}
