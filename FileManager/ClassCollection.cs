using Bluetooth.Core.Services;
using Bluetooth.Services.Obex;
using Google.Cloud.Translation.V2;
using ICSharpCode.SharpZipLib.Zip;
using Microsoft.Data.Sqlite;
using Microsoft.Toolkit.Uwp.Notifications;
using MySql.Data.MySqlClient;
using NetworkAccess;
using SQLConnectionPoolProvider;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.Data.Common;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TinyPinyin.Core;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Core;
using Windows.Devices.Enumeration;
using Windows.Foundation;
using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Media.Editing;
using Windows.Media.MediaProperties;
using Windows.Media.Transcoding;
using Windows.Networking;
using Windows.Networking.Connectivity;
using Windows.Security.Credentials;
using Windows.Security.Cryptography;
using Windows.Security.Cryptography.Core;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.Storage.Search;
using Windows.Storage.Streams;
using Windows.System.UserProfile;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.Notifications;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using TreeView = Microsoft.UI.Xaml.Controls.TreeView;
using TreeViewItem = Microsoft.UI.Xaml.Controls.TreeViewItem;
using TreeViewNode = Microsoft.UI.Xaml.Controls.TreeViewNode;

namespace FileManager
{
    #region SQLite数据库
    /// <summary>
    /// 提供对SQLite数据库的访问支持
    /// </summary>
    public sealed class SQLite : IDisposable
    {
        private bool IsDisposed = false;
        private static SQLite SQL = null;
        private SQLConnectionPool<SqliteConnection> ConnectionPool;

        /// <summary>
        /// 初始化SQLite的实例
        /// </summary>
        private SQLite()
        {
            SQLitePCL.Batteries_V2.Init();
            SQLitePCL.raw.sqlite3_win32_set_directory(1, ApplicationData.Current.LocalFolder.Path);
            SQLitePCL.raw.sqlite3_win32_set_directory(2, ApplicationData.Current.TemporaryFolder.Path);

            ConnectionPool = new SQLConnectionPool<SqliteConnection>("Filename=RX_Sqlite.db;", 2, 0);

            InitializeDatabase();
        }

        /// <summary>
        /// 提供SQLite的实例
        /// </summary>
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

        /// <summary>
        /// 初始化数据库预先导入的数据
        /// </summary>
        private void InitializeDatabase()
        {
            string NodePadPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32\\notepad.exe");
            string Command = $@"Create Table If Not Exists SearchHistory (SearchText Text Not Null, Primary Key (SearchText));
                                Create Table If Not Exists WebFavourite (Subject Text Not Null, WebSite Text Not Null, Primary Key (WebSite));
                                Create Table If Not Exists WebHistory (Subject Text Not Null, WebSite Text Not Null, DateTime Text Not Null, Primary Key (Subject, WebSite, DateTime));
                                Create Table If Not Exists DownloadHistory (UniqueID Text Not Null, ActualName Text Not Null, Uri Text Not Null, State Text Not Null, Primary Key(UniqueID));
                                Create Table If Not Exists QuickStart (Name Text Not Null, FullPath Text Not Null, Protocal Text Not Null, Type Text Not Null, Primary Key (Name,FullPath,Protocal,Type));
                                Create Table If Not Exists FolderLibrary (Path Text Not Null, Primary Key (Path));
                                Create Table If Not Exists PathHistory (Path Text Not Null, Primary Key (Path));
                                Create Table If Not Exists BackgroundPicture (FileName Text Not Null, Primary Key (FileName));
                                Create Table If Not Exists DeviceVisibility (Path Text Not Null, IsVisible Text Not Null, Primary Key(Path));
                                Create Table If Not Exists ProgramPickerRecord (Path Text Not Null);
                                Insert Or Ignore Into BackgroundPicture Values('ms-appx:///CustomImage/Picture1.jpg');
                                Insert Or Ignore Into BackgroundPicture Values('ms-appx:///CustomImage/Picture2.jpg');
                                Insert Or Ignore Into BackgroundPicture Values('ms-appx:///CustomImage/Picture3.jpg');
                                Insert Or Ignore Into BackgroundPicture Values('ms-appx:///CustomImage/Picture4.jpg');
                                Insert Or Ignore Into BackgroundPicture Values('ms-appx:///CustomImage/Picture5.jpg');
                                Insert Or Ignore Into BackgroundPicture Values('ms-appx:///CustomImage/Picture6.jpg');
                                Insert Or Ignore Into BackgroundPicture Values('ms-appx:///CustomImage/Picture7.jpg');
                                Insert Or Ignore Into BackgroundPicture Values('ms-appx:///CustomImage/Picture8.jpg');
                                Insert Or Ignore Into BackgroundPicture Values('ms-appx:///CustomImage/Picture9.jpg');
                                Insert Or Ignore Into BackgroundPicture Values('ms-appx:///CustomImage/Picture10.jpg');
                                Insert Or Ignore Into BackgroundPicture Values('ms-appx:///CustomImage/Picture11.jpg');
                                Insert Or Ignore Into BackgroundPicture Values('ms-appx:///CustomImage/Picture12.jpg');
                                Insert Or Ignore Into BackgroundPicture Values('ms-appx:///CustomImage/Picture13.jpg');
                                Insert Or Ignore Into BackgroundPicture Values('ms-appx:///CustomImage/Picture14.jpg');
                                Delete From ProgramPickerRecord Where Path = '{NodePadPath}';
                                Insert Into ProgramPickerRecord Values ('{NodePadPath}');";
            using (SQLConnection Connection = ConnectionPool.GetConnectionFromDataBasePoolAsync().Result)
            using (SqliteCommand CreateTable = Connection.CreateDbCommandFromConnection<SqliteCommand>(Command))
            {
                _ = CreateTable.ExecuteNonQuery();
            }
        }

        public async Task SetProgramPickerRecordAsync(string Path)
        {
            using (SQLConnection Connection = await ConnectionPool.GetConnectionFromDataBasePoolAsync().ConfigureAwait(false))
            using (SqliteCommand Command = Connection.CreateDbCommandFromConnection<SqliteCommand>("Insert Into ProgramPickerRecord Values (@Path)"))
            {
                _ = Command.Parameters.AddWithValue("@Path", Path);
                _ = await Command.ExecuteNonQueryAsync().ConfigureAwait(false);
            }
        }

        public async Task<List<string>> GetProgramPickerRecordAsync()
        {
            using (SQLConnection Connection = await ConnectionPool.GetConnectionFromDataBasePoolAsync().ConfigureAwait(false))
            using (SqliteCommand Command = Connection.CreateDbCommandFromConnection<SqliteCommand>("Select * From ProgramPickerRecord"))
            using (SqliteDataReader query = await Command.ExecuteReaderAsync().ConfigureAwait(false))
            {
                List<string> list = new List<string>();
                while (query.Read())
                {
                    list.Add(query[0].ToString());
                }
                return list;
            }
        }

        public async Task DeleteProgramPickerRecordAsync(string Path)
        {
            using (SQLConnection Connection = await ConnectionPool.GetConnectionFromDataBasePoolAsync().ConfigureAwait(false))
            using (SqliteCommand Command = Connection.CreateDbCommandFromConnection<SqliteCommand>("Delete From ProgramPickerRecord Where Path=@Path"))
            {
                _ = Command.Parameters.AddWithValue("@Path", Path);
                _ = await Command.ExecuteNonQueryAsync().ConfigureAwait(false);
            }
        }

        public async Task<Dictionary<string, bool>> GetDeviceVisibilityMapAsync()
        {
            using (SQLConnection Connection = await ConnectionPool.GetConnectionFromDataBasePoolAsync().ConfigureAwait(false))
            using (SqliteCommand Command = Connection.CreateDbCommandFromConnection<SqliteCommand>("Select * From DeviceVisibility"))
            using (SqliteDataReader query = await Command.ExecuteReaderAsync().ConfigureAwait(false))
            {
                Dictionary<string, bool> Dic = new Dictionary<string, bool>();
                while (query.Read())
                {
                    Dic.Add(query[0].ToString(), Convert.ToBoolean(query[1]));
                }
                return Dic;
            }
        }

        public async Task SetDeviceVisibilityAsync(string Path, bool IsVisible)
        {
            using (SQLConnection Connection = await ConnectionPool.GetConnectionFromDataBasePoolAsync().ConfigureAwait(false))
            using (SqliteCommand Command1 = Connection.CreateDbCommandFromConnection<SqliteCommand>("Select Count(*) From DeviceVisibility Where Path=@Path"))
            {
                _ = Command1.Parameters.AddWithValue("@Path", Path);
                if (Convert.ToInt32(await Command1.ExecuteScalarAsync().ConfigureAwait(false)) == 0)
                {
                    using (SqliteCommand Command = Connection.CreateDbCommandFromConnection<SqliteCommand>("Insert Into DeviceVisibility Values (@Path,@IsVisible)"))
                    {
                        _ = Command.Parameters.AddWithValue("@Path", Path);
                        _ = Command.Parameters.AddWithValue("@IsVisible", IsVisible.ToString());
                        _ = await Command.ExecuteNonQueryAsync().ConfigureAwait(false);
                    }
                }
                else
                {
                    using (SqliteCommand Command = Connection.CreateDbCommandFromConnection<SqliteCommand>("Update DeviceVisibility Set IsVisible=@IsVisible Where Path=@Path"))
                    {
                        _ = Command.Parameters.AddWithValue("@Path", Path);
                        _ = Command.Parameters.AddWithValue("@IsVisible", IsVisible.ToString());
                        _ = await Command.ExecuteNonQueryAsync().ConfigureAwait(false);
                    }
                }
            }
        }

        /// <summary>
        /// 保存背景图片的Uri路径
        /// </summary>
        /// <param name="uri">图片Uri</param>
        /// <returns></returns>
        public async Task SetBackgroundPictureAsync(Uri uri)
        {
            if (uri != null)
            {
                using (SQLConnection Connection = await ConnectionPool.GetConnectionFromDataBasePoolAsync().ConfigureAwait(false))
                using (SqliteCommand Command = Connection.CreateDbCommandFromConnection<SqliteCommand>("Insert Into BackgroundPicture Values (@FileName)"))
                {
                    _ = Command.Parameters.AddWithValue("@FileName", uri.ToString());
                    _ = await Command.ExecuteNonQueryAsync().ConfigureAwait(false);
                }
            }
            else
            {
                throw new ArgumentNullException(nameof(uri), "Parameter could not be null");
            }
        }

        /// <summary>
        /// 获取背景图片的Uri信息
        /// </summary>
        /// <returns></returns>
        public async Task<List<Uri>> GetBackgroundPictureAsync()
        {
            List<Uri> list = new List<Uri>();
            using (SQLConnection Connection = await ConnectionPool.GetConnectionFromDataBasePoolAsync().ConfigureAwait(false))
            using (SqliteCommand Command = Connection.CreateDbCommandFromConnection<SqliteCommand>("Select * From BackgroundPicture"))
            using (SqliteDataReader query = await Command.ExecuteReaderAsync().ConfigureAwait(false))
            {
                while (query.Read())
                {
                    list.Add(new Uri(query[0].ToString()));
                }
            }
            return list;
        }

        /// <summary>
        /// 删除背景图片的Uri信息
        /// </summary>
        /// <param name="uri">图片Uri</param>
        /// <returns></returns>
        public async Task DeleteBackgroundPictureAsync(Uri uri)
        {
            if (uri != null)
            {
                using (SQLConnection Connection = await ConnectionPool.GetConnectionFromDataBasePoolAsync().ConfigureAwait(false))
                using (SqliteCommand Command = Connection.CreateDbCommandFromConnection<SqliteCommand>("Delete From BackgroundPicture Where FileName=@FileName"))
                {
                    _ = Command.Parameters.AddWithValue("@FileName", uri.ToString());
                    _ = await Command.ExecuteNonQueryAsync().ConfigureAwait(false);
                }
            }
            else
            {
                throw new ArgumentNullException(nameof(uri), "Parameter could not be null");
            }
        }

        /// <summary>
        /// 获取文件夹和库区域内用户自定义的文件夹路径
        /// </summary>
        /// <returns></returns>
        public async Task<List<string>> GetFolderLibraryAsync()
        {
            List<string> list = new List<string>();
            using (SQLConnection Connection = await ConnectionPool.GetConnectionFromDataBasePoolAsync().ConfigureAwait(false))
            using (SqliteCommand Command = Connection.CreateDbCommandFromConnection<SqliteCommand>("Select * From FolderLibrary"))
            using (SqliteDataReader query = await Command.ExecuteReaderAsync().ConfigureAwait(false))
            {
                while (query.Read())
                {
                    list.Add(query[0].ToString());
                }
            }
            return list;
        }

        /// <summary>
        /// 删除文件夹和库区域的用户自定义文件夹的数据
        /// </summary>
        /// <param name="Path">自定义文件夹的路径</param>
        /// <returns></returns>
        public async Task DeleteFolderLibraryAsync(string Path)
        {
            using (SQLConnection Connection = await ConnectionPool.GetConnectionFromDataBasePoolAsync().ConfigureAwait(false))
            using (SqliteCommand Command = Connection.CreateDbCommandFromConnection<SqliteCommand>("Delete From FolderLibrary Where Path = @Path"))
            {
                _ = Command.Parameters.AddWithValue("@Path", Path);
                _ = await Command.ExecuteNonQueryAsync().ConfigureAwait(false);
            }
        }

        /// <summary>
        /// 保存文件路径栏的记录
        /// </summary>
        /// <param name="Path">输入的文件路径</param>
        /// <returns></returns>
        public async Task SetPathHistoryAsync(string Path)
        {
            using (SQLConnection Connection = await ConnectionPool.GetConnectionFromDataBasePoolAsync().ConfigureAwait(false))
            using (SqliteCommand Command = Connection.CreateDbCommandFromConnection<SqliteCommand>("Insert Or Ignore Into PathHistory Values (@Para)"))
            {
                _ = Command.Parameters.AddWithValue("@Para", Path);
                _ = await Command.ExecuteNonQueryAsync().ConfigureAwait(false);
            }
        }

        /// <summary>
        /// 模糊查询与文件路径栏相关的输入历史记录
        /// </summary>
        /// <param name="Target">输入内容</param>
        /// <returns></returns>
        public async Task<List<string>> GetRelatedPathHistoryAsync()
        {
            List<string> PathList = new List<string>();
            using (SQLConnection Connection = await ConnectionPool.GetConnectionFromDataBasePoolAsync().ConfigureAwait(false))
            using (SqliteCommand Command = Connection.CreateDbCommandFromConnection<SqliteCommand>("Select * From PathHistory"))
            using (SqliteDataReader query = await Command.ExecuteReaderAsync().ConfigureAwait(false))
            {
                while (query.Read())
                {
                    PathList.Add(query[0].ToString());
                }
            }
            return PathList;
        }

        /// <summary>
        /// 保存在文件夹和库区域显示的用户自定义文件夹路径
        /// </summary>
        /// <param name="Path">文件夹路径</param>
        /// <returns></returns>
        public async Task SetFolderLibraryAsync(string Path)
        {
            using (SQLConnection Connection = await ConnectionPool.GetConnectionFromDataBasePoolAsync().ConfigureAwait(false))
            using (SqliteCommand Command = Connection.CreateDbCommandFromConnection<SqliteCommand>("Insert Into FolderLibrary Values (@Path)"))
            {
                _ = Command.Parameters.AddWithValue("@Path", Path);
                _ = await Command.ExecuteNonQueryAsync().ConfigureAwait(false);
            }
        }

        /// <summary>
        /// 保存搜索历史记录
        /// </summary>
        /// <param name="SearchText">搜索内容</param>
        /// <returns></returns>
        public async Task SetSearchHistoryAsync(string SearchText)
        {
            using (SQLConnection Connection = await ConnectionPool.GetConnectionFromDataBasePoolAsync().ConfigureAwait(false))
            using (SqliteCommand Command = Connection.CreateDbCommandFromConnection<SqliteCommand>("Insert Or Ignore Into SearchHistory Values (@Para)"))
            {
                _ = Command.Parameters.AddWithValue("@Para", SearchText);
                _ = await Command.ExecuteNonQueryAsync().ConfigureAwait(false);
            }
        }

        /// <summary>
        /// 保存快速启动栏内的信息
        /// </summary>
        /// <param name="Name">显示标题</param>
        /// <param name="FullPath">图标所在的路径</param>
        /// <param name="Protocal">使用的协议</param>
        /// <param name="Type">快速启动类型</param>
        /// <returns></returns>
        public async Task SetQuickStartItemAsync(string Name, string FullPath, string Protocal, QuickStartType Type)
        {
            using (SQLConnection Connection = await ConnectionPool.GetConnectionFromDataBasePoolAsync().ConfigureAwait(false))
            using (SqliteCommand Command = Connection.CreateDbCommandFromConnection<SqliteCommand>("Insert Or Ignore Into QuickStart Values (@Name,@Path,@Protocal,@Type)"))
            {
                _ = Command.Parameters.AddWithValue("@Name", Name);
                _ = Command.Parameters.AddWithValue("@Path", FullPath);
                _ = Command.Parameters.AddWithValue("@Protocal", Protocal);
                _ = Command.Parameters.AddWithValue("@Type", Enum.GetName(typeof(QuickStartType), Type));
                _ = await Command.ExecuteNonQueryAsync().ConfigureAwait(false);
            }
        }

        /// <summary>
        /// 更新快速启动项的内容
        /// </summary>
        /// <param name="OldName">旧名称</param>
        /// <param name="NewName">新名称</param>
        /// <param name="FullPath">图片路径</param>
        /// <param name="Protocal">协议</param>
        /// <param name="Type">快速启动项类型</param>
        /// <returns></returns>
        public async Task UpdateQuickStartItemAsync(string OldName, string NewName, string FullPath, string Protocal, QuickStartType Type)
        {
            using (SQLConnection Connection = await ConnectionPool.GetConnectionFromDataBasePoolAsync().ConfigureAwait(false))
            {
                if (FullPath != null)
                {
                    using (SqliteCommand Command = Connection.CreateDbCommandFromConnection<SqliteCommand>("Update QuickStart Set Name=@NewName, FullPath=@Path, Protocal=@Protocal Where Name=@OldName And Type=@Type"))
                    {
                        _ = Command.Parameters.AddWithValue("@OldName", OldName);
                        _ = Command.Parameters.AddWithValue("@Path", FullPath);
                        _ = Command.Parameters.AddWithValue("@NewName", NewName);
                        _ = Command.Parameters.AddWithValue("@Protocal", Protocal);
                        _ = Command.Parameters.AddWithValue("@Type", Enum.GetName(typeof(QuickStartType), Type));
                        _ = await Command.ExecuteNonQueryAsync().ConfigureAwait(false);
                    }
                }
                else
                {
                    using (SqliteCommand Command = Connection.CreateDbCommandFromConnection<SqliteCommand>("Update QuickStart Set Name=@NewName, Protocal=@Protocal Where Name=@OldName And Type=@Type"))
                    {
                        _ = Command.Parameters.AddWithValue("@OldName", OldName);
                        _ = Command.Parameters.AddWithValue("@NewName", NewName);
                        _ = Command.Parameters.AddWithValue("@Protocal", Protocal);
                        _ = Command.Parameters.AddWithValue("@Type", Enum.GetName(typeof(QuickStartType), Type));
                        _ = await Command.ExecuteNonQueryAsync().ConfigureAwait(false);
                    }
                }
            }
        }

        /// <summary>
        /// 删除快速启动项的内容
        /// </summary>
        /// <param name="Item">要删除的项</param>
        /// <returns></returns>
        public async Task DeleteQuickStartItemAsync(QuickStartItem Item)
        {
            if (Item != null)
            {
                using (SQLConnection Connection = await ConnectionPool.GetConnectionFromDataBasePoolAsync().ConfigureAwait(false))
                using (SqliteCommand Command = Connection.CreateDbCommandFromConnection<SqliteCommand>("Delete From QuickStart Where Name = @Name And FullPath = @FullPath And Type=@Type"))
                {
                    _ = Command.Parameters.AddWithValue("@Name", Item.DisplayName);
                    _ = Command.Parameters.AddWithValue("@FullPath", Item.RelativePath);
                    _ = Command.Parameters.AddWithValue("@Type", Enum.GetName(typeof(QuickStartType), Item.Type));
                    _ = await Command.ExecuteNonQueryAsync().ConfigureAwait(false);
                }
            }
            else
            {
                throw new ArgumentNullException(nameof(Item), "Parameter could not be null");
            }
        }

