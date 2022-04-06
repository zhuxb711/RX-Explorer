using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace RX_Explorer.Class
{
    public sealed class OperationListCopyUndoModel : OperationListUndoModel
    {
        public override string FromDescription
        {
            get
            {
                if (UndoFrom.Length > 5)
                {
                    return $"{Globalization.GetString("TaskList_From_Label")}: {Environment.NewLine}{string.Join(Environment.NewLine, UndoFrom.Take(5))}{Environment.NewLine}({UndoFrom.Length - 5} {Globalization.GetString("TaskList_More_Items")})...";
                }
                else
                {
                    return $"{Globalization.GetString("TaskList_From_Label")}: {Environment.NewLine}{string.Join(Environment.NewLine, UndoFrom)}";
                }
            }
        }

        public override string ToDescription => string.Empty;

        public string[] UndoFrom { get; }

        public override bool CanBeCancelled => true;

        protected override async Task<ProgressCalculator> PrepareSizeDataCoreAsync(CancellationToken Token)
        {
            ulong TotalSize = 0;

            await foreach (FileSystemStorageItemBase Item in FileSystemStorageItemBase.OpenInBatchAsync(UndoFrom, Token))
            {
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

            if (Token.IsCancellationRequested)
            {
                return null;
            }
            else
            {
                return new ProgressCalculator(TotalSize);
            }
        }

        public OperationListCopyUndoModel(string[] UndoFrom)
        {
            if (UndoFrom.Any((Path) => string.IsNullOrWhiteSpace(Path)))
            {
                throw new ArgumentNullException(nameof(UndoFrom), "Parameter could not be empty or null");
            }

            this.UndoFrom = UndoFrom;
        }
    }
}
