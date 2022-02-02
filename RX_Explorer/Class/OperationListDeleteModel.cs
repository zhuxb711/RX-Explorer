using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace RX_Explorer.Class
{
    public class OperationListDeleteModel : OperationListBaseModel
    {
        public bool IsPermanentDelete { get; }

        public override string OperationKindText => Globalization.GetString("TaskList_OperationKind_Delete");

        public override string FromDescription
        {
            get
            {
                if (DeleteFrom.Length > 5)
                {
                    return $"{Globalization.GetString("TaskList_From_Label")}: {Environment.NewLine}{string.Join(Environment.NewLine, DeleteFrom.Take(5))}{Environment.NewLine}({DeleteFrom.Length - 5} {Globalization.GetString("TaskList_More_Items")})...";
                }
                else
                {
                    return $"{Globalization.GetString("TaskList_From_Label")}: {Environment.NewLine}{string.Join(Environment.NewLine, DeleteFrom)}";
                }
            }
        }

        public override string ToDescription => string.Empty;

        public string[] DeleteFrom { get; }

        public override bool CanBeCancelled => true;

        public override async Task PrepareSizeDataAsync(CancellationToken Token)
        {
            ulong TotalSize = 0;

            foreach (FileSystemStorageItemBase Item in await FileSystemStorageItemBase.OpenInBatchAsync(DeleteFrom))
            {
                if (Token.IsCancellationRequested)
                {
                    break;
                }

                switch (Item)
                {
                    case FileSystemStorageFolder Folder:
                        {
                            TotalSize += await Folder.GetFolderSizeAsync(Token);
                            break;
                        }
                    case FileSystemStorageFile File:
                        {
                            TotalSize += File.Size;
                            break;
                        }
                }
            }

            Calculator = new ProgressCalculator(TotalSize);
        }

        public OperationListDeleteModel(string[] DeleteFrom, bool IsPermanentDelete, EventHandler OnCompleted = null, EventHandler OnErrorThrow = null, EventHandler OnCancelled = null) : base(OnCompleted, OnErrorThrow, OnCancelled)
        {
            if (DeleteFrom.Any((Path) => string.IsNullOrWhiteSpace(Path)))
            {
                throw new ArgumentNullException(nameof(DeleteFrom), "Parameter could not be empty or null");
            }

            this.DeleteFrom = DeleteFrom;
            this.IsPermanentDelete = IsPermanentDelete;
        }
    }
}
