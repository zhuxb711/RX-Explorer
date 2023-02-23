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
    public sealed partial class TreeViewColumnWidthSaver
    {
        private ColumnWidthData Cache;
        private readonly static object Locker = new object();
        private static TreeViewColumnWidthSaver Instance;

        public Visibility TreeViewVisibility { get; set; }

        private ColumnWidthData InnerData
        {
            get => Cache;
            set
            {
                Cache = value;
                ApplicationData.Current.LocalSettings.Values["TreeViewColumnWidthData"] = JsonSerializer.Serialize(value);
                ApplicationData.Current.SignalDataChanged();
            }
        }

        public static TreeViewColumnWidthSaver Current
        {
            get
            {
                lock (Locker)
                {
                    return Instance ??= new TreeViewColumnWidthSaver();
                }
            }
        }

        [DependsOn(nameof(TreeViewVisibility), nameof(InnerData))]
        public GridLength TreeViewColumnWidth
        {
            get
            {
                if (TreeViewVisibility == Visibility.Visible)
                {
                    return new GridLength(InnerData.TreeViewColumnWidth, GridUnitType.Star);
                }
                else
                {
                    return new GridLength(0, GridUnitType.Pixel);
                }
            }
            set
            {
                if (TreeViewVisibility == Visibility.Visible)
                {
                    ColumnWidthData NewData = (ColumnWidthData)InnerData.Clone();
                    NewData.TreeViewColumnWidth = value.Value;
                    InnerData = NewData;
                }
            }
        }

        [DependsOn(nameof(TreeViewVisibility))]
        public GridLength SpliterColumnWidth
        {
            get
            {
                if (TreeViewVisibility == Visibility.Visible)
                {
                    return new GridLength(2, GridUnitType.Pixel);
                }
                else
                {
                    return new GridLength(0, GridUnitType.Pixel);
                }
            }
        }

        [DependsOn(nameof(TreeViewVisibility), nameof(InnerData))]
        public GridLength BladeViewColumnWidth
        {
            get
            {
                if (TreeViewVisibility == Visibility.Visible)
                {
                    return new GridLength(InnerData.BladeViewColumnWidth, GridUnitType.Star);
                }
                else
                {
                    return new GridLength(1, GridUnitType.Star);
                }
            }
            set
            {
                if (TreeViewVisibility == Visibility.Visible)
                {
                    ColumnWidthData NewData = (ColumnWidthData)InnerData.Clone();
                    NewData.BladeViewColumnWidth = value.Value;
                    InnerData = NewData;
                }
            }
        }

        private TreeViewColumnWidthSaver()
        {
            if (ApplicationData.Current.LocalSettings.Values["TreeViewColumnWidthData"] is string RawString)
            {
                Cache = JsonSerializer.Deserialize<ColumnWidthData>(RawString);
            }
            else
            {
                Cache = new ColumnWidthData
                {
                    TreeViewColumnWidth = 1,
                    BladeViewColumnWidth = 3
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
                    if (ApplicationData.Current.LocalSettings.Values["TreeViewColumnWidthData"] is string RawString)
                    {
                        InnerData = JsonSerializer.Deserialize<ColumnWidthData>(RawString);
                    }
                });
            }
            catch (Exception)
            {
                //No need to handle this exception
            }
        }

        private class ColumnWidthData : ICloneable, IEquatable<ColumnWidthData>
        {
            public double TreeViewColumnWidth { get; set; }

            public double BladeViewColumnWidth { get; set; }

            public object Clone()
            {
                return MemberwiseClone();
            }

            public bool Equals(ColumnWidthData other)
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
                    return other.TreeViewColumnWidth == TreeViewColumnWidth && other.BladeViewColumnWidth == BladeViewColumnWidth;
                }
            }

            public override bool Equals(object obj)
            {
                return obj is ColumnWidthData Item && Equals(Item);
            }

            public override int GetHashCode()
            {
                return TreeViewColumnWidth.GetHashCode() ^ BladeViewColumnWidth.GetHashCode();
            }

            public static bool operator ==(ColumnWidthData left, ColumnWidthData right)
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

            public static bool operator !=(ColumnWidthData left, ColumnWidthData right)
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
