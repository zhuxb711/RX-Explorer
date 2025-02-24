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

        public abstract Task<bool> WaitForConnectionAsync(TimeSpan Timeout);

        protected NamedPipeControllerBase(string PackageFamilyName, string Id)
        {
            PipeId = Id;
            PipeStream = new NamedPipeClientStream(".", Helper.GetActualNamedPipeFromUwpApplication(PipeId, Helper.GetPackageNameFromPackageFamilyName(PackageFamilyName)), PipeDirection.InOut, PipeOptions.Asynchronous | PipeOptions.WriteThrough);
        }

        public virtual void Dispose()
        {
            GC.SuppressFinalize(this);

            if (!IsDisposed)
            {
                IsDisposed = true;
                PipeStream?.Dispose();
            }
        }

        ~NamedPipeControllerBase()
        {
            Dispose();
        }
    }
}
