using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Vanara.Extensions;
using Vanara.PInvoke;
using Vanara.Windows.Shell;

namespace FullTrustProcess
{
    public static class RecycleBinController
    {
        public static string GenerateRecycleItemsByJson()
        {
            List<Dictionary<string, string>> RecycleItemList = new List<Dictionary<string, string>>();

            using (ShellFolder RecycleBin = new ShellFolder(Shell32.KNOWNFOLDERID.FOLDERID_RecycleBinFolder))
            {
                foreach (ShellItem Item in RecycleBin)
                {
                    if (!Path.GetExtension(Item.FileSystemPath).Equals(".lnk", StringComparison.OrdinalIgnoreCase))
                    {
                        Dictionary<string, string> PropertyDic = new Dictionary<string, string>
                        {
                            { "OriginPath", Item.Name },
                            { "ActualPath", Item.FileSystemPath },
                            { "CreateTime", Convert.ToString(((System.Runtime.InteropServices.ComTypes.FILETIME)Item.Properties[Ole32.PROPERTYKEY.System.DateCreated]).ToDateTime().ToBinary())}
                        };

                        RecycleItemList.Add(PropertyDic);
                    }
                }
            }

            return JsonConvert.SerializeObject(RecycleItemList);
        }

        public static bool EmptyRecycleBin()
        {
            HRESULT Result = Shell32.SHEmptyRecycleBin(IntPtr.Zero, null, Shell32.SHERB.SHERB_NOSOUND | Shell32.SHERB.SHERB_NOPROGRESSUI | Shell32.SHERB.SHERB_NOCONFIRMATION);
            return Result == HRESULT.S_OK || Result == HRESULT.E_UNEXPECTED;
        }
    }
}
