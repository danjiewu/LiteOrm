using Autofac;
using LiteOrm.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Threading;
using System.Threading.Tasks;

namespace LiteOrm
{
    /// <summary>
    /// 会话管理器 - 管理数据库会话、事务和连接
    /// </summary>
    /// <remarks>
    /// SessionManager 是一个关键的会话管理类，负责管理数据库连接、事务和异步上下文。
    /// 
    /// 主要功能包括：
    /// 1. 会话上下文管理 - 通过 AsyncLocal 管理异步上下文中的会话
    /// 2. 连接池管理 - 使用 DAOContextPoolFactory 获取和管理连接
    /// 3. 事务处理 - 支持事务的开始、提交和回滚
    /// 4. 隔离级别控制 - 设置和管理事务的隔离级别
    /// 5. SQL日志记录 - 记录执行的SQL语句用于调试和监控
    /// 6. 异步支持 - 提供异步执行方法以支持异步编程
    /// 7. 资源管理 - 实现 IDisposable 接口确保资源正确释放
    /// 8. 会话生命周期 - 支持进入和退出会话的操作
    /// 
    /// 该类通过依赖注入框架以 Scoped 方式注册，每个请求/任务有一个实例。
    /// 使用 AsyncLocal 确保在异步调用中正确维护会话上下文。
    /// 
    /// 使用示例：
    /// <code>
    /// var sessionManager = SessionManager.Current;
    /// await sessionManager.ExecuteInTransactionAsync(sm =&gt;
    /// {
    ///     var data = await service.GetAsync(id);
    ///     return data;    
    /// }
    /// </code>
    /// </remarks>
    [AutoRegister(Lifetime.Scoped)]
    public class SessionManager : IDisposable, IAsyncDisposable
    {
        private readonly DAOContextPoolFactory _daoContextPoolFactory;
        private readonly ILogger<SessionManager> _logger;
        private readonly SemaphoreSlim _syncLock = new SemaphoreSlim(1, 1);
        private bool _disposed = false;

        private readonly ConcurrentDictionary<string, DAOContext> _daoContexts = new ConcurrentDictionary<string, DAOContext>(StringComparer.OrdinalIgnoreCase);
        private readonly LinkedList<string> _sqlStack = new LinkedList<string>();
        private string _currentTransactionId;
        private IsolationLevel _currentIsolationLevel = IsolationLevel.ReadCommitted;
        private static readonly AsyncLocal<Lazy<SessionManager>> _currentAsyncLocal = new AsyncLocal<Lazy<SessionManager>>();

        /// <summary>
        /// 唯一会话ID
        /// </summary>
        public string SessionID { get; } = Guid.NewGuid().ToString("N").Substring(0, 8);

        /// <summary>
        /// 当前异步上下文的会话管理器（缓加载，首次访问时才从容器解析）
        /// </summary>
        public static SessionManager Current
        {
            get => _currentAsyncLocal.Value?.Value;
            set => _currentAsyncLocal.Value = value is null ? null : new Lazy<SessionManager>(() => value);
        }

        /// <summary>
        /// 为当前异步上下文设置缓加载的会话管理器工厂，首次访问 <see cref="Current"/> 时才调用 <paramref name="factory"/> 解析实例
        /// </summary>
        /// <param name="factory">返回 <see cref="SessionManager"/> 实例的工厂委托；传入 null 时清空当前上下文</param>
        public static void SetCurrentFactory(Func<SessionManager> factory)
        {
            _currentAsyncLocal.Value = factory is null ? null : new Lazy<SessionManager>(factory);
        }

        /// <summary>
        /// 构造函数
        /// </summary>
        public SessionManager(DAOContextPoolFactory daoContextPoolFactory, ILogger<SessionManager> logger = null)
        {
            _daoContextPoolFactory = daoContextPoolFactory ?? throw new ArgumentNullException(nameof(daoContextPoolFactory));
            _logger = logger;
            _logger?.LogDebug("[{SessionID}]Session created.", SessionID);
        }

