using System;
using System.Threading;
using System.Threading.Tasks;

namespace RX_Explorer.Class
{
    public sealed class SignalContext
    {
        private TaskCompletionSource<bool> WaitSource;
        private TaskCompletionSource<bool> PauseSource;
        private bool IsCompleted;

        public async Task<IDisposable> SignalAndWaitTrappedAsync()
        {
            if (IsCompleted)
            {
                return new DisposeNotification();
            }
            else
            {
                TaskCompletionSource<bool> WaitSource = new TaskCompletionSource<bool>();
                TaskCompletionSource<bool> PauseSource = new TaskCompletionSource<bool>();

                if (Interlocked.Exchange(ref this.PauseSource, PauseSource) is TaskCompletionSource<bool> OldPauseSource)
                {
                    OldPauseSource.TrySetCanceled();
                }

                if (Interlocked.Exchange(ref this.WaitSource, WaitSource) is TaskCompletionSource<bool> OldWaitSource)
                {
                    OldWaitSource.TrySetCanceled();
                }

                await PauseSource.Task;

                return new DisposeNotification(() =>
                {
                    WaitSource.TrySetResult(true);
                });
            }
        }

        public async Task TrapOnSignalAsync()
        {
            if (Interlocked.Exchange(ref this.WaitSource, null) is TaskCompletionSource<bool> WaitSource)
            {
                if (Interlocked.Exchange(ref this.PauseSource, null) is TaskCompletionSource<bool> PauseSource)
                {
                    PauseSource.TrySetResult(true);
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
