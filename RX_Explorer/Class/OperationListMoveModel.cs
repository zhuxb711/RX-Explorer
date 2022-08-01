using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;

namespace RX_Explorer.Class
{
    public class OperationListMoveModel : OperationListBaseModel
    {
        public override string OperationKindText => Globalization.GetString("TaskList_OperationKind_Move");

        public override string FromDescription
        {
            get
            {
                string[] DisplayItems = MoveFrom.Where((Item) => !(Path.GetDirectoryName(Item)?.StartsWith(ApplicationData.Current.TemporaryFolder.Path, StringComparison.OrdinalIgnoreCase)).GetValueOrDefault()).ToArray();

                return DisplayItems.Length switch
                {
                    > 5 => $"{Globalization.GetString("TaskList_From_Label")}: {Environment.NewLine}{string.Join(Environment.NewLine, DisplayItems.Take(5))}{Environment.NewLine}({DisplayItems.Length - 5} {Globalization.GetString("TaskList_More_Items")})...",
                    > 0 => $"{Globalization.GetString("TaskList_From_Label")}: {Environment.NewLine}{string.Join(Environment.NewLine, DisplayItems)}",
                    _ => string.Empty
                };
            }
        }

        public override string ToDescription => $"{Globalization.GetString("TaskList_To_Label")}: {Environment.NewLine}{MoveTo}";

        public string[] MoveFrom { get; }

        public string MoveTo { get; }

        public override bool CanBeCancelled => true;

        protected override async Task<ProgressCalculator> PrepareSizeDataCoreAsync(CancellationToken Token)
        {
            ulong TotalSize = 0;

            await foreach (FileSystemStorageItemBase Item in FileSystemStorageItemBase.OpenInBatchAsync(MoveFrom, Token))
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

        public OperationListMoveModel(string[] MoveFrom, string MoveTo)
        {
            if (string.IsNullOrWhiteSpace(MoveTo))
            {
                throw new ArgumentNullException(nameof(MoveTo), "Parameter could not be empty or null");
            }

            if (MoveFrom.Any((Path) => string.IsNullOrWhiteSpace(Path)))
            {
                throw new ArgumentNullException(nameof(MoveFrom), "Parameter could not be empty or null");
            }

            this.MoveFrom = MoveFrom;
            this.MoveTo = MoveTo;
        }
    }
}
