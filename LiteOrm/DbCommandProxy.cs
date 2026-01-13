using System;
using System.Collections.Generic;
using System.Text;
using System.Data;
using System.Data.Common;
using LiteOrm.Common;
using System.Threading;
using System.Threading.Tasks;

namespace LiteOrm
{
    /// <summary>
    /// 数据库命令代理类 - 为IDbCommand提供包装和扩展功能
    /// </summary>
    /// <remarks>
    /// DbCommandProxy 是一个代理类，它包装了 DbCommand 对象并提供额外的功能，
    /// 如自动连接管理、事务处理、SQL日志记录等。
    /// 
    /// 主要功能包括：
    /// 1. 命令执行代理 - 代理 IDbCommand 的所有操作
    /// 2. 连接管理 - 自动打开和维护数据库连接
    /// 3. 事务支持 - 自动关联当前事务
    /// 4. 执行前后处理 - 在命令执行前后进行必要的设置和清理
    /// 5. SQL日志记录 - 记录执行的SQL语句用于调试
    /// 6. 锁管理 - 管理上下文的锁以确保线程安全
    /// 7. 异步支持 - 支持异步命令执行
    /// 
    /// 该类实现了 IDbCommand 接口，可以作为标准 DbCommand 的替代品使用。
    /// 通常由 ObjectDAOBase 的 NewCommand() 方法创建。
    /// 
    /// 使用示例：
    /// <code>
    /// var command = dao.NewCommand();
    /// command.CommandText = \"select * from users where id = @id\";
    /// var param = command.CreateParameter();
    /// param.ParameterName = \"@id\";
    /// param.Value = 123;
    /// command.Parameters.Add(param);
    /// 
    /// // 执行命令
    /// using (var reader = command.ExecuteReader())
    /// {
    ///     while (reader.Read())
    ///     {
    ///         // 处理结果
    ///     }
    /// }
    /// </code>
    /// </remarks>
    public class DbCommandProxy : IDbCommand
    {
        /// <summary>
        /// 初始化 <see cref="DbCommandProxy"/> 类的新实例。
        /// </summary>
        /// <param name="context">DAO 上下文，提供数据库连接和事务管理。</param>
        /// <param name="sqlBuilder">SQL 构建器，用于处理特定数据库的 SQL 语法。</param>
        public DbCommandProxy(DAOContext context, ISqlBuilder sqlBuilder)
        {
            Context = context ?? throw new ArgumentNullException(nameof(context));
            SqlBuilder = sqlBuilder ?? throw new ArgumentNullException(nameof(sqlBuilder));
            Target = context.DbConnection.CreateCommand();
        }

        /// <summary>
        /// 获取目标数据库命令对象。
        /// </summary>
        public DbCommand Target
        {
            get;
        }

        /// <summary>
        /// 获取 DAO 上下文。
        /// </summary>
        public DAOContext Context { get; }

        /// <summary>
        /// 获取 SQL 构建器。
        /// </summary>
        public ISqlBuilder SqlBuilder { get; }

        /// <summary>
        /// 在执行数据库命令之前的处理逻辑。
        /// </summary>
        /// <param name="excuteType">执行类型。</param>
        protected virtual void PreExcuteCommand(ExcuteType excuteType)
        {
            Context.AcquireLock();
            Context.EnsureConnectionOpen();
            Transaction = Context.CurrentTransaction;
            SessionManager.Current.PushSql(CommandText);
        }

        /// <summary>
        /// 在执行数据库命令之后的处理逻辑。
        /// </summary>
        /// <param name="excuteType">执行类型。</param>
        protected virtual void PostExcuteCommand(ExcuteType excuteType)
        {
            Context.LastActiveTime = DateTime.Now;
        }

        #region IDbCommand Members

        /// <summary>
        /// 尝试取消 <see cref="IDbCommand"/> 的执行。
        /// </summary>
        public void Cancel()
        {
            Target.Cancel();
        }

