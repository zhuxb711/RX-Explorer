using Microsoft.Toolkit.Deferred;
using ShareClassLibrary;
using System;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;

namespace FullTrustProcess
{
    public class NamedPipeReadController : NamedPipeControllerBase
    {
        public event EventHandler<NamedPipeDataReceivedArgs> OnDataReceived;
        private readonly Thread ProcessThread;

        public override PipeDirection PipeMode => PipeDirection.In;

        private void ReadProcess()
        {
            try
            {
                if (!IsConnected)
                {
                    PipeStream.Connect(2000);
                }

                using (StreamReader Reader = new StreamReader(PipeStream, new UTF8Encoding(false), false, 512, true))
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
            catch (Exception ex)
            {
                OnDataReceived?.InvokeAsync(this, new NamedPipeDataReceivedArgs(ex)).Wait();
            }
            finally
            {
                Dispose();
            }
        }

        public NamedPipeReadController(string PipeId) : base(PipeId)
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
