using System;
using System.Threading.Tasks;

namespace RX_Explorer.Class
{
    public sealed class OperationListRenameUndoModel : OperationListUndoModel
    {
        public override string FromDescription
        {
            get
            {
                return $"{Globalization.GetString("TaskList_From_Label")}: {Environment.NewLine}{UndoFrom}";
            }
        }

        public override string ToDescription
        {
            get
            {
                return $"{Globalization.GetString("TaskList_To_Label")}: {Environment.NewLine}{UndoTo}";
            }
        }

        public string UndoFrom { get; }

        public string UndoTo { get; }

        public override bool CanBeCancelled => false;

        public override Task PrepareSizeDataAsync()
        {
            Calculator = new ProgressCalculator(0);
            return Task.CompletedTask;
        }

        public OperationListRenameUndoModel(string UndoFrom, string UndoTo, EventHandler OnCompleted = null, EventHandler OnErrorHappended = null, EventHandler OnCancelled = null) : base(OnCompleted, OnErrorHappended, OnCancelled)
        {
            if (string.IsNullOrWhiteSpace(UndoTo))
            {
                throw new ArgumentNullException(nameof(UndoTo), "Parameter could not be empty or null");
            }

            if (string.IsNullOrWhiteSpace(UndoFrom))
            {
                throw new ArgumentNullException(nameof(UndoFrom), "Parameter could not be empty or null");
            }

            this.UndoFrom = UndoFrom;
            this.UndoTo = UndoTo;
        }
    }
}
