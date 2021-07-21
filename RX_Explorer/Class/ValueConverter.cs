using System;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Media;

namespace RX_Explorer.Class
{
    public sealed class ThemeToIndexConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is ElementTheme Theme)
            {
                return Theme == ElementTheme.Dark ? 0 : 1;
            }
            else
            {
                return null;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            if (value is int Index)
            {
                switch (Index)
                {
                    case 0:
                        {
                            return ElementTheme.Dark;
                        }
                    case 1:
                        {
                            return ElementTheme.Light;
                        }
                    default:
                        {
                            throw new NotImplementedException();
                        }
                }
            }
            else
            {
                return null;
            }
        }
    }

    public sealed class InverseConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            switch (value)
            {
                case bool Bool:
                    {
                        if (targetType == typeof(Visibility))
                        {
                            return Bool ? Visibility.Collapsed : Visibility.Visible;
                        }
                        else if (targetType == typeof(bool) || targetType == typeof(bool?))
                        {
                            return !Bool;
                        }
                        else
                        {
                            return null;
                        }
                    }
                case Visibility Vis:
                    {
                        if (targetType == typeof(Visibility))
                        {
                            return Vis == Visibility.Collapsed ? Visibility.Visible : Visibility.Collapsed;
                        }
                        else if (targetType == typeof(bool) || targetType == typeof(bool?))
                        {
                            return Vis == Visibility.Visible;
                        }
                        else
                        {
                            return null;
                        }
                    }
                default:
                    {
                        return null;
                    }
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            switch (value)
            {
                case bool Bool:
                    {
                        if (targetType == typeof(Visibility))
                        {
                            return Bool ? Visibility.Collapsed : Visibility.Visible;
                        }
                        else if (targetType == typeof(bool) || targetType == typeof(bool?))
                        {
                            return !Bool;
                        }
                        else
                        {
                            return null;
                        }
                    }
                case Visibility Vis:
                    {
                        if (targetType == typeof(Visibility))
                        {
                            return Vis == Visibility.Collapsed ? Visibility.Visible : Visibility.Collapsed;
                        }
                        else if (targetType == typeof(bool) || targetType == typeof(bool?))
                        {
                            return Vis == Visibility.Visible;
                        }
                        else
                        {
                            return null;
                        }
                    }
                default:
                    {
                        return null;
                    }
            }
        }
    }

    public sealed class EmptyTextFiliterConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is string v)
            {
                return string.IsNullOrEmpty(v) ? null : v;
            }
            else
            {
                return null;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            if (value is string v)
            {
                return string.IsNullOrEmpty(v) ? null : v;
            }
            else
            {
                return null;
            }
        }
    }

    public sealed class NullToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if ((value is string v && string.IsNullOrEmpty(v)) || value == null)
            {
                return Visibility.Collapsed;
            }
            else
            {
                return Visibility.Visible;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            if ((value is string v && string.IsNullOrEmpty(v)) || value == null)
            {
                return Visibility.Collapsed;
            }
            else
            {
                return Visibility.Visible;
            }
        }
    }

    public sealed class FolderStateConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is bool IsExpanded)
            {
                if (IsExpanded)
                {
                    return "\xE838";
                }
                else
                {
                    return "\xE8B7";
                }
            }
            else
            {
                return null;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }

    public sealed class TimespanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is double)
            {
                long Millisecond = System.Convert.ToInt64(value);
                int Hour = 0;
                int Minute = 0;
                int Second = 0;

                if (Millisecond >= 1000)
                {
                    Second = System.Convert.ToInt32(Millisecond / 1000);
                    Millisecond %= 1000;
                    if (Second >= 60)
                    {
                        Minute = Second / 60;
                        Second %= 60;
                        if (Minute >= 60)
                        {
                            Hour = Minute / 60;
                            Minute %= 60;
                        }
                    }
                }
                return string.Format("{0:D2}:{1:D2}:{2:D2}.{3:D3}", Hour, Minute, Second, Millisecond);
            }
            else
            {
                return null;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }

    public sealed class SpliterConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is GridLength Length)
            {
                if (Length.Value == 0)
                {
                    return new GridLength(0);
                }
                else
                {
                    return new GridLength(5, GridUnitType.Pixel);
                }
            }
            else
            {
                return null;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }

    public sealed class AlphaSliderValueConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            int Value = System.Convert.ToInt32(((double)value - 1) * 100);
            return Value > 0 ? ("+" + Value) : Value.ToString();
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }

    public sealed class BetaSliderValueConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            int Value = System.Convert.ToInt32(value);
            return Value > 0 ? ("+" + Value) : Value.ToString();
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }

    public sealed class RatingValueConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (uint.TryParse(System.Convert.ToString(value), out uint Result))
            {
                if (Result == 0)
                {
                    return -1;
                }
                else
                {
                    return Math.Max(Math.Min(Math.Round(Result / 20f, MidpointRounding.AwayFromZero), 5), 0);
                }
            }
            else
            {
                return -1;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }

    public class StringToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            return string.IsNullOrEmpty((string)value) ? Visibility.Collapsed : Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            return (Visibility)value == Visibility.Visible;
        }
    }

    public sealed class SyncStatusIconConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is SyncStatus Status)
            {
                switch (Status)
                {
                    case SyncStatus.AvailableOffline:
                        {
                            return "\uEC61";
                        }
                    case SyncStatus.AvailableOnline:
                        {
                            return "\uE753";
                        }
                    case SyncStatus.Sync:
                        {
                            return "\uEDAB";
                        }
                    case SyncStatus.Excluded:
                        {
                            return "\uE194";
                        }
                    default:
                        {
                            return string.Empty;
                        }
                }
            }
            else
            {
                return string.Empty;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }

    public sealed class SyncStatusColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is SyncStatus Status)
            {
                switch (Status)
                {
                    case SyncStatus.AvailableOffline:
                        {
                            return new SolidColorBrush(Colors.LightGreen);
                        }
                    case SyncStatus.AvailableOnline:
                    case SyncStatus.Sync:
                        {
                            return new SolidColorBrush(Colors.DeepSkyBlue);
                        }
                    case SyncStatus.Excluded:
                        {
                            return new SolidColorBrush(Colors.Orange);
                        }
                    default:
                        {
                            return new SolidColorBrush(Colors.Transparent);
                        }
                }
            }
            else
            {
                return new SolidColorBrush(Colors.Transparent);
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }

    public sealed class SyncStatusVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is SyncStatus Status)
            {
                if (Status == SyncStatus.Unknown)
                {
                    return Visibility.Collapsed;
                }
                else
                {
                    return Visibility.Visible;
                }
            }
            else
            {
                return Visibility.Visible;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }

    public sealed class SyncStatusToolTipConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is SyncStatus Status)
            {
                switch(Status)
                {
                    case SyncStatus.AvailableOffline:
                        {
                            return Globalization.GetString("OfflineAvailabilityText3");
                        }
                    case SyncStatus.AvailableOnline:
                        {
                            return Globalization.GetString("OfflineAvailabilityText1");
                        }
                    case SyncStatus.Excluded:
                        {
                            return Globalization.GetString("OfflineAvailabilityStatusText5");
                        }
                    case SyncStatus.Sync:
                        {
                            return Globalization.GetString("OfflineAvailabilityStatusText7");
                        }
                    default:
                        {
                            return null;
                        }
                }
            }
            else
            {
                return null;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
}
