using MySqlConnector;
using NetworkAccess;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.Storage;

namespace RX_Explorer.Class
{
    /// <summary>
    /// 提供对MySQL数据库的访问支持
    /// </summary>
    public sealed class MySQL : IAsyncDisposable, IDisposable
    {
        private volatile static MySQL Instance;

        private bool IsDisposed;

        private static readonly object Locker = new object();

        private MySqlConnection Connection;

        /// <summary>
        /// 提供对MySQL实例的访问
        /// </summary>
        public static MySQL Current
        {
            get
            {
                lock (Locker)
                {
                    return Instance ??= new MySQL();
                }
            }
        }

        /// <summary>
        /// 初始化MySQL实例
        /// </summary>
        private MySQL()
        {
            Connection = new MySqlConnection(SecureAccessProvider.GetMySQLAccessCredential(Package.Current));
        }

        /// <summary>
        /// 从数据库连接池中获取连接对象
        /// </summary>
        /// <returns></returns>
        public async Task<bool> MakeConnectionUseable()
        {
            try
            {
                if (!await Connection.PingAsync().ConfigureAwait(false))
                {
                    await Connection.CloseAsync().ConfigureAwait(false);
                    await Connection.OpenAsync().ConfigureAwait(false);
                }

                #region MySQL数据库存储过程和触发器初始化代码，仅首次运行时需要
                //StringBuilder Builder = new StringBuilder();
                //Builder.AppendLine("Create Table If Not Exists FeedBackTable (UserName Text Not Null, Title Text Not Null, Suggestion Text Not Null, LikeNum Text Not Null, DislikeNum Text Not Null, UserID Text Not Null, GUID Text Not Null);")
                //       .AppendLine("Create Table If Not Exists VoteRecordTable (UserID Text Not Null, GUID Text Not Null, Behavior Text Not Null);")

                //       .AppendLine("Drop Trigger If Exists RemoveVoteRecordTrigger;")
                //       .AppendLine("Create Trigger RemoveVoteRecordTrigger After Delete On FeedBackTable For Each Row Delete From VoteRecordTable Where GUID=old.GUID;")

                //       .AppendLine("Drop Procedure If Exists GetFeedBackProcedure;")
                //       .AppendLine("Create Procedure GetFeedBackProcedure(IN Para Text)")
                //       .AppendLine("Begin")
                //       .AppendLine("Declare EndSignal int Default 0;")
                //       .AppendLine("Declare P1 Text;")
                //       .AppendLine("Declare P2 Text;")
                //       .AppendLine("Declare P3 Text;")
                //       .AppendLine("Declare P4 Text;")
                //       .AppendLine("Declare P5 Text;")
                //       .AppendLine("Declare P6 Text;")
                //       .AppendLine("Declare P7 Text;")
                //       .AppendLine("Declare P8 Text;")
                //       .AppendLine("Declare RowData Cursor For Select * From FeedBackTable;")
                //       .AppendLine("Declare Continue Handler For Not Found Set EndSignal=1;")
                //       .AppendLine("Drop Table If Exists DataTemporary;")
                //       .AppendLine("Create Temporary Table DataTemporary (UserName Text, Title Text, Suggestion Text, LikeNum Text, DislikeNum Text, UserID Text, GUID Text, Behavior Text);")
                //       .AppendLine("Open RowData;")
                //       .AppendLine("Fetch RowData Into P1,P2,P3,P4,P5,P6,P7;")
                //       .AppendLine("While EndSignal<>1 Do")
                //       .AppendLine("If (Select Count(*) From VoteRecordTable Where UserID=Para And GUID=P7) <> 0")
                //       .AppendLine("Then")
                //       .AppendLine("Select Behavior Into P8 From VoteRecordTable Where UserID=Para And GUID=P7;")
                //       .AppendLine("Else")
                //       .AppendLine("Set P8 = 'NULL';")
                //       .AppendLine("End If;")
                //       .AppendLine("Insert Into DataTemporary Values (P1,P2,P3,P4,P5,P6,P7,P8);")
                //       .AppendLine("Fetch RowData Into P1,P2,P3,P4,P5,P6,P7;")
                //       .AppendLine("End While;")
                //       .AppendLine("Close RowData;")
                //       .AppendLine("Select * From DataTemporary;")
                //       .AppendLine("End;")

                //       .AppendLine("Drop Procedure If Exists UpdateFeedBackVoteProcedure;")
                //       .AppendLine("Create Procedure UpdateFeedBackVoteProcedure(IN LNum Text,IN DNum Text,IN UID Text,IN GID Text,IN Beh Text)")
                //       .AppendLine("Begin")
                //       .AppendLine("Update FeedBackTable Set LikeNum=LNum, DislikeNum=DNum Where GUID=GID;")
                //       .AppendLine("If (Select Count(*) From VoteRecordTable Where UserID=UID And GUID=GID) <> 0")
                //       .AppendLine("Then")
                //       .AppendLine("If Beh <> '='")
                //       .AppendLine("Then")
                //       .AppendLine("Update VoteRecordTable Set Behavior=Beh Where UserID=UID And GUID=GID;")
                //       .AppendLine("Else")
                //       .AppendLine("Delete From VoteRecordTable Where UserID=UID And GUID=GID;")
                //       .AppendLine("End If;")
                //       .AppendLine("Else")
                //       .AppendLine("If Beh <> '='")
                //       .AppendLine("Then")
                //       .AppendLine("Insert Into VoteRecordTable Values (UID,GID,Beh);")
                //       .AppendLine("End If;")
                //       .AppendLine("End If;")
                //       .AppendLine("End;");
                //using (MySqlCommand Command = new MySqlCommand(Builder.ToString(), Connection))
                //{
                //    await Command.ExecuteNonQueryAsync().ConfigureAwait(false);
                //}
                #endregion

                return Connection.State == ConnectionState.Open;
            }
            catch (Exception ex)
            {
                LogTracer.Log(ex, "Could not make sure mysql connection available");
                return false;
            }
        }

