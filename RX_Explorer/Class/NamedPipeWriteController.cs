using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RX_Explorer.Class
{
    public class NamedPipeWriteController : NamedPipeControllerBase
    {
        private CancellationTokenSource Cancellation;
        private readonly Thread ProcessThread;
        private readonly TaskCompletionSource<bool> ConnectionSet;
        private readonly BlockingCollection<string> MessageCollection;

        protected override int MaxAllowedConnection => 1;

        private void WriteProcess()
        {
            try
            {
                if (!IsConnected)
                {
                    using (CancellationTokenSource LocalCancellation = CancellationTokenSource.CreateLinkedTokenSource(Cancellation.Token))
                    {
                        try
                        {
                            PipeStream.WaitForConnectionAsync(LocalCancellation.Token).Wait();
                        }
                        catch (AggregateException ex) when (ex.InnerException is IOException)
                        {
                            LogTracer.Log("Could not write pipeline data because the pipeline is closed");
                        }
                        catch (AggregateException ex) when (ex.InnerException is OperationCanceledException)
                        {
                            LogTracer.Log("Could not write pipeline data because connection timeout");
                        }
                        catch (Exception ex)
                        {
                            LogTracer.Log(ex, "Could not write pipeline data because unknown exception");
                        }
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

        public override void Dispose()
        {
            if (Execution.CheckAlreadyExecuted(this))
            {
                throw new ObjectDisposedException(nameof(NamedPipeWriteController));
            }

            GC.SuppressFinalize(this);

            Execution.ExecuteOnce(this, () =>
            {
                MessageCollection.CompleteAdding();
                MessageCollection.Dispose();
                Cancellation?.Dispose();
                Cancellation = null;
            });

            base.Dispose();
        }

        public override async Task<bool> WaitForConnectionAsync(int TimeoutMilliseconds)
        {
            if (ConnectionSet.Task.IsCompleted)
            {
                return true;
            }
            else
            {
                if (await Task.WhenAny(ConnectionSet.Task, Task.Delay(TimeoutMilliseconds)) == ConnectionSet.Task)
                {
                    return ConnectionSet.Task.Result;
                }
                else
                {
                    Cancellation?.Cancel();
                }
            }

            return false;
        }

        public NamedPipeWriteController() : this($"Explorer_NamedPipe_Write_{Guid.NewGuid():D}")
        {

        }

        protected NamedPipeWriteController(string Id) : base(Id)
        {
            MessageCollection = new BlockingCollection<string>();
            Cancellation = new CancellationTokenSource();
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
