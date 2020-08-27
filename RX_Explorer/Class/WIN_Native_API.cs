using Microsoft.Win32.SafeHandles;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Threading.Tasks;
using Windows.Storage;
using FileAttributes = System.IO.FileAttributes;

namespace RX_Explorer.Class
{
    public static class WIN_Native_API
    {
        private enum FINDEX_INFO_LEVELS
        {
            FindExInfoStandard = 0,
            FindExInfoBasic = 1
        }

        private enum FINDEX_SEARCH_OPS
        {
            FindExSearchNameMatch = 0,
            FindExSearchLimitToDirectories = 1,
            FindExSearchLimitToDevices = 2
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        public struct WIN32_FIND_DATA
        {
            public uint dwFileAttributes;
            public FILETIME ftCreationTime;
            public FILETIME ftLastAccessTime;
            public FILETIME ftLastWriteTime;
            public uint nFileSizeHigh;
            public uint nFileSizeLow;
            public uint dwReserved0;
            public uint dwReserved1;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string cFileName;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 14)]
            public string cAlternateFileName;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct SYSTEMTIME
        {
            [MarshalAs(UnmanagedType.U2)] public short Year;
            [MarshalAs(UnmanagedType.U2)] public short Month;
            [MarshalAs(UnmanagedType.U2)] public short DayOfWeek;
            [MarshalAs(UnmanagedType.U2)] public short Day;
            [MarshalAs(UnmanagedType.U2)] public short Hour;
            [MarshalAs(UnmanagedType.U2)] public short Minute;
            [MarshalAs(UnmanagedType.U2)] public short Second;
            [MarshalAs(UnmanagedType.U2)] public short Milliseconds;

            public SYSTEMTIME(DateTime dt)
            {
                dt = dt.ToUniversalTime();  // SetSystemTime expects the SYSTEMTIME in UTC
                Year = (short)dt.Year;
                Month = (short)dt.Month;
                DayOfWeek = (short)dt.DayOfWeek;
                Day = (short)dt.Day;
                Hour = (short)dt.Hour;
                Minute = (short)dt.Minute;
                Second = (short)dt.Second;
                Milliseconds = (short)dt.Millisecond;
            }
        }

