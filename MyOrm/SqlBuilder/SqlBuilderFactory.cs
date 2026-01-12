using Microsoft.Extensions.DependencyInjection;
using MyOrm.Common;
using System;
using System.Collections.Concurrent;


namespace MyOrm
{
    /// <summary>
    /// SQL构建器工厂类 - 根据数据库连接类型提供相应的SQL构建器
    /// </summary>
    /// <remarks>
    /// SqlBuilderFactory 是一个工厂类，根据提供的数据库连接类型返回对应的 SQL 构建器实现。
    /// 
    /// 主要功能包括：
    /// 1. 构建器注册 - 允许注册自定义的 SQL 构建器
    /// 2. 构建器查询 - 根据数据库连接类型查询对应的 SQL 构建器
    /// 3. 自动类型识别 - 如果未显式注册，根据连接类型名称自动选择合适的构建器
    /// 4. 单例管理 - 提供了全局的工厂实例
    /// 5. 缓存机制 - 使用 ConcurrentDictionary 缓存已注册的构建器
    /// 6. 线程安全 - ConcurrentDictionary 确保线程安全
    /// 
    /// 支持的数据库系统：
    /// - Oracle (OracleBuilder)
    /// - MySQL (MySqlBuilder)
    /// - SQL Server (SqlServerBuilder)
    /// - SQLite (SQLiteBuilder)
    /// - 其他未知类型 (默认使用 SqlBuilder)
    /// 
    /// 该类通过依赖注入框架以单例方式注册。
    /// 
    /// 使用示例：
    /// <code>
    /// var factory = serviceProvider.GetRequiredService&lt;ISqlBuilderFactory&gt;();
    /// 
    /// // 根据连接类型获取构建器
    /// var sqlServerBuilder = factory.GetSqlBuilder(typeof(SqlConnection));
    /// var mySqlBuilder = factory.GetSqlBuilder(typeof(MySqlConnection));
    /// 
    /// // 注册自定义构建器
    /// factory.RegisterSqlBuilder(typeof(CustomConnection), new CustomSqlBuilder());
    /// </code>
    /// </remarks>
    [AutoRegister(ServiceLifetime.Singleton)]
    public class SqlBuilderFactory : ISqlBuilderFactory
    {
        /// <summary>
        /// 获取 <see cref="SqlBuilderFactory"/> 的单例实例。
        /// </summary>
        public static readonly SqlBuilderFactory Instance = new SqlBuilderFactory();

        /// <summary>
        /// 已注册的 SQL 构建器集合。
        /// </summary>
        public ConcurrentDictionary<Type, SqlBuilder> RegisteredSqlBuilders { get; } = new();

        /// <summary>
        /// 注册 SQL 构建器。
        /// </summary>
        /// <param name="providerType">提供程序类型。</param>
        /// <param name="sqlBuilder">SQL 构建器实例。</param>
        public void RegisterSqlBuilder(Type providerType, SqlBuilder sqlBuilder)
        {
            RegisteredSqlBuilders[providerType] = sqlBuilder;
        }

        /// <summary>
        /// 获取指定提供程序类型的 SQL 构建器。
        /// </summary>
        /// <param name="providerType">提供程序类型。</param>
        /// <returns>SQL 构建器实例。</returns>
        public virtual SqlBuilder GetSqlBuilder(Type providerType)
        {
            if (providerType == null) throw new ArgumentNullException("providerType");
            if (RegisteredSqlBuilders.ContainsKey(providerType)) return RegisteredSqlBuilders[providerType];
            var connectionTypeName = providerType.Name;
            connectionTypeName = connectionTypeName.ToUpper();
            if (connectionTypeName.Contains("ORACLE"))
                return Oracle.OracleBuilder.Instance;
            else if (connectionTypeName.Contains("MYSQL"))
                return MySql.MySqlBuilder.Instance;
            else if (connectionTypeName.Contains("SQLSERVER"))
                return SqlServer.SqlServerBuilder.Instance;
            else if (connectionTypeName.Contains("SQLITE"))
                return SQLite.SQLiteBuilder.Instance;
            else return SqlBuilder.Instance;
        }

        ISqlBuilder ISqlBuilderFactory.GetSqlBuilder(Type providerType)
        {
            return GetSqlBuilder(providerType);
        }
    }
}
