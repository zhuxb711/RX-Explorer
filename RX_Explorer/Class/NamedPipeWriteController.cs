using System;
using System.Collections.Concurrent;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RX_Explorer.Class
{
    public class NamedPipeWriteController : NamedPipeControllerBase
    {
        private readonly Thread ProcessThread;
        private readonly TaskCompletionSource<bool> ConnectionSet;
        private readonly ConcurrentQueue<string> MessageQueue = new ConcurrentQueue<string>();
        private readonly AutoResetEvent ProcessSleepLocker = new AutoResetEvent(false);

        protected override int MaxAllowedConnection => 1;

        private void WriteProcess()
        {
            try
            {
                if (!IsConnected)
                {
                    try
                    {
                        PipeStream.WaitForConnection();
                    }
                    catch (Exception ex)
                    {
                        LogTracer.Log(ex, "Could not read pipeline data because unknown exception");
                    }
                }

                ConnectionSet.SetResult(IsConnected);

                while (IsConnected)
                {
                    if (MessageQueue.IsEmpty)
                    {
                        ProcessSleepLocker.WaitOne();
                    }

                    while (MessageQueue.TryDequeue(out string Message))
                    {
                        byte[] ByteArray = Encoding.Unicode.GetBytes(Message);

                        PipeStream.Write(ByteArray, 0, ByteArray.Length);
                        PipeStream.WaitForPipeDrain();
                    }
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, "Could not send pipeline data");
            }
            finally
            {
                Dispose();
            }
        }

        public void SendData(string Data)
        {
            if (IsConnected)
            {
                MessageQueue.Enqueue(Data);
                ProcessSleepLocker.Set();
            }
            else
            {
                throw new InvalidOperationException("Named pipe is disconnected and could not send data anymore");
            }
        }

        public override void Dispose()
        {
            if (!IsDisposed)
            {
                ProcessSleepLocker.Dispose();
                MessageQueue.Clear();
            }

            base.Dispose();
        }

        public override async Task<bool> WaitForConnectionAsync(int TimeoutMilliseconds)
        {
            if (await Task.WhenAny(ConnectionSet.Task, Task.Delay(TimeoutMilliseconds)) == ConnectionSet.Task)
            {
                return ConnectionSet.Task.Result;
            }
            else
            {
                return false;
            }
        }

        public NamedPipeWriteController() : this($"Explorer_NamedPipe_Write_{Guid.NewGuid():D}")
        {

        }

        protected NamedPipeWriteController(string Id) : base(Id)
        {
            ConnectionSet = new TaskCompletionSource<bool>();

            ProcessThread = new Thread(WriteProcess)
            {
                Priority = ThreadPriority.Normal,
                IsBackground = true
            };
            ProcessThread.Start();
        }
    }
}
