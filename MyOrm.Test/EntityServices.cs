using Microsoft.Extensions.DependencyInjection;
using MyOrm;
using MyOrm.Common;
using MyOrm.Service;
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
        Session? UpdateSession(AccountingLog log);
    }

    public class AccountingLogDAO : ObjectDAO<AccountingLog>
    {
        public override void BatchInsert(IEnumerable<AccountingLog> entities)
        {
            MySqlBulkCopy bulkCopy = new MySqlBulkCopy(Connection as MySqlConnection, DAOContext.CurrentTransaction as MySqlTransaction);
            bulkCopy.DestinationTableName = FactTableName;
            bulkCopy.ConflictOption = MySqlBulkLoaderConflictOption.Replace;
            DataTable dt = new DataTable();
            int columnIndex = 0;
            foreach (ColumnDefinition column in TableDefinition.Columns)
            {
                if (!column.IsIdentity && column.Mode.CanInsert())
                {
                    dt.Columns.Add(column.PropertyName, Nullable.GetUnderlyingType(column.PropertyType) ?? column.PropertyType);
                    bulkCopy.ColumnMappings.Add(new MySqlBulkCopyColumnMapping(columnIndex++, column.Name));
                }
            }
            dt.BeginInit();
            foreach (AccountingLog entity in entities)
            {
                var row = dt.NewRow();
                foreach (DataColumn column in dt.Columns)
                {
                    row[column.ColumnName] = entity[column.ColumnName] ?? DBNull.Value;
                }
                dt.Rows.Add(row);
            }
            dt.EndInit();
            bulkCopy.WriteToServer(dt);
        }
    }
    public class AccountintLogService : EntityService<AccountingLog>, IAccountingLogService
    {
        public override void BatchInsert(IEnumerable<AccountingLog> entities)
        {
            base.BatchInsert(entities);
        }
    }
    public class SessionService : EntityService<Session>, ISessionService
    {
        public Session? UpdateSession(AccountingLog log)
        {
            if (log.AcctOutputOctets % 100 != 23) return null;
            var session = SearchOne(Statement.Property(nameof(AccountingLog.AcctSessionId), log.AcctSessionId));
            if (session == null)
            {
                session = new Session()
                {
                    AcctSessionId = log.AcctSessionId,
                    Status = log.AcctStatusType == 2 ? SessionStatus.Inactive : SessionStatus.Active,
                    UserName = log.UserName,
                    NasIP = log.NasIpAddress,
                    ClientIP = log.FramedIpAddress,
                    CreatedTime = DateTime.Now,
                    ClientMac = log.MacAddress,
                    AcctInputOctets = log.AcctInputOctets,
                    AcctOutputOctets = log.AcctOutputOctets,
                    LoginTime = log.AcctStatusType == 1 ? log.RequestDate : log.AcctStartTime
                };
                Insert(session);
            }
            else
            {
                if (log.AcctStatusType == 1)
                {
                    session.Status = SessionStatus.Active;
                    session.LoginTime = log.RequestDate;
                }
                else if (log.AcctStatusType == 2)
                {
                    session.Status = SessionStatus.Inactive;
                }
                else if (log.AcctStatusType == 3)
                {
                    if (session.LoginTime == null) session.LoginTime = log.AcctStartTime;
                    session.Status = SessionStatus.Active;
                    session.AcctInputOctets = log.AcctInputOctets;
                    session.AcctOutputOctets = log.AcctOutputOctets;
                }
                if (!String.IsNullOrEmpty(log.NasIpAddress)) session.NasIP = log.NasIpAddress;
                if (!String.IsNullOrEmpty(log.MacAddress)) session.ClientMac = log.MacAddress;
                if (!String.IsNullOrEmpty(log.FramedIpAddress)) session.ClientIP = log.FramedIpAddress;
                session.UpdateTime = log.RequestDate;
                Update(session);
            }
            return session;
        }
    }
}
