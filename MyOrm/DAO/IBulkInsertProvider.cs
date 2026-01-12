using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MyOrm
{
    /// <summary>
    /// 表示一个批量插入提供程序接口
    /// </summary>
    /// <remarks>
    /// IBulkInsertProvider 定义了批量插入数据的约定。
    /// 
    /// 不同的数据库系统提供了各自的批量插入机制和优化方案。
    /// 例如 SQL Server 的 SqlBulkCopy、MySQL 的 LOAD DATA 等。
    /// 
    /// 通过实现此接口，可以为特定的数据库系统提供最优的批量插入实现。
    /// 
    /// 主要功能：
    /// 1. 获取对应的数据库连接类型
    /// 2. 执行批量插入操作，将 DataTable 中的数据高效地批量插入数据库
    /// 
    /// 实现者应该为相应的数据库连接类型提供高效的批量插入实现，
    /// 通常比逐行插入具有更好的性能。
    /// </remarks>
    public interface IBulkInsertProvider
    {
        /// <summary>
        /// 获取数据库连接类型
        /// </summary>
        /// <remarks>
        /// 返回该批量插入提供程序支持的数据库连接类型。
        /// 例如 SqlConnection、MySqlConnection 等。
        /// </remarks>
        Type DbConnectionType { get; }
        
        /// <summary>
        /// 执行批量插入操作
        /// </summary>
        /// <param name="dt">要插入的数据表，包含要批量插入的数据</param>
        /// <param name="context">DAO上下文，包含数据库连接和其他必要的上下文信息</param>
        /// <returns>插入的行数</returns>
        /// <remarks>
        /// 此方法将 DataTable 中的所有行批量插入到数据库中。
        /// 实现应该利用数据库系统提供的批量插入机制以获得最优性能。
        /// 返回值表示成功插入的行数。
        /// </remarks>
        int BulkInsert(DataTable dt, DAOContext context);
    }

    /// <summary>
    /// 批量插入提供程序工厂类
    /// </summary>
    /// <remarks>
    /// BulkInsertProviderFactory 是一个工厂类，用于管理和获取各种数据库的批量插入提供程序。
    /// 
    /// 主要功能包括：
    /// 1. 提供程序注册 - 允许注册特定数据库连接类型的批量插入提供程序
    /// 2. 提供程序查询 - 根据数据库连接类型查询对应的批量插入提供程序
    /// 3. 线程安全 - 使用 ConcurrentDictionary 确保线程安全的访问
    /// 
    /// 使用示例：
    /// <code>
    /// // 注册 SQL Server 的批量插入提供程序
    /// BulkInsertProviderFactory.RegisterProvider(new SqlServerBulkInsertProvider());
    /// 
    /// // 获取指定连接类型的提供程序
    /// var provider = BulkInsertProviderFactory.GetProvider(typeof(SqlConnection));
    /// if (provider != null)
    /// {
    ///     int rowsInserted = provider.BulkInsert(dataTable, daoContext);
    /// }
    /// </code>
    /// </remarks>
    public static class BulkInsertProviderFactory
    {
        /// <summary>
        /// 存储已注册的批量插入提供程序的并发字典
        /// </summary>
        /// <remarks>
        /// 键为数据库连接类型，值为对应的批量插入提供程序实例。
        /// 使用 ConcurrentDictionary 以确保线程安全。
        /// </remarks>
        private static readonly ConcurrentDictionary<Type, IBulkInsertProvider> _providers = new  ConcurrentDictionary<Type, IBulkInsertProvider>();
        
        /// <summary>
        /// 注册批量插入提供程序
        /// </summary>
        /// <param name="provider">要注册的批量插入提供程序</param>
        /// <exception cref="ArgumentNullException">当 <paramref name="provider"/> 为 null 时抛出</exception>
        /// <remarks>
        /// 注册提供程序时，将根据其 DbConnectionType 属性作为键进行存储。
        /// 如果已存在相同连接类型的提供程序，新的提供程序将覆盖旧的。
        /// </remarks>
        public static void RegisterProvider(IBulkInsertProvider provider)
        {
            if (provider == null) throw new ArgumentNullException(nameof(provider));
            _providers[provider.DbConnectionType] = provider;
        }
        
        /// <summary>
        /// 获取指定数据库连接类型的批量插入提供程序
        /// </summary>
        /// <param name="dbConnectionType">数据库连接类型</param>
        /// <returns>如果找到则返回批量插入提供程序，否则返回 null</returns>
        /// <remarks>
        /// 根据提供的数据库连接类型从注册的提供程序中查询对应的实现。
        /// 如果未找到对应的提供程序，返回 null。
        /// 
        /// 常见的连接类型包括：
        /// - System.Data.SqlClient.SqlConnection (SQL Server)
        /// - MySql.Data.MySqlClient.MySqlConnection (MySQL)
        /// - Oracle.DataAccess.Client.OracleConnection (Oracle)
        /// - System.Data.SQLite.SQLiteConnection (SQLite)
        /// </remarks>
        public static IBulkInsertProvider GetProvider(Type dbConnectionType)
        {
            return _providers.TryGetValue(dbConnectionType, out var provider) ? provider : null;
        }
    }
}
