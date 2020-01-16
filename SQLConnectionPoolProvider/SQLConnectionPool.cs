using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SQLConnectionPoolProvider
{
    /// <summary>
    /// 提供SQL数据库连接池内的连接对象
    /// </summary>
    public sealed class SQLConnection : IDisposable
    {
        internal DbConnection InnerConnection;

        internal bool IsDisposed = false;

        /// <summary>
        /// 指示该连接是否已经连接
        /// </summary>
        public bool IsConnected { get; private set; }

        /// <summary>
        /// 打开SQL连接对象成功时调用此构造函数
        /// </summary>
        /// <param name="InnerConnection">实际的SQL连接对象</param>
        internal SQLConnection(DbConnection InnerConnection)
        {
            this.InnerConnection = InnerConnection;
            IsConnected = true;
        }

        /// <summary>
        /// 打开SQL连接对象错误时调用此构造函数
        /// </summary>
        internal SQLConnection()
        {
            IsConnected = false;
        }

        /// <summary>
        /// 从SQL连接对象中获取SQL命令对象
        /// </summary>
        /// <typeparam name="T">SQL命令对象，具体因不同数据库而异</typeparam>
        /// <param name="CommandText">SQL命令</param>
        /// <returns>SQL命令对象</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Security", "CA2100:Review SQL queries for security vulnerabilities", Justification = "<挂起>")]
        public T CreateDbCommandFromConnection<T>(string CommandText) where T : DbCommand, new()
        {
            T Command = new T
            {
                Connection = InnerConnection,
                CommandText = CommandText
            };

            return Command;
        }

        /// <summary>
        /// 调用此方法使SQL数据库连接池能够可持续回收并管理SQL连接，请勿通过其他方法直接释放SQL连接对象
        /// </summary>
        public void Dispose()
        {
            IsDisposed = true;
        }
    }

    /// <summary>
    /// SQL数据库连接池管理类
    /// </summary>
    /// <typeparam name="T">指定数据库连接池管理的是哪一类数据库连接，T必须继承DbConnection且具有空构造函数</typeparam>
    public sealed class SQLConnectionPool<T> : IDisposable where T : DbConnection, new()
    {
        /// <summary>
        /// 允许的最大连接数量
        /// </summary>
        public ushort MaxConnections { get; private set; }

        /// <summary>
        /// 维持的最少连接数量
        /// </summary>
        public ushort MinConnections { get; private set; }

        /// <summary>
        /// 数据库连接字符串
        /// </summary>
        public string ConnectString { get; private set; }

        /// <summary>
        /// 指示是否是否进行初始化操作，包括产生MinConnections指定的连接数量
        /// </summary>
        private bool IsInitialized = false;

        /// <summary>
        /// 存储可供使用的数据库连接
        /// </summary>
        private List<SQLConnection> AvaliableConnectionPool;

        /// <summary>
        /// 存储正在使用的数据库连接
        /// </summary>
        private List<SQLConnection> UsingConnectionPool;

        private AutoResetEvent Locker = new AutoResetEvent(true);

        private bool IsDisposed = false;

        /// <summary>
        /// 连接池维护任务计时器，包括定期清除超出MinConnections指定数量的空闲连接，或回收UsingConnectionPool的连接
        /// </summary>
        private System.Timers.Timer MaintainTimer;

        /// <summary>
        /// 初始化SQLConnectionPool的实例
        /// </summary>
        /// <param name="ConnectString">数据库连接字符串</param>
        /// <param name="MaxConnections">最大连接数量，超过此数量的数据库连接请求将被排队直到空闲连接空出</param>
        /// <param name="MinConnections">最少连接数量，数据库连接池将始终保持大于或等于此值指定的数据库连接。若数据库总连接数超过此值且存在空闲连接，则一定时间后将自动关闭空闲连接</param>
        /// <param name="ConnnectionKeepAlivePeriod">数据库连接回收时间。当数据库连接总数大于最小值并且某一连接连续空闲时间超过此值则回收此连接。单位：毫秒；默认值：30s</param>
        public SQLConnectionPool(string ConnectString, ushort MaxConnections, ushort MinConnections, uint ConnnectionKeepAlivePeriod = 30000)
        {
            if (MaxConnections <= MinConnections)
            {
                throw new Exception("MaxConnections must lager than MinConnections");
            }

            if (string.IsNullOrWhiteSpace(ConnectString))
            {
                throw new Exception("ConnectString could not be empty or null");
            }

            this.MaxConnections = MaxConnections;
            this.MinConnections = MinConnections;
            this.ConnectString = ConnectString;
            AvaliableConnectionPool = new List<SQLConnection>(MaxConnections);
            UsingConnectionPool = new List<SQLConnection>(MaxConnections);

            MaintainTimer = new System.Timers.Timer
            {
                Interval = ConnnectionKeepAlivePeriod,
                AutoReset = true,
                Enabled = true
            };
            MaintainTimer.Elapsed += (s, e) =>
            {
                Locker.Reset();

                IEnumerable<SQLConnection> Connections = UsingConnectionPool.Where((Item) => Item.IsDisposed);
                while (Connections.Count() != 0)
                {
                    SQLConnection RecycleConnection = Connections.First();
                    RecycleConnection.IsDisposed = false;
                    AvaliableConnectionPool.Add(RecycleConnection);
                    UsingConnectionPool.Remove(RecycleConnection);
                }

                while (AvaliableConnectionPool.Count + UsingConnectionPool.Count > MinConnections && AvaliableConnectionPool.Count > 0)
                {
                    SQLConnection Item = AvaliableConnectionPool.First();
                    Item.InnerConnection.Close();
                    Item.InnerConnection.Dispose();
                    AvaliableConnectionPool.RemoveAt(0);
                }

                Locker.Set();
            };
        }

        /// <summary>
        /// 从数据库连接池从获取一个数据库连接
        /// </summary>
        /// <returns>数据库连接</returns>
        public Task<SQLConnection> GetConnectionFromDataBaseAsync()
        {
            return Task.Run(() =>
            {
                Locker.WaitOne();
                MaintainTimer.Stop();

                try
                {
                    if (IsInitialized)
                    {
                        IEnumerable<SQLConnection> Connections = UsingConnectionPool.Where((Item) => Item.IsDisposed);
                        while (Connections.Count() != 0)
                        {
                            SQLConnection RecycleConnection = Connections.First();
                            RecycleConnection.IsDisposed = false;
                            AvaliableConnectionPool.Add(RecycleConnection);
                            UsingConnectionPool.Remove(RecycleConnection);
                        }

                        if (AvaliableConnectionPool.Count == 0)
                        {
                            if (UsingConnectionPool.Count < MaxConnections)
                            {
                                DbConnection Connection = new T
                                {
                                    ConnectionString = ConnectString
                                };
                                Connection.Open();

                                SQLConnection AvailableItem = new SQLConnection(Connection);

                                UsingConnectionPool.Add(AvailableItem);

                                return AvailableItem;
                            }
                            else
                            {
                                while (UsingConnectionPool.All((Connection) => !Connection.IsDisposed) && !IsDisposed)
                                {
                                    Thread.Sleep(200);
                                }

                                IEnumerable<SQLConnection> ReConnections = UsingConnectionPool.Where((Item) => Item.IsDisposed);
                                while (Connections.Count() != 0)
                                {
                                    SQLConnection RecycleConnection = ReConnections.First();
                                    RecycleConnection.IsDisposed = false;
                                    AvaliableConnectionPool.Add(RecycleConnection);
                                    UsingConnectionPool.Remove(RecycleConnection);
                                }

                                SQLConnection AvaliableItem = AvaliableConnectionPool.First();
                                AvaliableConnectionPool.Remove(AvaliableItem);
                                UsingConnectionPool.Add(AvaliableItem);
                                return AvaliableItem;
                            }
                        }
                        else
                        {
                            SQLConnection AvaliableItem = AvaliableConnectionPool.First();
                            AvaliableConnectionPool.Remove(AvaliableItem);
                            UsingConnectionPool.Add(AvaliableItem);
                            return AvaliableItem;
                        }
                    }
                    else
                    {
                        IsInitialized = true;

                        if (MinConnections > 0)
                        {
                            for (int i = 0; i < MinConnections; i++)
                            {
                                DbConnection Connection = new T
                                {
                                    ConnectionString = ConnectString
                                };
                                Connection.Open();

                                AvaliableConnectionPool.Add(new SQLConnection(Connection));
                            }

                            SQLConnection AvaliableItem = AvaliableConnectionPool.First();
                            AvaliableConnectionPool.Remove(AvaliableItem);
                            UsingConnectionPool.Add(AvaliableItem);
                            return AvaliableItem;
                        }
                        else
                        {
                            DbConnection Connection = new T
                            {
                                ConnectionString = ConnectString
                            };
                            Connection.Open();

                            SQLConnection AvailableItem = new SQLConnection(Connection);

                            UsingConnectionPool.Add(AvailableItem);

                            return AvailableItem;
                        }
                    }
                }
                catch (Exception)
                {
                    return new SQLConnection();
                }
                finally
                {
                    MaintainTimer.Start();
                    Locker.Set();
                }
            });
        }

        /// <summary>
        /// 使用此方法关闭所有数据库连接，清空数据库连接池
        /// </summary>
        public void Dispose()
        {
            if (!IsDisposed)
            {
                IsDisposed = true;

                MaintainTimer.Stop();
                MaintainTimer.Dispose();

                foreach (var Connection in AvaliableConnectionPool.Concat(UsingConnectionPool))
                {
                    Connection.InnerConnection.Close();
                    Connection.InnerConnection.Dispose();
                }

                AvaliableConnectionPool.Clear();
                UsingConnectionPool.Clear();
                AvaliableConnectionPool = null;
                UsingConnectionPool = null;
                Locker.Dispose();
                Locker = null;
            }
        }
    }
}
