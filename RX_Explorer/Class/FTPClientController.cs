using FluentFTP;
using System;
using System.Collections.Concurrent;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RX_Explorer.Class
{
    public sealed class FtpClientController : IDisposable
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

        public Task RunCommandAsync(Func<FtpClient, Task> Executor)
        {
            return RunCommandAsync<object>(Executor);
        }

        public Task RunCommandAsync(Action<FtpClient> Executor)
        {
            return RunCommandAsync((Client) =>
            {
                Executor(Client);
                return Task.CompletedTask;
            });
        }

        public Task<T> RunCommandAsync<T>(Func<FtpClient, T> Executor)
        {
            return RunCommandAsync((Client) => Task.FromResult(Executor(Client)));
        }

        public async Task<T> RunCommandAsync<T>(Func<FtpClient, Task<T>> Executor)
        {
            await ConnectAsync();

            FTPTaskData Data = new FTPTaskData(Executor);

            TaskCollection.Add(Data);

            return (T)await Data.TaskSource.Task;
        }

        private async Task ConnectAsync()
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
                            using (CancellationTokenSource CancellationSource = new CancellationTokenSource(10000))
                            {
                                FtpReply Reply = await Client.GetReplyAsync(CancellationSource.Token);

                                if (Reply.Success)
                                {
                                    return;
                                }
                            }
                        }
                        else
                        {
                            return;
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
                        LogTracer.Log($"Ftp server is connected, protocal: {Profile.Protocols}, encryption: {Profile.Encryption}, encoding: {Profile.Encoding.EncodingName}");

                        using (CancellationTokenSource CommandCancellation = new CancellationTokenSource(10000))
                        {
                            try
                            {
                                FtpReply EncodingReply = await Client.ExecuteAsync("OPTS UTF8 ON", CommandCancellation.Token);

                                if (EncodingReply.Code != "200" && EncodingReply.Code != "202")
                                {
                                    Client.Encoding = Encoding.GetEncoding("ISO-8859-1");
                                }
                            }
                            catch (Exception)
                            {
                                //No need to handle this exception
                            }
                        }

                        return;
                    }
                }

                throw new FtpException("Ftp server is not available");
            }
            finally
            {
                Locker.Release();
            }
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

        public static async Task<FtpClientController> MakeSureConnectionAndCloseOnceFailedAsync(FtpClientController Controller)
        {
            try
            {
                await Controller.ConnectAsync();
                return Controller;
            }
            catch (Exception)
            {
                Controller.Dispose();
                throw;
            }
        }

        public static async Task<FtpClientController> CreateAsync(string Host, int Port, string UserName, string Password)
        {
            FtpClientController Controller = new FtpClientController(Host, Port, UserName, Password);
            await Controller.ConnectAsync();
            return Controller;
        }

        private FtpClientController(string Host, int Port, string UserName, string Password)
        {
            Locker = new SemaphoreSlim(1, 1);
            TaskCollection = new BlockingCollection<FTPTaskData>();

            Client = new FtpClient(Host, Port, UserName, Password)
            {
                NoopInterval = 5000,
                Encoding = Encoding.UTF8,
                TimeConversion = FtpDate.UTC,
                LocalTimeZone = TimeZoneInfo.Local.BaseUtcOffset.Hours,
                EncryptionMode = FtpEncryptionMode.Auto,
                DataConnectionType = FtpDataConnectionType.AutoPassive,
                ReadTimeout = 60000,
                DataConnectionReadTimeout = 60000,
                DataConnectionConnectTimeout = 60000,
            };

            ProcessThread = new Thread(ProcessCore)
            {
                IsBackground = true,
                Priority = ThreadPriority.Normal
            };
            ProcessThread.Start();
        }

        ~FtpClientController()
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
