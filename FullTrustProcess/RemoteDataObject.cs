using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Windows.Forms;
using Vanara.PInvoke;
using STATSTG = System.Runtime.InteropServices.ComTypes.STATSTG;

namespace FullTrustProcess
{
    public class RemoteDataObject : System.Windows.Forms.IDataObject
    {
        #region Property(s)

        /// <summary>
        /// Holds the <see cref="System.Windows.IDataObject"/> that this class is wrapping
        /// </summary>
        private System.Windows.Forms.IDataObject underlyingDataObject;

        /// <summary>
        /// Holds the <see cref="System.Runtime.InteropServices.ComTypes.IDataObject"/> interface to the <see cref="System.Windows.IDataObject"/> that this class is wrapping.
        /// </summary>
        private System.Runtime.InteropServices.ComTypes.IDataObject comUnderlyingDataObject;

        /// <summary>
        /// Holds the internal ole <see cref="System.Windows.IDataObject"/> to the <see cref="System.Windows.IDataObject"/> that this class is wrapping.
        /// </summary>
        private System.Windows.Forms.IDataObject oleUnderlyingDataObject;

        /// <summary>
        /// Holds the <see cref="MethodInfo"/> of the "GetDataFromHGLOBAL" method of the internal ole <see cref="System.Windows.IDataObject"/>.
        /// </summary>
        private MethodInfo getDataFromHGLOBALMethod;

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Initializes a new instance of the <see cref="OutlookDataObject"/> class.
        /// </summary>
        /// <param name="underlyingDataObject">The underlying data object to wrap.</param>
        public RemoteDataObject(System.Windows.Forms.IDataObject underlyingDataObject)
        {
            //get the underlying dataobject and its ComType IDataObject interface to it
            this.underlyingDataObject = underlyingDataObject;
            comUnderlyingDataObject = (System.Runtime.InteropServices.ComTypes.IDataObject)this.underlyingDataObject;

            //get the internal ole dataobject and its GetDataFromHGLOBAL so it can be called later
            FieldInfo innerDataField = this.underlyingDataObject.GetType().GetField("innerData", BindingFlags.NonPublic | BindingFlags.Instance);
            oleUnderlyingDataObject = (System.Windows.Forms.IDataObject)innerDataField.GetValue(this.underlyingDataObject);
            getDataFromHGLOBALMethod = oleUnderlyingDataObject.GetType().GetMethod("GetDataFromHGLOBAL", BindingFlags.NonPublic | BindingFlags.Instance);
        }

        #endregion

        #region IDataObject Members

        /// <summary>
        /// Retrieves the data associated with the specified class type format.
        /// </summary>
        /// <param name="format">A <see cref="T:System.Type"></see> representing the format of the data to retrieve. See <see cref="T:System.Windows.DataFormats"></see> for predefined formats.</param>
        /// <returns>
        /// The data associated with the specified format, or null.
        /// </returns>
        public object GetData(Type format)
        {
            return GetData(format.FullName);
        }

        /// <summary>
        /// Retrieves the data associated with the specified data format.
        /// </summary>
        /// <param name="format">The format of the data to retrieve. See <see cref="T:System.Windows.DataFormats"></see> for predefined formats.</param>
        /// <returns>
        /// The data associated with the specified format, or null.
        /// </returns>
        public object GetData(string format)
        {
            return GetData(format, true);
        }