        /// <summary>
        /// 获取所有快速启动项
        /// </summary>
        /// <returns></returns>
        public async Task<List<KeyValuePair<QuickStartType, QuickStartItem>>> GetQuickStartItemAsync()
        {
            using (SQLConnection Connection = await ConnectionPool.GetConnectionFromDataBasePoolAsync().ConfigureAwait(true))
            {
                List<Tuple<string, string, string>> ErrorList = new List<Tuple<string, string, string>>();
                List<KeyValuePair<QuickStartType, QuickStartItem>> Result = new List<KeyValuePair<QuickStartType, QuickStartItem>>();

                using (SqliteCommand Command = Connection.CreateDbCommandFromConnection<SqliteCommand>("Select * From QuickStart"))
                using (SqliteDataReader Reader = await Command.ExecuteReaderAsync().ConfigureAwait(true))
                {
                    while (Reader.Read())
                    {
                        try
                        {
                            if (Reader[1].ToString().StartsWith("ms-appx"))
                            {
                                BitmapImage Bitmap = new BitmapImage(new Uri(Reader[1].ToString()))
                                {
                                    DecodePixelHeight = 100,
                                    DecodePixelWidth = 100
                                };

                                if ((QuickStartType)Enum.Parse(typeof(QuickStartType), Reader[3].ToString()) == QuickStartType.Application)
                                {
                                    Result.Add(new KeyValuePair<QuickStartType, QuickStartItem>(QuickStartType.Application, new QuickStartItem(Bitmap, new Uri(Reader[2].ToString()), QuickStartType.Application, Reader[1].ToString(), Reader[0].ToString())));
                                }
                                else
                                {
                                    Result.Add(new KeyValuePair<QuickStartType, QuickStartItem>(QuickStartType.WebSite, new QuickStartItem(Bitmap, new Uri(Reader[2].ToString()), QuickStartType.WebSite, Reader[1].ToString(), Reader[0].ToString())));
                                }
                            }
                            else
                            {
                                StorageFile ImageFile = await StorageFile.GetFileFromPathAsync(Path.Combine(ApplicationData.Current.LocalFolder.Path, Reader[1].ToString()));

                                using (IRandomAccessStream Stream = await ImageFile.OpenAsync(FileAccessMode.Read))
                                {
                                    BitmapImage Bitmap = new BitmapImage
                                    {
                                        DecodePixelHeight = 100,
                                        DecodePixelWidth = 100
                                    };
                                    await Bitmap.SetSourceAsync(Stream);

                                    if ((QuickStartType)Enum.Parse(typeof(QuickStartType), Reader[3].ToString()) == QuickStartType.Application)
                                    {
                                        Result.Add(new KeyValuePair<QuickStartType, QuickStartItem>(QuickStartType.Application, new QuickStartItem(Bitmap, new Uri(Reader[2].ToString()), QuickStartType.Application, Reader[1].ToString(), Reader[0].ToString())));
                                    }
                                    else
                                    {
                                        Result.Add(new KeyValuePair<QuickStartType, QuickStartItem>(QuickStartType.WebSite, new QuickStartItem(Bitmap, new Uri(Reader[2].ToString()), QuickStartType.WebSite, Reader[1].ToString(), Reader[0].ToString())));
                                    }
                                }
                            }
                        }
                        catch (Exception)
                        {
                            ErrorList.Add(new Tuple<string, string, string>(Reader[0].ToString(), Reader[1].ToString(), Reader[3].ToString()));
                        }
                    }
                }

                foreach (var ErrorItem in ErrorList)
                {
                    using (SqliteCommand Command = Connection.CreateDbCommandFromConnection<SqliteCommand>("Delete From QuickStart Where Name = @Name And FullPath = @FullPath And Type=@Type"))
                    {
                        _ = Command.Parameters.AddWithValue("@Name", ErrorItem.Item1);
                        _ = Command.Parameters.AddWithValue("@FullPath", ErrorItem.Item2);
                        _ = Command.Parameters.AddWithValue("@Type", ErrorItem.Item3);
                        _ = await Command.ExecuteNonQueryAsync().ConfigureAwait(true);
                    }
                }

                return Result;
            }
        }

        /// <summary>
        /// 获取与搜索内容有关的搜索历史
        /// </summary>
        /// <param name="Target">搜索内容</param>
        /// <returns></returns>
        public async Task<List<string>> GetRelatedSearchHistoryAsync(string Target)
        {
            List<string> HistoryList = new List<string>();
            using (SQLConnection Connection = await ConnectionPool.GetConnectionFromDataBasePoolAsync().ConfigureAwait(false))
            using (SqliteCommand Command = Connection.CreateDbCommandFromConnection<SqliteCommand>("Select * From SearchHistory Where SearchText Like @Target"))
            {
                _ = Command.Parameters.AddWithValue("@Target", "%" + Target + "%");
                using (SqliteDataReader query = await Command.ExecuteReaderAsync().ConfigureAwait(false))
                {
                    while (query.Read())
                    {
                        HistoryList.Add(query[0].ToString());
                    }
                    return HistoryList;
                }
            }
        }

        /// <summary>
        /// 清空特定的数据表
        /// </summary>
        /// <param name="TableName">数据表名</param>
        /// <returns></returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Security", "CA2100:Review SQL queries for security vulnerabilities", Justification = "<挂起>")]
        public async Task ClearTableAsync(string TableName)
        {
            using (SQLConnection Connection = await ConnectionPool.GetConnectionFromDataBasePoolAsync().ConfigureAwait(false))
            using (SqliteCommand Command = Connection.CreateDbCommandFromConnection<SqliteCommand>("Delete From " + TableName))
            {
                _ = await Command.ExecuteNonQueryAsync().ConfigureAwait(false);
            }
        }

        /// <summary>
        /// 清空搜索历史记录
        /// </summary>
        /// <returns></returns>
        public async Task ClearSearchHistoryRecord()
        {
            using (SQLConnection Connection = await ConnectionPool.GetConnectionFromDataBasePoolAsync().ConfigureAwait(false))
            using (SqliteCommand Command = Connection.CreateDbCommandFromConnection<SqliteCommand>("Delete From SearchHistory"))
            {
                _ = await Command.ExecuteNonQueryAsync().ConfigureAwait(false);
            }
        }

        /// <summary>
        /// 调用此方法以注销数据库连接
        /// </summary>
        public void Dispose()
        {
            if (!IsDisposed)
            {
                IsDisposed = true;

                ConnectionPool.Dispose();
                ConnectionPool = null;
                SQL = null;

                GC.SuppressFinalize(this);
            }
        }

        ~SQLite()
        {
            Dispose();
        }
    }
    #endregion

    #region MySQL数据库
    /// <summary>
    /// 提供对MySQL数据库的访问支持
    /// </summary>
    public sealed class MySQL : IDisposable
    {
        private static MySQL Instance;

        private bool IsDisposed = false;

        private AutoResetEvent ConnectionLocker;

        private SQLConnectionPool<MySqlConnection> ConnectionPool;

        /// <summary>
        /// 提供对MySQL实例的访问
        /// </summary>
        public static MySQL Current
        {
            get
            {
                lock (SyncRootProvider.SyncRoot)
                {
                    return Instance ?? (Instance = new MySQL());
                }
            }
        }

