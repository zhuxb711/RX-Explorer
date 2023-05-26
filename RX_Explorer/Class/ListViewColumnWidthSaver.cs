using Newtonsoft.Json;
using PropertyChanged;
using System;
using Windows.ApplicationModel.Core;
using Windows.Storage;
using Windows.UI.Core;
using Windows.UI.Xaml;

namespace RX_Explorer.Class
{
    [AddINotifyPropertyChangedInterface]
    public sealed partial class ListViewColumnWidthSaver
    {
        private ColumnWidthData Cache;

        public ListViewLocation Location { get; }

        private string Resource
        {
            get => Location switch
            {
                ListViewLocation.Presenter => "PresenterListViewColumnWidthData",
                ListViewLocation.RecycleBin => "RecycleBinListViewColumnWidthData",
                ListViewLocation.Search => "SearchListViewColumnWidthData",
                ListViewLocation.Compression => "CompressionListViewColumnWidthData",
                _ => throw new ArgumentException()
            };
        }

        private ColumnWidthData InnerData
        {
            get => Cache;
            set
            {
                Cache = value;
                ApplicationData.Current.LocalSettings.Values[Resource] = JsonConvert.SerializeObject(value);
                ApplicationData.Current.SignalDataChanged();
            }
        }

        [DependsOn(nameof(InnerData))]
        public GridLength NameColumnWidth
        {
            get
            {
                return new GridLength(InnerData.NameColumnWidth, GridUnitType.Star);
            }
            set
            {
                ColumnWidthData NewData = (ColumnWidthData)InnerData.Clone();
                NewData.NameColumnWidth = value.Value;
                InnerData = NewData;
            }
        }

        [DependsOn(nameof(InnerData))]
        public GridLength ModifiedColumnWidth
        {
            get
            {
                return new GridLength(InnerData.ModifiedColumnWidth, GridUnitType.Star);
            }
            set
            {
                ColumnWidthData NewData = (ColumnWidthData)InnerData.Clone();
                NewData.ModifiedColumnWidth = value.Value;
                InnerData = NewData;
            }
        }

        [DependsOn(nameof(InnerData))]
        public GridLength TypeColumnWidth
        {
            get
            {
                return new GridLength(InnerData.TypeColumnWidth, GridUnitType.Star);
            }
            set
            {
                ColumnWidthData NewData = (ColumnWidthData)InnerData.Clone();
                NewData.TypeColumnWidth = value.Value;
                InnerData = NewData;
            }
        }

        [DependsOn(nameof(InnerData))]
        public GridLength SizeColumnWidth
        {
            get
            {
                return new GridLength(InnerData.SizeColumnWidth, GridUnitType.Star);
            }
            set
            {
                ColumnWidthData NewData = (ColumnWidthData)InnerData.Clone();
                NewData.SizeColumnWidth = value.Value;
                InnerData = NewData;
            }
        }

        [DependsOn(nameof(InnerData))]
        public GridLength OriginPathColumnWidth
        {
            get
            {
                return new GridLength(InnerData.OriginPathColumnWidth, GridUnitType.Star);
            }
            set
            {
                ColumnWidthData NewData = (ColumnWidthData)InnerData.Clone();
                NewData.OriginPathColumnWidth = value.Value;
                InnerData = NewData;
            }
        }

        [DependsOn(nameof(InnerData))]
        public GridLength PathColumnWidth
        {
            get
            {
                return new GridLength(InnerData.PathColumnWidth, GridUnitType.Star);
            }
            set
            {
                ColumnWidthData NewData = (ColumnWidthData)InnerData.Clone();
                NewData.PathColumnWidth = value.Value;
                InnerData = NewData;
            }
        }

        [DependsOn(nameof(InnerData))]
        public GridLength CompressedSizeColumnWidth
        {
            get
            {
                return new GridLength(InnerData.CompressedSizeColumnWidth, GridUnitType.Star);
            }
            set
            {
                ColumnWidthData NewData = (ColumnWidthData)InnerData.Clone();
                NewData.CompressedSizeColumnWidth = value.Value;
                InnerData = NewData;
            }
        }

        [DependsOn(nameof(InnerData))]
        public GridLength CompressRateColumnWidth
        {
            get
            {
                return new GridLength(InnerData.CompressRateColumnWidth, GridUnitType.Star);
            }
            set
            {
                ColumnWidthData NewData = (ColumnWidthData)InnerData.Clone();
                NewData.CompressRateColumnWidth = value.Value;
                InnerData = NewData;
            }
        }

