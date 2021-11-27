using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Vanara.PInvoke;

namespace FullTrustProcess
{
    public sealed class STAThreadController
    {
        private readonly Thread STAThread;
        private readonly ConcurrentQueue<(TaskCompletionSource<bool>, Action)> TaskQueue;
        private readonly AutoResetEvent ProcessSleepLocker;


        private static STAThreadController Instance;
        public static STAThreadController Current
        {
            get
            {
                return Instance ??= new STAThreadController();
            }
        }

        public async Task<bool> RunAsync(Action Act)
        {
            TaskCompletionSource<bool> CompletionSource = new TaskCompletionSource<bool>();

            TaskQueue.Enqueue((CompletionSource, Act));
            ProcessSleepLocker.Set();

            return await CompletionSource.Task;
        }

        private void ThreadProcess()
        {
            while (true)
            {
                if (TaskQueue.IsEmpty)
                {
                    ProcessSleepLocker.WaitOne();
                }

                while (TaskQueue.TryDequeue(out (TaskCompletionSource<bool>, Action) Group))
                {
                    try
                    {
                        Group.Item2();
                        Group.Item1.SetResult(true);
                    }
                    catch (Exception ex)
                    {
                        Group.Item1.SetException(ex);
                    }
                }
            }
        }

        private STAThreadController()
        {
            Ole32.OleInitialize();

            ProcessSleepLocker = new AutoResetEvent(false);
            TaskQueue = new ConcurrentQueue<(TaskCompletionSource<bool>, Action)>();

            STAThread = new Thread(ThreadProcess)
            {
                Priority = ThreadPriority.Normal,
                IsBackground = true
            };
            STAThread.SetApartmentState(ApartmentState.STA);
            STAThread.Start();
        }

        ~STAThreadController()
        {
            Ole32.OleUninitialize();
        }
    }
}
