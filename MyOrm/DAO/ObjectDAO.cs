using System;
using System.Collections.Generic;
using System.Text;
using System.Data;
using MyOrm.Common;
using System.Linq.Expressions;
using Microsoft.Extensions.DependencyInjection;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;

namespace MyOrm
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
        protected ColumnDefinition IdentityColumn
        {
            get
            {
                foreach (ColumnDefinition column in TableDefinition.Columns) if (column.IsIdentity) return column;
                return null;
            }
        }

        /// <summary>
        /// 获取或设置用于生成 SQL 的上下文。
        /// </summary>
        protected override SqlBuildContext SqlBuildContext { get { base.SqlBuildContext.SingleTable = true; return base.SqlBuildContext; } set => base.SqlBuildContext = value; }

        #region 预构建Command
        /// <summary>
        /// 实体插入命令
        /// </summary>
        protected virtual IDbCommand MakeInsertCommand()
        {
            IDbCommand command = NewCommand();
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
                String.Format("insert into {0} ({1}) \nvalues ({2})", ToSqlName(FactTableName), strColumns, strValues)
                : SqlBuilder.BuildIdentityInsertSQL(command, IdentityColumn, FactTableName, strColumns.ToString(), strValues.ToString());
            return command;
        }

        /// <summary>
        /// 构建实体更新命令。
        /// </summary>
        /// <returns>返回更新命令实例。</returns>
        protected virtual IDbCommand MakeUpdateCommand()
        {
            IDbCommand command = NewCommand();
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
            command.CommandText = String.Format("update {0} set {1} \nwhere{2} ", ToSqlName(FactTableName), strColumns, MakeIsKeyCondition(command) + strTimestamp);
            return command;
        }

        /// <summary>
        /// 构建实体删除命令。
        /// </summary>
        /// <returns>返回删除命令实例。</returns>
        protected virtual IDbCommand MakeDeleteCommand()
        {
            IDbCommand command = NewCommand();
            command.CommandText = String.Format("delete from {0} \nwhere{1}", ToSqlName(FactTableName), MakeIsKeyCondition(command));
            return command;
        }

        /// <summary>
        /// 构建更新或插入（Upsert）命令。
        /// </summary>
        /// <returns>返回更新或插入命令实例。</returns>
        protected virtual IDbCommand MakeUpdateOrInsertCommand()
        {
            IDbCommand command = NewCommand();
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
            string insertCommandText = IdentityColumn is null ? String.Format("insert into {0} ({1}) \nvalues ({2})", ToSqlName(FactTableName), strColumns, strValues)
                : SqlBuilder.BuildIdentityInsertSQL(command, IdentityColumn, ToSqlName(FactTableName), strColumns.ToString(), strValues.ToString());
            string updateCommandText = String.Format("update {0} set {1} \nwhere{2};", ToSqlName(FactTableName), strUpdateColumns, MakeIsKeyCondition(command));

            command.CommandText = String.Format("BEGIN if exists(select 1 from {0} \nwhere{1}) begin {2} select -1; end else begin {3} end END;", ToSqlName(FactTableName), MakeIsKeyCondition(command), updateCommandText, insertCommandText);
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
            var insertCommand = MakeInsertCommand();
            if (t is null) throw new ArgumentNullException("t");
            foreach (IDataParameter param in insertCommand.Parameters)
            {
                ColumnDefinition column = TableDefinition.GetColumn(ToNativeName(param.ParameterName));
                param.Value = ConvertToDBValue(column.GetValue(t), column);
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
                    IdentityColumn.SetValue(t, ConvertValue(param.Value, IdentityColumn.PropertyType));
                }
                else
                {
                    IdentityColumn.SetValue(t, ConvertValue(insertCommand.ExecuteScalar(), IdentityColumn.PropertyType));
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
            var provider = BulkInsertProviderFactory.GetProvider(TableDefinition.DataProviderType);
            if (provider is not null)
            {
                DataTable dt = new DataTable(FactTableName);
                List<ColumnDefinition> insertableColumns = new List<ColumnDefinition>();
                foreach (ColumnDefinition column in TableDefinition.Columns)
                {
                    if (!column.IsIdentity && column.Mode.CanInsert())
                    {
                        dt.Columns.Add(new DataColumn(column.Name, Nullable.GetUnderlyingType(column.PropertyType) ?? column.PropertyType));
                        insertableColumns.Add(column);
                    }
                }
                dt.BeginInit();
                foreach (T t in values)
                {
                    DataRow dr = dt.NewRow();
                    foreach (ColumnDefinition column in insertableColumns)
                    {
                        dr[column.Name] = ConvertToDBValue(column.GetValue(t), column) ?? DBNull.Value;
                    }
                    dt.Rows.Add(dr);
                }
                dt.EndInit();
                provider.BulkInsert(dt, DAOContext);
            }
            else
            {
                foreach (T t in values)
                {
                    Insert(t);
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
            if (t is null) throw new ArgumentNullException("t");
            var updateCommand = MakeUpdateCommand();
            foreach (IDataParameter param in updateCommand.Parameters)
            {
                if (ToNativeName(param.ParameterName) == TimestampParamName)
                {
                    param.Value = timestamp;
                }
                else
                {
                    ColumnDefinition column = TableDefinition.GetColumn(ToNativeName(param.ParameterName));
                    param.Value = ConvertToDBValue(column.GetValue(t), column);
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
            var updateOrInsertCommand = MakeUpdateOrInsertCommand();
            foreach (IDataParameter param in updateOrInsertCommand.Parameters)
            {
                ColumnDefinition column = TableDefinition.GetColumn(ToNativeName(param.ParameterName));
                param.Value = ConvertToDBValue(column.GetValue(t), column);
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
                if (column is null) throw new Exception(String.Format("Property \"{0}\" does not exist in type \"{1}\".", value.Key, Table.DefinitionType.FullName));
                strSets.Add(column.FormattedName(SqlBuilder) + "=" + ToSqlParam(paramValues.Count.ToString()));
                paramValues.Add(paramValues.Count.ToString(), value.Value);
            }
            string updateSql = "update @Table@ set " + String.Join(",", strSets.ToArray()) + " \nwhere" + expr.ToSql(SqlBuildContext, SqlBuilder, paramValues);
            using (IDbCommand command = MakeNamedParamCommand(updateSql, paramValues))
            {
                return command.ExecuteNonQuery();
            }
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
            using (IDbCommand command = MakeConditionCommand("delete from @Table@ \nwhere@Condition@", expr))
            {
                return command.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// 将指定主键的对象从数据库删除
        /// </summary>
        /// <param name="keys">待删除的对象的主键</param>
        /// <returns>是否成功删除</returns>
        public virtual bool DeleteByKeys(params object[] keys)
        {
            ThrowExceptionIfWrongKeys(keys);
            var deleteCommand = MakeDeleteCommand();
            int i = 0;
            foreach (IDataParameter param in deleteCommand.Parameters)
            {
                param.Value = ConvertToDBValue(keys[i], TableDefinition.Keys[i]);
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
        public virtual Task<bool> InsertAsync(T t, CancellationToken cancellationToken = default)
        {
            return CurrentSession.ExecuteInSessionAsync(() => Insert(t), cancellationToken);
        }

        /// <summary>
        /// 异步批量插入实体对象到数据库中。
        /// </summary>
        /// <param name="values">要插入的实体对象集合。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>表示异步操作的任务。</returns>
        public virtual Task BatchInsertAsync(IEnumerable<T> values, CancellationToken cancellationToken = default)
        {
            return CurrentSession.ExecuteInSessionAsync(() => BatchInsert(values), cancellationToken);
        }

        /// <summary>
        /// 异步更新数据库中的实体对象。
        /// </summary>
        /// <param name="t">要更新的实体对象。</param>
        /// <param name="timestamp">时间戳值，用于乐观并发控制。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>表示异步操作的任务，如果更新成功则返回 true。</returns>
        public virtual Task<bool> UpdateAsync(T t, object timestamp = null, CancellationToken cancellationToken = default)
        {
            return CurrentSession.ExecuteInSessionAsync(() => Update(t, timestamp), cancellationToken);
        }

        /// <summary>
        /// 异步更新或插入实体对象到数据库中。
        /// </summary>
        /// <param name="t">要更新或插入的实体对象。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>表示异步操作的任务，返回操作结果，指示是插入还是更新。</returns>
        public virtual Task<UpdateOrInsertResult> UpdateOrInsertAsync(T t, CancellationToken cancellationToken = default)
        {
            return CurrentSession.ExecuteInSessionAsync(() => UpdateOrInsert(t), cancellationToken);
        }


        /// <summary>
        /// 异步将对象从数据库删除。
        /// </summary>
        /// <param name="t">待删除的对象。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>表示异步操作的任务，如果删除成功则返回 true。</returns>
        public virtual Task<bool> DeleteAsync(T t, CancellationToken cancellationToken = default)
        {
            return CurrentSession.ExecuteInSessionAsync(() => Delete(t), cancellationToken);
        }

        /// <summary>
        /// 异步将指定主键的对象从数据库删除。
        /// </summary>
        /// <param name="keys">待删除的对象的主键。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>表示异步操作的任务，如果删除成功则返回 true。</returns>
        public virtual Task<bool> DeleteByKeysAsync(object[] keys, CancellationToken cancellationToken = default)
        {
            return CurrentSession.ExecuteInSessionAsync(() => DeleteByKeys(keys), cancellationToken);
        }

        /// <summary>
        /// 异步根据条件删除对象。
        /// </summary>
        /// <param name="expr">条件。</param>
        /// <param name="cancellationToken">取消令牌。</param>
        /// <returns>表示异步操作的任务，返回删除对象数量。</returns>
        public virtual Task<int> DeleteAsync(Expr expr, CancellationToken cancellationToken = default)
        {
            return CurrentSession.ExecuteInSessionAsync(() => Delete(expr), cancellationToken);
        }

        // non-generic async wrappers
        Task<bool> IObjectDAOAsync.InsertAsync(object o, CancellationToken cancellationToken)
        {
            return InsertAsync((T)o, cancellationToken);
        }

        Task IObjectDAOAsync.BatchInsertAsync(IEnumerable values, CancellationToken cancellationToken)
        {
            if (values is IEnumerable<T>)
                return BatchInsertAsync(values as IEnumerable<T>, cancellationToken);
            else
            {
                List<T> list = new List<T>();
                foreach (T entity in values)
                {
                    list.Add(entity);
                }
                return BatchInsertAsync(list, cancellationToken);
            }
        }

        Task<bool> IObjectDAOAsync.UpdateAsync(object o, CancellationToken cancellationToken)
        {
            return UpdateAsync((T)o, null, cancellationToken);
        }

        Task<UpdateOrInsertResult> IObjectDAOAsync.UpdateOrInsertAsync(object o, CancellationToken cancellationToken)
        {
            return UpdateOrInsertAsync((T)o, cancellationToken);
        }

        Task<int> IObjectDAOAsync.UpdateAllValuesAsync(IEnumerable<KeyValuePair<string, object>> values, Expr expr, CancellationToken cancellationToken)
        {
            return CurrentSession.ExecuteInSessionAsync(() => UpdateAllValues(values, expr), cancellationToken);
        }

        Task<bool> IObjectDAOAsync.DeleteAsync(object o, CancellationToken cancellationToken)
        {
            return DeleteAsync((T)o, cancellationToken);
        }

        Task<bool> IObjectDAOAsync.DeleteByKeysAsync(object[] keys, CancellationToken cancellationToken)
        {
            return DeleteByKeysAsync(keys, cancellationToken);
        }

        Task<int> IObjectDAOAsync.DeleteAsync(Expr expr, CancellationToken cancellationToken)
        {
            return DeleteAsync(expr, cancellationToken);
        }

        #endregion
    }
}
