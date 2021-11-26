using Microsoft.Toolkit.Deferred;
using ShareClassLibrary;
using System;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;

namespace RX_Explorer.Class
{
    public class NamedPipeReadController : NamedPipeControllerBase
    {
        public event EventHandler<NamedPipeDataReceivedArgs> OnDataReceived;
        private readonly Thread ProcessThread;
        private readonly string PipeUniqueId = $"Explorer_NamedPipe_Read_{Guid.NewGuid():D}";

        public override string PipeId => PipeUniqueId;

        public override PipeDirection PipeMode => PipeDirection.In;

        protected override int MaxAllowedConnection => 1;

        private void ReadProcess()
        {
            try
            {
                if (!IsConnected)
                {
                    PipeStream.WaitForConnection();
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

        public NamedPipeReadController()
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
