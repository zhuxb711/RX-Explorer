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
        public ElementTheme Theme { get; private set; }

        public event PropertyChangedEventHandler PropertyChanged;

        private static AppThemeController Instance;

        public static AppThemeController Current
        {
            get
            {
                lock (SyncRootProvider.SyncRoot)
                {
                    return Instance ?? (Instance = new AppThemeController());
                }
            }
        }

        /// <summary>
        /// 使用此方法切换主题色
        /// </summary>
        /// <param name="Theme"></param>
        public void ChangeThemeTo(ElementTheme Theme)
        {
            this.Theme = Theme;
            ApplicationData.Current.LocalSettings.Values["AppFontColorMode"] = Enum.GetName(typeof(ElementTheme), Theme);
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Theme)));
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
