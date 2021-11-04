using System;
using System.Threading.Tasks;
using Windows.Storage;

namespace RX_Explorer.Class
{
    public sealed class LibraryStorageFolder : FileSystemStorageFolder
    {
        public LibraryType LibType { get; }

        protected override bool ShouldGenerateThumbnail
        {
            get
            {
                return true;
            }
        }

        public static async Task<LibraryStorageFolder> CreateAsync(LibraryType LibType, string Path)
        {
            try
            {
                Win32_File_Data Data = await Task.Run(() => Win32_Native_API.GetStorageItemRawData(Path));

                if (Data.IsDataValid)
                {
                    return new LibraryStorageFolder(LibType, Data);
                }
                else
                {
                    return new LibraryStorageFolder(LibType, await StorageFolder.GetFolderFromPathAsync(Path));
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, "Could not create the library folder");
                return null;
            }
        }

        private LibraryStorageFolder(LibraryType LibType, StorageFolder Folder) : base(Folder)
        {
            this.LibType = LibType;
        }

        private LibraryStorageFolder(LibraryType LibType, Win32_File_Data Data) : base(Data)
        {
            this.LibType = LibType;
        }
    }
}
