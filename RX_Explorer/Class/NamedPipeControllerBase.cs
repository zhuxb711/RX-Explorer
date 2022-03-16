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

        public abstract Task<bool> WaitForConnectionAsync(int TimeoutMilliseconds);

        protected NamedPipeControllerBase(string Id)
        {
            PipeId = Id;
            PipeStream = new NamedPipeServerStream(@$"LOCAL\{PipeId}", PipeDirection.InOut, MaxAllowedConnection, PipeTransmissionMode.Message, PipeOptions.Asynchronous | PipeOptions.WriteThrough);
        }

        public virtual void Dispose()
        {
            if (!IsDisposed)
            {
                IsDisposed = true;
                PipeStream?.Dispose();
                GC.SuppressFinalize(this);
            }
        }

        ~NamedPipeControllerBase()
        {
            Dispose();
        }
    }
}
