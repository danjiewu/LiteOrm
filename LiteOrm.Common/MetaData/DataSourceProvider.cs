using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace LiteOrm.Common
{
    /// <summary>
    /// 数据库连接配置
    /// </summary>
    public class DataSourceConfig
    {
        /// <summary>
        /// 数据源名称
        /// </summary>
        public string? Name { get; set; }

        /// <summary>
        /// 数据库连接字符串
        /// </summary>
        public string? ConnectionString { get; set; }

        /// <summary>
        /// 数据库提供程序类型（<see cref="System.Data.Common.DbConnection"/> 派生类型），可读写。
        /// 为 null 时无法创建连接池。
        /// </summary>
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
        public Type? ProviderType { get; set; }

        /// <summary>
        /// SQL 构建器类型（<c>SqlBuilder</c> 派生类型，可选），可读写。
        /// 赋 null 表示不指定，由工厂按 <see cref="ProviderType"/> 自动匹配。
        /// </summary>
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
        public Type? SqlBuilderType { get; set; }

        /// <summary>
        /// 连接保活时长
        /// </summary>
        public TimeSpan KeepAliveDuration { get; set; } = TimeSpan.FromMinutes(10);

        /// <summary>
        /// 连接池大小（允许在池中缓存的最大连接数），默认为16
        /// </summary>
        public int PoolSize { get; set; } = 16;

        /// <summary>
        /// 最大连接数限制，默认为100
        /// </summary>
        public int MaxPoolSize { get; set; } = 100;

        /// <summary>
        /// 数据库参数最大数量限制，为0表示无限制，默认为1000
        /// </summary>
        public int ParamCountLimit { get; set; } = 1000;

        /// <summary>
        /// 是否开启自动建表同步
        /// </summary>
        public bool SyncTable { get; set; }

        /// <summary>
        /// 只读数据库配置列表
        /// </summary>
        public List<ReadOnlyDataSourceConfig> ReadOnlyConfigs { get; set; } = new List<ReadOnlyDataSourceConfig>();

        /// <summary>
        /// 初始化一个空配置。
        /// </summary>
        public DataSourceConfig()
        {
        }

        /// <summary>
        /// 用连接类型与连接字符串初始化配置。
        /// </summary>
        /// <param name="providerType">数据库提供程序类型。</param>
        /// <param name="connectionString">连接字符串。</param>
        public DataSourceConfig([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type providerType, string? connectionString = null)
        {
            ProviderType = providerType;
            ConnectionString = connectionString;
        }
    }

    /// <summary>
    /// 只读数据库连接配置
    /// </summary>
    public class ReadOnlyDataSourceConfig
    {
        /// <summary>
        /// 数据库连接字符串
        /// </summary>
        public string? ConnectionString { get; set; }

        /// <summary>
        /// 数据库提供程序（<see cref="System.Data.Common.DbConnection"/> 派生类型）。
        /// 为 null 时沿用主库的 <see cref="DataSourceConfig.ProviderType"/>。
        /// </summary>
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
        public Type? ProviderType { get; set; }

        /// <summary>
        /// SQL 构建器类型（<c>SqlBuilder</c> 派生类型，可选）。
        /// 为 null 时沿用主库的 <see cref="DataSourceConfig.SqlBuilderType"/>。
        /// </summary>
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
        public Type? SqlBuilderType { get; set; }

        /// <summary>
        /// 连接保活时长（可选，不设置则使用主库配置）
        /// </summary>
        public TimeSpan? KeepAliveDuration { get; set; }

        /// <summary>
        /// 连接池大小（可选，不设置则使用主库配置）
        /// </summary>
        public int? PoolSize { get; set; }

        /// <summary>
        /// 最大连接数限制（可选，不设置则使用主库配置）
        /// </summary>
        public int? MaxPoolSize { get; set; }

        /// <summary>
        /// 数据库参数最大数量限制（可选，不设置则使用主库配置）
        /// </summary>
        public int? ParamCountLimit { get; set; }

        /// <summary>
        /// 初始化一个空配置。
        /// </summary>
        public ReadOnlyDataSourceConfig()
        {
        }

        /// <summary>
        /// 用连接字符串初始化配置。
        /// </summary>
        /// <param name="connectionString">连接字符串。</param>
        public ReadOnlyDataSourceConfig(string? connectionString)
        {
            ConnectionString = connectionString;
        }
    }

    /// <summary>
    /// 数据源提供程序接口，用于管理数据库连接配置
    /// </summary>
    public interface IDataSourceProvider : IEnumerable<DataSourceConfig>
    {
        /// <summary>
        /// 获取默认数据源名称
        /// </summary>
        string? DefaultDataSourceName { get; }

        /// <summary>
        /// 根据名称获取数据源配置
        /// </summary>
        /// <param name="name">数据源名称</param>
        /// <returns>数据源配置，不存在时返回 null</returns>
        DataSourceConfig? GetDataSource(string name);
    }

}
