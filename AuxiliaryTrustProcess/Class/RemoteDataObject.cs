using SharedLibrary;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.ComTypes;
using System.Threading;
using Vanara.InteropServices;
using Vanara.PInvoke;
using Vanara.Windows.Shell;

namespace AuxiliaryTrustProcess.Class
{
    public static class RemoteDataObject
    {
        public static RemoteClipboardRelatedData GetRemoteClipboardRelatedInformation()
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
                Shell32.FILEGROUPDESCRIPTOR FileGroupDescriptor = NativeClipboard.CurrentDataObject.GetData<Shell32.FILEGROUPDESCRIPTOR>(FormatId);
                return new RemoteClipboardRelatedData(FileGroupDescriptor.cItems, Convert.ToUInt64(FileGroupDescriptor.fgd.Sum((Descriptor) => (long)Descriptor.nFileSize)));
            }

            return null;
        }

        public static IEnumerable<RemoteClipboardData> GetRemoteClipboardData(CancellationToken CancelToken = default)
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
                IDataObject DataObject = NativeClipboard.CurrentDataObject;

                Shell32.FILEGROUPDESCRIPTOR FileGroupDescriptor = DataObject.GetData<Shell32.FILEGROUPDESCRIPTOR>(FormatId);

                if (NativeClipboard.IsFormatAvailable(Shell32.ShellClipboardFormat.CFSTR_FILECONTENTS))
                {
                    for (int Index = 0; Index < FileGroupDescriptor.cItems; Index++)
                    {
                        CancelToken.ThrowIfCancellationRequested();

                        Shell32.FILEDESCRIPTOR FileDescriptor = FileGroupDescriptor.fgd[Index];

                        if (FileDescriptor.dwFileAttributes.HasFlag(FileFlagsAndAttributes.FILE_ATTRIBUTE_DIRECTORY))
                        {
                            yield return new RemoteClipboardFolderData(FileDescriptor.cFileName);
                        }
                        else
                        {
                            Stream ContentStream = DataObject.GetData(Shell32.ShellClipboardFormat.CFSTR_FILECONTENTS, DVASPECT.DVASPECT_CONTENT, Index) switch
                            {
                                IStream Stream => new ComStream(Stream),
                                Ole32.IStorage Storage => new ComStream(Storage.AsStream()),
                                MemoryStream MStream => MStream,
                                byte[] RawData => new MemoryStream(RawData),
                                _ => throw new NotSupportedException()
                            };

                            yield return new RemoteClipboardFileData(FileDescriptor.cFileName, FileDescriptor.nFileSize, ContentStream);
                        }
                    }

                    yield break;
                }
            }

            throw new InvalidDataException("Could not read the data of RemoteClipboardData");
        }
    }
}
