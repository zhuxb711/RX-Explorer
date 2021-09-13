using System;

namespace RX_Explorer.Class
{
    public class OperationListRemoteModel : OperationListCopyModel
    {
        public override bool CanBeCancelled => true;

        public OperationListRemoteModel(string ToPath, EventHandler OnCompleted = null, EventHandler OnErrorHappended = null, EventHandler OnCancelled = null) : base(Array.Empty<string>(), ToPath, OnCompleted, OnErrorHappended, OnCancelled)
        {

        }
    }
}
