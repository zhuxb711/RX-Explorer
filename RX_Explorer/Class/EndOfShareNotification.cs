using System;

namespace RX_Explorer.Class
{
    public sealed class EndOfShareNotification : IDisposable
    {
        private readonly Action ActionOnDispose;

        public EndOfShareNotification(Action ActionOnDispose)
        {
            this.ActionOnDispose = ActionOnDispose;
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
            ActionOnDispose?.Invoke();
        }

        ~EndOfShareNotification()
        {
            Dispose();
        }
    }
}
