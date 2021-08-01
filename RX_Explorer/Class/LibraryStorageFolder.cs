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
                return new LibraryStorageFolder(LibType, Path);
            }
            catch (LocationNotAvailableException)
            {
                StorageFolder Folder = await StorageFolder.GetFolderFromPathAsync(Path);
                return new LibraryStorageFolder(LibType, Folder, await Folder.GetModifiedTimeAsync());
            }
            catch
            {
                return null;
            }
        }

        public static async Task<LibraryStorageFolder> CreateAsync(LibraryType LibType, StorageFolder Folder)
        {
            try
            {
                if (Folder != null)
                {
                    return new LibraryStorageFolder(LibType, Folder, await Folder.GetModifiedTimeAsync());
                }
                else
                {
                    return null;
                }
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

        private LibraryStorageFolder(LibraryType LibType, string Path) : base(Win32_Native_API.GetStorageItemRawData(Path))
        {
            this.LibType = LibType;
        }
    }
}
