using System;
using System.ComponentModel;
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
        public StorageItemTypes StorageType { get; private set; }

        private IStorageItem StorageItem;

        private string TempPathString;

        private string TempNameString;

        public bool IsRecycleItem { get; private set; } = false;

        public string RecycleItemOriginPath { get; private set; }

        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// 获取此文件的缩略图
        /// </summary>
        public BitmapImage Thumbnail { get; private set; }

        /// <summary>
        /// 初始化FileSystemStorageItem对象
        /// </summary>
        /// <param name="Item">文件或文件夹</param>
        /// <param name="Size">大小</param>
        /// <param name="Thumbnail">缩略图</param>
        /// <param name="ModifiedTime">修改时间</param>
        public FileSystemStorageItem(StorageFile Item, long Size, BitmapImage Thumbnail, DateTimeOffset ModifiedTime)
        {
            StorageItem = Item;
            StorageType = StorageItemTypes.File;

            SizeRaw = Size;
            ModifiedTimeRaw = ModifiedTime;
            this.Size = Size / 1024f < 1024 ? Math.Round(Size / 1024f, 2).ToString("0.00") + " KB" :
                        (Size / 1048576f < 1024 ? Math.Round(Size / 1048576f, 2).ToString("0.00") + " MB" :
                        (Size / 1073741824f < 1024 ? Math.Round(Size / 1073741824f, 2).ToString("0.00") + " GB" :
                        Math.Round(Size / Convert.ToDouble(1099511627776), 2).ToString() + " TB")); ;
            this.Thumbnail = Thumbnail ?? new BitmapImage(new Uri("ms-appx:///Assets/DocIcon.png"));
        }

        public FileSystemStorageItem(StorageFolder Item, DateTimeOffset ModifiedTime)
        {
            StorageItem = Item;
            StorageType = StorageItemTypes.Folder;

            Thumbnail = new BitmapImage(new Uri("ms-appx:///Assets/FolderIcon.png"));
            ModifiedTimeRaw = ModifiedTime;
        }

        public FileSystemStorageItem(WIN_Native_API.WIN32_FIND_DATA Data, StorageItemTypes StorageType, string Path, DateTimeOffset ModifiedTime)
        {
            long SizeBit = (Data.nFileSizeHigh << 32) + (long)Data.nFileSizeLow;
            SizeRaw = SizeBit;
            Size = SizeBit / 1024f < 1024 ? Math.Round(SizeBit / 1024f, 2).ToString("0.00") + " KB" :
                    (SizeBit / 1048576f < 1024 ? Math.Round(SizeBit / 1048576f, 2).ToString("0.00") + " MB" :
                    (SizeBit / 1073741824f < 1024 ? Math.Round(SizeBit / 1073741824f, 2).ToString("0.00") + " GB" :
                    Math.Round(SizeBit / Convert.ToDouble(1099511627776), 2).ToString() + " TB"));

            TempPathString = Path;
            TempNameString = Data.cFileName;
            ModifiedTimeRaw = ModifiedTime;
            this.StorageType = StorageType;

            if (StorageType == StorageItemTypes.Folder)
            {
                Thumbnail = new BitmapImage(new Uri("ms-appx:///Assets/FolderIcon.png"));
            }
        }

        private void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        public int CompareTo(object obj)
        {
            if (obj is FileSystemStorageItem Item)
            {
                return Item.Path.CompareTo(Path);
            }
            else
            {
                throw new ArgumentNullException(nameof(obj), "obj could not be null");
            }
        }

        public async Task<IStorageItem> GetStorageItem()
        {
            try
            {
                if (StorageItem == null)
                {
                    if (StorageType == StorageItemTypes.File)
                    {
                        return StorageItem = await StorageFile.GetFileFromPathAsync(TempPathString);
                    }
                    else
                    {
                        return StorageItem = await StorageFolder.GetFolderFromPathAsync(TempPathString);
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

        public void SetAsRecycleItem(string OriginPath, DateTimeOffset CreateTime)
        {
            IsRecycleItem = true;
            RecycleItemOriginPath = OriginPath;
            ModifiedTimeRaw = CreateTime;
        }

        public async Task RenameAsync(string Name)
        {
            if (StorageItem is StorageFile File)
            {
                await File.RenameAsync(Name, NameCollisionOption.GenerateUniqueName);
                File = await StorageFile.GetFileFromPathAsync(File.Path);
                Thumbnail = await File.GetThumbnailBitmapAsync().ConfigureAwait(true);
                ModifiedTimeRaw = await File.GetModifiedTimeAsync().ConfigureAwait(true);
                OnPropertyChanged(nameof(DisplayName));
                OnPropertyChanged(nameof(Thumbnail));
                OnPropertyChanged(nameof(ModifiedTime));
                OnPropertyChanged(nameof(DisplayType));
            }
            else if (StorageItem is StorageFolder Folder)
            {
                await Folder.RenameAsync(Name, NameCollisionOption.GenerateUniqueName);
                Folder = await StorageFolder.GetFolderFromPathAsync(Folder.Path);
                ModifiedTimeRaw = await Folder.GetModifiedTimeAsync().ConfigureAwait(true);
                OnPropertyChanged(nameof(DisplayName));
                OnPropertyChanged(nameof(ModifiedTime));
                OnPropertyChanged(nameof(DisplayType));
            }
        }

        /// <summary>
        /// 获取文件的文件名(不包含后缀)
        /// </summary>
        public string DisplayName
        {
            get
            {
                if (IsRecycleItem)
                {
                    return System.IO.Path.GetFileName(RecycleItemOriginPath);
                }
                else
                {
                    if (StorageItem is StorageFolder Folder)
                    {
                        return string.IsNullOrEmpty(Folder.DisplayName) ? Folder.Name : Folder.DisplayName;
                    }
                    else if (StorageItem is StorageFile File)
                    {
                        return string.IsNullOrEmpty(File.DisplayName) ? File.Name : (File.DisplayName.EndsWith(File.FileType) ? System.IO.Path.GetFileNameWithoutExtension(File.DisplayName) : File.DisplayName);
                    }
                    else
                    {
                        return Name;
                    }
                }
            }
        }

        /// <summary>
        /// 获取文件的修改时间
        /// </summary>
        public string ModifiedTime
        {
            get
            {
                return ModifiedTimeRaw.ToString("F");
            }
        }

        public DateTimeOffset ModifiedTimeRaw { get; private set; }

        /// <summary>
        /// 获取文件的路径
        /// </summary>
        public string Path
        {
            get
            {
                return StorageItem == null ? TempPathString : StorageItem.Path;
            }
        }

        /// <summary>
        /// 获取文件大小
        /// </summary>
        public string Size { get; private set; }

        public long SizeRaw { get; private set; }

        /// <summary>
        /// 获取文件的完整文件名(包括后缀)
        /// </summary>
        public string Name
        {
            get
            {
                return StorageItem == null ? TempNameString : StorageItem.Name;
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
    }
}
