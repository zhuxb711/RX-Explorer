using Bluetooth.Core.Services;
using Bluetooth.Services.Obex;
using DownloaderProvider;
using ICSharpCode.SharpZipLib.Zip;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Devices.Enumeration;
using Windows.Foundation;
using Windows.Graphics.Imaging;
using Windows.Networking;
using Windows.Networking.Connectivity;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.Storage.Search;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using WinRTXamlToolkit.Controls.Extensions;

namespace FileManager
{
    #region SQLite数据库
    public sealed class SQLite : IDisposable
    {
        private SqliteConnection OLEDB;
        private bool IsDisposed = false;
        private static SQLite SQL = null;
        private SQLite()
        {
            SQLitePCL.Batteries_V2.Init();
            SQLitePCL.raw.sqlite3_win32_set_directory(1, ApplicationData.Current.LocalFolder.Path);
            SQLitePCL.raw.sqlite3_win32_set_directory(2, ApplicationData.Current.TemporaryFolder.Path);

            OLEDB = new SqliteConnection("Filename=RX_Sqlite.db");
            OLEDB.Open();
            string Command = @"Create Table If Not Exists SearchHistory (SearchText Text Not Null, Primary Key (SearchText));
                               Create Table If Not Exists WebFavourite (Subject Text Not Null, WebSite Text Not Null, Primary Key (WebSite));
                               Create Table If Not Exists WebHistory (Subject Text Not Null, WebSite Text Not Null, DateTime Text Not Null, Primary Key (Subject, WebSite, DateTime));
                               Create Table If Not Exists DownloadHistory (UniqueID Text Not Null, ActualName Text Not Null, Uri Text Not Null, State Text Not Null, Primary Key(UniqueID));
                               Create Table If Not Exists QuickStart (Name Text Not Null, FullPath Text Not Null, Protocal Text Not Null, Type Text Not Null, Primary Key (Name,FullPath,Protocal,Type));
                               Create Table If Not Exists FolderLibrary (Path Text Not Null, Primary Key (Path));";
            using (SqliteCommand CreateTable = new SqliteCommand(Command, OLEDB))
            {
                _ = CreateTable.ExecuteNonQuery();
            }
        }

        public static SQLite Current
        {
            get
            {
                lock (SyncRootProvider.SyncRoot)
                {
                    return SQL ?? (SQL = new SQLite());
                }
            }
        }

        public async Task<List<string>> GetFolderLibraryAsync()
        {
            List<string> FolderPath = new List<string>();
            using (SqliteCommand Command = new SqliteCommand("Select * From FolderLibrary", OLEDB))
            using (SqliteDataReader query = await Command.ExecuteReaderAsync())
            {
                while (query.Read())
                {
                    FolderPath.Add(query[0].ToString());
                }
                return FolderPath;
            }
        }

        public async Task DeleteFolderLibraryAsync(string Path)
        {
            using (SqliteCommand Command = new SqliteCommand("Delete From FolderLibrary Where Path = @Path", OLEDB))
            {
                _ = Command.Parameters.AddWithValue("@Path", Path);
                _ = await Command.ExecuteNonQueryAsync();
            }
        }

        public async Task SetFolderLibraryAsync(string Path)
        {
            using (SqliteCommand Command = new SqliteCommand("Insert Into FolderLibrary Values (@Path)", OLEDB))
            {
                _ = Command.Parameters.AddWithValue("@Path", Path);
                _ = await Command.ExecuteNonQueryAsync();
            }
        }

        public async Task SetSearchHistoryAsync(string SearchText)
        {
            using (SqliteCommand Command = new SqliteCommand("Insert Or Ignore Into SearchHistory Values (@Para)", OLEDB))
            {
                _ = Command.Parameters.AddWithValue("@Para", SearchText);
                _ = await Command.ExecuteNonQueryAsync();
            }
        }

        public async Task SetQuickStartItemAsync(string Name, string FullPath, string Protocal, QuickStartType Type)
        {
            using (SqliteCommand Command = new SqliteCommand("Insert Or Ignore Into QuickStart Values (@Name,@Path,@Protocal,@Type)", OLEDB))
            {
                _ = Command.Parameters.AddWithValue("@Name", Name);
                _ = Command.Parameters.AddWithValue("@Path", FullPath);
                _ = Command.Parameters.AddWithValue("@Protocal", Protocal);
                _ = Command.Parameters.AddWithValue("@Type", Enum.GetName(typeof(QuickStartType), Type));
                _ = await Command.ExecuteNonQueryAsync();
            }
        }

        public async Task UpdateQuickStartItemAsync(string OldName, string NewName, string FullPath, string Protocal, QuickStartType Type)
        {
            if (FullPath != null)
            {
                using (SqliteCommand Command = new SqliteCommand("Update QuickStart Set Name=@NewName, FullPath=@Path, Protocal=@Protocal Where Name=@OldName And Type=@Type", OLEDB))
                {
                    _ = Command.Parameters.AddWithValue("@OldName", OldName);
                    _ = Command.Parameters.AddWithValue("@Path", FullPath);
                    _ = Command.Parameters.AddWithValue("@NewName", NewName);
                    _ = Command.Parameters.AddWithValue("@Protocal", Protocal);
                    _ = Command.Parameters.AddWithValue("@Type", Enum.GetName(typeof(QuickStartType), Type));
                    _ = await Command.ExecuteNonQueryAsync();
                }
            }
            else
            {
                using (SqliteCommand Command = new SqliteCommand("Update QuickStart Set Name=@NewName, Protocal=@Protocal Where Name=@OldName And Type=@Type", OLEDB))
                {
                    _ = Command.Parameters.AddWithValue("@OldName", OldName);
                    _ = Command.Parameters.AddWithValue("@NewName", NewName);
                    _ = Command.Parameters.AddWithValue("@Protocal", Protocal);
                    _ = Command.Parameters.AddWithValue("@Type", Enum.GetName(typeof(QuickStartType), Type));
                    _ = await Command.ExecuteNonQueryAsync();
                }
            }
        }