        /// <summary>
        /// Retrieves the data associated with the specified data format, using a Boolean to determine whether to convert the data to the format.
        /// </summary>
        /// <param name="Format">The format of the data to retrieve. See <see cref="T:System.Windows.DataFormats"></see> for predefined formats.</param>
        /// <param name="AutoConvert">true to convert the data to the specified format; otherwise, false.</param>
        /// <returns>
        /// The data associated with the specified format, or null.
        /// </returns>
        public object GetData(string Format, bool AutoConvert)
        {
            //handle the "FileGroupDescriptor" and "FileContents" format request in this class otherwise pass through to underlying IDataObject 
            switch (Format)
            {
                case Shell32.ShellClipboardFormat.CFSTR_FILEDESCRIPTORA:
                case Shell32.ShellClipboardFormat.CFSTR_FILEDESCRIPTORW:
                    {
                        //override the default handling of FileGroupDescriptor which returns a
                        //MemoryStream and instead return a string array of file names
                        //use the underlying IDataObject to get the FileGroupDescriptor as a MemoryStream

                        byte[] FileGroupDescriptorBytes = null;

                        using (MemoryStream FileGroupDescriptorStream = (MemoryStream)underlyingDataObject.GetData(Format, AutoConvert))
                        {
                            FileGroupDescriptorBytes = new byte[FileGroupDescriptorStream.Length];
                            FileGroupDescriptorStream.Read(FileGroupDescriptorBytes, 0, FileGroupDescriptorBytes.Length);
                        }

                        //copy the file group descriptor into unmanaged memory 
                        IntPtr FileGroupDescriptorAPointer = Marshal.AllocHGlobal(FileGroupDescriptorBytes.Length);

                        try
                        {
                            Marshal.Copy(FileGroupDescriptorBytes, 0, FileGroupDescriptorAPointer, FileGroupDescriptorBytes.Length);

                            ////marshal the unmanaged memory to to FILEGROUPDESCRIPTORA struct
                            //FIX FROM - https://stackoverflow.com/questions/27173844/accessviolationexception-after-copying-a-file-from-inside-a-zip-archive-to-the-c
                            int ItemCount = Marshal.ReadInt32(FileGroupDescriptorAPointer);

                            //create a new array to store file names in of the number of items in the file group descriptor
                            string[] FileNames = new string[ItemCount];

                            //get the pointer to the first file descriptor
                            IntPtr FileDescriptorPointer = (IntPtr)(FileGroupDescriptorAPointer.ToInt64() + Marshal.SizeOf(ItemCount));

                            //loop for the number of files acording to the file group descriptor
                            for (int FileDescriptorIndex = 0; FileDescriptorIndex < ItemCount; FileDescriptorIndex++)
                            {
                                //marshal the pointer top the file descriptor as a FILEDESCRIPTOR struct and get the file name
                                Shell32.FILEDESCRIPTOR FileDescriptor = Marshal.PtrToStructure<Shell32.FILEDESCRIPTOR>(FileDescriptorPointer);
                                
                                FileNames[FileDescriptorIndex] = FileDescriptor.cFileName;

                                //move the file descriptor pointer to the next file descriptor
                                FileDescriptorPointer = (IntPtr)(FileDescriptorPointer.ToInt64() + Marshal.SizeOf(FileDescriptor));
                            }

                            //return the array of filenames
                            return FileNames;
                        }
                        finally
                        {
                            Marshal.FreeHGlobal(FileGroupDescriptorAPointer);
                        }
                    }
                case Shell32.ShellClipboardFormat.CFSTR_FILECONTENTS:
                    {
                        //override the default handling of FileContents which returns the
                        //contents of the first file as a memory stream and instead return                    
                        //a array of MemoryStreams containing the data to each file dropped                    
                        //
                        // FILECONTENTS requires a companion FILEGROUPDESCRIPTOR to be                     
                        // available so we bail out if we don't find one in the data object.

                        string FormatName = string.Empty;

                        if (GetDataPresent(Shell32.ShellClipboardFormat.CFSTR_FILEDESCRIPTORW))
                        {
                            FormatName = Shell32.ShellClipboardFormat.CFSTR_FILEDESCRIPTORW;
                        }
                        else if (GetDataPresent(Shell32.ShellClipboardFormat.CFSTR_FILEDESCRIPTORA))
                        {
                            FormatName = Shell32.ShellClipboardFormat.CFSTR_FILEDESCRIPTORA;
                        }

                        if (!string.IsNullOrEmpty(FormatName))
                        {
                            //get the array of filenames which lets us know how many file contents exist                    
                            if (GetData(FormatName) is string[] FileContentNames)
                            {
                                //create a MemoryStream array to store the file contents
                                MemoryStream[] FileContents = new MemoryStream[FileContentNames.Length];

                                //loop for the number of files acording to the file names
                                for (int FileIndex = 0; FileIndex < FileContentNames.Length; FileIndex++)
                                {
                                    //get the data at the file index and store in array
                                    FileContents[FileIndex] = GetData(Format, FileIndex);
                                }

                                //return array of MemoryStreams containing file contents
                                return FileContents;
                            }
                            else
                            {
                                return null;
                            }
                        }
                        else
                        {
                            return null;
                        }
                    }
                default:
                    {
                        //use underlying IDataObject to handle getting of data
                        return underlyingDataObject.GetData(Format, AutoConvert);
                    }
            }
        }

