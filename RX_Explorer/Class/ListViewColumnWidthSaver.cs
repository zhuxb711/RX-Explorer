using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Windows.ApplicationModel.Core;
using Windows.Storage;
using Windows.UI.Core;
using Windows.UI.Xaml;

namespace RX_Explorer.Class
{
    public sealed class ListViewColumnWidthSaver : INotifyPropertyChanged
    {
        private readonly string ResourceKey;
        private ColumnWidthData InnerData;

        public event PropertyChangedEventHandler PropertyChanged;

        private ColumnWidthData Data
        {
            get => InnerData;
            set
            {
                InnerData = value;
                ApplicationData.Current.LocalSettings.Values[ResourceKey] = JsonSerializer.Serialize(value);
                ApplicationData.Current.SignalDataChanged();
            }
        }

        public GridLength NameColumnWidth
        {
            get
            {
                return new GridLength(Data.NameColumnWidth, GridUnitType.Star);
            }
            set
            {
                Data = new ColumnWidthData(value.Value, Data.ModifiedColumnWidth, Data.TypeColumnWidth, Data.SizeColumnWidth, Data.OriginPathColumnWidth, Data.PathColumnWidth, Data.CompressedSizeColumnWidth, Data.CompressRateColumnWidth);
            }
        }

        public GridLength ModifiedColumnWidth
        {
            get
            {
                return new GridLength(Data.ModifiedColumnWidth, GridUnitType.Star);
            }
            set
            {
                Data = new ColumnWidthData(Data.NameColumnWidth, value.Value, Data.TypeColumnWidth, Data.SizeColumnWidth, Data.OriginPathColumnWidth, Data.PathColumnWidth, Data.CompressedSizeColumnWidth, Data.CompressRateColumnWidth);
            }
        }

        public GridLength TypeColumnWidth
        {
            get
            {
                return new GridLength(Data.TypeColumnWidth, GridUnitType.Star);
            }
            set
            {
                Data = new ColumnWidthData(Data.NameColumnWidth, Data.ModifiedColumnWidth, value.Value, Data.SizeColumnWidth, Data.OriginPathColumnWidth, Data.PathColumnWidth, Data.CompressedSizeColumnWidth, Data.CompressRateColumnWidth);
            }
        }

        public GridLength SizeColumnWidth
        {
            get
            {
                return new GridLength(Data.SizeColumnWidth, GridUnitType.Star);
            }
            set
            {
                Data = new ColumnWidthData(Data.NameColumnWidth, Data.ModifiedColumnWidth, Data.TypeColumnWidth, value.Value, Data.OriginPathColumnWidth, Data.PathColumnWidth, Data.CompressedSizeColumnWidth, Data.CompressRateColumnWidth);
            }
        }

        public GridLength OriginPathColumnWidth
        {
            get
            {
                return new GridLength(Data.OriginPathColumnWidth, GridUnitType.Star);
            }
            set
            {
                Data = new ColumnWidthData(Data.NameColumnWidth, Data.ModifiedColumnWidth, Data.TypeColumnWidth, Data.SizeColumnWidth, value.Value, Data.PathColumnWidth, Data.CompressedSizeColumnWidth, Data.CompressRateColumnWidth);
            }
        }

        public GridLength PathColumnWidth
        {
            get
            {
                return new GridLength(Data.PathColumnWidth, GridUnitType.Star);
            }
            set
            {
                Data = new ColumnWidthData(Data.NameColumnWidth, Data.ModifiedColumnWidth, Data.TypeColumnWidth, Data.SizeColumnWidth, Data.OriginPathColumnWidth, value.Value, Data.CompressedSizeColumnWidth, Data.CompressRateColumnWidth);
            }
        }

        public GridLength CompressedSizeColumnWidth
        {
            get
            {
                return new GridLength(Data.CompressedSizeColumnWidth, GridUnitType.Star);
            }
            set
            {
                Data = new ColumnWidthData(Data.NameColumnWidth, Data.ModifiedColumnWidth, Data.TypeColumnWidth, Data.SizeColumnWidth, Data.OriginPathColumnWidth, Data.PathColumnWidth, value.Value, Data.CompressRateColumnWidth);
            }
        }