        [DllImport("api-ms-win-core-file-fromapp-l1-1-0.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr FindFirstFileExFromApp(string lpFileName,
                                                           FINDEX_INFO_LEVELS fInfoLevelId,
                                                           out WIN32_FIND_DATA lpFindFileData,
                                                           FINDEX_SEARCH_OPS fSearchOp,
                                                           IntPtr lpSearchFilter,
                                                           int dwAdditionalFlags);

        private const int FIND_FIRST_EX_CASE_SENSITIVE = 1;
        private const int FIND_FIRST_EX_LARGE_FETCH = 2;
        private const int FIND_FIRST_EX_ON_DISK_ENTRIES_ONLY = 4;

        [DllImport("api-ms-win-core-file-l1-1-0.dll", CharSet = CharSet.Unicode)]
        private static extern bool FindNextFile(IntPtr hFindFile, out WIN32_FIND_DATA lpFindFileData);

        [DllImport("api-ms-win-core-file-l1-1-0.dll")]
        private static extern bool FindClose(IntPtr hFindFile);

        [DllImport("api-ms-win-core-timezone-l1-1-0.dll", SetLastError = true)]
        private static extern bool FileTimeToSystemTime(ref FILETIME lpFileTime, out SYSTEMTIME lpSystemTime);

        [DllImport("api-ms-win-core-file-fromapp-l1-1-0.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr CreateFileFromApp(string lpFileName, uint dwDesiredAccess, uint dwShareMode, IntPtr SecurityAttributes, uint dwCreationDisposition, uint dwFlagsAndAttributes, IntPtr hTemplateFile);

        [DllImport("api-ms-win-core-handle-l1-1-0.dll")]
        private static extern bool CloseHandle(IntPtr hObject);

        [DllImport("api-ms-win-core-file-l2-1-0.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool ReadDirectoryChangesW(IntPtr hDirectory, IntPtr lpBuffer, uint nBufferLength, bool bWatchSubtree, uint dwNotifyFilter, out uint lpBytesReturned, IntPtr lpOverlapped, IntPtr lpCompletionRoutine);

        [DllImport("api-ms-win-core-io-l1-1-1.dll")]
        private static extern bool CancelIoEx(IntPtr hFile, IntPtr lpOverlapped);

        const uint GENERIC_READ = 0x80000000;
        const uint GENERIC_WRITE = 0x40000000;
        const uint FILE_LIST_DIRECTORY = 0x1;
        const uint FILE_NO_SHARE = 0x0;
        const uint FILE_SHARE_READ = 0x1;
        const uint FILE_SHARE_WRITE = 0x2;
        const uint FILE_SHARE_DELETE = 0x4;
        const uint OPEN_EXISTING = 3;
        const uint FILE_FLAG_BACKUP_SEMANTICS = 0x2000000;
        const uint FILE_NOTIFY_CHANGE_FILE_NAME = 0x1;
        const uint FILE_NOTIFY_CHANGE_DIR_NAME = 0x2;
        const uint FILE_NOTIFY_CHANGE_LAST_WRITE = 0x10;

        private enum StateChangeType
        {
            Unknown_Action = 0,
            Added_Action = 1,
            Removed_Action = 2,
            Modified_Action = 3,
            Rename_Action_OldName = 4,
            Rename_Action_NewName = 5
        }

        public static void StopDirectoryWatcher(ref IntPtr hDir)
        {
            CancelIoEx(hDir, IntPtr.Zero);
            CloseHandle(hDir);
            hDir = IntPtr.Zero;
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2008:不要在未传递 TaskScheduler 的情况下创建任务", Justification = "<挂起>")]
        public static IntPtr CreateDirectoryWatcher(string Path, Action<string> Added = null, Action<string> Removed = null, Action<string, string> Renamed = null, Action<string> Modified = null)
        {
            try
            {
                IntPtr hDir = CreateFileFromApp(Path, FILE_LIST_DIRECTORY, FILE_SHARE_READ | FILE_SHARE_WRITE | FILE_SHARE_DELETE, IntPtr.Zero, OPEN_EXISTING, FILE_FLAG_BACKUP_SEMANTICS, IntPtr.Zero);

                if (hDir == IntPtr.Zero || hDir.ToInt64() == -1)
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }

                Task.Factory.StartNew((Arguement) =>
                {
                    ValueTuple<IntPtr, Action<string>, Action<string>, Action<string, string>, Action<string>> Package = (ValueTuple<IntPtr, Action<string>, Action<string>, Action<string, string>, Action<string>>)Arguement;

                    while (true)
                    {
                        IntPtr BufferPointer = Marshal.AllocHGlobal(4096);

                        try
                        {
                            if (ReadDirectoryChangesW(Package.Item1, BufferPointer, 4096, false, FILE_NOTIFY_CHANGE_FILE_NAME | FILE_NOTIFY_CHANGE_DIR_NAME | FILE_NOTIFY_CHANGE_LAST_WRITE, out _, IntPtr.Zero, IntPtr.Zero))
                            {
                                IntPtr CurrentPointer = BufferPointer;
                                int Offset = 0;
                                string OldPath = null;

                                do
                                {
                                    CurrentPointer = (IntPtr)(Offset + CurrentPointer.ToInt64());

                                    // Read file length (in bytes) at offset 8
                                    int FileNameLength = Marshal.ReadInt32(CurrentPointer, 8);
                                    // Read file name (fileLen/2 characters) from offset 12
                                    string FileName = Marshal.PtrToStringUni((IntPtr)(12 + CurrentPointer.ToInt64()), FileNameLength / 2);
                                    // Read action at offset 4
                                    int ActionIndex = Marshal.ReadInt32(CurrentPointer, 4);

                                    if (ActionIndex < 1 || ActionIndex > 5)
                                    {
                                        ActionIndex = 0;
                                    }

                                    switch ((StateChangeType)ActionIndex)
                                    {
                                        case StateChangeType.Unknown_Action:
                                            {
                                                break;
                                            }
                                        case StateChangeType.Added_Action:
                                            {
                                                Package.Item2?.Invoke(System.IO.Path.Combine(Path, FileName));
                                                break;
                                            }
                                        case StateChangeType.Removed_Action:
                                            {
                                                Package.Item3?.Invoke(System.IO.Path.Combine(Path, FileName));
                                                break;
                                            }
                                        case StateChangeType.Modified_Action:
                                            {
                                                Package.Item5?.Invoke(System.IO.Path.Combine(Path, FileName));
                                                break;
                                            }
                                        case StateChangeType.Rename_Action_OldName:
                                            {
                                                OldPath = System.IO.Path.Combine(Path, FileName);
                                                break;
                                            }
                                        case StateChangeType.Rename_Action_NewName:
                                            {
                                                Package.Item4?.Invoke(OldPath, System.IO.Path.Combine(Path, FileName));
                                                break;
                                            }
                                    }

                                    // Read NextEntryOffset at offset 0 and move pointer to next structure if needed
                                    Offset = Marshal.ReadInt32(CurrentPointer);
                                }
                                while (Offset != 0);
                            }
                            else
                            {
                                break;
                            }
                        }
                        catch
                        {

                        }
                        finally
                        {
                            if (BufferPointer != IntPtr.Zero)
                            {
                                Marshal.FreeHGlobal(BufferPointer);
                            }
                        }
                    }
                }, (hDir, Added, Removed, Renamed, Modified));

                return hDir;
            }
            catch
            {
                return IntPtr.Zero;
            }
        }

        public static bool CheckContainsAnyItem(string Path, ItemFilters Filter)
        {
            if (string.IsNullOrWhiteSpace(Path))
            {
                throw new ArgumentException("Argument could not be empty", nameof(Path));
            }

            IntPtr Ptr = FindFirstFileExFromApp(System.IO.Path.Combine(Path, "*"), FINDEX_INFO_LEVELS.FindExInfoBasic, out WIN32_FIND_DATA Data, FINDEX_SEARCH_OPS.FindExSearchNameMatch, IntPtr.Zero, FIND_FIRST_EX_LARGE_FETCH);

            try
            {
                if (Ptr.ToInt64() != -1)
                {
                    do
                    {
                        FileAttributes Attribute = (FileAttributes)Data.dwFileAttributes;

                        if (!Attribute.HasFlag(FileAttributes.System))
                        {
                            if (Attribute.HasFlag(FileAttributes.Directory) && Filter.HasFlag(ItemFilters.Folder))
                            {
                                if (Data.cFileName != "." && Data.cFileName != "..")
                                {
                                    return true;
                                }
                            }
                            else if (Filter.HasFlag(ItemFilters.File) && !Data.cFileName.EndsWith(".url"))
                            {
                                return true;
                            }
                        }
                    }
                    while (FindNextFile(Ptr, out Data));

                    return false;
                }
                else
                {
                    Debug.WriteLine(new Win32Exception(Marshal.GetLastWin32Error()).Message);
                    return false;
                }
            }
            catch
            {
                return false;
            }
            finally
            {
                FindClose(Ptr);
            }
        }

        public static bool CheckExist(string Path)
        {
            if (string.IsNullOrWhiteSpace(Path))
            {
                throw new ArgumentException("Argument could not be empty", nameof(Path));
            }

            IntPtr Ptr = FindFirstFileExFromApp(Path, FINDEX_INFO_LEVELS.FindExInfoBasic, out _, FINDEX_SEARCH_OPS.FindExSearchNameMatch, IntPtr.Zero, FIND_FIRST_EX_LARGE_FETCH);

            try
            {
                if (Ptr.ToInt64() != -1)
                {
                    return true;
                }
                else
                {
                    Debug.WriteLine(new Win32Exception(Marshal.GetLastWin32Error()).Message);
                    return false;
                }
            }
            catch
            {
                return false;
            }
            finally
            {
                FindClose(Ptr);
            }
        }

        public static bool CheckIfHidden(string Path)
        {
            if (string.IsNullOrWhiteSpace(Path))
            {
                throw new ArgumentException("Argument could not be empty", nameof(Path));
            }

            IntPtr Ptr = FindFirstFileExFromApp(Path, FINDEX_INFO_LEVELS.FindExInfoBasic, out WIN32_FIND_DATA Data, FINDEX_SEARCH_OPS.FindExSearchNameMatch, IntPtr.Zero, FIND_FIRST_EX_LARGE_FETCH);

            try
            {
                if (Ptr.ToInt64() != -1)
                {
                    if (((FileAttributes)Data.dwFileAttributes).HasFlag(FileAttributes.Hidden))
                    {
                        return true;
                    }
                    else
                    {
                        return false;
                    }
                }
                else
                {
                    Debug.WriteLine(new Win32Exception(Marshal.GetLastWin32Error()).Message);
                    return false;
                }
            }
            catch
            {
                return false;
            }
            finally
            {
                FindClose(Ptr);
            }
        }

        public static ulong CalculateSize(string Path)
        {
            if (string.IsNullOrWhiteSpace(Path))
            {
                throw new ArgumentException("Argument could not be empty", nameof(Path));
            }

            IntPtr Ptr = FindFirstFileExFromApp(System.IO.Path.Combine(Path, "*"), FINDEX_INFO_LEVELS.FindExInfoBasic, out WIN32_FIND_DATA Data, FINDEX_SEARCH_OPS.FindExSearchNameMatch, IntPtr.Zero, FIND_FIRST_EX_LARGE_FETCH);

            try
            {
                if (Ptr.ToInt64() != -1)
                {
                    ulong TotalSize = 0;

                    do
                    {
                        if (((FileAttributes)Data.dwFileAttributes).HasFlag(FileAttributes.Directory))
                        {
                            if (Data.cFileName != "." && Data.cFileName != "..")
                            {
                                TotalSize += CalculateSize(System.IO.Path.Combine(Path, Data.cFileName));
                            }
                        }
                        else
                        {
                            TotalSize += ((ulong)Data.nFileSizeHigh << 32) + Data.nFileSizeLow;
                        }
                    }
                    while (FindNextFile(Ptr, out Data));

                    return TotalSize;
                }
                else
                {
                    Debug.WriteLine(new Win32Exception(Marshal.GetLastWin32Error()).Message);
                    return 0;
                }
            }
            catch
            {
                return 0;
            }
            finally
            {
                FindClose(Ptr);
            }
        }

        public static (uint, uint) CalculateFolderAndFileCount(string Path)
        {
            if (string.IsNullOrWhiteSpace(Path))
            {
                throw new ArgumentException("Argument could not be empty", nameof(Path));
            }

            IntPtr Ptr = FindFirstFileExFromApp(System.IO.Path.Combine(Path, "*"), FINDEX_INFO_LEVELS.FindExInfoBasic, out WIN32_FIND_DATA Data, FINDEX_SEARCH_OPS.FindExSearchNameMatch, IntPtr.Zero, FIND_FIRST_EX_LARGE_FETCH);

            try
            {
                if (Ptr.ToInt64() != -1)
                {
                    uint FolderCount = 0;
                    uint FileCount = 0;

                    do
                    {
                        if (((FileAttributes)Data.dwFileAttributes).HasFlag(FileAttributes.Directory))
                        {
                            if (Data.cFileName != "." && Data.cFileName != "..")
                            {
                                (uint SubFolderCount, uint SubFileCount) = CalculateFolderAndFileCount(System.IO.Path.Combine(Path, Data.cFileName));
                                FolderCount += ++SubFolderCount;
                                FileCount += SubFileCount;
                            }
                        }
                        else
                        {
                            FileCount++;
                        }
                    }
                    while (FindNextFile(Ptr, out Data));

                    return (FolderCount, FileCount);
                }
                else
                {
                    Debug.WriteLine(new Win32Exception(Marshal.GetLastWin32Error()).Message);
                    return (0, 0);
                }
            }
            catch
            {
                return (0, 0);
            }
            finally
            {
                FindClose(Ptr);
            }
        }

        public static List<FileSystemStorageItemBase> GetStorageItems(string Path, bool IncludeHiddenItem, ItemFilters Filter)
        {
            if (string.IsNullOrWhiteSpace(Path))
            {
                throw new ArgumentException("Argument could not be empty", nameof(Path));
            }

            IntPtr Ptr = FindFirstFileExFromApp(System.IO.Path.Combine(Path, "*"), FINDEX_INFO_LEVELS.FindExInfoBasic, out WIN32_FIND_DATA Data, FINDEX_SEARCH_OPS.FindExSearchNameMatch, IntPtr.Zero, FIND_FIRST_EX_LARGE_FETCH);

            try
            {
                if (Ptr.ToInt64() != -1)
                {
                    List<FileSystemStorageItemBase> Result = new List<FileSystemStorageItemBase>();

                    do
                    {
                        FileAttributes Attribute = (FileAttributes)Data.dwFileAttributes;

                        if (!Attribute.HasFlag(FileAttributes.ReparsePoint) && (IncludeHiddenItem || !Attribute.HasFlag(FileAttributes.Hidden)))
                        {
                            if (Attribute.HasFlag(FileAttributes.Directory) && Filter.HasFlag(ItemFilters.Folder))
                            {
                                if (Data.cFileName != "." && Data.cFileName != "..")
                                {
                                    FileTimeToSystemTime(ref Data.ftLastWriteTime, out SYSTEMTIME ModTime);
                                    DateTime ModifiedTime = new DateTime(ModTime.Year, ModTime.Month, ModTime.Day, ModTime.Hour, ModTime.Minute, ModTime.Second, ModTime.Milliseconds, DateTimeKind.Utc);

                                    if (Attribute.HasFlag(FileAttributes.Hidden))
                                    {
                                        Result.Add(new HiddenStorageItem(Data, StorageItemTypes.Folder, System.IO.Path.Combine(Path, Data.cFileName), ModifiedTime.ToLocalTime()));
                                    }
                                    else
                                    {
                                        Result.Add(new FileSystemStorageItemBase(Data, StorageItemTypes.Folder, System.IO.Path.Combine(Path, Data.cFileName), ModifiedTime.ToLocalTime()));
                                    }
                                }
                            }
                            else if (Filter.HasFlag(ItemFilters.File))
                            {
                                FileTimeToSystemTime(ref Data.ftLastWriteTime, out SYSTEMTIME ModTime);
                                DateTime ModifiedTime = new DateTime(ModTime.Year, ModTime.Month, ModTime.Day, ModTime.Hour, ModTime.Minute, ModTime.Second, ModTime.Milliseconds, DateTimeKind.Utc);

                                if (Attribute.HasFlag(FileAttributes.Hidden))
                                {
                                    Result.Add(new HiddenStorageItem(Data, StorageItemTypes.File, System.IO.Path.Combine(Path, Data.cFileName), ModifiedTime.ToLocalTime()));
                                }
                                else
                                {
                                    if (!Data.cFileName.EndsWith(".url"))
                                    {
                                        if (Data.cFileName.EndsWith(".lnk"))
                                        {
                                            Result.Add(new HyperlinkStorageItem(Data, System.IO.Path.Combine(Path, Data.cFileName), ModifiedTime.ToLocalTime()));
                                        }
                                        else
                                        {
                                            Result.Add(new FileSystemStorageItemBase(Data, StorageItemTypes.File, System.IO.Path.Combine(Path, Data.cFileName), ModifiedTime.ToLocalTime()));
                                        }
                                    }
                                }
                            }
                        }
                    }
                    while (FindNextFile(Ptr, out Data));

                    return Result;
                }
                else
                {
                    Debug.WriteLine(new Win32Exception(Marshal.GetLastWin32Error()).Message);
                    return new List<FileSystemStorageItemBase>();
                }
            }
            catch
            {
                return new List<FileSystemStorageItemBase>();
            }
            finally
            {
                FindClose(Ptr);
            }
        }

        public static List<FileSystemStorageItemBase> GetStorageItems(params string[] PathArray)
        {
            if (PathArray.Length == 0 || PathArray.Any((Item) => string.IsNullOrWhiteSpace(Item)))
            {
                throw new ArgumentException("Argument could not be empty", nameof(PathArray));
            }

            try
            {
                List<FileSystemStorageItemBase> Result = new List<FileSystemStorageItemBase>(PathArray.Length);

                foreach (string Path in PathArray)
                {
                    IntPtr Ptr = FindFirstFileExFromApp(Path, FINDEX_INFO_LEVELS.FindExInfoBasic, out WIN32_FIND_DATA Data, FINDEX_SEARCH_OPS.FindExSearchNameMatch, IntPtr.Zero, FIND_FIRST_EX_LARGE_FETCH);

                    try
                    {
                        if (Ptr.ToInt64() != -1)
                        {
                            FileAttributes Attribute = (FileAttributes)Data.dwFileAttributes;

                            if (!Attribute.HasFlag(FileAttributes.ReparsePoint))
                            {
                                if (Attribute.HasFlag(FileAttributes.Directory))
                                {
                                    if (Data.cFileName != "." && Data.cFileName != "..")
                                    {
                                        FileTimeToSystemTime(ref Data.ftLastWriteTime, out SYSTEMTIME ModTime);
                                        DateTime ModifiedTime = new DateTime(ModTime.Year, ModTime.Month, ModTime.Day, ModTime.Hour, ModTime.Minute, ModTime.Second, ModTime.Milliseconds, DateTimeKind.Utc);

                                        if (Attribute.HasFlag(FileAttributes.Hidden))
                                        {
                                            Result.Add(new HiddenStorageItem(Data, StorageItemTypes.Folder, Path, ModifiedTime.ToLocalTime()));
                                        }
                                        else
                                        {
                                            Result.Add(new FileSystemStorageItemBase(Data, StorageItemTypes.Folder, Path, ModifiedTime.ToLocalTime()));
                                        }
                                    }
                                }
                                else
                                {
                                    FileTimeToSystemTime(ref Data.ftLastWriteTime, out SYSTEMTIME ModTime);
                                    DateTime ModifiedTime = new DateTime(ModTime.Year, ModTime.Month, ModTime.Day, ModTime.Hour, ModTime.Minute, ModTime.Second, ModTime.Milliseconds, DateTimeKind.Utc);

                                    if (Attribute.HasFlag(FileAttributes.Hidden))
                                    {
                                        Result.Add(new HiddenStorageItem(Data, StorageItemTypes.File, Path, ModifiedTime.ToLocalTime()));
                                    }
                                    else
                                    {
                                        if (!Data.cFileName.EndsWith(".url"))
                                        {
                                            if (Data.cFileName.EndsWith(".lnk"))
                                            {
                                                Result.Add(new HyperlinkStorageItem(Data, Path, ModifiedTime.ToLocalTime()));
                                            }
                                            else
                                            {
                                                Result.Add(new FileSystemStorageItemBase(Data, StorageItemTypes.File, Path, ModifiedTime.ToLocalTime()));
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        else
                        {
                            Debug.WriteLine(new Win32Exception(Marshal.GetLastWin32Error()).Message);
                        }
                    }
                    finally
                    {
                        FindClose(Ptr);
                    }
                }

                return Result;
            }
            catch
            {
                return new List<FileSystemStorageItemBase>();
            }
        }

        public static List<FileSystemStorageItemBase> GetStorageItems(StorageFolder Folder, bool IncludeHiddenItem, ItemFilters Filter)
        {
            if (Folder == null)
            {
                throw new ArgumentNullException(nameof(Folder), "Argument could not be null");
            }

            IntPtr Ptr = FindFirstFileExFromApp(System.IO.Path.Combine(Folder.Path, "*"), FINDEX_INFO_LEVELS.FindExInfoBasic, out WIN32_FIND_DATA Data, FINDEX_SEARCH_OPS.FindExSearchNameMatch, IntPtr.Zero, FIND_FIRST_EX_LARGE_FETCH);

            try
            {
                if (Ptr.ToInt64() != -1)
                {
                    List<FileSystemStorageItemBase> Result = new List<FileSystemStorageItemBase>();

                    do
                    {
                        FileAttributes Attribute = (FileAttributes)Data.dwFileAttributes;

                        if (!Attribute.HasFlag(FileAttributes.ReparsePoint) && (IncludeHiddenItem || !Attribute.HasFlag(FileAttributes.Hidden)))
                        {
                            if (Attribute.HasFlag(FileAttributes.Directory) && Filter.HasFlag(ItemFilters.Folder))
                            {
                                if (Data.cFileName != "." && Data.cFileName != "..")
                                {
                                    FileTimeToSystemTime(ref Data.ftLastWriteTime, out SYSTEMTIME ModTime);
                                    DateTime ModifiedTime = new DateTime(ModTime.Year, ModTime.Month, ModTime.Day, ModTime.Hour, ModTime.Minute, ModTime.Second, ModTime.Milliseconds, DateTimeKind.Utc);

                                    if (Attribute.HasFlag(FileAttributes.Hidden))
                                    {
                                        Result.Add(new HiddenStorageItem(Data, StorageItemTypes.Folder, System.IO.Path.Combine(Folder.Path, Data.cFileName), ModifiedTime.ToLocalTime()));
                                    }
                                    else
                                    {
                                        Result.Add(new FileSystemStorageItemBase(Data, StorageItemTypes.Folder, System.IO.Path.Combine(Folder.Path, Data.cFileName), ModifiedTime.ToLocalTime()));
                                    }
                                }
                            }
                            else if (Filter.HasFlag(ItemFilters.File))
                            {
                                FileTimeToSystemTime(ref Data.ftLastWriteTime, out SYSTEMTIME ModTime);
                                DateTime ModifiedTime = new DateTime(ModTime.Year, ModTime.Month, ModTime.Day, ModTime.Hour, ModTime.Minute, ModTime.Second, ModTime.Milliseconds, DateTimeKind.Utc);

                                if (Attribute.HasFlag(FileAttributes.Hidden))
                                {
                                    Result.Add(new HiddenStorageItem(Data, StorageItemTypes.File, System.IO.Path.Combine(Folder.Path, Data.cFileName), ModifiedTime.ToLocalTime()));
                                }
                                else
                                {
                                    if (!Data.cFileName.EndsWith(".url"))
                                    {
                                        if (Data.cFileName.EndsWith(".lnk"))
                                        {
                                            Result.Add(new HyperlinkStorageItem(Data, System.IO.Path.Combine(Folder.Path, Data.cFileName), ModifiedTime.ToLocalTime()));
                                        }
                                        else
                                        {
                                            Result.Add(new FileSystemStorageItemBase(Data, StorageItemTypes.File, System.IO.Path.Combine(Folder.Path, Data.cFileName), ModifiedTime.ToLocalTime()));
                                        }
                                    }
                                }
                            }
                        }
                    }
                    while (FindNextFile(Ptr, out Data));

                    return Result;
                }
                else
                {
                    Debug.WriteLine(new Win32Exception(Marshal.GetLastWin32Error()).Message);
                    return new List<FileSystemStorageItemBase>();
                }
            }
            catch
            {
                return new List<FileSystemStorageItemBase>();
            }
            finally
            {
                FindClose(Ptr);
            }
        }

        public static List<string> GetStorageItemsPath(string Path, bool IncludeHiddenItem, ItemFilters Filter)
        {
            if (string.IsNullOrWhiteSpace(Path))
            {
                throw new ArgumentNullException(nameof(Path), "Argument could not be null");
            }

            IntPtr Ptr = FindFirstFileExFromApp(System.IO.Path.Combine(Path, "*"), FINDEX_INFO_LEVELS.FindExInfoBasic, out WIN32_FIND_DATA Data, FINDEX_SEARCH_OPS.FindExSearchNameMatch, IntPtr.Zero, FIND_FIRST_EX_LARGE_FETCH);

            try
            {
                if (Ptr.ToInt64() != -1)
                {
                    List<string> Result = new List<string>();

                    do
                    {
                        FileAttributes Attribute = (FileAttributes)Data.dwFileAttributes;

                        if (!Attribute.HasFlag(FileAttributes.ReparsePoint) && (IncludeHiddenItem || !Attribute.HasFlag(FileAttributes.Hidden)))
                        {
                            if (((FileAttributes)Data.dwFileAttributes).HasFlag(FileAttributes.Directory) && Filter.HasFlag(ItemFilters.Folder))
                            {
                                if (Data.cFileName != "." && Data.cFileName != "..")
                                {
                                    Result.Add(System.IO.Path.Combine(Path, Data.cFileName));
                                }
                            }
                            else if (Filter.HasFlag(ItemFilters.File))
                            {
                                Result.Add(System.IO.Path.Combine(Path, Data.cFileName));
                            }
                        }
                    }
                    while (FindNextFile(Ptr, out Data));

                    return Result;
                }
                else
                {
                    Debug.WriteLine(new Win32Exception(Marshal.GetLastWin32Error()).Message);
                    return new List<string>();
                }
            }
            catch
            {
                return new List<string>();
            }
            finally
            {
                FindClose(Ptr);
            }
        }

        public static SafePipeHandle GetHandleFromNamedPipe(string PipeName)
        {
            IntPtr Handle = CreateFileFromApp(@$"\\.\pipe\{PipeName}", GENERIC_READ | GENERIC_WRITE, FILE_NO_SHARE, IntPtr.Zero, OPEN_EXISTING, 0, IntPtr.Zero);

            SafePipeHandle SPipeHandle = new SafePipeHandle(Handle, true);

            if (SPipeHandle.IsInvalid)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }
            else
            {
                return SPipeHandle;
            }
        }
    }
}
