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

        public override string[] FromPath { get; }

        public override string ToPath { get; }

        public OperationListMoveModel(string[] FromPath, string ToPath, EventHandler OnCompleted = null) : base(OnCompleted)
        {
            this.FromPath = FromPath;
            this.ToPath = ToPath;
        }
    }
}
