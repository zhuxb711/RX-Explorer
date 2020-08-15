using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Windows.ApplicationModel.Core;
using Windows.Storage;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media.Imaging;

namespace RX_Explorer.Class
{
    /// <summary>
    /// 提供对设备中的存储对象的描述
    /// </summary>
    public class FileSystemStorageItem : INotifyPropertyChanged, IComparable
    {
        /// <summary>
        /// 指示所包含的存储对象类型
        /// </summary>
        public StorageItemTypes StorageType { get; protected set; }

        /// <summary>
        /// 存储对象
        /// </summary>
        protected IStorageItem StorageItem { get; set; }

        /// <summary>
        /// 用于兼容WIN_Native_API所提供的路径
        /// </summary>
        protected string InternalPathString { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// 获取此文件的缩略图
        /// </summary>
        public BitmapImage Thumbnail
        {
            get
            {
                if (Inner_Thumbnail != null)
                {
                    return Inner_Thumbnail;
                }
                else
                {
                    if (StorageType == StorageItemTypes.File)
                    {
                        return AppThemeController.Current.Theme == ElementTheme.Dark ? Const_File_White_Image : Const_File_Black_Image;
                    }
                    else
                    {
                        return Const_Folder_Image;
                    }
                }
            }
            set
            {
                Inner_Thumbnail = value;
            }
        }

        protected BitmapImage Inner_Thumbnail { get; set; }

        protected readonly static BitmapImage Const_Folder_Image = new BitmapImage(new Uri("ms-appx:///Assets/FolderIcon.png"));

        protected readonly static BitmapImage Const_File_White_Image = new BitmapImage(new Uri("ms-appx:///Assets/Page_Solid_White.png"));

        protected readonly static BitmapImage Const_File_Black_Image = new BitmapImage(new Uri("ms-appx:///Assets/Page_Solid_Black.png"));

        /// <summary>
        /// 初始化FileSystemStorageItem对象
        /// </summary>
        /// <param name="Item">文件</param>
        /// <param name="Size">大小</param>
        /// <param name="Thumbnail">缩略图</param>
        /// <param name="ModifiedTime">修改时间</param>
        public FileSystemStorageItem(StorageFile Item, ulong Size, BitmapImage Thumbnail, DateTimeOffset ModifiedTime)
        {
            StorageItem = Item;
            StorageType = StorageItemTypes.File;

            SizeRaw = Size;
            ModifiedTimeRaw = ModifiedTime;
            this.Thumbnail = Thumbnail;
        }

        /// <summary>
        /// 初始化FileSystemStorageItem对象
        /// </summary>
        /// <param name="Item">文件夹</param>
        /// <param name="ModifiedTime">修改时间</param>
        public FileSystemStorageItem(StorageFolder Item, DateTimeOffset ModifiedTime)
        {
            StorageItem = Item;
            StorageType = StorageItemTypes.Folder;

            ModifiedTimeRaw = ModifiedTime;
        }

        /// <summary>
        /// 初始化FileSystemStorageItem对象
        /// </summary>
        /// <param name="Data">WIN_Native_API所提供的数据</param>
        /// <param name="StorageType">指示存储类型</param>
        /// <param name="Path">路径</param>
        /// <param name="ModifiedTime">修改时间</param>
        public FileSystemStorageItem(WIN_Native_API.WIN32_FIND_DATA Data, StorageItemTypes StorageType, string Path, DateTimeOffset ModifiedTime)
        {
            InternalPathString = Path;
            ModifiedTimeRaw = ModifiedTime;
            this.StorageType = StorageType;

            if (StorageType != StorageItemTypes.Folder)
            {
                SizeRaw = ((ulong)Data.nFileSizeHigh << 32) + Data.nFileSizeLow;
            }
        }

        protected FileSystemStorageItem()
        {

        }

        protected void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        /// <summary>
        /// 调用此方法以获得存储对象
        /// </summary>
        /// <returns></returns>
        public virtual async Task<IStorageItem> GetStorageItem()
        {
            try
            {
                if (StorageItem == null)
                {
                    if (StorageType == StorageItemTypes.File)
                    {
                        return StorageItem = await StorageFile.GetFileFromPathAsync(InternalPathString);
                    }
                    else
                    {
                        return StorageItem = await StorageFolder.GetFolderFromPathAsync(InternalPathString);
                    }
                }
                else
                {
                    return StorageItem;
                }
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 加载并获取更多属性，例如缩略图，显示名称等
        /// </summary>
        /// <returns></returns>
        public async Task LoadMoreProperty()
        {
            if (StorageType == StorageItemTypes.File && Inner_Thumbnail == null && await GetStorageItem().ConfigureAwait(false) is IStorageItem Item)
            {
                await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, async () =>
                {
                    Thumbnail = await Item.GetThumbnailBitmapAsync().ConfigureAwait(true);
                    OnPropertyChanged(nameof(Thumbnail));
                    OnPropertyChanged(nameof(DisplayType));
                });
            }
        }

        /// <summary>
        /// 设置缩略图的透明度，用于表示文件的是否处于待移动或隐藏状态
        /// </summary>
        /// <param name="Status">状态</param>
        public void SetThumbnailOpacity(ThumbnailStatus Status)
        {
            switch (Status)
            {
                case ThumbnailStatus.Normal:
                    {
                        if (ThumbnailOpacity != 1d)
                        {
                            ThumbnailOpacity = 1d;
                        }
                        break;
                    }
                case ThumbnailStatus.ReduceOpacity:
                    {
                        if (ThumbnailOpacity != 0.5)
                        {
                            ThumbnailOpacity = 0.5;
                        }
                        break;
                    }
            }

            OnPropertyChanged(nameof(ThumbnailOpacity));
        }

        /// <summary>
        /// 用新路径的存储对象替代当前的FileSystemStorageItem的内容
        /// </summary>
        /// <param name="NewPath">新的路径</param>
        /// <returns></returns>
        public async Task Replace(string NewPath)
        {
            try
            {
                StorageFile File = await StorageFile.GetFileFromPathAsync(NewPath);
                StorageItem = File;
                StorageType = StorageItemTypes.File;

                SizeRaw = await File.GetSizeRawDataAsync().ConfigureAwait(true);
                ModifiedTimeRaw = await File.GetModifiedTimeAsync().ConfigureAwait(true);
                Thumbnail = await File.GetThumbnailBitmapAsync().ConfigureAwait(true);
            }
            catch
            {
                try
                {
                    StorageFolder Folder = await StorageFolder.GetFolderFromPathAsync(NewPath);
                    StorageItem = Folder;
                    StorageType = StorageItemTypes.Folder;

                    ModifiedTimeRaw = await Folder.GetModifiedTimeAsync().ConfigureAwait(true);
                }
                catch
                {
                    return;
                }
            }

            OnPropertyChanged(nameof(Thumbnail));
            OnPropertyChanged(nameof(Name));
            OnPropertyChanged(nameof(ModifiedTime));
            OnPropertyChanged(nameof(DisplayType));
            OnPropertyChanged(nameof(Size));
        }

        /// <summary>
        /// 手动更新界面显示
        /// </summary>
        /// <param name="ReGenerateSizeAndModifiedTime"><是否重新计算大小和修改时间/param>
        /// <returns></returns>
        public async Task Update(bool ReGenerateSizeAndModifiedTime)
        {
            if (ReGenerateSizeAndModifiedTime)
            {
                if (await GetStorageItem().ConfigureAwait(true) is IStorageItem Item)
                {
                    if (Item.IsOfType(StorageItemTypes.File))
                    {
                        SizeRaw = await Item.GetSizeRawDataAsync().ConfigureAwait(true);
                    }

                    ModifiedTimeRaw = await Item.GetModifiedTimeAsync().ConfigureAwait(true);
                }
            }

            OnPropertyChanged(nameof(Name));
            OnPropertyChanged(nameof(ModifiedTime));
            OnPropertyChanged(nameof(DisplayType));
            OnPropertyChanged(nameof(Size));
        }

        /// <summary>
        /// 获取文件的修改时间描述
        /// </summary>
        public virtual string ModifiedTime
        {
            get
            {
                return ModifiedTimeRaw.ToString("F");
            }
        }

        public double ThumbnailOpacity { get; protected set; } = 1d;

        /// <summary>
        /// 获取原始的修改时间
        /// </summary>
        public DateTimeOffset ModifiedTimeRaw { get; protected set; }

        /// <summary>
        /// 获取文件的路径
        /// </summary>
        public virtual string Path
        {
            get
            {
                return StorageItem == null ? InternalPathString : StorageItem.Path;
            }
        }

        /// <summary>
        /// 获取文件大小描述
        /// </summary>
        public virtual string Size
        {
            get
            {
                if (StorageType == StorageItemTypes.File)
                {
                    return SizeRaw / 1024d < 1024 ? Math.Round(SizeRaw / 1024d, 2).ToString("0.00") + " KB" :
                    (SizeRaw / 1048576d < 1024 ? Math.Round(SizeRaw / 1048576d, 2).ToString("0.00") + " MB" :
                    (SizeRaw / 1073741824d < 1024 ? Math.Round(SizeRaw / 1073741824d, 2).ToString("0.00") + " GB" :
                    Math.Round(SizeRaw / 1099511627776d, 2).ToString() + " TB"));
                }
                else
                {
                    return string.Empty;
                }
            }
        }

        /// <summary>
        /// 获取原始大小数据
        /// </summary>
        public ulong SizeRaw { get; protected set; }

        /// <summary>
        /// 获取文件的完整文件名(包括后缀)
        /// </summary>
        public virtual string Name
        {
            get
            {
                return StorageItem == null ? System.IO.Path.GetFileName(InternalPathString) : StorageItem.Name;
            }
        }

        /// <summary>
        /// 获取文件类型描述
        /// </summary>
        public virtual string DisplayType
        {
            get
            {
                if (StorageItem is StorageFile File)
                {
                    return File.DisplayType;
                }
                else if (StorageItem is StorageFolder Folder)
                {
                    return Folder.DisplayType;
                }
                else
                {
                    return StorageType == StorageItemTypes.File ? System.IO.Path.GetExtension(Name).ToUpper() : Globalization.GetString("Folder_Admin_DisplayType");
                }
            }
        }

        /// <summary>
        /// 获取文件的类型
        /// </summary>
        public virtual string Type
        {
            get
            {
                if (StorageItem is StorageFile File)
                {
                    return File.FileType;
                }
                else if (StorageItem is StorageFolder Folder)
                {
                    return Folder.DisplayType;
                }
                else
                {
                    return StorageType == StorageItemTypes.File ? System.IO.Path.GetExtension(Name).ToUpper() : Globalization.GetString("Folder_Admin_DisplayType");
                }
            }
        }

        public override string ToString()
        {
            return Name;
        }

        public int CompareTo(object obj)
        {
            if (obj is FileSystemStorageItem Item)
            {
                return Item.Path.CompareTo(Path);
            }
            else
            {
                throw new ArgumentNullException(nameof(obj), "obj must be FileSystemStorageItem");
            }
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(this, obj))
            {
                return true;
            }
            else
            {
                if (obj is FileSystemStorageItem Item)
                {
                    return Item.Path.Equals(Path);
                }
                else
                {
                    return false;
                }
            }
        }

        public override int GetHashCode()
        {
            return Path.GetHashCode();
        }

        public static bool operator ==(FileSystemStorageItem left, FileSystemStorageItem right)
        {
            if (left is null)
            {
                return right is null;
            }
            else
            {
                if (right is null)
                {
                    return false;
                }
                else
                {
                    return left.Path == right.Path;
                }
            }
        }

        public static bool operator !=(FileSystemStorageItem left, FileSystemStorageItem right)
        {
            if (left is null)
            {
                return right is object;
            }
            else
            {
                if (right is null)
                {
                    return true;
                }
                else
                {
                    return left.Path != right.Path;
                }
            }
        }
    }
}
