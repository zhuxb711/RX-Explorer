using Microsoft.Toolkit.Deferred;
using ShareClassLibrary;
using System;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FullTrustProcess
{
    public class NamedPipeReadController : NamedPipeControllerBase
    {
        private readonly Thread ProcessThread;
        private readonly TaskCompletionSource<bool> ConnectionSet;
        public event EventHandler<NamedPipeDataReceivedArgs> OnDataReceived;

        private void ReadProcess()
        {
            try
            {
                if (!IsConnected)
                {
                    try
                    {
                        PipeStream.Connect(10000);
                        PipeStream.ReadMode = PipeTransmissionMode.Message;
                    }
                    catch (IOException)
                    {
                        LogTracer.Log("Could not read pipeline data because the pipeline is closed");
                    }
                    catch (TimeoutException)
                    {
                        LogTracer.Log("Could not read pipeline data because connection timeout");
                    }
                }

                ConnectionSet.SetResult(IsConnected);

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
                        } while (IsConnected && !PipeStream.IsMessageComplete);

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

        public override async Task<bool> WaitForConnectionAsync(int TimeoutMilliseconds)
        {
            if (await Task.WhenAny(ConnectionSet.Task, Task.Delay(TimeoutMilliseconds)) == ConnectionSet.Task)
            {
                return ConnectionSet.Task.Result;
            }
            else
            {
                return false;
            }
        }

        public NamedPipeReadController(string PipeId) : base(PipeId)
        {
            ConnectionSet = new TaskCompletionSource<bool>();

            ProcessThread = new Thread(ReadProcess)
            {
                Priority = ThreadPriority.Normal,
                IsBackground = true
            };
            ProcessThread.Start();
        }
    }
}
