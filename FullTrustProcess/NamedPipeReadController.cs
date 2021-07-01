using System;
using System.IO;
using System.Text;
using System.Threading;

namespace FullTrustProcess
{
    public sealed class NamedPipeReadController : NamedPipeControllerBase
    {
        private readonly Thread ProcessThread;
        private readonly Action<string> OnDataReceivedCallBack;
        private bool ExitSignal = false;

        private void ReadProcess()
        {
            try
            {
                if (PipeStream.IsConnected)
                {
                    using (StreamReader Reader = new StreamReader(PipeStream, new UTF8Encoding(false), false, 1024, true))
                    {
                        while (!ExitSignal && PipeStream.IsConnected)
                        {
                            try
                            {
                                string ReadText = Reader.ReadLine();

                                if (!string.IsNullOrEmpty(ReadText))
                                {
                                    OnDataReceivedCallBack?.Invoke(ReadText);
                                }
                            }
                            catch (Exception ex)
                            {
                                LogTracer.Log(ex, "An exception was threw when receiving pipeline data");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, "Could not receive pipeline data");
            }
        }

        public NamedPipeReadController(Action<string> OnDataReceivedCallBack, uint ProcessId, string PipeName) : base(ProcessId, PipeName)
        {
            this.OnDataReceivedCallBack = OnDataReceivedCallBack;

            ProcessThread = new Thread(ReadProcess)
            {
                Priority = ThreadPriority.Normal,
                IsBackground = true
            };
            ProcessThread.Start();
        }

        public override void Dispose()
        {
            ExitSignal = true;
            base.Dispose();
        }
    }
}
