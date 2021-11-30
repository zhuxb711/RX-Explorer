using System;
using System.Collections.Concurrent;
using System.IO.Pipes;
using System.Text;
using System.Threading;

namespace FullTrustProcess
{
    public sealed class NamedPipeWriteController : NamedPipeControllerBase
    {
        private readonly Thread ProcessThread;
        private readonly ConcurrentQueue<string> MessageQueue = new ConcurrentQueue<string>();
        private readonly AutoResetEvent ProcessSleepLocker = new AutoResetEvent(false);

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

        private void WriteProcess()
        {
            try
            {
                if (!IsConnected)
                {
                    PipeStream.Connect(2000);
                    PipeStream.ReadMode = PipeTransmissionMode.Message;
                }

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

        public override void Dispose()
        {
            if (!IsDisposed)
            {
                ProcessSleepLocker.Dispose();
                MessageQueue.Clear();
            }

            base.Dispose();
        }

        public NamedPipeWriteController(string PipeId) : base(PipeId)
        {
            ProcessThread = new Thread(WriteProcess)
            {
                Priority = ThreadPriority.Normal,
                IsBackground = true
            };
            ProcessThread.Start();
        }
    }
}
