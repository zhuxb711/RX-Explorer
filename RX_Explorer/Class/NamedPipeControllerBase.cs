using System;
using System.IO.Pipes;
using System.Threading.Tasks;

namespace RX_Explorer.Class
{
    public abstract class NamedPipeControllerBase : IDisposable
    {
        public string PipeId { get; }

        public bool IsConnected => (PipeStream?.IsConnected).GetValueOrDefault() && !IsDisposed;

        protected bool IsDisposed { get; private set; }

        protected virtual int MaxAllowedConnection { get; } = -1;

        protected NamedPipeServerStream PipeStream { get; }

        public abstract Task<bool> WaitForConnectionAsync(TimeSpan Timeout);

        protected NamedPipeControllerBase(string Id)
        {
            PipeId = Id;
            PipeStream = new NamedPipeServerStream(@$"LOCAL\{PipeId}", PipeDirection.InOut, MaxAllowedConnection, PipeTransmissionMode.Message, PipeOptions.Asynchronous | PipeOptions.WriteThrough);
        }

        public virtual void Dispose()
        {
            if (Execution.CheckAlreadyExecuted(this))
            {
                throw new ObjectDisposedException(nameof(NamedPipeControllerBase));
            }

            GC.SuppressFinalize(this);

            Execution.ExecuteOnce(this, () =>
            {
                IsDisposed = true;
                PipeStream?.Dispose();
            });
        }

        ~NamedPipeControllerBase()
        {
            Dispose();
        }
    }
}
