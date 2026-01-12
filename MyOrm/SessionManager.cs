using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MyOrm.Common;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Threading;
using System.Threading.Tasks;

namespace MyOrm
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
    /// 9. 批量操作支持 - 提供批量操作的上下文管理
    /// 
    /// 该类通过依赖注入框架以 Scoped 方式注册，每个请求/任务有一个实例。
    /// 使用 AsyncLocal 确保在异步调用中正确维护会话上下文。
    /// 
    /// 使用示例：
    /// <code>
    /// var sessionManager = serviceProvider.GetRequiredService&lt;SessionManager&gt;();
    /// 
    /// // 进入会话
    /// using (sessionManager.Enter())
    /// {
    ///     try
    ///     {
    ///         // 开始事务
    ///         sessionManager.BeginTransaction();
    ///         
    ///         // 执行数据库操作
    ///         var user = userService.GetObject(userId);
    ///         user.Name = \"New Name\";
    ///         userService.Update(user);
    ///         
    ///         // 提交事务
    ///         sessionManager.CommitTransaction();
    ///     }
    ///     catch
    ///     {
    ///         // 回滚事务
    ///         sessionManager.RollbackTransaction();
    ///         throw;
    ///     }
    /// }
    /// 
    /// // 异步操作
    /// await sessionManager.ExecuteInSessionAsync(async () =&gt;
    /// {
    ///     var data = await service.GetAsync(id);
    ///     return data;
    /// });
    /// </code>
    /// </remarks>
    [AutoRegister(ServiceLifetime.Scoped)]
    public class SessionManager : IDisposable
    {
        private readonly DAOContextPoolFactory _daoContextPoolFactory;
        private readonly ILogger<SessionManager> _logger;
        private readonly object _syncLock = new object();
        private bool _disposed = false;

        private ConcurrentDictionary<string, DAOContext> _daoContexts = new ConcurrentDictionary<string, DAOContext>(StringComparer.OrdinalIgnoreCase);
        private LinkedList<string> _sqlStack = new LinkedList<string>();
        private string _currentTransactionId;
        private IsolationLevel _currentIsolationLevel = IsolationLevel.ReadCommitted;
        private static readonly AsyncLocal<SessionManager> _currentAsyncLocal = new AsyncLocal<SessionManager>();

        /// <summary>
        /// 当前异步上下文的会话管理器
        /// </summary>
        public static SessionManager Current
        {
            get => _currentAsyncLocal.Value;
            set => _currentAsyncLocal.Value = value;
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
        public IDisposable Enter(bool newSession = true)
        {
            if (newSession)
                // 返回一个作用域对象，在作用域结束时恢复之前的 Current
                return new ContextScope(CreateCopy());
            else
                return new ContextScope(this);
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
                    if (InTransaction)
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
        /// 将SQL语句推入堆栈（用于调试和日志记录）
        /// </summary>
        /// <param name="sql">SQL语句</param>
        public void PushSql(string sql)
        {
            lock (_syncLock)
            {
                _sqlStack.AddFirst(sql);
                while (_sqlStack.Count > 20)
                {
                    _sqlStack.RemoveLast();
                }
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
        /// 获取指定名称的DAO上下文
        /// </summary>
        /// <param name="name">上下文名称，如果为null则使用默认名称"_"</param>
        /// <returns>DAO上下文实例</returns>
        public DAOContext GetDaoContext(string name = null)
        {
            if (name is null) name = "_";
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
                    _logger?.LogError(ex, $"归还连接失败。连接池: {context.Pool?.Name}");
                }
            }
            _daoContexts.Clear();
        }

        #region IDisposable 实现

        ///<inheritdoc/> 
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
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

        /// <summary>
        /// 析构函数
        /// </summary>
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
                _sessionManager = sessionManager ?? throw new ArgumentNullException(nameof(sessionManager));
                _prevSessionManager = Current;
                Current = _sessionManager;
            }

            public void Dispose()
            {
                if (_disposed) return;
                // 即使Dispose抛出异常，也要尝试恢复之前的SessionManager
                try
                {
                    _sessionManager.Dispose();
                }
                finally
                {
                    Current = _prevSessionManager;
                    _disposed = true;
                }
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

        /// <summary>
        /// 在会话中异步执行函数
        /// </summary>
        /// <typeparam name="TResult">返回类型</typeparam>
        /// <param name="sessionManager">会话管理器</param>
        /// <param name="func">要执行的函数</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>异步任务结果</returns>
        public static Task<TResult> ExecuteInSessionAsync<TResult>(
            this SessionManager sessionManager,
            Func<TResult> func,
            CancellationToken cancellationToken = default)
        {
            return Task.Run(() =>
            {
                using (var session = sessionManager.Enter())
                {
                    return func();
                }
            }, cancellationToken);
        }

        /// <summary>
        /// 在会话中异步执行操作
        /// </summary>
        /// <param name="sessionManager">会话管理器</param>
        /// <param name="action">要执行的操作</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>异步任务</returns>
        public static Task ExecuteInSessionAsync(
            this SessionManager sessionManager,
            Action action,
            CancellationToken cancellationToken = default)
        {
            return Task.Run(() =>
            {
                using (var session = sessionManager.Enter())
                {
                    action();
                }
            }, cancellationToken);
        }
    }
}
