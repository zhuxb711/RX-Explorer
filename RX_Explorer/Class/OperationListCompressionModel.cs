using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace RX_Explorer.Class
{
    public class OperationListCompressionModel : OperationListBaseModel
    {
        public override string OperationKindText => Globalization.GetString("TaskList_OperationKind_Compression");

        public override string FromDescription
        {
            get
            {
                if (CompressionFrom.Length > 5)
                {
                    return $"{Globalization.GetString("TaskList_From_Label")}: {Environment.NewLine}{string.Join(Environment.NewLine, CompressionFrom.Take(5))}{Environment.NewLine}({CompressionFrom.Length - 5} {Globalization.GetString("TaskList_More_Items")})...";
                }
                else
                {
                    return $"{Globalization.GetString("TaskList_From_Label")}: {Environment.NewLine}{string.Join(Environment.NewLine, CompressionFrom)}";
                }
            }
        }

        public override string ToDescription => $"{Globalization.GetString("TaskList_To_Label")}: {Environment.NewLine}{CompressionTo}";

        public CompressionType Type { get; }

        public CompressionAlgorithm Algorithm { get; }

        public CompressionLevel Level { get; }

        public string[] CompressionFrom { get; }

        public string CompressionTo { get; }

        public override bool CanBeCancelled => true;

        protected override async Task<ProgressCalculator> PrepareSizeDataCoreAsync(CancellationToken Token)
        {
            ulong TotalSize = 0;

            await foreach (FileSystemStorageItemBase Item in FileSystemStorageItemBase.OpenInBatchAsync(CompressionFrom, Token))
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

        public OperationListCompressionModel(CompressionType Type, CompressionAlgorithm Algorithm, CompressionLevel Level, string[] CompressionFrom, string CompressionTo)
        {
            if (string.IsNullOrWhiteSpace(CompressionTo))
            {
                throw new ArgumentNullException(nameof(CompressionTo), "Parameter could not be empty or null");
            }

            if (CompressionFrom.Any((Path) => string.IsNullOrWhiteSpace(Path)))
            {
                throw new ArgumentNullException(nameof(CompressionFrom), "Parameter could not be empty or null");
            }

            this.Type = Type;
            this.Algorithm = Algorithm;
            this.Level = Level;
            this.CompressionFrom = CompressionFrom;
            this.CompressionTo = CompressionTo;
        }
    }
}
