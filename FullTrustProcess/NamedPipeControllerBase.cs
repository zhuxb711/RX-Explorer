using System;
using System.Diagnostics;
using System.IO.Pipes;
using System.Linq;
using System.Threading.Tasks;
using Vanara.PInvoke;
using Windows.ApplicationModel;

namespace FullTrustProcess
{
    public abstract class NamedPipeControllerBase : IDisposable
    {
        public string PipeId { get; }

        public bool IsConnected => (PipeStream?.IsConnected).GetValueOrDefault() && !IsDisposed;

        protected bool IsDisposed { get; private set; }

        protected NamedPipeClientStream PipeStream { get; }

        public abstract Task<bool> WaitForConnectionAsync(int TimeoutMilliseconds);

        private string GetActualNamedPipeStringFromUWP(string PipeId)
        {
            using (Process CurrentProcess = Process.GetCurrentProcess())
            {
                if (UserEnv.DeriveAppContainerSidFromAppContainerName(Package.Current.Id.Name, out AdvApi32.SafeAllocatedSID Sid).Succeeded)
                {
                    try
                    {
                        return $@"Sessions\{CurrentProcess.SessionId}\AppContainerNamedObjects\{string.Join("-", ((PSID)Sid).ToString("D").Split(new string[] { "-" }, StringSplitOptions.RemoveEmptyEntries).Take(11))}\{PipeId}";
                    }
                    finally
                    {
                        Sid.Dispose();
                    }
                }
                else
                {
                    return null;
                }
            }
        }

        protected NamedPipeControllerBase(string Id)
        {
            PipeId = Id;
            PipeStream = new NamedPipeClientStream(".", GetActualNamedPipeStringFromUWP(PipeId), PipeDirection.InOut, PipeOptions.Asynchronous | PipeOptions.WriteThrough);
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