        private void EnsureNotDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(SessionManager));
        }

        /// <summary>
        /// SQL语句堆栈（用于调试）
        /// </summary>
        public IReadOnlyCollection<string> SqlStack => _sqlStack;

        /// <summary>
        /// 是否在事务中
        /// </summary>
        public bool InTransaction => !string.IsNullOrEmpty(_currentTransactionId);

        /// <summary>
        /// 当前事务ID
        /// </summary>
        public string CurrentTransactionId => _currentTransactionId;

        /// <summary>
        /// 清除所有状态（SqlStack）
        /// </summary>
        public void Reset()
        {
            EnsureNotDisposed();
            _syncLock.Wait();
            try
            {
                _sqlStack.Clear();
            }
            finally
            {
                _syncLock.Release();
            }
        }

        /// <summary>
        /// 将SQL语句压入栈尾（用于调试和日志记录）
        /// </summary>
        /// <param name="sql">SQL语句</param>
        public void PushSql(string sql)
        {
            EnsureNotDisposed();
            _syncLock.Wait();
            try
            {
                _sqlStack.AddLast(sql);
                while (_sqlStack.Count > 10)
                {
                    _sqlStack.RemoveFirst();
                }
            }
            finally
            {
                _syncLock.Release();
            }
        }

        /// <summary>
        /// 开始事务
        /// </summary>
        /// <param name="isolationLevel">隔离级别</param>
        /// <returns>是否成功开始</returns>
        public bool BeginTransaction(IsolationLevel isolationLevel = IsolationLevel.ReadCommitted)
        {
            EnsureNotDisposed();
            _syncLock.Wait();
            try
            {
                if (InTransaction)
                {
                    _logger?.LogWarning("Session {SessionID} is already in a transaction, cannot begin a new one", SessionID);
                    return false;
                }

                _currentTransactionId = Guid.NewGuid().ToString();
                _currentIsolationLevel = isolationLevel;

                _logger?.LogDebug("Session {SessionID} began transaction. ID: {TransactionID}, Isolation: {IsolationLevel}", SessionID, _currentTransactionId, isolationLevel);

                // 为所有已存在的上下文开启事务，只读连接跳过事务
                foreach (var context in _daoContexts.Values)
                {
                    try
                    {
                        if (!context.IsReadOnly && !context.InTransaction)
                        {
                            context.BeginTransaction(isolationLevel);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, "Session {SessionID} failed to begin transaction for pool '{PoolName}'", SessionID, context.Pool?.Name);
                        // 如果某个连接开启事务失败，回滚并抛出异常
                        RollbackInternal();
                        throw new InvalidOperationException($"Session {SessionID} failed to start transaction: {ex.Message}", ex);
                    }
                }

                return true;
            }
            finally
            {
                _syncLock.Release();
            }
        }

        /// <summary>
        /// 异步开始事务
        /// </summary>
        /// <param name="isolationLevel">隔离级别</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>是否成功开始</returns>
        public async Task<bool> BeginTransactionAsync(IsolationLevel isolationLevel = IsolationLevel.ReadCommitted, CancellationToken cancellationToken = default)
        {
            EnsureNotDisposed();
            await _syncLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (InTransaction)
                {
                    _logger?.LogWarning("Session {SessionID} is already in a transaction, cannot begin a new one", SessionID);
                    return false;
                }

                _currentTransactionId = Guid.NewGuid().ToString();
                _currentIsolationLevel = isolationLevel;

                _logger?.LogDebug("Session {SessionID} began transaction. ID: {TransactionID}, Isolation: {IsolationLevel}", SessionID, _currentTransactionId, isolationLevel);

                foreach (var context in _daoContexts.Values)
                {
                    try
                    {
                        if (!context.IsReadOnly && !context.InTransaction)
                        {
                            await context.BeginTransactionAsync(isolationLevel, cancellationToken).ConfigureAwait(false);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, "Session {SessionID} failed to begin transaction for pool '{PoolName}'", SessionID, context.Pool?.Name);
                        await RollbackInternalAsync(cancellationToken).ConfigureAwait(false);
                        throw new InvalidOperationException($"Session {SessionID} failed to start transaction: {ex.Message}", ex);
                    }
                }

                return true;
            }
            finally
            {
                _syncLock.Release();
            }
        }

        /// <summary>
        /// 提交事务
        /// </summary>
        /// <returns>是否成功提交</returns>
        public bool Commit()
        {
            EnsureNotDisposed();
            _syncLock.Wait();
            try
            {
                if (!InTransaction)
                {
                    _logger?.LogWarning("Session {SessionID} is not in a transaction, cannot commit", SessionID);
                    return false;
                }

                return CommitInternal();
            }
            finally
            {
                _syncLock.Release();
            }
        }

        /// <summary>
        /// 异步提交事务
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>是否成功提交</returns>
        public async Task<bool> CommitAsync(CancellationToken cancellationToken = default)
        {
            EnsureNotDisposed();
            await _syncLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (!InTransaction)
                {
                    _logger?.LogWarning("Session {SessionID} is not in a transaction, cannot commit", SessionID);
                    return false;
                }

                return await CommitInternalAsync(cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                _syncLock.Release();
            }
        }

        /// <summary>
        /// 回滚事务
        /// </summary>
        /// <returns>是否成功回滚</returns>
        public bool Rollback()
        {
            EnsureNotDisposed();
            _syncLock.Wait();
            try
            {
                if (!InTransaction)
                {
                    _logger?.LogWarning("Session {SessionID} is not in a transaction, cannot roll back", SessionID);
                    return false;
                }

                return RollbackInternal();
            }
            finally
            {
                _syncLock.Release();
            }
        }

        /// <summary>
        /// 异步回滚事务
        /// </summary>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>是否成功回滚</returns>
        public async Task<bool> RollbackAsync(CancellationToken cancellationToken = default)
        {
            EnsureNotDisposed();
            await _syncLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (!InTransaction)
                {
                    _logger?.LogWarning("Session {SessionID} is not in a transaction, cannot roll back", SessionID);
                    return false;
                }

                return await RollbackInternalAsync(cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                _syncLock.Release();
            }
        }

        /// <summary>
        /// 内部提交方法
        /// </summary>
        private bool CommitInternal()
        {
            bool success = true;

            foreach (var context in _daoContexts.Values)
            {
                try
                {
                    if (!context.IsReadOnly && context.InTransaction)
                    {
                        context.Commit();
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Session {SessionID} failed to commit transaction. Pool: '{PoolName}'", SessionID, context.Pool?.Name);
                    success = false;
                }
            }

            // 清理事务状态
            _currentTransactionId = null;

            _logger?.LogDebug("Session {SessionID} transaction committed. ID: {TransactionID}, Success: {Success}", SessionID, _currentTransactionId, success);

            if (!success)
            {
                throw new InvalidOperationException("An error occurred while committing the transaction");
            }

            return success;
        }

        /// <summary>
        /// 内部异步提交方法
        /// </summary>
        private async Task<bool> CommitInternalAsync(CancellationToken cancellationToken = default)
        {
            bool success = true;

            foreach (var context in _daoContexts.Values)
            {
                try
                {
                    if (!context.IsReadOnly && context.InTransaction)
                    {
                        await context.CommitAsync(cancellationToken).ConfigureAwait(false);
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Session {SessionID} failed to commit transaction. Pool: '{PoolName}'", SessionID, context.Pool?.Name);
                    success = false;
                }
            }

            _currentTransactionId = null;

            _logger?.LogDebug("Session {SessionID} transaction committed. ID: {TransactionID}, Success: {Success}", SessionID, _currentTransactionId, success);

            if (!success)
            {
                throw new InvalidOperationException("An error occurred while committing the transaction");
            }

            return success;
        }

        /// <summary>
        /// 内部回滚方法
        /// </summary>
        private bool RollbackInternal()
        {
            bool success = true;

            foreach (var context in _daoContexts.Values)
            {
                try
                {
                    if (!context.IsReadOnly && context.InTransaction)
                    {
                        context.Rollback();
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Session {SessionID} failed to roll back transaction. Pool: '{PoolName}'", SessionID, context.Pool?.Name);
                    success = false;
                }
            }

            // 清理事务状态
            _currentTransactionId = null;

            _logger?.LogDebug("Session {SessionID} transaction rolled back. ID: {TransactionID}, Success: {Success}", SessionID, _currentTransactionId, success);

            if (!success)
            {
                throw new InvalidOperationException("An error occurred while rolling back the transaction");
            }

            return success;
        }

        /// <summary>
        /// 内部异步回滚方法
        /// </summary>
        private async Task<bool> RollbackInternalAsync(CancellationToken cancellationToken = default)
        {
            bool success = true;

            foreach (var context in _daoContexts.Values)
            {
                try
                {
                    if (!context.IsReadOnly && context.InTransaction)
                    {
                        await context.RollbackAsync(cancellationToken).ConfigureAwait(false);
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Session {SessionID} failed to roll back transaction. Pool: '{PoolName}'", SessionID, context.Pool?.Name);
                    success = false;
                }
            }

            _currentTransactionId = null;

            _logger?.LogDebug("Session {SessionID} transaction rolled back. ID: {TransactionID}, Success: {Success}", SessionID, _currentTransactionId, success);

            if (!success)
            {
                throw new InvalidOperationException("An error occurred while rolling back the transaction");
            }

            return success;
        }


        /// <summary>
        /// 获取指定名称的DAO上下文
        /// </summary>
        /// <param name="name">上下文名称，如果为null则使用默认名称"_"</param>
        /// <param name="readOnly">是否优先使用只读连接池，默认为 false。</param>
        /// <returns>DAO上下文实例</returns>
        public DAOContext GetDaoContext(string name = null, bool readOnly = false)
        {
            EnsureNotDisposed();
            if (name is null) name = "_";

            // 如果在事务中，忽略 readOnly 参数，必须返回主写连接以保证事务一致性
            if (InTransaction) readOnly = false;

            _syncLock.Wait();
            try
            {
                string rwKey = $"{name}:RW";

                // 当未配置只读池时，读请求回落到主连接，避免创建第二个连接
                var pool = _daoContextPoolFactory.GetPool(name);
                if (pool == null)
                    throw new InvalidOperationException($"Connection pool '{name}' not found");

                if (!pool.HasReadOnlyPools)
                {
                    readOnly = false;
                }

                string cacheKey = readOnly ? $"{name}:RO" : rwKey;
                if (_daoContexts.TryGetValue(cacheKey, out DAOContext context))
                {
                    return context;
                }
                // 从工厂获取上下文
                context = pool.PeekContext(readOnly);

                // 如果当前在事务中，开启事务
                if (InTransaction && !context.InTransaction)
                {
                    try
                    {
                        context.BeginTransaction(_currentIsolationLevel);
                    }
                    catch (Exception ex)
                    {
                        // 如果开启事务失败，归还连接并抛出异常
                        if (context.Pool != null)
                        {
                            context.Pool.ReturnContext(context);
                        }
                        else
                        {
                            context.Dispose();
                        }
                        _logger?.LogError(ex, "Session {SessionID} failed to begin transaction. Pool: '{PoolName}'", SessionID, name);
                        throw;
                    }
                }

                _daoContexts[cacheKey] = context;
                return context;
            }
            finally
            {
                _syncLock.Release();
            }
        }

        /// <summary>
        /// 异步获取指定名称的DAO上下文
        /// </summary>
        /// <param name="name">上下文名称，如果为null则使用默认名称"_"</param>
        /// <param name="readOnly">是否优先使用只读连接池，默认为 false。</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>DAO上下文实例</returns>
        public async Task<DAOContext> GetDaoContextAsync(string name = null, bool readOnly = false, CancellationToken cancellationToken = default)
        {
            EnsureNotDisposed();
            if (name is null) name = "_";

            if (InTransaction) readOnly = false;

            await _syncLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                string rwKey = $"{name}:RW";

                var pool = _daoContextPoolFactory.GetPool(name);
                if (pool == null)
                    throw new InvalidOperationException($"Connection pool '{name}' not found");

                if (!pool.HasReadOnlyPools)
                {
                    readOnly = false;
                }

                string cacheKey = readOnly ? $"{name}:RO" : rwKey;
                if (_daoContexts.TryGetValue(cacheKey, out DAOContext context))
                {
                    return context;
                }

                context = await pool.PeekContextAsync(readOnly).ConfigureAwait(false);

                if (InTransaction && !context.InTransaction)
                {
                    try
                    {
                        await context.BeginTransactionAsync(_currentIsolationLevel, cancellationToken).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        if (context.Pool != null)
                        {
                            context.Pool.ReturnContext(context);
                        }
                        else
                        {
                            await context.DisposeAsync().ConfigureAwait(false);
                        }
                        _logger?.LogError(ex, "Session {SessionID} failed to begin transaction. Pool: '{PoolName}'", SessionID, name);
                        throw;
                    }
                }

                _daoContexts[cacheKey] = context;
                return context;
            }
            finally
            {
                _syncLock.Release();
            }
        }


        /// <summary>
        /// 归还所有数据库上下文
        /// </summary>
        private void ReturnAllContexts()
        {
            foreach (var kvp in _daoContexts)
            {
                var context = kvp.Value;
                try
                {
                    if (context.Pool is not null)
                    {
                        context.Pool.ReturnContext(context);
                    }
                    else
                    {
                        context.Dispose();
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Session {SessionID} failed to return connection. Pool: '{PoolName}'", SessionID, context.Pool?.Name);
                }
            }
            _daoContexts.Clear();
        }

        /// <summary>
        /// 异步归还所有数据库上下文
        /// </summary>
        private async Task ReturnAllContextsAsync()
        {
            foreach (var kvp in _daoContexts)
            {
                var context = kvp.Value;
                try
                {
                    if (context.Pool is not null)
                    {
                        context.Pool.ReturnContext(context);
                    }
                    else
                    {
                        await context.DisposeAsync().ConfigureAwait(false);
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Session {SessionID} failed to return connection. Pool: '{PoolName}'", SessionID, context.Pool?.Name);
                }
            }
            _daoContexts.Clear();
        }

        /// <summary>
        /// 返回会话的字符串表示，包含会话ID
        /// </summary>
        /// <returns>包含会话ID的字符串表示。</returns>
        public override string ToString()
        {
            return $"[{SessionID}]";
        }
        #region IDisposable 实现

        ///<inheritdoc/> 
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// 异步释放资源
        /// </summary>
        public async ValueTask DisposeAsync()
        {
            if (_disposed) return;

            await _syncLock.WaitAsync().ConfigureAwait(false);
            try
            {
                if (_disposed) return;
                _logger?.LogDebug("[{SessionID}]Session disposed (async).", SessionID);
                _disposed = true;

                if (InTransaction)
                {
                    try
                    {
                        await RollbackInternalAsync().ConfigureAwait(false);
                        _logger?.LogDebug("Session {SessionID} transaction rolled back successfully on async dispose. ID: {TransactionID}", SessionID, _currentTransactionId);
                    }
                    catch (Exception commitEx)
                    {
                        _logger?.LogError(commitEx, "Session {SessionID} failed to roll back transaction on async dispose. ID: {TransactionID}", SessionID, _currentTransactionId);
                    }
                }

                await ReturnAllContextsAsync().ConfigureAwait(false);
            }
            finally
            {
                _syncLock.Release();
            }

            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        /// <param name="disposing">是否为显式调用</param>
        protected virtual void Dispose(bool disposing)
        {
            if (_disposed) return;
            _logger?.LogDebug("[{SessionID}]Session disposed ({DisposeType}).", SessionID, disposing ? "explicit" : "finalizer");
            _disposed = true;
            if (disposing)
            {
                // 如果有活动的事务，回滚
                if (InTransaction)
                {
                    try
                    {
                        // 尝试回滚事务
                        RollbackInternal();
                        _logger?.LogDebug("Session {SessionID} transaction rolled back successfully on dispose. ID: {TransactionID}", SessionID, _currentTransactionId);
                    }
                    catch (Exception commitEx)
                    {
                        _logger?.LogError(commitEx, "Session {SessionID} failed to roll back transaction on dispose. ID: {TransactionID}", SessionID, _currentTransactionId);
                    }
                }
                //归还所有连接
                ReturnAllContexts();
            }
        }

        /// <summary>
        /// 析构函数
        /// </summary>
        ~SessionManager()
        {
            Dispose(false);
        }
        #endregion
    }

    /// <summary>
    /// 会话管理器扩展方法
    /// </summary>
    public static class SessionManagerExtensions
    {
        /// <summary>
        /// 执行事务操作（简化版本）
        /// </summary>
        public static T ExecuteInTransaction<T>(this SessionManager sessionManager, Func<SessionManager, T> action,
            IsolationLevel isolationLevel = IsolationLevel.ReadCommitted)
        {
            if (sessionManager is null)
                throw new ArgumentNullException(nameof(sessionManager));

            if (action is null)
                throw new ArgumentNullException(nameof(action));

            sessionManager.BeginTransaction(isolationLevel);
            try
            {
                var result = action(sessionManager);
                sessionManager.Commit();
                return result;
            }
            catch
            {
                sessionManager.Rollback();
                throw;
            }
        }

        /// <summary>
        /// 执行事务操作（无返回值）
        /// </summary>
        public static void ExecuteInTransaction(this SessionManager sessionManager, Action<SessionManager> action,
            IsolationLevel isolationLevel = IsolationLevel.ReadCommitted)
        {
            ExecuteInTransaction(sessionManager, sm =>
            {
                action(sm);
                return true;
            }, isolationLevel);
        }

        /// <summary>
        /// 执行异步事务操作
        /// </summary>
        public static async Task<T> ExecuteInTransactionAsync<T>(this SessionManager sessionManager, Func<SessionManager, Task<T>> action,
            IsolationLevel isolationLevel = IsolationLevel.ReadCommitted)
        {
            if (sessionManager is null)
                throw new ArgumentNullException(nameof(sessionManager));

            if (action is null)
                throw new ArgumentNullException(nameof(action));

            await sessionManager.BeginTransactionAsync(isolationLevel).ConfigureAwait(false);
            try
            {
                var result = await action(sessionManager).ConfigureAwait(false);
                await sessionManager.CommitAsync().ConfigureAwait(false);
                return result;
            }
            catch
            {
                await sessionManager.RollbackAsync().ConfigureAwait(false);
                throw;
            }
        }

        /// <summary>
        /// 执行异步事务操作（无返回值）
        /// </summary>
        public static async Task ExecuteInTransactionAsync(this SessionManager sessionManager, Func<SessionManager, Task> action,
            IsolationLevel isolationLevel = IsolationLevel.ReadCommitted)
        {
            await ExecuteInTransactionAsync(sessionManager, async sm =>
            {
                await action(sm).ConfigureAwait(false);
                return true;
            }, isolationLevel).ConfigureAwait(false);
        }
    }
}
