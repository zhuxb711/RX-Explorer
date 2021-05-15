using System;
using System.ComponentModel;
using WeakEvent;
using Windows.ApplicationModel.Core;
using Windows.Storage;
using Windows.UI.Core;
using Windows.UI.Xaml;

namespace RX_Explorer.Class
{
    /// <summary>
    /// 提供对字体颜色的切换功能
    /// </summary>
    public sealed class AppThemeController : INotifyPropertyChanged
    {
        /// <summary>
        /// 指示当前应用的主题色
        /// </summary>
        public ElementTheme Theme
        {
            get
            {
                return theme;
            }
            set
            {
                if (value != theme)
                {
                    theme = value;

                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Theme)));

                    WeakEventCore.Raise(null, value);

                    ApplicationData.Current.LocalSettings.Values["AppFontColorMode"] = Enum.GetName(typeof(ElementTheme), value);
                    ApplicationData.Current.SignalDataChanged();
                }
            }
        }

        private ElementTheme theme;

        public event PropertyChangedEventHandler PropertyChanged;

        public event EventHandler<ElementTheme> ThemeChanged
        {
            add
            {
                WeakEventCore.Subscribe(value);
            }
            remove
            {
                WeakEventCore.Unsubscribe(value);
            }
        }

        private readonly WeakEventSource<ElementTheme> WeakEventCore;

        private static AppThemeController Instance;

        private static readonly object Locker = new object();

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

        /// <summary>
        /// 初始化AppThemeController对象
        /// </summary>
        private AppThemeController()
        {
            WeakEventCore = new WeakEventSource<ElementTheme>();

            ApplicationData.Current.DataChanged += Current_DataChanged;

            if (ApplicationData.Current.LocalSettings.Values["AppFontColorMode"] is string Mode)
            {
                Theme = Enum.Parse<ElementTheme>(Mode);
            }
            else
            {
                Theme = ElementTheme.Dark;
                ApplicationData.Current.LocalSettings.Values["AppFontColorMode"] = "Dark";
            }
        }

        private async void Current_DataChanged(ApplicationData sender, object args)
        {
            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                Theme = Enum.Parse<ElementTheme>(Convert.ToString(ApplicationData.Current.LocalSettings.Values["AppFontColorMode"]));
            });
        }
    }
}
