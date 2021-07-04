using Microsoft.Win32.SafeHandles;
using System;
using System.ComponentModel;
using System.IO.Pipes;
using System.Runtime.InteropServices;

namespace RX_Explorer.Class
{
    public class NamedPipeControllerBase : IDisposable
    {
        protected NamedPipeServerStream PipeStream { get; }

        public string PipeUniqueId { get; }

        public bool IsConnected
        {
            get
            {
                return (PipeStream?.IsConnected).GetValueOrDefault();
            }
        }

        protected NamedPipeControllerBase()
        {
            switch (this)
            {
                case NamedPipeReadController:
                    {
                        PipeUniqueId = $"Read_{Guid.NewGuid():D}";
                        SafePipeHandle Handle = WIN_Native_API.CreateHandleForNamedPipe($"Explorer_NamedPipe_{PipeUniqueId}", NamedPipeMode.Read);

                        if (!Handle.IsInvalid && !Handle.IsClosed)
                        {
                            PipeStream = new NamedPipeServerStream(PipeDirection.In, false, false, Handle);
                        }
                        else
                        {
                            throw new Win32Exception(Marshal.GetLastWin32Error());
                        }

                        break;
                    }
                case NamedPipeWriteController:
                    {
                        PipeUniqueId = $"Write_{Guid.NewGuid():D}";
                        SafePipeHandle Handle = WIN_Native_API.CreateHandleForNamedPipe($"Explorer_NamedPipe_{PipeUniqueId}", NamedPipeMode.Write);

                        if (!Handle.IsInvalid && !Handle.IsClosed)
                        {
                            PipeStream = new NamedPipeServerStream(PipeDirection.Out, false, false, Handle);
                        }
                        else
                        {
                            throw new Win32Exception(Marshal.GetLastWin32Error());
                        }

                        break;
                    }
                default:
                    {
                        throw new NotSupportedException();
                    }
            }
        }

        public virtual void Dispose()
        {
            GC.SuppressFinalize(this);
            PipeStream?.Dispose();
        }

        ~NamedPipeControllerBase()
        {
            Dispose();
        }
    }
}
