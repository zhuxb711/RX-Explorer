using System;
using System.Threading;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace RX_Explorer.Class
{
    public class QueueContentDialog : ContentDialog
    {
        private static readonly SemaphoreSlim Locker = new SemaphoreSlim(1, 1);

        public static bool IsRunningOrWaiting => Locker.CurrentCount == 0;

        public new async Task<ContentDialogResult> ShowAsync()
        {
            try
            {
                await Locker.WaitAsync();
                return await base.ShowAsync();
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, "Could not pop up the ContentDialog");
                return ContentDialogResult.None;
            }
            finally
            {
                Locker.Release();
            }
        }

        public QueueContentDialog()
        {
            DefaultButton = ContentDialogButton.Primary;

            RequestedTheme = AppThemeController.Current.Theme;
            Background = Application.Current.Resources["DialogAcrylicBrush"] as Brush;

            Opened += QueueContentDialog_Opened;
            Closed += QueueContentDialog_Closed;
        }

        private void QueueContentDialog_Opened(ContentDialog sender, ContentDialogOpenedEventArgs args)
        {
            AppThemeController.Current.ThemeChanged += Current_ThemeChanged;
        }

        private void QueueContentDialog_Closed(ContentDialog sender, ContentDialogClosedEventArgs args)
        {
            AppThemeController.Current.ThemeChanged -= Current_ThemeChanged;
        }

        private void Current_ThemeChanged(object sender, ElementTheme newTheme)
        {
            RequestedTheme = newTheme;
            Background = Application.Current.Resources["DialogAcrylicBrush"] as Brush;
        }
    }
}
