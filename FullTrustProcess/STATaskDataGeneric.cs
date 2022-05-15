using System;
using System.Threading.Tasks;

namespace FullTrustProcess
{
    public sealed class STATaskData<T> : STATaskData
    {
        public TaskCompletionSource<T> CompletionSource { get; }

        public STATaskData(TaskCompletionSource<T> CompletionSource, Delegate Executer) : base(Executer)
        {
            this.CompletionSource = CompletionSource;
        }
    }
}
