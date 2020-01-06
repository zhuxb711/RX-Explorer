using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Threading.Tasks;

namespace SQLConnectionPoolProvider
{
    public sealed class SQLConnection<T> : DbConnection where T : DbConnection, new()
    {
        private DbConnection InnerConnection;

        private SQLConnectionPool<T> Pool;

        internal SQLConnection(SQLConnectionPool<T> Pool, DbConnection InnerConnection)
        {
            this.InnerConnection = InnerConnection;
            this.Pool = Pool;
        }

        private SQLConnection()
        {

        }

        public override string ConnectionString
        {
            get
            {
                return InnerConnection.ConnectionString;
            }
            set
            {
                InnerConnection.ConnectionString = value;
            }
        }

        public override string Database
        {
            get
            {
                return InnerConnection.Database;
            }
        }

        public override string DataSource
        {
            get
            {
                return InnerConnection.DataSource;
            }
        }

        public override string ServerVersion
        {
            get
            {
                return InnerConnection.ServerVersion;
            }
        }

        public override ConnectionState State
        {
            get
            {
                return InnerConnection.State;
            }
        }

        public override void ChangeDatabase(string databaseName)
        {
            InnerConnection.ChangeDatabase(databaseName);
        }

        public override void Close()
        {
            if (Pool.GetFreeConnectionCount() > 0)
            {
                InnerConnection.Close();
                InnerConnection.Dispose();
            }
        }

        public new void Dispose()
        {
            Close();
        }

        public override void Open()
        {
            InnerConnection.Open();
        }

        protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel)
        {
            return InnerConnection.BeginTransaction(isolationLevel);
        }

        protected override DbCommand CreateDbCommand()
        {
            return InnerConnection.CreateCommand();
        }
    }

    public sealed class SQLConnectionPool<T> : IDisposable where T : DbConnection, new()
    {
        public ushort MaxConnections { get; private set; }

        public ushort MinConnections { get; private set; }

        public string ConnectString { get; private set; }

        private bool IsInitialized = false;

        private List<Tuple<SQLConnection<T>, bool>> ConnectionPool;

        public SQLConnectionPool(string ConnectString, ushort MaxConnections, ushort MinConnections)
        {
            this.MaxConnections = MaxConnections;
            this.MinConnections = MinConnections;
            ConnectionPool = new List<Tuple<SQLConnection<T>, bool>>(MaxConnections);
        }

        public Task<SQLConnection<T>> GetConnectionFromDataBase()
        {
            return Task.Run(() =>
            {
                if (IsInitialized)
                {

                }
                else
                {
                    if (MinConnections > 0)
                    {
                        try
                        {
                            for (int i = 0; i < MinConnections; i++)
                            {
                                DbConnection InnerConnection = new T
                                {
                                    ConnectionString = ConnectString
                                };
                                InnerConnection.Open();

                                SQLConnection<T> Connection = new SQLConnection<T>(this, InnerConnection);

                                ConnectionPool.Add(new Tuple<SQLConnection<T>, bool>(Connection, false));
                            }
                        }
                        catch (DbException e)
                        {
                            throw new Exception($"ConnectionString is invail: {e.Message}");
                        }

                        return null;
                    }

                    IsInitialized = true;
                }
            });
        }

        internal int GetFreeConnectionCount()
        {
            IGrouping<bool, Tuple<SQLConnection<T>, bool>> FreeConnections = ConnectionPool.GroupBy((Item) => Item.Item2).FirstOrDefault((Flag) => Flag.Key == false);
            return FreeConnections == null ? 0 : FreeConnections.Count();
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }
    }
}
