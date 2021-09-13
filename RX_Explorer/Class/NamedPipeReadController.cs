using Microsoft.Toolkit.Deferred;
using ShareClassLibrary;
using System;
using System.IO;
using System.Text;
using System.Threading;

namespace RX_Explorer.Class
{
    public sealed class NamedPipeReadController : NamedPipeControllerBase
    {
        public event EventHandler<NamedPipeDataReceivedArgs> OnDataReceived;
        private readonly Thread ProcessThread;

        public static bool TryCreateNamedPipe(out NamedPipeReadController Controller)
        {
            try
            {
                Controller = new NamedPipeReadController();
                return true;
            }
            catch (Exception ex)
            {
                Controller = null;
                LogTracer.Log(ex, "Could not create named pipe");
                return false;
            }
        }

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

        private NamedPipeReadController()
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
