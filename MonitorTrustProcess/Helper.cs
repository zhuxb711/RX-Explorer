using SharedLibrary;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;
using Vanara.PInvoke;
using Vanara.Windows.Shell;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Core;
using Windows.Management.Deployment;

namespace MonitorTrustProcess
{
    public static class Helper
    {
        public static string GetUWPActualNamedPipeName(string PipeId, string AppContainerName = null, int ProcessId = 0)
        {
            if (ProcessId > 0)
            {
                using (Process TargetProcess = Process.GetProcessById(ProcessId))
                using (AdvApi32.SafeHTOKEN Token = AdvApi32.SafeHTOKEN.FromProcess(new HPROCESS(TargetProcess.Handle), AdvApi32.TokenAccess.TOKEN_ALL_ACCESS))
                {
                    return $@"Sessions\{TargetProcess.SessionId}\AppContainerNamedObjects\{string.Join("-", Token.GetInfo<AdvApi32.TOKEN_APPCONTAINER_INFORMATION>(AdvApi32.TOKEN_INFORMATION_CLASS.TokenAppContainerSid).TokenAppContainer.ToString("D").Split(new string[] { "-" }, StringSplitOptions.RemoveEmptyEntries).Take(11))}\{PipeId}";
                }
            }
            else if (!string.IsNullOrEmpty(AppContainerName))
            {
                using (Process CurrentProcess = Process.GetCurrentProcess())
                {
                    if (UserEnv.DeriveAppContainerSidFromAppContainerName(AppContainerName, out AdvApi32.SafeAllocatedSID Sid).Succeeded)
                    {
                        try
                        {
                            return $@"Sessions\{CurrentProcess.SessionId}\AppContainerNamedObjects\{string.Join("-", ((PSID)Sid).ToString("D").Split(new string[] { "-" }, StringSplitOptions.RemoveEmptyEntries).Take(11))}\{PipeId}";
                        }
                        finally
                        {
                            Sid.Dispose();
                        }
                    }
                }
            }

            return string.Empty;
        }

        public static async Task<bool> LaunchApplicationFromPackageFamilyNameAsync(string PackageFamilyName, params string[] Arguments)
        {
            PackageManager Manager = new PackageManager();

            if (Manager.FindPackagesForUserWithPackageTypes(Convert.ToString(WindowsIdentity.GetCurrent()?.User), PackageFamilyName, PackageTypes.Main).FirstOrDefault() is Package Pack)
            {
                foreach (AppListEntry Entry in await Pack.GetAppListEntriesAsync())
                {
                    if (Arguments.Length == 0)
                    {
                        if (await Entry.LaunchAsync())
                        {
                            return true;
                        }
                    }

                    if (!string.IsNullOrEmpty(Entry.AppUserModelId))
                    {
                        return await LaunchApplicationFromAUMIDAsync(Entry.AppUserModelId, Arguments);
                    }
                }
            }

            return false;
        }

