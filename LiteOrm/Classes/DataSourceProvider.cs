using LiteOrm.Common;
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
    /// DataSourceProvider 是一个数据源管理类，负责管理数据库连接配置。LiteOrm 核心不再依赖
    /// Microsoft.Extensions.Configuration，连接配置通过显式 API（<see cref="AddDataSource"/>）手工提供。
    ///
    /// 主要功能包括：
    /// 1. 数据源添加 - 通过 <see cref="AddDataSource"/> 显式添加数据源配置
    /// 2. 数据源查询 - 根据名称查询数据源配置
    /// 3. 默认数据源管理 - 管理默认的数据源名称
    /// 4. 多数据源支持 - 支持多个数据源的配置和管理
    /// 5. 线程安全 - 使用 ConcurrentDictionary 确保线程安全
    /// 6. 配置验证 - 验证数据源配置的有效性
    ///
    /// 使用示例：
    /// <code>
    /// var provider = new DataSourceProvider();
    /// provider.AddDataSource(new DataSourceConfig
    /// {
    ///     Name = "DefaultConnection",
    ///     ConnectionString = "Data Source=demo.db",
    ///     Provider = typeof(SqliteConnection).AssemblyQualifiedName
    /// });
    /// provider.SetDefaultDataSource("DefaultConnection");
    ///
    /// // 获取默认数据源
    /// var defaultConfig = provider.GetDataSource(null);
    /// </code>
    /// </remarks>
    public class DataSourceProvider : IDataSourceProvider
    {
        /// <summary>
        /// 存储数据源配置的内部缓存，键为数据源名称（不区分大小写）
        /// </summary>
        private ConcurrentDictionary<string, DataSourceConfig> _connections = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// 默认连接名称
        /// </summary>
        public string? DefaultDataSourceName
        {
            get; set;
        }

        /// <summary>
        /// 构造函数
        /// </summary>
        public DataSourceProvider()
        {
        }

        /// <summary>
        /// 添加或更新一个数据源配置。名称已存在时覆盖原配置。
        /// </summary>
        /// <param name="config">数据源配置。</param>
        /// <returns>当前实例，便于链式调用。</returns>
        /// <exception cref="ArgumentNullException">当 <paramref name="config"/> 为 null 时抛出。</exception>
        public DataSourceProvider AddDataSource(DataSourceConfig config)
        {
            if (config is null) throw new ArgumentNullException(nameof(config));
            if (string.IsNullOrWhiteSpace(config.Name))
                throw new ArgumentException("DataSource name cannot be empty", nameof(config));

            _connections[config.Name!] = config;
            return this;
        }

        /// <summary>
        /// 移除指定的数据源配置。
        /// </summary>
        /// <param name="name">数据源名称。</param>
        /// <returns>是否移除成功。</returns>
        public bool RemoveDataSource(string name)
        {
            return _connections.TryRemove(name, out _);
        }

        /// <summary>
        /// 设置默认数据源名称。
        /// </summary>
        /// <param name="name">数据源名称。</param>
        /// <returns>当前实例，便于链式调用。</returns>
        public DataSourceProvider SetDefaultDataSource(string name)
        {
            DefaultDataSourceName = name;
            return this;
        }

        /// <summary>
        /// 获取指定名称的数据源配置
        /// </summary>
        /// <param name="name">数据源名称，如果为空则使用默认数据源</param>
        /// <returns>数据源配置，如果不存在则返回null</returns>
        public DataSourceConfig? GetDataSource(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                var defaultName = DefaultDataSourceName;
                if (!string.IsNullOrWhiteSpace(defaultName))
                    name = defaultName!;
                else if (_connections.Count > 0)
                    name = _connections.Keys.First();
                else
                    return null;
            }
            if (name is null) return null;
            if (_connections.TryGetValue(name, out var config))
                return config;
            return null;
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
