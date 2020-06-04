using System.IO;
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

        /// <summary>
        /// 初始化LibraryFolder
        /// </summary>
        /// <param name="Folder">文件夹对象</param>
        /// <param name="Thumbnail">缩略图</param>
        /// <param name="Source">类型</param>
        public LibraryFolder(StorageFolder Folder, BitmapImage Thumbnail)
        {
            this.Folder = Folder ?? throw new FileNotFoundException();
            this.Thumbnail = Thumbnail;
        }
    }
}
