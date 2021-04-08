using System;

namespace RX_Explorer.Class
{
    public class OperationListCopyModel : OperationListBaseModel
    {
        public override string OperationKindText
        {
            get
            {
                return Globalization.GetString("TaskList_OperationKind_Copy");
            }
        }

        public override string[] FromPath { get; }

        public override string ToPath { get; }

        public OperationListCopyModel(string[] FromPath, string ToPath, EventHandler OnCompleted = null) : base(OnCompleted)
        {
            this.FromPath = FromPath;
            this.ToPath = ToPath;
        }
    }
}
