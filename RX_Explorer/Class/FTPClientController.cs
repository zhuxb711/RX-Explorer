using FluentFTP;
using FluentFTP.Exceptions;
using Nito.AsyncEx;
using System;
using System.Collections.Concurrent;
using System.Security.Authentication;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RX_Explorer.Class
{
    public sealed class FtpClientController : IDisposable
    {
        private bool IsDisposed;
        private readonly AsyncFtpClient Client;
        private readonly Thread ProcessThread;
        private readonly AsyncLock Locker = new AsyncLock();
        private readonly BlockingCollection<FTPTaskData> TaskCollection = new BlockingCollection<FTPTaskData>();

        public string ServerHost => Client.Host;

        public int ServerPort => Client.Port;

        public bool UseEncryption { get; }

        public bool IsAvailable => Client.IsConnected && Client.IsAuthenticated && !Client.IsDisposed;

        private string UserName { get; }

        private string Password { get; }

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
                        Data.TaskSource.SetException(new FtpException("Ftp connection was lost"));
                    }
                }
            }
        }

        public Task RunCommandAsync(Func<AsyncFtpClient, Task> Executor)
        {
            return RunCommandAsync<object>(Executor);
        }

        public Task RunCommandAsync(Action<AsyncFtpClient> Executor)
        {
            return RunCommandAsync((Client) =>
            {
                Executor(Client);
                return Task.CompletedTask;
            });
        }

        public Task<T> RunCommandAsync<T>(Func<AsyncFtpClient, T> Executor)
        {
            return RunCommandAsync((Client) => Task.FromResult(Executor(Client)));
        }

        public async Task<T> RunCommandAsync<T>(Func<AsyncFtpClient, Task<T>> Executor)
        {
            await ConnectAsync();

            FTPTaskData Data = new FTPTaskData(Executor);

            TaskCollection.Add(Data);

            return (T)await Data.TaskSource.Task;
        }

        private async Task ConnectAsync(CancellationToken CancelToken = default)
        {
            if (!IsAvailable)
            {
                using (await Locker.LockAsync())
                {
                    foreach (FtpDataConnectionType ConnectionType in new FtpDataConnectionType[] { FtpDataConnectionType.AutoPassive, FtpDataConnectionType.AutoActive })
                    {
                        try
                        {
                            Client.Config.DataConnectionType = ConnectionType;

                            await Client.Connect(CancelToken);
                            await Client.GetNameListing(CancelToken);

                            if (IsAvailable)
                            {
                                break;
                            }
                        }
                        catch (Exception)
                        {
                            //No need to handle this exception
                        }
                    }

                    if (IsAvailable)
                    {
                        LogTracer.Log($"Ftp server is connected, connection protocal: {Enum.GetName(typeof(FtpDataConnectionType), Client.Config.DataConnectionType)} security protocal: {Client.Config.SslProtocols}, encryption: {Client.Config.EncryptionMode}, encoding: {Client.Encoding.EncodingName}");
                    }
                    else
                    {
                        throw new FtpException("Ftp server is not available");
                    }
                }
            }
        }

        public AsyncFtpClient DangerousGetFtpClient()
        {
            return Client;
        }

        public void Dispose()
        {
            if (Execution.CheckAlreadyExecuted(this))
            {
                throw new ObjectDisposedException(nameof(FtpClientController));
            }

            GC.SuppressFinalize(this);

            Execution.ExecuteOnce(this, () =>
            {
                IsDisposed = true;

                TaskCollection.CompleteAdding();
                TaskCollection.Dispose();

                Client.Dispose();
            });
        }

        public static async Task<FtpClientController> MakeSureConnectionAndCloseOnceFailedAsync(FtpClientController Controller, CancellationToken CancelToken = default)
        {
            try
            {
                await Controller.ConnectAsync(CancelToken);
                return Controller;
            }
            catch (Exception)
            {
                Controller.Dispose();
                throw;
            }
        }

        public static Task<FtpClientController> DuplicateClientControllerAsync(FtpClientController Controller)
        {
            return MakeSureConnectionAndCloseOnceFailedAsync(new FtpClientController(Controller.ServerHost, Controller.ServerPort, Controller.UserName, Controller.Password, Controller.UseEncryption));
        }

        public static Task<FtpClientController> CreateAsync(string Host, int Port, string UserName, string Password, bool UseEncryption, CancellationToken CancelToken = default)
        {
            return MakeSureConnectionAndCloseOnceFailedAsync(new FtpClientController(Host, Port, UserName, Password, UseEncryption), CancelToken);
        }

        private FtpClientController(string Host, int Port, string UserName, string Password, bool UseEncryption)
        {
            this.UserName = UserName;
            this.Password = Password;
            this.UseEncryption = UseEncryption;

            Client = new AsyncFtpClient(Host, UserName, Password, Port, new FtpConfig
            {
                TimeConversion = FtpDate.LocalTime,
                TimeZone = TimeZoneInfo.Utc.BaseUtcOffset.Hours,
                LocalTimeZone = TimeZoneInfo.Local.BaseUtcOffset.Hours,
                EncryptionMode = UseEncryption ? FtpEncryptionMode.Implicit : FtpEncryptionMode.None,
                SslProtocols = UseEncryption ? SslProtocols.Tls12 : SslProtocols.None,
                SocketKeepAlive = true,
                ValidateAnyCertificate = true,
                RetryAttempts = 3,
                ReadTimeout = 30000,
                ConnectTimeout = 30000
            })
            {
                Encoding = Encoding.UTF8,
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
