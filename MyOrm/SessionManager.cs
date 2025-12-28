using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Threading;
using System.Threading.Tasks;

namespace MyOrm
{
    public class SessionManager : IDisposable
    {
        private readonly DAOContextPoolFactory _daoContextPoolFactory;
        private readonly ILogger<SessionManager> _logger;
        private readonly object _syncLock = new object();
        private bool _disposed = false;

        private ConcurrentDictionary<string, DAOContext> _daoContexts = new ConcurrentDictionary<string, DAOContext>(StringComparer.OrdinalIgnoreCase);
        private Stack<string> _sqlStack = new Stack<string>();
        private string _currentTransactionId;
        private IsolationLevel _currentIsolationLevel = IsolationLevel.ReadCommitted;
        private static readonly AsyncLocal<SessionManager> _currentAsyncLocal = new AsyncLocal<SessionManager>();

        /// <summary>
        /// 当前异步上下文的会话管理器
        /// </summary>
        public static SessionManager Current
        {
            get => _currentAsyncLocal.Value;
            internal set => _currentAsyncLocal.Value = value;
        }

        /// <summary>
        /// 构造函数
        /// </summary>
        public SessionManager(DAOContextPoolFactory daoContextPoolFactory, ILogger<SessionManager> logger = null)
        {
            _daoContextPoolFactory = daoContextPoolFactory ?? throw new ArgumentNullException(nameof(daoContextPoolFactory));
            _logger = logger;
        }

        /// <summary>
        /// 进入当前上下文，置 SessionManager.Current 为为当前实例的副本
        /// </summary>
        /// <returns>上下文作用域对象，在 Dispose 时恢复之前的 Current</returns>
        public IDisposable EnterContext()
        {
            // 保存当前的 Current
            var previousCurrent = Current;

            // 返回一个作用域对象，在作用域结束时恢复之前的 Current
            return new ContextScope(CreateCopy());
        }

        /// <summary>
        /// 创建当前 SessionManager 的一个副本
        /// </summary>
        /// <returns>新的 SessionManager 实例</returns>
        public SessionManager CreateCopy()
        {
            return new SessionManager(
                _daoContextPoolFactory,
                _logger
            );
        }

        /// <summary>
        /// SQL语句堆栈（用于调试）
        /// </summary>
        public Stack<string> SqlStack => _sqlStack;

        /// <summary>
        /// 是否在事务中
        /// </summary>
        public bool InTransaction => !string.IsNullOrEmpty(_currentTransactionId);

        /// <summary>
        /// 当前事务ID
        /// </summary>
        public string CurrentTransactionId => _currentTransactionId;

        /// <summary>
        /// 启动会话（清除所有状态）
        /// </summary>
        public bool Start()
        {
            lock (_syncLock)
            {
                // 如果还有活动的事务，回滚
                if (InTransaction)
                {
                    try
                    {
                        RollbackInternal();
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, $"重置时回滚事务失败");
                    }
                }

                _sqlStack.Clear();
                return true;
            }
        }

        /// <summary>
        /// 完成会话（归还所有连接）
        /// </summary>
        public bool Finish()
        {
            lock (_syncLock)
            {
                try
                {
                    if (!InTransaction)
                    {
                        CommitInternal();
                    }
                }
                finally
                {
                    ReturnAllContexts();
                }
                return true;
            }
        }

        /// <summary>
        /// 开始事务
        /// </summary>
        /// <param name="isolationLevel">隔离级别</param>
        /// <returns>是否成功开始</returns>
        public bool BeginTransaction(IsolationLevel isolationLevel = IsolationLevel.ReadCommitted)
        {
            lock (_syncLock)
            {
                if (InTransaction)
                {
                    _logger?.LogWarning("已经在事务中，无法开始新事务");
                    return false;
                }

                _currentTransactionId = Guid.NewGuid().ToString();
                _currentIsolationLevel = isolationLevel;

                _logger?.LogDebug($"开始事务。Transaction ID: {_currentTransactionId}, 隔离级别: {isolationLevel}");

                // 为所有已存在的上下文开启事务
                foreach (var context in _daoContexts.Values)
                {
                    try
                    {
                        if (!context.InTransaction)
                        {
                            context.BeginTransaction(isolationLevel);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, $"为连接池 {context.Pool?.Name} 开启事务失败");
                        // 如果某个连接开启事务失败，回滚并抛出异常
                        RollbackInternal();
                        throw new InvalidOperationException($"开启事务失败: {ex.Message}", ex);
                    }
                }

                return true;
            }
        }

        /// <summary>
        /// 提交事务
        /// </summary>
        /// <returns>是否成功提交</returns>
        public bool Commit()
        {
            lock (_syncLock)
            {
                if (!InTransaction)
                {
                    _logger?.LogWarning("不在事务中，无法提交");
                    return false;
                }

                return CommitInternal();
            }
        }

