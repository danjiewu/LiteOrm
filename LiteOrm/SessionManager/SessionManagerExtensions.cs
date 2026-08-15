using Microsoft.Extensions.Logging;
using System;
using System.Data;
using System.Threading.Tasks;

namespace LiteOrm
{
    /// <summary>
    /// 会话管理器扩展方法
    /// </summary>
    public static class SessionManagerExtensions
    {
        /// <summary>
        /// 创建新的 SessionManager 实例
        /// </summary>
        /// <param name="factory">DAOContext 连接池工厂类</param>
        /// <param name="logger">日志输出</param>
        /// <returns> SessionManager 实例 </returns>
        public static SessionManager NewSession(this DAOContextPoolFactory factory, ILogger<SessionManager>? logger = null)
        {
            return new SessionManager(factory, logger);
        }

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
            catch (Exception original)
            {
                // 回滚失败不掩盖原始异常
                try { sessionManager.Rollback(); }
                catch (Exception rollbackEx)
                {
                    throw new AggregateException(original, rollbackEx);
                }
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
            catch (Exception original)
            {
                // 回滚失败不掩盖原始异常；容忍 SessionManager 已释放
                try
                {
                    await sessionManager.RollbackAsync().ConfigureAwait(false);
                }
                catch (Exception rollbackEx) when (rollbackEx is ObjectDisposedException)
                {
                    throw;
                }
                catch (Exception rollbackEx)
                {
                    throw new AggregateException(original, rollbackEx);
                }
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
