using System;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media;

namespace RX_Explorer.Class
{
    public sealed class AddressBlock : INotifyPropertyChanged, IDisposable
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
                OnPropertyChanged();
            }
        }

        private SolidColorBrush foregroundColor;

        public string DisplayName
        {
            get
            {
                if (string.IsNullOrEmpty(InnerDisplayName))
                {
                    string FileName = System.IO.Path.GetFileName(Path);

                    if (string.IsNullOrEmpty(FileName))
                    {
                        return Path.Split(@"\", StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
                    }
                    else
                    {
                        return FileName;
                    }
                }
                else
                {
                    return InnerDisplayName;
                }
            }
        }

        private readonly string InnerDisplayName;

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

        public AddressBlock(string Path, string DisplayName = null)
        {
            this.Path = Path;
            InnerDisplayName = DisplayName;
            AppThemeController.Current.ThemeChanged += Current_ThemeChanged;
        }

        private void Current_ThemeChanged(object sender, ElementTheme Theme)
        {
            if (BlockType == AddressBlockType.Normal)
            {
                ForegroundColor = new SolidColorBrush(Theme == ElementTheme.Dark ? Colors.White : Colors.Black);
            }
        }

        private void OnPropertyChanged([CallerMemberName] string PropertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(PropertyName));
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
            AppThemeController.Current.ThemeChanged -= Current_ThemeChanged;
        }

        ~AddressBlock()
        {
            Dispose();
        }
    }
}
