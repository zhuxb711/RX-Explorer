using Microsoft.Data.Sqlite;
using System;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.Background;
using Windows.Storage;

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
                await ClearUselessLogTask(Cancellation.Token).ConfigureAwait(true);

                using (SqliteConnection Connection = GetSQLConnection())
                {
                    await ClearAddressBarHistory(Connection).ConfigureAwait(true);
                    await UpdateProgramPickerTable(Connection).ConfigureAwait(true);
                }

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

        private async Task ClearUselessLogTask(CancellationToken CancelToken = default)
        {
            try
            {
                foreach (StorageFile File in from StorageFile File in await ApplicationData.Current.TemporaryFolder.GetFilesAsync()
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
                Debug.WriteLine($"An exception was threw in {nameof(ClearUselessLogTask)}, message: {ex.Message}");
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

        //Clear history for addressbar and keep only 25 items
        private async Task ClearAddressBarHistory(SqliteConnection Connection)
        {
            try
            {
                using (SqliteCommand Command = new SqliteCommand("Delete From PathHistory Where rowid > 25", Connection))
                {
                    await Command.ExecuteNonQueryAsync().ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"An exception was threw in {nameof(ClearAddressBarHistory)}, message: {ex.Message}");
            }
        }

        private async Task UpdateProgramPickerTable(SqliteConnection Connection)
        {
            try
            {
                using (SqliteCommand Command = new SqliteCommand("PRAGMA table_info('ProgramPicker')", Connection))
                {
                    using (SqliteDataReader Reader = await Command.ExecuteReaderAsync().ConfigureAwait(false))
                    {
                        while (Reader.Read())
                        {
                            if (Convert.ToString(Reader[1]) == "IsDefault")
                            {
                                return;
                            }
                        }
                    }
                }

                using (SqliteCommand Command = new SqliteCommand("Alter Table ProgramPicker Add Column IsDefault Text", Connection))
                {
                    await Command.ExecuteNonQueryAsync().ConfigureAwait(false);
                }

                using (SqliteCommand Command = new SqliteCommand("Delete From ProgramPicker Where FileType = '.*'", Connection))
                {
                    await Command.ExecuteNonQueryAsync().ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"An exception was threw in {nameof(UpdateProgramPickerTable)}, message: {ex.Message}");
            }
        }

        private void TaskInstance_Canceled(IBackgroundTaskInstance sender, BackgroundTaskCancellationReason reason)
        {
            Cancellation?.Cancel();
        }
    }
}