        private string commandText;
        /// <summary>
        /// 获取或设置要对数据源执行的文本命令。
        /// </summary>
        public string CommandText
        {
            get { return commandText; }
            set
            {
                commandText = value;
                Target.CommandText = SqlBuilder.ReplaceSqlName(value);
            }
        }

        /// <summary>
        /// 获取或设置在终止执行命令的尝试并生成错误之前的等待时间（以秒为单位）。
        /// </summary>
        public int CommandTimeout
        {
            get { return Target.CommandTimeout; }
            set { Target.CommandTimeout = value; }
        }

        /// <summary>
        /// 指示或设置如何解释 <see cref="CommandText"/> 属性。
        /// </summary>
        public CommandType CommandType
        {
            get { return Target.CommandType; }
            set { Target.CommandType = value; }
        }

        /// <summary>
        /// 获取或设置该 <see cref="IDbCommand"/> 实例使用的 <see cref="IDbConnection"/>。
        /// </summary>
        public IDbConnection Connection
        {
            get { return Target.Connection; }
            set { (Target as IDbCommand).Connection = value; }
        }

        /// <summary>
        /// 创建 <see cref="IDbDataParameter"/> 对象的新实例。
        /// </summary>
        /// <returns>IDbDataParameter 对象。</returns>
        public IDbDataParameter CreateParameter()
        {
            return Target.CreateParameter();
        }

        /// <summary>
        /// 对连接对象执行 SQL 语句，并返回受影响的行数。
        /// </summary>
        /// <returns>受影响的行数。</returns>
        public int ExecuteNonQuery()
        {
            try
            {
                PreExcuteCommand(ExcuteType.ExecuteNonQuery);
                int ret = Target.ExecuteNonQuery();
                PostExcuteCommand(ExcuteType.ExecuteNonQuery);
                return ret;
            }
            finally
            {
                Context.ReleaseLock();
            }
        }

