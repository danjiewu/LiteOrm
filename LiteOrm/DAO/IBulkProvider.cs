using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LiteOrm
{
    /// <summary>
    /// 批量插入提供程序接口
    /// </summary>
    /// <remarks>
    /// IBulkProvider 定义了批量插入数据的约定，实现时需使用AutoRegister特性进行标记。
    /// 不同的数据库系统提供了各自的批量插入机制和优化方案，例如 SQL Server 的 SqlBulkCopy、MySQL 的 LOAD DATA 等。
    /// 使用示例：
    /// <code>
    /// [AutoRegister(Key = typeof(MySqlConnection))]
    /// public class MysqlBulkInsertProvider : IBulkProvider
    /// {  
    ///     public int BulkInsert(DataTable dt, IDbConnection dbConnection, IDbTransaction transaction)
    ///     {
    ///         if (dt is null) throw new ArgumentNullException(nameof(dt));
    ///         if (dbConnection is null) throw new ArgumentNullException(nameof(dbConnection));
    ///         if (transaction is null) throw new ArgumentNullException(nameof(transaction));
    ///         if (dbConnection is not MySqlConnection)
    ///             throw new ArgumentException($"数据库连接必须是 MySqlConnection 类型，但实际类型是 {dbConnection.GetType().Name}");
    ///         if (transaction is not MySqlTransaction)
    ///             throw new ArgumentException($"事务必须是 MySqlTransaction 类型，但实际类型是 {transaction.GetType().Name}");
    ///         MySqlBulkCopy bulkCopy = new MySqlBulkCopy(dbConnection as MySqlConnection, transaction as MySqlTransaction);
    ///         bulkCopy.DestinationTableName = dt.TableName;
    ///         bulkCopy.ConflictOption = MySqlBulkLoaderConflictOption.Replace;
    ///         for (int i = 0; i &lt; dt.Columns.Count; i++)
    ///         {
    ///             bulkCopy.ColumnMappings.Add(new MySqlBulkCopyColumnMapping(i, dt.Columns[i].ColumnName));
    ///         }
    ///         return bulkCopy.WriteToServer(dt).RowsInserted;
    ///     }
    /// }
    /// </code>
    /// 通过实现此接口，可以为特定的数据库系统提供最优的批量插入实现。
    /// 
    /// 主要功能：
    /// 1. 获取对应的数据库连接类型
    /// 2. 执行批量插入操作，将 DataTable 中的数据高效地批量插入数据库
    /// 
    /// 实现者应该为相应的数据库连接类型提供高效的批量插入实现，
    /// 通常比逐行插入具有更好的性能。
    /// </remarks>
    public interface IBulkProvider
    {
        /// <summary>
        /// 执行批量插入操作
        /// </summary>
        /// <param name="dt">要插入的数据表，包含要批量插入的数据</param>
        /// <param name="dbConnection">数据库连接对象</param>
        /// <param name="transaction">数据库事务对象</param>
        /// <returns>插入的行数</returns>
        /// <remarks>
        /// 此方法将 DataTable 中的所有行批量插入到数据库中。
        /// 实现应该利用数据库系统提供的批量插入机制以获得最优性能。
        /// 返回值表示成功插入的行数。
        /// </remarks>
        int BulkInsert(DataTable dt, IDbConnection dbConnection, IDbTransaction transaction);
    }
}
