using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text.RegularExpressions;
using Vanara.Windows.Shell;

namespace FullTrustProcess
{
    public static class StorageItemController
    {
        public static bool CheckOccupied(string Path)
        {
            if (File.Exists(Path))
            {
                try
                {
                    FileInfo Info = new FileInfo(Path);

                    Info.Open(FileMode.Open, FileAccess.Read, FileShare.None).Dispose();

                    return false;
                }
                catch (IOException)
                {
                    return true;
                }
            }
            else
            {
                return false;
            }
        }

        public static bool TryUnoccupied(string Path)
        {
            if (File.Exists(Path))
            {
                try
                {
                    if (!CheckOccupied(Path))
                    {
                        return true;
                    }

                    using (Process CheckTool = new Process())
                    {
                        CheckTool.StartInfo.FileName = "handle.exe";
                        CheckTool.StartInfo.Arguments = $"\"{Path}\" /accepteula";
                        CheckTool.StartInfo.UseShellExecute = false;
                        CheckTool.StartInfo.RedirectStandardOutput = true;
                        CheckTool.StartInfo.CreateNoWindow = true;
                        CheckTool.Start();
                        CheckTool.WaitForExit();

                        bool IsKilled = false;

                        foreach (Match match in Regex.Matches(CheckTool.StandardOutput.ReadToEnd(), @"(?<=\s+pid:\s+)\b(\d+)\b(?=\s+)"))
                        {
                            using (Process TargetProcess = Process.GetProcessById(Convert.ToInt32(match.Value)))
                            {
                                try
                                {
                                    TargetProcess.Kill();
                                    IsKilled = true;
                                }
                                catch (InvalidOperationException)
                                {

                                }
                                catch
                                {
                                    return false;
                                }
                            }
                        }

                        return IsKilled;
                    }
                }
                catch
                {
                    return false;
                }
            }
            else
            {
                throw new FileNotFoundException();
            }
        }

        public static bool CheckPermission(FileSystemRights Permission, string Path)
        {
            try
            {
                bool InheritedDeny = false;
                bool InheritedAllow = false;

                WindowsIdentity CurrentUser = WindowsIdentity.GetCurrent();
                WindowsPrincipal CurrentPrincipal = new WindowsPrincipal(CurrentUser);

                FileSystemSecurity Security;

                if (Directory.Exists(Path))
                {
                    Security = Directory.GetAccessControl(Path);
                }
                else if (File.Exists(Path))
                {
                    Security = File.GetAccessControl(Path);
                }
                else
                {
                    //Relative path will come here, so we won't check its permission becasue no full path available
                    return true;
                }

                if (Security == null)
                {
                    return false;
                }

                AuthorizationRuleCollection AccessRules = Security.GetAccessRules(true, true, typeof(SecurityIdentifier));

                foreach (FileSystemAccessRule Rule in AccessRules)
                {
                    if (CurrentUser.User.Equals(Rule.IdentityReference) || CurrentPrincipal.IsInRole((SecurityIdentifier)Rule.IdentityReference))
                    {
                        if (Rule.AccessControlType == AccessControlType.Deny)
                        {
                            if ((Rule.FileSystemRights & Permission) == Permission)
                            {
                                if (Rule.IsInherited)
                                {
                                    InheritedDeny = true;
                                }
                                else
                                {
                                    // Non inherited "deny" takes overall precedence.
                                    return false;
                                }
                            }
                        }
                        else if (Rule.AccessControlType == AccessControlType.Allow)
                        {
                            if ((Rule.FileSystemRights & Permission) == Permission)
                            {
                                if (Rule.IsInherited)
                                {
                                    InheritedAllow = true;
                                }
                                else
                                {
                                    // Non inherited "allow" takes precedence over inherited rules
                                    return true;
                                }
                            }
                        }
                    }
                }

                return InheritedAllow && !InheritedDeny;
            }
            catch
            {
                return false;
            }
        }

