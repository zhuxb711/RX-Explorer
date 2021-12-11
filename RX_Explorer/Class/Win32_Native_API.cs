using Microsoft.Win32.SafeHandles;
using ShareClassLibrary;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text.RegularExpressions;
using System.Threading;
using Windows.Storage;
using FileAttributes = System.IO.FileAttributes;

namespace RX_Explorer.Class
{
    public static class Win32_Native_API
    {
        [DllImport("api-ms-win-core-file-fromapp-l1-1-0.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr FindFirstFileExFromApp(string lpFileName,
                                                            FINDEX_INFO_LEVELS fInfoLevelId,
                                                            out WIN32_FIND_DATA lpFindFileData,
                                                            FINDEX_SEARCH_OPS fSearchOp,
                                                            IntPtr lpSearchFilter,
                                                            FINDEX_ADDITIONAL_FLAGS dwAdditionalFlags);

        [DllImport("api-ms-win-core-file-l1-1-0.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool FindNextFile(IntPtr hFindFile, out WIN32_FIND_DATA lpFindFileData);

        [DllImport("api-ms-win-core-file-l1-1-0.dll", SetLastError = true)]
        private static extern bool FindClose(IntPtr hFindFile);

        [DllImport("api-ms-win-core-timezone-l1-1-0.dll", SetLastError = true)]
        public static extern bool FileTimeToSystemTime(ref FILETIME lpFileTime, out SYSTEMTIME lpSystemTime);

        [DllImport("api-ms-win-core-file-fromapp-l1-1-0.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern SafeFileHandle CreateFileFromApp(string lpFileName,
                                                               FILE_ACCESS dwDesiredAccess,
                                                               FILE_SHARE dwShareMode,
                                                               IntPtr SecurityAttributes,
                                                               CREATE_OPTION dwCreationDisposition,
                                                               FILE_ATTRIBUTE_FLAG dwFlagsAndAttributes,
                                                               IntPtr hTemplateFile);

        [DllImport("api-ms-win-core-handle-l1-1-0.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);

        [DllImport("api-ms-win-core-io-l1-1-1.dll", SetLastError = true)]
        private static extern bool CancelIoEx(IntPtr hFile, IntPtr lpOverlapped);

        [DllImport("api-ms-win-core-file-fromapp-l1-1-0.dll", EntryPoint = "CreateDirectoryFromAppW", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool CreateDirectoryFromApp(string lpPathName, IntPtr lpSecurityAttributes);

        [DllImport("api-ms-win-core-file-fromapp-l1-1-0.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool DeleteFileFromApp(string lpPathName);

        [DllImport("api-ms-win-core-file-fromapp-l1-1-0.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool RemoveDirectoryFromApp(string lpPathName);

        [DllImport("api-ms-win-core-file-l2-1-0.dll", EntryPoint = "ReadDirectoryChangesW", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern bool ReadDirectoryChanges(IntPtr hDirectory,
                                                       IntPtr lpBuffer,
                                                       uint nBufferLength,
                                                       bool bWatchSubtree,
                                                       FILE_NOTIFY_CHANGE dwNotifyFilter,
                                                       out uint lpBytesReturned,
                                                       IntPtr lpOverlapped,
                                                       IntPtr lpCompletionRoutine);

        [DllImport("api-ms-win-core-file-l1-1-0.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool GetFileSizeEx(IntPtr hFile, out long lpFileSize);

        [DllImport("api-ms-win-core-file-l2-1-0.dlll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool GetFileInformationByHandleEx(IntPtr hFile,
                                                                FILE_INFO_BY_HANDLE_CLASS FileInformationClass,
                                                                IntPtr lpFileInformation,
                                                                uint dwBufferSize);

        [Flags]
        private enum ACCESS_MASK : uint
        {
            Delete = 0x00010000,
            Read_Control = 0x00020000,
            Write_DAC = 0x00040000,
            Write_Owner = 0x00080000,
            Synchronize = 0x00100000,
            Standard_Rights_Required = 0x000F0000,
            Standard_Rights_Read = 0x00020000,
            Standard_Rights_Write = 0x00020000,
            Standard_Rights_Execute = 0x00020000,
            Standard_Rights_All = 0x001F0000,
            Specific_Rights_All = 0x0000FFFF,
            Access_System_Security = 0x01000000,
            Maximum_Allowed = 0x02000000,
            Generic_Read = 0x80000000,
            Generic_Write = 0x40000000,
            Generic_Execute = 0x20000000,
            Generic_All = 0x10000000
        }

        [Flags]
        private enum FILE_ACCESS : uint
        {
            Generic_Read = ACCESS_MASK.Generic_Read,
            Generic_Write = ACCESS_MASK.Generic_Write,
            Generic_Execute = ACCESS_MASK.Generic_Execute,
            Generic_All = ACCESS_MASK.Generic_All,
            File_Read_Data = 0x0001,
            File_List_Directory = 0x0001,
            File_Write_DATA = 0x0002,
            File_Add_File = 0x0002,
            File_Append_Data = 0x0004,
            File_Add_SubDirectory = 0x0004,
            File_Create_Pipe_Instance = 0x0004,
            File_Read_EA = 0x0008,
            File_Write_EA = 0x0010,
            File_Execute = 0x0020,
            File_Traverse = 0x0020,
            File_Delete_Child = 0x0040,
            File_Read_Attributes = 0x0080,
            File_Write_Attributes = 0x0100,
            Specific_Rights_All = 0x00FFFF,
            File_All_Access = ACCESS_MASK.Standard_Rights_Required | ACCESS_MASK.Synchronize | 0x1FF,
            File_Generic_Read = ACCESS_MASK.Standard_Rights_Read | File_Read_Data | File_Read_Attributes | File_Read_EA | ACCESS_MASK.Synchronize,
            File_Generic_Write = ACCESS_MASK.Standard_Rights_Write | File_Write_DATA | File_Write_Attributes | File_Write_EA | File_Append_Data | ACCESS_MASK.Synchronize,
            File_Generic_Execute = ACCESS_MASK.Standard_Rights_Execute | File_Read_Attributes | File_Execute | ACCESS_MASK.Synchronize,
        }

        [Flags]
        private enum FILE_SHARE : uint
        {
            None = 0,
            Read = 0x00000001,
            Write = 0x00000002,
            Delete = 0x00000004
        }

        [Flags]
        private enum FILE_ATTRIBUTE_FLAG : uint
        {
            File_Attribute_Archive = 0x20,
            File_Attribute_Encrypted = 0x4000,
            File_Attribute_HIDDEN = 0x2,
            File_Attribute_Normal = 0x80,
            File_Attribute_Offline = 0x1000,
            File_Attribute_ReadOnly = 0x1,
            File_Attribute_System = 0x4,
            File_Attribute_Temporary = 0x100,
            File_Flag_Backup_Semantics = 0x02000000,
            File_Flag_Delete_On_Close = 0x04000000,
            File_Flag_No_Buffering = 0x20000000,
            File_Flag_Open_No_Recall = 0x00100000,
            File_Flag_Open_Reparse_Point = 0x00200000,
            File_Flag_Overlapped = 0x40000000,
            File_Flag_Posix_Semantics = 0x01000000,
            File_Flag_Random_Access = 0x10000000,
            File_Flag_Session_Aware = 0x00800000,
            File_Flag_Sequential_Scan = 0x08000000,
            File_Flag_Write_Through = 0x80000000
        }

        private enum CREATE_OPTION : uint
        {
            Create_New = 1,
            Create_Always = 2,
            Open_Existing = 3,
            Open_Always = 4,
            Truncate_Existing = 5
        }

        private enum FINDEX_ADDITIONAL_FLAGS : uint
        {
            None = 0,
            Find_First_Ex_Case_Sensitive = 1,
            Find_First_Ex_Large_Fetch = 2,
            Find_First_Ex_On_Disk_Entries_Only = 4
        }

        private enum FINDEX_INFO_LEVELS : uint
        {
            FindExInfoStandard = 0,
            FindExInfoBasic = 1
        }

        private enum FINDEX_SEARCH_OPS : uint
        {
            FindExSearchNameMatch = 0,
            FindExSearchLimitToDirectories = 1,
            FindExSearchLimitToDevices = 2
        }

        [Flags]
        public enum FILE_NOTIFY_CHANGE : uint
        {
            File_Notify_Change_File_Name = 1,
            File_Notify_Change_Dir_Name = 2,
            File_Notify_Change_Attribute = 4,
            File_Notify_Change_Size = 8,
            File_Notify_Change_Last_Write = 16
        }

        public enum FILE_INFO_BY_HANDLE_CLASS : uint
        {
            FileBasicInfo,
            FileStandardInfo,
            FileNameInfo,
            FileRenameInfo,
            FileDispositionInfo,
            FileAllocationInfo,
            FileEndOfFileInfo,
            FileStreamInfo,
            FileCompressionInfo,
            FileAttributeTagInfo,
            FileIdBothDirectoryInfo,
            FileIdBothDirectoryRestartInfo,
            FileIoPriorityHintInfo,
            FileRemoteProtocolInfo,
            FileFullDirectoryInfo,
            FileFullDirectoryRestartInfo,
            FileStorageInfo,
            FileAlignmentInfo,
            FileIdInfo,
            FileIdExtdDirectoryInfo,
            FileIdExtdDirectoryRestartInfo,
            MaximumFileInfoByHandlesClass,
        }

        [StructLayout(LayoutKind.Sequential, Size = 40)]
        private struct FILE_BASIC_INFO
        {
            public FILETIME CreationTime;
            public FILETIME LastAccessTime;
            public FILETIME LastWriteTime;
            public FILETIME ChangeTime;
            public FileAttributes FileAttributes;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct FILE_STANDARD_INFO
        {
            public long AllocationSize;
            public long EndOfFile;
            public uint NumberOfLinks;
            [MarshalAs(UnmanagedType.U1)] public bool DeletePending;
            [MarshalAs(UnmanagedType.U1)] public bool Directory;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct WIN32_FIND_DATA : IEquatable<WIN32_FIND_DATA>
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

            public override bool Equals(object obj)
            {
                return cFileName.Equals(obj);
            }

            public override int GetHashCode()
            {
                return cFileName.GetHashCode();
            }

            public static bool operator ==(WIN32_FIND_DATA left, WIN32_FIND_DATA right)
            {
                return left.cFileName.Equals(right.cFileName);
            }

            public static bool operator !=(WIN32_FIND_DATA left, WIN32_FIND_DATA right)
            {
                return !(left.cFileName == right.cFileName);
            }

            public bool Equals(WIN32_FIND_DATA other)
            {
                return cFileName.Equals(other.cFileName);
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct SYSTEMTIME
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
                dt = dt.ToUniversalTime();
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

        public static FileStream CreateStreamFromFile(string Path, AccessMode AccessMode, OptimizeOption Option)
        {
            FILE_ATTRIBUTE_FLAG Flags = FILE_ATTRIBUTE_FLAG.File_Flag_Overlapped | Option switch
            {
                OptimizeOption.None => FILE_ATTRIBUTE_FLAG.File_Attribute_Normal,
                OptimizeOption.Optimize_Sequential => FILE_ATTRIBUTE_FLAG.File_Flag_Sequential_Scan,
                OptimizeOption.Optimize_RandomAccess => FILE_ATTRIBUTE_FLAG.File_Flag_Random_Access,
                _ => throw new NotSupportedException()
            };

            SafeFileHandle Handle = AccessMode switch
            {
                AccessMode.Read => CreateFileFromApp(Path, FILE_ACCESS.Generic_Read, FILE_SHARE.Read | FILE_SHARE.Write, IntPtr.Zero, CREATE_OPTION.Open_Existing, Flags, IntPtr.Zero),
                AccessMode.ReadWrite => CreateFileFromApp(Path, FILE_ACCESS.Generic_Read | FILE_ACCESS.Generic_Write, FILE_SHARE.Read, IntPtr.Zero, CREATE_OPTION.Open_Existing, Flags, IntPtr.Zero),
                AccessMode.Write => CreateFileFromApp(Path, FILE_ACCESS.Generic_Write, FILE_SHARE.Read, IntPtr.Zero, CREATE_OPTION.Open_Existing, Flags, IntPtr.Zero),
                AccessMode.Exclusive => CreateFileFromApp(Path, FILE_ACCESS.Generic_Read | FILE_ACCESS.Generic_Write, FILE_SHARE.None, IntPtr.Zero, CREATE_OPTION.Open_Existing, Flags, IntPtr.Zero),
                _ => throw new NotSupportedException()
            };

            if (Handle.IsInvalid)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            FileAccess Access = AccessMode switch
            {
                AccessMode.Read => FileAccess.Read,
                AccessMode.ReadWrite or AccessMode.Exclusive => FileAccess.ReadWrite,
                AccessMode.Write => FileAccess.Write,
                _ => throw new NotSupportedException()
            };

            return new FileStream(Handle, Access, 4096, true);
        }

        public static bool CreateDirectoryFromPath(string Path, CreateOption Option, out string NewFolderPath)
        {
            try
            {
                PathAnalysis Analysis = new PathAnalysis(Path, string.Empty);

                while (true)
                {
                    string NextPath = Analysis.NextFullPath();

                    if (Analysis.HasNextLevel)
                    {
                        if (!CheckExist(NextPath))
                        {
                            if (!CreateDirectoryFromApp(NextPath, IntPtr.Zero))
                            {
                                NewFolderPath = string.Empty;
                                LogTracer.Log(new Win32Exception(Marshal.GetLastWin32Error()), $"An exception was threw when create directory, Path: \"{Path}\"");
                                return false;
                            }
                        }
                    }
                    else
                    {
                        switch (Option)
                        {
                            case CreateOption.GenerateUniqueName:
                                {
                                    string UniquePath = GenerateUniquePath(NextPath, StorageItemTypes.Folder);

                                    if (CreateDirectoryFromApp(UniquePath, IntPtr.Zero))
                                    {
                                        NewFolderPath = UniquePath;
                                        return true;
                                    }
                                    else
                                    {
                                        NewFolderPath = string.Empty;
                                        LogTracer.Log(new Win32Exception(Marshal.GetLastWin32Error()), $"An exception was threw when create directory, Path: \"{Path}\"");
                                        return false;
                                    }
                                }
                            case CreateOption.OpenIfExist:
                                {
                                    if (CheckExist(NextPath))
                                    {
                                        NewFolderPath = NextPath;
                                        return true;
                                    }
                                    else
                                    {
                                        if (CreateDirectoryFromApp(NextPath, IntPtr.Zero))
                                        {
                                            NewFolderPath = NextPath;
                                            return true;
                                        }
                                        else
                                        {
                                            NewFolderPath = string.Empty;
                                            LogTracer.Log(new Win32Exception(Marshal.GetLastWin32Error()), $"An exception was threw when create directory, Path: \"{Path}\"");
                                            return false;
                                        }
                                    }
                                }
                            default:
                                {
                                    throw new ArgumentException("Argument is invalid", nameof(Option));
                                }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                NewFolderPath = string.Empty;
                LogTracer.Log(ex, $"An exception was threw when create directory, Path: \"{Path}\"");
                return false;
            }
        }

        public static bool CreateFileFromPath(string Path, CreateOption Option, out string NewPath)
        {
            try
            {
                switch (Option)
                {
                    case CreateOption.GenerateUniqueName:
                        {
                            if (CheckExist(Path))
                            {
                                string UniquePath = GenerateUniquePath(Path, StorageItemTypes.File);

                                using (SafeFileHandle Handle = CreateFileFromApp(UniquePath, FILE_ACCESS.Generic_Read, FILE_SHARE.Read | FILE_SHARE.Write | FILE_SHARE.Delete, IntPtr.Zero, CREATE_OPTION.Create_New, FILE_ATTRIBUTE_FLAG.File_Attribute_Normal, IntPtr.Zero))
                                {
                                    if (!Handle.IsInvalid)
                                    {
                                        NewPath = UniquePath;
                                        return true;
                                    }
                                }
                            }
                            else
                            {
                                using (SafeFileHandle Handle = CreateFileFromApp(Path, FILE_ACCESS.Generic_Read, FILE_SHARE.Read | FILE_SHARE.Write | FILE_SHARE.Delete, IntPtr.Zero, CREATE_OPTION.Create_New, FILE_ATTRIBUTE_FLAG.File_Attribute_Normal, IntPtr.Zero))
                                {
                                    if (!Handle.IsInvalid)
                                    {
                                        NewPath = Path;
                                        return true;
                                    }
                                }
                            }

                            throw new InvalidOperationException();
                        }
                    case CreateOption.OpenIfExist:
                        {
                            using (SafeFileHandle Handle = CreateFileFromApp(Path, FILE_ACCESS.Generic_Read, FILE_SHARE.Read | FILE_SHARE.Write | FILE_SHARE.Delete, IntPtr.Zero, CREATE_OPTION.Open_Always, FILE_ATTRIBUTE_FLAG.File_Attribute_Normal, IntPtr.Zero))
                            {
                                if (!Handle.IsInvalid)
                                {
                                    NewPath = Path;
                                    return true;
                                }
                            }

                            throw new InvalidOperationException();
                        }
                    case CreateOption.ReplaceExisting:
                        {
                            using (SafeFileHandle Handle = CreateFileFromApp(Path, FILE_ACCESS.Generic_Read, FILE_SHARE.Read | FILE_SHARE.Write | FILE_SHARE.Delete, IntPtr.Zero, CREATE_OPTION.Create_Always, FILE_ATTRIBUTE_FLAG.File_Attribute_Normal, IntPtr.Zero))
                            {
                                if (!Handle.IsInvalid)
                                {
                                    NewPath = Path;
                                    return true;
                                }
                            }

                            throw new InvalidOperationException();
                        }
                    default:
                        {
                            NewPath = string.Empty;
                            return false;
                        }
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"Could not create a new file, Path: \"{Path}\"");
                NewPath = string.Empty;
                return false;
            }
        }

        private static string GenerateUniquePath(string Path, StorageItemTypes ItemType)
        {
            string UniquePath = Path;

            if (ItemType == StorageItemTypes.File)
            {
                string NameWithoutExt = System.IO.Path.GetFileNameWithoutExtension(Path);
                string Extension = System.IO.Path.GetExtension(Path);
                string Directory = System.IO.Path.GetDirectoryName(Path);

                for (ushort Count = 1; CheckExist(UniquePath); Count++)
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
            else
            {
                string Directory = System.IO.Path.GetDirectoryName(Path);
                string Name = System.IO.Path.GetFileName(Path);

                for (ushort Count = 1; CheckExist(UniquePath); Count++)
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

        public static bool CloseDirectoryMonitorHandle(IntPtr hDir)
        {
            if (hDir.CheckIfValidPtr())
            {
                return CancelIoEx(hDir, IntPtr.Zero);
            }
            else
            {
                return true;
            }
        }

        public static SafeFileHandle CreateDirectoryMonitorHandle(string FolderPath)
        {
            return CreateFileFromApp(FolderPath, FILE_ACCESS.File_List_Directory, FILE_SHARE.Read | FILE_SHARE.Write | FILE_SHARE.Delete, IntPtr.Zero, CREATE_OPTION.Open_Existing, FILE_ATTRIBUTE_FLAG.File_Flag_Backup_Semantics, IntPtr.Zero);
        }

        public static bool CheckContainsAnyItem(string FolderPath, bool IncludeHiddenItem, bool IncludeSystemItem, BasicFilters Filter)
        {
            if (string.IsNullOrWhiteSpace(FolderPath))
            {
                throw new ArgumentException("Argument could not be empty", nameof(FolderPath));
            }

            IntPtr Ptr = FindFirstFileExFromApp(Path.Combine(FolderPath, "*"), FINDEX_INFO_LEVELS.FindExInfoBasic, out WIN32_FIND_DATA Data, FINDEX_SEARCH_OPS.FindExSearchNameMatch, IntPtr.Zero, FINDEX_ADDITIONAL_FLAGS.None);

            try
            {
                if (Ptr.CheckIfValidPtr())
                {
                    do
                    {
                        if (Data.cFileName != "." && Data.cFileName != "..")
                        {
                            FileAttributes Attribute = (FileAttributes)Data.dwFileAttributes;

                            if ((IncludeHiddenItem || !Attribute.HasFlag(FileAttributes.Hidden)) && (IncludeSystemItem || !Attribute.HasFlag(FileAttributes.System)))
                            {
                                if (Attribute.HasFlag(FileAttributes.Directory))
                                {
                                    if (Filter.HasFlag(BasicFilters.Folder))
                                    {
                                        return true;
                                    }
                                    else
                                    {
                                        return false;
                                    }
                                }
                                else if (Filter.HasFlag(BasicFilters.File))
                                {
                                    return true;
                                }
                            }
                        }
                    }
                    while (FindNextFile(Ptr, out Data));

                    return false;
                }
                else if (Marshal.GetLastWin32Error() is 2 or 3
                         && !Path.GetPathRoot(FolderPath).Equals(FolderPath, StringComparison.OrdinalIgnoreCase))
                {
                    LogTracer.Log($"Path not found: \"{FolderPath}\"");
                    return false;
                }
                else
                {
                    throw new LocationNotAvailableException();
                }
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

            IntPtr Ptr = FindFirstFileExFromApp(System.IO.Path.GetPathRoot(Path) == Path ? System.IO.Path.Combine(Path, "*") : Path.TrimEnd('\\'), FINDEX_INFO_LEVELS.FindExInfoBasic, out _, FINDEX_SEARCH_OPS.FindExSearchNameMatch, IntPtr.Zero, FINDEX_ADDITIONAL_FLAGS.None);

            try
            {
                if (Ptr.CheckIfValidPtr())
                {
                    return true;
                }
                else if (Marshal.GetLastWin32Error() is 2 or 3
                         && !System.IO.Path.GetPathRoot(Path).Equals(Path, StringComparison.OrdinalIgnoreCase))
                {
                    LogTracer.Log($"Path not found: \"{Path}\"");
                    return false;
                }
                else
                {
                    throw new LocationNotAvailableException();
                }
            }
            finally
            {
                FindClose(Ptr);
            }
        }

        public static IReadOnlyList<FileSystemStorageItemBase> Search(string FolderPath,
                                                                      string SearchWord,
                                                                      bool IncludeHiddenItem = false,
                                                                      bool IncludeSystemItem = false,
                                                                      bool IsRegexExpresstion = false,
                                                                      bool IgnoreCase = true,
                                                                      CancellationToken CancelToken = default)
        {
            if (string.IsNullOrWhiteSpace(FolderPath))
            {
                throw new ArgumentException("Argument could not be empty", nameof(FolderPath));
            }

            if (string.IsNullOrEmpty(SearchWord))
            {
                throw new ArgumentException("Argument could not be empty", nameof(SearchWord));
            }

            IntPtr SearchPtr = FindFirstFileExFromApp(Path.Combine(FolderPath, "*"), FINDEX_INFO_LEVELS.FindExInfoBasic, out WIN32_FIND_DATA Data, FINDEX_SEARCH_OPS.FindExSearchNameMatch, IntPtr.Zero, FINDEX_ADDITIONAL_FLAGS.Find_First_Ex_Large_Fetch);

            try
            {
                if (SearchPtr.CheckIfValidPtr())
                {
                    List<FileSystemStorageItemBase> SearchResult = new List<FileSystemStorageItemBase>();

                    do
                    {
                        CancelToken.ThrowIfCancellationRequested();

                        if (Data.cFileName != "." && Data.cFileName != "..")
                        {
                            FileAttributes Attribute = (FileAttributes)Data.dwFileAttributes;

                            if ((IncludeHiddenItem || !Attribute.HasFlag(FileAttributes.Hidden)) && (IncludeSystemItem || !Attribute.HasFlag(FileAttributes.System)))
                            {
                                if (IsRegexExpresstion ? Regex.IsMatch(Data.cFileName, SearchWord, IgnoreCase ? RegexOptions.IgnoreCase : RegexOptions.None)
                                                       : Data.cFileName.Contains(SearchWord, IgnoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
                                {
                                    string CurrentDataPath = Path.Combine(FolderPath, Data.cFileName);

                                    if (Attribute.HasFlag(FileAttributes.Directory))
                                    {
                                        if (Attribute.HasFlag(FileAttributes.Hidden))
                                        {
                                            SearchResult.Add(new HiddenStorageFolder(new Win32_File_Data(CurrentDataPath, Data)));
                                        }
                                        else
                                        {
                                            SearchResult.Add(new FileSystemStorageFolder(new Win32_File_Data(CurrentDataPath, Data)));
                                        }
                                    }
                                    else
                                    {
                                        if (Attribute.HasFlag(FileAttributes.Hidden))
                                        {
                                            SearchResult.Add(new HiddenStorageFile(new Win32_File_Data(CurrentDataPath, Data)));
                                        }
                                        else if (Data.cFileName.EndsWith(".url", StringComparison.OrdinalIgnoreCase))
                                        {
                                            SearchResult.Add(new UrlStorageFile(new Win32_File_Data(CurrentDataPath, Data)));
                                        }
                                        else if (Data.cFileName.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
                                        {
                                            SearchResult.Add(new LinkStorageFile(new Win32_File_Data(CurrentDataPath, Data)));
                                        }
                                        else
                                        {
                                            SearchResult.Add(new FileSystemStorageFile(new Win32_File_Data(CurrentDataPath, Data)));
                                        }
                                    }
                                }
                            }
                        }
                    }
                    while (FindNextFile(SearchPtr, out Data));

                    return SearchResult;
                }
                else if (Marshal.GetLastWin32Error() is 2 or 3
                         && !Path.GetPathRoot(FolderPath).Equals(FolderPath, StringComparison.OrdinalIgnoreCase))
                {
                    LogTracer.Log($"Path not found: \"{FolderPath}\"");
                    return new List<FileSystemStorageItemBase>(0);
                }
                else
                {
                    throw new LocationNotAvailableException();
                }
            }
            finally
            {
                FindClose(SearchPtr);
            }
        }

        public static IReadOnlyList<FileSystemStorageItemBase> GetStorageItems(string FolderPath,
                                                                               bool IncludeHiddenItem = false,
                                                                               bool IncludeSystemItem = false,
                                                                               uint MaxNumLimit = uint.MaxValue,
                                                                               BasicFilters Filter = BasicFilters.File | BasicFilters.Folder,
                                                                               Func<string, bool> AdvanceFilter = null)
        {
            if (string.IsNullOrWhiteSpace(FolderPath))
            {
                throw new ArgumentException("Argument could not be empty", nameof(FolderPath));
            }

            IntPtr Ptr = FindFirstFileExFromApp(Path.Combine(FolderPath, "*"), FINDEX_INFO_LEVELS.FindExInfoBasic, out WIN32_FIND_DATA Data, FINDEX_SEARCH_OPS.FindExSearchNameMatch, IntPtr.Zero, FINDEX_ADDITIONAL_FLAGS.Find_First_Ex_Large_Fetch);

            try
            {
                if (Ptr.CheckIfValidPtr())
                {
                    List<FileSystemStorageItemBase> Result = new List<FileSystemStorageItemBase>();

                    do
                    {
                        if (Data.cFileName != "." && Data.cFileName != "..")
                        {
                            if (AdvanceFilter != null && !AdvanceFilter(Data.cFileName))
                            {
                                continue;
                            }

                            FileAttributes Attribute = (FileAttributes)Data.dwFileAttributes;

                            if ((IncludeHiddenItem || !Attribute.HasFlag(FileAttributes.Hidden)) && (IncludeSystemItem || !Attribute.HasFlag(FileAttributes.System)))
                            {
                                if (Attribute.HasFlag(FileAttributes.Directory))
                                {
                                    if (Filter.HasFlag(BasicFilters.Folder))
                                    {
                                        string CurrentDataPath = Path.Combine(FolderPath, Data.cFileName);

                                        if (Attribute.HasFlag(FileAttributes.Hidden))
                                        {
                                            Result.Add(new HiddenStorageFolder(new Win32_File_Data(CurrentDataPath, Data)));
                                        }
                                        else
                                        {
                                            Result.Add(new FileSystemStorageFolder(new Win32_File_Data(CurrentDataPath, Data)));
                                        }
                                    }
                                }
                                else
                                {
                                    if (Filter.HasFlag(BasicFilters.File))
                                    {
                                        string CurrentDataPath = Path.Combine(FolderPath, Data.cFileName);

                                        if (Attribute.HasFlag(FileAttributes.Hidden))
                                        {
                                            Result.Add(new HiddenStorageFile(new Win32_File_Data(CurrentDataPath, Data)));
                                        }
                                        else if (Data.cFileName.EndsWith(".url", StringComparison.OrdinalIgnoreCase))
                                        {
                                            Result.Add(new UrlStorageFile(new Win32_File_Data(CurrentDataPath, Data)));
                                        }
                                        else if (Data.cFileName.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
                                        {
                                            Result.Add(new LinkStorageFile(new Win32_File_Data(CurrentDataPath, Data)));
                                        }
                                        else
                                        {
                                            Result.Add(new FileSystemStorageFile(new Win32_File_Data(CurrentDataPath, Data)));
                                        }
                                    }
                                }
                            }
                        }
                    }
                    while (FindNextFile(Ptr, out Data) && Result.Count < MaxNumLimit);

                    return Result;
                }
                else if (Marshal.GetLastWin32Error() is 2 or 3
                         && !Path.GetPathRoot(FolderPath).Equals(FolderPath, StringComparison.OrdinalIgnoreCase))
                {
                    LogTracer.Log($"Path not found: \"{FolderPath}\"");
                    return new List<FileSystemStorageItemBase>(0);
                }
                else
                {
                    throw new LocationNotAvailableException();
                }
            }
            finally
            {
                FindClose(Ptr);
            }
        }

        public static FileSystemStorageItemBase GetStorageItem(string ItemPath)
        {
            if (string.IsNullOrWhiteSpace(ItemPath))
            {
                throw new ArgumentNullException(nameof(ItemPath), "Argument could not be null");
            }

            IntPtr Ptr = FindFirstFileExFromApp(ItemPath.TrimEnd('\\'), FINDEX_INFO_LEVELS.FindExInfoBasic, out WIN32_FIND_DATA Data, FINDEX_SEARCH_OPS.FindExSearchNameMatch, IntPtr.Zero, FINDEX_ADDITIONAL_FLAGS.None);

            try
            {
                if (Ptr.CheckIfValidPtr())
                {
                    if (Data.cFileName != "." && Data.cFileName != "..")
                    {
                        FileAttributes Attribute = (FileAttributes)Data.dwFileAttributes;

                        if (Attribute.HasFlag(FileAttributes.Directory))
                        {
                            if (Attribute.HasFlag(FileAttributes.Hidden))
                            {
                                return new HiddenStorageFolder(new Win32_File_Data(ItemPath, Data));
                            }
                            else
                            {
                                return new FileSystemStorageFolder(new Win32_File_Data(ItemPath, Data));
                            }
                        }
                        else
                        {
                            if (Attribute.HasFlag(FileAttributes.Hidden))
                            {
                                return new HiddenStorageFile(new Win32_File_Data(ItemPath, Data));
                            }
                            else if (Data.cFileName.EndsWith(".url", StringComparison.OrdinalIgnoreCase))
                            {
                                return new UrlStorageFile(new Win32_File_Data(ItemPath, Data));
                            }
                            else if (Data.cFileName.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
                            {
                                return new LinkStorageFile(new Win32_File_Data(ItemPath, Data));
                            }
                            else
                            {
                                return new FileSystemStorageFile(new Win32_File_Data(ItemPath, Data));
                            }
                        }
                    }

                    return null;
                }
                else if (Marshal.GetLastWin32Error() is 2 or 3
                         && !Path.GetPathRoot(ItemPath).Equals(ItemPath, StringComparison.OrdinalIgnoreCase))
                {
                    LogTracer.Log($"Path not found: \"{ItemPath}\"");
                    return null;
                }
                else
                {
                    throw new LocationNotAvailableException();
                }
            }
            finally
            {
                FindClose(Ptr);
            }
        }

        public static Win32_File_Data GetStorageItemRawData(string ItemPath)
        {
            if (string.IsNullOrWhiteSpace(ItemPath))
            {
                throw new ArgumentNullException(nameof(ItemPath), "Argument could not be null");
            }

            IntPtr Ptr = FindFirstFileExFromApp(ItemPath.TrimEnd('\\'), FINDEX_INFO_LEVELS.FindExInfoBasic, out WIN32_FIND_DATA Data, FINDEX_SEARCH_OPS.FindExSearchNameMatch, IntPtr.Zero, FINDEX_ADDITIONAL_FLAGS.None);

            try
            {
                if (Ptr.CheckIfValidPtr())
                {
                    return new Win32_File_Data(ItemPath, Data);
                }
                else
                {
                    return new Win32_File_Data(ItemPath);
                }
            }
            finally
            {
                FindClose(Ptr);
            }
        }

        public static Win32_File_Data GetStorageItemRawDataFromHandle(string Path, IntPtr FileHandle)
        {
            if (FileHandle.CheckIfValidPtr())
            {
                int StructSize = Marshal.SizeOf<FILE_BASIC_INFO>();

                IntPtr StructPtr = Marshal.AllocCoTaskMem(StructSize);

                try
                {
                    if (GetFileInformationByHandleEx(FileHandle, FILE_INFO_BY_HANDLE_CLASS.FileBasicInfo, StructPtr, Convert.ToUInt32(StructSize)))
                    {
                        FILE_BASIC_INFO Info = Marshal.PtrToStructure<FILE_BASIC_INFO>(StructPtr);

                        if (Info.FileAttributes.HasFlag(FileAttributes.Directory))
                        {
                            return new Win32_File_Data(Path, 0, Info.FileAttributes, Info.LastWriteTime, Info.CreationTime);
                        }
                        else
                        {
                            if (GetFileSizeEx(FileHandle, out long Size))
                            {
                                return new Win32_File_Data(Path, Size > 0 ? Convert.ToUInt64(Size) : 0, Info.FileAttributes, Info.LastWriteTime, Info.CreationTime);
                            }
                            else
                            {
                                return new Win32_File_Data(Path, 0, Info.FileAttributes, Info.LastWriteTime, Info.CreationTime);
                            }
                        }
                    }
                    else
                    {
                        LogTracer.Log($"Could not get file information from native api, path: \"{Path}\"");
                        return new Win32_File_Data(Path);
                    }
                }
                finally
                {
                    Marshal.FreeCoTaskMem(StructPtr);
                }
            }
            else
            {
                return new Win32_File_Data(Path);
            }
        }

        public static ulong GetFileSpaceSize(IntPtr FileHandle)
        {
            if (FileHandle.CheckIfValidPtr())
            {
                int StructSize = Marshal.SizeOf<FILE_STANDARD_INFO>();

                IntPtr StructPtr = Marshal.AllocCoTaskMem(StructSize);

                try
                {
                    if (GetFileInformationByHandleEx(FileHandle, FILE_INFO_BY_HANDLE_CLASS.FileStandardInfo, StructPtr, Convert.ToUInt32(StructSize)))
                    {
                        FILE_STANDARD_INFO Info = Marshal.PtrToStructure<FILE_STANDARD_INFO>(StructPtr);
                        return Info.AllocationSize > 0 ? Convert.ToUInt64(Info.AllocationSize) : 0;
                    }
                    else
                    {
                        LogTracer.Log($"Could not get file space from native api");
                        return 0;
                    }
                }
                finally
                {
                    Marshal.FreeCoTaskMem(StructPtr);
                }
            }
            else
            {
                return 0;
            }
        }

        public static FileSystemStorageItemBase GetStorageItemFromHandle(string Path, IntPtr FileHandle)
        {
            Win32_File_Data Data = GetStorageItemRawDataFromHandle(Path, FileHandle);

            if (Data.IsDataValid)
            {
                if (Data.Attributes.HasFlag(FileAttributes.Directory))
                {
                    if (Data.Attributes.HasFlag(FileAttributes.Hidden))
                    {
                        return new HiddenStorageFolder(Data);
                    }
                    else
                    {
                        return new FileSystemStorageFolder(Data);
                    }
                }
                else
                {
                    if (Data.Attributes.HasFlag(FileAttributes.Hidden))
                    {
                        return new HiddenStorageFile(Data);
                    }
                    else if (Path.EndsWith(".url", StringComparison.OrdinalIgnoreCase))
                    {
                        return new UrlStorageFile(Data);
                    }
                    else if (Path.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
                    {
                        return new LinkStorageFile(Data);
                    }
                    else
                    {
                        return new FileSystemStorageFile(Data);
                    }
                }
            }
            else
            {
                return null;
            }
        }
    }
}
