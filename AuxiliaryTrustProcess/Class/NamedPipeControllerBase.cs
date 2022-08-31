using System;
using System.IO.Pipes;
using System.Threading.Tasks;

namespace AuxiliaryTrustProcess.Class
{
    public abstract class NamedPipeControllerBase : IDisposable
    {
        public string PipeId { get; }

        public bool IsConnected => (PipeStream?.IsConnected).GetValueOrDefault() && !IsDisposed;

        protected bool IsDisposed { get; private set; }

        protected NamedPipeClientStream PipeStream { get; }

        public abstract Task<bool> WaitForConnectionAsync(int TimeoutMilliseconds);

        protected NamedPipeControllerBase(string PackageFamilyName, string Id)
        {
            PipeId = Id;
            PipeStream = new NamedPipeClientStream(".", Helper.GetActualNamedPipeNameFromUwpApplication(PipeId, Helper.GetPackageNameFromPackageFamilyName(PackageFamilyName)), PipeDirection.InOut, PipeOptions.Asynchronous | PipeOptions.WriteThrough);
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
