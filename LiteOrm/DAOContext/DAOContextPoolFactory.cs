using Microsoft.Extensions.DependencyInjection;
using LiteOrm.Common;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LiteOrm
{
    /// <summary>
    /// DAOContext 连接池工厂类 - 管理多个数据源的连接池
    /// </summary>
    /// <remarks>
    /// DAOContextPoolFactory 是一个工厂类，负责为每个配置的数据源创建和管理对应的连接池。
    /// 
    /// 主要功能包括：
    /// 1. 连接池创建 - 根据数据源配置创建对应的连接池
    /// 2. 连接池管理 - 管理多个数据源的连接池
    /// 3. 连接池获取 - 根据数据源名称获取对应的连接池
    /// 4. 自动初始化 - 在构造时自动初始化所有配置的连接池
    /// 5. 线程安全 - 使用 ConcurrentDictionary 和锁确保线程安全
    /// 6. 资源管理 - 实现 IDisposable 接口确保所有连接池资源正确释放
    /// 7. 生命周期管理 - 管理连接池的创建和销毁生命周期
    /// 
    /// 该类通过依赖注入框架以单例方式注册，通常由 SessionManager 使用来获取连接。
    /// 
    /// 使用示例：
    /// <code>
    /// var factory = serviceProvider.GetRequiredService&lt;DAOContextPoolFactory&gt;();
    /// 
    /// // 获取指定数据源的连接池
    /// var pool = factory.GetDataSourcePool(\"DefaultConnection\");
    /// 
    /// // 从连接池中获取连接
    /// var context = pool.PeekContext();
    /// 
    /// // 使用连接进行数据库操作
    /// // ...
    /// 
    /// // 将连接返回到池中
    /// pool.ReturnContext(context);
    /// </code>
    /// </remarks>
    [AutoRegister(ServiceLifetime.Singleton)]
    public class DAOContextPoolFactory : IDisposable
    {
        private readonly ConcurrentDictionary<string, DAOContextPool> _pools = new(StringComparer.OrdinalIgnoreCase);
        private bool _disposed = false;
        private readonly object _initLock = new object();
        private readonly IDataSourceProvider _dataSourceProvider;

        /// <summary>
        /// 初始化 <see cref="DAOContextPoolFactory"/> 类的新实例。
        /// </summary>
        /// <param name="dataSourceProvider">数据源提供程序。</param>
        /// <exception cref="ArgumentNullException">当 <paramref name="dataSourceProvider"/> 为 null 时抛出。</exception>
        public DAOContextPoolFactory(IDataSourceProvider dataSourceProvider)
        {
            _dataSourceProvider = dataSourceProvider ?? throw new ArgumentNullException(nameof(dataSourceProvider));
            InitializePools();
        }

        /// <summary>
        /// 初始化所有连接池
        /// </summary>
        private void InitializePools()
        {
            lock (_initLock)
            {
                foreach (var config in _dataSourceProvider)
                {
                    if (string.IsNullOrWhiteSpace(config.Name))
                    {
                        throw new InvalidOperationException("连接配置中 Name 不能为空");
                    }

                    if (_pools.ContainsKey(config.Name))
                    {
                        throw new InvalidOperationException($"重复的连接池名称: {config.Name}");
                    }

                    CreatePool(config);
                }
            }
        }

        /// <summary>
        /// 创建连接池
        /// </summary>
        private void CreatePool(DataSourceConfig config)
        {
            var pool = new DAOContextPool(config.ProviderType, config.ConnectionString)
            {
                Name = config.Name,
                PoolSize = config.PoolSize,
                KeepAliveDuration = config.KeepAliveDuration
            };

            _pools.TryAdd(config.Name, pool);
        }

        /// <summary>
        /// 获取连接池
        /// </summary>
        public DAOContextPool GetPool(string name = null)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(DAOContextPoolFactory));
            if (string.IsNullOrWhiteSpace(name))
            {
                name = _dataSourceProvider.DefaultDataSourceName;
            }
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("连接池名称不能为空", nameof(name));
            }
            return _pools[name];
        }

        /// <summary>
        /// 获取数据库上下文
        /// </summary>
        public DAOContext PeekContext(string poolName = null)
        {
            var pool = GetPool(poolName);
            return pool.PeekContext();
        }

        /// <summary>
        /// 返回数据库上下文到连接池
        /// </summary>
        public void ReturnContext(DAOContext context)
        {
            if (context is null)
                throw new ArgumentNullException(nameof(context));

            if (context.Pool is null)
            {
                throw new InvalidOperationException("DAOContext 没有关联的连接池");
            }

            context.Pool.ReturnContext(context);
        }


        /// <summary>
        /// 清除所有连接池
        /// </summary>
        public void ClearAllPools()
        {
            lock (_initLock)
            {
                foreach (var pool in _pools.Values)
                {
                    try
                    {
                        pool.Dispose();
                    }
                    catch
                    {
                        // 记录日志
                    }
                }

                _pools.Clear();
            }
        }

        /// <summary>
        /// 检查连接池是否存在
        /// </summary>
        public bool PoolExists(string name)
        {
            return _pools.ContainsKey(name);
        }

        /// <summary>
        /// 释放工厂使用的所有资源。
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// 释放工厂使用的非托管资源，并可选择释放托管资源。
        /// </summary>
        /// <param name="disposing">true 表示释放托管资源和非托管资源；false 表示仅释放非托管资源。</param>
        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            if (disposing)
            {
                ClearAllPools();
            }

            _disposed = true;
        }
    }
}
