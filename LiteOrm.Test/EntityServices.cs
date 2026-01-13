using Microsoft.Extensions.DependencyInjection;
using LiteOrm;
using LiteOrm.Common;
using LiteOrm.Service;
using MySqlConnector;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LogRecord
{
    public interface IAccountingLogService : IEntityService<AccountingLog>, IEntityServiceAsync<AccountingLog>, IEntityViewService<AccountingLog>, IEntityViewServiceAsync<AccountingLog> { }
    public interface ISessionService : IEntityService<Session>, IEntityViewService<Session>
    {
    }

    [AutoRegister(Key = typeof(MySqlConnection))]
    public class MysqlBulkInsertProvider : IBulkProvider
    {
        public int BulkInsert(DataTable dt, IDbConnection dbConnection, IDbTransaction transaction)
        {
            if (dt is null) throw new ArgumentNullException(nameof(dt));
            if (dbConnection is null) throw new ArgumentNullException(nameof(dbConnection));
            if (transaction is null) throw new ArgumentNullException(nameof(transaction));
            if (dbConnection is not MySqlConnection)
                throw new ArgumentException($"数据库连接必须是 MySqlConnection 类型，但实际类型是 {dbConnection.GetType().Name}");
            if (transaction is not MySqlTransaction)
                throw new ArgumentException($"事务必须是 MySqlTransaction 类型，但实际类型是 {transaction.GetType().Name}");
            MySqlBulkCopy bulkCopy = new MySqlBulkCopy(dbConnection as MySqlConnection, transaction as MySqlTransaction);
            bulkCopy.DestinationTableName = dt.TableName;
            bulkCopy.ConflictOption = MySqlBulkLoaderConflictOption.Replace;
            for (int i = 0; i < dt.Columns.Count; i++)
            {
                bulkCopy.ColumnMappings.Add(new MySqlBulkCopyColumnMapping(i, dt.Columns[i].ColumnName));
            }
            return bulkCopy.WriteToServer(dt).RowsInserted;
        }
    }
}
