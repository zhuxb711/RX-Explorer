using AuxiliaryTrustProcess.Interface;
using SharedLibrary;
using System;
using System.Runtime.InteropServices;
using Vanara.PInvoke;

namespace AuxiliaryTrustProcess.Class
{
    public sealed class SeerServiceProvider : IPreviewServiceProvider
    {
        private const int Timeout = 1000;
        private const uint ToggleCommand = 5000;
        private const uint VisibleCommand = 5004;
        private const uint CloseCommand = 5005;

        private static SeerServiceProvider Instance;
        private static readonly object Locker = new object();

        public static SeerServiceProvider Current
        {
            get
            {
                lock (Locker)
                {
                    return Instance ??= new SeerServiceProvider();
                }
            }
        }

        public bool CheckServiceAvailable()
        {
            return User32.FindWindow("SeerWindowClass", null).DangerousGetHandle().CheckIfValidPtr();
        }

        public bool CheckWindowVisible()
        {
            if (CheckServiceAvailable())
            {
                HWND Window = User32.FindWindow("SeerWindowClass", null);

                if (Window.DangerousGetHandle().CheckIfValidPtr())
                {
                    NativeWin32Struct.COPYDATASTRUCT VisibleData = new NativeWin32Struct.COPYDATASTRUCT
                    {
                        dwData = new UIntPtr(VisibleCommand),
                        lpData = IntPtr.Zero,
                        cbData = 0
                    };

                    IntPtr VisibleStructPtr = Marshal.AllocHGlobal(Marshal.SizeOf<NativeWin32Struct.COPYDATASTRUCT>());

                    try
                    {
                        Marshal.StructureToPtr(VisibleData, VisibleStructPtr, false);

                        try
                        {
                            IntPtr Result = IntPtr.Zero;

                            if (User32.SendMessageTimeout(Window, (uint)User32.WindowMessage.WM_COPYDATA, IntPtr.Zero, VisibleStructPtr, User32.SMTO.SMTO_ABORTIFHUNG, Timeout, ref Result) != IntPtr.Zero)
                            {
                                return Result.ToInt64() > 0;
                            }
                            else
                            {
                                LogTracer.Log($"Could not send Visible command to Seer because it timeout after {Timeout}");
                            }
                        }
                        finally
                        {
                            Marshal.DestroyStructure<NativeWin32Struct.COPYDATASTRUCT>(VisibleStructPtr);
                        }
                    }
                    finally
                    {
                        Marshal.FreeHGlobal(VisibleStructPtr);
                    }
                }
            }

            return false;
        }