        /// <summary>
        /// 回滚事务
        /// </summary>
        /// <returns>是否成功回滚</returns>
        public bool Rollback()
        {
            lock (_syncLock)
            {
                if (!InTransaction)
                {
                    _logger?.LogWarning("不在事务中，无法回滚");
                    return false;
                }

                return RollbackInternal();
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
                    if (context.InTransaction)
                    {
                        context.Commit();
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, $"提交事务失败。连接池: {context.Pool?.Name}");
                    success = false;
                }
            }

            // 清理事务状态
            _currentTransactionId = null;

            _logger?.LogDebug($"事务提交完成。Transaction ID: {_currentTransactionId}, 成功: {success}");

            if (!success)
            {
                throw new InvalidOperationException("提交事务时发生错误");
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
                    if (context.InTransaction)
                    {
                        context.Rollback();
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, $"回滚事务失败。连接池: {context.Pool?.Name}");
                    success = false;
                }
            }

            // 清理事务状态
            _currentTransactionId = null;

            _logger?.LogDebug($"事务回滚完成。Transaction ID: {_currentTransactionId}, 返回: {success}");

            if (!success)
            {
                throw new InvalidOperationException("回滚事务时发生错误");
            }

            return success;
        }

        /// <summary>
        /// Retrieves a DAOContext instance associated with the specified name, creating or obtaining it from the pool
        /// if necessary.
        /// </summary>
        /// <remarks>If a transaction is active and the retrieved context is not already in a transaction,
        /// a transaction is automatically started on the context. The returned context is cached for subsequent calls
        /// with the same name within the current scope.</remarks>
        /// <param name="name">The name of the DAO context to retrieve. If null, the default context is used.</param>
        /// <returns>A DAOContext instance corresponding to the specified name. If the context does not exist, a new one is
        /// obtained from the pool.</returns>
        public DAOContext GetDaoContext(string name = null)
        {
            lock (_syncLock)
            {
                if (!_daoContexts.TryGetValue(name, out DAOContext context))
                {
                    // 从工厂获取上下文
                    context = _daoContextPoolFactory.GetPool(name).PeekContext();

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
                            _daoContextPoolFactory.ReturnContext(context);
                            _logger?.LogError(ex, $"开启事务失败。连接池: {name}");
                            throw;
                        }
                    }

                    _daoContexts[name] = context;
                }

                return context;
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
                    if (context.Pool != null)
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
                    _logger?.LogError(ex, $"归还连接失败。连接池: {context.Pool?.Name}");
                }
            }
            _daoContexts.Clear();
        }

        #region IDisposable 实现

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed) return;

            if (disposing)
            {
                // 如果有活动的事务，回滚
                if (InTransaction)
                {
                    try
                    {
                        // 尝试回滚事务
                        RollbackInternal();
                        _logger?.LogDebug("Dispose时回滚事务成功");
                    }
                    catch (Exception commitEx)
                    {
                        _logger?.LogError(commitEx, "Dispose时回滚事务失败");
                    }
                }
                //归还所有连接
                ReturnAllContexts();
            }
            _disposed = true;
        }

        ~SessionManager()
        {
            Dispose(false);
        }

        /// <summary>
        /// 上下文作用域
        /// </summary>
        private class ContextScope : IDisposable
        {
            private readonly SessionManager _prevSessionManager;
            private readonly SessionManager _sessionManager;
            private bool _disposed = false;

            public ContextScope(SessionManager sessionManager)
            {                
                _sessionManager = sessionManager?? throw new ArgumentNullException(nameof(sessionManager));  
                _prevSessionManager = Current;
                Current = _sessionManager;
            }

            public void Dispose()
            {
                if (_disposed) return;
                Current = _prevSessionManager;
                _sessionManager.Dispose();
                _disposed = true;
            }
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
            if (sessionManager == null)
                throw new ArgumentNullException(nameof(sessionManager));

            if (action == null)
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
            if (sessionManager == null)
                throw new ArgumentNullException(nameof(sessionManager));

            if (action == null)
                throw new ArgumentNullException(nameof(action));

            sessionManager.BeginTransaction(isolationLevel);
            try
            {
                var result = await action(sessionManager);
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
        /// 执行异步事务操作（无返回值）
        /// </summary>
        public static async Task ExecuteInTransactionAsync(this SessionManager sessionManager, Func<SessionManager, Task> action,
            IsolationLevel isolationLevel = IsolationLevel.ReadCommitted)
        {
            await ExecuteInTransactionAsync(sessionManager, async sm =>
            {
                await action(sm);
                return true;
            }, isolationLevel);
        }
    }
}