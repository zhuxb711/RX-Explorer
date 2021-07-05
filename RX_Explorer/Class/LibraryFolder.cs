using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.UI.Xaml.Media.Imaging;

namespace RX_Explorer.Class
{
    /// <summary>
    /// 提供对文件夹库的UI显示支持
    /// </summary>
    public sealed class LibraryFolder : INotifyPropertyChanged
    {
        /// <summary>
        /// 文件夹缩略图
        /// </summary>
        public BitmapImage Thumbnail
        {
            get
            {
                return InnerThumbnail ?? new BitmapImage(new Uri("ms-appx:///Assets/FolderIcon.png"));
            }
            private set
            {
                InnerThumbnail = value;
                OnPropertyChanged();
            }
        }

        private BitmapImage InnerThumbnail;

        /// <summary>
        /// 文件夹对象
        /// </summary>
        public StorageFolder LibFolder { get; }

        public LibraryType Type { get; }

        public string Path
        {
            get
            {
                return LibFolder.Path;
            }
        }

        /// <summary>
        /// 文件夹名称
        /// </summary>
        public string Name
        {
            get
            {
                return string.IsNullOrEmpty(LibFolder.DisplayName) ? LibFolder.Name : LibFolder.DisplayName;
            }
        }

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

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string PropertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(PropertyName));
        }

        public async Task LoadThumbnailAsync()
        {
            Thumbnail = (await LibFolder.GetThumbnailBitmapAsync(ThumbnailMode.ListView)) ?? new BitmapImage(new Uri("ms-appx:///Assets/FolderIcon.png"));
        }

        public LibraryFolder(LibraryType Type, StorageFolder LibFolder)
        {
            this.LibFolder = LibFolder ?? throw new FileNotFoundException();
            this.Type = Type;
        }
    }
}
