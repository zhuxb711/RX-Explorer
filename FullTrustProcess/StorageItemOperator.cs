using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

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

        private static bool CheckOccupied(string Path)
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

        private static string DetectAndGenerateNewName(string FullPath)
        {
            int Count = 1;

            string FileNameOnly = Path.GetFileNameWithoutExtension(FullPath);
            string Extension = Path.GetExtension(FullPath);
            string Directory = Path.GetDirectoryName(FullPath);
            string NewFullPath = FullPath;

            while (File.Exists(NewFullPath))
            {
                NewFullPath = Path.Combine(Directory, $"{FileNameOnly}({Count++}){Extension}");
            }

            return NewFullPath;
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
                        CheckTool.StartInfo.Arguments = $"{Path} /accepteula";
                        CheckTool.StartInfo.UseShellExecute = false;
                        CheckTool.StartInfo.RedirectStandardOutput = true;
                        CheckTool.Start();
                        CheckTool.WaitForExit();

                        foreach (Match match in Regex.Matches(CheckTool.StandardOutput.ReadToEnd(), @"(?<=\s+pid:\s+)\b(\d+)\b(?=\s+)"))
                        {
                            using (Process TargetProcess = Process.GetProcessById(int.Parse(match.Value)))
                            {
                                try
                                {
                                    TargetProcess.Kill();
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

                        return !CheckOccupied(Path);
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

        public static bool CopyFolder(string SourcePath, string DestinationPath)
        {
            try
            {
                if (!Directory.Exists(DestinationPath))
                {
                    Directory.CreateDirectory(DestinationPath);
                }

                foreach (string SubFile in Directory.GetFiles(SourcePath))
                {
                    File.Copy(SubFile, DetectAndGenerateNewName(Path.Combine(DestinationPath, Path.GetFileName(SubFile))));
                }

                List<bool> SuccessList = new List<bool>();

                foreach (string SubFolder in Directory.GetDirectories(SourcePath))
                {
                    SuccessList.Add(CopyFolder(SubFolder, Path.Combine(DestinationPath, Path.GetFileName(SubFolder))));
                }

                return SuccessList.Count == 0 || SuccessList.All((Item) => Item);
            }
            catch
            {
                return false;
            }
        }

        public static bool CopyFile(string SourcePath, string DestinationPath)
        {
            try
            {
                if (File.Exists(SourcePath))
                {
                    File.Copy(SourcePath, DetectAndGenerateNewName(DestinationPath));
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

        public static bool MoveFolder(string SourcePath, string DestinationPath)
        {
            try
            {
                if (Directory.Exists(SourcePath))
                {
                    if (Path.GetPathRoot(SourcePath) == Path.GetPathRoot(DestinationPath))
                    {
                        Directory.Move(SourcePath, DestinationPath);
                    }
                    else
                    {
                        CopyFolder(SourcePath, DestinationPath);
                        Directory.Delete(SourcePath, true);
                    }

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

        public static bool MoveFile(string SourcePath, string DestinationPath)
        {
            try
            {
                if (File.Exists(SourcePath))
                {
                    File.Move(SourcePath, DetectAndGenerateNewName(Path.Combine(DestinationPath, Path.GetFileName(SourcePath))));

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
