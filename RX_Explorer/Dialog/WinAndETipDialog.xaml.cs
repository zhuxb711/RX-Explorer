using RX_Explorer.Class;
using System;
using Windows.UI.Xaml;

namespace RX_Explorer.Dialog
{
    public sealed partial class WinAndETipDialog : QueueContentDialog
    {
        private DispatcherTimer Timer;
        private short TickCount = 5;

        public WinAndETipDialog()
        {
            InitializeComponent();
            Loaded += WinAndETipDialog_Loaded;
        }

        private void WinAndETipDialog_Loaded(object sender, RoutedEventArgs e)
        {
            PrimaryButtonText = $"{Globalization.GetString("Common_Dialog_ContinueButton")} ({TickCount}s)";

            Timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(1000)
            };
            Timer.Tick += Timer_Tick;
            Timer.Start();
        }

        private void Timer_Tick(object sender, object e)
        {
            if (TickCount > 1)
            {
                PrimaryButtonText = $"{Globalization.GetString("Common_Dialog_ContinueButton")} ({--TickCount}s)";
            }
            else
            {
                Timer.Tick -= Timer_Tick;
                Timer.Stop();

                PrimaryButtonText = Globalization.GetString("Common_Dialog_ContinueButton");
                IsPrimaryButtonEnabled = true;
            }
        }

        private void QueueContentDialog_ButtonClick(Windows.UI.Xaml.Controls.ContentDialog sender, Windows.UI.Xaml.Controls.ContentDialogButtonClickEventArgs args)
        {
            Timer.Tick -= Timer_Tick;
            Timer.Stop();
        }
    }
}
