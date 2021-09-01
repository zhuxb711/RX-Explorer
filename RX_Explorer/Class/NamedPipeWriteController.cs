using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RX_Explorer.Class
{
    public sealed class NamedPipeWriteController : NamedPipeControllerBase
    {
        private readonly Thread ProcessThread;
        private readonly ConcurrentQueue<string> MessageQueue = new ConcurrentQueue<string>();
        private readonly AutoResetEvent Locker = new AutoResetEvent(false);

        public static bool TryCreateNamedPipe(out NamedPipeWriteController Controller)
        {
            try
            {
                Controller = new NamedPipeWriteController();
                return true;
            }
            catch (Exception ex)
            {
                Controller = null;
                LogTracer.Log(ex, "Could not create named pipe");
                return false;
            }
        }

        private void WriteProcess()
        {
            try
            {
                if (!IsConnected)
                {
                    PipeStream.WaitForConnection();
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

        public void SendData(string Data)
        {
            MessageQueue.Enqueue(Data);

            if (ProcessThread.ThreadState.HasFlag(ThreadState.WaitSleepJoin))
            {
                Locker.Set();
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

        private NamedPipeWriteController()
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
