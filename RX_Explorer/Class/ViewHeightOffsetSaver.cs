using RX_Explorer.View;
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Windows.ApplicationModel.Core;
using Windows.Storage;
using Windows.UI.Core;
using Windows.UI.Xaml;

namespace RX_Explorer.Class
{
    public sealed class ViewHeightOffsetSaver : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private static ViewHeightOffsetSaver Instance;
        private static readonly object Locker = new object();

        public static ViewHeightOffsetSaver Current
        {
            get
            {
                lock (Locker)
                {
                    return Instance ??= new ViewHeightOffsetSaver();
                }
            }
        }

        public Thickness LineHeightOffset
        {
            get
            {
                double Offset = SettingPage.ViewHeightOffset;

                if (Offset >= 0)
                {
                    return new Thickness(0, Offset, 0, Offset);
                }
                else
                {
                    return new Thickness(0);
                }
            }
        }

        private ViewHeightOffsetSaver()
        {
            ApplicationData.Current.DataChanged += Current_DataChanged;
        }

        private async void Current_DataChanged(ApplicationData sender, object args)
        {
            try
            {
                await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    OnPropertyChanged(nameof(LineHeightOffset));
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
    }
}
