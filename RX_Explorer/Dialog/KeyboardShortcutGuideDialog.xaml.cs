using RX_Explorer.Class;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media;

namespace RX_Explorer.Dialog
{
    public sealed partial class KeyboardShortcutGuideDialog : QueueContentDialog
    {
        public KeyboardShortcutGuideDialog(string Text)
        {
            InitializeComponent();
            MarkDown.Header3Foreground = new SolidColorBrush((Color)Application.Current.Resources["SystemAccentColor"]);
            MarkDown.LinkForeground = AppThemeController.Current.Theme == ElementTheme.Dark ? new SolidColorBrush((Color)Application.Current.Resources["SystemAccentColorLight1"])
                                                                                            : new SolidColorBrush((Color)Application.Current.Resources["SystemAccentColorDark1"]);
            MarkDown.Text = Text;
        }
    }
}
