using System.Threading.Tasks;

namespace RX_Explorer.Class
{
    public static class UncPath
    {
        public static async Task<string> MapUncToDrivePath(string UncPath)
        {
            if (UncPath.StartsWith(@"\\"))
            {
                using (AuxiliaryTrustProcessController.Exclusive Exclusive = await AuxiliaryTrustProcessController.GetControllerExclusiveAsync())
                {
                    return await Exclusive.Controller.MapUncToDrivePathAsync(UncPath);
                }
            }

            return string.Empty;
        }
    }
}
