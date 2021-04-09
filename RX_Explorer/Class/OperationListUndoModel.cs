using System;

namespace RX_Explorer.Class
{
    public class OperationListUndoModel : OperationListBaseModel
    {
        public override string OperationKindText
        {
            get
            {
                return Globalization.GetString("TaskList_OperationKind_Undo");
            }
        }

        public OperationKind UndoOperationKind { get; }

        public OperationListUndoModel(OperationKind UndoOperationKind, string[] FromPath, string ToPath = null, EventHandler OnCompleted = null) : base(FromPath, ToPath, OnCompleted)
        {
            if (UndoOperationKind == OperationKind.Move && string.IsNullOrWhiteSpace(ToPath))
            {
                throw new ArgumentNullException(nameof(ToPath), $"Argument could not be null if {nameof(UndoOperationKind)} is Move");
            }

            this.UndoOperationKind = UndoOperationKind;
        }
    }
}
