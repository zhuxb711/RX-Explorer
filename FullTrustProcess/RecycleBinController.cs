using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Vanara.Extensions;
using Vanara.PInvoke;
using Vanara.Windows.Shell;

namespace FullTrustProcess
{
    public static class RecycleBinController
    {
        [DllImport("Shell32.dll", SetLastError = false, ExactSpelling = true)]
        private static extern HRESULT SHUpdateRecycleBinIcon();

        [DllImport("Shell32.dll", CharSet = CharSet.Unicode)]
        private static extern int SHEmptyRecycleBin(IntPtr hwnd, string pszRootPath, RecycleFlags dwFlags);

        private enum RecycleFlags
        {
            SHERB_NOCONFIRMATION = 0x00000001,
            SHERB_NOPROGRESSUI = 0x00000002,
            SHERB_NOSOUND = 0x00000004
        }

        public static string GenerateRecycleItemsByJson()
        {
            try
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
            catch
            {
                return string.Empty;
            }
        }

        public static bool EmptyRecycleBin()
        {
            try
            {
                HRESULT Result = SHEmptyRecycleBin(IntPtr.Zero, null, RecycleFlags.SHERB_NOSOUND|RecycleFlags.SHERB_NOCONFIRMATION);
                return Result == HRESULT.S_OK || Result == HRESULT.E_UNEXPECTED;
            }
            finally
            {
                SHUpdateRecycleBinIcon();
            }
        }
    }
}
