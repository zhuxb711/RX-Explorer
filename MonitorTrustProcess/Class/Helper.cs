using SharedLibrary;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Xml;
using Vanara.PInvoke;

namespace MonitorTrustProcess.Class
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
                    if (!Token.IsInvalid)
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
                            if (!Sid.IsInvalid)
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

        public static WindowInformation GetWindowInformationFromUwpApplication(uint TargetProcessId)
        {
            WindowInformation Info = null;

            if (TargetProcessId > 0)
            {
                User32.EnumWindowsProc Callback = new User32.EnumWindowsProc((WindowHandle, lParam) =>
                {
                    string ClassName = GetClassNameFromWindowHandle(WindowHandle.DangerousGetHandle());

                    if (!string.IsNullOrEmpty(ClassName))
                    {
                        HWND CoreWindowHandle = HWND.NULL;
                        HWND ApplicationFrameWindowHandle = HWND.NULL;

                        // Since User32.IsIconic is useless if the Window handle is belongs to Uwp
                        // Minimized : "Windows.UI.Core.CoreWindow" top window
                        // Normal : "Windows.UI.Core.CoreWindow" child of "ApplicationFrameWindow"
                        switch (ClassName)
                        {
                            case "ApplicationFrameWindow":
                                {
                                    ApplicationFrameWindowHandle = WindowHandle;
                                    CoreWindowHandle = User32.FindWindowEx(WindowHandle, lpszClass: "Windows.UI.Core.CoreWindow");
                                    break;
                                }
                            case "Windows.UI.Core.CoreWindow":
                                {
                                    CoreWindowHandle = WindowHandle;
                                    break;
                                }
                        }

                        if (!CoreWindowHandle.IsNull)
                        {
                            if (User32.GetWindowThreadProcessId(CoreWindowHandle, out uint ProcessId) > 0)
                            {
                                if (TargetProcessId == ProcessId)
                                {
                                    WindowState State = WindowState.Normal;

                                    if (ApplicationFrameWindowHandle == HWND.NULL)
                                    {
                                        State = WindowState.Minimized;
                                    }
                                    else if (User32.GetWindowRect(CoreWindowHandle, out RECT WindowRect))
                                    {
                                        if (User32.SystemParametersInfo(User32.SPI.SPI_GETWORKAREA, out RECT WorkAreaRect))
                                        {
                                            //If Window rect is equal or out of SPI_GETWORKAREA, it means it's maximized;
                                            if (WindowRect.left <= WorkAreaRect.left && WindowRect.top <= WorkAreaRect.top && WindowRect.right >= WorkAreaRect.right && WindowRect.bottom >= WorkAreaRect.bottom)
                                            {
                                                State = WindowState.Maximized;
                                            }
                                        }
                                    }

                                    Info = new WindowInformation(GetExecutablePathFromProcessId(ProcessId), ProcessId, State, ApplicationFrameWindowHandle.DangerousGetHandle(), CoreWindowHandle.DangerousGetHandle());

                                    return false;
                                }
                            }
                        }
                    }

                    return true;
                });

                User32.EnumWindows(Callback, IntPtr.Zero);
            }

            return Info;
        }

        public static string GetClassNameFromWindowHandle(IntPtr WindowHandle)
        {
            int NameLength = 256;

            StringBuilder Builder = new StringBuilder(NameLength);

            if (User32.GetClassName(WindowHandle, Builder, NameLength) > 0)
            {
                return Builder.ToString();
            }
            else
            {
                while (Win32Error.GetLastError() == Win32Error.ERROR_INSUFFICIENT_BUFFER)
                {
                    Builder.EnsureCapacity(NameLength *= 2);

                    if (User32.GetClassName(WindowHandle, Builder, NameLength) > 0)
                    {
                        return Builder.ToString();
                    }
                }
            }

            return string.Empty;
        }

        public static string GetExecutablePathFromProcessId(uint ProcessId)
        {
            uint PathLength = 256;

            using (Kernel32.SafeHPROCESS ProcessHandle = Kernel32.OpenProcess(ACCESS_MASK.GENERIC_READ, false, ProcessId))
            {
                if (!ProcessHandle.IsInvalid)
                {
                    StringBuilder Builder = new StringBuilder((int)PathLength);

                    if (Kernel32.QueryFullProcessImageName(ProcessHandle, Kernel32.PROCESS_NAME.PROCESS_NAME_WIN32, Builder, ref PathLength))
                    {
                        return Builder.ToString();
                    }
                    else
                    {
                        while (Win32Error.GetLastError() == Win32Error.ERROR_INSUFFICIENT_BUFFER)
                        {
                            Builder.EnsureCapacity((int)(PathLength *= 2));

                            if (Kernel32.QueryFullProcessImageName(ProcessHandle, Kernel32.PROCESS_NAME.PROCESS_NAME_WIN32, Builder, ref PathLength))
                            {
                                return Builder.ToString();
                            }
                        }
                    }
                }
            }

            return string.Empty;
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