        public ListViewColumnWidthSaver(ListViewLocation Location)
        {
            this.Location = Location;

            if (ApplicationData.Current.LocalSettings.Values[Resource] is string RawString)
            {
                Cache = JsonConvert.DeserializeObject<ColumnWidthData>(RawString);
            }
            else
            {
                switch (Location)
                {
                    case ListViewLocation.Presenter:
                        {
                            Cache = new ColumnWidthData
                            {
                                NameColumnWidth = 6,
                                ModifiedColumnWidth = 2.5,
                                TypeColumnWidth = 2,
                                SizeColumnWidth = 1.5,
                                OriginPathColumnWidth = 0,
                                PathColumnWidth = 0,
                                CompressedSizeColumnWidth = 0,
                                CompressRateColumnWidth = 0
                            };

                            break;
                        }
                    case ListViewLocation.RecycleBin:
                        {
                            Cache = new ColumnWidthData
                            {
                                NameColumnWidth = 4,
                                ModifiedColumnWidth = 2,
                                TypeColumnWidth = 2,
                                SizeColumnWidth = 1.5,
                                OriginPathColumnWidth = 3,
                                PathColumnWidth = 0,
                                CompressedSizeColumnWidth = 0,
                                CompressRateColumnWidth = 0
                            };

                            break;
                        }
                    case ListViewLocation.Search:
                        {
                            Cache = new ColumnWidthData
                            {
                                NameColumnWidth = 4,
                                ModifiedColumnWidth = 2,
                                TypeColumnWidth = 1.5,
                                SizeColumnWidth = 1,
                                OriginPathColumnWidth = 0,
                                PathColumnWidth = 3,
                                CompressedSizeColumnWidth = 0,
                                CompressRateColumnWidth = 0
                            };

                            break;
                        }
                    case ListViewLocation.Compression:
                        {
                            Cache = new ColumnWidthData
                            {
                                NameColumnWidth = 5,
                                ModifiedColumnWidth = 2.5,
                                TypeColumnWidth = 2,
                                SizeColumnWidth = 1,
                                OriginPathColumnWidth = 0,
                                PathColumnWidth = 0,
                                CompressedSizeColumnWidth = 1,
                                CompressRateColumnWidth = 1
                            };

                            break;
                        }
                    default:
                        {
                            throw new NotSupportedException();
                        }
                }
            }

            ApplicationData.Current.DataChanged += Current_DataChanged;
        }

        private async void Current_DataChanged(ApplicationData sender, object args)
        {
            try
            {
                await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                {
                    if (ApplicationData.Current.LocalSettings.Values[Resource] is string RawString)
                    {
                        InnerData = JsonConvert.DeserializeObject<ColumnWidthData>(RawString);
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
            public double NameColumnWidth { get; set; }

            public double ModifiedColumnWidth { get; set; }

            public double TypeColumnWidth { get; set; }

            public double SizeColumnWidth { get; set; }

            public double OriginPathColumnWidth { get; set; }

            public double PathColumnWidth { get; set; }

            public double CompressedSizeColumnWidth { get; set; }

            public double CompressRateColumnWidth { get; set; }

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
                    return other.NameColumnWidth == NameColumnWidth
                           && other.ModifiedColumnWidth == ModifiedColumnWidth
                           && other.TypeColumnWidth == TypeColumnWidth
                           && other.SizeColumnWidth == SizeColumnWidth
                           && other.OriginPathColumnWidth == OriginPathColumnWidth
                           && other.PathColumnWidth == PathColumnWidth
                           && other.CompressedSizeColumnWidth == CompressedSizeColumnWidth
                           && other.CompressRateColumnWidth == CompressRateColumnWidth;
                }
            }

            public override bool Equals(object obj)
            {
                return obj is ColumnWidthData Item && Equals(Item);
            }

            public override int GetHashCode()
            {
                return NameColumnWidth.GetHashCode()
                       ^ ModifiedColumnWidth.GetHashCode()
                       ^ TypeColumnWidth.GetHashCode()
                       ^ SizeColumnWidth.GetHashCode()
                       ^ OriginPathColumnWidth.GetHashCode()
                       ^ PathColumnWidth.GetHashCode()
                       ^ CompressedSizeColumnWidth.GetHashCode()
                       ^ CompressRateColumnWidth.GetHashCode();
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
