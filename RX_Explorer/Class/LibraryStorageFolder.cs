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
                    StorageFolder Folder = await StorageFolder.GetFolderFromPathAsync(Path);
                    return new LibraryStorageFolder(LibType, await Folder.GetNativeFileDataAsync());
                }
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"Could not create the library folder, lib type: {Enum.GetName(typeof(LibraryType), LibType)}, path: {Path}");
            }

            return null;
        }

        private LibraryStorageFolder(LibraryType LibType, NativeFileData Data) : base(Data)
        {
            this.LibType = LibType;
        }
    }
}
