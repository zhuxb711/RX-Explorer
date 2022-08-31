using SharedLibrary;
using System;
using System.Runtime.InteropServices;
using Vanara.PInvoke;

namespace AuxiliaryTrustProcess.Class
{
    public sealed class SeerConnector
    {
        private const int Timeout = 1000;
        private const uint ToggleCommand = 5000;
        private const uint VisibleCommand = 5004;

        [StructLayout(LayoutKind.Sequential)]
        private struct COPYDATASTRUCT
        {
            public UIntPtr dwData;
            public int cbData;
            public IntPtr lpData;
        }

        public static bool CheckIsAvailable()
        {
            return User32.FindWindow("SeerWindowClass", null).DangerousGetHandle().CheckIfValidPtr();
        }

        public static void ToggleService(string Path)
        {
            if (CheckIsAvailable())
            {
                try
                {
                    HWND Window = User32.FindWindow("SeerWindowClass", null);

                    if (Window.DangerousGetHandle().CheckIfValidPtr())
                    {
                        IntPtr PathPtr = Marshal.StringToHGlobalUni(Path);

                        try
                        {
                            COPYDATASTRUCT ToggleData = new COPYDATASTRUCT
                            {
                                dwData = new UIntPtr(ToggleCommand),
                                lpData = PathPtr,
                                cbData = (Path.Length + 1) * 2
                            };

                            IntPtr ToggleStructPtr = Marshal.AllocHGlobal(Marshal.SizeOf<COPYDATASTRUCT>());

                            try
                            {
                                Marshal.StructureToPtr(ToggleData, ToggleStructPtr, false);

                                try
                                {
                                    IntPtr Result = IntPtr.Zero;

                                    if (User32.SendMessageTimeout(Window, (uint)User32.WindowMessage.WM_COPYDATA, IntPtr.Zero, ToggleStructPtr, User32.SMTO.SMTO_ABORTIFHUNG, Timeout, ref Result) == IntPtr.Zero)
                                    {
                                        LogTracer.Log($"Could not send Toggle command to Seer because it timeout after {Timeout}");
                                    }
                                }
                                finally
                                {
                                    Marshal.DestroyStructure<COPYDATASTRUCT>(ToggleStructPtr);
                                }
                            }
                            finally
                            {
                                Marshal.FreeHGlobal(ToggleStructPtr);
                            }
                        }
                        finally
                        {
                            Marshal.FreeHGlobal(PathPtr);
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogTracer.Log(ex, $"An exception was threw in {nameof(ToggleService)}");
                }
            }
        }

        public static void SwitchService(string Path)
        {
            if (CheckIsAvailable())
            {
                try
                {
                    HWND Window = User32.FindWindow("SeerWindowClass", null);

                    if (Window.DangerousGetHandle().CheckIfValidPtr())
                    {
                        COPYDATASTRUCT VisibleData = new COPYDATASTRUCT
                        {
                            dwData = new UIntPtr(VisibleCommand),
                            lpData = IntPtr.Zero,
                            cbData = 0
                        };

                        IntPtr VisibleStructPtr = Marshal.AllocHGlobal(Marshal.SizeOf<COPYDATASTRUCT>());

                        try
                        {
                            Marshal.StructureToPtr(VisibleData, VisibleStructPtr, false);

                            try
                            {
                                IntPtr Result = IntPtr.Zero;

                                if (User32.SendMessageTimeout(Window, (uint)User32.WindowMessage.WM_COPYDATA, IntPtr.Zero, VisibleStructPtr, User32.SMTO.SMTO_ABORTIFHUNG, Timeout, ref Result) != IntPtr.Zero)
                                {
                                    if (Result.ToInt64() > 0)
                                    {
                                        IntPtr PathPtr = Marshal.StringToHGlobalUni(Path);

                                        try
                                        {
                                            COPYDATASTRUCT ToggleData = new COPYDATASTRUCT
                                            {
                                                dwData = new UIntPtr(ToggleCommand),
                                                lpData = PathPtr,
                                                cbData = (Path.Length + 1) * 2
                                            };

                                            IntPtr ToggleStructPtr = Marshal.AllocHGlobal(Marshal.SizeOf<COPYDATASTRUCT>());

                                            try
                                            {
                                                Marshal.StructureToPtr(ToggleData, ToggleStructPtr, false);

                                                try
                                                {
                                                    if (User32.SendMessageTimeout(Window, (uint)User32.WindowMessage.WM_COPYDATA, IntPtr.Zero, ToggleStructPtr, User32.SMTO.SMTO_ABORTIFHUNG, Timeout, ref Result) == IntPtr.Zero)
                                                    {
                                                        LogTracer.Log($"Could not send Switch command to Seer because it timeout after {Timeout}");
                                                    }
                                                }
                                                finally
                                                {
                                                    Marshal.DestroyStructure<COPYDATASTRUCT>(ToggleStructPtr);
                                                }
                                            }
                                            finally
                                            {
                                                Marshal.FreeHGlobal(ToggleStructPtr);
                                            }
                                        }
                                        finally
                                        {
                                            Marshal.FreeHGlobal(PathPtr);
                                        }
                                    }
                                }
                                else
                                {
                                    LogTracer.Log($"Could not send Visible command to Seer because it timeout after {Timeout}");
                                }
                            }
                            finally
                            {
                                Marshal.DestroyStructure<COPYDATASTRUCT>(VisibleStructPtr);
                            }
                        }
                        finally
                        {
                            Marshal.FreeHGlobal(VisibleStructPtr);
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogTracer.Log(ex, $"An exception was threw in {nameof(SwitchService)}");
                }
            }
        }
    }
}
