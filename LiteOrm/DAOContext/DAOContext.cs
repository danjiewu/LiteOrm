using Microsoft.Extensions.DependencyInjection;
using LiteOrm.Common;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using LiteOrm;
using System.Collections.Concurrent;

namespace LiteOrm
{
    /// <summary>
    /// 数据访问对象上下文，用于管理数据库连接和事务。
    /// </summary>
    /// <remarks>
    /// DAOContext 是 LiteOrm 的核心上下文类，封装了单个数据库连接（DbConnection）及其关联的事务管理逻辑。
    /// 
    /// 主要职责：
    /// 1. 生命周期管理：控制数据库连接的打开、重置与关闭。
    /// 2. 事务控制：提供同步与异步的事务开始、提交及回滚功能。
    /// 3. 并发安全：通过内置信号量支持独占式访问（Scope 模式），防止多线程竞争同一连接。
    /// 4. 资源自愈：在 Dispose 或从连接池回收（Reset）时，自动处理未提交的事务。
    /// </remarks>
    public class DAOContext : IDisposable
    {
        /// <summary>
        /// 设置或获取作用域锁定的超时时间（毫秒）。
        /// </summary>
        public static int ScopeTimeoutMilliseconds { get; set; } = 10000;
        /// <summary>
        /// 互斥信号量，确保在多线程环境中同一时间只有一个线程可以操作此上下文。
        /// </summary>
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);

        /// <summary>
        /// 使用指定的数据库连接初始化 <see cref="DAOContext"/> 类的新实例。
        /// </summary>
        /// <param name="connection">底层的数据库连接实例。</param>
        /// <exception cref="ArgumentNullException">当 <paramref name="connection"/> 为 null 时抛出。</exception>
        public DAOContext(DbConnection connection)
        {
            DbConnection = connection ?? throw new ArgumentNullException(nameof(connection));
            ProviderType = connection.GetType();
            SqlBuilder = SqlBuilderFactory.Instance.GetSqlBuilder(ProviderType);
            LastActiveTime = DateTime.Now;
        }

        /// <summary>
        /// 使用指定的数据库连接和所属连接池初始化 <see cref="DAOContext"/> 类的新实例。
        /// </summary>
        /// <param name="connection">底层的数据库连接实例。</param>
        /// <param name="pool">管理此上下文的连接池对象。</param>
        public DAOContext(DbConnection connection, DAOContextPool pool) : this(connection)
        {
            Pool = pool;
        }

        /// <summary>
        /// 
        /// </summary>
        public  SqlBuilder SqlBuilder { get; }

        public ConcurrentDictionary<(Type, string), DbCommandProxy> PreparedCommands { get;  }= new ConcurrentDictionary<(Type, string), DbCommandProxy>();

        /// <summary>
        /// 获取当前数据库提供程序的类型。
        /// </summary>
        public Type ProviderType { get; }

        /// <summary>
        /// 获取底层的数据库连接。
        /// </summary>
        public DbConnection DbConnection { get; protected set; }

        /// <summary>
        /// 获取创建此上下文的连接池（如果该上下文由池管理）。
        /// </summary>
        public DAOContextPool Pool { get; }

        /// <summary>
        /// 获取或设置该上下文最后一次执行操作的时间，用于连接池的老化检测。
        /// </summary>
        public DateTime LastActiveTime { get; set; }

        /// <summary>
        /// 获取当前活动的事务对象。如果没有正在进行的事务，则为 null。
        /// </summary>
        public IDbTransaction CurrentTransaction { get; private set; }

        /// <summary>
        /// 获取一个值，指示当前上下文是否处于活动事务中。
        /// </summary>
        public bool InTransaction => CurrentTransaction is not null;

        /// <summary>
        /// 获取一个同步锁定作用域，确保在该作用域内独占访问此上下文及其物理连接。
        /// </summary>
        /// <returns>一个用于控制锁定生命周期的 <see cref="IDisposable"/> 对象。</returns>
        /// <example>
        /// <code>
        /// using (context.AcquireScope()) 
        /// {
        ///    // 在此块内，其他线程将被阻塞，直到当前线程释放 Scope
        ///    // 执行一系列原子数据库操作...
        /// }
        /// </code>
        /// </example>
        public IDisposable AcquireScope()
        {
            if (_semaphore.Wait(ScopeTimeoutMilliseconds))
            {
                return new DAOScope(_semaphore);
            }

            throw new TimeoutException("无法获取数据库上下文锁定");
        }

