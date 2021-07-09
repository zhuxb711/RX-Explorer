using System;
using System.Threading.Tasks;
using Windows.Storage;

namespace RX_Explorer.Class
{
    public sealed class LibraryStorageFolder : FileSystemStorageFolder
    {
        public LibraryType LibType { get; }

        public static async Task<LibraryStorageFolder> CreateAsync(LibraryType LibType, string Path)
        {
            try
            {
                if (WIN_Native_API.CheckLocationAvailability(Path))
                {
                    return new LibraryStorageFolder(LibType, Path, WIN_Native_API.GetStorageItemRawData(Path));
                }
                else
                {
                    StorageFolder Folder = await StorageFolder.GetFolderFromPathAsync(Path);

                    return new LibraryStorageFolder(LibType, Folder, await Folder.GetModifiedTimeAsync());
                }
            }
            catch (Exception)
            {
                return null;
            }
        }

        public static async Task<LibraryStorageFolder> CreateAsync(LibraryType LibType, StorageFolder Folder)
        {
            try
            {
                return new LibraryStorageFolder(LibType, Folder, await Folder.GetModifiedTimeAsync());
            }
            catch (Exception)
            {
                return null;
            }
        }

        public override async Task LoadAsync()
        {
            try
            {
                await LoadThumbnailAsync(ThumbnailMode);
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"An exception was threw in {nameof(LoadAsync)}, StorageType: {GetType().FullName}, Path: {Path}");
            }
            finally
            {
                OnPropertyChanged(nameof(Thumbnail));
            }
        }

        private LibraryStorageFolder(LibraryType LibType, StorageFolder Folder, DateTimeOffset ModifiedTime) : base(Folder, ModifiedTime)
        {
            this.LibType = LibType;
        }

        private LibraryStorageFolder(LibraryType LibType, string Path, WIN_Native_API.WIN32_FIND_DATA Data) : base(Path, Data)
        {
            this.LibType = LibType;
        }
    }
}
