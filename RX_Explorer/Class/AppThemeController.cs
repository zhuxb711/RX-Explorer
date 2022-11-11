using PropertyChanged;
using System;
using Walterlv.WeakEvents;
using Windows.ApplicationModel.Core;
using Windows.Storage;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;

namespace RX_Explorer.Class
{
    [AddINotifyPropertyChangedInterface]
    public sealed partial class AppThemeController
    {
        private static AppThemeController Instance;
        private static readonly UISettings Settings = new UISettings();
        private static readonly object Locker = new object();

        private readonly WeakEvent<ElementTheme> WeakThemeChanged = new WeakEvent<ElementTheme>();

        public ElementTheme Theme
        {
            get
            {
                if (ApplicationData.Current.LocalSettings.Values["AppFontColorMode"] is string Mode)
                {
                    if (Enum.TryParse(Mode, out ElementTheme Result))
                    {
                        return Result;
                    }
                }

                return ElementTheme.Dark;
            }
            set
            {
                ApplicationData.Current.LocalSettings.Values["AppFontColorMode"] = Enum.GetName(typeof(ElementTheme), value);
                ApplicationData.Current.SignalDataChanged();
                WeakThemeChanged.Invoke(this, Theme);
            }
        }

        public event EventHandler<ElementTheme> ThemeChanged
        {
            add
            {
                WeakThemeChanged.Add(value, value.Invoke);
            }
            remove
            {
                WeakThemeChanged.Remove(value);
            }
        }

        public static AppThemeController Current
        {
            get
            {
                lock (Locker)
                {
                    return Instance ??= new AppThemeController();
                }
            }
        }

        public void SyncAndSetSystemTheme()
        {
            if (Settings.GetColorValue(UIColorType.Background) == Colors.White)
            {
                Theme = ElementTheme.Light;
            }
            else
            {
                Theme = ElementTheme.Dark;
            }
        }

        private AppThemeController()
        {
            ApplicationDataChangedWeakEventRelay.Create(ApplicationData.Current).DataChanged += Current_DataChanged;
        }

        private async void Current_DataChanged(ApplicationData sender, object args)
        {
            try
            {
                await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    OnPropertyChanged(nameof(Theme));
                });
            }
            catch (Exception)
            {
                //No need to handle this exception
            }
        }
    }
}
