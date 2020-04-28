using Windows.Foundation.Metadata;

namespace FileManager.Class
{
    public static class Win10VersionChecker
    {
        public static bool Windows10_1809 => ApiInformation.IsApiContractPresent("Windows.Foundation.UniversalApiContract", 7);

        public static bool Windows10_1903 => ApiInformation.IsApiContractPresent("Windows.Foundation.UniversalApiContract", 8);
    }
}
