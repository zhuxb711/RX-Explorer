using System;
using System.Threading.Tasks;

namespace FullTrustProcess
{
    public sealed class STATaskData<T> : STATaskData
    {
        public TaskCompletionSource<T> CompletionSource { get; }

        public Delegate Executer { get; }

        public STATaskData(TaskCompletionSource<T> CompletionSource, Delegate Executer)
        {
            this.CompletionSource = CompletionSource;
            this.Executer = Executer;
        }
    }
}