        public GridLength CompressRateColumnWidth
        {
            get
            {
                return new GridLength(Data.CompressRateColumnWidth, GridUnitType.Star);
            }
            set
            {
                Data = new ColumnWidthData(Data.NameColumnWidth, Data.ModifiedColumnWidth, Data.TypeColumnWidth, Data.SizeColumnWidth, Data.OriginPathColumnWidth, Data.PathColumnWidth, Data.CompressedSizeColumnWidth, value.Value);
            }
        }

        public ListViewColumnWidthSaver(ListViewLocation Location)
        {
            ResourceKey = Location switch
            {
                ListViewLocation.Presenter => "PresenterListViewColumnWidthData",
                ListViewLocation.RecycleBin => "RecycleBinListViewColumnWidthData",
                ListViewLocation.Search => "SearchListViewColumnWidthData",
                ListViewLocation.Compression => "CompressionListViewColumnWidthData",
                _ => throw new ArgumentException()
            };

            if (ApplicationData.Current.LocalSettings.Values[ResourceKey] is string RawString)
            {
                InnerData = JsonSerializer.Deserialize<ColumnWidthData>(RawString);
            }
            else
            {
                switch (Location)
                {
                    case ListViewLocation.Presenter:
                        {
                            InnerData = new ColumnWidthData(6, 2.5, 2, 1.5, 0, 0, 0, 0);
                            break;
                        }
                    case ListViewLocation.RecycleBin:
                        {
                            InnerData = new ColumnWidthData(4, 2, 2, 1.5, 3, 0, 0, 0);
                            break;
                        }
                    case ListViewLocation.Search:
                        {
                            InnerData = new ColumnWidthData(4, 2, 1.5, 1, 0, 3, 0, 0);
                            break;
                        }
                    case ListViewLocation.Compression:
                        {
                            InnerData = new ColumnWidthData(5, 2.5, 2, 1, 0, 0, 1, 1);
                            break;
                        }
                }
            }

            ApplicationData.Current.DataChanged += Current_DataChanged;
        }

        private async void Current_DataChanged(ApplicationData sender, object args)
        {
            if (ApplicationData.Current.LocalSettings.Values[ResourceKey] is string RawString)
            {
                InnerData = JsonSerializer.Deserialize<ColumnWidthData>(RawString);
            }

            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                OnPropertyChanged(nameof(NameColumnWidth));
                OnPropertyChanged(nameof(ModifiedColumnWidth));
                OnPropertyChanged(nameof(TypeColumnWidth));
                OnPropertyChanged(nameof(SizeColumnWidth));
                OnPropertyChanged(nameof(OriginPathColumnWidth));
                OnPropertyChanged(nameof(PathColumnWidth));
                OnPropertyChanged(nameof(CompressedSizeColumnWidth));
                OnPropertyChanged(nameof(CompressRateColumnWidth));
            });
        }

        private void OnPropertyChanged([CallerMemberName] string PropertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(PropertyName));
        }

        private class ColumnWidthData
        {
            public double NameColumnWidth { get; }

            public double ModifiedColumnWidth { get; }

            public double TypeColumnWidth { get; }

            public double SizeColumnWidth { get; }

            public double OriginPathColumnWidth { get; }

            public double PathColumnWidth { get; }

            public double CompressedSizeColumnWidth { get; }

            public double CompressRateColumnWidth { get; }

            public ColumnWidthData(double NameColumnWidth, double ModifiedColumnWidth, double TypeColumnWidth, double SizeColumnWidth, double OriginPathColumnWidth, double PathColumnWidth, double CompressedSizeColumnWidth, double CompressRateColumnWidth)
            {
                this.NameColumnWidth = NameColumnWidth;
                this.ModifiedColumnWidth = ModifiedColumnWidth;
                this.TypeColumnWidth = TypeColumnWidth;
                this.SizeColumnWidth = SizeColumnWidth;
                this.OriginPathColumnWidth = OriginPathColumnWidth;
                this.PathColumnWidth = PathColumnWidth;
                this.CompressedSizeColumnWidth = CompressedSizeColumnWidth;
                this.CompressRateColumnWidth = CompressRateColumnWidth;
            }
        }
    }
}
