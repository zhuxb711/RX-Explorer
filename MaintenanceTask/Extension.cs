using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;

namespace MaintenanceTask
{
    internal static class Extension
    {
        public static Task AsCancellable(this Task Instance, CancellationToken CancelToken)
        {
            return Instance.ContinueWith((PreviousTask) =>
            {
                if (PreviousTask.Exception is Exception Ex)
                {
                    ExceptionDispatchInfo.Throw(Ex);
                }

                return new object();
            }, TaskContinuationOptions.ExecuteSynchronously).AsCancellable(CancelToken);
        }

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

        public static IReadOnlyList<T> DuplicateAndClear<T>(this ICollection<T> Source)
        {
            try
            {
                return Source.ToArray();
            }
            finally
            {
                Source.Clear();
            }
        }
    }
}
