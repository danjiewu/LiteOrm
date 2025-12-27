using System;
using System.Collections.Generic;
using System.Text;
using System.Data;
using System.Data.Common;

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
        public DbCommandProxy(IDbCommand dbCommand, DAOContext context)
        {
            Target = dbCommand ?? throw new ArgumentNullException(nameof(dbCommand));
            Context = context ?? throw new ArgumentNullException(nameof(context)); ;
        }

        /// <summary>
        /// Ä¿±êCommand
        /// </summary>
        public IDbCommand Target
        {
            get;
        }

        public DAOContext Context { get; }

        protected virtual void PreExcuteCommand(ExcuteType excuteType)
        {
            Context.AcquireLock();
            Context.EnsureConnectionOpen();
            Transaction = Context.CurrentTransaction;
            SessionManager.Current.SqlStack.Push(CommandText);
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
                Target.CommandText = SqlBuilder.Instance.ReplaceSqlName(value);
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
            set { Target.Connection = value; }
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
            set { Target.Transaction = value; }
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