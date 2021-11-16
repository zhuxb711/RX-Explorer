using Microsoft.Toolkit.Deferred;

namespace RX_Explorer.Class
{
    public sealed class ProgressChangedDeferredArgs : DeferredEventArgs
    {
        public int ProgressValue { get; }

        public ProgressChangedDeferredArgs(int ProgressValue)
        {
            this.ProgressValue = ProgressValue;
        }
    }
}
