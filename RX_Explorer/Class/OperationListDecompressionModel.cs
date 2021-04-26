using System;
using System.Text;

namespace RX_Explorer.Class
{
    public class OperationListDecompressionModel : OperationListBaseModel
    {
        public override string OperationKindText
        {
            get
            {
                return Globalization.GetString("TaskList_OperationKind_Decompression");
            }
        }

        public Encoding Encoding { get; }
        public bool CreateFolder { get; }

        public OperationListDecompressionModel(string[] FromPath, string ToPath,bool CreateFolder, Encoding Encoding = null, EventHandler OnCompleted = null) : base(FromPath, ToPath, OnCompleted)
        {
            this.Encoding = Encoding ?? System.Text.Encoding.Default;
            this.CreateFolder = CreateFolder;
        }
    }
}
