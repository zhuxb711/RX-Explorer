using MediaDevices;
using Microsoft.Win32.SafeHandles;
using MimeTypes;
using ShareClassLibrary;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Vanara.PInvoke;
using Vanara.Windows.Shell;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Core;
using Windows.Management.Deployment;
using Windows.Storage.Streams;

namespace FullTrustProcess
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
            else if(!string.IsNullOrEmpty(AppContainerName))
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

        public static IReadOnlyList<Process> GetLockingProcesses(string Path)
        {
            StringBuilder SessionKey = new StringBuilder(Guid.NewGuid().ToString());

            if (RstrtMgr.RmStartSession(out uint SessionHandle, 0, SessionKey).Succeeded)
            {
                try
                {
                    string[] ResourcesFileName = new string[] { Path };

                    if (RstrtMgr.RmRegisterResources(SessionHandle, (uint)ResourcesFileName.Length, ResourcesFileName, 0, null, 0, null).Succeeded)
                    {
                        uint pnProcInfo = 0;

                        //Note: there's a race condition here -- the first call to RmGetList() returns
                        //      the total number of process. However, when we call RmGetList() again to get
                        //      the actual processes this number may have increased.
                        Win32Error Error = RstrtMgr.RmGetList(SessionHandle, out uint pnProcInfoNeeded, ref pnProcInfo, null, out _);

                        if (Error == Win32Error.ERROR_MORE_DATA)
                        {
                            RstrtMgr.RM_PROCESS_INFO[] ProcessInfo = new RstrtMgr.RM_PROCESS_INFO[pnProcInfoNeeded];

                            if (RstrtMgr.RmGetList(SessionHandle, out _, ref pnProcInfoNeeded, ProcessInfo, out _).Succeeded)
                            {
                                List<Process> LockProcesses = new List<Process>((int)pnProcInfoNeeded);

                                for (int i = 0; i < pnProcInfoNeeded; i++)
                                {
                                    try
                                    {
                                        LockProcesses.Add(Process.GetProcessById(Convert.ToInt32(ProcessInfo[i].Process.dwProcessId)));
                                    }
                                    catch (Exception ex)
                                    {
                                        LogTracer.Log(ex, "Process is no longer running");
                                    }
                                }

                                return LockProcesses;
                            }
                            else
                            {
                                LogTracer.Log("Could not list processes locking resource");
                            }
                        }
                        else if (Error != Win32Error.ERROR_SUCCESS)
                        {
                            LogTracer.Log("Could not list processes locking resource. Failed to get size of result.");
                        }
                    }
                    else
                    {
                        LogTracer.Log("Could not register resource");
                    }
                }
                finally
                {
                    RstrtMgr.RmEndSession(SessionHandle);
                }
            }
            else
            {
                LogTracer.Log("Could not begin restart session. Unable to determine locking process");
            }

            return new List<Process>(0);
        }

        public static ulong GetAllocationSize(string Path)
        {
            try
            {
                using (SafeFileHandle Handle = File.OpenHandle(Path))
                {
                    try
                    {
                        return Convert.ToUInt64(Math.Max(Kernel32.GetFileInformationByHandleEx<Kernel32.FILE_COMPRESSION_INFO>(Handle, Kernel32.FILE_INFO_BY_HANDLE_CLASS.FileCompressionInfo).CompressedFileSize, 0));
                    }
                    catch (Exception)
                    {
                        return Convert.ToUInt64(Math.Max(Kernel32.GetFileInformationByHandleEx<Kernel32.FILE_STANDARD_INFO>(Handle, Kernel32.FILE_INFO_BY_HANDLE_CLASS.FileStandardInfo).AllocationSize, 0));
                    }
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, "Could not get the size from GetFileInformationByHandleEx so we could not calculate the size on disk");
            }

            return 0;
        }

        public static void CopyTo(string From, string To)
        {
            if (File.Exists(From))
            {
                if (File.Exists(To))
                {
                    File.Copy(From, To, true);
                }
                else
                {
                    throw new FileNotFoundException(To);
                }
            }
            else if (Directory.Exists(From))
            {
                if (Directory.Exists(To))
                {
                    Directory.Delete(To, true);
                }

                Directory.CreateDirectory(To);

                foreach (string Path in Directory.EnumerateDirectories(From, "*", SearchOption.AllDirectories).Concat(Directory.EnumerateFiles(From, "*", SearchOption.AllDirectories)))
                {
                    string TargetPath = System.IO.Path.Combine(To, System.IO.Path.GetRelativePath(From, Path));

                    if (File.Exists(Path))
                    {
                        File.Copy(Path, TargetPath, true);
                    }
                    else if (Directory.Exists(Path))
                    {
                        Directory.CreateDirectory(TargetPath);
                    }
                }
            }
            else
            {
                throw new FileNotFoundException(From);
            }
        }

        public static byte[] GetThumbnailOverlay(string Path)
        {
            Shell32.SHFILEINFO Shfi = new Shell32.SHFILEINFO();

            IntPtr Result = Shell32.SHGetFileInfo(Path, 0, ref Shfi, Shell32.SHFILEINFO.Size, Shell32.SHGFI.SHGFI_OVERLAYINDEX | Shell32.SHGFI.SHGFI_ICON | Shell32.SHGFI.SHGFI_SYSICONINDEX | Shell32.SHGFI.SHGFI_ICONLOCATION);

            if (Result.CheckIfValidPtr())
            {
                User32.DestroyIcon(Shfi.hIcon);

                if (Shell32.SHGetImageList(Shell32.SHIL.SHIL_LARGE, typeof(ComCtl32.IImageList).GUID, out object PPV).Succeeded)
                {
                    using (ComCtl32.SafeHIMAGELIST ImageList = ComCtl32.SafeHIMAGELIST.FromIImageList((ComCtl32.IImageList)PPV))
                    {
                        if (!ImageList.IsNull && !ImageList.IsInvalid)
                        {
                            int OverlayIndex = Shfi.iIcon >> 24;

                            if (OverlayIndex != 0)
                            {
                                int OverlayImage = ImageList.Interface.GetOverlayImage(OverlayIndex);

                                using (User32.SafeHICON OverlayIcon = ImageList.Interface.GetIcon(OverlayImage, ComCtl32.IMAGELISTDRAWFLAGS.ILD_PRESERVEALPHA))
                                {
                                    if (!OverlayIcon.IsNull && !OverlayIcon.IsInvalid)
                                    {
                                        using (Bitmap OriginBitmap = Bitmap.FromHicon(OverlayIcon.DangerousGetHandle()))
                                        using (MemoryStream MStream = new MemoryStream())
                                        {
                                            OriginBitmap.MakeTransparent(Color.Black);
                                            OriginBitmap.Save(MStream, ImageFormat.Png);

                                            return MStream.ToArray();
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }

            return Array.Empty<byte>();
        }

        public static string MTPGenerateUniquePath(MediaDevice Device, string Path, CreateType Type)
        {
            string UniquePath = Path;

            if (Device.FileExists(Path) || Device.DirectoryExists(Path))
            {
                string Name = Type == CreateType.Folder ? System.IO.Path.GetFileName(Path) : System.IO.Path.GetFileNameWithoutExtension(Path);
                string Extension = Type == CreateType.Folder ? string.Empty : System.IO.Path.GetExtension(Path);
                string DirectoryPath = System.IO.Path.GetDirectoryName(Path);

                for (ushort Count = 1; Device.DirectoryExists(UniquePath) || Device.FileExists(UniquePath); Count++)
                {
                    if (Regex.IsMatch(Name, @".*\(\d+\)"))
                    {
                        UniquePath = System.IO.Path.Combine(DirectoryPath, $"{Name.Substring(0, Name.LastIndexOf("(", StringComparison.InvariantCultureIgnoreCase))}({Count}){Extension}");
                    }
                    else
                    {
                        UniquePath = System.IO.Path.Combine(DirectoryPath, $"{Name} ({Count}){Extension}");
                    }
                }
            }

            return UniquePath;
        }

        public static string StorageGenerateUniquePath(string Path, CreateType Type)
        {
            string UniquePath = Path;

            if (File.Exists(Path) || Directory.Exists(Path))
            {
                string Name = Type == CreateType.Folder ? System.IO.Path.GetFileName(Path) : System.IO.Path.GetFileNameWithoutExtension(Path);
                string Extension = Type == CreateType.Folder ? string.Empty : System.IO.Path.GetExtension(Path);
                string DirectoryPath = System.IO.Path.GetDirectoryName(Path);

                for (ushort Count = 1; Directory.Exists(UniquePath) || File.Exists(UniquePath); Count++)
                {
                    if (Regex.IsMatch(Name, @".*\(\d+\)"))
                    {
                        UniquePath = System.IO.Path.Combine(DirectoryPath, $"{Name.Substring(0, Name.LastIndexOf("(", StringComparison.InvariantCultureIgnoreCase))}({Count}){Extension}");
                    }
                    else
                    {
                        UniquePath = System.IO.Path.Combine(DirectoryPath, $"{Name} ({Count}){Extension}");
                    }
                }
            }

            return UniquePath;
        }

        public static string ConvertShortPathToLongPath(string ShortPath)
        {
            int BufferSize = 512;

            StringBuilder Builder = new StringBuilder(BufferSize);

            uint ReturnNum = Kernel32.GetLongPathName(ShortPath, Builder, Convert.ToUInt32(BufferSize));

            if (ReturnNum > BufferSize)
            {
                BufferSize = Builder.EnsureCapacity(Convert.ToInt32(ReturnNum));

                if (Kernel32.GetLongPathName(ShortPath, Builder, Convert.ToUInt32(BufferSize)) > 0)
                {
                    return Builder.ToString();
                }
                else
                {
                    return ShortPath;
                }
            }
            else if (ReturnNum > 0)
            {
                return Builder.ToString();
            }
            else
            {
                return ShortPath;
            }
        }

        public static DateTimeOffset ConvertToLocalDateTimeOffset(FILETIME FileTime)
        {
            try
            {
                if (Kernel32.FileTimeToSystemTime(FileTime, out SYSTEMTIME ModTime))
                {
                    return new DateTime(ModTime.wYear, ModTime.wMonth, ModTime.wDay, ModTime.wHour, ModTime.wMinute, ModTime.wSecond, ModTime.wMilliseconds, DateTimeKind.Utc).ToLocalTime();
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"A exception was threw in {nameof(ConvertToLocalDateTimeOffset)}");
            }

            return default;
        }

        public static IReadOnlyList<HWND> GetCurrentWindowsHandle()
        {
            List<HWND> HandleList = new List<HWND>();

            HWND Handle = HWND.NULL;

            while (true)
            {
                Handle = User32.FindWindowEx(HWND.NULL, Handle, null, null);

                if (Handle != HWND.NULL)
                {
                    if (User32.IsWindowVisible(Handle) && !User32.IsIconic(Handle))
                    {
                        HandleList.Add(Handle);
                    }
                }
                else
                {
                    break;
                }
            }

            return HandleList;
        }

        public static string GetMIMEFromPath(string Path)
        {
            if (!File.Exists(Path))
            {
                throw new FileNotFoundException($"\"{Path}\" not found");
            }

            if (MimeTypeMap.TryGetMimeType(System.IO.Path.GetExtension(Path), out string Mime))
            {
                return Mime;
            }
            else
            {
                return "unknown/unknown";
            }
        }

        public static WindowInformation GetUWPWindowInformation(string PackageFamilyName, uint WithPID = 0)
        {
            WindowInformation Info = null;

            User32.EnumWindowsProc Callback = new User32.EnumWindowsProc((HWND hWnd, IntPtr lParam) =>
            {
                StringBuilder SbClassName = new StringBuilder(260);

                if (User32.GetClassName(hWnd, SbClassName, SbClassName.Capacity) > 0)
                {
                    string ClassName = SbClassName.ToString();

                    // Minimized : "Windows.UI.Core.CoreWindow" top window
                    // Normal : "Windows.UI.Core.CoreWindow" child of "ApplicationFrameWindow"
                    if (ClassName == "ApplicationFrameWindow")
                    {
                        if (Shell32.SHGetPropertyStoreForWindow(hWnd, new Guid("{886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99}"), out object PropertyStore).Succeeded)
                        {
                            Ole32.PROPVARIANT Prop = new Ole32.PROPVARIANT();

                            ((PropSys.IPropertyStore)PropertyStore).GetValue(Ole32.PROPERTYKEY.System.AppUserModel.ID, Prop);

                            string AUMID = Prop.IsNullOrEmpty ? string.Empty : Prop.pwszVal;

                            if (!string.IsNullOrEmpty(AUMID) && AUMID.Contains(PackageFamilyName))
                            {
                                WindowState State = WindowState.Normal;

                                if (User32.GetWindowRect(hWnd, out RECT CurrentRect))
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

                                HWND hWndFind = User32.FindWindowEx(hWnd, IntPtr.Zero, "Windows.UI.Core.CoreWindow", null);

                                if (!hWndFind.IsNull)
                                {
                                    if (User32.GetWindowThreadProcessId(hWndFind, out uint PID) > 0)
                                    {
                                        if (WithPID > 0 && WithPID != PID)
                                        {
                                            return true;
                                        }

                                        using (Kernel32.SafeHPROCESS ProcessHandle = Kernel32.OpenProcess(new ACCESS_MASK(0x1000), false, PID))
                                        {
                                            if (!ProcessHandle.IsInvalid && !ProcessHandle.IsNull)
                                            {
                                                uint FamilyNameSize = 260;
                                                StringBuilder PackageFamilyNameBuilder = new StringBuilder((int)FamilyNameSize);

                                                if (Kernel32.GetPackageFamilyName(ProcessHandle, ref FamilyNameSize, PackageFamilyNameBuilder).Succeeded)
                                                {
                                                    if (PackageFamilyNameBuilder.ToString() == PackageFamilyName && User32.IsWindowVisible(hWnd))
                                                    {
                                                        uint ProcessNameSize = 260;
                                                        StringBuilder ProcessImageName = new StringBuilder((int)ProcessNameSize);

                                                        if (Kernel32.QueryFullProcessImageName(ProcessHandle, 0, ProcessImageName, ref ProcessNameSize))
                                                        {
                                                            Info = new WindowInformation(ProcessImageName.ToString(), PID, State, hWnd);
                                                        }
                                                        else
                                                        {
                                                            Info = new WindowInformation(string.Empty, PID, State, hWnd);
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
                }

                return true;
            });

            User32.EnumWindows(Callback, IntPtr.Zero);

            return Info;
        }

        public static string GetPackageFamilyNameFromUWPShellLink(string LinkPath)
        {
            using (ShellItem LinkItem = new ShellItem(LinkPath))
            {
                return LinkItem.Properties.GetPropertyString(Ole32.PROPERTYKEY.System.Link.TargetParsingPath).Split('!').FirstOrDefault();
            }
        }

        public static async Task<byte[]> GetIconDataFromPackageFamilyNameAsync(string PackageFamilyName)
        {
            PackageManager Manager = new PackageManager();

            if (Manager.FindPackagesForUserWithPackageTypes(Convert.ToString(WindowsIdentity.GetCurrent()?.User), PackageFamilyName, PackageTypes.Main).FirstOrDefault() is Package Pack)
            {
                try
                {
                    if (Pack.GetLogoAsRandomAccessStreamReference(new Windows.Foundation.Size(150, 150)) is RandomAccessStreamReference Reference)
                    {
                        IRandomAccessStreamWithContentType IconStream = await Reference.OpenReadAsync();

                        if (IconStream != null)
                        {
                            try
                            {
                                using (Stream Stream = IconStream.AsStreamForRead())
                                {
                                    byte[] Logo = new byte[IconStream.Size];

                                    Stream.Read(Logo, 0, (int)IconStream.Size);

                                    return Logo;
                                }
                            }
                            finally
                            {
                                IconStream.Dispose();
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogTracer.Log(ex, $"Could not get logo from PackageFamilyName: \"{PackageFamilyName}\"");
                }
            }

            return Array.Empty<byte>();
        }

        public static bool CheckIfPackageFamilyNameExist(string PackageFamilyName)
        {
            return new PackageManager().FindPackagesForUserWithPackageTypes(Convert.ToString(WindowsIdentity.GetCurrent()?.User), PackageFamilyName, PackageTypes.Main).Any();
        }

        public static Task<bool> LaunchApplicationFromAUMIDAsync(string AppUserModelId, params string[] PathArray)
        {
            return STAThreadController.Current.ExecuteOnSTAThreadAsync(() =>
            {
                try
                {
                    Shell32.IApplicationActivationManager Manager = (Shell32.IApplicationActivationManager)new Shell32.ApplicationActivationManager();

                    if (PathArray.Length > 0)
                    {
                        IEnumerable<ShellItem> SItemList = PathArray.Select((Path) => new ShellItem(Path));

                        try
                        {
                            using (ShellItemArray ItemArray = new ShellItemArray(SItemList))
                            {
                                List<Exception> ExceptionList = new List<Exception>();

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

                                try
                                {
                                    Manager.ActivateApplication(AppUserModelId, string.Join(' ', PathArray.Select((Path) => $"\"{Path}\"")), Shell32.ACTIVATEOPTIONS.AO_NONE, out uint ProcessId);

                                    if (ProcessId > 0)
                                    {
                                        return true;
                                    }
                                }
                                catch (Exception ex)
                                {
                                    ExceptionList.Add(ex);
                                }

                                throw new AggregateException(ExceptionList);
                            }
                        }
                        finally
                        {
                            SItemList.ForEach((Item) => Item.Dispose());
                        }
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

        public static async Task<bool> LaunchApplicationFromPackageFamilyNameAsync(string PackageFamilyName, params string[] PathArray)
        {
            PackageManager Manager = new PackageManager();

            if (Manager.FindPackagesForUserWithPackageTypes(Convert.ToString(WindowsIdentity.GetCurrent()?.User), PackageFamilyName, PackageTypes.Main).FirstOrDefault() is Package Pack)
            {
                foreach (AppListEntry Entry in await Pack.GetAppListEntriesAsync())
                {
                    if (PathArray.Length == 0)
                    {
                        if (await Entry.LaunchAsync())
                        {
                            return true;
                        }
                    }

                    if (!string.IsNullOrEmpty(Entry.AppUserModelId))
                    {
                        return await LaunchApplicationFromAUMIDAsync(Entry.AppUserModelId, PathArray);
                    }
                }
            }

            return false;
        }

        public static async Task<InstalledApplicationPackage> GetInstalledApplicationAsync(string PackageFamilyName)
        {
            PackageManager Manager = new PackageManager();

            if (Manager.FindPackagesForUserWithPackageTypes(Convert.ToString(WindowsIdentity.GetCurrent()?.User), PackageFamilyName, PackageTypes.Main).FirstOrDefault() is Package Pack)
            {
                try
                {
                    if (Pack.GetLogoAsRandomAccessStreamReference(new Windows.Foundation.Size(150, 150)) is RandomAccessStreamReference Reference)
                    {
                        if (await Reference.OpenReadAsync() is IRandomAccessStreamWithContentType IconStream)
                        {
                            try
                            {
                                using (Stream Stream = IconStream.AsStreamForRead())
                                using (BinaryReader Reader = new BinaryReader(Stream, Encoding.Default, true))
                                {
                                    return new InstalledApplicationPackage(Pack.DisplayName, Pack.PublisherDisplayName, Pack.Id.FamilyName, Reader.ReadBytes(Convert.ToInt32(IconStream.Size)));
                                }
                            }
                            finally
                            {
                                IconStream.Dispose();
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogTracer.Log(ex, $"Could not get logo from PackageFamilyName: \"{PackageFamilyName}\"");
                }
            }

            return null;
        }

        public static async Task<IEnumerable<InstalledApplicationPackage>> GetInstalledApplicationAsync()
        {
            ConcurrentBag<InstalledApplicationPackage> Result = new ConcurrentBag<InstalledApplicationPackage>();

            PackageManager Manager = new PackageManager();

            await Task.Run(() => Parallel.ForEach(Manager.FindPackagesForUserWithPackageTypes(Convert.ToString(WindowsIdentity.GetCurrent()?.User), PackageTypes.Main)
                                                         .Where((Pack) => !string.IsNullOrWhiteSpace(Pack.DisplayName)
                                                                             && Pack.Status.VerifyIsOK()
                                                                             && Pack.SignatureKind is PackageSignatureKind.Developer
                                                                                                   or PackageSignatureKind.Enterprise
                                                                                                   or PackageSignatureKind.Store),
                                                  (Pack) =>
                                                  {
                                                      try
                                                      {
                                                          if (Pack.GetLogoAsRandomAccessStreamReference(new Windows.Foundation.Size(150, 150)) is RandomAccessStreamReference Reference)
                                                          {
                                                              if (Reference.OpenReadAsync().AsTask().Result is IRandomAccessStreamWithContentType IconStream)
                                                              {
                                                                  try
                                                                  {
                                                                      using (Stream Stream = IconStream.AsStreamForRead())
                                                                      using (BinaryReader Reader = new BinaryReader(Stream, Encoding.Default, true))
                                                                      {
                                                                          Result.Add(new InstalledApplicationPackage(Pack.DisplayName, Pack.PublisherDisplayName, Pack.Id.FamilyName, Reader.ReadBytes(Convert.ToInt32(IconStream.Size))));
                                                                      }
                                                                  }
                                                                  finally
                                                                  {
                                                                      IconStream.Dispose();
                                                                  }
                                                              }
                                                          }
                                                      }
                                                      catch (Exception ex)
                                                      {
                                                          LogTracer.Log(ex, $"Could not get logo from PackageFamilyName: \"{Pack.Id.FamilyName}\"");
                                                      }
                                                  }));

            return Result.OrderBy((Pack) => Pack.AppDescription).ThenBy((Pack) => Pack.AppName);
        }
    }
}
