using Microsoft.Toolkit.Deferred;

namespace RX_Explorer.Class
{
    public sealed class DriveChangeDeferredEventArgs: DeferredEventArgs
    {
        public DriveRelatedData Data { get; }

        public DriveChangeDeferredEventArgs(DriveRelatedData Data)
        {
            this.Data = Data;
        }
    }
}
