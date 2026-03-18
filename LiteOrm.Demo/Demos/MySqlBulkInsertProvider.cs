using LiteOrm.Common;
using MySqlConnector;
using System;
using System.Collections.Generic;
using System.Data;
using System.Text;

namespace LiteOrm.Demo.Demos
{
    [AutoRegister(Key = typeof(MySqlConnection))]
    public class MySqlBulkCopyProvider : IBulkProvider
    {
        public int BulkInsert(DataTable dt, IDbConnection dbConnection, IDbTransaction transaction)
        {
            MySqlBulkCopy bulkCopy = CreateBulkCopy(dt, dbConnection, transaction);
            return bulkCopy.WriteToServer(dt).RowsInserted;
        }

        public async Task<int> BulkInsertAsync(DataTable dt, IDbConnection dbConnection, IDbTransaction transaction, CancellationToken cancellationToken = default)
        {
            MySqlBulkCopy bulkCopy = CreateBulkCopy(dt, dbConnection, transaction);
            return (await bulkCopy.WriteToServerAsync(dt).ConfigureAwait(false)).RowsInserted;
        }

        private static MySqlBulkCopy CreateBulkCopy(DataTable dt, IDbConnection dbConnection, IDbTransaction transaction)
        {
            MySqlBulkCopy bulkCopy = new MySqlBulkCopy(dbConnection as MySqlConnection, transaction as MySqlTransaction);
            bulkCopy.DestinationTableName = dt.TableName;
            bulkCopy.ConflictOption = MySqlBulkLoaderConflictOption.Replace;
            for (int i = 0; i < dt.Columns.Count; i++)
            {
                bulkCopy.ColumnMappings.Add(new MySqlBulkCopyColumnMapping(i, dt.Columns[i].ColumnName));
            }

            return bulkCopy;
        }
    }
}
