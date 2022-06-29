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
        public static bool IsRunningOrWaiting => DialogCollection.Count > 0 || InternalShowingDialogFlag;

        private static bool InternalShowingDialogFlag;

        private static readonly BlockingCollection<QueueContentDialogInternalData> DialogCollection = new BlockingCollection<QueueContentDialogInternalData>();

        private static readonly Thread BackgroundProcessThread = new Thread(ProcessThread)
        {
            IsBackground = true,
            Priority = ThreadPriority.BelowNormal
        };

        private static void ProcessThread()
        {
            while (true)
            {
                QueueContentDialogInternalData Data = DialogCollection.Take();

                InternalShowingDialogFlag = true;

                try
                {
                    CoreApplication.MainView.CoreWindow.Dispatcher.RunAndWaitAsyncTask(CoreDispatcherPriority.Normal, async () =>
                    {
                        Data.TaskSource.SetResult(await Data.Instance.ShowAsync());
                    }).Wait();
                }
                catch (Exception ex)
                {
                    Data.TaskSource.SetResult(ContentDialogResult.None);
                    LogTracer.Log(ex, "Could not pop the ContentDialog as expected");
                }
                finally
                {
                    InternalShowingDialogFlag = false;
                }
            }
        }

        public new Task<ContentDialogResult> ShowAsync()
        {
            QueueContentDialogInternalData Data = new QueueContentDialogInternalData(this);
            DialogCollection.Add(Data);
            return Data.TaskSource.Task;
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

            public TaskCompletionSource<ContentDialogResult> TaskSource { get; } = new TaskCompletionSource<ContentDialogResult>();

            public QueueContentDialogInternalData(QueueContentDialog Instance)
            {
                this.Instance = Instance;
            }
        }
    }
}
