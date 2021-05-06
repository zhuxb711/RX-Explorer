using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;
using Vanara.PInvoke;
using Vanara.Windows.Shell;

namespace FullTrustProcess
{
    public static class StorageController
    {
        /// <summary>
        /// Find out what process(es) have a lock on the specified file.
        /// </summary>
        /// <param name="path">Path of the file.</param>
        /// <returns>Processes locking the file</returns>
        public static List<Process> GetLockingProcesses(string path)
        {
            StringBuilder SessionKey = new StringBuilder(Guid.NewGuid().ToString());

            if (RstrtMgr.RmStartSession(out uint SessionHandle, 0, SessionKey).Succeeded)
            {
                try
                {
                    string[] ResourcesFileName = new string[] { path };

                    if (RstrtMgr.RmRegisterResources(SessionHandle, (uint)ResourcesFileName.Length, ResourcesFileName, 0, null, 0, null).Succeeded)
                    {
                        uint pnProcInfo = 0;

                        //Note: there's a race condition here -- the first call to RmGetList() returns
                        //      the total number of process. However, when we call RmGetList() again to get
                        //      the actual processes this number may have increased.
                        Win32Error Error = RstrtMgr.RmGetList(SessionHandle, out uint pnProcInfoNeeded, ref pnProcInfo, null, out _);

                        if (Error == Win32Error.ERROR_MORE_DATA)
                        {
                            // Create an array to store the process results
                            RstrtMgr.RM_PROCESS_INFO[] ProcessInfo = new RstrtMgr.RM_PROCESS_INFO[pnProcInfoNeeded];

                            pnProcInfo = pnProcInfoNeeded;

                            // Get the list
                            if (RstrtMgr.RmGetList(SessionHandle, out pnProcInfoNeeded, ref pnProcInfo, ProcessInfo, out _).Succeeded)
                            {
                                List<Process> LockProcesses = new List<Process>((int)pnProcInfo);

                                // Enumerate all of the results and add them to the 
                                // list to be returned
                                for (int i = 0; i < pnProcInfo; i++)
                                {
                                    try
                                    {
                                        LockProcesses.Add(Process.GetProcessById(Convert.ToInt32(ProcessInfo[i].Process.dwProcessId)));
                                    }
                                    catch
                                    {
                                        // catch the error -- in case the process is no longer running
                                        Debug.WriteLine("Process is no longer running");
                                    }
                                }

                                return LockProcesses;
                            }
                            else
                            {
                                Debug.WriteLine("Could not list processes locking resource");
                                return new List<Process>(0);
                            }
                        }
                        else if (Error != Win32Error.ERROR_SUCCESS)
                        {
                            Debug.WriteLine("Could not list processes locking resource. Failed to get size of result.");
                            return new List<Process>(0);
                        }
                        else
                        {
                            Debug.WriteLine("Unknown error");
                            return new List<Process>(0);
                        }
                    }
                    else
                    {
                        Debug.WriteLine("Could not register resource");
                        return new List<Process>(0);
                    }
                }
                finally
                {
                    RstrtMgr.RmEndSession(SessionHandle);
                }
            }
            else
            {
                Debug.WriteLine("Could not begin restart session. Unable to determine file locker.");
                return new List<Process>(0);
            }
        }

        public static bool CheckOccupied(string Path)
        {
            if (File.Exists(Path))
            {
                try
                {
                    using (Kernel32.SafeHFILE Handle = Kernel32.CreateFile(Path, Kernel32.FileAccess.FILE_GENERIC_READ, FileShare.None, null, FileMode.Open, FileFlagsAndAttributes.FILE_ATTRIBUTE_NORMAL))
                    {
                        if (Handle.IsInvalid)
                        {
                            return true;
                        }
                        else
                        {
                            return false;
                        }
                    }
                }
                catch
                {
                    return true;
                }
            }
            else
            {
                return false;
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
                    Options = ShellFileOperations.OperationFlags.AddUndoRecord
                                | ShellFileOperations.OperationFlags.NoConfirmMkDir
                                | ShellFileOperations.OperationFlags.Silent
                                | ShellFileOperations.OperationFlags.RequireElevation
                                | ShellFileOperations.OperationFlags.RenameOnCollision
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
                                ? ShellFileOperations.OperationFlags.Silent
                                | ShellFileOperations.OperationFlags.NoConfirmation
                                | ShellFileOperations.OperationFlags.RequireElevation

                                : ShellFileOperations.OperationFlags.Silent
                                | ShellFileOperations.OperationFlags.AddUndoRecord
                                | ShellFileOperations.OperationFlags.NoConfirmation
                                | ShellFileOperations.OperationFlags.RecycleOnDelete
                                | ShellFileOperations.OperationFlags.RequireElevation
                                | ShellFileOperations.OperationFlags.WantNukeWarning
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

        public static bool Copy(IEnumerable<string> SourcePath, string DestinationPath, ProgressChangedEventHandler Progress, EventHandler<ShellFileOperations.ShellFileOpEventArgs> PostCopyEvent)
        {
            try
            {
                if (!Directory.Exists(DestinationPath))
                {
                    _ = Directory.CreateDirectory(DestinationPath);
                }


                using (ShellFileOperations Operation = new ShellFileOperations
                {
                    Options = ShellFileOperations.OperationFlags.AddUndoRecord
                              | ShellFileOperations.OperationFlags.NoConfirmMkDir
                              | ShellFileOperations.OperationFlags.Silent
                              | ShellFileOperations.OperationFlags.RenameOnCollision
                              | ShellFileOperations.OperationFlags.RequireElevation
                })
                {
                    Operation.UpdateProgress += Progress;
                    Operation.PostCopyItem += PostCopyEvent;

                    foreach (string Source in SourcePath)
                    {
                        using (ShellItem SourceItem = new ShellItem(Source))
                        using (ShellFolder DestItem = new ShellFolder(DestinationPath))
                        {
                            Operation.QueueCopyOperation(SourceItem, DestItem);
                        }
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

        public static bool Move(IEnumerable<string> SourcePath, string DestinationPath, ProgressChangedEventHandler Progress, EventHandler<ShellFileOperations.ShellFileOpEventArgs> PostMoveEvent)
        {
            try
            {
                if (!Directory.Exists(DestinationPath))
                {
                    _ = Directory.CreateDirectory(DestinationPath);
                }

                using (ShellFileOperations Operation = new ShellFileOperations
                {
                    Options = ShellFileOperations.OperationFlags.AddUndoRecord
                                | ShellFileOperations.OperationFlags.NoConfirmMkDir
                                | ShellFileOperations.OperationFlags.Silent
                                | ShellFileOperations.OperationFlags.RequireElevation
                                | ShellFileOperations.OperationFlags.RenameOnCollision
                })
                {
                    Operation.UpdateProgress += Progress;
                    Operation.PostMoveItem += PostMoveEvent;

                    foreach (string Source in SourcePath)
                    {
                        using (ShellItem SourceItem = new ShellItem(Source))
                        using (ShellFolder DestItem = new ShellFolder(DestinationPath))
                        {
                            Operation.QueueMoveOperation(SourceItem, DestItem);
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
