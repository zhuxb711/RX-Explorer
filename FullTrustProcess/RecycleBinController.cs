using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
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

                using (ShellFolder RecycleBin = new ShellFolder(Shell32.KNOWNFOLDERID.FOLDERID_RecycleBinFolder))
                {
                    foreach (ShellItem Item in RecycleBin)
                    {
                        try
                        {
                            if (!Item.IsLink)
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
                        finally
                        {
                            Item.Dispose();
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
                RecycleBin.Empty(noSound: false);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static bool Restore(string Path)
        {
            try
            {
                using (ShellItem SourceItem = new ShellItem(Path))
                {
                    string DirectoryName = System.IO.Path.GetDirectoryName(SourceItem.Name);

                    if (!Directory.Exists(DirectoryName))
                    {
                        _ = Directory.CreateDirectory(DirectoryName);
                    }

                    using (ShellFolder DestItem = new ShellFolder(DirectoryName))
                    {
                        ShellFileOperations.Move(SourceItem, DestItem, null, ShellFileOperations.OperationFlags.AddUndoRecord | ShellFileOperations.OperationFlags.NoConfirmMkDir | ShellFileOperations.OperationFlags.Silent);
                    }

                    File.Delete(System.IO.Path.GetFileName(Path).Replace("$R", "$I"));
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
                File.Delete(System.IO.Path.GetFileName(Path));
                File.Delete(System.IO.Path.GetFileName(Path).Replace("$R", "$I"));
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