        public static Task<bool> LaunchApplicationFromAUMIDAsync(string AppUserModelId, params string[] Arguments)
        {
            return STAThreadController.Current.ExecuteOnSTAThreadAsync(() =>
            {
                try
                {
                    Shell32.IApplicationActivationManager Manager = (Shell32.IApplicationActivationManager)new Shell32.ApplicationActivationManager();

                    IEnumerable<string> AvailableArguments = Arguments.Where((Item) => !string.IsNullOrEmpty(Item));

                    if (AvailableArguments.Any())
                    {
                        List<Exception> ExceptionList = new List<Exception>();

                        if (AvailableArguments.All((Item) => File.Exists(Item) || Directory.Exists(Item)))
                        {
                            IEnumerable<ShellItem> SItemList = AvailableArguments.Select((Item) => new ShellItem(Item));

                            try
                            {
                                using (ShellItemArray ItemArray = new ShellItemArray(SItemList))
                                {
                                    try
                                    {
                                        Manager.ActivateForFile(AppUserModelId, ItemArray.IShellItemArray, "open", out uint ProcessId);

                                        if (ProcessId > 0)
                                        {
                                            return true;
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        ExceptionList.Add(ex);
                                    }

                                    try
                                    {
                                        Manager.ActivateForProtocol(AppUserModelId, ItemArray.IShellItemArray, out uint ProcessId);

                                        if (ProcessId > 0)
                                        {
                                            return true;
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        ExceptionList.Add(ex);
                                    }
                                }

                                try
                                {
                                    Manager.ActivateApplication(AppUserModelId, string.Join(' ', AvailableArguments.Select((Path) => $"\"{Path}\"")), Shell32.ACTIVATEOPTIONS.AO_NONE, out uint ProcessId);

                                    if (ProcessId > 0)
                                    {
                                        return true;
                                    }
                                }
                                catch (Exception ex)
                                {
                                    ExceptionList.Add(ex);
                                }
                            }
                            finally
                            {
                                SItemList.ForEach((Item) => Item.Dispose());
                            }
                        }
                        else
                        {
                            try
                            {
                                Manager.ActivateApplication(AppUserModelId, string.Join(' ', AvailableArguments.Select((Item) => $"\"{Item}\"")), Shell32.ACTIVATEOPTIONS.AO_NONE, out uint ProcessId);

                                if (ProcessId > 0)
                                {
                                    return true;
                                }
                            }
                            catch (Exception ex)
                            {
                                ExceptionList.Add(ex);
                            }
                        }

                        throw new AggregateException(ExceptionList);
                    }
                    else
                    {
                        Manager.ActivateApplication(AppUserModelId, null, Shell32.ACTIVATEOPTIONS.AO_NONE, out uint ProcessId);

                        if (ProcessId > 0)
                        {
                            return true;
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogTracer.Log(ex, "Could not launch the application from AUMID");
                }

                return false;
            });
        }

        public static WindowInformation GetUWPWindowInformation(string PackageFamilyName, uint KnownProcessId = 0)
        {
            WindowInformation Info = null;

            User32.EnumWindowsProc Callback = new User32.EnumWindowsProc((HWND WindowHandle, IntPtr lParam) =>
            {
                StringBuilder SbClassName = new StringBuilder(260);

                if (User32.GetClassName(WindowHandle, SbClassName, SbClassName.Capacity) > 0)
                {
                    string ClassName = SbClassName.ToString();

                    // Minimized : "Windows.UI.Core.CoreWindow" top window
                    // Normal : "Windows.UI.Core.CoreWindow" child of "ApplicationFrameWindow"
                    if (ClassName == "ApplicationFrameWindow")
                    {
                        string AUMID = string.Empty;

                        if (Shell32.SHGetPropertyStoreForWindow<PropSys.IPropertyStore>(WindowHandle) is PropSys.IPropertyStore PropertyStore)
                        {
                            AUMID = Convert.ToString(PropertyStore.GetValue(Ole32.PROPERTYKEY.System.AppUserModel.ID));
                        }

                        if (!string.IsNullOrEmpty(AUMID) && AUMID.Contains(PackageFamilyName))
                        {
                            WindowState State = WindowState.Normal;

                            if (User32.GetWindowRect(WindowHandle, out RECT CurrentRect))
                            {
                                IntPtr RectWorkAreaPtr = Marshal.AllocCoTaskMem(Marshal.SizeOf<RECT>());

                                try
                                {
                                    if (User32.SystemParametersInfo(User32.SPI.SPI_GETWORKAREA, 0, RectWorkAreaPtr, User32.SPIF.None))
                                    {
                                        RECT WorkAreaRect = Marshal.PtrToStructure<RECT>(RectWorkAreaPtr);

                                        //If Window rect is out of SPI_GETWORKAREA, it means it's maximized;
                                        if (CurrentRect.left < WorkAreaRect.left && CurrentRect.top < WorkAreaRect.top && CurrentRect.right > WorkAreaRect.right && CurrentRect.bottom > WorkAreaRect.bottom)
                                        {
                                            State = WindowState.Maximized;
                                        }
                                    }
                                }
                                finally
                                {
                                    Marshal.FreeCoTaskMem(RectWorkAreaPtr);
                                }
                            }

                            HWND CoreWindowHandle = User32.FindWindowEx(WindowHandle, IntPtr.Zero, "Windows.UI.Core.CoreWindow", null);

                            if (CoreWindowHandle.IsNull)
                            {
                                if (KnownProcessId > 0)
                                {
                                    using (Kernel32.SafeHPROCESS ProcessHandle = Kernel32.OpenProcess(new ACCESS_MASK(0x1000), false, KnownProcessId))
                                    {
                                        if (!ProcessHandle.IsInvalid && !ProcessHandle.IsNull)
                                        {
                                            uint FamilyNameSize = 260;
                                            StringBuilder PackageFamilyNameBuilder = new StringBuilder((int)FamilyNameSize);

                                            if (Kernel32.GetPackageFamilyName(ProcessHandle, ref FamilyNameSize, PackageFamilyNameBuilder).Succeeded)
                                            {
                                                if (PackageFamilyNameBuilder.ToString() == PackageFamilyName && User32.IsWindowVisible(WindowHandle))
                                                {
                                                    uint ProcessNameSize = 260;
                                                    StringBuilder ProcessImageName = new StringBuilder((int)ProcessNameSize);

                                                    if (Kernel32.QueryFullProcessImageName(ProcessHandle, Kernel32.PROCESS_NAME.PROCESS_NAME_WIN32, ProcessImageName, ref ProcessNameSize))
                                                    {
                                                        Info = new WindowInformation(ProcessImageName.ToString(), KnownProcessId, State, WindowHandle, HWND.NULL);
                                                    }
                                                    else
                                                    {
                                                        Info = new WindowInformation(string.Empty, KnownProcessId, State, WindowHandle, HWND.NULL);
                                                    }

                                                    return false;
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                            else
                            {
                                if (User32.GetWindowThreadProcessId(CoreWindowHandle, out uint ProcessId) > 0)
                                {
                                    if (KnownProcessId > 0 && KnownProcessId != ProcessId)
                                    {
                                        return true;
                                    }

                                    using (Kernel32.SafeHPROCESS ProcessHandle = Kernel32.OpenProcess(new ACCESS_MASK(0x1000), false, ProcessId))
                                    {
                                        if (!ProcessHandle.IsInvalid && !ProcessHandle.IsNull)
                                        {
                                            uint FamilyNameSize = 260;
                                            StringBuilder PackageFamilyNameBuilder = new StringBuilder((int)FamilyNameSize);

                                            if (Kernel32.GetPackageFamilyName(ProcessHandle, ref FamilyNameSize, PackageFamilyNameBuilder).Succeeded)
                                            {
                                                if (PackageFamilyNameBuilder.ToString() == PackageFamilyName && User32.IsWindowVisible(WindowHandle))
                                                {
                                                    uint ProcessNameSize = 260;
                                                    StringBuilder ProcessImageName = new StringBuilder((int)ProcessNameSize);

                                                    if (Kernel32.QueryFullProcessImageName(ProcessHandle, Kernel32.PROCESS_NAME.PROCESS_NAME_WIN32, ProcessImageName, ref ProcessNameSize))
                                                    {
                                                        Info = new WindowInformation(ProcessImageName.ToString(), ProcessId, State, WindowHandle, CoreWindowHandle);
                                                    }
                                                    else
                                                    {
                                                        Info = new WindowInformation(string.Empty, ProcessId, State, WindowHandle, CoreWindowHandle);
                                                    }

                                                    return false;
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                return true;
            });

            User32.EnumWindows(Callback, IntPtr.Zero);

            return Info;
        }

        public static bool CheckIfProcessIsBeingDebugged(IntPtr ProcessHandle)
        {
            if (ProcessHandle.CheckIfValidPtr())
            {
                try
                {
                    if (Kernel32.CheckRemoteDebuggerPresent(ProcessHandle, out bool IsDebuggerPresent))
                    {
                        return IsDebuggerPresent;
                    }
                    else
                    {
                        throw new Win32Exception(Marshal.GetLastWin32Error());
                    }
                }
                catch (Exception ex)
                {
                    LogTracer.Log(ex, "Could not check if the process is being debugged");
                }
            }

            return false;
        }
    }
}
