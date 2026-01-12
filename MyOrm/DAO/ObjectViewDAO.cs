using System;
using System.Collections.Generic;
using System.Text;
using System.Data;
using System.Collections;
using MyOrm.Common;
using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Data.Common;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;

namespace MyOrm
{
    /// <summary>
    /// 实体类的查询操作
    /// </summary>
    /// <typeparam name="T">实体类型</typeparam>
    public class ObjectViewDAO<T> : ObjectDAOBase, IObjectViewDAO<T> where T : new()
    {
        #region 属性
        /// <summary>
        /// 实体对象类型
        /// </summary>
        public override Type ObjectType
        {
            get { return typeof(T); }
        }

        /// <summary>
        /// 查询关联表
        /// </summary>
        public override SqlTable Table
        {
            get { return TableInfoProvider.GetTableView(ObjectType); }
        }
        #endregion

        #region 预定义Command
        /// <summary>
        /// 实现获取对象操作的IDbCommand
        /// </summary>
        protected virtual IDbCommand MakeGetObjectCommand()
        {
            IDbCommand command = NewCommand();
            command.CommandText = $"select {AllFieldsSql} \nfrom {From} \nwhere {MakeIsKeyCondition(command)}";
            return command;
        }

        /// <summary>
        /// 实现检查对象是否存在操作的IDbCommand
        /// </summary>
        protected virtual IDbCommand MakeObjectExistsCommand()
        {
            ThrowExceptionIfNoKeys();
            IDbCommand command = NewCommand();
            StringBuilder strConditions = new StringBuilder();
            foreach (ColumnDefinition key in TableDefinition.Keys)
            {
                if (strConditions.Length != 0) strConditions.Append(" and ");
                strConditions.AppendFormat("{0} = {1}", ToSqlName(key.Name), ToSqlParam(key.PropertyName));
                if (!command.Parameters.Contains(key.PropertyName))
                {
                    IDbDataParameter param = command.CreateParameter();
                    param.Size = key.Length;
                    param.DbType = key.DbType;
                    param.ParameterName = ToParamName(key.PropertyName);
                    command.Parameters.Add(param);
                }
            }
            command.CommandText = $"select count(1) \nfrom {ToSqlName(Table.Definition.Name)} \nwhere {strConditions}";
            return command;
        }
        #endregion

        #region 方法

        /// <summary>
        /// 根据主键获取对象
        /// </summary>
        /// <param name="keys">主键，多个主键按照主键名顺序排列</param>
        /// <returns>对象，若不存在则返回null</returns>
        public virtual T GetObject(params object[] keys)
        {
            ThrowExceptionIfWrongKeys(keys);
            var getObjectCommand = MakeGetObjectCommand();
            int i = 0;
            foreach (IDataParameter param in getObjectCommand.Parameters)
            {
                param.Value = ConvertToDBValue(keys[i], Table.Definition.Keys[i]);
                i++;
            }
            using (IDataReader reader = getObjectCommand.ExecuteReader())
            {
                return ReadOne(reader);
            }
        }

        /// <summary>
        /// 获取符合条件的对象个数
        /// </summary>
        /// <param name="condition">属性名与值的列表，若为null则表示没有条件</param>
        /// <returns>符合条件的对象个数</returns>
        public virtual int Count(Statement condition)
        {
            using (IDbCommand command = MakeConditionCommand("select count(*) \nfrom @FromTable@ \nwhere @Condition@", condition))
            {
                return Convert.ToInt32(command.ExecuteScalar());
            }
        }

        /// <summary>
        /// 判断对象是否存在
        /// </summary>
        /// <param name="o">对象</param>
        /// <returns>是否存在</returns>
        public virtual bool Exists(object o)
        {
            if (o == null) return false;
            return ExistsKey(GetKeyValues(o));
        }

