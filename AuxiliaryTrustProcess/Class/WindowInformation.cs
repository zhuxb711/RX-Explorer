using SharedLibrary;
using Vanara.PInvoke;

namespace AuxiliaryTrustProcess.Class
{
    public sealed class WindowInformation
    {
        public string FileName { get; }

        public uint ProcessId { get; }

        public WindowState State { get; }

        public HWND ApplicationFrameWindowHandle { get; }

        public HWND CoreWindowHandle { get; }

        public bool IsValidInfomation => ProcessId > 0 && (!ApplicationFrameWindowHandle.IsNull || !CoreWindowHandle.IsNull);

        public WindowInformation(string FileName, uint ProcessId, WindowState State, HWND ApplicationFrameWindowHandle, HWND CoreWindowHandle)
        {
            this.FileName = FileName;
            this.ProcessId = ProcessId;
            this.State = State;
            this.ApplicationFrameWindowHandle = ApplicationFrameWindowHandle;
            this.CoreWindowHandle = CoreWindowHandle;
        }
    }
}
