using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.Data.Sqlite;
using RX_Explorer.View;
using SharedLibrary;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Windows.Storage;

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
        private readonly SqliteConnection Connection;

        /// <summary>
        /// 初始化SQLite的实例
        /// </summary>
        private SQLite()
        {
            SqliteConnectionStringBuilder Builder = new SqliteConnectionStringBuilder
            {
                DataSource = Path.Combine(ApplicationData.Current.LocalFolder.Path, "RX_Sqlite.db"),
                Mode = SqliteOpenMode.ReadWriteCreate,
                Cache = SqliteCacheMode.Default
            };

            Connection = new SqliteConnection(Builder.ToString());
            Connection.Open();

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
            using SqliteTransaction Transaction = Connection.BeginTransaction();
            using SqliteCommand InitCommand = new SqliteCommand
            {
                Connection = Connection,
                Transaction = Transaction
            };

            StringBuilder Builder = new StringBuilder();

            Builder.Append("Create Table If Not Exists SearchHistory (SearchText Text Not Null, Primary Key (SearchText));")
                   .Append("Create Table If Not Exists QuickStart (Name Text Not Null, FullPath Text Not Null Collate NoCase, Protocal Text Not Null, Type Text Not Null, Primary Key (Name,FullPath,Protocal,Type));")
                   .Append("Create Table If Not Exists Library (Path Text Not Null Collate NoCase, Type Text Not Null, Primary Key (Path));")
                   .Append("Create Table If Not Exists PathHistory (Path Text Not Null Collate NoCase, Primary Key (Path));")
                   .Append("Create Table If Not Exists BackgroundPicture (FileName Text Not Null, Primary Key (FileName));")
                   .Append("Create Table If Not Exists ProgramPicker (FileType Text Not Null, Path Text Not Null Collate NoCase, IsDefault Text Default 'False' Check(IsDefault In ('True','False')), IsRecommanded Text Default 'False' Check(IsRecommanded In ('True','False')), Primary Key(FileType, Path));")
                   .Append("Create Table If Not Exists TerminalProfile (Name Text Not Null, Path Text Not Null Collate NoCase, Argument Text Not Null, RunAsAdmin Text Not Null, Primary Key(Name));")
                   .Append("Create Table If Not Exists PathConfiguration (Path Text Not Null Collate NoCase, DisplayMode Integer Default 1 Check(DisplayMode In (0,1,2,3,4,5)), SortColumn Text Default 'Name' Check(SortColumn In ('Name','ModifiedTime','Type','Size')), SortDirection Text Default 'Ascending' Check(SortDirection In ('Ascending','Descending')), GroupColumn Text Default 'None' Check(GroupColumn In ('None','Name','ModifiedTime','Type','Size')), GroupDirection Text Default 'Ascending' Check(GroupDirection In ('Ascending','Descending')), Primary Key(Path));")
                   .Append("Create Table If Not Exists PathTagMapping (Path Text Not Null Collate NoCase, Label Text Not Null, Primary Key (Path));");

            InitCommand.CommandText = Builder.ToString();
            InitCommand.ExecuteNonQuery();

            if (!ApplicationData.Current.LocalSettings.Values.ContainsKey("DatabaseInit"))
            {
                Builder.Clear();

                Builder.Append($"Insert Or Replace Into Library Values ('{Guid.NewGuid():N}', '{Enum.GetName(typeof(LibraryType), LibraryType.Downloads)}');")
                       .Append($"Insert Or Replace Into Library Values ('{Guid.NewGuid():N}', '{Enum.GetName(typeof(LibraryType), LibraryType.Desktop)}');")
                       .Append($"Insert Or Replace Into Library Values ('{Guid.NewGuid():N}', '{Enum.GetName(typeof(LibraryType), LibraryType.Videos)}');")
                       .Append($"Insert Or Replace Into Library Values ('{Guid.NewGuid():N}', '{Enum.GetName(typeof(LibraryType), LibraryType.Pictures)}');")
                       .Append($"Insert Or Replace Into Library Values ('{Guid.NewGuid():N}', '{Enum.GetName(typeof(LibraryType), LibraryType.Document)}');")
                       .Append($"Insert Or Replace Into Library Values ('{Guid.NewGuid():N}', '{Enum.GetName(typeof(LibraryType), LibraryType.Music)}');")
                       .Append($"Insert Or Replace Into Library Values ('{Guid.NewGuid():N}', '{Enum.GetName(typeof(LibraryType), LibraryType.OneDrive)}');");

                foreach (int Index in Enumerable.Range(1, 15))
                {
                    Builder.Append($"Insert Or Replace Into BackgroundPicture Values ('ms-appx:///CustomImage/Picture{Index}.jpg');");
                }

                Builder.Append($"Insert Or Replace Into TerminalProfile Values ('Powershell', '{Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "WindowsPowerShell\\v1.0\\powershell.exe")}', '-NoExit -Command \"Set-Location ''[CurrentLocation]''\"', 'True');")
                       .Append($"Insert Or Replace Into TerminalProfile Values ('CMD', '{Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe")}', '/k cd /d [CurrentLocation]', 'True');");

                InitCommand.CommandText = Builder.ToString();
                InitCommand.ExecuteNonQuery();

                InitCommand.CommandText = "Insert Or Replace Into QuickStart Values (@Name,@Path,@Protocal,@Type)";

                IReadOnlyList<(string, string, string, QuickStartType)> DefaultQuickStartList = new List<(string, string, string, QuickStartType)>
                {
                    (Globalization.GetString("ExtendedSplash_QuickStartItem_Name_1"), "ms-appx:///QuickStartImage/MicrosoftStore.png", "ms-windows-store://home", QuickStartType.Application),
                    (Globalization.GetString("ExtendedSplash_QuickStartItem_Name_2"), "ms-appx:///QuickStartImage/Calculator.png", "calculator:", QuickStartType.Application),
                    (Globalization.GetString("ExtendedSplash_QuickStartItem_Name_3"), "ms-appx:///QuickStartImage/Setting.png", "ms-settings:", QuickStartType.Application),
                    (Globalization.GetString("ExtendedSplash_QuickStartItem_Name_4"), "ms-appx:///QuickStartImage/Email.png", "mailto:", QuickStartType.Application),
                    (Globalization.GetString("ExtendedSplash_QuickStartItem_Name_5"), "ms-appx:///QuickStartImage/Calendar.png", "outlookcal:", QuickStartType.Application),
                    (Globalization.GetString("ExtendedSplash_QuickStartItem_Name_6"), "ms-appx:///QuickStartImage/Photos.png", "ms-photos:", QuickStartType.Application),
                    (Globalization.GetString("ExtendedSplash_QuickStartItem_Name_7"), "ms-appx:///QuickStartImage/Weather.png", "msnweather:", QuickStartType.Application),
                    (Globalization.GetString("ExtendedSplash_QuickStartItem_Name_9"), "ms-appx:///HotWebImage/Facebook.png", "https://www.facebook.com/", QuickStartType.WebSite),
                    (Globalization.GetString("ExtendedSplash_QuickStartItem_Name_10"), "ms-appx:///HotWebImage/Instagram.png", "https://www.instagram.com/", QuickStartType.WebSite),
                    (Globalization.GetString("ExtendedSplash_QuickStartItem_Name_11"), "ms-appx:///HotWebImage/Twitter.png", "https://twitter.com", QuickStartType.WebSite)
                };

                foreach ((string Name, string FullPath, string Protocal, QuickStartType Type) in DefaultQuickStartList)
                {
                    InitCommand.Parameters.Clear();
                    InitCommand.Parameters.AddWithValue("@Name", Name);
                    InitCommand.Parameters.AddWithValue("@Path", FullPath);
                    InitCommand.Parameters.AddWithValue("@Protocal", Protocal);
                    InitCommand.Parameters.AddWithValue("@Type", Enum.GetName(typeof(QuickStartType), Type));
                    InitCommand.ExecuteNonQuery();
                }

                ApplicationData.Current.LocalSettings.Values["DatabaseInit"] = true;
            }

            if (ApplicationData.Current.LocalSettings.Values.ContainsKey("RefreshQuickStart"))
            {
                ApplicationData.Current.LocalSettings.Values.Remove("RefreshQuickStart");

                IReadOnlyList<(string, string, QuickStartType)> UpdateArray = new List<(string, string, QuickStartType)>
                {
                    ("ms-appx:///QuickStartImage/MicrosoftStore.png", Globalization.GetString("ExtendedSplash_QuickStartItem_Name_1"), QuickStartType.Application),
                    ("ms-appx:///QuickStartImage/Calculator.png", Globalization.GetString("ExtendedSplash_QuickStartItem_Name_2"), QuickStartType.Application),
                    ("ms-appx:///QuickStartImage/Setting.png", Globalization.GetString("ExtendedSplash_QuickStartItem_Name_3"), QuickStartType.Application),
                    ("ms-appx:///QuickStartImage/Email.png", Globalization.GetString("ExtendedSplash_QuickStartItem_Name_4"), QuickStartType.Application),
                    ("ms-appx:///QuickStartImage/Calendar.png", Globalization.GetString("ExtendedSplash_QuickStartItem_Name_5"), QuickStartType.Application),
                    ("ms-appx:///QuickStartImage/Photos.png", Globalization.GetString("ExtendedSplash_QuickStartItem_Name_6"), QuickStartType.Application),
                    ("ms-appx:///QuickStartImage/Weather.png", Globalization.GetString("ExtendedSplash_QuickStartItem_Name_7"), QuickStartType.Application),
                    ("ms-appx:///HotWebImage/Facebook.png", Globalization.GetString("ExtendedSplash_QuickStartItem_Name_9"), QuickStartType.WebSite),
                    ("ms-appx:///HotWebImage/Instagram.png", Globalization.GetString("ExtendedSplash_QuickStartItem_Name_10"), QuickStartType.WebSite),
                    ("ms-appx:///HotWebImage/Twitter.png", Globalization.GetString("ExtendedSplash_QuickStartItem_Name_11"), QuickStartType.WebSite)
                };

                foreach ((string FullPath, string NewName, QuickStartType Type) in UpdateArray)
                {
                    InitCommand.Parameters.Clear();

                    InitCommand.CommandText = "Select Count(*) From QuickStart Where FullPath = @FullPath";
                    InitCommand.Parameters.AddWithValue("@FullPath", FullPath);

                    if (Convert.ToInt32(InitCommand.ExecuteScalar()) > 0)
                    {
                        InitCommand.Parameters.Clear();

                        InitCommand.CommandText = "Update QuickStart Set Name = @NewName Where FullPath = @FullPath And Type = @Type";
                        InitCommand.Parameters.AddWithValue("@FullPath", FullPath);
                        InitCommand.Parameters.AddWithValue("@NewName", NewName);
                        InitCommand.Parameters.AddWithValue("@Type", Enum.GetName(typeof(QuickStartType), Type));
                        InitCommand.ExecuteNonQuery();
                    }
                }
            }

            Transaction.Commit();
        }

        public void SetDefaultDisplayMode(int Index)
        {
            StringBuilder Builder = new StringBuilder().Append("Drop Table PathConfiguration;")
                                                       .Append($"Create Table PathConfiguration (Path Text Not Null Collate NoCase, DisplayMode Integer Default {Index} Check(DisplayMode In (0,1,2,3,4,5)), SortColumn Text Default 'Name' Check(SortColumn In ('Name','ModifiedTime','Type','Size')), SortDirection Text Default 'Ascending' Check(SortDirection In ('Ascending','Descending')), GroupColumn Text Default 'None' Check(GroupColumn In ('None','Name','ModifiedTime','Type','Size')), GroupDirection Text Default 'Ascending' Check(GroupDirection In ('Ascending','Descending')), Primary Key(Path));");
            using SqliteTransaction Transaction = Connection.BeginTransaction();
            using SqliteCommand Command = new SqliteCommand(Builder.ToString(), Connection, Transaction);
            Command.ExecuteNonQuery();

            Transaction.Commit();
        }

        public void SetPathConfiguration(PathConfiguration Configuration)
        {
            using SqliteCommand Command = new SqliteCommand
            {
                Connection = Connection
            };

            List<string> ValueLeft = new List<string>(4)
            {
                "Path"
            };

            List<string> ValueRight = new List<string>(4)
            {
                "@Path"
            };

            List<string> UpdatePart = new List<string>(4)
            {
                "Path = @Path"
            };

            Command.Parameters.AddWithValue("@Path", Configuration.Path);

            if (Configuration.DisplayModeIndex != null)
            {
                ValueLeft.Add("DisplayMode");
                ValueRight.Add("@DisplayMode");
                UpdatePart.Add("DisplayMode = @DisplayMode");

                Command.Parameters.AddWithValue("@DisplayMode", Configuration.DisplayModeIndex);
            }

            if (Configuration.SortTarget != null)
            {
                ValueLeft.Add("SortColumn");
                ValueRight.Add("@SortColumn");
                UpdatePart.Add("SortColumn = @SortColumn");

                Command.Parameters.AddWithValue("@SortColumn", Enum.GetName(typeof(SortTarget), Configuration.SortTarget));
            }

            if (Configuration.SortDirection != null)
            {
                ValueLeft.Add("SortDirection");
                ValueRight.Add("@SortDirection");
                UpdatePart.Add("SortDirection = @SortDirection");

                Command.Parameters.AddWithValue("@SortDirection", Enum.GetName(typeof(SortDirection), Configuration.SortDirection));
            }

            if (Configuration.GroupTarget != null)
            {
                ValueLeft.Add("GroupColumn");
                ValueRight.Add("@GroupColumn");
                UpdatePart.Add("GroupColumn = @GroupColumn");

                Command.Parameters.AddWithValue("@GroupColumn", Enum.GetName(typeof(GroupTarget), Configuration.GroupTarget));
            }

            if (Configuration.GroupDirection != null)
            {
                ValueLeft.Add("GroupDirection");
                ValueRight.Add("@GroupDirection");
                UpdatePart.Add("GroupDirection = @GroupDirection");

                Command.Parameters.AddWithValue("@GroupDirection", Enum.GetName(typeof(GroupDirection), Configuration.GroupDirection));
            }

            Command.CommandText = $"Insert Into PathConfiguration ({string.Join(", ", ValueLeft)}) Values ({string.Join(", ", ValueRight)}) On Conflict (Path) Do Update Set {string.Join(", ", UpdatePart)} Where Path = @Path";
            Command.ExecuteNonQuery();
        }

        public PathConfiguration GetPathConfiguration(string Path)
        {
            using (SqliteCommand Command = new SqliteCommand("Select * From PathConfiguration Where Path = @Path", Connection))
            {
                Command.Parameters.AddWithValue("@Path", Path);

                using (SqliteDataReader Reader = Command.ExecuteReader())
                {
                    if (Reader.Read())
                    {
                        return new PathConfiguration(Path, Convert.ToInt32(Reader[1]), Enum.Parse<SortTarget>(Convert.ToString(Reader[2])), Enum.Parse<SortDirection>(Convert.ToString(Reader[3])), Enum.Parse<GroupTarget>(Convert.ToString(Reader[4])), Enum.Parse<GroupDirection>(Convert.ToString(Reader[5])));
                    }
                    else
                    {
                        return new PathConfiguration(Path, SettingPage.DefaultDisplayModeIndex, SortTarget.Name, SortDirection.Ascending, GroupTarget.None, GroupDirection.Ascending);
                    }
                }
            }
        }

        public IReadOnlyList<TerminalProfile> GetAllTerminalProfile()
        {
            List<TerminalProfile> Result = new List<TerminalProfile>();

            using (SqliteCommand Command = new SqliteCommand("Select * From TerminalProfile", Connection))
            using (SqliteDataReader Reader = Command.ExecuteReader())
            {
                while (Reader.Read())
                {
                    Result.Add(new TerminalProfile(Reader[0].ToString(), Reader[1].ToString(), Reader[2].ToString(), Convert.ToBoolean(Reader[3])));
                }
            }

            return Result;
        }

        public TerminalProfile GetTerminalProfile(string Name, string Path = null)
        {
            if (!string.IsNullOrEmpty(Name))
            {
                using SqliteCommand Command = new SqliteCommand("Select * From TerminalProfile Where Name = @Name And Path Like @Path Limit 0,1", Connection);

                Command.Parameters.AddWithValue("@Name", Name);

                if (string.IsNullOrEmpty(Path))
                {
                    Command.Parameters.AddWithValue("@Path", "%%");
                }
                else
                {
                    Command.Parameters.AddWithValue("@Path", Path);
                }

                using SqliteDataReader Reader = Command.ExecuteReader();

                if (Reader.Read())
                {
                    return new TerminalProfile(Reader[0].ToString(), Reader[1].ToString(), Reader[2].ToString(), Convert.ToBoolean(Reader[3]));
                }
            }

            return null;
        }

        public bool DeleteTerminalProfile(TerminalProfile Profile)
        {
            if (Profile == null)
            {
                throw new ArgumentNullException(nameof(Profile), "Argument could not be null");
            }

            using (SqliteCommand Command = new SqliteCommand("Delete From TerminalProfile Where Name = @Name", Connection))
            {
                Command.Parameters.AddWithValue("@Name", Profile.Name);
                return Command.ExecuteNonQuery() > 0;
            }
        }

        public void SetTerminalProfile(TerminalProfile Profile)
        {
            if (Profile == null)
            {
                throw new ArgumentNullException(nameof(Profile), "Argument could not be null");
            }

            using SqliteTransaction Transaction = Connection.BeginTransaction();
            using SqliteCommand Command = new SqliteCommand("Insert Or Replace Into TerminalProfile Values (@Name,@Path,@Argument,@RunAsAdmin)", Connection, Transaction);

            Command.Parameters.AddWithValue("@Name", Profile.Name);
            Command.Parameters.AddWithValue("@Path", Profile.Path);
            Command.Parameters.AddWithValue("@Argument", Profile.Argument);
            Command.Parameters.AddWithValue("@RunAsAdmin", Convert.ToString(Profile.RunAsAdmin));
            Command.ExecuteNonQuery();

            Transaction.Commit();
        }

        public void UpdateProgramPickerRecord(IEnumerable<AssociationPackage> AssociationList)
        {
            if (AssociationList.Any())
            {
                string Extension = AssociationList.First().Extension;

                if (AssociationList.Skip(1).All((Item) => Item.Extension.Equals(Extension, StringComparison.OrdinalIgnoreCase)))
                {
                    string DefaultPath = GetDefaultProgramPickerRecord(Extension);

                    using SqliteTransaction Transaction = Connection.BeginTransaction();
                    using SqliteCommand Command = new SqliteCommand("Insert Or Replace Into ProgramPicker Values (@FileType, @ExecutablePath, @IsDefault, @IsRecommanded);", Connection, Transaction);

                    foreach (AssociationPackage Package in AssociationList)
                    {
                        Command.Parameters.Clear();
                        Command.Parameters.AddWithValue("@FileType", Package.Extension);
                        Command.Parameters.AddWithValue("@ExecutablePath", Package.ExecutablePath);
                        Command.Parameters.AddWithValue("@IsDefault", Convert.ToString(Package.ExecutablePath.Equals(DefaultPath, StringComparison.OrdinalIgnoreCase)));
                        Command.Parameters.AddWithValue("@IsRecommanded", Convert.ToString(Package.IsRecommanded));
                        Command.ExecuteNonQuery();
                    }

                    Transaction.Commit();
                }
            }
        }

        public string GetDefaultProgramPickerRecord(string Extension)
        {
            using (SqliteCommand Command = new SqliteCommand("Select Path From ProgramPicker Where FileType = @FileType And IsDefault = 'True'", Connection))
            {
                Command.Parameters.AddWithValue("@FileType", Extension.ToLower());
                return Convert.ToString(Command.ExecuteScalar());
            }
        }

        public void SetDefaultProgramPickerRecord(string Extension, string Path)
        {
            using SqliteTransaction Transaction = Connection.BeginTransaction();
            using SqliteCommand Command = new SqliteCommand("Update ProgramPicker Set IsDefault = 'False' Where FileType = @FileType", Connection, Transaction);

            Command.Parameters.AddWithValue("@FileType", Extension.ToLower());
            Command.ExecuteNonQuery();

            Command.CommandText = "Insert Into ProgramPicker Values (@FileType, @Path, 'True', 'True') On Conflict (FileType, Path) Do Update Set IsDefault = 'True'";
            Command.Parameters.AddWithValue("@Path", Path);

            Command.ExecuteNonQuery();

            Transaction.Commit();
        }

        public IReadOnlyList<AssociationPackage> GetProgramPickerRecord(string Extension)
        {
            try
            {
                List<AssociationPackage> Result = new List<AssociationPackage>();

                using (SqliteCommand Command = new SqliteCommand("Select * From ProgramPicker Where FileType = @FileType", Connection))
                {
                    Command.Parameters.AddWithValue("@FileType", Extension.ToLower());

                    using (SqliteDataReader Reader = Command.ExecuteReader())
                    {
                        while (Reader.Read())
                        {
                            Result.Add(new AssociationPackage(Extension, Convert.ToString(Reader[1]), Convert.ToBoolean(Reader[3])));
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

        public void DeleteProgramPickerRecord(AssociationPackage Package)
        {
            using (SqliteCommand Command = new SqliteCommand("Delete From ProgramPicker Where FileType = @FileType And Path = @Path", Connection))
            {
                Command.Parameters.AddWithValue("@FileType", Package.Extension);
                Command.Parameters.AddWithValue("@Path", Package.ExecutablePath);
                Command.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// 保存背景图片的Uri路径
        /// </summary>
        /// <param name="uri">图片Uri</param>
        /// <returns></returns>
        public void SetBackgroundPicture(Uri uri)
        {
            if (uri != null)
            {
                using (SqliteCommand Command = new SqliteCommand("Insert Or Replace Into BackgroundPicture Values (@FileName)", Connection))
                {
                    Command.Parameters.AddWithValue("@FileName", uri.ToString());
                    Command.ExecuteNonQuery();
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
        public IReadOnlyList<Uri> GetBackgroundPicture()
        {
            List<Uri> list = new List<Uri>();

            using (SqliteCommand Command = new SqliteCommand("Select * From BackgroundPicture", Connection))
            using (SqliteDataReader Reader = Command.ExecuteReader())
            {
                while (Reader.Read())
                {
                    list.Add(new Uri(Reader[0].ToString()));
                }
            }

            return list;
        }

        /// <summary>
        /// 删除背景图片的Uri信息
        /// </summary>
        /// <param name="uri">图片Uri</param>
        /// <returns></returns>
        public void DeleteBackgroundPicture(Uri uri)
        {
            if (uri != null)
            {
                using (SqliteCommand Command = new SqliteCommand("Delete From BackgroundPicture Where FileName=@FileName", Connection))
                {
                    Command.Parameters.AddWithValue("@FileName", uri.ToString());
                    Command.ExecuteNonQuery();
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
        public IReadOnlyList<LibraryFolderRecord> GetLibraryFolderRecord()
        {
            List<LibraryFolderRecord> Result = new List<LibraryFolderRecord>();

            using (SqliteCommand Command = new SqliteCommand("Select * From Library", Connection))
            using (SqliteDataReader Reader = Command.ExecuteReader())
            {
                while (Reader.Read())
                {
                    Result.Add(new LibraryFolderRecord(Enum.Parse<LibraryType>(Convert.ToString(Reader[1])), Convert.ToString(Reader[0])));
                }
            }

            return Result;
        }

        public IReadOnlyList<string> GetPathListFromLabelKind(LabelKind Kind)
        {
            List<string> PathList = new List<string>();

            using (SqliteCommand Command = new SqliteCommand("Select * From PathTagMapping Where Label = @Label", Connection))
            {
                Command.Parameters.AddWithValue("@Label", Enum.GetName(typeof(LabelKind), Kind));

                using (SqliteDataReader Reader = Command.ExecuteReader())
                {
                    while (Reader.Read())
                    {
                        string Path = Convert.ToString(Reader[0]);

                        if (!string.IsNullOrEmpty(Path))
                        {
                            PathList.Add(Path);
                        }
                    }
                }
            }

            return PathList;
        }

        public void DeleteLabelKindByPath(string Path)
        {
            if (!string.IsNullOrEmpty(Path))
            {
                using (SqliteCommand Command = new SqliteCommand("Delete From PathTagMapping Where Path = @Path", Connection))
                {
                    Command.Parameters.AddWithValue("@Path", Path);
                    Command.ExecuteNonQuery();
                }
            }
        }

        public void DeleteLabelKindByPathList(IEnumerable<string> PathList)
        {
            if (PathList.Any())
            {
                using SqliteTransaction Transaction = Connection.BeginTransaction();
                using SqliteCommand Command = new SqliteCommand("Delete From PathTagMapping Where Path = @Path", Connection, Transaction);

                foreach (string Path in PathList)
                {
                    Command.Parameters.Clear();
                    Command.Parameters.AddWithValue("@Path", Path);
                    Command.ExecuteNonQuery();
                }

                Transaction.Commit();
            }
        }

        public void SetLabelKindByPath(string Path, LabelKind Label)
        {
            using (SqliteCommand Command = new SqliteCommand("Insert Or Replace Into PathTagMapping Values (@Path, @Label)", Connection))
            {
                Command.Parameters.AddWithValue("@Path", Path);
                Command.Parameters.AddWithValue("@Label", Enum.GetName(typeof(LabelKind), Label));
                Command.ExecuteNonQuery();
            }
        }

        public LabelKind GetLabelKindFromPath(string Path)
        {
            using (SqliteCommand Command = new SqliteCommand("Select Label From PathTagMapping Where Path = @Path", Connection))
            {
                Command.Parameters.AddWithValue("@Path", Path);

                string LabelRawString = Convert.ToString(Command.ExecuteScalar());

                if (!string.IsNullOrEmpty(LabelRawString) && Enum.TryParse(LabelRawString, out LabelKind Label))
                {
                    return Label;
                }

                return LabelKind.None;
            }
        }

        /// <summary>
        /// 删除文件夹和库区域的用户自定义文件夹的数据
        /// </summary>
        /// <param name="Path">自定义文件夹的路径</param>
        /// <returns></returns>
        public void DeleteLibraryFolderRecord(string Path)
        {
            using (SqliteCommand Command = new SqliteCommand("Delete From Library Where Path = @Path", Connection))
            {
                Command.Parameters.AddWithValue("@Path", Path);
                Command.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// 保存文件路径栏的记录
        /// </summary>
        /// <param name="Path">输入的文件路径</param>
        /// <returns></returns>
        public void SetPathHistory(string Path)
        {
            using (SqliteCommand Command = new SqliteCommand("Insert Or Replace Into PathHistory Values (@Para)", Connection))
            {
                Command.Parameters.AddWithValue("@Para", Path);
                Command.ExecuteNonQuery();
            }
        }

        public void DeletePathHistory(string Path)
        {
            using (SqliteCommand Command = new SqliteCommand("Delete From PathHistory Where Path = @Para", Connection))
            {
                Command.Parameters.AddWithValue("@Para", Path);
                Command.ExecuteNonQuery();
            }
        }


        /// <summary>
        /// 模糊查询与文件路径栏相关的输入历史记录
        /// </summary>
        /// <param name="Target">输入内容</param>
        /// <returns></returns>
        public IReadOnlyList<string> GetRelatedPathHistory()
        {
            List<string> PathList = new List<string>(25);

            using (SqliteCommand Command = new SqliteCommand("Select * From PathHistory Order By rowid Desc Limit 0,25", Connection))
            using (SqliteDataReader Reader = Command.ExecuteReader())
            {
                while (Reader.Read())
                {
                    PathList.Add(Convert.ToString(Reader[0]));
                }
            }

            return PathList;
        }

        public void UpdateLibraryFolderRecord(IEnumerable<LibraryFolderRecord> Records)
        {
            using SqliteTransaction Transaction = Connection.BeginTransaction();
            using SqliteCommand Command = new SqliteCommand("Update Or Ignore Library Set Path = @Path Where Type = @Type", Connection, Transaction);

            foreach (LibraryFolderRecord Record in Records.Where((Pair) => !string.IsNullOrEmpty(Pair.Path)))
            {
                Command.Parameters.Clear();
                Command.Parameters.AddWithValue("@Path", Record.Path);
                Command.Parameters.AddWithValue("@Type", Enum.GetName(typeof(LibraryType), Record.Type));
                Command.ExecuteNonQuery();
            }

            Transaction.Commit();
        }

        public void SetLibraryPathRecord(LibraryType Type, string Path)
        {
            using SqliteCommand Command = new SqliteCommand("Insert Or Replace Into Library Values (@Path, @Type)", Connection);
            {
                Command.Parameters.AddWithValue("@Path", Path);
                Command.Parameters.AddWithValue("@Type", Enum.GetName(typeof(LibraryType), Type));
                Command.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// 保存搜索历史记录
        /// </summary>
        /// <param name="SearchText">搜索内容</param>
        /// <returns></returns>
        public void SetSearchHistory(string SearchText)
        {
            using (SqliteCommand Command = new SqliteCommand("Insert Or Ignore Into SearchHistory Values (@Para)", Connection))
            {
                Command.Parameters.AddWithValue("@Para", SearchText);
                Command.ExecuteNonQuery();
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
        public void SetQuickStartItem(string Name, string FullPath, string Protocal, QuickStartType Type)
        {
            using (SqliteCommand Command = new SqliteCommand("Insert Or Ignore Into QuickStart Values (@Name,@Path,@Protocal,@Type)", Connection))
            {
                Command.Parameters.AddWithValue("@Name", Name);
                Command.Parameters.AddWithValue("@Path", FullPath);
                Command.Parameters.AddWithValue("@Protocal", Protocal);
                Command.Parameters.AddWithValue("@Type", Enum.GetName(typeof(QuickStartType), Type));
                Command.ExecuteNonQuery();
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
        public void UpdateQuickStartItem(string OldName, string NewName, string FullPath, string Protocal, QuickStartType Type)
        {
            using SqliteTransaction Transaction = Connection.BeginTransaction();
            using SqliteCommand Command = new SqliteCommand("Select Count(*) From QuickStart Where Name=@OldName", Connection, Transaction);

            Command.Parameters.AddWithValue("@OldName", OldName);

            if (Convert.ToInt32(Command.ExecuteScalar()) == 0)
            {
                return;
            }

            Command.Parameters.Clear();

            if (FullPath != null)
            {
                Command.CommandText = "Update QuickStart Set Name=@NewName, FullPath=@Path, Protocal=@Protocal Where Name=@OldName And Type=@Type";
                Command.Parameters.AddWithValue("@Path", FullPath);
            }
            else
            {
                Command.CommandText = "Update QuickStart Set Name = @NewName, Protocal = @Protocal Where Name = @OldName And Type = @Type";
            }

            Command.Parameters.AddWithValue("@OldName", OldName);
            Command.Parameters.AddWithValue("@NewName", NewName);
            Command.Parameters.AddWithValue("@Protocal", Protocal);
            Command.Parameters.AddWithValue("@Type", Enum.GetName(typeof(QuickStartType), Type));
            Command.ExecuteNonQuery();

            Transaction.Commit();
        }

        /// <summary>
        /// 删除快速启动项的内容
        /// </summary>
        /// <param name="Item">要删除的项</param>
        /// <returns></returns>
        public void DeleteQuickStartItem(QuickStartItem Item)
        {
            if (Item == null)
            {
                throw new ArgumentNullException(nameof(Item), "Parameter could not be null");
            }

            using (SqliteCommand Command = new SqliteCommand("Delete From QuickStart Where Name = @Name And Protocal = @Protocol And FullPath = @FullPath And Type=@Type", Connection))
            {
                Command.Parameters.AddWithValue("@Name", Item.DisplayName);
                Command.Parameters.AddWithValue("@Protocol", Item.Protocol);
                Command.Parameters.AddWithValue("@FullPath", Item.IconPath);
                Command.Parameters.AddWithValue("@Type", Enum.GetName(typeof(QuickStartType), Item.Type));
                Command.ExecuteNonQuery();
            }
        }

        public void DeleteQuickStartItem(QuickStartType Type)
        {
            using (SqliteCommand Command = new SqliteCommand("Delete From QuickStart Where Type=@Type", Connection))
            {
                Command.Parameters.AddWithValue("@Type", Enum.GetName(typeof(QuickStartType), Type));
                Command.ExecuteNonQuery();
            }
        }

        public void DeleteQuickStartItem(string Name, string Protocol, string FullPath, string Type)
        {
            using (SqliteCommand Command = new SqliteCommand("Delete From QuickStart Where Name = @Name And Protocal = @Protocol And FullPath = @FullPath And Type=@Type", Connection))
            {
                Command.Parameters.AddWithValue("@Name", Name);
                Command.Parameters.AddWithValue("@Protocol", Protocol);
                Command.Parameters.AddWithValue("@FullPath", FullPath);
                Command.Parameters.AddWithValue("@Type", Type);
                Command.ExecuteNonQuery();
            }
        }


        /// <summary>
        /// 获取所有快速启动项
        /// </summary>
        /// <returns></returns>
        public IReadOnlyList<(string, string, string, string)> GetQuickStartItem()
        {
            List<(string, string, string, string)> Result = new List<(string, string, string, string)>();

            using (SqliteCommand Command = new SqliteCommand("Select * From QuickStart", Connection))
            using (SqliteDataReader Reader = Command.ExecuteReader())
            {
                while (Reader.Read())
                {
                    Result.Add((Convert.ToString(Reader[0]), Convert.ToString(Reader[1]), Convert.ToString(Reader[2]), Convert.ToString(Reader[3])));
                }
            }

            return Result;
        }

        /// <summary>
        /// 获取与搜索内容有关的搜索历史
        /// </summary>
        /// <param name="Target">搜索内容</param>
        /// <returns></returns>
        public IReadOnlyList<string> GetRelatedSearchHistory(string Target)
        {
            List<string> HistoryList = new List<string>();

            using (SqliteCommand Command = new SqliteCommand("Select * From SearchHistory Where SearchText Like @Target Order By rowid Desc Limit 0,25", Connection))
            {
                Command.Parameters.AddWithValue("@Target", $"%{Target}%");

                using (SqliteDataReader Reader = Command.ExecuteReader())
                {
                    while (Reader.Read())
                    {
                        HistoryList.Add(Convert.ToString(Reader[0]));
                    }

                    return HistoryList;
                }
            }
        }

        public void DeleteSearchHistory(string RecordText)
        {
            using (SqliteCommand Command = new SqliteCommand("Delete From SearchHistory Where SearchText = @RecordText", Connection))
            {
                Command.Parameters.AddWithValue("@RecordText", RecordText);
                Command.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// 清空特定的数据表
        /// </summary>
        /// <param name="TableName">数据表名</param>
        /// <returns></returns>
        public void ClearTable(string TableName)
        {
            using (SqliteCommand Command = new SqliteCommand($"Delete From {TableName}", Connection))
            {
                Command.ExecuteNonQuery();
            }
        }

        public IReadOnlyList<(string, IReadOnlyList<object[]>)> ExportData()
        {
            List<string> TableNameArray = new List<string>();

            using SqliteTransaction Transaction = Connection.BeginTransaction();

            using (SqliteCommand Command = new SqliteCommand("Select name From sqlite_master Where type='table' Order By name", Connection, Transaction))
            using (SqliteDataReader Reader = Command.ExecuteReader())
            {
                while (Reader.Read())
                {
                    TableNameArray.Add(Convert.ToString(Reader[0]));
                }
            }

            List<(string, IReadOnlyList<object[]>)> Result = new List<(string, IReadOnlyList<object[]>)>();

            foreach (string TableName in TableNameArray)
            {
                using (SqliteCommand SubCommand = new SqliteCommand($"Select * From {TableName}", Connection, Transaction))
                using (SqliteDataReader SubReader = SubCommand.ExecuteReader())
                {
                    List<object[]> TableData = new List<object[]>();

                    while (SubReader.Read())
                    {
                        object[] ColumnData = new object[SubReader.FieldCount];

                        for (int Index = 0; Index < SubReader.FieldCount; Index++)
                        {
                            ColumnData[Index] = SubReader[Index];
                        }

                        TableData.Add(ColumnData);
                    }

                    Result.Add((TableName, TableData));
                }
            }

            Transaction.Commit();

            return Result;
        }

        public void ImportData(IEnumerable<(string TableName, IEnumerable<object[]> Data)> InputData)
        {
            if (InputData.Any())
            {
                using SqliteTransaction Transaction = Connection.BeginTransaction();
                using SqliteCommand Command = new SqliteCommand("Select Name From sqlite_master Where type='table'", Connection, Transaction);

                List<string> IncomingTableNames = InputData.Select((Item) => Item.TableName).ToList();
                List<string> CurrentTableNames = new List<string>();

                using (SqliteDataReader Reader = Command.ExecuteReader())
                {
                    while (Reader.Read())
                    {
                        CurrentTableNames.Add(Convert.ToString(Reader[0]));
                    }
                }

                IReadOnlyList<string> ValidTableName = CurrentTableNames.Intersect(IncomingTableNames).ToList();

                Command.CommandText = string.Join(';', ValidTableName.Select((TableName) => $"Delete From {TableName}"));
                Command.ExecuteNonQuery();

                List<SqliteParameter> Parameters = new List<SqliteParameter>();

                foreach ((string TableName, IEnumerable<object[]> Data) in InputData.Where((Input) => ValidTableName.Contains(Input.TableName)))
                {
                    if (Data.Any())
                    {
                        int ColumnNum = Data.Max((Row) => Row.Length);

                        if (Parameters.Count < ColumnNum)
                        {
                            for (int i = Parameters.Count; i < ColumnNum; i++)
                            {
                                SqliteParameter Para = Command.CreateParameter();
                                Para.ParameterName = $"$param_{i}";
                                Parameters.Add(Para);
                            }
                        }
                        else if (Parameters.Count > ColumnNum)
                        {
                            Parameters.RemoveRange(ColumnNum, Parameters.Count - ColumnNum);
                        }

                        Command.CommandText = $"Insert Into {TableName} Values ({string.Join(", ", Parameters.Select((Para) => Para.ParameterName))})";

                        foreach (object[] RowData in Data.Where((Row) => Row.Length > 0))
                        {
                            for (int i = 0; i < RowData.Length; i++)
                            {
                                Parameters[i].Value = RowData[i];
                            }

                            Command.Parameters.Clear();
                            Command.Parameters.AddRange(Parameters);
                            Command.ExecuteNonQuery();
                        }
                    }
                }

                Transaction.Commit();
            }
        }

        public void ClearAllData()
        {
            using SqliteTransaction Transaction = Connection.BeginTransaction();
            using SqliteCommand Command = new SqliteCommand("Select Name From sqlite_master Where type='table'", Connection, Transaction);

            List<string> TableNames = new List<string>();

            using (SqliteDataReader Reader = Command.ExecuteReader())
            {
                while (Reader.Read())
                {
                    TableNames.Add(Convert.ToString(Reader[0]));
                }
            }

            foreach (string Name in TableNames)
            {
                Command.CommandText = $"Drop Table {Name}";
                Command.ExecuteNonQuery();
            }

            Transaction.Commit();
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
