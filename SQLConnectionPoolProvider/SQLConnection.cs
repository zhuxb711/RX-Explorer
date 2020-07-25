using System;
using System.Data;
using System.Data.Common;

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
        public bool IsConnected
        {
            get
            {
                return InnerConnection != null && InnerConnection.State == ConnectionState.Open;
            }
        }

        /// <summary>
        /// 打开SQL连接对象成功时调用此构造函数
        /// </summary>
        /// <param name="InnerConnection">实际的SQL连接对象</param>
        internal SQLConnection(DbConnection InnerConnection)
        {
            this.InnerConnection = InnerConnection;
        }

        /// <summary>
        /// 打开SQL连接对象错误时调用此构造函数
        /// </summary>
        internal SQLConnection()
        {
            InnerConnection = null;
        }

        /// <summary>
        /// 从SQL连接对象中获取SQL命令对象
        /// </summary>
        /// <typeparam name="T">SQL命令对象，具体因不同数据库而异</typeparam>
        /// <param name="CommandText">SQL命令</param>
        /// <returns>SQL命令对象</returns>
        public T CreateDbCommandFromConnection<T>(string CommandText, CommandType Type = CommandType.Text) where T : DbCommand, new()
        {
            T Command = new T
            {
                Connection = InnerConnection,
                CommandText = CommandText,
                CommandType = Type
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
}
