using System;

namespace RX_Explorer.Class
{
    public class OperationListRemoteModel : OperationListCopyModel
    {
        public OperationListRemoteModel(string ToPath, EventHandler OnCompleted = null) : base(Array.Empty<string>(), ToPath, OnCompleted)
        {

        }
    }
}
