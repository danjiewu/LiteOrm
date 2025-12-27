using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MyOrm
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Data.Common;
    using System.Linq;
    using Microsoft.Extensions.Configuration;

    namespace MyOrm
    {
        /// <summary>
        /// 数据库连接配置
        /// </summary>
        public class DbConnectionConfig
        {
            public string Name { get; set; }
            public string ConnectionString { get; set; }
            public string Provider { get; set; }
            public TimeSpan KeepAliveDuration { get; set; }
            public int PoolSize { get; set; } = 16;

            /// <summary>
            /// 获取提供程序类型
            /// </summary>
            public Type GetProviderType()
            {
                if (string.IsNullOrEmpty(Provider))
                    throw new InvalidOperationException("数据库提供程序未指定");

                var type = Type.GetType(Provider);
                if (type == null)
                    throw new TypeLoadException($"无法加载数据库提供程序类型: {Provider}");

                return type;
            }
        }

        /// <summary>
        /// DAOContextPool 工厂类
        /// </summary>
        public class DAOContextPoolFactory : IDisposable
        {
            private readonly ConcurrentDictionary<string, DAOContextPool> _pools;
            private readonly object _initLock = new object();
            private bool _disposed = false;

            private string _defaultConnectionName;
            private List<DbConnectionConfig> _connections = new List<DbConnectionConfig>();

            /// <summary>
            /// 默认连接名称
            /// </summary>
            public string DefaultConnectionName
            {
                get => _defaultConnectionName;
                set => _defaultConnectionName = value;
            }

            /// <summary>
            /// 所有连接配置
            /// </summary>
            public IReadOnlyList<DbConnectionConfig> Connections => _connections.AsReadOnly();

            /// <summary>
            /// 空构造函数
            /// </summary>
            public DAOContextPoolFactory()
            {
                _pools = new ConcurrentDictionary<string, DAOContextPool>(StringComparer.OrdinalIgnoreCase);
            }

            /// <summary>
            /// 从 IConfiguration 加载配置
            /// </summary>
            public DAOContextPoolFactory(IConfiguration configuration)
            {
                if (configuration == null)
                    throw new ArgumentNullException(nameof(configuration));

                _pools = new ConcurrentDictionary<string, DAOContextPool>(StringComparer.OrdinalIgnoreCase);
                LoadConfiguration(configuration);
            }

            /// <summary>
            /// 从 MyOrm 配置节点加载配置
            /// </summary>
            public void LoadConfiguration(IConfiguration configuration)
            {
                if (configuration == null)
                    throw new ArgumentNullException(nameof(configuration));

                lock (_initLock)
                {
                    // 清理现有连接池
                    ClearAllPools();

                    // 加载默认连接名称
                    _defaultConnectionName = configuration["DefaultConnectionName"];

                    // 加载连接配置
                    var connectionsSection = configuration.GetSection("ConnectionStrings");
                    if (connectionsSection != null && connectionsSection.GetChildren().Any())
                    {
                        _connections = new List<DbConnectionConfig>();

                        foreach (var section in connectionsSection.GetChildren())
                        {
                            var dbConfig = new DbConnectionConfig
                            {
                                Name = section["Name"],
                                ConnectionString = section["ConnectionString"],
                                Provider = section["Provider"]
                            };

                            if (TimeSpan.TryParse(section["KeepAliveDuration"], out TimeSpan timeSpan))
                                dbConfig.KeepAliveDuration = timeSpan;

                            if (Int32.TryParse(section["PoolSize"], out int poolSize))
                                dbConfig.PoolSize = poolSize;

                            _connections.Add(dbConfig);
                        }
                    }
                    else
                    {
                        // 如果没有 MyOrm.ConnectionStrings 节点，尝试直接从 ConnectionStrings 节点读取
                        var legacyConnections = configuration.GetSection("ConnectionStrings");
                        if (legacyConnections != null && legacyConnections.GetChildren().Any())
                        {
                            _connections = new List<DbConnectionConfig>();

                            foreach (var section in legacyConnections.GetChildren())
                            {
                                var dbConfig = new DbConnectionConfig
                                {
                                    Name = section.Key,
                                    ConnectionString = section.Value,
                                    Provider = section["Provider"] // 尝试获取 Provider，可能不存在
                                };

                                // 尝试从子节点获取 Provider
                                if (string.IsNullOrEmpty(dbConfig.Provider))
                                {
                                    dbConfig.Provider = section["ProviderName"];
                                }

                                _connections.Add(dbConfig);
                            }
                        }
                    }

                    // 初始化连接池
                    InitializePools();
                }
            }

            /// <summary>
            /// 添加连接配置
            /// </summary>
            public void AddConnectionConfig(DbConnectionConfig config)
            {
                if (config == null)
                    throw new ArgumentNullException(nameof(config));

                if (string.IsNullOrWhiteSpace(config.Name))
                    throw new ArgumentException("连接配置名称不能为空", nameof(config));

                lock (_initLock)
                {
                    // 检查是否已存在
                    var existing = _connections.FirstOrDefault(c =>
                        string.Equals(c.Name, config.Name, StringComparison.OrdinalIgnoreCase));

                    if (existing != null)
                    {
                        // 更新现有配置
                        existing.ConnectionString = config.ConnectionString;
                        existing.Provider = config.Provider;
                        existing.KeepAliveDuration = config.KeepAliveDuration;
                        existing.PoolSize = config.PoolSize;

                        // 重新创建连接池
                        RecreatePool(config.Name);
                    }
                    else
                    {
                        // 添加新配置
                        _connections.Add(config);
                        CreatePool(config);
                    }
                }
            }

            /// <summary>
            /// 移除连接配置
            /// </summary>
            public bool RemoveConnectionConfig(string name)
            {
                if (string.IsNullOrWhiteSpace(name))
                    return false;

                lock (_initLock)
                {
                    var config = _connections.FirstOrDefault(c =>
                        string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase));

                    if (config == null)
                        return false;

                    // 移除连接池
                    if (_pools.TryRemove(name, out var pool))
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

                    // 移除配置
                    return _connections.Remove(config);
                }
            }

            /// <summary>
            /// 获取连接配置
            /// </summary>
            public DbConnectionConfig GetConnectionConfig(string name)
            {
                if (string.IsNullOrWhiteSpace(name))
                {
                    if (!string.IsNullOrWhiteSpace(_defaultConnectionName))
                        name = _defaultConnectionName;
                    else if (_connections.Count > 0)
                        name = _connections[0].Name;
                    else
                        return null;
                }

                return _connections.FirstOrDefault(c =>
                    string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase));
            }

            /// <summary>
            /// 初始化所有连接池
            /// </summary>
            private void InitializePools()
            {
                foreach (var config in _connections)
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

            /// <summary>
            /// 创建连接池
            /// </summary>
            private void CreatePool(DbConnectionConfig config)
            {
                var pool = new DAOContextPool(config.GetProviderType(), config.ConnectionString)
                {
                    Name = config.Name,
                    PoolSize = config.PoolSize,
                    KeepAliveDuration = config.KeepAliveDuration
                };

                _pools.TryAdd(config.Name, pool);
            }

            /// <summary>
            /// 重新创建连接池
            /// </summary>
            private void RecreatePool(string name)
            {
                var config = GetConnectionConfig(name);
                if (config == null)
                    return;

                // 移除现有连接池
                if (_pools.TryRemove(name, out var oldPool))
                {
                    try
                    {
                        oldPool.Dispose();
                    }
                    catch
                    {
                        // 记录日志
                    }
                }

                // 创建新的连接池
                CreatePool(config);
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
                    if (!string.IsNullOrWhiteSpace(_defaultConnectionName))
                        name = _defaultConnectionName;
                    else if (_connections.Count > 0)
                        name = _connections[0].Name;
                    else
                        throw new InvalidOperationException("未指定连接池名称且未配置任何连接池");
                }

                if (!_pools.TryGetValue(name, out var pool))
                {
                    throw new KeyNotFoundException($"未找到连接池: {name}");
                }

                return pool;
            }

            /// <summary>
            /// 获取数据库上下文
            /// </summary>
            public DAOContext PickContext(string poolName = null)
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
            /// 获取所有已注册的连接池名称
            /// </summary>
            public IEnumerable<string> GetAllPoolNames()
            {
                return _pools.Keys;
            }

            /// <summary>
            /// 检查连接池是否存在
            /// </summary>
            public bool PoolExists(string name)
            {
                return _pools.ContainsKey(name);
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
            /// 重新加载配置
            /// </summary>
            public void ReloadConfiguration(IConfiguration configuration)
            {
                if (_disposed)
                    throw new ObjectDisposedException(nameof(DAOContextPoolFactory));

                LoadConfiguration(configuration);
            }

            /// <summary>
            /// 从连接字符串名称创建或获取连接池（支持传统连接字符串格式）
            /// </summary>
            public DAOContextPool GetOrCreatePoolFromConnectionString(string name, Type providerType, string connectionString)
            {
                if (_disposed)
                    throw new ObjectDisposedException(nameof(DAOContextPoolFactory));

                if (string.IsNullOrWhiteSpace(name))
                    throw new ArgumentException("名称不能为空", nameof(name));

                if (providerType == null)
                    throw new ArgumentNullException(nameof(providerType));

                if (string.IsNullOrWhiteSpace(connectionString))
                    throw new ArgumentException("连接字符串不能为空", nameof(connectionString));

                lock (_initLock)
                {
                    if (_pools.TryGetValue(name, out var existingPool))
                    {
                        return existingPool;
                    }

                    // 创建新的连接池
                    var pool = new DAOContextPool(providerType, connectionString)
                    {
                        Name = name,
                        PoolSize = 16 // 默认大小
                    };

                    // 创建配置
                    var config = new DbConnectionConfig
                    {
                        Name = name,
                        ConnectionString = connectionString,
                        Provider = providerType.AssemblyQualifiedName,
                        PoolSize = 16
                    };

                    _connections.Add(config);
                    _pools.TryAdd(name, pool);

                    return pool;
                }
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
}
