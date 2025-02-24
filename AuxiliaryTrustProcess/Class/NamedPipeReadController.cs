using System;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Vanara.PInvoke;

namespace AuxiliaryTrustProcess.Class
{
    internal class NamedPipeReadController : NamedPipeControllerBase
    {
        private readonly Thread ProcessThread;
        private readonly TaskCompletionSource<bool> ConnectionSet;
        public event EventHandler<NamedPipeDataReceivedArgs> OnDataReceived;

        private void ReadProcess()
        {
            Ole32.OleInitialize();

            try
            {
                if (!IsConnected)
                {
                    try
                    {
                        PipeStream.Connect(TimeSpan.FromSeconds(15));
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
                            OnDataReceived?.Invoke(this, new NamedPipeDataReceivedArgs(ReadText));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                OnDataReceived?.Invoke(this, new NamedPipeDataReceivedArgs(ex));
            }
            finally
            {
                Ole32.OleUninitialize();
            }
        }

        public override async Task<bool> WaitForConnectionAsync(TimeSpan Timeout)
        {
            if (await Task.WhenAny(ConnectionSet.Task, Task.Delay(Timeout)) == ConnectionSet.Task)
            {
                return ConnectionSet.Task.Result;
            }

            return false;
        }

        public NamedPipeReadController(string PackageFamilyName, string PipeId) : base(PackageFamilyName, PipeId)
        {
            ConnectionSet = new TaskCompletionSource<bool>();

            ProcessThread = new Thread(ReadProcess)
            {
                Priority = ThreadPriority.Normal,
                IsBackground = true
            };
            ProcessThread.TrySetApartmentState(ApartmentState.STA);
            ProcessThread.Start();
        }
    }
}
