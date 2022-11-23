using PropertyChanged;
using System;
using System.Text.Json;
using Windows.ApplicationModel.Core;
using Windows.Storage;
using Windows.UI.Core;
using Windows.UI.Xaml;

namespace RX_Explorer.Class
{
    [AddINotifyPropertyChangedInterface]
    public sealed partial class HomeRowHeightSaver
    {
        private RowHeightData Cache;
        private readonly static object Locker = new object();
        private static HomeRowHeightSaver Instance;

        private RowHeightData InnerData
        {
            get => Cache;
            set
            {
                Cache = value;
                ApplicationData.Current.LocalSettings.Values["HomeRowHeightData"] = JsonSerializer.Serialize(value);
                ApplicationData.Current.SignalDataChanged();
            }
        }

        public static HomeRowHeightSaver Current
        {
            get
            {
                lock (Locker)
                {
                    return Instance ??= new HomeRowHeightSaver();
                }
            }
        }

        [DependsOn(nameof(InnerData))]
        public GridLength LibraryListRowHeight
        {
            get
            {
                return new GridLength(InnerData.LibraryListRowHeight, GridUnitType.Star);
            }
            set
            {
                RowHeightData NewData = (RowHeightData)InnerData.Clone();
                NewData.LibraryListRowHeight = value.Value;
                InnerData = NewData;
            }
        }

        [DependsOn(nameof(InnerData))]
        public GridLength DriveListRowHeight
        {
            get
            {
                return new GridLength(InnerData.DriveListRowHeight, GridUnitType.Star);
            }
            set
            {
                RowHeightData NewData = (RowHeightData)InnerData.Clone();
                NewData.DriveListRowHeight = value.Value;
                InnerData = NewData;
            }
        }

        private HomeRowHeightSaver()
        {
            if (ApplicationData.Current.LocalSettings.Values["HomeRowHeightData"] is string RawString)
            {
                Cache = JsonSerializer.Deserialize<RowHeightData>(RawString);
            }
            else
            {
                Cache = new RowHeightData
                {
                    LibraryListRowHeight = 1,
                    DriveListRowHeight = 1
                };
            }

            ApplicationData.Current.DataChanged += Current_DataChanged;
        }

        private async void Current_DataChanged(ApplicationData sender, object args)
        {
            try
            {
                await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                {
                    if (ApplicationData.Current.LocalSettings.Values["HomeRowHeightData"] is string RawString)
                    {
                        InnerData = JsonSerializer.Deserialize<RowHeightData>(RawString);
                    }
                });
            }
            catch (Exception)
            {
                //No need to handle this exception
            }
        }

        private class RowHeightData : ICloneable, IEquatable<RowHeightData>
        {
            public double LibraryListRowHeight { get; set; }

            public double DriveListRowHeight { get; set; }

            public object Clone()
            {
                return MemberwiseClone();
            }

            public bool Equals(RowHeightData other)
            {
                if (other == null)
                {
                    return false;
                }

                if (ReferenceEquals(this, other))
                {
                    return true;
                }
                else
                {
                    return other.LibraryListRowHeight == LibraryListRowHeight && other.DriveListRowHeight == DriveListRowHeight;
                }
            }

            public override bool Equals(object obj)
            {
                return obj is RowHeightData Item && Equals(Item);
            }

            public override int GetHashCode()
            {
                return LibraryListRowHeight.GetHashCode() ^ DriveListRowHeight.GetHashCode();
            }

            public static bool operator ==(RowHeightData left, RowHeightData right)
            {
                if (left is null)
                {
                    return right is null;
                }
                else
                {
                    if (right is null)
                    {
                        return false;
                    }
                    else
                    {
                        return left.Equals(right);
                    }
                }
            }

            public static bool operator !=(RowHeightData left, RowHeightData right)
            {
                if (left is null)
                {
                    return right is not null;
                }
                else
                {
                    if (right is null)
                    {
                        return true;
                    }
                    else
                    {
                        return !left.Equals(right);
                    }
                }
            }
        }
    }

}
