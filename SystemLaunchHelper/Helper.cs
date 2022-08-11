using System;
using Vanara.PInvoke;

namespace SystemLaunchHelper
{
    internal static class Helper
    {
        public static bool CheckIfPackageFamilyNameExist(string PackageFamilyName)
        {
            if (!string.IsNullOrWhiteSpace(PackageFamilyName))
            {
                try
                {
                    uint FullNameCount = 0;
                    uint FullNameBufferLength = 0;

                    return Kernel32.FindPackagesByPackageFamily(PackageFamilyName, Kernel32.PACKAGE_FLAGS.PACKAGE_FILTER_HEAD | Kernel32.PACKAGE_FLAGS.PACKAGE_FILTER_DIRECT, ref FullNameCount, IntPtr.Zero, ref FullNameBufferLength, IntPtr.Zero, IntPtr.Zero) == Win32Error.ERROR_INSUFFICIENT_BUFFER;
                }
                catch (Exception)
                {
                    //No need to handle this exception
                }
            }

            return false;
        }
    }
}
