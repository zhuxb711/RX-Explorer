using System;
using System.Collections.Generic;
using System.ComponentModel;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace RX_Explorer.Class
{
    public sealed class SortIndicatorController : INotifyPropertyChanged, IDisposable
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

        private static readonly List<SortIndicatorController> CurrentInstance = new List<SortIndicatorController>();

        public static void SetIndicatorStatus(SortTarget Target, SortDirection Direction)
        {
            foreach (SortIndicatorController Instance in CurrentInstance)
            {
                SetIndicatorCore(Instance, Target, Direction);
            }
        }

        private static void SetIndicatorCore(SortIndicatorController Instance, SortTarget Target, SortDirection Direction)
        {
            switch (Target)
            {
                case SortTarget.Name:
                    {
                        Instance.Indicator1Icon = new FontIcon { Glyph = Direction == SortDirection.Ascending ? UpArrowIcon : DownArrowIcon };

                        Instance.Indicator1Visibility = Visibility.Visible;
                        Instance.Indicator2Visibility = Visibility.Collapsed;
                        Instance.Indicator3Visibility = Visibility.Collapsed;
                        Instance.Indicator4Visibility = Visibility.Collapsed;

                        break;
                    }
                case SortTarget.ModifiedTime:
                    {
                        Instance.Indicator2Icon = new FontIcon { Glyph = Direction == SortDirection.Ascending ? UpArrowIcon : DownArrowIcon };

                        Instance.Indicator1Visibility = Visibility.Collapsed;
                        Instance.Indicator2Visibility = Visibility.Visible;
                        Instance.Indicator3Visibility = Visibility.Collapsed;
                        Instance.Indicator4Visibility = Visibility.Collapsed;

                        break;
                    }
                case SortTarget.Type:
                    {
                        Instance.Indicator3Icon = new FontIcon { Glyph = Direction == SortDirection.Ascending ? UpArrowIcon : DownArrowIcon };

                        Instance.Indicator1Visibility = Visibility.Collapsed;
                        Instance.Indicator2Visibility = Visibility.Collapsed;
                        Instance.Indicator3Visibility = Visibility.Visible;
                        Instance.Indicator4Visibility = Visibility.Collapsed;

                        break;
                    }
                case SortTarget.Size:
                    {
                        Instance.Indicator4Icon = new FontIcon { Glyph = Direction == SortDirection.Ascending ? UpArrowIcon : DownArrowIcon };

                        Instance.Indicator1Visibility = Visibility.Collapsed;
                        Instance.Indicator2Visibility = Visibility.Collapsed;
                        Instance.Indicator3Visibility = Visibility.Collapsed;
                        Instance.Indicator4Visibility = Visibility.Visible;

                        break;
                    }
                default:
                    {
                        throw new NotSupportedException("SortTarget is not supported");
                    }
            }

            Instance.PropertyChanged?.Invoke(Instance, new PropertyChangedEventArgs(nameof(Indicator1Icon)));
            Instance.PropertyChanged?.Invoke(Instance, new PropertyChangedEventArgs(nameof(Indicator2Icon)));
            Instance.PropertyChanged?.Invoke(Instance, new PropertyChangedEventArgs(nameof(Indicator3Icon)));
            Instance.PropertyChanged?.Invoke(Instance, new PropertyChangedEventArgs(nameof(Indicator4Icon)));
            Instance.PropertyChanged?.Invoke(Instance, new PropertyChangedEventArgs(nameof(Indicator1Visibility)));
            Instance.PropertyChanged?.Invoke(Instance, new PropertyChangedEventArgs(nameof(Indicator2Visibility)));
            Instance.PropertyChanged?.Invoke(Instance, new PropertyChangedEventArgs(nameof(Indicator3Visibility)));
            Instance.PropertyChanged?.Invoke(Instance, new PropertyChangedEventArgs(nameof(Indicator4Visibility)));
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);

            if (CurrentInstance.Contains(this))
            {
                CurrentInstance.Remove(this);
            }
        }

        ~SortIndicatorController()
        {
            Dispose();
        }

        public SortIndicatorController()
        {
            SetIndicatorCore(this, SortCollectionGenerator.Current.SortTarget, SortCollectionGenerator.Current.SortDirection);
            CurrentInstance.Add(this);
        }
    }
}
