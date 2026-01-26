using System;
using System.Collections.Generic;
using System.Text;
using System.Data;
using LiteOrm.Common;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace LiteOrm
{
    public partial class ObjectDAO<T>
    {
        /// <summary>
        /// 实体对象类型
        /// </summary>
        public override Type ObjectType
        {
            get { return typeof(T); }
        }
        /// <summary>
        /// 获取实体对应的数据库表元数据。
        /// </summary>
        public override SqlTable Table
        {
            get { return TableInfoProvider.GetTableDefinition(ObjectType); }
        }

        /// <summary>
        /// 使用指定的参数创建新的DAO实例
        /// </summary>
        /// <param name="args">表名参数</param>
        /// <returns>新的DAO实例</returns>
        public ObjectDAO<T> WithArgs(params string[] args)
        {
            ObjectDAO<T> newDAO = MemberwiseClone() as ObjectDAO<T>;
            newDAO.TableNameArgs = args;
            newDAO.SqlBuildContext = null;
            return newDAO;
        }

        /// <summary>
        /// 识别列
        /// </summary>
        protected ColumnDefinition IdentityColumn => TableDefinition.Columns.FirstOrDefault(col => col.IsIdentity);
        private ColumnDefinition[] _insertableColumns;
        private ColumnDefinition[] _updatableColumns;

        private ColumnDefinition[] InsertableColumns
        {
            get
            {
                if (_insertableColumns is null)
                {
                    _insertableColumns = TableDefinition.Columns.Where(column => !column.IsIdentity && column.Mode.CanInsert()).ToArray();
                }
                return _insertableColumns;
            }
        }

        private ColumnDefinition[] UpdatableColumns
        {
            get
            {
                if (_updatableColumns is null)
                {
                    _updatableColumns = TableDefinition.Columns.Where(column => !column.IsPrimaryKey && column.Mode.CanUpdate()).ToArray();
                }
                return _updatableColumns;
            }
        }

        /// <summary>
        /// 获取或设置用于生成 SQL 的上下文。
        /// </summary>

        protected override SqlBuildContext SqlBuildContext
        {
            get { base.SqlBuildContext.SingleTable = true; return base.SqlBuildContext; }
        }

        #region 预构建Command
        /// <summary>
        /// 实体插入命令
        /// </summary>
        protected virtual DbCommandProxy MakeInsertCommand()
        {
            DbCommandProxy command = NewCommand();
            StringBuilder strColumns = new StringBuilder();
            StringBuilder strValues = new StringBuilder();
            ColumnDefinition[] columns = InsertableColumns;
            int count = columns.Length;
            for (int i = 0; i < count; i++)
            {
                ColumnDefinition column = columns[i];
                if (i > 0)
                {
                    strColumns.Append(",");
                    strValues.Append(",");
                }

                strColumns.Append(ToSqlName(column.Name));
                strValues.Append(ToSqlParam(column.PropertyName));
                IDbDataParameter param = command.CreateParameter();
                param.Size = column.Length;
                param.DbType = column.DbType;
                param.ParameterName = ToParamName(column.PropertyName);
                command.Parameters.Add(param);
            }

            command.CommandText = IdentityColumn is null ?
                $"insert into {ToSqlName(FactTableName)} ({strColumns}) \nvalues ({strValues})"
                : SqlBuilder.BuildIdentityInsertSql(command, IdentityColumn, FactTableName, strColumns.ToString(), strValues.ToString());
            return command;
        }


        /// <summary>
        /// 构建实体更新命令。
        /// </summary>
        /// <returns>返回更新命令实例。</returns>
        protected virtual DbCommandProxy MakeUpdateCommand(bool withTimestamp)
        {
            DbCommandProxy command = NewCommand();
            StringBuilder strColumns = new StringBuilder();
            ColumnDefinition[] columns = UpdatableColumns;
            int count = columns.Length;
            for (int i = 0; i < count; i++)
            {
                ColumnDefinition column = columns[i];
                if (i > 0) strColumns.Append(",");
                strColumns.AppendFormat("{0} = {1}", ToSqlName(column.Name), ToSqlParam(column.PropertyName));
                IDbDataParameter param = command.CreateParameter();
                param.Size = column.Length;
                param.DbType = column.DbType;
                param.ParameterName = ToParamName(column.PropertyName);
                command.Parameters.Add(param);
            }
            string strTimestamp = withTimestamp ? MakeTimestampCondition(command, null) : null;
            if (!String.IsNullOrEmpty(strTimestamp)) strTimestamp = $" and {strTimestamp}";
            command.CommandText = $"update {ToSqlName(FactTableName)} set {strColumns} {ToWhereSql(MakeKeyCondition(command) + strTimestamp)}";
            return command;
        }


        /// <summary>
        /// 构建实体删除命令。
        /// </summary>
        /// <returns>返回删除命令实例。</returns>
        protected virtual DbCommandProxy MakeDeleteCommand()
        {
            DbCommandProxy command = NewCommand();
            command.CommandText = $"delete from {ToSqlName(FactTableName)} {ToWhereSql(MakeKeyCondition(command))}";
            return command;
        }


        /// <summary>
        /// 构建更新或插入（Upsert）命令。
        /// 该命令会根据数据库类型生成对应的原子 Upsert 语句（如 MySQL 的 ON DUPLICATE KEY UPDATE 或 SQL Server 的 IF EXISTS）。
        /// </summary>
        /// <returns>返回更新或插入命令代理实例。</returns>
        protected virtual DbCommandProxy MakeUpdateOrInsertCommand()
        {
            DbCommandProxy command = NewCommand();
            StringBuilder strColumns = new StringBuilder();
            StringBuilder strValues = new StringBuilder();
            StringBuilder strUpdateColumns = new StringBuilder();

            foreach (ColumnDefinition column in TableDefinition.Columns)
            {
                bool handled = false;
                if (!column.IsIdentity && column.Mode.CanInsert())
                {
                    if (strColumns.Length != 0)
                    {
                        strColumns.Append(",");
                        strValues.Append(",");
                    }
                    strColumns.Append(ToSqlName(column.Name));
                    strValues.Append(ToSqlParam(column.PropertyName));
                    handled = true;
                }

                if (column.Mode.CanUpdate() && !column.IsPrimaryKey)
                {
                    if (strUpdateColumns.Length != 0) strUpdateColumns.Append(",");
                    strUpdateColumns.AppendFormat("{0} = {1}", ToSqlName(column.Name), ToSqlParam(column.PropertyName));
                    handled = true;
                }

                if (handled)
                {
                    IDbDataParameter param = command.CreateParameter();
                    param.DbType = column.DbType;
                    param.Size = column.Length;
                    param.ParameterName = ToParamName(column.PropertyName);
                    command.Parameters.Add(param);
                }
            }

            command.CommandText = SqlBuilder.BuildUpsertSql(command, FactTableName, strColumns.ToString(), strValues.ToString(), strUpdateColumns.ToString(), TableDefinition.Keys, IdentityColumn);
            return command;
        }

        /// <summary>
        /// 创建一次性批量插入实体集合的Command
        /// </summary>
        /// <param name="batchSize">要插入的实体集合数量</param>
        /// <remarks>一次性批量插入不支持返回自增列</remarks>
        protected virtual DbCommandProxy MakeBatchInsertCommand(int batchSize)
        {
            DbCommandProxy command = NewCommand();
            StringBuilder strColumns = new StringBuilder();
            List<string> valuesList = new List<string>(batchSize);
            ColumnDefinition[] insertColumns = InsertableColumns;
            int columnCount = insertColumns.Length;

            for (int j = 0; j < columnCount; j++)
            {
                if (j > 0) strColumns.Append(",");
                strColumns.Append(ToSqlName(insertColumns[j].Name));
            }

            int paramIndex = 0;
            for (int i = 0; i < batchSize; i++)
            {
                StringBuilder strValuesRepeat = new StringBuilder();
                for (int j = 0; j < columnCount; j++)
                {
                    ColumnDefinition column = insertColumns[j];
                    if (strValuesRepeat.Length != 0) strValuesRepeat.Append(",");

                    string idxStr = paramIndex.ToString();
                    strValuesRepeat.Append(ToSqlParam(idxStr));
                    IDbDataParameter param = command.CreateParameter();
                    param.Size = column.Length;
                    param.DbType = column.DbType;
                    param.ParameterName = ToParamName(idxStr);
                    command.Parameters.Add(param);
                    paramIndex++;
                }
                valuesList.Add($"({strValuesRepeat})");
            }

            if (IdentityColumn is not null && SqlBuilder.SupportBatchInsertWithIdentity)
                command.CommandText = SqlBuilder.BuildBatchIdentityInsertSql(command, IdentityColumn, FactTableName, strColumns.ToString(), valuesList);
            else
                command.CommandText = SqlBuilder.BuildBatchInsertSql(FactTableName, strColumns.ToString(), valuesList);
            return command;
        }

        /// <summary>
        /// 创建批量更新命令。
        /// </summary>
        protected virtual DbCommandProxy MakeBatchUpdateCommand(int batchSize)
        {
            ColumnDefinition[] updatableColumns = UpdatableColumns;
            var keyColumns = TableDefinition.Keys.ToArray();

            DbCommandProxy command = NewCommand();
            command.CommandText = SqlBuilder.BuildBatchUpdateSql(FactTableName, updatableColumns, keyColumns, batchSize);

            for (int b = 0; b < batchSize; b++)
            {
                foreach (var col in updatableColumns)
                {
                    IDbDataParameter param = command.CreateParameter();
                    param.ParameterName = ToParamName("p" + command.Parameters.Count);
                    param.Size = col.Length;
                    param.DbType = col.DbType;
                    command.Parameters.Add(param);
                }
                foreach (var key in keyColumns)
                {
                    IDbDataParameter param = command.CreateParameter();
                    param.ParameterName = ToParamName("p" + command.Parameters.Count);
                    param.Size = key.Length;
                    param.DbType = key.DbType;
                    command.Parameters.Add(param);
                }
            }
            return command;
        }

        /// <summary>
        /// 创建批量更新命令。
        /// </summary>
        protected virtual DbCommandProxy MakeBatchIDExistsCommand(int batchSize)
        {
            var keyColumns = TableDefinition.Keys;

            DbCommandProxy command = NewCommand();
            command.CommandText = SqlBuilder.BuildBatchIDExistsSql(FactTableName, keyColumns, batchSize);
            for (int b = 0; b < batchSize; b++)
            {
                foreach (var key in keyColumns)
                {
                    IDbDataParameter param = command.CreateParameter();
                    param.ParameterName = ToParamName("p" + command.Parameters.Count);
                    param.Size = key.Length;
                    param.DbType = key.DbType;
                    command.Parameters.Add(param);
                }
            }
            return command;
        }

        #endregion

        #region Helpers

        private void SetParameterValues(ColumnDefinition[] insertableColumns, List<T> batch, DbCommandProxy command)
        {
            int paramIndex = 0;
            var parameters = command.Parameters;
            int columnCount = insertableColumns.Length;
            int batchCount = batch.Count;

            for (int i = 0; i < batchCount; i++)
            {
                T item = batch[i];
                for (int j = 0; j < columnCount; j++)
                {
                    ColumnDefinition column = insertableColumns[j];
                    var param = (IDataParameter)parameters[paramIndex++];
                    param.Value = ConvertToDbValue(column.GetValue(item), column.DbType);
                }
            }
        }

        /// <summary>
        /// 更新批量操作中的实体 ID。
        /// </summary>
        protected virtual void UpdateBatchIds(List<T> batch, ref long firstId)
        {
            int count = batch.Count;
            for (int i = 0; i < count; i++)
            {
                IdentityColumn.SetValue(batch[i], ConvertFromDbValue(firstId++, IdentityColumn.PropertyType));
            }
        }

        protected DataTable ToDataTable(IEnumerable<T> values, ColumnDefinition[] columns)
        {
            DataTable dt = new DataTable(FactTableName);
            int columnCount = columns.Length;
            for (int i = 0; i < columnCount; i++)
            {
                dt.Columns.Add(new DataColumn(columns[i].Name, columns[i].PropertyType.GetUnderlyingType()));
            }
            dt.BeginInit();
            foreach (T t in values)
            {
                DataRow dr = dt.NewRow();
                for (int i = 0; i < columnCount; i++)
                {
                    ColumnDefinition column = columns[i];
                    dr[column.Name] = ConvertToDbValue(column.GetValue(t), column.DbType) ?? DBNull.Value;
                }
                dt.Rows.Add(dr);
            }
            dt.EndInit();
            return dt;
        }

        private void SetBatchUpdateParameterValues(ColumnDefinition[] updatableColumns, ColumnDefinition[] keyColumns, List<T> batch, DbCommandProxy command)
        {
            int paramIndex = 0;
            var parameters = command.Parameters;
            int updatableCount = updatableColumns.Length;
            int keyCount = keyColumns.Length;
            int batchCount = batch.Count;

            for (int i = 0; i < batchCount; i++)
            {
                T item = batch[i];
                for (int j = 0; j < updatableCount; j++)
                {
                    ColumnDefinition column = updatableColumns[j];
                    ((IDataParameter)parameters[paramIndex++]).Value = ConvertToDbValue(column.GetValue(item), column.DbType);
                }
                for (int j = 0; j < keyCount; j++)
                {
                    ColumnDefinition key = keyColumns[j];
                    ((IDataParameter)parameters[paramIndex++]).Value = ConvertToDbValue(key.GetValue(item), key.DbType);
                }
            }
        }
        #endregion
    }
}