        /// <summary>
        /// 初始化MySQL实例
        /// </summary>
        private MySQL()
        {
            ConnectionLocker = new AutoResetEvent(true);
            using (SecureString Secure = SecureAccessProvider.GetMySQLAccessCredential(Package.Current))
            {
                IntPtr Bstr = Marshal.SecureStringToBSTR(Secure);
                string AccessCredential = Marshal.PtrToStringBSTR(Bstr);

                try
                {
                    ConnectionPool = new SQLConnectionPool<MySqlConnection>($"{AccessCredential}CharSet=utf8;Database=FeedBackDataBase;", 2, 1);
                }
                finally
                {
                    Marshal.ZeroFreeBSTR(Bstr);
                    unsafe
                    {
                        fixed (char* ClearPtr = AccessCredential)
                        {
                            for (int i = 0; i < AccessCredential.Length; i++)
                            {
                                ClearPtr[i] = '\0';
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 从数据库连接池中获取连接对象
        /// </summary>
        /// <returns></returns>
        public Task<SQLConnection> GetConnectionFromPoolAsync()
        {
            return Task.Run(() =>
            {
                ConnectionLocker.WaitOne();

                SQLConnection Connection = ConnectionPool.GetConnectionFromDataBasePoolAsync().Result;

                #region MySQL数据库存储过程和触发器初始化代码，仅首次运行时需要
                //if (Connection.IsConnected)
                //{
                //    StringBuilder Builder = new StringBuilder();
                //    Builder.AppendLine("Create Table If Not Exists FeedBackTable (UserName Text Not Null, Title Text Not Null, Suggestion Text Not Null, LikeNum Text Not Null, DislikeNum Text Not Null, UserID Text Not Null, GUID Text Not Null);")
                //           .AppendLine("Create Table If Not Exists VoteRecordTable (UserID Text Not Null, GUID Text Not Null, Behavior Text Not Null);")

                //           .AppendLine("Drop Trigger RemoveVoteRecordTrigger;")
                //           .AppendLine("Create Trigger RemoveVoteRecordTrigger After Delete On FeedBackTable For Each Row Delete From VoteRecordTable Where GUID=old.GUID;")

                //           .AppendLine("Drop Procedure If Exists GetFeedBackProcedure;")
                //           .AppendLine("Create Procedure GetFeedBackProcedure(IN Para Text)")
                //           .AppendLine("Begin")
                //           .AppendLine("Declare EndSignal int Default 0;")
                //           .AppendLine("Declare P1 Text;")
                //           .AppendLine("Declare P2 Text;")
                //           .AppendLine("Declare P3 Text;")
                //           .AppendLine("Declare P4 Text;")
                //           .AppendLine("Declare P5 Text;")
                //           .AppendLine("Declare P6 Text;")
                //           .AppendLine("Declare P7 Text;")
                //           .AppendLine("Declare P8 Text;")
                //           .AppendLine("Declare RowData Cursor For Select * From FeedBackTable;")
                //           .AppendLine("Declare Continue Handler For Not Found Set EndSignal=1;")
                //           .AppendLine("Drop Table If Exists DataTemporary;")
                //           .AppendLine("Create Temporary Table DataTemporary (UserName Text, Title Text, Suggestion Text, LikeNum Text, DislikeNum Text, UserID Text, GUID Text, Behavior Text);")
                //           .AppendLine("Open RowData;")
                //           .AppendLine("Fetch RowData Into P1,P2,P3,P4,P5,P6,P7;")
                //           .AppendLine("While EndSignal<>1 Do")
                //           .AppendLine("If (Select Count(*) From VoteRecordTable Where UserID=Para And GUID=P7) <> 0")
                //           .AppendLine("Then")
                //           .AppendLine("Select Behavior Into P8 From VoteRecordTable Where UserID=Para And GUID=P7;")
                //           .AppendLine("Else")
                //           .AppendLine("Set P8 = 'NULL';")
                //           .AppendLine("End If;")
                //           .AppendLine("Insert Into DataTemporary Values (P1,P2,P3,P4,P5,P6,P7,P8);")
                //           .AppendLine("Fetch RowData Into P1,P2,P3,P4,P5,P6,P7;")
                //           .AppendLine("End While;")
                //           .AppendLine("Close RowData;")
                //           .AppendLine("Select * From DataTemporary;")
                //           .AppendLine("End;")

                //           .AppendLine("Drop Procedure If Exists UpdateFeedBackVoteProcedure;")
                //           .AppendLine("Create Procedure UpdateFeedBackVoteProcedure(IN LNum Text,IN DNum Text,IN UID Text,IN GID Text,IN Beh Text)")
                //           .AppendLine("Begin")
                //           .AppendLine("Update FeedBackTable Set LikeNum=LNum, DislikeNum=DNum Where GUID=GID;")
                //           .AppendLine("If (Select Count(*) From VoteRecordTable Where UserID=UID And GUID=GID) <> 0")
                //           .AppendLine("Then")
                //           .AppendLine("If Beh <> '='")
                //           .AppendLine("Then")
                //           .AppendLine("Update VoteRecordTable Set Behavior=Beh Where UserID=UID And GUID=GID;")
                //           .AppendLine("Else")
                //           .AppendLine("Delete From VoteRecordTable Where UserID=UID And GUID=GID;")
                //           .AppendLine("End If;")
                //           .AppendLine("Else")
                //           .AppendLine("If Beh <> '='")
                //           .AppendLine("Then")
                //           .AppendLine("Insert Into VoteRecordTable Values (UID,GID,Beh);")
                //           .AppendLine("End If;")
                //           .AppendLine("End If;")
                //           .AppendLine("End;");
                //    using (MySqlCommand Command = Connection.CreateDbCommandFromConnection<MySqlCommand>(Builder.ToString()))
                //    {
                //        _ = Command.ExecuteNonQuery();
                //    }
                //}
                #endregion

                ConnectionLocker.Set();

                return Connection;
            });
        }

        /// <summary>
        /// 获取所有反馈对象
        /// </summary>
        /// <returns></returns>
        public async IAsyncEnumerable<FeedBackItem> GetAllFeedBackAsync()
        {
            using (SQLConnection Connection = await GetConnectionFromPoolAsync().ConfigureAwait(false))
            {
                if (Connection.IsConnected)
                {
                    using (MySqlCommand Command = Connection.CreateDbCommandFromConnection<MySqlCommand>("GetFeedBackProcedure", CommandType.StoredProcedure))
                    {
                        _ = Command.Parameters.AddWithValue("Para", SettingPage.ThisPage.UserID);
                        using (DbDataReader Reader = await Command.ExecuteReaderAsync().ConfigureAwait(false))
                        {
                            while (await Reader.ReadAsync().ConfigureAwait(false))
                            {
                                if (Reader["Behavior"].ToString() != "NULL")
                                {
                                    yield return new FeedBackItem(Globalization.Language == LanguageEnum.Chinese ? Reader["UserName"].ToString() : (Reader["UserName"].ToString().All((Char) => !PinyinHelper.IsChinese(Char)) ? Reader["UserName"].ToString() : PinyinHelper.GetPinyin(Reader["UserName"].ToString())), Reader["Title"].ToString(), Reader["Suggestion"].ToString(), Reader["LikeNum"].ToString(), Reader["DislikeNum"].ToString(), Reader["UserID"].ToString(), Reader["GUID"].ToString(), Reader["Behavior"].ToString());
                                }
                                else
                                {
                                    yield return new FeedBackItem(Globalization.Language == LanguageEnum.Chinese ? Reader["UserName"].ToString() : (Reader["UserName"].ToString().All((Char) => !PinyinHelper.IsChinese(Char)) ? Reader["UserName"].ToString() : PinyinHelper.GetPinyin(Reader["UserName"].ToString())), Reader["Title"].ToString(), Reader["Suggestion"].ToString(), Reader["LikeNum"].ToString(), Reader["DislikeNum"].ToString(), Reader["UserID"].ToString(), Reader["GUID"].ToString());
                                }
                            }
                        }
                    }
                }
                else
                {
                    yield break;
                }
            }
        }

        /// <summary>
        /// 更新反馈对象的投票信息
        /// </summary>
        /// <param name="Item">反馈对象</param>
        /// <returns></returns>
        public async Task<bool> UpdateFeedBackVoteAsync(FeedBackItem Item)
        {
            if (Item != null)
            {
                using (SQLConnection Connection = await GetConnectionFromPoolAsync().ConfigureAwait(false))
                {
                    if (Connection.IsConnected)
                    {
                        try
                        {
                            using (MySqlCommand Command = Connection.CreateDbCommandFromConnection<MySqlCommand>("UpdateFeedBackVoteProcedure", CommandType.StoredProcedure))
                            {
                                _ = Command.Parameters.AddWithValue("LNum", Item.LikeNum);
                                _ = Command.Parameters.AddWithValue("DNum", Item.DislikeNum);
                                _ = Command.Parameters.AddWithValue("Beh", Item.UserVoteAction);
                                _ = Command.Parameters.AddWithValue("GID", Item.GUID);
                                _ = Command.Parameters.AddWithValue("UID", SettingPage.ThisPage.UserID);
                                _ = await Command.ExecuteNonQueryAsync().ConfigureAwait(false);
                            }
                            return true;
                        }
                        catch (Exception)
                        {
                            return false;
                        }
                    }
                    else
                    {
                        return false;
                    }
                }
            }
            else
            {
                throw new ArgumentNullException(nameof(Item), "Parameter could not be null");
            }
        }

        /// <summary>
        /// 更新反馈对象的标题和建议内容
        /// </summary>
        /// <param name="Title">标题</param>
        /// <param name="Suggestion">建议</param>
        /// <param name="Guid">唯一标识</param>
        /// <returns></returns>
        public async Task<bool> UpdateFeedBackTitleAndSuggestionAsync(string Title, string Suggestion, string Guid)
        {
            using (SQLConnection Connection = await GetConnectionFromPoolAsync().ConfigureAwait(false))
            {
                if (Connection.IsConnected)
                {
                    try
                    {
                        using (MySqlCommand Command = Connection.CreateDbCommandFromConnection<MySqlCommand>("Update FeedBackTable Set Title=@NewTitle, Suggestion=@NewSuggestion Where GUID=@GUID"))
                        {
                            _ = Command.Parameters.AddWithValue("@NewTitle", Title);
                            _ = Command.Parameters.AddWithValue("@NewSuggestion", Suggestion);
                            _ = Command.Parameters.AddWithValue("@GUID", Guid);
                            _ = Command.ExecuteNonQuery();
                        }
                        return true;
                    }
                    catch (Exception)
                    {
                        return false;
                    }
                }
                else
                {
                    return false;
                }
            }
        }

        /// <summary>
        /// 删除反馈内容
        /// </summary>
        /// <param name="Item">反馈对象</param>
        /// <returns></returns>
        public async Task<bool> DeleteFeedBackAsync(FeedBackItem Item)
        {
            if (Item != null)
            {
                using (SQLConnection Connection = await GetConnectionFromPoolAsync().ConfigureAwait(false))
                {
                    if (Connection.IsConnected)
                    {
                        try
                        {
                            using (MySqlCommand Command = Connection.CreateDbCommandFromConnection<MySqlCommand>("Delete From FeedBackTable Where GUID=@GUID"))
                            {
                                _ = Command.Parameters.AddWithValue("@GUID", Item.GUID);
                                _ = Command.ExecuteNonQuery();
                            }

                            return true;
                        }
                        catch (Exception)
                        {
                            return false;
                        }
                    }
                    else
                    {
                        return false;
                    }
                }
            }
            else
            {
                throw new ArgumentNullException(nameof(Item), "Parameter could not be null");
            }
        }

        /// <summary>
        /// 提交反馈内容
        /// </summary>
        /// <param name="Item">反馈对象</param>
        /// <returns></returns>
        public async Task<bool> SetFeedBackAsync(FeedBackItem Item)
        {
            if (Item != null)
            {
                using (SQLConnection Connection = await GetConnectionFromPoolAsync().ConfigureAwait(false))
                {
                    if (Connection.IsConnected)
                    {
                        try
                        {
                            using (MySqlCommand Command = Connection.CreateDbCommandFromConnection<MySqlCommand>("Insert Into FeedBackTable Values (@UserName,@Title,@Suggestion,@Like,@Dislike,@UserID,@GUID)"))
                            {
                                _ = Command.Parameters.AddWithValue("@UserName", Item.UserName);
                                _ = Command.Parameters.AddWithValue("@Title", Item.Title);
                                _ = Command.Parameters.AddWithValue("@Suggestion", Item.Suggestion);
                                _ = Command.Parameters.AddWithValue("@Like", Item.LikeNum);
                                _ = Command.Parameters.AddWithValue("@Dislike", Item.DislikeNum);
                                _ = Command.Parameters.AddWithValue("@UserID", Item.UserID);
                                _ = Command.Parameters.AddWithValue("@GUID", Item.GUID);
                                _ = Command.ExecuteNonQuery();
                            }
                            return true;
                        }
                        catch (Exception)
                        {
                            return false;
                        }
                    }
                    else
                    {
                        return false;
                    }
                }
            }
            else
            {
                throw new ArgumentNullException(nameof(Item), "Parameter could not be null");
            }
        }

        /// <summary>
        /// 调用此方法以完全释放MySQL的资源
        /// </summary>
        public void Dispose()
        {
            if (!IsDisposed)
            {
                IsDisposed = true;

                ConnectionPool.Dispose();
                ConnectionLocker?.Dispose();
                ConnectionPool = null;
                ConnectionLocker = null;
                Instance = null;

                GC.SuppressFinalize(this);
            }
        }

        ~MySQL()
        {
            Dispose();
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
        /// 获取文件类型
        /// </summary>
        public string Type { get; private set; }

        /// <summary>
        /// 文件全名
        /// </summary>
        public string FullName { get; private set; }

        /// <summary>
        /// 初始化ZipFileDisplay的实例
        /// </summary>
        /// <param name="Entry">入口点</param>
        public ZipFileDisplay(ZipEntry Entry)
        {
            if (Entry != null)
            {
                FullName = Entry.Name;

                if (Globalization.Language == LanguageEnum.Chinese)
                {
                    CompresionSize = "压缩大小：" + GetSize(Entry.CompressedSize);
                    ActualSize = "解压大小：" + GetSize(Entry.Size);

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
                }
                else
                {
                    CompresionSize = "Compressed：" + GetSize(Entry.CompressedSize);
                    ActualSize = "ActualSize：" + GetSize(Entry.Size);

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
                }
            }
            else
            {
                throw new ArgumentNullException(nameof(Entry), "Parameter could not be null");
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

        /// <summary>
        /// 指示当前的显示是否是缩略图
        /// </summary>
        private bool IsThumbnailPicture = true;

        /// <summary>
        /// 旋转角度
        /// </summary>
        public int RotateAngle { get; set; } = 0;

        /// <summary>
        /// 获取Photo的StorageFile对象
        /// </summary>
        public StorageFile PhotoFile { get; private set; }

        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// 初始化PhotoDisplaySupport的实例
        /// </summary>
        /// <param name="ImageSource">缩略图</param>
        /// <param name="File">文件</param>
        public PhotoDisplaySupport(BitmapImage ImageSource, StorageFile File)
        {
            BitmapSource = ImageSource;
            PhotoFile = File;
        }

        /// <summary>
        /// 使用原图替换缩略图
        /// </summary>
        /// <returns></returns>
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

        /// <summary>
        /// 更新图片的显示
        /// </summary>
        /// <returns></returns>
        public async Task UpdateImage()
        {
            using (var Stream = await PhotoFile.OpenAsync(FileAccessMode.Read))
            {
                await BitmapSource.SetSourceAsync(Stream);
            }
            OnPropertyChanged("BitmapSource");
        }

        /// <summary>
        /// 根据RotateAngle的值来旋转图片
        /// </summary>
        /// <returns></returns>
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
    /// <summary>
    /// 图片滤镜类型
    /// </summary>
    public enum FilterType
    {
        /// <summary>
        /// 原图
        /// </summary>
        Origin = 0,
        /// <summary>
        /// 反色滤镜
        /// </summary>
        Invert = 1,
        /// <summary>
        /// 灰度滤镜
        /// </summary>
        Gray = 2,
        /// <summary>
        /// 二值化滤镜
        /// </summary>
        Threshold = 4,
        /// <summary>
        /// 素描滤镜
        /// </summary>
        Sketch = 8,
        /// <summary>
        /// 高斯模糊滤镜
        /// </summary>
        GaussianBlur = 16,
        /// <summary>
        /// 怀旧滤镜
        /// </summary>
        Sepia = 32,
        /// <summary>
        /// 马赛克滤镜
        /// </summary>
        Mosaic = 64,
        /// <summary>
        /// 油画滤镜
        /// </summary>
        OilPainting = 128
    }

    /// <summary>
    /// 滤镜缩略效果图对象
    /// </summary>
    public sealed class FilterItem : IDisposable
    {
        /// <summary>
        /// 滤镜名称
        /// </summary>
        public string Text { get; private set; }

        /// <summary>
        /// 滤镜类型
        /// </summary>
        public FilterType Type { get; private set; }

        /// <summary>
        /// 缩略效果图
        /// </summary>
        public SoftwareBitmapSource Bitmap { get; private set; }

        private bool IsDisposed = false;

        /// <summary>
        /// 初始化FilterItem对象
        /// </summary>
        /// <param name="Bitmap">缩略效果图</param>
        /// <param name="Text">滤镜名称</param>
        /// <param name="Type">滤镜类型</param>
        public FilterItem(SoftwareBitmapSource Bitmap, string Text, FilterType Type)
        {
            this.Bitmap = Bitmap;
            this.Text = Text;
            this.Type = Type;
        }

        /// <summary>
        /// 调用此方法以释放资源
        /// </summary>
        public void Dispose()
        {
            if (!IsDisposed)
            {
                IsDisposed = true;
                Bitmap.Dispose();

                GC.SuppressFinalize(this);
            }
        }

        ~FilterItem()
        {
            Dispose();
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

    #region lock关键字同步锁全局对象提供器
    /// <summary>
    /// 提供全局锁定根
    /// </summary>
    public static class SyncRootProvider
    {
        /// <summary>
        /// 锁定根对象
        /// </summary>
        public static object SyncRoot { get; } = new object();
    }
    #endregion

    #region 文件系统对象
    /// <summary>
    /// 文件对象内容的枚举
    /// </summary>
    public enum ContentType
    {
        /// <summary>
        /// 文件夹
        /// </summary>
        Folder = 0,
        /// <summary>
        /// 文件
        /// </summary>
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
        /// 获取此对象的StorageFile
        /// </summary>
        public StorageFile File { get; private set; }

        /// <summary>
        /// 获取此对的StorageFolder
        /// </summary>
        public StorageFolder Folder { get; private set; }

        /// <summary>
        /// 获取内容类型
        /// </summary>
        public ContentType ContentType { get; private set; }

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
        public FileSystemStorageItem(IStorageItem Item, string Size, BitmapImage Thumbnail, string ModifiedTime)
        {
            if (Item is StorageFile File)
            {
                this.File = File;
                ContentType = ContentType.File;
            }
            else if (Item is StorageFolder Folder)
            {
                this.Folder = Folder;
                ContentType = ContentType.Folder;
            }
            else
            {
                throw new ArgumentException("Item must be folder or file", nameof(Item));
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
                throw new ArgumentException("Unsupport IStorageItem Or IStorageItem does not match the RemovableDeviceFile", nameof(Item));
            }

            Size = await Item.GetSizeDescriptionAsync().ConfigureAwait(true);

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
                    {
                        Size = await File.GetSizeDescriptionAsync().ConfigureAwait(true);
                        break;
                    }
                case ContentType.Folder:
                    {
                        throw new ArgumentException("Could not update folder size", string.Empty);
                    }
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

        /// <summary>
        /// 获取文件的修改时间
        /// </summary>
        public string ModifiedTime { get; private set; }

        /// <summary>
        /// 获取文件的路径
        /// </summary>
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
    /// <summary>
    /// 提供扩展方法的静态类
    /// </summary>
    public static class Extention
    {
        /// <summary>
        /// 使用GoogleAPI自动检测语言并将文字翻译为对应语言
        /// </summary>
        /// <param name="Text">要翻译的内容</param>
        /// <returns></returns>
        public static Task<string> Translate(this string Text)
        {
            return Task.Run(() =>
            {
                using (SecureString Secure = SecureAccessProvider.GetGoogleTranslateAccessKey(Package.Current))
                {
                    IntPtr Bstr = Marshal.SecureStringToBSTR(Secure);
                    string APIKey = Marshal.PtrToStringBSTR(Bstr);

                    try
                    {
                        using (TranslationClient Client = TranslationClient.CreateFromApiKey(APIKey, TranslationModel.ServiceDefault))
                        {
                            Detection DetectResult = Client.DetectLanguage(Text);

                            string CurrentLanguage = Globalization.Language == LanguageEnum.Chinese ? LanguageCodes.ChineseSimplified : LanguageCodes.English;

                            if (DetectResult.Language.StartsWith(CurrentLanguage))
                            {
                                return Text;
                            }
                            else
                            {
                                TranslationResult TranslateResult = Client.TranslateText(Text, CurrentLanguage, DetectResult.Language);
                                return TranslateResult.TranslatedText;
                            }
                        }
                    }
                    catch
                    {
                        return Text;
                    }
                    finally
                    {
                        Marshal.ZeroFreeBSTR(Bstr);
                        unsafe
                        {
                            fixed (char* ClearPtr = APIKey)
                            {
                                for (int i = 0; i < APIKey.Length; i++)
                                {
                                    ClearPtr[i] = '\0';
                                }
                            }
                        }
                    }
                }
            });
        }

        /// <summary>
        /// 移动一个文件夹内的所有文件夹和文件到指定的文件夹
        /// </summary>
        /// <param name="Folder">源文件夹</param>
        /// <param name="TargetFolder">目标文件夹</param>
        /// <returns></returns>
        public static async Task MoveSubFilesAndSubFoldersAsync(this StorageFolder Folder, StorageFolder TargetFolder)
        {
            if (Folder == null)
            {
                throw new ArgumentNullException(nameof(Folder), "Parameter could not be null");
            }

            if (TargetFolder == null)
            {
                throw new ArgumentNullException(nameof(TargetFolder), "Parameter could not be null");
            }

            foreach (var Item in await Folder.GetItemsAsync())
            {
                if (Item is StorageFolder SubFolder)
                {
                    StorageFolder NewFolder = await TargetFolder.CreateFolderAsync(SubFolder.Name, CreationCollisionOption.OpenIfExists);
                    await MoveSubFilesAndSubFoldersAsync(SubFolder, NewFolder).ConfigureAwait(false);
                }
                else
                {
                    await ((StorageFile)Item).MoveAsync(TargetFolder, Item.Name, NameCollisionOption.GenerateUniqueName);
                }
            }
        }

        /// <summary>
        /// 复制文件夹下的所有文件夹和文件到指定文件夹
        /// </summary>
        /// <param name="Folder">源文件夹</param>
        /// <param name="TargetFolder">目标文件夹</param>
        /// <returns></returns>
        public static async Task CopySubFilesAndSubFoldersAsync(this StorageFolder Folder, StorageFolder TargetFolder)
        {
            if (Folder == null)
            {
                throw new ArgumentNullException(nameof(Folder), "Parameter could not be null");
            }

            if (TargetFolder == null)
            {
                throw new ArgumentNullException(nameof(TargetFolder), "Parameter could not be null");
            }

            foreach (var Item in await Folder.GetItemsAsync())
            {
                if (Item is StorageFolder SubFolder)
                {
                    StorageFolder NewFolder = await TargetFolder.CreateFolderAsync(SubFolder.Name, CreationCollisionOption.OpenIfExists);
                    await CopySubFilesAndSubFoldersAsync(SubFolder, NewFolder).ConfigureAwait(false);
                }
                else
                {
                    await ((StorageFile)Item).CopyAsync(TargetFolder, Item.Name, NameCollisionOption.GenerateUniqueName);
                }
            }
        }

        /// <summary>
        /// 选中TreeViewNode并将其滚动到UI中间
        /// </summary>
        /// <param name="Node">要选中的Node</param>
        /// <param name="View">Node所属的TreeView控件</param>
        /// <returns></returns>
        public static void SelectNode(this TreeView View, TreeViewNode Node)
        {
            if (Node == null)
            {
                throw new ArgumentNullException(nameof(Node), "Parameter could not be null");
            }

            if (View == null)
            {
                throw new ArgumentNullException(nameof(View), "Parameter could not be null");
            }

            View.UpdateLayout();

            if (View.ContainerFromNode(Node) is TreeViewItem Item)
            {
                Item.IsSelected = true;
                Item.StartBringIntoView(new BringIntoViewOptions { AnimationDesired = true, VerticalAlignmentRatio = 0.5 });
            }
        }

        /// <summary>
        /// 检查文件是否存在于物理驱动器上
        /// </summary>
        /// <param name="File"></param>
        /// <returns></returns>
        public static async Task<bool> CheckExist(this StorageFile File)
        {
            if (File == null)
            {
                throw new ArgumentNullException(nameof(File), "Parameter could not be null");
            }

            try
            {
                if ((await File.GetParentAsync()) is StorageFolder ParentFolder)
                {
                    return (await ParentFolder.TryGetItemAsync(File.Name)) != null;
                }
                else
                {
                    try
                    {
                        _ = await StorageFile.GetFileFromPathAsync(File.Path);
                        return true;
                    }
                    catch (Exception)
                    {
                        return false;
                    }
                }
            }
            catch (Exception)
            {
                try
                {
                    _ = await StorageFile.GetFileFromPathAsync(File.Path);
                    return true;
                }
                catch (Exception)
                {
                    return false;
                }
            }
        }

        /// <summary>
        /// 检查文件夹是否存在于物理驱动器上
        /// </summary>
        /// <param name="Folder"></param>
        /// <returns></returns>
        public static async Task<bool> CheckExist(this StorageFolder Folder)
        {
            if (Folder == null)
            {
                throw new ArgumentNullException(nameof(Folder), "Parameter could not be null");
            }

            try
            {
                if ((await Folder.GetParentAsync()) is StorageFolder ParenetFolder)
                {
                    return (await ParenetFolder.TryGetItemAsync(Folder.Name)) != null;
                }
                else
                {
                    try
                    {
                        _ = await StorageFolder.GetFolderFromPathAsync(Folder.Path);
                        return true;
                    }
                    catch (Exception)
                    {
                        return false;
                    }
                }
            }
            catch (Exception)
            {
                try
                {
                    _ = await StorageFolder.GetFolderFromPathAsync(Folder.Path);
                    return true;
                }
                catch (Exception)
                {
                    return false;
                }
            }
        }

        /// <summary>
        /// 根据指定的密钥使用AES-128-CBC加密字符串
        /// </summary>
        /// <param name="OriginText">要加密的内容</param>
        /// <param name="Key">密钥</param>
        /// <returns></returns>
        public static async Task<string> EncryptAsync(this string OriginText, string Key)
        {
            if (string.IsNullOrEmpty(OriginText))
            {
                throw new ArgumentNullException(nameof(OriginText), "Parameter could not be null or empty");
            }

            if (string.IsNullOrEmpty(Key))
            {
                throw new ArgumentNullException(nameof(Key), "Parameter could not be null or empty");
            }

            try
            {
                using (SecureString Secure = SecureAccessProvider.GetStringEncryptionAesIV(Package.Current))
                {
                    IntPtr Bstr = Marshal.SecureStringToBSTR(Secure);
                    string IV = Marshal.PtrToStringBSTR(Bstr);

                    try
                    {
                        using (AesCryptoServiceProvider AES = new AesCryptoServiceProvider
                        {
                            KeySize = 128,
                            Key = Key.Length > 16 ? Encoding.UTF8.GetBytes(Key.Substring(0, 16)) : Encoding.UTF8.GetBytes(Key.PadRight(16, '0')),
                            Mode = CipherMode.CBC,
                            Padding = PaddingMode.PKCS7,
                            IV = Encoding.UTF8.GetBytes(IV)
                        })
                        {
                            using (MemoryStream EncryptStream = new MemoryStream())
                            {
                                using (ICryptoTransform Encryptor = AES.CreateEncryptor())
                                using (CryptoStream TransformStream = new CryptoStream(EncryptStream, Encryptor, CryptoStreamMode.Write))
                                {
                                    using (StreamWriter Writer = new StreamWriter(TransformStream))
                                    {
                                        await Writer.WriteAsync(OriginText).ConfigureAwait(false);
                                    }
                                }

                                return Convert.ToBase64String(EncryptStream.ToArray());
                            }
                        }
                    }
                    finally
                    {
                        Marshal.ZeroFreeBSTR(Bstr);
                        unsafe
                        {
                            fixed (char* ClearPtr = IV)
                            {
                                for (int i = 0; i < IV.Length; i++)
                                {
                                    ClearPtr[i] = '\0';
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// 根据指定的密钥解密密文
        /// </summary>
        /// <param name="OriginText">密文</param>
        /// <param name="Key">密钥</param>
        /// <returns></returns>
        public static async Task<string> DecryptAsync(this string OriginText, string Key)
        {
            if (string.IsNullOrEmpty(OriginText))
            {
                throw new ArgumentNullException(nameof(OriginText), "Parameter could not be null or empty");
            }

            if (string.IsNullOrEmpty(Key))
            {
                throw new ArgumentNullException(nameof(Key), "Parameter could not be null or empty");
            }

            try
            {
                using (SecureString Secure = SecureAccessProvider.GetStringEncryptionAesIV(Package.Current))
                {
                    IntPtr Bstr = Marshal.SecureStringToBSTR(Secure);
                    string IV = Marshal.PtrToStringBSTR(Bstr);

                    try
                    {
                        using (AesCryptoServiceProvider AES = new AesCryptoServiceProvider
                        {
                            KeySize = 128,
                            Key = Key.Length > 16 ? Encoding.UTF8.GetBytes(Key.Substring(0, 16)) : Encoding.UTF8.GetBytes(Key.PadRight(16, '0')),
                            Mode = CipherMode.CBC,
                            Padding = PaddingMode.PKCS7,
                            IV = Encoding.UTF8.GetBytes(IV)
                        })
                        {
                            using (MemoryStream DecryptStream = new MemoryStream(Convert.FromBase64String(OriginText)))
                            {
                                using (ICryptoTransform Decryptor = AES.CreateDecryptor())
                                using (CryptoStream TransformStream = new CryptoStream(DecryptStream, Decryptor, CryptoStreamMode.Read))
                                using (StreamReader Writer = new StreamReader(TransformStream, Encoding.UTF8))
                                {
                                    return await Writer.ReadToEndAsync().ConfigureAwait(false);
                                }
                            }
                        }
                    }
                    finally
                    {
                        Marshal.ZeroFreeBSTR(Bstr);
                        unsafe
                        {
                            fixed (char* ClearPtr = IV)
                            {
                                for (int i = 0; i < IV.Length; i++)
                                {
                                    ClearPtr[i] = '\0';
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// 根据指定的密钥、加密强度，使用AES将文件加密并保存至指定的文件夹
        /// </summary>
        /// <param name="OriginFile">要加密的文件</param>
        /// <param name="ExportFolder">指定加密文件保存的文件夹</param>
        /// <param name="Key">加密密钥</param>
        /// <param name="KeySize">加密强度，值仅允许 128 和 256</param>
        /// <param name="CancelToken">取消通知</param>
        /// <returns></returns>
        public static async Task<StorageFile> EncryptAsync(this StorageFile OriginFile, StorageFolder ExportFolder, string Key, int KeySize, CancellationToken CancelToken = default)
        {
            if (OriginFile == null)
            {
                throw new ArgumentNullException(nameof(OriginFile), "OriginFile could not be null");
            }

            if (ExportFolder == null)
            {
                throw new ArgumentNullException(nameof(ExportFolder), "ExportFolder could not be null");
            }

            if (KeySize != 256 && KeySize != 128)
            {
                throw new InvalidEnumArgumentException("AES密钥长度仅支持128或256任意一种");
            }

            if (string.IsNullOrEmpty(Key))
            {
                throw new ArgumentNullException(nameof(Key), "Parameter could not be null or empty");
            }

            byte[] KeyArray = null;

            int KeyLengthNeed = KeySize / 8;

            KeyArray = Key.Length > KeyLengthNeed
                       ? Encoding.UTF8.GetBytes(Key.Substring(0, KeyLengthNeed))
                       : Encoding.UTF8.GetBytes(Key.PadRight(KeyLengthNeed, '0'));

            StorageFile EncryptedFile = null;
            try
            {
                EncryptedFile = await ExportFolder.CreateFileAsync($"{ Path.GetFileNameWithoutExtension(OriginFile.Name)}.sle", CreationCollisionOption.GenerateUniqueName);

                using (SecureString Secure = SecureAccessProvider.GetFileEncryptionAesIV(Package.Current))
                {
                    IntPtr Bstr = Marshal.SecureStringToBSTR(Secure);
                    string IV = Marshal.PtrToStringBSTR(Bstr);

                    try
                    {
                        using (AesCryptoServiceProvider AES = new AesCryptoServiceProvider
                        {
                            KeySize = KeySize,
                            Key = KeyArray,
                            Mode = CipherMode.CBC,
                            Padding = PaddingMode.Zeros,
                            IV = Encoding.UTF8.GetBytes(IV)
                        })
                        {
                            using (Stream OriginFileStream = await OriginFile.OpenStreamForReadAsync().ConfigureAwait(false))
                            using (Stream EncryptFileStream = await EncryptedFile.OpenStreamForWriteAsync().ConfigureAwait(false))
                            using (ICryptoTransform Encryptor = AES.CreateEncryptor())
                            {
                                byte[] Detail = Encoding.UTF8.GetBytes("$" + KeySize + "|" + OriginFile.FileType + "$");
                                await EncryptFileStream.WriteAsync(Detail, 0, Detail.Length).ConfigureAwait(false);

                                byte[] PasswordFlag = Encoding.UTF8.GetBytes("PASSWORD_CORRECT");
                                byte[] EncryptPasswordFlag = Encryptor.TransformFinalBlock(PasswordFlag, 0, PasswordFlag.Length);
                                await EncryptFileStream.WriteAsync(EncryptPasswordFlag, 0, EncryptPasswordFlag.Length).ConfigureAwait(false);

                                using (CryptoStream TransformStream = new CryptoStream(EncryptFileStream, Encryptor, CryptoStreamMode.Write))
                                {
                                    await OriginFileStream.CopyToAsync(TransformStream, 81920, CancelToken).ConfigureAwait(false);
                                    TransformStream.FlushFinalBlock();
                                }
                            }
                        }
                    }
                    finally
                    {
                        Marshal.ZeroFreeBSTR(Bstr);
                        unsafe
                        {
                            fixed (char* ClearPtr = IV)
                            {
                                for (int i = 0; i < IV.Length; i++)
                                {
                                    ClearPtr[i] = '\0';
                                }
                            }
                        }
                    }
                }

                return EncryptedFile;
            }
            catch (TaskCanceledException)
            {
                await EncryptedFile?.DeleteAsync(StorageDeleteOption.PermanentDelete);
                throw;
            }
            catch (CryptographicException)
            {
                await EncryptedFile?.DeleteAsync(StorageDeleteOption.PermanentDelete);
                throw;
            }
            catch (Exception)
            {
                await EncryptedFile?.DeleteAsync(StorageDeleteOption.PermanentDelete);
                return null;
            }
        }

        /// <summary>
        /// 根据指定的密钥，使用AES将文件解密至指定的文件夹
        /// </summary>
        /// <param name="EncryptedFile">要解密的文件</param>
        /// <param name="ExportFolder">指定解密文件的保存位置</param>
        /// <param name="Key">解密密钥</param>
        /// <param name="CancelToken">取消通知</param>
        /// <returns></returns>
        public static async Task<StorageFile> DecryptAsync(this StorageFile EncryptedFile, StorageFolder ExportFolder, string Key, CancellationToken CancelToken = default)
        {
            if (ExportFolder == null)
            {
                throw new ArgumentNullException(nameof(ExportFolder), "ExportFolder could not be null");
            }

            if (EncryptedFile == null)
            {
                throw new ArgumentNullException(nameof(EncryptedFile), "EncryptedFile could not be null");
            }

            if (string.IsNullOrEmpty(Key))
            {
                throw new ArgumentNullException(nameof(Key), "Key could not be null or empty");
            }

            StorageFile DecryptedFile = null;
            try
            {
                using (SecureString Secure = SecureAccessProvider.GetFileEncryptionAesIV(Package.Current))
                {
                    IntPtr Bstr = Marshal.SecureStringToBSTR(Secure);
                    string IV = Marshal.PtrToStringBSTR(Bstr);

                    try
                    {
                        using (AesCryptoServiceProvider AES = new AesCryptoServiceProvider
                        {
                            Mode = CipherMode.CBC,
                            Padding = PaddingMode.Zeros,
                            IV = Encoding.UTF8.GetBytes(IV)
                        })
                        {
                            using (Stream EncryptFileStream = await EncryptedFile.OpenStreamForReadAsync().ConfigureAwait(false))
                            {
                                byte[] DecryptByteBuffer = new byte[20];

                                await EncryptFileStream.ReadAsync(DecryptByteBuffer, 0, DecryptByteBuffer.Length).ConfigureAwait(false);

                                string FileType;
                                if (Encoding.UTF8.GetString(DecryptByteBuffer).Split('$', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() is string Info)
                                {
                                    string[] InfoGroup = Info.Split('|');
                                    if (InfoGroup.Length == 2)
                                    {
                                        int KeySize = Convert.ToInt32(InfoGroup[0]);
                                        FileType = InfoGroup[1];

                                        AES.KeySize = KeySize;

                                        int KeyLengthNeed = KeySize / 8;
                                        AES.Key = Key.Length > KeyLengthNeed ? Encoding.UTF8.GetBytes(Key.Substring(0, KeyLengthNeed)) : Encoding.UTF8.GetBytes(Key.PadRight(KeyLengthNeed, '0'));
                                    }
                                    else
                                    {
                                        throw new FileDamagedException("文件损坏，无法解密");
                                    }
                                }
                                else
                                {
                                    throw new FileDamagedException("文件损坏，无法解密");
                                }

                                DecryptedFile = await ExportFolder.CreateFileAsync($"{ Path.GetFileNameWithoutExtension(EncryptedFile.Name)}{FileType}", CreationCollisionOption.GenerateUniqueName);

                                using (Stream DecryptFileStream = await DecryptedFile.OpenStreamForWriteAsync().ConfigureAwait(false))
                                using (ICryptoTransform Decryptor = AES.CreateDecryptor(AES.Key, AES.IV))
                                {
                                    byte[] PasswordConfirm = new byte[16];
                                    EncryptFileStream.Seek(Info.Length + 2, SeekOrigin.Begin);
                                    await EncryptFileStream.ReadAsync(PasswordConfirm, 0, PasswordConfirm.Length).ConfigureAwait(false);

                                    if (Encoding.UTF8.GetString(Decryptor.TransformFinalBlock(PasswordConfirm, 0, PasswordConfirm.Length)) == "PASSWORD_CORRECT")
                                    {
                                        using (CryptoStream TransformStream = new CryptoStream(DecryptFileStream, Decryptor, CryptoStreamMode.Write))
                                        {
                                            await EncryptFileStream.CopyToAsync(TransformStream, 81920, CancelToken).ConfigureAwait(false);
                                            TransformStream.FlushFinalBlock();
                                        }
                                    }
                                    else
                                    {
                                        throw new PasswordErrorException("密码错误");
                                    }
                                }
                            }
                        }
                    }
                    finally
                    {
                        Marshal.ZeroFreeBSTR(Bstr);
                        unsafe
                        {
                            fixed (char* ClearPtr = IV)
                            {
                                for (int i = 0; i < IV.Length; i++)
                                {
                                    ClearPtr[i] = '\0';
                                }
                            }
                        }
                    }
                }

                return DecryptedFile;
            }
            catch (TaskCanceledException)
            {
                await DecryptedFile?.DeleteAsync(StorageDeleteOption.PermanentDelete);
                throw;
            }
            catch (CryptographicException)
            {
                await DecryptedFile?.DeleteAsync(StorageDeleteOption.PermanentDelete);
                throw;
            }
            catch (Exception)
            {
                await DecryptedFile?.DeleteAsync(StorageDeleteOption.PermanentDelete);
                return null;
            }
        }

        /// <summary>
        /// 根据类型寻找指定UI元素的子元素
        /// </summary>
        /// <typeparam name="T">寻找的类型</typeparam>
        /// <param name="root"></param>
        /// <returns></returns>
        public static T FindChildOfType<T>(this DependencyObject root) where T : DependencyObject
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

        /// <summary>
        /// 根据名称和类型寻找指定UI元素的子元素
        /// </summary>
        /// <typeparam name="T">类型</typeparam>
        /// <param name="root"></param>
        /// <param name="name">子元素名称</param>
        /// <returns></returns>
        public static T FindChildOfName<T>(this DependencyObject root, string name) where T : DependencyObject
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
                        if (ChildObject is T TypedChild && (TypedChild as FrameworkElement).Name == name)
                        {
                            return TypedChild;
                        }
                        ObjectQueue.Enqueue(ChildObject);
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// 更新TreeViewNode所有子节点所包含的文件夹对象
        /// </summary>
        /// <param name="ParentNode"></param>
        /// <returns></returns>
        public static async Task UpdateAllSubNodeFolder(this TreeViewNode ParentNode)
        {
            if (ParentNode == null)
            {
                throw new ArgumentNullException(nameof(ParentNode), "Node could not be null");
            }

            StorageFolder ParentFolder = ParentNode.Content as StorageFolder;
            foreach (var Package in ParentNode.Children.Select((SubNode) => new { (SubNode.Content as StorageFolder).Name, SubNode }))
            {
                Package.SubNode.Content = await ParentFolder.GetFolderAsync(Package.Name);

                if (Package.SubNode.HasChildren)
                {
                    await UpdateAllSubNodeFolder(Package.SubNode).ConfigureAwait(false);
                }
            }
        }

        /// <summary>
        /// 删除当前文件夹下的所有子文件和子文件夹
        /// </summary>
        /// <param name="Folder">当前文件夹</param>
        /// <returns></returns>
        public static async Task DeleteAllSubFilesAndFolders(this StorageFolder Folder)
        {
            if (Folder == null)
            {
                throw new ArgumentNullException(nameof(Folder), "Folder could not be null");
            }

            foreach (var Item in await Folder.GetItemsAsync())
            {
                if (Item is StorageFolder folder)
                {
                    await DeleteAllSubFilesAndFolders(folder).ConfigureAwait(false);
                }
                else
                {
                    await Item.DeleteAsync(StorageDeleteOption.PermanentDelete);
                }
            }
        }

        /// <summary>
        /// 获取存储对象的大小描述
        /// </summary>
        /// <param name="Item">存储对象</param>
        /// <returns></returns>
        public static async Task<string> GetSizeDescriptionAsync(this IStorageItem Item)
        {
            if (Item == null)
            {
                throw new ArgumentNullException(nameof(Item), "Item could not be null");
            }

            BasicProperties Properties = await Item.GetBasicPropertiesAsync();

            return Properties.Size / 1024f < 1024 ? Math.Round(Properties.Size / 1024f, 2).ToString("0.00") + " KB" :
            (Properties.Size / 1048576f < 1024 ? Math.Round(Properties.Size / 1048576f, 2).ToString("0.00") + " MB" :
            (Properties.Size / 1073741824f < 1024 ? Math.Round(Properties.Size / 1073741824f, 2).ToString("0.00") + " GB" :
            Math.Round(Properties.Size / Convert.ToDouble(1099511627776), 2).ToString() + " TB"));
        }

        /// <summary>
        /// 获取存储对象的修改日期
        /// </summary>
        /// <param name="Item">存储对象</param>
        /// <returns></returns>
        public static async Task<string> GetModifiedTimeAsync(this IStorageItem Item)
        {
            if (Item == null)
            {
                throw new ArgumentNullException(nameof(Item), "Item could not be null");
            }

            BasicProperties Properties = await Item.GetBasicPropertiesAsync();
            return Properties.DateModified.ToString("F");
        }

        /// <summary>
        /// 获取存储对象的缩略图
        /// </summary>
        /// <param name="Item">存储对象</param>
        /// <returns></returns>
        public static async Task<BitmapImage> GetThumbnailBitmapAsync(this IStorageItem Item)
        {
            try
            {
                if (Item is StorageFolder Folder)
                {
                    using (StorageItemThumbnail Thumbnail = await Folder.GetScaledImageAsThumbnailAsync(ThumbnailMode.ListView, 100))
                    {
                        if (Thumbnail == null || Thumbnail.Size == 0 || Thumbnail.OriginalHeight == 0 || Thumbnail.OriginalWidth == 0)
                        {
                            return null;
                        }

                        BitmapImage bitmapImage = new BitmapImage
                        {
                            DecodePixelHeight = 100,
                            DecodePixelWidth = 100
                        };
                        await bitmapImage.SetSourceAsync(Thumbnail);
                        return bitmapImage;
                    }
                }
                else if (Item is StorageFile File)
                {
                    using (StorageItemThumbnail Thumbnail = await File.GetScaledImageAsThumbnailAsync(ThumbnailMode.ListView, 100))
                    {
                        if (Thumbnail == null)
                        {
                            return null;
                        }

                        BitmapImage bitmapImage = new BitmapImage
                        {
                            DecodePixelHeight = 100,
                            DecodePixelWidth = 100
                        };
                        await bitmapImage.SetSourceAsync(Thumbnail);
                        return bitmapImage;
                    }
                }
                else
                {
                    return null;
                }
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// 平滑滚动至指定的项
        /// </summary>
        /// <param name="listViewBase"></param>
        /// <param name="item">指定项</param>
        /// <param name="alignment">对齐方式</param>
        public static void ScrollIntoViewSmoothly(this ListViewBase listViewBase, object item, ScrollIntoViewAlignment alignment = ScrollIntoViewAlignment.Default)
        {
            if (listViewBase == null)
            {
                throw new ArgumentNullException(nameof(listViewBase), "listViewBase could not be null");
            }

            if (listViewBase.FindChildOfType<ScrollViewer>() is ScrollViewer scrollViewer)
            {
                double originHorizontalOffset = scrollViewer.HorizontalOffset;
                double originVerticalOffset = scrollViewer.VerticalOffset;

                void layoutUpdatedHandler(object sender, object e)
                {
                    listViewBase.LayoutUpdated -= layoutUpdatedHandler;

                    double targetHorizontalOffset = scrollViewer.HorizontalOffset;
                    double targetVerticalOffset = scrollViewer.VerticalOffset;

                    void scrollHandler(object s, ScrollViewerViewChangedEventArgs t)
                    {
                        scrollViewer.ViewChanged -= scrollHandler;

                        scrollViewer.ChangeView(targetHorizontalOffset, targetVerticalOffset, null);
                    }

                    scrollViewer.ViewChanged += scrollHandler;

                    scrollViewer.ChangeView(originHorizontalOffset, originVerticalOffset, null, true);
                }

                listViewBase.LayoutUpdated += layoutUpdatedHandler;

                listViewBase.ScrollIntoView(item, alignment);
            }
            else
            {
                listViewBase.ScrollIntoView(item, alignment);
            }
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
                return string.IsNullOrWhiteSpace(DeviceInfo.Name) ? (Globalization.Language == LanguageEnum.Chinese ? "未知设备" : "Unknown") : DeviceInfo.Name;
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
                    return Globalization.Language == LanguageEnum.Chinese ? "已配对" : "Paired";
                }
                else
                {
                    return Globalization.Language == LanguageEnum.Chinese ? "准备配对" : "ReadyToPair";
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
                    return Globalization.Language == LanguageEnum.Chinese ? "取消配对" : "Unpair";
                }
                else
                {
                    return Globalization.Language == LanguageEnum.Chinese ? "配对" : "Pair";
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
    /// <summary>
    /// 提供对文件路径的逐层解析
    /// </summary>
    public sealed class PathAnalysis
    {
        /// <summary>
        /// 完整路径
        /// </summary>
        public string FullPath { get; private set; }

        private Queue<string> PathQueue;

        private string CurrentLevel;

        /// <summary>
        /// 指示是否还有下一级路径
        /// </summary>
        public bool HasNextLevel { get; private set; }

        /// <summary>
        /// 初始化PathAnalysis对象
        /// </summary>
        /// <param name="FullPath">完整路径</param>
        /// <param name="CurrentPath">当前路径</param>
        public PathAnalysis(string FullPath, string CurrentPath)
        {
            if (string.IsNullOrEmpty(FullPath))
            {
                throw new ArgumentNullException(nameof(FullPath), "FullPath could not be null or empty");
            }

            this.FullPath = FullPath;

            CurrentLevel = CurrentPath;

            if (string.IsNullOrEmpty(CurrentPath))
            {
                string[] Split = FullPath.Split("\\", StringSplitOptions.RemoveEmptyEntries);
                Split[0] = Split[0] + "\\";
                PathQueue = new Queue<string>(Split);
                HasNextLevel = true;
            }
            else
            {
                if (FullPath != CurrentPath)
                {
                    HasNextLevel = true;
                    string[] Split = Path.GetRelativePath(CurrentPath, FullPath).Split("\\", StringSplitOptions.RemoveEmptyEntries);
                    PathQueue = new Queue<string>(Split);
                }
                else
                {
                    HasNextLevel = false;
                    PathQueue = new Queue<string>(0);
                }
            }
        }

        /// <summary>
        /// 获取下一级文件夹的完整路径
        /// </summary>
        /// <returns></returns>
        public string NextFullPath()
        {
            if (PathQueue.Count != 0)
            {
                CurrentLevel = Path.Combine(CurrentLevel, PathQueue.Dequeue());
                if (PathQueue.Count == 0)
                {
                    HasNextLevel = false;
                }
                return CurrentLevel;
            }
            else
            {
                return CurrentLevel;
            }
        }

        /// <summary>
        /// 获取下一级文件夹的相对路径
        /// </summary>
        /// <returns></returns>
        public string NextRelativePath()
        {
            if (PathQueue.Count != 0)
            {
                string RelativePath = PathQueue.Dequeue();
                if (PathQueue.Count == 0)
                {
                    HasNextLevel = false;
                }
                return RelativePath;
            }
            else
            {
                return string.Empty;
            }
        }
    }
    #endregion

    #region 这台电脑相关驱动器和库显示类
    /// <summary>
    /// 提供驱动器的界面支持
    /// </summary>
    public sealed class HardDeviceInfo
    {
        /// <summary>
        /// 驱动器缩略图
        /// </summary>
        public BitmapImage Thumbnail { get; private set; }

        /// <summary>
        /// 驱动器对象
        /// </summary>
        public StorageFolder Folder { get; private set; }

        /// <summary>
        /// 驱动器名称
        /// </summary>
        public string Name
        {
            get
            {
                return Folder.DisplayName;
            }
        }

        /// <summary>
        /// 容量百分比
        /// </summary>
        public double Percent { get; private set; }

        /// <summary>
        /// 总容量的描述
        /// </summary>
        public string Capacity { get; private set; }

        /// <summary>
        /// 总字节数
        /// </summary>
        public ulong TotalByte { get; private set; }

        /// <summary>
        /// 空闲字节数
        /// </summary>
        public ulong FreeByte { get; private set; }

        /// <summary>
        /// 可用空间的描述
        /// </summary>
        public string FreeSpace { get; private set; }

        /// <summary>
        /// 容量显示条对可用空间不足的情况转换颜色
        /// </summary>
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

        /// <summary>
        /// 存储空间描述
        /// </summary>
        public string StorageSpaceDescription
        {
            get
            {
                if (Globalization.Language == LanguageEnum.Chinese)
                {
                    return FreeSpace + " 可用, 共 " + Capacity;
                }
                else
                {
                    return FreeSpace + " free of " + Capacity;
                }
            }
        }

        /// <summary>
        /// 初始化HardDeviceInfo对象
        /// </summary>
        /// <param name="Device">驱动器文件夹</param>
        /// <param name="Thumbnail">缩略图</param>
        /// <param name="PropertiesRetrieve">额外信息</param>
        public HardDeviceInfo(StorageFolder Device, BitmapImage Thumbnail, IDictionary<string, object> PropertiesRetrieve)
        {
            Folder = Device ?? throw new FileNotFoundException();

            if (PropertiesRetrieve == null)
            {
                throw new ArgumentNullException(nameof(PropertiesRetrieve), "Parameter could not be null");
            }

            this.Thumbnail = Thumbnail;

            if (PropertiesRetrieve["System.Capacity"] is ulong TotalByte && PropertiesRetrieve["System.FreeSpace"] is ulong FreeByte)
            {
                this.TotalByte = (ulong)PropertiesRetrieve["System.Capacity"];
                this.FreeByte = (ulong)PropertiesRetrieve["System.FreeSpace"];
                Capacity = GetSizeDescription(TotalByte);
                FreeSpace = GetSizeDescription(FreeByte);
                Percent = 1 - FreeByte / Convert.ToDouble(TotalByte);
            }
            else
            {
                Capacity = "Unknown";
                FreeSpace = "Unknown";
                Percent = 0;
            }
        }

        /// <summary>
        /// 根据Size计算大小描述
        /// </summary>
        /// <param name="Size">大小</param>
        /// <returns></returns>
        private string GetSizeDescription(ulong Size)
        {
            return Size / 1024f < 1024 ? Math.Round(Size / 1024f, 2).ToString("0.00") + " KB" :
            (Size / 1048576f < 1024 ? Math.Round(Size / 1048576f, 2).ToString("0.00") + " MB" :
            (Size / 1073741824f < 1024 ? Math.Round(Size / 1073741824f, 2).ToString("0.00") + " GB" :
            Math.Round(Size / Convert.ToDouble(1099511627776), 2).ToString("0.00") + " TB"));
        }
    }

    /// <summary>
    /// 指定文件夹和库是自带还是用户固定
    /// </summary>
    public enum LibrarySource
    {
        /// <summary>
        /// 自带库
        /// </summary>
        SystemBase = 0,
        /// <summary>
        /// 用户自定义库
        /// </summary>
        UserCustom = 1
    }

    /// <summary>
    /// 提供对文件夹库的UI显示支持
    /// </summary>
    public sealed class LibraryFolder
    {
        /// <summary>
        /// 文件夹名称
        /// </summary>
        public string Name { get; private set; }

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
                return Folder.DisplayType;
            }
        }

        /// <summary>
        /// 指示库的类型
        /// </summary>
        public LibrarySource Source { get; private set; }

        /// <summary>
        /// 初始化LibraryFolder
        /// </summary>
        /// <param name="Folder">文件夹对象</param>
        /// <param name="Thumbnail">缩略图</param>
        /// <param name="Source">类型</param>
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
    /// <summary>
    /// 提供增量加载集合的实现
    /// </summary>
    /// <typeparam name="T">集合内容的类型</typeparam>
    public sealed class IncrementalLoadingCollection<T> : ObservableCollection<T>, ISupportIncrementalLoading
    {
        public StorageItemQueryResult ItemQuery { get; private set; }
        public StorageFolderQueryResult FolderQuery { get; private set; }

        private uint CurrentIndex = 0;
        private Func<uint, uint, StorageItemQueryResult, Task<IEnumerable<T>>> MoreItemsNeed;
        private Func<uint, uint, StorageFolderQueryResult, Task<IEnumerable<T>>> MoreFolderNeed;
        private uint MaxNum = 0;

        /// <summary>
        /// 初始化IncrementalLoadingCollection
        /// </summary>
        /// <param name="MoreItemsNeed">提供需要加载更多数据时能够调用的委托</param>
        public IncrementalLoadingCollection(Func<uint, uint, StorageItemQueryResult, Task<IEnumerable<T>>> MoreItemsNeed)
        {
            this.MoreItemsNeed = MoreItemsNeed;
        }

        public IncrementalLoadingCollection(Func<uint, uint, StorageFolderQueryResult, Task<IEnumerable<T>>> MoreFolderNeed)
        {
            this.MoreFolderNeed = MoreFolderNeed;
        }

        public async Task SetStorageQueryResultAsync(StorageItemQueryResult InputQuery)
        {
            if (InputQuery == null)
            {
                throw new ArgumentNullException(nameof(InputQuery), "Parameter could not be null");
            }

            ItemQuery = InputQuery;

            MaxNum = await ItemQuery.GetItemCountAsync();

            CurrentIndex = MaxNum > 100 ? 100 : MaxNum;

            if (MaxNum > 100)
            {
                HasMoreItems = true;
            }
        }

        public async Task SetStorageQueryResultAsync(StorageFolderQueryResult InputQuery)
        {
            if (InputQuery == null)
            {
                throw new ArgumentNullException(nameof(InputQuery), "Parameter could not be null");
            }

            FolderQuery = InputQuery;

            MaxNum = await FolderQuery.GetItemCountAsync();

            CurrentIndex = MaxNum > 20 ? 20 : MaxNum;

            if (MaxNum > 20)
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
                        IEnumerable<T> Result;

                        if (MoreItemsNeed == null)
                        {
                            Result = await MoreFolderNeed(CurrentIndex, ItemNeedNum, FolderQuery).ConfigureAwait(true);
                        }
                        else
                        {
                            Result = await MoreItemsNeed(CurrentIndex, ItemNeedNum, ItemQuery).ConfigureAwait(true);
                        }

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
                    IEnumerable<T> Result;

                    if (MoreItemsNeed == null)
                    {
                        Result = await MoreFolderNeed(CurrentIndex, count, FolderQuery).ConfigureAwait(true);
                    }
                    else
                    {
                        Result = await MoreItemsNeed(CurrentIndex, count, ItemQuery).ConfigureAwait(true);
                    }

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

    #region 快速启动类
    /// <summary>
    /// 提供对快速启动项状态或类型的枚举
    /// </summary>
    public enum QuickStartType
    {
        /// <summary>
        /// 应用区域的快速启动项
        /// </summary>
        Application = 1,
        /// <summary>
        /// 网站区域的快速启动项
        /// </summary>
        WebSite = 2,
        /// <summary>
        /// 指示此项用于更新应用区域
        /// </summary>
        UpdateApp = 4,
        /// <summary>
        /// 指示此向用于更新网站区域
        /// </summary>
        UpdateWeb = 8
    }

    /// <summary>
    /// 提供对快速启动区域的UI支持
    /// </summary>
    public sealed class QuickStartItem : INotifyPropertyChanged
    {
        /// <summary>
        /// 图标
        /// </summary>
        public BitmapImage Image { get; private set; }

        /// <summary>
        /// 显示名称
        /// </summary>
        public string DisplayName { get; private set; }

        /// <summary>
        /// 图标位置
        /// </summary>
        public string RelativePath { get; private set; }

        /// <summary>
        /// 快速启动项类型
        /// </summary>
        public QuickStartType Type { get; private set; }

        /// <summary>
        /// 协议或网址
        /// </summary>
        public Uri ProtocalUri { get; private set; }

        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// 更新快速启动项的信息
        /// </summary>
        /// <param name="Image">缩略图</param>
        /// <param name="ProtocalUri">协议</param>
        /// <param name="RelativePath">图标位置</param>
        /// <param name="DisplayName">显示名称</param>
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

        /// <summary>
        /// 初始化QuickStartItem对象
        /// </summary>
        /// <param name="Image">图标</param>
        /// <param name="Uri">协议</param>
        /// <param name="Type">类型</param>
        /// <param name="RelativePath">图标位置</param>
        /// <param name="DisplayName">显示名称</param>
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
    /// <summary>
    /// 提供对WIFI分享功能支持的类
    /// </summary>
    public sealed class WiFiShareProvider : IDisposable
    {
        private HttpListener Listener;

        private bool IsDisposed = false;

        private CancellationTokenSource Cancellation = new CancellationTokenSource();

        public event EventHandler<Exception> ThreadExitedUnexpectly;

        public KeyValuePair<string, string> FilePathMap { get; set; }

        public string CurrentUri { get; private set; }

        public bool IsListeningThreadWorking { get; private set; } = false;

        /// <summary>
        /// 初始化WiFiShareProvider对象
        /// </summary>
        public WiFiShareProvider()
        {
            Listener = new HttpListener();
            Listener.Prefixes.Add("http://*:8125/");
            Listener.AuthenticationSchemes = AuthenticationSchemes.Anonymous;

            HostName CurrentHostName = NetworkInformation.GetHostNames().Where((IP) => IP.Type == HostNameType.Ipv4).FirstOrDefault();
            CurrentUri = $"http://{ CurrentHostName }:8125/";
        }

        /// <summary>
        /// 启动WIFI连接侦听器
        /// </summary>
        public async Task StartToListenRequest()
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
                    HttpListenerContext Context = await Listener.GetContextAsync().ConfigureAwait(false);

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
                       }, Context, Cancellation.Token, TaskCreationOptions.LongRunning, TaskScheduler.Current);
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
            finally
            {
                Cancellation?.Dispose();
                Cancellation = null;
            }
        }

        /// <summary>
        /// 调用此方法以释放资源
        /// </summary>
        public void Dispose()
        {
            if (!IsDisposed)
            {
                IsDisposed = true;
                Cancellation?.Cancel();
                Listener.Stop();
                Listener.Abort();
                Listener.Close();
                Listener = null;

                GC.SuppressFinalize(this);
            }
        }

        ~WiFiShareProvider()
        {
            Dispose();
        }
    }
    #endregion

    #region 全局背景控制器
    /// <summary>
    /// 背景图片类型的枚举
    /// </summary>
    public enum BackgroundBrushType
    {
        /// <summary>
        /// 使用亚克力背景
        /// </summary>
        Acrylic = 0,
        /// <summary>
        /// 使用图片背景
        /// </summary>
        Picture = 1
    }

    /// <summary>
    /// 提供对全局背景的控制功能
    /// </summary>
    public class BackgroundController : INotifyPropertyChanged
    {
        /// <summary>
        /// 亚克力背景刷
        /// </summary>
        private readonly AcrylicBrush AcrylicBackgroundBrush;

        /// <summary>
        /// 图片背景刷
        /// </summary>
        private readonly ImageBrush PictureBackgroundBrush;

        /// <summary>
        /// 指示当前的背景类型
        /// </summary>
        private BackgroundBrushType CurrentType;

        /// <summary>
        /// 对外统一提供背景
        /// </summary>
        public Brush BackgroundBrush
        {
            get
            {
                return CurrentType == BackgroundBrushType.Picture ? PictureBackgroundBrush : (Brush)AcrylicBackgroundBrush;
            }
            set
            {

            }
        }

        private static BackgroundController Instance;

        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// 获取背景控制器的实例
        /// </summary>
        public static BackgroundController Current
        {
            get
            {
                lock (SyncRootProvider.SyncRoot)
                {
                    return Instance ?? (Instance = new BackgroundController());
                }
            }
        }

        /// <summary>
        /// 初始化BackgroundController对象
        /// </summary>
        private BackgroundController()
        {
            if (ApplicationData.Current.LocalSettings.Values["UIDisplayMode"] is string Mode)
            {
                if (Mode == "推荐" || Mode == "Recommand")
                {
                    AcrylicBackgroundBrush = new AcrylicBrush
                    {
                        BackgroundSource = AcrylicBackgroundSource.HostBackdrop,
                        TintColor = Colors.LightSlateGray,
                        TintOpacity = 0.4,
                        FallbackColor = Colors.DimGray
                    };

                    CurrentType = BackgroundBrushType.Acrylic;
                }
                else
                {
                    AcrylicBackgroundBrush = new AcrylicBrush
                    {
                        BackgroundSource = AcrylicBackgroundSource.HostBackdrop,
                        TintColor = ApplicationData.Current.LocalSettings.Values["AcrylicThemeColor"] is string Color ? GetColorFromHexString(Color) : Colors.LightSlateGray,
                        TintOpacity = 1 - Convert.ToSingle(ApplicationData.Current.LocalSettings.Values["BackgroundTintOpacity"]),
                        TintLuminosityOpacity = 1 - Convert.ToSingle(ApplicationData.Current.LocalSettings.Values["BackgroundTintLuminosity"]),
                        FallbackColor = Colors.DimGray
                    };

                    if (ApplicationData.Current.LocalSettings.Values["CustomUISubMode"] is string SubMode)
                    {
                        CurrentType = (BackgroundBrushType)Enum.Parse(typeof(BackgroundBrushType), SubMode);
                    }
                }
            }
            else
            {
                AcrylicBackgroundBrush = new AcrylicBrush
                {
                    BackgroundSource = AcrylicBackgroundSource.HostBackdrop,
                    TintColor = Colors.LightSlateGray,
                    TintOpacity = 0.4,
                    FallbackColor = Colors.DimGray
                };

                CurrentType = BackgroundBrushType.Acrylic;
            }

            if (ApplicationData.Current.LocalSettings.Values["PictureBackgroundUri"] is string uri)
            {
                PictureBackgroundBrush = new ImageBrush
                {
                    ImageSource = new BitmapImage(new Uri(uri)),
                    Stretch = Stretch.UniformToFill
                };
            }
            else
            {
                PictureBackgroundBrush = new ImageBrush
                {
                    Stretch = Stretch.UniformToFill
                };
            }
        }

        /// <summary>
        /// 提供颜色透明度的值
        /// </summary>
        public double TintOpacity
        {
            get
            {
                return 1 - (double)AcrylicBackgroundBrush.GetValue(AcrylicBrush.TintOpacityProperty);
            }
            set
            {
                AcrylicBackgroundBrush.SetValue(AcrylicBrush.TintOpacityProperty, 1 - value);
                ApplicationData.Current.LocalSettings.Values["BackgroundTintOpacity"] = value.ToString("0.0");
            }
        }

        /// <summary>
        /// 提供背景光透过率的值
        /// </summary>
        public double TintLuminosityOpacity
        {
            get
            {
                return 1 - ((double?)AcrylicBackgroundBrush.GetValue(AcrylicBrush.TintLuminosityOpacityProperty)).GetValueOrDefault();
            }
            set
            {
                if (value == -1)
                {
                    AcrylicBackgroundBrush.SetValue(AcrylicBrush.TintLuminosityOpacityProperty, null);
                }
                else
                {
                    AcrylicBackgroundBrush.SetValue(AcrylicBrush.TintLuminosityOpacityProperty, 1 - value);
                    ApplicationData.Current.LocalSettings.Values["BackgroundTintLuminosity"] = value.ToString("0.0");
                }
            }
        }

        /// <summary>
        /// 提供主题色的值
        /// </summary>
        public Color AcrylicColor
        {
            get
            {
                return (Color)AcrylicBackgroundBrush.GetValue(AcrylicBrush.TintColorProperty);
            }
            set
            {
                AcrylicBackgroundBrush.SetValue(AcrylicBrush.TintColorProperty, value);
            }
        }

        /// <summary>
        /// 使用此方法以切换背景类型
        /// </summary>
        /// <param name="Type">背景类型</param>
        /// <param name="uri">图片背景的Uri</param>
        public void SwitchTo(BackgroundBrushType Type, Uri uri = null)
        {
            CurrentType = Type;

            if (Type == BackgroundBrushType.Picture)
            {
                if (uri == null)
                {
                    throw new ArgumentNullException(nameof(Type), "if parameter: 'Type' is BackgroundBrushType.Picture, parameter: 'uri' could not be null or empty");
                }

                if (ApplicationData.Current.LocalSettings.Values["PictureBackgroundUri"] is string Ur)
                {
                    if (Ur != uri.ToString())
                    {
                        BitmapImage Bitmap = new BitmapImage();
                        PictureBackgroundBrush.ImageSource = Bitmap;
                        Bitmap.UriSource = uri;
                        ApplicationData.Current.LocalSettings.Values["PictureBackgroundUri"] = uri.ToString();
                    }
                }
                else
                {
                    BitmapImage Bitmap = new BitmapImage();
                    PictureBackgroundBrush.ImageSource = Bitmap;
                    Bitmap.UriSource = uri;
                    ApplicationData.Current.LocalSettings.Values["PictureBackgroundUri"] = uri.ToString();
                }
            }

            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(BackgroundBrush)));
        }

        /// <summary>
        /// 将16进制字符串转换成Color对象
        /// </summary>
        /// <param name="Hex">十六进制字符串</param>
        /// <returns></returns>
        public static Color GetColorFromHexString(string Hex)
        {
            if (string.IsNullOrWhiteSpace(Hex))
            {
                throw new ArgumentException("Hex could not be null or empty", nameof(Hex));
            }

            Hex = Hex.Replace("#", string.Empty);

            bool ExistAlpha = Hex.Length == 8 || Hex.Length == 4;
            bool IsDoubleHex = Hex.Length == 8 || Hex.Length == 6;

            if (!ExistAlpha && Hex.Length != 6 && Hex.Length != 3)
            {
                throw new ArgumentException("输入的hex不是有效颜色");
            }

            int n = 0;
            byte a;
            int HexCount = IsDoubleHex ? 2 : 1;
            if (ExistAlpha)
            {
                n = HexCount;
                a = (byte)ConvertHexToByte(Hex, 0, HexCount);
                if (!IsDoubleHex)
                {
                    a = (byte)(a * 16 + a);
                }
            }
            else
            {
                a = 0xFF;
            }

            var r = (byte)ConvertHexToByte(Hex, n, HexCount);
            var g = (byte)ConvertHexToByte(Hex, n + HexCount, HexCount);
            var b = (byte)ConvertHexToByte(Hex, n + 2 * HexCount, HexCount);
            if (!IsDoubleHex)
            {
                r = (byte)(r * 16 + r);
                g = (byte)(g * 16 + g);
                b = (byte)(b * 16 + b);
            }

            return Color.FromArgb(a, r, g, b);
        }

        /// <summary>
        /// 将十六进制字符串转换成byte
        /// </summary>
        /// <param name="hex">十六进制字符串</param>
        /// <param name="n">起始位置</param>
        /// <param name="count">长度</param>
        /// <returns></returns>
        private static uint ConvertHexToByte(string hex, int n, int count = 2)
        {
            return Convert.ToUInt32(hex.Substring(n, count), 16);
        }
    }
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
    /// <summary>
    /// 提供ContentDialog队列(按序)弹出的实现
    /// </summary>
    public class QueueContentDialog : ContentDialog
    {
        private static readonly AutoResetEvent Locker = new AutoResetEvent(true);

        private static int WaitCount = 0;

        /// <summary>
        /// 指示当前是否存在正处于弹出状态的ContentDialog
        /// </summary>
        public static bool IsRunningOrWaiting
        {
            get
            {
                return WaitCount != 0;
            }
        }

        private bool IsCloseRequested = false;
        private ContentDialogResult CloseWithResult;

        /// <summary>
        /// 显示对话框
        /// </summary>
        /// <returns></returns>
        public new async Task<ContentDialogResult> ShowAsync()
        {
            _ = Interlocked.Increment(ref WaitCount);

            await Task.Run(() =>
            {
                Locker.WaitOne();
            });

            var Result = await base.ShowAsync();

            _ = Interlocked.Decrement(ref WaitCount);

            Locker.Set();

            if (IsCloseRequested)
            {
                IsCloseRequested = false;
                return CloseWithResult;
            }
            else
            {
                return Result;
            }
        }

        /// <summary>
        /// 关闭对话框并返回指定的枚举值
        /// </summary>
        /// <param name="CloseWithResult"></param>
        public void Close(ContentDialogResult CloseWithResult)
        {
            IsCloseRequested = true;
            this.CloseWithResult = CloseWithResult;
            Hide();
        }

        /// <summary>
        /// 初始化QueueContentDialog
        /// </summary>
        public QueueContentDialog()
        {
            if (AppThemeController.Current.Theme == ElementTheme.Dark)
            {
                Background = Application.Current.Resources["DialogAcrylicBrush"] as Brush;
                RequestedTheme = ElementTheme.Dark;
            }
            else
            {
                RequestedTheme = ElementTheme.Light;
            }
        }
    }
    #endregion

    #region 反馈对象
    /// <summary>
    /// 提供对反馈内容的UI支持
    /// </summary>
    public sealed class FeedBackItem : INotifyPropertyChanged
    {
        /// <summary>
        /// 用户名
        /// </summary>
        public string UserName { get; private set; }

        /// <summary>
        /// 建议或反馈内容
        /// </summary>
        public string Suggestion { get; private set; }

        /// <summary>
        /// 支持的人数
        /// </summary>
        public string LikeNum { get; private set; }

        /// <summary>
        /// 踩的人数
        /// </summary>
        public string DislikeNum { get; private set; }

        /// <summary>
        /// 文字描述
        /// </summary>
        public string SupportDescription { get; private set; }

        /// <summary>
        /// 标题
        /// </summary>
        public string Title { get; private set; }

        /// <summary>
        /// 用户ID
        /// </summary>
        public string UserID { get; private set; }

        /// <summary>
        /// 此反馈的GUID
        /// </summary>
        public string GUID { get; private set; }

        /// <summary>
        /// 记录当前用户的操作
        /// </summary>
        public string UserVoteAction
        {
            get
            {
                return userVoteAction;
            }
            set
            {
                switch (value)
                {
                    case "=":
                        {
                            isLike = false;
                            isDislike = false;
                            break;
                        }
                    case "+":
                        {
                            isLike = true;
                            isDislike = false;
                            break;
                        }
                    case "-":
                        {
                            isLike = false;
                            isDislike = true;
                            break;
                        }
                }
                userVoteAction = value;
            }
        }

        public bool? IsLike
        {
            get
            {
                return isLike;
            }
            set
            {
                if (value != isLike)
                {
                    if (value.GetValueOrDefault())
                    {
                        isLike = true;
                        isDislike = false;
                        UpdateSupportInfo(FeedBackUpdateType.AddLike);
                    }
                    else
                    {
                        isLike = false;
                        isDislike = false;
                        UpdateSupportInfo(FeedBackUpdateType.DelLike);
                    }
                }
            }
        }

        public bool? IsDislike
        {
            get
            {
                return isDislike;
            }
            set
            {
                if (value != isDislike)
                {
                    if (value.GetValueOrDefault())
                    {
                        isLike = false;
                        isDislike = true;
                        UpdateSupportInfo(FeedBackUpdateType.AddDislike);
                    }
                    else
                    {
                        isLike = false;
                        isDislike = false;
                        UpdateSupportInfo(FeedBackUpdateType.DelDislike);
                    }
                }
            }
        }

        private string userVoteAction;
        private bool? isLike;
        private bool? isDislike;
        private static AutoResetEvent Locker = new AutoResetEvent(true);

        /// <summary>
        /// 初始化FeedBackItem
        /// </summary>
        /// <param name="UserName">用户名</param>
        /// <param name="Title">标题</param>
        /// <param name="Suggestion">建议或反馈内容</param>
        /// <param name="LikeNum">支持的人数</param>
        /// <param name="DislikeNum">反对的人数</param>
        /// <param name="UserID">用户ID</param>
        /// <param name="GUID">反馈的GUID</param>
        /// <param name="UserVoteAction">指示支持或反对</param>
        public FeedBackItem(string UserName, string Title, string Suggestion, string LikeNum, string DislikeNum, string UserID, string GUID, string UserVoteAction = "=")
        {
            this.UserName = UserName;
            this.Title = Title;
            this.Suggestion = Suggestion;
            this.LikeNum = LikeNum;
            this.DislikeNum = DislikeNum;
            this.UserID = UserID;
            this.GUID = GUID;
            this.UserVoteAction = UserVoteAction;
            if (Globalization.Language == LanguageEnum.Chinese)
            {
                SupportDescription = $"({LikeNum} 人支持 , {DislikeNum} 人反对)";
            }
            else
            {
                SupportDescription = $"({LikeNum} people agree , {DislikeNum} people against)";
            }
        }

        /// <summary>
        /// 更新支持或反对的信息
        /// </summary>
        /// <param name="Type">更新类型</param>
        private async void UpdateSupportInfo(FeedBackUpdateType Type)
        {
            switch (Type)
            {
                case FeedBackUpdateType.AddLike:
                    {
                        if (UserVoteAction == "-")
                        {
                            DislikeNum = (Convert.ToInt16(DislikeNum) - 1).ToString();
                        }

                        LikeNum = (Convert.ToInt16(LikeNum) + 1).ToString();
                        UserVoteAction = "+";
                        break;
                    }
                case FeedBackUpdateType.DelLike:
                    {
                        LikeNum = (Convert.ToInt16(LikeNum) - 1).ToString();
                        UserVoteAction = "=";
                        break;
                    }
                case FeedBackUpdateType.AddDislike:
                    {
                        if (UserVoteAction == "+")
                        {
                            LikeNum = (Convert.ToInt16(LikeNum) - 1).ToString();
                        }

                        DislikeNum = (Convert.ToInt16(DislikeNum) + 1).ToString();
                        UserVoteAction = "-";
                        break;
                    }
                case FeedBackUpdateType.DelDislike:
                    {
                        DislikeNum = (Convert.ToInt16(DislikeNum) - 1).ToString();
                        UserVoteAction = "=";
                        break;
                    }
            }

            SupportDescription = Globalization.Language == LanguageEnum.Chinese
                                 ? $"({LikeNum} 人支持 , {DislikeNum} 人反对)"
                                 : $"({LikeNum} people agree , {DislikeNum} people against)";

            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SupportDescription)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsLike)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsDislike)));

            await Task.Run(() =>
            {
                Locker.WaitOne();
            }).ConfigureAwait(true);

            try
            {
                if (!await MySQL.Current.UpdateFeedBackVoteAsync(this).ConfigureAwait(true))
                {
                    if (Globalization.Language == LanguageEnum.Chinese)
                    {
                        QueueContentDialog dialog = new QueueContentDialog
                        {
                            Title = "错误",
                            Content = "因网络原因无法进行此项操作",
                            CloseButtonText = "确定"
                        };
                        _ = await dialog.ShowAsync().ConfigureAwait(true);
                    }
                    else
                    {
                        QueueContentDialog dialog = new QueueContentDialog
                        {
                            Title = "Error",
                            Content = "This operation cannot be performed due to network reasons",
                            CloseButtonText = "Got it"
                        };
                        _ = await dialog.ShowAsync().ConfigureAwait(true);
                    }
                }
            }
            finally
            {
                Locker.Set();
            }
        }

        /// <summary>
        /// 更新反馈内容
        /// </summary>
        /// <param name="Title">标题</param>
        /// <param name="Suggestion">建议</param>
        public void UpdateTitleAndSuggestion(string Title, string Suggestion)
        {
            this.Title = Title;
            this.Suggestion = Suggestion;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(this.Title)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(this.Suggestion)));
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }

    /// <summary>
    /// 反馈更新的类型
    /// </summary>
    public enum FeedBackUpdateType
    {
        /// <summary>
        /// 新增支持
        /// </summary>
        AddLike = 0,
        /// <summary>
        /// 删除支持
        /// </summary>
        DelLike = 1,
        /// <summary>
        /// 新增反对
        /// </summary>
        AddDislike = 2,
        /// <summary>
        /// 删除反对
        /// </summary>
        DelDislike = 4
    }
    #endregion

    #region 背景图片
    /// <summary>
    /// 提供对背景图片选择的UI支持
    /// </summary>
    public sealed class BackgroundPicture
    {
        /// <summary>
        /// 背景图片
        /// </summary>
        public BitmapImage Picture { get; private set; }

        /// <summary>
        /// 图片Uri
        /// </summary>
        public Uri PictureUri { get; private set; }

        /// <summary>
        /// 初始化BackgroundPicture
        /// </summary>
        /// <param name="Picture">图片</param>
        /// <param name="PictureUri">图片Uri</param>
        public BackgroundPicture(BitmapImage Picture, Uri PictureUri)
        {
            this.Picture = Picture;
            this.PictureUri = PictureUri;
        }
    }
    #endregion

    #region 错误类型
    /// <summary>
    /// 密码错误异常
    /// </summary>
    public sealed class PasswordErrorException : Exception
    {
        public PasswordErrorException(string ErrorMessage) : base(ErrorMessage)
        {
        }

        public PasswordErrorException() : base()
        {
        }

        public PasswordErrorException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }

    /// <summary>
    /// 文件损坏异常
    /// </summary>
    public sealed class FileDamagedException : Exception
    {
        public FileDamagedException(string ErrorMessage) : base(ErrorMessage)
        {
        }

        public FileDamagedException() : base()
        {
        }

        public FileDamagedException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }

    /// <summary>
    /// 网络错误
    /// </summary>
    public sealed class NetworkException : Exception
    {
        public NetworkException(string ErrorMessage) : base(ErrorMessage)
        {

        }

        public NetworkException() : base()
        {

        }

        public NetworkException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
    #endregion

    #region 本地化指示器
    /// <summary>
    /// 语言枚举
    /// </summary>
    public enum LanguageEnum
    {
        /// <summary>
        /// 界面使用中文
        /// </summary>
        Chinese = 1,
        /// <summary>
        /// 界面使用英文
        /// </summary>
        English = 2
    }

    /// <summary>
    /// 指示UI语言类型
    /// </summary>
    public static class Globalization
    {
        /// <summary>
        /// 当前使用的语言
        /// </summary>
        public static LanguageEnum Language { get; private set; }

        public static void SetAppPrimaryLanguageAccordingToSystemLanguage()
        {
            ApplicationLanguages.PrimaryLanguageOverride = Language == LanguageEnum.English ? "en-US" : "zh-Hans";
        }

        static Globalization()
        {
            Language = GlobalizationPreferences.Languages[0].StartsWith("zh", StringComparison.OrdinalIgnoreCase) ? LanguageEnum.Chinese : LanguageEnum.English;
        }
    }
    #endregion

    #region WindowsHello授权管理器
    /// <summary>
    /// Windows Hello授权状态
    /// </summary>
    public enum AuthenticatorState
    {
        /// <summary>
        /// 注册成功
        /// </summary>
        RegisterSuccess = 0,
        /// <summary>
        /// 用户取消
        /// </summary>
        UserCanceled = 1,
        /// <summary>
        /// 凭据丢失
        /// </summary>
        CredentialNotFound = 2,
        /// <summary>
        /// 未知错误
        /// </summary>
        UnknownError = 4,
        /// <summary>
        /// 系统不支持Windows Hello
        /// </summary>
        WindowsHelloUnsupport = 8,
        /// <summary>
        /// 授权通过
        /// </summary>
        VerifyPassed = 16,
        /// <summary>
        /// 授权失败
        /// </summary>
        VerifyFailed = 32,
        /// <summary>
        /// 用户未注册
        /// </summary>
        UserNotRegistered = 64
    }

    /// <summary>
    /// Windows Hello授权管理器
    /// </summary>
    public static class WindowsHelloAuthenticator
    {
        /// <summary>
        /// 质询内容
        /// </summary>
        private const string ChallengeText = "This is a challenge send by RX, to verify secure area access authorization";

        /// <summary>
        /// 凭据保存的名称
        /// </summary>
        private const string CredentialName = "RX-SecureProtection";

        /// <summary>
        /// 检查系统是否支持Windows Hello
        /// </summary>
        /// <returns></returns>
        public static Task<bool> CheckSupportAsync()
        {
            return KeyCredentialManager.IsSupportedAsync().AsTask();
        }

        /// <summary>
        /// 请求注册用户
        /// </summary>
        /// <returns></returns>
        public static async Task<AuthenticatorState> RegisterUserAsync()
        {
            if (await CheckSupportAsync().ConfigureAwait(false))
            {
                KeyCredentialRetrievalResult CredentiaResult = await KeyCredentialManager.RequestCreateAsync(CredentialName, KeyCredentialCreationOption.ReplaceExisting);
                switch (CredentiaResult.Status)
                {
                    case KeyCredentialStatus.Success:
                        {
                            string PublicKey = CryptographicBuffer.EncodeToHexString(CredentiaResult.Credential.RetrievePublicKey());
                            ApplicationData.Current.LocalSettings.Values["WindowsHelloPublicKeyForUser"] = PublicKey;
                            return AuthenticatorState.RegisterSuccess;
                        }
                    case KeyCredentialStatus.UserCanceled:
                        {
                            return AuthenticatorState.UserCanceled;
                        }
                    default:
                        {
                            return AuthenticatorState.UnknownError;
                        }
                }
            }
            else
            {
                return AuthenticatorState.WindowsHelloUnsupport;
            }
        }

        /// <summary>
        /// 认证用户授权
        /// </summary>
        /// <returns></returns>
        public static async Task<AuthenticatorState> VerifyUserAsync()
        {
            if (await CheckSupportAsync().ConfigureAwait(false))
            {
                if (ApplicationData.Current.LocalSettings.Values["WindowsHelloPublicKeyForUser"] is string PublicKey)
                {
                    KeyCredentialRetrievalResult RetrievalResult = await KeyCredentialManager.OpenAsync(CredentialName);
                    switch (RetrievalResult.Status)
                    {
                        case KeyCredentialStatus.Success:
                            {
                                KeyCredentialOperationResult OperationResult = await RetrievalResult.Credential.RequestSignAsync(CryptographicBuffer.ConvertStringToBinary(ChallengeText, BinaryStringEncoding.Utf8));
                                if (OperationResult.Status == KeyCredentialStatus.Success)
                                {
                                    var Algorithm = AsymmetricKeyAlgorithmProvider.OpenAlgorithm(AsymmetricAlgorithmNames.RsaSignPkcs1Sha256);
                                    var Key = Algorithm.ImportPublicKey(CryptographicBuffer.DecodeFromHexString(PublicKey));
                                    return CryptographicEngine.VerifySignature(Key, CryptographicBuffer.ConvertStringToBinary(ChallengeText, BinaryStringEncoding.Utf8), OperationResult.Result) ? AuthenticatorState.VerifyPassed : AuthenticatorState.VerifyFailed;
                                }
                                else
                                {
                                    return AuthenticatorState.UnknownError;
                                }
                            }
                        case KeyCredentialStatus.NotFound:
                            {
                                return AuthenticatorState.CredentialNotFound;
                            }
                        default:
                            {
                                return AuthenticatorState.UnknownError;
                            }
                    }
                }
                else
                {
                    return AuthenticatorState.UserNotRegistered;
                }
            }
            else
            {
                return AuthenticatorState.WindowsHelloUnsupport;
            }
        }

        /// <summary>
        /// 删除并注销用户
        /// </summary>
        /// <returns></returns>
        public static async Task DeleteUserAsync()
        {
            if (await CheckSupportAsync().ConfigureAwait(false))
            {
                try
                {
                    ApplicationData.Current.LocalSettings.Values["WindowsHelloPublicKeyForUser"] = null;
                    await KeyCredentialManager.DeleteAsync(CredentialName);
                }
                catch (Exception)
                {
                }
            }
        }
    }
    #endregion

    #region 密码生成器
    /// <summary>
    /// 提供密码或散列函数的实现
    /// </summary>
    public static class KeyGenerator
    {
        /// <summary>
        /// 获取可变长度的MD5散列值
        /// </summary>
        /// <param name="OriginKey">要散列的内容</param>
        /// <param name="Length">返回结果的长度</param>
        /// <returns></returns>
        public static string GetMD5FromKey(string OriginKey, int Length = 32)
        {
            using (MD5 md5 = MD5.Create())
            {
                byte[] bytes = Encoding.UTF8.GetBytes(OriginKey);
                byte[] Md5Buffer = md5.ComputeHash(bytes);
                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < Md5Buffer.Length; i++)
                {
                    _ = sb.Append(Md5Buffer[i].ToString("x2"));
                }

                if (Length <= 32)
                {
                    return sb.ToString().Substring((32 - Length) / 2, Length);
                }
                else
                {
                    string Result = sb.ToString();
                    return Result + Result.Substring(0, Length - 32);
                }
            }
        }

        /// <summary>
        /// 生成指定长度的随机密钥
        /// </summary>
        /// <param name="Length">密钥长度</param>
        /// <returns></returns>
        public static string GetRandomKey(uint Length)
        {
            StringBuilder Builder = new StringBuilder();
            Random CharNumRandom = new Random();

            for (int i = 0; i < Length; i++)
            {
                switch (CharNumRandom.Next(0, 3))
                {
                    case 0:
                        {
                            _ = Builder.Append((char)CharNumRandom.Next(65, 91));
                            break;
                        }
                    case 1:
                        {
                            _ = Builder.Append((char)CharNumRandom.Next(97, 123));
                            break;
                        }
                    case 2:
                        {
                            _ = Builder.Append((char)CharNumRandom.Next(48, 58));
                            break;
                        }
                }
            }

            return Builder.ToString();
        }
    }
    #endregion

    #region 凭据保护器
    /// <summary>
    /// 提供对用户凭据的保护功能
    /// </summary>
    public static class CredentialProtector
    {
        /// <summary>
        /// 从凭据保护器中取得密码
        /// </summary>
        /// <param name="Name">名称</param>
        /// <returns></returns>
        public static string GetPasswordFromProtector(string Name)
        {
            PasswordVault Vault = new PasswordVault();

            PasswordCredential Credential = Vault.RetrieveAll().Where((Cre) => Cre.UserName == Name).FirstOrDefault();

            if (Credential != null)
            {
                Credential.RetrievePassword();
                return Credential.Password;
            }
            else
            {
                return string.Empty;
            }
        }

        /// <summary>
        /// 请求保护指定的内容
        /// </summary>
        /// <param name="Name">用户名</param>
        /// <param name="Password">密码</param>
        public static void RequestProtectPassword(string Name, string Password)
        {
            PasswordVault Vault = new PasswordVault();

            PasswordCredential Credential = Vault.RetrieveAll().Where((Cre) => Cre.UserName == Name).FirstOrDefault();

            if (Credential != null)
            {
                Vault.Remove(Credential);
            }

            Vault.Add(new PasswordCredential("RX_Secure_Vault", Name, Password));
        }
    }
    #endregion

    #region 通用文件转换器
    /// <summary>
    /// 提供各类文件的转换功能
    /// </summary>
    public static class GeneralTransformer
    {
        private static CancellationTokenSource AVTranscodeCancellation;

        /// <summary>
        /// 指示是否存在正在进行中的任务
        /// </summary>
        public static bool IsAnyTransformTaskRunning { get; private set; } = false;

        /// <summary>
        /// 将指定的视频文件合并并产生新文件
        /// </summary>
        /// <param name="DestinationFile">新文件</param>
        /// <param name="Composition">片段</param>
        /// <param name="EncodingProfile">编码</param>
        /// <returns></returns>
        public static Task GenerateMergeVideoFromOriginAsync(StorageFile DestinationFile, MediaComposition Composition, MediaEncodingProfile EncodingProfile)
        {
            return Task.Factory.StartNew((ob) =>
            {
                IsAnyTransformTaskRunning = true;

                AVTranscodeCancellation = new CancellationTokenSource();

                var Para = (ValueTuple<StorageFile, MediaComposition, MediaEncodingProfile>)ob;

                SendUpdatableToastWithProgressForMergeVideo();
                Progress<double> CropVideoProgress = new Progress<double>((CurrentValue) =>
                {
                    string Tag = "MergeVideoNotification";

                    var data = new NotificationData
                    {
                        SequenceNumber = 0
                    };
                    data.Values["ProgressValue"] = Math.Round(CurrentValue / 100, 2, MidpointRounding.AwayFromZero).ToString();
                    data.Values["ProgressValueString"] = Convert.ToInt32(CurrentValue) + "%";

                    ToastNotificationManager.CreateToastNotifier().Update(data, Tag);
                });

                try
                {
                    Para.Item2.RenderToFileAsync(Para.Item1, MediaTrimmingPreference.Precise, Para.Item3).AsTask(AVTranscodeCancellation.Token, CropVideoProgress).Wait();
                    ApplicationData.Current.LocalSettings.Values["MediaMergeStatus"] = "Success";
                }
                catch (AggregateException)
                {
                    ApplicationData.Current.LocalSettings.Values["MediaMergeStatus"] = "Cancel";
                    Para.Item1.DeleteAsync(StorageDeleteOption.PermanentDelete).AsTask().Wait();
                }
                catch (Exception e)
                {
                    ApplicationData.Current.LocalSettings.Values["MediaMergeStatus"] = e.Message;
                    Para.Item1.DeleteAsync(StorageDeleteOption.PermanentDelete).AsTask().Wait();
                }

            }, (DestinationFile, Composition, EncodingProfile), CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Current).ContinueWith((task) =>
              {
                  CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                  {
                      if (Globalization.Language == LanguageEnum.Chinese)
                      {
                          switch (ApplicationData.Current.LocalSettings.Values["MediaMergeStatus"].ToString())
                          {
                              case "Success":
                                  {
                                      TabViewContainer.ThisPage.Notification.Show("视频已成功完成合并", 5000);
                                      ShowMergeCompleteNotification();
                                      break;
                                  }
                              case "Cancel":
                                  {
                                      TabViewContainer.ThisPage.Notification.Show("视频合并任务被取消", 5000);
                                      ShowMergeCancelNotification();
                                      break;
                                  }
                              default:
                                  {
                                      TabViewContainer.ThisPage.Notification.Show("合并视频时遇到未知错误", 5000);
                                      break;
                                  }
                          }
                      }
                      else
                      {
                          switch (ApplicationData.Current.LocalSettings.Values["MediaMergeStatus"].ToString())
                          {
                              case "Success":
                                  {
                                      TabViewContainer.ThisPage.Notification.Show("Video successfully merged", 5000);
                                      ShowMergeCompleteNotification();
                                      break;
                                  }
                              case "Cancel":
                                  {
                                      TabViewContainer.ThisPage.Notification.Show("Video merge task was canceled", 5000);
                                      ShowMergeCancelNotification();
                                      break;
                                  }
                              default:
                                  {
                                      TabViewContainer.ThisPage.Notification.Show("Encountered unknown error while merging video", 5000);
                                      break;
                                  }
                          }
                      }
                  }).AsTask().Wait();

                  IsAnyTransformTaskRunning = false;

              }, TaskScheduler.Current);
        }

        /// <summary>
        /// 将指定的视频文件裁剪后保存值新文件中
        /// </summary>
        /// <param name="DestinationFile">新文件</param>
        /// <param name="Composition">片段</param>
        /// <param name="EncodingProfile">编码</param>
        /// <param name="TrimmingPreference">裁剪精度</param>
        /// <returns></returns>
        public static Task GenerateCroppedVideoFromOriginAsync(StorageFile DestinationFile, MediaComposition Composition, MediaEncodingProfile EncodingProfile, MediaTrimmingPreference TrimmingPreference)
        {
            return Task.Factory.StartNew((ob) =>
            {
                IsAnyTransformTaskRunning = true;

                AVTranscodeCancellation = new CancellationTokenSource();

                var Para = (ValueTuple<StorageFile, MediaComposition, MediaEncodingProfile, MediaTrimmingPreference>)ob;

                SendUpdatableToastWithProgressForCropVideo(Para.Item1);
                Progress<double> CropVideoProgress = new Progress<double>((CurrentValue) =>
                {
                    string Tag = "CropVideoNotification";

                    var data = new NotificationData
                    {
                        SequenceNumber = 0
                    };
                    data.Values["ProgressValue"] = Math.Round(CurrentValue / 100, 2, MidpointRounding.AwayFromZero).ToString();
                    data.Values["ProgressValueString"] = Convert.ToInt32(CurrentValue) + "%";

                    ToastNotificationManager.CreateToastNotifier().Update(data, Tag);
                });

                try
                {
                    Para.Item2.RenderToFileAsync(Para.Item1, Para.Item4, Para.Item3).AsTask(AVTranscodeCancellation.Token, CropVideoProgress).Wait();
                    ApplicationData.Current.LocalSettings.Values["MediaCropStatus"] = "Success";
                }
                catch (AggregateException)
                {
                    ApplicationData.Current.LocalSettings.Values["MediaCropStatus"] = "Cancel";
                    Para.Item1.DeleteAsync(StorageDeleteOption.PermanentDelete).AsTask().Wait();
                }
                catch (Exception e)
                {
                    ApplicationData.Current.LocalSettings.Values["MediaCropStatus"] = e.Message;
                    Para.Item1.DeleteAsync(StorageDeleteOption.PermanentDelete).AsTask().Wait();
                }

            }, (DestinationFile, Composition, EncodingProfile, TrimmingPreference), CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Current).ContinueWith((task) =>
               {
                   CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                   {
                       if (Globalization.Language == LanguageEnum.Chinese)
                       {
                           switch (ApplicationData.Current.LocalSettings.Values["MediaCropStatus"].ToString())
                           {
                               case "Success":
                                   {
                                       TabViewContainer.ThisPage.Notification.Show("视频已成功完成裁剪", 5000);
                                       ShowCropCompleteNotification();
                                       break;
                                   }
                               case "Cancel":
                                   {
                                       TabViewContainer.ThisPage.Notification.Show("视频裁剪任务被取消", 5000);
                                       ShowCropCancelNotification();
                                       break;
                                   }
                               default:
                                   {
                                       TabViewContainer.ThisPage.Notification.Show("裁剪视频时遇到未知错误", 5000);
                                       break;
                                   }
                           }
                       }
                       else
                       {
                           switch (ApplicationData.Current.LocalSettings.Values["MediaCropStatus"].ToString())
                           {
                               case "Success":
                                   {
                                       TabViewContainer.ThisPage.Notification.Show("Video successfully cropped", 5000);
                                       ShowCropCompleteNotification();
                                       break;
                                   }
                               case "Cancel":
                                   {
                                       TabViewContainer.ThisPage.Notification.Show("Video crop task was canceled", 5000);
                                       ShowCropCancelNotification();
                                       break;
                                   }
                               default:
                                   {
                                       TabViewContainer.ThisPage.Notification.Show("Encountered unknown error while cropping video", 5000);
                                       break;
                                   }
                           }
                       }
                   }).AsTask().Wait();

                   IsAnyTransformTaskRunning = false;

               }, TaskScheduler.Current);
        }

        /// <summary>
        /// 提供图片转码
        /// </summary>
        /// <param name="SourceFile">源文件</param>
        /// <param name="DestinationFile">目标文件</param>
        /// <param name="IsEnableScale">是否启用缩放</param>
        /// <param name="ScaleWidth">缩放宽度</param>
        /// <param name="ScaleHeight">缩放高度</param>
        /// <param name="InterpolationMode">插值模式</param>
        /// <returns></returns>
        public static Task TranscodeFromImageAsync(StorageFile SourceFile, StorageFile DestinationFile, bool IsEnableScale = false, uint ScaleWidth = default, uint ScaleHeight = default, BitmapInterpolationMode InterpolationMode = default)
        {
            return Task.Run(() =>
            {
                IsAnyTransformTaskRunning = true;

                using (var OriginStream = SourceFile.OpenAsync(FileAccessMode.Read).AsTask().Result)
                {
                    BitmapDecoder Decoder = BitmapDecoder.CreateAsync(OriginStream).AsTask().Result;
                    using (var TargetStream = DestinationFile.OpenAsync(FileAccessMode.ReadWrite).AsTask().Result)
                    using (var TranscodeImage = Decoder.GetSoftwareBitmapAsync().AsTask().Result)
                    {
                        BitmapEncoder Encoder = DestinationFile.FileType switch
                        {
                            ".png" => BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, TargetStream).AsTask().Result,
                            ".jpg" => BitmapEncoder.CreateAsync(BitmapEncoder.JpegEncoderId, TargetStream).AsTask().Result,
                            ".bmp" => BitmapEncoder.CreateAsync(BitmapEncoder.BmpEncoderId, TargetStream).AsTask().Result,
                            ".heic" => BitmapEncoder.CreateAsync(BitmapEncoder.HeifEncoderId, TargetStream).AsTask().Result,
                            ".tiff" => BitmapEncoder.CreateAsync(BitmapEncoder.TiffEncoderId, TargetStream).AsTask().Result,
                            _ => throw new InvalidOperationException("Unsupport image format"),
                        };

                        if (IsEnableScale)
                        {
                            Encoder.BitmapTransform.ScaledWidth = ScaleWidth;
                            Encoder.BitmapTransform.ScaledHeight = ScaleHeight;
                            Encoder.BitmapTransform.InterpolationMode = InterpolationMode;
                        }

                        Encoder.SetSoftwareBitmap(TranscodeImage);
                        Encoder.IsThumbnailGenerated = true;
                        try
                        {
                            Encoder.FlushAsync().AsTask().Wait();
                        }
                        catch (Exception err)
                        {
                            if (err.HResult == unchecked((int)0x88982F81))
                            {
                                Encoder.IsThumbnailGenerated = false;
                                Encoder.FlushAsync().AsTask().Wait();
                            }
                            else
                            {
                                try
                                {
                                    DestinationFile.DeleteAsync(StorageDeleteOption.PermanentDelete).AsTask().Wait();
                                }
                                catch
                                {

                                }

                                CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
                                {
                                    if (Globalization.Language == LanguageEnum.Chinese)
                                    {
                                        QueueContentDialog dialog = new QueueContentDialog
                                        {
                                            Title = "错误",
                                            Content = "由于缺失编码器或操作不受支持，无法进行图像格式转换",
                                            CloseButtonText = "确定"
                                        };
                                        _ = await dialog.ShowAsync().ConfigureAwait(true);
                                    }
                                    else
                                    {
                                        QueueContentDialog dialog = new QueueContentDialog
                                        {
                                            Title = "Error",
                                            Content = "Cannot convert image format due to missing encoder or unsupported operation",
                                            CloseButtonText = "Got it"
                                        };
                                        _ = await dialog.ShowAsync().ConfigureAwait(true);
                                    }
                                }).AsTask().Wait();
                            }
                        }
                    }
                }

                IsAnyTransformTaskRunning = false;
            });
        }

        /// <summary>
        /// 提供音视频转码
        /// </summary>
        /// <param name="SourceFile">源文件</param>
        /// <param name="DestinationFile">目标文件</param>
        /// <param name="MediaTranscodeEncodingProfile">转码编码</param>
        /// <param name="MediaTranscodeQuality">转码质量</param>
        /// <param name="SpeedUp">是否启用硬件加速</param>
        /// <returns></returns>
        public static Task TranscodeFromAudioOrVideoAsync(StorageFile SourceFile, StorageFile DestinationFile, string MediaTranscodeEncodingProfile, string MediaTranscodeQuality, bool SpeedUp)
        {
            return Task.Factory.StartNew((ob) =>
            {
                IsAnyTransformTaskRunning = true;

                AVTranscodeCancellation = new CancellationTokenSource();

                var Para = (ValueTuple<StorageFile, StorageFile, string, string, bool>)ob;

                MediaTranscoder Transcoder = new MediaTranscoder
                {
                    HardwareAccelerationEnabled = true,
                    VideoProcessingAlgorithm = Para.Item5 ? MediaVideoProcessingAlgorithm.Default : MediaVideoProcessingAlgorithm.MrfCrf444
                };

                try
                {
                    MediaEncodingProfile Profile = null;
                    VideoEncodingQuality VideoQuality = default;
                    AudioEncodingQuality AudioQuality = default;

                    switch (Para.Item4)
                    {
                        case "UHD2160p":
                            VideoQuality = VideoEncodingQuality.Uhd2160p;
                            break;
                        case "QVGA":
                            VideoQuality = VideoEncodingQuality.Qvga;
                            break;
                        case "HD1080p":
                            VideoQuality = VideoEncodingQuality.HD1080p;
                            break;
                        case "HD720p":
                            VideoQuality = VideoEncodingQuality.HD720p;
                            break;
                        case "WVGA":
                            VideoQuality = VideoEncodingQuality.Wvga;
                            break;
                        case "VGA":
                            VideoQuality = VideoEncodingQuality.Vga;
                            break;
                        case "High":
                            AudioQuality = AudioEncodingQuality.High;
                            break;
                        case "Medium":
                            AudioQuality = AudioEncodingQuality.Medium;
                            break;
                        case "Low":
                            AudioQuality = AudioEncodingQuality.Low;
                            break;
                    }

                    switch (Para.Item3)
                    {
                        case "MKV":
                            Profile = MediaEncodingProfile.CreateHevc(VideoQuality);
                            break;
                        case "MP4":
                            Profile = MediaEncodingProfile.CreateMp4(VideoQuality);
                            break;
                        case "WMV":
                            Profile = MediaEncodingProfile.CreateWmv(VideoQuality);
                            break;
                        case "AVI":
                            Profile = MediaEncodingProfile.CreateAvi(VideoQuality);
                            break;
                        case "MP3":
                            Profile = MediaEncodingProfile.CreateMp3(AudioQuality);
                            break;
                        case "ALAC":
                            Profile = MediaEncodingProfile.CreateAlac(AudioQuality);
                            break;
                        case "WMA":
                            Profile = MediaEncodingProfile.CreateWma(AudioQuality);
                            break;
                        case "M4A":
                            Profile = MediaEncodingProfile.CreateM4a(AudioQuality);
                            break;
                    }

                    PrepareTranscodeResult Result = Transcoder.PrepareFileTranscodeAsync(Para.Item1, Para.Item2, Profile).AsTask().Result;
                    if (Result.CanTranscode)
                    {
                        SendUpdatableToastWithProgressForTranscode(Para.Item1, Para.Item2);
                        Progress<double> TranscodeProgress = new Progress<double>((CurrentValue) =>
                        {
                            string Tag = "TranscodeNotification";

                            var data = new NotificationData
                            {
                                SequenceNumber = 0
                            };
                            data.Values["ProgressValue"] = (Math.Ceiling(CurrentValue) / 100).ToString();
                            data.Values["ProgressValueString"] = Convert.ToInt32(CurrentValue) + "%";

                            ToastNotificationManager.CreateToastNotifier().Update(data, Tag);
                        });

                        Result.TranscodeAsync().AsTask(AVTranscodeCancellation.Token, TranscodeProgress).Wait();

                        ApplicationData.Current.LocalSettings.Values["MediaTranscodeStatus"] = "Success";
                    }
                    else
                    {
                        ApplicationData.Current.LocalSettings.Values["MediaTranscodeStatus"] = "NotSupport";
                        Para.Item2.DeleteAsync(StorageDeleteOption.PermanentDelete).AsTask().Wait();
                    }
                }
                catch (AggregateException)
                {
                    ApplicationData.Current.LocalSettings.Values["MediaTranscodeStatus"] = "Cancel";
                    Para.Item2.DeleteAsync(StorageDeleteOption.PermanentDelete).AsTask().Wait();
                }
                catch (Exception e)
                {
                    ApplicationData.Current.LocalSettings.Values["MediaTranscodeStatus"] = e.Message;
                    Para.Item2.DeleteAsync(StorageDeleteOption.PermanentDelete).AsTask().Wait();
                }
            }, (SourceFile, DestinationFile, MediaTranscodeEncodingProfile, MediaTranscodeQuality, SpeedUp), CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Current).ContinueWith((task, ob) =>
               {
                   AVTranscodeCancellation.Dispose();
                   AVTranscodeCancellation = null;

                   var Para = (ValueTuple<StorageFile, StorageFile>)ob;

                   if (ApplicationData.Current.LocalSettings.Values["MediaTranscodeStatus"] is string ExcuteStatus)
                   {
                       CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                       {
                           if (Globalization.Language == LanguageEnum.Chinese)
                           {
                               switch (ExcuteStatus)
                               {
                                   case "Success":
                                       TabViewContainer.ThisPage.Notification.Show("转码已成功完成", 5000);
                                       ShowTranscodeCompleteNotification(Para.Item1, Para.Item2);
                                       break;
                                   case "Cancel":
                                       TabViewContainer.ThisPage.Notification.Show("转码任务被取消", 5000);
                                       ShowTranscodeCancelNotification();
                                       break;
                                   case "NotSupport":
                                       TabViewContainer.ThisPage.Notification.Show("转码格式不支持", 5000);
                                       break;
                                   default:
                                       TabViewContainer.ThisPage.Notification.Show("转码失败:" + ExcuteStatus, 5000);
                                       break;
                               }
                           }
                           else
                           {
                               switch (ExcuteStatus)
                               {
                                   case "Success":
                                       TabViewContainer.ThisPage.Notification.Show("Transcoding has been successfully completed", 5000);
                                       ShowTranscodeCompleteNotification(Para.Item1, Para.Item2);
                                       break;
                                   case "Cancel":
                                       TabViewContainer.ThisPage.Notification.Show("Transcoding task is cancelled", 5000);
                                       ShowTranscodeCancelNotification();
                                       break;
                                   case "NotSupport":
                                       TabViewContainer.ThisPage.Notification.Show("Transcoding format is not supported", 5000);
                                       break;
                                   default:
                                       TabViewContainer.ThisPage.Notification.Show("Transcoding failed:" + ExcuteStatus, 5000);
                                       break;
                               }
                           }
                       }).AsTask().Wait();
                   }

                   IsAnyTransformTaskRunning = false;

               }, (SourceFile, DestinationFile), TaskScheduler.Current);
        }

        private static void SendUpdatableToastWithProgressForCropVideo(StorageFile SourceFile)
        {
            var content = new ToastContent()
            {
                Launch = "Transcode",
                Scenario = ToastScenario.Reminder,
                Visual = new ToastVisual()
                {
                    BindingGeneric = new ToastBindingGeneric()
                    {
                        Children =
                        {
                            new AdaptiveText()
                            {
                                Text = Globalization.Language==LanguageEnum.Chinese
                                ? ("正在裁剪:"+SourceFile.DisplayName)
                                : ("Cropping:"+SourceFile.DisplayName)
                            },

                            new AdaptiveProgressBar()
                            {
                                Title = Globalization.Language==LanguageEnum.Chinese?"正在处理...":"Processing",
                                Value = new BindableProgressBarValue("ProgressValue"),
                                ValueStringOverride = new BindableString("ProgressValueString"),
                                Status = new BindableString("ProgressStatus")
                            }
                        }
                    }
                }
            };

            var Toast = new ToastNotification(content.GetXml())
            {
                Tag = "CropVideoNotification",
                Data = new NotificationData()
            };
            Toast.Data.Values["ProgressValue"] = "0";
            Toast.Data.Values["ProgressValueString"] = "0%";
            Toast.Data.Values["ProgressStatus"] = Globalization.Language == LanguageEnum.Chinese
                ? "点击该提示以取消"
                : "Click the prompt to cancel";
            Toast.Data.SequenceNumber = 0;

            Toast.Activated += (s, e) =>
            {
                if (s.Tag == "CropVideoNotification")
                {
                    AVTranscodeCancellation.Cancel();
                }
            };

            ToastNotificationManager.CreateToastNotifier().Show(Toast);
        }

        private static void SendUpdatableToastWithProgressForMergeVideo()
        {
            var content = new ToastContent()
            {
                Launch = "Transcode",
                Scenario = ToastScenario.Reminder,
                Visual = new ToastVisual()
                {
                    BindingGeneric = new ToastBindingGeneric()
                    {
                        Children =
                        {
                            new AdaptiveText()
                            {
                                Text = Globalization.Language==LanguageEnum.Chinese
                                ? "正在合并视频文件..."
                                : "Merging the video..."
                            },

                            new AdaptiveProgressBar()
                            {
                                Title = Globalization.Language==LanguageEnum.Chinese?"正在处理...":"Processing",
                                Value = new BindableProgressBarValue("ProgressValue"),
                                ValueStringOverride = new BindableString("ProgressValueString"),
                                Status = new BindableString("ProgressStatus")
                            }
                        }
                    }
                }
            };

            var Toast = new ToastNotification(content.GetXml())
            {
                Tag = "MergeVideoNotification",
                Data = new NotificationData()
            };
            Toast.Data.Values["ProgressValue"] = "0";
            Toast.Data.Values["ProgressValueString"] = "0%";
            Toast.Data.Values["ProgressStatus"] = Globalization.Language == LanguageEnum.Chinese
                ? "点击该提示以取消"
                : "Click the prompt to cancel";
            Toast.Data.SequenceNumber = 0;

            Toast.Activated += (s, e) =>
            {
                if (s.Tag == "MergeVideoNotification")
                {
                    AVTranscodeCancellation.Cancel();
                }
            };

            ToastNotificationManager.CreateToastNotifier().Show(Toast);
        }

        private static void SendUpdatableToastWithProgressForTranscode(StorageFile SourceFile, StorageFile DestinationFile)
        {
            string Tag = "TranscodeNotification";

            var content = new ToastContent()
            {
                Launch = "Transcode",
                Scenario = ToastScenario.Reminder,
                Visual = new ToastVisual()
                {
                    BindingGeneric = new ToastBindingGeneric()
                    {
                        Children =
                        {
                            new AdaptiveText()
                            {
                                Text = Globalization.Language==LanguageEnum.Chinese
                                ? ("正在转换:"+SourceFile.DisplayName)
                                : ("Transcoding:"+SourceFile.DisplayName)
                            },

                            new AdaptiveProgressBar()
                            {
                                Title = SourceFile.FileType.Substring(1).ToUpper()+" ⋙⋙⋙⋙ "+DestinationFile.FileType.Substring(1).ToUpper(),
                                Value = new BindableProgressBarValue("ProgressValue"),
                                ValueStringOverride = new BindableString("ProgressValueString"),
                                Status = new BindableString("ProgressStatus")
                            }
                        }
                    }
                }
            };

            var Toast = new ToastNotification(content.GetXml())
            {
                Tag = Tag,
                Data = new NotificationData()
            };
            Toast.Data.Values["ProgressValue"] = "0";
            Toast.Data.Values["ProgressValueString"] = "0%";
            Toast.Data.Values["ProgressStatus"] = Globalization.Language == LanguageEnum.Chinese
                ? "点击该提示以取消"
                : "Click the prompt to cancel";
            Toast.Data.SequenceNumber = 0;

            Toast.Activated += (s, e) =>
            {
                if (s.Tag == "TranscodeNotification")
                {
                    AVTranscodeCancellation.Cancel();
                }
            };

            ToastNotificationManager.CreateToastNotifier().Show(Toast);
        }

        private static void ShowCropCompleteNotification()
        {
            ToastNotificationManager.History.Remove("CropVideoNotification");

            if (Globalization.Language == LanguageEnum.Chinese)
            {
                var Content = new ToastContent()
                {
                    Scenario = ToastScenario.Default,
                    Launch = "Transcode",
                    Visual = new ToastVisual()
                    {
                        BindingGeneric = new ToastBindingGeneric()
                        {
                            Children =
                            {
                                new AdaptiveText()
                                {
                                    Text = "裁剪已完成！"
                                },

                                new AdaptiveText()
                                {
                                    Text = "点击以消除提示"
                                }
                            }
                        }
                    },
                };
                ToastNotificationManager.CreateToastNotifier().Show(new ToastNotification(Content.GetXml()));
            }
            else
            {
                var Content = new ToastContent()
                {
                    Scenario = ToastScenario.Default,
                    Launch = "Transcode",
                    Visual = new ToastVisual()
                    {
                        BindingGeneric = new ToastBindingGeneric()
                        {
                            Children =
                            {
                                new AdaptiveText()
                                {
                                    Text = "Cropping has been completed！"
                                },

                                new AdaptiveText()
                                {
                                    Text = "Click to remove the prompt"
                                }
                            }
                        }
                    },
                };
                ToastNotificationManager.CreateToastNotifier().Show(new ToastNotification(Content.GetXml()));
            }
        }

        private static void ShowMergeCompleteNotification()
        {
            ToastNotificationManager.History.Remove("MergeVideoNotification");

            if (Globalization.Language == LanguageEnum.Chinese)
            {
                var Content = new ToastContent()
                {
                    Scenario = ToastScenario.Default,
                    Launch = "Transcode",
                    Visual = new ToastVisual()
                    {
                        BindingGeneric = new ToastBindingGeneric()
                        {
                            Children =
                            {
                                new AdaptiveText()
                                {
                                    Text = "合并已完成！"
                                },

                                new AdaptiveText()
                                {
                                    Text = "点击以消除提示"
                                }
                            }
                        }
                    },
                };
                ToastNotificationManager.CreateToastNotifier().Show(new ToastNotification(Content.GetXml()));
            }
            else
            {
                var Content = new ToastContent()
                {
                    Scenario = ToastScenario.Default,
                    Launch = "Transcode",
                    Visual = new ToastVisual()
                    {
                        BindingGeneric = new ToastBindingGeneric()
                        {
                            Children =
                            {
                                new AdaptiveText()
                                {
                                    Text = "Merging has been completed！"
                                },

                                new AdaptiveText()
                                {
                                    Text = "Click to remove the prompt"
                                }
                            }
                        }
                    },
                };
                ToastNotificationManager.CreateToastNotifier().Show(new ToastNotification(Content.GetXml()));
            }
        }

        private static void ShowTranscodeCompleteNotification(StorageFile SourceFile, StorageFile DestinationFile)
        {
            ToastNotificationManager.History.Remove("TranscodeNotification");

            if (Globalization.Language == LanguageEnum.Chinese)
            {
                var Content = new ToastContent()
                {
                    Scenario = ToastScenario.Default,
                    Launch = "Transcode",
                    Visual = new ToastVisual()
                    {
                        BindingGeneric = new ToastBindingGeneric()
                        {
                            Children =
                            {
                                new AdaptiveText()
                                {
                                    Text = "转换已完成！"
                                },

                                new AdaptiveText()
                                {
                                   Text = SourceFile.Name + " 已成功转换为 " + DestinationFile.Name
                                },

                                new AdaptiveText()
                                {
                                    Text = "点击以消除提示"
                                }
                            }
                        }
                    },
                };
                ToastNotificationManager.CreateToastNotifier().Show(new ToastNotification(Content.GetXml()));
            }
            else
            {
                var Content = new ToastContent()
                {
                    Scenario = ToastScenario.Default,
                    Launch = "Transcode",
                    Visual = new ToastVisual()
                    {
                        BindingGeneric = new ToastBindingGeneric()
                        {
                            Children =
                            {
                                new AdaptiveText()
                                {
                                    Text = "Transcoding has been completed！"
                                },

                                new AdaptiveText()
                                {
                                   Text = SourceFile.Name + " has been successfully transcoded to " + DestinationFile.Name
                                },

                                new AdaptiveText()
                                {
                                    Text = "Click to remove the prompt"
                                }
                            }
                        }
                    },
                };
                ToastNotificationManager.CreateToastNotifier().Show(new ToastNotification(Content.GetXml()));
            }
        }

        private static void ShowCropCancelNotification()
        {
            ToastNotificationManager.History.Remove("CropVideoNotification");

            if (Globalization.Language == LanguageEnum.Chinese)
            {
                var Content = new ToastContent()
                {
                    Scenario = ToastScenario.Default,
                    Launch = "Transcode",
                    Visual = new ToastVisual()
                    {
                        BindingGeneric = new ToastBindingGeneric()
                        {
                            Children =
                            {
                                new AdaptiveText()
                                {
                                    Text = "裁剪任务已被取消"
                                },

                                new AdaptiveText()
                                {
                                   Text = "您可以尝试重新启动此任务"
                                },

                                new AdaptiveText()
                                {
                                    Text = "点击以消除提示"
                                }
                            }
                        }
                    }
                };
                ToastNotificationManager.CreateToastNotifier().Show(new ToastNotification(Content.GetXml()));
            }
            else
            {
                var Content = new ToastContent()
                {
                    Scenario = ToastScenario.Default,
                    Launch = "Transcode",
                    Visual = new ToastVisual()
                    {
                        BindingGeneric = new ToastBindingGeneric()
                        {
                            Children =
                            {
                                new AdaptiveText()
                                {
                                    Text = "Cropping task has been cancelled"
                                },

                                new AdaptiveText()
                                {
                                   Text = "You can try restarting the task"
                                },

                                new AdaptiveText()
                                {
                                    Text = "Click to remove the prompt"
                                }
                            }
                        }
                    }
                };
                ToastNotificationManager.CreateToastNotifier().Show(new ToastNotification(Content.GetXml()));
            }
        }

        private static void ShowMergeCancelNotification()
        {
            ToastNotificationManager.History.Remove("MergeVideoNotification");

            if (Globalization.Language == LanguageEnum.Chinese)
            {
                var Content = new ToastContent()
                {
                    Scenario = ToastScenario.Default,
                    Launch = "Transcode",
                    Visual = new ToastVisual()
                    {
                        BindingGeneric = new ToastBindingGeneric()
                        {
                            Children =
                            {
                                new AdaptiveText()
                                {
                                    Text = "合并任务已被取消"
                                },

                                new AdaptiveText()
                                {
                                   Text = "您可以尝试重新启动此任务"
                                },

                                new AdaptiveText()
                                {
                                    Text = "点击以消除提示"
                                }
                            }
                        }
                    }
                };
                ToastNotificationManager.CreateToastNotifier().Show(new ToastNotification(Content.GetXml()));
            }
            else
            {
                var Content = new ToastContent()
                {
                    Scenario = ToastScenario.Default,
                    Launch = "Transcode",
                    Visual = new ToastVisual()
                    {
                        BindingGeneric = new ToastBindingGeneric()
                        {
                            Children =
                            {
                                new AdaptiveText()
                                {
                                    Text = "Merging task has been cancelled"
                                },

                                new AdaptiveText()
                                {
                                   Text = "You can try restarting the task"
                                },

                                new AdaptiveText()
                                {
                                    Text = "Click to remove the prompt"
                                }
                            }
                        }
                    }
                };
                ToastNotificationManager.CreateToastNotifier().Show(new ToastNotification(Content.GetXml()));
            }
        }

        private static void ShowTranscodeCancelNotification()
        {
            ToastNotificationManager.History.Remove("TranscodeNotification");

            if (Globalization.Language == LanguageEnum.Chinese)
            {
                var Content = new ToastContent()
                {
                    Scenario = ToastScenario.Default,
                    Launch = "Transcode",
                    Visual = new ToastVisual()
                    {
                        BindingGeneric = new ToastBindingGeneric()
                        {
                            Children =
                            {
                                new AdaptiveText()
                                {
                                    Text = "格式转换已被取消"
                                },

                                new AdaptiveText()
                                {
                                   Text = "您可以尝试重新启动转换"
                                },

                                new AdaptiveText()
                                {
                                    Text = "点击以消除提示"
                                }
                            }
                        }
                    }
                };
                ToastNotificationManager.CreateToastNotifier().Show(new ToastNotification(Content.GetXml()));
            }
            else
            {
                var Content = new ToastContent()
                {
                    Scenario = ToastScenario.Default,
                    Launch = "Transcode",
                    Visual = new ToastVisual()
                    {
                        BindingGeneric = new ToastBindingGeneric()
                        {
                            Children =
                            {
                                new AdaptiveText()
                                {
                                    Text = "Transcode has been cancelled"
                                },

                                new AdaptiveText()
                                {
                                   Text = "You can try restarting the transcode"
                                },

                                new AdaptiveText()
                                {
                                    Text = "Click to remove the prompt"
                                }
                            }
                        }
                    }
                };
                ToastNotificationManager.CreateToastNotifier().Show(new ToastNotification(Content.GetXml()));
            }
        }
    }

    #endregion

    #region 时间和反转布尔值转换器
    public sealed class InverseBooleanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is bool v)
            {
                if (targetType == typeof(Visibility))
                {
                    return v ? Visibility.Collapsed : Visibility.Visible;
                }
                else if (targetType == typeof(bool))
                {
                    return !v;
                }
                else
                {
                    return null;
                }
            }
            else
            {
                return null;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            if (value is bool v)
            {
                if (targetType == typeof(Visibility))
                {
                    return v ? Visibility.Collapsed : Visibility.Visible;
                }
                else if (targetType == typeof(bool))
                {
                    return !v;
                }
                else
                {
                    return null;
                }
            }
            else
            {
                return null;
            }
        }
    }

    public sealed class TimespanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            if (value is double)
            {
                long Millisecond = System.Convert.ToInt64(value);
                int Hour = 0;
                int Minute = 0;
                int Second = 0;

                if (Millisecond >= 1000)
                {
                    Second = System.Convert.ToInt32(Millisecond / 1000);
                    Millisecond %= 1000;
                    if (Second >= 60)
                    {
                        Minute = Second / 60;
                        Second %= 60;
                        if (Minute >= 60)
                        {
                            Hour = Minute / 60;
                            Minute %= 60;
                        }
                    }
                }
                return string.Format("{0:D2}:{1:D2}:{2:D2}.{3:D3}", Hour, Minute, Second, Millisecond);
            }
            else
            {
                return null;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            throw new NotImplementedException();
        }
    }
    #endregion

    #region 主题转换器
    /// <summary>
    /// 提供对字体颜色的切换功能
    /// </summary>
    public sealed class AppThemeController : INotifyPropertyChanged
    {
        /// <summary>
        /// 指示当前应用的主题色
        /// </summary>
        public ElementTheme Theme { get; private set; }

        public event PropertyChangedEventHandler PropertyChanged;

        private static AppThemeController Instance;

        public static AppThemeController Current
        {
            get
            {
                lock (SyncRootProvider.SyncRoot)
                {
                    return Instance ?? (Instance = new AppThemeController());
                }
            }
        }

        /// <summary>
        /// 使用此方法切换主题色
        /// </summary>
        /// <param name="Theme"></param>
        public void ChangeThemeTo(ElementTheme Theme)
        {
            this.Theme = Theme;
            ApplicationData.Current.LocalSettings.Values["AppFontColorMode"] = Enum.GetName(typeof(ElementTheme), Theme);
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Theme)));
        }

        /// <summary>
        /// 初始化AppThemeController对象
        /// </summary>
        public AppThemeController()
        {
            if (ApplicationData.Current.LocalSettings.Values["AppFontColorMode"] is string Mode)
            {
                Theme = (ElementTheme)Enum.Parse(typeof(ElementTheme), Mode);
            }
            else
            {
                Theme = ElementTheme.Dark;
                ApplicationData.Current.LocalSettings.Values["AppFontColorMode"] = "Dark";
            }
        }
    }
    #endregion

    #region 错误追踪器
    /// <summary>
    /// 提供对错误的捕获和记录，以及蓝屏的导向
    /// </summary>
    public static class ExceptionTracer
    {
        private static AutoResetEvent Locker = new AutoResetEvent(true);

        /// <summary>
        /// 请求进入蓝屏状态
        /// </summary>
        /// <param name="Ex">错误内容</param>
        public static void RequestBlueScreen(Exception Ex)
        {
            if (Ex == null)
            {
                throw new ArgumentNullException(nameof(Ex), "Exception could not be null");
            }

            if (!(Window.Current.Content is Frame rootFrame))
            {
                rootFrame = new Frame();

                Window.Current.Content = rootFrame;
            }

            if (Globalization.Language == LanguageEnum.Chinese)
            {
                string Message =
                "\r\r以下是错误信息：\r\rException Code错误代码：" + Ex.HResult +
                "\r\rMessage错误消息：" + Ex.Message +
                "\r\rSource来源：" + (string.IsNullOrEmpty(Ex.Source) ? "Unknown" : Ex.Source) +
                "\r\rStackTrace堆栈追踪：\r" + (string.IsNullOrEmpty(Ex.StackTrace) ? "Unknown" : Ex.StackTrace);

                rootFrame.Navigate(typeof(BlueScreen), Message);
            }
            else
            {
                string Message =
                "\r\rThe following is the error message：\r\rException Code：" + Ex.HResult +
                "\r\rMessage：" + Ex.Message +
                "\r\rSource：" + (string.IsNullOrEmpty(Ex.Source) ? "Unknown" : Ex.Source) +
                "\r\rStackTrace：\r" + (string.IsNullOrEmpty(Ex.StackTrace) ? "Unknown" : Ex.StackTrace);

                rootFrame.Navigate(typeof(BlueScreen), Message);
            }
        }

        /// <summary>
        /// 记录错误
        /// </summary>
        /// <param name="Ex">错误</param>
        /// <returns></returns>
        public static async Task LogAsync(Exception Ex)
        {
            if (Ex == null)
            {
                throw new ArgumentNullException(nameof(Ex), "Exception could not be null");
            }

            await LogAsync(Ex.Message + Environment.NewLine + Ex.StackTrace).ConfigureAwait(false);
        }

        /// <summary>
        /// 记录错误
        /// </summary>
        /// <param name="Message">错误消息</param>
        /// <returns></returns>
        public static async Task LogAsync(string Message)
        {
            await Task.Run(() =>
            {
                Locker.WaitOne();
            }).ConfigureAwait(false);

            StorageFile TempFile = await ApplicationData.Current.LocalCacheFolder.CreateFileAsync("RX_Error_Message.txt", CreationCollisionOption.OpenIfExists);
            await FileIO.AppendTextAsync(TempFile, Message + Environment.NewLine);

            Locker.Set();
        }
    }
    #endregion

    #region 选择应用程序项目
    /// <summary>
    /// 提供对显示应用项目的支持
    /// </summary>
    public sealed class ProgramPickerItem
    {
        /// <summary>
        /// 应用缩略图
        /// </summary>
        public BitmapImage Thumbnuil { get; private set; }

        /// <summary>
        /// 应用描述
        /// </summary>
        public string Description { get; private set; }

        /// <summary>
        /// 应用名称
        /// </summary>
        public string Name { get; private set; }

        /// <summary>
        /// 应用包名称
        /// </summary>
        public string PackageName { get; private set; }

        /// <summary>
        /// 应用可执行程序路径
        /// </summary>
        public string Path { get; private set; }

        /// <summary>
        /// 是否是用户自定义应用
        /// </summary>
        public bool IsCustomApp { get; private set; } = false;

        /// <summary>
        /// 初始化ProgramPickerItem实例
        /// </summary>
        /// <param name="Thumbnuil">应用缩略图</param>
        /// <param name="Name">应用名称</param>
        /// <param name="Description">应用描述</param>
        /// <param name="PackageName">应用包名称</param>
        /// <param name="Path">应用可执行文件路径</param>
        public ProgramPickerItem(BitmapImage Thumbnuil, string Name, string Description, string PackageName = null, string Path = null)
        {
            this.Thumbnuil = Thumbnuil;
            this.Name = Name;
            this.Description = Description;
            if (!string.IsNullOrEmpty(PackageName))
            {
                this.PackageName = PackageName;
            }
            else if (!string.IsNullOrEmpty(Path))
            {
                this.Path = Path;
                IsCustomApp = true;
            }
        }
    }
    #endregion

    #region 完全信任执行过程控制器
    /// <summary>
    /// 用于启动具备完全权限的附加程序的控制器
    /// </summary>
    public sealed class FullTrustExcutorController
    {
        /// <summary>
        /// 启动指定路径的程序
        /// </summary>
        /// <param name="Path">程序路径</param>
        /// <returns></returns>
        public static async Task Run(string Path)
        {
            ApplicationData.Current.LocalSettings.Values["ExcutePath"] = Path;
            ApplicationData.Current.LocalSettings.Values["ExcuteParameter"] = string.Empty;
            ApplicationData.Current.LocalSettings.Values["ExcuteAuthority"] = "Normal";
            await FullTrustProcessLauncher.LaunchFullTrustProcessForCurrentAppAsync();
        }

        /// <summary>
        /// 启动指定路径的程序，并传递指定的参数
        /// </summary>
        /// <param name="Path">程序路径</param>
        /// <param name="Parameter">传递的参数</param>
        /// <returns></returns>
        public static async Task Run(string Path, string Parameter)
        {
            ApplicationData.Current.LocalSettings.Values["ExcutePath"] = Path;
            ApplicationData.Current.LocalSettings.Values["ExcuteParameter"] = Parameter;
            ApplicationData.Current.LocalSettings.Values["ExcuteAuthority"] = "Normal";
            await FullTrustProcessLauncher.LaunchFullTrustProcessForCurrentAppAsync();
        }

        /// <summary>
        /// 使用管理员权限启动指定路径的程序
        /// </summary>
        /// <param name="Path">程序路径</param>
        /// <returns></returns>
        public static async Task RunAsAdministrator(string Path)
        {
            ApplicationData.Current.LocalSettings.Values["ExcutePath"] = Path;
            ApplicationData.Current.LocalSettings.Values["ExcuteParameter"] = string.Empty;
            ApplicationData.Current.LocalSettings.Values["ExcuteAuthority"] = "Administrator";
            await FullTrustProcessLauncher.LaunchFullTrustProcessForCurrentAppAsync();
        }

        /// <summary>
        /// 使用管理员权限启动指定路径的程序，并传递指定的参数
        /// </summary>
        /// <param name="Path">程序路径</param>
        /// <param name="Parameter">传递的参数</param>
        /// <returns></returns>
        public static async Task RunAsAdministrator(string Path, string Parameter)
        {
            ApplicationData.Current.LocalSettings.Values["ExcutePath"] = Path;
            ApplicationData.Current.LocalSettings.Values["ExcuteParameter"] = Parameter;
            ApplicationData.Current.LocalSettings.Values["ExcuteAuthority"] = "Administrator";
            await FullTrustProcessLauncher.LaunchFullTrustProcessForCurrentAppAsync();
        }
    }
    #endregion

    #region 地址栏分块对象
    public sealed class AddressBlock
    {
        public string Name { get; private set; }

        public override string ToString()
        {
            return Name;
        }

        public AddressBlock(string Name)
        {
            this.Name = Name;
        }
    }
    #endregion
}