        /// <summary>
        /// Retrieves the data associated with the specified data format at the specified index.
        /// </summary>
        /// <param name="Format">The format of the data to retrieve. See <see cref="T:System.Windows.DataFormats"></see> for predefined formats.</param>
        /// <param name="Index">The index of the data to retrieve.</param>
        /// <returns>
        /// A <see cref="MemoryStream"/> containing the raw data for the specified data format at the specified index.
        /// </returns>
        public MemoryStream GetData(string Format, int Index)
        {
            //create a FORMATETC struct to request the data with
            FORMATETC Formatetc = new FORMATETC
            {
                cfFormat = (short)DataFormats.GetFormat(Format).Id,
                dwAspect = DVASPECT.DVASPECT_CONTENT,
                lindex = Index,
                ptd = new IntPtr(0),
                tymed = TYMED.TYMED_ISTREAM | TYMED.TYMED_ISTORAGE | TYMED.TYMED_HGLOBAL
            };

            //using the Com IDataObject interface get the data using the defined FORMATETC
            comUnderlyingDataObject.GetData(ref Formatetc, out STGMEDIUM Medium);

            //retrieve the data depending on the returned store type
            switch (Medium.tymed)
            {
                case TYMED.TYMED_ISTORAGE:
                    {
                        //to handle a IStorage it needs to be written into a second unmanaged
                        //memory mapped storage and then the data can be read from memory into
                        //a managed byte and returned as a MemoryStream

                        try
                        {
                            //marshal the returned pointer to a IStorage object
                            Ole32.IStorage IStorageObject = (Ole32.IStorage)Marshal.GetObjectForIUnknown(Medium.unionmember);

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

                                    IntPtr LockBytesContentPtr = Marshal.AllocHGlobal(CbSize);

                                    try
                                    {
                                        LockBytes.ReadAt(0, LockBytesContentPtr, Convert.ToUInt32(LockBytesStat.cbSize), out _);

                                        byte[] LockBytesContent = new byte[CbSize];

                                        Marshal.Copy(LockBytesContentPtr, LockBytesContent, 0, LockBytesContent.Length);

                                        return new MemoryStream(LockBytesContent);
                                    }
                                    finally
                                    {
                                        Marshal.FreeHGlobal(LockBytesContentPtr);
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
                        finally
                        {
                            Marshal.Release(Medium.unionmember);
                        }
                    }
                case TYMED.TYMED_ISTREAM:
                    {
                        //to handle a IStream it needs to be read into a managed byte and
                        //returned as a MemoryStream

                        IStream IStreamObject = (IStream)Marshal.GetObjectForIUnknown(Medium.unionmember);

                        try
                        {
                            //get the STATSTG of the IStream to determine how many bytes are in it
                            IStreamObject.Stat(out STATSTG iStreamStat, 0);

                            byte[] IStreamContent = new byte[(Convert.ToInt32(iStreamStat.cbSize))];

                            IStreamObject.Read(IStreamContent, IStreamContent.Length, IntPtr.Zero);

                            return new MemoryStream(IStreamContent);
                        }
                        finally
                        {
                            Marshal.Release(Medium.unionmember);
                            Marshal.ReleaseComObject(IStreamObject);
                        }
                    }
                case TYMED.TYMED_HGLOBAL:
                    {
                        //to handle a HGlobal the exisitng "GetDataFromHGLOBAL" method is invoked via
                        //reflection

                        try
                        {
                            return (MemoryStream)getDataFromHGLOBALMethod.Invoke(oleUnderlyingDataObject, new object[] { DataFormats.GetFormat(Formatetc.cfFormat).Name, Medium.unionmember });
                        }
                        finally
                        {
                            Marshal.Release(Medium.unionmember);
                        }
                    }
                default:
                    {
                        return null;
                    }
            }
        }

        /// <summary>
        /// Determines whether data stored in this instance is associated with, or can be converted to, the specified format.
        /// </summary>
        /// <param name="format">A <see cref="T:System.Type"></see> representing the format for which to check. See <see cref="T:System.Windows.DataFormats"></see> for predefined formats.</param>
        /// <returns>
        /// true if data stored in this instance is associated with, or can be converted to, the specified format; otherwise, false.
        /// </returns>
        public bool GetDataPresent(Type format)
        {
            return underlyingDataObject.GetDataPresent(format);
        }

        /// <summary>
        /// Determines whether data stored in this instance is associated with, or can be converted to, the specified format.
        /// </summary>
        /// <param name="format">The format for which to check. See <see cref="T:System.Windows.DataFormats"></see> for predefined formats.</param>
        /// <returns>
        /// true if data stored in this instance is associated with, or can be converted to, the specified format; otherwise false.
        /// </returns>
        public bool GetDataPresent(string format)
        {
            return underlyingDataObject.GetDataPresent(format);
        }

        /// <summary>
        /// Determines whether data stored in this instance is associated with the specified format, using a Boolean value to determine whether to convert the data to the format.
        /// </summary>
        /// <param name="format">The format for which to check. See <see cref="T:System.Windows.DataFormats"></see> for predefined formats.</param>
        /// <param name="autoConvert">true to determine whether data stored in this instance can be converted to the specified format; false to check whether the data is in the specified format.</param>
        /// <returns>
        /// true if the data is in, or can be converted to, the specified format; otherwise, false.
        /// </returns>
        public bool GetDataPresent(string format, bool autoConvert)
        {
            return underlyingDataObject.GetDataPresent(format, autoConvert);
        }

        /// <summary>
        /// Returns a list of all formats that data stored in this instance is associated with or can be converted to.
        /// </summary>
        /// <returns>
        /// An array of the names that represents a list of all formats that are supported by the data stored in this object.
        /// </returns>
        public string[] GetFormats()
        {
            return underlyingDataObject.GetFormats();
        }

        /// <summary>
        /// Gets a list of all formats that data stored in this instance is associated with or can be converted to, using a Boolean value to determine whether to retrieve all formats that the data can be converted to or only native data formats.
        /// </summary>
        /// <param name="autoConvert">true to retrieve all formats that data stored in this instance is associated with or can be converted to; false to retrieve only native data formats.</param>
        /// <returns>
        /// An array of the names that represents a list of all formats that are supported by the data stored in this object.
        /// </returns>
        public string[] GetFormats(bool autoConvert)
        {
            return underlyingDataObject.GetFormats(autoConvert);
        }

        /// <summary>
        /// Stores the specified data in this instance, using the class of the data for the format.
        /// </summary>
        /// <param name="data">The data to store.</param>
        public void SetData(object data)
        {
            underlyingDataObject.SetData(data);
        }

        /// <summary>
        /// Stores the specified data and its associated class type in this instance.
        /// </summary>
        /// <param name="format">A <see cref="T:System.Type"></see> representing the format associated with the data. See <see cref="T:System.Windows.DataFormats"></see> for predefined formats.</param>
        /// <param name="data">The data to store.</param>
        public void SetData(Type format, object data)
        {
            underlyingDataObject.SetData(format, data);
        }

        /// <summary>
        /// Stores the specified data and its associated format in this instance.
        /// </summary>
        /// <param name="format">The format associated with the data. See <see cref="T:System.Windows.DataFormats"></see> for predefined formats.</param>
        /// <param name="data">The data to store.</param>
        public void SetData(string format, object data)
        {
            underlyingDataObject.SetData(format, data);
        }

        /// <summary>
        /// Stores the specified data and its associated format in this instance, using a Boolean value to specify whether the data can be converted to another format.
        /// </summary>
        /// <param name="format">The format associated with the data. See <see cref="T:System.Windows.DataFormats"></see> for predefined formats.</param>
        /// <param name="autoConvert">true to allow the data to be converted to another format; otherwise, false.</param>
        /// <param name="data">The data to store.</param>
        public void SetData(string format, bool autoConvert, object data)
        {
            underlyingDataObject.SetData(format, autoConvert, data);
        }

        #endregion
    }
}
