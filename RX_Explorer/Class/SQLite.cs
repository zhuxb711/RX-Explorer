using Microsoft.Data.Sqlite;
using ShareClassLibrary;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
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
        private bool IsDisposed;
        private static readonly object Locker = new object();
        private static volatile SQLite SQL;
        private SqliteConnection Connection;

        /// <summary>
        /// 初始化SQLite的实例
        /// </summary>
        private SQLite()
        {
            SQLitePCL.Batteries_V2.Init();
            SQLitePCL.raw.sqlite3_win32_set_directory(1, ApplicationData.Current.LocalFolder.Path);
            SQLitePCL.raw.sqlite3_win32_set_directory(2, ApplicationData.Current.TemporaryFolder.Path);

            Connection = new SqliteConnection("Filename=RX_Sqlite.db;");
            Connection.Open();

            if (!ApplicationData.Current.LocalSettings.Values.ContainsKey("DatabaseInit"))
            {
                InitializeDatabase();
            }
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
            string Command = "Create Table If Not Exists SearchHistory (SearchText Text Not Null, Primary Key (SearchText));"
                           + "Create Table If Not Exists QuickStart (Name Text Not Null, FullPath Text Not Null Collate NoCase, Protocal Text Not Null, Type Text Not Null, Primary Key (Name,FullPath,Protocal,Type));"
                           + "Create Table If Not Exists Library (Path Text Not Null Collate NoCase, Type Text Not Null, Primary Key (Path));"
                           + "Create Table If Not Exists PathHistory (Path Text Not Null Collate NoCase, Primary Key (Path));"
                           + "Create Table If Not Exists BackgroundPicture (FileName Text Not Null, Primary Key (FileName));"
                           + "Create Table If Not Exists ProgramPicker (FileType Text Not Null, Path Text Not Null Collate NoCase, IsDefault Text Default 'False' Check(IsDefault In ('True','False')), IsRecommanded Text Default 'False' Check(IsRecommanded In ('True','False')), Primary Key(FileType, Path));"
                           + "Create Table If Not Exists TerminalProfile (Name Text Not Null, Path Text Not Null Collate NoCase, Argument Text Not Null, RunAsAdmin Text Not Null, Primary Key(Name));"
                           + "Create Table If Not Exists PathConfiguration (Path Text Not Null Collate NoCase, DisplayMode Integer Default 1 Check(DisplayMode In (0,1,2,3,4,5)), SortColumn Text Default 'Name' Check(SortColumn In ('Name','ModifiedTime','Type','Size')), SortDirection Text Default 'Ascending' Check(SortDirection In ('Ascending','Descending')), Primary Key(Path));"
                           + "Insert Or Ignore Into BackgroundPicture Values('ms-appx:///CustomImage/Picture1.jpg');"
                           + "Insert Or Ignore Into BackgroundPicture Values('ms-appx:///CustomImage/Picture2.jpg');"
                           + "Insert Or Ignore Into BackgroundPicture Values('ms-appx:///CustomImage/Picture3.jpg');"
                           + "Insert Or Ignore Into BackgroundPicture Values('ms-appx:///CustomImage/Picture4.jpg');"
                           + "Insert Or Ignore Into BackgroundPicture Values('ms-appx:///CustomImage/Picture5.jpg');"
                           + "Insert Or Ignore Into BackgroundPicture Values('ms-appx:///CustomImage/Picture6.jpg');"
                           + "Insert Or Ignore Into BackgroundPicture Values('ms-appx:///CustomImage/Picture7.jpg');"
                           + "Insert Or Ignore Into BackgroundPicture Values('ms-appx:///CustomImage/Picture8.jpg');"
                           + "Insert Or Ignore Into BackgroundPicture Values('ms-appx:///CustomImage/Picture9.jpg');"
                           + "Insert Or Ignore Into BackgroundPicture Values('ms-appx:///CustomImage/Picture10.jpg');"
                           + "Insert Or Ignore Into BackgroundPicture Values('ms-appx:///CustomImage/Picture11.jpg');"
                           + "Insert Or Ignore Into BackgroundPicture Values('ms-appx:///CustomImage/Picture12.jpg');"
                           + "Insert Or Ignore Into BackgroundPicture Values('ms-appx:///CustomImage/Picture13.jpg');"
                           + "Insert Or Ignore Into BackgroundPicture Values('ms-appx:///CustomImage/Picture14.jpg');"
                           + "Insert Or Ignore Into BackgroundPicture Values('ms-appx:///CustomImage/Picture15.jpg');"
                           + $"Insert Or Ignore Into TerminalProfile Values ('Powershell', '{Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "WindowsPowerShell\\v1.0\\powershell.exe")}', '-NoExit -Command \"Set-Location ''[CurrentLocation]''\"', 'True');"
                           + $"Insert Or Ignore Into TerminalProfile Values ('CMD', '{Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe")}', '/k cd /d [CurrentLocation]', 'True');";

            using (SqliteCommand CreateTable = new SqliteCommand(Command, Connection))
            {
                CreateTable.ExecuteNonQuery();
            }

            ApplicationData.Current.LocalSettings.Values["DatabaseInit"] = true;
        }

        public async Task SetPathConfiguration(PathConfiguration Configuration)
        {
            bool ShouldCreateNew = true;

            using (SqliteCommand Command = new SqliteCommand("Select Count(*) From PathConfiguration Where Path = @Path", Connection))
            {
                Command.Parameters.AddWithValue("@Path", Configuration.Path);

                if (Convert.ToInt32(await Command.ExecuteScalarAsync().ConfigureAwait(false)) > 0)
                {
                    ShouldCreateNew = false;
                }
            }

            StringBuilder Builder = new StringBuilder();

            if (ShouldCreateNew)
            {
                Builder.Append($"Insert Into PathConfiguration (Path) Values ('{Configuration.Path}');");
            }

            if (Configuration.DisplayModeIndex.HasValue)
            {
                Builder.Append($"Update PathConfiguration Set DisplayMode = {Configuration.DisplayModeIndex.Value} Where Path = '{Configuration.Path}';");
            }

            if (Configuration.SortColumn.HasValue)
            {
                Builder.Append($"Update PathConfiguration Set SortColumn = '{Enum.GetName(typeof(SortTarget), Configuration.SortColumn)}' Where Path = '{Configuration.Path}';");
            }

            if (Configuration.SortDirection.HasValue)
            {
                Builder.Append($"Update PathConfiguration Set SortDirection = '{Enum.GetName(typeof(SortDirection), Configuration.SortDirection)}' Where Path = '{Configuration.Path}';");
            }

            if (!string.IsNullOrEmpty(Builder.ToString()))
            {
                using (SqliteCommand Command = new SqliteCommand(Builder.ToString(), Connection))
                {
                    await Command.ExecuteNonQueryAsync().ConfigureAwait(false);
                }
            }
        }

        public async Task<PathConfiguration> GetPathConfiguration(string Path)
        {
            using (SqliteCommand Command = new SqliteCommand("Select * From PathConfiguration Where Path = @Path", Connection))
            {
                Command.Parameters.AddWithValue("@Path", Path);

                using (SqliteDataReader Reader = await Command.ExecuteReaderAsync().ConfigureAwait(false))
                {
                    if (Reader.Read())
                    {
                        return new PathConfiguration(Path, Convert.ToInt32(Reader[1]), Enum.Parse<SortTarget>(Convert.ToString(Reader[2])), Enum.Parse<SortDirection>(Convert.ToString(Reader[3])));
                    }
                    else
                    {
                        return new PathConfiguration(Path, 1, SortTarget.Name, SortDirection.Ascending);
                    }
                }
            }
        }

        public async Task<List<TerminalProfile>> GetAllTerminalProfile()
        {
            List<TerminalProfile> Result = new List<TerminalProfile>();

            using (SqliteCommand Command = new SqliteCommand("Select * From TerminalProfile", Connection))
            using (SqliteDataReader Reader = await Command.ExecuteReaderAsync().ConfigureAwait(false))
            {
                while (Reader.Read())
                {
                    Result.Add(new TerminalProfile(Reader[0].ToString(), Reader[1].ToString(), Reader[2].ToString(), Convert.ToBoolean(Reader[3])));
                }
            }

            return Result;
        }

        public async Task<TerminalProfile> GetTerminalProfileByName(string Name)
        {
            using (SqliteCommand Command = new SqliteCommand("Select * From TerminalProfile Where Name = @Name", Connection))
            {
                Command.Parameters.AddWithValue("@Name", Name);

                using (SqliteDataReader Reader = await Command.ExecuteReaderAsync().ConfigureAwait(false))
                {
                    if (Reader.Read())
                    {
                        return new TerminalProfile(Reader[0].ToString(), Reader[1].ToString(), Reader[2].ToString(), Convert.ToBoolean(Reader[3]));
                    }
                    else
                    {
                        return null;
                    }
                }
            }
        }

        public async Task DeleteTerminalProfile(TerminalProfile Profile)
        {
            if (Profile == null)
            {
                throw new ArgumentNullException(nameof(Profile), "Argument could not be null");
            }

            using (SqliteCommand Command = new SqliteCommand("Delete From TerminalProfile Where Name = @Name", Connection))
            {
                Command.Parameters.AddWithValue("@Name", Profile.Name);
                await Command.ExecuteNonQueryAsync().ConfigureAwait(false);
            }
        }

        public async Task SetOrModifyTerminalProfile(TerminalProfile Profile)
        {
            if (Profile == null)
            {
                throw new ArgumentNullException(nameof(Profile), "Argument could not be null");
            }

            int Count = 0;

            using (SqliteCommand Command = new SqliteCommand("Select Count(*) From TerminalProfile Where Name = @Name", Connection))
            {
                Command.Parameters.AddWithValue("@Name", Profile.Name);
                Count = Convert.ToInt32(await Command.ExecuteScalarAsync().ConfigureAwait(false));
            }

            if (Count > 0)
            {
                using (SqliteCommand UpdateCommand = new SqliteCommand("Update TerminalProfile Set Path = @Path, Argument = @Argument, RunAsAdmin = @RunAsAdmin Where Name = @Name", Connection))
                {
                    UpdateCommand.Parameters.AddWithValue("@Name", Profile.Name);
                    UpdateCommand.Parameters.AddWithValue("@Path", Profile.Path);
                    UpdateCommand.Parameters.AddWithValue("@Argument", Profile.Argument);
                    UpdateCommand.Parameters.AddWithValue("@RunAsAdmin", Convert.ToString(Profile.RunAsAdmin));
                    await UpdateCommand.ExecuteNonQueryAsync().ConfigureAwait(false);
                }
            }
            else
            {
                using (SqliteCommand AddCommand = new SqliteCommand("Insert Into TerminalProfile Values (@Name,@Path,@Argument,@RunAsAdmin)", Connection))
                {
                    AddCommand.Parameters.AddWithValue("@Name", Profile.Name);
                    AddCommand.Parameters.AddWithValue("@Path", Profile.Path);
                    AddCommand.Parameters.AddWithValue("@Argument", Profile.Argument);
                    AddCommand.Parameters.AddWithValue("@RunAsAdmin", Convert.ToString(Profile.RunAsAdmin));
                    await AddCommand.ExecuteNonQueryAsync().ConfigureAwait(false);
                }
            }

        }

        public async Task SetProgramPickerRecordAsync(params AssociationPackage[] Packages)
        {
            StringBuilder AddPathBuilder = new StringBuilder();

            foreach (AssociationPackage Package in Packages)
            {
                AddPathBuilder.Append($"Insert Or Ignore Into ProgramPicker Values ('{Package.Extension}', '{Package.ExecutablePath}', 'False', '{Package.IsRecommanded}');");
            }

            using (SqliteCommand Command = new SqliteCommand(AddPathBuilder.ToString(), Connection))
            {
                await Command.ExecuteNonQueryAsync().ConfigureAwait(false);
            }
        }

        public async Task UpdateProgramPickerRecordAsync(string FileType, IEnumerable<AssociationPackage> AssociationList)
        {
            List<AssociationPackage> ExistedPackage = await GetProgramPickerRecordAsync(FileType, true).ConfigureAwait(false);

            StringBuilder DeletePathBuilder = new StringBuilder();
            foreach (AssociationPackage Package in ExistedPackage.Except(AssociationList))
            {
                DeletePathBuilder.Append($"Delete From ProgramPicker Where FileType = '{FileType}' And Path = '{Package.ExecutablePath}';");
            }

            StringBuilder AddPathBuilder = new StringBuilder();
            foreach (AssociationPackage Package in AssociationList.Except(ExistedPackage))
            {
                AddPathBuilder.Append($"Insert Or Ignore Into ProgramPicker Values ('{FileType}', '{Package.ExecutablePath}', 'False', '{Package.IsRecommanded}');");
            }

            string SQLQuery = DeletePathBuilder.ToString() + AddPathBuilder.ToString();

            if (!string.IsNullOrWhiteSpace(SQLQuery))
            {
                using (SqliteCommand Command = new SqliteCommand(SQLQuery, Connection))
                {
                    await Command.ExecuteNonQueryAsync().ConfigureAwait(false);
                }
            }
        }

        public async Task<string> GetDefaultProgramPickerRecordAsync(string Extension)
        {
            using (SqliteCommand Command = new SqliteCommand("Select Path From ProgramPicker Where FileType = @FileType And IsDefault = 'True'", Connection))
            {
                Command.Parameters.AddWithValue("@FileType", Extension);

                return Convert.ToString(await Command.ExecuteScalarAsync().ConfigureAwait(false));
            }
        }

        public async Task SetDefaultProgramPickerRecordAsync(string FileType, string Path)
        {
            using (SqliteCommand Command = new SqliteCommand("Update ProgramPicker Set IsDefault = 'False' Where FileType = @FileType", Connection))
            {
                Command.Parameters.AddWithValue("@FileType", FileType);
                await Command.ExecuteNonQueryAsync().ConfigureAwait(false);
            }

            using (SqliteCommand Command = new SqliteCommand("Update ProgramPicker Set IsDefault = 'True' Where FileType = @FileType And Path = @Path Collate NoCase", Connection))
            {
                Command.Parameters.AddWithValue("@FileType", FileType);
                Command.Parameters.AddWithValue("@Path", Path);
                await Command.ExecuteNonQueryAsync().ConfigureAwait(false);
            }
        }

        public async Task<List<AssociationPackage>> GetProgramPickerRecordAsync(string Extension, bool IncludeUWPApplication)
        {
            try
            {
                List<AssociationPackage> Result = new List<AssociationPackage>();

                using (SqliteCommand Command = new SqliteCommand("Select * From ProgramPicker Where FileType = @FileType", Connection))
                {
                    Command.Parameters.AddWithValue("@FileType", Extension);

                    using (SqliteDataReader Reader = await Command.ExecuteReaderAsync().ConfigureAwait(false))
                    {
                        while (Reader.Read())
                        {
                            //Reader.IsDBNull check is for the user who updated to v5.8.0 and v5.8.0 have DatabaseTable defect on 'ProgramPicker', maybe we could delete this check after several version
                            if (IncludeUWPApplication)
                            {
                                Result.Add(new AssociationPackage(Extension, Convert.ToString(Reader[1]), !Reader.IsDBNull(3) && Convert.ToBoolean(Reader[3])));
                            }
                            else
                            {
                                if (Path.IsPathRooted(Convert.ToString(Reader[1])))
                                {
                                    System.Diagnostics.Debug.WriteLine($"{Reader[0]}  {Reader[1]}  {Reader[2]}  {Reader[3]}");
                                    Result.Add(new AssociationPackage(Extension, Convert.ToString(Reader[1]), !Reader.IsDBNull(3) && Convert.ToBoolean(Reader[3])));
                                }
                            }
                        }
                    }
                }

                Result.Reverse();

                return Result;
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, "An exception was threw when reading association data from database");
                return new List<AssociationPackage>(0);
            }
        }

        public async Task DeleteProgramPickerRecordAsync(AssociationPackage Package)
        {
            using (SqliteCommand Command = new SqliteCommand("Delete From ProgramPicker Where FileType = @FileType And Path = @Path", Connection))
            {
                Command.Parameters.AddWithValue("@FileType", Package.Extension);
                Command.Parameters.AddWithValue("@Path", Package.ExecutablePath);
                await Command.ExecuteNonQueryAsync().ConfigureAwait(false);
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
                using (SqliteCommand Command = new SqliteCommand("Insert Into BackgroundPicture Values (@FileName)", Connection))
                {
                    Command.Parameters.AddWithValue("@FileName", uri.ToString());
                    await Command.ExecuteNonQueryAsync().ConfigureAwait(false);
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

            using (SqliteCommand Command = new SqliteCommand("Select * From BackgroundPicture", Connection))
            using (SqliteDataReader Query = await Command.ExecuteReaderAsync().ConfigureAwait(false))
            {
                while (Query.Read())
                {
                    list.Add(new Uri(Query[0].ToString()));
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
                using (SqliteCommand Command = new SqliteCommand("Delete From BackgroundPicture Where FileName=@FileName", Connection))
                {
                    Command.Parameters.AddWithValue("@FileName", uri.ToString());
                    await Command.ExecuteNonQueryAsync().ConfigureAwait(false);
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
        public async Task<List<(string, LibraryType)>> GetLibraryPathAsync()
        {
            List<(string, LibraryType)> list = new List<(string, LibraryType)>();

            using (SqliteCommand Command = new SqliteCommand("Select * From Library", Connection))
            using (SqliteDataReader Query = await Command.ExecuteReaderAsync().ConfigureAwait(false))
            {
                while (Query.Read())
                {
                    list.Add((Query[0].ToString(), Enum.Parse<LibraryType>(Query[1].ToString())));
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
            using (SqliteCommand Command = new SqliteCommand("Delete From Library Where Path = @Path", Connection))
            {
                Command.Parameters.AddWithValue("@Path", Path);
                await Command.ExecuteNonQueryAsync().ConfigureAwait(false);
            }
        }

        /// <summary>
        /// 保存文件路径栏的记录
        /// </summary>
        /// <param name="Path">输入的文件路径</param>
        /// <returns></returns>
        public async Task SetPathHistoryAsync(string Path)
        {
            using (SqliteCommand Command = new SqliteCommand("Delete From PathHistory Where Path = @Para", Connection))
            {
                Command.Parameters.AddWithValue("@Para", Path);
                await Command.ExecuteNonQueryAsync().ConfigureAwait(false);
            }

            using (SqliteCommand Command = new SqliteCommand("Insert Into PathHistory Values (@Para)", Connection))
            {
                Command.Parameters.AddWithValue("@Para", Path);
                await Command.ExecuteNonQueryAsync().ConfigureAwait(false);
            }
        }

        /// <summary>
        /// 模糊查询与文件路径栏相关的输入历史记录
        /// </summary>
        /// <param name="Target">输入内容</param>
        /// <returns></returns>
        public async Task<List<string>> GetRelatedPathHistoryAsync()
        {
            List<string> PathList = new List<string>(25);

            using (SqliteCommand Command = new SqliteCommand("Select * From PathHistory Order By rowid Desc Limit 0,25", Connection))
            using (SqliteDataReader Query = await Command.ExecuteReaderAsync().ConfigureAwait(false))
            {
                while (Query.Read())
                {
                    PathList.Add(Query[0].ToString());
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
            using (SqliteCommand Command = new SqliteCommand("Insert Or Ignore Into Library Values (@Path,@Type)", Connection))
            {
                Command.Parameters.AddWithValue("@Path", Path);
                Command.Parameters.AddWithValue("@Type", Enum.GetName(typeof(LibraryType), Type));
                await Command.ExecuteNonQueryAsync().ConfigureAwait(false);
            }
        }

        public async Task UpdateLibraryAsync(string NewPath, LibraryType Type)
        {
            using (SqliteCommand Command = new SqliteCommand("Update Library Set Path=@NewPath Where Type=@Type", Connection))
            {
                Command.Parameters.AddWithValue("@NewPath", NewPath);
                Command.Parameters.AddWithValue("@Type", Enum.GetName(typeof(LibraryType), Type));
                await Command.ExecuteNonQueryAsync().ConfigureAwait(false);
            }
        }

        /// <summary>
        /// 保存搜索历史记录
        /// </summary>
        /// <param name="SearchText">搜索内容</param>
        /// <returns></returns>
        public async Task SetSearchHistoryAsync(string SearchText)
        {
            using (SqliteCommand Command = new SqliteCommand("Insert Or Ignore Into SearchHistory Values (@Para)", Connection))
            {
                Command.Parameters.AddWithValue("@Para", SearchText);
                await Command.ExecuteNonQueryAsync().ConfigureAwait(false);
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
            using (SqliteCommand Command = new SqliteCommand("Insert Or Ignore Into QuickStart Values (@Name,@Path,@Protocal,@Type)", Connection))
            {
                Command.Parameters.AddWithValue("@Name", Name);
                Command.Parameters.AddWithValue("@Path", FullPath);
                Command.Parameters.AddWithValue("@Protocal", Protocal);
                Command.Parameters.AddWithValue("@Type", Enum.GetName(typeof(QuickStartType), Type));
                await Command.ExecuteNonQueryAsync().ConfigureAwait(false);
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
            using (SqliteCommand Command = new SqliteCommand("Select Count(*) From QuickStart Where Name=@OldName", Connection))
            {
                Command.Parameters.AddWithValue("@OldName", OldName);

                if (Convert.ToInt32(await Command.ExecuteScalarAsync().ConfigureAwait(false)) == 0)
                {
                    return;
                }
            }

            if (FullPath != null)
            {
                using (SqliteCommand Command = new SqliteCommand("Update QuickStart Set Name=@NewName, FullPath=@Path, Protocal=@Protocal Where Name=@OldName And Type=@Type", Connection))
                {
                    Command.Parameters.AddWithValue("@OldName", OldName);
                    Command.Parameters.AddWithValue("@Path", FullPath);
                    Command.Parameters.AddWithValue("@NewName", NewName);
                    Command.Parameters.AddWithValue("@Protocal", Protocal);
                    Command.Parameters.AddWithValue("@Type", Enum.GetName(typeof(QuickStartType), Type));
                    await Command.ExecuteNonQueryAsync().ConfigureAwait(false);
                }
            }
            else
            {
                using (SqliteCommand Command = new SqliteCommand("Update QuickStart Set Name=@NewName, Protocal=@Protocal Where Name=@OldName And Type=@Type", Connection))
                {
                    Command.Parameters.AddWithValue("@OldName", OldName);
                    Command.Parameters.AddWithValue("@NewName", NewName);
                    Command.Parameters.AddWithValue("@Protocal", Protocal);
                    Command.Parameters.AddWithValue("@Type", Enum.GetName(typeof(QuickStartType), Type));
                    await Command.ExecuteNonQueryAsync().ConfigureAwait(false);
                }
            }
        }

        public async Task UpdateQuickStartItemAsync(string FullPath, string NewName, QuickStartType Type)
        {
            using (SqliteCommand Command = new SqliteCommand("Select Count(*) From QuickStart Where FullPath=@FullPath", Connection))
            {
                Command.Parameters.AddWithValue("@FullPath", FullPath);

                if (Convert.ToInt32(await Command.ExecuteScalarAsync().ConfigureAwait(false)) == 0)
                {
                    return;
                }
            }

            using (SqliteCommand Command = new SqliteCommand("Update QuickStart Set Name=@NewName Where FullPath=@FullPath And Type=@Type", Connection))
            {
                Command.Parameters.AddWithValue("@FullPath", FullPath);
                Command.Parameters.AddWithValue("@NewName", NewName);
                Command.Parameters.AddWithValue("@Type", Enum.GetName(typeof(QuickStartType), Type));
                await Command.ExecuteNonQueryAsync().ConfigureAwait(false);
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
                using (SqliteCommand Command = new SqliteCommand("Delete From QuickStart Where Name = @Name And FullPath = @FullPath And Type=@Type", Connection))
                {
                    Command.Parameters.AddWithValue("@Name", Item.DisplayName);
                    Command.Parameters.AddWithValue("@FullPath", Item.RelativePath);
                    Command.Parameters.AddWithValue("@Type", Enum.GetName(typeof(QuickStartType), Item.Type));
                    await Command.ExecuteNonQueryAsync().ConfigureAwait(false);
                }
            }
            else
            {
                throw new ArgumentNullException(nameof(Item), "Parameter could not be null");
            }
        }

        public async Task DeleteQuickStartItemAsync(QuickStartType Type)
        {
            using (SqliteCommand Command = new SqliteCommand("Delete From QuickStart Where Type=@Type", Connection))
            {
                Command.Parameters.AddWithValue("@Type", Enum.GetName(typeof(QuickStartType), Type));
                await Command.ExecuteNonQueryAsync().ConfigureAwait(false);
            }
        }

        /// <summary>
        /// 获取所有快速启动项
        /// </summary>
        /// <returns></returns>
        public async Task<List<KeyValuePair<QuickStartType, QuickStartItem>>> GetQuickStartItemAsync()
        {
            List<Tuple<string, string, string>> ErrorList = new List<Tuple<string, string, string>>();
            List<KeyValuePair<QuickStartType, QuickStartItem>> Result = new List<KeyValuePair<QuickStartType, QuickStartItem>>();

            using (SqliteCommand Command = new SqliteCommand("Select * From QuickStart", Connection))
            using (SqliteDataReader Reader = await Command.ExecuteReaderAsync().ConfigureAwait(true))
            {
                while (Reader.Read())
                {
                    StorageFile ImageFile = null;

                    try
                    {
                        ImageFile = Convert.ToString(Reader[1]).StartsWith("ms-appx")
                                                ? await StorageFile.GetFileFromApplicationUriAsync(new Uri(Reader[1].ToString()))
                                                : await StorageFile.GetFileFromPathAsync(Path.Combine(ApplicationData.Current.LocalFolder.Path, Convert.ToString(Reader[1])));

                        BitmapImage Bitmap = new BitmapImage();

                        using (IRandomAccessStream Stream = await ImageFile.OpenAsync(FileAccessMode.Read))
                        {
                            await Bitmap.SetSourceAsync(Stream);
                        }

                        if (Enum.Parse<QuickStartType>(Reader[3].ToString()) == QuickStartType.Application)
                        {
                            Result.Add(new KeyValuePair<QuickStartType, QuickStartItem>(QuickStartType.Application, new QuickStartItem(Bitmap, Convert.ToString(Reader[2]), QuickStartType.Application, Reader[1].ToString(), Reader[0].ToString())));
                        }
                        else
                        {
                            Result.Add(new KeyValuePair<QuickStartType, QuickStartItem>(QuickStartType.WebSite, new QuickStartItem(Bitmap, Convert.ToString(Reader[2]), QuickStartType.WebSite, Reader[1].ToString(), Reader[0].ToString())));
                        }
                    }
                    catch (Exception ex)
                    {
                        LogTracer.Log(ex, $"Could not load QuickStart item, Name: {Reader[0]}");

                        ErrorList.Add(new Tuple<string, string, string>(Convert.ToString(Reader[0]), Convert.ToString(Reader[1]), Convert.ToString(Reader[3])));

                        if (ImageFile != null)
                        {
                            await ImageFile.DeleteAsync(StorageDeleteOption.PermanentDelete);
                        }
                    }
                }
            }

            foreach (Tuple<string, string, string> ErrorItem in ErrorList)
            {
                using (SqliteCommand Command = new SqliteCommand("Delete From QuickStart Where Name = @Name And FullPath = @FullPath And Type=@Type", Connection))
                {
                    Command.Parameters.AddWithValue("@Name", ErrorItem.Item1);
                    Command.Parameters.AddWithValue("@FullPath", ErrorItem.Item2);
                    Command.Parameters.AddWithValue("@Type", ErrorItem.Item3);
                    await Command.ExecuteNonQueryAsync().ConfigureAwait(true);
                }
            }

            return Result;
        }

        /// <summary>
        /// 获取与搜索内容有关的搜索历史
        /// </summary>
        /// <param name="Target">搜索内容</param>
        /// <returns></returns>
        public async Task<List<string>> GetRelatedSearchHistoryAsync(string Target)
        {
            List<string> HistoryList = new List<string>();

            using (SqliteCommand Command = new SqliteCommand("Select * From SearchHistory Where SearchText Like @Target Order By rowid Desc", Connection))
            {
                Command.Parameters.AddWithValue("@Target", $"%{Target}%");

                using (SqliteDataReader Query = await Command.ExecuteReaderAsync().ConfigureAwait(false))
                {
                    while (Query.Read())
                    {
                        HistoryList.Add(Query[0].ToString());
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
        public async Task ClearTableAsync(string TableName)
        {
            using (SqliteCommand Command = new SqliteCommand($"Delete From {TableName}", Connection))
            {
                await Command.ExecuteNonQueryAsync().ConfigureAwait(false);
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

                Connection.Dispose();
                Connection = null;
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
