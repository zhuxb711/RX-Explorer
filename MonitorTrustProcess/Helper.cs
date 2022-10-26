using SharedLibrary;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Xml;
using Vanara.PInvoke;

namespace MonitorTrustProcess
{
    public static class Helper
    {
        public static string GetActualNamedPipeNameFromUwpApplication(string PipeId, string AppContainerName = null, int ProcessId = 0)
        {
            if (ProcessId > 0)
            {
                using (Process TargetProcess = Process.GetProcessById(ProcessId))
                using (AdvApi32.SafeHTOKEN Token = AdvApi32.SafeHTOKEN.FromProcess(TargetProcess, AdvApi32.TokenAccess.TOKEN_ALL_ACCESS))
                {
                    if (!Token.IsInvalid && !Token.IsNull)
                    {
                        return $@"Sessions\{TargetProcess.SessionId}\AppContainerNamedObjects\{string.Join("-", Token.GetInfo<AdvApi32.TOKEN_APPCONTAINER_INFORMATION>(AdvApi32.TOKEN_INFORMATION_CLASS.TokenAppContainerSid).TokenAppContainer.ToString("D").Split(new string[] { "-" }, StringSplitOptions.RemoveEmptyEntries).Take(11))}\{PipeId}";
                    }
                }
            }

            if (!string.IsNullOrEmpty(AppContainerName))
            {
                using (Process CurrentProcess = Process.GetCurrentProcess())
                {
                    if (UserEnv.DeriveAppContainerSidFromAppContainerName(AppContainerName, out AdvApi32.SafeAllocatedSID Sid).Succeeded)
                    {
                        try
                        {
                            if (!Sid.IsInvalid && !Sid.IsNull)
                            {
                                return $@"Sessions\{CurrentProcess.SessionId}\AppContainerNamedObjects\{string.Join("-", ((PSID)Sid).ToString("D").Split(new string[] { "-" }, StringSplitOptions.RemoveEmptyEntries).Take(11))}\{PipeId}";
                            }
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

        public static bool LaunchApplicationFromPackageFamilyName(string PackageFamilyName, params string[] Arguments)
        {
            string AppUserModelId = GetAppUserModeIdFromPackageFullName(GetPackageFullNameFromPackageFamilyName(PackageFamilyName));

            if (!string.IsNullOrEmpty(AppUserModelId))
            {
                return LaunchApplicationFromAppUserModelId(AppUserModelId, Arguments);
            }

            return false;
        }

        public static bool LaunchApplicationFromAppUserModelId(string AppUserModelId, params string[] Arguments)
        {
            Guid CLSID_ApplicationActivationManager = new Guid("45BA127D-10A8-46EA-8AB7-56EA9078943C");
            Guid IID_IApplicationActivationManager = new Guid("2E941141-7F97-4756-BA1D-9DECDE894A3D");

            if (Ole32.CoCreateInstance(CLSID_ApplicationActivationManager, null, Ole32.CLSCTX.CLSCTX_LOCAL_SERVER, IID_IApplicationActivationManager, out object ppv).Succeeded)
            {
                Shell32.IApplicationActivationManager Manager = (Shell32.IApplicationActivationManager)ppv;

                Manager.ActivateApplication(AppUserModelId, string.Join(' ', Arguments.Where((Item) => !string.IsNullOrEmpty(Item)).Select((Path) => $"\"{Path}\"")), Shell32.ACTIVATEOPTIONS.AO_NONE, out uint ProcessId);

                if (ProcessId > 0)
                {
                    return true;
                }
            }

            return false;
        }

        public static WindowInformation GetWindowInformationFromUwpApplication(string PackageFamilyName, uint KnownProcessId = 0)
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

        public static bool CheckIfDebuggerIsAttached(IntPtr ProcessHandle)
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
                    LogTracer.Log(ex, "Could not check whether debugger is attached");
                }
            }

            return false;
        }

        public static string GetPackageFamilyNameFromPackageFullName(string PackageFullName)
        {
            if (!string.IsNullOrEmpty(PackageFullName))
            {
                try
                {
                    uint NameLength = 0;

                    if (Kernel32.PackageFamilyNameFromFullName(PackageFullName, ref NameLength) == Win32Error.ERROR_INSUFFICIENT_BUFFER)
                    {
                        StringBuilder Builder = new StringBuilder(Convert.ToInt32(NameLength));

                        if (Kernel32.PackageFamilyNameFromFullName(PackageFullName, ref NameLength, Builder).Succeeded)
                        {
                            return Builder.ToString();
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogTracer.Log(ex, "Could not get the package family name from package full name");
                }
            }

            return string.Empty;
        }

        public static string GetPackageFullNameFromPackageFamilyName(string PackageFamilyName)
        {
            if (!string.IsNullOrWhiteSpace(PackageFamilyName))
            {
                try
                {
                    uint FullNameCount = 0;
                    uint FullNameBufferLength = 0;

                    if (Kernel32.FindPackagesByPackageFamily(PackageFamilyName, Kernel32.PACKAGE_FLAGS.PACKAGE_FILTER_HEAD | Kernel32.PACKAGE_FLAGS.PACKAGE_FILTER_DIRECT, ref FullNameCount, IntPtr.Zero, ref FullNameBufferLength, IntPtr.Zero, IntPtr.Zero) == Win32Error.ERROR_INSUFFICIENT_BUFFER)
                    {
                        IntPtr PackageFullNamePtr = Marshal.AllocHGlobal(Convert.ToInt32(FullNameCount * IntPtr.Size));
                        IntPtr PackageFullNameBufferPtr = Marshal.AllocHGlobal(Convert.ToInt32(FullNameBufferLength * 2));

                        try
                        {
                            if (Kernel32.FindPackagesByPackageFamily(PackageFamilyName, Kernel32.PACKAGE_FLAGS.PACKAGE_FILTER_HEAD | Kernel32.PACKAGE_FLAGS.PACKAGE_FILTER_DIRECT, ref FullNameCount, PackageFullNamePtr, ref FullNameBufferLength, PackageFullNameBufferPtr, IntPtr.Zero).Succeeded)
                            {
                                return Marshal.PtrToStringUni(Marshal.ReadIntPtr(PackageFullNamePtr));
                            }
                        }
                        finally
                        {
                            Marshal.FreeHGlobal(PackageFullNamePtr);
                            Marshal.FreeHGlobal(PackageFullNameBufferPtr);
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogTracer.Log(ex, "Could not get the package full name from the package family name");
                }
            }

            return string.Empty;
        }

        public static string GetAppUserModeIdFromPackageFullName(string PackageFullName)
        {
            try
            {
                if (!string.IsNullOrEmpty(PackageFullName))
                {
                    try
                    {
                        Kernel32.PACKAGE_INFO_REFERENCE Reference = new Kernel32.PACKAGE_INFO_REFERENCE();

                        if (Kernel32.OpenPackageInfoByFullName(PackageFullName, 0, ref Reference).Succeeded)
                        {
                            try
                            {
                                uint AppIdBufferLength = 0;

                                if (Kernel32.GetPackageApplicationIds(Reference, ref AppIdBufferLength, IntPtr.Zero, out _) == Win32Error.ERROR_INSUFFICIENT_BUFFER)
                                {
                                    IntPtr AppIdBufferPtr = Marshal.AllocHGlobal(Convert.ToInt32(AppIdBufferLength));

                                    try
                                    {
                                        if (Kernel32.GetPackageApplicationIds(Reference, ref AppIdBufferLength, AppIdBufferPtr, out uint AppIdCount).Succeeded && AppIdCount > 0)
                                        {
                                            return Marshal.PtrToStringUni(Marshal.ReadIntPtr(AppIdBufferPtr));
                                        }
                                    }
                                    finally
                                    {
                                        Marshal.FreeHGlobal(AppIdBufferPtr);
                                    }
                                }
                            }
                            finally
                            {
                                Kernel32.ClosePackageInfo(Reference);
                            }
                        }
                    }
                    catch (Exception)
                    {
                        // No need to handle this exception
                    }

                    string InstalledPath = GetInstalledPathFromPackageFullName(PackageFullName);

                    if (!string.IsNullOrEmpty(InstalledPath))
                    {
                        string AppxManifestPath = Path.Combine(InstalledPath, "AppXManifest.xml");

                        if (File.Exists(AppxManifestPath))
                        {
                            XmlDocument Document = new XmlDocument();

                            using (XmlTextReader DocReader = new XmlTextReader(AppxManifestPath) { Namespaces = false })
                            {
                                Document.Load(DocReader);

                                string AppId = Document.SelectSingleNode("/Package/Applications/Application")?.Attributes?.GetNamedItem("Id")?.InnerText;

                                if (!string.IsNullOrEmpty(AppId))
                                {
                                    return $"{GetPackageFamilyNameFromPackageFullName(PackageFullName)}!{AppId}";
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, "Could not get the AUMID from the package full name");
            }

            return string.Empty;
        }

        public static string GetPackageNameFromPackageFamilyName(string PackageFamilyName)
        {
            try
            {
                if (!string.IsNullOrEmpty(PackageFamilyName))
                {
                    uint PackageNameLength = 0;
                    uint PackagePublisherIdLength = 0;

                    if (Kernel32.PackageNameAndPublisherIdFromFamilyName(PackageFamilyName, ref PackageNameLength, null, ref PackagePublisherIdLength) == Win32Error.ERROR_INSUFFICIENT_BUFFER)
                    {
                        StringBuilder PackageNameBuilder = new StringBuilder(Convert.ToInt32(PackageNameLength));
                        StringBuilder PackagePublisherIdBuilder = new StringBuilder(Convert.ToInt32(PackagePublisherIdLength));

                        if (Kernel32.PackageNameAndPublisherIdFromFamilyName(PackageFamilyName, ref PackageNameLength, PackageNameBuilder, ref PackagePublisherIdLength, PackagePublisherIdBuilder).Succeeded)
                        {
                            return PackageNameBuilder.ToString();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, "Could not get the AUMID from the package family name");
            }

            return string.Empty;
        }

        public static string GetInstalledPathFromPackageFullName(string PackageFullName)
        {
            try
            {
                uint PathLength = 0;

                if (Kernel32.GetStagedPackagePathByFullName(PackageFullName, ref PathLength) == Win32Error.ERROR_INSUFFICIENT_BUFFER)
                {
                    StringBuilder Builder = new StringBuilder(Convert.ToInt32(PathLength));

                    if (Kernel32.GetStagedPackagePathByFullName(PackageFullName, ref PathLength, Builder).Succeeded)
                    {
                        return Builder.ToString();
                    }
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, "Could not get installed path from package full name");
            }

            return string.Empty;
        }
    }
}
