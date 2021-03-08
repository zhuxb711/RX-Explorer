using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices.ComTypes;
using System.Text.Json;
using Vanara.Extensions;
using Vanara.PInvoke;
using Vanara.Windows.Shell;
using Windows.Storage;

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
                            { "ActualPath", Item.FileSystemPath },
                            { "DeleteTime", (Item.IShellItem as Shell32.IShellItem2).GetFileTime(Ole32.PROPERTYKEY.System.Recycle.DateDeleted).ToInt64().ToString() }
                        };

                        if (File.Exists(Item.FileSystemPath))
                        {
                            PropertyDic.Add("StorageType", Enum.GetName(typeof(StorageItemTypes), StorageItemTypes.File));

                            if (Path.HasExtension(Item.Name))
                            {
                                PropertyDic.Add("OriginPath", Item.Name);
                            }
                            else
                            {
                                PropertyDic.Add("OriginPath", Item.Name + Item.FileInfo.Extension);
                            }
                        }
                        else if (Directory.Exists(Item.FileSystemPath))
                        {
                            PropertyDic.Add("OriginPath", Item.Name);
                            PropertyDic.Add("StorageType", Enum.GetName(typeof(StorageItemTypes), StorageItemTypes.Folder));
                        }
                        else
                        {
                            continue;
                        }

                        RecycleItemList.Add(PropertyDic);
                    }
                    finally
                    {
                        Item.Dispose();
                    }
                }

                return JsonSerializer.Serialize(RecycleItemList);
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
                RecycleBin.Empty(false);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static bool Restore(string OriginPath)
        {
            try
            {
                using (ShellItem SourceItem = RecycleBin.GetItemFromOriginalPath(OriginPath))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(SourceItem.Name));

                    if (File.Exists(SourceItem.FileSystemPath))
                    {
                        File.Move(SourceItem.FileSystemPath, StorageItemController.GenerateUniquePath(SourceItem.Name));
                    }
                    else if (Directory.Exists(SourceItem.FileSystemPath))
                    {
                        Directory.Move(SourceItem.FileSystemPath, StorageItemController.GenerateUniquePath(SourceItem.Name));
                    }

                    string ExtraInfoPath = Path.Combine(Path.GetDirectoryName(SourceItem.FileSystemPath), Path.GetFileName(SourceItem.FileSystemPath).Replace("$R", "$I"));

                    if (File.Exists(ExtraInfoPath))
                    {
                        File.Delete(ExtraInfoPath);
                    }
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
                if (File.Exists(Path))
                {
                    File.Delete(Path);
                }
                else if (Directory.Exists(Path))
                {
                    Directory.Delete(Path, true);
                }
                else
                {
                    return false;
                }

                string ExtraInfoFilePath = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Path), System.IO.Path.GetFileName(Path).Replace("$R", "$I"));

                if (File.Exists(ExtraInfoFilePath))
                {
                    File.Delete(ExtraInfoFilePath);
                }

                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
