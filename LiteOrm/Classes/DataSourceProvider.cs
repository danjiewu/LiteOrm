using LiteOrm.Common;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

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
    ///{
    ///  "LiteOrm": {
    ///    "Default": "DefaultConnection",
    ///    "DataSources": [
    ///      {
    ///        "Name": "DefaultConnection",
    ///        "ConnectionString": "Data Source=demo.db",
    ///        "Provider": "Microsoft.Data.Sqlite.SqliteConnection, Microsoft.Data.Sqlite",
    ///        "PoolSize": 10
    ///      }
    ///    ]
    ///  }
    ///}
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
    [AutoRegister(Lifetime.Singleton)]
    public class DataSourceProvider : IDataSourceProvider
    {
        /// <summary>
        /// 存储数据源配置的内部缓存，键为数据源名称（不区分大小写）
        /// </summary>
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

            // 从配置节点中读取 "DataSources" 节并映射为 DataSourceConfig 列表
            var dataSourcesSection = configuration.GetSection("DataSources");
            var connections = new List<DataSourceConfig>();

            foreach (var section in dataSourcesSection.GetChildren())
            {
                var config = new DataSourceConfig
                {
                    Name = section["Name"],
                    ConnectionString = section["ConnectionString"],
                    Provider = section["Provider"],
                    SqlBuilder = section["SqlBuilder"]
                };

                if (int.TryParse(section["PoolSize"], out var poolSize)) config.PoolSize = poolSize;
                if (int.TryParse(section["MaxPoolSize"], out var maxPoolSize)) config.MaxPoolSize = maxPoolSize;
                if (int.TryParse(section["ParamCountLimit"], out var paramLimit)) config.ParamCountLimit = paramLimit;
                if (bool.TryParse(section["SyncTable"], out var syncTable)) config.SyncTable = syncTable;
                if (TimeSpan.TryParse(section["KeepAliveDuration"], out var keepAlive)) config.KeepAliveDuration = keepAlive;

                foreach (var readOnlySection in section.GetSection("ReadOnlyConfigs").GetChildren())
                {
                    var readOnlyConfig = new ReadOnlyDataSourceConfig
                    {
                        ConnectionString = readOnlySection["ConnectionString"],
                        Provider = readOnlySection["Provider"],
                        SqlBuilder = readOnlySection["SqlBuilder"],
                        PoolSize = int.TryParse(readOnlySection["PoolSize"], out var roPoolSize) ? roPoolSize : config.PoolSize,
                        MaxPoolSize = int.TryParse(readOnlySection["MaxPoolSize"], out var roMaxPoolSize) ? roMaxPoolSize : config.MaxPoolSize,
                        ParamCountLimit = int.TryParse(readOnlySection["ParamCountLimit"], out var roParamLimit) ? roParamLimit : config.ParamCountLimit,
                        KeepAliveDuration = TimeSpan.TryParse(readOnlySection["KeepAliveDuration"], out var roKeepAlive) ? roKeepAlive : config.KeepAliveDuration
                    };
                    config.ReadOnlyConfigs ??= new List<ReadOnlyDataSourceConfig>();
                    config.ReadOnlyConfigs.Add(readOnlyConfig);
                }
                connections.Add(config);
            }

            // 如果配置中定义了有效的数据源集合，则更新内部缓存
            if (connections != null && connections.Any())
            {
                _connections = new(StringComparer.OrdinalIgnoreCase);
                foreach (var config in connections)
                {
                    if (!string.IsNullOrEmpty(config.Name))
                        _connections[config.Name] = config;
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
