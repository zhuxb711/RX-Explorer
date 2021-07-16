using ShareClassLibrary;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
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
using Winista.Mime;

namespace FullTrustProcess
{
    public static class Helper
    {
        public static string GetMIMEFromPath(string Path)
        {
            if (!File.Exists(Path))
            {
                throw new FileNotFoundException($"\"{Path}\" not found");
            }

            byte[] Buffer = new byte[256];

            using (FileStream Stream = new FileStream(Path, FileMode.Open, FileAccess.Read))
            {
                Stream.Read(Buffer, 0, 256);
            }

            IntPtr Pointer = Marshal.AllocHGlobal(256);

            try
            {
                Marshal.Copy(Buffer, 0, Pointer, 256);

                if (UrlMon.FindMimeFromData(null, null, Pointer, 256, null, UrlMon.FMFD.FMFD_DEFAULT, out string MIMEResult) == HRESULT.S_OK)
                {
                    return MIMEResult;
                }
                else
                {
                    MimeType MIME = new MimeTypes().GetMimeTypeFromFile(Path);

                    if (MIME != null)
                    {
                        return MIME.Name;
                    }
                    else
                    {
                        return "unknown/unknown";
                    }
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"An exception was threw in {nameof(GetMIMEFromPath)}");
                return "unknown/unknown";
            }
            finally
            {
                Marshal.FreeHGlobal(Pointer);
            }
        }

        public static WindowInformation GetUWPWindowInformation(string PackageFamilyName, uint? WithPID = null)
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

                            string AUMID = Prop.pwszVal;

                            if (!string.IsNullOrEmpty(AUMID) && AUMID.Contains(PackageFamilyName))
                            {
                                WindowState State = WindowState.Normal;

                                if (User32.GetWindowRect(hWnd, out RECT CurrentRect))
                                {
                                    IntPtr RectWorkAreaPtr = Marshal.AllocHGlobal(Marshal.SizeOf<RECT>());

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
                                        Marshal.FreeHGlobal(RectWorkAreaPtr);
                                    }
                                }

                                HWND hWndFind = User32.FindWindowEx(hWnd, IntPtr.Zero, "Windows.UI.Core.CoreWindow", null);

