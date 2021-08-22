using Microsoft.Toolkit.Deferred;

namespace RX_Explorer.Class
{
    public sealed class PostProcessingDeferredEventArgs : DeferredEventArgs
    {
        public string OriginPath { get; }

        public PostProcessingDeferredEventArgs(string OriginPath)
        {
            this.OriginPath = OriginPath;
        }
    }
}
