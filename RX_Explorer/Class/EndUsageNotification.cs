using System;

namespace RX_Explorer.Class
{
    public sealed class EndUsageNotification : IDisposable
    {
        private readonly Action ActionOnDispose;

        public EndUsageNotification(Action ActionOnDispose)
        {
            this.ActionOnDispose = ActionOnDispose;
        }

        public EndUsageNotification()
        {

        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
            ActionOnDispose?.Invoke();
        }

        ~EndUsageNotification()
        {
            Dispose();
        }
    }
}
