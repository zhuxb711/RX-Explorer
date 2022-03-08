using System;
using System.Threading;
using System.Threading.Tasks;

namespace RX_Explorer.Class
{
    public sealed class OperationListNewUndoModel : OperationListUndoModel
    {
        public override string FromDescription => $"{Globalization.GetString("TaskList_From_Label")}: {Environment.NewLine}{UndoFrom}";

        public override string ToDescription => string.Empty;

        public string UndoFrom { get; }

        public override bool CanBeCancelled => false;

        protected override Task<ProgressCalculator> PrepareSizeDataCoreAsync(CancellationToken Token)
        {
            return Task.FromResult(new ProgressCalculator(0));
        }

        public OperationListNewUndoModel(string UndoFrom)
        {
            if (string.IsNullOrWhiteSpace(UndoFrom))
            {
                throw new ArgumentNullException(nameof(UndoFrom), "Parameter could not be empty or null");
            }

            this.UndoFrom = UndoFrom;
        }
    }
}
