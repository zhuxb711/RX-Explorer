using Microsoft.Data.Sqlite;
using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.Background;
using Windows.Storage;
using Windows.Storage.Search;

namespace MaintenanceTask
{
    public sealed class MaintenanceTask : IBackgroundTask
    {
        public async void Run(IBackgroundTaskInstance taskInstance)
        {
            BackgroundTaskDeferral Deferral = taskInstance.GetDeferral();

            try
            {
                using (CancellationTokenSource Cancellation = new CancellationTokenSource())
                {
                    taskInstance.Canceled += (s, e) =>
                    {
                        Cancellation.Cancel();
                    };

                    await Task.WhenAll(UpdateSystemLaunchHelperAsync(Cancellation.Token),
                                       UpdateSQLiteAsync(Cancellation.Token),
                                       ClearTemporaryFolderAsync(Cancellation.Token));
                }
            }
            catch (Exception ex)
            {
#if DEBUG
                if (Debugger.IsAttached)
                {
                    Debugger.Break();
                }
                else
                {
                    Debugger.Launch();
                }
#endif

                Debug.WriteLine($"An exception threw in {nameof(MaintenanceTask.Run)}, message: {ex.Message}");
            }
            finally
            {
                Deferral.Complete();
            }
        }

        private async Task UpdateSystemLaunchHelperAsync(CancellationToken CancelToken = default)
        {
#if !DEBUG
            await Task.CompletedTask;
#else
            StorageFolder SourceFolder = await StorageFolder.GetFolderFromPathAsync(Path.Combine(Windows.ApplicationModel.Package.Current.InstalledPath, "SystemLaunchHelper"));
            StorageFolder LocalAppDataFolder = await StorageFolder.GetFolderFromPathAsync(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));
            StorageFolder TargetFolder = await LocalAppDataFolder.CreateFolderAsync("RX-Explorer_Launch_Helper", CreationCollisionOption.ReplaceExisting);

            if (!CancelToken.IsCancellationRequested)
            {
                await CopyFolderAsync(SourceFolder, TargetFolder, CancelToken);
            }
#endif
        }

        private async Task CopyFolderAsync(StorageFolder From, StorageFolder To, CancellationToken CancelToken = default)
        {
            StorageItemQueryResult Query = From.CreateItemQueryWithOptions(new QueryOptions
            {
                FolderDepth = FolderDepth.Shallow,
                IndexerOption = IndexerOption.DoNotUseIndexer
            });

            foreach (IStorageItem Item in await Query.GetItemsAsync())
            {
                if (CancelToken.IsCancellationRequested)
                {
                    break;
                }

                switch (Item)
                {
                    case StorageFolder SubFolder:
                        {
                            await CopyFolderAsync(SubFolder, await To.CreateFolderAsync(SubFolder.Name, CreationCollisionOption.ReplaceExisting));
                            break;
                        }
                    case StorageFile SubFile:
                        {
                            await SubFile.CopyAsync(To, SubFile.Name, NameCollisionOption.ReplaceExisting);
                            break;
                        }
                }
            }
        }

        private async Task ClearTemporaryFolderAsync(CancellationToken CancelToken = default)
        {
            try
            {
                await ApplicationData.Current.ClearAsync(ApplicationDataLocality.Temporary);
            }
            catch (Exception)
            {
                StorageItemQueryResult Query = ApplicationData.Current.TemporaryFolder.CreateItemQueryWithOptions(new QueryOptions
                {
                    IndexerOption = IndexerOption.DoNotUseIndexer,
                    FolderDepth = FolderDepth.Shallow
                });

                foreach (IStorageItem Item in await Query.GetItemsAsync())
                {
                    if (CancelToken.IsCancellationRequested)
                    {
                        break;
                    }

                    await Item.DeleteAsync(StorageDeleteOption.PermanentDelete);
                }
            }
        }

        private SqliteConnection GetSQLConnection()
        {
            SqliteConnectionStringBuilder Builder = new SqliteConnectionStringBuilder
            {
                DataSource = Path.Combine(ApplicationData.Current.LocalFolder.Path, "RX_Sqlite.db"),
                Mode = SqliteOpenMode.ReadWrite,
                Cache = SqliteCacheMode.Default
            };

            SqliteConnection Connection = new SqliteConnection(Builder.ToString());

            Connection.Open();

            return Connection;
        }

        private Task UpdateSQLiteAsync(CancellationToken CancelToken = default)
        {
            return Task.Run(() =>
            {
                using SqliteConnection Connection = GetSQLConnection();
                using SqliteTransaction Transaction = Connection.BeginTransaction();

                StringBuilder Builder = new StringBuilder("Delete From ProgramPicker Where FileType = '.*';");

                using (SqliteCommand Command = new SqliteCommand("Select Count(*) From sqlite_master Where type = \"table\" And name = \"FileColor\"", Connection, Transaction))
                {
                    if (Convert.ToInt32(Command.ExecuteScalar()) > 0)
                    {
                        Builder.Append("Insert Or Ignore Into FileTag (Path, ColorTag) Select Path, Color From FileColor;")
                               .Append("Update FileTag Set ColorTag = 'Blue' Where ColorTag = '#FF42C5FF';")
                               .Append("Update FileTag Set ColorTag = 'Green' Where ColorTag = '#FF22B324';")
                               .Append("Update FileTag Set ColorTag = 'Orange' Where ColorTag = '#FFFFA500';")
                               .Append("Update FileTag Set ColorTag = 'Purple' Where ColorTag = '#FFCC6EFF';")
                               .Append("Drop Table FileColor;");
                    }
                }

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

                if (!CancelToken.IsCancellationRequested)
                {
                    using (SqliteCommand Command = new SqliteCommand(Builder.ToString(), Connection, Transaction))
                    {
                        Command.ExecuteNonQuery();
                    }

                    Transaction.Commit();
                }
            });
        }
    }
}
