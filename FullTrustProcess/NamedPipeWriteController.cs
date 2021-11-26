using System;
using System.Collections.Concurrent;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;

namespace FullTrustProcess
{
    public sealed class NamedPipeWriteController : NamedPipeControllerBase
    {
        private readonly Thread ProcessThread;
        private readonly ConcurrentQueue<string> MessageQueue = new ConcurrentQueue<string>();
        private readonly AutoResetEvent Locker = new AutoResetEvent(false);

        public override PipeDirection PipeMode => PipeDirection.Out;

        public void SendData(string Data)
        {
            MessageQueue.Enqueue(Data);

            if (ProcessThread.ThreadState.HasFlag(ThreadState.WaitSleepJoin))
            {
                Locker.Set();
            }
        }

        private void WriteProcess()
        {
            try
            {
                if (!IsConnected)
                {
                    PipeStream.Connect(2000);
                }

                using (StreamWriter Writer = new StreamWriter(PipeStream, new UTF8Encoding(false), 512, true))
                {
                    while (IsConnected)
                    {
                        if (MessageQueue.IsEmpty)
                        {
                            Locker.WaitOne();
                        }

                        while (MessageQueue.TryDequeue(out string Message))
                        {
                            Writer.WriteLine(Message);
                        }

                        Writer.Flush();

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
                Locker.Dispose();
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
