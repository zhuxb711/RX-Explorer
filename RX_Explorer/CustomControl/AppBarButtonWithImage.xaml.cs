using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Imaging;

namespace RX_Explorer.CustomControl
{
    public sealed partial class AppBarButtonWithImage : AppBarButton
    {
        public BitmapImage ImageIcon
        {
            get
            {
                return GetValue(ImageIconProperty) as BitmapImage;
            }
            set
            {
                SetValue(ImageIconProperty, value);
            }
        }

        public static readonly DependencyProperty ImageIconProperty = DependencyProperty.Register("ImageIcon", typeof(BitmapImage), typeof(AppBarButtonWithImage), new PropertyMetadata(null));

        public AppBarButtonWithImage()
        {
            InitializeComponent();

            Icon = new FontIcon();
        }
    }
}