        public bool ToggleServiceWindow(string Path)
        {
            if (CheckServiceAvailable())
            {
                try
                {
                    HWND Window = User32.FindWindow("SeerWindowClass", null);

                    if (Window.DangerousGetHandle().CheckIfValidPtr())
                    {
                        IntPtr PathPtr = Marshal.StringToHGlobalUni(Path);

                        try
                        {
                            NativeWin32Struct.COPYDATASTRUCT ToggleData = new NativeWin32Struct.COPYDATASTRUCT
                            {
                                dwData = new UIntPtr(ToggleCommand),
                                lpData = PathPtr,
                                cbData = (Path.Length + 1) * 2
                            };

                            IntPtr ToggleStructPtr = Marshal.AllocHGlobal(Marshal.SizeOf<NativeWin32Struct.COPYDATASTRUCT>());

                            try
                            {
                                Marshal.StructureToPtr(ToggleData, ToggleStructPtr, false);

                                try
                                {
                                    IntPtr Result = IntPtr.Zero;

                                    if (User32.SendMessageTimeout(Window, (uint)User32.WindowMessage.WM_COPYDATA, IntPtr.Zero, ToggleStructPtr, User32.SMTO.SMTO_ABORTIFHUNG, Timeout, ref Result) != IntPtr.Zero)
                                    {
                                        return true;
                                    }
                                    else
                                    {
                                        LogTracer.Log($"Could not send Toggle command to Seer because it timeout after {Timeout}");
                                    }
                                }
                                finally
                                {
                                    Marshal.DestroyStructure<NativeWin32Struct.COPYDATASTRUCT>(ToggleStructPtr);
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
                    LogTracer.Log(ex, $"An exception was threw in {nameof(ToggleServiceWindow)}");
                }
            }

            return false;
        }

        public bool SwitchServiceWindow(string Path)
        {
            if (CheckServiceAvailable())
            {
                try
                {
                    HWND Window = User32.FindWindow("SeerWindowClass", null);

                    if (Window.DangerousGetHandle().CheckIfValidPtr())
                    {
                        IntPtr PathPtr = Marshal.StringToHGlobalUni(Path);

                        try
                        {
                            NativeWin32Struct.COPYDATASTRUCT ToggleData = new NativeWin32Struct.COPYDATASTRUCT
                            {
                                dwData = new UIntPtr(ToggleCommand),
                                lpData = PathPtr,
                                cbData = (Path.Length + 1) * 2
                            };

                            IntPtr ToggleStructPtr = Marshal.AllocHGlobal(Marshal.SizeOf<NativeWin32Struct.COPYDATASTRUCT>());

                            try
                            {
                                Marshal.StructureToPtr(ToggleData, ToggleStructPtr, false);

                                try
                                {
                                    IntPtr Result = IntPtr.Zero;

                                    if (User32.SendMessageTimeout(Window, (uint)User32.WindowMessage.WM_COPYDATA, IntPtr.Zero, ToggleStructPtr, User32.SMTO.SMTO_ABORTIFHUNG, Timeout, ref Result) != IntPtr.Zero)
                                    {
                                        return true;
                                    }
                                    else
                                    {
                                        LogTracer.Log($"Could not send Switch command to Seer because it timeout after {Timeout}");
                                    }
                                }
                                finally
                                {
                                    Marshal.DestroyStructure<NativeWin32Struct.COPYDATASTRUCT>(ToggleStructPtr);
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
                    LogTracer.Log(ex, $"An exception was threw in {nameof(SwitchServiceWindow)}");
                }
            }

            return false;
        }

        public bool CloseServiceWindow()
        {
            if (CheckServiceAvailable())
            {
                try
                {
                    HWND Window = User32.FindWindow("SeerWindowClass", null);

                    if (Window.DangerousGetHandle().CheckIfValidPtr())
                    {
                        NativeWin32Struct.COPYDATASTRUCT CloseData = new NativeWin32Struct.COPYDATASTRUCT
                        {
                            dwData = new UIntPtr(CloseCommand),
                            lpData = IntPtr.Zero,
                            cbData = 0
                        };

                        IntPtr CloseStructPtr = Marshal.AllocHGlobal(Marshal.SizeOf<NativeWin32Struct.COPYDATASTRUCT>());

                        try
                        {
                            Marshal.StructureToPtr(CloseData, CloseStructPtr, false);

                            try
                            {
                                IntPtr Result = IntPtr.Zero;

                                if (User32.SendMessageTimeout(Window, (uint)User32.WindowMessage.WM_COPYDATA, IntPtr.Zero, CloseStructPtr, User32.SMTO.SMTO_ABORTIFHUNG, Timeout, ref Result) != IntPtr.Zero)
                                {
                                    return true;
                                }
                                else
                                {
                                    LogTracer.Log($"Could not send Close command to Seer because it timeout after {Timeout}");
                                }
                            }
                            finally
                            {
                                Marshal.DestroyStructure<NativeWin32Struct.COPYDATASTRUCT>(CloseStructPtr);
                            }
                        }
                        finally
                        {
                            Marshal.FreeHGlobal(CloseStructPtr);
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogTracer.Log(ex, $"An exception was threw in {nameof(CloseServiceWindow)}");
                }
            }

            return false;
        }

        private SeerServiceProvider()
        {

        }
    }
}
