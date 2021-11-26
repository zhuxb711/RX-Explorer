using System;
using System.Diagnostics;
using System.IO.Pipes;
using System.Linq;
using Vanara.PInvoke;
using Windows.ApplicationModel;

namespace FullTrustProcess
{
    public abstract class NamedPipeControllerBase : IDisposable
    {
        protected NamedPipeClientStream PipeStream { get; private set; }

        public bool IsConnected => (PipeStream?.IsConnected).GetValueOrDefault();

        public abstract PipeDirection PipeMode { get; }

        protected bool IsDisposed { get; private set; }

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

        protected NamedPipeControllerBase(string PipeId)
        {
            PipeStream = new NamedPipeClientStream(".", GetActualNamedPipeStringFromUWP(PipeId), PipeMode, PipeOptions.Asynchronous | PipeOptions.WriteThrough);
        }

        public virtual void Dispose()
        {
            if (!IsDisposed)
            {
                IsDisposed = true;
                PipeStream?.Dispose();
                PipeStream = null;
                GC.SuppressFinalize(this);
            }
        }

        ~NamedPipeControllerBase()
        {
            Dispose();
        }
    }
}