        /// <summary>
        /// 异步获取一个锁定作用域，确保在该作用域内独占访问此上下文。
        /// </summary>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>一个异步完成任务，其结果为用于控制锁定生命周期的 <see cref="IDisposable"/> 对象。</returns>
        public async Task<IDisposable> AcquireScopeAsync(CancellationToken cancellationToken = default)
        {
            if (await _semaphore.WaitAsync(ScopeTimeoutMilliseconds, cancellationToken).ConfigureAwait(false))
            {
                return new DAOScope(_semaphore);
            }
            throw new TimeoutException("无法获取数据库上下文锁定");
        }

        /// <summary>
        /// 同步开始一个新事务。
        /// </summary>
        /// <param name="isolationLevel">事务的隔离级别，默认为 <see cref="IsolationLevel.ReadCommitted"/>。</param>
        /// <returns>如果事务成功开始返回 true；如果当前已在事务中则返回 false。</returns>
        public bool BeginTransaction(IsolationLevel isolationLevel = IsolationLevel.ReadCommitted)
        {
            using (AcquireScope())
            {
                if (InTransaction)
                    return false;

                try
                {
                    EnsureConnectionOpen();
                    CurrentTransaction = DbConnection.BeginTransaction(isolationLevel);
                    return true;
                }
                catch
                {
                    CurrentTransaction?.Dispose();
                    CurrentTransaction = null;
                    throw;
                }
            }
        }

        /// <summary>
        /// 提交当前活动事务并释放相关资源。
        /// </summary>
        /// <returns>如果成功提交返回 true；如果没有活动事务则返回 false。</returns>
        public bool Commit()
        {
            using (AcquireScope())
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
        /// 回滚当前活动事务并释放相关资源。
        /// </summary>
        /// <returns>如果成功回滚返回 true；如果没有活动事务则返回 false。</returns>
        public bool Rollback()
        {
            using (AcquireScope())
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
        /// 确保底层的数据库连接已开启。如果连接处于关闭状态，则执行打开操作。
        /// </summary>
        public void EnsureConnectionOpen()
        {
            if (DbConnection.State == ConnectionState.Closed)
            {
                DbConnection.Open();
            }
        }

        /// <summary>
        /// 重置上下文状态。通常在连接池回收连接时内部调用。
        /// </summary>
        /// <remarks>
        /// 如果连接处于事务中，此方法会尝试回滚事务并重置最后活动时间。
        /// </remarks>
        internal void Reset()
        {
            using (AcquireScope())
            {
                if (InTransaction)
                {
                    try { CurrentTransaction?.Rollback(); }
                    catch { /* 忽略回滚异常 */ }
                    finally
                    {
                        CurrentTransaction?.Dispose();
                        CurrentTransaction = null;
                    }
                }
                LastActiveTime = DateTime.Now;
            }
        }

        /// <summary>
        /// 释放由 <see cref="DAOContext"/> 占用的所有资源。
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// 执行与释放或重置资源相关的应用程序定义的任务。
        /// </summary>
        /// <param name="disposing">如果为 true，则释放托管资源和非托管资源；如果为 false，则仅释放非托管资源。</param>
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                Reset();
                DbConnection?.Dispose();
                _semaphore.Dispose();
            }
        }

        /// <summary>
        /// 内部锁定作用域实现类，利用结构体避免堆分配开销。
        /// </summary>
        private readonly struct DAOScope : IDisposable
        {
            private readonly SemaphoreSlim _semaphore;

            /// <summary>
            /// 初始化 <see cref="DAOScope"/>。
            /// </summary>
            /// <param name="semaphore">要释放的信号量。</param>
            public DAOScope(SemaphoreSlim semaphore) => _semaphore = semaphore;

            /// <summary>
            /// 释放信号量，允许其他线程访问上下文。
            /// </summary>
            public void Dispose() => _semaphore.Release();
        }
    }
}
