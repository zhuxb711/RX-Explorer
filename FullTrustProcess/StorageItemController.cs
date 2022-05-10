using ShareClassLibrary;
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
using Vanara.PInvoke;
using Vanara.Windows.Shell;

namespace FullTrustProcess
{
    public static class StorageItemController
    {
        /// <summary>
        /// Find out what process(es) have a lock on the specified file.
        /// </summary>
        /// <param name="path">Path of the file.</param>
        /// <returns>Processes locking the file</returns>
        public static IReadOnlyList<Process> GetLockingProcesses(string path)
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
                            RstrtMgr.RM_PROCESS_INFO[] ProcessInfo = new RstrtMgr.RM_PROCESS_INFO[pnProcInfoNeeded];

                            pnProcInfo = pnProcInfoNeeded;

                            if (RstrtMgr.RmGetList(SessionHandle, out pnProcInfoNeeded, ref pnProcInfo, ProcessInfo, out _).Succeeded)
                            {
                                List<Process> LockProcesses = new List<Process>((int)pnProcInfo);

                                for (int i = 0; i < pnProcInfo; i++)
                                {
                                    try
                                    {
                                        LockProcesses.Add(Process.GetProcessById(Convert.ToInt32(ProcessInfo[i].Process.dwProcessId)));
                                    }
                                    catch (Exception ex)
                                    {
                                        LogTracer.Log(ex, "Process is no longer running");
                                    }
                                }

                                return LockProcesses;
                            }
                            else
                            {
                                LogTracer.Log("Could not list processes locking resource");
                                return new List<Process>(0);
                            }
                        }
                        else if (Error != Win32Error.ERROR_SUCCESS)
                        {
                            LogTracer.Log("Could not list processes locking resource. Failed to get size of result.");
                            return new List<Process>(0);
                        }
                        else
                        {
                            LogTracer.Log("Unknown error");
                            return new List<Process>(0);
                        }
                    }
                    else
                    {
                        LogTracer.Log("Could not register resource");
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
                LogTracer.Log("Could not begin restart session. Unable to determine file locker.");
                return new List<Process>(0);
            }
        }

        public static bool CheckCaptured(string Path)
        {
            if (File.Exists(Path))
            {
                try
                {
                    using (Kernel32.SafeHFILE Handle = Kernel32.CreateFile(Path, Kernel32.FileAccess.FILE_GENERIC_READ, FileShare.None, null, FileMode.Open, FileFlagsAndAttributes.FILE_ATTRIBUTE_NORMAL))
                    {
                        if (Handle.IsNull || Handle.IsInvalid)
                        {
                            return true;
                        }
                        else
                        {
                            if (System.IO.Path.GetExtension(Path).Equals(".exe", StringComparison.OrdinalIgnoreCase))
                            {
                                Process[] RunningProcess = Process.GetProcessesByName(System.IO.Path.GetFileNameWithoutExtension(Path));

                                try
                                {
                                    if (RunningProcess.Length > 0)
                                    {
                                        foreach (Process Pro in RunningProcess)
                                        {
                                            using (Kernel32.SafeHPROCESS ProcessHandle = Kernel32.OpenProcess(new ACCESS_MASK(0x1000), false, Convert.ToUInt32(Pro.Id)))
                                            {
                                                if (!ProcessHandle.IsInvalid && !ProcessHandle.IsNull)
                                                {
                                                    uint Size = 260;
                                                    StringBuilder ProcessImageName = new StringBuilder((int)Size);

                                                    if (Kernel32.QueryFullProcessImageName(ProcessHandle, 0, ProcessImageName, ref Size))
                                                    {
                                                        if (Path.Equals(ProcessImageName.ToString(), StringComparison.OrdinalIgnoreCase))
                                                        {
                                                            return true;
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                                finally
                                {
                                    Array.ForEach(RunningProcess, (Pro) => Pro.Dispose());
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogTracer.Log(ex, $"An exception was threw in {nameof(CheckCaptured)}");
                }
            }
            else if (Directory.Exists(Path))
            {
                foreach (string SubFilePath in Directory.GetFiles(Path, "*", SearchOption.AllDirectories))
                {
                    if (CheckCaptured(SubFilePath))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public static IReadOnlyList<PermissionDataPackage> GetAllAccountPermissions(string Path)
        {
            FileSystemSecurity Security;

            if (Directory.Exists(Path))
            {
                Security = new DirectoryInfo(Path).GetAccessControl(AccessControlSections.Access);
            }
            else if (File.Exists(Path))
            {
                Security = new FileInfo(Path).GetAccessControl(AccessControlSections.Access);
            }
            else
            {
                throw new FileNotFoundException(Path);
            }

            static bool CheckPermissionCore(IEnumerable<FileSystemAccessRule> Rules, FileSystemRights Permission)
            {
                bool InheritedDeny = false;
                bool InheritedAllow = false;

                foreach (FileSystemAccessRule Rule in Rules)
                {
                    if (Rule.AccessControlType == AccessControlType.Deny)
                    {
                        if (Rule.FileSystemRights.HasFlag(Permission))
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
                        if (Rule.FileSystemRights.HasFlag(Permission))
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

                return InheritedAllow && !InheritedDeny;
            }

            HashSet<string> WellKnownSID = new HashSet<string>(8)
            {
                "S-1-1-0",
                "S-1-3-0",
                "S-1-5-11",
                "S-1-5-18",
                "S-1-5-32-544",
                "S-1-5-32-546",
                "S-1-5-32-545",
                "S-1-5-21domain-500",
                "S-1-5-21domain-501"
            };

            List<PermissionDataPackage> PermissionsResult = new List<PermissionDataPackage>();

            foreach (IGrouping<IdentityReference, FileSystemAccessRule> RuleGroupByAccount in Security.GetAccessRules(true, true, typeof(NTAccount)).Cast<FileSystemAccessRule>()
                                                                                                                                                    .GroupBy((Rule) => Rule.IdentityReference))
            {
                try
                {
                    SecurityIdentifier SecurityId = new SecurityIdentifier(RuleGroupByAccount.Key.Translate(typeof(SecurityIdentifier)).Value);

                    byte[] SidBuffer = new byte[SecurityId.BinaryLength];
                    SecurityId.GetBinaryForm(SidBuffer, 0);

                    int CchName = 256;
                    int CchRefDomainName = 256;
                    StringBuilder Name = new StringBuilder(CchName);
                    StringBuilder Domain = new StringBuilder(CchRefDomainName);
                    AccountType Type = AccountType.Unknown;

                    if (AdvApi32.LookupAccountSid(null, SidBuffer, Name, ref CchName, Domain, ref CchRefDomainName, out AdvApi32.SID_NAME_USE SidType))
                    {
                        switch (SidType)
                        {
                            case AdvApi32.SID_NAME_USE.SidTypeGroup:
                            case AdvApi32.SID_NAME_USE.SidTypeWellKnownGroup:
                            case AdvApi32.SID_NAME_USE.SidTypeAlias:
                                {
                                    Type = AccountType.Group;
                                    break;
                                }
                            case AdvApi32.SID_NAME_USE.SidTypeUser:
                                {
                                    Type = AccountType.User;
                                    break;
                                }
                        }
                    }

                    PermissionsResult.Add(new PermissionDataPackage
                    (
                        Type switch
                        {
                            AccountType.User or AccountType.Group => (WellKnownSID.Contains(SecurityId.Value) || string.IsNullOrEmpty(Domain.ToString())) ? Name.ToString() : $"{Domain}\\{Name}",
                            _ => RuleGroupByAccount.Key.Value
                        },
                        Type,
                        new Dictionary<Permissions, bool>
                        {
                        { Permissions.FullControl, CheckPermissionCore(RuleGroupByAccount, FileSystemRights.FullControl) },
                        { Permissions.Modify, CheckPermissionCore(RuleGroupByAccount, FileSystemRights.Modify) },
                        { Permissions.ListDirectory, CheckPermissionCore(RuleGroupByAccount, FileSystemRights.ListDirectory) },
                        { Permissions.ReadAndExecute, CheckPermissionCore(RuleGroupByAccount, FileSystemRights.ReadAndExecute) },
                        { Permissions.Read, CheckPermissionCore(RuleGroupByAccount, FileSystemRights.Read) },
                        { Permissions.Write, CheckPermissionCore(RuleGroupByAccount, FileSystemRights.Write) },
                        }
                    ));
                }
                catch (Exception ex)
                {
                    LogTracer.Log(ex, $"Could not load the permission of {RuleGroupByAccount.Key.Value}");
                }
            }

            return PermissionsResult;
        }

        public static bool CheckPermission(string Path, FileSystemRights Permission)
        {
            try
            {
                using (WindowsIdentity CurrentUser = WindowsIdentity.GetCurrent())
                {
                    WindowsPrincipal CurrentPrincipal = new WindowsPrincipal(CurrentUser);

                    FileSystemSecurity Security;

                    if (Directory.Exists(Path))
                    {
                        Security = new DirectoryInfo(Path).GetAccessControl(AccessControlSections.Access);
                    }
                    else if (File.Exists(Path))
                    {
                        Security = new FileInfo(Path).GetAccessControl(AccessControlSections.Access);
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

                    bool InheritedDeny = false;
                    bool InheritedAllow = false;

                    foreach (FileSystemAccessRule Rule in Security.GetAccessRules(true, true, typeof(SecurityIdentifier)).Cast<FileSystemAccessRule>()
                                                                                                                         .Where((Rule) => CurrentUser.User.Equals(Rule.IdentityReference)
                                                                                                                                          || CurrentPrincipal.IsInRole((SecurityIdentifier)Rule.IdentityReference)))
                    {
                        switch (Rule.AccessControlType)
                        {
                            case AccessControlType.Deny:
                                {
                                    if (Rule.FileSystemRights.HasFlag(Permission))
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

                                    break;
                                }
                            case AccessControlType.Allow:
                                {
                                    if (Rule.FileSystemRights.HasFlag(Permission))
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

                                    break;
                                }
                        }
                    }

                    return InheritedAllow && !InheritedDeny;
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"An exception was threw in {nameof(CheckPermission)}");
            }

            return false;
        }

        public static bool Create(CreateType Type, string Path)
        {
            try
            {
                switch (Type)
                {
                    case CreateType.File:
                        {
                            using (Kernel32.SafeHFILE Handle = Kernel32.CreateFile(Path, Kernel32.FileAccess.GENERIC_READ, FileShare.None, null, FileMode.CreateNew, FileFlagsAndAttributes.FILE_ATTRIBUTE_NORMAL))
                            {
                                return !Handle.IsInvalid && !Handle.IsNull;
                            }
                        }
                    case CreateType.Folder:
                        {
                            return Kernel32.CreateDirectory(Path);
                        }
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, "Could not create an item");
            }

            return false;
        }

        public static bool Rename(string Source, string DesireName, EventHandler<ShellFileOperations.ShellFileOpEventArgs> PostRenameEvent = null)
        {
            try
            {
                using (ShellFileOperations Operation = new ShellFileOperations
                {
                    Options = ShellFileOperations.OperationFlags.AddUndoRecord
                              | ShellFileOperations.OperationFlags.Silent
                              | ShellFileOperations.OperationFlags.RequireElevation
                              | ShellFileOperations.OperationFlags.RenameOnCollision
                              | ShellFileOperations.OperationFlags.NoErrorUI
                              | ShellFileOperations.OperationFlags.EarlyFailure
                              | ShellFileOperations.OperationFlags.ShowElevationPrompt
                })
                {
                    if (PostRenameEvent != null)
                    {
                        Operation.PostRenameItem += PostRenameEvent;
                    }

                    try
                    {
                        using (ShellItem Item = new ShellItem(Source))
                        {
                            Operation.QueueRenameOperation(Item, DesireName);
                        }

                        Operation.PerformOperations();

                        if (Operation.AnyOperationsAborted)
                        {
                            throw new OperationCanceledException();
                        }
                    }
                    finally
                    {
                        if (PostRenameEvent != null)
                        {
                            Operation.PostRenameItem -= PostRenameEvent;
                        }
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"An exception was threw in {nameof(Rename)}");
            }

            return false;
        }

        public static bool Delete(IEnumerable<string> Source,
                                  bool PermanentDelete,
                                  ProgressChangedEventHandler Progress = null,
                                  EventHandler<ShellFileOperations.ShellFileOpEventArgs> PreDeleteEvent = null,
                                  EventHandler<ShellFileOperations.ShellFileOpEventArgs> PostDeleteEvent = null)
        {
            try
            {
                ShellFileOperations.OperationFlags Flags = ShellFileOperations.OperationFlags.Silent
                                                           | ShellFileOperations.OperationFlags.NoConfirmation
                                                           | ShellFileOperations.OperationFlags.RequireElevation
                                                           | ShellFileOperations.OperationFlags.NoErrorUI
                                                           | ShellFileOperations.OperationFlags.EarlyFailure
                                                           | ShellFileOperations.OperationFlags.ShowElevationPrompt;

                if (!PermanentDelete)
                {
                    Flags |= ShellFileOperations.OperationFlags.AddUndoRecord;
                    Flags |= ShellFileOperations.OperationFlags.RecycleOnDelete;
                    Flags |= ShellFileOperations.OperationFlags.WantNukeWarning;
                }

                using (ShellFileOperations Operation = new ShellFileOperations
                {
                    Options = Flags
                })
                {
                    if (Progress != null)
                    {
                        Operation.UpdateProgress += Progress;
                    }

                    if (PreDeleteEvent != null)
                    {
                        Operation.PreDeleteItem += PreDeleteEvent;
                    }

                    if (PostDeleteEvent != null)
                    {
                        Operation.PostDeleteItem += PostDeleteEvent;
                    }

                    try
                    {
                        foreach (string Path in Source)
                        {
                            using (ShellItem Item = new ShellItem(Path))
                            {
                                Operation.QueueDeleteOperation(Item);
                            }
                        }

                        Operation.PerformOperations();

                        if (Operation.AnyOperationsAborted)
                        {
                            throw new OperationCanceledException();
                        }
                    }
                    finally
                    {
                        if (PreDeleteEvent != null)
                        {
                            Operation.PreDeleteItem -= PreDeleteEvent;
                        }

                        if (PostDeleteEvent != null)
                        {
                            Operation.PostDeleteItem -= PostDeleteEvent;
                        }

                        if (Progress != null)
                        {
                            Operation.UpdateProgress -= Progress;
                        }
                    }
                }

                return true;
            }
            catch (Exception ex) when (ex is not COMException and not OperationCanceledException)
            {
                LogTracer.Log(ex, $"An exception was threw in {nameof(Delete)}");
            }

            return false;
        }

        public static bool Copy(IEnumerable<string> SourcePath,
                                string DestinationPath,
                                CollisionOptions Option,
                                ProgressChangedEventHandler Progress = null,
                                EventHandler<ShellFileOperations.ShellFileOpEventArgs> PreCopyEvent = null,
                                EventHandler<ShellFileOperations.ShellFileOpEventArgs> PostCopyEvent = null)
        {
            try
            {
                if (!Directory.Exists(DestinationPath))
                {
                    Directory.CreateDirectory(DestinationPath);
                }

                ShellFileOperations.OperationFlags Flags = ShellFileOperations.OperationFlags.AddUndoRecord
                                                           | ShellFileOperations.OperationFlags.NoConfirmMkDir
                                                           | ShellFileOperations.OperationFlags.Silent
                                                           | ShellFileOperations.OperationFlags.RequireElevation
                                                           | ShellFileOperations.OperationFlags.NoErrorUI
                                                           | ShellFileOperations.OperationFlags.EarlyFailure
                                                           | ShellFileOperations.OperationFlags.ShowElevationPrompt;

                switch (Option)
                {
                    case CollisionOptions.RenameOnCollision:
                        {
                            Flags |= ShellFileOperations.OperationFlags.RenameOnCollision;
                            Flags |= ShellFileOperations.OperationFlags.PreserveFileExtensions;
                            break;
                        }
                    case CollisionOptions.OverrideOnCollision:
                        {
                            Flags |= ShellFileOperations.OperationFlags.NoConfirmation;
                            break;
                        }
                }

                using (ShellFileOperations Operation = new ShellFileOperations
                {
                    Options = Flags
                })
                {
                    if (Progress != null)
                    {
                        Operation.UpdateProgress += Progress;
                    }

                    if (PreCopyEvent != null)
                    {
                        Operation.PreCopyItem += PreCopyEvent;
                    }

                    if (PostCopyEvent != null)
                    {
                        Operation.PostCopyItem += PostCopyEvent;
                    }

                    try
                    {
                        foreach (string Source in SourcePath)
                        {
                            using (ShellItem SourceItem = new ShellItem(Source))
                            using (ShellFolder DestItem = new ShellFolder(DestinationPath))
                            {
                                Operation.QueueCopyOperation(SourceItem, DestItem);
                            }
                        }

                        Operation.PerformOperations();

                        if (Operation.AnyOperationsAborted)
                        {
                            throw new OperationCanceledException();
                        }
                    }
                    finally
                    {
                        if (PreCopyEvent != null)
                        {
                            Operation.PreCopyItem -= PreCopyEvent;
                        }

                        if (PostCopyEvent != null)
                        {
                            Operation.PostCopyItem -= PostCopyEvent;
                        }

                        if (Progress != null)
                        {
                            Operation.UpdateProgress -= Progress;
                        }
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"An exception was threw in {nameof(Copy)}");
            }

            return false;
        }

        public static bool Move(IDictionary<string, string> SourcePath,
                                string DestinationPath,
                                CollisionOptions Option,
                                ProgressChangedEventHandler Progress = null,
                                EventHandler<ShellFileOperations.ShellFileOpEventArgs> PreMoveEvent = null,
                                EventHandler<ShellFileOperations.ShellFileOpEventArgs> PostMoveEvent = null)
        {
            try
            {
                if (!Directory.Exists(DestinationPath))
                {
                    Directory.CreateDirectory(DestinationPath);
                }

                ShellFileOperations.OperationFlags Flags = ShellFileOperations.OperationFlags.AddUndoRecord
                                                           | ShellFileOperations.OperationFlags.NoConfirmMkDir
                                                           | ShellFileOperations.OperationFlags.Silent
                                                           | ShellFileOperations.OperationFlags.RequireElevation
                                                           | ShellFileOperations.OperationFlags.NoErrorUI
                                                           | ShellFileOperations.OperationFlags.EarlyFailure
                                                           | ShellFileOperations.OperationFlags.ShowElevationPrompt;

                switch (Option)
                {
                    case CollisionOptions.RenameOnCollision:
                        {
                            Flags |= ShellFileOperations.OperationFlags.RenameOnCollision;
                            Flags |= ShellFileOperations.OperationFlags.PreserveFileExtensions;
                            break;
                        }
                    case CollisionOptions.OverrideOnCollision:
                        {
                            Flags |= ShellFileOperations.OperationFlags.NoConfirmation;
                            break;
                        }
                }

                using (ShellFileOperations Operation = new ShellFileOperations
                {
                    Options = Flags
                })
                {
                    if (Progress != null)
                    {
                        Operation.UpdateProgress += Progress;
                    }

                    if (PreMoveEvent != null)
                    {
                        Operation.PreMoveItem += PreMoveEvent;
                    }

                    if (PostMoveEvent != null)
                    {
                        Operation.PostMoveItem += PostMoveEvent;
                    }

                    try
                    {
                        foreach (KeyValuePair<string, string> Source in SourcePath)
                        {
                            using (ShellItem SourceItem = new ShellItem(Source.Key))
                            using (ShellFolder DestItem = new ShellFolder(DestinationPath))
                            {
                                Operation.QueueMoveOperation(SourceItem, DestItem, string.IsNullOrEmpty(Source.Value) ? null : Source.Value);
                            }
                        }

                        Operation.PerformOperations();

                        if (Operation.AnyOperationsAborted)
                        {
                            throw new OperationCanceledException();
                        }
                    }
                    finally
                    {
                        if (PreMoveEvent != null)
                        {
                            Operation.PreMoveItem -= PreMoveEvent;
                        }

                        if (PostMoveEvent != null)
                        {
                            Operation.PostMoveItem -= PostMoveEvent;
                        }

                        if (Progress != null)
                        {
                            Operation.UpdateProgress -= Progress;
                        }
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"An exception was threw in {nameof(Move)}");
            }

            return false;
        }
    }
}
