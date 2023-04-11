using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Xml;
using Vanara.PInvoke;

namespace SystemLaunchHelper.Class
{
    internal static class Helper
    {
        public static string GetPackageFullNameFromPackageFamilyName(string PackageFamilyName)
        {
            if (!string.IsNullOrWhiteSpace(PackageFamilyName))
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

            return string.Empty;
        }

        public static string GetInstalledPathFromPackageFullName(string PackageFullName)
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

            return string.Empty;
        }

        public static string GetPackageFamilyNameFromPackageFullName(string PackageFullName)
        {
            if (!string.IsNullOrEmpty(PackageFullName))
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

            return string.Empty;
        }

        public static string GetAppUserModeIdFromPackageFullName(string PackageFullName)
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

            return string.Empty;
        }
    }
}
