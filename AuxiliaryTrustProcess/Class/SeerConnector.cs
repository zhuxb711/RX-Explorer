using SharedLibrary;
using System;
using System.Runtime.InteropServices;
using Vanara.PInvoke;

namespace AuxiliaryTrustProcess.Class
{
    public sealed class SeerConnector
    {
        private const uint ToggleCommand = 5000;
        private const uint VisibleCommand = 5004;

        [StructLayout(LayoutKind.Sequential)]
        private struct COPYDATASTRUCT
        {
            public IntPtr dwData;
            public int cbData;
            public IntPtr lpData;
        }

        public static bool CheckIsAvailable()
        {
            return User32.FindWindowEx(HWND.NULL, HWND.NULL, "SeerWindowClass", null).DangerousGetHandle().CheckIfValidPtr();
        }

        public static void ToggleService(string Path)
        {
            if (CheckIsAvailable())
            {
                try
                {
                    HWND Window = User32.FindWindowEx(HWND.NULL, HWND.NULL, "SeerWindowClass", null);

                    if (Window.DangerousGetHandle().CheckIfValidPtr())
                    {
                        IntPtr PathPtr = Marshal.StringToHGlobalUni(Path);

                        try
                        {
                            COPYDATASTRUCT ToggleData = new COPYDATASTRUCT
                            {
                                dwData = new IntPtr(ToggleCommand),
                                lpData = PathPtr,
                                cbData = (Path.Length + 1) * 2
                            };

                            IntPtr ToggleStructPtr = Marshal.AllocHGlobal(Marshal.SizeOf<COPYDATASTRUCT>());

                            try
                            {
                                Marshal.StructureToPtr(ToggleData, ToggleStructPtr, false);

                                try
                                {
                                    User32.SendMessage(Window, User32.WindowMessage.WM_COPYDATA, IntPtr.Zero, ToggleStructPtr);
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
                    HWND Window = User32.FindWindowEx(HWND.NULL, HWND.NULL, "SeerWindowClass", null);

                    if (Window.DangerousGetHandle().CheckIfValidPtr())
                    {
                        COPYDATASTRUCT VisibleData = new COPYDATASTRUCT
                        {
                            dwData = new IntPtr(VisibleCommand),
                            lpData = IntPtr.Zero,
                            cbData = 0
                        };

                        IntPtr VisibleStructPtr = Marshal.AllocHGlobal(Marshal.SizeOf<COPYDATASTRUCT>());

                        try
                        {
                            Marshal.StructureToPtr(VisibleData, VisibleStructPtr, false);

                            try
                            {
                                if (User32.SendMessage(Window, User32.WindowMessage.WM_COPYDATA, IntPtr.Zero, VisibleStructPtr) == new IntPtr(1))
                                {
                                    IntPtr PathPtr = Marshal.StringToHGlobalUni(Path);

                                    try
                                    {
                                        COPYDATASTRUCT ToggleData = new COPYDATASTRUCT
                                        {
                                            dwData = new IntPtr(ToggleCommand),
                                            lpData = PathPtr,
                                            cbData = (Path.Length + 1) * 2
                                        };

                                        IntPtr ToggleStructPtr = Marshal.AllocHGlobal(Marshal.SizeOf<COPYDATASTRUCT>());

                                        try
                                        {
                                            Marshal.StructureToPtr(ToggleData, ToggleStructPtr, false);

                                            try
                                            {
                                                User32.SendMessage(Window, User32.WindowMessage.WM_COPYDATA, IntPtr.Zero, ToggleStructPtr);
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
