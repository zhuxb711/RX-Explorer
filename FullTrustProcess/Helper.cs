using ShareClassLibrary;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
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
        public static Task<T> CreateSTATask<T>(Func<T> Executor)
        {
            TaskCompletionSource<T> CompletionSource = new TaskCompletionSource<T>();

            Thread STAThread = new Thread(() =>
            {
                try
                {
                    Ole32.OleInitialize();
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
            });
            STAThread.SetApartmentState(ApartmentState.STA);
            STAThread.Start();

            return CompletionSource.Task;
        }

        public static string GetPackageFamilyNameFromUWPShellLink(string LinkPath)
        {
            using (ShellItem LinkItem = new ShellItem(LinkPath))
            {
                return LinkItem.Properties.GetPropertyString(Ole32.PROPERTYKEY.System.Link.TargetParsingPath).Split('!').FirstOrDefault();
            }
        }

        public static async Task<byte[]> GetIconDataFromPackageFamilyName(string PackageFamilyName)
        {
            PackageManager Manager = new PackageManager();

            if (Manager.FindPackagesForUserWithPackageTypes(string.Empty, PackageFamilyName, PackageTypes.Main).FirstOrDefault() is Package Pack)
            {
                RandomAccessStreamReference Reference = Pack.GetLogoAsRandomAccessStreamReference(new Windows.Foundation.Size(150, 150));

                using(IRandomAccessStreamWithContentType IconStream = await Reference.OpenReadAsync())
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

        public static async Task<bool> LaunchApplicationFromPackageFamilyName(string PackageFamilyName)
        {
            PackageManager Manager = new PackageManager();

            if (Manager.FindPackagesForUserWithPackageTypes(string.Empty, PackageFamilyName, PackageTypes.Main).FirstOrDefault() is Package Pack)
            {
                foreach (AppListEntry Entry in await Pack.GetAppListEntriesAsync())
                {
                    if (await Entry.LaunchAsync())
                    {
                        return true;
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
