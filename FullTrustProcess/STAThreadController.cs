using System;
using System.Collections.Concurrent;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Vanara.PInvoke;

namespace FullTrustProcess
{
    public sealed class STAThreadController
    {
        private readonly Thread STAThread;
        private readonly ConcurrentQueue<STATaskData> TaskQueue;
        private readonly AutoResetEvent ProcessSleepLocker;
        private readonly static object Locker = new object();
        private bool IsCleanUp = false;

        private static STAThreadController Instance;
        public static STAThreadController Current
        {
            get
            {
                lock (Locker)
                {
                    return Instance ??= new STAThreadController();
                }
            }
        }

        public Task ExecuteOnSTAThreadAsync(Action Executer)
        {
            if (IsCleanUp)
            {
                throw new InvalidOperationException("Already cleaned up");
            }

            TaskCompletionSource<object> CompletionSource = new TaskCompletionSource<object>();

            TaskQueue.Enqueue(new STATaskData<object>(CompletionSource, Executer));
            ProcessSleepLocker.Set();

            return CompletionSource.Task;
        }

        public Task<T> ExecuteOnSTAThreadAsync<T>(Func<T> Executer)
        {
            if (IsCleanUp)
            {
                throw new InvalidOperationException("Already cleaned up");
            }

            TaskCompletionSource<T> CompletionSource = new TaskCompletionSource<T>();

            TaskQueue.Enqueue(new STATaskData<T>(CompletionSource, Executer));
            ProcessSleepLocker.Set();

            return CompletionSource.Task;
        }

        private void ThreadProcess()
        {
            Ole32.OleInitialize();

            try
            {
                while (true)
                {
                    if (TaskQueue.IsEmpty)
                    {
                        ProcessSleepLocker.WaitOne();
                    }

                    if (IsCleanUp)
                    {
                        break;
                    }

                    try
                    {
                        while (TaskQueue.TryDequeue(out STATaskData Core))
                        {
                            object CompletionSourceObject = Core.GetType()
                                                                .GetProperty("CompletionSource")
                                                                .GetValue(Core);

                            try
                            {
                                object ExecuterResult = ((Delegate)Core.GetType()
                                                                       .GetProperty("Executer")
                                                                       .GetValue(Core)).DynamicInvoke();

                                MethodInfo SetResultMethod = CompletionSourceObject.GetType()
                                                                                   .GetMethod("SetResult");

                                if (ExecuterResult != null)
                                {
                                    SetResultMethod.Invoke(CompletionSourceObject, new object[] { ExecuterResult });
                                }
                                else
                                {
                                    Type ParameterType = SetResultMethod.GetParameters()[0].ParameterType;

                                    if (ParameterType.IsValueType)
                                    {
                                        SetResultMethod.Invoke(CompletionSourceObject, new object[] { Activator.CreateInstance(ParameterType) });
                                    }
                                    else
                                    {
                                        SetResultMethod.Invoke(CompletionSourceObject, new object[] { null });
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                CompletionSourceObject.GetType()
                                                      .GetMethod("SetException", new Type[] { typeof(Exception) })
                                                      .Invoke(CompletionSourceObject, new object[] { ex.InnerException ?? ex });
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        LogTracer.Log(ex, $"Unexpected exception was threw in the thread of {nameof(STAThreadController)}");
                    }
                }
            }
            finally
            {
                Ole32.OleUninitialize();
            }
        }

        public void CleanUp()
        {
            if (!IsCleanUp)
            {
                IsCleanUp = true;

                TaskQueue.Clear();
                ProcessSleepLocker.Set();
                ProcessSleepLocker.Dispose();

                GC.SuppressFinalize(this);

                SpinWait.SpinUntil(() => STAThread.ThreadState.HasFlag(ThreadState.Stopped), 2000);
            }
        }

        private STAThreadController()
        {
            ProcessSleepLocker = new AutoResetEvent(false);
            TaskQueue = new ConcurrentQueue<STATaskData>();

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
            CleanUp();
        }
    }
}
