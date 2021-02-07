using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FullTrustProcess
{
    public static class Helper
    {
        public static Task<T> CreateSTATask<T>(Func<T> Executor)
        {
            TaskCompletionSource<T> CompletionSource = new TaskCompletionSource<T>();

            Thread STAThread = new Thread(() =>
            {
                try
                {
                    T Result = Executor();
                    CompletionSource.SetResult(Result);
                }
                catch(Exception ex)
                {
                    CompletionSource.SetException(ex);
                }
            });
            STAThread.SetApartmentState(ApartmentState.STA);
            STAThread.Start();

            return CompletionSource.Task;
        }
    }
}
