using ShareClassLibrary;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
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

                                                        Kernel32.QueryFullProcessImageName(ProcessHandle, 0, ProcessImageName, ref Size);

                                                        Info = new WindowInformation(ProcessImageName.ToString(), PID, State, hWnd);

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

        public static async Task ExecuteOnSTAThreadAsync(Action Executor)
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

                await CompletionSource.Task;
            }
            else
            {
                Executor();
            }
        }

        public static async Task<T> ExecuteOnSTAThreadAsync<T>(Func<T> Executor)
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

                return await CompletionSource.Task;
            }
            else
            {
                return Executor();
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

            if (Manager.FindPackagesForUserWithPackageTypes(string.Empty, PackageFamilyName, PackageTypes.Main).FirstOrDefault() is Package Pack)
            {
                RandomAccessStreamReference Reference = Pack.GetLogoAsRandomAccessStreamReference(new Windows.Foundation.Size(150, 150));

                using (IRandomAccessStreamWithContentType IconStream = await Reference.OpenReadAsync())
                using (DataReader Reader = new DataReader(IconStream))
                {
                    byte[] Buffer = new byte[IconStream.Size];

                    await Reader.LoadAsync(Convert.ToUInt32(IconStream.Size));

                    Reader.ReadBytes(Buffer);

                    return Buffer;
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

            return Manager.FindPackagesForUserWithPackageTypes(string.Empty, PackageFamilyName, PackageTypes.Main).Any();
        }

        public static Task<bool> LaunchApplicationFromAUMIDAsync(string AppUserModelId, params string[] PathArray)
        {
            if (PathArray.Length > 0)
            {
                return ExecuteOnSTAThreadAsync(() =>
                {
                    List<ShellItem> SItemList = new List<ShellItem>(PathArray.Length);

                    try
                    {
                        if (new Shell32.ApplicationActivationManager() is Shell32.IApplicationActivationManager Manager)
                        {
                            foreach (string Path in PathArray)
                            {
                                SItemList.Add(new ShellItem(Path));
                            }

                            using (ShellItemArray ItemArray = new ShellItemArray(SItemList))
                            {
                                Manager.ActivateForFile(AppUserModelId, ItemArray.IShellItemArray, "Open", out _);
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
                    finally
                    {
                        SItemList.ForEach((Item) => Item.Dispose());
                    }
                });
            }
            else
            {
                return LaunchApplicationFromAUMIDAsync(AppUserModelId);
            }
        }

        public static Task<bool> LaunchApplicationFromAUMIDAsync(string AppUserModelId)
        {
            return ExecuteOnSTAThreadAsync(() =>
            {
                try
                {
                    if (new Shell32.ApplicationActivationManager() is Shell32.IApplicationActivationManager Manager)
                    {
                        Manager.ActivateApplication(AppUserModelId, null, Shell32.ACTIVATEOPTIONS.AO_NONE, out _);
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

            if (Manager.FindPackagesForUserWithPackageTypes(string.Empty, PackageFamilyName, PackageTypes.Main).FirstOrDefault() is Package Pack)
            {
                foreach (AppListEntry Entry in await Pack.GetAppListEntriesAsync())
                {
                    if (PathArray.Length > 0)
                    {
                        if (!string.IsNullOrEmpty(Entry.AppUserModelId))
                        {
                            return await LaunchApplicationFromAUMIDAsync(Entry.AppUserModelId, PathArray);
                        }
                    }
                    else
                    {
                        if (await Entry.LaunchAsync())
                        {
                            return true;
                        }
                        else if (!string.IsNullOrEmpty(Entry.AppUserModelId))
                        {
                            return await LaunchApplicationFromAUMIDAsync(Entry.AppUserModelId);
                        }
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

            if (Manager.FindPackagesForUserWithPackageTypes(string.Empty, PackageFamilyName, PackageTypes.Main).FirstOrDefault() is Package Pack)
            {
                try
                {
                    RandomAccessStreamReference Reference = Pack.GetLogoAsRandomAccessStreamReference(new Windows.Foundation.Size(150, 150));

                    using (IRandomAccessStreamWithContentType IconStream = await Reference.OpenReadAsync())
                    using (DataReader Reader = new DataReader(IconStream))
                    {
                        await Reader.LoadAsync(Convert.ToUInt32(IconStream.Size));

                        byte[] Logo = new byte[IconStream.Size];

                        Reader.ReadBytes(Logo);

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

        public static async Task<InstalledApplicationPackage[]> GetInstalledApplicationAsync()
        {
            List<InstalledApplicationPackage> Result = new List<InstalledApplicationPackage>();

            PackageManager Manager = new PackageManager();

            foreach (Package Pack in Manager.FindPackagesForUserWithPackageTypes(string.Empty, PackageTypes.Main).Where((Pack) => !Pack.DisplayName.Equals("XSurfUwp", StringComparison.OrdinalIgnoreCase) && !Pack.IsDevelopmentMode).OrderBy((Pack) => Pack.Id.Publisher))
            {
                try
                {
                    RandomAccessStreamReference Reference = Pack.GetLogoAsRandomAccessStreamReference(new Windows.Foundation.Size(150, 150));

                    using (IRandomAccessStreamWithContentType IconStream = await Reference.OpenReadAsync())
                    using (DataReader Reader = new DataReader(IconStream))
                    {
                        await Reader.LoadAsync(Convert.ToUInt32(IconStream.Size));

                        byte[] Logo = new byte[IconStream.Size];

                        Reader.ReadBytes(Logo);

                        Result.Add(new InstalledApplicationPackage(Pack.DisplayName, Pack.PublisherDisplayName, Pack.Id.FamilyName, Logo));
                    }
                }
                catch
                {
                    continue;
                }
            }

            return Result.ToArray();
        }
    }
}
