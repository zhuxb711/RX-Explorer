using Microsoft.Data.Sqlite;
using ShareClassLibrary;
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
            StringBuilder Builder = new StringBuilder();

            Builder.Append("Create Table If Not Exists SearchHistory (SearchText Text Not Null, Primary Key (SearchText));")
                   .Append("Create Table If Not Exists QuickStart (Name Text Not Null, FullPath Text Not Null Collate NoCase, Protocal Text Not Null, Type Text Not Null, Primary Key (Name,FullPath,Protocal,Type));")
                   .Append("Create Table If Not Exists Library (Path Text Not Null Collate NoCase, Type Text Not Null, Primary Key (Path));")
                   .Append("Create Table If Not Exists PathHistory (Path Text Not Null Collate NoCase, Primary Key (Path));")
                   .Append("Create Table If Not Exists BackgroundPicture (FileName Text Not Null, Primary Key (FileName));")
                   .Append("Create Table If Not Exists ProgramPicker (FileType Text Not Null, Path Text Not Null Collate NoCase, IsDefault Text Default 'False' Check(IsDefault In ('True','False')), IsRecommanded Text Default 'False' Check(IsRecommanded In ('True','False')), Primary Key(FileType, Path));")
                   .Append("Create Table If Not Exists TerminalProfile (Name Text Not Null, Path Text Not Null Collate NoCase, Argument Text Not Null, RunAsAdmin Text Not Null, Primary Key(Name));")
                   .Append("Create Table If Not Exists PathConfiguration (Path Text Not Null Collate NoCase, DisplayMode Integer Default 1 Check(DisplayMode In (0,1,2,3,4,5)), SortColumn Text Default 'Name' Check(SortColumn In ('Name','ModifiedTime','Type','Size')), SortDirection Text Default 'Ascending' Check(SortDirection In ('Ascending','Descending')), GroupColumn Text Default 'None' Check(GroupColumn In ('None','Name','ModifiedTime','Type','Size')), GroupDirection Text Default 'Ascending' Check(GroupDirection In ('Ascending','Descending')), Primary Key(Path));")
                   .Append("Create Table If Not Exists FileColor (Path Text Not Null Collate NoCase, Color Text Not Null, Primary Key (Path));");

            if (!ApplicationData.Current.LocalSettings.Values.ContainsKey("DatabaseInit"))
            {
                Builder.Append("Insert Or Ignore Into BackgroundPicture Values('ms-appx:///CustomImage/Picture1.jpg');")
                       .Append("Insert Or Ignore Into BackgroundPicture Values('ms-appx:///CustomImage/Picture2.jpg');")
                       .Append("Insert Or Ignore Into BackgroundPicture Values('ms-appx:///CustomImage/Picture3.jpg');")
                       .Append("Insert Or Ignore Into BackgroundPicture Values('ms-appx:///CustomImage/Picture4.jpg');")
                       .Append("Insert Or Ignore Into BackgroundPicture Values('ms-appx:///CustomImage/Picture5.jpg');")
                       .Append("Insert Or Ignore Into BackgroundPicture Values('ms-appx:///CustomImage/Picture6.jpg');")
                       .Append("Insert Or Ignore Into BackgroundPicture Values('ms-appx:///CustomImage/Picture7.jpg');")
                       .Append("Insert Or Ignore Into BackgroundPicture Values('ms-appx:///CustomImage/Picture8.jpg');")
                       .Append("Insert Or Ignore Into BackgroundPicture Values('ms-appx:///CustomImage/Picture9.jpg');")
                       .Append("Insert Or Ignore Into BackgroundPicture Values('ms-appx:///CustomImage/Picture10.jpg');")
                       .Append("Insert Or Ignore Into BackgroundPicture Values('ms-appx:///CustomImage/Picture11.jpg');")
                       .Append("Insert Or Ignore Into BackgroundPicture Values('ms-appx:///CustomImage/Picture12.jpg');")
                       .Append("Insert Or Ignore Into BackgroundPicture Values('ms-appx:///CustomImage/Picture13.jpg');")
                       .Append("Insert Or Ignore Into BackgroundPicture Values('ms-appx:///CustomImage/Picture14.jpg');")
                       .Append("Insert Or Ignore Into BackgroundPicture Values('ms-appx:///CustomImage/Picture15.jpg');")
                       .Append($"Insert Or Ignore Into TerminalProfile Values ('Powershell', '{Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "WindowsPowerShell\\v1.0\\powershell.exe")}', '-NoExit -Command \"Set-Location ''[CurrentLocation]''\"', 'True');")
                       .Append($"Insert Or Ignore Into TerminalProfile Values ('CMD', '{Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe")}', '/k cd /d [CurrentLocation]', 'True');");

                ApplicationData.Current.LocalSettings.Values["DatabaseInit"] = true;
            }

            using SqliteTransaction Transaction = Connection.BeginTransaction();
            using SqliteCommand CreateTable = new SqliteCommand(Builder.ToString(), Connection, Transaction);

            CreateTable.ExecuteNonQuery();

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
                        return new PathConfiguration(Path, 1, SortTarget.Name, SortDirection.Ascending, GroupTarget.None, GroupDirection.Ascending);
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

        public TerminalProfile GetTerminalProfileByName(string Name)
        {
            using SqliteCommand Command = new SqliteCommand("Select * From TerminalProfile Where Name = @Name", Connection);

            Command.Parameters.AddWithValue("@Name", Name);

            using (SqliteDataReader Reader = Command.ExecuteReader())
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

        public void DeleteTerminalProfile(TerminalProfile Profile)
        {
            if (Profile == null)
            {
                throw new ArgumentNullException(nameof(Profile), "Argument could not be null");
            }

            using (SqliteCommand Command = new SqliteCommand("Delete From TerminalProfile Where Name = @Name", Connection))
            {
                Command.Parameters.AddWithValue("@Name", Profile.Name);
                Command.ExecuteNonQuery();
            }
        }

        public void SetOrModifyTerminalProfile(TerminalProfile Profile)
        {
            if (Profile == null)
            {
                throw new ArgumentNullException(nameof(Profile), "Argument could not be null");
            }

            using SqliteTransaction Transaction = Connection.BeginTransaction();

            using SqliteCommand Command = new SqliteCommand("Select Count(*) From TerminalProfile Where Name = @Name", Connection, Transaction);

            Command.Parameters.AddWithValue("@Name", Profile.Name);

            int Count = Convert.ToInt32(Command.ExecuteScalar());

            Command.CommandText = Count > 0 ?
                "Update TerminalProfile Set Path = @Path, Argument = @Argument, RunAsAdmin = @RunAsAdmin Where Name = @Name" :
                "Insert Into TerminalProfile Values (@Name,@Path,@Argument,@RunAsAdmin)";


            Command.Parameters.AddWithValue("@Path", Profile.Path);
            Command.Parameters.AddWithValue("@Argument", Profile.Argument);
            Command.Parameters.AddWithValue("@RunAsAdmin", Convert.ToString(Profile.RunAsAdmin));
            Command.ExecuteNonQuery();

            Transaction.Commit();
        }

        public void SetProgramPickerRecord(params AssociationPackage[] Packages)
        {
            using var Transaction = Connection.BeginTransaction();
            using var Command = Connection.CreateCommand();

            Command.CommandText = $"Insert Or Ignore Into ProgramPicker Values (@Extension, @ExecutablePath, 'False', @IsRecommanded);";

            var ExtensionPara = Command.CreateParameter();
            ExtensionPara.ParameterName = "@Extension";
            var ExerPathPara = Command.CreateParameter();
            ExerPathPara.ParameterName = "@ExecutablePath";
            var IsRecommandedPara = Command.CreateParameter();
            IsRecommandedPara.ParameterName = "@IsRecommanded";

            Command.Parameters.Add(ExtensionPara);
            Command.Parameters.Add(ExerPathPara);
            Command.Parameters.Add(IsRecommandedPara);

            foreach (var package in Packages)
            {
                ExtensionPara.Value = package.Extension.ToLower();
                ExerPathPara.Value = package.ExecutablePath;
                IsRecommandedPara.Value = package.IsRecommanded.ToString();
                Command.ExecuteNonQuery();
            }

            Transaction.Commit();
        }

        public void UpdateProgramPickerRecord(string Extension, params AssociationPackage[] AssociationList)
        {
            string DefaultPath = GetDefaultProgramPickerRecord(Extension);

            using var Transaction = Connection.BeginTransaction();

            using var Command = Connection.CreateCommand();
            Command.CommandText = "Update ProgramPicker Set IsDefault = 'False' Where FileType = @FileType;";

            var fileTypePara = new SqliteParameter("@FileType", Extension.ToLower());

            Command.Parameters.Add(fileTypePara);
            Command.ExecuteNonQuery();


            Command.CommandText = $"Insert Or Replace Into ProgramPicker Values (@FileType, @ExecutablePath, @IsDefault, @IsRecommanded);";

            var ExerPathPara = Command.CreateParameter();
            ExerPathPara.ParameterName = "@ExecutablePath";
            var IsDefaultPara = Command.CreateParameter();
            IsDefaultPara.ParameterName = "@IsDefault";
            var IsRecommandedPara = Command.CreateParameter();
            IsRecommandedPara.ParameterName = "@IsRecommanded";

            Command.Parameters.Add(ExerPathPara);
            Command.Parameters.Add(IsDefaultPara);
            Command.Parameters.Add(IsRecommandedPara);

            foreach (var association in AssociationList)
            {
                ExerPathPara.Value = association.ExecutablePath;
                IsDefaultPara.Value = association.ExecutablePath.Equals(DefaultPath, StringComparison.OrdinalIgnoreCase).ToString();
                IsRecommandedPara.Value = association.IsRecommanded.ToString();
                Command.ExecuteNonQuery();
            }

            Transaction.Commit();
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

            Command.CommandText = "Update ProgramPicker Set IsDefault = 'True' Where FileType = @FileType And Path = @Path";
            Command.Parameters.AddWithValue("@Path", Path);

            Command.ExecuteNonQuery();

            Transaction.Commit();
        }

        public IReadOnlyList<AssociationPackage> GetProgramPickerRecord(string Extension, bool IncludeUWPApplication)
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
                            //Reader.IsDBNull check is for the user who updated to v5.8.0 and v5.8.0 have DatabaseTable defect on 'ProgramPicker', maybe we could delete this check after several version
                            if (IncludeUWPApplication)
                            {
                                Result.Add(new AssociationPackage(Extension, Convert.ToString(Reader[1]), !Reader.IsDBNull(3) && Convert.ToBoolean(Reader[3])));
                            }
                            else
                            {
                                if (Path.IsPathRooted(Convert.ToString(Reader[1])))
                                {
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

        public void DeleteProgramPickerRecord(AssociationPackage Package)
        {
            using (SqliteCommand Command = new SqliteCommand("Delete From ProgramPicker Where FileType = @FileType And Path = @Path", Connection))
            {
                Command.Parameters.AddWithValue("@FileType", Package.Extension.ToLower());
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
                using (SqliteCommand Command = new SqliteCommand("Insert Into BackgroundPicture Values (@FileName)", Connection))
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
            using (SqliteDataReader Query = Command.ExecuteReader())
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
        public IReadOnlyList<(string, LibraryType)> GetLibraryPath()
        {
            List<(string, LibraryType)> list = new List<(string, LibraryType)>();

            using (SqliteCommand Command = new SqliteCommand("Select * From Library", Connection))
            using (SqliteDataReader Query = Command.ExecuteReader())
            {
                while (Query.Read())
                {
                    list.Add((Query[0].ToString(), Enum.Parse<LibraryType>(Query[1].ToString())));
                }
            }

            return list;
        }

        /// <summary>
        /// 取消文件颜色 
        /// </summary>
        /// <param name="Path">文件路径</param>
        /// <returns></returns>
        public void DeleteFileColor(string Path)
        {
            using (SqliteCommand Command = new SqliteCommand("Delete From FileColor Where Path = @Path", Connection))
            {
                Command.Parameters.AddWithValue("@Path", Path);
                Command.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// 设置文件颜色 
        /// </summary>
        /// <param name="Path">文件路径</param>
        /// <param name="Color">颜色</param>
        /// <returns></returns>
        public void SetFileColor(string Path, string Color)
        {
            using (SqliteCommand Command = new SqliteCommand("Insert Or Replace Into FileColor Values (@Path, @Color)", Connection))
            {
                Command.Parameters.AddWithValue("@Path", Path);
                Command.Parameters.AddWithValue("@Color", Color);
                Command.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// 获取所有文件颜色
        /// </summary>
        /// <returns></returns>
        public string GetFileColor(string Path)
        {
            using (SqliteCommand Command = new SqliteCommand("Select Color From FileColor Where Path = @Path", Connection))
            {
                Command.Parameters.AddWithValue("@Path", Path);
                return Convert.ToString(Command.ExecuteScalar());
            }
        }

        /// <summary>
        /// 删除文件夹和库区域的用户自定义文件夹的数据
        /// </summary>
        /// <param name="Path">自定义文件夹的路径</param>
        /// <returns></returns>
        public void DeleteLibrary(string Path)
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
            using (SqliteDataReader Query = Command.ExecuteReader())
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
        public void SetLibraryPath(string Path, LibraryType Type)
        {
            using (SqliteCommand Command = new SqliteCommand("Insert Or Ignore Into Library Values (@Path,@Type)", Connection))
            {
                Command.Parameters.AddWithValue("@Path", Path);
                Command.Parameters.AddWithValue("@Type", Enum.GetName(typeof(LibraryType), Type));
                Command.ExecuteNonQuery();
            }
        }

        public void UpdateLibrary(string NewPath, LibraryType Type)
        {
            using (SqliteCommand Command = new SqliteCommand("Update Library Set Path=@NewPath Where Type=@Type", Connection))
            {
                Command.Parameters.AddWithValue("@NewPath", NewPath);
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

            using (SqliteCommand Command = new SqliteCommand("Select Count(*) From QuickStart Where Name=@OldName", Connection, Transaction))
            {
                Command.Parameters.AddWithValue("@OldName", OldName);

                if (Convert.ToInt32(Command.ExecuteScalar()) == 0)
                {
                    return;
                }
            }

            if (FullPath != null)
            {
                using (SqliteCommand Command = new SqliteCommand("Update QuickStart Set Name=@NewName, FullPath=@Path, Protocal=@Protocal Where Name=@OldName And Type=@Type", Connection, Transaction))
                {
                    Command.Parameters.AddWithValue("@OldName", OldName);
                    Command.Parameters.AddWithValue("@Path", FullPath);
                    Command.Parameters.AddWithValue("@NewName", NewName);
                    Command.Parameters.AddWithValue("@Protocal", Protocal);
                    Command.Parameters.AddWithValue("@Type", Enum.GetName(typeof(QuickStartType), Type));
                    Command.ExecuteNonQuery();
                }
            }
            else
            {
                using (SqliteCommand Command = new SqliteCommand("Update QuickStart Set Name=@NewName, Protocal=@Protocal Where Name=@OldName And Type=@Type", Connection, Transaction))
                {
                    Command.Parameters.AddWithValue("@OldName", OldName);
                    Command.Parameters.AddWithValue("@NewName", NewName);
                    Command.Parameters.AddWithValue("@Protocal", Protocal);
                    Command.Parameters.AddWithValue("@Type", Enum.GetName(typeof(QuickStartType), Type));
                    Command.ExecuteNonQuery();
                }
            }

            Transaction.Commit();
        }

        public void UpdateQuickStartItem(string FullPath, string NewName, QuickStartType Type)
        {
            using SqliteTransaction Transaction = Connection.BeginTransaction();

            using (SqliteCommand Command = new SqliteCommand("Select Count(*) From QuickStart Where FullPath=@FullPath", Connection, Transaction))
            {
                Command.Parameters.AddWithValue("@FullPath", FullPath);

                if (Convert.ToInt32(Command.ExecuteScalar()) == 0)
                {
                    return;
                }
            }

            using (SqliteCommand Command = new SqliteCommand("Update QuickStart Set Name=@NewName Where FullPath=@FullPath And Type=@Type", Connection, Transaction))
            {
                Command.Parameters.AddWithValue("@FullPath", FullPath);
                Command.Parameters.AddWithValue("@NewName", NewName);
                Command.Parameters.AddWithValue("@Type", Enum.GetName(typeof(QuickStartType), Type));
                Command.ExecuteNonQuery();
            }

            Transaction.Commit();
        }

        /// <summary>
        /// 删除快速启动项的内容
        /// </summary>
        /// <param name="Item">要删除的项</param>
        /// <returns></returns>
        public void DeleteQuickStartItem(QuickStartItem Item)
        {
            if (Item != null)
            {
                using (SqliteCommand Command = new SqliteCommand("Delete From QuickStart Where Name = @Name And Protocal = @Protocol And FullPath = @FullPath And Type=@Type", Connection))
                {
                    Command.Parameters.AddWithValue("@Name", Item.DisplayName);
                    Command.Parameters.AddWithValue("@Protocol", Item.Protocol);
                    Command.Parameters.AddWithValue("@FullPath", Item.IconPath);
                    Command.Parameters.AddWithValue("@Type", Enum.GetName(typeof(QuickStartType), Item.Type));
                    Command.ExecuteNonQuery();
                }
            }
            else
            {
                throw new ArgumentNullException(nameof(Item), "Parameter could not be null");
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

                using (SqliteDataReader Query = Command.ExecuteReader())
                {
                    while (Query.Read())
                    {
                        HistoryList.Add(Query[0].ToString());
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

                StringBuilder DeleteCommandBuilder = new StringBuilder();

                foreach (string TableName in InputData.Select((Item) => Item.TableName))
                {
                    DeleteCommandBuilder.Append($"Delete From {TableName};");
                }

                using SqliteCommand Command = new SqliteCommand(DeleteCommandBuilder.ToString(), Connection, Transaction);

                Command.ExecuteNonQuery();

                List<SqliteParameter> Parameters = new List<SqliteParameter>();

                foreach ((string TableName, IEnumerable<object[]> Data) in InputData)
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
                            Command.Parameters.AddRange(Parameters.Take(RowData.Length));
                            Command.ExecuteNonQuery();
                        }
                    }
                }

                Transaction.Commit();
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
