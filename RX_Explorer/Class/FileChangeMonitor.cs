using Microsoft.Toolkit.Deferred;
using ShareClassLibrary;
using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace RX_Explorer.Class
{
    public sealed class FileChangeMonitor : IDisposable
    {
        private IntPtr WatcherPtr;
        private Thread BackgroundThread;

        public event EventHandler<FileChangedDeferredEventArgs> FileChanged;

        public string CurrentLocation { get; private set; }

        private enum StateChangeType
        {
            Unknown_Action = 0,
            Added_Action = 1,
            Removed_Action = 2,
            Modified_Action = 3,
            Rename_Action_OldName = 4,
            Rename_Action_NewName = 5
        }

        public async Task StartMonitorAsync(string Path)
        {
            await StopMonitorAsync();

            if (!string.IsNullOrWhiteSpace(Path))
            {
                CurrentLocation = Path;

                WatcherPtr = Win32_Native_API.CreateDirectoryMonitorHandle(Path);

                if (!WatcherPtr.CheckIfValidPtr())
                {
                    LogTracer.Log(new Win32Exception(Marshal.GetLastWin32Error()), "Could not create a monitor on directory in native api, fallback to fulltrust process");

                    using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableControllerAsync())
                    {
                        WatcherPtr = await Exclusive.Controller.GetDirectoryMonitorHandleAsync(Path);
                    }
                }

                if (WatcherPtr.CheckIfValidPtr())
                {
                    BackgroundThread = new Thread(ThreadProcess)
                    {
                        IsBackground = true,
                        Priority = ThreadPriority.BelowNormal
                    };
                    BackgroundThread.Start((Path, WatcherPtr));
                }
                else
                {
                    LogTracer.Log($"Could not create a monitor on directory. Path: \"{Path}\"");
                }
            }
        }

        public Task StopMonitorAsync()
        {
            return Task.Run(() =>
            {
                StopMonitor();
            });
        }

        private void StopMonitor()
        {
            if (WatcherPtr.CheckIfValidPtr())
            {
                if (Win32_Native_API.CloseDirectoryMonitorHandle(WatcherPtr))
                {
                    WatcherPtr = IntPtr.Zero;
                }
                else
                {
                    LogTracer.Log("Could not close the directory monitor handle normally");
                }
            }
        }

        private void ThreadProcess(object Parameter)
        {
            if (Parameter is (string DirPath, IntPtr DirPtr))
            {
                while (DirPtr.CheckIfValidPtr())
                {
                    IntPtr BufferPtr = Marshal.AllocHGlobal(4096);

                    try
                    {
                        if (Win32_Native_API.ReadDirectoryChangesW(DirPtr,
                                                                   BufferPtr,
                                                                   4096,
                                                                   false,
                                                                   Win32_Native_API.FILE_NOTIFY_CHANGE.FILE_NOTIFY_CHANGE_FILE_NAME
                                                                   | Win32_Native_API.FILE_NOTIFY_CHANGE.FILE_NOTIFY_CHANGE_DIR_NAME
                                                                   | Win32_Native_API.FILE_NOTIFY_CHANGE.FILE_NOTIFY_CHANGE_LAST_WRITE
                                                                   | Win32_Native_API.FILE_NOTIFY_CHANGE.FILE_NOTIFY_CHANGE_SIZE
                                                                   | Win32_Native_API.FILE_NOTIFY_CHANGE.FILE_NOTIFY_CHANGE_ATTRIBUTES,
                                                                   out uint BytesReturned,
                                                                   IntPtr.Zero,
                                                                   IntPtr.Zero))
                        {
                            if (BytesReturned > 0)
                            {
                                int Offset = 0;
                                string OldPath = null;
                                IntPtr CurrentPointer = BufferPtr;

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
                                                FileChanged.InvokeAsync(this, new FileAddedDeferredEventArgs(Path.Combine(DirPath, FileName))).Wait();
                                                break;
                                            }
                                        case StateChangeType.Removed_Action:
                                            {
                                                FileChanged.InvokeAsync(this, new FileRemovedDeferredEventArgs(Path.Combine(DirPath, FileName))).Wait();
                                                break;
                                            }
                                        case StateChangeType.Modified_Action:
                                            {
                                                FileChanged.InvokeAsync(this, new FileModifiedDeferredEventArgs(Path.Combine(DirPath, FileName))).Wait();
                                                break;
                                            }
                                        case StateChangeType.Rename_Action_OldName:
                                            {
                                                OldPath = Path.Combine(DirPath, FileName);
                                                break;
                                            }
                                        case StateChangeType.Rename_Action_NewName:
                                            {
                                                FileChanged.InvokeAsync(this, new FileRenamedDeferredEventArgs(OldPath, FileName)).Wait();
                                                break;
                                            }
                                    }

                                    // Read NextEntryOffset at offset 0 and move pointer to next structure if needed
                                    Offset = Marshal.ReadInt32(CurrentPointer);
                                }
                                while (Offset != 0);
                            }
                        }
                        else
                        {
                            break;
                        }
                    }
                    catch (Exception ex)
                    {
                        LogTracer.Log(ex, "An exception was threw when watching the directory");
                    }
                    finally
                    {
                        if (BufferPtr.CheckIfValidPtr())
                        {
                            Marshal.FreeHGlobal(BufferPtr);
                        }
                    }
                }
            }
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);

            StopMonitor();
            CurrentLocation = string.Empty;
        }

        public FileChangeMonitor()
        {
            WatcherPtr = IntPtr.Zero;
        }

        ~FileChangeMonitor()
        {
            Dispose();
        }
    }
}
