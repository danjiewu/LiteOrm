using Microsoft.Extensions.DependencyInjection;
using MyOrm.Common;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Threading;

namespace MyOrm
{
    public class DAOContext : IDisposable
    {
        private readonly object _syncLock = new object();
        private bool _isLocked = false;
        private int _lockCount = 0; // 支持重入锁
        public DAOContext(IDbConnection connection)
        {
            DbConnection = connection ?? throw new ArgumentNullException(nameof(connection));
            ProviderType = connection.GetType();
            LastActiveTime = DateTime.Now;
        }
        public DAOContext(IDbConnection connection, DAOContextPool pool):this(connection)
        {
            Pool= pool;
        }
        public Type ProviderType { get; }
        public IDbConnection DbConnection { get; protected set; }
        public DAOContextPool Pool { get; }
        public DateTime LastActiveTime { get; set; }
        public IDbTransaction CurrentTransaction { get; private set; }
        public bool InTransaction => CurrentTransaction != null;
        public bool IsLocked => _isLocked;

        /// <summary>
        /// 获取锁（支持重入）
        /// </summary>
        public bool AcquireLock()
        {
            lock (_syncLock)
            {
                // 支持锁重入
                if (_lockCount > 0)
                {
                    _lockCount++;
                    return true;
                }

                // 尝试获取锁
                if (Monitor.TryEnter(_syncLock))
                {
                    _isLocked = true;
                    _lockCount = 1;
                    return true;
                }

                return false;
            }
        }

        /// <summary>
        /// 释放锁（支持重入）
        /// </summary>
        public void ReleaseLock()
        {
            lock (_syncLock)
            {
                if (_lockCount > 0)
                {
                    _lockCount--;

                    if (_lockCount == 0)
                    {
                        _isLocked = false;
                        Monitor.Exit(_syncLock);
                    }
                }
            }
        }

        /// <summary>
        /// 开始事务（支持隔离级别）
        /// </summary>
        public bool BeginTransaction(IsolationLevel isolationLevel = IsolationLevel.ReadCommitted)
        {
            try
            {
                lock (_syncLock)
                {
                    if (InTransaction)
                        return false;

                    EnsureConnectionOpen();
                    CurrentTransaction = DbConnection.BeginTransaction(isolationLevel);
                    return true;
                }
            }
            catch
            {
                // 清理资源
                CurrentTransaction?.Dispose();
                CurrentTransaction = null;
                throw;
            }
        }

        public bool Commit()
        {
            lock (_syncLock)
            {
                if (!InTransaction)
                    return false;

                try
                {
                    CurrentTransaction.Commit();
                    return true;
                }
                finally
                {
                    CurrentTransaction?.Dispose();
                    CurrentTransaction = null;
                    LastActiveTime = DateTime.Now;
                }
            }
        }

        public bool Rollback()
        {
            lock (_syncLock)
            {
                if (!InTransaction)
                    return false;

                try
                {
                    CurrentTransaction.Rollback();
                    return true;
                }
                finally
                {
                    CurrentTransaction?.Dispose();
                    CurrentTransaction = null;
                    LastActiveTime = DateTime.Now;
                }
            }
        }

        public IDbCommand CreateDbCommand()
        {
            return new DbCommandProxy(DbConnection.CreateCommand(), this);
        }

        /// <summary>
        /// 确保连接已打开
        /// </summary>
        public void EnsureConnectionOpen()
        {
            if (DbConnection.State == ConnectionState.Closed)
            {
                DbConnection.Open();
            }
        }

        /// <summary>
        /// 重置上下文状态（从连接池返回时调用）
        /// </summary>
        internal void Reset()
        {
            lock (_syncLock)
            {
                // 如果还在事务中，回滚
                if (InTransaction)
                {
                    try
                    {
                        CurrentTransaction?.Rollback();
                    }
                    catch
                    {
                        // 忽略回滚异常
                    }
                    finally
                    {
                        CurrentTransaction?.Dispose();
                        CurrentTransaction = null;
                    }
                }

                // 释放锁
                while (_lockCount > 0)
                {
                    ReleaseLock();
                }

                LastActiveTime = DateTime.Now;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                Reset();
                CurrentTransaction?.Dispose();
                DbConnection?.Dispose();
            }
        }
    }

}