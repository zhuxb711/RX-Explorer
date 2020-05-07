using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using Windows.Storage;
using FileAttributes = System.IO.FileAttributes;

namespace FileManager.Class
{
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

        [DllImport("api-ms-win-core-file-l1-1-0.dll", CharSet = CharSet.Unicode)]
        private static extern bool FindNextFile(IntPtr hFindFile, out WIN32_FIND_DATA lpFindFileData);

        [DllImport("api-ms-win-core-file-l1-1-0.dll")]
        private static extern bool FindClose(IntPtr hFindFile);

        [DllImport("api-ms-win-core-timezone-l1-1-0.dll", SetLastError = true)]
        private static extern bool FileTimeToSystemTime(ref FILETIME lpFileTime, out SYSTEMTIME lpSystemTime);

        public static bool CheckContainsAnyItem(string Path, ItemFilter Filter)
        {
            IntPtr Ptr = FindFirstFileExFromApp(System.IO.Path.Combine(Path, "*.*"), FINDEX_INFO_LEVELS.FindExInfoBasic, out WIN32_FIND_DATA Data, FINDEX_SEARCH_OPS.FindExSearchNameMatch, IntPtr.Zero, FIND_FIRST_EX_LARGE_FETCH);
            if (Ptr.ToInt64() != -1)
            {
                try
                {
                    do
                    {
                        if (!((FileAttributes)Data.dwFileAttributes).HasFlag(FileAttributes.Hidden) && !((FileAttributes)Data.dwFileAttributes).HasFlag(FileAttributes.System))
                        {
                            if (((FileAttributes)Data.dwFileAttributes).HasFlag(FileAttributes.Directory) && Filter.HasFlag(ItemFilter.Folder))
                            {
                                if (Data.cFileName != "." && Data.cFileName != "..")
                                {
                                    return true;
                                }
                            }
                            else if (Filter.HasFlag(ItemFilter.File))
                            {
                                if (!Data.cFileName.EndsWith(".lnk") && !Data.cFileName.EndsWith(".url"))
                                {
                                    return true;
                                }
                            }
                        }
                    }
                    while (FindNextFile(Ptr, out Data));
                }
                finally
                {
                    FindClose(Ptr);
                }
            }

            return false;
        }

        public static List<FileSystemStorageItem> GetStorageItems(string Path, ItemFilter Filter)
        {
            List<FileSystemStorageItem> Result = new List<FileSystemStorageItem>();

            IntPtr Ptr = FindFirstFileExFromApp(System.IO.Path.Combine(Path, "*.*"), FINDEX_INFO_LEVELS.FindExInfoBasic, out WIN32_FIND_DATA Data, FINDEX_SEARCH_OPS.FindExSearchNameMatch, IntPtr.Zero, FIND_FIRST_EX_LARGE_FETCH);
            if (Ptr.ToInt64() != -1)
            {
                try
                {
                    do
                    {
                        if (!((FileAttributes)Data.dwFileAttributes).HasFlag(FileAttributes.Hidden) && !((FileAttributes)Data.dwFileAttributes).HasFlag(FileAttributes.System))
                        {
                            if (((FileAttributes)Data.dwFileAttributes).HasFlag(FileAttributes.Directory) && Filter.HasFlag(ItemFilter.Folder))
                            {
                                if (Data.cFileName != "." && Data.cFileName != "..")
                                {
                                    FileTimeToSystemTime(ref Data.ftLastWriteTime, out SYSTEMTIME ModTime);
                                    DateTime ModifiedTime = new DateTime(ModTime.Year, ModTime.Month, ModTime.Day, ModTime.Hour, ModTime.Minute, ModTime.Second, ModTime.Milliseconds);
                                    Result.Add(new FileSystemStorageItem(Data, StorageItemTypes.Folder, System.IO.Path.Combine(Path, Data.cFileName), ModifiedTime));
                                }
                            }
                            else if (Filter.HasFlag(ItemFilter.File))
                            {
                                if (!Data.cFileName.EndsWith(".lnk") && !Data.cFileName.EndsWith(".url"))
                                {
                                    FileTimeToSystemTime(ref Data.ftLastWriteTime, out SYSTEMTIME ModTime);
                                    DateTime ModifiedTime = new DateTime(ModTime.Year, ModTime.Month, ModTime.Day, ModTime.Hour, ModTime.Minute, ModTime.Second, ModTime.Milliseconds);
                                    Result.Add(new FileSystemStorageItem(Data, StorageItemTypes.File, System.IO.Path.Combine(Path, Data.cFileName), ModifiedTime));
                                }
                            }
                        }
                    }
                    while (FindNextFile(Ptr, out Data));
                }
                finally
                {
                    FindClose(Ptr);
                }
            }

            return Result;
        }

        public static List<FileSystemStorageItem> GetStorageItems(StorageFolder Folder, ItemFilter Filter)
        {
            List<FileSystemStorageItem> Result = new List<FileSystemStorageItem>();

            IntPtr Ptr = FindFirstFileExFromApp(System.IO.Path.Combine(Folder.Path, "*.*"), FINDEX_INFO_LEVELS.FindExInfoBasic, out WIN32_FIND_DATA Data, FINDEX_SEARCH_OPS.FindExSearchNameMatch, IntPtr.Zero, FIND_FIRST_EX_LARGE_FETCH);
            if (Ptr.ToInt64() != -1)
            {
                do
                {
                    if (!((FileAttributes)Data.dwFileAttributes).HasFlag(FileAttributes.Hidden) && !((FileAttributes)Data.dwFileAttributes).HasFlag(FileAttributes.System))
                    {
                        if (((FileAttributes)Data.dwFileAttributes).HasFlag(FileAttributes.Directory) && Filter.HasFlag(ItemFilter.Folder))
                        {
                            if (Data.cFileName != "." && Data.cFileName != "..")
                            {
                                FileTimeToSystemTime(ref Data.ftLastWriteTime, out SYSTEMTIME ModTime);
                                DateTime ModifiedTime = new DateTime(ModTime.Year, ModTime.Month, ModTime.Day, ModTime.Hour, ModTime.Minute, ModTime.Second, ModTime.Milliseconds);
                                Result.Add(new FileSystemStorageItem(Data, StorageItemTypes.Folder, System.IO.Path.Combine(Folder.Path, Data.cFileName), ModifiedTime));
                            }
                        }
                        else if (Filter.HasFlag(ItemFilter.File))
                        {
                            if (!Data.cFileName.EndsWith(".lnk") && !Data.cFileName.EndsWith(".url"))
                            {
                                FileTimeToSystemTime(ref Data.ftLastWriteTime, out SYSTEMTIME ModTime);
                                DateTime ModifiedTime = new DateTime(ModTime.Year, ModTime.Month, ModTime.Day, ModTime.Hour, ModTime.Minute, ModTime.Second, ModTime.Milliseconds);
                                Result.Add(new FileSystemStorageItem(Data, StorageItemTypes.File, System.IO.Path.Combine(Folder.Path, Data.cFileName), ModifiedTime));
                            }
                        }
                    }
                }
                while (FindNextFile(Ptr, out Data));

                FindClose(Ptr);
            }

            return Result;
        }
    }
}
