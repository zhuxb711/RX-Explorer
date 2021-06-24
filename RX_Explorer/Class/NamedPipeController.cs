using Microsoft.Win32.SafeHandles;
using System;
using System.ComponentModel;
using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace RX_Explorer.Class
{
    public sealed class NamedPipeController : IDisposable
    {
        private readonly NamedPipeServerStream ClientStream;
        private readonly Thread ProcessThread;

        private bool ExitSignal = false;

        public string PipeUniqueId { get; }

        public event EventHandler<string> OnDataReceived;

        private void ThreadProcess()
        {
            ClientStream.WaitForConnection();

            using (StreamReader Reader = new StreamReader(ClientStream, new UTF8Encoding(false), false, 1024, true))
            {
                while (!ExitSignal)
                {
                    try
                    {
                        string ReadText = Reader.ReadLine();

                        if (!string.IsNullOrEmpty(ReadText))
                        {
                            OnDataReceived?.Invoke(this, ReadText);
                        }
                    }
                    catch (Exception ex)
                    {
                        LogTracer.Log(ex, "An exception was threw when receiving pipeline data");
                    }
                }
            }
        }

        public NamedPipeController()
        {
            PipeUniqueId = Guid.NewGuid().ToString("D");

            SafePipeHandle Handle = WIN_Native_API.CreateHandleForNamedPipe($"Explorer_NamedPipe_{PipeUniqueId}");

            if (!Handle.IsInvalid && !Handle.IsClosed)
            {
                ClientStream = new NamedPipeServerStream(PipeDirection.InOut, false, false, Handle);

                ProcessThread = new Thread(ThreadProcess)
                {
                    Priority = ThreadPriority.Normal,
                    IsBackground = true
                };
                ProcessThread.Start();
            }
            else
            {
                LogTracer.Log(new Win32Exception(Marshal.GetLastWin32Error()), "Could not create named pipe");
            }
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);

            ExitSignal = true;
            ClientStream?.Dispose();
        }

        ~NamedPipeController()
        {
            Dispose();
        }
    }
}
