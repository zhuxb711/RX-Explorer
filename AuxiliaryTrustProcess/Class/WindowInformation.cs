using SharedLibrary;
using System;

namespace AuxiliaryTrustProcess.Class
{
    internal sealed class WindowInformation
    {
        public string FileName { get; }

        public uint ProcessId { get; }

        public WindowState State { get; }

        public IntPtr ApplicationFrameWindowHandle { get; }

        public IntPtr CoreWindowHandle { get; }

        public bool IsValidInfomation => ProcessId > 0 && (ApplicationFrameWindowHandle.CheckIfValidPtr() || CoreWindowHandle.CheckIfValidPtr());

        public WindowInformation(string FileName, uint ProcessId, WindowState State, IntPtr ApplicationFrameWindowHandle, IntPtr CoreWindowHandle)
        {
            this.FileName = FileName;
            this.ProcessId = ProcessId;
            this.State = State;
            this.ApplicationFrameWindowHandle = ApplicationFrameWindowHandle;
            this.CoreWindowHandle = CoreWindowHandle;
        }
    }
}
