using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Imaging;

namespace RX_Explorer.CustomControl
{
    public sealed partial class MenuFlyoutItemWithImage : MenuFlyoutItem
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

        public static readonly DependencyProperty ImageIconProperty = DependencyProperty.Register("ImageIcon", typeof(BitmapImage), typeof(MenuFlyoutItemWithImage), new PropertyMetadata(null));

        public MenuFlyoutItemWithImage()
        {
            InitializeComponent();

            Icon = new FontIcon();
        }
    }
}
