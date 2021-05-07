using System;

namespace RX_Explorer.Class
{
    public class OperationListMoveModel : OperationListBaseModel
    {
        public override string OperationKindText
        {
            get
            {
                return Globalization.GetString("TaskList_OperationKind_Move");
            }
        }

        public OperationListMoveModel(string[] FromPath, string ToPath, EventHandler OnCompleted = null, EventHandler OnErrorHappended = null, EventHandler OnCancelled = null) : base(FromPath, ToPath, OnCompleted, OnErrorHappended, OnCancelled)
        {

        }
    }
}
