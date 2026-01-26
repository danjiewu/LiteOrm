using System;
using System.Collections.Generic;
using System.Text;
using System.Data;
using LiteOrm.Common;
using System.Linq.Expressions;
using Microsoft.Extensions.DependencyInjection;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using Autofac.Core;

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
    public class ObjectDAO<T> : ObjectDAOBase, IObjectDAO<T>
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
            command.CommandText = $"update {ToSqlName(FactTableName)} set {strColumns} {ToWhereSql(MakeIsKeyCondition(command) + strTimestamp)}";
            return command;
        }


        /// <summary>
        /// 构建实体删除命令。
        /// </summary>
        /// <returns>返回删除命令实例。</returns>
        protected virtual DbCommandProxy MakeDeleteCommand()
        {
            DbCommandProxy command = NewCommand();
            command.CommandText = $"delete from {ToSqlName(FactTableName)} {ToWhereSql(MakeIsKeyCondition(command))}";
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
            var insertCommand = DAOContext.PreparedCommands.GetOrAdd((ObjectType, "Insert"), _ => MakeInsertCommand());
            var columns = InsertableColumns;
            int count = columns.Length;
            var parameters = insertCommand.Parameters;
            for (int i = 0; i < count; i++)
            {
                var column = columns[i];
                var param = (IDataParameter)parameters[i];
                param.Value = ConvertToDbValue(column.GetValue(t), column.DbType);
            }

            if (IdentityColumn is null)
            {
                insertCommand.ExecuteNonQuery();
            }
            else
            {
                IDataParameter param = insertCommand.Parameters[ToParamName(IdentityColumn.PropertyName)] as IDataParameter;
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
                int columnCount = Math.Max(insertableColumns.Length, 1);
                int batchSize = 100 / columnCount * 10;
                if (batchSize == 0) batchSize = 1;

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
                        DbCommandProxy command = DAOContext.PreparedCommands.GetOrAdd((ObjectType, "BatchInsert" + batchSize), _ => MakeBatchInsertCommand(batchSize));
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
        /// 更新数据库中的实体对象。
        /// </summary>
        /// <param name="t">要更新的实体对象。</param>
        /// <param name="timestamp">时间戳值，用于乐观并发控制。</param>
        /// <returns>如果更新成功则返回 true。</returns>
        /// <exception cref="ArgumentNullException">当 <paramref name="t"/> 为 null 时抛出。</exception>
        public virtual bool Update(T t, object timestamp = null)
        {
            var updateCommand = DAOContext.PreparedCommands.GetOrAdd((ObjectType, timestamp == null ? "Update" : "UpdateWithTimestamp"), _ => MakeUpdateCommand(timestamp != null));
            var updatableColumns = UpdatableColumns;
            var keys = TableDefinition.Keys;
            int updatableCount = updatableColumns.Length;
            int keyCount = keys.Count;
            var parameters = updateCommand.Parameters;
            int paramIndex = 0;

            for (int i = 0; i < updatableCount; i++)
            {
                var column = updatableColumns[i];
                ((IDataParameter)parameters[paramIndex++]).Value = ConvertToDbValue(column.GetValue(t), column.DbType);
            }

            for (int i = 0; i < keyCount; i++)
            {
                var key = keys[i];
                ((IDataParameter)parameters[paramIndex++]).Value = ConvertToDbValue(key.GetValue(t), key.DbType);
            }

            if (timestamp != null)
            {
                var timestampCol = TableDefinition.Columns.First(c => c.IsTimestamp);
                ((IDataParameter)parameters[paramIndex]).Value = ConvertToDbValue(timestamp, timestampCol.DbType);
            }

            return updateCommand.ExecuteNonQuery() > 0;
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
            var command = DAOContext.PreparedCommands.GetOrAdd((ObjectType, "UpdateOrInsert"), _ => MakeUpdateOrInsertCommand());
            foreach (IDataParameter param in command.Parameters)
            {
                if (param.Direction == ParameterDirection.Input || param.Direction == ParameterDirection.InputOutput)
                {
                    ColumnDefinition column = TableDefinition.GetColumn(ToNativeName(param.ParameterName));
                    param.Value = ConvertToDbValue(column.GetValue(t), column.DbType);
                }
            }

            if (IdentityColumn != null)
            {
                string propertyName = ToParamName(IdentityColumn.PropertyName);
                IDataParameter param = null;
                foreach (IDataParameter p in command.Parameters)
                    if (p.Direction == ParameterDirection.Output)
                    {
                        param = p;
                        break;
                    }
                if (param != null)
                {
                    command.ExecuteNonQuery();
                    int ret = Convert.ToInt32(param.Value);
                    if (ret > 0)
                    {
                        IdentityColumn.SetValue(t, ConvertFromDbValue(param.Value, IdentityColumn.PropertyType));
                        return UpdateOrInsertResult.Inserted;
                    }
                    return UpdateOrInsertResult.Updated;
                }
            }

            object res = command.ExecuteScalar();
            int retVal = Convert.ToInt32(res);
            if (retVal > 0)
            {
                if (IdentityColumn is not null) IdentityColumn.SetValue(t, ConvertFromDbValue(res, IdentityColumn.PropertyType));
                return UpdateOrInsertResult.Inserted;
            }
            return UpdateOrInsertResult.Updated;
        }


        /// <summary>
        /// 根据条件更新数据
        /// </summary>
        /// <param name="values">需要更新的属性及数值，key为属性名，value为数值</param>
        /// <param name="expr">更新的条件</param>
        /// <returns>更新的记录数</returns>
        public virtual int UpdateAllValues(IEnumerable<KeyValuePair<string, object>> values, Expr expr)
        {
            List<string> strSets = new List<string>();
            List<KeyValuePair<string, object>> paramValues = new List<KeyValuePair<string, object>>();
            foreach (KeyValuePair<string, object> value in values)
            {
                SqlColumn column = Table.GetColumn(value.Key);
                if (column is null) throw new Exception($"Property \"{value.Key}\" does not exist in type \"{Table.DefinitionType.FullName}\".");
                strSets.Add($"{SqlBuilder.ToSqlName(column.Name)} ={ToSqlParam(paramValues.Count.ToString())}");
                paramValues.Add(paramValues.Count.ToString(), value.Value);
            }
            string where = expr.ToSql(SqlBuildContext, SqlBuilder, paramValues);
            string updateSql = $"update {ParamTable} set {String.Join(",", strSets.ToArray())} {ToWhereSql(where)}";
            using var command = MakeNamedParamCommand(updateSql, paramValues);
            return command.ExecuteNonQuery();
        }

        /// <summary>
        /// 根据主键更新数据
        /// </summary>
        /// <param name="values">需要更新的属性及数值，key为属性名，value为数值</param>
        /// <param name="keys">主键</param>
        /// <returns>更新是否成功</returns>
        public virtual bool UpdateValues(IEnumerable<KeyValuePair<string, object>> values, params object[] keys)
        {
            ThrowExceptionIfNoKeys();
            ThrowExceptionIfWrongKeys(keys);
            ExprSet expr = new ExprSet(ExprJoinType.And);
            int i = 0;
            foreach (ColumnDefinition column in Table.Keys)
            {
                expr.Add(Expr.Property(column.PropertyName, keys[i++]));
            }
            return UpdateAllValues(values, expr) > 0;
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
        public virtual int Delete(Expr expr)
        {
            using var command = MakeConditionCommand($"delete from {ParamTable} {ParamWhere}", expr);
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
            var deleteCommand = DAOContext.PreparedCommands.GetOrAdd((ObjectType, "Delete"), _ => MakeDeleteCommand());
            int count = deleteCommand.Parameters.Count;
            var parameters = deleteCommand.Parameters;
            var keyColumns = Table.Keys;

            for (int i = 0; i < count; i++)
            {
                ((IDataParameter)parameters[i]).Value = ConvertToDbValue(keys[i], keyColumns[i].DbType);
            }
            return deleteCommand.ExecuteNonQuery() > 0;
        }


        #endregion

        #region IObjectDAO Members

        bool IObjectDAO.Insert(object o)
        {
            return Insert((T)o);
        }
        void IObjectDAO.BatchInsert(IEnumerable values)
        {
            if (values is IEnumerable<T>)
                BatchInsert(values as IEnumerable<T>);
            else
            {
                List<T> list = new List<T>();
                foreach (T entity in values)
                {
                    list.Add(entity);
                }
                BatchInsert(list);
            }
        }

        bool IObjectDAO.Update(object o)
        {
            return Update((T)o);
        }

        bool IObjectDAO.Delete(object o)
        {
            return Delete((T)o);
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
            var insertCommand = DAOContext.PreparedCommands.GetOrAdd((ObjectType, "Insert"), _ => MakeInsertCommand());
            var columns = InsertableColumns;
            int count = columns.Length;
            var parameters = insertCommand.Parameters;

            for (int i = 0; i < count; i++)
            {
                var column = columns[i];
                ((IDataParameter)parameters[i]).Value = ConvertToDbValue(column.GetValue(t), column.DbType);
            }

            if (IdentityColumn is null)
            {
                await insertCommand.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
            else
            {
                string propertyName = ToParamName(IdentityColumn.PropertyName);
                IDataParameter param = insertCommand.Parameters.Contains(propertyName) ? (IDataParameter)insertCommand.Parameters[propertyName] : null;
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
                int columnCount = Math.Max(insertableColumns.Length, 1);
                int batchSize = 100 / columnCount * 10;
                if (batchSize == 0) batchSize = 1;

                long nextManualId = 0;
                bool idExists = false;
                var batch = new List<T>(batchSize);
                foreach (var item in values)
                {
                    if (!idExists && IdentityColumn is not null && !SqlBuilder.SupportBatchInsertWithIdentity)
                    {
                        var res = await InsertAsync(item);
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
                        DbCommandProxy command = DAOContext.PreparedCommands.GetOrAdd((ObjectType, "BatchInsert" + batchSize), _ => MakeBatchInsertCommand(batchSize));
                        SetParameterValues(insertableColumns, batch, command);

                        if (!idExists && IdentityColumn is not null && SqlBuilder.SupportBatchInsertWithIdentity)
                        {
                            object res = await command.ExecuteScalarAsync();
                            if (res != null && res != DBNull.Value)
                            {
                                nextManualId = Convert.ToInt64(res);
                                idExists = true;
                            }
                        }
                        else
                        {
                            await command.ExecuteNonQueryAsync();
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
                        object res = await command.ExecuteScalarAsync();
                        if (res != null && res != DBNull.Value)
                        {
                            nextManualId = Convert.ToInt64(res);
                            idExists = true;
                        }
                    }
                    else
                    {
                        await command.ExecuteNonQueryAsync();
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
            var updateCommand = DAOContext.PreparedCommands.GetOrAdd((ObjectType, timestamp == null ? "Update" : "UpdateWithTimestamp"), _ => MakeUpdateCommand(timestamp != null));
            var updatableColumns = UpdatableColumns;
            var keys = TableDefinition.Keys;
            int updatableCount = updatableColumns.Length;
            int keyCount = keys.Count;
            var parameters = updateCommand.Parameters;
            int paramIndex = 0;

            for (int i = 0; i < updatableCount; i++)
            {
                var column = updatableColumns[i];
                ((IDataParameter)parameters[paramIndex++]).Value = ConvertToDbValue(column.GetValue(t), column.DbType);
            }

            for (int i = 0; i < keyCount; i++)
            {
                var key = keys[i];
                ((IDataParameter)parameters[paramIndex++]).Value = ConvertToDbValue(key.GetValue(t), key.DbType);
            }

            if (timestamp != null)
            {
                var timestampCol = TableDefinition.Columns.First(c => c.IsTimestamp);
                ((IDataParameter)parameters[paramIndex]).Value = ConvertToDbValue(timestamp, timestampCol.DbType);
            }
            return await updateCommand.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false) > 0;
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
            var command = DAOContext.PreparedCommands.GetOrAdd((ObjectType, "UpdateOrInsert"), _ => MakeUpdateOrInsertCommand());
            foreach (IDataParameter param in command.Parameters)
            {
                if (param.Direction == ParameterDirection.Input || param.Direction == ParameterDirection.InputOutput)
                {
                    ColumnDefinition column = TableDefinition.GetColumn(ToNativeName(param.ParameterName));
                    param.Value = ConvertToDbValue(column.GetValue(t), column.DbType);
                }
            }

            if (IdentityColumn != null)
            {
                string propertyName = ToParamName(IdentityColumn.PropertyName);
                IDataParameter param = null;
                foreach (IDataParameter p in command.Parameters)
                    if (p.Direction == ParameterDirection.Output)
                    {
                        param = p;
                        break;
                    }
                if (param != null)
                {
                    await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                    int ret = Convert.ToInt32(param.Value);
                    if (ret > 0)
                    {
                        IdentityColumn.SetValue(t, ConvertFromDbValue(param.Value, IdentityColumn.PropertyType));
                        return UpdateOrInsertResult.Inserted;
                    }
                    return UpdateOrInsertResult.Updated;
                }
            }

            object res = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            int retVal = Convert.ToInt32(res);
            if (retVal > 0)
            {
                if (IdentityColumn is not null) IdentityColumn.SetValue(t, ConvertFromDbValue(res, IdentityColumn.PropertyType));
                return UpdateOrInsertResult.Inserted;
            }
            return UpdateOrInsertResult.Updated;
        }


        /// <summary>
        /// 异步将对象从数据库删除。
        /// </summary>
        /// <param name="t">待删除的对象。</param>
        /// <param name="cancellationToken">取消令牌。</param>
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
            var deleteCommand = DAOContext.PreparedCommands.GetOrAdd((ObjectType, "Delete"), _ => MakeDeleteCommand());
            int i = 0;
            foreach (IDataParameter param in deleteCommand.Parameters)
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
        public async virtual Task<int> DeleteAsync(Expr expr, CancellationToken cancellationToken = default)
        {
            using var command = MakeConditionCommand($"delete from {ParamTable} {ParamWhere}", expr);
            return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }


        // non-generic async wrappers
        async Task<bool> IObjectDAOAsync.InsertAsync(object o, CancellationToken cancellationToken)
        {
            return await InsertAsync((T)o, cancellationToken).ConfigureAwait(false);
        }

        async Task IObjectDAOAsync.BatchInsertAsync(IEnumerable values, CancellationToken cancellationToken)
        {
            if (values is IEnumerable<T>)
                await BatchInsertAsync(values as IEnumerable<T>, cancellationToken).ConfigureAwait(false);
            else
            {
                List<T> list = new List<T>();
                foreach (T entity in values)
                {
                    list.Add(entity);
                }
                await BatchInsertAsync(list, cancellationToken).ConfigureAwait(false);
            }
        }

        async Task<bool> IObjectDAOAsync.UpdateAsync(object o, CancellationToken cancellationToken)
        {
            return await UpdateAsync((T)o, null, cancellationToken).ConfigureAwait(false);
        }


        /// <summary>
        /// 异步根据条件更新数据
        /// </summary>
        /// <param name="values">需要更新的属性及数值，key为属性名，value为数值</param>
        /// <param name="expr">更新的条件</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>表示异步操作的任务，任务结果包含更新的记录数</returns>
        public async Task<int> UpdateAllValuesAsync(IEnumerable<KeyValuePair<string, object>> values, Expr expr, CancellationToken cancellationToken)
        {
            List<string> strSets = new List<string>();
            List<KeyValuePair<string, object>> paramValues = new List<KeyValuePair<string, object>>();
            foreach (KeyValuePair<string, object> value in values)
            {
                SqlColumn column = Table.GetColumn(value.Key);
                if (column is null) throw new Exception($"Property \"{value.Key}\" does not exist in type \"{Table.DefinitionType.FullName}\".");
                strSets.Add(SqlBuilder.ToSqlName(column.Name) + "=" + ToSqlParam(paramValues.Count.ToString()));
                paramValues.Add(paramValues.Count.ToString(), value.Value);
            }
            string updateSql = $"update {ParamTable} set {String.Join(",", strSets.ToArray())} {ToWhereSql(expr.ToSql(SqlBuildContext, SqlBuilder, paramValues))}";

            using var command = MakeNamedParamCommand(updateSql, paramValues);
            return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// 异步根据主键更新数据
        /// </summary>
        /// <param name="values">需要更新的属性及数值，key为属性名，value为数值</param>
        /// <param name="keys">主键</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>表示异步操作的任务，任务结果包含更新是否成功</returns>
        public async Task<bool> UpdateValuesAsync(IEnumerable<KeyValuePair<string, object>> values, object[] keys, CancellationToken cancellationToken)
        {
            ThrowExceptionIfNoKeys();
            ThrowExceptionIfWrongKeys(keys);
            ExprSet expr = new ExprSet(ExprJoinType.And);
            int i = 0;
            foreach (ColumnDefinition column in TableDefinition.Keys)
            {
                expr.Add(Expr.Property(column.PropertyName, keys[i++]));
            }
            return await UpdateAllValuesAsync(values, expr, cancellationToken).ConfigureAwait(false) > 0;
        }


        async Task<bool> IObjectDAOAsync.DeleteAsync(object o, CancellationToken cancellationToken)
        {
            return await DeleteAsync((T)o, cancellationToken).ConfigureAwait(false);
        }

        async Task<bool> IObjectDAOAsync.DeleteByKeysAsync(object[] keys, CancellationToken cancellationToken)
        {
            return await DeleteByKeysAsync(keys, cancellationToken).ConfigureAwait(false);
        }

        async Task<int> IObjectDAOAsync.DeleteAsync(Expr expr, CancellationToken cancellationToken)
        {
            return await DeleteAsync(expr, cancellationToken).ConfigureAwait(false);
        }


        #endregion
    }
}