        /// <summary>
        /// 判断主键对应的对象是否存在
        /// </summary>
        /// <param name="keys">主键，多个主键按照名称顺序排列</param>
        /// <returns>是否存在</returns>
        public virtual bool ExistsKey(params object[] keys)
        {
            ThrowExceptionIfWrongKeys(keys);
            var objectExistsCommand = MakeObjectExistsCommand();
            int i = 0;
            foreach (IDataParameter param in objectExistsCommand.Parameters)
            {
                param.Value = ConvertToDBValue(keys[i], Table.Definition.Keys[i]);
                i++;
            }
            return Convert.ToInt32(objectExistsCommand.ExecuteScalar()) > 0;
        }

        /// <summary>
        /// 判断符合条件的对象是否存在
        /// </summary>
        /// <param name="condition">属性名与值的列表，若为null则表示没有条件</param>
        /// <returns>是否存在</returns>
        public virtual bool Exists(Statement condition)
        {
            using (IDbCommand command = MakeConditionCommand("select 1 \nfrom @FromTable@ \nwhere @Condition@", condition))
            {
                return command.ExecuteScalar() != null;
            }
        }

        public void ForEach(Statement condition, Action<T> func)
        {
            using (IDbCommand command = MakeConditionCommand("select @AllFields@ \nfrom @FromTable@" + (condition == null ? null : " \nwhere @Condition@"), condition))
            {
                using (IDataReader reader = command.ExecuteReader())
                {
                    func(ReadOne(reader));
                }
            }
        }

        /// <summary>
        /// 根据条件查询，多个条件以逻辑与连接
        /// </summary>
        /// <param name="condition">属性名与值的列表，若为null则表示没有条件</param>
        /// <returns>符合条件的对象列表</returns>
        public virtual List<T> Search(Statement condition)
        {
            using (IDbCommand command = MakeConditionCommand("select @AllFields@ \nfrom @FromTable@" + (condition == null ? null : " \nwhere @Condition@"), condition))
            {
                return GetAll(command);
            }
        }

        /// <summary>
        /// 根据条件查询，多个条件以逻辑与连接
        /// </summary>
        /// <param name="condition">属性名与值的列表，若为null则表示没有条件</param>
        /// <param name="orderBy">排列顺序，若为null则表示不指定顺序</param>
        /// <returns>符合条件的对象列表</returns>
        public virtual List<T> Search(Statement condition, params Sorting[] orderBy)
        {
            if (orderBy == null || orderBy.Length == 0) return Search(condition);
            else
                using (IDbCommand command = MakeConditionCommand("select @AllFields@ \nfrom @FromTable@" + (condition == null ? null : " \nwhere @Condition@") + " order by " + GetOrderBySQL(orderBy), condition))
                {
                    return GetAll(command);
                }
        }

        /// <summary>
        /// 获取单个符合条件的对象
        /// </summary>
        /// <param name="condition">属性名与值的列表，若为null则表示没有条件</param>
        /// <returns>第一个符合条件的对象，若不存在则返回null</returns>
        public virtual T SearchOne(Statement condition)
        {
            using (IDbCommand command = MakeConditionCommand("select @AllFields@ \nfrom @FromTable@ \nwhere @Condition@", condition))
            {
                return GetOne(command);
            }
        }


        /// <summary>
        /// 分页查询
        /// </summary>
        /// <param name="condition">查询条件</param>
        /// <param name="section">分页设定</param>
        /// <returns></returns>
        public virtual List<T> SearchSection(Statement condition, SectionSet section)
        {
            string sql = SqlBuilder.GetSelectSectionSql(AllFieldsSql, From, ParamCondition, GetOrderBySQL(section.Orders), section.StartIndex, section.SectionSize);
            using (IDbCommand command = MakeConditionCommand(sql, condition))
            {
                return GetAll(command);
            }
        }

        #endregion

        #region IObjectViewDAO Members

        object IObjectViewDAO.GetObject(params object[] keys)
        {
            return GetObject(keys);
        }

        object IObjectViewDAO.SearchOne(Statement condition)
        {
            return SearchOne(condition);
        }

        IList IObjectViewDAO.Search(Statement condition)
        {
            return Search(condition);
        }

