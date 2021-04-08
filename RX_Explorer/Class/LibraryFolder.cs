using System;
using System.IO;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.UI.Xaml.Media.Imaging;

namespace RX_Explorer.Class
{
    /// <summary>
    /// 提供对文件夹库的UI显示支持
    /// </summary>
    public sealed class LibraryFolder
    {
        /// <summary>
        /// 文件夹名称
        /// </summary>
        public string Name
        {
            get
            {
                return Folder.DisplayName;
            }
        }

        /// <summary>
        /// 文件夹缩略图
        /// </summary>
        public BitmapImage Thumbnail { get; private set; }

        /// <summary>
        /// 文件夹对象
        /// </summary>
        public StorageFolder Folder { get; private set; }

        /// <summary>
        /// 文件夹的类型
        /// </summary>
        public string DisplayType
        {
            get
            {
                return Globalization.GetString("Folder_Admin_DisplayType");
            }
        }

        public LibraryType Type { get; private set; }

        public static async Task<LibraryFolder> CreateAsync(string Path, LibraryType Type)
        {
            StorageFolder PinFolder = await StorageFolder.GetFolderFromPathAsync(Path);
            BitmapImage Thumbnail = await PinFolder.GetThumbnailBitmapAsync();
            return new LibraryFolder(PinFolder, Thumbnail, Type);
        }

        public static async Task<LibraryFolder> CreateAsync(StorageFolder Folder, LibraryType Type)
        {
            BitmapImage Thumbnail = await Folder.GetThumbnailBitmapAsync();
            return new LibraryFolder(Folder, Thumbnail, Type);
        }

        private LibraryFolder(StorageFolder Folder, BitmapImage Thumbnail, LibraryType Type)
        {
            this.Folder = Folder ?? throw new FileNotFoundException();
            this.Thumbnail = Thumbnail ?? new BitmapImage(new Uri("ms-appx:///Assets/FolderIcon.png"));
            this.Type = Type;
        }
    }
}
