using Windows.Foundation.Metadata;

namespace RX_Explorer.Class
{
    public static class WindowsVersionChecker
    {
        public static bool Windows10_1809 => ApiInformation.IsApiContractPresent("Windows.Foundation.UniversalApiContract", 7);

        public static bool Windows10_1903 => ApiInformation.IsApiContractPresent("Windows.Foundation.UniversalApiContract", 8);

        public static bool Windows10_1909 => ApiInformation.IsApiContractPresent("Windows.Foundation.UniversalApiContract", 9);

        public static bool Windows10_2004 => ApiInformation.IsApiContractPresent("Windows.Foundation.UniversalApiContract", 10);

        public static bool IsNewerOrEqual(Version Version)
        {
            return ApiInformation.IsApiContractPresent("Windows.Foundation.UniversalApiContract", (ushort)Version);
        }
    }
}
