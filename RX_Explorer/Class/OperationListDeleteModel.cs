using System;

namespace RX_Explorer.Class
{
    public class OperationListDeleteModel : OperationListBaseModel
    {
        public bool IsPermanentDelete { get; }

        public override string OperationKindText
        {
            get
            {
                return Globalization.GetString("TaskList_OperationKind_Delete");
            }
        }

        public override string[] FromPath { get; }

        public override string ToPath { get; }

        public override string ToPathText
        {
            get
            {
                return string.Empty;
            }
        }

        public OperationListDeleteModel(string[] DeleteFrom, bool IsPermanentDelete, EventHandler OnCompleted = null) : base(OnCompleted)
        {
            FromPath = DeleteFrom;
            this.IsPermanentDelete = IsPermanentDelete;
        }
    }
}
