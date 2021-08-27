using System;
using System.ComponentModel;
using System.IO.Pipes;
using System.Linq;
using System.Runtime.InteropServices;
using Vanara.PInvoke;
using Windows.ApplicationModel;

namespace FullTrustProcess
{
    public class NamedPipeControllerBase : IDisposable
    {
        protected NamedPipeClientStream PipeStream { get; private set; }

        public bool IsConnected
        {
            get
            {
                return (PipeStream?.IsConnected).GetValueOrDefault();
            }
        }

        private bool IsDisposed;

        private string GetActualNamedPipeStringFromUWP(uint ProcessId, string PipeName)
        {
            using (Kernel32.SafeHPROCESS PHandle = Kernel32.OpenProcess(new ACCESS_MASK(0x1000), false, ProcessId))
            {
                if (!PHandle.IsInvalid && !PHandle.IsNull)
                {
                    using (AdvApi32.SafeHTOKEN Token = AdvApi32.SafeHTOKEN.FromProcess(PHandle, AdvApi32.TokenAccess.TOKEN_QUERY))
                    {
                        if (!Token.IsInvalid && !Token.IsNull)
                        {
                            uint SessionId = Token.GetInfo<uint>(AdvApi32.TOKEN_INFORMATION_CLASS.TokenSessionId);

                            if (UserEnv.DeriveAppContainerSidFromAppContainerName(Package.Current.Id.Name, out AdvApi32.SafeAllocatedSID Sid).Succeeded)
                            {
                                try
                                {
                                    return $@"Sessions\{SessionId}\AppContainerNamedObjects\{string.Join("-", ((PSID)Sid).ToString("D").Split(new string[] { "-" }, StringSplitOptions.RemoveEmptyEntries).Take(11))}\{PipeName}";
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
                        else
                        {
                            return null;
                        }
                    }
                }
                else
                {
                    return null;
                }
            }
        }

        protected NamedPipeControllerBase(uint ProcessId, string PipeName)
        {
            string ActualPipePath = GetActualNamedPipeStringFromUWP(ProcessId, PipeName);

            if (!string.IsNullOrEmpty(ActualPipePath))
            {
                switch (this)
                {
                    case NamedPipeReadController:
                        {
                            PipeStream = new NamedPipeClientStream(".", ActualPipePath, PipeDirection.In, PipeOptions.WriteThrough);
                            PipeStream.Connect(2000);
                            break;
                        }
                    case NamedPipeWriteController:
                        {
                            PipeStream = new NamedPipeClientStream(".", ActualPipePath, PipeDirection.Out, PipeOptions.WriteThrough);
                            PipeStream.Connect(2000);
                            break;
                        }
                    default:
                        {
                            throw new NotSupportedException();
                        }
                }

            }
            else
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }
        }

        public void Dispose()
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
