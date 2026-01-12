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
    /// 实体类增删改等实现
    /// </summary>
    /// <typeparam name="T">实体类型</typeparam>
    public class ObjectDAO<T> : ObjectDAOBase, IObjectDAO<T>
    {
        /// <summary>
        /// 实体对象类型
        /// </summary>
        public override Type ObjectType
        {
            get { return typeof(T); }
        }
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

            command.CommandText = IdentityColumn == null ?
                String.Format("insert into {0} ({1}) \nvalues ({2})", ToSqlName(FactTableName), strColumns, strValues)
                : SqlBuilder.BuildIdentityInsertSQL(command, IdentityColumn, FactTableName, strColumns.ToString(), strValues.ToString());
            return command;
        }

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
            if (strTimestamp != null) strTimestamp = " and " + strTimestamp;
            command.CommandText = String.Format("update {0} set {1} \nwhere{2} ", ToSqlName(FactTableName), strColumns, MakeIsKeyCondition(command) + strTimestamp);
            return command;
        }

        protected virtual IDbCommand MakeDeleteCommand()
        {
            IDbCommand command = NewCommand();
            command.CommandText = String.Format("delete from {0} \nwhere{1}", ToSqlName(FactTableName), MakeIsKeyCondition(command));
            return command;
        }

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
            string insertCommandText = IdentityColumn == null ? String.Format("insert into {0} ({1}) \nvalues ({2})", ToSqlName(FactTableName), strColumns, strValues)
                : SqlBuilder.BuildIdentityInsertSQL(command, IdentityColumn, ToSqlName(FactTableName), strColumns.ToString(), strValues.ToString());
            string updateCommandText = String.Format("update {0} set {1} \nwhere{2};", ToSqlName(FactTableName), strUpdateColumns, MakeIsKeyCondition(command));

            command.CommandText = String.Format("BEGIN if exists(select 1 from {0} \nwhere{1}) begin {2} select -1; end else begin {3} end END;", ToSqlName(FactTableName), MakeIsKeyCondition(command), updateCommandText, insertCommandText);
            return command;
        }

        #endregion

        #region CRUD
        public virtual bool Insert(T t)
        {
            var insertCommand = MakeInsertCommand();
            if (t == null) throw new ArgumentNullException("t");
            foreach (IDataParameter param in insertCommand.Parameters)
            {
                ColumnDefinition column = TableDefinition.GetColumn(ToNativeName(param.ParameterName));
                param.Value = ConvertToDBValue(column.GetValue(t), column);
            }
            if (IdentityColumn == null)
            {
                insertCommand.ExecuteNonQuery();
            }
            else
            {
                IDataParameter param = insertCommand.Parameters.Contains(ToParamName(IdentityColumn.PropertyName)) ? (IDataParameter)insertCommand.Parameters[ToParamName(IdentityColumn.PropertyName)] : null;
                if (param != null && param.Direction == ParameterDirection.Output)
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

        public virtual void BatchInsert(IEnumerable<T> values)
        {
            var provider = BulkInsertProviderFactory.GetProvider(TableDefinition.DataProviderType);
            if (provider != null)
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

        public virtual bool Update(T t, object timestamp = null)
        {
            if (t == null) throw new ArgumentNullException("t");
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

        public virtual UpdateOrInsertResult UpdateOrInsert(T t)
        {
            if (t == null) throw new ArgumentNullException("t");
            var updateOrInsertCommand = MakeUpdateOrInsertCommand();
            foreach (IDataParameter param in updateOrInsertCommand.Parameters)
            {
                ColumnDefinition column = TableDefinition.GetColumn(ToNativeName(param.ParameterName));
                param.Value = ConvertToDBValue(column.GetValue(t), column);
            }
            int ret = Convert.ToInt32(updateOrInsertCommand.ExecuteScalar());
            if (ret >= 0)
            {
                if (IdentityColumn != null) IdentityColumn.SetValue(t, ret);
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
        /// <param name="condition">更新的条件</param>
        /// <returns>更新的记录数</returns>
        public virtual int UpdateAllValues(IEnumerable<KeyValuePair<string, object>> values, Statement condition)
        {
            List<string> strSets = new List<string>();
            List<KeyValuePair<string, object>> paramValues = new List<KeyValuePair<string, object>>();
            foreach (KeyValuePair<string, object> value in values)
            {
                SqlColumn column = Table.GetColumn(value.Key);
                if (column == null) throw new Exception(String.Format("Property \"{0}\" does not exist in type \"{1}\".", value.Key, Table.DefinitionType.FullName));
                strSets.Add(column.FormattedName(SqlBuilder) + "=" + ToSqlParam(paramValues.Count.ToString()));
                paramValues.Add(paramValues.Count.ToString(), value.Value);
            }
            string updateSql = "update @Table@ set " + String.Join(",", strSets.ToArray()) + " \nwhere" + condition.ToSql(SqlBuildContext, SqlBuilder, paramValues);
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
            StatementSet condition = new StatementSet(StatementJoinType.And);
            int i = 0;
            foreach (ColumnDefinition column in TableDefinition.Keys)
            {
                condition.Add(Statement.Property(column.PropertyName, keys[i++]));
            }
            return UpdateAllValues(values, condition) > 0;
        }

        /// <summary>
        /// 将对象从数据库删除
        /// </summary>
        /// <param name="t">待删除的对象</param>
        /// <returns>是否成功删除</returns>
        public virtual bool Delete(T t)
        {
            if (t == null) throw new ArgumentNullException("t");
            return DeleteByKeys(GetKeyValues(t));
        }

        /// <summary>
        /// 根据条件删除对象
        /// </summary>
        /// <param name="condition">条件</param>
        /// <returns>删除对象数量</returns>
        public virtual int Delete(Statement condition)
        {
            using (IDbCommand command = MakeConditionCommand("delete from @Table@ \nwhere@Condition@", condition))
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

        public virtual Task<bool> InsertAsync(T t, CancellationToken cancellationToken = default)
        {
            return CurrentSession.ExecuteInSessionAsync(() => Insert(t), cancellationToken);
        }

        public virtual Task BatchInsertAsync(IEnumerable<T> values, CancellationToken cancellationToken = default)
        {
            return CurrentSession.ExecuteInSessionAsync(() => BatchInsert(values), cancellationToken);
        }

        public virtual Task<bool> UpdateAsync(T t, object timestamp = null, CancellationToken cancellationToken = default)
        {
            return CurrentSession.ExecuteInSessionAsync(() => Update(t, timestamp), cancellationToken);
        }

        public virtual Task<UpdateOrInsertResult> UpdateOrInsertAsync(T t, CancellationToken cancellationToken = default)
        {
            return CurrentSession.ExecuteInSessionAsync(() => UpdateOrInsert(t), cancellationToken);
        }


        public virtual Task<bool> DeleteAsync(T t, CancellationToken cancellationToken = default)
        {
            return CurrentSession.ExecuteInSessionAsync(() => Delete(t), cancellationToken);
        }

        public virtual Task<bool> DeleteByKeysAsync(object[] keys, CancellationToken cancellationToken = default)
        {
            return CurrentSession.ExecuteInSessionAsync(() => DeleteByKeys(keys), cancellationToken);
        }

        public virtual Task<int> DeleteAsync(Statement condition, CancellationToken cancellationToken = default)
        {
            return CurrentSession.ExecuteInSessionAsync(() => Delete(condition), cancellationToken);
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

        Task<int> IObjectDAOAsync.UpdateAllValuesAsync(IEnumerable<KeyValuePair<string, object>> values, Statement condition, CancellationToken cancellationToken)
        {
            return CurrentSession.ExecuteInSessionAsync(() => UpdateAllValues(values, condition), cancellationToken);
        }

        Task<bool> IObjectDAOAsync.DeleteAsync(object o, CancellationToken cancellationToken)
        {
            return DeleteAsync((T)o, cancellationToken);
        }

        Task<bool> IObjectDAOAsync.DeleteByKeysAsync(object[] keys, CancellationToken cancellationToken)
        {
            return DeleteByKeysAsync(keys, cancellationToken);
        }

        Task<int> IObjectDAOAsync.DeleteAsync(Statement condition, CancellationToken cancellationToken)
        {
            return DeleteAsync(condition, cancellationToken);
        }

        #endregion
    }
}
