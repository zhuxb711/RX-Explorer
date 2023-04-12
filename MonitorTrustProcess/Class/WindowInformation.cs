using SharedLibrary;
using System;

namespace MonitorTrustProcess.Class
{
    public sealed class WindowInformation
    {
        public string FileName { get; }

        public uint ProcessId { get; }

        public WindowState WindowState { get; }

        public IntPtr ApplicationFrameWindowHandle { get; }

        public IntPtr CoreWindowHandle { get; }

        public bool IsValidInfomation => ProcessId > 0 && (ApplicationFrameWindowHandle.CheckIfValidPtr() || CoreWindowHandle.CheckIfValidPtr());

        public WindowInformation(string FileName, uint ProcessId, WindowState WindowState, IntPtr ApplicationFrameWindowHandle, IntPtr CoreWindowHandle)
        {
            this.FileName = FileName;
            this.ProcessId = ProcessId;
            this.WindowState = WindowState;
            this.ApplicationFrameWindowHandle = ApplicationFrameWindowHandle;
            this.CoreWindowHandle = CoreWindowHandle;
        }
    }
}
