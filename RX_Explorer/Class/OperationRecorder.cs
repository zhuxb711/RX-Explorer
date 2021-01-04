using System.Collections.Generic;
using System.Linq;

namespace RX_Explorer.Class
{
    public sealed class OperationRecorder
    {
        private readonly Stack<List<string>> Container;

        private static readonly object Locker = new object();

        private static OperationRecorder Instance;

        public void Push(List<string> InputList)
        {
            lock (Locker)
            {
                List<string> FilterList = new List<string>();

                foreach (string Record in InputList)
                {
                    string SourcePath = Record.Split("||", System.StringSplitOptions.None).FirstOrDefault();

                    if (FilterList.Select((Rec) => Rec.Split("||", System.StringSplitOptions.None).FirstOrDefault()).All((RecPath) => !SourcePath.StartsWith(RecPath)))
                    {
                        FilterList.Add(Record);
                    }
                }

                Container.Push(FilterList);
            }
        }

        public List<string> Pop()
        {
            lock (Locker)
            {
                return Container.Pop();
            }
        }

        public int Count { get => Container.Count; }

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
            Container = new Stack<List<string>>();
        }
    }
}
