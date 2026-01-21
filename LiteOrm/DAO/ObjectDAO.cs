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

        /// <summary>
        /// 获取或设置用于生成 SQL 的上下文。
        /// </summary>
        protected override SqlBuildContext SqlBuildContext { get { base.SqlBuildContext.SingleTable = true; return base.SqlBuildContext; } set => base.SqlBuildContext = value; }

        #region 预构建Command
        /// <summary>
        /// 实体插入命令
        /// </summary>
        protected virtual DbCommandProxy MakeInsertCommand()
        {
            DbCommandProxy command = NewCommand();
            StringBuilder strColumns = new StringBuilder();
            StringBuilder strValues = new StringBuilder();
            foreach (ColumnDefinition column in TableDefinition.Columns)
            {
                if (!column.IsIdentity && column.Mode.CanInsert())
                {
                    if (strColumns.Length != 0) strColumns.Append(",");
                    if (strValues.Length != 0) strValues.Append(",");

                    strColumns.Append(ToSqlName(column.Name));
                    strValues.Append(ToSqlParam(column.PropertyName));
                    IDbDataParameter param = command.CreateParameter();
                    param.Size = column.Length;
                    param.DbType = column.DbType;
                    param.ParameterName = ToParamName(column.PropertyName);
                    command.Parameters.Add(param);
                }
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
        protected virtual DbCommandProxy MakeUpdateCommand()
        {
            DbCommandProxy command = NewCommand();
            StringBuilder strColumns = new StringBuilder();
            foreach (ColumnDefinition column in TableDefinition.Columns)
            {
                if (column.Mode.CanUpdate() && !column.IsPrimaryKey)
                {
                    if (strColumns.Length != 0) strColumns.Append(",");
                    strColumns.AppendFormat("{0} = {1}", ToSqlName(column.Name), ToSqlParam(column.PropertyName));
                    IDbDataParameter param = command.CreateParameter();
                    param.Size = column.Length;
                    param.DbType = column.DbType;
                    param.ParameterName = ToParamName(column.PropertyName);
                    command.Parameters.Add(param);
                }
            }

            string strTimestamp = MakeTimestampCondition(command);
            if (strTimestamp is not null) strTimestamp = " and " + strTimestamp;
            command.CommandText = $"update {ToSqlName(FactTableName)} set {strColumns} \nwhere{MakeIsKeyCondition(command) + strTimestamp} ";
            return command;
        }

        /// <summary>
        /// 构建实体删除命令。
        /// </summary>
        /// <returns>返回删除命令实例。</returns>
        protected virtual DbCommandProxy MakeDeleteCommand()
        {
            DbCommandProxy command = NewCommand();
            command.CommandText = $"delete from {ToSqlName(FactTableName)} \nwhere{MakeIsKeyCondition(command)}";
            return command;
        }

        /// <summary>
        /// 构建更新或插入（Upsert）命令。
        /// </summary>
        /// <returns>返回更新或插入命令实例。</returns>
        protected virtual DbCommandProxy MakeUpdateOrInsertCommand()
        {
            DbCommandProxy command = NewCommand();
            StringBuilder strColumns = new StringBuilder();
            StringBuilder strValues = new StringBuilder();
            StringBuilder strUpdateColumns = new StringBuilder();
            foreach (ColumnDefinition column in TableDefinition.Columns)
            {
                bool columnAdded = false;
                if (!column.IsIdentity && column.Mode.CanInsert())
                {
                    if (strColumns.Length != 0) strColumns.Append(",");
                    if (strValues.Length != 0) strValues.Append(",");

                    strColumns.Append(ToSqlName(column.Name));
                    strValues.Append(ToSqlParam(column.PropertyName));
                    columnAdded = true;
                }

                if (column.Mode.CanUpdate() && !column.IsPrimaryKey)
                {
                    if (strUpdateColumns.Length != 0) strUpdateColumns.Append(",");
                    strUpdateColumns.AppendFormat("{0} = {1}", ToSqlName(column.Name), ToSqlParam(column.PropertyName));
                    columnAdded = true;
                }

                if (columnAdded)
                {
                    IDbDataParameter param = command.CreateParameter();
                    param.DbType = column.DbType;
                    param.Size = column.Length;
                    param.ParameterName = ToParamName(column.PropertyName);
                    command.Parameters.Add(param);
                }
            }
            string insertCommandText = IdentityColumn is null ? $"insert into {ToSqlName(FactTableName)} ({strColumns}) \nvalues ({strValues})"
                : SqlBuilder.BuildIdentityInsertSql(command, IdentityColumn, FactTableName, strColumns.ToString(), strValues.ToString());
            string updateCommandText = $"update {ToSqlName(FactTableName)} set {strUpdateColumns} \nwhere{MakeIsKeyCondition(command)};";

            command.CommandText = $"BEGIN if exists(select 1 from {ToSqlName(FactTableName)} \nwhere{MakeIsKeyCondition(command)}) begin {updateCommandText} select -1; end else begin {insertCommandText} end END;";
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
            using var insertCommand = MakeInsertCommand();
            foreach (IDataParameter param in insertCommand.Parameters)
            {
                ColumnDefinition column = TableDefinition.GetColumn(ToNativeName(param.ParameterName));
                param.Value = ConvertToDbValue(column.GetValue(t), column.DbType);
            }
            if (IdentityColumn is null)
            {
                insertCommand.ExecuteNonQuery();
            }
            else
            {
                IDataParameter param = insertCommand.Parameters.Contains(ToParamName(IdentityColumn.PropertyName)) ? (IDataParameter)insertCommand.Parameters[ToParamName(IdentityColumn.PropertyName)] : null;
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
            var insertableColumns = TableDefinition.Columns.Where(column => !column.IsIdentity && column.Mode.CanInsert());
            if (provider is not null)
            {
                using (var scope = DAOContext.AcquireScope())
                {
                    provider.BulkInsert(ToDataTable(values, insertableColumns), Connection, DAOContext.CurrentTransaction);
                }
            }
            else if (SqlBuilder.SupportBatchInsert)
            {
                int batchSize = (100 / insertableColumns.Count() + 1) * 100;
                var batch = new List<T>(batchSize);
                foreach (var item in values)
                {
                    batch.Add(item);
                    if (batch.Count >= batchSize)
                    {
                        BatchInsertInternal(batch);
                        batch.Clear();
                    }
                }
                if (batch.Count > 0)
                {
                    BatchInsertInternal(batch);
                }
            }
            else
                foreach (T t in values) Insert(t);
        }

        /// <summary>
        /// Creates a DataTable containing rows and columns based on the specified values and column definitions.
        /// </summary>
        /// <remarks>The resulting DataTable uses the FactTableName as its table name. All property values
        /// are converted to database-compatible types; null values are represented as DBNull.Value. The order of
        /// columns in the DataTable matches the order of the columns parameter.</remarks>
        /// <param name="values">The collection of objects to be converted into rows of the DataTable. Each object represents a single row.</param>
        /// <param name="columns">The collection of column definitions specifying the column names, types, and value extraction logic for the
        /// DataTable.</param>
        /// <returns>A DataTable populated with the provided values and columns. Each row corresponds to an item in the values
        /// collection, and each column is defined by the columns parameter.</returns>
        protected DataTable ToDataTable(IEnumerable<T> values, IEnumerable<ColumnDefinition> columns)
        {
            DataTable dt = new DataTable(FactTableName);
            foreach (ColumnDefinition column in columns)
            {
                dt.Columns.Add(new DataColumn(column.Name, column.PropertyType.GetUnderlyingType()));
            }
            dt.BeginInit();
            foreach (T t in values)
            {
                DataRow dr = dt.NewRow();
                foreach (ColumnDefinition column in columns)
                {
                    dr[column.Name] = ConvertToDbValue(column.GetValue(t), column.DbType) ?? DBNull.Value;
                }
                dt.Rows.Add(dr);
            }
            dt.EndInit();
            return dt;
        }

        /// <summary>
        /// 一次性批量插入实体集合
        /// </summary>
        /// <param name="values">要插入的实体集合</param>
        /// <remarks>一次性批量插入不支持返回自增列</remarks>
        protected virtual void BatchInsertInternal(IEnumerable<T> values)
        {
            using var command = NewCommand();
            StringBuilder strColumns = new StringBuilder();
            List<string> valuesList = new List<string>();
            var insertColumns = TableDefinition.Columns.Where(col => !col.IsIdentity && col.Mode.CanInsert());
            foreach (ColumnDefinition column in insertColumns)
            {
                if (strColumns.Length != 0) strColumns.Append(",");
                strColumns.Append(ToSqlName(column.Name));
            }
            int paramIndex = 0;
            foreach (var item in values)
            {
                StringBuilder strValuesRepeat = new StringBuilder();
                foreach (ColumnDefinition column in insertColumns)
                {
                    if (strValuesRepeat.Length != 0) strValuesRepeat.Append(",");
                    strValuesRepeat.Append(ToSqlParam(paramIndex.ToString()));
                    IDbDataParameter param = command.CreateParameter();
                    param.Size = column.Length;
                    param.DbType = column.DbType;
                    param.Value = ConvertToDbValue(column.GetValue(item), column.DbType);
                    param.ParameterName = ToParamName(paramIndex.ToString());
                    command.Parameters.Add(param);
                    paramIndex++;
                }
                valuesList.Add($"({strValuesRepeat})");
            }

            command.CommandText = SqlBuilder.BuildBatchInsertSql(FactTableName, strColumns.ToString(), valuesList);
            command.ExecuteNonQuery();
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
            if (t is null) throw new ArgumentNullException("t");
            using var updateCommand = MakeUpdateCommand();
            foreach (IDataParameter param in updateCommand.Parameters)
            {
                if (ToNativeName(param.ParameterName) == TimestampParamName)
                {
                    param.Value = timestamp;
                }
                else
                {
                    ColumnDefinition column = TableDefinition.GetColumn(ToNativeName(param.ParameterName));
                    param.Value = ConvertToDbValue(column.GetValue(t), column.DbType);
                }
            }
            return updateCommand.ExecuteNonQuery() > 0;
        }

        /// <summary>
        /// 更新或插入实体对象到数据库中。
        /// </summary>
        /// <param name="t">要更新或插入的实体对象。</param>
        /// <returns>操作结果，指示是插入还是更新。</returns>
        /// <exception cref="ArgumentNullException">当 <paramref name="t"/> 为 null 时抛出。</exception>
        public virtual UpdateOrInsertResult UpdateOrInsert(T t)
        {
            if (t is null) throw new ArgumentNullException("t");
            using var updateOrInsertCommand = MakeUpdateOrInsertCommand();
            foreach (IDataParameter param in updateOrInsertCommand.Parameters)
            {
                ColumnDefinition column = TableDefinition.GetColumn(ToNativeName(param.ParameterName));
                param.Value = ConvertToDbValue(column.GetValue(t), column.DbType);
            }
            int ret = Convert.ToInt32(updateOrInsertCommand.ExecuteScalar());
            if (ret >= 0)
            {
                if (IdentityColumn is not null) IdentityColumn.SetValue(t, ret);
                return UpdateOrInsertResult.Inserted;
            }
            else
            {
                return UpdateOrInsertResult.Updated;
            }
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
                strSets.Add(column.FormattedName(SqlBuilder) + "=" + ToSqlParam(paramValues.Count.ToString()));
                paramValues.Add(paramValues.Count.ToString(), value.Value);
            }
            string where = expr.ToSql(SqlBuildContext, SqlBuilder, paramValues);
            if(!string.IsNullOrWhiteSpace(where))where = $"where {where}";
            string updateSql = $"update @Table@ set {String.Join(",", strSets.ToArray())} \n{where}";
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
            foreach (ColumnDefinition column in TableDefinition.Keys)
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
            using var command = MakeConditionCommand("delete from @Table@ @Where@", expr);
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
            using var deleteCommand = MakeDeleteCommand();
            int i = 0;
            foreach (IDataParameter param in deleteCommand.Parameters)
            {
                param.Value = ConvertToDbValue(keys[i], TableDefinition.Keys[i].DbType);
                i++;
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

        UpdateOrInsertResult IObjectDAO.UpdateOrInsert(object o)
        {
            return UpdateOrInsert((T)o);
        }

        bool IObjectDAO.Delete(object o)
        {
            return Delete((T)o);
        }
        #endregion

        #region IObjectDAOAsync implementations (wrappers)

        /// <summary>
        /// 异步将实体对象插入到数据库中。
        /// </summary>
        /// <param name="t">要插入的实体对象。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>表示异步操作的任务，如果插入成功则返回 true。</returns>
        public async virtual Task<bool> InsertAsync(T t, CancellationToken cancellationToken = default)
        {
            if (t is null) throw new ArgumentNullException("t");
            using var insertCommand = MakeInsertCommand();
            foreach (IDataParameter param in insertCommand.Parameters)
            {
                ColumnDefinition column = TableDefinition.GetColumn(ToNativeName(param.ParameterName));
                param.Value = ConvertToDbValue(column.GetValue(t), column.DbType);
            }
            if (IdentityColumn is null)
            {
                await insertCommand.ExecuteNonQueryAsync(cancellationToken);
            }
            else
            {
                IDataParameter param = insertCommand.Parameters.Contains(ToParamName(IdentityColumn.PropertyName)) ? (IDataParameter)insertCommand.Parameters[ToParamName(IdentityColumn.PropertyName)] : null;
                if (param is not null && param.Direction == ParameterDirection.Output)
                {
                    await insertCommand.ExecuteNonQueryAsync(cancellationToken);
                    IdentityColumn.SetValue(t, ConvertFromDbValue(param.Value, IdentityColumn.PropertyType));
                }
                else
                {
                    IdentityColumn.SetValue(t, ConvertFromDbValue(await insertCommand.ExecuteScalarAsync(cancellationToken), IdentityColumn.PropertyType));
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
            var insertableColumns = TableDefinition.Columns.Where(column => !column.IsIdentity && column.Mode.CanInsert());
            if (provider is not null)
            {
                using (var scope = await DAOContext.AcquireScopeAsync(cancellationToken))
                {
                    await Task.Run(() => provider.BulkInsert(ToDataTable(values, insertableColumns), Connection, DAOContext.CurrentTransaction), cancellationToken);
                }
            }
            else if (SqlBuilder.SupportBatchInsert)
            {
                int batchSize = (100 / insertableColumns.Count() + 1) * 100;
                var batch = new List<T>(batchSize);
                foreach (var item in values)
                {
                    batch.Add(item);
                    if (batch.Count >= batchSize)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        await BatchInsertInternalAsync(batch, cancellationToken);
                        batch.Clear();
                    }
                }
                if (batch.Count > 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await BatchInsertInternalAsync(batch, cancellationToken);
                }
            }
            else
                foreach (T t in values)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await InsertAsync(t, cancellationToken);
                }
        }

        /// <summary>
        /// 异步一次性批量插入实体集合
        /// </summary>
        /// <param name="values">要插入的实体集合</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <remarks>一次性批量插入不支持返回自增列</remarks>
        protected virtual async Task BatchInsertInternalAsync(IEnumerable<T> values, CancellationToken cancellationToken)
        {
            using DbCommandProxy command = NewCommand();
            StringBuilder strColumns = new StringBuilder();
            List<string> valuesList = new List<string>();
            var insertColumns = TableDefinition.Columns.Where(col => !col.IsIdentity && col.Mode.CanInsert());
            foreach (ColumnDefinition column in insertColumns)
            {
                if (strColumns.Length != 0) strColumns.Append(",");
                strColumns.Append(ToSqlName(column.Name));
            }
            int paramIndex = 0;
            foreach (var item in values)
            {
                StringBuilder strValuesRepeat = new StringBuilder();
                foreach (ColumnDefinition column in insertColumns)
                {
                    if (strValuesRepeat.Length != 0) strValuesRepeat.Append(",");
                    strValuesRepeat.Append(ToSqlParam(paramIndex.ToString()));
                    IDbDataParameter param = command.CreateParameter();
                    param.Size = column.Length;
                    param.DbType = column.DbType;
                    param.Value = ConvertToDbValue(column.GetValue(item), column.DbType);
                    param.ParameterName = ToParamName(paramIndex.ToString());
                    command.Parameters.Add(param);
                    paramIndex++;
                }
                valuesList.Add($"({strValuesRepeat})");
            }

            command.CommandText = SqlBuilder.BuildBatchInsertSql(FactTableName, strColumns.ToString(), valuesList);
            await command.ExecuteNonQueryAsync(cancellationToken);
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
            if (t is null) throw new ArgumentNullException("t");
            using var updateCommand = MakeUpdateCommand();
            foreach (IDataParameter param in updateCommand.Parameters)
            {
                if (ToNativeName(param.ParameterName) == TimestampParamName)
                {
                    param.Value = timestamp;
                }
                else
                {
                    ColumnDefinition column = TableDefinition.GetColumn(ToNativeName(param.ParameterName));
                    param.Value = ConvertToDbValue(column.GetValue(t), column.DbType);
                }
            }
            return await updateCommand.ExecuteNonQueryAsync(cancellationToken) > 0;
        }

        /// <summary>
        /// 异步更新或插入实体对象到数据库中。
        /// </summary>
        /// <param name="t">要更新或插入的实体对象。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>表示异步操作的任务，返回操作结果，指示是插入还是更新。</returns>
        public async virtual Task<UpdateOrInsertResult> UpdateOrInsertAsync(T t, CancellationToken cancellationToken = default)
        {
            if (t is null) throw new ArgumentNullException("t");
            using var updateOrInsertCommand = MakeUpdateOrInsertCommand();
            foreach (IDataParameter param in updateOrInsertCommand.Parameters)
            {
                ColumnDefinition column = TableDefinition.GetColumn(ToNativeName(param.ParameterName));
                param.Value = ConvertToDbValue(column.GetValue(t), column.DbType);
            }
            int ret = Convert.ToInt32(await updateOrInsertCommand.ExecuteScalarAsync(cancellationToken));
            if (ret >= 0)
            {
                if (IdentityColumn is not null) IdentityColumn.SetValue(t, ret);
                return UpdateOrInsertResult.Inserted;
            }
            else
            {
                return UpdateOrInsertResult.Updated;
            }
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
            return await DeleteByKeysAsync(GetKeyValues(t), cancellationToken);
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
            using var deleteCommand = MakeDeleteCommand();
            int i = 0;
            foreach (IDataParameter param in deleteCommand.Parameters)
            {
                param.Value = ConvertToDbValue(keys[i], TableDefinition.Keys[i].DbType);
                i++;
            }
            return await deleteCommand.ExecuteNonQueryAsync(cancellationToken) > 0;
        }

        /// <summary>
        /// 异步根据条件删除对象。
        /// </summary>
        /// <param name="expr">条件。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>表示异步操作的任务，返回删除对象数量。</returns>
        public async virtual Task<int> DeleteAsync(Expr expr, CancellationToken cancellationToken = default)
        {
            using var command = MakeConditionCommand("delete from @Table@ @Where@", expr);
            return await command.ExecuteNonQueryAsync(cancellationToken);
        }

        // non-generic async wrappers
        async Task<bool> IObjectDAOAsync.InsertAsync(object o, CancellationToken cancellationToken)
        {
            return await InsertAsync((T)o, cancellationToken);
        }

        async Task IObjectDAOAsync.BatchInsertAsync(IEnumerable values, CancellationToken cancellationToken)
        {
            if (values is IEnumerable<T>)
                await BatchInsertAsync(values as IEnumerable<T>, cancellationToken);
            else
            {
                List<T> list = new List<T>();
                foreach (T entity in values)
                {
                    list.Add(entity);
                }
                await BatchInsertAsync(list, cancellationToken);
            }
        }

        async Task<bool> IObjectDAOAsync.UpdateAsync(object o, CancellationToken cancellationToken)
        {
            return await UpdateAsync((T)o, null, cancellationToken);
        }

        async Task<UpdateOrInsertResult> IObjectDAOAsync.UpdateOrInsertAsync(object o, CancellationToken cancellationToken)
        {
            return await UpdateOrInsertAsync((T)o, cancellationToken);
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
                strSets.Add(column.FormattedName(SqlBuilder) + "=" + ToSqlParam(paramValues.Count.ToString()));
                paramValues.Add(paramValues.Count.ToString(), value.Value);
            }
            string updateSql = "update @Table@ set " + String.Join(",", strSets.ToArray()) + " \nwhere" + expr.ToSql(SqlBuildContext, SqlBuilder, paramValues);
            using var command = MakeNamedParamCommand(updateSql, paramValues);
            return await command.ExecuteNonQueryAsync(cancellationToken);
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
            return await UpdateAllValuesAsync(values, expr, cancellationToken) > 0;
        }

        async Task<bool> IObjectDAOAsync.DeleteAsync(object o, CancellationToken cancellationToken)
        {
            return await DeleteAsync((T)o, cancellationToken);
        }

        async Task<bool> IObjectDAOAsync.DeleteByKeysAsync(object[] keys, CancellationToken cancellationToken)
        {
            return await DeleteByKeysAsync(keys, cancellationToken);
        }

        async Task<int> IObjectDAOAsync.DeleteAsync(Expr expr, CancellationToken cancellationToken)
        {
            return await DeleteAsync(expr, cancellationToken);
        }

        #endregion
    }
}
