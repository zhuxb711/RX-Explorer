using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace RX_Explorer.Class
{
    public sealed class SortIndicatorController : INotifyPropertyChanged
    {
        public Visibility Indicator1Visibility { get; private set; }

        public Visibility Indicator2Visibility { get; private set; }

        public Visibility Indicator3Visibility { get; private set; }

        public Visibility Indicator4Visibility { get; private set; }

        public FontIcon Indicator1Icon { get; private set; }

        public FontIcon Indicator2Icon { get; private set; }

        public FontIcon Indicator3Icon { get; private set; }

        public FontIcon Indicator4Icon { get; private set; }

        private const string UpArrowIcon = "\uF0AD";

        private const string DownArrowIcon = "\uF0AE";

        public event PropertyChangedEventHandler PropertyChanged;

        public void SetIndicatorStatus(SortTarget Target, SortDirection Direction)
        {
            switch (Target)
            {
                case SortTarget.Name:
                    {
                        Indicator1Icon = new FontIcon { Glyph = Direction == SortDirection.Ascending ? UpArrowIcon : DownArrowIcon };

                        Indicator1Visibility = Visibility.Visible;
                        Indicator2Visibility = Visibility.Collapsed;
                        Indicator3Visibility = Visibility.Collapsed;
                        Indicator4Visibility = Visibility.Collapsed;

                        break;
                    }
                case SortTarget.ModifiedTime:
                    {
                        Indicator2Icon = new FontIcon { Glyph = Direction == SortDirection.Ascending ? UpArrowIcon : DownArrowIcon };

                        Indicator1Visibility = Visibility.Collapsed;
                        Indicator2Visibility = Visibility.Visible;
                        Indicator3Visibility = Visibility.Collapsed;
                        Indicator4Visibility = Visibility.Collapsed;

                        break;
                    }
                case SortTarget.Type:
                    {
                        Indicator3Icon = new FontIcon { Glyph = Direction == SortDirection.Ascending ? UpArrowIcon : DownArrowIcon };

                        Indicator1Visibility = Visibility.Collapsed;
                        Indicator2Visibility = Visibility.Collapsed;
                        Indicator3Visibility = Visibility.Visible;
                        Indicator4Visibility = Visibility.Collapsed;

                        break;
                    }
                case SortTarget.Size:
                    {
                        Indicator4Icon = new FontIcon { Glyph = Direction == SortDirection.Ascending ? UpArrowIcon : DownArrowIcon };

                        Indicator1Visibility = Visibility.Collapsed;
                        Indicator2Visibility = Visibility.Collapsed;
                        Indicator3Visibility = Visibility.Collapsed;
                        Indicator4Visibility = Visibility.Visible;

                        break;
                    }
                default:
                    {
                        throw new NotSupportedException("SortTarget is not supported");
                    }
            }

            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Indicator1Icon)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Indicator2Icon)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Indicator3Icon)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Indicator4Icon)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Indicator1Visibility)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Indicator2Visibility)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Indicator3Visibility)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Indicator4Visibility)));
        }

        public SortIndicatorController()
        {
            SetIndicatorStatus(SortTarget.Name, SortDirection.Ascending);
        }
    }
}
