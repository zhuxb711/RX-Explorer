using System;
using System.Collections.Generic;
using System.Linq;
using Windows.Foundation.Metadata;

namespace RX_Explorer.Class
{
    public static class WindowsVersionChecker
    {
        public static bool IsNewerOrEqual(WindowsVersion Version)
        {
            return ApiInformation.IsApiContractPresent("Windows.Foundation.UniversalApiContract", (ushort)Version);
        }

        public static bool IsOlderOrEqual(WindowsVersion Version)
        {
            if (IsNewerOrEqual(Version))
            {
                IReadOnlyList<int> VersionEnumValues = Enum.GetValues(typeof(WindowsVersion)).Cast<WindowsVersion>().Select((Enum) => (int)Enum).ToArray();

                int CurrentVersionIndex = VersionEnumValues.FindIndex((int)Version);

                if (CurrentVersionIndex < VersionEnumValues.Count - 1)
                {
                    return !IsNewerOrEqual((WindowsVersion)VersionEnumValues[CurrentVersionIndex + 1]);
                }
            }

            return false;
        }
    }
}
