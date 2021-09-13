using System;
using System.Collections.Generic;
using System.Linq;
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

        public override string ToDescription
        {
            get
            {
                return $"{Globalization.GetString("TaskList_To_Label")}: {Environment.NewLine}{UndoTo}";
            }
        }

        public Dictionary<string, string> UndoFrom { get; }

        public string UndoTo { get; }

        public override bool CanBeCancelled => true;

        public override async Task PrepareSizeDataAsync()
        {
            ulong TotalSize = 0;

            foreach (FileSystemStorageItemBase Item in await FileSystemStorageItemBase.OpenInBatchAsync(UndoFrom.Keys))
            {
                switch (Item)
                {
                    case FileSystemStorageFolder Folder:
                        {
                            TotalSize += await Folder.GetFolderSizeAsync();
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

        public OperationListMoveUndoModel(Dictionary<string, string> UndoFrom, string UndoTo, EventHandler OnCompleted = null, EventHandler OnErrorHappended = null, EventHandler OnCancelled = null) : base(OnCompleted, OnErrorHappended, OnCancelled)
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