                                if (!hWndFind.IsNull)
                                {
                                    if (User32.GetWindowThreadProcessId(hWndFind, out uint PID) > 0)
                                    {
                                        if (WithPID != null && WithPID.Value != PID)
                                        {
                                            return true;
                                        }

                                        using (Kernel32.SafeHPROCESS ProcessHandle = Kernel32.OpenProcess(new ACCESS_MASK(0x1000), false, PID))
                                        {
                                            if (!ProcessHandle.IsInvalid && !ProcessHandle.IsNull)
                                            {
                                                uint Size = 260;
                                                StringBuilder PackageFamilyNameBuilder = new StringBuilder((int)Size);

                                                if (Kernel32.GetPackageFamilyName(ProcessHandle, ref Size, PackageFamilyNameBuilder).Succeeded)
                                                {
                                                    if (PackageFamilyNameBuilder.ToString() == PackageFamilyName && User32.IsWindowVisible(hWnd))
                                                    {
                                                        Size = 260;
                                                        StringBuilder ProcessImageName = new StringBuilder((int)Size);

                                                        if (Kernel32.QueryFullProcessImageName(ProcessHandle, 0, ProcessImageName, ref Size))
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

        public static IEnumerable<FileInfo> GetAllSubFiles(DirectoryInfo Directory)
        {
            foreach (FileInfo File in Directory.EnumerateFiles())
            {
                yield return File;
            }

            foreach (DirectoryInfo Dic in Directory.EnumerateDirectories())
            {
                foreach (FileInfo SubFile in GetAllSubFiles(Dic))
                {
                    yield return SubFile;
                }
            }
        }

        public static IEnumerable<string> GetAllSubFiles(string ParentDirectory)
        {
            foreach (string FilePath in Directory.EnumerateFiles(ParentDirectory))
            {
                yield return FilePath;
            }

            foreach (string SubDirectory in Directory.EnumerateDirectories(ParentDirectory))
            {
                foreach (string SubFilePath in GetAllSubFiles(SubDirectory))
                {
                    yield return SubFilePath;
                }
            }
        }

        public static Task ExecuteOnSTAThreadAsync(Action Executor)
        {
            if (Thread.CurrentThread.GetApartmentState() != ApartmentState.STA)
            {
                TaskCompletionSource CompletionSource = new TaskCompletionSource();

                Thread STAThread = new Thread(() =>
                {
                    Ole32.OleInitialize();

                    try
                    {
                        Executor();
                        CompletionSource.SetResult();
                    }
                    catch (Exception ex)
                    {
                        CompletionSource.SetException(ex);
                    }
                    finally
                    {
                        Ole32.OleUninitialize();
                    }
                })
                {
                    IsBackground = true,
                    Priority = ThreadPriority.Normal
                };
                STAThread.SetApartmentState(ApartmentState.STA);
                STAThread.Start();

                return CompletionSource.Task;
            }
            else
            {
                Executor();
                return Task.CompletedTask;
            }
        }

        public static Task<T> ExecuteOnSTAThreadAsync<T>(Func<T> Executor)
        {
            if (Thread.CurrentThread.GetApartmentState() != ApartmentState.STA)
            {
                TaskCompletionSource<T> CompletionSource = new TaskCompletionSource<T>();

                Thread STAThread = new Thread(() =>
                {
                    Ole32.OleInitialize();

                    try
                    {
                        CompletionSource.SetResult(Executor());
                    }
                    catch (Exception ex)
                    {
                        CompletionSource.SetException(ex);
                    }
                    finally
                    {
                        Ole32.OleUninitialize();
                    }
                })
                {
                    IsBackground = true,
                    Priority = ThreadPriority.Normal
                };
                STAThread.SetApartmentState(ApartmentState.STA);
                STAThread.Start();

                return CompletionSource.Task;
            }
            else
            {
                return Task.FromResult(Executor());
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
                RandomAccessStreamReference Reference = Pack.GetLogoAsRandomAccessStreamReference(new Windows.Foundation.Size(150, 150));

                using (IRandomAccessStreamWithContentType IconStream = await Reference.OpenReadAsync())
                using (Stream Stream = IconStream.AsStream())
                {
                    byte[] Logo = new byte[IconStream.Size];

                    Stream.Read(Logo, 0, (int)IconStream.Size);

                    return Logo;
                }
            }
            else
            {
                return Array.Empty<byte>();
            }
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
                try
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

                        return true;
                    }
                    else
                    {
                        return false;
                    }
                }
                catch
                {
                    return false;
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

                return false;
            }
            else
            {
                return false;
            }
        }

        public static async Task<InstalledApplicationPackage> GetInstalledApplicationAsync(string PackageFamilyName)
        {
            PackageManager Manager = new PackageManager();

            if (Manager.FindPackagesForUserWithPackageTypes(Convert.ToString(WindowsIdentity.GetCurrent()?.User), PackageFamilyName, PackageTypes.Main).FirstOrDefault() is Package Pack)
            {
                try
                {
                    RandomAccessStreamReference Reference = Pack.GetLogoAsRandomAccessStreamReference(new Windows.Foundation.Size(150, 150));

                    using (IRandomAccessStreamWithContentType IconStream = await Reference.OpenReadAsync())
                    using (Stream Stream = IconStream.AsStream())
                    {
                        byte[] Logo = new byte[IconStream.Size];

                        Stream.Read(Logo, 0, (int)IconStream.Size);

                        return new InstalledApplicationPackage(Pack.DisplayName, Pack.PublisherDisplayName, Pack.Id.FamilyName, Logo);
                    }
                }
                catch
                {
                    return null;
                }
            }
            else
            {
                return null;
            }
        }

        public static async Task<IEnumerable<InstalledApplicationPackage>> GetInstalledApplicationAsync()
        {
            List<Task<InstalledApplicationPackage>> ParallelList = new List<Task<InstalledApplicationPackage>>();

            PackageManager Manager = new PackageManager();

            foreach (Package Pack in Manager.FindPackagesForUserWithPackageTypes(Convert.ToString(WindowsIdentity.GetCurrent()?.User), PackageTypes.Main)
                                            .Where((Pack) => !string.IsNullOrWhiteSpace(Pack.DisplayName)
                                                                && Pack.Status.VerifyIsOK()
                                                                && Pack.SignatureKind is PackageSignatureKind.Developer
                                                                                      or PackageSignatureKind.Enterprise
                                                                                      or PackageSignatureKind.Store))
            {
                ParallelList.Add(Task.Run(() =>
                {
                    try
                    {
                        RandomAccessStreamReference Reference = Pack.GetLogoAsRandomAccessStreamReference(new Windows.Foundation.Size(150, 150));

                        using (IRandomAccessStreamWithContentType IconStream = Reference.OpenReadAsync().AsTask().Result)
                        using (Stream Stream = IconStream.AsStream())
                        {
                            byte[] Logo = new byte[IconStream.Size];

                            Stream.Read(Logo, 0, (int)IconStream.Size);

                            return new InstalledApplicationPackage(Pack.DisplayName, Pack.PublisherDisplayName, Pack.Id.FamilyName, Logo);
                        }
                    }
                    catch
                    {
                        return null;
                    }
                }));
            }

            return await Task.WhenAll(ParallelList).ContinueWith((PreTask) => PreTask.Result.OfType<InstalledApplicationPackage>()
                                                                                            .OrderBy((Pack) => Pack.AppDescription)
                                                                                            .ThenBy((Pack) => Pack.AppName));
        }
    }
}
