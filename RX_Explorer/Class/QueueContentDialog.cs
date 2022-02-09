using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.Core;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace RX_Explorer.Class
{
    public class QueueContentDialog : ContentDialog
    {
        public static bool IsRunningOrWaiting => !Queue.IsEmpty || InternalRunningStatus;

        private static readonly ConcurrentQueue<QueueContentDialogInternalData> Queue = new ConcurrentQueue<QueueContentDialogInternalData>();

        private static readonly AutoResetEvent ProcessSleepLocker = new AutoResetEvent(false);

        private static readonly Thread BackgroundProcessThread = new Thread(ProcessThread)
        {
            IsBackground = true,
            Priority = ThreadPriority.BelowNormal
        };

        private static bool InternalRunningStatus = false;

        private static void ProcessThread()
        {
            while (true)
            {
                InternalRunningStatus = false;

                ProcessSleepLocker.WaitOne();

                InternalRunningStatus = true;

                while (Queue.TryDequeue(out QueueContentDialogInternalData Data))
                {
                    TaskCompletionSource<ContentDialogResult> CompleteSource = new TaskCompletionSource<ContentDialogResult>();

                    CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
                    {
                        try
                        {
                            CompleteSource.SetResult(await Data.Instance.ShowAsync());
                        }
                        catch (Exception ex)
                        {
                            LogTracer.Log(ex, "Could not pop up the ContentDialog");
                            CompleteSource.SetResult(ContentDialogResult.None);
                        }
                    }).AsTask().Wait();

                    Data.ResultSource.SetResult(CompleteSource.Task.Result);
                }
            }
        }

        public new Task<ContentDialogResult> ShowAsync()
        {
            TaskCompletionSource<ContentDialogResult> CompletionSource = new TaskCompletionSource<ContentDialogResult>();
            Queue.Enqueue(new QueueContentDialogInternalData(this, CompletionSource));
            ProcessSleepLocker.Set();
            return CompletionSource.Task;
        }

        static QueueContentDialog()
        {
            BackgroundProcessThread.Start();
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

        private class QueueContentDialogInternalData
        {
            public ContentDialog Instance { get; }

            public TaskCompletionSource<ContentDialogResult> ResultSource { get; }

            public QueueContentDialogInternalData(QueueContentDialog Instance, TaskCompletionSource<ContentDialogResult> ResultSource)
            {
                this.Instance = Instance;
                this.ResultSource = ResultSource;
            }
        }
    }
}
