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
    public class ObjectViewDAO<T> : ObjectDAOBase, IObjectViewDAO<T>, IObjectViewDAOAsync<T>, IObjectViewDAO, IObjectViewDAOAsync where T : new()
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
        protected override Table Table
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
            command.CommandText = String.Format("select {0} \nfrom {1} \nwhere {2}", AllFieldsSql, From, MakeIsKeyCondition(command));
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
            command.CommandText = String.Format("select count(1) \nfrom {0} \nwhere {1}", ToSqlName(Table.Definition.Name), strConditions);
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
        public virtual int Count(Condition condition)
        {
            using (IDbCommand command = MakeConditionCommand("select count(*) \nfrom @FromTable@ \nwhere @Condition@", condition))
            {
                return Convert.ToInt32(command.ExecuteScalar());
            }
        }

        public bool Exists(Expression<Func<T, bool>> expression)
        {
            ExpressionParser parser = new ExpressionParser(SqlBuilder, SqlBuildContext);
            parser.Visit(expression);
            string where = null;
            if (!String.IsNullOrEmpty(parser.Result)) { where = " \nwhere " + parser.Result; }
            using (IDbCommand command = MakeNamedParamCommand("select 1 \nfrom @FromTable@ " + where, parser.Arguments))
            {
                return command.ExecuteScalar() != null;
            }
        }

        public int Count(Expression<Func<T, bool>> expression)
        {
            ExpressionParser parser = new ExpressionParser(SqlBuilder, SqlBuildContext);
            parser.Visit(expression);
            string where = null;
            if (!String.IsNullOrEmpty(parser.Result)) { where = " \nwhere " + parser.Result; }
            using (IDbCommand command = MakeNamedParamCommand("select count(*) \nfrom @FromTable@ " + where, parser.Arguments))
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
        public virtual bool Exists(Condition condition)
        {
            using (IDbCommand command = MakeConditionCommand("select 1 \nfrom @FromTable@ \nwhere @Condition@", condition))
            {
                return command.ExecuteScalar() != null;
            }
        }

        public void ForEach(Condition condition, Action<T> func)
        {
            using (IDbCommand command = MakeConditionCommand("select @AllFields@ \nfrom @FromTable@" + (condition == null ? null : " \nwhere @Condition@"), condition))
            {
                using (IDataReader reader = command.ExecuteReader())
                {
                    func(ReadOne(reader));
                }
            }
        }

        public void ForEach(Expression<Func<T, bool>> expression, Action<T> func)
        {
            ForEach(Condition.Exp(expression), func);
        }

        /// <summary>
        /// 根据单个条件查询
        /// </summary>
        /// <param name="name">属性名</param>
        /// <param name="value">值</param>
        /// <returns>符合条件的对象列表</returns>
        public List<T> Search(string name, object value)
        {
            return Search(new SimpleCondition(name, value));
        }

        /// <summary>
        /// 根据条件查询，多个条件以逻辑与连接
        /// </summary>
        /// <param name="condition">属性名与值的列表，若为null则表示没有条件</param>
        /// <returns>符合条件的对象列表</returns>
        public virtual List<T> Search(Condition condition)
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
        public virtual List<T> Search(Condition condition, params Sorting[] orderBy)
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
        public virtual T SearchOne(Condition condition)
        {
            using (IDbCommand command = MakeConditionCommand("select @AllFields@ \nfrom @FromTable@ \nwhere @Condition@", condition))
            {
                return GetOne(command);
            }
        }

        /// <summary>
        /// 获取单个符合条件的对象
        /// </summary>
        /// <param name="expression">查询表达式</param>
        /// <returns>第一个符合条件的对象，若不存在则返回null</returns>
        public T SearchOne(Expression<Func<T, bool>> expression)
        {
            return SearchOne(Condition.Exp(expression));
        }

        /// <summary>
        /// 分页查询
        /// </summary>
        /// <param name="condition">查询条件</param>
        /// <param name="section">分页设定</param>
        /// <returns></returns>
        public virtual List<T> SearchSection(Condition condition, SectionSet section)
        {
            string sql = SqlBuilder.GetSelectSectionSql(AllFieldsSql, From, ParamCondition, GetOrderBySQL(section.Orders), section.StartIndex, section.SectionSize);
            using (IDbCommand command = MakeConditionCommand(sql, condition))
            {
                return GetAll(command);
            }
        }
        public List<T> Search(Expression<Func<T, bool>> expression)
        {
            return Search(Condition.Exp(expression));
        }
        public List<T> Search(Expression<Func<T, bool>> expression, params Sorting[] orderby)
        {
            return Search(Condition.Exp(expression), orderby);
        }

        public List<T> SearchSection(Expression<Func<T, bool>> expression, SectionSet section)
        {
            return SearchSection(Condition.Exp(expression), section);
        }

        #endregion

        #region IObjectViewDAO Members

        object IObjectViewDAO.GetObject(params object[] keys)
        {
            return GetObject(keys);
        }

        object IObjectViewDAO.SearchOne(Condition condition)
        {
            return SearchOne(condition);
        }

        IList IObjectViewDAO.Search(Condition condition)
        {
            return Search(condition);
        }

        IList IObjectViewDAO.Search(Condition condition, params Sorting[] orderBy)
        {
            return Search(condition, orderBy);
        }

        IList IObjectViewDAO.SearchSection(Condition condition, SectionSet section)
        {
            return SearchSection(condition, section);
        }

        #endregion

        #region IObjectViewDAOAsync implementations

        public virtual Task<T> GetObjectAsync(object[] keys, CancellationToken cancellationToken = default)
        {
            return Session.ExecuteInSessionAsync(() => GetObject(keys), cancellationToken);
        }

        Task<object> IObjectViewDAOAsync.GetObjectAsync(object[] keys, CancellationToken cancellationToken = default)
        {
            return Session.ExecuteInSessionAsync(() => (object)GetObject(keys), cancellationToken);
        }

        public virtual Task<int> CountAsync(Condition condition, CancellationToken cancellationToken = default)
        {
            return Session.ExecuteInSessionAsync(() => Count(condition), cancellationToken);
        }

        public virtual Task<int> CountAsync(Expression<Func<T, bool>> expression, CancellationToken cancellationToken = default)
        {
            return Session.ExecuteInSessionAsync(() => Count(expression), cancellationToken);
        }

        public virtual Task<bool> ExistsAsync(object o, CancellationToken cancellationToken = default)
        {
            return Session.ExecuteInSessionAsync(() => Exists(o), cancellationToken);
        }

        public virtual Task<bool> ExistsKeyAsync(object[] keys, CancellationToken cancellationToken = default)
        {
            return Session.ExecuteInSessionAsync(() => ExistsKey(keys), cancellationToken);
        }

        public virtual Task<bool> ExistsAsync(Condition condition, CancellationToken cancellationToken = default)
        {
            return Session.ExecuteInSessionAsync(() => Exists(condition), cancellationToken);
        }

        public virtual Task<bool> ExistsAsync(Expression<Func<T, bool>> expression, CancellationToken cancellationToken = default)
        {
            return Session.ExecuteInSessionAsync(() => Exists(expression), cancellationToken);
        }

        public virtual Task<T> SearchOneAsync(Condition condition, CancellationToken cancellationToken = default)
        {
            return Session.ExecuteInSessionAsync(() => SearchOne(condition), cancellationToken);
        }

        Task<object> IObjectViewDAOAsync.SearchOneAsync(Condition condition, CancellationToken cancellationToken = default)
        {
            return Session.ExecuteInSessionAsync(() => (object)SearchOne(condition), cancellationToken);
        }

        public virtual Task<T> SearchOneAsync(Expression<Func<T, bool>> expression, CancellationToken cancellationToken = default)
        {
            return Session.ExecuteInSessionAsync(() => SearchOne(expression), cancellationToken);
        }

        public virtual Task ForEachAsync(Condition condition, Func<T, Task> func, CancellationToken cancellationToken = default)
        {
            return Session.ExecuteInSessionAsync(() =>
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

        public virtual Task ForEachAsync(Expression<Func<T, bool>> expression, Func<T, Task> func, CancellationToken cancellationToken = default)
        {
            return ForEachAsync(Condition.Exp(expression), func, cancellationToken);
        }

        public virtual Task<List<T>> SearchAsync(Condition condition = null, CancellationToken cancellationToken = default)
        {
            return Session.ExecuteInSessionAsync(() => Search(condition), cancellationToken);
        }

        Task<IList> IObjectViewDAOAsync.SearchAsync(Condition condition, CancellationToken cancellationToken = default)
        {
            return Session.ExecuteInSessionAsync(() => (IList)Search(condition), cancellationToken);
        }

        public virtual Task<List<T>> SearchAsync(Condition condition, Sorting[] orderBy, CancellationToken cancellationToken = default)
        {
            return Session.ExecuteInSessionAsync(() => Search(condition, orderBy), cancellationToken);
        }

        Task<IList> IObjectViewDAOAsync.SearchAsync(Condition condition, Sorting[] orderBy, CancellationToken cancellationToken = default)
        {
            return Session.ExecuteInSessionAsync(() => (IList)Search(condition, orderBy), cancellationToken);
        }

        public virtual Task<List<T>> SearchAsync(Expression<Func<T, bool>> expression, CancellationToken cancellationToken = default)
        {
            return Session.ExecuteInSessionAsync(() => Search(expression), cancellationToken);
        }

        public virtual Task<List<T>> SearchAsync(Expression<Func<T, bool>> expression, Sorting[] orderBy, CancellationToken cancellationToken = default)
        {
            return Session.ExecuteInSessionAsync(() => Search(expression, orderBy), cancellationToken);
        }

        public virtual Task<List<T>> SearchSectionAsync(Condition condition, SectionSet section, CancellationToken cancellationToken = default)
        {
            return Session.ExecuteInSessionAsync(() => SearchSection(condition, section), cancellationToken);
        }

        Task<IList> IObjectViewDAOAsync.SearchSectionAsync(Condition condition, SectionSet section, CancellationToken cancellationToken = default)
        {
            return Session.ExecuteInSessionAsync(() => (IList)SearchSection(condition, section), cancellationToken);
        }

        public virtual Task<List<T>> SearchSectionAsync(Expression<Func<T, bool>> expression, SectionSet section, CancellationToken cancellationToken = default)
        {
            return Session.ExecuteInSessionAsync(() => SearchSection(expression, section), cancellationToken);
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
            foreach (Column column in SelectColumns)
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
