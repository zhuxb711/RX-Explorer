using Microsoft.Toolkit.Deferred;

namespace RX_Explorer.Class
{
    public sealed class PostProcessingDeferredEventArgs : DeferredEventArgs
    {
        public OperationStatus Status { get; }

        public object Parameter { get; }

        public PostProcessingDeferredEventArgs(OperationStatus Status, object Parameter)
        {
            this.Status = Status;
            this.Parameter = Parameter;
        }
    }
}
