using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Vanara.Windows.Shell;

namespace FullTrustProcess
{
    public static class StorageItemController
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

        public static bool Delete(IEnumerable<string> Source, bool PermanentDelete, ProgressChangedEventHandler Progress)
        {
            try
            {
                using (ShellFileOperations Operation = new ShellFileOperations
                {
                    Options = PermanentDelete
                    ? ShellFileOperations.OperationFlags.NoConfirmMkDir | ShellFileOperations.OperationFlags.Silent | ShellFileOperations.OperationFlags.NoConfirmation
                    : ShellFileOperations.OperationFlags.AddUndoRecord | ShellFileOperations.OperationFlags.Silent | ShellFileOperations.OperationFlags.NoConfirmMkDir | ShellFileOperations.OperationFlags.RecycleOnDelete
                })
                {
                    Operation.UpdateProgress += Progress;

                    foreach (string Path in Source)
                    {
                        using (ShellItem Item = new ShellItem(Path))
                        {
                            Operation.QueueDeleteOperation(Item);
                        }
                    }

                    Operation.PerformOperations();

                    Operation.UpdateProgress -= Progress;
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        public static bool Copy(IEnumerable<KeyValuePair<string, string>> Source, string DestinationPath, ProgressChangedEventHandler Progress)
        {
            try
            {
                if (!Directory.Exists(DestinationPath))
                {
                    _ = Directory.CreateDirectory(DestinationPath);
                }

                using (ShellFileOperations Operation = new ShellFileOperations
                {
                    Options = ShellFileOperations.OperationFlags.AddUndoRecord | ShellFileOperations.OperationFlags.NoConfirmMkDir | ShellFileOperations.OperationFlags.Silent
                })
                {
                    Operation.UpdateProgress += Progress;

                    foreach (KeyValuePair<string, string> SourceInfo in Source)
                    {
                        using (ShellItem SourceItem = new ShellItem(SourceInfo.Key))
                        using (ShellFolder DestItem = new ShellFolder(DestinationPath))
                        {
                            Operation.QueueCopyOperation(SourceItem, DestItem, string.IsNullOrEmpty(SourceInfo.Value) ? null : SourceInfo.Value);
                        }
                    }

                    Operation.PerformOperations();

                    Operation.UpdateProgress -= Progress;
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        public static bool Move(IEnumerable<KeyValuePair<string, string>> Source, string DestinationPath, ProgressChangedEventHandler Progress)
        {
            try
            {
                if (!Directory.Exists(DestinationPath))
                {
                    _ = Directory.CreateDirectory(DestinationPath);
                }

                using (ShellFileOperations Operation = new ShellFileOperations
                {
                    Options = ShellFileOperations.OperationFlags.AddUndoRecord | ShellFileOperations.OperationFlags.NoConfirmMkDir | ShellFileOperations.OperationFlags.Silent
                })
                {
                    Operation.UpdateProgress += Progress;

                    foreach (KeyValuePair<string, string> SourceInfo in Source)
                    {
                        using (ShellItem SourceItem = new ShellItem(SourceInfo.Key))
                        using (ShellFolder DestItem = new ShellFolder(DestinationPath))
                        {
                            Operation.QueueMoveOperation(SourceItem, DestItem, string.IsNullOrEmpty(SourceInfo.Value) ? null : SourceInfo.Value);
                        }
                    }

                    Operation.PerformOperations();

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
