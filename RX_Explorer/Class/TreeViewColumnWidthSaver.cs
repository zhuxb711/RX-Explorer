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
    public sealed class TreeViewColumnWidthSaver : INotifyPropertyChanged
    {
        private ColumnWidthData InnerData;
        private readonly static object Locker = new object();
        private static TreeViewColumnWidthSaver Instance;
        private Visibility TreeViewVisibility;

        private ColumnWidthData Data
        {
            get => InnerData;
            set
            {
                InnerData = value;
                ApplicationData.Current.LocalSettings.Values["TreeViewColumnWidthData"] = JsonSerializer.Serialize(value);
                ApplicationData.Current.SignalDataChanged();
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

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

        public GridLength TreeViewColumnWidth
        {
            get
            {
                if (TreeViewVisibility == Visibility.Visible)
                {
                    return new GridLength(Data.TreeViewColumnWidth, GridUnitType.Star);
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
                    Data = new ColumnWidthData(value.Value, Data.BladeViewColumnWidth);
                }
            }
        }

        public GridLength SpliterColumnWidth
        {
            get
            {
                if (TreeViewVisibility == Visibility.Visible)
                {
                    return new GridLength(8, GridUnitType.Pixel);
                }
                else
                {
                    return new GridLength(0, GridUnitType.Pixel);
                }
            }
        }

        public GridLength BladeViewColumnWidth
        {
            get
            {
                if (TreeViewVisibility == Visibility.Visible)
                {
                    return new GridLength(Data.BladeViewColumnWidth, GridUnitType.Star);
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
                    Data = new ColumnWidthData(Data.TreeViewColumnWidth, value.Value);
                }
            }
        }

        public void SetTreeViewVisibility(Visibility Visibility)
        {
            TreeViewVisibility = Visibility;
            OnPropertyChanged(nameof(TreeViewColumnWidth));
            OnPropertyChanged(nameof(BladeViewColumnWidth));
            OnPropertyChanged(nameof(SpliterColumnWidth));
        }

        private TreeViewColumnWidthSaver()
        {
            if (ApplicationData.Current.LocalSettings.Values["TreeViewColumnWidthData"] is string RawString)
            {
                InnerData = JsonSerializer.Deserialize<ColumnWidthData>(RawString);
            }
            else
            {
                InnerData = new ColumnWidthData(1, 3);
            }

            ApplicationData.Current.DataChanged += Current_DataChanged;
        }

        private async void Current_DataChanged(ApplicationData sender, object args)
        {
            try
            {
                if (ApplicationData.Current.LocalSettings.Values["TreeViewColumnWidthData"] is string RawString)
                {
                    InnerData = JsonSerializer.Deserialize<ColumnWidthData>(RawString);
                }

                await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    OnPropertyChanged(nameof(TreeViewColumnWidth));
                    OnPropertyChanged(nameof(BladeViewColumnWidth));
                });
            }
            catch (Exception)
            {
                //No need to handle this exception
            }
        }

        private void OnPropertyChanged([CallerMemberName] string PropertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(PropertyName));
        }

        private class ColumnWidthData
        {
            public double TreeViewColumnWidth { get; }

            public double BladeViewColumnWidth { get; }

            public ColumnWidthData(double TreeViewColumnWidth, double BladeViewColumnWidth)
            {
                this.TreeViewColumnWidth = TreeViewColumnWidth;
                this.BladeViewColumnWidth = BladeViewColumnWidth;
            }
        }
    }
}
