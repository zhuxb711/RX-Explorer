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
                try
                {
                    return new LibraryStorageFolder(LibType, Path);
                }
                catch (LocationNotAvailableException)
                {
                    return new LibraryStorageFolder(LibType, await StorageFolder.GetFolderFromPathAsync(Path));
                }
            }
            catch (Exception)
            {
                return null;
            }
        }

        private LibraryStorageFolder(LibraryType LibType, StorageFolder Folder) : base(Folder)
        {
            this.LibType = LibType;
        }

        private LibraryStorageFolder(LibraryType LibType, string Path) : base(Win32_Native_API.GetStorageItemRawData(Path))
        {
            this.LibType = LibType;
        }
    }
}