        public async Task DeleteQuickStartItemAsync(QuickStartItem Item)
        {
            using (SqliteCommand Command = new SqliteCommand("Delete From QuickStart Where Name = @Name And FullPath = @FullPath And Type=@Type", OLEDB))
            {
                _ = Command.Parameters.AddWithValue("@Name", Item.DisplayName);
                _ = Command.Parameters.AddWithValue("@FullPath", Item.RelativePath);
                _ = Command.Parameters.AddWithValue("@Type", Enum.GetName(typeof(QuickStartType), Item.Type));
                _ = await Command.ExecuteNonQueryAsync();
            }
        }

        public async Task<List<KeyValuePair<QuickStartType, QuickStartItem>>> GetQuickStartItemAsync()
        {
            List<KeyValuePair<QuickStartType, QuickStartItem>> QuickStartItemList = new List<KeyValuePair<QuickStartType, QuickStartItem>>();

            using (SqliteCommand Command = new SqliteCommand("Select * From QuickStart", OLEDB))
            using (SqliteDataReader query = await Command.ExecuteReaderAsync())
            {
                while (query.Read())
                {
                    try
                    {
                        StorageFile ImageFile = await StorageFile.GetFileFromPathAsync(Path.Combine(ApplicationData.Current.LocalFolder.Path, query[1].ToString()));
                        using (var Stream = await ImageFile.OpenAsync(FileAccessMode.Read))
                        {
                            BitmapImage Bitmap = new BitmapImage
                            {
                                DecodePixelHeight = 80,
                                DecodePixelWidth = 80
                            };
                            await Bitmap.SetSourceAsync(Stream);
                            if ((QuickStartType)Enum.Parse(typeof(QuickStartType), query[3].ToString()) == QuickStartType.Application)
                            {
                                QuickStartItemList.Add(new KeyValuePair<QuickStartType, QuickStartItem>(QuickStartType.Application, new QuickStartItem(Bitmap, new Uri(query[2].ToString()), QuickStartType.Application, query[1].ToString(), query[0].ToString())));
                            }
                            else
                            {
                                QuickStartItemList.Add(new KeyValuePair<QuickStartType, QuickStartItem>(QuickStartType.WebSite, new QuickStartItem(Bitmap, new Uri(query[2].ToString()), QuickStartType.WebSite, query[1].ToString(), query[0].ToString())));
                            }
                        }
                    }
                    catch (Exception)
                    {
                        using (SqliteCommand Command1 = new SqliteCommand("Delete From QuickStart Where Name = @Name And FullPath = @FullPath And Type=@Type", OLEDB))
                        {
                            _ = Command1.Parameters.AddWithValue("@Name", query[0].ToString());
                            _ = Command1.Parameters.AddWithValue("@FullPath", query[1].ToString());
                            _ = Command1.Parameters.AddWithValue("@Type", query[3].ToString());
                            _ = await Command1.ExecuteNonQueryAsync();
                        }
                    }
                }
                return QuickStartItemList;
            }
        }

        public async Task<List<string>> GetRelatedSearchHistoryAsync(string Target)
        {
            List<string> HistoryList = new List<string>();
            using (SqliteCommand Command = new SqliteCommand("Select * From SearchHistory Where SearchText Like @Target", OLEDB))
            {
                _ = Command.Parameters.AddWithValue("@Target", "%" + Target + "%");
                using (SqliteDataReader query = await Command.ExecuteReaderAsync())
                {
                    while (query.Read())
                    {
                        HistoryList.Add(query[0].ToString());
                    }
                    return HistoryList;
                }
            }
        }

        public async Task GetDownloadHistoryAsync()
        {
            using (SqliteCommand Command = new SqliteCommand("Select * From DownloadHistory", OLEDB))
            using (SqliteDataReader query = await Command.ExecuteReaderAsync())
            {
                for (int i = 0; query.Read(); i++)
                {
                    DownloadState State = (DownloadState)Enum.Parse(typeof(DownloadState), query[3].ToString());
                    if (State == DownloadState.Downloading || State == DownloadState.Paused)
                    {
                        State = DownloadState.Canceled;
                    }

                    WebDownloader.DownloadList.Add(WebDownloader.CreateDownloadOperatorFromDatabase(new Uri(query[2].ToString()), query[1].ToString(), State, query[0].ToString())); ;
                }
            }
        }

