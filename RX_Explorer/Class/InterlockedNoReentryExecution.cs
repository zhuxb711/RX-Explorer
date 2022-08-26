using System;
using System.Threading;
using System.Threading.Tasks;

namespace RX_Explorer.Class
{
    public sealed class InterlockedNoReentryExecution
    {
        private int Locker = 0;

        public void Execute(Action Method)
        {
            if (Interlocked.Exchange(ref Locker, 1) == 0)
            {
                try
                {
                    Method();
                }
                finally
                {
                    Interlocked.Exchange(ref Locker, 0);
                }
            }
        }

        public async Task ExecuteAsync(Func<Task> Method)
        {
            if (Interlocked.Exchange(ref Locker, 1) == 0)
            {
                try
                {
                    await Method();
                }
                finally
                {
                    Interlocked.Exchange(ref Locker, 0);
                }
            }
        }
    }
}
