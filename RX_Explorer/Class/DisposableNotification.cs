using System;

namespace RX_Explorer.Class
{
    public sealed class DisposableNotification : IDisposable
    {
        private readonly Action ActionOnDispose;

        public DisposableNotification(Action ActionOnDispose)
        {
            this.ActionOnDispose = ActionOnDispose;
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
            ActionOnDispose?.Invoke();
        }

        ~DisposableNotification()
        {
            Dispose();
        }
    }
}
