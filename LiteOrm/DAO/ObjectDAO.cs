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
    public partial class ObjectDAO<T> : ObjectDAOBase, IObjectDAO<T>
    {
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
                    DbCommandProxy command = DAOContext.PreparedCommands.GetOrAdd((ObjectType, "BatchUpdate" + batchSize), _ => MakeBatchUpdateCommand(batchSize));
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
        /// 当主键为单列时，先查询已存在记录进行筛选，再分批执行插入和更新，提高效率。
        /// </summary>
        /// <param name="values">要处理的实体对象集合。</param>
        public virtual void BatchUpdateOrInsert(IEnumerable<T> values)
        {
            if (values is null) throw new ArgumentNullException(nameof(values));

            var keyColumns = TableDefinition.Keys;
            if (keyColumns.Count == 1)
            {
                var keyColumn = keyColumns[0];
                var list = values.ToList();
                if (list.Count == 0) return;

                var existingIds = new HashSet<object>();
                int paramLimit = DAOContext.ParamCountLimit;

                for (int i = 0; i < list.Count; i += paramLimit)
                {
                    var batch = list.Skip(i).Take(paramLimit).ToList();
                    var batchKeys = batch.Select(v => keyColumn.GetValue(v)).Where(v => v != null && v != DBNull.Value).ToList();
                    if (batchKeys.Count == 0) continue;

                    var expr = Expr.In(keyColumn.PropertyName, batchKeys);
                    var querySql = $"select {ToSqlName(keyColumn.Name)} from {ParamTable} {ParamWhere}";
                    using var command = MakeConditionCommand(querySql, expr);
                    using var reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        existingIds.Add(ConvertFromDbValue(reader[0], keyColumn.PropertyType));
                    }
                }

                var toUpdate = new List<T>();
                var toInsert = new List<T>();

                foreach (var item in list)
                {
                    var keyValue = keyColumn.GetValue(item);
                    if (keyValue != null && existingIds.Contains(keyValue))
                    {
                        toUpdate.Add(item);
                    }
                    else
                    {
                        toInsert.Add(item);
                    }
                }

                if (toInsert.Count > 0) BatchInsert(toInsert);
                if (toUpdate.Count > 0) BatchUpdate(toUpdate);
            }
            else
            {
                foreach (var t in values)
                {
                    UpdateOrInsert(t);
                }
            }
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
                cancellationToken.ThrowIfCancellationRequested();
                batch.Add(t);
                if (batch.Count == batchSize)
                {
                    DbCommandProxy command = DAOContext.PreparedCommands.GetOrAdd((ObjectType, "BatchUpdate" + batchSize), _ => MakeBatchUpdateCommand(batchSize));
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
        /// 当主键为单列时，采用优化的筛选机制分批处理。
        /// </summary>
        /// <param name="values">要处理的实体集。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        public async virtual Task BatchUpdateOrInsertAsync(IEnumerable<T> values, CancellationToken cancellationToken = default)
        {
            if (values is null) throw new ArgumentNullException(nameof(values));

            var keyColumns = TableDefinition.Keys;
            if (keyColumns.Count == 1)
            {
                var keyColumn = keyColumns[0];
                var list = values.ToList();
                if (list.Count == 0) return;

                var existingIds = new HashSet<object>();
                int paramLimit = DAOContext.ParamCountLimit;

                for (int i = 0; i < list.Count; i += paramLimit)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var batch = list.Skip(i).Take(paramLimit).ToList();
                    var batchKeys = batch.Select(v => keyColumn.GetValue(v)).Where(v => v != null && v != DBNull.Value).ToList();
                    if (batchKeys.Count == 0) continue;

                    var expr = Expr.In(keyColumn.PropertyName, batchKeys);
                    var querySql = $"select {ToSqlName(keyColumn.Name)} from {ParamTable} {ParamWhere}";
                    using var command = MakeConditionCommand(querySql, expr);
                    using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                    if (reader is AutoLockDataReader autoLockReader)
                    {
                        while (await autoLockReader.ReadAsync(cancellationToken).ConfigureAwait(false))
                        {
                            existingIds.Add(ConvertFromDbValue(autoLockReader[0], keyColumn.PropertyType));
                        }
                    }
                    else
                    {
                        while (reader.Read())
                        {
                            existingIds.Add(ConvertFromDbValue(reader[0], keyColumn.PropertyType));
                        }
                    }
                }

                var toUpdate = new List<T>();
                var toInsert = new List<T>();

                foreach (var item in list)
                {
                    var keyValue = keyColumn.GetValue(item);
                    if (keyValue != null && existingIds.Contains(keyValue))
                    {
                        toUpdate.Add(item);
                    }
                    else
                    {
                        toInsert.Add(item);
                    }
                }

                if (toInsert.Count > 0) await BatchInsertAsync(toInsert, cancellationToken).ConfigureAwait(false);
                if (toUpdate.Count > 0) await BatchUpdateAsync(toUpdate, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                foreach (var t in values)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await UpdateOrInsertAsync(t, cancellationToken).ConfigureAwait(false);
                }
            }
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

        #endregion

        /// <summary>
        /// 异步根据条件更新数据
        /// 根据条件更新数据
        /// </summary>
        /// <param name="values">需要更新的属性及数值，key为属性名，value为数值</param>
        /// <param name="expr">更新的条件</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>表示异步操作的任务，任务结果包含更新的记录数</returns>
        public async Task<int> UpdateAllValuesAsync(IEnumerable<KeyValuePair<string, object>> values, Expr expr, CancellationToken cancellationToken = default)
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
        public async Task<bool> UpdateValuesAsync(IEnumerable<KeyValuePair<string, object>> values, object[] keys, CancellationToken cancellationToken = default)
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
    }
}
