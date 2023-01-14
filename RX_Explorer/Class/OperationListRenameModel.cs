using System;
using System.Threading;
using System.Threading.Tasks;

namespace RX_Explorer.Class
{
    public class OperationListRenameModel : OperationListBaseModel
    {
        public override string OperationKindText => Globalization.GetString("TaskList_OperationKind_Rename");

        public override string FromDescription => $"{Globalization.GetString("TaskList_From_Label")}: {Environment.NewLine}{RenameFrom}";

        public override string ToDescription => $"{Globalization.GetString("TaskList_To_Label")}: {Environment.NewLine}{RenameTo}";

        public string RenameFrom { get; }

        public string RenameTo { get; }

        public override bool CanBeCancelled => false;

        protected override Task<ProgressCalculator> PrepareSizeDataCoreAsync(CancellationToken Token = default)
        {
            return Task.FromResult(new ProgressCalculator(0));
        }

        public OperationListRenameModel(string RenameFrom, string RenameTo)
        {
            if (string.IsNullOrWhiteSpace(RenameTo))
            {
                throw new ArgumentNullException(nameof(RenameTo), "Parameter could not be empty or null");
            }

            if (string.IsNullOrWhiteSpace(RenameFrom))
            {
                throw new ArgumentNullException(nameof(RenameFrom), "Parameter could not be empty or null");
            }

            this.RenameFrom = RenameFrom;
            this.RenameTo = RenameTo;
        }
    }
}
