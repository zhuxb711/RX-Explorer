using System;

namespace RX_Explorer.Class
{
    public abstract class OperationListUndoModel : OperationListBaseModel
    {
        public override string OperationKindText => Globalization.GetString("TaskList_OperationKind_Undo");
    }
}
