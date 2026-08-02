using LiteOrm.Common;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;

namespace LiteOrm
{
    /// <summary>
    /// 数据源配置扩展 - 从 <see cref="IConfiguration"/> 加载连接配置。
    /// </summary>
    /// <remarks>
    /// 提供从宿主应用的 <c>LiteOrm</c> 配置节点读取连接配置并填充到 <see cref="DataSourceProvider"/> 的扩展方法。
    /// </remarks>
    public static class DataSourceProviderExtensions
    {
        /// <summary>
        /// 从 LiteOrm 配置节点加载数据源配置。
        /// </summary>
        /// <param name="provider">数据源提供程序。</param>
        /// <param name="configuration">LiteOrm 配置节点。</param>
        /// <returns>数据源提供程序。</returns>
        public static DataSourceProvider LoadConfiguration(this DataSourceProvider provider, IConfiguration configuration)
        {
            if (provider is null) throw new ArgumentNullException(nameof(provider));
            if (configuration is null) throw new ArgumentNullException(nameof(configuration));

            // 加载默认连接名称
            var defaultName = configuration["Default"];
            if (!string.IsNullOrWhiteSpace(defaultName))
            {
                provider.DefaultDataSourceName = defaultName;
            }

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
                    config.ReadOnlyConfigs.Add(readOnlyConfig);
                }
                connections.Add(config);
            }

            // 如果配置中定义了有效的数据源集合，则更新
            foreach (var config in connections)
            {
                if (!string.IsNullOrEmpty(config.Name))
                {
                    provider.AddDataSource(config);
                }
            }

            return provider;
        }
    }
}
