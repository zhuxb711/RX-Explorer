using System.Threading;
using System.Threading.Tasks;

namespace UpdateCheckBackgroundTask
{
    internal static class Extension
    {
        public static async Task<T> AsCancellable<T>(this Task<T> Instance, CancellationToken CancelToken)
        {
            if (!CancelToken.CanBeCanceled)
            {
                return await Instance;
            }

            TaskCompletionSource<T> TCS = new TaskCompletionSource<T>();

            using (CancellationTokenRegistration CancelRegistration = CancelToken.Register(() => TCS.TrySetCanceled(CancelToken), false))
            {
                _ = Instance.ContinueWith((PreviousTask) =>
                {
                    CancelRegistration.Dispose();

                    if (Instance.IsCanceled)
                    {
                        TCS.TrySetCanceled();
                    }
                    else if (Instance.IsFaulted)
                    {
                        TCS.TrySetException(PreviousTask.Exception);
                    }
                    else
                    {
                        TCS.TrySetResult(PreviousTask.Result);
                    }
                }, TaskContinuationOptions.ExecuteSynchronously);

                return await TCS.Task;
            }
        }
    }
}
