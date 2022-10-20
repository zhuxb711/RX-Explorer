using System;
using System.Runtime.InteropServices;

namespace AuxiliaryTrustProcess.Class
{
    internal class NativeWin32Struct
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct COPYDATASTRUCT
        {
            public UIntPtr dwData;
            public int cbData;
            public IntPtr lpData;
        }
    }
}
