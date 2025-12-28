using System;
using System.Collections.Generic;
using System.Text;
using System.Data;
using System.Data.Common;
using MyOrm.Common;
using System.Threading;
using System.Threading.Tasks;

namespace MyOrm
{
    /// <summary>
    /// 
    /// </summary>
    public class DbCommandProxy : IDbCommand
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="dbCommand"></param>
        /// <param name="context"></param>
        public DbCommandProxy(DAOContext context, ISqlBuilder sqlBuilder)
        {
            Context = context ?? throw new ArgumentNullException(nameof(context));
            SqlBuilder = sqlBuilder ?? throw new ArgumentNullException(nameof(sqlBuilder));
            Target = context.DbConnection.CreateCommand();
        }

        /// <summary>
        /// Ä¿±êCommand
        /// </summary>
        public DbCommand Target
        {
            get;
        }

        public DAOContext Context { get; }
        public ISqlBuilder SqlBuilder { get; }

        protected virtual void PreExcuteCommand(ExcuteType excuteType)
        {
            Context.AcquireLock();
            Context.EnsureConnectionOpen();
            Transaction = Context.CurrentTransaction;
            SessionManager.Current.PushSql(CommandText);
        }

        protected virtual void PostExcuteCommand(ExcuteType excuteType)
        {
            Context.LastActiveTime = DateTime.Now;
        }

        #region IDbCommand Members

        public void Cancel()
        {
            Target.Cancel();
        }

        private string commandText;
        public string CommandText
        {
            get { return commandText; }
            set
            {
                commandText = value;
                Target.CommandText = SqlBuilder.ReplaceSqlName(value);
            }
        }

        public int CommandTimeout
        {
            get { return Target.CommandTimeout; }
            set { Target.CommandTimeout = value; }
        }

        public CommandType CommandType
        {
            get { return Target.CommandType; }
            set { Target.CommandType = value; }
        }

        public IDbConnection Connection
        {
            get { return Target.Connection; }
            set { (Target as IDbCommand).Connection = value; }
        }

        public IDbDataParameter CreateParameter()
        {
            return Target.CreateParameter();
        }

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

        public IDataParameterCollection Parameters
        {
            get { return Target.Parameters; }
        }

        public void Prepare()
        {
            if (Context != null) Transaction = Context.CurrentTransaction;
            Context.EnsureConnectionOpen();
            Target.Prepare();
        }

        public IDbTransaction Transaction
        {
            get { return Target.Transaction; }
            set { (Target as IDbCommand).Transaction = value; }
        }

        public UpdateRowSource UpdatedRowSource
        {
            get { return Target.UpdatedRowSource; }
            set { Target.UpdatedRowSource = value; }
        }

        #endregion

        #region IDisposable Members

        public void Dispose()
        {
            Target.Dispose();
        }
        #endregion
    }

    public enum ExcuteType
    {
        ExecuteNonQuery,
        ExecuteReader,
        ExecuteScalar
    }
}