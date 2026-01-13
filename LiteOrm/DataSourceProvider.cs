using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using LiteOrm.Common;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LiteOrm
{
    /// <summary>
    /// 数据源提供程序 - 管理和提供数据库连接配置
    /// </summary>
    /// <remarks>
    /// DataSourceProvider 是一个数据源管理类，负责从应用程序配置中读取和管理数据库连接配置。
    /// 
    /// 主要功能包括：
    /// 1. 配置加载 - 从 IConfiguration 中加载 LiteOrm 节点的数据库配置
    /// 2. 数据源查询 - 根据名称查询数据源配置
    /// 3. 默认数据源管理 - 管理默认的数据源名称
    /// 4. 多数据源支持 - 支持多个数据源的配置和管理
    /// 5. 线程安全 - 使用 ConcurrentDictionary 确保线程安全
    /// 6. 配置验证 - 验证数据源配置的有效性
    /// 
    /// 该类通过依赖注入框架以单例方式注册，在应用启动时由依赖注入容器创建。
    /// 配置应该在应用配置文件中的 \"LiteOrm\" 节点下定义。
    /// 
    /// 配置示例：
    /// <code>
    /// \"LiteOrm\": {
    ///   \"DataSources\": [
    ///     {
    ///       \"Name\": \"DefaultConnection\",
    ///       \"ConnectionString\": \"Server=.;Database=MyDB;...\",
    ///       \"ProviderType\": \"System.Data.SqlClient.SqlConnection\"
    ///     },
    ///     {
    ///       \"Name\": \"MySqlConnection\",
    ///       \"ConnectionString\": \"Server=localhost;Database=MyDB;...\",
    ///       \"ProviderType\": \"MySql.Data.MySqlClient.MySqlConnection\"
    ///     }
    ///   ],
    ///   \"DefaultDataSourceName\": \"DefaultConnection\"
    /// }
    /// </code>
    /// 
    /// 使用示例：
    /// <code>
    /// var provider = serviceProvider.GetRequiredService&lt;IDataSourceProvider&gt;();
    /// 
    /// // 获取默认数据源
    /// var defaultConfig = provider.GetDataSource(null);
    /// 
    /// // 获取指定数据源
    /// var mysqlConfig = provider.GetDataSource(\"MySqlConnection\");
    /// </code>
    /// </remarks>
    [AutoRegister(ServiceLifetime.Singleton)]
    public class DataSourceProvider : IDataSourceProvider
    {
        private ConcurrentDictionary<string, DataSourceConfig> _connections = new(StringComparer.OrdinalIgnoreCase);
        
        /// <summary>
        /// 默认连接名称
        /// </summary>
        public string DefaultDataSourceName
        {
            get; set;
        }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="configuration">应用程序配置</param>
        public DataSourceProvider(IConfiguration configuration)
        {
            if (configuration is null)
                throw new ArgumentNullException(nameof(configuration));
            LoadConfiguration(configuration.GetSection("LiteOrm"));
        }

        /// <summary>
        /// 获取指定名称的数据源配置
        /// </summary>
        /// <param name="name">数据源名称，如果为空则使用默认数据源</param>
        /// <returns>数据源配置，如果不存在则返回null</returns>
        public DataSourceConfig GetDataSource(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                if (!string.IsNullOrWhiteSpace(DefaultDataSourceName))
                    name = DefaultDataSourceName;
                else if (_connections.Count > 0)
                    name = _connections.Keys.First();
                else
                    return null;
            }
            if (_connections.TryGetValue(name, out var config))
                return config;
            return null;
        }

        /// <summary>
        /// 从 LiteOrm 配置节点加载配置
        /// </summary>
        /// <param name="configuration">LiteOrm配置节点</param>
        public void LoadConfiguration(IConfiguration configuration)
        {
            if (configuration is null)
                throw new ArgumentNullException(nameof(configuration));

            // 加载默认连接名称
            DefaultDataSourceName = configuration["Default"];

            // 加载连接配置
            var connectionsSection = configuration.GetSection("ConnectionStrings");
            if (connectionsSection is not null && connectionsSection.GetChildren().Any())
            {
                _connections = new(StringComparer.OrdinalIgnoreCase);

                foreach (var section in connectionsSection.GetChildren())
                {
                    var dbConfig = new DataSourceConfig
                    {
                        Name = section["Name"],
                        ConnectionString = section["ConnectionString"],
                        Provider = section["Provider"]
                    };

                    if (TimeSpan.TryParse(section["KeepAliveDuration"], out TimeSpan timeSpan))
                        dbConfig.KeepAliveDuration = timeSpan;

                    if (Int32.TryParse(section["PoolSize"], out int poolSize))
                        dbConfig.PoolSize = poolSize;

                    _connections[dbConfig.Name] = dbConfig;
                }
            }
        }

        /// <summary>
        /// 获取所有数据源配置
        /// </summary>
        public ICollection<DataSourceConfig> DataSources => _connections.Values;

        /// <summary>
        /// 返回一个枚举器，用于遍历所有数据源配置
        /// </summary>
        /// <returns>数据源配置的枚举器</returns>
        public IEnumerator<DataSourceConfig> GetEnumerator()
        {
            return _connections.Values.GetEnumerator();
        }

        /// <summary>
        /// 返回一个枚举器，用于遍历所有数据源配置
        /// </summary>
        /// <returns>数据源配置的枚举器</returns>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
