using System;
using System.IO.Pipes;

namespace RX_Explorer.Class
{
    public abstract class NamedPipeControllerBase : IDisposable
    {
        protected NamedPipeServerStream PipeStream { get; }

        public abstract string PipeId { get; }

        public abstract PipeDirection PipeMode { get; }

        protected virtual int MaxAllowedConnection { get; } = -1;

        public bool IsConnected => (PipeStream?.IsConnected).GetValueOrDefault();

        protected bool IsDisposed { get; private set; }

        public static bool TryCreateNamedPipe<T>(out T Controller) where T : NamedPipeControllerBase, new()
        {
            try
            {
                Controller = new T();
                return true;
            }
            catch (Exception ex)
            {
                Controller = null;
                LogTracer.Log(ex, "Could not create named pipe");
                return false;
            }
        }


        protected NamedPipeControllerBase()
        {
            PipeStream = new NamedPipeServerStream(@$"LOCAL\{PipeId}", PipeMode, MaxAllowedConnection, PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.WriteThrough);
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
