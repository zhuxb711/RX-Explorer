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
    public sealed class StorageAreaWatcher : IDisposable
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

        public void StartWatchDirectory(string Path)
        {
            if (!string.IsNullOrWhiteSpace(Path))
            {
                StopWatchDirectory();

                CurrentLocation = Path;

                WatcherPtr = Win32_Native_API.CreateDirectoryWatcher(Path);

                if (WatcherPtr.CheckIfValidPtr())
                {
                    BackgroundThread = new Thread(ThreadProcess)
                    {
                        IsBackground = true,
                        Priority = ThreadPriority.BelowNormal
                    };
                    BackgroundThread.Start();
                }
                else
                {
                    LogTracer.Log(new Win32Exception(Marshal.GetLastWin32Error()), $"Could not create a watcher on directory. Path: \"{Path}\"");
                }
            }
        }

        public void StopWatchDirectory()
        {
            if (WatcherPtr.CheckIfValidPtr())
            {
                Win32_Native_API.StopDirectoryWatcher(ref WatcherPtr);
            }
        }

        private void ThreadProcess()
        {
            while (true)
            {
                IntPtr BufferPtr = Marshal.AllocHGlobal(4096);

                try
                {
                    if (Win32_Native_API.ReadDirectoryChangesW(WatcherPtr,
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
                                            FileChanged.InvokeAsync(this, new FileAddedDeferredEventArgs(Path.Combine(CurrentLocation, FileName))).Wait();
                                            break;
                                        }
                                    case StateChangeType.Removed_Action:
                                        {
                                            FileChanged.InvokeAsync(this, new FileRemovedDeferredEventArgs(Path.Combine(CurrentLocation, FileName))).Wait();
                                            break;
                                        }
                                    case StateChangeType.Modified_Action:
                                        {
                                            FileChanged.InvokeAsync(this, new FileModifiedDeferredEventArgs(Path.Combine(CurrentLocation, FileName))).Wait();
                                            break;
                                        }
                                    case StateChangeType.Rename_Action_OldName:
                                        {
                                            OldPath = Path.Combine(CurrentLocation, FileName);
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
                    if (BufferPtr != IntPtr.Zero)
                    {
                        Marshal.FreeHGlobal(BufferPtr);
                    }
                }
            }
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);

            StopWatchDirectory();
            CurrentLocation = string.Empty;
        }

        public StorageAreaWatcher()
        {
            WatcherPtr = IntPtr.Zero;
        }

        ~StorageAreaWatcher()
        {
            Dispose();
        }
    }
}
