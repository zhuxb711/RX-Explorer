using Microsoft.Data.Sqlite;
using System;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.Background;
using Windows.Storage;
using Windows.Storage.Search;

namespace MaintenanceTask
{
    public sealed class MaintenanceTask : IBackgroundTask
    {
        private CancellationTokenSource Cancellation = new CancellationTokenSource();

        public async void Run(IBackgroundTaskInstance taskInstance)
        {
            BackgroundTaskDeferral Deferral = taskInstance.GetDeferral();
            taskInstance.Canceled += TaskInstance_Canceled;

            try
            {
                UpdateSQLite();

                await ClearUselessLogAsync(Cancellation.Token);

                //The following code is used to update the globalization problem of the ContextMenu in the old version
                if (!ApplicationData.Current.LocalSettings.Values.ContainsKey("GlobalizationStringForContextMenu"))
                {
                    if (ApplicationData.Current.LocalSettings.Values["LanguageOverride"] is int LanguageIndex)
                    {
                        switch (LanguageIndex)
                        {
                            case 0:
                                {
                                    ApplicationData.Current.LocalSettings.Values["GlobalizationStringForContextMenu"] = "使用RX文件管理器打开";
                                    break;
                                }
                            case 1:
                                {
                                    ApplicationData.Current.LocalSettings.Values["GlobalizationStringForContextMenu"] = "Open in RX-Explorer";
                                    break;
                                }
                            case 2:
                                {
                                    ApplicationData.Current.LocalSettings.Values["GlobalizationStringForContextMenu"] = "Ouvrir dans RX-Explorer";
                                    break;
                                }
                            case 3:
                                {
                                    ApplicationData.Current.LocalSettings.Values["GlobalizationStringForContextMenu"] = "使用RX文件管理器打開";
                                    break;
                                }
                            case 4:
                                {
                                    ApplicationData.Current.LocalSettings.Values["GlobalizationStringForContextMenu"] = "Abrir con RX-Explorer";
                                    break;
                                }
                            case 5:
                                {
                                    ApplicationData.Current.LocalSettings.Values["GlobalizationStringForContextMenu"] = "Öffnen Sie im RX-Explorer";
                                    break;
                                }
                        }
                    }
                }

                //To-Do: Do more things as needed when users update the app to newer version
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"An exception threw in MaintenanceTask, message: {ex.Message}");
            }
            finally
            {
                taskInstance.Canceled -= TaskInstance_Canceled;
                Cancellation?.Dispose();
                Cancellation = null;
                Deferral.Complete();
            }
        }

        private async Task ClearUselessLogAsync(CancellationToken CancelToken = default)
        {
            try
            {
                StorageFileQueryResult Query = ApplicationData.Current.TemporaryFolder.CreateFileQueryWithOptions(new QueryOptions(CommonFileQuery.DefaultQuery, new string[] { ".txt" })
                {
                    IndexerOption = IndexerOption.DoNotUseIndexer,
                    FolderDepth = FolderDepth.Shallow,
                    ApplicationSearchFilter = "System.FileName:~<\"Log_GeneratedTime\" AND System.Size:>0"
                });

                foreach (StorageFile File in from StorageFile File in await Query.GetFilesAsync()
                                             let Mat = Regex.Match(File.Name, @"(?<=\[)(.+)(?=\])")
                                             where Mat.Success && DateTime.TryParseExact(Mat.Value, "yyyy-MM-dd HH-mm-ss.fff", CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out _)
                                             select File)
                {
                    await File.DeleteAsync(StorageDeleteOption.PermanentDelete);

                    if (CancelToken.IsCancellationRequested)
                    {
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"An exception was threw in {nameof(ClearUselessLogAsync)}, message: {ex.Message}");
            }
        }

        private SqliteConnection GetSQLConnection()
        {
            try
            {
                SQLitePCL.Batteries_V2.Init();
                SQLitePCL.raw.sqlite3_win32_set_directory(1, ApplicationData.Current.LocalFolder.Path);
                SQLitePCL.raw.sqlite3_win32_set_directory(2, ApplicationData.Current.TemporaryFolder.Path);

                SqliteConnection Connection = new SqliteConnection("Filename=RX_Sqlite.db;");
                Connection.Open();

                return Connection;
            }
            catch
            {
                return null;
            }
        }

        private void UpdateSQLite()
        {
            try
            {
                using SqliteConnection Connection = GetSQLConnection();
                using SqliteTransaction Transaction = Connection.BeginTransaction();

                StringBuilder Builder = new StringBuilder()
                                        .Append("Update FileColor Set Color = '#22B324' Where Color = '#FFADFF2F';")
                                        .Append("Update FileColor Set Color = '#CC6EFF' Where Color = '#FFFFFF00';")
                                        .Append("Update FileColor Set Color = '#42C5FF' Where Color = '#FF409EFE';")
                                        .Append("Delete From ProgramPicker Where FileType = '.*';");

                bool HasGroupColumnColumn = false;
                bool HasGroupDirectionColumn = false;

                using (SqliteCommand Command = new SqliteCommand("PRAGMA table_info('PathConfiguration')", Connection, Transaction))
                using (SqliteDataReader Reader = Command.ExecuteReader())
                {
                    while (Reader.Read())
                    {
                        switch (Convert.ToString(Reader[1]))
                        {
                            case "GroupColumn":
                                HasGroupColumnColumn = true;
                                break;
                            case "GroupDirection":
                                HasGroupDirectionColumn = true;
                                break;
                        }
                    }
                }

                if (!HasGroupColumnColumn)
                {
                    Builder.Append("Alter Table PathConfiguration Add GroupColumn Text Default 'None' Check(GroupColumn In ('None','Name','ModifiedTime','Type','Size'));");
                }

                if (!HasGroupDirectionColumn)
                {
                    Builder.Append("Alter Table PathConfiguration Add GroupDirection Text Default 'Ascending' Check(GroupDirection In ('Ascending','Descending'));");
                }


                bool HasIsDefaultColumn = false;
                bool HasIsRecommandColumn = false;

                using (SqliteCommand Command = new SqliteCommand("PRAGMA table_info('ProgramPicker')", Connection, Transaction))
                using (SqliteDataReader Reader = Command.ExecuteReader())
                {
                    while (Reader.Read())
                    {
                        switch (Convert.ToString(Reader[1]))
                        {
                            case "IsRecommanded":
                                HasIsRecommandColumn = true;
                                break;
                            case "IsDefault":
                                HasIsDefaultColumn = true;
                                break;
                        }
                    }
                }

                if (!HasIsDefaultColumn)
                {
                    Builder.AppendLine("Alter Table ProgramPicker Add Column IsDefault Text Default 'False' Check(IsDefault In ('True','False'));");
                }

                if (!HasIsRecommandColumn)
                {
                    Builder.AppendLine("Alter Table ProgramPicker Add Column IsRecommanded Text Default 'False' Check(IsDefault In ('True','False'));");
                }

                using (SqliteCommand Command = new SqliteCommand(Builder.ToString(), Connection, Transaction))
                {
                    Command.ExecuteNonQuery();
                }

                Transaction.Commit();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"An exception was threw in {nameof(UpdateSQLite)}, message: {ex.Message}");
            }
        }

        private void TaskInstance_Canceled(IBackgroundTaskInstance sender, BackgroundTaskCancellationReason reason)
        {
            Cancellation?.Cancel();
        }
    }
}
