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
                            { "ActualPath", Item.FileSystemPath }
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
                RecycleBin.Empty();
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
                        Directory.CreateDirectory(DirectoryName);
                    }

                    using (ShellFolder DestItem = new ShellFolder(DirectoryName))
                    {
                        ShellFileOperations.Move(SourceItem, DestItem, null, ShellFileOperations.OperationFlags.AddUndoRecord | ShellFileOperations.OperationFlags.NoConfirmMkDir | ShellFileOperations.OperationFlags.Silent | ShellFileOperations.OperationFlags.RenameOnCollision);
                    }

                    string ExtraInfoPath = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Path), System.IO.Path.GetFileName(Path).Replace("$R", "$I"));

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
                string ExtraInfoFilePath = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Path), System.IO.Path.GetFileName(Path).Replace("$R", "$I"));

                if (File.Exists(ExtraInfoFilePath))
                {
                    File.Delete(ExtraInfoFilePath);
                }

                if (File.Exists(Path))
                {
                    File.Delete(Path);

                    return true;
                }
                else if (Directory.Exists(Path))
                {
                    Directory.Delete(Path, true);

                    return true;
                }
                else
                {
                    return false;
                }
            }
            catch
            {
                return false;
            }
        }
    }
}
