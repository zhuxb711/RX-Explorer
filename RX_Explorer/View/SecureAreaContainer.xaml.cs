using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Animation;

namespace RX_Explorer.View
{
    public sealed partial class SecureAreaContainer : Page
    {
        public Frame NavFrame => Nav;

        public SecureAreaContainer()
        {
            InitializeComponent();
            Nav.Navigate(typeof(SecureArea), null, new SuppressNavigationTransitionInfo());
        }

        private void Nav_Navigated(object sender, Windows.UI.Xaml.Navigation.NavigationEventArgs e)
        {
            MainPage.Current.NavView.IsBackEnabled = Nav.CanGoBack;
        }
    }
}
