using Microsoft.Data.Sqlite;
using SQLConnectionPoolProvider;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI.Xaml.Media.Imaging;

namespace RX_Explorer.Class
{
    /// <summary>
    /// 提供对SQLite数据库的访问支持
    /// </summary>
    public sealed class SQLite : IDisposable
    {
        private bool IsDisposed = false;
        private static readonly object Locker = new object();
        private volatile static SQLite SQL;
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
                lock (Locker)
                {
                    return SQL ??= new SQLite();
                }
            }
        }

        /// <summary>
        /// 初始化数据库预先导入的数据
        /// </summary>
        private void InitializeDatabase()
        {
            string Command = $@"Create Table If Not Exists SearchHistory (SearchText Text Not Null, Primary Key (SearchText));
                                Create Table If Not Exists WebFavourite (Subject Text Not Null, WebSite Text Not Null, Primary Key (WebSite));
                                Create Table If Not Exists WebHistory (Subject Text Not Null, WebSite Text Not Null, DateTime Text Not Null, Primary Key (Subject, WebSite, DateTime));
                                Create Table If Not Exists DownloadHistory (UniqueID Text Not Null, ActualName Text Not Null, Uri Text Not Null, State Text Not Null, Primary Key(UniqueID));
                                Create Table If Not Exists QuickStart (Name Text Not Null, FullPath Text Not Null, Protocal Text Not Null, Type Text Not Null, Primary Key (Name,FullPath,Protocal,Type));
                                Create Table If Not Exists Library (Path Text Not Null, Type Text Not Null, Primary Key (Path));
                                Create Table If Not Exists PathHistory (Path Text Not Null, Primary Key (Path));
                                Create Table If Not Exists BackgroundPicture (FileName Text Not Null, Primary Key (FileName));
                                Create Table If Not Exists DeviceVisibility (Path Text Not Null, IsVisible Text Not Null, Primary Key(Path));
                                Create Table If Not Exists ProgramPicker (FileType Text Not Null, Path Text Not Null, Primary Key(FileType,Path));
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
                                Insert Or Ignore Into BackgroundPicture Values('ms-appx:///CustomImage/Picture15.jpg');
                                Insert Or Ignore Into ProgramPicker Values ('.*','{Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32\\notepad.exe")}');";
            using (SQLConnection Connection = ConnectionPool.GetConnectionFromDataBasePoolAsync().Result)
            using (SqliteCommand CreateTable = Connection.CreateDbCommandFromConnection<SqliteCommand>(Command))
            {
                _ = CreateTable.ExecuteNonQuery();
            }
        }

        public async Task SetProgramPickerRecordAsync(string FileType, string Path)
        {
            using (SQLConnection Connection = await ConnectionPool.GetConnectionFromDataBasePoolAsync().ConfigureAwait(false))
            using (SqliteCommand Command = Connection.CreateDbCommandFromConnection<SqliteCommand>("Insert Or Ignore Into ProgramPicker Values (@FileType,@Path)"))
            {
                _ = Command.Parameters.AddWithValue("@FileType", FileType);
                _ = Command.Parameters.AddWithValue("@Path", Path);
                _ = await Command.ExecuteNonQueryAsync().ConfigureAwait(false);
            }
        }

        public async Task<List<string>> GetProgramPickerRecordAsync(string FileType)
        {
            List<string> Result = new List<string>();

            using (SQLConnection Connection = await ConnectionPool.GetConnectionFromDataBasePoolAsync().ConfigureAwait(false))
            using (SqliteCommand Command = Connection.CreateDbCommandFromConnection<SqliteCommand>("Select * From ProgramPicker Where FileType = @FileType Or FileType = '.*'"))
            {
                _ = Command.Parameters.AddWithValue("@FileType", FileType);

                using (SqliteDataReader Reader = await Command.ExecuteReaderAsync().ConfigureAwait(false))
                {
                    while (Reader.Read())
                    {
                        Result.Add(Reader[1].ToString());
                    }
                }
            }

            Result.Reverse();

            return Result;
        }

        public async Task DeleteProgramPickerRecordAsync(string FileType, string Path)
        {
            using (SQLConnection Connection = await ConnectionPool.GetConnectionFromDataBasePoolAsync().ConfigureAwait(false))
            using (SqliteCommand Command = Connection.CreateDbCommandFromConnection<SqliteCommand>("Delete From ProgramPicker Where FileType = @FileType And Path = @Path"))
            {
                _ = Command.Parameters.AddWithValue("@FileType", FileType);
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
        public async Task<List<string>> GetLibraryPathAsync()
        {
            List<string> list = new List<string>();
            using (SQLConnection Connection = await ConnectionPool.GetConnectionFromDataBasePoolAsync().ConfigureAwait(false))
            using (SqliteCommand Command = Connection.CreateDbCommandFromConnection<SqliteCommand>("Select * From Library"))
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
        public async Task DeleteLibraryAsync(string Path)
        {
            using (SQLConnection Connection = await ConnectionPool.GetConnectionFromDataBasePoolAsync().ConfigureAwait(false))
            using (SqliteCommand Command = Connection.CreateDbCommandFromConnection<SqliteCommand>("Delete From Library Where Path = @Path"))
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
        /// 保存在文件夹和库区域显示的文件夹路径
        /// </summary>
        /// <param name="Path">文件夹路径</param>
        /// <returns></returns>
        public async Task SetLibraryPathAsync(string Path, LibraryType Type)
        {
            using (SQLConnection Connection = await ConnectionPool.GetConnectionFromDataBasePoolAsync().ConfigureAwait(false))
            using (SqliteCommand Command = Connection.CreateDbCommandFromConnection<SqliteCommand>("Insert Or Ignore Into Library Values (@Path,@Type)"))
            {
                _ = Command.Parameters.AddWithValue("@Path", Path);
                _ = Command.Parameters.AddWithValue("@Type", Enum.GetName(typeof(LibraryType), Type));
                _ = await Command.ExecuteNonQueryAsync().ConfigureAwait(false);
            }
        }

        public async Task UpdateLibraryAsync(string NewPath, LibraryType Type)
        {
            using (SQLConnection Connection = await ConnectionPool.GetConnectionFromDataBasePoolAsync().ConfigureAwait(false))
            using (SqliteCommand Command = Connection.CreateDbCommandFromConnection<SqliteCommand>("Update Library Set Path=@NewPath Where Type=@Type"))
            {
                _ = Command.Parameters.AddWithValue("@NewPath", NewPath);
                _ = Command.Parameters.AddWithValue("@Type", Enum.GetName(typeof(LibraryType), Type));
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
                using (SqliteCommand Command = Connection.CreateDbCommandFromConnection<SqliteCommand>("Select Count(*) From QuickStart Where Name=@OldName"))
                {
                    _ = Command.Parameters.AddWithValue("@OldName", OldName);

                    if (Convert.ToInt32(await Command.ExecuteScalarAsync().ConfigureAwait(false)) == 0)
                    {
                        return;
                    }
                }

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

        public async Task UpdateQuickStartItemAsync(string FullPath, string NewName, QuickStartType Type)
        {
            using (SQLConnection Connection = await ConnectionPool.GetConnectionFromDataBasePoolAsync().ConfigureAwait(false))
            {
                using (SqliteCommand Command = Connection.CreateDbCommandFromConnection<SqliteCommand>("Select Count(*) From QuickStart Where FullPath=@FullPath"))
                {
                    _ = Command.Parameters.AddWithValue("@FullPath", FullPath);

                    if (Convert.ToInt32(await Command.ExecuteScalarAsync().ConfigureAwait(false)) == 0)
                    {
                        return;
                    }
                }

                using (SqliteCommand Command = Connection.CreateDbCommandFromConnection<SqliteCommand>("Update QuickStart Set Name=@NewName Where FullPath=@FullPath And Type=@Type"))
                {
                    _ = Command.Parameters.AddWithValue("@FullPath", FullPath);
                    _ = Command.Parameters.AddWithValue("@NewName", NewName);
                    _ = Command.Parameters.AddWithValue("@Type", Enum.GetName(typeof(QuickStartType), Type));
                    _ = await Command.ExecuteNonQueryAsync().ConfigureAwait(false);
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

        public async Task<List<string>> GetQuickStartAsync()
        {
            List<string> result = new List<string>();
            using (SQLConnection Connection = await ConnectionPool.GetConnectionFromDataBasePoolAsync().ConfigureAwait(false))
            using (SqliteCommand Command = Connection.CreateDbCommandFromConnection<SqliteCommand>("Select * From QuickStart"))
            using (SqliteDataReader Reader = await Command.ExecuteReaderAsync().ConfigureAwait(true))
            {
                while (Reader.Read())
                {
                    result.Add(Reader[0].ToString());
                }
            }
            return result;
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
                                StorageFile BitmapFile = await StorageFile.GetFileFromApplicationUriAsync(new Uri(Reader[1].ToString()));

                                BitmapImage Bitmap = new BitmapImage()
                                {
                                    DecodePixelHeight = 100,
                                    DecodePixelWidth = 100
                                };

                                using (IRandomAccessStream Stream = await BitmapFile.OpenAsync(FileAccessMode.Read))
                                {
                                    await Bitmap.SetSourceAsync(Stream);
                                }

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
}
