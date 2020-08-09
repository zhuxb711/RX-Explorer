using System;
using System.ComponentModel;
using Windows.Storage;
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
                    ApplicationData.Current.LocalSettings.Values["AppFontColorMode"] = Enum.GetName(typeof(ElementTheme), value);
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Theme)));
                }
            }
        }

        private ElementTheme theme;

        public event PropertyChangedEventHandler PropertyChanged;

        private volatile static AppThemeController Instance;

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
        public AppThemeController()
        {
            if (ApplicationData.Current.LocalSettings.Values["AppFontColorMode"] is string Mode)
            {
                Theme = (ElementTheme)Enum.Parse(typeof(ElementTheme), Mode);
            }
            else
            {
                Theme = ElementTheme.Dark;
                ApplicationData.Current.LocalSettings.Values["AppFontColorMode"] = "Dark";
            }
        }
    }
}
