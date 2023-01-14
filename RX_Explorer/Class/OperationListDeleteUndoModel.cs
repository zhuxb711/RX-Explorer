using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace RX_Explorer.Class
{
    public sealed class OperationListDeleteUndoModel : OperationListUndoModel
    {
        public override string FromDescription => string.Empty;

        public override string ToDescription
        {
            get
            {
                if (UndoFrom.Length > 5)
                {
                    return $"{Globalization.GetString("TaskList_To_Label")}: {Environment.NewLine}{string.Join(Environment.NewLine, UndoFrom.Take(5))}{Environment.NewLine}({UndoFrom.Length - 5} {Globalization.GetString("TaskList_More_Items")})...";
                }
                else
                {
                    return $"{Globalization.GetString("TaskList_To_Label")}: {Environment.NewLine}{string.Join(Environment.NewLine, UndoFrom)}";
                }
            }
        }

        public string[] UndoFrom { get; }

        public override bool CanBeCancelled => true;

        protected override async Task<ProgressCalculator> PrepareSizeDataCoreAsync(CancellationToken Token = default)
        {
            string[] UndoRecyclePath;

            using (AuxiliaryTrustProcessController.Exclusive Exclusive = await AuxiliaryTrustProcessController.GetControllerExclusiveAsync())
            {
                UndoRecyclePath = await Task.WhenAll(UndoFrom.Select((Path) => Exclusive.Controller.GetRecyclePathFromOriginPathAsync(Path)));
            }

            ulong TotalSize = 0;

            await foreach (FileSystemStorageItemBase Item in FileSystemStorageItemBase.OpenInBatchAsync(UndoRecyclePath, Token))
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

            return new ProgressCalculator(TotalSize);
        }

        public OperationListDeleteUndoModel(string[] UndoFrom)
        {
            if (UndoFrom.Any((Path) => string.IsNullOrWhiteSpace(Path)))
            {
                throw new ArgumentNullException(nameof(UndoFrom), "Parameter could not be empty or null");
            }

            this.UndoFrom = UndoFrom;
        }
    }
}
