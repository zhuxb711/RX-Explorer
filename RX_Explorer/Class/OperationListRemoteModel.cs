using System;

namespace RX_Explorer.Class
{
    public class OperationListRemoteModel : OperationListCopyModel
    {
        public override bool CanBeCancelled => true;

        public OperationListRemoteModel(string ToPath) : base(Array.Empty<string>(), ToPath)
        {

        }
    }
}
