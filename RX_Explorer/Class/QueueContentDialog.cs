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
            foreach (QueueContentDialogInternalData Data in DialogCollection.GetConsumingEnumerable())
            {
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
                    Data.TaskSource.TrySetException(ex);
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

        protected QueueContentDialog()
        {
            XamlRoot = (Window.Current.Content as FrameworkElement)?.XamlRoot;
            DefaultButton = ContentDialogButton.Primary;
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
