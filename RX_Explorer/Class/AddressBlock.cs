using System.ComponentModel;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media;

namespace RX_Explorer.Class
{
    public sealed class AddressBlock : INotifyPropertyChanged
    {
        public string Path { get; }

        public AddressBlockType BlockType { get; private set; }

        public SolidColorBrush ForegroundColor
        {
            get
            {
                return foregroundColor ??= new SolidColorBrush(AppThemeController.Current.Theme == ElementTheme.Dark ? Colors.White : Colors.Black);
            }
            private set
            {
                foregroundColor = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ForegroundColor)));
            }
        }

        private SolidColorBrush foregroundColor;

        public string DisplayName
        {
            get
            {
                return InnerDisplayName ?? System.IO.Path.GetFileName(Path);
            }
        }

        private string InnerDisplayName;

        public event PropertyChangedEventHandler PropertyChanged;

        public void SetAsGrayBlock()
        {
            BlockType = AddressBlockType.Gray;
            ForegroundColor = new SolidColorBrush(Colors.DarkGray);
        }

        public void SetAsNormalBlock()
        {
            BlockType = AddressBlockType.Normal;
            ForegroundColor = new SolidColorBrush(AppThemeController.Current.Theme == ElementTheme.Dark ? Colors.White : Colors.Black);
        }

        public void ThemeChanged(FrameworkElement element, object obj)
        {
            if (BlockType == AddressBlockType.Normal)
            {
                ForegroundColor = new SolidColorBrush(AppThemeController.Current.Theme == ElementTheme.Dark ? Colors.White : Colors.Black);
            }
            else
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ForegroundColor)));
            }
        }

        public AddressBlock(string Path, string DisplayName = null)
        {
            this.Path = Path;
            InnerDisplayName = DisplayName;
        }
    }
}
