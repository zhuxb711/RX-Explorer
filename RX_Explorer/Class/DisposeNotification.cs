using System;
using static RX_Explorer.Class.AuxiliaryTrustProcessController;

namespace RX_Explorer.Class
{
    public sealed class DisposeNotification : IDisposable
    {
        private readonly Action ActionOnDispose;

        public static DisposeNotification Empty { get; } = new DisposeNotification();

        public DisposeNotification(Action ActionOnDispose)
        {
            this.ActionOnDispose = ActionOnDispose;
        }

        private DisposeNotification()
        {

        }

        public void Dispose()
        {
            if (Execution.CheckAlreadyExecuted(this))
            {
                throw new ObjectDisposedException(nameof(DisposeNotification));
            }

            GC.SuppressFinalize(this);

            Execution.ExecuteOnce(this, () =>
            {
                ActionOnDispose?.Invoke();
            });
        }

        ~DisposeNotification()
        {
            Dispose();
        }
    }
}
