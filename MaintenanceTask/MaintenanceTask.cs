using System;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.Background;
using Windows.Storage;

namespace MaintenanceTask
{
    public sealed class MaintenanceTask : IBackgroundTask
    {
        private CancellationTokenSource Cancellation = new CancellationTokenSource();

        public async void Run(IBackgroundTaskInstance taskInstance)
        {
            BackgroundTaskDeferral Deferral = taskInstance.GetDeferral();
            taskInstance.Canceled += TaskInstance_Canceled;

            try
            {
                await ClearUselessLogTask(Cancellation.Token);

                //To-Do: Do more things as needed when users update the app
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"An exception threw in MaintenanceTask, message: {ex.Message}");
            }
            finally
            {
                taskInstance.Canceled -= TaskInstance_Canceled;
                Cancellation?.Dispose();
                Cancellation = null;
                Deferral.Complete();
            }
        }

        private async Task ClearUselessLogTask(CancellationToken CancelToken = default)
        {
            foreach (StorageFile File in from StorageFile File in await ApplicationData.Current.TemporaryFolder.GetFilesAsync()
                                         let Mat = Regex.Match(File.Name, @"(?<=\[)(.+)(?=\])")
                                         where Mat.Success && DateTime.TryParseExact(Mat.Value, "yyyy-MM-dd HH-mm-ss.fff", CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out _)
                                         select File)
            {
                await File.DeleteAsync(StorageDeleteOption.PermanentDelete);

                if (CancelToken.IsCancellationRequested)
                {
                    break;
                }
            }
        }

        private void TaskInstance_Canceled(IBackgroundTaskInstance sender, BackgroundTaskCancellationReason reason)
        {
            Cancellation?.Cancel();
        }
    }
}
