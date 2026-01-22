using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LiteOrm
{
    /// <summary>
    /// DAO上下文连接池，用于管理和复用数据库连接
    /// </summary>
    /// <remarks>
    /// DAOContextPool 是一个连接池管理类，用于高效地管理数据库连接，
    /// 避免频繁创建和销毁数据库连接的性能开销。
    /// 
    /// 主要功能包括：
    /// 1. 连接池管理 - 维护一个可复用的连接队列
    /// 2. 连接创建 - 按需创建新的数据库连接
    /// 3. 连接验证 - 验证池中的连接是否仍然有效
    /// 4. 连接复用 - 从池中获取可用的连接进行复用
    /// 5. 连接回收 - 将使用完的连接返回到池中
    /// 6. 生命周期管理 - 监控连接在池中的存活时间
    /// 7. 线程安全 - 使用锁机制确保多线程安全
    /// 8. 资源释放 - 实现 IDisposable 接口以正确释放所有资源
    /// 
    /// 该类通常由 DAOContextPoolFactory 进行创建和管理。
    /// 
    /// 使用示例：
    /// <code>
    /// var pool = new DAOContextPool(typeof(SqlConnection), connectionString);
    /// pool.PoolSize = 20;
    /// 
    /// // 获取连接
    /// var context = pool.PeekContext();
    /// 
    /// // 使用连接进行数据库操作
    /// // ...
    /// 
    /// // 将连接返回到池中
    /// pool.ReturnContext(context);
    /// 
    /// // 释放资源
    /// pool.Dispose();
    /// </code>
    /// </remarks>
    public class DAOContextPool : IDisposable
    {
        private readonly Queue<DAOContext> _pool = new Queue<DAOContext>();
        private readonly object _poolLock = new object();
        private bool _disposed = false;
        private readonly TaskCompletionSource<bool> _initializeTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>
        /// 获取或设置连接池的最大大小。
        /// </summary>
        public int PoolSize { get; set; } = 20;
        
        /// <summary>
        /// 获取数据库提供程序类型。
        /// </summary>
        public Type ProviderType { get; }
        
        /// <summary>
        /// 获取数据库连接字符串。
        /// </summary>
        public string ConnectionString { get; }
        
        /// <summary>
        /// 获取或设置连接在池中的最长存活时间。
        /// </summary>
        public TimeSpan KeepAliveDuration { get; set; } = TimeSpan.FromMinutes(30);
        
        /// <summary>
        /// 获取或设置连接池的名称。
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 初始化 <see cref="DAOContextPool"/> 类的新实例。
        /// </summary>
        /// <param name="providerType">数据库提供程序类型。</param>
        /// <param name="connectionString">数据库连接字符串。</param>
        /// <exception cref="ArgumentNullException">当 <paramref name="providerType"/> 或 <paramref name="connectionString"/> 为 null 时抛出。</exception>
        public DAOContextPool(Type providerType, string connectionString)
        {
            ProviderType = providerType ?? throw new ArgumentNullException(nameof(providerType));
            ConnectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
            Name = providerType.Name;
        }

        /// <summary>
        /// 标记连接池已完成初始化并可以提供服务。
        /// </summary>
        public void MarkInitialized()
        {
            _initializeTcs.TrySetResult(true);
        }

        /// <summary>
        /// 等待连接池初始化完成。
        /// </summary>
        /// <returns>返回等待任务。</returns>
        public Task WaitForInitializationAsync()
        {
            return _initializeTcs.Task;
        }

        /// <summary>
        /// 从连接池中获取一个可用的DAO上下文。
        /// </summary>
        /// <returns>一个可用的 <see cref="DAOContext"/> 实例。</returns>
        /// <exception cref="ObjectDisposedException">当连接池已被释放时抛出。</exception>
        public DAOContext PeekContext()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(DAOContextPool));

            // 等待初始化完成（如自动建表同步）
            _initializeTcs.Task.Wait();

            return PeekContextInternal();
        }

        /// <summary>
        /// 内部获取DAO上下文，不进行初始化检查。
        /// </summary>
        /// <returns>一个可用的 <see cref="DAOContext"/> 实例。</returns>
        internal DAOContext PeekContextInternal()
        {
            lock (_poolLock)
            {
                // 尝试从池中获取可用的上下文
                while (_pool.Count > 0)
                {
                    var context = _pool.Dequeue();

                    // 检查连接是否仍然有效
                    if (IsContextValid(context))
                    {
                        context.EnsureConnectionOpen();
                        return context;
                    }

                    // 无效则销毁
                    context.Dispose();
                }

                // 池为空，创建新连接
                return CreateNewContext();
            }
        }


        /// <summary>
        /// 将DAO上下文返回到连接池中。
        /// </summary>
        /// <param name="context">要返回的DAO上下文。</param>
        /// <exception cref="ArgumentNullException">当 <paramref name="context"/> 为 null 时抛出。</exception>
        public void ReturnContext(DAOContext context)
        {
            if (context is null)
                throw new ArgumentNullException(nameof(context));

            if (_disposed)
            {
                context.Dispose();
                return;
            }

            lock (_poolLock)
            {
                // 重置上下文状态
                context.Reset();

                // 如果连接无效，销毁
                if (!IsContextValid(context))
                {
                    context.Dispose();
                    return;
                }

                // 如果池已满，销毁多余连接
                if (_pool.Count >= PoolSize)
                {
                    _pool.Dequeue()?.Dispose();
                    return;
                }

                _pool.Enqueue(context);
            }
        }

        private bool IsContextValid(DAOContext context)
        {
            if (context is null)
                return false;

            // 检查连接是否存活
            if (KeepAliveDuration != TimeSpan.Zero &&
                context.LastActiveTime + KeepAliveDuration < DateTime.Now)
            {
                return false;
            }

            // 检查连接状态
            try
            {
                var connection = context.DbConnection;
                if (connection.State == ConnectionState.Broken)
                    return false;

                return true;
            }
            catch
            {
                return false;
            }
        }

        private DAOContext CreateNewContext()
        {
            var connection = Activator.CreateInstance(ProviderType) as DbConnection;
            if (connection is null)
                throw new InvalidOperationException($"无法创建类型为 {ProviderType} 的数据库连接");

            connection.ConnectionString = ConnectionString;

            var context = new DAOContext(connection, this);

            return context;
        }


        /// <summary>
        /// 释放连接池使用的所有资源。
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// 释放连接池使用的非托管资源，并可选择释放托管资源。
        /// </summary>
        /// <param name="disposing">true 表示释放托管资源和非托管资源；false 表示仅释放非托管资源。</param>
        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            if (disposing)
            {
                lock (_poolLock)
                {
                    while (_pool.Count > 0)
                    {
                        _pool.Dequeue()?.Dispose();
                    }

                    _disposed = true;
                }
            }
        }
    }
}