        /// <summary>
        /// 异步执行 SQL 语句并返回受影响的行数。
        /// </summary>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>表示异步操作的任务，其结果为受影响的行数。</returns>
        public async Task<int> ExecuteNonQueryAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                PreExcuteCommand(ExcuteType.ExecuteNonQuery);
                if (Target is DbCommand dbCmd)
                {
                    int ret = await dbCmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                    PostExcuteCommand(ExcuteType.ExecuteNonQuery);
                    return ret;
                }
                else
                {
                    int ret = await Task.Run(() => Target.ExecuteNonQuery(), cancellationToken).ConfigureAwait(false);
                    PostExcuteCommand(ExcuteType.ExecuteNonQuery);
                    return ret;
                }
            }
            finally
            {
                Context.ReleaseLock();
            }
        }

        /// <summary>
        /// 对 <see cref="Connection"/> 执行 <see cref="CommandText"/>，并使用 <see cref="CommandBehavior"/> 值之一返回 <see cref="IDataReader"/>。
        /// </summary>
        /// <param name="behavior">命令行为特性。</param>
        /// <returns>一个 <see cref="IDataReader"/> 对象。</returns>
        public IDataReader ExecuteReader(CommandBehavior behavior)
        {
            try
            {
                PreExcuteCommand(ExcuteType.ExecuteReader);
                IDataReader ret = Target.ExecuteReader(behavior);
                PostExcuteCommand(ExcuteType.ExecuteReader);
                return ret;
            }
            finally
            {
                Context.ReleaseLock();
            }
        }

        /// <summary>
        /// 对 <see cref="Connection"/> 执行 <see cref="CommandText"/>，并返回 <see cref="IDataReader"/>。
        /// </summary>
        /// <returns>一个 <see cref="IDataReader"/> 对象。</returns>
        public IDataReader ExecuteReader()
        {
            try
            {
                PreExcuteCommand(ExcuteType.ExecuteReader);
                IDataReader ret = Target.ExecuteReader();
                PostExcuteCommand(ExcuteType.ExecuteReader);
                return ret;
            }
            finally
            {
                Context.ReleaseLock();
            }
        }

        /// <summary>
        /// 异步执行 SQL 语句并返回 <see cref="IDataReader"/>。
        /// </summary>
        /// <param name="behavior">命令行为特性。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>表示异步操作的任务，其结果为一个 <see cref="IDataReader"/> 对象。</returns>
        public async Task<IDataReader> ExecuteReaderAsync(CommandBehavior behavior = CommandBehavior.Default, CancellationToken cancellationToken = default)
        {
            try
            {
                PreExcuteCommand(ExcuteType.ExecuteReader);

                var task = Target.ExecuteReaderAsync(behavior, cancellationToken);
                IDataReader ret = await task.ConfigureAwait(false);
                PostExcuteCommand(ExcuteType.ExecuteReader);
                return ret;
            }
            finally
            {
                Context.ReleaseLock();
            }
        }

        /// <summary>
        /// 执行查询，并返回查询所返回的结果集中第一行的第一列。忽略额外的列或行。
        /// </summary>
        /// <returns>结果集中第一行的第一列。</returns>
        public object ExecuteScalar()
        {
            try
            {
                PreExcuteCommand(ExcuteType.ExecuteScalar);
                object ret = Target.ExecuteScalar();
                PostExcuteCommand(ExcuteType.ExecuteScalar);
                return ret;
            }
            finally
            {
                Context.ReleaseLock();
            }
        }

        /// <summary>
        /// 异步执行查询，并返回查询所返回的结果集中第一行的第一列。
        /// </summary>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>表示异步操作的任务，其结果为结果集中第一行的第一列。</returns>
        public async Task<object> ExecuteScalarAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                PreExcuteCommand(ExcuteType.ExecuteScalar);
                var ret = await Target.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
                PostExcuteCommand(ExcuteType.ExecuteScalar);
                return ret;
            }
            finally
            {
                Context.ReleaseLock();
            }
        }

        /// <summary>
        /// 获取 <see cref="IDataParameterCollection"/>。
        /// </summary>
        public IDataParameterCollection Parameters
        {
            get { return Target.Parameters; }
        }

        /// <summary>
        /// 在数据源上创建该命令的准备好的或编译的版本。
        /// </summary>
        public void Prepare()
        {
            if (Context is not null) Transaction = Context.CurrentTransaction;
            Context.EnsureConnectionOpen();
            Target.Prepare();
        }

        /// <summary>
        /// 获取或设置在其中执行 .NET 数据提供程序的 Command 对象的事务。
        /// </summary>
        public IDbTransaction Transaction
        {
            get { return Target.Transaction; }
            set { (Target as IDbCommand).Transaction = value; }
        }

        /// <summary>
        /// 获取或设置当 <see cref="IDataAdapter.Update(DataSet)"/> 使用 <see cref="IDataAdapter"/> 时如何将查询结果应用于 <see cref="DataRow"/>。
        /// </summary>
        public UpdateRowSource UpdatedRowSource
        {
            get { return Target.UpdatedRowSource; }
            set { Target.UpdatedRowSource = value; }
        }

        #endregion

        #region IDisposable Members

        /// <summary>
        /// 释放由该 <see cref="DbCommandProxy"/> 使用的资源。
        /// </summary>
        public void Dispose()
        {
            Target.Dispose();
        }
        #endregion
    }

    /// <summary>
    /// 命令执行类型。
    /// </summary>
    public enum ExcuteType
    {
        /// <summary>
        /// 非查询执行。
        /// </summary>
        ExecuteNonQuery,
        /// <summary>
        /// 读取器执行。
        /// </summary>
        ExecuteReader,
        /// <summary>
        /// 标量执行。
        /// </summary>
        ExecuteScalar
    }
}