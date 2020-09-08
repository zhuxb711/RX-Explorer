using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices.ComTypes;
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
                    try
                    {
                        Dictionary<string, string> PropertyDic = new Dictionary<string, string>
                        {
                            { "OriginPath", Item.IsLink ? $"{Item.Name}.lnk" : Item.Name },
                            { "ActualPath", Item.FileSystemPath }
                        };

                        if (Item.Properties.TryGetValue(Ole32.PROPERTYKEY.System.Recycle.DateDeleted, out object DeleteFileTime))
                        {
                            PropertyDic.Add("DeleteTime", Convert.ToString(((FILETIME)DeleteFileTime).ToInt64()));
                        }
                        else
                        {
                            PropertyDic.Add("DeleteTime", Convert.ToString(DateTimeOffset.MaxValue.ToFileTime()));
                        }

                        RecycleItemList.Add(PropertyDic);
                    }
                    finally
                    {
                        Item.Dispose();
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
                        ShellFileOperations.Move(SourceItem, DestItem, null, ShellFileOperations.OperationFlags.AddUndoRecord | ShellFileOperations.OperationFlags.NoConfirmMkDir | ShellFileOperations.OperationFlags.Silent | ShellFileOperations.OperationFlags.RenameOnCollision);
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
                File.Delete(Path);
                File.Delete(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Path), System.IO.Path.GetFileName(Path).Replace("$R", "$I")));
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
