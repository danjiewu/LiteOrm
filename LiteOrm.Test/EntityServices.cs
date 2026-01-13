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
using static LogRecord.ISessionService;
using static System.Collections.Specialized.BitVector32;

namespace LogRecord
{
    public interface IAccountingLogService : IEntityService<AccountingLog>, IEntityServiceAsync<AccountingLog>, IEntityViewService<AccountingLog>, IEntityViewServiceAsync<AccountingLog> { }
    public interface ISessionService : IEntityService<Session>, IEntityViewService<Session>
    {
    }

    public class MysqlBulkInsertProvider : IBulkInsertProvider
    {
        public Type DbConnectionType => typeof(MySqlConnection);

        public int BulkInsert(DataTable dt, DAOContext context)
        {
            if (dt is null) throw new ArgumentNullException(nameof(dt));
            if (context is null) throw new ArgumentNullException(nameof(context));
            MySqlBulkCopy bulkCopy = new MySqlBulkCopy(context.DbConnection as MySqlConnection, context.CurrentTransaction as MySqlTransaction);
            bulkCopy.DestinationTableName = dt.TableName;
            bulkCopy.ConflictOption = MySqlBulkLoaderConflictOption.Replace;
            for (int i = 0; i < dt.Columns.Count; i++)
            {
                bulkCopy.ColumnMappings.Add(new MySqlBulkCopyColumnMapping(i, dt.Columns[i].ColumnName));
            }
            return bulkCopy.WriteToServer(dt).RowsInserted;
        }
    }
    public class AccountintLogService : EntityService<AccountingLog>, IAccountingLogService
    {
    }
    public class SessionService : EntityService<Session>, ISessionService
    {        
    }
}
