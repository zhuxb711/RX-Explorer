using Microsoft.Toolkit.Deferred;

namespace RX_Explorer.Class
{
    public sealed class DriveChangeDeferredEventArgs: DeferredEventArgs
    {
        public DriveDataBase Data { get; }

        public DriveChangeDeferredEventArgs(DriveDataBase Data)
        {
            this.Data = Data;
        }
    }
}
