using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.Background;
using Windows.Storage;
using Windows.Storage.Search;
using Windows.UI.StartScreen;

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
                                       ClearTemporaryFolderAsync(Cancellation.Token),
                                       RefreshJumpListAsync(Cancellation.Token));
                }
            }
            catch (OperationCanceledException)
            {
                // No need to handle this exception
            }
            catch (Exception)
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
            }
            finally
            {
                Deferral.Complete();
            }
        }

        private async Task UpdateSystemLaunchHelperAsync(CancellationToken CancelToken = default)
        {
            if (Convert.ToBoolean(ApplicationData.Current.LocalSettings.Values["InterceptDesktopFolder"])
                || Convert.ToBoolean(ApplicationData.Current.LocalSettings.Values["InterceptWindowsE"]))
            {
                StorageFolder SourceFolder = await StorageFolder.GetFolderFromPathAsync(Path.Combine(Windows.ApplicationModel.Package.Current.InstalledPath, "SystemLaunchHelper"));
                StorageFolder LocalAppDataFolder = await StorageFolder.GetFolderFromPathAsync(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));
                StorageFolder TargetFolder = await LocalAppDataFolder.CreateFolderAsync("RX-Explorer_Launch_Helper", CreationCollisionOption.ReplaceExisting);

                if (!CancelToken.IsCancellationRequested)
                {
                    await CopyFolderAsync(SourceFolder, TargetFolder, CancelToken);

                    StorageFile VersionLockFile = await TargetFolder.CreateFileAsync("Version.lock", CreationCollisionOption.ReplaceExisting);

                    using (Stream FileStream = await VersionLockFile.OpenStreamForWriteAsync())
                    using (StreamWriter Writer = new StreamWriter(FileStream, Encoding.UTF8, 128, true))
                    {
                        await Writer.WriteLineAsync($"{Windows.ApplicationModel.Package.Current.Id.Version.Major}.{Windows.ApplicationModel.Package.Current.Id.Version.Minor}.{Windows.ApplicationModel.Package.Current.Id.Version.Build}.{Windows.ApplicationModel.Package.Current.Id.Version.Revision}");
                        await Writer.FlushAsync();
                    }
                }
            }
        }

        private async Task CopyFolderAsync(StorageFolder From, StorageFolder To, CancellationToken CancelToken = default)
        {
            const uint FetchItemEachNum = 50;

            StorageItemQueryResult Query = From.CreateItemQueryWithOptions(new QueryOptions
            {
                FolderDepth = FolderDepth.Shallow,
                IndexerOption = IndexerOption.DoNotUseIndexer
            });

            for (uint Index = 0; !CancelToken.IsCancellationRequested; Index += FetchItemEachNum)
            {
                IReadOnlyList<IStorageItem> StorageItemList = await Query.GetItemsAsync(Index, FetchItemEachNum);

                if (StorageItemList.Count == 0)
                {
                    break;
                }

                foreach (IStorageItem Item in StorageItemList)
                {
                    CancelToken.ThrowIfCancellationRequested();

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
        }

        private async Task ClearTemporaryFolderAsync(CancellationToken CancelToken = default)
        {
            try
            {
                await ApplicationData.Current.ClearAsync(ApplicationDataLocality.Temporary);
            }
            catch (Exception)
            {
                const uint FetchItemEachNum = 50;

                StorageItemQueryResult Query = ApplicationData.Current.TemporaryFolder.CreateItemQueryWithOptions(new QueryOptions
                {
                    IndexerOption = IndexerOption.DoNotUseIndexer,
                    FolderDepth = FolderDepth.Shallow
                });

                for (uint Index = 0; !CancelToken.IsCancellationRequested; Index += FetchItemEachNum)
                {
                    IReadOnlyList<IStorageItem> StorageItemList = await Query.GetItemsAsync(Index, FetchItemEachNum);

                    if (StorageItemList.Count == 0)
                    {
                        break;
                    }

                    foreach (IStorageItem Item in StorageItemList)
                    {
                        CancelToken.ThrowIfCancellationRequested();

                        await Item.DeleteAsync(StorageDeleteOption.PermanentDelete);
                    }
                }
            }
        }

        private async Task UpdateSQLiteAsync(CancellationToken CancelToken = default)
        {
            if (await ApplicationData.Current.LocalFolder.TryGetItemAsync("RX_Sqlite.db").AsTask().AsCancellable(CancelToken) is StorageFile DBFile)
            {
                await Task.Run(() =>
                {
                    SqliteConnectionStringBuilder ConnectionBuilder = new SqliteConnectionStringBuilder
                    {
                        DataSource = DBFile.Path,
                        Mode = SqliteOpenMode.ReadWrite,
                        Cache = SqliteCacheMode.Private
                    };

                    using (SqliteConnection Connection = new SqliteConnection(ConnectionBuilder.ToString()))
                    {
                        Connection.Open();

                        using (SqliteTransaction Transaction = Connection.BeginTransaction())
                        {
                            StringBuilder QueryBuilder = new StringBuilder("Delete From ProgramPicker Where FileType = '.*';");

                            using (SqliteCommand Command = new SqliteCommand("Select Count(*) From sqlite_master Where type = \"table\" And name = \"FileColor\"", Connection, Transaction))
                            {
                                if (Convert.ToInt32(Command.ExecuteScalar()) > 0)
                                {
                                    QueryBuilder.Append("Insert Or Ignore Into FileTag (Path, ColorTag) Select Path, Color From FileColor;")
                                           .Append("Update FileTag Set ColorTag = 'Blue' Where ColorTag = '#FF42C5FF';")
                                           .Append("Update FileTag Set ColorTag = 'Green' Where ColorTag = '#FF22B324';")
                                           .Append("Update FileTag Set ColorTag = 'Orange' Where ColorTag = '#FFFFA500';")
                                           .Append("Update FileTag Set ColorTag = 'Purple' Where ColorTag = '#FFCC6EFF';")
                                           .Append("Drop Table FileColor;");
                                }
                            }

                            QueryBuilder.Append("Create Table If Not Exists PathTagMapping (Path Text Not Null Collate NoCase, Label Text Not Null, Primary Key (Path));");
                            QueryBuilder.Append("Update PathTagMapping Set Label = 'None' Where Label = 'Transparent';");
                            QueryBuilder.Append("Delete From PathTagMapping Where Label = 'None';");

                            using (SqliteCommand Command = new SqliteCommand("Select Count(*) From sqlite_master Where type = \"table\" And name = \"FileTag\"", Connection, Transaction))
                            {
                                if (Convert.ToInt32(Command.ExecuteScalar()) > 0)
                                {
                                    QueryBuilder.Append("Insert Or Ignore Into PathTagMapping (Path, Label) Select Path, ColorTag From FileTag;")
                                           .Append("Update PathTagMapping Set Label = 'None' Where Label = 'Transparent';")
                                           .Append("Update PathTagMapping Set Label = 'PredefineLabel1' Where Label = 'Blue';")
                                           .Append("Update PathTagMapping Set Label = 'PredefineLabel2' Where Label = 'Green';")
                                           .Append("Update PathTagMapping Set Label = 'PredefineLabel3' Where Label = 'Orange';")
                                           .Append("Update PathTagMapping Set Label = 'PredefineLabel4' Where Label = 'Purple';")
                                           .Append("Drop Table FileTag;");
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
                                QueryBuilder.Append("Alter Table PathConfiguration Add GroupColumn Text Default 'None' Check(GroupColumn In ('None','Name','ModifiedTime','Type','Size'));");
                            }

                            if (!HasGroupDirectionColumn)
                            {
                                QueryBuilder.Append("Alter Table PathConfiguration Add GroupDirection Text Default 'Ascending' Check(GroupDirection In ('Ascending','Descending'));");
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
                                QueryBuilder.AppendLine("Alter Table ProgramPicker Add Column IsDefault Text Default 'False' Check(IsDefault In ('True','False'));");
                            }

                            if (!HasIsRecommandColumn)
                            {
                                QueryBuilder.AppendLine("Alter Table ProgramPicker Add Column IsRecommanded Text Default 'False' Check(IsDefault In ('True','False'));");
                            }

                            if (!CancelToken.IsCancellationRequested)
                            {
                                using (SqliteCommand Command = new SqliteCommand(QueryBuilder.ToString(), Connection, Transaction))
                                {
                                    Command.ExecuteNonQuery();
                                }

                                Transaction.Commit();
                            }
                        }
                    }
                });
            }
        }

        private async Task RefreshJumpListAsync(CancellationToken CancelToken = default)
        {
            if (JumpList.IsSupported())
            {
                JumpList CurrentJumpList = await JumpList.LoadCurrentAsync().AsTask().AsCancellable(CancelToken);

                foreach (JumpListItem OldItem in CurrentJumpList.Items.ToArray())
                {
                    JumpListItem NewItem = JumpListItem.CreateWithArguments(OldItem.Arguments, OldItem.DisplayName);

                    NewItem.Description = OldItem.Arguments;
                    NewItem.GroupName = OldItem.GroupName;
                    NewItem.Logo = OldItem.Logo;

                    CurrentJumpList.Items[CurrentJumpList.Items.IndexOf(OldItem)] = NewItem;
                }

                await CurrentJumpList.SaveAsync().AsTask().AsCancellable(CancelToken);
            }
        }
    }
}
