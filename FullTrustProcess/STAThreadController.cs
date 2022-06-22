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
        private readonly BlockingCollection<STATaskData> TaskCollection;
        private readonly static object Locker = new object();

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
            STATaskData<object> Data = new STATaskData<object>(Executer);

            TaskCollection.Add(Data);

            return Data.TaskSource.Task;
        }

        public Task<T> ExecuteOnSTAThreadAsync<T>(Func<T> Executer)
        {
            STATaskData<T> Data = new STATaskData<T>(Executer);

            TaskCollection.Add(Data);

            return Data.TaskSource.Task;
        }

        private void ThreadProcess()
        {
            Ole32.OleInitialize();

            try
            {
                while (true)
                {
                    try
                    {
                        STATaskData Data = TaskCollection.Take();

                        object TaskSourceObject = Data.GetType()
                                                      .GetProperty("TaskSource")
                                                      .GetValue(Data);

                        try
                        {
                            object ExecuterResult = ((Delegate)Data.GetType()
                                                                   .GetProperty("Executer")
                                                                   .GetValue(Data)).DynamicInvoke();

                            MethodInfo SetResultMethod = TaskSourceObject.GetType()
                                                                         .GetMethod("SetResult");

                            if (ExecuterResult != null)
                            {
                                SetResultMethod.Invoke(TaskSourceObject, new object[] { ExecuterResult });
                            }
                            else
                            {
                                Type ParameterType = SetResultMethod.GetParameters()[0].ParameterType;

                                if (ParameterType.IsValueType)
                                {
                                    SetResultMethod.Invoke(TaskSourceObject, new object[] { Activator.CreateInstance(ParameterType) });
                                }
                                else
                                {
                                    SetResultMethod.Invoke(TaskSourceObject, new object[] { null });
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            TaskSourceObject.GetType()
                                            .GetMethod("SetException", new Type[] { typeof(Exception) })
                                            .Invoke(TaskSourceObject, new object[] { ex.InnerException ?? ex });
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
            GC.SuppressFinalize(this);

            TaskCollection.CompleteAdding();
            TaskCollection.Dispose();

            SpinWait.SpinUntil(() => STAThread.ThreadState.HasFlag(ThreadState.Stopped), 2000);
        }

        private STAThreadController()
        {
            TaskCollection = new BlockingCollection<STATaskData>();

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

        private sealed class STATaskData<T> : STATaskData
        {
            public TaskCompletionSource<T> TaskSource { get; } = new TaskCompletionSource<T>();

            public STATaskData(Delegate Executer) : base(Executer)
            {

            }
        }

        private abstract class STATaskData
        {
            public Delegate Executer { get; }

            protected STATaskData(Delegate Executer)
            {
                this.Executer = Executer;
            }
        }
    }
}
