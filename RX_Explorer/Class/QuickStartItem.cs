using PropertyChanged;
using System;
using Windows.ApplicationModel.Core;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media.Imaging;

namespace RX_Explorer.Class
{
    [AddINotifyPropertyChangedInterface]
    public sealed partial class QuickStartItem
    {
        public string DisplayName { get; private set; }

        public string Protocol { get; private set; }

        public string IconPath { get; private set; }

        public QuickStartType Type { get; private set; }

        public BitmapImage Thumbnail { get; private set; }

        public void Update(BitmapImage Thumbnail, string Protocol, string IconPath, string DisplayName)
        {
            this.Thumbnail = Thumbnail;
            this.Protocol = Protocol;
            this.DisplayName = DisplayName;

            if (!string.IsNullOrEmpty(IconPath))
            {
                this.IconPath = IconPath;
            }
        }

        public QuickStartItem(QuickStartType Type, BitmapImage Image, string Protocol, string IconPath, string DisplayName = null)
        {
            this.Type = Type;
            this.Thumbnail = Image;
            this.Protocol = Protocol;
            this.IconPath = IconPath;
            this.DisplayName = DisplayName;
        }

        public QuickStartItem()
        {
            Type = QuickStartType.AddButton;
            AppThemeController.Current.ThemeChanged += Current_ThemeChanged;

            if (AppThemeController.Current.Theme == ElementTheme.Dark)
            {
                Thumbnail = new BitmapImage(new Uri("ms-appx:///Assets/AddImage_Light.png"));
            }
            else
            {
                Thumbnail = new BitmapImage(new Uri("ms-appx:///Assets/AddImage_Dark.png"));
            }
        }

        private async void Current_ThemeChanged(object sender, ElementTheme Theme)
        {
            await CoreApplication.MainView.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
            {
                if (Theme == ElementTheme.Dark)
                {
                    Thumbnail = new BitmapImage(new Uri("ms-appx:///Assets/AddImage_Light.png"));
                }
                else
                {
                    Thumbnail = new BitmapImage(new Uri("ms-appx:///Assets/AddImage_Dark.png"));
                }
            });
        }
    }
}
