using MyOrm.Common;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MyOrm
{
    /// <summary>
    /// DAOContextPool 工厂类
    /// </summary>
    public class DAOContextPoolFactory : IDisposable
    {
        private readonly ConcurrentDictionary<string, DAOContextPool> _pools = new(StringComparer.OrdinalIgnoreCase);
        private bool _disposed = false;
        private readonly object _initLock = new object();
        private readonly IDataSourceProvider _dataSourceProvider;

        /// <summary>
        /// 空构造函数
        /// </summary>
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
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            if (context.Pool == null)
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

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

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
