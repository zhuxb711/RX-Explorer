using SharedLibrary;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Threading;
using Vanara.PInvoke;
using Vanara.Windows.Shell;

namespace AuxiliaryTrustProcess.Class
{
    public static class RemoteDataObject
    {
        public static RemoteClipboardRelatedData GetRemoteClipboardRelatedData()
        {
            uint FILEDESCRIPTIONWID = NativeClipboard.RegisterFormat(Shell32.ShellClipboardFormat.CFSTR_FILEDESCRIPTORW);
            uint FILEDESCRIPTIONAID = NativeClipboard.RegisterFormat(Shell32.ShellClipboardFormat.CFSTR_FILEDESCRIPTORA);

            uint FormatId = 0;

            if (NativeClipboard.IsFormatAvailable(FILEDESCRIPTIONWID))
            {
                FormatId = FILEDESCRIPTIONWID;
            }
            else if (NativeClipboard.IsFormatAvailable(FILEDESCRIPTIONAID))
            {
                FormatId = FILEDESCRIPTIONAID;
            }

            if (FormatId > 0)
            {
                using (MemoryStream FileGroupDescriptorStream = GetContentDataFromId(FormatId))
                {
                    if (FileGroupDescriptorStream != null)
                    {
                        IntPtr FileGroupDescriptorAPointer = Marshal.AllocHGlobal(Convert.ToInt32(FileGroupDescriptorStream.Length));

                        try
                        {
                            Marshal.Copy(FileGroupDescriptorStream.ToArray(), 0, FileGroupDescriptorAPointer, Convert.ToInt32(FileGroupDescriptorStream.Length));

                            ulong TotalSize = 0;
                            ulong ItemCount = Convert.ToUInt64(Marshal.ReadInt32(FileGroupDescriptorAPointer));

                            IntPtr FileDescriptorPointer = (IntPtr)(FileGroupDescriptorAPointer.ToInt64() + Marshal.SizeOf<int>());

                            for (uint FileDescriptorIndex = 0; FileDescriptorIndex < ItemCount; FileDescriptorIndex++)
                            {
                                Shell32.FILEDESCRIPTOR FileDescriptor = Marshal.PtrToStructure<Shell32.FILEDESCRIPTOR>(FileDescriptorPointer);

                                if (!FileDescriptor.dwFileAttributes.HasFlag(FileFlagsAndAttributes.FILE_ATTRIBUTE_DIRECTORY))
                                {
                                    TotalSize += FileDescriptor.nFileSize;
                                }

                                FileDescriptorPointer = (IntPtr)(FileDescriptorPointer.ToInt64() + Marshal.SizeOf(FileDescriptor));
                            }

                            return new RemoteClipboardRelatedData(ItemCount, TotalSize);
                        }
                        finally
                        {
                            Marshal.FreeHGlobal(FileGroupDescriptorAPointer);
                        }
                    }
                    else
                    {
                        throw new InvalidDataException("Could not read the data of RemoteClipboardData");
                    }
                }
            }

            return null;
        }

        public static IEnumerable<RemoteClipboardData> GetRemoteClipboardData(CancellationToken CancelToken = default)
        {
            uint FILECONTENTSID = NativeClipboard.RegisterFormat(Shell32.ShellClipboardFormat.CFSTR_FILECONTENTS);
            uint FILEDESCRIPTIONWID = NativeClipboard.RegisterFormat(Shell32.ShellClipboardFormat.CFSTR_FILEDESCRIPTORW);
            uint FILEDESCRIPTIONAID = NativeClipboard.RegisterFormat(Shell32.ShellClipboardFormat.CFSTR_FILEDESCRIPTORA);

            uint FormatId = 0;

            if (NativeClipboard.IsFormatAvailable(FILEDESCRIPTIONWID))
            {
                FormatId = FILEDESCRIPTIONWID;
            }
            else if (NativeClipboard.IsFormatAvailable(FILEDESCRIPTIONAID))
            {
                FormatId = FILEDESCRIPTIONAID;
            }

            if (FormatId > 0)
            {
                using (MemoryStream FileGroupDescriptorStream = GetContentDataFromId(FormatId))
                {
                    if (FileGroupDescriptorStream != null)
                    {
                        IntPtr FileGroupDescriptorAPointer = Marshal.AllocHGlobal(Convert.ToInt32(FileGroupDescriptorStream.Length));

                        try
                        {
                            Marshal.Copy(FileGroupDescriptorStream.ToArray(), 0, FileGroupDescriptorAPointer, Convert.ToInt32(FileGroupDescriptorStream.Length));

                            int ItemCount = Marshal.ReadInt32(FileGroupDescriptorAPointer);

                            IntPtr FileDescriptorPointer = (IntPtr)(FileGroupDescriptorAPointer.ToInt64() + Marshal.SizeOf<int>());

                            for (int FileDescriptorIndex = 0; FileDescriptorIndex < ItemCount; FileDescriptorIndex++)
                            {
                                CancelToken.ThrowIfCancellationRequested();

                                Shell32.FILEDESCRIPTOR FileDescriptor = Marshal.PtrToStructure<Shell32.FILEDESCRIPTOR>(FileDescriptorPointer);

                                if (FileDescriptor.dwFileAttributes.HasFlag(FileFlagsAndAttributes.FILE_ATTRIBUTE_DIRECTORY))
                                {
                                    yield return new RemoteClipboardFolderData(FileDescriptor.cFileName);
                                }
                                else if (GetContentDataFromId(FILECONTENTSID, FileDescriptorIndex) is MemoryStream FileContentStream)
                                {
                                    yield return new RemoteClipboardFileData(FileDescriptor.cFileName, FileDescriptor.nFileSize, FileContentStream);
                                }
                                else
                                {
                                    throw new InvalidDataException($"{FileDescriptor.cFileName} is not found in the remote data object");
                                }

                                FileDescriptorPointer = (IntPtr)(FileDescriptorPointer.ToInt64() + Marshal.SizeOf(FileDescriptor));
                            }
                        }
                        finally
                        {
                            Marshal.FreeHGlobal(FileGroupDescriptorAPointer);
                        }
                    }
                    else
                    {
                        throw new InvalidDataException("Could not read the data of RemoteClipboardData");
                    }
                }
            }
            else
            {
                throw new InvalidDataException("Could not read the data of RemoteClipboardData");
            }
        }

