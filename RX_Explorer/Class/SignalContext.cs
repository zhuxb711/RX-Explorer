using System;
using System.Threading;
using System.Threading.Tasks;

namespace RX_Explorer.Class
{
    public sealed class SignalContext
    {
        private bool IsCompleted;
        private TaskCompletionSource<object> WaitSource;
        private TaskCompletionSource<object> PauseSource;

        public async Task<IDisposable> SignalAndWaitTrappedAsync()
        {
            if (IsCompleted)
            {
                return DisposeNotification.Empty;
            }
            else
            {
                TaskCompletionSource<object> WaitSource = new TaskCompletionSource<object>();
                TaskCompletionSource<object> PauseSource = new TaskCompletionSource<object>();

                if (Interlocked.Exchange(ref this.PauseSource, PauseSource) is TaskCompletionSource<object> OldPauseSource)
                {
                    OldPauseSource.TrySetCanceled();
                }

                if (Interlocked.Exchange(ref this.WaitSource, WaitSource) is TaskCompletionSource<object> OldWaitSource)
                {
                    OldWaitSource.TrySetCanceled();
                }

                await PauseSource.Task;

                return new DisposeNotification(() =>
                {
                    WaitSource.TrySetResult(null);
                });
            }
        }

        public async Task TrapOnSignalAsync()
        {
            if (Interlocked.Exchange(ref this.WaitSource, null) is TaskCompletionSource<object> WaitSource)
            {
                if (Interlocked.Exchange(ref this.PauseSource, null) is TaskCompletionSource<object> PauseSource)
                {
                    PauseSource.TrySetResult(null);
                }

                await WaitSource.Task;
            }
        }

        public void MarkAsCompleted()
        {
            IsCompleted = true;
        }
    }
}
