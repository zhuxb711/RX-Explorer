using Newtonsoft.Json;
using System;
using System.Collections.Generic;
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
            try
            {

                List<Dictionary<string, string>> RecycleItemList = new List<Dictionary<string, string>>();

                foreach (ShellItem Item in RecycleBin.GetItems())
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

                return JsonConvert.SerializeObject(RecycleItemList);
            }
            catch
            {
                return string.Empty;
            }
        }

        public static bool Restore(string Path)
        {
            try
            {
                using (ShellItem Item = new ShellItem(Path))
                {
                    RecycleBin.Restore(Item);
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static bool Delete(string Path)
        {
            try
            {
                RecycleBin.DeleteToRecycleBin(Path);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static bool EmptyRecycleBin()
        {
            try
            {
                RecycleBin.Empty();
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
