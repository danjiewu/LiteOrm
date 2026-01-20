using System;
using System.Collections.Generic;
using System.Text;
using System.Data;
using System.Collections;
using LiteOrm.Common;
using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Data.Common;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;

namespace LiteOrm
{
    /// <summary>
    /// 实体类的查询数据访问对象实现
    /// </summary>
    /// <typeparam name="T">实体类型</typeparam>
    /// <remarks>
    /// ObjectViewDAO&lt;T&gt; 是 IObjectViewDAO&lt;T&gt; 接口的实现，提供针对特定实体类型的查询操作。
    /// 
    /// 主要功能包括：
    /// 1. 单对象查询 - 根据主键获取单个实体对象
    /// 2. 列表查询 - 根据条件获取实体对象列表
    /// 3. 分页查询 - 支持带分页参数的查询操作
    /// 4. 存在性检查 - 检查实体是否存在于数据库中
    /// 5. 关联查询 - 支持多表关联查询以获取关联的实体数据
    /// 6. 异步查询 - 提供基于 Task 的异步查询方法
    /// 7. 动态条件查询 - 支持使用 Lambda 表达式或 Expr 对象构建动态查询条件
    /// 
    /// 该类继承自 ObjectDAOBase 并实现了相应的查询接口，
    /// 处理复杂的SQL生成、参数处理和数据映射工作。
    /// 它支持与 TableJoinAttribute 定义的多表关联进行查询。
    /// </remarks>
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
            using var getObjectCommand = MakeGetObjectCommand();
            int i = 0;
            foreach (IDataParameter param in getObjectCommand.Parameters)
            {
                param.Value = ConvertToDbValue(keys[i], Table.Definition.Keys[i].DbType);
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
        /// <param name="expr">属性名与值的列表，若为null则表示没有条件</param>
        /// <returns>符合条件的对象个数</returns>
        public virtual int Count(Expr expr)
        {
            using (IDbCommand command = MakeConditionCommand("select count(*) \nfrom @FromTable@ \nwhere @Condition@", expr))
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
            if (o is null) return false;
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
            using var objectExistsCommand = MakeObjectExistsCommand();
            int i = 0;
            foreach (IDataParameter param in objectExistsCommand.Parameters)
            {
                param.Value = ConvertToDbValue(keys[i], Table.Definition.Keys[i].DbType);
                i++;
            }
            return Convert.ToInt32(objectExistsCommand.ExecuteScalar()) > 0;
        }

        /// <summary>
        /// 判断符合条件的对象是否存在
        /// </summary>
        /// <param name="expr">属性名与值的列表，若为null则表示没有条件</param>
        /// <returns>是否存在</returns>
        public virtual bool Exists(Expr expr)
        {
            using (IDbCommand command = MakeConditionCommand("select 1 \nfrom @FromTable@ \nwhere @Condition@", expr))
            {
                return command.ExecuteScalar() is not null;
            }
        }

        /// <summary>
        /// 对符合条件的每个对象执行指定操作
        /// </summary>
        /// <param name="expr">查询条件</param>
        /// <param name="func">要对每个对象执行的操作</param>
        public void ForEach(Expr expr, Action<T> func)
        {
            using (IDbCommand command = MakeConditionCommand("select @AllFields@ \nfrom @FromTable@" + (expr is null ? null : " \nwhere @Condition@"), expr))
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
        /// <param name="expr">属性名与值的列表，若为null则表示没有条件</param>
        /// <returns>符合条件的对象列表</returns>
        public virtual List<T> Search(Expr expr)
        {
            using (IDbCommand command = MakeConditionCommand("select @AllFields@ \nfrom @FromTable@" + (expr is null ? null : " \nwhere @Condition@"), expr))
            {
                return GetAll(command);
            }
        }

        /// <summary>
        /// 根据条件查询，多个条件以逻辑与连接
        /// </summary>
        /// <param name="expr">属性名与值的列表，若为null则表示没有条件</param>
        /// <param name="orderBy">排列顺序，若为null则表示不指定顺序</param>
        /// <returns>符合条件的对象列表</returns>
        public virtual List<T> Search(Expr expr, params Sorting[] orderBy)
        {
            if (orderBy is null || orderBy.Length == 0) return Search(expr);
            else
                using (IDbCommand command = MakeConditionCommand("select @AllFields@ \nfrom @FromTable@" + (expr is null ? null : " \nwhere @Condition@") + " order by " + GetOrderBySql(orderBy), expr))
                {
                    return GetAll(command);
                }
        }

        /// <summary>
        /// 获取单个符合条件的对象
        /// </summary>
        /// <param name="expr">属性名与值的列表，若为null则表示没有条件</param>
        /// <returns>第一个符合条件的对象，若不存在则返回null</returns>
        public virtual T SearchOne(Expr expr)
        {
            using (IDbCommand command = MakeConditionCommand("select @AllFields@ \nfrom @FromTable@ \nwhere @Condition@", expr))
            {
                return GetOne(command);
            }
        }


        /// <summary>
        /// 分页查询
        /// </summary>
        /// <param name="expr">查询条件</param>
        /// <param name="section">分页设定</param>
        /// <returns></returns>
        public virtual List<T> SearchSection(Expr expr, PageSection section)
        {
            string sql = SqlBuilder.GetSelectSectionSql(AllFieldsSql, From, ParamCondition, GetOrderBySql(section.Orders), section.StartIndex, section.SectionSize);
            using (IDbCommand command = MakeConditionCommand(sql, expr))
            {
                return GetAll(command);
            }
        }

        #endregion

        #region IObjectViewDAO Members

        /// <summary>
        /// 根据主键获取对象（接口实现）
        /// </summary>
        /// <param name="keys">主键，多个主键按照主键名顺序排列</param>
        /// <returns>对象，若不存在则返回null</returns>
        object IObjectViewDAO.GetObject(params object[] keys)
        {
            return GetObject(keys);
        }

        /// <summary>
        /// 获取单个符合条件的对象（接口实现）
        /// </summary>
        /// <param name="expr">属性名与值的列表，若为null则表示没有条件</param>
        /// <returns>第一个符合条件的对象，若不存在则返回null</returns>
        object IObjectViewDAO.SearchOne(Expr expr)
        {
            return SearchOne(expr);
        }

        /// <summary>
        /// 根据条件查询，多个条件以逻辑与连接（接口实现）
        /// </summary>
        /// <param name="expr">属性名与值的列表，若为null则表示没有条件</param>
        /// <returns>符合条件的对象列表</returns>
        IList IObjectViewDAO.Search(Expr expr)
        {
            return Search(expr);
        }

        /// <summary>
        /// 根据条件查询，多个条件以逻辑与连接（接口实现）
        /// </summary>
        /// <param name="expr">属性名与值的列表，若为null则表示没有条件</param>
        /// <param name="orderBy">排列顺序，若为null则表示不指定顺序</param>
        /// <returns>符合条件的对象列表</returns>
        IList IObjectViewDAO.Search(Expr expr, params Sorting[] orderBy)
        {
            return Search(expr, orderBy);
        }

        /// <summary>
        /// 分页查询（接口实现）
        /// </summary>
        /// <param name="expr">查询条件</param>
        /// <param name="section">分页设定</param>
        /// <returns>分页查询结果</returns>
        IList IObjectViewDAO.SearchSection(Expr expr, PageSection section)
        {
            return SearchSection(expr, section);
        }

        #endregion

        #region IObjectViewDAOAsync implementations

        /// <summary>
        /// 根据主键异步获取对象
        /// </summary>
        /// <param name="keys">主键，多个主键按照主键名顺序排列</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>表示异步操作的任务，任务结果包含对象，若不存在则返回null</returns>
        public async virtual Task<T> GetObjectAsync(object[] keys, CancellationToken cancellationToken = default)
        {
            return await Task.Run(() => GetObject(keys), cancellationToken);
        }

        /// <summary>
        /// 根据主键异步获取对象（接口实现）
        /// </summary>
        /// <param name="keys">主键，多个主键按照主键名顺序排列</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>表示异步操作的任务，任务结果包含对象，若不存在则返回null</returns>
        async Task<object> IObjectViewDAOAsync.GetObjectAsync(object[] keys, CancellationToken cancellationToken)
        {
            return await Task.Run(() => (object)GetObject(keys), cancellationToken);
        }

        /// <summary>
        /// 异步获取符合条件的对象个数
        /// </summary>
        /// <param name="expr">属性名与值的列表，若为null则表示没有条件</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>表示异步操作的任务，任务结果包含符合条件的对象个数</returns>
        public async virtual Task<int> CountAsync(Expr expr, CancellationToken cancellationToken = default)
        {
            return await Task.Run(() => Count(expr), cancellationToken);
        }

        /// <summary>
        /// 异步判断对象是否存在
        /// </summary>
        /// <param name="o">对象</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>表示异步操作的任务，任务结果表示对象是否存在</returns>
        public async virtual Task<bool> ExistsAsync(object o, CancellationToken cancellationToken = default)
        {
            return await Task.Run(() => Exists(o), cancellationToken);
        }

        /// <summary>
        /// 异步判断主键对应的对象是否存在
        /// </summary>
        /// <param name="keys">主键，多个主键按照名称顺序排列</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>表示异步操作的任务，任务结果表示对象是否存在</returns>
        public async virtual Task<bool> ExistsKeyAsync(object[] keys, CancellationToken cancellationToken = default)
        {
            return await Task.Run(() => ExistsKey(keys), cancellationToken);
        }

        /// <summary>
        /// 异步判断符合条件的对象是否存在
        /// </summary>
        /// <param name="expr">属性名与值的列表，若为null则表示没有条件</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>表示异步操作的任务，任务结果表示对象是否存在</returns>
        public async virtual Task<bool> ExistsAsync(Expr expr, CancellationToken cancellationToken = default)
        {
            return await Task.Run(() => Exists(expr), cancellationToken);
        }

        /// <summary>
        /// 异步判断符合条件的对象是否存在（使用表达式）
        /// </summary>
        /// <param name="expression">Lambda表达式条件</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>表示异步操作的任务，任务结果表示对象是否存在</returns>
        public async virtual Task<bool> ExistsAsync(Expression<Func<T, bool>> expression, CancellationToken cancellationToken = default)
        {
            return await Task.Run(() => Exists(expression), cancellationToken);
        }

        /// <summary>
        /// 异步获取单个符合条件的对象
        /// </summary>
        /// <param name="expr">属性名与值的列表，若为null则表示没有条件</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>表示异步操作的任务，任务结果包含第一个符合条件的对象，若不存在则返回null</returns>
        public async virtual Task<T> SearchOneAsync(Expr expr, CancellationToken cancellationToken = default)
        {
            return await Task.Run(() => SearchOne(expr), cancellationToken);
        }

        /// <summary>
        /// 异步获取单个符合条件的对象（接口实现）
        /// </summary>
        /// <param name="expr">属性名与值的列表，若为null则表示没有条件</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>表示异步操作的任务，任务结果包含第一个符合条件的对象，若不存在则返回null</returns>
        async Task<object> IObjectViewDAOAsync.SearchOneAsync(Expr expr, CancellationToken cancellationToken)
        {
            return await Task.Run(() => (object)SearchOne(expr), cancellationToken);
        }

        /// <summary>
        /// 异步对符合条件的每个对象执行指定操作
        /// </summary>
        /// <param name="expr">查询条件</param>
        /// <param name="func">要对每个对象执行的异步操作</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>表示异步操作的任务</returns>
        public async virtual Task ForEachAsync(Expr expr, Func<T, Task> func, CancellationToken cancellationToken = default)
        {
            await Task.Run(() =>
            {
                var list = Search(expr);
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

        /// <summary>
        /// 异步根据条件查询，多个条件以逻辑与连接
        /// </summary>
        /// <param name="expr">属性名与值的列表，若为null则表示没有条件</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>表示异步操作的任务，任务结果包含符合条件的对象列表</returns>
        public async virtual Task<List<T>> SearchAsync(Expr expr = null, CancellationToken cancellationToken = default)
        {
            return await Task.Run(() => Search(expr), cancellationToken);
        }

        /// <summary>
        /// 异步根据条件查询，多个条件以逻辑与连接（接口实现）
        /// </summary>
        /// <param name="expr">属性名与值的列表，若为null则表示没有条件</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>表示异步操作的任务，任务结果包含符合条件的对象列表</returns>
        async Task<IList> IObjectViewDAOAsync.SearchAsync(Expr expr, CancellationToken cancellationToken)
        {
            return await Task.Run(() => (IList)Search(expr), cancellationToken);
        }

        /// <summary>
        /// 异步根据条件查询，多个条件以逻辑与连接
        /// </summary>
        /// <param name="expr">属性名与值的列表，若为null则表示没有条件</param>
        /// <param name="orderBy">排列顺序，若为null则表示不指定顺序</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>表示异步操作的任务，任务结果包含符合条件的对象列表</returns>
        public async virtual Task<List<T>> SearchAsync(Expr expr, Sorting[] orderBy, CancellationToken cancellationToken = default)
        {
            return await Task.Run(() => Search(expr, orderBy), cancellationToken);
        }

        /// <summary>
        /// 异步根据条件查询，多个条件以逻辑与连接（接口实现）
        /// </summary>
        /// <param name="expr">属性名与值的列表，若为null则表示没有条件</param>
        /// <param name="orderBy">排列顺序，若为null则表示不指定顺序</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>表示异步操作的任务，任务结果包含符合条件的对象列表</returns>
        async Task<IList> IObjectViewDAOAsync.SearchAsync(Expr expr, Sorting[] orderBy, CancellationToken cancellationToken)
        {
            return await Task.Run(() => (IList)Search(expr, orderBy), cancellationToken);
        }

        /// <summary>
        /// 异步分页查询
        /// </summary>
        /// <param name="expr">查询条件</param>
        /// <param name="section">分页设定</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>表示异步操作的任务，任务结果包含分页查询结果</returns>
        public async virtual Task<List<T>> SearchSectionAsync(Expr expr, PageSection section, CancellationToken cancellationToken = default)
        {
            return await Task.Run(() => SearchSection(expr, section), cancellationToken);
        }

        /// <summary>
        /// 异步分页查询（接口实现）
        /// </summary>
        /// <param name="expr">查询条件</param>
        /// <param name="section">分页设定</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>表示异步操作的任务，任务结果包含分页查询结果</returns>
        async Task<IList> IObjectViewDAOAsync.SearchSectionAsync(Expr expr, PageSection section, CancellationToken cancellationToken)
        {
            return await Task.Run(() => (IList)SearchSection(expr, section), cancellationToken);
        }

        #endregion

        #region 常用方法

        /// <summary>
        /// 替换 SQL 中的标记为实际 SQL。
        /// </summary>
        /// <param name="sqlWithParam">包含标记的 SQL 语句，标记可以为 ParamAllFields，ParamFromTable。</param>
        /// <param name="context">SQL 构建上下文。</param>
        /// <returns>替换后的 SQL 语句。</returns>
        protected override string ReplaceParam(string sqlWithParam, SqlBuildContext context = null)
        {
            return base.ReplaceParam(sqlWithParam, context).Replace(ParamAllFields, AllFieldsSql);
        }

        /// <summary>
        /// 读取所有记录并转化为对象集合，查询 AllFieldsSql 时可用
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
        /// 读取所有记录并转化为对象集合，查询 AllFieldsSql 时可用
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
                column.SetValue(t, record.IsDBNull(i) ? null : ConvertFromDbValue(record[i], column.PropertyType));
                i++;
            }
            return t;
        }

        /// <summary>
        /// 执行 IDbCommand，读取所有记录并转化为对象的集合，查询 AllFieldsSql 时可用
        /// </summary>
        /// <param name="command">待执行的 IDbCommand</param>
        /// <returns></returns>
        protected List<T> GetAll(IDbCommand command)
        {
            using (IDataReader reader = command.ExecuteReader())
            {
                return ReadAll(reader);
            }
        }

        /// <summary>
        /// 执行 IDbCommand，读取所有记录并转化为对象的集合，查询 AllFieldsSql 时可用
        /// </summary>
        /// <param name="command">待执行的 IDbCommand</param>
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
        /// 执行 IDbCommand，读取一条记录并转化为单个对象，查询 AllFieldsSql 时可用
        /// </summary>
        /// <param name="command">待执行的 IDbCommand</param>
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
