using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Vanara.Windows.Shell;

namespace FullTrustProcess
{
    public static class StorageItemOperator
    {
        [DllImport("kernel32.dll")]
        private static extern IntPtr _lopen(string lpPathName, int iReadWrite);
        [DllImport("kernel32.dll")]
        private static extern bool CloseHandle(IntPtr hObject);
        private const int OF_READWRITE = 2;
        private const int OF_SHARE_DENY_NONE = 0x40;
        private static readonly IntPtr HFILE_ERROR = new IntPtr(-1);

        public static bool CheckOccupied(string Path)
        {
            if (File.Exists(Path))
            {
                IntPtr Handle = IntPtr.Zero;

                try
                {
                    Handle = _lopen(Path, OF_READWRITE | OF_SHARE_DENY_NONE);

                    if (Handle == HFILE_ERROR)
                    {
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
                finally
                {
                    CloseHandle(Handle);
                }
            }
            else
            {
                throw new FileNotFoundException();
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
                        CheckTool.StartInfo.Arguments = $"\"{Path.Replace("\\", "/")}\"";
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

        public static bool Delete(string Path)
        {
            try
            {
                using (ShellItem Item = new ShellItem(Path))
                {
                    ShellFileOperations.Delete(Item, ShellFileOperations.OperationFlags.AllowUndo | ShellFileOperations.OperationFlags.NoConfirmMkDir | ShellFileOperations.OperationFlags.Silent);
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        public static bool Copy(string SourcePath, string DestinationPath, string NewName = null)
        {
            try
            {
                using (ShellItem SourceItem = new ShellItem(SourcePath))
                using (ShellFolder DestItem = new ShellFolder(DestinationPath))
                {
                    ShellFileOperations.Copy(SourceItem, DestItem, NewName, ShellFileOperations.OperationFlags.AllowUndo | ShellFileOperations.OperationFlags.NoConfirmMkDir | ShellFileOperations.OperationFlags.Silent);
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        public static bool Move(string SourcePath, string DestinationPath, string NewName = null)
        {
            try
            {
                using (ShellItem SourceItem = new ShellItem(SourcePath))
                using (ShellFolder DestItem = new ShellFolder(DestinationPath))
                {
                    ShellFileOperations.Move(SourceItem, DestItem, NewName, ShellFileOperations.OperationFlags.AllowUndo | ShellFileOperations.OperationFlags.NoConfirmMkDir | ShellFileOperations.OperationFlags.Silent);
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
