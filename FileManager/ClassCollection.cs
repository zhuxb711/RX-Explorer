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
using System.Runtime.InteropServices.WindowsRuntime;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel.Core;
using Windows.Devices.Enumeration;
using Windows.Foundation;
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

namespace FileManager
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
            string Command = @"Create Table If Not Exists SearchHistory (SearchText Text Not Null);
                               Create Table If Not Exists WebFavourite (Subject Text Not Null, WebSite Text Not Null, Primary Key (WebSite));
                               Create Table If Not Exists WebHistory (Subject Text Not Null, WebSite Text Not Null, DateTime Text Not Null, Primary Key (Subject, WebSite, DateTime));
                               Create Table If Not Exists DownloadHistory (UniqueID Text Not Null, ActualName Text Not Null, Uri Text Not Null, State Text Not Null, Primary Key(UniqueID));
                               Create Table If Not Exists QuickStart (Name Text Not Null, FullPath Text Not Null, Protocal Text Not Null, Type Text Not Null, Primary Key (Name,FullPath,Protocal,Type))";
            using (SqliteCommand CreateTable = new SqliteCommand(Command, OLEDB))
            {
                _ = CreateTable.ExecuteNonQuery();
            }
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
            using (SqliteCommand Command = new SqliteCommand("Insert Into SearchHistory Values (@Para)", OLEDB))
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

        public async Task DeleteQuickStartItemAsync(QuickStartItem Item)
        {
            using (SqliteCommand Command = new SqliteCommand("Delete From QuickStart Where Name = @Name And FullPath=@FullPath And Type=@Type", OLEDB))
            {
                _ = Command.Parameters.AddWithValue("@Name", Item.DisplayName);
                _ = Command.Parameters.AddWithValue("@FullPath", Item.FullPath);
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
                    StorageFile ImageFile = await StorageFile.GetFileFromPathAsync(query[1].ToString());
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
                            QuickStartItemList.Add(new KeyValuePair<QuickStartType, QuickStartItem>(QuickStartType.Application, new QuickStartItem(Bitmap, new Uri(query[2].ToString()), QuickStartType.Application, ImageFile.Path, query[0].ToString())));
                        }
                        else
                        {
                            QuickStartItemList.Add(new KeyValuePair<QuickStartType, QuickStartItem>(QuickStartType.WebSite, new QuickStartItem(Bitmap, new Uri(query[2].ToString()), QuickStartType.WebSite, ImageFile.Path, query[0].ToString())));
                        }
                    }
                }
                return QuickStartItemList;
            }
        }

        public async Task<List<string>> GetSearchHistoryAsync()
        {
            List<string> HistoryList = new List<string>();
            using (SqliteCommand Command = new SqliteCommand("Select * From SearchHistory", OLEDB))
            using (SqliteDataReader query = await Command.ExecuteReaderAsync())
            {
                while (query.Read())
                {
                    HistoryList.Add(query[0].ToString());
                }
                return HistoryList;
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
        public RemovableDeviceStorageItem(IStorageItem Item, string Size, BitmapImage Thumbnail, string ModifiedTime)
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
        public string DisplayName { get => "无文件夹"; }
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
        private StorageItemQueryResult ItemQuery;
        private TreeViewNode TrackNode;
        private bool IsProcessing = false;
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

        public FileSystemTracker(StorageItemQueryResult Query)
        {
            TrackerMode = TrackerMode.TraceFile;

            ItemQuery = Query ?? throw new ArgumentNullException("Query");

            ItemQuery.ContentsChanged += FileQuery_ContentsChanged;
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
                    ItemQuery = TrackFolder.CreateItemQueryWithOptions(Options);

                    _ = await ItemQuery.GetItemsAsync(0, 1);

                    ItemQuery.ContentsChanged += FileQuery_ContentsChanged;
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
                ItemQuery.ContentsChanged -= FileQuery_ContentsChanged;
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
                if (ItemQuery != null)
                {
                    ItemQuery.ContentsChanged += FileQuery_ContentsChanged;
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

            IReadOnlyList<IStorageItem> FileList = await sender.Folder.GetItemsAsync();

            if (FileList.Count != FilePresenter.ThisPage.FileCollection.Count)
            {
                if (FileList.Count > FilePresenter.ThisPage.FileCollection.Count)
                {
                    List<IStorageItem> AddFileList = await ExceptAsync(FilePresenter.ThisPage.FileCollection, FileList);
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
                    List<IStorageItem> DeleteFileList = await ExceptAsync(FilePresenter.ThisPage.FileCollection, FileList);
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
                if (FilePresenter.ThisPage.FileCollection.Count != 0)
                {
                    List<IStorageItem> DeleteFileList = await ExceptAsync(FilePresenter.ThisPage.FileCollection, FileList);
                    if (DeleteFileList.Count == 0)
                    {
                        IsProcessing = false;
                        return;
                    }

                    List<IStorageItem> AddFileList = await ExceptAsync(FileList, FilePresenter.ThisPage.FileCollection);
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
                    await FolderChangeAnalysis(TrackNode);
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
            if (ItemQuery != null)
            {
                ItemQuery.ContentsChanged -= FileQuery_ContentsChanged;
                ItemQuery = null;
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

        private async Task<List<IStorageItem>> ExceptAsync(IEnumerable<RemovableDeviceStorageItem> list1, IEnumerable<IStorageItem> list2)
        {
            List<IStorageItem> FileList = new List<IStorageItem>(list1.Where((y) => y.ContentType == ContentType.Folder).Select(x => x.Folder).ToList());
            FileList.AddRange(list1.Where((y) => y.ContentType == ContentType.File).Select(x => x.File));

            return await ExceptAsync(list2, FileList);
        }

        private async Task<List<IStorageItem>> ExceptAsync(IEnumerable<IStorageItem> list2, IEnumerable<RemovableDeviceStorageItem> list1)
        {
            List<IStorageItem> FileList = new List<IStorageItem>(list1.Where((y) => y.ContentType == ContentType.Folder).Select(x => x.Folder).ToList());
            FileList.AddRange(list1.Where((y) => y.ContentType == ContentType.File).Select(x => x.File));

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

        private Task<List<IStorageItem>> ExceptAsync(IEnumerable<IStorageItem> list1, IEnumerable<IStorageItem> list2)
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
                    return new List<IStorageItem>();
                }

                if (list1.Count() > list2.Count())
                {
                    return list1.Where(x => list2.All(y =>
                    {
                        if (x is StorageFile && y is StorageFile)
                        {
                            return ((StorageFile)x).FolderRelativeId != ((StorageFile)y).FolderRelativeId;
                        }
                        else if (x is StorageFile && y is StorageFolder)
                        {
                            return ((StorageFile)x).FolderRelativeId != ((StorageFolder)y).FolderRelativeId;
                        }
                        else if (x is StorageFolder && y is StorageFile)
                        {
                            return ((StorageFolder)x).FolderRelativeId != ((StorageFile)y).FolderRelativeId;
                        }
                        else
                        {
                            return ((StorageFolder)x).FolderRelativeId != ((StorageFolder)y).FolderRelativeId;
                        }
                    })).ToList();
                }
                else
                {
                    return list2.Where(x => list1.All(y =>
                    {
                        if (x is StorageFile && y is StorageFile)
                        {
                            return ((StorageFile)x).FolderRelativeId != ((StorageFile)y).FolderRelativeId;
                        }
                        else if (x is StorageFile && y is StorageFolder)
                        {
                            return ((StorageFile)x).FolderRelativeId != ((StorageFolder)y).FolderRelativeId;
                        }
                        else if (x is StorageFolder && y is StorageFile)
                        {
                            return ((StorageFolder)x).FolderRelativeId != ((StorageFile)y).FolderRelativeId;
                        }
                        else
                        {
                            return ((StorageFolder)x).FolderRelativeId != ((StorageFolder)y).FolderRelativeId;
                        }
                    })).ToList();
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
            return Properties.Size / 1024f < 1024 ? Math.Round(Properties.Size / 1024f, 2).ToString() + " KB" :
            (Properties.Size / 1048576f >= 1024 ? Math.Round(Properties.Size / 1073741824f, 2).ToString() + " GB" :
            Math.Round(Properties.Size / 1048576f, 2).ToString() + " MB");
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

    #region 文件路径逐层解析类
    public sealed class PathAnalysis
    {
        public string FullPath { get; private set; }

        private Queue<string> PathQueue;

        private string CurrentLevel = string.Empty;

        public PathAnalysis(string FullPath)
        {
            this.FullPath = FullPath;

            string[] Split = FullPath.Split("\\", StringSplitOptions.RemoveEmptyEntries);
            Split[0] = Split[0] + "\\";

            PathQueue = new Queue<string>(Split.Take(2).Concat(Split.Skip(2).Select((Item) => "\\" + Item)));
        }

        public string NextPathLevel()
        {
            if (PathQueue.Count != 0)
            {
                return CurrentLevel += PathQueue.Dequeue();
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
        public string Name { get; private set; }

        public double Percent { get; private set; }

        public string Capacity { get; private set; }

        public string FreeSpace { get; private set; }

        public string StorageSpaceDescription
        {
            get
            {
                return FreeSpace + " 可用, 共 " + Capacity;
            }
        }

        public HardDeviceInfo(StorageFolder Device, BitmapImage Thumbnail, IDictionary<string, object> PropertiesRetrieve)
        {
            if (Device == null)
            {
                throw new FileNotFoundException();
            }

            Name = Device.DisplayName;
            Folder = Device;
            this.Thumbnail = Thumbnail;
            Capacity = GetSizeDescription((ulong)PropertiesRetrieve["System.Capacity"]);
            FreeSpace = GetSizeDescription((ulong)PropertiesRetrieve["System.FreeSpace"]);
            Percent = 1 - (ulong)PropertiesRetrieve["System.FreeSpace"] / Convert.ToDouble(PropertiesRetrieve["System.Capacity"]);
        }

        private string GetSizeDescription(ulong Size)
        {
            return Size / 1024f < 1024 ? Math.Round(Size / 1024f, 2).ToString() + " KB" :
            (Size / 1048576f >= 1024 ? Math.Round(Size / 1073741824f, 2).ToString() + " GB" :
            Math.Round(Size / 1048576f, 2).ToString() + " MB");
        }
    }

    public sealed class LibraryFolder
    {
        public string Name { get; private set; }

        public BitmapImage Thumbnail { get; private set; }

        public StorageFolder Folder { get; private set; }

        public LibraryFolder(StorageFolder Folder, BitmapImage Thumbnail)
        {
            if (Folder == null)
            {
                throw new FileNotFoundException();
            }

            Name = Folder.Name;
            this.Thumbnail = Thumbnail;
            this.Folder = Folder;
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

            CurrentIndex = MaxNum > 50 ? 50 : MaxNum;

            if (MaxNum > 50)
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
                if ((Subject == "今天" || Subject == "昨天" || Subject == "更早") && WebSite == string.Empty)
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
                if ((Subject == "今天" || Subject == "昨天" || Subject == "更早") && WebSite == string.Empty)
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

        public string FullPath { get; private set; }

        public QuickStartType Type { get; private set; }

        public Uri ProtocalUri { get; private set; }

        public event PropertyChangedEventHandler PropertyChanged;

        public void Update(BitmapImage Image, Uri ProtocalUri, string FullPath, string DisplayName)
        {
            this.Image = Image;
            this.ProtocalUri = ProtocalUri;

            this.DisplayName = DisplayName;
            this.FullPath = FullPath;

            OnPropertyChanged("DisplayName");
            OnPropertyChanged("Image");
        }

        private void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        public QuickStartItem(BitmapImage Image, Uri Uri, QuickStartType Type, string FullPath, string DisplayName = null)
        {
            this.Image = Image;
            this.ProtocalUri = Uri;
            this.Type = Type;

            if (DisplayName == null)
            {
                this.DisplayName = string.Empty;
                this.FullPath = string.Empty;
            }
            else
            {
                this.DisplayName = DisplayName;
                this.FullPath = FullPath;
            }
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
            if (item is QuickStartItem Start && Start.DisplayName == string.Empty)
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

    #region 默认快速启动初始化图标包
    public sealed class InitializePackage
    {
        public string FullPath { get; private set; }

        public string Name { get; private set; }

        public string UriString { get; private set; }

        public QuickStartType Type { get; private set; }

        public InitializePackage(string Name, string FullPath, string UriString, QuickStartType Type)
        {
            this.FullPath = FullPath;
            this.Name = Name;
            this.UriString = UriString;
            this.Type = Type;
        }
    }
    #endregion
}
