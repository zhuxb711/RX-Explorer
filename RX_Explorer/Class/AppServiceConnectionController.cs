using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.ApplicationModel.AppService;
using Windows.ApplicationModel.Background;
using Windows.Foundation.Collections;

namespace RX_Explorer.Class
{
    public sealed class AppServiceConnectionController : IAsyncDisposable
    {
        private const string ExecuteType_Test_Connection = "Execute_Test_Connection";

        private static readonly ConcurrentQueue<TaskCompletionSource<AppServiceConnectionController>> CompletionSourceQueue = new ConcurrentQueue<TaskCompletionSource<AppServiceConnectionController>>();
        private static readonly int ProcessId;
        private readonly BackgroundTaskDeferral Deferral;
        private readonly AppServiceConnection Connection;
        private readonly IBackgroundTaskInstance BackgroundTask;
        private event EventHandler ConnectionClosed;
        private bool IsDisposed;

        public static void SetIncomeBackgroundTask(IBackgroundTaskInstance BackgroundTask)
        {
            if (CompletionSourceQueue.TryDequeue(out TaskCompletionSource<AppServiceConnectionController> CompletionSource))
            {
                CompletionSource.SetResult(new AppServiceConnectionController(BackgroundTask));
            }
            else
            {
                throw new InvalidOperationException($"{nameof(CreateNewAsync)} should be called first");
            }
        }

        public static async Task<AppServiceConnectionController> CreateNewAsync()
        {
            TaskCompletionSource<AppServiceConnectionController> CompletionSource = new TaskCompletionSource<AppServiceConnectionController>();

            CompletionSourceQueue.Enqueue(CompletionSource);

            await FullTrustProcessLauncher.LaunchFullTrustProcessForCurrentAppAsync();

            return await CompletionSource.Task;
        }

        private AppServiceConnectionController(IBackgroundTaskInstance BackgroundTask)
        {
            Deferral = BackgroundTask.GetDeferral();

            Connection = (BackgroundTask.TriggerDetails as AppServiceTriggerDetails).AppServiceConnection;
            Connection.RequestReceived += Connection_RequestReceived;
            Connection.ServiceClosed += Connection_ServiceClosed;

            BackgroundTask.Canceled += BackgroundTask_Canceled;

            this.BackgroundTask = BackgroundTask;
        }

        public async Task<bool> TestConnectionAsync()
        {
            AppServiceResponse Response = await Connection.SendMessageAsync(new ValueSet { { "ExecuteType", ExecuteType_Test_Connection } });

            if (Response.Status == AppServiceResponseStatus.Success)
            {
                if (Response.Message.ContainsKey(ExecuteType_Test_Connection))
                {
                    return true;
                }
                else
                {
                    LogTracer.Log($"{nameof(TestConnectionAsync)} report that connection is not available, reason: response data is not correct");
                    return false;
                }
            }
            else
            {
                LogTracer.Log($"{nameof(TestConnectionAsync)} report that connection is not available, reason: {Enum.GetName(typeof(AppServiceResponseStatus), Response.Status)}");
                return false;
            }
        }

        public Task<AppServiceResponse> SendCommandAsync(ValueSet Value)
        {
            return Connection.SendMessageAsync(Value).AsTask();
        }

        private void Connection_ServiceClosed(AppServiceConnection sender, AppServiceClosedEventArgs args)
        {
            LogTracer.Log($"FullTrustProcess exited unexpected, AppService exited for connection closed, Close reason: {Enum.GetName(typeof(AppServiceClosedStatus), args.Status)}");

            Connection.RequestReceived -= Connection_RequestReceived;
            Connection.ServiceClosed -= Connection_ServiceClosed;
            BackgroundTask.Canceled -= BackgroundTask_Canceled;

            ConnectionClosed?.Invoke(this, new EventArgs());

            Connection?.Dispose();
            Deferral?.Complete();
        }

        private async void Connection_RequestReceived(AppServiceConnection sender, AppServiceRequestReceivedEventArgs args)
        {
            AppServiceDeferral Deferral = args.GetDeferral();

            try
            {
                switch (args.Request.Message["ExecuteType"])
                {
                    case "ProcessId":
                        {
                            await args.Request.SendResponseAsync(new ValueSet { { "ProcessId", "ProcessId" } });
                            break;
                        }
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex);
            }
            finally
            {
                Deferral.Complete();
            }
        }

        private void BackgroundTask_Canceled(IBackgroundTaskInstance sender, BackgroundTaskCancellationReason reason)
        {
            Connection.RequestReceived -= Connection_RequestReceived;
            Connection.ServiceClosed -= Connection_ServiceClosed;
            BackgroundTask.Canceled -= BackgroundTask_Canceled;

            Connection?.Dispose();
            Deferral?.Complete();

            if (!IsDisposed)
            {
                LogTracer.Log($"FullTrustProcess exited unexpected, AppService exited for BackgroundTask canceled, Cancel reason: {Enum.GetName(typeof(BackgroundTaskCancellationReason), reason)}");
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (!IsDisposed)
            {
                IsDisposed = true;

                Connection.RequestReceived -= Connection_RequestReceived;
                Connection.ServiceClosed -= Connection_ServiceClosed;
                BackgroundTask.Canceled -= BackgroundTask_Canceled;

                await Task.WhenAny(Connection.SendMessageAsync(new ValueSet { { "ExecuteType", "Execute_Exit" } }).AsTask(), Task.Delay(2000));

                Connection?.Dispose();
                Deferral?.Complete();
            }
        }

        static AppServiceConnectionController()
        {
            using (Process Process = Process.GetCurrentProcess())
            {
                ProcessId = Process.Id;
            }
        }
    }
}
