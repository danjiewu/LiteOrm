using Microsoft.Extensions.Logging;
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
    public class SessionManager : IDisposable, IAsyncDisposable
    {
        private readonly DAOContextPoolFactory _daoContextPoolFactory;
        private readonly ILogger<SessionManager>? _logger;
        private readonly SemaphoreSlim _syncLock = new SemaphoreSlim(1, 1);
        private bool _disposed = false;
        private Task? _disposeTask;

        private readonly ConcurrentDictionary<string, DAOContext> _daoContexts = new ConcurrentDictionary<string, DAOContext>(StringComparer.OrdinalIgnoreCase);
        private readonly LinkedList<string> _sqlStack = new LinkedList<string>();
        private string? _currentTransactionId;
        private IsolationLevel _currentIsolationLevel = IsolationLevel.ReadCommitted;
        private static readonly AsyncLocal<Lazy<SessionManager?>?> _currentSessionFactory = new AsyncLocal<Lazy<SessionManager?>?>();

        /// <summary>
        /// 最大SQL历史记录条数，超过此数量的旧SQL将被丢弃
        /// </summary>
        public static int MaxSqlHistorySize = 10;

        /// <summary>
        /// 唯一会话ID
        /// </summary>
        public string SessionID { get; } = ShortId.NewId();

        /// <summary>
        /// 当前异步上下文的会话管理器。通过 <see cref="SetCurrent"/> 设置的工厂委托延迟创建并缓存；
        /// 当前上下文未设置工厂、工厂返回 null 或实例已释放时返回 null。
        /// </summary>
        public static SessionManager? Current
        {
            get
            {
                var factory = _currentSessionFactory.Value;
                if (factory is null) return null;
                try
                {
                    var instance = factory.Value;
                    if (instance is null || instance._disposed) return null;
                    return instance;
                }
                catch { return null; }
            }
        }

        /// <summary>
        /// 为当前异步上下文设置会话工厂委托。工厂委托在 <see cref="Current"/> 首次访问时通过 <see cref="Lazy{T}"/> 延迟执行并缓存结果。
        /// 传入 null 时清空当前上下文。
        /// </summary>
        /// <remarks>
        /// 该方法合并了原先的手动设置会话与设置服务提供者两种场景：
        /// <list type="bullet">
        /// <item>手动构造场景：<c>SetCurrent(() =&gt; sessionManager)</c></item>
        /// <item>DI 集成场景：<c>SetCurrent(() =&gt; serviceProvider.GetService&lt;SessionManager&gt;())</c></item>
        /// </list>
        /// LiteOrm 核心不依赖 DI 容器，手动构造后通过此方法激活当前会话。
        /// </remarks>
        /// <param name="sessionFactory">返回当前会话实例的工厂委托；传入 null 时清空当前上下文</param>
        public static void SetCurrent(Func<SessionManager?>? sessionFactory)
        {
            _currentSessionFactory.Value = sessionFactory is null
                ? null
                : new Lazy<SessionManager?>(sessionFactory, LazyThreadSafetyMode.ExecutionAndPublication);
        }

        /// <summary>
        /// 构造函数
        /// </summary>
        public SessionManager(DAOContextPoolFactory daoContextPoolFactory, ILogger<SessionManager>? logger = null)
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
        public string? CurrentTransactionId => _currentTransactionId;

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
                while (_sqlStack.Count > MaxSqlHistorySize)
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

                _currentTransactionId = ShortId.NewId();
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
                        _logger?.LogError(ex, "Session {SessionID} failed to begin transaction for pool '{PoolName}' (ContextId: {ContextId})", SessionID, context.Pool?.Name, context.Id);
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

                _currentTransactionId = ShortId.NewId();
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
                        _logger?.LogError(ex, "Session {SessionID} failed to begin transaction for pool '{PoolName}' (ContextId: {ContextId})", SessionID, context.Pool?.Name, context.Id);
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
                    _logger?.LogError(ex, "Session {SessionID} failed to commit transaction. Pool: '{PoolName}' (ContextId: {ContextId})", SessionID, context.Pool?.Name, context.Id);
                    success = false;
                }
            }

            var committedTransactionId = _currentTransactionId;
            // 清理事务状态
            _currentTransactionId = null;
            _logger?.LogDebug("Session {SessionID} transaction committed. ID: {TransactionID}, Success: {Success}", SessionID, committedTransactionId, success);

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
                    _logger?.LogError(ex, "Session {SessionID} failed to commit transaction. Pool: '{PoolName}' (ContextId: {ContextId})", SessionID, context.Pool?.Name, context.Id);
                    success = false;
                }
            }

            var committedTransactionId = _currentTransactionId;
            _currentTransactionId = null;
            _logger?.LogDebug("Session {SessionID} transaction committed. ID: {TransactionID}, Success: {Success}", SessionID, committedTransactionId, success);

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
                    _logger?.LogError(ex, "Session {SessionID} failed to roll back transaction. Pool: '{PoolName}' (ContextId: {ContextId})", SessionID, context.Pool?.Name, context.Id);
                    success = false;
                }
            }

            var rollbackTransactionId = _currentTransactionId;
            // 清理事务状态
            _currentTransactionId = null;

            _logger?.LogDebug("Session {SessionID} transaction rolled back. ID: {TransactionID}, Success: {Success}", SessionID, rollbackTransactionId, success);

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
                    _logger?.LogError(ex, "Session {SessionID} failed to roll back transaction. Pool: '{PoolName}' (ContextId: {ContextId})", SessionID, context.Pool?.Name, context.Id);
                    success = false;
                }
            }

            var rollbackTransactionId = _currentTransactionId;
            _currentTransactionId = null;
            _logger?.LogDebug("Session {SessionID} transaction rolled back. ID: {TransactionID}, Success: {Success}", SessionID, rollbackTransactionId, success);

            if (!success)
            {
                throw new InvalidOperationException("An error occurred while rolling back the transaction");
            }

            return success;
        }


        /// <summary>
        /// 获取指定数据源的连接池。当未指定名称时返回默认数据源的连接池。
        /// </summary>
        /// <param name="name">数据源名称；为 null 或空白时使用默认数据源。</param>
        /// <returns>对应的 <see cref="DAOContextPool"/> 实例；若不存在则返回 null。</returns>
        public DAOContextPool? GetDAOContextPool(string? name = null)
        {
            EnsureNotDisposed();
            return _daoContextPoolFactory.GetPool(name);
        }

        /// <summary>
        /// 获取指定名称的DAO上下文
        /// </summary>
        /// <param name="name">上下文名称，如果为null则使用默认名称"_"</param>
        /// <param name="readOnly">是否优先使用只读连接池，默认为 false。</param>
        /// <returns>DAO上下文实例</returns>
        public DAOContext GetDaoContext(string? name = null, bool readOnly = false)
        {
            EnsureNotDisposed();
            // 当 name 为 null 时，由 DAOContextPoolFactory.GetPool 解析为默认数据源

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
                if (_daoContexts.TryGetValue(cacheKey, out DAOContext? context))
                {
                    if (context.IsValid)
                        return context;
                    // 先移除再释放，避免其他线程拿到已 Dispose 的实例
                    else
                    {
                        if (_daoContexts.TryRemove(cacheKey, out var stale))
                        {
                            stale.Dispose();
                        }
                    }
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
                        _logger?.LogError(ex, "Session {SessionID} failed to begin transaction. Pool: '{PoolName}' (ContextId: {ContextId})", SessionID, name, context.Id);
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
        public async Task<DAOContext> GetDaoContextAsync(string? name = null, bool readOnly = false, CancellationToken cancellationToken = default)
        {
            EnsureNotDisposed();
            // 当 name 为 null 时，由 DAOContextPoolFactory.GetPool 解析为默认数据源

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
                if (_daoContexts.TryGetValue(cacheKey, out DAOContext? context))
                {
                    if (context.IsValid)
                        return context;
                    else
                    {
                        // 先移除再释放，避免其他线程拿到已 Dispose 的实例
                        if (_daoContexts.TryRemove(cacheKey, out var stale))
                        {
                            await stale.DisposeAsync().ConfigureAwait(false);
                        }
                    }
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
                        _logger?.LogError(ex, "Session {SessionID} failed to begin transaction. Pool: '{PoolName}' (ContextId: {ContextId})", SessionID, name, context.Id);
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
                    _logger?.LogError(ex, "Session {SessionID} failed to return connection. Pool: '{PoolName}' (ContextId: {ContextId})", SessionID, context.Pool?.Name, context.Id);
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
                    _logger?.LogError(ex, "Session {SessionID} failed to return connection. Pool: '{PoolName}' (ContextId: {ContextId})", SessionID, context.Pool?.Name, context.Id);
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
        public ValueTask DisposeAsync()
        {
            if (_disposed) return default;

            if (_disposeTask != null)
                return new ValueTask(_disposeTask);

            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var existing = Interlocked.CompareExchange(ref _disposeTask, tcs.Task, null);
            if (existing != null)
                return new ValueTask(existing);

            _ = DisposeAsyncInternal(tcs);
            return new ValueTask(tcs.Task);
        }

        private async Task DisposeAsyncInternal(TaskCompletionSource<bool> tcs)
        {
            bool lockAcquired = false;
            try
            {
                await _syncLock.WaitAsync().ConfigureAwait(false);
                lockAcquired = true;
            }
            catch
            {
                // 信号量可能已被释放，降级到无锁路径
            }

            try
            {
                if (_disposed)
                {
                    tcs.TrySetResult(true);
                    return;
                }
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
                if (lockAcquired)
                {
                    try { _syncLock.Release(); } catch { }
                }
                try { _syncLock.Dispose(); } catch { }
            }

            GC.SuppressFinalize(this);
            tcs.TrySetResult(true);
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        /// <param name="disposing">是否为显式调用</param>
        protected virtual void Dispose(bool disposing)
        {
            if (_disposed) return;

            if (disposing)
            {
                // 同步 Dispose 也获取锁，避免与 DisposeAsync 并发归还连接造成 double-return
                bool lockAcquired = false;
                try
                {
                    _syncLock.Wait();
                    lockAcquired = true;
                }
                catch
                {
                    // 信号量可能已被释放，降级到无锁路径
                }

                try
                {
                    if (_disposed) return;
                    _logger?.LogDebug("[{SessionID}]Session disposed ({DisposeType}).", SessionID, "explicit");
                    _disposed = true;

                    if (InTransaction)
                    {
                        try
                        {
                            RollbackInternal();
                            _logger?.LogDebug("Session {SessionID} transaction rolled back successfully on dispose. ID: {TransactionID}", SessionID, _currentTransactionId);
                        }
                        catch (Exception commitEx)
                        {
                            _logger?.LogError(commitEx, "Session {SessionID} failed to roll back transaction on dispose. ID: {TransactionID}", SessionID, _currentTransactionId);
                        }
                    }
                    ReturnAllContexts();
                }
                finally
                {
                    if (lockAcquired)
                    {
                        try { _syncLock.Release(); } catch { }
                    }
                    try { _syncLock.Dispose(); } catch { }
                }
            }
            else
            {
                _disposed = true;
            }
        }
        #endregion
    }    
}
