using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace RX_Explorer.Class
{
    public static class UncPath
    {
        public static async Task<string> MapUncToDrivePath(IEnumerable<string> SearchDriveRootPathList, string UncPath)
        {
            if (UncPath.StartsWith(@"\\"))
            {
                using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableControllerAsync())
                {
                    IReadOnlyDictionary<string, string> MapResult = await Exclusive.Controller.MapToUNCPathAsync(SearchDriveRootPathList);

                    Dictionary<string, string> ExchangedMapResult = new Dictionary<string, string>();

                    foreach (KeyValuePair<string, string> Pair in MapResult)
                    {
                        if (!ExchangedMapResult.ContainsKey(Pair.Value))
                        {
                            ExchangedMapResult.Add(Pair.Value, Pair.Key);
                        }
                    }

                    string RootPath = Path.GetPathRoot(UncPath);

                    if (ExchangedMapResult.TryGetValue(RootPath, out string DriveRootPath))
                    {
                        return UncPath.Equals(RootPath, StringComparison.OrdinalIgnoreCase) ? DriveRootPath : Path.Combine(DriveRootPath, Path.GetRelativePath(RootPath, UncPath));
                    }
                }
            }

            return UncPath;
        }
    }
}
