using SharedLibrary;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Threading;
using Vanara.InteropServices;
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
                using (Stream FileGroupDescriptorStream = GetContentStream(FormatId))
                using (BinaryReader GroupDescriptorReader = new BinaryReader(FileGroupDescriptorStream))
                {
                    IntPtr FileGroupDescriptorAPointer = Marshal.AllocHGlobal(Convert.ToInt32(FileGroupDescriptorStream.Length));

                    try
                    {
                        Marshal.Copy(GroupDescriptorReader.ReadBytes(Convert.ToInt32(FileGroupDescriptorStream.Length)), 0, FileGroupDescriptorAPointer, Convert.ToInt32(FileGroupDescriptorStream.Length));

                        ulong TotalSize = 0;
                        ulong ItemCount = Convert.ToUInt64(Marshal.ReadInt32(FileGroupDescriptorAPointer));

                        IntPtr FileDescriptorPointer = new IntPtr(FileGroupDescriptorAPointer.ToInt64() + Marshal.SizeOf<int>());

                        for (uint FileDescriptorIndex = 0; FileDescriptorIndex < ItemCount; FileDescriptorIndex++)
                        {
                            Shell32.FILEDESCRIPTOR FileDescriptor = Marshal.PtrToStructure<Shell32.FILEDESCRIPTOR>(FileDescriptorPointer);

                            if (!FileDescriptor.dwFileAttributes.HasFlag(FileFlagsAndAttributes.FILE_ATTRIBUTE_DIRECTORY))
                            {
                                TotalSize += FileDescriptor.nFileSize;
                            }

                            FileDescriptorPointer = new IntPtr(FileDescriptorPointer.ToInt64() + Marshal.SizeOf(FileDescriptor));
                        }

                        return new RemoteClipboardRelatedData(ItemCount, TotalSize);
                    }
                    finally
                    {
                        Marshal.FreeHGlobal(FileGroupDescriptorAPointer);
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
                using (Stream FileGroupDescriptorStream = GetContentStream(FormatId))
                using (BinaryReader GroupDescriptorReader = new BinaryReader(FileGroupDescriptorStream))
                {
                    IntPtr FileGroupDescriptorAPointer = Marshal.AllocHGlobal(Convert.ToInt32(FileGroupDescriptorStream.Length));

                    try
                    {
                        Marshal.Copy(GroupDescriptorReader.ReadBytes(Convert.ToInt32(FileGroupDescriptorStream.Length)), 0, FileGroupDescriptorAPointer, Convert.ToInt32(FileGroupDescriptorStream.Length));

                        int ItemCount = Marshal.ReadInt32(FileGroupDescriptorAPointer);

                        IntPtr FileDescriptorPointer = new IntPtr(FileGroupDescriptorAPointer.ToInt64() + Marshal.SizeOf<int>());

                        for (int FileDescriptorIndex = 0; FileDescriptorIndex < ItemCount; FileDescriptorIndex++)
                        {
                            CancelToken.ThrowIfCancellationRequested();

                            Shell32.FILEDESCRIPTOR FileDescriptor = Marshal.PtrToStructure<Shell32.FILEDESCRIPTOR>(FileDescriptorPointer);

                            if (FileDescriptor.dwFileAttributes.HasFlag(FileFlagsAndAttributes.FILE_ATTRIBUTE_DIRECTORY))
                            {
                                yield return new RemoteClipboardFolderData(FileDescriptor.cFileName);
                            }
                            else
                            {
                                yield return new RemoteClipboardFileData(FileDescriptor.cFileName, FileDescriptor.nFileSize, GetContentStream(FILECONTENTSID, FileDescriptorIndex));
                            }

                            FileDescriptorPointer = new IntPtr(FileDescriptorPointer.ToInt64() + Marshal.SizeOf(FileDescriptor));
                        }
                    }
                    finally
                    {
                        Marshal.FreeHGlobal(FileGroupDescriptorAPointer);
                    }
                }
            }
            else
            {
                throw new InvalidDataException("Could not read the data of RemoteClipboardData");
            }
        }

        private static Stream GetContentStream(uint FormatId, int Index = -1)
        {
            return NativeClipboard.GetData(FormatId, DVASPECT.DVASPECT_CONTENT, Index) switch
            {
                IStream Stream => new ComStream(Stream),
                Ole32.IStorage Storage => new ComStream(Storage.AsStream()),
                MemoryStream MStream => MStream,
                byte[] RawData => new MemoryStream(RawData),
                _ => throw new NotSupportedException()
            };
        }
    }
}
