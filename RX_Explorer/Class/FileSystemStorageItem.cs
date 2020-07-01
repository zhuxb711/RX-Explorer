using System;
using System.ComponentModel;
using System.IO;
using System.Threading.Tasks;
using Windows.ApplicationModel.Core;
using Windows.Storage;
using Windows.UI.Core;
using Windows.UI.Xaml.Media.Imaging;

namespace RX_Explorer.Class
{
    /// <summary>
    /// 提供对设备中的存储对象的描述
    /// </summary>
    public sealed class FileSystemStorageItem : INotifyPropertyChanged, IComparable
    {
        /// <summary>
        /// 指示所包含的存储对象类型
        /// </summary>
        public StorageItemTypes StorageType { get; private set; }

        /// <summary>
        /// 存储对象
        /// </summary>
        private IStorageItem StorageItem;

        /// <summary>
        /// 用于兼容WIN_Native_API所提供的路径
        /// </summary>
        private readonly string InternalPathString;

        /// <summary>
        /// 指示是否是回收站对象，此值为true时将改变一些呈现内容
        /// </summary>
        public bool IsRecycleItem { get; private set; } = false;

        /// <summary>
        /// 当IsRecycleItem=true时提供回收站对象的原始路径
        /// </summary>
        public string RecycleItemOriginPath { get; private set; }

        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// 获取此文件的缩略图
        /// </summary>
        public BitmapImage Thumbnail { get; private set; }

        /// <summary>
        /// 初始化FileSystemStorageItem对象
        /// </summary>
        /// <param name="Item">文件</param>
        /// <param name="Size">大小</param>
        /// <param name="Thumbnail">缩略图</param>
        /// <param name="ModifiedTime">修改时间</param>
        public FileSystemStorageItem(StorageFile Item, long Size, BitmapImage Thumbnail, DateTimeOffset ModifiedTime)
        {
            StorageItem = Item;
            StorageType = StorageItemTypes.File;

            SizeRaw = Size;
            ModifiedTimeRaw = ModifiedTime;
            this.Thumbnail = Thumbnail ?? new BitmapImage(new Uri("ms-appx:///Assets/DocIcon.png"));
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

            Thumbnail = new BitmapImage(new Uri("ms-appx:///Assets/FolderIcon.png"));
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

            if (StorageType == StorageItemTypes.Folder)
            {
                Thumbnail = new BitmapImage(new Uri("ms-appx:///Assets/FolderIcon.png"));
            }
            else
            {
                SizeRaw = (Data.nFileSizeHigh << 32) + (long)Data.nFileSizeLow;
            }
        }

        private void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        /// <summary>
        /// 调用此方法以获得存储对象
        /// </summary>
        /// <returns></returns>
        public async Task<IStorageItem> GetStorageItem()
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
            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Low, async () =>
             {
                 if (await GetStorageItem().ConfigureAwait(true) is IStorageItem Item)
                 {
                     if (Item.IsOfType(StorageItemTypes.File) && Thumbnail == null)
                     {
                         Thumbnail = (await Item.GetThumbnailBitmapAsync().ConfigureAwait(true)) ?? new BitmapImage(new Uri("ms-appx:///Assets/DocIcon.png"));
                         OnPropertyChanged(nameof(Thumbnail));
                         OnPropertyChanged(nameof(DisplayType));
                     }
                 }
             });
        }

        /// <summary>
        /// 调用此方法以将该对象标记为回收站对象
        /// </summary>
        /// <param name="OriginPath">原始路径</param>
        /// <param name="CreateTime">修改时间</param>
        public void SetAsRecycleItem(string OriginPath, DateTimeOffset CreateTime)
        {
            IsRecycleItem = true;
            RecycleItemOriginPath = OriginPath;
            ModifiedTimeRaw = CreateTime;
        }


        public async Task Replace(string NewPath)
        {
            try
            {
                StorageFile File = await StorageFile.GetFileFromPathAsync(NewPath);
                StorageItem = File;
                StorageType = StorageItemTypes.File;

                SizeRaw = await File.GetSizeRawDataAsync().ConfigureAwait(true);
                ModifiedTimeRaw = await File.GetModifiedTimeAsync().ConfigureAwait(true);
                Thumbnail = await File.GetThumbnailBitmapAsync().ConfigureAwait(true) ?? new BitmapImage(new Uri("ms-appx:///Assets/DocIcon.png"));
            }
            catch (FileNotFoundException)
            {
                StorageFolder Folder = await StorageFolder.GetFolderFromPathAsync(NewPath);
                StorageItem = Folder;
                StorageType = StorageItemTypes.Folder;

                Thumbnail = new BitmapImage(new Uri("ms-appx:///Assets/FolderIcon.png"));
                ModifiedTimeRaw = await Folder.GetModifiedTimeAsync().ConfigureAwait(true);
            }

            OnPropertyChanged(nameof(this.Name));
            OnPropertyChanged(nameof(ModifiedTime));
            OnPropertyChanged(nameof(DisplayType));
        }

        /// <summary>
        /// 获取文件的修改时间描述
        /// </summary>
        public string ModifiedTime
        {
            get
            {
                return ModifiedTimeRaw.ToString("F");
            }
        }

        /// <summary>
        /// 获取原始的修改时间
        /// </summary>
        public DateTimeOffset ModifiedTimeRaw { get; private set; }

        /// <summary>
        /// 获取文件的路径
        /// </summary>
        public string Path
        {
            get
            {
                return StorageItem == null ? InternalPathString : StorageItem.Path;
            }
        }

        /// <summary>
        /// 获取文件大小描述
        /// </summary>
        public string Size
        {
            get
            {
                if (StorageType == StorageItemTypes.File)
                {
                    return SizeRaw / 1024f < 1024 ? Math.Round(SizeRaw / 1024f, 2).ToString("0.00") + " KB" :
                    (SizeRaw / 1048576f < 1024 ? Math.Round(SizeRaw / 1048576f, 2).ToString("0.00") + " MB" :
                    (SizeRaw / 1073741824f < 1024 ? Math.Round(SizeRaw / 1073741824f, 2).ToString("0.00") + " GB" :
                    Math.Round(SizeRaw / Convert.ToDouble(1099511627776), 2).ToString() + " TB"));
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
        public long SizeRaw { get; private set; } = 0;

        /// <summary>
        /// 获取文件的完整文件名(包括后缀)
        /// </summary>
        public string Name
        {
            get
            {
                if (IsRecycleItem)
                {
                    return System.IO.Path.GetFileName(RecycleItemOriginPath);
                }
                else
                {
                    return StorageItem == null ? System.IO.Path.GetFileName(InternalPathString) : StorageItem.Name;
                }
            }
        }

        /// <summary>
        /// 获取文件类型描述
        /// </summary>
        public string DisplayType
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
        public string Type
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
