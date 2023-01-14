using System;
using System.Threading;
using System.Threading.Tasks;

namespace RX_Explorer.Class
{
    public sealed class OperationListRenameUndoModel : OperationListUndoModel
    {
        public override string FromDescription => $"{Globalization.GetString("TaskList_From_Label")}: {Environment.NewLine}{UndoFrom}";

        public override string ToDescription => $"{Globalization.GetString("TaskList_To_Label")}: {Environment.NewLine}{UndoTo}";

        public string UndoFrom { get; }

        public string UndoTo { get; }

        public override bool CanBeCancelled => false;

        protected override Task<ProgressCalculator> PrepareSizeDataCoreAsync(CancellationToken Token = default)
        {
            return Task.FromResult(new ProgressCalculator(0));
        }

        public OperationListRenameUndoModel(string UndoFrom, string UndoTo)
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
