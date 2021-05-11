using System;
using System.Linq;

namespace RX_Explorer.Class
{
    public class OperationListUndoModel : OperationListBaseModel
    {
        public override string OperationKindText
        {
            get
            {
                return Globalization.GetString("TaskList_OperationKind_Undo");
            }
        }

        public override string FromPathText
        {
            get
            {
                return UndoOperationKind switch
                {
                    OperationKind.Delete => string.Empty,
                    _ => base.FromPathText
                };
            }
        }

        public override string ToPathText
        {
            get
            {
                switch (UndoOperationKind)
                {
                    case OperationKind.Delete:
                        {
                            if (FromPath.Length > 5)
                            {
                                return $"{Globalization.GetString("TaskList_To_Label")}: {Environment.NewLine}{string.Join(Environment.NewLine, FromPath.Take(5))}{Environment.NewLine}({FromPath.Length - 5} {Globalization.GetString("TaskList_More_Items")})...";
                            }
                            else
                            {
                                return $"{Globalization.GetString("TaskList_To_Label")}: {Environment.NewLine}{string.Join(Environment.NewLine, FromPath)}";
                            }
                        }
                    case OperationKind.Copy:
                        {
                            return string.Empty;
                        }
                    default:
                        {
                            return base.ToPathText;
                        }
                }
            }
        }

        public OperationKind UndoOperationKind { get; }

        public OperationListUndoModel(OperationKind UndoOperationKind, string[] FromPath, string ToPath = null, EventHandler OnCompleted = null, EventHandler OnErrorHappended = null, EventHandler OnCancelled = null) : base(FromPath, ToPath, OnCompleted, OnErrorHappended, OnCancelled)
        {
            if (UndoOperationKind == OperationKind.Move && string.IsNullOrWhiteSpace(ToPath))
            {
                throw new ArgumentNullException(nameof(ToPath), $"Argument could not be null if {nameof(UndoOperationKind)} is Move");
            }

            this.UndoOperationKind = UndoOperationKind;
        }
    }
}
