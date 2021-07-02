using DeferredEvents;
using ShareClassLibrary;
using System;
using System.IO;
using System.Text;
using System.Threading;

namespace FullTrustProcess
{
    public sealed class NamedPipeReadController : NamedPipeControllerBase
    {
        public event EventHandler<NamedPipeDataReceivedArgs> OnDataReceived;
        private readonly Thread ProcessThread;
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
                                    OnDataReceived?.InvokeAsync(this, new NamedPipeDataReceivedArgs(ReadText)).Wait();
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

        public NamedPipeReadController(uint ProcessId, string PipeName) : base(ProcessId, PipeName)
        {
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
