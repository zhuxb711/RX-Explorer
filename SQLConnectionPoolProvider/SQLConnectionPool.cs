using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SQLConnectionPoolProvider
{
    public sealed class SQLConnection : IDisposable
    {
        internal DbConnection InnerConnection;

        internal bool IsDisposed = false;

        internal SQLConnection(DbConnection InnerConnection)
        {
            this.InnerConnection = InnerConnection;
        }

        private SQLConnection()
        {

        }

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

        public void Dispose()
        {
            IsDisposed = true;
        }
    }

    public sealed class SQLConnectionPool<T> : IDisposable where T : DbConnection, new()
    {
        public ushort MaxConnections { get; private set; }

        public ushort MinConnections { get; private set; }

        public string ConnectString { get; private set; }

        private bool IsInitialized = false;

        private List<SQLConnection> AvaliableConnectionPool;

        private List<SQLConnection> UsingConnectionPool;

        private AutoResetEvent Locker = new AutoResetEvent(true);

        private bool IsDisposed = false;

        private System.Timers.Timer MaintainTimer;

        public SQLConnectionPool(string ConnectString, ushort MaxConnections, ushort MinConnections)
        {
            if (MaxConnections <= MinConnections)
            {
                throw new Exception("MaxConnections must lager than MinConnections");
            }

            if(string.IsNullOrWhiteSpace(ConnectString))
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
                Interval = 5000,
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

        public Task<SQLConnection> GetConnectionFromDataBase()
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
                            try
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
                            }
                            catch (DbException e)
                            {
                                throw new Exception($"ConnectionString is invail: {e.Message}");
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
                catch (Exception e)
                {
                    throw e;
                }
                finally
                {
                    MaintainTimer.Start();
                    Locker.Set();
                }
            });
        }

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
                Locker.Dispose();
            }
        }
    }
}
