using Microsoft.Extensions.DependencyInjection;
using MyOrm.Common;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Threading;

namespace MyOrm
{
    /// <summary>
    /// 数据访问对象上下文，用于管理数据库连接和事务
    /// </summary>
    /// <remarks>
    /// DAOContext 是一个重要的上下文类，它封装了单个数据库连接和相关的事务管理功能。
    /// 
    /// 主要功能包括：
    /// 1. 连接管理 - 管理数据库连接的生命周期
    /// 2. 事务处理 - 支持事务的开始、提交和回滚
    /// 3. 连接池集成 - 可与连接池集成以实现连接复用
    /// 4. 线程安全 - 使用锁机制确保线程安全的访问
    /// 5. 资源释放 - 实现 IDisposable 接口以确保资源正确释放
    /// 6. 连接状态监控 - 跟踪连接的活动时间和状态
    /// 
    /// 该类通常由 DAOContextPool 或 SessionManager 进行创建和管理，
    /// 应用代码通常不需要直接创建 DAOContext 实例。
    /// 
    /// 使用示例：
    /// <code>
    /// // 通常由框架自动管理，不需要手动创建
    /// var context = SessionManager.Current.GetDaoContext(dataSource);
    /// // 对数据库进行操作
    /// using (context)
    /// {
    ///     // 使用 context.DbConnection 执行数据库操作
    /// }
    /// </code>
    /// </remarks>
    public class DAOContext : IDisposable
    {
        private readonly object _syncLock = new object();
        private bool _isLocked = false;
        private int _lockCount = 0; // 支持重入锁
        /// <summary>
        /// 使用指定的数据库连接初始化DAOContext
        /// </summary>
        /// <param name="connection">数据库连接</param>
        public DAOContext(DbConnection connection)
        {
            DbConnection = connection ?? throw new ArgumentNullException(nameof(connection));
            ProviderType = connection.GetType();
            LastActiveTime = DateTime.Now;
        }
        /// <summary>
        /// 使用指定的数据库连接和连接池初始化DAOContext
        /// </summary>
        /// <param name="connection">数据库连接</param>
        /// <param name="pool">连接池</param>
        public DAOContext(DbConnection connection, DAOContextPool pool) : this(connection)
        {
            Pool = pool;
        }
        /// <summary>
        /// 获取数据库提供程序类型
        /// </summary>
        public Type ProviderType { get; }
        
        /// <summary>
        /// 获取或设置数据库连接
        /// </summary>
        public DbConnection DbConnection { get; protected set; }
        
        /// <summary>
        /// 获取连接池
        /// </summary>
        public DAOContextPool Pool { get; }
        
        /// <summary>
        /// 获取或设置最后活动时间
        /// </summary>
        public DateTime LastActiveTime { get; set; }
        
        /// <summary>
        /// 获取当前事务
        /// </summary>
        public IDbTransaction CurrentTransaction { get; private set; }
        
        /// <summary>
        /// 获取是否在事务中
        /// </summary>
        public bool InTransaction => CurrentTransaction is not null;
        
        /// <summary>
        /// 获取是否已锁定
        /// </summary>
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

        /// <summary>
        /// 提交当前事务
        /// </summary>
        /// <returns>如果成功提交返回true，如果没有活动事务返回false</returns>
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

        /// <summary>
        /// 回滚当前事务
        /// </summary>
        /// <returns>如果成功回滚返回true，如果没有活动事务返回false</returns>
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

        /// <summary>
        /// 释放DAOContext使用的所有资源
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// 释放DAOContext使用的非托管资源，并可选择释放托管资源
        /// </summary>
        /// <param name="disposing">true表示释放托管资源和非托管资源；false表示仅释放非托管资源</param>
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
