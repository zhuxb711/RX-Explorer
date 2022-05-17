using FluentFTP;
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace RX_Explorer.Class
{
    public sealed class FTPClientController : IDisposable
    {
        private Thread ProcessThread;
        private readonly ConcurrentQueue<FTPTaskData> TaskQueue;
        private readonly AutoResetEvent ProcessSleepLocker;
        private readonly FtpClient Client;

        public string ServerHost => Client.Host;

        public bool IsAvailable => Client.IsConnected && Client.IsAuthenticated;

        private void ProcessCore()
        {
            while (IsAvailable)
            {
                if (TaskQueue.IsEmpty)
                {
                    ProcessSleepLocker.WaitOne();
                }

                while (TaskQueue.TryDequeue(out FTPTaskData Data))
                {
                    try
                    {
                        object ExecuteResult = Data.Executor.DynamicInvoke(Client);
                        
                        if (ExecuteResult is Task AsyncTask)
                        {
                            AsyncTask.Wait();
                            Data.CompletionSource.SetResult(AsyncTask.GetType().GetProperty("Result").GetValue(AsyncTask));
                        }
                    }
                    catch (Exception ex)
                    {
                        Data.CompletionSource.SetException(ex);
                    }
                }
            }
        }

        public Task<T> RunCommandAsync<T>(Func<FtpClient, T> Executor)
        {
            FTPTaskData Data = new FTPTaskData(Executor);

            TaskQueue.Enqueue(Data);

            ProcessSleepLocker.Set();

            return Data.CompletionSource.Task.ContinueWith((PreviousTask) =>
            {
                if (PreviousTask.Exception is Exception InnerException)
                {
                    throw new AggregateException(InnerException);
                }

                return (T)PreviousTask.Result;
            });
        }

        public Task RunCommandAsync(Func<FtpClient, Task> Executor)
        {
            FTPTaskData Data = new FTPTaskData(Executor);

            TaskQueue.Enqueue(Data);

            ProcessSleepLocker.Set();

            return Data.CompletionSource.Task;
        }

        public Task<T> RunCommandAsync<T>(Func<FtpClient, Task<T>> Executor)
        {
            FTPTaskData Data = new FTPTaskData(Executor);

            TaskQueue.Enqueue(Data);

            ProcessSleepLocker.Set();

            return Data.CompletionSource.Task.ContinueWith((PreviousTask) =>
            {
                if (PreviousTask.Exception is Exception InnerException)
                {
                    throw new AggregateException(InnerException);
                }

                return (T)PreviousTask.Result;
            });
        }

        public Task RunCommandAsync(Action<FtpClient> Executor)
        {
            FTPTaskData Data = new FTPTaskData(Executor);

            TaskQueue.Enqueue(Data);

            ProcessSleepLocker.Set();

            return Data.CompletionSource.Task;
        }

        public async Task<bool> ConnectAsync()
        {
            if (IsAvailable)
            {
                return true;
            }
            else if (await Client.AutoConnectAsync() is FtpProfile Profile)
            {
                if (IsAvailable)
                {
                    if (ProcessThread == null
                        || ProcessThread.ThreadState.HasFlag(ThreadState.Stopped)
                        || ProcessThread.ThreadState.HasFlag(ThreadState.Unstarted))
                    {
                        ProcessThread = new Thread(ProcessCore)
                        {
                            IsBackground = true,
                            Priority = ThreadPriority.Normal
                        };

                        ProcessThread.Start();
                    }

                    LogTracer.Log($"Ftp server is connected, protocal: {Profile.Protocols}, encryption: {Profile.Encryption}, encoding: {Profile.Encoding.EncodingName}");

                    return true;
                }
            }

            return false;
        }

        public FtpClient DangerousGetFtpClient()
        {
            return Client;
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
            Client.Disconnect();
            Client.Dispose();
        }

        public FTPClientController(string Host, int Port, string UserName, string Password)
        {
            Client = new FtpClient(Host, Port, UserName, Password);
            ProcessSleepLocker = new AutoResetEvent(false);
            TaskQueue = new ConcurrentQueue<FTPTaskData>();
        }

        ~FTPClientController()
        {
            Dispose();
        }

        private sealed class FTPTaskData
        {
            public Delegate Executor { get; }

            public TaskCompletionSource<object> CompletionSource { get; } = new TaskCompletionSource<object>();

            public FTPTaskData(Delegate Executor)
            {
                this.Executor = Executor;
            }
        }
    }
}
