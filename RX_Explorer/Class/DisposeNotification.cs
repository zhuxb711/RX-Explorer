using System;

namespace RX_Explorer.Class
{
    public sealed class DisposeNotification : IDisposable
    {
        private readonly Action ActionOnDispose;

        public DisposeNotification(Action ActionOnDispose)
        {
            this.ActionOnDispose = ActionOnDispose;
        }

        public DisposeNotification()
        {

        }

        public void Dispose()
        {
            ActionOnDispose?.Invoke();
            GC.SuppressFinalize(this);
        }

        ~DisposeNotification()
        {
            Dispose();
        }
    }
}
