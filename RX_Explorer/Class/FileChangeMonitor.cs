using Microsoft.Toolkit.Deferred;
using Microsoft.Win32.SafeHandles;
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
        private Thread BackgroundThread;
        private CancellationTokenSource Cancellation;
        public event EventHandler<FileChangedDeferredEventArgs> FileChanged;

        public Task InvokeAddedEventManuallyAsync(FileAddedDeferredEventArgs Args)
        {
            return InvokeEventManuallyCoreAsync(Args);
        }

        public Task InvokeRemovedEventManuallyAsync(FileRemovedDeferredEventArgs Args)
        {
            return InvokeEventManuallyCoreAsync(Args);
        }

        public Task InvokeRenamedEventManuallyAsync(FileRenamedDeferredEventArgs Args)
        {
            return InvokeEventManuallyCoreAsync(Args);
        }

        public Task InvokeModifiedEventManuallyAsync(FileModifiedDeferredEventArgs Args)
        {
            return InvokeEventManuallyCoreAsync(Args);
        }

        private async Task InvokeEventManuallyCoreAsync(FileChangedDeferredEventArgs Args)
        {
            await FileChanged?.InvokeAsync(this, Args);
        }

        public async Task StartMonitorAsync(string Path)
        {
            StopMonitor();

            if (!string.IsNullOrWhiteSpace(Path))
            {
                SafeFileHandle MonitorPointer = NativeWin32API.CreateDirectoryMonitorHandle(Path);

                if (MonitorPointer.IsInvalid)
                {
                    using (AuxiliaryTrustProcessController.Exclusive Exclusive = await AuxiliaryTrustProcessController.GetControllerExclusiveAsync(Priority: PriorityLevel.High))
                    {
                        MonitorPointer = await Exclusive.Controller.GetDirectoryMonitorHandleAsync(Path);
                    }
                }

                if ((MonitorPointer?.IsInvalid).GetValueOrDefault(true))
                {
                    LogTracer.Log($"Could not create the monitor on directory, path: \"{Path}\"");
                }
                else
                {
                    BackgroundThread = new Thread(ThreadProcess)
                    {
                        IsBackground = true,
                        Priority = ThreadPriority.BelowNormal
                    };
                    BackgroundThread.Start(new FileChangeMonitorInternalData(Path, MonitorPointer, Cancellation.Token));
                }
            }
        }

        public void StopMonitor()
        {
            CancelMonitorCore(false);
        }

        private void CancelMonitorCore(bool IsDisposing)
        {
            if (Interlocked.Exchange(ref Cancellation, IsDisposing ? null : new CancellationTokenSource()) is CancellationTokenSource PreviousCancellation)
            {
                PreviousCancellation.Cancel();
                PreviousCancellation.Dispose();
            }
        }

        private void ThreadProcess(object Parameter)
        {
            if (Parameter is FileChangeMonitorInternalData Data)
            {
                try
                {
                    while (true)
                    {
                        IntPtr BufferPtr = Marshal.AllocCoTaskMem(4096);

                        try
                        {
                            if (NativeWin32API.ReadDirectoryChanges(Data.Handle.DangerousGetHandle(),
                                                                    BufferPtr,
                                                                    4096,
                                                                    false,
                                                                    NativeWin32API.FILE_NOTIFY_CHANGE.File_Notify_Change_File_Name
                                                                    | NativeWin32API.FILE_NOTIFY_CHANGE.File_Notify_Change_Dir_Name
                                                                    | NativeWin32API.FILE_NOTIFY_CHANGE.File_Notify_Change_Last_Write
                                                                    | NativeWin32API.FILE_NOTIFY_CHANGE.File_Notify_Change_Size
                                                                    | NativeWin32API.FILE_NOTIFY_CHANGE.File_Notify_Change_Attribute,
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
                                        CurrentPointer = new IntPtr(Offset + CurrentPointer.ToInt64());

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
                                                    FileChanged.InvokeAsync(this, new FileAddedDeferredEventArgs(Path.Combine(Data.Path, FileName))).Wait();
                                                    break;
                                                }
                                            case StateChangeType.Removed_Action:
                                                {
                                                    FileChanged.InvokeAsync(this, new FileRemovedDeferredEventArgs(Path.Combine(Data.Path, FileName))).Wait();
                                                    break;
                                                }
                                            case StateChangeType.Modified_Action:
                                                {
                                                    FileChanged.InvokeAsync(this, new FileModifiedDeferredEventArgs(Path.Combine(Data.Path, FileName))).Wait();
                                                    break;
                                                }
                                            case StateChangeType.Rename_Action_OldName:
                                                {
                                                    OldPath = Path.Combine(Data.Path, FileName);
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
                        finally
                        {
                            Marshal.FreeCoTaskMem(BufferPtr);
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogTracer.Log(ex, "An exception was threw when watching the directory");
                }
                finally
                {
                    Data.Dispose();
                }
            }
        }

        public void Dispose()
        {
            CancelMonitorCore(true);
            GC.SuppressFinalize(this);
        }

        ~FileChangeMonitor()
        {
            Dispose();
        }

        private sealed class FileChangeMonitorInternalData : IDisposable
        {
            public string Path { get; }

            public SafeFileHandle Handle { get; }

            private readonly CancellationTokenRegistration Registration;

            private void OnCancelled(object Parameter)
            {
                if (Parameter is IntPtr RawHandle)
                {
                    try
                    {
                        if (!NativeWin32API.CloseDirectoryMonitorHandle(RawHandle))
                        {
                            throw new Win32Exception(Marshal.GetLastWin32Error());
                        }
                    }
                    catch (Exception)
                    {
                        //No need to handle this exception
                    }
                }
            }

            public FileChangeMonitorInternalData(string Path, SafeFileHandle Handle, CancellationToken CancelToken)
            {
                this.Path = Path;
                this.Handle = Handle;

                Registration = CancelToken.Register(OnCancelled, Handle.DangerousGetHandle());
            }

            public void Dispose()
            {
                Registration.Dispose();
                Handle.Dispose();

                GC.SuppressFinalize(this);
            }

            ~FileChangeMonitorInternalData()
            {
                Dispose();
            }
        }
    }
}