        /// <summary>
        /// 获取所有反馈对象
        /// </summary>
        /// <returns></returns>
        public async IAsyncEnumerable<FeedBackItem> GetAllFeedBackAsync()
        {
            if (await MakeConnectionUseable().ConfigureAwait(false))
            {
                using (MySqlCommand Command = new MySqlCommand("GetFeedBackProcedure", Connection))
                {
                    Command.CommandType = CommandType.StoredProcedure;
                    Command.Parameters.AddWithValue("Para", ApplicationData.Current.LocalSettings.Values["SystemUserID"].ToString());

                    using (DbDataReader Reader = await Command.ExecuteReaderAsync().ConfigureAwait(false))
                    {
                        while (await Reader.ReadAsync().ConfigureAwait(false))
                        {
                            if (Reader["Behavior"].ToString() != "NULL")
                            {
                                yield return new FeedBackItem(Reader["UserName"].ToString(), Reader["Title"].ToString(), Reader["Suggestion"].ToString(), Reader["LikeNum"].ToString(), Reader["DislikeNum"].ToString(), Reader["UserID"].ToString(), Reader["GUID"].ToString(), Reader["Behavior"].ToString());
                            }
                            else
                            {
                                yield return new FeedBackItem(Reader["UserName"].ToString(), Reader["Title"].ToString(), Reader["Suggestion"].ToString(), Reader["LikeNum"].ToString(), Reader["DislikeNum"].ToString(), Reader["UserID"].ToString(), Reader["GUID"].ToString());
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

        /// <summary>
        /// 更新反馈对象的投票信息
        /// </summary>
        /// <param name="Item">反馈对象</param>
        /// <returns></returns>
        public async Task<bool> UpdateFeedBackVoteAsync(FeedBackItem Item)
        {
            if (Item != null)
            {
                if (await MakeConnectionUseable().ConfigureAwait(false))
                {
                    try
                    {
                        using (MySqlCommand Command = new MySqlCommand("UpdateFeedBackVoteProcedure", Connection))
                        {
                            Command.CommandType = CommandType.StoredProcedure;
                            Command.Parameters.AddWithValue("LNum", Item.LikeNum);
                            Command.Parameters.AddWithValue("DNum", Item.DislikeNum);
                            Command.Parameters.AddWithValue("Beh", Item.UserVoteAction);
                            Command.Parameters.AddWithValue("GID", Item.GUID);
                            Command.Parameters.AddWithValue("UID", ApplicationData.Current.LocalSettings.Values["SystemUserID"].ToString());
                            await Command.ExecuteNonQueryAsync().ConfigureAwait(false);
                        }
                        return true;
                    }
                    catch (Exception ex)
                    {
                        LogTracer.Log(ex, $"An error was threw in {nameof(UpdateFeedBackVoteAsync)}");
                        return false;
                    }
                }
                else
                {
                    return false;
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
        public async Task<bool> UpdateFeedBackAsync(string Title, string Suggestion, string Guid)
        {
            if (await MakeConnectionUseable().ConfigureAwait(false))
            {
                try
                {
                    using (MySqlCommand Command = new MySqlCommand("Update FeedBackTable Set Title=@NewTitle, Suggestion=@NewSuggestion Where GUID=@GUID", Connection))
                    {
                        Command.Parameters.AddWithValue("@NewTitle", Title);
                        Command.Parameters.AddWithValue("@NewSuggestion", Suggestion);
                        Command.Parameters.AddWithValue("@GUID", Guid);
                        await Command.ExecuteNonQueryAsync().ConfigureAwait(false);
                    }

                    return true;
                }
                catch (Exception ex)
                {
                    LogTracer.Log(ex, $"An error was threw in { nameof(UpdateFeedBackAsync)}");
                    return false;
                }
            }
            else
            {
                return false;
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
                if (await MakeConnectionUseable().ConfigureAwait(false))
                {
                    try
                    {
                        using (MySqlCommand Command = new MySqlCommand("Delete From FeedBackTable Where GUID=@GUID", Connection))
                        {
                            Command.Parameters.AddWithValue("@GUID", Item.GUID);
                            await Command.ExecuteNonQueryAsync().ConfigureAwait(false);
                        }

                        return true;
                    }
                    catch (Exception ex)
                    {
                        LogTracer.Log(ex, $"An error was threw in { nameof(DeleteFeedBackAsync)}");
                        return false;
                    }
                }
                else
                {
                    return false;
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
                if (await MakeConnectionUseable().ConfigureAwait(false))
                {
                    try
                    {
                        using (MySqlCommand Command = new MySqlCommand("Insert Into FeedBackTable Values (@UserName,@Title,@Suggestion,@Like,@Dislike,@UserID,@GUID)", Connection))
                        {
                            Command.Parameters.AddWithValue("@UserName", Item.UserName);
                            Command.Parameters.AddWithValue("@Title", Item.Title);
                            Command.Parameters.AddWithValue("@Suggestion", Item.Suggestion);
                            Command.Parameters.AddWithValue("@Like", Item.LikeNum);
                            Command.Parameters.AddWithValue("@Dislike", Item.DislikeNum);
                            Command.Parameters.AddWithValue("@UserID", Item.UserID);
                            Command.Parameters.AddWithValue("@GUID", Item.GUID);
                            await Command.ExecuteNonQueryAsync().ConfigureAwait(false);
                        }

                        return true;
                    }
                    catch (Exception ex)
                    {
                        LogTracer.Log(ex, $"An error was threw in { nameof(SetFeedBackAsync)}");
                        return false;
                    }
                }
                else
                {
                    return false;
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
        public async ValueTask DisposeAsync()
        {
            if (!IsDisposed)
            {
                IsDisposed = true;
                Instance = null;

                await Connection.DisposeAsync();
                Connection = null;

                GC.SuppressFinalize(this);
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
                Instance = null;

                Connection.Dispose();
                Connection = null;

                GC.SuppressFinalize(this);
            }
        }

        ~MySQL()
        {
            Dispose();
        }
    }
}