        public async Task SetDownloadHistoryAsync(DownloadOperator Task)
        {
            using (SqliteCommand Command = new SqliteCommand("Insert Into DownloadHistory Values (@UniqueID,@ActualName,@Uri,@State)", OLEDB))
            {
                _ = Command.Parameters.AddWithValue("@UniqueID", Task.UniqueID);
                _ = Command.Parameters.AddWithValue("@ActualName", Task.ActualFileName);
                _ = Command.Parameters.AddWithValue("@Uri", Task.Address.AbsoluteUri);
                _ = Command.Parameters.AddWithValue("@State", Enum.GetName(typeof(DownloadState), Task.State));
                _ = await Command.ExecuteNonQueryAsync();
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Security", "CA2100:Review SQL queries for security vulnerabilities", Justification = "<挂起>")]
        public async Task ClearTableAsync(string TableName)
        {
            using (SqliteCommand Command = new SqliteCommand("Delete From " + TableName, OLEDB))
            {
                _ = await Command.ExecuteNonQueryAsync();
            }
        }

        public async Task DeleteDownloadHistoryAsync(DownloadOperator Task)
        {
            using (SqliteCommand Command = new SqliteCommand("Delete From DownloadHistory Where UniqueID = @UniqueID", OLEDB))
            {
                _ = Command.Parameters.AddWithValue("@UniqueID", Task.UniqueID);
                _ = await Command.ExecuteNonQueryAsync();
            }
        }

        public async Task UpdateDownloadHistoryAsync(DownloadOperator Task)
        {
            using (SqliteCommand Command = new SqliteCommand("Update DownloadHistory Set State = @State Where UniqueID = @UniqueID", OLEDB))
            {
                _ = Command.Parameters.AddWithValue("@UniqueID", Task.UniqueID);
                _ = Command.Parameters.AddWithValue("@State", Enum.GetName(typeof(DownloadState), Task.State));
                _ = await Command.ExecuteNonQueryAsync();
            }
        }

        public async Task SetWebFavouriteListAsync(WebSiteItem Info)
        {
            using (SqliteCommand Command = new SqliteCommand("Insert Into WebFavourite Values (@Subject,@WebSite)", OLEDB))
            {
                _ = Command.Parameters.AddWithValue("@Subject", Info.Subject);
                _ = Command.Parameters.AddWithValue("@WebSite", Info.WebSite);
                _ = await Command.ExecuteNonQueryAsync();
            }
        }

        public async Task DeleteWebFavouriteListAsync(WebSiteItem Info)
        {
            using (SqliteCommand Command = new SqliteCommand("Delete From WebFavourite Where WebSite = @WebSite", OLEDB))
            {
                _ = Command.Parameters.AddWithValue("@WebSite", Info.WebSite);
                _ = await Command.ExecuteNonQueryAsync();
            }
        }

        public void DeleteWebHistory(KeyValuePair<DateTime, WebSiteItem> Info)
        {
            using (SqliteCommand Command = new SqliteCommand("Delete From WebHistory Where Subject=@Subject And WebSite=@WebSite And DateTime=@DateTime", OLEDB))
            {
                _ = Command.Parameters.AddWithValue("@Subject", Info.Value.Subject);
                _ = Command.Parameters.AddWithValue("@WebSite", Info.Value.WebSite);
                _ = Command.Parameters.AddWithValue("@DateTime", Info.Key.ToBinary().ToString());

                _ = Command.ExecuteNonQuery();
            }
        }

        public void SetWebHistoryList(KeyValuePair<DateTime, WebSiteItem> Info)
        {
            using (SqliteCommand Command = new SqliteCommand("Insert Into WebHistory Values (@Subject,@WebSite,@DateTime)", OLEDB))
            {
                _ = Command.Parameters.AddWithValue("@Subject", Info.Value.Subject);
                _ = Command.Parameters.AddWithValue("@WebSite", Info.Value.WebSite);
                _ = Command.Parameters.AddWithValue("@DateTime", Info.Key.ToBinary().ToString());

                _ = Command.ExecuteNonQuery();
            }
        }

        public async Task<List<KeyValuePair<DateTime, WebSiteItem>>> GetWebHistoryListAsync()
        {
            using (SqliteCommand Command = new SqliteCommand("Select * From WebHistory", OLEDB))
            using (SqliteDataReader Query = await Command.ExecuteReaderAsync())
            {
                List<KeyValuePair<DateTime, WebSiteItem>> HistoryList = new List<KeyValuePair<DateTime, WebSiteItem>>();

                while (Query.Read())
                {
                    HistoryList.Add(new KeyValuePair<DateTime, WebSiteItem>(DateTime.FromBinary(Convert.ToInt64(Query[2])), new WebSiteItem(Query[0].ToString(), Query[1].ToString())));
                }

                HistoryList.Reverse();
                return HistoryList;
            }
        }

        public async Task<List<WebSiteItem>> GetWebFavouriteListAsync()
        {
            using (SqliteCommand Command = new SqliteCommand("Select * From WebFavourite", OLEDB))
            using (SqliteDataReader Query = await Command.ExecuteReaderAsync())
            {
                List<WebSiteItem> FavList = new List<WebSiteItem>();

                while (Query.Read())
                {
                    FavList.Add(new WebSiteItem(Query[0].ToString(), Query[1].ToString()));
                }

                return FavList;
            }
        }

        public async Task ClearSearchHistoryRecord()
        {
            using (SqliteCommand Command = new SqliteCommand("Delete From SearchHistory", OLEDB))
            {
                _ = await Command.ExecuteNonQueryAsync();
            }
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

    #region 文件系统StorageFile类

    public enum ContentType
    {
        Folder = 0,
        File = 1
    }

    /// <summary>
    /// 提供对设备中的存储对象的描述
    /// </summary>
    public sealed class FileSystemStorageItem : INotifyPropertyChanged
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
        public FileSystemStorageItem(IStorageItem Item, string Size, BitmapImage Thumbnail, string ModifiedTime)
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
            this.Size = Size;
            this.Thumbnail = Thumbnail;
            this.ModifiedTime = ModifiedTime;
        }

        private void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
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
                return ContentType == ContentType.Folder ?
                    (string.IsNullOrEmpty(Folder.DisplayName) ? Folder.Name : Folder.DisplayName) :
                    (string.IsNullOrEmpty(File.DisplayName) ? File.Name : (File.DisplayName.EndsWith(File.FileType) ? File.DisplayName.Remove(File.DisplayName.LastIndexOf(".")) : File.DisplayName));
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

        public override string ToString()
        {
            return Name;
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

        public string FullName { get; private set; }

        /// <summary>
        /// 创建ZipFileDisplay的实例
        /// </summary>
        public ZipFileDisplay(ZipEntry Entry)
        {
            FullName = Entry.Name;

            if (MainPage.ThisPage.CurrentLanguage == LanguageEnum.Chinese)
            {
                CompresionSize = "压缩大小：" + GetSize(Entry.CompressedSize);
                ActualSize = "解压大小：" + GetSize(Entry.Size);
                Time = "创建时间：" + Entry.DateTime.ToString("F");

                int index = FullName.LastIndexOf(".");
                if (index != -1)
                {
                    Name = Path.GetFileNameWithoutExtension(FullName);
                    Type = Path.GetExtension(FullName).Substring(1).ToUpper() + "文件";
                }
                else
                {
                    Name = FullName;
                    Type = "未知文件类型";
                }

                if (Entry.IsCrypted)
                {
                    IsCrypted = "密码保护：是";
                }
                else
                {
                    IsCrypted = "密码保护：否";
                }
            }
            else
            {
                CompresionSize = "Compressed：" + GetSize(Entry.CompressedSize);
                ActualSize = "ActualSize：" + GetSize(Entry.Size);
                Time = "Created：" + Entry.DateTime.ToString("F");

                int index = FullName.LastIndexOf(".");
                if (index != -1)
                {
                    Name = Path.GetFileNameWithoutExtension(FullName);
                    Type = Path.GetExtension(FullName).Substring(1).ToUpper() + "File";
                }
                else
                {
                    Name = FullName;
                    Type = "Unknown Type";
                }

                if (Entry.IsCrypted)
                {
                    IsCrypted = "Encrypted：True";
                }
                else
                {
                    IsCrypted = "Encrypted：False";
                }
            }
        }

        /// <summary>
        /// 获取文件大小的描述
        /// </summary>
        /// <param name="Size">大小</param>
        /// <returns>大小描述</returns>
        private string GetSize(long Size)
        {
            return Size / 1024f < 1024 ? Math.Round(Size / 1024f, 2).ToString() + " KB" :
            (Size / 1048576f >= 1024 ? Math.Round(Size / 1073741824f, 2).ToString() + " GB" :
            Math.Round(Size / 1048576f, 2).ToString() + " MB");
        }
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
        public static byte[] CBCEncrypt(byte[] ToEncrypt, string key, int KeySize)
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
        public static byte[] CBCDecrypt(byte[] ToDecrypt, string key, int KeySize)
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
        public static byte[] ECBEncrypt(byte[] ToEncrypt, string key, int KeySize)
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
        public static byte[] ECBDecrypt(byte[] ToDecrypt, string key, int KeySize)
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

    #region 图片展示类
    /// <summary>
    /// 为图片查看提供支持
    /// </summary>
    public sealed class PhotoDisplaySupport : INotifyPropertyChanged
    {
        /// <summary>
        /// 获取Bitmap图片对象
        /// </summary>
        public BitmapImage BitmapSource { get; private set; }

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

        private bool IsThumbnailPicture = true;

        public int RotateAngle { get; set; } = 0;

        /// <summary>
        /// 获取Photo的StorageFile对象
        /// </summary>
        public StorageFile PhotoFile { get; private set; }

        public event PropertyChangedEventHandler PropertyChanged;

        public PhotoDisplaySupport(BitmapImage ImageSource, StorageFile File)
        {
            BitmapSource = ImageSource;
            PhotoFile = File;
        }

        public async Task ReplaceThumbnailBitmap()
        {
            if (IsThumbnailPicture)
            {
                IsThumbnailPicture = false;
                using (var Stream = await PhotoFile.OpenAsync(FileAccessMode.Read))
                {
                    await BitmapSource.SetSourceAsync(Stream);
                }
                OnPropertyChanged("BitmapSource");
            }
        }

        public async Task UpdateImage()
        {
            using (var Stream = await PhotoFile.OpenAsync(FileAccessMode.Read))
            {
                await BitmapSource.SetSourceAsync(Stream);
            }
            OnPropertyChanged("BitmapSource");
        }

        public async Task<SoftwareBitmap> GenerateImageWithRotation()
        {
            using (var stream = await PhotoFile.OpenAsync(FileAccessMode.Read))
            {
                BitmapDecoder decoder = await BitmapDecoder.CreateAsync(stream);

                switch (RotateAngle % 360)
                {
                    case 0:
                        {
                            return await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
                        }
                    case 90:
                        {
                            using (var Origin = await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied))
                            {
                                SoftwareBitmap Processed = new SoftwareBitmap(BitmapPixelFormat.Bgra8, Origin.PixelHeight, Origin.PixelWidth, BitmapAlphaMode.Premultiplied);
                                OpenCV.OpenCVLibrary.RotateEffect(Origin, Processed, 90);
                                return Processed;
                            }
                        }
                    case 180:
                        {
                            var Origin = await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
                            OpenCV.OpenCVLibrary.RotateEffect(Origin, Origin, 180);
                            return Origin;
                        }
                    case 270:
                        {
                            using (var Origin = await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied))
                            {
                                SoftwareBitmap Processed = new SoftwareBitmap(BitmapPixelFormat.Bgra8, Origin.PixelHeight, Origin.PixelWidth, BitmapAlphaMode.Premultiplied);
                                OpenCV.OpenCVLibrary.RotateEffect(Origin, Processed, -90);
                                return Processed;
                            }
                        }
                    default:
                        {
                            return null;
                        }
                }
            }
        }

        private void OnPropertyChanged(string Name)
        {
            if (!string.IsNullOrEmpty(Name))
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(Name));
            }
        }
    }
    #endregion