        IList IObjectViewDAO.Search(Statement condition, params Sorting[] orderBy)
        {
            return Search(condition, orderBy);
        }

        IList IObjectViewDAO.SearchSection(Statement condition, SectionSet section)
        {
            return SearchSection(condition, section);
        }

        #endregion

        #region IObjectViewDAOAsync implementations

        public virtual Task<T> GetObjectAsync(object[] keys, CancellationToken cancellationToken = default)
        {
            return CurrentSession.ExecuteInSessionAsync(() => GetObject(keys), cancellationToken);
        }

        Task<object> IObjectViewDAOAsync.GetObjectAsync(object[] keys, CancellationToken cancellationToken = default)
        {
            return CurrentSession.ExecuteInSessionAsync(() => (object)GetObject(keys), cancellationToken);
        }

        public virtual Task<int> CountAsync(Statement condition, CancellationToken cancellationToken = default)
        {
            return CurrentSession.ExecuteInSessionAsync(() => Count(condition), cancellationToken);
        }

        public virtual Task<bool> ExistsAsync(object o, CancellationToken cancellationToken = default)
        {
            return CurrentSession.ExecuteInSessionAsync(() => Exists(o), cancellationToken);
        }

        public virtual Task<bool> ExistsKeyAsync(object[] keys, CancellationToken cancellationToken = default)
        {
            return CurrentSession.ExecuteInSessionAsync(() => ExistsKey(keys), cancellationToken);
        }

        public virtual Task<bool> ExistsAsync(Statement condition, CancellationToken cancellationToken = default)
        {
            return CurrentSession.ExecuteInSessionAsync(() => Exists(condition), cancellationToken);
        }

        public virtual Task<bool> ExistsAsync(Expression<Func<T, bool>> expression, CancellationToken cancellationToken = default)
        {
            return CurrentSession.ExecuteInSessionAsync(() => Exists(expression), cancellationToken);
        }

        public virtual Task<T> SearchOneAsync(Statement condition, CancellationToken cancellationToken = default)
        {
            return CurrentSession.ExecuteInSessionAsync(() => SearchOne(condition), cancellationToken);
        }

        Task<object> IObjectViewDAOAsync.SearchOneAsync(Statement condition, CancellationToken cancellationToken = default)
        {
            return CurrentSession.ExecuteInSessionAsync(() => (object)SearchOne(condition), cancellationToken);
        }

        public virtual Task ForEachAsync(Statement condition, Func<T, Task> func, CancellationToken cancellationToken = default)
        {
            return CurrentSession.ExecuteInSessionAsync(() =>
            {
                var list = Search(condition);
                return list;
            }, cancellationToken).ContinueWith(async t =>
            {
                // unwrap and execute callbacks sequentially
                var list = await t;
                foreach (var item in list)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await func(item).ConfigureAwait(false);
                }
            }, cancellationToken).Unwrap();
        }

        public virtual Task<List<T>> SearchAsync(Statement condition = null, CancellationToken cancellationToken = default)
        {
            return CurrentSession.ExecuteInSessionAsync(() => Search(condition), cancellationToken);
        }

        Task<IList> IObjectViewDAOAsync.SearchAsync(Statement condition, CancellationToken cancellationToken = default)
        {
            return CurrentSession.ExecuteInSessionAsync(() => (IList)Search(condition), cancellationToken);
        }

        public virtual Task<List<T>> SearchAsync(Statement condition, Sorting[] orderBy, CancellationToken cancellationToken = default)
        {
            return CurrentSession.ExecuteInSessionAsync(() => Search(condition, orderBy), cancellationToken);
        }

        Task<IList> IObjectViewDAOAsync.SearchAsync(Statement condition, Sorting[] orderBy, CancellationToken cancellationToken = default)
        {
            return CurrentSession.ExecuteInSessionAsync(() => (IList)Search(condition, orderBy), cancellationToken);
        }

