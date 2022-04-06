using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace RX_Explorer.Class
{
    public sealed class OperationListMoveUndoModel : OperationListUndoModel
    {
        public override string FromDescription
        {
            get
            {
                if (UndoFrom.Count > 5)
                {
                    return $"{Globalization.GetString("TaskList_From_Label")}: {Environment.NewLine}{string.Join(Environment.NewLine, UndoFrom.Keys.Take(5))}{Environment.NewLine}({UndoFrom.Count - 5} {Globalization.GetString("TaskList_More_Items")})...";
                }
                else
                {
                    return $"{Globalization.GetString("TaskList_From_Label")}: {Environment.NewLine}{string.Join(Environment.NewLine, UndoFrom.Keys)}";
                }
            }
        }

        public override string ToDescription => $"{Globalization.GetString("TaskList_To_Label")}: {Environment.NewLine}{UndoTo}";

        public Dictionary<string, string> UndoFrom { get; }

        public string UndoTo { get; }

        public override bool CanBeCancelled => true;

        protected override async Task<ProgressCalculator> PrepareSizeDataCoreAsync(CancellationToken Token)
        {
            ulong TotalSize = 0;

            await foreach (FileSystemStorageItemBase Item in FileSystemStorageItemBase.OpenInBatchAsync(UndoFrom.Keys, Token))
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

        public OperationListMoveUndoModel(Dictionary<string, string> UndoFrom, string UndoTo)
        {
            if (string.IsNullOrWhiteSpace(UndoTo))
            {
                throw new ArgumentNullException(nameof(UndoTo), "Parameter could not be empty or null");
            }

            if (UndoFrom.Keys.Any((Path) => string.IsNullOrWhiteSpace(Path)))
            {
                throw new ArgumentNullException(nameof(UndoFrom), "Parameter could not be empty or null");
            }

            this.UndoFrom = UndoFrom;
            this.UndoTo = UndoTo;
        }
    }
}
