using Microsoft.Win32.SafeHandles;
using System;
using System.Threading;

namespace RX_Explorer.Class
{
    public sealed class ProcessWaitHandle : WaitHandle
    {
        public ProcessWaitHandle(IntPtr ProcessHandle, bool OwnHandle)
        {
            SafeWaitHandle = new SafeWaitHandle(ProcessHandle, OwnHandle);
        }
    }
}
