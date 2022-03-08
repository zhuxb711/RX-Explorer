using ShareClassLibrary;
using System;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.UI.Xaml.Media.Imaging;

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
                if (Path.StartsWith(@"\\?\"))
                {
                    using (FullTrustProcessController.ExclusiveUsage Exclusive = await FullTrustProcessController.GetAvailableControllerAsync())
                    {
                        if (await Exclusive.Controller.GetMTPItemDataAsync(Path) is MTPFileData Data)
                        {
                            return new LibraryStorageFolder(LibType, Data);
                        }
                    }
                }
                else
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
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, $"Could not create the library folder, lib type: {Enum.GetName(typeof(LibraryType), LibType)}, path: {Path}");
            }

            return null;
        }

        public override Task<IStorageItem> GetStorageItemAsync()
        {
            if (!Path.StartsWith(@"\\?\"))
            {
                return base.GetStorageItemAsync();
            }

            return Task.FromResult<IStorageItem>(null);
        }

        protected override Task<BitmapImage> GetThumbnailCoreAsync(ThumbnailMode Mode)
        {
            if (!Path.StartsWith(@"\\?\"))
            {
                return base.GetThumbnailCoreAsync(Mode);
            }

            return Task.FromResult<BitmapImage>(null);
        }

        private LibraryStorageFolder(LibraryType LibType, MTPFileData Data) : base(Data)
        {
            this.LibType = LibType;
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
