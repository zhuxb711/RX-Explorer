using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using Vanara.PInvoke;
using Vanara.Windows.Shell;

namespace FullTrustProcess
{
    public static class RemoteDataObject
    {
        public static IReadOnlyList<RemoteClipboardDataPackage> GetRemoteClipboardData()
        {
            List<RemoteClipboardDataPackage> Result = new List<RemoteClipboardDataPackage>();

            uint FILEDESCRIPTIONWID = NativeClipboard.RegisterFormat(Shell32.ShellClipboardFormat.CFSTR_FILEDESCRIPTORW);
            uint FILEDESCRIPTIONAID = NativeClipboard.RegisterFormat(Shell32.ShellClipboardFormat.CFSTR_FILEDESCRIPTORA);
            uint FILECONTENTSID = NativeClipboard.RegisterFormat(Shell32.ShellClipboardFormat.CFSTR_FILECONTENTS);

            uint FormatId;

            if (NativeClipboard.IsFormatAvailable(FILEDESCRIPTIONWID))
            {
                FormatId = FILEDESCRIPTIONWID;
            }
            else if (NativeClipboard.IsFormatAvailable(FILEDESCRIPTIONAID))
            {
                FormatId = FILEDESCRIPTIONAID;
            }
            else
            {
                return Result;
            }

            if (NativeClipboard.GetData(FormatId) is MemoryStream FileGroupDescriptorStream)
            {
                try
                {
                    byte[] FileGroupDescriptorBytes = FileGroupDescriptorStream.ToArray();

                    IntPtr FileGroupDescriptorAPointer = Marshal.AllocCoTaskMem(FileGroupDescriptorBytes.Length);

                    try
                    {
                        Marshal.Copy(FileGroupDescriptorBytes, 0, FileGroupDescriptorAPointer, FileGroupDescriptorBytes.Length);

                        int ItemCount = Marshal.ReadInt32(FileGroupDescriptorAPointer);

                        IntPtr FileDescriptorPointer = (IntPtr)(FileGroupDescriptorAPointer.ToInt64() + Marshal.SizeOf(ItemCount));

                        for (int FileDescriptorIndex = 0; FileDescriptorIndex < ItemCount; FileDescriptorIndex++)
                        {
                            Shell32.FILEDESCRIPTOR FileDescriptor = Marshal.PtrToStructure<Shell32.FILEDESCRIPTOR>(FileDescriptorPointer);

                            if (FileDescriptor.dwFileAttributes.HasFlag(FileFlagsAndAttributes.FILE_ATTRIBUTE_DIRECTORY))
                            {
                                Result.Add(new RemoteClipboardDataPackage(FileDescriptor.cFileName, RemoteClipboardStorageType.Folder, null));
                            }
                            else
                            {
                                Result.Add(new RemoteClipboardDataPackage(FileDescriptor.cFileName, RemoteClipboardStorageType.File, GetContentData(FILECONTENTSID, FileDescriptorIndex)));
                            }

                            FileDescriptorPointer = (IntPtr)(FileDescriptorPointer.ToInt64() + Marshal.SizeOf(FileDescriptor));
                        }
                    }
                    finally
                    {
                        Marshal.FreeCoTaskMem(FileGroupDescriptorAPointer);
                    }
                }
                finally
                {
                    FileGroupDescriptorStream.Dispose();
                }
            }

            return Result;
        }

        /// <summary>
        /// Retrieves the data associated with the specified data format at the specified index.
        /// </summary>
        /// <param name="Format">The format of the data to retrieve. See <see cref="T:System.Windows.DataFormats"></see> for predefined formats.</param>
        /// <param name="Index">The index of the data to retrieve.</param>
        /// <returns>
        /// A <see cref="MemoryStream"/> containing the raw data for the specified data format at the specified index.
        /// </returns>
        private static MemoryStream GetContentData(uint FormatId, int Index)
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

                                int CbSize = Convert.ToInt32(LockBytesStat.cbSize);

                                IntPtr LockBytesContentPtr = Marshal.AllocCoTaskMem(CbSize);

                                try
                                {
                                    LockBytes.ReadAt(0, LockBytesContentPtr, Convert.ToUInt32(LockBytesStat.cbSize), out _);

                                    byte[] LockBytesContent = new byte[CbSize];

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
                default:
                    {
                        return null;
                    }
            }
        }
    }
}
