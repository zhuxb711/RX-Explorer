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
        private readonly Thread ProcessThread;
        public event EventHandler<NamedPipeDataReceivedArgs> OnDataReceived;

        private void ReadProcess()
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
                    using (MemoryStream MStream = new MemoryStream())
                    {
                        byte[] ReadBuffer = new byte[1024];

                        do
                        {
                            int BytesRead = PipeStream.Read(ReadBuffer, 0, ReadBuffer.Length);

                            if (BytesRead > 0)
                            {
                                MStream.Write(ReadBuffer, 0, BytesRead);
                            }
                        } while (!PipeStream.IsMessageComplete);

                        string ReadText = Encoding.Unicode.GetString(MStream.ToArray());

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
