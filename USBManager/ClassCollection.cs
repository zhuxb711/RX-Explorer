using Bluetooth.Core.Services;
using Bluetooth.Services.Obex;
using ICSharpCode.SharpZipLib.Zip;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel.Core;
using Windows.Devices.Enumeration;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.Storage.Search;
using Windows.Storage.Streams;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Media.Imaging;
using WinRTXamlToolkit.Controls.Extensions;

namespace USBManager
{
    #region SQLite数据库
    public sealed class SQLite : IDisposable
    {
        private SqliteConnection OLEDB = new SqliteConnection("Filename=SmartLens_SQLite.db");
        private bool IsDisposed = false;
        private static SQLite SQL = null;
        private SQLite()
        {
            OLEDB.Open();
            string Command = @"Create Table If Not Exists SearchHistory (SearchText Text Not Null);";
            SqliteCommand CreateTable = new SqliteCommand(Command, OLEDB);
            _ = CreateTable.ExecuteNonQuery();
        }

        public static SQLite GetInstance()
        {
            lock (SyncRootProvider.SyncRoot)
            {
                return SQL ?? (SQL = new SQLite());
            }
        }

        public async Task SetSearchHistoryAsync(string SearchText)
        {
            SqliteCommand Command = new SqliteCommand("Insert Into SearchHistory Values (@Para)", OLEDB);
            _ = Command.Parameters.AddWithValue("@Para", SearchText);
            _ = await Command.ExecuteNonQueryAsync();
        }

        public async Task<List<string>> GetSearchHistoryAsync()
        {
            List<string> HistoryList = new List<string>();
            SqliteCommand Command = new SqliteCommand("Select * From SearchHistory", OLEDB);
            SqliteDataReader query = await Command.ExecuteReaderAsync();
            while (query.Read())
            {
                HistoryList.Add(query[0].ToString());
            }
            return HistoryList;
        }

        public async Task ClearSearchHistoryRecord()
        {
            SqliteCommand Command = new SqliteCommand("Delete From SearchHistory", OLEDB);
            _ = await Command.ExecuteNonQueryAsync();
        }

        public void Dispose()
        {
            if (!IsDisposed)
            {
                OLEDB.Dispose();
                OLEDB = null;
                SQL = null;
            }
            IsDisposed = true;
        }

        ~SQLite()
        {
            Dispose();
        }
    }
    #endregion

    #region 可移动设备StorageFile类

    public enum ContentType
    {
        Folder = 0,
        File = 1
    }

    /// <summary>
    /// 提供USB设备中的存储对象的描述
    /// </summary>
    public sealed class RemovableDeviceStorageItem : INotifyPropertyChanged
    {
        /// <summary>
        /// 获取文件大小
        /// </summary>
        public string Size { get; private set; }
        public event PropertyChangedEventHandler PropertyChanged;
        /// <summary>
        /// 获取此文件的StorageFile对象
        /// </summary>
        public StorageFile File { get; private set; }

        public StorageFolder Folder { get; private set; }

        public ContentType ContentType { get; private set; }

        /// <summary>
        /// 获取此文件的缩略图
        /// </summary>
        public BitmapImage Thumbnail { get; private set; }

        /// <summary>
        /// 创建RemovableDeviceFile实例
        /// </summary>
        /// <param name="Size">文件大小</param>
        /// <param name="Item">文件StorageFile对象</param>
        /// <param name="Thumbnail">文件缩略图</param>
        public RemovableDeviceStorageItem(IStorageItem Item)
        {
            if (Item.IsOfType(StorageItemTypes.File))
            {
                File = Item as StorageFile;
                ContentType = ContentType.File;
            }
            else if (Item.IsOfType(StorageItemTypes.Folder))
            {
                Folder = Item as StorageFolder;
                ContentType = ContentType.Folder;
            }
            else
            {
                throw new Exception("Item must be folder or file");
            }

            GetNecessaryInfo();
        }

        private async void GetNecessaryInfo()
        {
            switch (ContentType)
            {
                case ContentType.File:
                    Size = await File.GetSizeDescriptionAsync();
                    Thumbnail = await File.GetThumbnailBitmapAsync() ?? new BitmapImage(new Uri("ms-appx:///Assets/DocIcon.png"));
                    ModifiedTime = await File.GetModifiedTimeAsync();
                    break;
                case ContentType.Folder:
                    Size = await Folder.GetSizeDescriptionAsync();
                    Thumbnail = await Folder.GetThumbnailBitmapAsync() ?? new BitmapImage(new Uri("ms-appx:///Assets/DocIcon.png"));
                    ModifiedTime = await Folder.GetModifiedTimeAsync();
                    break;
            }

            OnPropertyChanged("ModifiedTime");
            OnPropertyChanged("Size");
            OnPropertyChanged("Thumbnail");
        }

