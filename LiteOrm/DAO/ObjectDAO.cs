using LiteOrm.Common;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace LiteOrm
{
    /// <summary>
    /// 实体类数据访问对象实现 - 负责增删改等操作
    /// </summary>
    /// <typeparam name="T">实体类型</typeparam>
    /// <remarks>
    /// ObjectDAO&lt;T&gt; 是 IObjectDAO&lt;T&gt; 接口的实现，提供针对特定实体类型的数据访问操作。
    /// 
    /// 主要功能包括：
    /// 1. 插入操作 - 向数据库插入新的实体记录
    /// 2. 更新操作 - 更新现有的实体记录
    /// 3. 删除操作 - 删除指定的实体记录
    /// 4. 批量操作 - 支持批量插入、更新、删除等操作
    /// 5. 事务支持 - 支持事务处理以确保数据一致性
    /// 
    /// 该类继承自 ObjectDAOBase，使用泛型参数 T 来指定具体的实体类型，
    /// 提供强类型的数据访问接口。
    /// </remarks>
    [AutoRegister(ServiceLifetime.Scoped)]
    public class ObjectDAO<T> : DAOBase, IObjectDAO<T>
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
            newDAO.TableArgs = args;
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

        protected override SqlBuildContext CreateSqlBuildContext(bool initTable = false)
        {
            var context = base.CreateSqlBuildContext(true);
            context.SingleTable = true;
            return context;
        }

        #region 预构建Command
        /// <summary>
        /// 实体插入命令
        /// </summary>
        protected virtual DbCommandProxy MakeInsertCommand()
        {
            DbCommandProxy command = NewCommand();
            Span<char> colBuf = stackalloc char[256];
            Span<char> valBuf = stackalloc char[256];
            var strColumns = new ValueStringBuilder(colBuf);
            var strValues = new ValueStringBuilder(valBuf);

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
                DbParameter param = command.CreateParameter();
                param.Size = column.Length;
                param.DbType = column.DbType;
                param.ParameterName = ToParamName(column.PropertyName);
                command.Parameters.Add(param);
            }

            command.CommandText = IdentityColumn is null ?
                $"INSERT INTO {ToSqlName(FactTableName)} ({strColumns.ToString()}) \nVALUES ({strValues.ToString()})"
                : SqlBuilder.BuildIdentityInsertSql(command, IdentityColumn, FactTableName, strColumns.ToString(), strValues.ToString());

            strColumns.Dispose();
            strValues.Dispose();
            return command;
        }


        /// <summary>
        /// 构建实体更新命令。
        /// </summary>
        /// <returns>返回更新命令实例。</returns>
        protected virtual DbCommandProxy MakeUpdateCommand(bool withTimestamp)
        {
            DbCommandProxy command = NewCommand();
            Span<char> colBuf = stackalloc char[512];
            var strColumns = new ValueStringBuilder(colBuf);
            ColumnDefinition[] columns = UpdatableColumns;
            int count = columns.Length;
            for (int i = 0; i < count; i++)
            {
                ColumnDefinition column = columns[i];
                if (i > 0) strColumns.Append(",");
                strColumns.Append(ToSqlName(column.Name));
                strColumns.Append(" = ");
                strColumns.Append(ToSqlParam(column.PropertyName));
                DbParameter param = command.CreateParameter();
                param.Size = column.Length;
                param.DbType = column.DbType;
                param.ParameterName = ToParamName(column.PropertyName);
                command.Parameters.Add(param);
            }
            string strTimestamp = withTimestamp ? MakeTimestampCondition(command, null) : null;
            if (!String.IsNullOrEmpty(strTimestamp)) strTimestamp = $" AND {strTimestamp}";
            command.CommandText = $"UPDATE {ToSqlName(FactTableName)} SET {strColumns.ToString()} {ToWhereSql(MakeKeyCondition(command) + strTimestamp)}";
            strColumns.Dispose();
            return command;
        }


        /// <summary>
        /// 构建实体删除命令。
        /// </summary>
        /// <returns>返回删除命令实例。</returns>
        protected virtual DbCommandProxy MakeDeleteCommand()
        {
            DbCommandProxy command = NewCommand();
            command.CommandText = $"DELETE FROM {ToSqlName(FactTableName)} {ToWhereSql(MakeKeyCondition(command))}";
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
            Span<char> colBuf = stackalloc char[512];
            var strColumns = new ValueStringBuilder(colBuf);
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
                var strValuesRepeat = ValueStringBuilder.Create(128);
                for (int j = 0; j < columnCount; j++)
                {
                    ColumnDefinition column = insertColumns[j];
                    if (strValuesRepeat.Length != 0) strValuesRepeat.Append(",");

                    string idxStr = paramIndex.ToString();
                    strValuesRepeat.Append(ToSqlParam(idxStr));
                    DbParameter param = command.CreateParameter();
                    param.Size = column.Length;
                    param.DbType = column.DbType;
                    param.ParameterName = ToParamName(idxStr);
                    command.Parameters.Add(param);
                    paramIndex++;
                }
                valuesList.Add($"({strValuesRepeat.ToString()})");
                strValuesRepeat.Dispose();
            }

            string columnsStr = strColumns.ToString();
            if (IdentityColumn is not null && SqlBuilder.SupportBatchInsertWithIdentity)
                command.CommandText = SqlBuilder.BuildBatchIdentityInsertSql(command, IdentityColumn, FactTableName, columnsStr, valuesList);
            else
                command.CommandText = SqlBuilder.BuildBatchInsertSql(FactTableName, columnsStr, valuesList);

            strColumns.Dispose();
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
                    DbParameter param = command.CreateParameter();
                    param.ParameterName = ToParamName("p" + command.Parameters.Count);
                    param.Size = col.Length;
                    param.DbType = col.DbType;
                    command.Parameters.Add(param);
                }
                foreach (var key in keyColumns)
                {
                    DbParameter param = command.CreateParameter();
                    param.ParameterName = ToParamName("p" + command.Parameters.Count);
                    param.Size = key.Length;
                    param.DbType = key.DbType;
                    command.Parameters.Add(param);
                }
            }
            return command;
        }

        /// <summary>
        /// 创建批量ID查询命令。
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
                    DbParameter param = command.CreateParameter();
                    param.ParameterName = ToParamName("p" + command.Parameters.Count);
                    param.Size = key.Length;
                    param.DbType = key.DbType;
                    command.Parameters.Add(param);
                }
            }
            return command;
        }

        /// <summary>
        /// 创建批量删除命令。
        /// </summary>
        protected virtual DbCommandProxy MakeBatchDeleteCommand(int batchSize)
        {
            ColumnDefinition[] keyColumns = TableDefinition.Keys.ToArray();

            DbCommandProxy command = NewCommand();
            command.CommandText = SqlBuilder.BuildBatchDeleteSql(FactTableName, keyColumns, batchSize);

            for (int b = 0; b < batchSize; b++)
            {
                foreach (var key in keyColumns)
                {
                    DbParameter param = command.CreateParameter();
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
                    var param = (DbParameter)parameters[paramIndex++];
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

        /// <summary>
        /// 将对象集合转换为 DataTable。
        /// </summary>
        /// <param name="values">包含要转换的数据的对象集合。</param>
        /// <param name="columns">要在 DataTable 中创建的列定义集合。</param>
        /// <returns>返回填充了集合数据的 DataTable 实例。</returns>
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
                    ((DbParameter)parameters[paramIndex++]).Value = ConvertToDbValue(column.GetValue(item), column.DbType);
                }
                for (int j = 0; j < keyCount; j++)
                {
                    ColumnDefinition key = keyColumns[j];
                    ((DbParameter)parameters[paramIndex++]).Value = ConvertToDbValue(key.GetValue(item), key.DbType);
                }
            }
        }

        private void SetBatchDeleteParameterValues(ColumnDefinition[] keyColumns, List<T> batch, DbCommandProxy command)
        {
            int paramIndex = 0;
            var parameters = command.Parameters;
            int keyCount = keyColumns.Length;
            int batchCount = batch.Count;

            for (int i = 0; i < batchCount; i++)
            {
                T item = batch[i];
                for (int j = 0; j < keyCount; j++)
                {
                    ColumnDefinition key = keyColumns[j];
                    ((DbParameter)parameters[paramIndex++]).Value = ConvertToDbValue(key.GetValue(item), key.DbType);
                }
            }
        }

        private void SetBatchDeleteByKeysParameterValues(ColumnDefinition[] keyColumns, List<object[]> batch, DbCommandProxy command)
        {
            int paramIndex = 0;
            var parameters = command.Parameters;
            int keyCount = keyColumns.Length;
            int batchCount = batch.Count;

            for (int i = 0; i < batchCount; i++)
            {
                object[] keys = batch[i];
                for (int j = 0; j < keyCount; j++)
                {
                    ColumnDefinition key = keyColumns[j];
                    ((DbParameter)parameters[paramIndex++]).Value = ConvertToDbValue(keys[j], key.DbType);
                }
            }
        }
        #endregion

        #region CRUD
        /// <summary>
        /// 将实体对象插入到数据库中。
        /// </summary>
        /// <param name="t">要插入的实体对象。</param>
        /// <returns>如果插入成功则返回 true。</returns>
        /// <exception cref="ArgumentNullException">当 <paramref name="t"/> 为 null 时抛出。</exception>
        public virtual bool Insert(T t)
        {
            if (t is null) throw new ArgumentNullException("t");
            var insertCommand = GetPreparedCommand("Insert", MakeInsertCommand);
            var columns = InsertableColumns;
            int count = columns.Length;
            var parameters = insertCommand.Parameters;
            for (int i = 0; i < count; i++)
            {
                var column = columns[i];
                var param = (DbParameter)parameters[i];
                param.Value = ConvertToDbValue(column.GetValue(t), column.DbType);
            }

            if (IdentityColumn is null)
            {
                insertCommand.ExecuteNonQuery();
            }
            else
            {
                DbParameter param = insertCommand.Parameters[ToParamName(IdentityColumn.PropertyName)] as DbParameter;
                if (param is not null && param.Direction == ParameterDirection.Output)
                {
                    insertCommand.ExecuteNonQuery();
                    IdentityColumn.SetValue(t, ConvertFromDbValue(param.Value, IdentityColumn.PropertyType));
                }
                else
                {
                    IdentityColumn.SetValue(t, ConvertFromDbValue(insertCommand.ExecuteScalar(), IdentityColumn.PropertyType));
                }
            }
            return true;
        }


        /// <summary>
        /// 批量插入实体对象到数据库中。
        /// </summary>
        /// <param name="values">要插入的实体对象集合。</param>
        public virtual void BatchInsert(IEnumerable<T> values)
        {
            var provider = BulkFactory.GetProvider(TableDefinition.DataProviderType);
            var insertableColumns = TableDefinition.Columns.Where(column => !column.IsIdentity && column.Mode.CanInsert()).ToArray();
            if (provider is not null)
            {
                using (var scope = DAOContext.AcquireScope())
                {
                    provider.BulkInsert(ToDataTable(values, insertableColumns), Connection, DAOContext.CurrentTransaction);
                }
            }
            else
            {
                int columnCount = insertableColumns.Length;
                if (columnCount == 0) return;
                int batchSize = DAOContext.ParamCountLimit / 10 / columnCount * 10;
                if (batchSize == 0) batchSize = Math.Max(DAOContext.ParamCountLimit / columnCount, 1);

                long nextManualId = 0;
                bool idExists = false;
                var batch = new List<T>(batchSize);
                foreach (var item in values)
                {
                    if (!idExists && IdentityColumn is not null && !SqlBuilder.SupportBatchInsertWithIdentity)
                    {
                        Insert(item);
                        nextManualId = Convert.ToInt64(IdentityColumn.GetValue(item)) + 1;
                        idExists = true;
                        continue;
                    }

                    batch.Add(item);
                    if (batch.Count == batchSize)
                    {
                        DbCommandProxy command = GetPreparedCommand("BatchInsert" + batchSize, () => MakeBatchInsertCommand(batchSize));
                        SetParameterValues(insertableColumns, batch, command);

                        if (!idExists && IdentityColumn is not null && SqlBuilder.SupportBatchInsertWithIdentity)
                        {
                            object res = command.ExecuteScalar();
                            if (res != null && res != DBNull.Value)
                            {
                                nextManualId = Convert.ToInt64(res);
                                idExists = true;
                            }
                        }
                        else
                        {
                            command.ExecuteNonQuery();
                        }
                        if (idExists) UpdateBatchIds(batch, ref nextManualId);
                        batch.Clear();
                    }
                }
                if (batch.Count > 0)
                {
                    using DbCommandProxy command = MakeBatchInsertCommand(batch.Count);
                    SetParameterValues(insertableColumns, batch, command);

                    if (!idExists && IdentityColumn is not null && SqlBuilder.SupportBatchInsertWithIdentity)
                    {
                        object res = command.ExecuteScalar();
                        if (res != null && res != DBNull.Value)
                        {
                            nextManualId = Convert.ToInt64(res);
                            idExists = true;
                        }
                    }
                    else
                    {
                        command.ExecuteNonQuery();
                    }

                    if (idExists) UpdateBatchIds(batch, ref nextManualId);
                    batch.Clear();
                }
            }
        }


        /// <summary>
        /// 更新数据库中的实体对象。
        /// </summary>
        /// <param name="t">要更新的实体对象。</param>
        /// <param name="timestamp">时间戳值，用于乐观并发控制。</param>
        /// <returns>如果更新成功则返回 true。</returns>
        /// <exception cref="ArgumentNullException">当 <paramref name="t"/> 为 null 时抛出。</exception>
        public virtual bool Update(T t, object timestamp = null)
        {
            var updateCommand = GetPreparedCommand(timestamp == null ? "Update" : "UpdateWithTimestamp", () => MakeUpdateCommand(timestamp != null));
            var updatableColumns = UpdatableColumns;
            var keys = TableDefinition.Keys;
            int updatableCount = updatableColumns.Length;
            int keyCount = keys.Length;
            var parameters = updateCommand.Parameters;
            int paramIndex = 0;

            for (int i = 0; i < updatableCount; i++)
            {
                var column = updatableColumns[i];
                ((DbParameter)parameters[paramIndex++]).Value = ConvertToDbValue(column.GetValue(t), column.DbType);
            }

            for (int i = 0; i < keyCount; i++)
            {
                var key = keys[i];
                ((DbParameter)parameters[paramIndex++]).Value = ConvertToDbValue(key.GetValue(t), key.DbType);
            }

            if (timestamp != null)
            {
                var timestampCol = TableDefinition.Columns.First(c => c.IsTimestamp);
                ((DbParameter)parameters[paramIndex]).Value = ConvertToDbValue(timestamp, timestampCol.DbType);
            }

            return updateCommand.ExecuteNonQuery() > 0;
        }

        /// <summary>
        /// 批量更新实体对象到数据库中。
        /// </summary>
        /// <param name="values">要更新的实体对象集合。</param>
        public virtual void BatchUpdate(IEnumerable<T> values)
        {
            if (values is null) throw new ArgumentNullException(nameof(values));

            ColumnDefinition[] updatableColumns = UpdatableColumns;
            var keyColumns = TableDefinition.Keys.ToArray();
            int paramsPerUpdate = updatableColumns.Length + keyColumns.Length;
            if (paramsPerUpdate == 0) return;

            int batchSize = DAOContext.ParamCountLimit / 10 / paramsPerUpdate * 10;
            if (batchSize == 0) batchSize = Math.Max(DAOContext.ParamCountLimit / paramsPerUpdate, 1);

            var batch = new List<T>(batchSize);
            foreach (var item in values)
            {
                batch.Add(item);
                if (batch.Count == batchSize)
                {
                    DbCommandProxy command = GetPreparedCommand("BatchUpdate" + batchSize, () => MakeBatchUpdateCommand(batchSize));
                    SetBatchUpdateParameterValues(updatableColumns, keyColumns, batch, command);
                    command.ExecuteNonQuery();
                    batch.Clear();
                }
            }
            if (batch.Count > 0)
            {
                using DbCommandProxy command = MakeBatchUpdateCommand(batch.Count);
                SetBatchUpdateParameterValues(updatableColumns, keyColumns, batch, command);
                command.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// 批量更新或插入实体对象。
        /// 通过分批查询已存在记录进行筛选，再分批执行插入和更新，提高效率。
        /// </summary>
        /// <param name="values">要处理的实体对象集合。</param>
        public virtual void BatchUpdateOrInsert(IEnumerable<T> values)
        {
            if (values is null) throw new ArgumentNullException(nameof(values));

            var list = values.ToList();
            if (list.Count == 0) return;

            var keyColumns = TableDefinition.Keys;
            if (keyColumns.Length == 0)
            {
                BatchInsert(list);
                return;
            }

            var existingIds = new HashSet<List<object>>(new ListEqualityComparer<object>());
            int paramsPerKey = keyColumns.Length;
            int batchSize = DAOContext.ParamCountLimit / 10 / paramsPerKey * 10;
            if (batchSize == 0) batchSize = 1;

            var batch = new List<T>(batchSize);
            var command = GetPreparedCommand("BatchIDExists" + batchSize, () => MakeBatchIDExistsCommand(batchSize));

            foreach (var item in list)
            {
                bool validId = true;
                for (int j = 0; j < keyColumns.Length; j++)
                {
                    if (keyColumns[j].GetValue(item) == null || keyColumns[j].GetValue(item) == DBNull.Value)
                    {
                        validId = false;
                        break;
                    }
                }

                if (!validId) continue;

                for (int j = 0; j < keyColumns.Length; j++)
                {
                    ((DbParameter)command.Parameters[batch.Count * paramsPerKey + j]).Value = ConvertToDbValue(keyColumns[j].GetValue(item), keyColumns[j].DbType);
                }

                batch.Add(item);
                if (batch.Count == batchSize)
                {
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            List<object> keyValues = new List<object>();
                            for (int i = 0; i < keyColumns.Length; i++) keyValues.Add(ConvertFromDbValue(reader[i], keyColumns[i].PropertyType));
                            existingIds.Add(keyValues);
                        }
                    }
                    batch.Clear();
                }
            }
            if (batch.Count > 0)
            {
                using var cmd = MakeBatchIDExistsCommand(batch.Count);
                for (int i = 0; i < batch.Count; i++)
                {
                    for (int j = 0; j < keyColumns.Length; j++)
                    {
                        ((DbParameter)cmd.Parameters[i * paramsPerKey + j]).Value = ConvertToDbValue(keyColumns[j].GetValue(batch[i]), keyColumns[j].DbType);
                    }
                }
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        List<object> keyValues = new List<object>();
                        for (int i = 0; i < keyColumns.Length; i++) keyValues.Add(ConvertFromDbValue(reader[i], keyColumns[i].PropertyType));
                        existingIds.Add(keyValues);
                    }
                }
            }

            var toUpdate = new List<T>();
            var toInsert = new List<T>();

            foreach (var item in list)
            {
                List<object> keyValues = new List<object>();
                bool validId = true;
                for (int i = 0; i < keyColumns.Length; i++)
                {
                    var val = keyColumns[i].GetValue(item);
                    if (val == null || val == DBNull.Value)
                    {
                        validId = false;
                        break;
                    }
                    keyValues.Add(val);
                }

                if (validId && existingIds.Contains(keyValues))
                    toUpdate.Add(item);
                else
                    toInsert.Add(item);
            }

            if (toInsert.Count > 0) BatchInsert(toInsert);
            if (toUpdate.Count > 0) BatchUpdate(toUpdate);
        }

        /// <summary>
        /// 执行更新或插入（Upsert）操作。
        /// 如果记录存在（基于主键/唯一键）则更新，否则执行插入。
        /// </summary>
        /// <param name="t">要处理的实体对象。</param>
        /// <returns>操作结果：<see cref="UpdateOrInsertResult.Inserted"/> 或 <see cref="UpdateOrInsertResult.Updated"/>。</returns>
        public virtual UpdateOrInsertResult UpdateOrInsert(T t)
        {
            if (t is null) throw new ArgumentNullException("t");
            if (Update(t))
            {
                return UpdateOrInsertResult.Updated;
            }
            Insert(t);
            return UpdateOrInsertResult.Inserted;
        }

        /// <summary>
        /// 将对象从数据库删除
        /// </summary>
        /// <param name="t">待删除的对象</param>
        /// <returns>是否成功删除</returns>
        public virtual bool Delete(T t)
        {
            if (t is null) throw new ArgumentNullException("t");
            return DeleteByKeys(GetKeyValues(t));
        }

        /// <summary>
        /// 根据条件删除对象
        /// </summary>
        /// <param name="expr">条件</param>
        /// <returns>删除对象数量</returns>
        public virtual int Delete(LogicExpr expr)
        {
            var deleteExpr = new DeleteExpr(new FromExpr(ObjectType), expr);
            using var command = MakeExprCommand(deleteExpr);    
            return command.ExecuteNonQuery();
        }

        /// <summary>
        /// 根据UpdateExpr更新数据
        /// </summary>
        /// <param name="expr">更新表达式</param>
        /// <returns>更新的记录数</returns>
        public virtual int Update(UpdateExpr expr)
        {
            var command = MakeExprCommand(expr);
            return command.ExecuteNonQuery();
        }

        /// <summary>
        /// 将指定主键的对象从数据库删除
        /// </summary>
        /// <param name="keys">待删除的对象的主键</param>
        /// <returns>是否成功删除</returns>
        public virtual bool DeleteByKeys(params object[] keys)
        {
            ThrowExceptionIfWrongKeys(keys);
            var deleteCommand = GetPreparedCommand("Delete", MakeDeleteCommand);
            int count = deleteCommand.Parameters.Count;
            var parameters = deleteCommand.Parameters;
            var keyColumns = Table.Keys;

            for (int i = 0; i < count; i++)
            {
                ((DbParameter)parameters[i]).Value = ConvertToDbValue(keys[i], keyColumns[i].DbType);
            }
            return deleteCommand.ExecuteNonQuery() > 0;
        }

        /// <summary>
        /// 批量删除实体对象。
        /// </summary>
        /// <param name="values">要删除的实体对象集合。</param>
        public virtual void BatchDelete(IEnumerable<T> values)
        {
            if (values is null) throw new ArgumentNullException(nameof(values));
            BatchDeleteByKeys(values.Select(GetKeyValues));
        }

        /// <summary>
        /// 批量根据主键删除实体对象。
        /// </summary>
        /// <param name="keys">主键集合。</param>
        public virtual void BatchDeleteByKeys(IEnumerable keys)
        {
            if (keys is null) throw new ArgumentNullException(nameof(keys));

            var keyColumns = TableDefinition.Keys.ToArray();
            int paramsPerDelete = keyColumns.Length;
            if (paramsPerDelete == 0) return;

            int batchSize = DAOContext.ParamCountLimit / 10 / paramsPerDelete * 10;
            if (batchSize == 0) batchSize = Math.Max(DAOContext.ParamCountLimit / paramsPerDelete, 1);

            var batch = new List<object[]>(batchSize);
            foreach (var item in keys)
            {
                object[] keyValues = item as object[];
                if (keyValues == null || keyValues.Length != paramsPerDelete)
                    throw new ArgumentException($"Composite key requires object[{paramsPerDelete}]");

                batch.Add(keyValues);
                if (batch.Count == batchSize)
                {
                    DbCommandProxy command = GetPreparedCommand("BatchDelete" + batchSize, () => MakeBatchDeleteCommand(batchSize));
                    SetBatchDeleteByKeysParameterValues(keyColumns, batch, command);
                    command.ExecuteNonQuery();
                    batch.Clear();
                }
            }
            if (batch.Count > 0)
            {
                using DbCommandProxy command = MakeBatchDeleteCommand(batch.Count);
                SetBatchDeleteByKeysParameterValues(keyColumns, batch, command);
                command.ExecuteNonQuery();
            }
        }

        #endregion

        #region IObjectDAOAsync implementations

        /// <summary>
        /// 异步将实体对象插入到数据库中。
        /// </summary>
        /// <param name="t">要插入的实体对象。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>表示异步操作的任务，如果插入成功则返回 true。</returns>
        public async virtual Task<bool> InsertAsync(T t, CancellationToken cancellationToken = default)
        {
            if (t is null) throw new ArgumentNullException("t");
            var insertCommand = GetPreparedCommand("Insert", MakeInsertCommand);
            var columns = InsertableColumns;
            int count = columns.Length;
            var parameters = insertCommand.Parameters;

            for (int i = 0; i < count; i++)
            {
                var column = columns[i];
                ((DbParameter)parameters[i]).Value = ConvertToDbValue(column.GetValue(t), column.DbType);
            }

            if (IdentityColumn is null)
            {
                await insertCommand.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
            else
            {
                string propertyName = ToParamName(IdentityColumn.PropertyName);
                DbParameter param = insertCommand.Parameters.Contains(propertyName) ? (DbParameter)insertCommand.Parameters[propertyName] : null;
                if (param is not null && param.Direction == ParameterDirection.Output)
                {
                    await insertCommand.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                    IdentityColumn.SetValue(t, ConvertFromDbValue(param.Value, IdentityColumn.PropertyType));
                }
                else
                {
                    IdentityColumn.SetValue(t, ConvertFromDbValue(await insertCommand.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false), IdentityColumn.PropertyType));
                }
            }

            return true;
        }


        /// <summary>   
        /// 异步批量插入实体对象到数据库中。
        /// </summary>
        /// <param name="values">要插入的实体对象集合。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>表示异步操作的任务。</returns>
        public async virtual Task BatchInsertAsync(IEnumerable<T> values, CancellationToken cancellationToken = default)
        {
            var provider = BulkFactory.GetProvider(TableDefinition.DataProviderType);
            var insertableColumns = TableDefinition.Columns.Where(column => !column.IsIdentity && column.Mode.CanInsert()).ToArray();
            if (provider is not null)
            {
                using (var scope = await DAOContext.AcquireScopeAsync(cancellationToken).ConfigureAwait(false))
                {
                    await Task.Run(() => provider.BulkInsert(ToDataTable(values, insertableColumns), Connection, DAOContext.CurrentTransaction), cancellationToken).ConfigureAwait(false);
                }
            }
            else
            {
                int columnCount = insertableColumns.Length;
                if (columnCount == 0) return;
                int batchSize = DAOContext.ParamCountLimit / 10 / columnCount * 10;
                if (batchSize == 0) batchSize = 1;

                long nextManualId = 0;
                bool idExists = false;
                var batch = new List<T>(batchSize);
                foreach (var item in values)
                {
                    if (!idExists && IdentityColumn is not null && !SqlBuilder.SupportBatchInsertWithIdentity)
                    {
                        var res = await InsertAsync(item, cancellationToken);
                        if (res)
                        {
                            nextManualId = Convert.ToInt64(IdentityColumn.GetValue(item)) + 1;
                            idExists = true;
                        }
                        continue;
                    }

                    batch.Add(item);
                    if (batch.Count == batchSize)
                    {
                        DbCommandProxy command = GetPreparedCommand("BatchInsert" + batchSize, () => MakeBatchInsertCommand(batchSize));
                        SetParameterValues(insertableColumns, batch, command);

                        if (!idExists && IdentityColumn is not null && SqlBuilder.SupportBatchInsertWithIdentity)
                        {
                            object res = await command.ExecuteScalarAsync(cancellationToken);
                            if (res != null && res != DBNull.Value)
                            {
                                nextManualId = Convert.ToInt64(res);
                                idExists = true;
                            }
                        }
                        else
                        {
                            await command.ExecuteNonQueryAsync(cancellationToken);
                        }
                        if (idExists) UpdateBatchIds(batch, ref nextManualId);
                        batch.Clear();
                    }
                }
                if (batch.Count > 0)
                {
                    using DbCommandProxy command = MakeBatchInsertCommand(batch.Count);
                    SetParameterValues(insertableColumns, batch, command);

                    if (!idExists && IdentityColumn is not null && SqlBuilder.SupportBatchInsertWithIdentity)
                    {
                        object res = await command.ExecuteScalarAsync(cancellationToken);
                        if (res != null && res != DBNull.Value)
                        {
                            nextManualId = Convert.ToInt64(res);
                            idExists = true;
                        }
                    }
                    else
                    {
                        await command.ExecuteNonQueryAsync(cancellationToken);
                    }

                    if (idExists) UpdateBatchIds(batch, ref nextManualId);
                    batch.Clear();
                }
            }
        }


        /// <summary>
        /// 异步更新数据库中的实体对象。
        /// </summary>
        /// <param name="t">要更新的实体对象。</param>
        /// <param name="timestamp">时间戳值，用于乐观并发控制。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>表示异步操作的任务，如果更新成功则返回 true。</returns>
        public async virtual Task<bool> UpdateAsync(T t, object timestamp = null, CancellationToken cancellationToken = default)
        {
            var updateCommand = GetPreparedCommand(timestamp == null ? "Update" : "UpdateWithTimestamp", () => MakeUpdateCommand(timestamp != null));
            var updatableColumns = UpdatableColumns;
            var keys = TableDefinition.Keys;
            int updatableCount = updatableColumns.Length;
            int keyCount = keys.Length;
            var parameters = updateCommand.Parameters;
            int paramIndex = 0;

            for (int i = 0; i < updatableCount; i++)
            {
                var column = updatableColumns[i];
                ((DbParameter)parameters[paramIndex++]).Value = ConvertToDbValue(column.GetValue(t), column.DbType);
            }

            for (int i = 0; i < keyCount; i++)
            {
                var key = keys[i];
                ((DbParameter)parameters[paramIndex++]).Value = ConvertToDbValue(key.GetValue(t), key.DbType);
            }

            if (timestamp != null)
            {
                var timestampCol = TableDefinition.Columns.First(c => c.IsTimestamp);
                ((DbParameter)parameters[paramIndex]).Value = ConvertToDbValue(timestamp, timestampCol.DbType);
            }
            return await updateCommand.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false) > 0;
        }

        /// <summary>
        /// 异步批量更新实体对象到数据库中。
        /// </summary>
        /// <param name="values">要更新的实体对象集合。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>表示异步操作的任务。</returns>
        public async virtual Task BatchUpdateAsync(IEnumerable<T> values, CancellationToken cancellationToken = default)
        {
            if (values is null) throw new ArgumentNullException(nameof(values));

            ColumnDefinition[] updatableColumns = UpdatableColumns;
            if (updatableColumns.Length == 0) return;
            var keyColumns = TableDefinition.Keys.ToArray();
            int paramsPerUpdate = updatableColumns.Length + keyColumns.Length;

            int batchSize = DAOContext.ParamCountLimit / 10 / paramsPerUpdate * 10;
            if (batchSize == 0) batchSize = 1;

            var batch = new List<T>(batchSize);
            foreach (var t in values)
            {
                batch.Add(t);
                if (batch.Count == batchSize)
                {
                    DbCommandProxy command = GetPreparedCommand("BatchUpdate" + batchSize, () => MakeBatchUpdateCommand(batchSize));
                    SetBatchUpdateParameterValues(updatableColumns, keyColumns, batch, command);
                    await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                    batch.Clear();
                }
            }
            if (batch.Count > 0)
            {
                using DbCommandProxy command = MakeBatchUpdateCommand(batch.Count);
                SetBatchUpdateParameterValues(updatableColumns, keyColumns, batch, command);
                await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// 异步批量更新或插入实体对象。
        /// 通过分批查询已存在记录进行筛选，再分批执行插入和更新，提高效率。
        /// </summary>
        /// <param name="values">要处理的实体集。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        public async virtual Task BatchUpdateOrInsertAsync(IEnumerable<T> values, CancellationToken cancellationToken = default)
        {
            if (values is null) throw new ArgumentNullException(nameof(values));

            var list = values.ToList();
            if (list.Count == 0) return;

            var keyColumns = TableDefinition.Keys;
            if (keyColumns.Length == 0)
            {
                await BatchInsertAsync(list, cancellationToken).ConfigureAwait(false);
                return;
            }

            var existingIds = new HashSet<List<object>>(new ListEqualityComparer<object>());
            int paramsPerKey = keyColumns.Length;
            int batchSize = DAOContext.ParamCountLimit / 10 / paramsPerKey * 10;
            if (batchSize == 0) batchSize = 1;

            var batch = new List<T>(batchSize);
            var command = GetPreparedCommand("BatchIDExists" + batchSize, () => MakeBatchIDExistsCommand(batchSize));

            foreach (var item in list)
            {
                cancellationToken.ThrowIfCancellationRequested();
                bool validId = true;
                for (int j = 0; j < keyColumns.Length; j++)
                {
                    if (keyColumns[j].GetValue(item) == null || keyColumns[j].GetValue(item) == DBNull.Value)
                    {
                        validId = false;
                        break;
                    }
                }

                if (!validId) continue;

                for (int j = 0; j < keyColumns.Length; j++)
                {
                    ((DbParameter)command.Parameters[batch.Count * paramsPerKey + j]).Value = ConvertToDbValue(keyColumns[j].GetValue(item), keyColumns[j].DbType);
                }

                batch.Add(item);
                if (batch.Count == batchSize)
                {
                    using (var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
                    {
                        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                        {
                            List<object> keyValues = new List<object>();
                            for (int i = 0; i < keyColumns.Length; i++) keyValues.Add(ConvertFromDbValue(reader[i], keyColumns[i].PropertyType));
                            existingIds.Add(keyValues);
                        }
                    }
                    batch.Clear();
                }
            }

            if (batch.Count > 0)
            {
                using var cmd = MakeBatchIDExistsCommand(batch.Count);
                for (int i = 0; i < batch.Count; i++)
                {
                    for (int j = 0; j < keyColumns.Length; j++)
                    {
                        ((DbParameter)cmd.Parameters[i * paramsPerKey + j]).Value = ConvertToDbValue(keyColumns[j].GetValue(batch[i]), keyColumns[j].DbType);
                    }
                }
                using (var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
                {
                    while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                    {
                        List<object> keyValues = new List<object>();
                        for (int i = 0; i < keyColumns.Length; i++) keyValues.Add(ConvertFromDbValue(reader[i], keyColumns[i].PropertyType));
                        existingIds.Add(keyValues);
                    }
                }
            }

            var toUpdate = new List<T>();
            var toInsert = new List<T>();

            foreach (var item in list)
            {
                List<object> keyValues = new List<object>();
                bool validId = true;
                for (int i = 0; i < keyColumns.Length; i++)
                {
                    var val = keyColumns[i].GetValue(item);
                    if (val == null || val == DBNull.Value)
                    {
                        validId = false;
                        break;
                    }
                    keyValues.Add(val);
                }

                if (validId && existingIds.Contains(keyValues))
                    toUpdate.Add(item);
                else
                    toInsert.Add(item);
            }

            if (toInsert.Count > 0) await BatchInsertAsync(toInsert, cancellationToken).ConfigureAwait(false);
            if (toUpdate.Count > 0) await BatchUpdateAsync(toUpdate, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// 异步执行更新或插入（Upsert）操作。
        /// </summary>
        /// <param name="t">要处理的实体对象。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>表示异步操作的任务，结果为操作结果类型。</returns>
        public async virtual Task<UpdateOrInsertResult> UpdateOrInsertAsync(T t, CancellationToken cancellationToken = default)
        {
            if (t is null) throw new ArgumentNullException("t");
            if (await UpdateAsync(t, null, cancellationToken).ConfigureAwait(false))
            {
                return UpdateOrInsertResult.Updated;
            }
            await InsertAsync(t, cancellationToken).ConfigureAwait(false);
            return UpdateOrInsertResult.Inserted;
        }


        /// <summary>
        /// 将对象 from 数据库删除
        /// </summary>
        /// <param name="t">待删除的对象</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>表示异步操作的任务，如果删除成功则返回 true。</returns>
        public async virtual Task<bool> DeleteAsync(T t, CancellationToken cancellationToken = default)
        {
            if (t is null) throw new ArgumentNullException("t");
            return await DeleteByKeysAsync(GetKeyValues(t), cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// 异步将指定主键的对象从数据库删除。
        /// </summary>
        /// <param name="keys">待删除的对象的主键。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>表示异步操作的任务，如果删除成功则返回 true。</returns>
        public async virtual Task<bool> DeleteByKeysAsync(object[] keys, CancellationToken cancellationToken = default)
        {
            ThrowExceptionIfWrongKeys(keys);
            var deleteCommand = GetPreparedCommand("Delete", MakeDeleteCommand);
            int i = 0;
            foreach (DbParameter param in deleteCommand.Parameters)
            {
                param.Value = ConvertToDbValue(keys[i], Table.Keys[i].DbType);
                i++;
            }
            return await deleteCommand.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false) > 0;
        }


        /// <summary>
        /// 异步根据条件删除对象。
        /// </summary>
        /// <param name="expr">条件。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>表示异步操作的任务，返回删除对象数量。</returns>
        public async virtual Task<int> DeleteAsync(LogicExpr expr, CancellationToken cancellationToken = default)
        {
            var deleteExpr = new DeleteExpr(new FromExpr(ObjectType), expr);
            using var command = MakeExprCommand(deleteExpr);    
            return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// 异步根据UpdateExpr更新数据
        /// </summary>
        /// <param name="expr">更新表达式</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>表示异步操作的任务，任务结果包含更新的记录数</returns>
        public async virtual Task<int> UpdateAsync(UpdateExpr expr, CancellationToken cancellationToken = default)
        {
            var command = MakeExprCommand(expr);
            return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// 异步批量删除实体对象。
        /// </summary>
        /// <param name="values">要删除的实体对象集合。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        public async virtual Task BatchDeleteAsync(IEnumerable<T> values, CancellationToken cancellationToken = default)
        {
            if (values is null) throw new ArgumentNullException(nameof(values));
            await BatchDeleteByKeysAsync(values.Select(GetKeyValues), cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// 异步批量根据主键删除实体对象。
        /// </summary>
        /// <param name="keys">主键集合。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        public async virtual Task BatchDeleteByKeysAsync(IEnumerable keys, CancellationToken cancellationToken = default)
        {
            if (keys is null) throw new ArgumentNullException(nameof(keys));

            var keyColumns = TableDefinition.Keys.ToArray();
            int paramsPerDelete = keyColumns.Length;
            if (paramsPerDelete == 0) return;

            int batchSize = DAOContext.ParamCountLimit / 10 / paramsPerDelete * 10;
            if (batchSize == 0) batchSize = Math.Max(DAOContext.ParamCountLimit / paramsPerDelete, 1);

            var batch = new List<object[]>(batchSize);
            foreach (var item in keys)
            {
                cancellationToken.ThrowIfCancellationRequested();
                object[] keyValues = item as object[];
                if (keyValues == null || keyValues.Length != paramsPerDelete)
                    throw new ArgumentException($"Composite key requires object[{paramsPerDelete}]");

                batch.Add(keyValues);
                if (batch.Count == batchSize)
                {
                    DbCommandProxy command = GetPreparedCommand("BatchDelete" + batchSize, () => MakeBatchDeleteCommand(batchSize));
                    SetBatchDeleteByKeysParameterValues(keyColumns, batch, command);
                    await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                    batch.Clear();
                }
            }
            if (batch.Count > 0)
            {
                using DbCommandProxy command = MakeBatchDeleteCommand(batch.Count);
                SetBatchDeleteByKeysParameterValues(keyColumns, batch, command);
                await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        #endregion

        #region IObjectDAO Members

        bool IObjectDAO.Insert(object o)
        {
            return Insert((T)o);
        }
        void IObjectDAO.BatchInsert(IEnumerable values)
        {
            if (values is IEnumerable<T> typed)
                BatchInsert(typed);
            else
            {
                List<T> list = new List<T>();
                foreach (T entity in values)
                {
                    list.Add((T)entity);
                }
                BatchInsert(list);
            }
        }

        bool IObjectDAO.Update(object o)
        {
            return Update((T)o);
        }

        UpdateOrInsertResult IObjectDAO.UpdateOrInsert(object o)
        {
            return UpdateOrInsert((T)o);
        }

        void IObjectDAO.BatchUpdateOrInsert(IEnumerable values)
        {
            if (values is IEnumerable<T> typed)
                BatchUpdateOrInsert(typed);
            else
            {
                List<T> list = new List<T>();
                foreach (T entity in values)
                {
                    list.Add((T)entity);
                }
                BatchUpdateOrInsert(list);
            }
        }

        void IObjectDAO.BatchUpdate(IEnumerable values)
        {
            if (values is IEnumerable<T> typed)
                BatchUpdate(typed);
            else
            {
                List<T> list = new List<T>();
                foreach (T entity in values)
                {
                    list.Add((T)entity);
                }
                BatchUpdate(list);
            }
        }

        bool IObjectDAO.Delete(object o)
        {
            return Delete((T)o);
        }

        void IObjectDAO.BatchDelete(IEnumerable values)
        {
            if (values is IEnumerable<T> typed)
                BatchDelete(typed);
            else
            {
                List<T> list = new List<T>();
                foreach (object entity in values)
                {
                    list.Add((T)entity);
                }
                BatchDelete(list);
            }
        }

        void IObjectDAO.BatchDeleteByKeys(IEnumerable keys)
        {
            BatchDeleteByKeys(keys);
        }
        #endregion

        #region IObjectDAOAsync implementations

        async Task<bool> IObjectDAOAsync.InsertAsync(object o, CancellationToken cancellationToken)
        {
            return await InsertAsync((T)o, cancellationToken).ConfigureAwait(false);
        }

        async Task IObjectDAOAsync.BatchInsertAsync(IEnumerable values, CancellationToken cancellationToken)
        {
            if (values is IEnumerable<T> typed)
                await BatchInsertAsync(typed, cancellationToken).ConfigureAwait(false);
            else
            {
                List<T> list = new List<T>();
                foreach (T entity in values)
                {
                    list.Add((T)entity);
                }
                await BatchInsertAsync(list, cancellationToken).ConfigureAwait(false);
            }
        }

        async Task<bool> IObjectDAOAsync.UpdateAsync(object o, CancellationToken cancellationToken)
        {
            return await UpdateAsync((T)o, null, cancellationToken).ConfigureAwait(false);
        }

        async Task<UpdateOrInsertResult> IObjectDAOAsync.UpdateOrInsertAsync(object o, CancellationToken cancellationToken)
        {
            return await UpdateOrInsertAsync((T)o, cancellationToken).ConfigureAwait(false);
        }

        async Task IObjectDAOAsync.BatchUpdateAsync(IEnumerable values, CancellationToken cancellationToken)
        {
            if (values is IEnumerable<T> typed)
                await BatchUpdateAsync(typed, cancellationToken).ConfigureAwait(false);
            else
            {
                List<T> list = new List<T>();
                foreach (T entity in values)
                {
                    list.Add((T)entity);
                }
                await BatchUpdateAsync(list, cancellationToken).ConfigureAwait(false);
            }
        }

        async Task IObjectDAOAsync.BatchUpdateOrInsertAsync(IEnumerable values, CancellationToken cancellationToken)
        {
            if (values is IEnumerable<T> typed)
                await BatchUpdateOrInsertAsync(typed, cancellationToken).ConfigureAwait(false);
            else
            {
                List<T> list = new List<T>();
                foreach (T entity in values)
                {
                    list.Add((T)entity);
                }
                await BatchUpdateOrInsertAsync(list, cancellationToken).ConfigureAwait(false);
            }
        }

        async Task<bool> IObjectDAOAsync.DeleteAsync(object o, CancellationToken cancellationToken)
        {
            return await DeleteAsync((T)o, cancellationToken).ConfigureAwait(false);
        }

        async Task IObjectDAOAsync.BatchDeleteAsync(IEnumerable values, CancellationToken cancellationToken)
        {
            if (values is IEnumerable<T> typed)
                await BatchDeleteAsync(typed, cancellationToken).ConfigureAwait(false);
            else
            {
                List<T> list = new List<T>();
                foreach (object entity in values)
                {
                    list.Add((T)entity);
                }
                await BatchDeleteAsync(list, cancellationToken).ConfigureAwait(false);
            }
        }

        async Task IObjectDAOAsync.BatchDeleteByKeysAsync(IEnumerable keys, CancellationToken cancellationToken)
        {
            await BatchDeleteByKeysAsync(keys, cancellationToken).ConfigureAwait(false);
        }

        async Task<bool> IObjectDAOAsync.DeleteByKeysAsync(object[] keys, CancellationToken cancellationToken)
        {
            return await DeleteByKeysAsync(keys, cancellationToken).ConfigureAwait(false);
        }

        async Task<int> IObjectDAOAsync.DeleteAsync(LogicExpr expr, CancellationToken cancellationToken)
        {
            return await DeleteAsync(expr, cancellationToken).ConfigureAwait(false);
        }

        #endregion
    }
}
