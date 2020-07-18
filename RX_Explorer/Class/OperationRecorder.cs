using System.Collections.Generic;

namespace RX_Explorer.Class
{
    public sealed class OperationRecorder
    {
        public Stack<string> Value { get; private set; }

        private static readonly object Locker = new object();

        private static OperationRecorder Instance;

        public static OperationRecorder Current
        {
            get
            {
                lock (Locker)
                {
                    return Instance ??= new OperationRecorder();
                }
            }
        }

        private OperationRecorder()
        {
            Value = new Stack<string>();
        }
    }
}
