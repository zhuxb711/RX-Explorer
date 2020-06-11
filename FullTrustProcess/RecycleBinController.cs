using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices.ComTypes;
using Vanara.Extensions;
using Vanara.PInvoke;
using Vanara.Windows.Shell;

namespace FullTrustProcess
{
    public static class RecycleBinController
    {
        private static readonly ShellFolder RecycleBin = new ShellFolder(Shell32.KNOWNFOLDERID.FOLDERID_RecycleBinFolder);

        public static string GenerateRecycleItemsByJson()
        {
            List<Dictionary<string, string>> RecycleItemList = new List<Dictionary<string, string>>();

            foreach (ShellItem Item in RecycleBin)
            {
                Dictionary<string, string> PropertyDic = new Dictionary<string, string>
                {
                    { "OriginPath", Item.Name },
                    { "ActualPath", Item.FileSystemPath },
                    { "CreateTime", Convert.ToString(((FILETIME)Item.Properties[Ole32.PROPERTYKEY.System.DateCreated]).ToDateTime().ToBinary())}
                };

                RecycleItemList.Add(PropertyDic);
            }

            return JsonConvert.SerializeObject(RecycleItemList);
        }

        public static void EmptyRecycleBin()
        {
            Shell32.SHEmptyRecycleBin(IntPtr.Zero, null, Shell32.SHERB.SHERB_NOCONFIRMATION | Shell32.SHERB.SHERB_NOPROGRESSUI);
        }
    }
}
