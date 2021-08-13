using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RX_Explorer.Class
{
    public class OperationListCopyModel : OperationListBaseModel
    {
        public override string OperationKindText
        {
            get
            {
                return Globalization.GetString("TaskList_OperationKind_Copy");
            }
        }

        public override string FromDescription
        {
            get
            {
                if (CopyFrom.Length > 5)
                {
                    return $"{Globalization.GetString("TaskList_From_Label")}: {Environment.NewLine}{string.Join(Environment.NewLine, CopyFrom.Take(5))}{Environment.NewLine}({CopyFrom.Length - 5} {Globalization.GetString("TaskList_More_Items")})...";
                }
                else
                {
                    return $"{Globalization.GetString("TaskList_From_Label")}: {Environment.NewLine}{string.Join(Environment.NewLine, CopyFrom)}";
                }
            }
        }

        public override string ToDescription
        {
            get
            {
                return $"{Globalization.GetString("TaskList_To_Label")}: {Environment.NewLine}{CopyTo}";
            }
        }

        public string[] CopyFrom { get; }

        public string CopyTo { get; }

        public override async Task PrepareSizeDataAsync()
        {
            ulong TotalSize = 0;

            foreach (string Path in CopyFrom)
            {
                switch (await FileSystemStorageItemBase.OpenAsync(Path))
                {
                    case FileSystemStorageFolder Folder:
                        {
                            TotalSize += await Folder.GetFolderSizeAsync();
                            break;
                        }
                    case FileSystemStorageFile File:
                        {
                            TotalSize += File.SizeRaw;
                            break;
                        }
                }
            }

            Calculator = new ProgressCalculator(TotalSize);
        }

        public OperationListCopyModel(string[] CopyFrom, string CopyTo, EventHandler OnCompleted = null, EventHandler OnErrorHappended = null, EventHandler OnCancelled = null) : base(OnCompleted, OnErrorHappended, OnCancelled)
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
