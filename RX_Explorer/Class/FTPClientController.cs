using FluentFTP;
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace RX_Explorer.Class
{
    public sealed class FTPClientController : IDisposable
    {
        private bool IsDisposed;
        private readonly Thread ProcessThread;
        private readonly BlockingCollection<FTPTaskData> TaskCollection;
        private readonly FtpClient Client;
        private readonly SemaphoreSlim Locker;

        public string ServerHost => Client.Host;

        public int ServerPort => Client.Port;

        public bool IsAvailable => Client.IsConnected && Client.IsAuthenticated;

        private void ProcessCore()
        {
            while (!IsDisposed)
            {
                FTPTaskData Data = null;

                try
                {
                    Data = TaskCollection.Take();
                }
                catch (Exception)
                {
                    //No need to handle this exception
                }

                if (Data != null)
                {
                    if (IsAvailable)
                    {
                        try
                        {
                            object ExecuteResult = Data.Executor.DynamicInvoke(Client);

                            if (ExecuteResult is Task AsyncTask)
                            {
                                AsyncTask.Wait();

                                if (AsyncTask.Exception is Exception ex)
                                {
                                    Data.TaskSource.SetException(ex);
                                }
                                else
                                {
                                    Data.TaskSource.SetResult(AsyncTask.GetType().GetProperty("Result").GetValue(AsyncTask));
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Data.TaskSource.SetException(ex);
                        }
                    }
                    else
                    {
                        Data.TaskSource.SetException(new FtpException("Connection lost"));
                    }
                }
            }
        }

        public async Task<T> RunCommandAsync<T>(Func<FtpClient, T> Executor)
        {
            if (await ConnectAsync())
            {
                FTPTaskData Data = new FTPTaskData(Executor);

                TaskCollection.Add(Data);

                return (T)await Data.TaskSource.Task;
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

                TaskCollection.Add(Data);

                await Data.TaskSource.Task;
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

                TaskCollection.Add(Data);

                return (T)await Data.TaskSource.Task;
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

                TaskCollection.Add(Data);

                await Data.TaskSource.Task;
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
                        //Connection lost and we continue to try reconnect
                    }
                }

                if (await Client.AutoConnectAsync() is FtpProfile Profile)
                {
                    if (IsAvailable)
                    {
                        ProcessThread.Start();
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
            if (!IsDisposed)
            {
                IsDisposed = true;

                GC.SuppressFinalize(this);

                TaskCollection.CompleteAdding();
                TaskCollection.Dispose();

                Client.Disconnect();
                Client.Dispose();

                Locker.Dispose();
            }
        }

        public FTPClientController(string Host, int Port, string UserName, string Password)
        {
            Locker = new SemaphoreSlim(1, 1);
            TaskCollection = new BlockingCollection<FTPTaskData>();

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
        }

        ~FTPClientController()
        {
            Dispose();
        }

        private sealed class FTPTaskData
        {
            public Delegate Executor { get; }

            public TaskCompletionSource<object> TaskSource { get; } = new TaskCompletionSource<object>();

            public FTPTaskData(Delegate Executor)
            {
                this.Executor = Executor;
            }
        }
    }
}