        public virtual Task<List<T>> SearchSectionAsync(Statement condition, SectionSet section, CancellationToken cancellationToken = default)
        {
            return CurrentSession.ExecuteInSessionAsync(() => SearchSection(condition, section), cancellationToken);
        }

        Task<IList> IObjectViewDAOAsync.SearchSectionAsync(Statement condition, SectionSet section, CancellationToken cancellationToken = default)
        {
            return CurrentSession.ExecuteInSessionAsync(() => (IList)SearchSection(condition, section), cancellationToken);
        }

        #endregion

        #region 常用方法

        /// <summary>
        /// 替换Sql中的标记为实际Sql
        /// </summary>
        /// <param name="SQLWithParam">包含标记的Sql语句，标记可以为ParamAllFields，ParamFromTable</param>
        /// <returns></returns>
        protected override string ReplaceParam(string SQLWithParam, SqlBuildContext context = null)
        {
            return base.ReplaceParam(SQLWithParam, context).Replace(ParamAllFields, AllFieldsSql);
        }

        /// <summary>
        /// 读取所有记录并转化为对象集合，查询AllFieldsSQL时可用
        /// </summary>
        /// <param name="reader">只读结果集</param>
        /// <returns>对象列表</returns>
        private List<T> ReadAll(IDataReader reader)
        {
            List<T> results = new List<T>();
            while (reader.Read())
            {
                results.Add(ConvertToObject(reader));
            }
            return results;
        }

        /// <summary>
        /// 读取所有记录并转化为对象集合，查询AllFieldsSQL时可用
        /// </summary>
        /// <param name="reader">只读结果集</param>
        /// <param name="count">查询结果条数</param>
        /// <returns>对象列表</returns>
        private List<T> Read(IDataReader reader, int count)
        {
            List<T> results = new List<T>();
            int i = 0;
            while (reader.Read() && i < count)
            {
                results.Add(ConvertToObject(reader));
                i++;
            }
            return results;
        }

        /// <summary>
        /// 从IDataReader中读取一条记录转化为对象，若无记录则返回null
        /// </summary>
        /// <param name="dataReader">IDataReader</param>
        /// <returns>对象，若无记录则返回null</returns>
        private T ReadOne(IDataReader dataReader)
        {
            return dataReader.Read() ? ConvertToObject(dataReader) : default(T);
        }

        /// <summary>
        /// 将一行记录转化为对象
        /// </summary>
        /// <param name="record">一行记录</param>
        /// <returns>对象</returns>
        protected virtual T ConvertToObject(IDataRecord record)
        {
            T t = new T();
            int i = 0;
            foreach (SqlColumn column in SelectColumns)
            {
                column.SetValue(t, record.IsDBNull(i) ? null : ConvertValue(record[i], column.PropertyType));
                i++;
            }
            return t;
        }

        /// <summary>
        /// 执行IDbCommand，读取所有记录并转化为对象的集合，查询AllFieldsSQL时可用
        /// </summary>
        /// <param name="command">待执行的IDbCommand</param>
        /// <returns></returns>
        protected List<T> GetAll(IDbCommand command)
        {
            using (IDataReader reader = command.ExecuteReader())
            {
                return ReadAll(reader);
            }
        }

        /// <summary>
        /// 执行IDbCommand，读取所有记录并转化为对象的集合，查询AllFieldsSQL时可用
        /// </summary>
        /// <param name="command">待执行的IDbCommand</param>
        /// <param name="count">查询结果条数</param>
        /// <returns></returns>
        protected List<T> GetAll(IDbCommand command, int count)
        {
            using (IDataReader reader = command.ExecuteReader())
            {
                return Read(reader, count);
            }
        }

        /// <summary>
        /// 执行IDbCommand，读取一条记录并转化为单个对象，查询AllFieldsSQL时可用
        /// </summary>
        /// <param name="command">待执行的IDbCommand</param>
        /// <returns></returns>
        protected T GetOne(IDbCommand command)
        {
            using (IDataReader reader = command.ExecuteReader())
            {
                return ReadOne(reader);
            }
        }
        #endregion
    }
}
