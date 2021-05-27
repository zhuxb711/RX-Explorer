using ShareClassLibrary;
using Vanara.PInvoke;

namespace FullTrustProcess
{
    public sealed class WindowInformation
    {
        public string ExeName { get; }

        public uint PID { get; }

        public WindowState State { get; }

        public HWND Handle { get; }

        public WindowInformation(string ExeName, uint PID, WindowState State, HWND Handle)
        {
            this.ExeName = ExeName;
            this.PID = PID;
            this.State = State;
            this.Handle = Handle;
        }
    }
}
