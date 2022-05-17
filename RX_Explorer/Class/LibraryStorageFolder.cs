using System;
using System.Threading.Tasks;
using Windows.Storage;

namespace RX_Explorer.Class
{
    public sealed class LibraryStorageFolder : FileSystemStorageFolder
    {
        public LibraryType LibType { get; }

        protected override bool ShouldGenerateThumbnail => true;

        public static async Task<LibraryStorageFolder> CreateAsync(LibraryType LibType, string Path)
        {
            try
            {
                NativeFileData Data = NativeWin32API.GetStorageItemRawData(Path);

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
                LogTracer.Log(ex, $"Could not create the library folder, lib type: {Enum.GetName(typeof(LibraryType), LibType)}, path: {Path}");
            }

            return null;
        }

        private LibraryStorageFolder(LibraryType LibType, StorageFolder Folder) : base(Folder)
        {
            this.LibType = LibType;
        }

        private LibraryStorageFolder(LibraryType LibType, NativeFileData Data) : base(Data)
        {
            this.LibType = LibType;
        }
    }
}