        public static string GenerateUniquePath(string Path)
        {
            string UniquePath = Path;

            if (File.Exists(Path))
            {
                string NameWithoutExt = System.IO.Path.GetFileNameWithoutExtension(Path);
                string Extension = System.IO.Path.GetExtension(Path);
                string Directory = System.IO.Path.GetDirectoryName(Path);

                for (ushort Count = 1; File.Exists(UniquePath); Count++)
                {
                    if (Regex.IsMatch(NameWithoutExt, @".*\(\d+\)"))
                    {
                        UniquePath = System.IO.Path.Combine(Directory, $"{NameWithoutExt.Substring(0, NameWithoutExt.LastIndexOf("(", StringComparison.InvariantCultureIgnoreCase))}({Count}){Extension}");
                    }
                    else
                    {
                        UniquePath = System.IO.Path.Combine(Directory, $"{NameWithoutExt} ({Count}){Extension}");
                    }
                }
            }
            else if (Directory.Exists(Path))
            {
                string Directory = System.IO.Path.GetDirectoryName(Path);
                string Name = System.IO.Path.GetFileName(Path);

                for (ushort Count = 1; System.IO.Directory.Exists(UniquePath); Count++)
                {
                    if (Regex.IsMatch(Name, @".*\(\d+\)"))
                    {
                        UniquePath = System.IO.Path.Combine(Directory, $"{Name.Substring(0, Name.LastIndexOf("(", StringComparison.InvariantCultureIgnoreCase))}({Count})");
                    }
                    else
                    {
                        UniquePath = System.IO.Path.Combine(Directory, $"{Name} ({Count})");
                    }
                }
            }

            return UniquePath;
        }

        public static bool Rename(string Source, string DesireName, EventHandler<ShellFileOperations.ShellFileOpEventArgs> PostRenameEvent)
        {
            try
            {
                using (ShellFileOperations Operation = new ShellFileOperations
                {
                    Options = ShellFileOperations.OperationFlags.AddUndoRecord | ShellFileOperations.OperationFlags.NoConfirmMkDir | ShellFileOperations.OperationFlags.Silent | ShellFileOperations.OperationFlags.RequireElevation | ShellFileOperations.OperationFlags.RenameOnCollision
                })
                {
                    Operation.PostRenameItem += PostRenameEvent;

                    using (ShellItem Item = new ShellItem(Source))
                    {
                        Operation.QueueRenameOperation(Item, DesireName);
                    }

                    Operation.PerformOperations();

                    Operation.PostRenameItem -= PostRenameEvent;
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        public static bool Delete(IEnumerable<string> Source, bool PermanentDelete, ProgressChangedEventHandler Progress, EventHandler<ShellFileOperations.ShellFileOpEventArgs> PostDeleteEvent)
        {
            try
            {
                using (ShellFileOperations Operation = new ShellFileOperations
                {
                    Options = PermanentDelete
                    ? ShellFileOperations.OperationFlags.NoConfirmMkDir | ShellFileOperations.OperationFlags.Silent | ShellFileOperations.OperationFlags.NoConfirmation | ShellFileOperations.OperationFlags.RequireElevation
                    : ShellFileOperations.OperationFlags.AddUndoRecord | ShellFileOperations.OperationFlags.Silent | ShellFileOperations.OperationFlags.NoConfirmMkDir | ShellFileOperations.OperationFlags.RecycleOnDelete | ShellFileOperations.OperationFlags.RequireElevation
                })
                {
                    Operation.UpdateProgress += Progress;
                    Operation.PostDeleteItem += PostDeleteEvent;

                    foreach (string Path in Source)
                    {
                        using (ShellItem Item = new ShellItem(Path))
                        {
                            Operation.QueueDeleteOperation(Item);
                        }
                    }

                    Operation.PerformOperations();

                    Operation.PostDeleteItem -= PostDeleteEvent;
                    Operation.UpdateProgress -= Progress;
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        public static bool Copy(IEnumerable<KeyValuePair<string, string>> Source, string DestinationPath, ProgressChangedEventHandler Progress, EventHandler<ShellFileOperations.ShellFileOpEventArgs> PostCopyEvent)
        {
            try
            {
                if (!Directory.Exists(DestinationPath))
                {
                    _ = Directory.CreateDirectory(DestinationPath);
                }

                ShellFileOperations.OperationFlags Options = Source.All((Item) => Path.GetDirectoryName(Item.Key) == DestinationPath)
                                                             ? ShellFileOperations.OperationFlags.AddUndoRecord | ShellFileOperations.OperationFlags.NoConfirmMkDir | ShellFileOperations.OperationFlags.Silent | ShellFileOperations.OperationFlags.RenameOnCollision | ShellFileOperations.OperationFlags.RequireElevation
                                                             : ShellFileOperations.OperationFlags.AddUndoRecord | ShellFileOperations.OperationFlags.NoConfirmMkDir | ShellFileOperations.OperationFlags.Silent | ShellFileOperations.OperationFlags.RequireElevation;

                using (ShellFileOperations Operation = new ShellFileOperations
                {
                    Options = Options
                })
                {
                    Operation.UpdateProgress += Progress;
                    Operation.PostCopyItem += PostCopyEvent;

                    foreach (KeyValuePair<string, string> SourceInfo in Source)
                    {
                        ShellItem SourceItem = new ShellItem(SourceInfo.Key);
                        ShellFolder DestItem = new ShellFolder(DestinationPath);
                        Operation.QueueCopyOperation(SourceItem, DestItem, string.IsNullOrEmpty(SourceInfo.Value) ? null : SourceInfo.Value);
                    }

                    Operation.PerformOperations();

                    Operation.PostCopyItem -= PostCopyEvent;
                    Operation.UpdateProgress -= Progress;
                }

                return true;
            }
            catch
            {
                var temp = new Win32Exception(Marshal.GetLastWin32Error());
                return false;
            }
        }

        public static bool Move(IEnumerable<KeyValuePair<string, string>> Source, string DestinationPath, ProgressChangedEventHandler Progress, EventHandler<ShellFileOperations.ShellFileOpEventArgs> PostMoveEvent)
        {
            try
            {
                if (!Directory.Exists(DestinationPath))
                {
                    _ = Directory.CreateDirectory(DestinationPath);
                }

                using (ShellFileOperations Operation = new ShellFileOperations
                {
                    Options = ShellFileOperations.OperationFlags.AddUndoRecord | ShellFileOperations.OperationFlags.NoConfirmMkDir | ShellFileOperations.OperationFlags.Silent | ShellFileOperations.OperationFlags.RequireElevation
                })
                {
                    Operation.UpdateProgress += Progress;
                    Operation.PostMoveItem += PostMoveEvent;

                    foreach (KeyValuePair<string, string> SourceInfo in Source)
                    {
                        using (ShellItem SourceItem = new ShellItem(SourceInfo.Key))
                        using (ShellFolder DestItem = new ShellFolder(DestinationPath))
                        {
                            Operation.QueueMoveOperation(SourceItem, DestItem, string.IsNullOrEmpty(SourceInfo.Value) ? null : SourceInfo.Value);
                        }
                    }

                    Operation.PerformOperations();

                    Operation.PostMoveItem -= PostMoveEvent;
                    Operation.UpdateProgress -= Progress;
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
