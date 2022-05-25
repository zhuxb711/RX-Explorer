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
        private readonly SemaphoreSlim Locker;

        public string ServerHost => Client.Host;

        public int ServerPort => Client.Port;

        public bool IsAvailable => Client.IsConnected && Client.IsAuthenticated;

        private void ProcessCore()
        {
            while (true)
            {
                if (TaskQueue.IsEmpty)
                {
                    ProcessSleepLocker.WaitOne();
                }

                while (TaskQueue.TryDequeue(out FTPTaskData Data))
                {
                    try
                    {
                        if (ConnectAsync().Result)
                        {
                            object ExecuteResult = Data.Executor.DynamicInvoke(Client);

                            if (ExecuteResult is Task AsyncTask)
                            {
                                AsyncTask.Wait();
                                Data.CompletionSource.SetResult(AsyncTask.GetType().GetProperty("Result").GetValue(AsyncTask));
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Data.CompletionSource.SetException(ex);
                    }
                }
            }
        }

        public async Task<T> RunCommandAsync<T>(Func<FtpClient, T> Executor)
        {
            if (await ConnectAsync())
            {
                FTPTaskData Data = new FTPTaskData(Executor);

                TaskQueue.Enqueue(Data);

                ProcessSleepLocker.Set();

                return (T)await Data.CompletionSource.Task;
            }
            else
            {
                throw new Exception("FtpClient is not available");
            }
        }

        public async Task RunCommandAsync(Func<FtpClient, Task> Executor)
        {
            if (await ConnectAsync())
            {
                FTPTaskData Data = new FTPTaskData(Executor);

                TaskQueue.Enqueue(Data);

                ProcessSleepLocker.Set();

                await Data.CompletionSource.Task;
            }
            else
            {
                throw new Exception("FtpClient is not available");
            }
        }

        public async Task<T> RunCommandAsync<T>(Func<FtpClient, Task<T>> Executor)
        {
            if (await ConnectAsync())
            {
                FTPTaskData Data = new FTPTaskData(Executor);

                TaskQueue.Enqueue(Data);

                ProcessSleepLocker.Set();

                return (T)await Data.CompletionSource.Task;
            }
            else
            {
                throw new Exception("FtpClient is not available");
            }
        }

        public async Task RunCommandAsync(Action<FtpClient> Executor)
        {
            if (await ConnectAsync())
            {
                FTPTaskData Data = new FTPTaskData(Executor);

                TaskQueue.Enqueue(Data);

                ProcessSleepLocker.Set();

                await Data.CompletionSource.Task;
            }
            else
            {
                throw new Exception("FtpClient is not available");
            }
        }

        public async Task<bool> ConnectAsync()
        {
            await Locker.WaitAsync();

            try
            {
                if (IsAvailable)
                {
                    try
                    {
                        if (await Task.Run(() => Client.Noop()))
                        {
                            using (CancellationTokenSource CancellationSource = new CancellationTokenSource(5000))
                            {
                                FtpReply Reply = await Client.GetReplyAsync(CancellationSource.Token);

                                if (Reply.Success)
                                {
                                    return true;
                                }
                            }
                        }
                        else
                        {
                            return true;
                        }
                    }
                    catch (Exception)
                    {
                        //Connection lost
                    }
                }

                if (await Client.AutoConnectAsync() is FtpProfile Profile)
                {
                    if (IsAvailable)
                    {
                        LogTracer.Log($"Ftp server is connected, protocal: {Profile.Protocols}, encryption: {Profile.Encryption}, encoding: {Profile.Encoding.EncodingName}");

                        return true;
                    }
                }
            }
            catch (Exception ex) when (ex is not FtpAuthenticationException)
            {
                LogTracer.Log(ex, $"Could not connect to the ftp server: {Client.Host}");
            }
            finally
            {
                Locker.Release();
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
            Locker.Dispose();
        }

        public FTPClientController(string Host, int Port, string UserName, string Password)
        {
            Locker = new SemaphoreSlim(1, 1);
            ProcessSleepLocker = new AutoResetEvent(false);
            TaskQueue = new ConcurrentQueue<FTPTaskData>();
            Client = new FtpClient(Host, Port, UserName, Password)
            {
                NoopInterval = 5000,
                TimeConversion = FtpDate.UTC,
                LocalTimeZone = TimeZoneInfo.Local.BaseUtcOffset.Hours
            };

            ProcessThread = new Thread(ProcessCore)
            {
                IsBackground = true,
                Priority = ThreadPriority.Normal
            };

            ProcessThread.Start();
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