        private static MemoryStream GetContentDataFromId(uint FormatId, int Index = -1)
        {
            if (NativeClipboard.IsFormatAvailable(FormatId))
            {
                try
                {
                    switch (NativeClipboard.GetData(FormatId, DVASPECT.DVASPECT_CONTENT, Index))
                    {
                        case Ole32.IStorage IStorageObject:
                            {
                                try
                                {
                                    //create a ILockBytes (unmanaged byte array) and then create a IStorage using the byte array as a backing store
                                    Ole32.CreateILockBytesOnHGlobal(IntPtr.Zero, true, out Ole32.ILockBytes LockBytes);
                                    Ole32.StgCreateDocfileOnILockBytes(LockBytes, STGM.STGM_READWRITE | STGM.STGM_SHARE_EXCLUSIVE | STGM.STGM_CREATE, ppstgOpen: out Ole32.IStorage IStorageObjectCopy);

                                    try
                                    {
                                        //copy the returned IStorage into the new IStorage
                                        IStorageObject.CopyTo(snbExclude: IntPtr.Zero, pstgDest: IStorageObjectCopy);
                                        LockBytes.Flush();
                                        IStorageObjectCopy.Commit(Ole32.STGC.STGC_DEFAULT);

                                        //get the STATSTG of the LockBytes to determine how many bytes were written to it
                                        LockBytes.Stat(out STATSTG LockBytesStat, Ole32.STATFLAG.STATFLAG_NONAME);

                                        IntPtr LockBytesContentPtr = Marshal.AllocCoTaskMem(Convert.ToInt32(LockBytesStat.cbSize));

                                        try
                                        {
                                            LockBytes.ReadAt(0, LockBytesContentPtr, Convert.ToUInt32(LockBytesStat.cbSize), out _);

                                            byte[] LockBytesContent = new byte[(Convert.ToInt32(LockBytesStat.cbSize))];

                                            Marshal.Copy(LockBytesContentPtr, LockBytesContent, 0, LockBytesContent.Length);

                                            return new MemoryStream(LockBytesContent);
                                        }
                                        finally
                                        {
                                            Marshal.FreeCoTaskMem(LockBytesContentPtr);
                                        }
                                    }
                                    finally
                                    {
                                        Marshal.ReleaseComObject(IStorageObjectCopy);
                                        Marshal.ReleaseComObject(LockBytes);
                                    }
                                }
                                finally
                                {
                                    Marshal.ReleaseComObject(IStorageObject);
                                }
                            }
                        case IStream IStreamObject:
                            {
                                try
                                {
                                    IStreamObject.Stat(out STATSTG iStreamStat, 0);

                                    byte[] IStreamContent = new byte[(Convert.ToInt32(iStreamStat.cbSize))];

                                    IStreamObject.Read(IStreamContent, IStreamContent.Length, IntPtr.Zero);

                                    return new MemoryStream(IStreamContent);
                                }
                                finally
                                {
                                    Marshal.ReleaseComObject(IStreamObject);
                                }
                            }
                        case MemoryStream Stream:
                            {
                                return Stream;
                            }
                        case byte[] Bytes:
                            {
                                return new MemoryStream(Bytes);
                            }
                    }
                }
                catch (Exception ex)
                {
                    LogTracer.Log(ex, "Could not get the contents of RemoteClipboardData");
                }
            }

            return null;
        }
    }
}