        private void OnPropertyChanged(string name)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged.Invoke(this, new PropertyChangedEventArgs(name));
            }
        }

        /// <summary>
        /// 更新文件以及文件大小，并通知UI界面
        /// </summary>
        /// <param name="File"></param>
        public async Task UpdateRequested(IStorageItem Item)
        {
            if (Item is StorageFolder Folder && ContentType == ContentType.Folder)
            {
                this.Folder = Folder;
            }
            else if (Item is StorageFile File && ContentType == ContentType.File)
            {
                this.File = File;
            }
            else
            {
                throw new Exception("Unsupport IStorageItem Or IStorageItem does not match the RemovableDeviceFile");
            }

            Size = await Item.GetSizeDescriptionAsync();
            OnPropertyChanged("DisplayName");
            OnPropertyChanged("Size");
        }

        /// <summary>
        /// 更新文件名称，并通知UI界面
        /// </summary>
        public void NameUpdateRequested()
        {
            OnPropertyChanged("DisplayName");
        }

        /// <summary>
        /// 更新文件大小，并通知UI界面
        /// </summary>
        public async Task SizeUpdateRequested()
        {
            switch (ContentType)
            {
                case ContentType.File:
                    Size = await File.GetSizeDescriptionAsync();
                    break;
                case ContentType.Folder:
                    throw new Exception("Could not update folder size");
            }
            OnPropertyChanged("Size");
        }

        /// <summary>
        /// 获取文件的文件名(不包含后缀)
        /// </summary>
        public string DisplayName
        {
            get
            {
                return ContentType == ContentType.Folder ? Folder.DisplayName : File.DisplayName;
            }
        }

        public string ModifiedTime { get; private set; }

        public string Path
        {
            get
            {
                return ContentType == ContentType.Folder ? Folder.Path : File.Path;
            }
        }

        /// <summary>
        /// 获取文件的完整文件名(包括后缀)
        /// </summary>
        public string Name
        {
            get
            {
                return ContentType == ContentType.Folder ? Folder.Name : File.Name;
            }
        }

        /// <summary>
        /// 获取文件类型描述
        /// </summary>
        public string DisplayType
        {
            get
            {
                return ContentType == ContentType.Folder ? Folder.DisplayType : File.DisplayType;
            }
        }

        /// <summary>
        /// 获取文件的类型
        /// </summary>
        public string Type
        {
            get
            {
                return ContentType == ContentType.Folder ? Folder.DisplayType : File.FileType;
            }
        }

        /// <summary>
        /// 获取文件唯一标识符
        /// </summary>
        public string RelativeId
        {
            get
            {
                return ContentType == ContentType.Folder ? Folder.FolderRelativeId : File.FolderRelativeId;
            }
        }
    }
    #endregion

    #region Zip文件查看器显示类
    /// <summary>
    /// 提供Zip内部文件的显示
    /// </summary>
    public sealed class ZipFileDisplay
    {
        /// <summary>
        /// 获取文件名
        /// </summary>
        public string Name { get; private set; }

        /// <summary>
        /// 获取压缩后的大小
        /// </summary>
        public string CompresionSize { get; private set; }

        /// <summary>
        /// 获取文件实际大小
        /// </summary>
        public string ActualSize { get; private set; }

        /// <summary>
        /// 获取文件修改时间
        /// </summary>
        public string Time { get; private set; }

        /// <summary>
        /// 获取文件类型
        /// </summary>
        public string Type { get; private set; }

        /// <summary>
        /// 获取是否加密的描述
        /// </summary>
        public string IsCrypted { get; private set; }

        /// <summary>
        /// 创建ZipFileDisplay的实例
        /// </summary>
        /// <param name="Name">文件名称</param>
        /// <param name="Type">文件类型</param>
        /// <param name="CompresionSize">压缩后大小</param>
        /// <param name="ActualSize">实际大小</param>
        /// <param name="Time">修改时间</param>
        /// <param name="IsCrypted">加密描述</param>
        public ZipFileDisplay(string Name, string Type, string CompresionSize, string ActualSize, string Time, bool IsCrypted)
        {
            this.CompresionSize = CompresionSize;
            this.Name = Name;
            this.Time = Time;
            this.Type = Type;
            this.ActualSize = ActualSize;
            if (IsCrypted)
            {
                this.IsCrypted = "密码保护：是";
            }
            else
            {
                this.IsCrypted = "密码保护：否";
            }
        }
    }
    #endregion

    #region USB设备为空时的文件目录树显示类
    public sealed class EmptyDeviceDisplay
    {
        public string DisplayName { get => "无USB设备接入"; }
    }
    #endregion

    #region Zip相关枚举
    /// <summary>
    /// AES加密密钥长度枚举
    /// </summary>
    public enum KeySize
    {
        /// <summary>
        /// 无
        /// </summary>
        None = 0,

        /// <summary>
        /// AES-128bit
        /// </summary>
        AES128 = 128,

        /// <summary>
        /// AES-256bit
        /// </summary>
        AES256 = 256
    }

    /// <summary>
    /// 压缩等级枚举
    /// </summary>
    public enum CompressionLevel
    {
        /// <summary>
        /// 最大
        /// </summary>
        Max = 9,

        /// <summary>
        /// 高于标准
        /// </summary>
        AboveStandard = 7,

        /// <summary>
        /// 标准
        /// </summary>
        Standard = 5,

        /// <summary>
        /// 低于标准
        /// </summary>
        BelowStandard = 3,

        /// <summary>
        /// 仅打包
        /// </summary>
        PackOnly = 1
    }
    #endregion

    #region Zip加密界面绑定转换器
    public sealed class ZipCryptConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (!(value is bool))
            {
                return null;
            }

            var IsEnable = (bool)value;
            if (IsEnable)
            {
                return Visibility.Visible;
            }
            else
            {
                return Visibility.Collapsed;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
    #endregion

    #region AES加密解密方法类
    /// <summary>
    /// 提供AES加密相关方法
    /// </summary>
    public sealed class AESProvider
    {
        /// <summary>
        /// 默认256位密钥
        /// </summary>
        public const string Admin256Key = "12345678876543211234567887654321";

        /// <summary>
        /// 默认128位密钥
        /// </summary>
        public const string Admin128Key = "1234567887654321";

        /// <summary>
        /// 默认IV加密向量
        /// </summary>
        private static readonly byte[] AdminIV = Encoding.UTF8.GetBytes("r7BXXKkLb8qrSNn0");

        /// <summary>
        /// 使用AES-CBC加密方式的加密算法
        /// </summary>
        /// <param name="ToEncrypt">待加密的数据</param>
        /// <param name="key">密码</param>
        /// <param name="KeySize">密钥长度</param>
        /// <returns>加密后数据</returns>
        public static byte[] Encrypt(byte[] ToEncrypt, string key, int KeySize)
        {
            if (KeySize != 256 && KeySize != 128)
            {
                throw new InvalidEnumArgumentException("AES密钥长度仅支持128或256任意一种");
            }
            byte[] KeyArray = Encoding.UTF8.GetBytes(key);
            byte[] result;
            using (RijndaelManaged Rijndael = new RijndaelManaged
            {
                KeySize = KeySize,
                Key = KeyArray,
                Mode = CipherMode.CBC,
                Padding = PaddingMode.PKCS7,
                IV = AdminIV
            })
            {
                ICryptoTransform CryptoTransform = Rijndael.CreateEncryptor();
                result = CryptoTransform.TransformFinalBlock(ToEncrypt, 0, ToEncrypt.Length);
            }
            return result;
        }

        /// <summary>
        /// 使用AES-CBC加密方式的解密算法
        /// </summary>
        /// <param name="ToDecrypt">待解密数据</param>
        /// <param name="key">密码</param>
        /// <param name="KeySize">密钥长度</param>
        /// <returns>解密后数据</returns>
        public static byte[] Decrypt(byte[] ToDecrypt, string key, int KeySize)
        {
            if (KeySize != 256 && KeySize != 128)
            {
                throw new InvalidEnumArgumentException("AES密钥长度仅支持128或256任意一种");
            }

            byte[] KeyArray = Encoding.UTF8.GetBytes(key);
            byte[] result;
            using (RijndaelManaged Rijndael = new RijndaelManaged
            {
                KeySize = KeySize,
                Key = KeyArray,
                Mode = CipherMode.CBC,
                Padding = PaddingMode.PKCS7,
                IV = AdminIV
            })
            {
                ICryptoTransform CryptoTransform = Rijndael.CreateDecryptor();
                result = CryptoTransform.TransformFinalBlock(ToDecrypt, 0, ToDecrypt.Length);
            }
            return result;
        }

        /// <summary>
        /// 使用AES-ECB方式的加密算法
        /// </summary>
        /// <param name="ToEncrypt">待加密数据</param>
        /// <param name="key">密码</param>
        /// <param name="KeySize">密钥长度</param>
        /// <returns></returns>
        public static byte[] EncryptForUSB(byte[] ToEncrypt, string key, int KeySize)
        {
            if (KeySize != 256 && KeySize != 128)
            {
                throw new InvalidEnumArgumentException("AES密钥长度仅支持128或256任意一种");
            }

            byte[] KeyArray = Encoding.UTF8.GetBytes(key);
            byte[] result;
            using (RijndaelManaged Rijndael = new RijndaelManaged
            {
                KeySize = KeySize,
                Key = KeyArray,
                Mode = CipherMode.ECB,
                Padding = PaddingMode.Zeros
            })
            {
                ICryptoTransform CryptoTransform = Rijndael.CreateEncryptor();
                result = CryptoTransform.TransformFinalBlock(ToEncrypt, 0, ToEncrypt.Length);
            }
            return result;
        }

        /// <summary>
        /// 使用AES-ECB方式的解密算法
        /// </summary>
        /// <param name="ToDecrypt">待解密数据</param>
        /// <param name="key">密码</param>
        /// <param name="KeySize">密钥长度</param>
        /// <returns>解密后数据</returns>
        public static byte[] DecryptForUSB(byte[] ToDecrypt, string key, int KeySize)
        {
            if (KeySize != 256 && KeySize != 128)
            {
                throw new InvalidEnumArgumentException("AES密钥长度仅支持128或256任意一种");
            }

            byte[] KeyArray = Encoding.UTF8.GetBytes(key);
            byte[] result;
            using (RijndaelManaged Rijndael = new RijndaelManaged
            {
                KeySize = KeySize,
                Key = KeyArray,
                Mode = CipherMode.ECB,
                Padding = PaddingMode.Zeros
            })
            {

                ICryptoTransform CryptoTransform = Rijndael.CreateDecryptor();

                result = CryptoTransform.TransformFinalBlock(ToDecrypt, 0, ToDecrypt.Length);
            }
            return result;
        }

    }
    #endregion

    #region USB图片展示类
    /// <summary>
    /// 为USB图片查看提供支持
    /// </summary>
    public sealed class PhotoDisplaySupport
    {
        /// <summary>
        /// 获取Bitmap图片对象
        /// </summary>
        public BitmapImage Bitmap { get; private set; }

        /// <summary>
        /// 获取Photo文件名称
        /// </summary>
        public string FileName
        {
            get
            {
                return PhotoFile.Name;
            }
        }

        /// <summary>
        /// 获取Photo的StorageFile对象
        /// </summary>
        public StorageFile PhotoFile { get; private set; }

        /// <summary>
        /// 创建PhotoDisplaySupport的实例
        /// </summary>
        /// <param name="stream">缩略图的流</param>
        /// <param name="File">图片文件</param>
        public PhotoDisplaySupport(IRandomAccessStream stream, StorageFile File)
        {
            Bitmap = new BitmapImage();
            Bitmap.SetSource(stream);
            PhotoFile = File;
        }
    }
    #endregion

    #region Zip自定义静态数据源
    public sealed class CustomStaticDataSource : IStaticDataSource
    {
        private Stream stream;

        public Stream GetSource()
        {
            return stream;
        }

        public void SetStream(Stream inputStream)
        {
            stream = inputStream;
            stream.Position = 0;
        }
    }
    #endregion

    #region lock关键字同步锁全局对象提供器
    /// <summary>
    /// 提供全局锁定根
    /// </summary>
    public class SyncRootProvider
    {
        /// <summary>
        /// 锁定根对象
        /// </summary>
        public static object SyncRoot { get; } = new object();
    }
    #endregion

    #region 文件系统追踪类
    public enum TrackerMode
    {
        TraceFolder = 0,
        TraceFile = 1
    }

    public sealed class FileSystemChangeSet : EventArgs
    {
        public List<IStorageItem> StorageItems { get; private set; }
        public TreeViewNode ParentNode { get; private set; }

        public FileSystemChangeSet(List<IStorageItem> Item)
        {
            StorageItems = Item;
        }

        public FileSystemChangeSet(List<IStorageItem> Item, TreeViewNode Node)
        {
            StorageItems = Item;
            ParentNode = Node;
        }
    }

    public sealed class FileSystemRenameSet : EventArgs
    {
        public List<IStorageItem> ToDeleteFileList { get; private set; }
        public List<IStorageItem> ToAddFileList { get; private set; }
        public TreeViewNode ParentNode { get; private set; }

        public FileSystemRenameSet(List<IStorageItem> ToDeleteFileList, List<IStorageItem> ToAddFileList)
        {
            this.ToDeleteFileList = ToDeleteFileList;
            this.ToAddFileList = ToAddFileList;
        }

        public FileSystemRenameSet(List<IStorageItem> ToDeleteFileList, List<IStorageItem> ToAddFileList, TreeViewNode Node)
        {
            this.ToDeleteFileList = ToDeleteFileList;
            this.ToAddFileList = ToAddFileList;
            ParentNode = Node;
        }
    }

    public sealed class FileSystemTracker : IDisposable
    {
        private StorageFolderQueryResult FolderQuery;
        private StorageFileQueryResult FileQuery;
        private TreeViewNode TrackNode;
        private static bool IsProcessing = false;
        private bool IsResuming = false;
        public TrackerMode TrackerMode { get; }
        public event EventHandler<FileSystemChangeSet> Deleted;
        public event EventHandler<FileSystemRenameSet> Renamed;
        public event EventHandler<FileSystemChangeSet> Created;

        public FileSystemTracker(TreeViewNode TrackNode)
        {
            TrackerMode = TrackerMode.TraceFolder;

            this.TrackNode = TrackNode ?? throw new ArgumentNullException("TrackNode");

            StorageFolder TrackFolder = TrackNode.Content as StorageFolder;
            _ = Initialize(TrackFolder);
        }

        public FileSystemTracker(StorageFolder TrackFolder)
        {
            if (TrackFolder == null)
            {
                throw new ArgumentNullException("TrackFolder");
            }

            TrackerMode = TrackerMode.TraceFolder;

            _ = Initialize(TrackFolder);
        }

        public FileSystemTracker(StorageFileQueryResult Query)
        {
            TrackerMode = TrackerMode.TraceFile;

            FileQuery = Query ?? throw new ArgumentNullException("Query");

            FileQuery.ContentsChanged += FileQuery_ContentsChanged;
        }

        private async Task Initialize(StorageFolder TrackFolder)
        {
            switch (TrackerMode)
            {
                case TrackerMode.TraceFolder:
                    QueryOptions option = new QueryOptions(CommonFileQuery.DefaultQuery, null)
                    {
                        FolderDepth = FolderDepth.Deep,
                        IndexerOption = IndexerOption.UseIndexerWhenAvailable
                    };
                    FolderQuery = TrackFolder.CreateFolderQueryWithOptions(option);

                    _ = await FolderQuery.GetFoldersAsync(0, 1);

                    FolderQuery.ContentsChanged += FolderQuery_ContentsChanged;
                    break;

                case TrackerMode.TraceFile:
                    QueryOptions Options = new QueryOptions(CommonFileQuery.DefaultQuery, null)
                    {
                        FolderDepth = FolderDepth.Shallow,
                        IndexerOption = IndexerOption.UseIndexerWhenAvailable
                    };
                    FileQuery = TrackFolder.CreateFileQueryWithOptions(Options);

                    _ = await FileQuery.GetFilesAsync(0, 1);

                    FileQuery.ContentsChanged += FileQuery_ContentsChanged;
                    break;
            }
        }

        public void PauseDetection()
        {
            if (TrackerMode == TrackerMode.TraceFolder)
            {
                FolderQuery.ContentsChanged -= FolderQuery_ContentsChanged;
            }
            else
            {
                FileQuery.ContentsChanged -= FileQuery_ContentsChanged;
            }
        }

        public async void ResumeDetection()
        {
            if (IsResuming)
            {
                return;
            }
            IsResuming = true;

            await Task.Delay(2000);

            if (TrackerMode == TrackerMode.TraceFolder)
            {
                if (FolderQuery != null)
                {
                    FolderQuery.ContentsChanged += FolderQuery_ContentsChanged;
                }
            }
            else
            {
                if (FileQuery != null)
                {
                    FileQuery.ContentsChanged += FileQuery_ContentsChanged;
                }
            }

            IsResuming = false;
        }

        private async Task FolderChangeAnalysis(TreeViewNode ParentNode)
        {
            var Folder = ParentNode.Content as StorageFolder;

            var FolderList = await Folder.GetFoldersAsync();
            if (FolderList.Count != ParentNode.Children.Count)
            {
                if (ParentNode.IsExpanded == false && ParentNode.HasUnrealizedChildren)
                {
                    return;
                }

                List<StorageFolder> SubFolders = new List<StorageFolder>(ParentNode.Children.Select((SubNode) => SubNode.Content as StorageFolder));

                if (FolderList.Count > ParentNode.Children.Count)
                {
                    List<IStorageItem> NewFolderList = new List<IStorageItem>(Except(SubFolders, FolderList));

                    Created?.Invoke(this, new FileSystemChangeSet(NewFolderList, ParentNode));
                }
                else
                {
                    List<IStorageItem> OldFolderList = new List<IStorageItem>(Except(FolderList, SubFolders));

                    Deleted?.Invoke(this, new FileSystemChangeSet(OldFolderList, ParentNode));
                }
                return;
            }
            else
            {
                if (ParentNode.IsExpanded == false && ParentNode.HasUnrealizedChildren)
                {
                    return;
                }

                var ExNodeList = Except(ParentNode.Children, FolderList);
                var ExFolderList = Except(FolderList, ParentNode.Children);

                if (ExNodeList.Count != 0 && ExFolderList.Count != 0)
                {
                    List<IStorageItem> DeleteFileList = new List<IStorageItem>(ExNodeList);
                    List<IStorageItem> AddFileList = new List<IStorageItem>(ExFolderList);

                    Renamed?.Invoke(this, new FileSystemRenameSet(DeleteFileList, AddFileList, ParentNode));

                    return;
                }
            }

            foreach (TreeViewNode Node in ParentNode.Children)
            {
                await FolderChangeAnalysis(Node);
            }
        }

        private async void FileQuery_ContentsChanged(IStorageQueryResultBase sender, object args)
        {
            if (!(bool)ApplicationData.Current.LocalSettings.Values["EnableTrace"])
            {
                return;
            }

            lock (SyncRootProvider.SyncRoot)
            {
                if (IsProcessing)
                {
                    return;
                }
                IsProcessing = true;
            }

            IReadOnlyList<StorageFile> FileList = await sender.Folder.GetFilesAsync();

            if (FileList.Count != USBFilePresenter.ThisPage.FileCollection.Count)
            {
                if (FileList.Count > USBFilePresenter.ThisPage.FileCollection.Count)
                {
                    List<IStorageItem> AddFileList = new List<IStorageItem>(await ExceptAsync(USBFilePresenter.ThisPage.FileCollection, FileList));
                    if (AddFileList.Count == 0)
                    {
                        IsProcessing = false;
                        return;
                    }

                    await CoreApplication.MainView.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                    {
                        Created?.Invoke(this, new FileSystemChangeSet(AddFileList));
                    });
                }
                else
                {
                    List<IStorageItem> DeleteFileList = new List<IStorageItem>(await ExceptAsync(USBFilePresenter.ThisPage.FileCollection, FileList));
                    if (DeleteFileList.Count == 0)
                    {
                        IsProcessing = false;
                        return;
                    }

                    await CoreApplication.MainView.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                    {
                        Deleted?.Invoke(this, new FileSystemChangeSet(DeleteFileList));
                    });
                }
            }
            else
            {
                if (USBFilePresenter.ThisPage.FileCollection.Count != 0)
                {
                    List<IStorageItem> DeleteFileList = new List<IStorageItem>(await ExceptAsync(USBFilePresenter.ThisPage.FileCollection, FileList));
                    if (DeleteFileList.Count == 0)
                    {
                        IsProcessing = false;
                        return;
                    }

                    List<IStorageItem> AddFileList = new List<IStorageItem>(await ExceptAsync(FileList, USBFilePresenter.ThisPage.FileCollection));
                    if (AddFileList.Count == 0)
                    {
                        IsProcessing = false;
                        return;
                    }

                    await CoreApplication.MainView.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                    {
                        Renamed?.Invoke(this, new FileSystemRenameSet(DeleteFileList, AddFileList));
                    });
                }
            }

            IsProcessing = false;
        }

        private async void FolderQuery_ContentsChanged(IStorageQueryResultBase sender, object args)
        {
            if (!(bool)ApplicationData.Current.LocalSettings.Values["EnableTrace"])
            {
                return;
            }

            lock (SyncRootProvider.SyncRoot)
            {
                if (IsProcessing)
                {
                    return;
                }
                IsProcessing = true;
            }

            await CoreApplication.MainView.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
            {
                IReadOnlyList<StorageFolder> USBDevice = await (TrackNode.Content as StorageFolder).GetFoldersAsync();

                if (TrackNode.Children.FirstOrDefault()?.Content is EmptyDeviceDisplay && USBDevice.Count > 0)
                {
                    TrackNode.Children.Clear();
                }

                if (USBDevice.Count > TrackNode.Children.Count)
                {
                    List<StorageFolder> AddDeviceList = Except(USBDevice, TrackNode.Children);
                    foreach (var Device in AddDeviceList)
                    {
                        TrackNode.Children.Add(new TreeViewNode
                        {
                            Content = Device,
                            HasUnrealizedChildren = (await Device.GetFoldersAsync()).Count != 0
                        });
                    }
                }
                else if (USBDevice.Count < TrackNode.Children.Count && !(TrackNode.Children.FirstOrDefault().Content is EmptyDeviceDisplay))
                {
                    List<StorageFolder> DeleteDeviceList = Except(TrackNode.Children, USBDevice);
                    foreach (var Device in DeleteDeviceList)
                    {
                        foreach (var CurrentDevice in TrackNode.Children)
                        {
                            if ((CurrentDevice.Content as StorageFolder).FolderRelativeId == Device.FolderRelativeId)
                            {
                                TrackNode.Children.Remove(CurrentDevice);
                            }
                        }
                    }

                    if (TrackNode.Children.Count == 0)
                    {
                        TrackNode.Children.Add(new TreeViewNode { Content = new EmptyDeviceDisplay() });
                        IsProcessing = false;
                        return;
                    }
                }

                if (!(TrackNode.Children.FirstOrDefault().Content is EmptyDeviceDisplay))
                {
                    foreach (var DeviceNode in TrackNode.Children)
                    {
                        await FolderChangeAnalysis(DeviceNode);
                    }
                }
            });

            IsProcessing = false;
        }

        public void Dispose()
        {
            if (FolderQuery != null)
            {
                FolderQuery.ContentsChanged -= FolderQuery_ContentsChanged;
                FolderQuery = null;
            }
            if (FileQuery != null)
            {
                FileQuery.ContentsChanged -= FileQuery_ContentsChanged;
                FileQuery = null;
            }
            TrackNode = null;
        }

        private List<StorageFolder> Except(IEnumerable<TreeViewNode> list1, IEnumerable<StorageFolder> list2)
        {
            IEnumerable<StorageFolder> FolderList = list1.Select(x => x.Content as StorageFolder);
            return Except(list2, FolderList);
        }

        private List<StorageFolder> Except(IEnumerable<StorageFolder> list2, IEnumerable<TreeViewNode> list1)
        {
            IEnumerable<StorageFolder> FolderList = list1.Select(x => x.Content as StorageFolder);
            return Except(FolderList, list2);
        }

        private async Task<List<StorageFile>> ExceptAsync(IEnumerable<RemovableDeviceStorageItem> list1, IEnumerable<StorageFile> list2)
        {
            IEnumerable<StorageFile> FileList = list1.Select(x => x.File);
            return await ExceptAsync(list2, FileList);
        }

        private async Task<List<StorageFile>> ExceptAsync(IEnumerable<StorageFile> list2, IEnumerable<RemovableDeviceStorageItem> list1)
        {
            IEnumerable<StorageFile> FileList = list1.Select(x => x.File);
            return await ExceptAsync(FileList, list2);
        }


        private List<StorageFolder> Except(IEnumerable<StorageFolder> list1, IEnumerable<StorageFolder> list2)
        {
            if (list1.Count() == 0 && list2.Count() != 0)
            {
                return list2.ToList();
            }
            if (list1.Count() != 0 && list2.Count() == 0)
            {
                return list1.ToList();
            }
            if (list1.Count() == 0 && list2.Count() == 0)
            {
                return new List<StorageFolder>();
            }

            if (list1.Count() > list2.Count())
            {
                return list1.Where(x => list2.All(y => x.FolderRelativeId != y.FolderRelativeId)).ToList();
            }
            else
            {
                return list2.Where(x => list1.All(y => x.FolderRelativeId != y.FolderRelativeId)).ToList();
            }
        }

        private Task<List<StorageFile>> ExceptAsync(IEnumerable<StorageFile> list1, IEnumerable<StorageFile> list2)
        {
            return Task.Run(() =>
            {
                if (list1.Count() == 0 && list2.Count() != 0)
                {
                    return list2.ToList();
                }
                if (list1.Count() != 0 && list2.Count() == 0)
                {
                    return list1.ToList();
                }
                if (list1.Count() == 0 && list2.Count() == 0)
                {
                    return new List<StorageFile>();
                }

                if (list1.Count() > list2.Count())
                {
                    return list1.Where(x => list2.All(y => x.FolderRelativeId != y.FolderRelativeId)).ToList();
                }
                else
                {
                    return list2.Where(x => list1.All(y => x.FolderRelativeId != y.FolderRelativeId)).ToList();
                }
            });
        }
    }
    #endregion

    #region 文件夹图标状态更改转换器
    public sealed class FolderStateConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (!(value is bool))
            {
                return null;
            }

            if ((bool)value)
            {
                return "\xE838";
            }
            else
            {
                return "\xED41";
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
    #endregion

    #region 文件大小显示状态更改转换器
    public sealed class SizeDisplayConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is StorageFolder)
            {
                return Visibility.Collapsed;
            }
            else
            {
                return Visibility.Visible;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
    #endregion

    #region 扩展方法类
    public static class Extention
    {
        public static async Task<string> GetSizeDescriptionAsync(this IStorageItem Item)
        {
            BasicProperties Properties = await Item.GetBasicPropertiesAsync();
            return Properties.Size / 1024f < 1024 ? Math.Round(Properties.Size / 1024f, 2).ToString() + " KB" :
            (Properties.Size / 1048576f >= 1024 ? Math.Round(Properties.Size / 1073741824f, 2).ToString() + " GB" :
            Math.Round(Properties.Size / 1048576f, 2).ToString() + " MB");
        }

        public static async Task<string>GetModifiedTimeAsync(this IStorageItem Item)
        {
            var Properties = await Item.GetBasicPropertiesAsync();
            return Properties.DateModified.Year + "年" + Properties.DateModified.Month + "月" + Properties.DateModified.Day + "日, " + (Properties.DateModified.Hour < 10 ? "0" + Properties.DateModified.Hour : Properties.DateModified.Hour.ToString()) + ":" + (Properties.DateModified.Minute < 10 ? "0" + Properties.DateModified.Minute : Properties.DateModified.Minute.ToString()) + ":" + (Properties.DateModified.Second < 10 ? "0" + Properties.DateModified.Second : Properties.DateModified.Second.ToString());
        }

        public static async Task<BitmapImage> GetThumbnailBitmapAsync(this StorageFolder Item)
        {
            var Thumbnail = await Item.GetThumbnailAsync(ThumbnailMode.ListView);
            if (Thumbnail == null)
            {
                return null;
            }

            BitmapImage bitmapImage = new BitmapImage
            {
                DecodePixelHeight = 60,
                DecodePixelWidth = 60
            };
            await bitmapImage.SetSourceAsync(Thumbnail);
            return bitmapImage;
        }

        public static async Task<BitmapImage> GetThumbnailBitmapAsync(this StorageFile Item)
        {
            var Thumbnail = await Item.GetThumbnailAsync(ThumbnailMode.ListView);
            if (Thumbnail == null)
            {
                return null;
            }

            BitmapImage bitmapImage = new BitmapImage
            {
                DecodePixelHeight = 60,
                DecodePixelWidth = 60
            };
            await bitmapImage.SetSourceAsync(Thumbnail);
            return bitmapImage;
        }

        public static void ScrollIntoViewSmoothly(this ListViewBase listViewBase, object item, ScrollIntoViewAlignment alignment = ScrollIntoViewAlignment.Default)
        {
            if (listViewBase == null)
            {
                throw new ArgumentNullException(nameof(listViewBase));
            }

            // GetFirstDescendantOfType 是 WinRTXamlToolkit 中的扩展方法，
            // 寻找该控件在可视树上第一个符合类型的子元素。
            ScrollViewer scrollViewer = listViewBase.GetFirstDescendantOfType<ScrollViewer>();

            // 记录初始位置，用于 ScrollIntoView 检测目标位置后复原。
            double originHorizontalOffset = scrollViewer.HorizontalOffset;
            double originVerticalOffset = scrollViewer.VerticalOffset;

            void layoutUpdatedHandler(object sender, object e)
            {
                listViewBase.LayoutUpdated -= layoutUpdatedHandler;

                // 获取目标位置。
                double targetHorizontalOffset = scrollViewer.HorizontalOffset;
                double targetVerticalOffset = scrollViewer.VerticalOffset;

                void scrollHandler(object s, ScrollViewerViewChangedEventArgs m)
                {
                    scrollViewer.ViewChanged -= scrollHandler;

                    // 最终目的，带平滑滚动效果滚动到 item。
                    scrollViewer.ChangeView(targetHorizontalOffset, targetVerticalOffset, null);
                }

                scrollViewer.ViewChanged += scrollHandler;

                // 复原位置，且不需要使用动画效果。
                scrollViewer.ChangeView(originHorizontalOffset, originVerticalOffset, null, true);

            }

            listViewBase.LayoutUpdated += layoutUpdatedHandler;

            listViewBase.ScrollIntoView(item, alignment);
        }
    }
    #endregion

    #region 蓝牙设备列表类
    /// <summary>
    /// 为蓝牙模块提供蓝牙设备信息保存功能
    /// </summary>
    public sealed class BluetoothList : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// 表示蓝牙设备
        /// </summary>
        public DeviceInformation DeviceInfo { get; set; }
        public void OnPropertyChanged(string name)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged.Invoke(this, new PropertyChangedEventArgs(name));
            }
        }

        /// <summary>
        /// 获取蓝牙设备名称
        /// </summary>
        public string Name
        {
            get
            {
                return string.IsNullOrWhiteSpace(DeviceInfo.Name) ? "未知设备" : DeviceInfo.Name;
            }
        }

        public BitmapImage Glyph { get; private set; }

        /// <summary>
        /// 获取蓝牙标识字符串
        /// </summary>
        public string Id
        {
            get
            {
                return DeviceInfo.Id;
            }
        }

        /// <summary>
        /// 获取配对情况描述字符串
        /// </summary>
        public string IsPaired
        {
            get
            {
                if (DeviceInfo.Pairing.IsPaired)
                {
                    return "已配对";
                }
                else
                {
                    return "准备配对";
                }
            }
        }

        /// <summary>
        /// Button显示属性
        /// </summary>
        public string CancelOrPairButton
        {
            get
            {
                if (DeviceInfo.Pairing.IsPaired)
                {
                    return "取消配对";
                }
                else
                {
                    return "配对";
                }
            }
        }

        /// <summary>
        /// 更新蓝牙设备信息
        /// </summary>
        /// <param name="DeviceInfoUpdate">蓝牙设备的更新属性</param>
        public void Update(DeviceInformationUpdate DeviceInfoUpdate)
        {
            DeviceInfo.Update(DeviceInfoUpdate);
            OnPropertyChanged("IsPaired");
            OnPropertyChanged("Name");
        }

        /// <summary>
        /// 创建BluetoothList的实例
        /// </summary>
        /// <param name="DeviceInfo">蓝牙设备</param>
        public BluetoothList(DeviceInformation DeviceInfo)
        {
            this.DeviceInfo = DeviceInfo;
            GetGlyphImage();
        }

        private async void GetGlyphImage()
        {
            BitmapImage Image = new BitmapImage
            {
                DecodePixelHeight = 30,
                DecodePixelWidth = 30,
                DecodePixelType = DecodePixelType.Logical
            };

            using (var Thumbnail = await DeviceInfo.GetGlyphThumbnailAsync())
            {
                await Image.SetSourceAsync(Thumbnail);
            }

            Glyph = Image;

            OnPropertyChanged("Glyph");
        }
    }
    #endregion

    #region 蓝牙Obex协议对象类
    /// <summary>
    /// 提供蓝牙OBEX协议服务
    /// </summary>
    public sealed class ObexServiceProvider
    {
        /// <summary>
        /// 蓝牙设备
        /// </summary>
        private static BluetoothDevice BTDevice;

        /// <summary>
        /// OBEX协议服务
        /// </summary>
        public static ObexService GetObexNewInstance()
        {
            if (BTDevice != null)
            {
                return ObexService.GetDefaultForBluetoothDevice(BTDevice);
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// 设置Obex对象的实例
        /// </summary>
        /// <param name="obex">OBEX对象</param>
        public static void SetObexInstance(BluetoothDevice BT)
        {
            BTDevice = BT;
        }
    }
    #endregion
}
