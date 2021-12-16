using MimeTypes;
using ShareClassLibrary;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Security.Principal;
using System.Text;
using System.Threading;
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

        public static Task<bool> ExecuteOnSTAThreadAsync(Action Act)
        {
            if (Thread.CurrentThread.GetApartmentState() != ApartmentState.STA)
            {
                return STAThreadController.Current.RunAsync(Act);
            }
            else
            {
                Act();
                return Task.FromResult(true);
            }
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
                    RandomAccessStreamReference Reference = Pack.GetLogoAsRandomAccessStreamReference(new Windows.Foundation.Size(150, 150));

                    if (Reference != null)
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
            PackageManager Manager = new PackageManager();

            return Manager.FindPackagesForUserWithPackageTypes(Convert.ToString(WindowsIdentity.GetCurrent()?.User), PackageFamilyName, PackageTypes.Main).Any();
        }

        public static Task<bool> LaunchApplicationFromAUMIDAsync(string AppUserModelId, params string[] PathArray)
        {
            return ExecuteOnSTAThreadAsync(() =>
            {
                if (new Shell32.ApplicationActivationManager() is Shell32.IApplicationActivationManager Manager)
                {
                    if (PathArray.Length > 0)
                    {
                        List<ShellItem> SItemList = new List<ShellItem>(PathArray.Length);

                        try
                        {
                            foreach (string Path in PathArray)
                            {
                                SItemList.Add(new ShellItem(Path));
                            }

                            using (ShellItemArray ItemArray = new ShellItemArray(SItemList))
                            {
                                Manager.ActivateForFile(AppUserModelId, ItemArray.IShellItemArray, "Open", out _);
                            }
                        }
                        finally
                        {
                            SItemList.ForEach((Item) => Item.Dispose());
                        }
                    }
                    else
                    {
                        Manager.ActivateApplication(AppUserModelId, null, Shell32.ACTIVATEOPTIONS.AO_NONE, out _);
                    }
                }
                else
                {
                    throw new NotSupportedException();
                }
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
                    RandomAccessStreamReference Reference = Pack.GetLogoAsRandomAccessStreamReference(new Windows.Foundation.Size(150, 150));

                    if (Reference != null)
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

                                    return new InstalledApplicationPackage(Pack.DisplayName, Pack.PublisherDisplayName, Pack.Id.FamilyName, Logo);
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
                                                          RandomAccessStreamReference Reference = Pack.GetLogoAsRandomAccessStreamReference(new Windows.Foundation.Size(150, 150));

                                                          if (Reference != null)
                                                          {
                                                              IRandomAccessStreamWithContentType IconStream = Reference.OpenReadAsync().AsTask().Result;

                                                              if (IconStream != null)
                                                              {
                                                                  try
                                                                  {
                                                                      using (Stream Stream = IconStream.AsStreamForRead())
                                                                      {
                                                                          byte[] Logo = new byte[IconStream.Size];

                                                                          Stream.Read(Logo, 0, (int)IconStream.Size);

                                                                          Result.Add(new InstalledApplicationPackage(Pack.DisplayName, Pack.PublisherDisplayName, Pack.Id.FamilyName, Logo));
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
