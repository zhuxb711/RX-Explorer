using FluentFTP;
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
        private readonly Thread ProcessThread;
        private readonly BlockingCollection<FTPTaskData> TaskCollection;
        private readonly FtpClient Client;
        private readonly SemaphoreSlim Locker;

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
                if (!IsAvailable)
                {
                    foreach (FtpDataConnectionType ConnectionType in new FtpDataConnectionType[] { FtpDataConnectionType.AutoPassive, FtpDataConnectionType.AutoActive })
                    {
                        try
                        {
                            Client.DataConnectionType = ConnectionType;

                            using (CancellationTokenSource Cancellation = new CancellationTokenSource(30000))
                            {
                                await Client.ConnectAsync(Cancellation.Token);
                                await Client.GetNameListingAsync(Cancellation.Token);

                                if (IsAvailable)
                                {
                                    break;
                                }
                            }
                        }
                        catch (Exception)
                        {
                            //No need to handle this exception
                        }
                    }

                    if (IsAvailable)
                    {
                        LogTracer.Log($"Ftp server is connected, connection protocal: {Enum.GetName(typeof(FtpDataConnectionType), Client.DataConnectionType)} security protocal: {Client.SslProtocols}, encryption: {Client.EncryptionMode}, encoding: {Client.Encoding.EncodingName}");
                    }
                    else
                    {
                        throw new FtpException("Ftp server is not available");
                    }
                }
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

        public static Task<FtpClientController> DuplicateClientControllerAsync(FtpClientController Controller)
        {
            return MakeSureConnectionAndCloseOnceFailedAsync(new FtpClientController(Controller.ServerHost, Controller.ServerPort, Controller.UserName, Controller.Password, Controller.UseEncryption));
        }

        public static Task<FtpClientController> CreateAsync(string Host, int Port, string UserName, string Password, bool UseEncryption)
        {
            return MakeSureConnectionAndCloseOnceFailedAsync(new FtpClientController(Host, Port, UserName, Password, UseEncryption));
        }

        private FtpClientController(string Host, int Port, string UserName, string Password, bool UseEncryption)
        {
            Locker = new SemaphoreSlim(1, 1);
            TaskCollection = new BlockingCollection<FTPTaskData>();

            this.UserName = UserName;
            this.Password = Password;
            this.UseEncryption = UseEncryption;

            Client = new FtpClient(Host, Port, UserName, Password)
            {
                Encoding = Encoding.UTF8,
                TimeConversion = FtpDate.UTC,
                LocalTimeZone = TimeZoneInfo.Local.BaseUtcOffset.Hours,
                EncryptionMode = UseEncryption ? FtpEncryptionMode.Implicit : FtpEncryptionMode.None,
                SslProtocols = UseEncryption ? SslProtocols.Tls12 : SslProtocols.None,
                SocketKeepAlive = true,
                ValidateAnyCertificate = true,
                RetryAttempts = 3
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