    #region 图片查看器相关
    public enum FilterType
    {
        Origin = 0,
        Invert = 1,
        Gray = 2,
        Threshold = 4,
        Sketch = 8,
        GaussianBlur = 16,
        Sepia = 32,
        Mosaic = 64,
        OilPainting = 128
    }

    public sealed class FilterItem : IDisposable
    {
        public string Text { get; private set; }

        public FilterType Type { get; private set; }

        public SoftwareBitmapSource Bitmap { get; private set; }

        private bool IsDisposed = false;

        public FilterItem(SoftwareBitmapSource Bitmap, string Text, FilterType Type)
        {
            this.Bitmap = Bitmap;
            this.Text = Text;
            this.Type = Type;
        }

        public void Dispose()
        {
            if (!IsDisposed)
            {
                IsDisposed = true;
                Bitmap.Dispose();
            }
        }
    }

    public sealed class AlphaSliderValueConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            int Value = System.Convert.ToInt32(((double)value - 1) * 100);
            return Value > 0 ? ("+" + Value) : Value.ToString();
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }

    public sealed class BetaSliderValueConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            int Value = System.Convert.ToInt32(value);
            return Value > 0 ? ("+" + Value) : Value.ToString();
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
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
        public static T FindChildOfType<T>(this DependencyObject root) where T : class
        {
            Queue<DependencyObject> ObjectQueue = new Queue<DependencyObject>();
            ObjectQueue.Enqueue(root);
            while (ObjectQueue.Count > 0)
            {
                DependencyObject Current = ObjectQueue.Dequeue();
                if (Current != null)
                {
                    for (int i = 0; i < VisualTreeHelper.GetChildrenCount(Current); i++)
                    {
                        var ChildObject = VisualTreeHelper.GetChild(Current, i);
                        if (ChildObject is T TypedChild)
                        {
                            return TypedChild;
                        }
                        ObjectQueue.Enqueue(ChildObject);
                    }
                }
            }
            return null;
        }

        public static async Task UpdateAllSubNodeFolder(this TreeViewNode ParentNode)
        {
            StorageFolder ParentFolder = ParentNode.Content as StorageFolder;
            foreach (var Package in ParentNode.Children.Select((SubNode) => new { (SubNode.Content as StorageFolder).Name, SubNode }))
            {
                Package.SubNode.Content = await ParentFolder.GetFolderAsync(Package.Name);

                if (Package.SubNode.HasChildren)
                {
                    await UpdateAllSubNodeFolder(Package.SubNode);
                }
            }
        }

        public static async Task DeleteAllSubFilesAndFolders(this StorageFolder Folder)
        {
            IReadOnlyList<IStorageItem> ItemList = await Folder.GetItemsAsync();
            foreach (var Item in ItemList)
            {
                if (Item is StorageFolder folder)
                {
                    await DeleteAllSubFilesAndFolders(folder);
                }
                else
                {
                    await Item.DeleteAsync(StorageDeleteOption.PermanentDelete);
                }
            }
        }

        public static async Task<string> GetSizeDescriptionAsync(this IStorageItem Item)
        {
            BasicProperties Properties = await Item.GetBasicPropertiesAsync();
            return Properties.Size / 1024f < 1024 ? Math.Round(Properties.Size / 1024f, 2).ToString("0.00") + " KB" :
            (Properties.Size / 1048576f < 1024 ? Math.Round(Properties.Size / 1048576f, 2).ToString("0.00") + " MB" :
            (Properties.Size / 1073741824f < 1024 ? Math.Round(Properties.Size / 1073741824f, 2).ToString("0.00") + " GB" :
            Math.Round(Properties.Size / Convert.ToDouble(1099511627776), 2).ToString() + " TB"));
        }

        public static async Task<string> GetModifiedTimeAsync(this IStorageItem Item)
        {
            var Properties = await Item.GetBasicPropertiesAsync();
            return Properties.DateModified.Year + "年" + Properties.DateModified.Month + "月" + Properties.DateModified.Day + "日, " + (Properties.DateModified.Hour < 10 ? "0" + Properties.DateModified.Hour : Properties.DateModified.Hour.ToString()) + ":" + (Properties.DateModified.Minute < 10 ? "0" + Properties.DateModified.Minute : Properties.DateModified.Minute.ToString()) + ":" + (Properties.DateModified.Second < 10 ? "0" + Properties.DateModified.Second : Properties.DateModified.Second.ToString());
        }

        public static async Task<BitmapImage> GetThumbnailBitmapAsync(this IStorageItem Item)
        {
            if (Item is StorageFolder Folder)
            {
                var Thumbnail = await Folder.GetThumbnailAsync(ThumbnailMode.ListView, 60);
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
            else if (Item is StorageFile File)
            {
                var Thumbnail = await File.GetThumbnailAsync(ThumbnailMode.ListView, 60);
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
            else
            {
                return null;
            }
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
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        /// <summary>
        /// 获取蓝牙设备名称
        /// </summary>
        public string Name
        {
            get
            {
                return string.IsNullOrWhiteSpace(DeviceInfo.Name) ? (MainPage.ThisPage.CurrentLanguage == LanguageEnum.Chinese ? "未知设备" : "Unknown") : DeviceInfo.Name;
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
                    return MainPage.ThisPage.CurrentLanguage == LanguageEnum.Chinese ? "已配对" : "Paired";
                }
                else
                {
                    return MainPage.ThisPage.CurrentLanguage == LanguageEnum.Chinese ? "准备配对" : "ReadyToPair";
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
                    return MainPage.ThisPage.CurrentLanguage == LanguageEnum.Chinese ? "取消配对" : "Unpair";
                }
                else
                {
                    return MainPage.ThisPage.CurrentLanguage == LanguageEnum.Chinese ? "配对" : "Pair";
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
        public BluetoothList(DeviceInformation DeviceInfo, BitmapImage Glyph)
        {
            this.DeviceInfo = DeviceInfo;
            this.Glyph = Glyph;
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

    #region 文件路径逐层解析类
    public sealed class PathAnalysis
    {
        public string FullPath { get; private set; }

        private Queue<string> PathQueue;

        private string CurrentLevel;

        public PathAnalysis(string FullPath, string CurrentPath)
        {
            this.FullPath = FullPath;

            CurrentLevel = CurrentPath;

            string[] Split = FullPath.Replace(CurrentPath, string.Empty).Split("\\", StringSplitOptions.RemoveEmptyEntries);

            PathQueue = new Queue<string>(Split);
        }

        public string NextPathLevel()
        {
            if (PathQueue.Count != 0)
            {
                return CurrentLevel = Path.Combine(CurrentLevel, PathQueue.Dequeue());
            }
            else
            {
                return CurrentLevel;
            }
        }
    }
    #endregion

    #region 这台电脑相关驱动器和库显示类
    public sealed class HardDeviceInfo
    {
        public BitmapImage Thumbnail { get; private set; }

        public StorageFolder Folder { get; private set; }

        public string Name
        {
            get
            {
                return Folder.DisplayName;
            }
        }

        public double Percent { get; private set; }

        public string Capacity { get; private set; }

        public ulong TotalByte { get; private set; }

        public ulong FreeByte { get; private set; }

        public string FreeSpace { get; private set; }

        public SolidColorBrush ProgressBarForeground
        {
            get
            {
                if (Percent >= 0.8)
                {
                    return new SolidColorBrush(Colors.Red);
                }
                else
                {
                    return new SolidColorBrush((Color)Application.Current.Resources["SystemAccentColor"]);
                }
            }
        }

        public string StorageSpaceDescription
        {
            get
            {
                if (MainPage.ThisPage.CurrentLanguage == LanguageEnum.Chinese)
                {
                    return FreeSpace + " 可用, 共 " + Capacity;
                }
                else
                {
                    return FreeSpace + " free of " + Capacity;
                }
            }
        }

        public HardDeviceInfo(StorageFolder Device, BitmapImage Thumbnail, IDictionary<string, object> PropertiesRetrieve)
        {
            Folder = Device ?? throw new FileNotFoundException();
            this.Thumbnail = Thumbnail;

            TotalByte = (ulong)PropertiesRetrieve["System.Capacity"];
            FreeByte = (ulong)PropertiesRetrieve["System.FreeSpace"];
            Capacity = GetSizeDescription(TotalByte);
            FreeSpace = GetSizeDescription(FreeByte);
            Percent = 1 - FreeByte / Convert.ToDouble(TotalByte);
        }

        private string GetSizeDescription(ulong Size)
        {
            return Size / 1024f < 1024 ? Math.Round(Size / 1024f, 2).ToString("0.00") + " KB" :
            (Size / 1048576f < 1024 ? Math.Round(Size / 1048576f, 2).ToString("0.00") + " MB" :
            (Size / 1073741824f < 1024 ? Math.Round(Size / 1073741824f, 2).ToString("0.00") + " GB" :
            Math.Round(Size / Convert.ToDouble(1099511627776), 2).ToString("0.00") + " TB"));
        }
    }

    public enum LibrarySource
    {
        SystemBase = 0,
        UserAdded = 1
    }

    public sealed class LibraryFolder
    {
        public string Name { get; private set; }

        public BitmapImage Thumbnail { get; private set; }

        public StorageFolder Folder { get; private set; }

        public string DisplayType
        {
            get
            {
                return Folder.DisplayType;
            }
        }

        public LibrarySource Source { get; private set; }

        public LibraryFolder(StorageFolder Folder, BitmapImage Thumbnail, LibrarySource Source)
        {
            if (Folder == null)
            {
                throw new FileNotFoundException();
            }

            Name = Folder.Name;
            this.Thumbnail = Thumbnail;
            this.Folder = Folder;
            this.Source = Source;
        }
    }
    #endregion

    #region 增量加载集合类
    public sealed class IncrementalLoadingCollection<T> : ObservableCollection<T>, ISupportIncrementalLoading
    {
        private StorageItemQueryResult Query;
        private uint CurrentIndex = 0;
        private Func<uint, uint, StorageItemQueryResult, Task<IEnumerable<T>>> MoreItemsNeed;
        private uint MaxNum = 0;

        public IncrementalLoadingCollection(Func<uint, uint, StorageItemQueryResult, Task<IEnumerable<T>>> MoreItemsNeed)
        {
            this.MoreItemsNeed = MoreItemsNeed;
        }

        public async Task SetStorageItemQuery(StorageItemQueryResult InputQuery)
        {
            Query = InputQuery;

            MaxNum = await Query.GetItemCountAsync();

            CurrentIndex = MaxNum > 100 ? 100 : MaxNum;

            if (MaxNum > 100)
            {
                HasMoreItems = true;
            }
        }

        public IAsyncOperation<LoadMoreItemsResult> LoadMoreItemsAsync(uint count)
        {
            return AsyncInfo.Run(async (c) =>
            {
                if (CurrentIndex + count >= MaxNum)
                {
                    uint ItemNeedNum = MaxNum - CurrentIndex;
                    if (ItemNeedNum == 0)
                    {
                        HasMoreItems = false;
                        return new LoadMoreItemsResult { Count = 0 };
                    }
                    else
                    {
                        IEnumerable<T> Result = await MoreItemsNeed(CurrentIndex, ItemNeedNum, Query);

                        for (int i = 0; i < Result.Count() && HasMoreItems; i++)
                        {
                            Add(Result.ElementAt(i));
                        }

                        CurrentIndex = MaxNum;
                        HasMoreItems = false;
                        return new LoadMoreItemsResult { Count = ItemNeedNum };
                    }
                }
                else
                {
                    IEnumerable<T> Result = await MoreItemsNeed(CurrentIndex, count, Query);

                    for (int i = 0; i < Result.Count() && HasMoreItems; i++)
                    {
                        Add(Result.ElementAt(i));
                    }

                    CurrentIndex += count;
                    HasMoreItems = true;
                    return new LoadMoreItemsResult { Count = count };
                }
            });
        }

        public bool HasMoreItems { get; set; } = false;
    }
    #endregion

    #region 网页信息存储类
    /// <summary>
    /// 存储网页信息
    /// </summary>
    public sealed class WebSiteItem
    {
        /// <summary>
        /// 获取网页标题
        /// </summary>
        public string Subject { get; private set; }

        /// <summary>
        /// 获取网址
        /// </summary>
        public string WebSite { get; private set; }

        /// <summary>
        /// 获取网址域名
        /// </summary>
        public string DominName
        {
            get
            {
                return WebSite.StartsWith("https://") ? WebSite.Substring(8) : WebSite.StartsWith("http://") ? WebSite.Substring(7) : WebSite.StartsWith("ftp://") ? WebSite.Substring(6) : null;
            }
        }

        /// <summary>
        /// 指示历史记录树的分类标题前的星形控件的显隐性
        /// </summary>
        public Visibility StarVisibility
        {
            get
            {
                if ((Subject == "今天" || Subject == "昨天" || Subject == "更早") && string.IsNullOrEmpty(WebSite))
                {
                    return Visibility.Collapsed;
                }
                else
                {
                    return Visibility.Visible;
                }
            }
        }

        /// <summary>
        /// 指示字体大小以区分网页内容和分类标题
        /// </summary>
        public double FontSize
        {
            get
            {
                if ((Subject == "今天" || Subject == "昨天" || Subject == "更早") && string.IsNullOrEmpty(WebSite))
                {
                    return 18;
                }
                else
                {
                    return 15;
                }
            }
        }

        /// <summary>
        /// 创建WebSiteItem的新实例
        /// </summary>
        /// <param name="Subject">网页标题</param>
        /// <param name="WebSite">网址</param>
        public WebSiteItem(string Subject, string WebSite)
        {
            this.Subject = Subject;
            this.WebSite = WebSite;
        }
    }
    #endregion

    #region 网页历史记录标志枚举
    /// <summary>
    /// 历史记录分类标题种类枚举
    /// </summary>
    [Flags]
    public enum HistoryTreeCategoryFlag
    {
        Today = 1,
        Yesterday = 2,
        Earlier = 4,
        None = 8
    }
    #endregion

    #region 下载列表模板选择器
    public sealed class DownloadTemplateSelector : DataTemplateSelector
    {
        public DataTemplate DownloadingTemplate { get; set; }
        public DataTemplate DownloadErrorTemplate { get; set; }
        public DataTemplate DownloadCompleteTemplate { get; set; }
        public DataTemplate DownloadCancelTemplate { get; set; }
        public DataTemplate DownloadPauseTemplate { get; set; }

        protected override DataTemplate SelectTemplateCore(object item, DependencyObject container)
        {
            if (item is DownloadOperator Operator)
            {
                switch (Operator.State)
                {
                    case DownloadState.AlreadyFinished:
                        return DownloadCompleteTemplate;
                    case DownloadState.Canceled:
                        return DownloadCancelTemplate;
                    case DownloadState.Downloading:
                        return DownloadingTemplate;
                    case DownloadState.Error:
                        return DownloadErrorTemplate;
                    case DownloadState.Paused:
                        return DownloadPauseTemplate;
                    default: return null;
                }
            }
            else
            {
                return null;
            }
        }
    }
    #endregion

    #region 快速启动类
    public enum QuickStartType
    {
        Application = 1,
        WebSite = 2,
        UpdateApp = 4,
        UpdateWeb = 8
    }

    public sealed class QuickStartItem : INotifyPropertyChanged
    {
        public BitmapImage Image { get; private set; }

        public string DisplayName { get; private set; }

        public string RelativePath { get; private set; }

        public QuickStartType Type { get; private set; }

        public Uri ProtocalUri { get; private set; }

        public event PropertyChangedEventHandler PropertyChanged;

        public void Update(BitmapImage Image, Uri ProtocalUri, string RelativePath, string DisplayName)
        {
            this.Image = Image;
            this.ProtocalUri = ProtocalUri;

            this.DisplayName = DisplayName;

            if (RelativePath != null)
            {
                this.RelativePath = RelativePath;
            }

            OnPropertyChanged("DisplayName");
            OnPropertyChanged("Image");
        }

        private void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        public QuickStartItem(BitmapImage Image, Uri Uri, QuickStartType Type, string RelativePath, string DisplayName = null)
        {
            this.Image = Image;
            ProtocalUri = Uri;
            this.Type = Type;

            this.DisplayName = DisplayName;
            this.RelativePath = RelativePath;
        }
    }
    #endregion

    #region 快速启动区域的模板转换器
    public sealed class QuickStartSelector : DataTemplateSelector
    {
        public DataTemplate NormalDataTemplate { get; set; }
        public DataTemplate AddDataTemplate { get; set; }

        protected override DataTemplate SelectTemplateCore(object item, DependencyObject container)
        {
            if (item is QuickStartItem Start && Start.DisplayName == null)
            {
                return AddDataTemplate;
            }
            else
            {
                return NormalDataTemplate;
            }
        }
    }
    #endregion

    #region WIFI分享功能提供器
    public sealed class WiFiShareProvider : IDisposable
    {
        private HttpListener Listener;

        private bool IsDisposed = false;

        private CancellationTokenSource Cancellation = new CancellationTokenSource();

        public event EventHandler<Exception> ThreadExitedUnexpectly;

        public KeyValuePair<string, string> FilePathMap { get; set; }

        public string CurrentUri { get; private set; }

        public bool IsListeningThreadWorking { get; private set; } = false;

        public WiFiShareProvider()
        {
            Listener = new HttpListener();
            Listener.Prefixes.Add("http://*:8125/");
            Listener.AuthenticationSchemes = AuthenticationSchemes.Anonymous;

            HostName CurrentHostName = NetworkInformation.GetHostNames().Where((IP) => IP.Type == HostNameType.Ipv4).FirstOrDefault();
            CurrentUri = "http://" + CurrentHostName + ":8125/";
        }

        public async void StartToListenRequest()
        {
            if (IsListeningThreadWorking)
            {
                return;
            }

            if (IsDisposed)
            {
                throw new ObjectDisposedException("This Object has been disposed");
            }

            IsListeningThreadWorking = true;
            try
            {
                Listener.Start();

                while (true)
                {
                    HttpListenerContext Context = await Listener.GetContextAsync();

                    _ = Task.Factory.StartNew((Para) =>
                     {
                         try
                         {
                             HttpListenerContext HttpContext = Para as HttpListenerContext;
                             if (HttpContext.Request.Url.LocalPath.Substring(1) == FilePathMap.Key)
                             {
                                 StorageFile File = StorageFile.GetFileFromPathAsync(FilePathMap.Value).AsTask().Result;
                                 using (Stream FileStream = File.OpenStreamForReadAsync().Result)
                                 {
                                     Context.Response.ContentLength64 = FileStream.Length;
                                     Context.Response.ContentType = File.ContentType;
                                     Context.Response.AddHeader("Content-Disposition", "Attachment;filename=" + Uri.EscapeDataString(File.Name));

                                     try
                                     {
                                         FileStream.CopyTo(Context.Response.OutputStream);
                                     }
                                     catch (HttpListenerException) { }
                                     finally
                                     {
                                         Context.Response.Close();
                                     }
                                 }
                             }
                             else
                             {
                                 string ErrorMessage = "<html><head><title>Error 404 Bad Request</title></head><body><p style=\"font-size:50px\">HTTP ERROR 404</p><p style=\"font-size:40px\">无法找到指定的资源，请检查URL</p></body></html>";
                                 byte[] SendByte = Encoding.UTF8.GetBytes(ErrorMessage);

                                 Context.Response.StatusCode = 404;
                                 Context.Response.StatusDescription = "Bad Request";
                                 Context.Response.ContentType = "text/html";
                                 Context.Response.ContentLength64 = SendByte.Length;
                                 Context.Response.OutputStream.Write(SendByte, 0, SendByte.Length);
                                 Context.Response.Close();
                             }
                         }
                         catch (Exception e)
                         {
                             ThreadExitedUnexpectly?.Invoke(this, e);
                         }
                     }, Context, Cancellation.Token);
                }
            }
            catch (ObjectDisposedException)
            {
                IsListeningThreadWorking = false;
            }
            catch (Exception e)
            {
                IsListeningThreadWorking = false;
                ThreadExitedUnexpectly?.Invoke(this, e);
            }

            Cancellation?.Dispose();
            Cancellation = null;
        }

        public void Dispose()
        {
            if (IsDisposed)
            {
                return;
            }
            else
            {
                IsDisposed = true;
                Cancellation?.Cancel();
                Listener.Stop();
                Listener.Abort();
                Listener.Close();
                Listener = null;
            }
        }

        ~WiFiShareProvider()
        {
            Dispose();
        }
    }
    #endregion

    #region 语言枚举
    public enum LanguageEnum
    {
        Chinese = 1,
        English = 2
    }
    #endregion

    #region 亚克力材质背景控制器
    public static class AcrylicBackgroundController
    {
        private static AcrylicBrush BackgroundBrush;

        static AcrylicBackgroundController()
        {
            BackgroundBrush = (AcrylicBrush)Application.Current.Resources["NavigationViewTopPaneBackground"];
        }

        public static double TintOpacity
        {
            get
            {
                return 1 - (double)BackgroundBrush.GetValue(AcrylicBrush.TintOpacityProperty);
            }
            set
            {
                BackgroundBrush.SetValue(AcrylicBrush.TintOpacityProperty, 1 - value);
                ApplicationData.Current.LocalSettings.Values["BackgroundTintOpacity"] = value.ToString("0.0");
            }
        }

        public static double TintLuminosityOpacity
        {
            get
            {
                return 1 - ((double?)BackgroundBrush.GetValue(AcrylicBrush.TintLuminosityOpacityProperty)).GetValueOrDefault();
            }
            set
            {
                if (value == -1)
                {
                    BackgroundBrush.SetValue(AcrylicBrush.TintLuminosityOpacityProperty, null);
                }
                else
                {
                    BackgroundBrush.SetValue(AcrylicBrush.TintLuminosityOpacityProperty, 1 - value);
                    ApplicationData.Current.LocalSettings.Values["BackgroundTintLuminosity"] = value.ToString("0.0");
                }
            }
        }

        public static Color AcrylicColor
        {
            get
            {
                return (Color)BackgroundBrush.GetValue(AcrylicBrush.TintColorProperty);
            }
            set
            {
                BackgroundBrush.SetValue(AcrylicBrush.TintColorProperty, value);
            }
        }

        public static Color GetColorFromHexString(string hex)
        {
            hex = hex.Replace("#", string.Empty);

            bool existAlpha = hex.Length == 8 || hex.Length == 4;
            bool isDoubleHex = hex.Length == 8 || hex.Length == 6;

            if (!existAlpha && hex.Length != 6 && hex.Length != 3)
            {
                throw new ArgumentException("输入的hex不是有效颜色");
            }

            int n = 0;
            byte a;
            int hexCount = isDoubleHex ? 2 : 1;
            if (existAlpha)
            {
                n = hexCount;
                a = (byte)ConvertHexToByte(hex, 0, hexCount);
                if (!isDoubleHex)
                {
                    a = (byte)(a * 16 + a);
                }
            }
            else
            {
                a = 0xFF;
            }

            var r = (byte)ConvertHexToByte(hex, n, hexCount);
            var g = (byte)ConvertHexToByte(hex, n + hexCount, hexCount);
            var b = (byte)ConvertHexToByte(hex, n + 2 * hexCount, hexCount);
            if (!isDoubleHex)
            {
                //#FD92 = #FFDD9922

                r = (byte)(r * 16 + r);
                g = (byte)(g * 16 + g);
                b = (byte)(b * 16 + b);
            }

            return Color.FromArgb(a, r, g, b);
        }

        private static uint ConvertHexToByte(string hex, int n, int count = 2)
        {
            return Convert.ToUInt32(hex.Substring(n, count), 16);
        }
    }
    #endregion

    #region DebugLog
    //public static class Log
    //{
    //    static Log()
    //    {
    //        System.Diagnostics.Debug.WriteLine(ApplicationData.Current.LocalCacheFolder.Path);
    //    }

    //    public static void Write(Exception Ex)
    //    {
    //        string Message = Ex.Message + Environment.NewLine + Ex.StackTrace;
    //        Write(Message);
    //    }

    //    public static void Write(string Message)
    //    {
    //        lock (SyncRootProvider.SyncRoot)
    //        {
    //            StorageFolder BaseFolder = ApplicationData.Current.LocalCacheFolder;
    //            StorageFile TempFile = BaseFolder.CreateFileAsync("RX_Error_Message.txt", CreationCollisionOption.OpenIfExists).AsTask().Result;
    //            FileIO.AppendTextAsync(TempFile, Message + Environment.NewLine).AsTask().Wait();
    //        }
    //    }
    //}
    #endregion

    #region 搜索建议Json解析类
    public class BaiduSearchSuggestionResult
    {
        public string q { get; set; }
        public bool p { get; set; }
        public List<string> s { get; set; }
    }

    public class Suggests
    {
        public string Txt { get; set; }
        public string Type { get; set; }
        public string Sk { get; set; }
        public double HCS { get; set; }
    }

    public class Results
    {
        public string Type { get; set; }
        public List<Suggests> Suggests { get; set; }
    }

    public class AS
    {
        public string Query { get; set; }
        public int FullResults { get; set; }
        public List<Results> Results { get; set; }
    }

    public class BingSearchSuggestionResult
    {
        public AS AS { get; set; }
    }

    #endregion

    #region ContentDialog队列实现
    public class QueueContentDialog : ContentDialog
    {
        private static readonly AutoResetEvent Locker = new AutoResetEvent(true);

        public new async Task<ContentDialogResult> ShowAsync()
        {
            await Task.Run(() =>
            {
                Locker.WaitOne();
            });

            var Result = await base.ShowAsync();
            Locker.Set();
            return Result;
        }
    }
    #endregion
}
