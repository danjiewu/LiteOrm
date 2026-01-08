using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MyOrm.Common;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MyOrm
{
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

        public DataSourceProvider(IConfiguration configuration)
        {
            if (configuration == null)
                throw new ArgumentNullException(nameof(configuration));
            LoadConfiguration(configuration.GetSection("MyOrm"));
        }

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
        /// 从 MyOrm 配置节点加载配置
        /// </summary>
        public void LoadConfiguration(IConfiguration configuration)
        {
            if (configuration == null)
                throw new ArgumentNullException(nameof(configuration));

            // 加载默认连接名称
            DefaultDataSourceName = configuration["Default"];

            // 加载连接配置
            var connectionsSection = configuration.GetSection("ConnectionStrings");
            if (connectionsSection != null && connectionsSection.GetChildren().Any())
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

        public ICollection<DataSourceConfig> DataSources => _connections.Values;

        public IEnumerator<DataSourceConfig> GetEnumerator()
        {
            return _connections.Values.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
