using System;
using System.Collections.Concurrent;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AuxiliaryTrustProcess
{
    public sealed class NamedPipeWriteController : NamedPipeControllerBase
    {
        private readonly Thread ProcessThread;
        private readonly TaskCompletionSource<bool> ConnectionSet;
        private readonly BlockingCollection<string> MessageCollection = new BlockingCollection<string>();

        public void SendData(string Data)
        {
            if (IsConnected && !MessageCollection.IsAddingCompleted)
            {
                MessageCollection.Add(Data);
            }
            else
            {
                throw new InvalidOperationException("Named pipe is disconnected and could not send data anymore");
            }
        }

        private void WriteProcess()
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
                        LogTracer.Log("Could not write pipeline data because the pipeline is closed");
                    }
                    catch (TimeoutException)
                    {
                        LogTracer.Log("Could not write pipeline data because connection timeout");
                    }
                }

                ConnectionSet.SetResult(IsConnected);

                while (IsConnected)
                {
                    string Message = null;

                    try
                    {
                        Message = MessageCollection.Take();
                    }
                    catch (Exception)
                    {
                        //No need to handle this exception
                    }

                    if (!string.IsNullOrEmpty(Message))
                    {
                        byte[] ByteArray = Encoding.Unicode.GetBytes(Message);
                        PipeStream.Write(ByteArray, 0, ByteArray.Length);
                        PipeStream.WaitForPipeDrain();
                    }
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, "Could not send pipeline data");
            }
        }

        public override void Dispose()
        {
            if (!IsDisposed)
            {
                base.Dispose();
                MessageCollection.CompleteAdding();
                MessageCollection.Dispose();
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

        public NamedPipeWriteController(string PackageFamilyName, string PipeId) : base(PackageFamilyName, PipeId)
        {
            ConnectionSet = new TaskCompletionSource<bool>();

            ProcessThread = new Thread(WriteProcess)
            {
                Priority = ThreadPriority.Normal,
                IsBackground = true
            };
            ProcessThread.Start();
        }

        ~NamedPipeWriteController()
        {
            Dispose();
        }
    }
}
