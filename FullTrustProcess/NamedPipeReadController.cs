using Microsoft.Toolkit.Deferred;
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

        private void ReadProcess()
        {
            try
            {
                if (PipeStream.IsConnected)
                {
                    using (StreamReader Reader = new StreamReader(PipeStream, new UTF8Encoding(false), false, 1024, true))
                    {
                        while (IsConnected)
                        {
                            string ReadText = Reader.ReadLine();

                            if (!string.IsNullOrEmpty(ReadText))
                            {
                                OnDataReceived?.InvokeAsync(this, new NamedPipeDataReceivedArgs(ReadText)).Wait();
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, "Could not receive pipeline data");
            }
            finally
            {
                Dispose();
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
    }
}
