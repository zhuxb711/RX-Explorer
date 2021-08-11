using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Animation;

namespace RX_Explorer
{
    public sealed partial class SecureAreaContainer : Page
    {
        public static SecureAreaContainer Current { get; private set; }

        public SecureAreaContainer()
        {
            InitializeComponent();
            Current = this;
            Loaded += SecureAreaContainer_Loaded;
        }

        private void SecureAreaContainer_Loaded(object sender, RoutedEventArgs e)
        {
            Loaded -= SecureAreaContainer_Loaded;
            Nav.Navigate(typeof(SecureArea), null, new SuppressNavigationTransitionInfo());
        }

        private void Nav_Navigated(object sender, Windows.UI.Xaml.Navigation.NavigationEventArgs e)
        {
            MainPage.Current.NavView.IsBackEnabled = Nav.CanGoBack;
        }
    }
}
