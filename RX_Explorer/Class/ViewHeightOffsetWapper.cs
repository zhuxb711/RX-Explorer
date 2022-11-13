using PropertyChanged;
using RX_Explorer.View;
using System;
using Windows.ApplicationModel.Core;
using Windows.Storage;
using Windows.UI.Core;
using Windows.UI.Xaml;

namespace RX_Explorer.Class
{
    [AddINotifyPropertyChangedInterface]
    public sealed partial class ViewHeightOffsetWapper
    {
        private static ViewHeightOffsetWapper Instance;
        private static readonly object Locker = new object();

        public static ViewHeightOffsetWapper Current
        {
            get
            {
                lock (Locker)
                {
                    return Instance ??= new ViewHeightOffsetWapper();
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

        private ViewHeightOffsetWapper()
        {
            ApplicationData.Current.DataChanged += Current_DataChanged;
        }

        private async void Current_DataChanged(ApplicationData sender, object args)
        {
            try
            {
                await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                {
                    OnPropertyChanged(nameof(LineHeightOffset));
                });
            }
            catch (Exception)
            {
                //No need to handle this exception
            }
        }
    }
}
