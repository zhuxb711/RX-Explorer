using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;

namespace RX_Explorer.Class
{
    public class OperationListCopyModel : OperationListBaseModel
    {
        public override string OperationKindText => Globalization.GetString("TaskList_OperationKind_Copy");

        public override string FromDescription
        {
            get
            {
                if (CopyFrom.All((Item) => !Path.GetDirectoryName(Item).StartsWith(ApplicationData.Current.TemporaryFolder.Path, StringComparison.OrdinalIgnoreCase)))
                {
                    return CopyFrom.Length switch
                    {
                        > 5 => $"{Globalization.GetString("TaskList_From_Label")}: {Environment.NewLine}{string.Join(Environment.NewLine, CopyFrom.Take(5))}{Environment.NewLine}({CopyFrom.Length - 5} {Globalization.GetString("TaskList_More_Items")})...",
                        > 0 => $"{Globalization.GetString("TaskList_From_Label")}: {Environment.NewLine}{string.Join(Environment.NewLine, CopyFrom)}",
                        _ => string.Empty
                    };
                }
                else
                {
                    return string.Empty;
                }
            }
        }

        public override string ToDescription => $"{Globalization.GetString("TaskList_To_Label")}: {Environment.NewLine}{CopyTo}";

        public string[] CopyFrom { get; }

        public string CopyTo { get; }

        public override bool CanBeCancelled => true;

        protected override async Task<ProgressCalculator> PrepareSizeDataCoreAsync(CancellationToken Token)
        {
            ulong TotalSize = 0;

            foreach (FileSystemStorageItemBase Item in await FileSystemStorageItemBase.OpenInBatchAsync(CopyFrom))
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

            return new ProgressCalculator(TotalSize);
        }

        public OperationListCopyModel(string[] CopyFrom, string CopyTo)
        {
            if (string.IsNullOrWhiteSpace(CopyTo))
            {
                throw new ArgumentNullException(nameof(CopyTo), "Parameter could not be empty or null");
            }

            if (CopyFrom.Any((Path) => string.IsNullOrWhiteSpace(Path)))
            {
                throw new ArgumentNullException(nameof(CopyFrom), "Parameter could not be empty or null");
            }

            this.CopyFrom = CopyFrom;
            this.CopyTo = CopyTo;
        }
    }
}
