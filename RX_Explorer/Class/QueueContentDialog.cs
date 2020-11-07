using System;
using System.Threading;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace RX_Explorer.Class
{
    /// <summary>
    /// 提供ContentDialog队列(按序)弹出的实现
    /// </summary>
    public class QueueContentDialog : ContentDialog
    {
        private static readonly SemaphoreSlim Locker = new SemaphoreSlim(1, 1);

        private static volatile int WaitCount;

        /// <summary>
        /// 指示当前是否存在正处于弹出状态的ContentDialog
        /// </summary>
        public static bool IsRunningOrWaiting => WaitCount != 0;

        /// <summary>
        /// 显示对话框
        /// </summary>
        /// <returns></returns>
        public new async Task<ContentDialogResult> ShowAsync()
        {
            try
            {
                _ = Interlocked.Increment(ref WaitCount);

                await Locker.WaitAsync().ConfigureAwait(true);

                return await base.ShowAsync();
            }
            finally
            {
                Locker.Release();
                _ = Interlocked.Decrement(ref WaitCount);
            }
        }

        /// <summary>
        /// 初始化QueueContentDialog
        /// </summary>
        public QueueContentDialog()
        {
            DefaultButton = ContentDialogButton.Primary;

            if (AppThemeController.Current.Theme == ElementTheme.Dark)
            {
                Background = Application.Current.Resources["DialogAcrylicBrush"] as Brush;
                RequestedTheme = ElementTheme.Dark;
            }
            else
            {
                RequestedTheme = ElementTheme.Light;
            }
        }
    }
}
